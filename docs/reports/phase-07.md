# Phase 7: The real `op` on the owner's computer

## Items built

Part 1, before the owner's first run.

**`06` PRT-ENG-008**, the scenarios, as P-043 gives them: `build/check-real-op.ps1`, renamed in git from `build/record-op-scenarios.ps1` in a commit of its own, then changed.
- It refuses an `op` other than 2.41.1 before it asks for anything.
- It asks for the two service accounts' tokens without echo, runs the agent's commands with the test service account's token, the tool's with none, so the 1Password app prompts, and one command with the network disconnected, as P-035's script did.
- It keeps what it records in memory, replaces every 1Password identifier and sign-in address with its placeholder (P-035), and refuses, removing everything, where an identifier is left, an `id` of an unknown form was recorded, or either token is in what `op` wrote. It writes the files under `tmp/check-real-op/` first, reads them again and checks them again.
- It compares the files with the recorded scenarios' files the commit holds (`git show HEAD:`), each RFC 3339 instant set aside and line endings as line feeds: the same set of files, each the same.
- On `changed` it writes the new files in place, removes a recorded file no longer recorded, builds `Portunus.sln`, runs the `integration` and `linux` tests through `build/gates/gate-tests.ps1 -Kinds integration, linux` and prints their result.
- A written scenario (P-028), one marked `"recorded": false`, and a file only it names are neither compared nor touched.
- It ends with one line, `op <version>, commit <sha>, same|changed|failed`, and exits 0 for `same`, 2 for `changed` and 1 for `failed`. A failed run removes `tmp/check-real-op/` and puts back any file it had put in place.

`build/gates/gate-tests.ps1` takes `-Kinds`, the kinds to run of the four, by default every one, as the gate runs them.

## Fixed after the owner's first runs

On `phase-07-fix`, two defects his runs found, under P-035 and P-043 as they stand:
- With no `op` on the `PATH`, the run stopped on a property of nothing and ended `op none ... failed`. It now ends with one line, `op was not found on the PATH: install op 2.41.1 and add its folder to the PATH, then run this again; commit <sha>, failed`, exits 1 and leaves nothing behind.
- A real item's custom fields land in a section whose id is `Section_` and an identifier, which each field's `section.id` and its `reference` hold too; the identifier was left, and the leak check stopped the run on `sample-test.item-1.out`. The identifier inside a section id of that form, under any member the script gathers, is now replaced everywhere, the references included. The leak check is as strict, and an `id` of any other unknown form, `Section_` and a string of another length among them, is still refused.

## Items built in part 2

After the owner's first run, which ended `op 2.41.1, commit eaf51c7, changed` with the `integration` and `linux` tests passed. The `linux` kind ran here in the SDK's container, where the agent's 58 tests and the tool's 6 passed.

**`06` PRT-ENG-008**, the scenarios:
- The owner's 33 recorded files, committed as he recorded them, in a `test:` commit of their own (P-043).
- The written scenarios, once, in the form P-028 gives (P-043 as f8f7582 amends it), each marked `"recorded": false`: `rate-limited`, the revoked token's recorded list with its standard error's status made `(429) Too Many Requests`; `wrong-version`, the recorded `op --version` answering 2.41.0.
- The fakes keep each call's process identifier beside its arguments and its environment's names (`tests/Portunus.Fakes`), so a test watches the `op` it started alone.
- The agent's tests: a test clock (`TestClock`), the recordings' reader (`Recording`), and the launcher's fake answering as a recorded scenario, its list paused where a test asks, the scenario swapped whole between attempts (`FakeOp.Answering`, `Answer`, `Environments`, `IsRunning`). The tool's tests: the recordings' reader, a scenario copied with its files (`Recording.Copy`, `Calls`), and the running agent answering as a recording through the fakes, its process `web` reading every secret of "Sample Test" through the harness's client (`RunningAgent.Prepared`, `ReadAsync`).

