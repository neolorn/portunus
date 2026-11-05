using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Portunus.Core;
using Portunus.Testing;

using Xunit;

namespace Harbour.Tests;

/// <summary>
/// The harbour started through <see cref="HarbourFactory"/>, a <c>WebApplicationFactory&lt;Program&gt;</c>, with
/// <c>Portunus.Testing</c>'s client in place of the agent's (<c>06</c> PRT-ENG-001, P-042).
/// </summary>
public sealed class HarbourTests
{
    private static readonly SecretAddress ManifestKey = new("Harbour", "manifestKey");

    private static readonly SecretAddress Connection = new("Hermes", "connection");

    [Fact]
    public async Task Start_SecretsSet_ReadsBothAndLogsHowManyVersionsEachHoldsAndNoValue()
    {
        byte[] first = Encoding.UTF8.GetBytes("manifest key one");
        byte[] second = Encoding.UTF8.GetBytes("manifest key two");
        byte[] connection = Encoding.UTF8.GetBytes("Host=harbour.invalid;Database=hermes");
        using CapturedLog log = new();
        FixedSecretClient? fixedClient = null;

        await using (HarbourFactory factory = new(log, client =>
        {
            fixedClient = client;
            client.Set(ManifestKey, new Secret { Current = 2, Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = (byte[])first.Clone(), [2] = (byte[])second.Clone() } });
            client.Set(Connection, new Secret { Current = 1, Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = (byte[])connection.Clone() } });
        }))
        {
            _ = factory.Services;
        }

        Assert.NotNull(fixedClient);
        Assert.Equal([ManifestKey, Connection], fixedClient.Reads);
        Assert.Contains("The manifest key holds 2 version(s), the current 2.", log.Lines);
        Assert.Contains("Hermes's connection holds 1 version(s).", log.Lines);
        HoldsNone(log, first, second, connection);
    }

    [Fact]
    public async Task Start_ManifestKeyMissing_IsRefused()
    {
        using CapturedLog log = new();
        await using HarbourFactory factory = new(log, client => client.Set(Connection, new Secret { Current = 1, Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Encoding.UTF8.GetBytes("Host=harbour.invalid") } }));

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Equal("The harbour cannot start without Harbour/manifestKey.", refused.Message);
        Assert.Contains("The secret Harbour/manifestKey could not be read: secrets.secret.unknown.", log.Lines);
    }

    [Fact]
    public async Task Start_HermesConnectionMissing_IsRefused()
    {
        using CapturedLog log = new();
        await using HarbourFactory factory = new(log, client => client.Set(ManifestKey, new Secret { Current = 1, Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Encoding.UTF8.GetBytes("manifest key") } }));

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Equal("The harbour cannot start without Hermes's connection.", refused.Message);
        Assert.Contains("The secret connection could not be read: none.", log.Lines);
    }

    private static void HoldsNone(CapturedLog log, params byte[][] values)
    {
        foreach (byte[] value in values)
        {
            string text = Encoding.UTF8.GetString(value);
            string base64 = Convert.ToBase64String(value);
            Assert.DoesNotContain(log.Lines, line => line.Contains(text, StringComparison.Ordinal) || line.Contains(base64, StringComparison.Ordinal));
        }
    }
}