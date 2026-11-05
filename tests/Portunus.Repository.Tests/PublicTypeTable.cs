using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Portunus.Repository.Tests;

/// <summary>
/// The public types the tables of <c>04</c> PRT-LIB-002 and PRT-LIB-008 give, by part, each in
/// its part's namespace (<c>06</c> PRT-ENG-001), as the public API files write them. The tool
/// and the agent are executables and have no public types.
/// </summary>
internal static class PublicTypeTable
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> Types = new Dictionary<string, string[]>
    {
        [Packages.Core] =
        [
            "SecretAddress", "Secret", "AgentStatus",
            "Result", "Result`1", "Failure",
            "SecretsStartupException",
            "ISecretClient",
            "AgentState", "WaitReason", "SetSource",
        ],
        [Packages.Client] = ["SecretsServiceCollectionExtensions"],
        [Packages.Janus] = ["JanusSecretsServiceCollectionExtensions"],
        [Packages.Hermes] = ["HermesSecretsServiceCollectionExtensions"],
        [Packages.Themis] = ["ThemisSecretsServiceCollectionExtensions"],
        [Packages.Testing] = ["FixedSecretClient", "SecretsTestingServiceCollectionExtensions"],
        [Packages.Cli] = [],
        [Packages.Agent] = [],
    }.ToFrozenDictionary(
        entry => entry.Key,
        entry => entry.Value.Select(type => entry.Key + "." + type).ToFrozenSet(StringComparer.Ordinal),
        StringComparer.Ordinal);

    /// <summary>Answers each type the tables give <paramref name="part"/> that <paramref name="types"/> does not hold.</summary>
    internal static IEnumerable<string> MissingTypes(string part, IEnumerable<PublicType> types)
    {
        HashSet<string> declared = [.. types.Select(type => type.Name)];
        return Types.GetValueOrDefault(part, FrozenSet<string>.Empty)
            .Where(name => !declared.Contains(name))
            .Order(StringComparer.Ordinal)
            .Select(name => $"{part} does not declare the public type {name}, which 04 PRT-LIB-002 gives.");
    }

    /// <summary>Answers each public type of <paramref name="part"/> whose name the tables do not give.</summary>
    internal static IEnumerable<string> Violations(string part, IEnumerable<PublicType> types)
    {
        FrozenSet<string> given = Types.GetValueOrDefault(part, FrozenSet<string>.Empty);
        return types.Select(type => type.Name)
            .Where(name => !given.Contains(name))
            .Order(StringComparer.Ordinal)
            .Select(name => $"{part} declares the public type {name}, which no table of 04 PRT-LIB-002 or PRT-LIB-008 gives.");
    }
}