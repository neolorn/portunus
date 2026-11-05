using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace Portunus.Repository.Tests;

/// <summary>
/// The third-party packages of <c>06</c> PRT-ENG-009, the packages no test may use
/// (PRT-ENG-008), and the packed packages' properties (PRT-ENG-011).
/// </summary>
internal static class PackageRules
{
    /// <summary>The packages of PRT-ENG-009's table.</summary>
    internal static IReadOnlyList<string> Allowed { get; } =
    [
        .. ReferenceRules.HostingAbstractions,
        .. ReferenceRules.Hosting,
        .. ReferenceRules.Siblings,
        .. JanusClosure.Pinned.Keys,
        ReferenceRules.PublicApiAnalyzers,
        ReferenceRules.MinVer,
        .. ReferenceRules.TestPackages,

        // The library's own packages, which the sample host alone references (04 PRT-LIB-001).
        .. Packages.Packed,
    ];

    /// <summary>Mocking packages, none of which a project may take, directly or not.</summary>
    internal static IReadOnlyList<string> Mocking { get; } =
        ["Moq", "NSubstitute", "FakeItEasy", "RhinoMocks", "Rhino.Mocks", "JustMock", "Telerik.JustMock", "NMock", "NMock3", "Typemock.Isolator", "Microsoft.QualityTools.Testing.Fakes"];

    /// <summary>The elements of a packed package's metadata: its identifier, version, authors, description, repository and dependencies.</summary>
    private static readonly FrozenSet<string> ManifestElements =
        FrozenSet.Create(StringComparer.Ordinal, "id", "version", "authors", "description", "repository", "dependencies");

    /// <summary>The element <c>Portunus.Cli</c> alone carries, its package type as a .NET tool, which the SDK writes of its own accord.</summary>
    private const string ToolPackageType = "packageTypes";

    /// <summary>The siblings' packages at the versions of <c>07</c> section 7 (P-022, P-036).</summary>
    internal static FrozenDictionary<string, string> SiblingVersions { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Janus.Hosting"] = "1.0.0-rc.4",
        ["Hermes.Core"] = "1.0.0-rc.6",
        ["Themis.Core"] = "1.0.0-rc.2",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Answers each dependency of a packed package on another of the library's packages that is
    /// not the exact version packed, <c>[v]</c>, and of an adapter on its sibling's package that
    /// is not the exact version of <c>07</c> section 7 (<c>04</c> PRT-LIB-001 criterion 2).
    /// </summary>
    internal static IEnumerable<string> DependencyViolations(IReadOnlyList<PackageManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        Dictionary<string, string> packed = manifests.ToDictionary(manifest => manifest.Id, manifest => manifest.Version, StringComparer.Ordinal);
        foreach (PackageManifest manifest in manifests)
        {
            foreach (PackageDependency dependency in manifest.Dependencies.Where(dependency => packed.ContainsKey(dependency.Id) || Packages.Packed.Contains(dependency.Id, StringComparer.Ordinal)))
            {
                if (!packed.TryGetValue(dependency.Id, out string? version))
                {
                    yield return $"{manifest.Id} depends on {dependency.Id}, which was not packed with it.";
                }
                else if (!string.Equals(dependency.Version, $"[{version}]", StringComparison.Ordinal))
                {
                    yield return $"{manifest.Id} depends on {dependency.Id} at {dependency.Version}, not the exact version packed, [{version}].";
                }
            }

            foreach (PackageDependency dependency in manifest.Dependencies.Where(dependency => SiblingVersions.ContainsKey(dependency.Id)))
            {
                string pinned = SiblingVersions[dependency.Id];
                if (!string.Equals(dependency.Version, $"[{pinned}]", StringComparison.Ordinal))
                {
                    yield return $"{manifest.Id} depends on {dependency.Id} at {dependency.Version}, not the exact version of 07 section 7, [{pinned}].";
                }
            }
        }
    }

    /// <summary>
    /// Answers each property of a packed package outside <c>06</c> PRT-ENG-011: its description
    /// is its cell of <c>00</c> section 2.2, its authors and repository are those the lead's
    /// first message of phase 0 gives, and it carries no other package property.
    /// </summary>
    internal static IEnumerable<string> ManifestViolations(PackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        bool tool = string.Equals(manifest.Id, Packages.Cli, StringComparison.Ordinal);
        foreach (string element in manifest.Elements.Where(element => !ManifestElements.Contains(element) && !(tool && string.Equals(element, ToolPackageType, StringComparison.Ordinal))))
        {
            yield return $"{manifest.Id} carries the package property {element}, which is left unset.";
        }

        if (!string.Equals(manifest.Authors, Packages.Authors, StringComparison.Ordinal))
        {
            yield return $"{manifest.Id}'s authors are {manifest.Authors}, not {Packages.Authors}.";
        }

        if (!PackageTable.Descriptions.TryGetValue(manifest.Id, out string? description) || !string.Equals(manifest.Description, description, StringComparison.Ordinal))
        {
            yield return $"{manifest.Id}'s description is not its cell of 00 section 2.2.";
        }

        if (!string.Equals(manifest.RepositoryUrl, Packages.RepositoryUrl, StringComparison.Ordinal))
        {
            yield return $"{manifest.Id}'s repository is {manifest.RepositoryUrl}, not {Packages.RepositoryUrl}.";
        }
    }

    /// <summary>Answers each package of the repository at <paramref name="root"/> outside PRT-ENG-009.</summary>
    internal static IReadOnlyList<string> AllowListViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            // A reference made outside the project file reaches its lockfile's direct entries.
            IEnumerable<string> direct = project.PackageReferences.Select(ProjectFile.Include).Union(project.LockedDirectPackages, StringComparer.Ordinal);
            foreach (string package in direct.Where(package => !Allowed.Contains(package, StringComparer.Ordinal)))
            {
                violations.Add($"{project.RelativePath} references {package}, which is not in PRT-ENG-009's table.");
            }
        }

        foreach (string package in PackageVersions(root).Where(package => !Allowed.Contains(package, StringComparer.Ordinal)))
        {
            violations.Add($"Directory.Packages.props names {package}, which is not in PRT-ENG-009's table.");
        }

        return violations;
    }

    /// <summary>Answers each mocking package a project of the repository at <paramref name="root"/> takes, directly or through another.</summary>
    internal static IReadOnlyList<string> MockingViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            IEnumerable<string> taken = project.PackageReferences.Select(ProjectFile.Include);
            string lockFile = Path.Combine(project.Folder, "packages.lock.json");
            if (File.Exists(lockFile))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(lockFile));
                taken = taken.Concat([.. document.RootElement.GetProperty("dependencies").EnumerateObject().SelectMany(framework => framework.Value.EnumerateObject().Select(package => package.Name))]);
            }

            foreach (string package in taken.Distinct(StringComparer.OrdinalIgnoreCase).Where(package => Mocking.Contains(package, StringComparer.OrdinalIgnoreCase)))
            {
                violations.Add($"{project.RelativePath} takes the mocking package {package}.");
            }
        }

        return violations;
    }

    private static IReadOnlyList<string> PackageVersions(string root)
    {
        string props = Path.Combine(root, "Directory.Packages.props");
        if (!File.Exists(props))
        {
            return [];
        }

        return [.. XDocument.Load(props).Root!.Elements("ItemGroup").Elements("PackageVersion").Select(ProjectFile.Include)];
    }
}