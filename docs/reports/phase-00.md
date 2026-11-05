# Phase 0: Repository and gates

## Items built

Every test below is in `tests/Portunus.Repository.Tests`, written before the code or script it proves.

**`06` PRT-ENG-001** (all but the sample host, `RELEASE-NOTES.md`, `deploy/`, `tests/Portunus.Fakes` and `vendor/`): criterion 1, but for `vendor/`.
- RepositoryLayoutTests: PRT_ENG_001_AC1_ProjectsFollowTheLayout, ProjectFoldersFollowTheLayout, ProjectOutsideTheLayoutIsFound, ProjectOfNoPlaceIsFound, PartWithoutItsProjectIsFound, PartOfALaterPhaseIsNotYetRequired, PartOfALaterPhaseWithItsTestProjectIsAccepted, SourceProjectWithoutItsTestProjectIsFound, RepositoryWithoutItsRepositoryTestsIsFound, SolutionOtherThanTheTwoIsFound, SampleSolutionIsAccepted, ProjectTheSolutionDoesNotListIsFound, ProjectTheSolutionListsOutsideTheLayoutIsFound, FileWithoutANamespaceIsFound, TwoPublicTypesInOneFileAreFound, TwoPublicTypesInABlockScopedNamespaceAreFound, TypeWithItsNestedTypesIsOneTopLevelType, FolderOutsideTheLayoutIsFound, AgentsFoldersAreAccepted, FolderOfTheAgentOutsideItsTwoIsFound, FolderInsideAnAgentFolderIsFound, FolderOfATestProjectIsFound, NamespaceThatDoesNotMatchItsFolderIsFound, PublicTypeOutsideTheProjectsRootIsFound, PackageSourcesFollowTheRule, PackageSourcesOutsideTheRuleAreFound, PackageSourceMappingOutsideTheRuleIsFound, IgnoreFileListsThePackedPackages, IgnoreFileWithoutThePackedPackagesIsFound, RootOfTheListedFilesIsAccepted, RootFileOutsideTheListIsFound, ListedRootFileMissingIsFound; RepositoryFixture_AsMade_KeepsEveryRule
- RootFileTests: PRT_ENG_001_AC1_RootHoldsTheFilesTheLayoutLists
- GrantTests: PRT_ENG_001_AC1_GrantsFollowTheRule
- AssemblyContractProbeTests: PRT_ENG_001_AC1_GrantOfEveryFormIsReadFromTheBuiltAssembly
- ScannerConfigurationTests: PRT_ENG_001_AC1_ScannerConfigurationAddsNoEntry, DefaultExtendedAloneIsAccepted, EntryAddedToTheConfigurationIsFound, ConfigurationThatDoesNotExtendTheDefaultAloneIsFound
- SecretScanTests: FetchSecretScanner_ChangedScanner_IsReplacedByTheArchives, FetchSecretScanner_ArchiveOtherThanThePublished_Fails

**`06` PRT-ENG-002**: criteria 1 and 2.
- SettingRuleTests: PRT_ENG_002_AC1_ProjectsAndSharedFilesSetOnlyWhatTheRuleAllows, ProjectSettingWhatOnlyTheSharedFilesSetIsFound, ProjectPropertyThatTurnsAWarningOffIsFound, PropertyThatTurnsAWarningOffInATargetIsFound, ItemThatTurnsAWarningOffIsFound, ReferenceThatTurnsAWarningOffIsFound, SharedBuildSettingsTurningAWarningOffIsFound, PackageVersionsSettingAnotherPropertyIsFound, AnalyserConfigurationBesideTheRootEditorConfigIsFound, RootEditorConfigTmpAndBuildOutputAreLeftOut, SharedBuildSettingsOtherThanTheRulesAreFound, SharedBuildFileBesideTheRootsIsFound, SdkOtherThanTheRulesIsFound, RepositoryWithoutGlobalJsonIsFound, UnsafeBlocksOutsideTheAgentAreFound, UnsafeBlocksOfTheAgentAreAccepted, UnsafeBlocksOfTheAgentOtherThanTrueAreFound, UnsafeBlocksInTheSharedBuildSettingsAreFound, PublicationSetByAProjectIsFound; SettingViolations_ProjectWithoutItsLockfile_IsFound
- BuildSettingsTests: PRT_ENG_002_AC2_AWarningFailsTheBuild, AnMSBuildWarningFailsTheBuild, ALockedRestoreThatDiffersFromTheLockfileFails

