# Phase 2: Client and testing

## Items built

Every test below was written before the code it proves. The client's tests are in `tests/Portunus.Tests`, the fixed client's in `tests/Portunus.Testing.Tests`, the formats' in `tests/Portunus.Core.Tests` and the contract tests in `tests/Portunus.Repository.Tests`. The client takes the packages of `06` PRT-ENG-009's hosting abstractions row, at 10.0.14, the release of the SDK's runtime.

**`05` PRT-OPS-001** (the client's keys): criterion 1. Each key of `Portunus:Client` is checked on its raw value, then bound by the binder's source generator and validated by the generated validator (`ClientConfiguration`, `ClientOptions`, `ClientOptionsValidator`).
- ClientConfigurationTests: PRT_OPS_001_AC1_EveryClientKeyHasItsDefaultWhenAbsent, ClientKeyOutsideItsRuleStopsTheStartNamingIt, SocketPathOfOneHundredAndEightBytesStopsTheStart, FileSourceWithoutAFileStopsTheStartNamingTheFile; Read_ValuesWithinTheirRules_AnswersThemBound

**`04` PRT-LIB-004**: criteria 1 to 4 (`AgentSecretClient`, `AgentConnection`, `ClientLog`).
- AgentSecretClientTests (integration, a fake agent on a real socket and a test clock): PRT_LIB_004_AC1_ReadWhileTheAgentWaitsAnswersTheSecretAfterTheWaitLoggedOnce; PRT_LIB_004_AC2_ReadWithNoSocketAnswersTheWaitTimeoutOnceItHasPassedAndNotBefore; PRT_LIB_004_AC3_DeniedAddressAnswersAtOnceWithoutWaiting; PRT_LIB_004_AC4_NoLogLineOfTheReadsHoldsAValueTheyUsed; ReadAsync_SecretOfThreeVersions_AnswersEachInArraysOfItsOwn, ReadAsync_AddressTheSetLacks_AnswersUnknownAtOnceAndLogsIt, ReadAsync_AgentOfAnotherProtocolVersion_AnswersTheVersionFailureAtOnce, ReadAsync_WaitTimeoutOfZero_AsksOnce, ReadAsync_AgentClosingWithoutAnAnswer_WaitsAsForAnAgentItCannotReach, ReadAsync_AnswerOutsideEveryForm_WaitsAsForAnAgentItCannotReach, ReadAsync_WaitOfMoreThanAMinute_LogsTheWaitAtMostEveryThirtySeconds, StatusAsync_ReadyAgent_AnswersItsStatus, StatusAsync_WaitingAgent_AnswersItsReason, StatusAsync_NoSocket_AnswersUnreachableOnce

**`04` PRT-LIB-003** (the client's making, `ClientFactory`): criteria 2 and 3. The client checks its configuration when it is made, and each refusal is the `SecretsStartupException` the item gives. The registration is not built (below).
- ClientFactoryTests (integration): PRT_LIB_003_AC2_ConfigurationValueOutsideItsRuleStopsTheStartNamingTheKey, PRT_LIB_003_AC3_FileSourceInAProductionHostStopsTheStart; Make_FileSourceInAHostThatRegistersNoEnvironment_StopsTheStartAsNotDevelopment, Make_FileThatCannotBeRead_StopsTheStartAsFileInvalid, Make_FileThatIsNotJson_StopsTheStartAsFileInvalid, Make_FileBreakingTheLayout_StopsTheStartNamingItsFirstFinding, Make_AgentSource_AnswersTheAgentsClient

**`04` PRT-LIB-005** and **`03` PRT-VLT-008**: criterion 1 of PRT-LIB-005 (`FileSecretClient`; `DevelopmentFile` and `VaultSet.Find`, internal to `Portunus.Core`).
- ClientFactoryTests: PRT_LIB_005_AC1_DevelopmentHostReadsTheFileAndAnAddressItLacksIsUnknown; Make_FileOfAVersionedSecret_AnswersItsLiveVersionsAndItsStatus
- DevelopmentFileTests: Read_FileOfItsForm_AnswersTheVaultItsItemsAndTheirValues, Read_EscapedValue_AnswersItsText, Read_MembersItDoesNotKnow_AnswersTheSet, Read_ItemTwice_AnswersBothForTheLayoutsCheck, Read_DocumentOutsideItsForm_AnswersNone
- VaultSetTests: Find_SingleSecret_AnswersItAsVersionOne, Find_VersionedSecret_AnswersItsLiveVersionsAndItsCurrentOne, Find_AddressOfAnotherCase_AnswersTheSecret, Find_SecretHoldingOnlyRetiredVersions_AnswersNone, Find_AddressTheSetLacks_AnswersNone

**`05` PRT-OPS-004** (the client's health check, `ClientHealthCheck`): criterion 1. Its registration by `AddPortunus` is not built (below).
- ClientHealthCheckTests (integration): PRT_OPS_004_AC1_ReadyAgentReadsHealthy, WaitingAgentReadsDegradedWithTheReason, AgentItCannotReachReadsDegradedWithUnreachable; CheckHealthAsync_FileSource_ReadsHealthy

**`05` PRT-OPS-005** (the client's logs): the client logs its waits and each failure it answers through `LoggerMessage` methods, by address, reason and code. Criterion 1 for the client: each integration test of `tests/Portunus.Tests` checks its captured log for every value it used when it ends.

**`04` PRT-LIB-008** (`FixedSecretClient`): its members, with no criterion of its own (criterion 1 below).
- FixedSecretClientTests: ReadAsync_NothingSet_AnswersUnknown, StatusAsync_NothingSet_AnswersReady, ReadAsync_SecretSet_AnswersItAtAnAddressOfAnyCase, ReadAsync_SecretRemoved_AnswersUnknown, ReadAsync_AddressDenied_AnswersDeniedWhileWaitingToo, ReadAsync_WhileWaiting_AnswersTheWaitTimeoutWithTheReason, ReadAsync_Released_AnswersTheSecretAgain, Reads_EveryAddressAsked_InOrder
- AssemblyContractTests (repository): PRT_LIB_002_AC2_TestingDeclaresTheMembersOfTheFixedClient

**`06` PRT-ENG-008** (the fake agent): `FakeAgent` answers recorded answers of `07` section 6 on a real Unix domain socket under `tmp/`, and `TestClock` is the test `TimeProvider`.

**`06` PRT-ENG-007** (the configuration keys): criterion 1 for the keys.
- AssemblyContractTests (repository): PRT_ENG_007_AC1_EveryConfigurationKeyTheLibraryBindsIsOneTheReferenceLists
- AssemblyContractProbeTests (repository): PRT_ENG_007_AC1_ConfigurationKeyTheReferenceDoesNotListIsFound

**`07`**: section 1, the startup codes, `secrets.secret.unknown`, `secrets.secret.denied`, `secrets.agent.waittimeout` and `secrets.agent.unreachable`; section 2, `Portunus:Client:Source`; section 4, the client's keys; section 5, the client's wait log.

## Items not built

- `04` PRT-LIB-003, the registration: `AddPortunus`, its one call registering once, the `IHostedLifecycleService` that makes the client, and criterion 1. Parked by open question 1.
- `05` PRT-OPS-004, the health check's registration as `portunus` by `AddPortunus`. Parked by open question 1.
- `04` PRT-LIB-008, `AddPortunusTesting`. Parked by open question 1. Criterion 1 also needs `AddPortunusThemisSecrets`: phase 5.
- `05` PRT-OPS-001: the adapters' keys in phase 5; the agent's keys and its file's safety in phase 3.
- `05` PRT-OPS-004 and PRT-OPS-005, the agent's part: phase 3; the tool's and the adapters' logs: phases 4 and 5.
- `06` PRT-ENG-005 criterion 2, the buffers after a read through internal hooks (`01` PRT-AGT-001 criterion 3): phase 3.
- `03` PRT-VLT-005 criterion 2, a secret holding only retired versions not delivered by the agent: phase 3. The development file's client answers it as unknown already (Find_SecretHoldingOnlyRetiredVersions_AnswersNone).

## Criteria checked without a test

- `06` PRT-ENG-005 criterion 1, reviewed in each path of the client that holds a value:
  - The agent's client: `AgentConnection` reads the answer into `PinnedBufferWriter`, whose arrays are pinned and zeroed; `AnswerFormat.Read` decodes each value into the pinned arrays of `PinnedValues`, disposed, and so zeroed, in a `using` once `OwnedSecret` has copied each value into a new pinned array of the caller's own. The request holds an address alone.
  - The development file's client: `FileSecretClient` reads the file whole with `RandomAccess` into a pinned array, zeroed in a `finally`. `DevelopmentFile` unescapes each value with `Utf8JsonReader.CopyString` into a pinned array of `PinnedValues`, zeroed when the client is disposed. Each read copies through `OwnedSecret`. What the runtime may hold while it unescapes a field's text is the copy P-023 names for `op`'s output.
  - The fixed client holds and answers the `Secret` a test set, in the test's own arrays.
  - No value becomes a `string`.
- `05` PRT-OPS-005 criterion 1, for the client: each integration test class of `tests/Portunus.Tests` asserts in its disposal that no captured line holds a value the test used, as its text, its base64 or its base64url.
- `06` PRT-ENG-008, tests written first: each item's tests were written before the code they prove.

## Awaiting the pipeline's run

- The integration tests on Linux, a Unix domain socket under the runner's `tmp/`. Here they ran on Windows' own Unix domain sockets. The gate of record is the `gates` run on this commit, which the message naming the pull request gives.

## Analyser suppressions

None added. No suppression in code.

## Paths that make a string of a value or the token

None. The strings the client makes are addresses, reasons, codes, keys, titles and labels.

## Resolved by rule

- `src/Portunus.Core/DevelopmentFile.cs`, a value of the development file: the field's text as the vault holds it, its UTF-8 bytes, not base64url. Governs: `03` PRT-VLT-008 with `07` section 6, which writes `<b64>` where a value is base64url and the development file's form `<value>`. It is read with `CopyString` into a pinned array, as `06` PRT-ENG-005 gives for a field's text.
- `src/Portunus.Core/DevelopmentFile.cs`, the reader's form: a member it does not know left out; a member named twice at the top, and anything after the document's object, refused. Governs: `06` PRT-ENG-006, whose reader rules `07` section 6 applies to the development file. An item title or a label given twice is kept, so the layout's check names it as a duplicate.
- `src/Portunus/FileSecretClient.cs`, the development file's size: read whole, with no bound of its own. Governs: `07` section 5, which bounds the set's values (1 MiB, checked by the layout's check) and gives the file no bound.
- `src/Portunus/ClientConfiguration.cs`, an absolute path: the running platform's own fully qualified form (`Path.IsPathFullyQualified`), and the socket's 107 bytes counted in UTF-8. Governs: `07` section 4, "an absolute path of at most 107 bytes".
- `src/Portunus/ClientConfiguration.cs`, `Portunus:Client:Source`: one of `agent` and `file` exactly as `07` section 2 spells them.
- `src/Portunus/ClientConfiguration.cs`, a duration: .NET's constant `TimeSpan` form (`c`). Governs: `07` section 4, "a duration is written in .NET's `TimeSpan` form (`00:00:05`)".
- `src/Portunus/ClientConfiguration.cs`, an absent key: not checked, its default taken. Governs: `05` PRT-OPS-001, which checks a key's raw value, and an absent key has none.
- `src/Portunus/AgentSecretClient.cs`, an answer over its bound, outside every form, or of a status's form to a read: no answer, so the read waits as for an agent it cannot reach. Governs: `01` PRT-AGT-008, a connection carrying one request and one answer of `07` section 6.
- `src/Portunus/AgentSecretClient.cs`, the reason of `secrets.agent.waittimeout`: that of the last ask, the agent's reason where it answered `waiting` and `unreachable` where it could not be reached. Governs: `04` PRT-LIB-004, "the last reason the agent gave, or `unreachable`".
- `src/Portunus/AgentSecretClient.cs`, the last pause: the shorter of `PollInterval` and the time left, so the last ask comes as `WaitTimeout` passes and the timeout is answered then. Governs: `04` PRT-LIB-004, waiting "until `WaitTimeout` has passed since the call began".
- `src/Portunus/ClientLog.cs`, a failure the client answers: logged at the Warning level, with the address and the code. Governs: `05` PRT-OPS-005 with `04` PRT-LIB-004, the one level `04` gives the client's lines.
- `src/Portunus/AgentSecretClient.cs`, `StatusAsync`: an agent's failure answered as it is; another protocol version as `secrets.request.version`, as a read answers it; anything else as `secrets.agent.unreachable`. While `waiting`, `Items` is 0. Governs: `04` PRT-LIB-004 and `04` PRT-LIB-002, whose `Items` is given while `Ready`.
- `src/Portunus/ClientHealthCheck.cs`, a status the check cannot read: `Degraded` with `unreachable`. Governs: `05` PRT-OPS-004, whose `Degraded` cases are `waiting` and an agent that cannot be reached, never `Unhealthy`.
- `src/Portunus/FileSecretClient.cs`, the status's instants: `Since` and `LoadedAt` both the instant the file was read. Governs: `04` PRT-LIB-005, "loaded when the file was read", the moment the client became ready.
- `src/Portunus/OwnedSecret.cs`, the caller's arrays: pinned. Governs: `06` PRT-ENG-005, every array holding a value pinned.
- `src/Portunus.Testing/FixedSecretClient.cs`:
  - a denied address is answered as denied while waiting too (P-008, a denied address answered at once whatever the state);
  - each address holds one setting, so `Set`, `Deny` and `Remove` replace one another;
  - `Ready` reads the source `vault`, `LoadedAt` the instant it became ready, and `Items` the items a secret is set in;
  - a read answers the `Secret` as it was set, its arrays the test's own.

  Governs: `04` PRT-LIB-008, an `ISecretClient` answering what the test sets, as the agent would.

## Open questions

1. **Tier 2: a host in the client's tests.** Items: `04` PRT-LIB-003 (the registration and criterion 1), PRT-LIB-008 (`AddPortunusTesting`), `05` PRT-OPS-004 (the check's registration).

   What the code needs: a test that makes the one call and reads a secret needs a service container, `Microsoft.Extensions.DependencyInjection`'s `BuildServiceProvider`, or a generic host. `06` PRT-ENG-009 and `04` PRT-LIB-001 give the client's and the testing package's test projects xunit.v3 alone, and nothing they reference brings the container: the hosting abstractions hold `ServiceCollection` but no provider. Configuration is there already, through `Microsoft.Extensions.Configuration`, which the binder depends on.

   Readings:
   - (a) Add `Microsoft.Extensions.DependencyInjection` as a test package for `tests/Portunus.Tests` and `tests/Portunus.Testing.Tests`, so a test builds a provider from the one call and starts its hosted services by hand.
   - (b) Give those test projects `Microsoft.Extensions.Hosting`, already in the table for the agent, so a test builds a generic host and runs its start; this also brings the container.
   - (c) No package: a hand-written container in the tests. It cannot make the types `AddHealthChecks` and the options register without reflection, which `06` PRT-ENG-004 refuses, so it cannot prove the health check's registration.

   Smallest fix:
   - (a) one row of `06` PRT-ENG-009, one package version, and one reference in each of the two test projects;
   - (b) the same, with the hosting package;
   - (c) none in the documents, and criterion 1 of PRT-LIB-003 proven by a fake.

   Parked: `AddPortunus`, the hosted service, the health check's registration, `AddPortunusTesting`. Everything they register is built and tested on its own.

## Checks

- The commit: the report's own commit, the head of `phase-02`, on `5b083b6`; the branch stands on `main` at `5cf2b5c` with the lead's `81a75a9` and `67f178f` first.
- Fast checks on every commit:
  - each commit of `phase-02` built in Release with warnings as errors, with its analysers, in a clone of its own with a locked restore, and the unit tests of the client, the testing package and Core run: green;
  - `dotnet format Portunus.sln --verify-no-changes`: green.
- Before the push, all green:
  - every unit test, and the integration tests this phase wrote;
  - the commit-message gate and the full-history secret scan;
  - `run-gates.ps1 -Base main -Head HEAD` on the report's own commit: every gate passed.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
