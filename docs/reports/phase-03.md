# Phase 3: The agent

## Items built

Every test below was written before the code it proves, in `tests/Portunus.Agent.Tests` but for the unit files' tests in `tests/Portunus.Repository.Tests`. The `linux` kind runs as root (P-027): here in the SDK's container, where all 20 passed, and under `sudo` on the pipeline's runner. The agent takes `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.Systemd` and `Microsoft.Extensions.Configuration.Json` at 10.0.14.

**`01` PRT-AGT-001**: criteria 1 and 2. The start marks the agent not dumpable (`prctl`), then refuses an active swap area before it reads anything (`StartChecks.Swap`).
- AgentStartTests (linux): PRT_AGT_001_AC1_ActiveSwapAreaRefusesTheStartAndReadsNothing, PRT_AGT_001_AC2_AgentMarkedNotDumpableAnswersZero

**`01` PRT-AGT-002** and **`05` PRT-OPS-001** (the agent's keys and its file's safety): PRT-AGT-002 criterion 1 for the refusals before `op` runs, and criterion 2; PRT-OPS-001 criteria 1 and 2. The file and each directory above it are read through `statx` (P-026); each key is checked on its raw value, then bound and validated by the generated validator (`AgentConfiguration`, `AgentOptions`, `AgentOptionsValidator`).
- AgentConfigurationTests (unit): PRT_OPS_001_AC1_EveryAgentKeyHasItsDefaultWhenAbsent, AgentKeyOutsideItsRuleStopsTheStartNamingIt, RootLeavingASocketPathOverItsBytesStopsTheStart; Read_FileThatIsNotJson_StopsTheStartAsConfigurationInvalid
- AgentStartTests (linux): PRT_OPS_001_AC2_FileWritableByItsGroupStopsTheStart; PRT_AGT_002_AC1_DirectoryAboveTheFileWritableByOthersStopsTheStartNamingIt, KeyOutsideItsRuleStopsTheStartNamingIt, DeclarationOutsideItsRuleStopsTheStartNamingProcessAndMember; MainAsync_CommandOutsideItsRule_IsAFault
- AgentSignalTests (linux, the harness): PRT_AGT_002_AC2_PushModeStartsWithNoTokenFileAndNoOp

**`01` PRT-AGT-007**: criterion 1.
- AgentConfigurationTests (unit): PRT_AGT_007_AC1_DeclarationOutsideItsRuleStopsTheStartNamingProcessAndMember, SixtyFiveDeclarationsStopTheStartNamingTheSixtyFifth; Read_DeclarationOfTheRule_AnswersItsAddresses

