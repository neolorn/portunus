# Phase 6: Release gate, sample host and release 1.0.0-rc.1

## Items built

Every test below was written before the code it proves.

**`06` PRT-ENG-011** criteria 2 and 3, the release commit's checks: the commit-message gate takes `chore: release V`, `V` `X.Y.Z` or `X.Y.Z-rc.N` with `N` from 1, as the one commit that may change `PublicAPI.Shipped.txt` and `build/gates/release-baseline.json`, and only as the value gives: each `PublicAPI.Shipped.txt` its parent's entries and those of the `PublicAPI.Unshipped.txt` beside it, less those marked `*REMOVED*`, in ordinal order after `#nullable enable`; each `PublicAPI.Unshipped.txt` `#nullable enable` alone; `CHANGELOG.md` with the `Unreleased` entries under `## [V] - YYYY-MM-DD`, the commit's date in UTC, below an empty `## [Unreleased]`; `RELEASE-NOTES.md` holding `## V`; no other file. A commit after it in the range checked other than a `docs:` commit changing only `docs/` fails, as does a first line naming a release outside the rule. The baseline the release commit writes is the release's own set, which the release gate checks while nothing outside `docs/` has changed since that commit (`gate-commit-messages.ps1`, `ReleaseGate`).
- ReleaseCommitGateTests (integration): PRT_ENG_011_AC2_ReleaseCommitChangingTheReleasedFilesAsItsValueGivesPasses; PRT_ENG_011_AC3_ReleaseCommitChangingAFileOutsideItsValueFails, ReleaseCommitWritingOneOfItsFilesOtherwiseThanItsValueGivesFails, ReleaseCommitFollowedByACommitOtherThanADocsCommitChangingOnlyDocsFails, ReleaseCommitNamingAVersionOutsideTheRuleFails; Gate_ChoreThatNamesNoVersion_IsNoReleaseCommit
- ReleaseHistoryTests (integration): PRT_ENG_011_AC3_ReleaseCommitWhoseBaselineIsNotTheReleasesOwnSetFails; BaselineViolations_ReleaseCommitWritingTheReleasesOwnSet_AreNone, BaselineViolations_ChangeOutsideDocsSinceTheReleaseCommit_AreNotLookedFor
- The phase 0 tests of CommitMessageGateTests, unchanged and passing

**`04` PRT-LIB-007** criteria 1 and 2, the compatibility gate: `build/gates/gate-release-compatibility.ps1` runs `ReleaseGateTests` on the repository at HEAD. The current set is read from the built assemblies' metadata (`ReleaseSet`): the public surface from each package's public API files; the codes and the configuration keys, each string of their shape a shipped assembly holds, as the contract tests of PRT-ENG-007 read them; each public enumeration's members and spellings; the wire formats' members, each string of a member's shape the format types of `Portunus.Core` load (`RequestFormat`, `AnswerFormat`, `PushAnswerFormat`, `WireJson`, `DevelopmentFile`), an argument's name handed to a guard left out; and the executables' commands and options, each string constant of their shape that `ToolArguments` and `AgentCommand` declare. It is compared with the set and the `PublicAPI.Shipped.txt` files the tag of the newest final release holds (`previous-release-tag.ps1 -Final`), a release candidate's tag never being that release; with none, the baseline is empty and nothing is a break. A removal or a change needs a major release and an addition a minor one (`ReleaseRules`); a release tag at HEAD must carry at least that, a release candidate held to its final release's version; elsewhere a change that needs a major release fails unless a commit since the final release carries `!` or `BREAKING CHANGE:` (`ReleaseGate`). The agent's commands are named as constants for the read (`AgentCommand.Run`, `Receive`, `Status`).
- ReleaseRuleTests (unit): PRT_LIB_007_AC1_ChangeOrRemovalOfTheSurfaceCodesKeysEnumerationsFormatsOrCommandsNeedsAMajorRelease, AdditionOfAMemberCodeKeySpellingFormatMemberOrCommandNeedsAMinorRelease, BeforeTheFirstFinalReleaseTheBaselineIsEmptySoNothingIsABreak, ReleaseTagBelowWhatChangedNeedsFails, SetReadFromTheBuiltAssembliesHoldsTheCodesKeysEnumerationsFormatsAndCommands; PRT_LIB_007_AC2_PullRequestNeedingAMajorReleasePassesOnlyWhereACommitSinceTheNewestFinalReleaseIsMarkedBreaking; Compare_NothingChanged_NeedsAPatchRelease, MarkedBreaking_CommitMessage_ReadsItsMarkOrFooter, ReleaseSet_WrittenAndParsed_IsTheSameSet
- ReleaseHistoryTests (integration, repositories with tags made under `tmp/`): PRT_LIB_007_AC2_WithNoFinalReleaseTaggedAPullRequestAndAReleaseCandidatesTagThatRemoveAPublicMemberPass, WithAFinalRelease100TaggedTheSameRemovalFailsInAPullRequestCarryingNoBreakingMarkAndInATagV110Rc1; CompatibilityViolations_RemovalMarkedBreakingAfterTheFinalRelease_PassInAPullRequestAndInAMajorReleasesTag, CodeRemovedSinceTheFinalReleasesBaseline_FailsInAPullRequest
- ReleaseGateTests (integration, the gate): ReleaseGate_ThisRepositoryAtHead_IsWithinWhatItsNewestFinalReleaseAllows, ReleaseGate_ThisRepositoryAtHead_HoldsTheOwnSetOfItsLastReleaseCommit