**`06` PRT-ENG-004**: criteria 1 and 2.
- AssemblyContractTests: PRT_ENG_004_AC1_EveryPublicClassIsSealedOrAbstract, EveryPublicAsynchronousMethodTakesACancellationToken, PRT_ENG_004_AC2_NoMemberIsLookedUpByName, NoNativeCodeIsImportedButTheAgentsOne, NoSourceDeclaresANativeImportOutsideTheAgentsOne; AssemblySurface_ShippedAssemblies_AreEachPackagesBuiltAssembly
- AssemblyContractProbeTests: PRT_ENG_004_AC1_PublicClassNeitherSealedNorAbstractIsFound, PublicAsynchronousMethodWithoutATokenIsFound, PRT_ENG_004_AC2_LookupOfMembersByNameIsFound, LookupsOfGeneratedCodeAreLeftOut, NativeImportOutsideTheAgentIsFound, NativeImportOfTheAgentOtherThanItsOneIsFound, DllImportInASourceIsFound, LibraryImportOutsideTheAgentIsFound, LibraryImportOfTheAgentAndNamesInCommentsOrLiteralsAreAccepted
- PublicSurfaceBuildTests: PRT_ENG_004_AC1_PublicMemberWithoutDocumentationFailsTheBuild

**`06` PRT-ENG-007**: criterion 1, the public type whose name holds the library's name.
- AssemblyContractTests: PRT_ENG_007_AC1_NoPublicTypeNameHoldsTheLibrarysName
- AssemblyContractProbeTests: PRT_ENG_007_AC1_PublicTypeNamedAfterTheLibraryIsFound

