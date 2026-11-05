using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Extensions.Configuration;

using Portunus.Core;

using Xunit;

namespace Portunus.Tests;

[Trait("Category", "unit")]
public sealed class ClientConfigurationTests
{
    // An absolute path in the platform's own form, which the rule of a path takes.
    private static readonly string AbsoluteFile = Path.Combine(Path.GetTempPath(), "development.json");

    [Fact]
    public void PRT_OPS_001_AC1_EveryClientKeyHasItsDefaultWhenAbsent()
    {
        ClientOptions options = ClientConfiguration.Read(Configuration());

        Assert.Equal(ClientSource.Agent, options.Source);
        Assert.Equal("/run/portunus/agent.sock", options.Socket);
        Assert.Null(options.File);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(5), options.WaitTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), options.PollInterval);
    }

    [Theory]
    [InlineData("Portunus:Client:Source", "Agent")]
    [InlineData("Portunus:Client:Source", "vault")]
    [InlineData("Portunus:Client:Source", "")]
    [InlineData("Portunus:Client:Socket", "agent.sock")]
    [InlineData("Portunus:Client:File", "development.json")]
    [InlineData("Portunus:Client:Timeout", "00:00:00.5")]
    [InlineData("Portunus:Client:Timeout", "00:01:01")]
    [InlineData("Portunus:Client:Timeout", "soon")]
    [InlineData("Portunus:Client:WaitTimeout", "-00:00:01")]
    [InlineData("Portunus:Client:WaitTimeout", "01:00:01")]
    [InlineData("Portunus:Client:PollInterval", "00:00:00")]
    [InlineData("Portunus:Client:PollInterval", "1.00:00:00")]
    public void PRT_OPS_001_AC1_ClientKeyOutsideItsRuleStopsTheStartNamingIt(string key, string value)
    {
        SecretsStartupException refusal = Assert.Throws<SecretsStartupException>(() => ClientConfiguration.Read(Configuration((key, value))));

        Assert.Equal("secrets.startup.configurationinvalid", refusal.Code);
        Assert.Equal(key, refusal.Details["key"]);
    }

    [Fact]
    public void PRT_OPS_001_AC1_SocketPathOfOneHundredAndEightBytesStopsTheStart()
    {
        string within = Path.Combine(Path.GetTempPath(), "s");
        string path = within + new string('a', 108 - within.Length);

        SecretsStartupException refusal = Assert.Throws<SecretsStartupException>(() => ClientConfiguration.Read(Configuration((ClientKeys.Socket, path))));

        Assert.Equal(ClientKeys.Socket, refusal.Details["key"]);
        Assert.Equal(path[..^1], ClientConfiguration.Read(Configuration((ClientKeys.Socket, path[..^1]))).Socket);
    }

    [Fact]
    public void PRT_OPS_001_AC1_FileSourceWithoutAFileStopsTheStartNamingTheFile()
    {
        SecretsStartupException refusal = Assert.Throws<SecretsStartupException>(() => ClientConfiguration.Read(Configuration((ClientKeys.Source, "file"))));

        Assert.Equal(ClientKeys.File, refusal.Details["key"]);
    }

    [Fact]
    public void Read_ValuesWithinTheirRules_AnswersThemBound()
    {
        ClientOptions options = ClientConfiguration.Read(Configuration(
            (ClientKeys.Source, "file"),
            (ClientKeys.File, AbsoluteFile),
            (ClientKeys.Timeout, "00:01:00"),
            (ClientKeys.WaitTimeout, "00:00:00"),
            (ClientKeys.PollInterval, "00:00:01")));

        Assert.Equal(ClientSource.File, options.Source);
        Assert.Equal(AbsoluteFile, options.File);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Timeout);
        Assert.Equal(TimeSpan.Zero, options.WaitTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), options.PollInterval);
    }

    internal static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        Dictionary<string, string?> entries = [];
        foreach ((string key, string value) in values)
        {
            entries[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
    }
}