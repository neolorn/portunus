using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Portunus.Core;

using Xunit;

namespace Portunus.Tests;

[Trait("Category", "integration")]
public sealed class SecretsServiceCollectionExtensionsTests : IAsyncDisposable
{
    private static readonly byte[] Connection = Recorded.Value("sample-connection");

    private readonly CapturedLog _log = new();

    private readonly string _socket = Scratch.NewPath(".sock");

    private readonly string _file = Scratch.NewPath(".json");

    private FakeAgent? _agent;

    [Fact]
    public async Task PRT_LIB_003_AC1_HostWithValidConfigurationReadsASecretAfterTheOneCall()
    {
        _agent = FakeAgent.Start(_socket, _ => Recorded.Secret(1, Connection));
        using IHost host = Host(Environments.Production, (ClientKeys.Socket, _socket));

        await host.StartAsync(TestContext.Current.CancellationToken);
        Result<Secret> read = await host.Services.GetRequiredService<ISecretClient>().ReadAsync(SecretAddress.Parse("Themis/connection"), TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Connection, read.Value!.CurrentValue.ToArray());
        Assert.IsType<AgentSecretClient>(host.Services.GetRequiredService<ISecretClient>());
    }

    [Fact]
    public async Task PRT_LIB_003_AC2_ConfigurationValueOutsideItsRuleStopsTheStartNamingTheKey()
    {
        using IHost host = Host(Environments.Production, (ClientKeys.Timeout, "00:05:00"));

        SecretsStartupException refusal = await Assert.ThrowsAsync<SecretsStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("secrets.startup.configurationinvalid", refusal.Code);
        Assert.Equal("Portunus:Client:Timeout", refusal.Details["key"]);
    }

    [Fact]
    public async Task PRT_LIB_003_AC3_FileSourceInAProductionHostStopsTheStart()
    {
        await File.WriteAllTextAsync(_file, """{"vault":"Sample Development","items":{"Themis":{"connection":"sample-connection"}}}""", TestContext.Current.CancellationToken);
        using IHost host = Host(Environments.Production, (ClientKeys.Source, "file"), (ClientKeys.File, _file));

        SecretsStartupException refusal = await Assert.ThrowsAsync<SecretsStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("secrets.startup.notdevelopment", refusal.Code);
    }

    [Fact]
    public async Task AddPortunus_FileSourceInADevelopmentHost_ReadsTheFile()
    {
        await File.WriteAllTextAsync(_file, """{"vault":"Sample Development","items":{"Themis":{"connection":"sample-connection"}}}""", TestContext.Current.CancellationToken);
        using IHost host = Host(Environments.Development, (ClientKeys.Source, "file"), (ClientKeys.File, _file));

        await host.StartAsync(TestContext.Current.CancellationToken);
        Result<Secret> read = await host.Services.GetRequiredService<ISecretClient>().ReadAsync(SecretAddress.Parse("Themis/connection"), TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Connection, read.Value!.CurrentValue.ToArray());
    }

    [Fact]
    public async Task AddPortunus_HealthCheck_IsRegisteredAsPortunus()
    {
        _agent = FakeAgent.Start(_socket, _ => Recorded.Waiting("fetching"));
        using IHost host = Host(Environments.Production, (ClientKeys.Socket, _socket));
        await host.StartAsync(TestContext.Current.CancellationToken);

        HealthReport report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        HealthReportEntry entry = Assert.Single(report.Entries, entry => entry.Key == "portunus").Value;
        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.Equal("fetching", entry.Data["reason"]);
    }

    [Fact]
    public void AddPortunus_CalledTwice_RegistersOnce()
    {
        ServiceCollection services = new();
        services.AddLogging();
        IConfiguration configuration = ClientConfigurationTests.Configuration();

        services.AddPortunus(configuration).AddPortunus(configuration);

        Assert.Single(services, service => service.ServiceType == typeof(ISecretClient));
        Assert.Single(services, service => service.ServiceType == typeof(ClientStart));
        using ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckServiceOptions checks = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Single(checks.Registrations, registration => registration.Name == "portunus");
        Assert.Single(provider.GetServices<IHostedService>().OfType<ClientStart>());
    }

    [Fact]
    public void AddPortunus_NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SecretsServiceCollectionExtensions.AddPortunus(null!, ClientConfigurationTests.Configuration()));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddPortunus(null!));
    }

    public async ValueTask DisposeAsync()
    {
        if (_agent is not null)
        {
            await _agent.DisposeAsync();
        }

        File.Delete(_file);

        // No line the host logged holds a value the test used (05 PRT-OPS-005).
        _log.HoldsNone(Connection);
    }

    private IHost Host(string environment, params (string Key, string Value)[] values)
    {
        HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)));
        builder.Services.AddSingleton<ILoggerProvider>(_ => new CapturedLogProvider(_log));
        builder.Services.AddPortunus(builder.Configuration);
        return builder.Build();
    }
}