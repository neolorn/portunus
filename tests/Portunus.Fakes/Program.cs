using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Portunus.Fakes;

/// <summary>
/// The fakes' executable (<c>06</c> PRT-ENG-001, PRT-ENG-008): it acts as <c>op</c> or as
/// <c>ssh</c> by the name it is started under, with the scenario file its own environment
/// variable <c>PORTUNUS_FAKES_SCENARIO</c> names, and keeps each call in the file beside the
/// scenario, <c>&lt;scenario&gt;.calls</c>: its arguments and its environment's names, one JSON
/// line each. As <c>op</c>, it answers from the scenario, which lists commands by their
/// arguments, <c>--config</c>'s value left out, each with the files holding its standard output
/// and standard error as recorded, its exit code, and a pause before it answers; a call that
/// matches none exits 2. As <c>ssh</c>, it runs the command it is given after <c>--</c> and the
/// destination on this machine, with its own standard streams, and exits as that command does.
/// </summary>
internal static class Program
{
    private const string ScenarioVariable = "PORTUNUS_FAKES_SCENARIO";

    private static int Main(string[] args)
    {
        string role = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
        string? scenario = Environment.GetEnvironmentVariable(ScenarioVariable);
        if (role is not ("op" or "ssh") || scenario is null)
        {
            Console.Error.WriteLine($"The fakes act as op or ssh, with {ScenarioVariable} naming a scenario.");
            return 2;
        }

        File.AppendAllLines(scenario + ".calls", [JsonSerializer.Serialize(new Call(args, [.. Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal)]), FakesJson.Default.Call)]);
        return role == "op" ? Op(args, scenario) : Ssh(args);
    }

    /// <summary>Answers as <c>op</c> from <paramref name="scenario"/>.</summary>
    private static int Op(string[] args, string scenario)
    {
        string[] matched = [.. Without(args, "--config")];
        Scenario? read = JsonSerializer.Deserialize(File.ReadAllText(scenario), FakesJson.Default.Scenario);
        Command? command = read?.Commands.FirstOrDefault(command => command.Arguments.SequenceEqual(matched, StringComparer.Ordinal));
        if (command is null)
        {
            Console.Error.WriteLine("The scenario holds no such command.");
            return 2;
        }

        Thread.Sleep(command.PauseMilliseconds);
        string folder = Path.GetDirectoryName(scenario)!;
        using (Stream output = Console.OpenStandardOutput())
        {
            if (command.Output is string file)
            {
                output.Write(File.ReadAllBytes(Path.Combine(folder, file)));
            }
        }

        using (Stream error = Console.OpenStandardError())
        {
            if (command.Error is string file)
            {
                error.Write(File.ReadAllBytes(Path.Combine(folder, file)));
            }
        }

        return command.ExitCode;
    }

    /// <summary>Runs, as <c>ssh</c>, the command after <c>--</c> and the destination, on this machine, its standard streams this process's own.</summary>
    private static int Ssh(string[] args)
    {
        int end = Array.IndexOf(args, "--");
        if (end < 0 || end + 2 >= args.Length)
        {
            Console.Error.WriteLine("The fake ssh runs the command after -- and the destination.");
            return 255;
        }

        ProcessStartInfo start = new(args[end + 2]) { UseShellExecute = false };
        foreach (string argument in args[(end + 3)..])
        {
            start.ArgumentList.Add(argument);
        }

        using Process command = Process.Start(start)!;
        command.WaitForExit();
        return command.ExitCode;
    }

    private static IEnumerable<string> Without(string[] args, string option)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == option)
            {
                index++;
                continue;
            }

            yield return args[index];
        }
    }
}