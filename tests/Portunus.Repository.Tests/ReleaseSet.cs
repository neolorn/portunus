using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Portunus.Repository.Tests;

/// <summary>
/// What the release gate compares beside the public surface (<c>04</c> PRT-LIB-007): the codes,
/// the configuration keys, the enumerations' spellings, the wire formats' members and the
/// executables' commands and options, as <c>build/gates/release-baseline.json</c> holds them.
/// </summary>
internal sealed partial record ReleaseSet(
    IReadOnlyCollection<string> Codes,
    IReadOnlyCollection<string> ConfigurationKeys,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Enumerations,
    IReadOnlyCollection<string> WireFormats,
    IReadOnlyCollection<string> Commands)
{
    /// <summary>The types of <c>Portunus.Core</c> that write and read the wire formats of <c>07</c> section 6, the development file's among them.</summary>
    internal static readonly string[] FormatTypes =
    [
        "Portunus.Core.RequestFormat",
        "Portunus.Core.AnswerFormat",
        "Portunus.Core.PushAnswerFormat",
        "Portunus.Core.WireJson",
        "Portunus.Core.DevelopmentFile",
    ];

    /// <summary>Each executable's command (<c>07</c> section 7), by the type that reads its command line.</summary>
    internal static readonly (string Assembly, string Type, string Command)[] CommandTypes =
    [
        (Packages.Cli, "Portunus.Cli.ToolArguments", "portunus"),
        (Packages.Agent, "Portunus.Agent.AgentCommand", "portunus-agent"),
    ];

    /// <summary>The set before the first final release, which holds nothing.</summary>
    internal static ReleaseSet Empty { get; } = new([], [], new Dictionary<string, IReadOnlyDictionary<string, string>>(), [], []);

    /// <summary>
    /// The set the built assemblies beside the test run hold, read from their metadata as the
    /// contract tests of <c>06</c> PRT-ENG-007 read it: each string of a code's shape and of a
    /// configuration key's shape that a shipped assembly holds; each public enumeration's
    /// members with their spellings; each string of a member's shape that a format's type loads,
    /// as <c>Type member</c>; and each string constant of a command's or an option's shape that
    /// an executable's command line type declares, as <c>command word</c>.
    /// </summary>
    internal static ReleaseSet Read()
    {
        HashSet<string> codes = new(StringComparer.Ordinal);
        HashSet<string> keys = new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyDictionary<string, string>> enumerations = new(StringComparer.Ordinal);
        foreach (string assembly in AssemblySurface.ShippedAssemblies())
        {
            IReadOnlyList<string> strings = AssemblySurface.Strings(assembly);
            codes.UnionWith(strings.Where(NamingRules.IsCode));
            keys.UnionWith(strings.Where(NamingRules.IsKey));
            foreach ((string type, IReadOnlyList<(string Name, string? Spelling)> members) in AssemblySurface.Enumerations(assembly))
            {
                enumerations[type[(type.LastIndexOf('.') + 1)..]] = members.ToDictionary(member => member.Name, member => member.Spelling ?? string.Empty, StringComparer.Ordinal);
            }
        }

        IReadOnlyDictionary<string, IReadOnlySet<string>> core = AssemblySurface.TypeStrings(Built(Packages.Core));
        string[] formats = [.. FormatTypes.SelectMany(type => core.GetValueOrDefault(type, new HashSet<string>()).Where(text => MemberPattern().IsMatch(text)).Select(text => $"{type[(type.LastIndexOf('.') + 1)..]} {text}"))];

        List<string> commands = [];
        foreach ((string assembly, string type, string command) in CommandTypes)
        {
            commands.AddRange(AssemblySurface.StringConstants(Built(assembly))
                .Where(constant => string.Equals(constant.Type, type, StringComparison.Ordinal) && CommandPattern().IsMatch(constant.Value))
                .Select(constant => $"{command} {constant.Value}"));
        }

        return new ReleaseSet(codes, keys, enumerations, formats, commands);
    }

    internal static ReleaseSet Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new ReleaseSet(
            Strings(Member(root, "codes")),
            Strings(Member(root, "configurationKeys")),
            Member(root, "enumerations").EnumerateObject().ToDictionary(
                enumeration => enumeration.Name,
                enumeration => (IReadOnlyDictionary<string, string>)enumeration.Value.EnumerateObject().ToDictionary(member => member.Name, member => member.Value.GetString()!, StringComparer.Ordinal),
                StringComparer.Ordinal),
            Strings(Member(root, "wireFormats")),
            Strings(Member(root, "commands")));
    }

    /// <summary>The set as the release commit writes it: each list and object in ordinal order, indented by two spaces, ending in a line feed.</summary>
    internal string Write()
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            WriteList(writer, "codes", Codes);
            WriteList(writer, "configurationKeys", ConfigurationKeys);
            writer.WriteStartObject("enumerations");
            foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> enumeration in Enumerations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(enumeration.Key);
                foreach (KeyValuePair<string, string> member in enumeration.Value.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(member.Key, member.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            WriteList(writer, "wireFormats", WireFormats);
            WriteList(writer, "commands", Commands);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void WriteList(Utf8JsonWriter writer, string name, IReadOnlyCollection<string> values)
    {
        writer.WriteStartArray(name);
        foreach (string value in values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static string[] Strings(JsonElement list)
    {
        return [.. list.EnumerateArray().Select(value => value.GetString()!)];
    }

    private static JsonElement Member(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement member) ? member : throw new InvalidDataException($"The release baseline has no member {name}.");
    }

    private static string Built(string part)
    {
        return Path.Combine(AppContext.BaseDirectory, part + ".dll");
    }

    /// <summary>A JSON member's name, or a closed value written in its place: a lower-case letter, then letters and digits.</summary>
    [GeneratedRegex(@"\A[a-z][A-Za-z0-9]*\z")]
    private static partial Regex MemberPattern();

    /// <summary>A command, lower-case letters, or an option, <c>--</c> and lower-case letters.</summary>
    [GeneratedRegex(@"\A(?:--)?[a-z]+\z")]
    private static partial Regex CommandPattern();
}