**`06` PRT-ENG-008** (all but the fakes and the `linux` kind's container run): criteria 1, 2 and 3.
- PackageAllowListTests: PRT_ENG_008_AC1_NoMockingPackageIsReferenced, MockingPackageReferencedIsFound, MockingPackageTakenThroughAnotherIsFound
- TestNameGateTests: PRT_ENG_008_AC2_EveryListedCriterionWithItsTestPasses, ListedCriterionWithoutItsTestFails; TestNameGate_NoCriterionListed_Fails
- TestsGateTests: PRT_ENG_008_AC3_TestsOfOneKindEachRunAndPass, TestWithoutExactlyOneKindFailsTheTestsGate; TestsGate_FailingTest_FailsTheGate, TestsGate_NoTestProject_Fails

**`06` PRT-ENG-009** (all but the siblings' packages, phase 5): criterion 1.
- PackageAllowListTests: PRT_ENG_009_AC1_PackagesAreWithinTheTable, PackageOutsideTheTableIsFound, PackageVersionOutsideTheTableIsFound, PackageVersionWithinTheTableIsAccepted, PackageTakenByAReferenceOutsideTheProjectFileIsFound

**`06` PRT-ENG-010**: criterion 1.
- BuildSettingsTests: PRT_ENG_010_AC1_ARestoreResolvingAPackageWithAPublishedAdvisoryFailsTheBuild

**`06` PRT-ENG-011** (all but the release step): criterion 1, and criterion 2 without the release commit's exemption; the packages' description, authors and address, and the exact-version dependency.
- CommitMessageGateTests: PRT_ENG_011_AC1_CommitsWithinTheRulePass, NameInItsOwnSpellingPasses, CommitMessageOutsideTheRuleFails, BehaviourChangingCommitWithoutAChangelogLineFails, ChangelogLineOutsideUnreleasedFails, ChangelogLineUnderChangedPassesOnceAReleaseIsTagged, ChangelogLineUnderChangedFailsAfterATagThatIsNoRelease, MergeCommitsThePlatformWritesAreNotChecked, LocalMergeCommitOutsideTheRuleFails, PRT_ENG_011_AC2_CommitChangingAReleasedFileFails, CommitDeletingAReleasedFileFails, CommitMovingAReleasedFileDeletesItAndFails, NewProjectsPublicApiFileAndAnUnshippedChangePass; PreviousReleaseTag_TagsOfOtherShapes_AreLeftOut, PreviousReleaseTag_ReleasesAndReleaseCandidates_AnswersTheHighestByPrecedence, PreviousReleaseTag_TagOnHeadItself_IsLeftOut, PreviousReleaseTag_FirstRelease_IsNone
- PackTests: Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone
- PackageManifestRuleTests: ManifestViolations_PackageAsTheRuleGivesIt_HasNone, ManifestViolations_ToolWithItsPackageType_HasNone, ManifestViolations_PackageTypeOfAPackageOtherThanTheTool_IsFound, ManifestViolations_AnotherPackageProperty_IsFound, ManifestViolations_PropertyNotAsTheRuleGivesIt_IsFound, PackageTable_Descriptions_AreTheCellsWithoutMarkupOrReferences

**`06` PRT-ENG-012** (all but the agent's publish, release compatibility and sample host gates): the gates as scripts under `build/gates/`, `run-gates.ps1`, and the `gates` job of `.github/workflows/gates.yml`.
- RunGatesTests: RunGates_WorkingTreeOfHead_RunsEachGate, RunGates_FailingGate_FailsTheRun, RunGates_HeadOtherThanTheCheckedOutCommit_Refuses, RunGates_WorkingTreeOtherThanHeads_Refuses, RunGates_UntrackedFileOfAnExcludedPath_Runs, RunGates_NoGateScript_Fails

**`04` PRT-LIB-001** (the rows of `Portunus.Core`, `Portunus`, `Portunus.Testing` and the test projects): criteria 1 and 2.
- ProjectReferenceTests: PRT_LIB_001_AC1_EachProjectReferencesOnlyWhatItsRowAllows, ProjectReferenceOutsideTheTableIsFound, PackageReferenceOutsideTheTableIsFound, ClientsPackageWithinItsRowIsAccepted, TestProjectReferenceOutsideItsRowIsFound, TestProjectTakingTheTestingPackageIsAccepted, TestProjectPackageOutsideTheTestPackagesIsFound, ShippedProjectWithoutItsBuildOnlyReferencesIsFound, FrameworkReferenceIsFound, PackageReferenceInTheSharedBuildSettingsIsFound, ItemInATargetOtherThanTheExactVersionUpdateIsFound, GlobalPackageReferenceIsFound
- PackTests: PRT_LIB_001_AC2_EachPackageDependsOnTheLibrarysOtherPackagesAtTheExactVersionPacked
- PackageManifestRuleTests: PRT_LIB_001_AC2_DependencyOnALibraryPackageNotAtTheExactVersionPackedIsFound, DependencyAtTheExactVersionPackedIsAccepted, DependencyOnALibraryPackageNotPackedWithItIsFound

**`04` PRT-LIB-002** (the projects `Portunus.Core`, `Portunus` and `Portunus.Testing` and their empty surface files): criteria 1 and 2.
- PublicSurfaceBuildTests: PRT_LIB_002_AC1_AddingAPublicTypeWithoutItsPublicApiFileFailsTheBuild, RemovingAPublicTypeWithoutItsPublicApiFileFailsTheBuild, PublicSurfaceGateFailsOnAPublicTypeNoFileLists; PublicSurfaceGate_NoProjectUnderSrc_Fails
- AssemblyContractTests: PRT_LIB_002_AC2_EveryPublicTypeIsOneTheTablesGive, PublicApiFilesListExactlyThePublicTypes
- AssemblyContractProbeTests: PRT_LIB_002_AC2_PublicTypeTheFilesDoNotListIsFound, PublicTypeNoTableGivesIsFound

**`08` PRT-THR-004**: criteria 1 and 2.
- SecretScanTests: PRT_THR_004_AC1_ACommitCarryingACredentialShapedStringFailsTheScan, ACredentialShapedStringRemovedLaterStillFailsTheScan, PRT_THR_004_AC2_ALineCarryingTheInlineAllowMarkerFailsTheScan, ATrackedGitleaksIgnoreFailsTheScanWhateverItLists, AGitleaksIgnoreAtTheScannedRootFailsTheScanWhateverItLists, AFindingAGitleaksIgnoreOfTheWorkingDirectoryListsFailsTheScan; SecretScan_HistoryWithoutSecrets_Passes. Each test that writes the credential-shaped string also checks that the scan's output does not hold it.

**`00` PRT-OVR-001**: criterion 1, checked in review (below).

## Items not built

- `06` PRT-ENG-001: the sample host, its gate and `RELEASE-NOTES.md`, and criterion 2: phase 6. `deploy/` and `tests/Portunus.Fakes`, with the fakes' place in the layout rule: phase 3. `vendor/`, its part of criterion 1, and the mapping of `Janus.Core`, `Hermes.Core` and `Themis.Core` to `vendor/` in `nuget.config`, which names the folder phase 5 brings: phase 5. Until then `nuget.config` maps every identifier to nuget.org alone, and the layout rule checks that.
- `06` PRT-ENG-008: the fake `op` and the fake `ssh`; the run of the `linux` kind in a container of the .NET SDK's image on a machine other than Linux: phase 3, with the first `linux` test. The tests gate already takes the four kinds and runs each, the `linux` kind on the machine the gate runs on.
- `06` PRT-ENG-009: `Janus.Core`, `Hermes.Core` and `Themis.Core` are in the allow-list; their references and versions come with the adapters, phase 5.
- `06` PRT-ENG-011: the release commit's exemption from criterion 2, criteria 3 and 4, and the release job: phase 6.
- `06` PRT-ENG-012: the agent's publish gate, phase 3; the release compatibility and sample host gates, phase 6. The log gate has no value to look for yet; the integration tests that make one, from phase 2 on, carry it.
- `06` PRT-ENG-007 criterion 1 for codes, configuration keys and spellings: the phases that add them; phase 0 has none.
- `06` PRT-ENG-003, PRT-ENG-005, PRT-ENG-006: phase 1.
- `04` PRT-LIB-001: the rows of the agent (phase 3), the tool (phase 4) and the adapters (phase 5), and the adapters' exact-version dependency on their siblings (phase 5).
- `04` PRT-LIB-002: `Portunus.Agent`'s project comes in phase 3, `Portunus.Cli`'s in phase 4 and the adapters' in phase 5; each type comes with the first item that uses it, the types of `Portunus.Core` in phase 1. The type table the contract test holds already lists every type of PRT-LIB-002 and PRT-LIB-008. Criterion 3: phase 1, with `Secret`.

## Criteria checked without a test

- `00` PRT-OVR-001 criterion 1: every tracked file outside `docs/` read for a host product, company or application; none is named. The one address, `https://github.com/neolorn/portunus`, is the repository's own; the siblings are named only where `04` PRT-LIB-001 and `06` PRT-ENG-009 name them.
- `06` PRT-ENG-001, the fetch script's SHA-256 values: each of the six (`linux`, `windows` and `darwin`, `x64` and `arm64`) compared with `gitleaks_8.30.1_checksums.txt` of the gitleaks 8.30.1 release, and with the digest the release lists for each asset.
- `06` PRT-ENG-001, the actions: `actions/checkout` v7.0.1 (`3d3c42e5aac5ba805825da76410c181273ba90b1`) and `actions/setup-dotnet` v6.1.0 (`0f4a354dd5547a28c70b7853dbd95d5b67886e73`), each the latest release of its repository when the workflow was written, pinned by the commit its tag names.
- `06` PRT-ENG-002, `.editorconfig`: the output of `dotnet new editorconfig` for SDK 10.0.403, compared line by line, with the turn-offs of PRT-ENG-002 added after it.
- `06` PRT-ENG-008, tests written first: each rule's tests were written with its rule, before the gate script that runs them; the exact-version test, `PRT_LIB_001_AC2_EachPackageDependsOnTheLibrarysOtherPackagesAtTheExactVersionPacked`, was run failing before the target was added.
- `06` PRT-ENG-009, the versions: `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0, `MinVer` 8.0.0 and `xunit.v3` 4.0.2, each the latest stable release on nuget.org, listed, not deprecated and with no advisory; SDK 10.0.403, the latest .NET 10 SDK, for runtime 10.0.14.
- `06` PRT-ENG-010, vulnerability alerting: on for the repository, as the API answers for its vulnerability alerts.
- `06` PRT-ENG-011, the imperative description and the body only where the diff cannot explain itself: each commit message of `phase-00` read.

## Awaiting the pipeline's run

- `06` PRT-ENG-012 criterion 1, each gate running on the pull request and a failing one blocking the merge: the `gates` job runs every gate on pull request #1, and the protection of `main` requires its `gates` check. Its result on this commit is the gate of record, which the message naming the pull request gives.
- `06` PRT-ENG-012 criterion 2, each script giving locally the result the pipeline gives: `run-gates.ps1` passed every gate locally on Windows on `d71500b`, and the pipeline's `gates` run 41951617989 on `ubuntu-24.04` passed every gate on the same commit. Its result on this commit is in the message naming the pull request.

## Analyser suppressions

All in the root `.editorconfig`, each one `06` PRT-ENG-002 lists, its reason on the line above it:
- CA1303 for `*.cs`: exception messages are for developers and never shown to people.
- CA1812 under `src/`, `tests/` and `samples/`: an internal class the dependency injection container makes is made, though no code calls `new` on it.
- CA1032 for `src/Portunus.Core/SecretsStartupException.cs`: it always carries a code and details, which the standard constructors would leave out.
- CA1707 and CA1515 under `tests/` and `samples/*.Tests/`: a test's name carries its criterion's identifier (PRT-ENG-008); xUnit.net runs only public test classes.
- CS1591 and CA2007 under `tests/` and `samples/*.Tests/`: a test states no contract of its own; xUnit.net's own analyser refuses the `ConfigureAwait(false)` CA2007 asks for.

No suppression in code.

## Paths that make a string of a value or the token

None: phase 0 holds no code that handles a value or the token.

## Resolved by rule

- `tests/Portunus.Repository.Tests/SettingRules.cs`, `AllowUnsafeBlocks`: refused in every project file and shared build file but `src/Portunus.Agent/Portunus.Agent.csproj`, which may set it to `true` alone. Governs: `06` PRT-ENG-002. The value says the agent alone sets it, true; criterion 1 sets it aside from the listed properties for the agent alone.
- `tests/Portunus.Repository.Tests/SettingRules.cs`, `RuntimeIdentifier`, `RuntimeIdentifiers`, `SelfContained` and `PublishSingleFile`: refused in every project file and shared build file. Governs: `06` PRT-ENG-002. The value makes the agent's runtime identifier, self-contained and single-file publication arguments of the release's publish step, never properties of a project.
- `tests/Portunus.Repository.Tests/NativeRules.cs`, a native import: read from the built assemblies' metadata for the function imported, and from the sources of `src/` for its form (no `DllImport`, a `LibraryImport` in the agent alone). Governs: `06` PRT-ENG-004 criterion 2. A `LibraryImport` of a function whose arguments need no marshalling is built as the same metadata a `DllImport` is, so only the source tells the two apart.
- `build/gates/gate-commit-messages.ps1` and `build/gates/previous-release-tag.ps1`, a release tag: `vX.Y.Z` or `vX.Y.Z-rc.N`, `N` from 1, a release candidate ranking below its final release. Governs: `06` PRT-ENG-011 and P-017. The value makes a release candidate a release, tagged `v<V>`.
- `build/gates/gate-commit-messages.ps1`, a move of `PublicAPI.Shipped.txt` or `build/gates/release-baseline.json`: the gate reads changes with renames off, so a move is the deletion of the file at its path. Governs: `06` PRT-ENG-011 criterion 2. A commit that leaves no file at an existing released file's path deletes it.
- `build/gates/gate-commit-messages.ps1`, the footer: `BREAKING CHANGE:` lines are allowed beside `Implements:` lines. Governs: `06` PRT-ENG-011. The value names `BREAKING CHANGE:` as the mark of a break, and in Conventional Commits 1.0.0 it is a footer.
- `tests/Portunus.Repository.Tests/ReflectionRules.cs`, `Delegate.CreateDelegate`: counted as a lookup by name. Governs: `06` PRT-ENG-004 criterion 2. Making a delegate from a method's name is a lookup of a member by name.

## Open questions

1. **The push of `main` waits on the owner** (not a question of the specification). `git push origin main` is refused by the machine's permission settings; it was not worked around, and the owner pushes `main` himself. Settings set: dependency vulnerability alerting on, secret scanning on, push protection on, squash merges off. Not set: the default branch, which GitHub refuses to set on an empty repository (the API reports it as `main`, which the first push makes the default), and the protection of `main`, which comes after its push. **Settled by the owner's push of `main` at `bbeb746`**, now the default branch. Then the protection of `main` was set (the `gates` check required, force pushes and deletion refused, administrators not included), `phase-00` was pushed after the full-history secret scan passed, and pull request #1 was opened into `main`. Nothing was parked.
2. **Tier 2: the docs commit's description is not in the imperative.** Item: `06` PRT-ENG-011. The docs commit `df22b6c` reads "docs: the specification, its decisions and its plan"; the rule asks for a description in the imperative (checked in review), and the commit-message gate, which checks the form alone, passes it. Once pushed it is never rewritten. Readings: (a) it stands as written; (b) the lead rewords the unpushed commit before the push of `main` (for example "docs: specify the secrets library"), with the same tree, and `phase-00` is rebased onto it. Smallest fix for (a): none; for (b): the reworded commit, and the rebase, which changes nothing else. Nothing is parked. **Answered by the lead's message: reading (b).** The lead reworded the unpushed docs commit as `bbeb746` "docs: add the specification, its decisions and its plan", with the tree of `df22b6c`, and `phase-00` was rebased onto it. The commit-message gate and the full-history secret scan pass after the rebase.

## Checks

- The commit: the report's own commit, the head of `phase-00`, on `d71500b` (the report's first commit), on `3cf4d40`, itself on `main` at `bbeb746`.
- Fast checks on every commit: the build of `Portunus.sln` in Release with warnings as errors, the analysers, `dotnet format Portunus.sln --verify-no-changes`, and the unit tests: green.
- Before the push: every unit test, the commit-message gate over `main..HEAD` and over the whole history, the full-history secret scan, and every integration and contract test: green. `run-gates.ps1 -Base main -Head HEAD` on the report's own commit: every gate passed (build, commit messages, dependency allow-list, format, project references, public surface, restore, secret scan, test names, tests).
- The gate of record: the pipeline's `gates` run on this commit, on pull request #1, whose identifier and result the message naming the pull request gives. The run on `d71500b`, 41951617989, passed every gate.
