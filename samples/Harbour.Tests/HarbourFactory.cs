using System;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Portunus.Testing;

namespace Harbour.Tests;

/// <summary>
/// The harbour started through its own entry point, its client <c>Portunus.Testing</c>'s, set
/// by <paramref name="configure"/>, in place of the agent's, and its log lines kept in
/// <paramref name="log"/> (<c>06</c> PRT-ENG-001, P-042).
/// </summary>
internal sealed class HarbourFactory(CapturedLog log, Action<FixedSecretClient> configure) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The factory's test server needs an application, and the harbour serves no request, so it gets an empty one.
        builder.Configure(_ => { });
        builder.ConfigureTestServices(services =>
        {
            services.AddPortunusTesting(configure);
            services.AddSingleton<ILoggerProvider>(log);
        });
    }
}