The tests phases 3 and 4 left waiting on the recordings:
- AgentStartTests (linux): PRT_AGT_002_AC1_OpOfAnotherVersionStopsTheStartNamingTheVersionFound, TokenFileMissingOrEmptyStopsTheStartWithTokenUnavailableHavingRunOnlyOpVersion
- AgentFetchTests (linux): PRT_AGT_003_AC1_VaultOfFourValidItemsIsFetchedWithOneListAndFourGetsAndTheAgentIsReady; PRT_AGT_003_AC2_VaultWithABrokenItemLeavesTheAgentWaitingAsLayoutInvalidHoldingNothing; PRT_AGT_003_AC3_FetchedAgentRunsNoFurtherOpWhateverTheTimeAndHoldsNoToken; PRT_AGT_003_AC4_OpPassingItsTimeoutIsKilledAndTheAttemptFailsAsUnreachable; PRT_AGT_004_AC1_AttemptsAfterFailuresStartAtZeroThenDoubleFromFirstWithinAFifthUpToMax; PRT_AGT_004_AC2_RateLimitMakesTheNextPauseMaxAndTheStatusRateLimited; PRT_AGT_004_AC3_SetBreakingTheLayoutIsFetchedOnceAndNeverAgain; PRT_AGT_005_AC1_StatusAnswersTheFailuresReasonBetweenAttemptsFetchingDuringOneAndReadyAfter; PRT_PSH_003_AC4_PushIntoAVaultAgentWaitingBetweenAttemptsEndsItsAttempts; PRT_AGT_001_AC4_FetchPushAndReadsLeaveNoValueInAnyFileOrLogLine
- ToolPushTests (linux): PRT_PSH_002_AC1_PushRunsTheCommandsWithTheirArgumentsAndEnvironmentAndTheAgentReceivesEveryField; PRT_PSH_002_AC3_PushWritesNoValueOnItsOutputOrErrorOrInAnyFile
- ToolVaultTests (integration): PRT_PSH_002_AC2_PushOfAVaultBreakingTheLayoutIsRefusedNamingItemAndFieldAndSshNeverRuns; PRT_PSH_002_AC4_OpBelowThePinnedVersionIsRefusedWithOpVersion; PRT_PSH_004_AC1_CheckOfAVaultWithOneErrorAndOneWarningWritesBothAndExitsOne, CheckOfAVaultWithAWarningAloneWritesItAndExitsZero

`build/gates/built-items.txt` holds each of these criteria.

**The test that timed out under load**, `AgentSecretClientTests.ReadAsync_AgentClosingWithoutAnAnswer_WaitsAsForAnAgentItCannotReach` ("No timer was made within the test's patience"). The cause: on Windows, a Unix domain socket at times never delivers the end of the client's sending side to a read that waits for it. The fake agent read each request to that end, so it waited for ever; the client's exchange is bounded by `Portunus:Client:Timeout` on the test's clock, which the test advances only to the client's pauses, so the exchange never ended and the test's patience passed. The fake agent now takes a request once it holds one whole JSON document, the form of every request, or at the end of the sending side, whichever comes first (`FakeAgent`). No retry and no patience changed; the client is unchanged, since the agent it talks to runs on Linux alone.

## Items not built

- `08` PRT-THR-001 criterion 1's test over this repository: parked, on the lead's message.
- The owner's check by hand on Windows (P-009): parked since phase 4, on the lead's message.

`06` PRT-ENG-011, the release's wait on the owner's run, is checked by the lead when a tag is pushed.

## What the owner makes once, then runs before each release

Once, on his own computer:
1. Install `op` 2.41.1 and put it on the `PATH`; turn on the 1Password app's integration with `op`.
2. Install PowerShell 7, Git, the .NET SDK that `global.json` names, and Docker Desktop, and start Docker Desktop.
3. Make a vault named "Sample Test" holding four Secure Note items. Give every field an obviously dummy value such as `sample-value-1`, never a random-looking one, which the secret scan could take for a credential:
   - `Themis`: a field `connection`, and a note.
   - `Janus`: fields `keys.1`, `keys.2`, `keys.3`, and `keys.current` holding `2`, and a note.
   - `Hermes`: a field `token`, and a note.
   - `Ceres`: a field `password`, and no note.
4. Make a vault named "Sample Broken" holding two Secure Note items:
   - `Themis`: fields `connection` and `bad label`, and a note.
   - `Hermes`: a field `token`, and no note.
5. Make a service account named `Portunus Test` that can read items in both vaults and nothing else. Save its token in your own private vault.
6. Make a service account named `Portunus Test Revoked` that can read items in "Sample Test". Save its token in your own private vault, then revoke the token.
7. Keep both vaults, both service accounts and both saved tokens for every later run.

