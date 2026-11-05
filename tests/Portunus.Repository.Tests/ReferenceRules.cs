using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Portunus.Repository.Tests;

/// <summary>
/// What each project may reference (<c>04</c> PRT-LIB-001) and the grants of
/// <c>InternalsVisibleTo</c> (<c>06</c> PRT-ENG-001).
/// </summary>
internal static class ReferenceRules
{
    internal const string PublicApiAnalyzers = "Microsoft.CodeAnalysis.PublicApiAnalyzers";

    internal const string MinVer = "MinVer";

    /// <summary>The Microsoft.Extensions abstractions of <c>06</c> PRT-ENG-009, which <c>Portunus</c> may reference.</summary>
    internal static IReadOnlyList<string> HostingAbstractions { get; } =
    [
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.Configuration.Binder",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Options.ConfigurationExtensions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Hosting.Abstractions",
        "Microsoft.Extensions.Diagnostics.HealthChecks",
    ];

    /// <summary>The hosting packages of <c>06</c> PRT-ENG-009, which <c>Portunus.Agent</c> may reference.</summary>
    internal static IReadOnlyList<string> Hosting { get; } =
        ["Microsoft.Extensions.Hosting", "Microsoft.Extensions.Hosting.Systemd", "Microsoft.Extensions.Configuration.Json"];

    /// <summary>The siblings' <c>Core</c> packages of <c>06</c> PRT-ENG-009, each of which its adapter alone may reference.</summary>
    internal static IReadOnlyList<string> Siblings { get; } = ["Janus.Core", "Hermes.Core", "Themis.Core"];

    /// <summary>The test packages of <c>06</c> PRT-ENG-009.</summary>
    internal static IReadOnlyList<string> TestPackages { get; } = ["xunit.v3"];

    /// <summary>The test projects that may also reference <c>Microsoft.Extensions.Hosting</c>, to run a real host (<c>06</c> PRT-ENG-009, P-025).</summary>
    private static readonly IReadOnlyList<string> HostTestProjects = [Packages.TestProject(Packages.Client), Packages.TestProject(Packages.Testing)];

    /// <summary>
    /// The library's own projects each part's project may reference, by the rows phase 0 builds:
    /// the agent's row comes in phase 3, the tool's in phase 4 and the adapters' in phase 5 (plan).
    /// </summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<string>> AllowedProjects = new Dictionary<string, IReadOnlyList<string>>
    {
        [Packages.Core] = [],
        [Packages.Client] = [Packages.Core],
        [Packages.Testing] = [Packages.Core, Packages.Client],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The packages each part's project may reference at run time, by the same rows.</summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<string>> AllowedPackages = new Dictionary<string, IReadOnlyList<string>>
    {
        [Packages.Core] = [],
        [Packages.Client] = HostingAbstractions,
        [Packages.Testing] = [],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Answers each reference of a project of the repository at <paramref name="root"/> that PRT-LIB-001 does not allow.</summary>
    internal static IReadOnlyList<string> ReferenceViolations(string root)
    {
        List<string> violations = [];

        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            bool shipped = project.RelativePath.StartsWith("src/", StringComparison.Ordinal);
            bool test = project.RelativePath.StartsWith("tests/", StringComparison.Ordinal);
            if (!shipped && !test)
            {
                continue;
            }

            IReadOnlyList<string> allowedProjects = shipped ? AllowedProjects.GetValueOrDefault(project.Name, []) : AllowedTestProjects(project.Name);
            IReadOnlyList<string> allowedPackages = shipped ? AllowedPackages.GetValueOrDefault(project.Name, []) : AllowedTestPackages(project.Name);

            foreach (string reference in project.ProjectReferences.Where(reference => !allowedProjects.Contains(reference, StringComparer.Ordinal)))
            {
                violations.Add($"{project.RelativePath} references the project {reference}, which PRT-LIB-001 does not allow it.");
            }

            List<string> buildOnly = [];
            foreach (XElement reference in project.PackageReferences)
            {
                string package = ProjectFile.Include(reference);
                if (IsBuildOnly(project, package))
                {
                    buildOnly.Add(package);
                    if (!string.Equals(reference.Attribute("PrivateAssets")?.Value, "all", StringComparison.Ordinal))
                    {
                        violations.Add($"{project.RelativePath} references {package} without PrivateAssets=\"all\", though it is build-only.");
                    }
                }
                else if (!allowedPackages.Contains(package, StringComparer.Ordinal))
                {
                    violations.Add($"{project.RelativePath} references the package {package}, which PRT-LIB-001 does not allow it.");
                }
            }

            HashSet<string> referenced = [.. project.PackageReferences.Select(ProjectFile.Include)];
            foreach (string package in project.LockedDirectPackages.Where(package => !referenced.Contains(package)))
            {
                violations.Add($"{project.RelativePath} takes the package {package} by a reference outside its project file, which PRT-LIB-001 does not allow it.");
            }

            if (shipped)
            {
                foreach (string required in RequiredBuildOnly(project.Name).Where(required => !buildOnly.Contains(required, StringComparer.Ordinal)))
                {
                    violations.Add($"{project.RelativePath} does not reference {required} as a build-only reference.");
                }
            }

            // No row of the table gives a project of src/ or tests/ a shared framework.
            foreach (string framework in project.FrameworkReferences)
            {
                violations.Add($"{project.RelativePath} references the framework {framework}, which PRT-LIB-001 does not allow it.");
            }
        }

        violations.AddRange(SharedItemViolations(root));
        return violations;
    }

    /// <summary>
    /// Answers each item of <c>Directory.Build.props</c> but the CLSCompliant attribute and the
    /// exact-version target's update (<c>06</c> PRT-ENG-011), and each item of
    /// <c>Directory.Packages.props</c> but a <c>PackageVersion</c>, since such an item would reach
    /// every project unseen.
    /// </summary>
    private static IEnumerable<string> SharedItemViolations(string root)
    {
        string buildProps = Path.Combine(root, "Directory.Build.props");
        if (File.Exists(buildProps))
        {
            foreach (XElement item in XDocument.Load(buildProps).Descendants("ItemGroup").Elements())
            {
                string name = item.Name.LocalName;
                XElement? owner = item.Parent!.Parent;
                bool attribute = owner == item.Document!.Root && string.Equals(name, "AssemblyAttribute", StringComparison.Ordinal)
                    && string.Equals(item.Attribute("Include")?.Value, "System.CLSCompliantAttribute", StringComparison.Ordinal);
                bool exactVersion = owner is { Name.LocalName: "Target" } && string.Equals(owner.Attribute("Name")?.Value, "DependOnTheExactVersionPacked", StringComparison.Ordinal)
                    && string.Equals(name, "_ProjectReferencesWithVersions", StringComparison.Ordinal);
                if (!attribute && !exactVersion)
                {
                    yield return $"Directory.Build.props adds a {name} item {Target(item)}; it adds no item but the CLSCompliant attribute and the exact-version target's update.";
                }
            }
        }

        string packagesProps = Path.Combine(root, "Directory.Packages.props");
        if (File.Exists(packagesProps))
        {
            foreach (XElement item in XDocument.Load(packagesProps).Descendants("ItemGroup").Elements().Where(item => item.Name.LocalName is not "PackageVersion"))
            {
                yield return $"Directory.Packages.props adds a {item.Name.LocalName} item {Target(item)}; it adds no item but PackageVersion.";
            }
        }
    }

    private static string Target(XElement item)
    {
        return item.Attribute("Include")?.Value ?? item.Attribute("Update")?.Value ?? string.Empty;
    }

    /// <summary>
    /// Answers each grant of <c>InternalsVisibleTo</c> outside <c>06</c> PRT-ENG-001's rule that
    /// the built assembly of a project of the repository at <paramref name="root"/> carries,
    /// whatever form declared it: an item, an attribute item or an attribute in a source file.
    /// </summary>
    internal static IReadOnlyList<string> GrantViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            string assembly = Path.Combine(project.Folder, "bin", "Release", "net10.0", project.Name + ".dll");
            if (!File.Exists(assembly))
            {
                violations.Add($"{project.RelativePath} has no built assembly at bin/Release/net10.0/, so its grants cannot be read.");
                continue;
            }

            violations.AddRange(GrantViolations(project.RelativePath, project.Name, AssemblySurface.Grants(assembly)));
        }

