# Phase 4: The push and the tool

## Items built

Every test below was written before the code it proves. The `linux` kind runs as root (P-027): here in the SDK's container, where the agent's 44 and the tool's 3 passed, and under `sudo` on the pipeline's runner. A test that reads a process's socket runs the harness as `client` under `setpriv` as the declared user (P-030).

Phase 3's items taken up again from the lead's answers:

**`01` PRT-AGT-006** (P-032): a connection to a process's socket whose `SO_PEERCRED` cannot be read is closed without an answer and logged with the peer `unknown` (`AgentServer.ServeAsync`, `PeerUser`). Criterion 3's client part:
- AgentServingTests (linux): PRT_AGT_006_AC3_ClientHoldingTheDirectoryReachesTheNewSocket

**`01` PRT-AGT-002 and PRT-AGT-003** (P-031): the token is read less one final `\n` or `\r\n`, and a file holding a line ending alone is unavailable (`TokenFile`). PRT-AGT-002 criterion 1's `secrets.agent.opversion` where `op` does not run, and where it answers no version, having run only `op --version` through the test's launcher (P-029):
- TokenFileTests (unit): Read_FinalLineEnding_IsLeftOut, Read_AnyOtherByte_Stays, Read_LineEndingAloneOrNothing_IsUnavailable, Read_OverFourKibibytes_IsUnavailable
- AgentStartTests (linux): PRT_AGT_002_AC1_OpThatDoesNotRunStopsTheStartWithOpVersionFoundEmpty, PRT_AGT_002_AC1_OpAnsweringNoVersionStopsTheStartHavingRunOnlyOpVersion

**`01` PRT-AGT-007** criterion 2, **PRT-AGT-008** criteria 1 to 4 (P-030), through the harness's client:
- AgentServingTests (linux): PRT_AGT_007_AC2_UndeclaredAddressIsDeniedWhileWaiting; PRT_AGT_008_AC1_DeclaredReadWhileWaitingAnswersWaitingAndItsReasonAtOnce; PRT_AGT_008_AC3_RequestOverFourKibibytesIsClosedWithoutAnAnswer, RequestNotFinishedWithinFiveSecondsIsClosedWithoutAnAnswer; PRT_AGT_008_AC4_PushOnAProcessSocketAnswersRequestInvalid
- AgentPushTests (linux): PRT_AGT_007_AC2_UndeclaredAddressIsDeniedWhileReadyWhetherOrNotTheSetHoldsIt; PRT_AGT_008_AC2_DeclaredReadWhileReadyAnswersTheValueOfASingleSecret, DeclaredReadWhileReadyAnswersEachVersionOfASetAndItsCurrentOne

**`01` PRT-AGT-001** criterion 3 and **PRT-AGT-005** criteria 1 and 2 for a `ready` agent, its set given by a push:
- AgentPushTests (linux): PRT_AGT_001_AC3_SetBeforeAReplaceAndSetAtTheStopHoldZeros; PRT_AGT_005_AC1_StatusAfterAPushAnswersReadyWithItsSourceInstantAndItemCount; PRT_AGT_005_AC2_StatusOfAReadyAgentExitsZeroWithItsAnswer

Phase 4:

**`02` PRT-PSH-003**: `portunus-agent receive` (`AgentCommand.ReceiveAsync`) and the push's taking on the push socket (`AgentServer.Take`, `SetHolder.TryFill`), its check of the vault, of `replace` and of the layout, the end of the fetch's attempts and the token dropped; each push logged with the peer, the outcome and the number of items. Criteria 1, 2, 3 and 5:
- AgentPushTests (linux): PRT_PSH_003_AC1_PushIntoAWaitingAgentMakesItReadyFromPushAndAReadAnswersIt; PRT_PSH_003_AC2_PushIntoAReadyAgentWithoutReplaceIsRefusedAndChangesNothing, PushIntoAReadyAgentWithReplaceReplacesTheSet; PRT_PSH_003_AC3_PushOfAnotherVaultIsRefusedWithWrongVault; PRT_PSH_003_AC5_PayloadOverTwoMebibytesIsRefusedByReceiveAndTheAgentReceivesNothing; Take_VaultTitleOfAnotherCase_IsTheAgentsVault, Take_SetBreakingTheLayout_IsRefusedNamingItsFirstFindingsItemAndField, Receive_AgentNotRunning_IsRefusedWithUnreachable

