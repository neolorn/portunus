using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Portunus.Repository.Tests;

/// <summary>
/// The layout of <c>06</c> PRT-ENG-001: one project per part under <c>src/</c>, one test
/// project per source project under <c>tests/</c>, the solution holding exactly those, flat
/// projects but for the named folders, and namespaces that follow the folders.
/// </summary>
internal static partial class LayoutRules
{
    private const string NuGetOrg = "https://api.nuget.org/v3/index.json";

    /// <summary>The folder of the siblings' packages, and its source's key in <c>nuget.config</c>.</summary>
    private const string VendorSource = "vendor";

    /// <summary>The list of the vendored files' SHA-256.</summary>
    private const string VendorSums = "SHA256SUMS";

    /// <summary>The identifiers <c>nuget.config</c> maps to <c>vendor/</c> (PRT-ENG-001, P-036).</summary>
    private static readonly string[] VendorPatterns = ["Janus.*", "Hermes.Core", "Themis.Core"];

    /// <summary>The files at the repository's root (06 PRT-ENG-001).</summary>
    private static readonly IReadOnlyList<string> RootFiles =
        ["Portunus.sln", "Directory.Build.props", "Directory.Packages.props", "global.json", ".editorconfig", ".gitignore", "nuget.config", "CHANGELOG.md", "RELEASE-NOTES.md"];

    /// <summary>
    /// The folders at the root: the projects, the sample host, the agent's unit files, the
    /// siblings' packages, the documents, the gates and the pipeline.
    /// </summary>
    private static readonly IReadOnlyList<string> RootFolders = ["src", "tests", "samples", "deploy", "vendor", "docs", "build", ".github"];

    /// <summary>Answers each project of the repository at <paramref name="root"/> that is outside the layout.</summary>
    internal static IReadOnlyList<string> ProjectViolations(string root)
    {
        List<string> violations = [];
        IReadOnlyList<ProjectFile> projects = ProjectFile.ReadAll(root);
        List<string> sources = [];
        List<string> tests = [];

        foreach (ProjectFile project in projects)
        {
            string[] parts = project.RelativePath.Split('/');
            bool placed = parts.Length == 3 && string.Equals(parts[1], project.Name, StringComparison.Ordinal);
            if (placed && string.Equals(parts[0], "src", StringComparison.Ordinal) && Packages.All.Contains(project.Name, StringComparer.Ordinal))
            {
                sources.Add(project.Name);
            }
            else if (placed && string.Equals(parts[0], "tests", StringComparison.Ordinal))
            {
                tests.Add(project.Name);
            }
            else if (!(parts.Length == 3 && string.Equals(parts[0], "samples", StringComparison.Ordinal)))
            {
                violations.Add($"{project.RelativePath} is not a part's project at src/<Part>/ or a test project at tests/<Project>/.");
            }
        }

        foreach (string test in tests)
        {
            bool known = test is Packages.RepositoryTests or Packages.Fakes or Packages.Harness
                || sources.Any(source => string.Equals(test, Packages.TestProject(source), StringComparison.Ordinal));
            if (!known)
            {
                violations.Add($"tests/{test} is the test project of no source project.");
            }
        }

        foreach (string source in sources.Where(source => !tests.Contains(Packages.TestProject(source), StringComparer.Ordinal)))
        {
            violations.Add($"src/{source} has no test project tests/{Packages.TestProject(source)}.");
        }

        if (!tests.Contains(Packages.RepositoryTests, StringComparer.Ordinal))
        {
            violations.Add($"tests/{Packages.RepositoryTests} is missing.");
        }

        foreach (string part in Packages.Present.Where(part => !sources.Contains(part, StringComparer.Ordinal)))
        {
            violations.Add($"src/{part} is missing, though {part} is a part of 00 section 2.2.");
        }

        violations.AddRange(SolutionViolations(root, [.. sources.Select(name => $"src/{name}/{name}.csproj"), .. tests.Select(name => $"tests/{name}/{name}.csproj")]));
        violations.AddRange(SolutionFileViolations(root));
        return violations;
    }

