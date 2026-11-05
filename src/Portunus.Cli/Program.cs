using System;
using System.Threading;
using System.Threading.Tasks;

namespace Portunus.Cli;

/// <summary>The entry point of <c>portunus</c>, which passes the process's own environment (<c>02</c>).</summary>
internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        return ToolCommand.MainAsync(args, ToolEnvironment.OfProcess(), Console.Out, Console.Error, CancellationToken.None);
    }
}