        return violations;
    }

    /// <summary>Answers each of <paramref name="grantees"/> the project at <paramref name="relativePath"/> may not grant its internals to.</summary>
    internal static IEnumerable<string> GrantViolations(string relativePath, string name, IEnumerable<string> grantees)
    {
        return grantees.Where(grantee => !IsAllowedGrant(relativePath, name, grantee))
            .Select(grantee => $"{relativePath} grants its internals to {grantee}, which PRT-ENG-001 does not allow.");
    }

    /// <summary>
    /// A source project grants its own test project; <c>Portunus.Core</c> grants <c>Portunus</c>,
    /// <c>Portunus.Agent</c>, <c>Portunus.Cli</c> and <c>Portunus.Testing</c>, which share its
    /// layout validator, its address and name rules and its wire formats; and <c>Portunus</c>
    /// grants <c>Portunus.Testing</c>, which removes the client's registration in favour of the
    /// fixed one.
    /// </summary>
    private static bool IsAllowedGrant(string relativePath, string name, string grantee)
    {
        if (!relativePath.StartsWith("src/", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(grantee, Packages.TestProject(name), StringComparison.Ordinal))
        {
            return true;
        }

        IReadOnlyList<string> allowed = name switch
        {
            Packages.Core => [Packages.Client, Packages.Agent, Packages.Cli, Packages.Testing],
            Packages.Client => [Packages.Testing],
            _ => [],
        };
        return allowed.Contains(grantee, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> AllowedTestPackages(string testProject)
    {
        return HostTestProjects.Contains(testProject, StringComparer.Ordinal) ? [.. TestPackages, "Microsoft.Extensions.Hosting"] : TestPackages;
    }

    private static IReadOnlyList<string> AllowedTestProjects(string testProject)
    {
        // The repository's tests read every shipped project's built assembly (06 PRT-ENG-004).
        if (string.Equals(testProject, Packages.RepositoryTests, StringComparison.Ordinal))
        {
            return Packages.All;
        }

        string source = testProject.EndsWith(".Tests", StringComparison.Ordinal) ? testProject[..^".Tests".Length] : testProject;
        return [source, Packages.Testing];
    }

    /// <summary>
    /// The build-only references of a shipped project: the public API analysers for every one,
    /// and MinVer for every one that is packed, which the agent is not.
    /// </summary>
    private static IReadOnlyList<string> RequiredBuildOnly(string name)
    {
        return Packages.Packed.Contains(name, StringComparer.Ordinal) ? [PublicApiAnalyzers, MinVer] : [PublicApiAnalyzers];
    }

    private static bool IsBuildOnly(ProjectFile project, string package)
    {
        bool shipped = project.RelativePath.StartsWith("src/", StringComparison.Ordinal);
        return shipped && RequiredBuildOnly(project.Name).Contains(package, StringComparer.Ordinal);
    }
}