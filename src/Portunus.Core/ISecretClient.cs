using System.Threading;
using System.Threading.Tasks;

namespace Portunus.Core;

/// <summary>
/// The client a process reads its secrets through (<c>04</c> PRT-LIB-004): from the agent, or
/// in development from a local file (PRT-LIB-005). It holds nothing between calls.
/// </summary>
public interface ISecretClient
{
    /// <summary>
    /// Reads the secret at <paramref name="address"/> at the moment it is asked, waiting while
    /// the agent holds no set or cannot be reached, within the bound the host sets.
    /// </summary>
    /// <param name="address">The secret's address.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The secret, its arrays the caller's own; or <c>secrets.secret.unknown</c>,
    /// <c>secrets.secret.denied</c>, <c>secrets.request.version</c> or
    /// <c>secrets.agent.waittimeout</c>.
    /// </returns>
    ValueTask<Result<Secret>> ReadAsync(SecretAddress address, CancellationToken cancellationToken);

    /// <summary>Asks the agent's status once.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The agent's status; or <c>secrets.agent.unreachable</c> where it cannot be reached.</returns>
    ValueTask<Result<AgentStatus>> StatusAsync(CancellationToken cancellationToken);
}