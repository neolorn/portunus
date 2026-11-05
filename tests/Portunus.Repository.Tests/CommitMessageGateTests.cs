using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Repository.Tests;

[Trait("Category", "integration")]
public sealed class CommitMessageGateTests
{
    private const string Changelog = """
        # Changelog

        ## [Unreleased]

        ### Added

        """;

    private const string Header = "the first line is not \"type(scope): description\" of an allowed type and scope";

    private const string Fragment = "is not a dash fragment, and the footer holds only Implements: and BREAKING CHANGE: lines";

    [Fact]
    public async Task PRT_ENG_011_AC1_CommitsWithinTheRulePass()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.CommitAsync("""
            test(core): add the layout tests

            - the layout is read from the project files, not the solution
            - a fixture breaks one rule at a time

            Implements: PRT-ENG-001, PRT-LIB-001
            """, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog + "- A secret is read from the agent at its address.\n");
        await repository.CommitAsync("feat(client): read a secret from the agent\n\nImplements: PRT-LIB-004\n", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("3 commit(s)", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build: add the ISecretClient interface")]
    [InlineData("feat(agent): read the vault through the op CLI")]
    public async Task PRT_ENG_011_AC1_NameInItsOwnSpellingPasses(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog + "- The agent reads the vault through the op CLI.\n");
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    [Theory]
    [InlineData("add the shared build settings", Header)]
    [InlineData("feature: add the shared build settings", Header)]
    [InlineData("build(api): add the shared build settings", Header)]
    [InlineData("build(engine): add the shared build settings", Header)]
    [InlineData("build(vault): add the shared build settings", Header)]
    [InlineData("build(Core): add the shared build settings", Header)]
    [InlineData("build:add the shared build settings", Header)]
    [InlineData("build: Add the shared build settings", "the description does not begin with a lower-case letter")]
    [InlineData("build: NuGet settings", "the description does not begin with a lower-case letter")]
    [InlineData("build: add the shared build settings.", "the description ends with a full stop")]
    [InlineData("build: add the shared build settings that every project of the solution takes", "the first line is longer than 72 characters")]
    [InlineData("build: add the shared build settings\nsecond line", "the first line is not followed by a blank line")]
    [InlineData("build: add the shared build settings\n\nThe settings are shared.", "\"The settings are shared.\" " + Fragment)]
    [InlineData("build: add the shared build settings\n\n- a fragment that runs on for longer than the seventy-two characters allowed", "is longer than 72 characters")]
    [InlineData("build: add the shared build settings\n\nCo-Authored-By: Someone <someone@example.invalid>", Fragment)]
    [InlineData("build: add the shared build settings\n\nSigned-off-by: Someone <someone@example.invalid>", Fragment)]
    [InlineData("build: add the shared build settings\n\nImplements: PRT-AGT-4", "\"Implements: PRT-AGT-4\" " + Fragment)]
    [InlineData("build: add the shared build settings\n\nImplements: PRT-AGT-004\nReviewed-by: Someone", "\"Reviewed-by: Someone\" " + Fragment)]
    public async Task PRT_ENG_011_AC1_CommitMessageOutsideTheRuleFails(string message, string reason)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(reason);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains(reason, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("feat(client): read a secret from the agent")]
    [InlineData("fix(agent): refuse a request over its bound")]
    [InlineData("perf(agent): fetch the items in one call")]
    [InlineData("refactor(core)!: rename the secret's versions")]
    [InlineData("refactor(core): rename the secret's versions\n\nBREAKING CHANGE: the versions are renamed")]
    public async Task PRT_ENG_011_AC1_BehaviourChangingCommitWithoutAChangelogLineFails(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("adds no line under the changelog's Unreleased section", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRT_ENG_011_AC1_ChangelogLineOutsideUnreleasedFails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog + "## [1.0.0-rc.1] - 2026-10-22\n\n### Added\n\n- A secret is read from the agent at its address.\n");
        await repository.CommitAsync("feat(client): read a secret from the agent", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("adds no line under the changelog's Unreleased section", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("v1.0.0")]
    [InlineData("v1.0.0-rc.1")]
    [InlineData("v1.0.0-rc.12")]
    public async Task PRT_ENG_011_AC1_ChangelogLineUnderChangedPassesOnceAReleaseIsTagged(string tag)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", tag, start);
        repository.Write("CHANGELOG.md", Changelog.Replace("### Added", "### Changed", StringComparison.Ordinal) + "- A read waits for the agent within its bound.\n");
        await repository.CommitAsync("feat(client): wait for the agent within the bound", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    [Theory]
    [InlineData("v1.0")]
    [InlineData("version-1.0.0")]
    [InlineData("v1.0.0-rc.0")]
    [InlineData("v1.0.0-rc.01")]
    [InlineData("v1.0.0-beta.1")]
    [InlineData("v01.0.0")]
    public async Task PRT_ENG_011_AC1_ChangelogLineUnderChangedFailsAfterATagThatIsNoRelease(string tag)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", tag, start);
        repository.Write("CHANGELOG.md", Changelog.Replace("### Added", "### Changed", StringComparison.Ordinal) + "- A read waits for the agent within its bound.\n");
        await repository.CommitAsync("feat(client): wait for the agent within the bound", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("adds no line under the changelog's Unreleased section", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRT_ENG_011_AC1_MergeCommitsThePlatformWritesAreNotChecked()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "--create", "phase-01");
        await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "main");
        await repository.GitAsync(cancellationToken, "-c", "user.name=GitHub", "-c", "user.email=noreply@github.com", "merge", "--quiet", "--no-ff", "--no-edit", "-m", "Merge pull request #1 from owner/phase-01", "phase-01");

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("1 commit(s)", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRT_ENG_011_AC1_LocalMergeCommitOutsideTheRuleFails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "--create", "phase-01");
        await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "main");
        await repository.GitAsync(cancellationToken, "merge", "--quiet", "--no-ff", "--no-edit", "-m", "Merge branch 'phase-01'", "phase-01");

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("\"Merge branch 'phase-01'\": " + Header, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Portunus.Core/PublicAPI.Shipped.txt")]
    [InlineData("build/gates/release-baseline.json")]
    public async Task PRT_ENG_011_AC2_CommitChangingAReleasedFileFails(string path)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithReleasedFilesAsync(folder, cancellationToken);
        repository.Write(path, "#nullable enable\nPortunus.Core.Secret\n");
        await repository.CommitAsync("build: list the secret", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"it changes {path}, which changes only in a release commit", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Portunus.Core/PublicAPI.Shipped.txt")]
    [InlineData("build/gates/release-baseline.json")]
    public async Task PRT_ENG_011_AC2_CommitDeletingAReleasedFileFails(string path)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithReleasedFilesAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "rm", "--quiet", path);
        await repository.CommitAsync("build: remove the list", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"it deletes {path}, which changes only in a release commit", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRT_ENG_011_AC2_NewProjectsPublicApiFileAndAnUnshippedChangePass()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithReleasedFilesAsync(folder, cancellationToken);
        repository.Write("src/Portunus.Cli/PublicAPI.Shipped.txt", "#nullable enable\n");
        repository.Write("src/Portunus.Core/PublicAPI.Unshipped.txt", "#nullable enable\nPortunus.Core.Secret\n");
        await repository.CommitAsync("build: add the command line project", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    [Theory]
    [InlineData("src/Portunus.Core", "src/Renamed.Core", "src/Portunus.Core/PublicAPI.Shipped.txt")]
    [InlineData("build/gates/release-baseline.json", "build/release-baseline.json", "build/gates/release-baseline.json")]
    public async Task PRT_ENG_011_AC2_CommitMovingAReleasedFileDeletesItAndFails(string from, string to, string deleted)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithReleasedFilesAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "mv", from, to);
        await repository.CommitAsync("refactor: move the released file", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"it deletes {deleted}, which changes only in a release commit", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviousReleaseTag_TagsOfOtherShapes_AreLeftOut()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0-rc.1", start);
        string second = await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v9", second);
        await repository.GitAsync(cancellationToken, "tag", "v2.0.0-beta.1", second);
        await repository.GitAsync(cancellationToken, "tag", "v2.0.0-rc.0", second);
        string head = await repository.CommitAsync("docs: add the release notes", cancellationToken);

        ProcessResult previous = await Processes.GateAsync("previous-release-tag.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head);

        Assert.True(previous.ExitCode == 0, previous.Output);
        Assert.Equal("v1.0.0-rc.1", previous.Output.Trim());
    }

    [Theory]
    [InlineData("v1.0.0-rc.9 v1.0.0-rc.10", "v1.0.0-rc.10")]
    [InlineData("v1.0.0-rc.3 v1.0.0", "v1.0.0")]
    [InlineData("v1.0.0 v1.1.0-rc.1", "v1.1.0-rc.1")]
    [InlineData("v1.9.0 v1.10.0-rc.2", "v1.10.0-rc.2")]
    public async Task PreviousReleaseTag_ReleasesAndReleaseCandidates_AnswersTheHighestByPrecedence(string tags, string expected)
    {
        ArgumentNullException.ThrowIfNull(tags);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        foreach (string tag in tags.Split(' '))
        {
            await repository.GitAsync(cancellationToken, "tag", tag, start);
        }

        string head = await repository.CommitAsync("docs: add the release notes", cancellationToken);

        ProcessResult previous = await Processes.GateAsync("previous-release-tag.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head);

        Assert.True(previous.ExitCode == 0, previous.Output);
        Assert.Equal(expected, previous.Output.Trim());
    }

    [Fact]
    public async Task PreviousReleaseTag_TagOnHeadItself_IsLeftOut()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0-rc.1", start);
        string head = await repository.CommitAsync("chore: release 1.0.0-rc.2", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0-rc.2", head);

        ProcessResult previous = await Processes.GateAsync("previous-release-tag.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head);

        Assert.True(previous.ExitCode == 0, previous.Output);
        Assert.Equal("v1.0.0-rc.1", previous.Output.Trim());
    }

    [Fact]
    public async Task PreviousReleaseTag_FirstRelease_IsNone()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, _) = await CreateAsync(folder, cancellationToken);
        string head = await repository.CommitAsync("docs: add the release notes", cancellationToken);

        ProcessResult previous = await Processes.GateAsync("previous-release-tag.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head);

        Assert.True(previous.ExitCode == 0, previous.Output);
        Assert.Equal(string.Empty, previous.Output.Trim());
    }

    private static async Task<(GitRepository Repository, string Start)> CreateAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog);
        string start = await repository.CommitAsync("docs: add the changelog", cancellationToken);
        return (repository, start);
    }

    private static async Task<(GitRepository Repository, string Start)> CreateWithReleasedFilesAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog);
        repository.Write("src/Portunus.Core/PublicAPI.Shipped.txt", "#nullable enable\n");
        repository.Write("src/Portunus.Core/PublicAPI.Unshipped.txt", "#nullable enable\n");
        repository.Write("build/gates/release-baseline.json", "{}\n");
        string start = await repository.CommitAsync("docs: add the changelog and the released files", cancellationToken);
        return (repository, start);
    }

    private static Task<ProcessResult> RunAsync(GitRepository repository, string start, CancellationToken cancellationToken)
    {
        return Processes.GateAsync("gate-commit-messages.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", start, "-Head", "HEAD");
    }
}