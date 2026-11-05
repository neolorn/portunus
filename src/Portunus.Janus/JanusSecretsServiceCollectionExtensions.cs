using System;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Portunus.Core;

using JanusCore = Janus.Core;

namespace Portunus.Janus;

/// <summary>The one call a Janus host registers its secret source over the client with (<c>04</c> PRT-LIB-006).</summary>
public static class JanusSecretsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the client through <c>AddPortunus</c>, and Janus's
    /// <see cref="JanusCore.ISecretSource"/> as a singleton over it, which reads each secret at the item
    /// <c>Portunus:Janus:Items:&lt;secret name&gt;</c> names, else at
    /// <c>Portunus:Janus:Item</c> (by default <c>Janus</c>), in the field of the secret's name.
    /// Calling it twice registers once.
    /// </summary>
    /// <remarks>
    /// The source checks its configuration when it is first made, as Janus starts, and throws a
    /// <see cref="SecretsStartupException"/> there: <c>secrets.startup.configurationinvalid</c>
    /// for a key outside its rule, naming it in <c>details.key</c>. A secret it cannot read, or
    /// whose value is not of its form, it answers as Janus's
    /// <c>model.startup.secretunavailable</c> naming the secret, so Janus refuses its start
    /// where it needs it.
    /// </remarks>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
    public static IServiceCollection AddPortunusJanusSecrets(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddPortunus(configuration);
        if (services.Any(service => service.ServiceType == typeof(JanusSecretSource)))
        {
            return services;
        }

        services.AddSingleton(provider => new JanusSecretSource(
            JanusConfiguration.Read(configuration),
            provider.GetRequiredService<ISecretClient>(),
            provider.GetService<ILogger<JanusSecretSource>>() ?? (ILogger)NullLogger.Instance));
        services.AddSingleton<JanusCore.ISecretSource>(provider => provider.GetRequiredService<JanusSecretSource>());
        return services;
    }
}