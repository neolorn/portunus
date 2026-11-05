using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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

    private FakeOp(string folder, string scenario)
    {
        Scenario = scenario;
        Launcher = Path.Combine(folder, "op-launcher");
        File.WriteAllText(Launcher, $"#!/bin/sh\nPORTUNUS_FAKES_SCENARIO='{scenario}' exec '{Path.Combine(Fakes, "op")}' \"$@\"\n");
        File.SetUnixFileMode(Launcher, Executable);
    }

    /// <summary>The folder the fakes are built in.</summary>
    internal static string Fakes { get; } = Path.Combine(Harness.Repository, "tests", "Portunus.Fakes", "bin", "Release", "net10.0");

    /// <summary>The launcher, which <c>Portunus:Agent:OpPath</c> names.</summary>
    internal string Launcher { get; }

    /// <summary>The scenario file the fake answers from.</summary>
    internal string Scenario { get; }

    /// <summary>A fake that answers no command, which records nothing of <c>op</c>: every call exits 2 with nothing on its standard output.</summary>
    internal static FakeOp AnsweringNothing(AgentFiles files)
    {
        string folder = Path.Combine(files.Folder, "op");
        Directory.CreateDirectory(folder);
        string scenario = Path.Combine(folder, "scenario.json");
        File.WriteAllText(scenario, """{"recorded":false,"commands":[]}""");
        return new FakeOp(folder, scenario);
    }

    /// <summary>The arguments of each call the fake answered, in order.</summary>
    internal IReadOnlyList<string[]> Calls()
    {
        string calls = Scenario + ".calls";
        return File.Exists(calls)
            ? [.. File.ReadAllLines(calls).Select(line => JsonDocument.Parse(line).RootElement.GetProperty("arguments").EnumerateArray().Select(argument => argument.GetString()!).ToArray())]
            : [];
    }
}