using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Portunus;
using Portunus.Hermes;

namespace Harbour;

/// <summary>
/// The harbour's services: the client, Hermes's secret source over it, and the start that
/// reads the harbour's own manifest key and Hermes's connection (<c>06</c> PRT-ENG-001, P-040).
/// </summary>
internal static class HarbourServices
{
    internal static IServiceCollection Add(IServiceCollection services, IConfiguration configuration)
    {
        services.AddPortunus(configuration);
        services.AddPortunusHermesSecrets(configuration);
        services.AddHostedService<SecretsAtStart>();
        return services;
    }
}