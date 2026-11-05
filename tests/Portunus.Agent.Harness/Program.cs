using System;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

// The harness runs the agent, a Linux executable, on Linux alone (P-027).
[assembly: SupportedOSPlatform("linux")]

namespace Portunus.Agent.Harness;

/// <summary>
/// The agent as the shipped executable runs it, through the same method and signal handling,
/// with the system paths a test gives as its first two arguments (P-027): the configuration's
/// directory and the swaps file, then the command. As <c>client &lt;socket&gt; &lt;request
/// file&gt; [&lt;pause in milliseconds&gt;]</c>, it is a process's client instead (P-030).
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        switch (args)
        {
            case ["client", string socket, string request]:
                return await Client.RunAsync(socket, request, TimeSpan.Zero).ConfigureAwait(false);
            case ["client", string socket, string request, string pause]:
                return await Client.RunAsync(socket, request, TimeSpan.FromMilliseconds(int.Parse(pause, CultureInfo.InvariantCulture))).ConfigureAwait(false);
            default:
                using (Stream input = Console.OpenStandardInput())
                {
                    return await AgentCommand.MainAsync(args[2..], new AgentPaths(args[0], args[1]), input, Console.Out, Console.Error).ConfigureAwait(false);
                }
        }
    }
}