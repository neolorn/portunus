using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Repository.Tests;

/// <summary>The release gate read against a repository's tags and commits (<c>04</c> PRT-LIB-007, <c>06</c> PRT-ENG-011).</summary>
[Trait("Category", "integration")]
public sealed class ReleaseHistoryTests
{
    private const string Shipped = "#nullable enable\nPortunus.Core.Result\nPortunus.Core.Address\n";

    private const string Emptied = "#nullable enable\n";

    private const string Removed = "#nullable enable\n*REMOVED*Portunus.Core.Address\n";

    private static readonly ReleaseSet Current = new(
        ["secrets.secret.denied"],
        ["Portunus:Client:Socket"],
        new Dictionary<string, IReadOnlyDictionary<string, string>> { ["AgentState"] = new Dictionary<string, string> { ["Ready"] = "ready" } },
        ["RequestFormat address"],
        ["portunus push"]);

    [Fact]
    public async Task PRT_LIB_007_AC2_WithNoFinalReleaseTaggedAPullRequestAndAReleaseCandidatesTagThatRemoveAPublicMemberPass()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await ReleasedAsync(folder, "v1.0.0-rc.1", cancellationToken);
        repository.Write("src/Portunus.Core/PublicAPI.Unshipped.txt", Removed);
        string head = await repository.CommitAsync("refactor(core): remove the address", cancellationToken);

        IReadOnlyList<string> pullRequest = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0-rc.2", head);
        IReadOnlyList<string> candidate = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, cancellationToken);

        Assert.Empty(pullRequest);
        Assert.Empty(candidate);
    }

    [Fact]
    public async Task PRT_LIB_007_AC2_WithAFinalRelease100TaggedTheSameRemovalFailsInAPullRequestCarryingNoBreakingMarkAndInATagV110Rc1()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await ReleasedAsync(folder, "v1.0.0", cancellationToken);
        repository.Write("src/Portunus.Core/PublicAPI.Unshipped.txt", Removed);
        string head = await repository.CommitAsync("refactor(core): remove the address", cancellationToken);

        IReadOnlyList<string> pullRequest = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.1.0-rc.1", head);
        IReadOnlyList<string> candidate = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, cancellationToken);

        Assert.Equal(["the public member Portunus.Core: Portunus.Core.Address is changed or removed since v1.0.0, and no commit since then carries ! or BREAKING CHANGE:"], pullRequest);
        Assert.Equal(["v1.1.0-rc.1 is below the major release the changes since v1.0.0 need: the public member Portunus.Core: Portunus.Core.Address is changed or removed"], candidate);
    }

    [Fact]
    public async Task CompatibilityViolations_RemovalMarkedBreakingAfterTheFinalRelease_PassInAPullRequestAndInAMajorReleasesTag()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await ReleasedAsync(folder, "v1.0.0", cancellationToken);
        repository.Write("src/Portunus.Core/PublicAPI.Unshipped.txt", Removed);
        await repository.CommitAsync("refactor(core)!: remove the address", cancellationToken);
        string head = await repository.CommitAsync("docs: report the removal", cancellationToken);

        IReadOnlyList<string> pullRequest = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v2.0.0-rc.1", head);
        IReadOnlyList<string> candidate = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, ReleaseSet.Empty, cancellationToken);

        Assert.Empty(pullRequest);
        Assert.Empty(candidate);
    }

    [Fact]
    public async Task CompatibilityViolations_CodeRemovedSinceTheFinalReleasesBaseline_FailsInAPullRequest()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await ReleasedAsync(folder, "v1.0.0", cancellationToken, Current);
        await repository.CommitAsync("refactor(client): answer the denied code no more", cancellationToken);

        IReadOnlyList<string> violations = await ReleaseGate.CompatibilityViolationsAsync(repository.Root, Current with { Codes = [] }, cancellationToken);

        Assert.Equal(["the code secrets.secret.denied is removed since v1.0.0, and no commit since then carries ! or BREAKING CHANGE:"], violations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PRT_ENG_011_AC3_ReleaseCommitWhoseBaselineIsNotTheReleasesOwnSetFails(bool reported)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, cancellationToken);
        repository.Write("build/gates/release-baseline.json", ReleaseSet.Empty.Write());
        await repository.CommitAsync("chore: release 1.0.0-rc.1", cancellationToken);
        if (reported)
        {
            repository.Write("docs/reports/phase-06.md", "# Phase 6\n");
            await repository.CommitAsync("docs: report the release", cancellationToken);
        }

        IReadOnlyList<string> violations = await ReleaseGate.BaselineViolationsAsync(repository.Root, Current, cancellationToken);

        Assert.Equal(["build/gates/release-baseline.json is not the release's own set, which the release commit of 1.0.0-rc.1 writes"], violations);
    }

    [Fact]
    public async Task BaselineViolations_ReleaseCommitWritingTheReleasesOwnSet_AreNone()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, cancellationToken);
        repository.Write("build/gates/release-baseline.json", Current.Write());
        await repository.CommitAsync("chore: release 1.0.0-rc.1", cancellationToken);

        Assert.Empty(await ReleaseGate.BaselineViolationsAsync(repository.Root, Current, cancellationToken));
    }

    [Fact]
    public async Task BaselineViolations_ChangeOutsideDocsSinceTheReleaseCommit_AreNotLookedFor()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await CreateAsync(folder, cancellationToken);
        repository.Write("build/gates/release-baseline.json", ReleaseSet.Empty.Write());
        await repository.CommitAsync("chore: release 1.0.0-rc.1", cancellationToken);
        repository.Write("src/Portunus.Core/Codes.cs", "namespace Portunus.Core;\n");
        await repository.CommitAsync("feat(core): answer a new code", cancellationToken);

        Assert.Empty(await ReleaseGate.BaselineViolationsAsync(repository.Root, Current, cancellationToken));
    }

    private static async Task<GitRepository> CreateAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("src/Portunus.Core/PublicAPI.Shipped.txt", Shipped);
        repository.Write("src/Portunus.Core/PublicAPI.Unshipped.txt", Emptied);
        await repository.CommitAsync("build: add the public API files", cancellationToken);
        return repository;
    }

    /// <summary>A repository whose first commit is released as <paramref name="tag"/>, its baseline <paramref name="baseline"/> or the empty set.</summary>
    private static async Task<GitRepository> ReleasedAsync(ScratchFolder folder, string tag, CancellationToken cancellationToken, ReleaseSet? baseline = null)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("src/Portunus.Core/PublicAPI.Shipped.txt", Shipped);
        repository.Write("src/Portunus.Core/PublicAPI.Unshipped.txt", Emptied);
        repository.Write("build/gates/release-baseline.json", (baseline ?? ReleaseSet.Empty).Write());
        string release = await repository.CommitAsync($"chore: release {tag[1..]}", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", tag, release);
        return repository;
    }
}