Before each release, and now for the first run:
1. Open PowerShell 7 in a clone of the repository, on the commit the lead names, with no change of your own in it (`git status` shows nothing).
2. Run `pwsh build/check-real-op.ps1`.
3. Paste the `Portunus Test` token when it asks, then the `Portunus Test Revoked` token. Neither is shown as you paste it.
4. Approve each prompt of the 1Password app.
5. When it asks, disconnect the computer from every network and press Enter; when it asks again, reconnect and press Enter.
6. Read its last line, which names the `op` version, the commit and one word:
   - `same`: send the lead that line.
   - `changed`: send the lead that line, the line before it with the tests' result, and the files `git status` lists under `tests/Portunus.Fakes/`.
   - `failed`: send the lead what it printed. It prints no value, no token and no identifier, and it leaves nothing behind.

## Criteria checked without a test

- The script, tried against the fake `op` under `tmp/`, in a clone of this repository under `tmp/`, through a wrapper `op` that answers as the vaults, the revoked token and the network cut, its prompts answered by the trial:
  - the first run, no recorded file committed: `changed`, every file new and put in place, the solution built and the `integration` and `linux` tests passed, exit 2;
  - those files committed: `same`, exit 0; with only the instants of `op`'s answers changed: `same`;
  - one answer altered (a field's label), which the agent's and the tool's recordings both hold: `changed`, those two files differ, the tests passed, exit 2;
  - a committed written scenario: `same`, and it is untouched;
  - a token in `op`'s answer, an identifier in a member it does not gather (`uuid`), and an `id` of an unknown form: `failed`, exit 1, `tmp/check-real-op/` removed and nothing of `tests/Portunus.Fakes/` changed;
  - an `op` answering 2.41.0: `failed` before any prompt, exit 1.
- The fix, tried the same way, the fake `op`'s items now holding the section `add more` and an unnamed `Section_` one, which each field and its reference name; the script before the fix failing on both, as the owner's runs did:
  - no `op` on the `PATH`: the one line, exit 1, nothing left in the clone;
  - the first run: `changed`, every section id and reference holding the placeholder of its identifier, exit 2; of its tests, one of `Portunus.Tests` that waits on a test clock timed out once under the run's load and passed on each of eight runs after; those files committed: `same`, and `same` with only the instants changed;
  - a section id of `Section_` and 23 letters: `failed`, exit 1; a token, `uuid`, an `id` of an unknown form and 2.41.0: `failed` as before.
- The check on part 2's commits, with the recorded files committed: run in a clone under `tmp/` through a wrapper `op` that runs the fakes' `op` with the committed scenarios, chosen by the token and the vault each command names, its prompts answered by the trial: `op 2.41.1, commit 5ae10c2, same` and `op 2.41.1, commit f4e51f2, same`, exit 0, the clone unchanged and the written scenarios untouched. The placeholders the fake answers are of an identifier's form, and the check numbers identifiers in their sorted order, so each placeholder is replaced by itself.
- The cause of the client's test timing out: a program under `tmp/` whose client sends a request and shuts down its sending side and whose server reads to that end, on Windows, waited for ever on the server's read with the whole request read, at round 181 and round 287 of 300 in two of four runs; with the server taking the request once it is whole, 6000 rounds ended each one. Portunus.Tests' `integration` kind then passed ten runs in a row.
- The prompts without echo: `Read-Host -AsSecureString`, as P-035's script did; the trial answered them in its own session, since a secure prompt reads the console alone.
- `06` PRT-ENG-008, tests written first: the script is a tool the owner runs, tried by hand as above, as the recording script was in phase 4.

## Awaiting the pipeline's run

- Nothing beyond the gate of record.

## Analyser suppressions

- None new.

## Paths that make a string of a value or the token

- `build/check-real-op.ps1`: each token, as `OP_SERVICE_ACCOUNT_TOKEN` of `op`'s `ProcessStartInfo`, which demands a string, and as the string it is compared with in what `op` wrote; dropped once the files are checked. As P-035's script did.

## Resolved by rule

- `build/check-real-op.ps1`, the committed files compared: those of the commit (`git show HEAD:`), not the working tree's, since the run checks the commit checked out. Governs: P-043, "compares the files with those committed".
- `build/check-real-op.ps1`, the solution built before the tests on `changed`: the tests start the fakes and the harness, which the solution's build makes, as the gates build it before `gate-tests.ps1` runs. Governs: P-043, "runs the `integration` and `linux` tests on them".
- `build/check-real-op.ps1`, the exit codes: 0, 2 and 1 for `same`, `changed` and `failed`, beside the last line P-043 gives, so a run is told apart by its code too. Governs: P-043, "ends with one line".
- `build/gates/gate-tests.ps1`, `-Kinds`: by default every kind, so the gate runs as before. Governs: P-043 with `06` PRT-ENG-012.
- `tests/Portunus.Agent.Tests/FakeOp.cs`, the environment's names checked by PRT-AGT-003 criterion 1: less the fake's own variable and `PWD`, which the launcher's `/bin/sh` sets as it starts, after the agent's start, where the agent passed none. Governs: P-029, "checks them less the fake's own variable, which the script added after the agent's start".
- The searches of PRT-AGT-001 criterion 4 and PRT-PSH-002 criterion 3: every regular file under the test's folder but the fakes' copy of the recording, which holds the vault's values as `op` answered them, and each value as its bytes and in base64url; a set's `.current`, a version's number, is not searched, since the configuration's numbers hold it. Governs: `06` PRT-ENG-008, a scenario of the fakes is the test's.
- `tests/Portunus.Tests/FakeAgent.cs`, a request ended by its whole JSON document: the client sends nothing after its request, so the answer is the one the end of its sending side would give. Governs: `06` PRT-ENG-008, a fake agent answering recorded answers on a real socket.
- `tests/Portunus.Cli.Tests/OpCommandTests.cs`, the `op` checked killed: the one whose process the fake kept, since the tool's other tests now start the fake `op` beside it, and checked as ended within 10 seconds, since a kill takes effect a moment after it is sent (the pipeline's first run on Linux read it as running at once); the fake not killed waits 120. Governs: `02` PRT-PSH-002, the tool's child processes killed when it is cancelled.

