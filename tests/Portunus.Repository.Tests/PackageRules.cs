using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace Portunus.Repository.Tests;

/// <summary>
/// The third-party packages of <c>06</c> PRT-ENG-009 and the packages no test may use
/// (PRT-ENG-008).
/// </summary>
internal static class PackageRules
{
    /// <summary>The packages of PRT-ENG-009's table.</summary>
    internal static IReadOnlyList<string> Allowed { get; } =
    [
        .. ReferenceRules.HostingAbstractions,
        .. ReferenceRules.Hosting,
        .. ReferenceRules.Siblings,
        ReferenceRules.PublicApiAnalyzers,
        ReferenceRules.MinVer,
        .. ReferenceRules.TestPackages,
    ];

    /// <summary>Mocking packages, none of which a project may take, directly or not.</summary>
    internal static IReadOnlyList<string> Mocking { get; } =
        ["Moq", "NSubstitute", "FakeItEasy", "RhinoMocks", "Rhino.Mocks", "JustMock", "Telerik.JustMock", "NMock", "NMock3", "Typemock.Isolator", "Microsoft.QualityTools.Testing.Fakes"];

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