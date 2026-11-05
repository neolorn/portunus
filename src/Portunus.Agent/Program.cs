using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

// The agent is a Linux executable (00 section 2.2).
[assembly: SupportedOSPlatform("linux")]

namespace Portunus.Agent;

/// <summary>The entry point of <c>portunus-agent</c>, which always passes the real system paths (P-027).</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using Stream input = Console.OpenStandardInput();
        return await AgentCommand.MainAsync(args, AgentPaths.System, input, Console.Out, Console.Error).ConfigureAwait(false);
    }
}