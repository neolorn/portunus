using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Portunus.Core;

namespace Portunus;

/// <summary>
/// One exchange with the agent on its socket (<c>01</c> PRT-AGT-008): one request, the
/// sending side then shut down, and one answer read until the agent closes the connection,
/// bounded by <c>Portunus:Client:Timeout</c> and by the answer's 2 MiB (<c>07</c> section 5).
/// What is read stays in pinned arrays, zeroed once the answer is read (<c>06</c> PRT-ENG-005).
/// </summary>
internal static class AgentConnection
{
    private const int ReceiveBytes = 4096;

    /// <summary>
    /// Sends <paramref name="request"/> to the agent at <paramref name="socketPath"/> and
    /// answers what it answered, read in its format; or none where the socket is missing,
    /// refuses the connection, closes it without an answer, answers over its bound, or does
    /// not answer within <paramref name="timeout"/>.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    internal static async ValueTask<Result<WireAnswer>?> ExchangeAsync(string socketPath, ReadOnlyMemory<byte> request, TimeSpan timeout, TimeProvider time, CancellationToken cancellationToken)
    {
        using CancellationTokenSource bound = new(timeout, time);
        using CancellationTokenSource exchange = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, cancellationToken);
        using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), exchange.Token).ConfigureAwait(false);
            for (int sent = 0; sent < request.Length;)
            {
                sent += await socket.SendAsync(request[sent..], SocketFlags.None, exchange.Token).ConfigureAwait(false);
            }

            socket.Shutdown(SocketShutdown.Send);
            using PinnedBufferWriter answer = new(Limits.AnswerBytes);
            while (true)
            {
                int count = await socket.ReceiveAsync(answer.GetMemory(ReceiveBytes), SocketFlags.None, exchange.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                answer.Advance(count);
                if (answer.IsOverBound)
                {
                    return null;
                }
            }

            using PinnedDocument document = answer.Detach();
            return AnswerFormat.Read(document.Span);
        }
        catch (SocketException)
        {
            // A missing socket, a refused connection or one the agent broke: no answer.
            return null;
        }
        catch (OperationCanceledException) when (bound.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The agent did not answer within the exchange's bound.
            return null;
        }
    }
}