**`01` PRT-AGT-006**: criteria 1, 2 and 3 (the directories' part). Owners and groups are set through `fchownat` with `AT_SYMLINK_NOFOLLOW` (P-026), and modes through the runtime (`SocketDirectories`, `AgentServer`).
- AgentServingTests (linux): PRT_AGT_006_AC1_EachDirectoryAndSocketHasItsOwnerGroupAndMode, DirectoryFoundWithOtherOnesIsCorrected; PRT_AGT_006_AC2_ConnectionFromAnotherUserIsClosedWithoutAnAnswerAndLogged; PRT_AGT_006_AC3_RestartLeavesEachDirectoryInPlaceByInode

**`01` PRT-AGT-005**: the states and their status (`SetHolder`); criterion 1 for a `push` agent's `waiting`; criterion 2 for the exits 3 and 1.
- AgentServingTests (linux): PRT_AGT_005_AC1_PushAgentAnswersWaitingForAPushOnThePushSocket; PRT_AGT_005_AC2_StatusOfAWaitingAgentExitsThreeWithItsAnswer, StatusOfAnAgentItCannotReachExitsOne

**`01` PRT-AGT-008**: the serving of a connection: at most 4 KiB within 5 seconds, 64 at once, the declaration's check, the answers of each state, and a request of a kind the socket does not take.
- AgentServingTests (linux): ServeAsync_ReadOnThePushSocket_AnswersRequestInvalid

**`01` PRT-AGT-009**: criterion 1.
- AgentSignalTests (linux, the harness): PRT_AGT_009_AC1_SigtermStopsTheAgentWithExitZeroWithinTenSecondsItsSocketsRemoved
- AgentServingTests (linux): PRT_AGT_009_AC1_StoppedAgentRemovesItsSockets

**`01` PRT-AGT-003** and **PRT-AGT-004**: the fetch, `OpRunner`, `OpJson`, `Fetcher`, `TokenFile` and `RetryPolicy`, built as `01` gives them; their criteria wait (below).
- RetryPolicyTests (unit): Next_FailuresInARow_DoubleFromFirstWithinAFifthAndNeverBelowFirst, Next_RateLimit_IsMaxAtOnce, Next_ManyPauses_SpreadEitherWay, Classify_StandardError_AnswersItsClass

**`05` PRT-OPS-003** criterion 2: `deploy/portunus-agent@.service`, `deploy/token.conf` and `deploy/example.json`.
- DeployTests (repository): PRT_OPS_003_AC2_UnitAndDropInHoldEveryLineOfTheirSettings, UnitLackingALineIsFound; Settings_Reference_GiveTheUnitAndTheDropInTheirLines

**`05` PRT-OPS-005** (the agent's logs): `AgentLog`, through `LoggerMessage` methods, by instance, title, address, process, user, reason, code, count and `op`'s command and exit code.

**`04` PRT-LIB-001**, the agent's row, and **`06` PRT-ENG-001**: `src/Portunus.Agent` with its `Fetch` and `Serving` folders, `tests/Portunus.Fakes` (the fake `op`, `PORTUNUS_FAKES_SCENARIO`), `tests/Portunus.Agent.Harness` (P-027), and `deploy/`; the repository's rules give each its row, and the native rule the agent's three functions.
- The phase 0 tests of ProjectReferenceTests, GrantTests, RepositoryLayoutTests and AssemblyContractTests, now over these projects; AssemblyContractProbeTests: PRT_ENG_004_AC2 (the native import's message)

**`06` PRT-ENG-008**, the `linux` kind, and **PRT-ENG-012**, the agent's publish gate: `build/gates/gate-tests.ps1` runs the `linux` kind under `sudo` on Linux and in the SDK's container elsewhere; `build/gates/gate-agent-publish.ps1` publishes the agent self-contained and single-file for `linux-x64` and `linux-arm64`, from a copy under `tmp/`.

**`03` PRT-ROT-003**: no code; the token is read at the start alone, so a rotated token reaches the agent by its restart.

## Items not built

Waiting on the owner's recordings (P-005, P-028):
- The fake `op`'s scenario files: the recordings of the vault "Sample Test", of the broken vault, and of the error cases; the `(429)` scenario is written once the others give its shape (P-028).
- `01` PRT-AGT-003 criteria 1 to 4, PRT-AGT-004 criteria 1 to 3, the fetch's part of PRT-AGT-005 criterion 1, and PRT-AGT-001 criterion 4's fetch.

Parked by open question 1 (every test that runs `op` through the agent):
- `01` PRT-AGT-002 criterion 1, the refusals `secrets.agent.opversion` and `secrets.agent.tokenunavailable`, and "starts no `op` but `op --version`".

Parked by open question 2 (a client of a process's socket as the declared user):
- `01` PRT-AGT-007 criterion 2, PRT-AGT-008 criteria 1, 3 and 4, and PRT-AGT-006 criterion 3's client.

Waiting on a set (a fetch, or the push of phase 4):
- `01` PRT-AGT-001 criterion 3, PRT-AGT-005 criterion 2's exit 0, PRT-AGT-008 criterion 2, PRT-AGT-007 criterion 2 for a ready agent.

Phase 4:
- `portunus-agent receive` and the push's taking (`02` PRT-PSH-003); until then a push on the push socket answers `secrets.request.invalid`.
- `06` PRT-ENG-001, the fake `ssh`.

## Criteria checked without a test

- `06` PRT-ENG-005 criterion 1, reviewed in each path of the agent that holds a value or the token:
  - the token is read with `RandomAccess` into a pinned array, made a string only for `op`'s environment, and zeroed when dropped (`AgentToken`), after a fetch succeeds and at the stop;
  - `op`'s standard output and standard error are read from their base streams into `PinnedBufferWriter`s of at most 4 MiB, zeroed when disposed; a value is unescaped by `CopyString` into a pinned array of `PinnedValues`;
  - a request is read into a `PinnedBufferWriter` of 4 KiB; an answer is written by `AnswerFormat` into pinned arrays and disposed once sent;
  - the set's values are zeroed when it is replaced, once no read holds it, and at the stop (`SetHolder`).
- `05` PRT-OPS-005 criterion 1, for the agent: no log line names a value, the token or `op`'s output; the reads' lines name the process, the address and the outcome.
- `06` PRT-ENG-008, tests written first.

## Awaiting the pipeline's run

- The `linux` kind under `sudo` on the runner (`gate-tests.ps1`), and the agent's publish gate for both runtimes.

## Analyser suppressions

- `src/Portunus.Agent/Fetch/RetryPolicy.cs`, CA5394, on the method: the pauses' spread keeps agents from asking together and guards nothing; `06` PRT-ENG-004 gives the injected `Random`.
- `src/Portunus.Agent/Fetch/OpRunner.cs`, CA2025, around the two reads: both are awaited on every path before their buffers are disposed; the rule does not follow a task held in a local.

## Paths that make a string of a value or the token

- `src/Portunus.Agent/Fetch/OpRunner.cs`: the token, as `OP_SERVICE_ACCOUNT_TOKEN` of `op`'s `ProcessStartInfo`, which demands a string (`01` PRT-AGT-003, `06` PRT-ENG-005).

## Resolved by rule

- `src/Portunus.Agent/AgentSettings.cs`, `op`'s configuration directory and home: `<root>/<instance>/.op`, mode `0700`. Governs: `01` PRT-AGT-003, "a directory under the agent's runtime directory", with `00` section 1.5, whose names never start with a dot, so no process's directory can be it.
- `src/Portunus.Agent/Serving/SocketDirectories.cs`, the instance's directory where it is missing: made with mode `0711`. Governs: `07` section 7, `RuntimeDirectoryMode=0711`, the mode the unit gives it.
- `src/Portunus.Agent/Serving/SocketDirectories.cs`, the agent's own user: the owner of `/proc/self`. Governs: `01` PRT-AGT-006, "owned by the agent's user", which is the process's effective user.
- `src/Portunus.Agent/AgentConfiguration.cs`, a declaration's details: `member` is `name` for a process's name outside its rule or `push`, and `Processes`, with `process` empty, for no declaration, or the 65th in ordinal order for too many. Governs: `01` PRT-AGT-007 criterion 1, naming the process and the member.
- `src/Portunus.Agent/AgentConfiguration.cs`, a file that is not JSON: `secrets.agent.configurationinvalid` naming `Portunus:Agent`. Governs: `01` PRT-AGT-002; no key of such a file is within its rule.
- `src/Portunus.Agent/AgentConfiguration.cs`, `Retry:Max` absent where `Retry:First` is over an hour: refused naming `Retry:Max`, whose default is then outside its rule. Governs: `07` section 4, "from `Retry:First` to 1 day".
- `src/Portunus.Agent/Fetch/RetryPolicy.cs`, a pause: the doubled one, or `Max` after a rate limit, then moved by up to a fifth either way, never below `First`, and the doubling going on from `Max` after a rate limit. Governs: `01` PRT-AGT-004 criterion 1, "never closer than `First`".
- `src/Portunus.Agent/AgentCommand.cs`, a command or an instance outside its rule: `secrets.fault`. Governs: `06` PRT-ENG-003, a mistake only the caller makes.
- `src/Portunus.Agent/AgentCommand.cs`, `status` where the agent's answer is neither `ready` nor `waiting`: exit 1 with `secrets.agent.unreachable`. Governs: `01` PRT-AGT-005, "1 where it cannot reach it".
- `src/Portunus.Agent/Fetch/OpRunner.cs`, `op --version`: run with no token. Governs: `01` PRT-AGT-003, which gives the token to the fetch's commands.
- `src/Portunus.Agent/Fetch/OpJson.cs`, an item's note: present where `notesPlain` holds text. Governs: `01` PRT-AGT-003 with `03` PRT-VLT-002.

## Open questions

1. **Tier 3: the fake `op`'s scenario and the agent's environment.** Items: `01` PRT-AGT-002 criterion 1 (the `op` refusals), PRT-AGT-003, PRT-AGT-004. `06` PRT-ENG-001 has the fake answer "from a scenario file a test names in an environment variable of the fake's own", and AGENTS.md has the fakes the only `op` a test runs; `01` PRT-AGT-003 runs `op` in an environment holding nothing but `OP_SERVICE_ACCOUNT_TOKEN`, `OP_CACHE` and `HOME`, and its criterion 1 checks that environment. A fake started by the agent never sees its variable, so no test runs `op` through the agent. Parked: the tests above; the code is built.
2. **Tier 2: a client of a process's socket in the `linux` tests.** Items: `01` PRT-AGT-007 criterion 2, PRT-AGT-008 criteria 1, 3 and 4, PRT-AGT-006 criterion 3. The tests run as root, and a declaration never names root, so every connection the test makes is refused by the peer check before its request is read. A served read needs a client running as the declared user, which .NET starts no child as. Readings:
   - (a) `tests/Portunus.Agent.Harness` takes a second role, a client sending one request and writing the answer, which a test starts under `setpriv --reuid --regid`;
   - (b) `tests/Portunus.Fakes` takes that role;
   - (c) a system tool (`socat` or `nc -U`) in the SDK's container and on the runner.

   Smallest fix: (a) one command of the harness and a row of `06` PRT-ENG-001; (b) the same in the fakes; (c) a package in the container and none in the repository. Parked: the tests above; the serving is built.
3. **Tier 3: the token file's line ending.** Item: `01` PRT-AGT-003, `05` PRT-OPS-002. The token is read as the file holds it, so a file written with a final line ending passes it to `op`. Parked: nothing; the code reads the file as `01` gives it.

## Checks

- The commit: the report's own commit, the head of `phase-03`, on `0c6e8ec`; the branch stands on `main` at `0630cef` with the lead's `cf2d63f` first.
- Fast checks on every commit: built in Release with warnings as errors and the analysers; `dotnet format --verify-no-changes`; the unit tests of the projects each commit changes; green.
- Before the push: every unit test, the integration tests, the `linux` kind in the SDK's container (20 tests), the agent's publish for both runtimes, the commit-message gate and the full-history secret scan, by `run-gates.ps1 -Base main -Head HEAD`: every gate passed.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
