using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Portunus.Agent.Tests;

/// <summary>
/// The fake <c>op</c>'s scenarios in <c>tests/Portunus.Fakes</c>, recorded by the owner's run
/// against the real <c>op</c> or written from it (P-028, P-043), and what their answers hold:
/// the identifiers <c>op</c> listed and the dummy values of the sample vaults' fields.
/// </summary>
internal static class Recording
{
    private const string NoteLabel = "notesPlain";

    /// <summary>The folder the scenarios and their files are in.</summary>
    internal static string Folder { get; } = Path.Combine(Harness.Repository, "tests", "Portunus.Fakes");

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