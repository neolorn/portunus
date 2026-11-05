using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

// The agent is a Linux executable (00 section 2.2).
[assembly: SupportedOSPlatform("linux")]

namespace Portunus.Agent;

/// <summary>The entry point of <c>portunus-agent</c>, which always passes the real system paths (P-027).</summary>
internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        return AgentCommand.MainAsync(args, AgentPaths.System, Console.Out, Console.Error);
    }
}