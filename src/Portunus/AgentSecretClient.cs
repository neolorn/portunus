using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Portunus.Core;

namespace Portunus;

/// <summary>
/// The client that reads each secret from the agent at the moment it is asked (<c>04</c>
/// PRT-LIB-004), waiting while the agent has no set or cannot be reached, within
/// <c>Portunus:Client:WaitTimeout</c>. It holds nothing between calls.
/// </summary>
internal sealed class AgentSecretClient(ClientOptions options, TimeProvider time, ILogger log) : ISecretClient
{
    /// <summary>The reason a wait gives where the agent could not be reached (<c>07</c> section 1).</summary>
    private const string Unreachable = "unreachable";

    /// <summary>The client's wait log comes at most this often (<c>07</c> section 5).</summary>
    private static readonly TimeSpan WaitLogInterval = TimeSpan.FromSeconds(30);

    public async ValueTask<Result<Secret>> ReadAsync(SecretAddress address, CancellationToken cancellationToken)
    {
        long began = time.GetTimestamp();
        TimeSpan? logged = null;
        using PinnedDocument request = RequestFormat.WriteRead(address);
        while (true)
        {
            Result<WireAnswer>? answer = await AgentConnection.ExchangeAsync(options.Socket, request.Memory, options.Timeout, time, cancellationToken).ConfigureAwait(false);
            string reason;
            switch (answer)
            {
                case { Value: WireAnswer.Value value }:
                    using (value.Values)
                    {
                        return Result.Success(OwnedSecret.Copy(value.Current, value.Versions));
                    }

                case { Value: WireAnswer.Refused refused }:
                    return Fail(address, refused.Failure);
                case { Failure: { Code: Codes.RequestVersion } version }:
                    return Fail(address, version);
                case { Value: WireAnswer.Waiting waiting }:
                    reason = Spellings.Of(waiting.Reason);
                    break;
                default:
                    // No answer, or none of a read's forms: the agent was not reached.
                    reason = Unreachable;
                    break;
            }

            TimeSpan waited = time.GetElapsedTime(began);
            if (waited >= options.WaitTimeout)
            {
                return Fail(address, new Failure { Code = Codes.AgentWaitTimeout, Details = new Dictionary<string, string> { ["reason"] = reason }.ToFrozenDictionary(StringComparer.Ordinal) });
            }

            if (logged is not TimeSpan last || waited - last >= WaitLogInterval)
            {
                ClientLog.WaitingForSecrets(log, reason, waited);
                logged = waited;
            }

            TimeSpan left = options.WaitTimeout - waited;
            await Task.Delay(left < options.PollInterval ? left : options.PollInterval, time, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<Result<AgentStatus>> StatusAsync(CancellationToken cancellationToken)
    {
        using PinnedDocument request = RequestFormat.WriteStatus();
        Result<WireAnswer>? answer = await AgentConnection.ExchangeAsync(options.Socket, request.Memory, options.Timeout, time, cancellationToken).ConfigureAwait(false);
        switch (answer)
        {
            case { Value: WireAnswer.Ready ready }:
                return Result.Success(new AgentStatus { State = AgentState.Ready, Since = ready.Since, Source = ready.Source, LoadedAt = ready.LoadedAt, Items = ready.Items });
            case { Value: WireAnswer.Waiting waiting }:
                return Result.Success(new AgentStatus { State = AgentState.Waiting, Reason = waiting.Reason, Since = waiting.Since, Items = 0 });
            case { Value: WireAnswer.Refused refused }:
                return FailStatus(refused.Failure);
            case { Failure: { Code: Codes.RequestVersion } version }:
                return FailStatus(version);
            case { Value: WireAnswer.Value value }:
                value.Values.Dispose();
                return FailStatus(new Failure { Code = Codes.AgentUnreachable });
            default:
                return FailStatus(new Failure { Code = Codes.AgentUnreachable });
        }
    }

    private Result<Secret> Fail(SecretAddress address, Failure failure)
    {
        ClientLog.ReadFailed(log, address.ToString(), failure.Code);
        return Result.Fail<Secret>(failure);
    }

    private Result<AgentStatus> FailStatus(Failure failure)
    {
        ClientLog.StatusFailed(log, failure.Code);
        return Result.Fail<AgentStatus>(failure);
    }
}