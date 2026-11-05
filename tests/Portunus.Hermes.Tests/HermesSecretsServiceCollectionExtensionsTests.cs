using System.Linq;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Portunus.Hermes.Tests;

/// <summary>The adapter's registration in a real host (<c>04</c> PRT-LIB-006, P-039).</summary>
[Trait("Category", "unit")]
public sealed class HermesSecretsServiceCollectionExtensionsTests
{
    [Fact]
    public void PRT_LIB_006_AC4_HostBuiltWithTheAdaptersCallAloneResolvesItsSourceOnce()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddPortunusHermesSecrets(builder.Configuration);
        builder.Services.AddPortunusHermesSecrets(builder.Configuration);
        using IHost host = builder.Build();

        ISecretSource[] sources = [.. host.Services.GetServices<ISecretSource>()];

        Assert.IsType<HermesSecretSource>(Assert.Single(sources));
        Assert.Same(sources[0], host.Services.GetRequiredService<ISecretSource>());
    }
}