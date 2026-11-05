using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Portunus.Core;
using Portunus.Testing;

using Themis.Core;

using Xunit;

namespace Portunus.Themis.Tests;

/// <summary>The adapter's registration in a real host (<c>04</c> PRT-LIB-006, PRT-LIB-008, P-039).</summary>
[Trait("Category", "unit")]
public sealed class ThemisSecretsServiceCollectionExtensionsTests
{
    [Fact]
    public void PRT_LIB_006_AC4_HostBuiltWithTheAdaptersCallAloneResolvesItsSourceOnce()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddPortunusThemisSecrets(builder.Configuration);
        builder.Services.AddPortunusThemisSecrets(builder.Configuration);
        using IHost host = builder.Build();

        ISecretSource[] sources = [.. host.Services.GetServices<ISecretSource>()];

        Assert.IsType<ThemisSecretSource>(Assert.Single(sources));
        Assert.Same(sources[0], host.Services.GetRequiredService<ISecretSource>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PRT_LIB_008_AC1_HostTestWithTheFixedClientSetsThemisConnectionInEitherOrder(bool testingFirst)
    {
        byte[] value = Encoding.UTF8.GetBytes($"Host=approvals.invalid;Password={Guid.NewGuid():N}");
        Secret secret = new() { Current = 1, Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = value } };
        SecretAddress connection = new("Themis", "connection");
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        if (testingFirst)
        {
            builder.Services.AddPortunusTesting(client => client.Set(connection, secret)).AddPortunusThemisSecrets(builder.Configuration);
        }
        else
        {
            builder.Services.AddPortunusThemisSecrets(builder.Configuration).AddPortunusTesting(client => client.Set(connection, secret));
        }

        using IHost host = builder.Build();

        SecretVersions? read = await host.Services.GetRequiredService<ISecretSource>().ReadAsync("connection", TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(read!.Versions).Value.Span.SequenceEqual(value));
    }
}