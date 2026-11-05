using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Portunus.Core;

namespace Portunus.Testing;

/// <summary>The call a host's tests put the fixed client in place of the client with (<c>04</c> PRT-LIB-008).</summary>
public static class SecretsTestingServiceCollectionExtensions
{
    /// <summary>
    /// Registers one <see cref="FixedSecretClient"/> as a singleton, resolvable by its own type
    /// and as <see cref="ISecretClient"/>, in place of the client, in either order with
    /// <c>AddPortunus</c> or an adapter's call; <paramref name="configure"/> runs on it at once.
    /// Called again, it configures the same client.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configure">What the test sets on the client, if anything.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddPortunusTesting(this IServiceCollection services, Action<FixedSecretClient>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        FixedSecretClient? client = services.FirstOrDefault(service => service.ServiceType == typeof(FixedSecretClient))?.ImplementationInstance as FixedSecretClient;
        if (client is null)
        {
            client = new FixedSecretClient();
            services.RemoveAll<ISecretClient>();
            services.AddSingleton(client);
            services.AddSingleton<ISecretClient>(client);
        }

        configure?.Invoke(client);
        return services;
    }
}