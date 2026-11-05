using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace Portunus.Repository.Tests;

/// <summary>
/// The pipeline's definition, <c>.github/workflows/gates.yml</c>, read as text (<c>06</c>
/// PRT-ENG-011, PRT-ENG-012): the release tags it runs for, and the release job.
/// </summary>
/// <remarks>The release itself is proven by a release tag's run, which the phase report names as awaiting it.</remarks>
[Trait("Category", "unit")]
public sealed class WorkflowTests
{
    private const string AssetsLine = "$assets = @(Get-ChildItem -Path artifacts/packages -Filter '*.nupkg' -File | ForEach-Object FullName) + @(Get-ChildItem -Path artifacts/release -File | ForEach-Object FullName) + @('docs/guide/compromise-checklist.md')";

    private const string CreateLine = "gh release create $env:TAG --verify-tag --title $env:TAG --notes-file RELEASE-NOTES.md @arguments @assets";

    private const string PreReleaseLine = @"if ($env:TAG -cmatch '^v\d+\.\d+\.\d+-rc\.[1-9]\d*$') { $arguments += '--prerelease' }";

    private static readonly string[] Lines = File.ReadAllLines(RepositoryRoot.Combine(".github", "workflows", "gates.yml"));

    [Fact]
    public void PRT_ENG_011_AC4_PipelineRunsForTheTagsOfFinalReleasesAndReleaseCandidates()
    {
        string[] tags = [.. Block(Lines, "on:").SkipWhile(line => line.Trim() != "tags:").Skip(1).TakeWhile(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal)).Select(line => line.Trim())];