**`02` PRT-PSH-005** criterion 1, through the harness, the launcher's fake `op` never called:
- AgentPushTests (linux): PRT_PSH_005_AC1_PushModeAgentWithNoTokenFileRunsNoOpWaitsAndIsReadyAfterAPush

**`02` PRT-PSH-002 and PRT-PSH-004**: `src/Portunus.Cli`, the .NET tool `portunus` (`PackAsTool`, `ToolCommandName` `portunus`): its options checked before anything runs (`ToolArguments`); `op` in the tool's environment less `OP_SERVICE_ACCOUNT_TOKEN`, `OP_CONNECT_HOST`, `OP_CONNECT_TOKEN` and every `OP_SESSION_` variable, its version checked against 2.41.1 or a later 2.x, its output read into pinned buffers (`ToolEnvironment`, `OpCommand`, `OpJson`, `VaultReader`); the layout's check; the payload written by `RequestFormat.TryWritePush`; `ssh` found on the `PATH` and run as `02` gives it (`SshCommand`); `check`'s findings and `status` (`ToolCommand`). PRT-PSH-004 criterion 2:
- ToolStatusTests (linux, the fake `ssh` into the harness's agent): PRT_PSH_004_AC2_StatusOfAWaitingAgentExitsThreeWithItsAnswer, StatusOfAReadyAgentExitsZeroWithItsAnswer, StatusOfAnAgentItCannotReachExitsOne
- ToolArgumentsTests (unit): MainAsync_OptionMissingOrOutsideItsRule_IsRefusedNamingItBeforeAnythingRuns, MainAsync_CommandOrWordOutsideTheTable_IsAFault, MainAsync_StatusWithNoSshOnThePath_IsRefusedWithSshMissing, MainAsync_PushWithNoOpOnThePath_IsRefusedWithOpVersionFoundEmpty; IsDestination_WithinItsRule_IsTrue, OutsideItsRule_IsFalse, OverTwoHundredFiftyFiveCharacters_IsFalse; IsSupported_Version_IsThePinnedOrALaterOfItsMajor; ForOp_Environment_LeavesOutTheTokenConnectAndSessionVariables

**`04` PRT-LIB-001**, the tool's row: `Portunus.Cli` references `Portunus.Core` alone, which grants it its internals; `tests/Portunus.Cli.Tests`; the repository's rules give the tool its row.
- The phase 0 tests of ProjectReferenceTests, GrantTests, RepositoryLayoutTests, PackTests and AssemblyContractTests, now over these projects

**`06` PRT-ENG-001** and **PRT-ENG-008**: the fakes act as `ssh`, running the command after the destination on this machine (`tests/Portunus.Fakes`); the fakes' executable is copied as `op` and `ssh` at its build; the launcher of P-029 (`FakeOp`); the harness's `client` role (P-030).

**The owner's recordings** (P-035): `build/record-op-scenarios.ps1`, run once by the owner. It keeps what it records in memory and replaces every 1Password identifier (a vault's, an item's, a user's, an account's, a field's and a section's, gathered from the members `id`, `*_id`, `*_by` and `*account`) with a placeholder of its length and case in letters alone, the same identifier always the same placeholder across every file, the commands' arguments and the `op://` references included, and an account's sign-in address with `account<n>`. It writes nothing where a token, or any string of an identifier's form but a placeholder, is left, and it reads what it wrote again, removing all of it on a finding. Tried here against the fake `op` in `tmp/` through every case, and through its two refusals, an identifier in a member it does not gather and a token in what `op` wrote, each of which wrote nothing and exited 1.

## Items not built

Waiting on the owner's recordings (P-005, P-028), which `build/record-op-scenarios.ps1` writes as flat files of `tests/Portunus.Fakes/`: `version.out`; `sample-test`, `sample-broken` (the agent's commands), `sample-test-tool`, `sample-broken-tool` (the tool's), `refused`, `unreachable` (recorded); `rate-limited`, `wrong-version` (written, marked not recorded):
- `01` PRT-AGT-002 criterion 1's `secrets.agent.opversion` naming a version found, and its `secrets.agent.tokenunavailable` through the start, which runs only after `op --version` answers the pinned version, so it needs the recorded `--version`; `TokenFile`'s rule is proven by unit tests above.
- `01` PRT-AGT-003 criteria 1 to 4, PRT-AGT-004 criteria 1 to 3, the fetch's part of PRT-AGT-005 criterion 1, PRT-AGT-001 criterion 4.
- `02` PRT-PSH-002 criteria 1 to 4, PRT-PSH-003 criterion 4, PRT-PSH-004 criterion 1.

What the owner makes and runs:
1. `op` 2.41.1 on the `PATH` of the owner's computer, the 1Password app's integration on, and PowerShell 7.
2. A vault "Sample Test" of four Secure Note items, each field a custom text or password field with an obviously dummy value (`sample-value-1`, not a random-looking one, which the secret scan could take for a credential): `Themis` (`connection`, a note), `Janus` (`keys.1`, `keys.2`, `keys.3`, `keys.current` holding `2`, a note), `Hermes` (`token`, a note), `Ceres` (`password`, no note: the one warning).
3. A vault "Sample Broken": `Themis` (`connection` and `bad label`, the one error, a note), `Hermes` (`token`, no note: the one warning).
4. A service account `Portunus Test` with `read_items` on both vaults, its token at hand.
5. A service account `Portunus Test Revoked` with `read_items` on "Sample Test", its token copied, then revoked.
6. From the repository's root, `pwsh build/record-op-scenarios.ps1`: it asks for both tokens without echo, the 1Password app prompts for the tool's commands, and it asks for the network to be disconnected for one command and reconnected after.
7. The files it writes under `tests/Portunus.Fakes/` handed over.

The launcher, the fake and the scenarios' names are in place, so each of these is a test and no code. Their tests are not committed before the files: a test that fails until they land would fail the gate, and a skipped test is never left.

## Criteria checked without a test

- `01` PRT-AGT-006, P-032: a peer whose credentials cannot be read. Reviewed in `AgentServer.ServeAsync` and `PeerUser`, which answers none where `SO_PEERCRED` answers another length or throws; the refusal and its log line are the ones of PRT-AGT-006 criterion 2's test. No test makes the kernel refuse `SO_PEERCRED` on a Unix domain socket, and a seam for it would be a test switch in the shipped code.
- `06` PRT-ENG-005 criterion 1, reviewed in each new path that holds a value or the token:
  - `TokenFile` reads the file into a pinned array, copies the token less its line ending into a second, and zeroes the first in a `finally`;
  - `receive` reads standard input into a `PinnedBufferWriter` of 2 MiB, and the push socket receives into one of the same bound; both are zeroed when disposed;
  - a push's values are `RequestFormat`'s pinned arrays, zeroed on a refusal and owned by the holder once taken, which zeroes them when the set is replaced or the agent stops;
  - the tool reads `op`'s output into `PinnedBufferWriter`s of 4 MiB, values by `CopyString` into `PinnedValues`, writes the payload into a pinned document, and disposes all of them before it exits; `ssh`'s answer is read into a pinned buffer.
- `05` PRT-OPS-005, for the push: the line names the peer's user, the outcome and the number of items, never a value.
- `02` PRT-PSH-002, the owner's check by hand (plan, phase 4's inputs): that a push prompts through the 1Password app on the owner's Windows computer and that none of its `op` commands writes a value to the terminal. Awaiting the owner; macOS and Linux not checked by hand until the owner has one to check on.
- `06` PRT-ENG-008, tests written first.