    /// <summary>Answers each folder inside a project, and each file's namespace or public type, outside the layout.</summary>
    internal static IReadOnlyList<string> FolderViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            IReadOnlyList<string> allowed = Packages.Folders.TryGetValue(project.Name, out IReadOnlyList<string>? folders) ? folders : [];
            foreach (string folder in Directory.EnumerateDirectories(project.Folder))
            {
                string name = Path.GetFileName(folder);
                if (name is "bin" or "obj")
                {
                    continue;
                }

                if (!allowed.Contains(name, StringComparer.Ordinal))
                {
                    violations.Add($"{project.RelativePath}: the folder {name} is not one the project may hold.");
                }
                else if (Directory.EnumerateDirectories(folder).Any())
                {
                    violations.Add($"{project.RelativePath}: the folder {name} holds a folder of its own.");
                }
            }

            violations.AddRange(SourceFileViolations(root, project));
        }

        return violations;
    }

    private static IEnumerable<string> SourceFileViolations(string root, ProjectFile project)
    {
        bool shipped = project.RelativePath.StartsWith("src/", StringComparison.Ordinal);
        foreach (string file in ProjectFile.Enumerate(project.Folder, "*.cs"))
        {
            string relative = Path.GetRelativePath(project.Folder, file).Replace('\\', '/');
            string folder = Path.GetDirectoryName(relative)!.Replace('\\', '/');
            string expected = folder.Length == 0 ? project.Name : project.Name + "." + folder.Replace('/', '.');
            string code = CSharpSource.Read(File.ReadAllText(file)).Code;
            string where = Path.GetRelativePath(root, file).Replace('\\', '/');

            Match declared = NamespacePattern().Match(code);
            if (!declared.Success)
            {
                yield return $"{where} declares no namespace.";
            }
            else if (!string.Equals(declared.Groups["name"].Value, expected, StringComparison.Ordinal))
            {
                yield return $"{where} declares the namespace {declared.Groups["name"].Value}, not {expected}.";
            }

            int publicTypes = PublicTopLevelTypes(code);
            if (publicTypes > 1)
            {
                yield return $"{where} declares more than one public top-level type.";
            }

            if (shipped && folder.Length > 0 && publicTypes > 0)
            {
                yield return $"{where} declares a public type outside the project's root folder.";
            }
        }
    }

    private static IEnumerable<string> SolutionViolations(string root, IReadOnlyList<string> expected)
    {
        string solution = Path.Combine(root, "Portunus.sln");
        if (!File.Exists(solution))
        {
            yield return "Portunus.sln is missing.";
            yield break;
        }

        HashSet<string> listed = [.. SolutionProjectPattern().Matches(File.ReadAllText(solution)).Select(match => match.Groups["path"].Value.Replace('\\', '/'))];
        foreach (string project in expected.Where(project => !listed.Contains(project)))
        {
            yield return $"Portunus.sln does not list {project}.";
        }

        foreach (string project in listed.Where(project => !expected.Contains(project, StringComparer.Ordinal)))
        {
            yield return $"Portunus.sln lists {project}, which is not a part's project or a test project.";
        }
    }

    /// <summary>
    /// Counts the public types declared at the namespace's own level, in a file-scoped
    /// namespace or inside the braces of a block-scoped one, never inside a type.
    /// </summary>
    private static int PublicTopLevelTypes(string code)
    {
        Stack<bool> blocks = new();
        int count = 0;
        foreach (Match token in StructurePattern().Matches(code))
        {
            if (token.Groups["namespace"].Success)
            {
                if (token.Value.EndsWith('{'))
                {
                    blocks.Push(true);
                }
            }
            else if (token.Value == "{")
            {
                blocks.Push(false);
            }
            else if (token.Value == "}")
            {
                _ = blocks.TryPop(out _);
            }
            else if (blocks.All(block => block))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Answers each file of <paramref name="tracked"/>, the paths Git tracks, outside the root
    /// PRT-ENG-001 lists, and each listed file missing.
    /// </summary>
    internal static IEnumerable<string> RootFileViolations(IReadOnlyCollection<string> tracked)
    {
        foreach (string file in RootFiles.Where(file => !tracked.Contains(file, StringComparer.Ordinal)))
        {
            yield return $"{file} is missing from the repository's root.";
        }

        foreach (string path in tracked.Order(StringComparer.Ordinal))
        {
            string[] parts = path.Split('/');
            if (parts.Length == 1 && !RootFiles.Contains(path, StringComparer.Ordinal))
            {
                yield return $"{path} sits at the repository's root, which holds only the files PRT-ENG-001 lists.";
            }
        }

        foreach (string folder in tracked.Where(path => path.Contains('/', StringComparison.Ordinal)).Select(path => path.Split('/')[0]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!RootFolders.Contains(folder, StringComparer.Ordinal))
            {
                yield return $"{folder}/ is a folder at the repository's root that PRT-ENG-001 does not list.";
            }
        }
    }

    /// <summary>
    /// Answers each part of the root <c>nuget.config</c> outside PRT-ENG-001: the sources
    /// cleared, then nuget.org and <c>vendor/</c>; the siblings' identifiers (<c>Janus.*</c>,
    /// <c>Hermes.Core</c>, <c>Themis.Core</c>) mapped to <c>vendor/</c> alone and every other
    /// identifier to nuget.org alone (P-036).
    /// </summary>
    internal static IEnumerable<string> PackageSourceViolations(string root)
    {
        string path = Path.Combine(root, "nuget.config");
        if (!File.Exists(path))
        {
            yield return "nuget.config is missing.";
            yield break;
        }

        XElement configuration = XDocument.Load(path).Root!;
        XElement? sources = configuration.Element("packageSources");
        List<XElement> entries = sources is null ? [] : [.. sources.Elements()];
        if (entries.Count == 0 || entries[0].Name.LocalName is not "clear")
        {
            yield return "nuget.config does not clear the package sources first.";
        }

        string[] added = [.. entries.Skip(1).Select(entry => $"{entry.Name.LocalName} {entry.Attribute("key")?.Value} {entry.Attribute("value")?.Value}")];
        if (!added.SequenceEqual([$"add nuget.org {NuGetOrg}", $"add {VendorSource} {VendorSource}"], StringComparer.Ordinal))
        {
            yield return $"nuget.config does not add nuget.org, {NuGetOrg}, and {VendorSource}/ as its two sources.";
        }

        Dictionary<string, string[]> mapped = [];
        foreach (XElement source in configuration.Element("packageSourceMapping")?.Elements() ?? [])
        {
            string[] patterns = [.. source.Elements().Select(package => package.Name.LocalName is "package" ? package.Attribute("pattern")?.Value ?? string.Empty : string.Empty)];
            mapped[source.Name.LocalName is "packageSource" ? source.Attribute("key")?.Value ?? string.Empty : string.Empty] = patterns;
        }

        bool mappedAsGiven = mapped.Count == 2
            && mapped.TryGetValue("nuget.org", out string[]? everything) && everything.SequenceEqual(["*"], StringComparer.Ordinal)
            && mapped.TryGetValue(VendorSource, out string[]? siblings) && siblings.Order(StringComparer.Ordinal).SequenceEqual(VendorPatterns.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        if (!mappedAsGiven)
        {
            yield return $"nuget.config does not map {string.Join(", ", VendorPatterns)} to {VendorSource}/ alone and every other identifier, \"*\", to nuget.org alone.";
        }

        foreach (XElement section in configuration.Elements().Where(section => section.Name.LocalName is not ("packageSources" or "packageSourceMapping")))
        {
            yield return $"nuget.config holds the section {section.Name.LocalName}, which PRT-ENG-001 does not give it.";
        }
    }

    /// <summary>
    /// Answers each file of <c>vendor/</c> whose SHA-256 differs from <c>vendor/SHA256SUMS</c>
    /// or that it does not list, and each line of it naming a file that is not there
    /// (PRT-ENG-001 criterion 1).
    /// </summary>
    internal static IEnumerable<string> VendorViolations(string root)
    {
        string folder = Path.Combine(root, VendorSource);
        if (!Directory.Exists(folder))
        {
            yield break;
        }

        string sums = Path.Combine(folder, VendorSums);
        Dictionary<string, string> listed = new(StringComparer.Ordinal);
        if (File.Exists(sums))
        {
            foreach (string line in File.ReadLines(sums).Where(line => line.Length > 0))
            {
                Match entry = SumLine().Match(line);
                if (!entry.Success || !listed.TryAdd(entry.Groups["file"].Value, entry.Groups["hash"].Value))
                {
                    yield return $"vendor/{VendorSums} holds the line \"{line}\", which is not one SHA-256 and one file.";
                }
            }
        }

        foreach (string file in Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>().Where(file => file != VendorSums).Order(StringComparer.Ordinal))
        {
            if (!listed.Remove(file, out string? hash))
            {
                yield return $"vendor/{file} is not listed in vendor/{VendorSums}.";
            }
            else
            {
                using FileStream stream = File.OpenRead(Path.Combine(folder, file));
                if (!string.Equals(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream)), hash, StringComparison.Ordinal))
                {
                    yield return $"vendor/{file}'s SHA-256 differs from vendor/{VendorSums}.";
                }
            }
        }

        foreach (string missing in listed.Keys.Order(StringComparer.Ordinal))
        {
            yield return $"vendor/{VendorSums} lists {missing}, which vendor/ does not hold.";
        }
    }
    /// <summary>Answers whether the root <c>.gitignore</c> lists <c>artifacts/packages/</c>, where the gate packs the packages.</summary>
    internal static IEnumerable<string> IgnoreViolations(string root)
    {
        string path = Path.Combine(root, ".gitignore");
        bool lists = File.Exists(path) && File.ReadLines(path).Select(line => line.Trim()).Any(line => line is "artifacts/packages/" or "/artifacts/packages/");
        if (!lists)
        {
            yield return ".gitignore does not list artifacts/packages/.";
        }
    }

    private static IEnumerable<string> SolutionFileViolations(string root)
    {
        IEnumerable<string> solutions = ProjectFile.Enumerate(root, "*.sln").Concat(ProjectFile.Enumerate(root, "*.slnx"));
        foreach (string solution in solutions)
        {
            string relative = Path.GetRelativePath(root, solution).Replace('\\', '/');
            if (relative is not "Portunus.sln" and not "samples/Samples.sln")
            {
                yield return $"{relative} is a solution other than Portunus.sln and samples/Samples.sln.";
            }
        }
    }

    [GeneratedRegex(@"^\s*namespace\s+(?<name>[\w.]+)", RegexOptions.Multiline)]
    private static partial Regex NamespacePattern();

    /// <summary>A namespace's declaration, a brace, or a public type's declaration.</summary>
    [GeneratedRegex(@"(?<namespace>\bnamespace\s+[\w.]+\s*[{;])|[{}]|\bpublic\s+(?:(?:abstract|sealed|static|partial|readonly|ref|unsafe|new)\s+)*(?:class|record|struct|interface|enum|delegate)\b")]
    private static partial Regex StructurePattern();

    [GeneratedRegex(@"^Project\(""\{[0-9A-F-]+\}""\)\s*=\s*""[^""]+"",\s*""(?<path>[^""]+\.csproj)""", RegexOptions.Multiline)]
    private static partial Regex SolutionProjectPattern();

    /// <summary>One line of <c>vendor/SHA256SUMS</c>, as <c>sha256sum</c> writes it: the hash, two spaces, the file.</summary>
    [GeneratedRegex(@"^(?<hash>[0-9a-f]{64})  (?<file>[A-Za-z0-9._-]+)$")]
    private static partial Regex SumLine();
}