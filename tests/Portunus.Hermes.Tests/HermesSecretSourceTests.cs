using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Portunus.Core;
using Portunus.Testing;

using Xunit;

namespace Portunus.Hermes.Tests;

/// <summary>Hermes's secret source over a fixed client (<c>04</c> PRT-LIB-006, <c>03</c> PRT-ROT-002, P-037); every value is made at run time.</summary>
[Trait("Category", "unit")]
public sealed class HermesSecretSourceTests
{
    private readonly FixedSecretClient _client = new();

    private readonly CapturedLog _log = new();

    [Theory]
    [InlineData("connection")]
    [InlineData("listenConnection")]
    [InlineData("emailSubmission")]
    [InlineData("smsCequens")]
    public async Task PRT_LIB_006_AC1_TextNameAnswersItsValueAsVersionOne(string name)
    {
        byte[] value = Text(24);
        _client.Set(new SecretAddress("Hermes", name), Single(value));

        SecretVersions? read = await Source().ReadAsync(name, TestContext.Current.CancellationToken);

        SecretVersion version = Assert.Single(read!.Versions);
        Assert.Equal(1, version.Version);
        Assert.True(version.Value.Span.SequenceEqual(value));
    }

    [Theory]
    [InlineData("contentKeys")]
    [InlineData("hashKeys")]
    [InlineData("tokenKeys")]
    [InlineData("smsReportKey")]
    [InlineData("pushVapidKeys")]
    public async Task PRT_LIB_006_AC1_Base64NameAnswersEachVersionDecodedInAscendingOrder(string name)
    {
        byte[] first = RandomNumberGenerator.GetBytes(32);
        byte[] second = RandomNumberGenerator.GetBytes(32);
        _client.Set(new SecretAddress("Hermes", name), Versions(2, (2, Base64(second)), (1, Base64(first))));

        SecretVersions? read = await Source().ReadAsync(name, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], read!.Versions.Select(version => version.Version));
        Assert.True(read.Versions[0].Value.Span.SequenceEqual(first));
        Assert.True(read.Versions[1].Value.Span.SequenceEqual(second));
    }

    [Fact]
    public async Task PRT_ROT_002_AC1_KeySetOfTwoVersionsCurrentTwoReachesHermesAsOneAndTwoInOrder()
    {
        byte[] first = RandomNumberGenerator.GetBytes(32);
        byte[] second = RandomNumberGenerator.GetBytes(32);
        _client.Set(new SecretAddress("Hermes", "contentKeys"), Versions(2, (1, Base64(first)), (2, Base64(second))));

        SecretVersions? read = await Source().ReadAsync("contentKeys", TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], read!.Versions.Select(version => version.Version));
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_DeniedAddressAnswersNoneAndLogsTheNameFirst()
    {
        _client.Deny(new SecretAddress("Hermes", "connection"));

        SecretVersions? read = await Source().ReadAsync("connection", TestContext.Current.CancellationToken);

        Assert.Null(read);
        Assert.Contains((LogLevel.Warning, "Hermes's secret connection at Hermes/connection: secrets.secret.denied"), _log.Entries);
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_SecretTheAgentDoesNotHoldAnswersNone()
    {
        Assert.Null(await Source().ReadAsync("connection", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("AAEC AwQF")]
    [InlineData("AAECAwQ")]
    [InlineData("AAECAw-_")]
    public async Task ReadAsync_Base64NameWhoseValueIsNotOfItsForm_AnswersNoneAndLogsTheForm(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _client.Set(new SecretAddress("Hermes", "hashKeys"), Single(Encoding.ASCII.GetBytes(text)));

        SecretVersions? read = await Source().ReadAsync("hashKeys", TestContext.Current.CancellationToken);

        Assert.Null(read);
        Assert.Contains((LogLevel.Warning, "secrets.secret.forminvalid at Hermes/hashKeys"), _log.Entries);
    }

    [Fact]
    public async Task ReadAsync_NameTheTableDoesNotList_AnswersItsTextAndLogsItOnce()
    {
        byte[] value = Text(16);
        _client.Set(new SecretAddress("Hermes", "smsOther"), Single(value));
        HermesSecretSource source = Source();

        SecretVersions? read = await source.ReadAsync("smsOther", TestContext.Current.CancellationToken);
        _client.Set(new SecretAddress("Hermes", "smsOther"), Single(Text(16)));
        await source.ReadAsync("smsOther", TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(read!.Versions).Value.Span.SequenceEqual(value));
        Assert.Single(_log.Entries, entry => entry == (LogLevel.Information, "Hermes's secret smsOther at Hermes/smsOther, which the table does not list, is read as UTF-8 text"));
    }

    [Fact]
    public async Task ReadAsync_NameOutsideTheSecretNamesRule_AnswersNone()
    {
        Assert.Null(await Source().ReadAsync("not a name", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_Base64Name_ZeroesTheTextItWasHanded()
    {
        byte[] text = Base64(RandomNumberGenerator.GetBytes(32));
        _client.Set(new SecretAddress("Hermes", "tokenKeys"), Single(text));

        await Source().ReadAsync("tokenKeys", TestContext.Current.CancellationToken);

        Assert.All(text, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_ItemOfTheNameIsReadWhereItsKeyIsSet()
    {
        byte[] value = Text(24);
        _client.Set(new SecretAddress("Cequens", "smsCequens"), Single(value));

        SecretVersions? read = await Source(("Portunus:Hermes:Items:smsCequens", "Cequens")).ReadAsync("smsCequens", TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(read!.Versions).Value.Span.SequenceEqual(value));
    }

    [Fact]
    public async Task PRT_OPS_001_AC1_HermesItemHasItsDefaultWhenAbsent()
    {
        _client.Set(new SecretAddress("Hermes", "connection"), Single(Text(8)));
        _client.Set(new SecretAddress("Messaging", "connection"), Single(Text(8)));

        await Source().ReadAsync("connection", TestContext.Current.CancellationToken);
        await Source(("Portunus:Hermes:Item", "Messaging")).ReadAsync("connection", TestContext.Current.CancellationToken);

        Assert.Equal([new SecretAddress("Hermes", "connection"), new SecretAddress("Messaging", "connection")], _client.Reads);
    }

    [Theory]
    [InlineData("Portunus:Hermes:Item", "9Messaging", "Portunus:Hermes:Item")]
    [InlineData("Portunus:Hermes:Item", "", "Portunus:Hermes:Item")]
    [InlineData("Portunus:Hermes:Items:smsCequens", "Cequens ", "Portunus:Hermes:Items:smsCequens")]
    [InlineData("Portunus:Hermes:Items:sms..Cequens", "Cequens", "Portunus:Hermes:Items:sms..Cequens")]
    public void PRT_OPS_001_AC1_HermesKeyOutsideItsRuleStopsTheStartNamingIt(string key, string value, string named)
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

    private static byte[] Base64(byte[] value)
    {
        return Encoding.ASCII.GetBytes(Convert.ToBase64String(value));
    }

    private static Secret Single(byte[] value)
    {
        return Versions(1, (1, value));
    }

    private static Secret Versions(int current, params (int Number, byte[] Value)[] versions)
    {
        return new Secret { Current = current, Versions = versions.ToDictionary(version => version.Number, version => (ReadOnlyMemory<byte>)version.Value) };
    }

    private HermesSecretSource Source(params (string Key, string Value)[] keys)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(keys.Select(key => new KeyValuePair<string, string?>(key.Key, key.Value))).Build();
        return new HermesSecretSource(HermesConfiguration.Read(configuration), _client, _log);
    }
}