## Awaiting the pipeline's run

- The `linux` kind under `sudo` on the runner with the `setpriv` client, the tool's `status` through the fake `ssh`, and the launcher's fake `op` started in the agent's environment of three variables, which finds the runtime where the SDK's setup installs it.

## Analyser suppressions

- `src/Portunus.Cli/OpCommand.cs`, CA2025, around the two reads: both are awaited before their buffers are disposed; the rule does not follow a task held in a local.
- `src/Portunus.Cli/SshCommand.cs`, CA2025, around the read of the answer: likewise.

## Paths that make a string of a value or the token

- No new path. The token's one string stays the one of phase 3 (`OpRunner`); the tool holds no token.

## Resolved by rule

- `src/Portunus.Agent/Serving/AgentServer.cs`, the push socket's bound: a request of at most 2 MiB, received within the same 5 seconds as any connection. Governs: `02` PRT-PSH-003 (a payload of at most 2 MiB) with `01` PRT-AGT-008 (a connection's request within 5 seconds).
- `src/Portunus.Agent/SetHolder.cs`, a replaced set's `since`: the instant the agent became `ready`, its new set's `loadedAt` its own. Governs: `01` PRT-AGT-005, "the instant the state began".
- `src/Portunus.Agent/AgentService.cs`, a fetch that ends after a push filled the agent: its set is zeroed, the push's kept. Governs: `02` PRT-PSH-003, a push "ends any retries", with `01` PRT-AGT-005, no move back.
- `src/Portunus.Agent/Serving/AgentServer.cs`, a layout finding with no item or field: the detail written empty. Governs: `02` PRT-PSH-003, `details.item` and `details.field` always named.
- `src/Portunus.Agent/AgentCommand.cs`, `receive` with no answer from the agent: `secrets.push.unreachable`. Governs: `07` section 1, "`receive` cannot reach the push socket".
- `src/Portunus.Cli/ToolArguments.cs`, a command `07` does not list, or a word no option of it: `secrets.fault`; an option given twice: `secrets.cli.inputinvalid` naming it. Governs: `06` PRT-ENG-003, as phase 3's agent commands, and `07` section 1, "an option outside its rule".
- `src/Portunus.Cli/OpCommand.cs`, `op`'s standard error: read and never kept, nor shown. Governs: `02` PRT-PSH-002 step 3, "as the agent does".
- `src/Portunus.Cli/ToolCommand.cs`, `ssh` exiting 255 with no answer: `secrets.push.unreachable`; the agent's command refusing on its own standard error, which `ssh` carries to the terminal: exit 1 and no second line. Governs: `07` section 1, "the tool cannot reach the server".
- `tests/Portunus.Fakes/`, the scenarios as flat files of the project. Governs: `06` PRT-ENG-001, every project flat but the agent's two folders.

