using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Portunus.Agent.Tests;

/// <summary>
/// The fake <c>op</c> of <c>tests/Portunus.Fakes</c> as the agent starts it (P-029): a launcher
/// script in the test's folder, owned by root and mode <c>0755</c>, that sets the fake's
/// scenario variable and starts the fake's <c>op</c> with the arguments it was given, which
/// <c>Portunus:Agent:OpPath</c> names. The fake keeps each call beside the scenario.
/// </summary>
internal sealed class FakeOp
{
    private const UnixFileMode Executable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const string ScenarioVariable = "PORTUNUS_FAKES_SCENARIO";

    private FakeOp(string folder, string scenario)
    {
        Folder = folder;
        Scenario = scenario;
        Launcher = Path.Combine(folder, "op-launcher");
        File.WriteAllText(Launcher, $"#!/bin/sh\n{ScenarioVariable}='{scenario}' exec '{Path.Combine(Fakes, "op")}' \"$@\"\n");
        File.SetUnixFileMode(Launcher, Executable);
    }

    /// <summary>The folder the fakes are built in.</summary>
    internal static string Fakes { get; } = Path.Combine(Harness.Repository, "tests", "Portunus.Fakes", "bin", "Release", "net10.0");

    /// <summary>The folder of the fake's scenario, its files and its launcher.</summary>
    internal string Folder { get; }

    /// <summary>The launcher, which <c>Portunus:Agent:OpPath</c> names.</summary>
    internal string Launcher { get; }

    /// <summary>The scenario file the fake answers from.</summary>
    internal string Scenario { get; }

    /// <summary>A fake that answers no command, which records nothing of <c>op</c>: every call exits 2 with nothing on its standard output.</summary>
    internal static FakeOp AnsweringNothing(AgentFiles files)
    {
        FakeOp op = Made(files);
        File.WriteAllText(op.Scenario, """{"recorded":false,"commands":[]}""");
        return op;
    }

    /// <summary>A fake that answers as the scenario <paramref name="scenario"/> of <c>tests/Portunus.Fakes</c> does.</summary>
    internal static FakeOp Answering(AgentFiles files, string scenario)
    {
        FakeOp op = Made(files);
        op.Answer(scenario);
        return op;
    }

    /// <summary>
    /// Answers from now on as the scenario <paramref name="scenario"/> of <c>tests/Portunus.Fakes</c>
    /// does, its files copied beside the fake's scenario; where <paramref name="listPause"/> is
    /// given, the list waits that long before it answers, and the copy is marked as not recorded.
    /// </summary>
    internal void Answer(string scenario, TimeSpan listPause = default)
    {
        JsonNode read = JsonNode.Parse(File.ReadAllText(Path.Combine(Recording.Folder, scenario + ".json")))!;
        foreach (JsonNode? command in read["commands"]!.AsArray())
        {
            foreach (string member in new[] { "output", "error" })
            {
                if (command![member]?.GetValue<string>() is string file)
                {
                    File.Copy(Path.Combine(Recording.Folder, file), Path.Combine(Folder, file), overwrite: true);
                }
            }

            if (listPause > TimeSpan.Zero && command!["arguments"]!.AsArray().Select(argument => argument!.GetValue<string>()).Take(2).SequenceEqual(["item", "list"]))
            {
                command["pauseMilliseconds"] = (int)listPause.TotalMilliseconds;
                read["recorded"] = false;
            }
        }

        // The fake reads its scenario at each call: the new one replaces the old whole.
        string written = Scenario + ".new";
        File.WriteAllText(written, read.ToJsonString());
        File.Move(written, Scenario, overwrite: true);
    }

    /// <summary>The arguments of each call the fake answered, in order.</summary>
    internal IReadOnlyList<string[]> Calls()
    {
        return [.. Lines().Select(line => Strings(line.GetProperty("arguments")))];
    }

    /// <summary>
    /// The names of each call's environment, in order, less what the launcher added after the
    /// agent's start: the fake's own variable, and <c>PWD</c>, which its shell sets as it starts.
    /// </summary>
    internal IReadOnlyList<string[]> Environments()
    {
        return [.. Lines().Select(line => Strings(line.GetProperty("environment")).Where(name => name is not (ScenarioVariable or "PWD")).ToArray())];
    }

    /// <summary>Whether a process this fake started still runs: one whose environment names its scenario.</summary>
    internal bool IsRunning()
    {
        byte[] variable = Encoding.UTF8.GetBytes($"{ScenarioVariable}={Scenario}\0");
        foreach (string process in Directory.EnumerateDirectories("/proc").Where(path => Path.GetFileName(path).All(char.IsAsciiDigit)))
        {
            try
            {
                if (File.ReadAllBytes(Path.Combine(process, "environ")).AsSpan().IndexOf(variable) >= 0)
                {
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The process ended while it was read.
            }
        }

        return false;
    }

    private static FakeOp Made(AgentFiles files)
    {
        string folder = Path.Combine(files.Folder, "op");
        Directory.CreateDirectory(folder);
        return new FakeOp(folder, Path.Combine(folder, "scenario.json"));
    }

    private static string[] Strings(JsonElement array)
    {
        return [.. array.EnumerateArray().Select(element => element.GetString()!)];
    }

    /// <summary>Each whole line the fake kept, a line it is still writing left out.</summary>
    private List<JsonElement> Lines()
    {
        List<JsonElement> read = [];
        string calls = Scenario + ".calls";
        if (!File.Exists(calls))
        {
            return read;
        }

        string text;
        using (FileStream stream = new(calls, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (StreamReader reader = new(stream))
        {
            text = reader.ReadToEnd();
        }

        foreach (string line in text.Split('\n')[..^1])
        {
            using JsonDocument call = JsonDocument.Parse(line);
            read.Add(call.RootElement.Clone());
        }

        return read;
    }
}