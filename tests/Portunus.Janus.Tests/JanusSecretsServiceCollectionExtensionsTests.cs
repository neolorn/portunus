using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

using JanusCore = Janus.Core;

namespace Portunus.Janus.Tests;

/// <summary>The adapter's registration in a real host (<c>04</c> PRT-LIB-006, P-039).</summary>
[Trait("Category", "unit")]
public sealed class JanusSecretsServiceCollectionExtensionsTests
{
    [Fact]
    public void PRT_LIB_006_AC4_HostBuiltWithTheAdaptersCallAloneResolvesItsSourceOnce()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddPortunusJanusSecrets(builder.Configuration);
        builder.Services.AddPortunusJanusSecrets(builder.Configuration);
        using IHost host = builder.Build();

        JanusCore.ISecretSource[] sources = [.. host.Services.GetServices<JanusCore.ISecretSource>()];

        Assert.IsType<JanusSecretSource>(Assert.Single(sources));
        Assert.Same(sources[0], host.Services.GetRequiredService<JanusCore.ISecretSource>());
    }
}