## Open questions

Phase 3's questions, answered:
1. The fake `op`'s scenario and the agent's environment: P-029. Built: the launcher; PRT-AGT-002 criterion 1's `op` refusals where `op` does not run.
2. A client of a process's socket: P-030. Built: the harness's `client`; the criteria above.
3. The token file's line ending: P-031. Built.
4. The review's finding, a peer whose credentials cannot be read: P-032. Built.

Asked in phase 4, answered:

5. **Tier 2: the spelling of `secrets.cli.inputinvalid`'s `details.member`.** Item: `02` PRT-PSH-002. `07` section 1 gives the detail `member` for "an option missing or outside its rule", and section 3 writes each option as `--vault`. Readings: (a) the option as section 3 writes it, `--vault`; (b) its name alone, `vault`. Smallest fix: one constant per option in `ToolArguments`. Answered by P-033: (a), as built.
6. **Tier 2: an SSH destination's `@`.** Item: `07` section 3, "ASCII letters, digits, `.`, `-`, `_` and one `@`". Readings: (a) at most one, so `host` alone is a destination; (b) exactly one, so a user is always named. Smallest fix: one comparison in `ToolArguments.IsDestination`. Answered by P-034: (a), as built.

## Checks

- The commit: the report's own commit, the head of `phase-04`, on `3a40144`; the branch stands on the lead's `36ffdaa` and `c26f8d6`, on `370ee0e` of `phase-03`.
- Fast checks on every commit: built in Release with warnings as errors and the analysers; `dotnet format --verify-no-changes`; the unit tests of the projects each commit changes; green.
- Before the push: every unit test, the integration tests, the `linux` kind in the SDK's container, the agent's publish, the commit-message gate and the full-history secret scan, by `run-gates.ps1 -Base main -Head HEAD`: every gate passed on `ac0b659` but the commit-message gate, which finds one body line of `2c90ede` over 72 characters; the unpushed commits' replay that rewords it waits on the owner's decision, so nothing is pushed.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
