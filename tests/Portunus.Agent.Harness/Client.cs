using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Portunus.Agent.Harness;

/// <summary>
/// The harness's <c>client</c> role (P-030): it connects to the socket it is given, sends the
/// bytes of the file it is given, waits the pause it is given, shuts down its sending side, and
/// writes the answer's bytes to standard output, or the line <c>no answer</c> where none came.
/// A test starts it under <c>setpriv</c> as a declared process's user.
/// </summary>
internal static class Client
{
    /// <summary>What the client writes where the agent closed the connection without an answer.</summary>
    private static ReadOnlySpan<byte> NoAnswer => "no answer\n"u8;

    internal static async Task<int> RunAsync(string socketPath, string requestFile, TimeSpan pause)
    {
        byte[] request = await File.ReadAllBytesAsync(requestFile).ConfigureAwait(false);
        using MemoryStream answer = new();
        using (Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath)).ConfigureAwait(false);
            try
            {
                for (int sent = 0; sent < request.Length;)
                {
                    sent += await socket.SendAsync(request.AsMemory(sent), SocketFlags.None).ConfigureAwait(false);
                }

                await Task.Delay(pause).ConfigureAwait(false);
                socket.Shutdown(SocketShutdown.Send);
                byte[] buffer = new byte[65536];
                int count;
                while ((count = await socket.ReceiveAsync(buffer, SocketFlags.None).ConfigureAwait(false)) > 0)
                {
                    await answer.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
                }
            }
            catch (SocketException)
            {
                // The agent closed the connection before the request was sent or answered.
            }
        }

        using Stream output = Console.OpenStandardOutput();
        if (answer.Length == 0)
        {
            output.Write(NoAnswer);
        }
        else
        {
            answer.WriteTo(output);
        }

        return 0;
    }
}