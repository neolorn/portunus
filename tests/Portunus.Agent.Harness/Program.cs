using System;
using System.Globalization;
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
    private static Task<int> Main(string[] args)
    {
        return args switch
        {
            ["client", string socket, string request] => Client.RunAsync(socket, request, TimeSpan.Zero),
            ["client", string socket, string request, string pause] => Client.RunAsync(socket, request, TimeSpan.FromMilliseconds(int.Parse(pause, CultureInfo.InvariantCulture))),
            _ => AgentCommand.MainAsync(args[2..], new AgentPaths(args[0], args[1]), Console.Out, Console.Error),
        };
    }
}