using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Portunus.Core;

using Xunit;

namespace Portunus.Testing.Tests;

[Trait("Category", "unit")]
public sealed class SecretsTestingServiceCollectionExtensionsTests
{
    private static readonly SecretAddress Connection = SecretAddress.Parse("Themis/connection");

    private static readonly Secret Set = new() { Current = 1, Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Encoding.UTF8.GetBytes("sample-connection") } };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddPortunusTesting_EitherOrderWithAddPortunus_ServesTheHostTheFixedClient(bool testingFirst)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = Environments.Production });
        if (testingFirst)
        {
            builder.Services.AddPortunusTesting(client => client.Set(Connection, Set)).AddPortunus(builder.Configuration);
        }
        else
        {
            builder.Services.AddPortunus(builder.Configuration).AddPortunusTesting(client => client.Set(Connection, Set));
        }

        using IHost host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        ISecretClient client = host.Services.GetRequiredService<ISecretClient>();
        Result<Secret> read = await client.ReadAsync(Connection, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Same(host.Services.GetRequiredService<FixedSecretClient>(), client);
        Assert.Same(Set, read.Value);
    }

    [Fact]
    public void AddPortunusTesting_CalledTwice_RegistersOneClientConfiguredByEach()
    {
        ServiceCollection services = new();
        SecretAddress keys = SecretAddress.Parse("Janus/keys");

        services.AddPortunusTesting(client => client.Set(Connection, Set)).AddPortunusTesting(client => client.Deny(keys));

        Assert.Single(services, service => service.ServiceType == typeof(FixedSecretClient));
        Assert.Single(services, service => service.ServiceType == typeof(ISecretClient));
        using ServiceProvider provider = services.BuildServiceProvider();
        FixedSecretClient fixedClient = provider.GetRequiredService<FixedSecretClient>();
        Assert.Same(fixedClient, provider.GetRequiredService<ISecretClient>());
    }

    [Fact]
    public async Task AddPortunusTesting_WithoutConfigure_RegistersAClientAnsweringUnknown()
    {
        ServiceCollection services = new();

        services.AddPortunusTesting();

        using ServiceProvider provider = services.BuildServiceProvider();
        Result<Secret> read = await provider.GetRequiredService<ISecretClient>().ReadAsync(Connection, TestContext.Current.CancellationToken);
        Assert.Equal("secrets.secret.unknown", read.Failure!.Code);
    }

    [Fact]
    public void AddPortunusTesting_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SecretsTestingServiceCollectionExtensions.AddPortunusTesting(null!));
    }
}