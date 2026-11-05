using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Portunus.Core;

namespace Portunus;

/// <summary>
/// Makes the client as the host starts (<c>04</c> PRT-LIB-003): its <see cref="StartingAsync"/>
/// asks for <see cref="ISecretClient"/>, so the client's configuration is checked before any
/// hosted service starts, and a refusal stops the start, unless a sibling or the host asked for
/// the client first, which checked it then.
/// </summary>
internal sealed class ClientStart(IServiceProvider services) : IHostedLifecycleService
{
    /// <exception cref="SecretsStartupException">The client's configuration was refused (<c>04</c> PRT-LIB-003).</exception>
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        services.GetRequiredService<ISecretClient>();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}