**`06` PRT-ENG-011** criterion 4, the release job: the job `release` of `gates.yml` runs only for a tag, after the `gates` job, and alone holds `contents: write`, the workflow's own being `contents: read`. It refuses a tag outside `vX.Y.Z` and `vX.Y.Z-rc.N` and a tag on a commit whose subject is not `chore: release V`; packs every package at the tag's version (`build/gates/pack-packages.ps1`, every project of `src/` but the agent, `MinVerVersionOverride` the tag's version); publishes the agent through `gate-agent-publish.ps1` and names the two executables `portunus-agent-linux-x64` and `portunus-agent-linux-arm64`; writes `SHA256SUMS` over them and the three files of `deploy/`, in `sha256sum`'s form; and creates the tag's release with the runner's `gh`, its notes `RELEASE-NOTES.md`, a release candidate's marked a pre-release by its tag's exact form, the packages, the two executables, the three files of `deploy/`, `SHA256SUMS` and `docs/guide/compromise-checklist.md` attached. It publishes to no feed. Its checkout keeps no credentials.
- WorkflowTests (unit): PRT_ENG_011_AC4_PipelineRunsForTheTagsOfFinalReleasesAndReleaseCandidates, ReleaseJobRunsOnlyForATagOnceTheGatesJobHasPassed, OnlyTheReleaseJobCanWriteToTheRepositorysContents, ReleaseJobPacksEveryPackageAtTheTagsVersionAndPublishesTheAgentForBothRuntimes, ReleaseJobReleasesNothingForATagOffItsReleaseCommit, ReleaseStepAttachesItsFilesAndMarksTheReleaseCandidateFormAloneAPreRelease, ReleaseStepOutsideTheRuleIsFound; GatesJob_EveryEvent_RunsEveryGateAndCannotWriteToTheContents

**`05` PRT-OPS-003** and **`RELEASE-NOTES.md`**, with **`08` PRT-THR-002**, **PRT-THR-003** and **PRT-THR-005**: `RELEASE-NOTES.md` at the root, its section `## 1.0.0-rc.1` written in a `docs:` commit before the release commit: what the release ships; the siblings' versions the adapters are built against; every duty of PRT-OPS-003 (no swap and no crash dumps; the agent's install, user, unit and drop-in; the token's file; `op` at its pinned version and signing key; the vault; the quota; the processes, with PRT-THR-005's statement that the agent decides by the declarations alone; the push; old versions; backups); what memory does not protect against, as PRT-THR-002 gives it; and the checklist, named and attached, with its steps. `RELEASE-NOTES.md` is now required at the root.
- RepositoryLayoutTests (unit): PRT_ENG_001_AC1_ListedRootFileMissingIsFound, now with `RELEASE-NOTES.md`
- RootFileTests (integration): PRT_ENG_001_AC1_RootHoldsTheFilesTheLayoutLists

`build/gates/built-items.txt` gains PRT-ENG-011 AC3, PRT-LIB-007 AC1 and PRT-LIB-007 AC2.

## Items not built

Waiting on the answer to open question 1:
- `06` PRT-ENG-001 the sample host and its criterion 2, the sample host's gate of PRT-ENG-012 (`build/gates/gate-sample-host.ps1`), and `04` PRT-LIB-001 the sample host's row. `pack-packages.ps1`, which the gate will run, is built for the release job.

Waiting on `Themis.Core.1.0.0-rc.2.nupkg`, the owner's download:
- `src/Portunus.Themis` and its tests, as phase 5 named them; the sample host's `AddPortunusThemisSecrets` and its read of Themis's `connection`; the Themis line of `RELEASE-NOTES.md`'s packages and siblings' versions.

Waiting on the fake `op`'s recordings:
- `08` PRT-THR-001 criterion 1. The controls of `01` PRT-AGT-003, PRT-AGT-004 and `02` PRT-PSH-002 have no test carrying their identifiers until the recorded scenarios come, so the test of the criterion cannot pass before them. See open question 2.

Waiting on the lead's message after the review:
- The release commit `chore: release 1.0.0-rc.1` and the tag `v1.0.0-rc.1`.

Still parked from phase 4, waiting on the owner: his check by hand on Windows.

## Criteria checked without a test

- `05` PRT-OPS-003 criterion 1: `RELEASE-NOTES.md` states each duty of the item, read against it line by line.
- `08` PRT-THR-003 criterion 1: the release notes name the checklist; the release job attaches it, which WorkflowTests checks in the job's text.
- `08` PRT-THR-002 and PRT-THR-005: stated in `RELEASE-NOTES.md`; PRT-THR-005 is the agent's design as built in phase 3, which decides each read by the declaration alone.
- `00` PRT-OVR-003: the first release is designed to take a later source of secrets or adapter without a breaking change: a host reads through `ISecretClient`, which a later source implements beside the agent's and the development file's; each adapter is a package of its own over the client; the release gate now fails a break after the first final release. Building every requirement of `01` to `08` waits on the items above.
- `06` PRT-ENG-008, tests written first.

