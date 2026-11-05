using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Portunus.Core;
using Portunus.Testing;

using Xunit;

using JanusCore = Janus.Core;

namespace Portunus.Janus.Tests;

/// <summary>Janus's secret source over a fixed client (<c>04</c> PRT-LIB-006, <c>03</c> PRT-ROT-002); every value is made at run time.</summary>
[Trait("Category", "unit")]
public sealed class JanusSecretSourceTests
{
    private readonly FixedSecretClient _client = new();

    private readonly CapturedLog _log = new();

    [Fact]
    public async Task PRT_LIB_006_AC1_KeyEncryptionKeysAnswerEveryVersionAndTheCurrent()
    {
        byte[] first = RandomNumberGenerator.GetBytes(32);
        byte[] second = RandomNumberGenerator.GetBytes(16);
        _client.Set(new SecretAddress("Janus", "keyEncryptionKeys"), Versions(1, (1, Base64(first)), (2, Base64(second))));

        JanusCore.KeyEncryptionKeys keys = Value(await Source().ReadKeyEncryptionKeysAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, keys.CurrentVersion);
        Assert.True(keys.Versions[1].Span.SequenceEqual(first));
        Assert.True(keys.Versions[2].Span.SequenceEqual(second));
    }

    [Fact]
    public async Task PRT_ROT_002_AC1_KeySetOfTwoVersionsCurrentTwoReachesJanusAsCurrentTwoWithBoth()
    {
        _client.Set(new SecretAddress("Janus", "keyEncryptionKeys"), Versions(2, (1, Base64(RandomNumberGenerator.GetBytes(32))), (2, Base64(RandomNumberGenerator.GetBytes(32)))));

        JanusCore.KeyEncryptionKeys keys = Value(await Source().ReadKeyEncryptionKeysAsync(TestContext.Current.CancellationToken));

        Assert.Equal(2, keys.CurrentVersion);
        Assert.Equal([1, 2], keys.Versions.Keys.Order());
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_FingerprintKeysAnswerEveryVersionAndTheCurrent()
    {
        byte[] first = RandomNumberGenerator.GetBytes(32);
        byte[] second = RandomNumberGenerator.GetBytes(48);
        _client.Set(new SecretAddress("Janus", "fingerprintKeys"), Versions(2, (1, Base64(first)), (2, Base64(second))));

        JanusCore.FingerprintKeys keys = Value(await Source().ReadFingerprintKeysAsync(TestContext.Current.CancellationToken));

        Assert.Equal(2, keys.CurrentVersion);
        Assert.True(keys.Versions[1].Span.SequenceEqual(first));
        Assert.True(keys.Versions[2].Span.SequenceEqual(second));
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_MaintenanceCredentialAnswersItsBytes()
    {
        byte[] value = Text(32);
        _client.Set(new SecretAddress("Janus", "maintenanceCredential"), Single(value));

        ReadOnlyMemory<byte> read = Value(await Source().ReadMaintenanceCredentialAsync(TestContext.Current.CancellationToken));

        Assert.True(read.Span.SequenceEqual(value));
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_MailServerSecretAnswersItsBytes()
    {
        byte[] value = Text(32);
        _client.Set(new SecretAddress("Janus", "mailServerSecret"), Single(value));

        ReadOnlyMemory<byte> read = Value(await Source().ReadMailServerSecretAsync(TestContext.Current.CancellationToken));

        Assert.True(read.Span.SequenceEqual(value));
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_ProviderCredentialOfPlainTextAnswersASecret()
    {
        byte[] value = Text(40);
        _client.Set(new SecretAddress("Janus", "socialProvider.google"), Single(value));

        JanusCore.ProviderCredential credential = Value(await Source().ReadProviderCredentialAsync("google", TestContext.Current.CancellationToken));

        Assert.False(credential.IsSigned);
        Assert.True(credential.Material.Span.SequenceEqual(value));
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_ProviderCredentialOfAnObjectAnswersASignedOne()
    {
        byte[] key = RandomNumberGenerator.GetBytes(64);
        _client.Set(new SecretAddress("Janus", "socialProvider.apple"), Single(Encoding.UTF8.GetBytes($$"""{"issuer":"TEAM","keyId":"KEY1","key":"{{Convert.ToBase64String(key)}}"}""")));

        JanusCore.ProviderCredential credential = Value(await Source().ReadProviderCredentialAsync("apple", TestContext.Current.CancellationToken));

        Assert.True(credential.IsSigned);
        Assert.Equal("TEAM", credential.Issuer);
        Assert.Equal("KEY1", credential.KeyId);
        Assert.True(credential.Material.Span.SequenceEqual(key));
    }

    [Theory]
    [InlineData("""{"issuer":"TEAM","keyId":"KEY1"}""")]
    [InlineData("""{"issuer":"TEAM","keyId":"KEY1","key":"AAEC","other":"x"}""")]
    [InlineData("""{"issuer":"TEAM","keyId":"KEY1","key":"not base64"}""")]
    [InlineData("""{"issuer":"TEAM","keyId":1,"key":"AAEC"}""")]
    [InlineData("""{"issuer":"TEAM","keyId":"KEY1","key":"AAEC","key":"AAEC"}""")]
    [InlineData("""{"issuer":"TEAM",""")]
    public async Task PRT_LIB_006_AC1_ProviderObjectOfAnotherFormIsRefusedNamingTheSecret(string value)
    {
        _client.Set(new SecretAddress("Janus", "socialProvider.apple"), Single(Encoding.UTF8.GetBytes(value)));

        AssertUnavailable(await Source().ReadProviderCredentialAsync("apple", TestContext.Current.CancellationToken), "socialProvider.apple");
        Assert.Contains((LogLevel.Warning, "secrets.secret.forminvalid at Janus/socialProvider.apple"), _log.Entries);
    }

    [Fact]
    public async Task PRT_LIB_006_AC1_EachFailureIsRefusedNamingTheSecret()
    {
        _client.Deny(new SecretAddress("Janus", "maintenanceCredential"));
        JanusSecretSource source = Source();

        AssertUnavailable(await source.ReadKeyEncryptionKeysAsync(TestContext.Current.CancellationToken), "keyEncryptionKeys");
        AssertUnavailable(await source.ReadFingerprintKeysAsync(TestContext.Current.CancellationToken), "fingerprintKeys");
        AssertUnavailable(await source.ReadMaintenanceCredentialAsync(TestContext.Current.CancellationToken), "maintenanceCredential");
        AssertUnavailable(await source.ReadMailServerSecretAsync(TestContext.Current.CancellationToken), "mailServerSecret");
        AssertUnavailable(await source.ReadProviderCredentialAsync("google", TestContext.Current.CancellationToken), "socialProvider.google");
        AssertUnavailable(await source.ReadProviderCredentialAsync("not a name", TestContext.Current.CancellationToken), "socialProvider.not a name");
    }

    [Fact]
    public async Task PRT_LIB_006_AC2_KeySetWhoseVersionDecodesToTwentyBytesIsRefusedAndLogged()
    {
        _client.Set(new SecretAddress("Janus", "keyEncryptionKeys"), Versions(2, (1, Base64(RandomNumberGenerator.GetBytes(32))), (2, Base64(RandomNumberGenerator.GetBytes(20)))));

        AssertUnavailable(await Source().ReadKeyEncryptionKeysAsync(TestContext.Current.CancellationToken), "keyEncryptionKeys");
        Assert.Contains((LogLevel.Warning, "secrets.secret.forminvalid at Janus/keyEncryptionKeys"), _log.Entries);
    }

    [Fact]
    public async Task PRT_LIB_006_AC3_ProvidersItemIsReadWhereItsKeyIsSet()
    {
        byte[] value = Text(24);
        _client.Set(new SecretAddress("Example", "socialProvider.example"), Single(value));

        JanusCore.ProviderCredential credential = Value(await Source(("Portunus:Janus:Items:socialProvider.example", "Example")).ReadProviderCredentialAsync("example", TestContext.Current.CancellationToken));

        Assert.True(credential.Material.Span.SequenceEqual(value));
        Assert.Equal([new SecretAddress("Example", "socialProvider.example")], _client.Reads);
    }

    [Fact]
    public async Task ReadKeyEncryptionKeysAsync_Read_ZeroesTheTextItWasHanded()
    {
        byte[] text = Base64(RandomNumberGenerator.GetBytes(32));
        _client.Set(new SecretAddress("Janus", "keyEncryptionKeys"), Single(text));

        await Source().ReadKeyEncryptionKeysAsync(TestContext.Current.CancellationToken);

        Assert.All(text, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task PRT_OPS_001_AC1_JanusItemHasItsDefaultWhenAbsent()
    {
        _client.Set(new SecretAddress("Janus", "mailServerSecret"), Single(Text(8)));
        _client.Set(new SecretAddress("Identity", "mailServerSecret"), Single(Text(8)));

        await Source().ReadMailServerSecretAsync(TestContext.Current.CancellationToken);
        await Source(("Portunus:Janus:Item", "Identity")).ReadMailServerSecretAsync(TestContext.Current.CancellationToken);

        Assert.Equal([new SecretAddress("Janus", "mailServerSecret"), new SecretAddress("Identity", "mailServerSecret")], _client.Reads);
    }

    [Theory]
    [InlineData("Portunus:Janus:Item", "9Identity", "Portunus:Janus:Item")]
    [InlineData("Portunus:Janus:Items:socialProvider.example", "Example ", "Portunus:Janus:Items:socialProvider.example")]
    public void PRT_OPS_001_AC1_JanusKeyOutsideItsRuleStopsTheStartNamingIt(string key, string value, string named)
    {
        SecretsStartupException refused = Assert.Throws<SecretsStartupException>(() => Source((key, value)));

        Assert.Equal("secrets.startup.configurationinvalid", refused.Code);
        Assert.Equal(named, refused.Details["key"]);
    }

    private static TValue Value<TValue>(JanusCore.Result<TValue> result)
    {
        return result.Match(value => value, error => throw new Xunit.Sdk.XunitException($"Janus's source answered {error.Code}."));
    }

    private static void AssertUnavailable<TValue>(JanusCore.Result<TValue> result, string name)
    {
        JanusCore.Error error = result.Match(_ => throw new Xunit.Sdk.XunitException("Janus's source answered a value."), failure => failure);
        Assert.Equal("model.startup.secretunavailable", error.Code.ToString());
        Assert.Equal(name, error.Details["key"].GetString());
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

    private JanusSecretSource Source(params (string Key, string Value)[] keys)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(keys.Select(key => new KeyValuePair<string, string?>(key.Key, key.Value))).Build();
        return new JanusSecretSource(JanusConfiguration.Read(configuration), _client, _log);
    }
}