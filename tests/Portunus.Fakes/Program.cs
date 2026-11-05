using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Portunus.Fakes;

/// <summary>
/// The fakes' executable (<c>06</c> PRT-ENG-001, PRT-ENG-008): it acts as <c>op</c> by the name
/// it is started under, answering from the scenario file its own environment variable
/// <c>PORTUNUS_FAKES_SCENARIO</c> names, and keeps each call it answers in the file beside the
/// scenario, <c>&lt;scenario&gt;.calls</c>: its arguments and its environment's names, one JSON
/// line each. A scenario lists commands by their arguments, <c>--config</c>'s value left out,
/// each with the files holding its standard output and standard error as recorded, its exit
/// code, and a pause before it answers; a call that matches none exits 2.
/// </summary>
internal static class Program
{
    private const string ScenarioVariable = "PORTUNUS_FAKES_SCENARIO";

    private static int Main(string[] args)
    {
        string role = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
        string? scenario = Environment.GetEnvironmentVariable(ScenarioVariable);
        if (role != "op" || scenario is null)
        {
            Console.Error.WriteLine($"The fakes act as op, with {ScenarioVariable} naming a scenario.");
            return 2;
        }

        string[] matched = [.. Without(args, "--config")];
        File.AppendAllLines(scenario + ".calls", [JsonSerializer.Serialize(new Call(args, [.. Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal)]), FakesJson.Default.Call)]);
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