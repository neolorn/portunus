using System;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Portunus.Core;

namespace Portunus;

/// <summary>The one call a host registers the secret client with (<c>04</c> PRT-LIB-003).</summary>
public static class SecretsServiceCollectionExtensions
{
    /// <summary>The client's health check's name (<c>07</c> section 7).</summary>
    private const string HealthCheckName = "portunus";

    /// <summary>
    /// Registers the client: <see cref="ISecretClient"/> as a singleton bound from the section
    /// <c>Portunus:Client</c> of <paramref name="configuration"/>, a hosted lifecycle service
    /// whose <c>StartingAsync</c> makes it, and the health check <c>portunus</c>. Calling it
    /// twice registers once. A client already registered, as the fixed client of
    /// <c>Portunus.Testing</c>, is kept.
    /// </summary>
    /// <remarks>
    /// The client checks its configuration when it is first made, by that service or by the
    /// first code that asks for it, and throws a <see cref="SecretsStartupException"/> there:
    /// <c>secrets.startup.configurationinvalid</c> for a key outside its rule, naming it in
    /// <c>details.key</c>; <c>secrets.startup.notdevelopment</c> for the <c>file</c> source in
    /// a host whose environment is not Development, or that registers none;
    /// <c>secrets.startup.fileinvalid</c> for a development file that cannot be read, is not
    /// JSON or breaks the layout, naming <c>details.item</c> and <c>details.field</c> where the
    /// layout names them.
    /// </remarks>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
    public static IServiceCollection AddPortunus(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (services.Any(service => service.ServiceType == typeof(ClientStart)))
        {
            return services;
        }

        services.TryAddSingleton<ISecretClient>(provider => ClientFactory.Make(
            configuration,
            provider.GetService<IHostEnvironment>(),
            provider.GetService<TimeProvider>() ?? TimeProvider.System,
            provider.GetService<ILogger<ISecretClient>>() ?? (ILogger)NullLogger.Instance));
        services.AddSingleton(provider => new ClientStart(provider));
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ClientStart>());

        // The check reads Degraded, never Unhealthy, where it cannot read the agent (05 PRT-OPS-004).
        services.AddHealthChecks().Add(new HealthCheckRegistration(
            HealthCheckName,
            provider => new ClientHealthCheck(provider.GetRequiredService<ISecretClient>()),
            HealthStatus.Degraded,
            tags: null));
        return services;
    }
}