## Awaiting the pipeline's run

- `06` PRT-ENG-011 criterion 4: the release job's run, which only a release tag starts.

## Analyser suppressions

- None new.

## Paths that make a string of a value or the token

- None new.

## Resolved by rule

- `tests/Portunus.Repository.Tests/ReleaseSet.cs`, what the release gate reads as the wire formats' members: the strings of a member's shape the format types of `Portunus.Core` load, as `<Type> <member>`, the closed values of `kind` among them, since a change of either breaks a reader of the format. Governs: `04` PRT-LIB-007, "the wire formats' members", with `06` PRT-ENG-004, which leaves the metadata the one way the tests read a built assembly.
- `tests/Portunus.Repository.Tests/ReleaseSet.cs`, the executables' commands and options: the string constants of the type that reads each command line, `ToolArguments` and `AgentCommand`, the latter's commands named as constants by a refactor that changes no behaviour. Governs: `04` PRT-LIB-007 with `07` section 3.
- `build/gates/pack-packages.ps1`, the packages packed: every project of `src/` but `Portunus.Agent`, an executable published and not packed. Governs: `00` section 2.2 with `06` PRT-ENG-011.
- `.github/workflows/gates.yml`, `SHA256SUMS`: one line per file, its SHA-256 in lower case, two spaces and the file's name, as `sha256sum -c` reads it, the form the owner checks the agent against at its install. Governs: `05` PRT-OPS-003, "its SHA-256 checked against the release's `SHA256SUMS`".

## Open questions

1. **Tier 2: the sample host's host, and what it registers.** Items: `06` PRT-ENG-001 (the sample host, criterion 2), its gate of PRT-ENG-012, `04` PRT-LIB-001 the sample host's row. Two points:
   - (i) The sample host is "a worker service" whose tests run it through `Portunus.Testing`, so it needs the generic host (`Host.CreateApplicationBuilder`). `04` PRT-LIB-001 gives the sample host "the packed packages above" alone, and none of them brings `Microsoft.Extensions.Hosting`; `06` PRT-ENG-009 gives that package to `Portunus.Agent` and the host tests alone. Readings: (a) the sample host takes `Microsoft.Extensions.Hosting`, the agent's row's package, and its test project takes it through the sample host; (b) the sample host takes the shared framework `Microsoft.AspNetCore.App`, which holds the generic host, through the Web SDK as the siblings' samples do, no package. Smallest fix: (a) a row of PRT-ENG-009 and of PRT-LIB-001; (b) the PRT-LIB-001 row naming the framework.
   - (ii) `06` PRT-ENG-001 has the sample host register `AddPortunus` and `AddPortunusThemisSecrets` and read one secret of its own and Themis's `connection`; it names neither the Janus nor the Hermes adapter. Your message asked for the sample host "for Janus and Hermes". Readings: (a) build it now with `AddPortunus` and its own secret, the Themis registration and read added with the Themis adapter; (b) wait for the Themis adapter and build it whole; (c) the chapter changes to take the Janus or Hermes adapter. Smallest fix: (a) none in the chapters; (b) none; (c) the sentence of PRT-ENG-001.

   Parked: the sample host, its gate and its row of PRT-LIB-001.
2. **Tier 2: which controls of `08` PRT-THR-001 "the library itself carries".** Item: `08` PRT-THR-001 criterion 1. The table names requirements of which some carry no test by their nature: `06` PRT-ENG-012 (the pipeline's), `05` PRT-OPS-005 (checked by the integration tests' run, which the gate's table gives no script), `08` PRT-THR-003 (checked in review), `05` PRT-OPS-002 and `02` PRT-PSH-001 (no criteria), and `06` PRT-ENG-005 (criterion 1 in review, criterion 2 by `01` PRT-AGT-001 criterion 3's tests). Readings: (a) the test leaves these out by a list held beside it, each with its reason, as the siblings' test leaves out the pipeline's requirement; (b) only PRT-ENG-012 is left out, and a test carrying each other identifier is added. Smallest fix: (a) none in the chapters; (b) tests named after PRT-OPS-005 and PRT-ENG-005, and criteria for PRT-OPS-002 and PRT-PSH-001. Parked: nothing more than the criterion, which waits on the recordings.

## Checks

- The commit: the report's own commit, the head of `phase-06`, on `fdef96c`; the branch stands on `61b75ce` of `main`.
- Fast checks on every commit: built in Release with warnings as errors and the analysers; `dotnet format --verify-no-changes`; the unit tests of the projects each commit changes; green.
- Before the push: every unit test, the integration tests, the `linux` kind in the SDK's container, the agent's publish, the commit-message gate and the full-history secret scan, by `run-gates.ps1 -Base main -Head HEAD`.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
