using System;
using System.Collections.Frozen;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Portunus.Core;

namespace Portunus;

/// <summary>
/// Makes the client once, checking its configuration first (<c>04</c> PRT-LIB-003): the
/// agent's client, or in a Development host whose source is <c>file</c>, the development
/// file's.
/// </summary>
internal static class ClientFactory
{
    /// <summary>Makes the client the host's configuration names.</summary>
    /// <param name="configuration">The host's whole configuration.</param>
    /// <param name="environment">The host's environment, or none where it registers none.</param>
    /// <param name="time">The clock the client's waits are measured by.</param>
    /// <param name="log">Where the client logs.</param>
    /// <exception cref="SecretsStartupException">
    /// <c>secrets.startup.configurationinvalid</c> for a key outside its rule;
    /// <c>secrets.startup.notdevelopment</c> for the <c>file</c> source in a host that is not
    /// Development; <c>secrets.startup.fileinvalid</c> for a development file that cannot be
    /// read, is not JSON or breaks the layout.
    /// </exception>
    internal static ISecretClient Make(IConfiguration configuration, IHostEnvironment? environment, TimeProvider time, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(log);
        ClientOptions options = ClientConfiguration.Read(configuration);
        if (options.Source == ClientSource.Agent)
        {
            return new AgentSecretClient(options, time, log);
        }

        if (environment is null || !environment.IsDevelopment())
        {
            throw new SecretsStartupException(Codes.StartupNotDevelopment, FrozenDictionary<string, string>.Empty);
        }

        return FileSecretClient.Read(options.File!, time, log);
    }
}