using System;
using System.IO;

namespace Portunus.Tests;

/// <summary>
/// Paths under the repository's <c>tmp/</c> for what an integration test writes (<c>06</c>
/// PRT-ENG-008): a socket's path short enough for its rule, and a file of its own.
/// </summary>
internal static class Scratch
{
    private static readonly string Folder = Find();

    /// <summary>A new path for a socket or a file, ending in <paramref name="extension"/>, that nothing holds yet.</summary>
    internal static string NewPath(string extension)
    {
        Directory.CreateDirectory(Folder);
        return Path.Combine(Folder, Guid.CreateVersion7().ToString("N")[^12..] + extension);
    }

    private static string Find()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Portunus.sln")))
            {
                return Path.Combine(folder.FullName, "tmp", "client");
            }

            folder = folder.Parent;
        }

        throw new InvalidOperationException("The tests run below the repository's root.");
    }
}