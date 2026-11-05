using System;
using System.Collections.Generic;
using System.IO;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary>Where the tool finds <c>op</c> and <c>ssh</c>: an absolute folder of the <c>PATH</c> alone (<c>02</c> PRT-PSH-002).</summary>
[Trait("Category", "unit")]
public sealed class ToolEnvironmentTests : IDisposable
{
    private readonly string _folder = Path.Combine(Fakes.Repository, "tmp", "cli", Guid.CreateVersion7().ToString("N"));

    public ToolEnvironmentTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, OperatingSystem.IsWindows() ? "op.exe" : "op"), string.Empty);
    }

    [Fact]
    public void Find_ProgramInAnAbsoluteFolder_IsFound()
    {
        Assert.Equal(Path.Combine(_folder, OperatingSystem.IsWindows() ? "op.EXE" : "op"), Environment(_folder).Find("op"));
    }

    [Fact]
    public void Find_ProgramInAFolderRelativeToTheWorkingDirectory_IsSkipped()
    {
        string relative = Path.GetRelativePath(System.Environment.CurrentDirectory, _folder);

        Assert.False(Path.IsPathFullyQualified(relative));
        Assert.Null(Environment(relative).Find("op"));
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static ToolEnvironment Environment(string path)
    {
        return new ToolEnvironment(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = path, ["PATHEXT"] = ".EXE" });
    }
}