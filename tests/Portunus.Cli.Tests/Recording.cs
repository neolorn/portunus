using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Portunus.Cli.Tests;

/// <summary>
/// The fake <c>op</c>'s scenarios in <c>tests/Portunus.Fakes</c>, recorded by the owner's run
/// against the real <c>op</c> or written from it (P-028, P-043), and what their answers hold:
/// the identifiers <c>op</c> listed and the dummy values of the sample vaults' fields.
/// </summary>
internal static class Recording
{
    private const string NoteLabel = "notesPlain";

    /// <summary>The folder the scenarios and their files are in.</summary>
    internal static string Folder { get; } = Path.Combine(Fakes.Repository, "tests", "Portunus.Fakes");

    /// <summary>
    /// Copies the scenario <paramref name="scenario"/> and every file it names into
    /// <paramref name="folder"/>, where the fakes keep their calls beside it, and answers the
    /// copy's path.
    /// </summary>
    internal static string Copy(string scenario, string folder)
    {
        string copy = Path.Combine(folder, scenario + ".json");
        File.Copy(Path.Combine(Folder, scenario + ".json"), copy);
        using JsonDocument read = JsonDocument.Parse(File.ReadAllBytes(copy));
        foreach (JsonElement command in read.RootElement.GetProperty("commands").EnumerateArray())
        {
            foreach (string member in new[] { "output", "error" })
            {
                if (command.GetProperty(member).GetString() is string file)
                {
                    File.Copy(Path.Combine(Folder, file), Path.Combine(folder, file), overwrite: true);
                }
            }
        }

        return copy;
    }

    /// <summary>Each entry of the recorded list <paramref name="file"/>, in order: the item's identifier and its vault's.</summary>
    internal static IReadOnlyList<(string Item, string Vault)> Listed(string file)
    {
        using JsonDocument list = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder, file)));
        return [.. list.RootElement.EnumerateArray().Select(entry => (entry.GetProperty("id").GetString()!, entry.GetProperty("vault").GetProperty("id").GetString()!))];
    }

    /// <summary>The value of the field <paramref name="label"/> of the item <paramref name="title"/> in the recorded items of <paramref name="scenario"/>.</summary>
    internal static byte[] Value(string scenario, string title, string label)
    {
        return Fields(scenario).Single(field => field.Title == title && field.Label == label).Value;
    }

    /// <summary>Every secret value of the recorded items of <paramref name="scenario"/>: the notes and each set's <c>.current</c>, a version's number, left out.</summary>
    internal static IReadOnlyList<byte[]> Values(string scenario)
    {
        return [.. Fields(scenario).Where(field => !field.Label.EndsWith(".current", StringComparison.Ordinal)).Select(field => field.Value)];
    }

    /// <summary>
    /// The arguments, the environment's names and the process of each call the fakes kept beside
    /// <paramref name="scenario"/>, in order, a line a fake is still writing left out.
    /// </summary>
    internal static IReadOnlyList<(string[] Arguments, string[] Environment, int ProcessId)> Calls(string scenario)
    {
        List<(string[], string[], int)> read = [];
        string calls = scenario + ".calls";
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
            read.Add((Strings(call.RootElement.GetProperty("arguments")), Strings(call.RootElement.GetProperty("environment")), call.RootElement.GetProperty("processId").GetInt32()));
        }

        return read;
    }

    private static string[] Strings(JsonElement array)
    {
        return [.. array.EnumerateArray().Select(element => element.GetString()!)];
    }

    private static IEnumerable<(string Title, string Label, byte[] Value)> Fields(string scenario)
    {
        foreach (string file in Directory.EnumerateFiles(Folder, scenario + ".item-*.out").Order(StringComparer.Ordinal))
        {
            using JsonDocument item = JsonDocument.Parse(File.ReadAllBytes(file));
            string title = item.RootElement.GetProperty("title").GetString()!;
            foreach (JsonElement field in item.RootElement.GetProperty("fields").EnumerateArray())
            {
                string label = field.GetProperty("label").GetString()!;
                if (label != NoteLabel && field.TryGetProperty("value", out JsonElement value))
                {
                    yield return (title, label, Encoding.UTF8.GetBytes(value.GetString()!));
                }
            }
        }
    }
}