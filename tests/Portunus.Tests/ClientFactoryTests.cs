using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using Portunus.Core;

using Xunit;

namespace Portunus.Tests;

[Trait("Category", "integration")]
public sealed class ClientFactoryTests : IDisposable
{
    private static readonly byte[] Connection = Recorded.Value("sample-connection");

    private static readonly byte[] Second = Recorded.Value("sample-key-second");

    private readonly TestClock _clock = new();

    private readonly CapturedLog _log = new();

    private readonly string _file = Scratch.NewPath(".json");

    [Fact]
    public async Task PRT_LIB_005_AC1_DevelopmentHostReadsTheFileAndAnAddressItLacksIsUnknown()
    {
        WriteFile("""{"vault":"Sample Development","items":{"Themis":{"connection":"sample-connection"}}}""");
        ISecretClient client = Make(Environments.Development);

        Result<Secret> read = await client.ReadAsync(SecretAddress.Parse("Themis/connection"), TestContext.Current.CancellationToken);
        Result<Secret> absent = await client.ReadAsync(SecretAddress.Parse("Themis/other"), TestContext.Current.CancellationToken);

        Assert.Equal(Connection, read.Value!.CurrentValue.ToArray());
        Assert.Equal("secrets.secret.unknown", absent.Failure!.Code);
    }

    [Fact]
    public void Make_ConfigurationValueOutsideItsRule_StopsTheStartNamingTheKey()
    {
        IConfiguration configuration = ClientConfigurationTests.Configuration((ClientKeys.PollInterval, "00:02:00"));

        SecretsStartupException refusal = Assert.Throws<SecretsStartupException>(() => ClientFactory.Make(configuration, new TestEnvironment(Environments.Production), _clock, _log));

        Assert.Equal("secrets.startup.configurationinvalid", refusal.Code);
        Assert.Equal("Portunus:Client:PollInterval", refusal.Details["key"]);
    }

    [Fact]
    public void Make_FileSourceInAProductionHost_StopsTheStartAsNotDevelopment()
    {
        WriteFile("""{"vault":"Sample Development","items":{"Themis":{"connection":"sample-connection"}}}""");

        SecretsStartupException refusal = Assert.Throws<SecretsStartupException>(() => Make(Environments.Production));

        Assert.Equal("secrets.startup.notdevelopment", refusal.Code);
        Assert.Empty(refusal.Details);
    }

    [Fact]
    public void Make_FileSourceInAHostThatRegistersNoEnvironment_StopsTheStartAsNotDevelopment()
    {
        WriteFile("""{"vault":"Sample Development","items":{}}""");

        SecretsStartupException refusal = Assert.Throws<SecretsStartupException>(() => ClientFactory.Make(FileConfiguration(), null, _clock, _log));

        Assert.Equal("secrets.startup.notdevelopment", refusal.Code);
    }

    [Fact]
    public void Make_FileThatCannotBeRead_StopsTheStartAsFileInvalid()
    {
        SecretsStartupException refusal = Assert.Throws<SecretsStartupException>(() => Make(Environments.Development));

        Assert.Equal("secrets.startup.fileinvalid", refusal.Code);
        Assert.Empty(refusal.Details);
    }

    [Fact]
    public void Make_FileThatIsNotJson_StopsTheStartAsFileInvalid()
    {
        WriteFile("Themis/connection = sample-connection");

        SecretsStartupException refusal = Assert.Throws<SecretsStartupException>(() => Make(Environments.Development));

        Assert.Equal("secrets.startup.fileinvalid", refusal.Code);
        Assert.Empty(refusal.Details);
    }

    [Fact]
    public void Make_FileBreakingTheLayout_StopsTheStartNamingItsFirstFinding()
    {
        WriteFile("""{"vault":"Sample Development","items":{"Themis":{"connection":"sample-connection","connection.1":"sample-connection"}}}""");

        SecretsStartupException refusal = Assert.Throws<SecretsStartupException>(() => Make(Environments.Development));

        Assert.Equal("secrets.startup.fileinvalid", refusal.Code);
        Assert.Equal("Themis", refusal.Details["item"]);
        Assert.Equal("connection", refusal.Details["field"]);
        Assert.DoesNotContain("sample-connection", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Make_FileOfAVersionedSecret_AnswersItsLiveVersionsAndItsStatus()
    {
        WriteFile("""{"vault":"Sample Development","items":{"Janus":{"keys.1.retired":"sample-key-first","keys.2":"sample-key-second","keys.current":"2"},"Themis":{"connection":"sample-connection"}}}""");
        ISecretClient client = Make(Environments.Development);

        Secret keys = (await client.ReadAsync(SecretAddress.Parse("Janus/keys"), TestContext.Current.CancellationToken)).Value!;
        AgentStatus status = (await client.StatusAsync(TestContext.Current.CancellationToken)).Value!;

        Assert.Equal(2, keys.Current);
        Assert.Equal([2], keys.Versions.Keys);
        Assert.Equal(Second, keys.CurrentValue.ToArray());
        Assert.Equal(AgentState.Ready, status.State);
        Assert.Equal(SetSource.Vault, status.Source);
        Assert.Equal(TestClock.Start, status.LoadedAt);
        Assert.Equal(2, status.Items);
    }

    [Fact]
    public void Make_AgentSource_AnswersTheAgentsClient()
    {
        ISecretClient client = ClientFactory.Make(ClientConfigurationTests.Configuration(), new TestEnvironment(Environments.Production), _clock, _log);

        Assert.IsType<AgentSecretClient>(client);
    }

    public void Dispose()
    {
        File.Delete(_file);

        // No line the client logged holds a value the test used (05 PRT-OPS-005).
        _log.HoldsNone(Connection, Second, Recorded.Value("sample-key-first"));
    }

    private ISecretClient Make(string environment)
    {
        return ClientFactory.Make(FileConfiguration(), new TestEnvironment(environment), _clock, _log);
    }

    private IConfiguration FileConfiguration()
    {
        return ClientConfigurationTests.Configuration((ClientKeys.Source, "file"), (ClientKeys.File, _file));
    }

    private void WriteFile(string content)
    {
        File.WriteAllText(_file, content);
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Sample";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}