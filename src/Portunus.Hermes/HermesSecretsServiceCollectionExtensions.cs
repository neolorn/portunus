using System;
using System.Linq;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Portunus.Core;

namespace Portunus.Hermes;

/// <summary>The one call a Hermes host registers its secret source over the client with (<c>04</c> PRT-LIB-006).</summary>
public static class HermesSecretsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the client through <c>AddPortunus</c>, and Hermes's <see cref="ISecretSource"/>
    /// as a singleton over it, which reads each secret at the item
    /// <c>Portunus:Hermes:Items:&lt;secret name&gt;</c> names, else at
    /// <c>Portunus:Hermes:Item</c> (by default <c>Hermes</c>), in the field of the secret's name.
    /// Calling it twice registers once.
    /// </summary>
    /// <remarks>
    /// The source checks its configuration when it is first made, as Hermes starts, and throws a
    /// <see cref="SecretsStartupException"/> there: <c>secrets.startup.configurationinvalid</c>
    /// for a key outside its rule, naming it in <c>details.key</c>. A secret it cannot read, or
    /// whose value is not of its form, it answers as none, so Hermes refuses its start naming
    /// the secret where it needs it.
    /// </remarks>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
    public static IServiceCollection AddPortunusHermesSecrets(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddPortunus(configuration);
        if (services.Any(service => service.ServiceType == typeof(HermesSecretSource)))
        {
            return services;
        }

        services.AddSingleton(provider => new HermesSecretSource(
            HermesConfiguration.Read(configuration),
            provider.GetRequiredService<ISecretClient>(),
            provider.GetService<ILogger<HermesSecretSource>>() ?? (ILogger)NullLogger.Instance));
        services.AddSingleton<ISecretSource>(provider => provider.GetRequiredService<HermesSecretSource>());
        return services;
    }
}