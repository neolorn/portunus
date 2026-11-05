using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Portunus.Repository.Tests;

/// <summary>
/// The native code of <c>06</c> PRT-ENG-004: called by the agent alone, for <c>prctl</c>
/// alone, through a source-generated <c>LibraryImport</c>. The built assemblies tell which
/// functions are imported; the sources tell how, since a <c>LibraryImport</c> of a function
/// whose arguments need no marshalling is built as the same metadata a <c>DllImport</c> is.
/// </summary>
internal static partial class NativeRules
{
    /// <summary>The one native function the agent imports (<c>01</c> PRT-AGT-001).</summary>
    internal const string Function = "prctl";

    /// <summary>Answers each native import of <paramref name="assembly"/> but the agent's import of <see cref="Function"/>.</summary>
    internal static IEnumerable<string> ImportViolations(string assembly, IEnumerable<NativeImport> imports)
    {
        ArgumentNullException.ThrowIfNull(imports);
        bool agent = string.Equals(Path.GetFileNameWithoutExtension(assembly), Packages.Agent, StringComparison.Ordinal);
        foreach (NativeImport import in imports)
        {
            if (!agent || !string.Equals(import.Function, Function, StringComparison.Ordinal))
            {
                yield return $"{assembly} imports the native function {import.Function} of {import.Library}, in {import.Method}; only the agent imports native code, prctl alone.";
            }
        }
    }

    /// <summary>
    /// Answers each source file of a project under <c>src/</c> of the repository at
    /// <paramref name="root"/> that declares a <c>DllImport</c>, and each outside the agent that
    /// declares a <c>LibraryImport</c>, comments and literals left out.
    /// </summary>
    internal static IReadOnlyList<string> SourceViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            if (!project.RelativePath.StartsWith("src/", StringComparison.Ordinal))
            {
                continue;
            }

            bool agent = string.Equals(project.Name, Packages.Agent, StringComparison.Ordinal);
            foreach (string file in ProjectFile.Enumerate(project.Folder, "*.cs"))
            {
                string code = CSharpSource.Read(File.ReadAllText(file)).Code;
                string where = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (DllImportPattern().IsMatch(code))
                {
                    violations.Add($"{where} declares a DllImport; native code is called only through a source-generated LibraryImport.");
                }

                if (!agent && LibraryImportPattern().IsMatch(code))
                {
                    violations.Add($"{where} declares a LibraryImport; only the agent imports native code.");
                }
            }
        }

        return violations;
    }

    [GeneratedRegex(@"\bDllImport(Attribute)?\b")]
    private static partial Regex DllImportPattern();

    [GeneratedRegex(@"\bLibraryImport(Attribute)?\b")]
    private static partial Regex LibraryImportPattern();
}