        Assert.Equal(["- 'v[0-9]+.[0-9]+.[0-9]+'", "- 'v[0-9]+.[0-9]+.[0-9]+-rc.[0-9]+'"], tags);
    }

    [Fact]
    public void PRT_ENG_011_AC4_ReleaseJobRunsOnlyForATagOnceTheGatesJobHasPassed()
    {
        string[] release = Job("release");

        Assert.Contains("    needs: gates", release);
        Assert.Contains("    if: github.ref_type == 'tag'", release);
    }

    [Fact]
    public void PRT_ENG_011_AC4_OnlyTheReleaseJobCanWriteToTheRepositorysContents()
    {
        string[] permissions = [.. Block(Lines, "permissions:").Skip(1).Where(line => line.Length > 0)];

        Assert.Equal(["  contents: read"], permissions);
        Assert.Equal(["release"], [.. Jobs().Where(job => Job(job).Any(line => line.Trim() == "contents: write"))]);
        Assert.DoesNotContain(Jobs(), job => Job(job).Any(line => line.Trim() == "permissions: write-all"));
    }

    [Fact]
    public void PRT_ENG_011_AC4_ReleaseJobPacksEveryPackageAtTheTagsVersionAndPublishesTheAgentForBothRuntimes()
    {
        string release = string.Join('\n', Job("release"));

        Assert.Contains("run: ./build/gates/pack-packages.ps1 -Version $env:TAG.Substring(1)", release, StringComparison.Ordinal);
        Assert.Contains("./build/gates/gate-agent-publish.ps1", release, StringComparison.Ordinal);
        Assert.Contains("foreach ($runtime in 'linux-x64', 'linux-arm64') {", release, StringComparison.Ordinal);
        Assert.Contains("Copy-Item (Join-Path 'tmp' 'publish' $runtime 'Portunus.Agent') (Join-Path $files \"portunus-agent-$runtime\")", release, StringComparison.Ordinal);
        Assert.Contains("foreach ($file in 'portunus-agent@.service', 'token.conf', 'example.json') {", release, StringComparison.Ordinal);
        Assert.Contains("[System.IO.File]::WriteAllText((Join-Path $files 'SHA256SUMS'), ($sums -join \"`n\") + \"`n\")", release, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet nuget push", release, StringComparison.Ordinal);
    }

    [Fact]
    public void PRT_ENG_011_AC4_ReleaseJobReleasesNothingForATagOffItsReleaseCommit()
    {
        string release = string.Join('\n', Job("release"));

        Assert.Contains("$expected = \"chore: release $($env:TAG.Substring(1))\"", release, StringComparison.Ordinal);
        Assert.Contains("if ($subject -cne $expected) {", release, StringComparison.Ordinal);
    }

    [Fact]
    public void PRT_ENG_011_AC4_ReleaseStepAttachesItsFilesAndMarksTheReleaseCandidateFormAloneAPreRelease()
    {
        Assert.Empty(ReleaseStepViolations(Job("release")));
    }

    [Theory]
    [InlineData(AssetsLine, "$assets = @(Get-ChildItem -Path artifacts/packages -Filter '*.nupkg' -File | ForEach-Object FullName)", "the release attaches other files than the packages, the agent's executables, the files of deploy/, SHA256SUMS and the checklist")]
    [InlineData(CreateLine, CreateLine + " CHANGELOG.md", "the release attaches other files than the packages, the agent's executables, the files of deploy/, SHA256SUMS and the checklist")]
    [InlineData(PreReleaseLine, "", "the release candidate's release is not marked a pre-release by its tag's exact form")]
    [InlineData(PreReleaseLine, "$arguments += '--prerelease'", "the release candidate's release is not marked a pre-release by its tag's exact form")]
    [InlineData(PreReleaseLine, "if ($env:TAG.Contains('-rc.')) { $arguments += '--prerelease' }", "the release candidate's release is not marked a pre-release by its tag's exact form")]
    public void PRT_ENG_011_AC4_ReleaseStepOutsideTheRuleIsFound(string line, string replacement, string expected)
    {
        string[] release = [.. Job("release").Select(found => found.Trim() == line ? found.Replace(line, replacement, StringComparison.Ordinal) : found)];

        Assert.Contains(expected, ReleaseStepViolations(release));
    }

    [Fact]
    public void GatesJob_EveryEvent_RunsEveryGateAndCannotWriteToTheContents()
    {
        string gates = string.Join('\n', Job("gates"));

        Assert.Contains("./build/gates/run-gates.ps1 -Base $base -Head $head", gates, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", gates, StringComparison.Ordinal);
    }

    /// <summary>
    /// The problems of the release step: it attaches the packages, the folder of the agent's
    /// executables, the files of <c>deploy/</c> and <c>SHA256SUMS</c>, and the checklist
    /// (<c>08</c> PRT-THR-003), and nothing else, and marks the release a pre-release where, and
    /// only where, its tag has the release candidate's form, <c>vX.Y.Z-rc.N</c> with <c>N</c>
    /// from 1 (<c>06</c> PRT-ENG-011).
    /// </summary>
    private static List<string> ReleaseStepViolations(string[] release)
    {
        List<string> violations = [];
        string[] lines = [.. release.Select(line => line.Trim())];
        if (!lines.Contains(AssetsLine, StringComparer.Ordinal) || !lines.Contains(CreateLine, StringComparer.Ordinal))
        {
            violations.Add("the release attaches other files than the packages, the agent's executables, the files of deploy/, SHA256SUMS and the checklist");
        }

        string[] marked = [.. lines.Where(line => line.Contains("--prerelease", StringComparison.Ordinal))];
        if (marked is not [PreReleaseLine])
        {
            violations.Add("the release candidate's release is not marked a pre-release by its tag's exact form");
        }

        return violations;
    }

    private static string[] Jobs()
    {
        return [.. Block(Lines, "jobs:").Skip(1).Where(IsJobLine).Select(line => line.Trim().TrimEnd(':'))];
    }

    /// <summary>The lines of the job <paramref name="name"/>, from its own line to the next job's.</summary>
    private static string[] Job(string name)
    {
        string[] jobs = [.. Block(Lines, "jobs:").Skip(1)];
        return [.. jobs.SkipWhile(line => line != $"  {name}:").TakeWhile((line, index) => index == 0 || !IsJobLine(line))];
    }

    private static bool IsJobLine(string line)
    {
        return line.Length > 2 && line.StartsWith("  ", StringComparison.Ordinal) && line[2] is not ' ' and not '#' && line.EndsWith(':');
    }

    /// <summary>The lines of the top-level key <paramref name="key"/>, from its own line to the next top-level key's.</summary>
    private static IEnumerable<string> Block(IEnumerable<string> lines, string key)
    {
        return lines.SkipWhile(line => line != key).TakeWhile((line, index) => index == 0 || line.Length == 0 || line.StartsWith(' ') || line.StartsWith('#'));
    }
}