## Open questions

1. **Tier 2: the written scenarios on the first run.** Item: `06` PRT-ENG-008 (P-028, P-043). P-043 leaves the written scenarios "as they are", and the check neither runs nor writes them; P-035's script wrote both, `rate-limited` from the revoked token's error with its status changed to `(429)`, and `wrong-version` from the recorded version changed to 2.41.0. No written scenario is committed yet, so after the first run none exists. Readings: (a) the check writes them on a run where none is committed, from what it recorded, as P-035's script did, and never after; (b) part 2 writes them from the first run's files, as P-028 gives their form, and the check never does. Smallest fix: (a) the lines P-035's script had, behind one condition; (b) none. Parked: nothing of part 1 under (b); under (a), the check changes before the owner's first run. Answered by f8f7582 (P-043): (b), as built in part 2.
2. **Tier 2: the order of `op item list`.** Item: `06` PRT-ENG-008, PRT-ENG-011 (P-043). In the owner's first run the two lists of "Sample Test", the agent's and the tool's, came in different orders (Janus, Hermes, Themis, Ceres; and Janus, Ceres, Hermes, Themis), and each item's file is numbered in its list's order. The check compares each file whole, its instants set aside, so a later run whose list comes in another order ends `changed` with no change of shape, and holds the release. Readings: (a) as P-043 gives it: such a run is `changed`, the lead reads it, and the new files come in a `test:` commit; (b) the check sets the lists' order aside too, comparing each list's entries and each item's file by the item's placeholder rather than by its place. Smallest fix: (a) none; (b) in the check's comparison, the list's entries in their identifiers' order and each item's file matched by its identifier. Parked: nothing.

## Checks

- The commit: the report's own commit, the head of `phase-07`, on `b986b90`; the branch stands on the lead's `cf2c781`.
- Fast checks on every commit; before the push, the full gates by `run-gates.ps1 -Base origin/phase-07 -Head HEAD`.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
- The fix: the head of `phase-07-fix`, on `01f361e`, before the push the full gates by `run-gates.ps1 -Base origin/main -Head HEAD`, and the pipeline's `gates` run on its pull request.
- Part 2: the report's own commit, the head of `phase-07-part2`, on `eaf51c7`; fast checks on every commit, the `linux` kind in the SDK's container, and before the push the full gates by `run-gates.ps1 -Base origin/main -Head HEAD`; the gate of record, the pipeline's `gates` run on its pull request. The first run failed on the tool's cancel test above, fixed in a new commit.
