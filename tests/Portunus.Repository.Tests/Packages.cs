using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Portunus.Repository.Tests;

/// <summary>
/// The library's parts (<c>00</c> section 2.2, <c>04</c> PRT-LIB-001), each a project under
/// <c>src/</c>: the packages and the agent, which is an executable and no package.
/// </summary>
internal static class Packages
{
    internal const string Core = "Portunus.Core";

    internal const string Client = "Portunus";

    internal const string Janus = "Portunus.Janus";

    internal const string Hermes = "Portunus.Hermes";

    internal const string Themis = "Portunus.Themis";

    internal const string Testing = "Portunus.Testing";

    internal const string Cli = "Portunus.Cli";

    internal const string Agent = "Portunus.Agent";

    /// <summary>The packages' authors, the name the lead's first message of phase 0 gives (<c>06</c> PRT-ENG-011).</summary>
    internal const string Authors = "Hossam Ismail";

    /// <summary>The repository's address, which every package carries (<c>06</c> PRT-ENG-011).</summary>
    internal const string RepositoryUrl = "https://github.com/neolorn/portunus";

    /// <summary>The test project over the whole repository, the one with no source project.</summary>
    internal const string RepositoryTests = "Portunus.Repository.Tests";

    /// <summary>The fakes' console project, the fake <c>op</c> and <c>ssh</c> (<c>06</c> PRT-ENG-001).</summary>
    internal const string Fakes = "Portunus.Fakes";

    /// <summary>The console project the agent's signal tests run (<c>06</c> PRT-ENG-001, P-027).</summary>
    internal const string Harness = "Portunus.Agent.Harness";

    /// <summary>Every part, in the order of <c>00</c> section 2.2.</summary>
    internal static IReadOnlyList<string> All { get; } = [Core, Client, Janus, Hermes, Themis, Testing, Cli, Agent];

    /// <summary>The packed parts, the packages: every part but the agent, which is published as an executable.</summary>
    internal static IReadOnlyList<string> Packed { get; } = [Core, Client, Janus, Hermes, Themis, Testing, Cli];

    /// <summary>
    /// The parts whose projects the repository holds: from phase 0, and the agent's from phase 3;
    /// the tool's from phase 4; the adapters' from phase 5, Themis's from phase 6 (plan).
    /// </summary>
    internal static IReadOnlyList<string> Present { get; } = [Core, Client, Janus, Hermes, Themis, Testing, Cli, Agent];

    /// <summary>The folders a project may hold, by project (<c>06</c> PRT-ENG-001); every other project is flat.</summary>
    internal static FrozenDictionary<string, IReadOnlyList<string>> Folders { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [Agent] = ["Fetch", "Serving"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static string TestProject(string part)
    {
        return part + ".Tests";
    }
}