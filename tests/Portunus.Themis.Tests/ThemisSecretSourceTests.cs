using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Portunus.Core;
using Portunus.Testing;

using Themis.Core;

using Xunit;

namespace Portunus.Themis.Tests;

/// <summary>Themis's secret source over a fixed client (<c>04</c> PRT-LIB-006, P-037); every value is made at run time.</summary>
[Trait("Category", "unit")]
public sealed class ThemisSecretSourceTests
{
    private readonly FixedSecretClient _client = new();

    private readonly CapturedLog _log = new();

    [Fact]
    public async Task PRT_LIB_006_AC1_ConnectionAnswersItsValueAsVersionOne()
    {
        byte[] value = Text(24);
        _client.Set(new SecretAddress("Themis", "connection"), Single(value));

        SecretVersions? read = await Source().ReadAsync("connection", TestContext.Current.CancellationToken);

        SecretVersion version = Assert.Single(read!.Versions);
        Assert.Equal(1, version.Version);
        Assert.True(version.Value.Span.SequenceEqual(value));
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_ConnectionOfTwoVersionsAnswersBothInAscendingOrder()
    {
        byte[] first = Text(16);
        byte[] second = Text(16);
        _client.Set(new SecretAddress("Themis", "connection"), Versions(2, (2, second), (1, first)));

        SecretVersions? read = await Source().ReadAsync("connection", TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], read!.Versions.Select(version => version.Version));
        Assert.True(read.Versions[0].Value.Span.SequenceEqual(first));
        Assert.True(read.Versions[1].Value.Span.SequenceEqual(second));
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_DeniedAddressAnswersNoneAndLogsTheNameFirst()
    {
        _client.Deny(new SecretAddress("Themis", "connection"));

        SecretVersions? read = await Source().ReadAsync("connection", TestContext.Current.CancellationToken);

        Assert.Null(read);
        Assert.Contains((LogLevel.Warning, "Themis's secret connection at Themis/connection: secrets.secret.denied"), _log.Entries);
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_SecretTheAgentDoesNotHoldAnswersNone()
    {
        Assert.Null(await Source().ReadAsync("connection", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_ItemOfTheNameIsReadWhereItsKeyIsSet()
    {
        byte[] value = Text(24);
        _client.Set(new SecretAddress("Approvals", "connection"), Single(value));

        SecretVersions? read = await Source(("Portunus:Themis:Items:connection", "Approvals")).ReadAsync("connection", TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(read!.Versions).Value.Span.SequenceEqual(value));
    }

    [Fact]
    public async Task ReadAsync_NameTheTableDoesNotList_AnswersItsTextAndLogsItOnce()
    {
        byte[] value = Text(16);
        _client.Set(new SecretAddress("Themis", "alertWebhook"), Single(value));
        ThemisSecretSource source = Source();

        SecretVersions? read = await source.ReadAsync("alertWebhook", TestContext.Current.CancellationToken);
        _client.Set(new SecretAddress("Themis", "alertWebhook"), Single(Text(16)));
        await source.ReadAsync("alertWebhook", TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(read!.Versions).Value.Span.SequenceEqual(value));
        Assert.Single(_log.Entries, entry => entry == (LogLevel.Information, "Themis's secret alertWebhook at Themis/alertWebhook, which the table does not list, is read as UTF-8 text"));
    }

    [Fact]
    public async Task ReadAsync_NameOutsideTheSecretNamesRule_AnswersNone()
    {
        Assert.Null(await Source().ReadAsync("not a name", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PRT_OPS_001_AC1_ThemisItemHasItsDefaultWhenAbsent()
    {
        _client.Set(new SecretAddress("Themis", "connection"), Single(Text(8)));
        _client.Set(new SecretAddress("Approvals", "connection"), Single(Text(8)));

        await Source().ReadAsync("connection", TestContext.Current.CancellationToken);
        await Source(("Portunus:Themis:Item", "Approvals")).ReadAsync("connection", TestContext.Current.CancellationToken);

        Assert.Equal([new SecretAddress("Themis", "connection"), new SecretAddress("Approvals", "connection")], _client.Reads);
    }

    [Theory]
    [InlineData("Portunus:Themis:Item", "9Approvals", "Portunus:Themis:Item")]
    [InlineData("Portunus:Themis:Item", "", "Portunus:Themis:Item")]
    [InlineData("Portunus:Themis:Items:connection", "Approvals ", "Portunus:Themis:Items:connection")]
    [InlineData("Portunus:Themis:Items:con..nection", "Approvals", "Portunus:Themis:Items:con..nection")]
    public void PRT_OPS_001_AC1_ThemisKeyOutsideItsRuleStopsTheStartNamingIt(string key, string value, string named)
    {
        SecretsStartupException refused = Assert.Throws<SecretsStartupException>(() => Source((key, value)));

        Assert.Equal("secrets.startup.configurationinvalid", refused.Code);
        Assert.Equal(named, refused.Details["key"]);
    }

    private static byte[] Text(int length)
    {
        byte[] text = new byte[length];
        for (int index = 0; index < text.Length; index++)
        {
            text[index] = (byte)RandomNumberGenerator.GetInt32('a', 'z' + 1);
        }

        return text;
    }

    private static Secret Single(byte[] value)
    {
        return Versions(1, (1, value));
    }

    private static Secret Versions(int current, params (int Number, byte[] Value)[] versions)
    {
        return new Secret { Current = current, Versions = versions.ToDictionary(version => version.Number, version => (ReadOnlyMemory<byte>)version.Value) };
    }

    private ThemisSecretSource Source(params (string Key, string Value)[] keys)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(keys.Select(key => new KeyValuePair<string, string?>(key.Key, key.Value))).Build();
        return new ThemisSecretSource(ThemisConfiguration.Read(configuration), _client, _log);
    }
}