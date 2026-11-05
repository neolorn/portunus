# Phase 5: The adapters

## Items built

Every test below was written before the code it proves.

The review's two findings of phase 4, first:

**`02` PRT-PSH-002**, the tool's `PATH`: a folder of the `PATH` that is not an absolute path is skipped, so a program planted in the working directory never runs (`ToolEnvironment.Find`).
- ToolEnvironmentTests (unit): Find_ProgramInAnAbsoluteFolder_IsFound, Find_ProgramInAFolderRelativeToTheWorkingDirectory_IsSkipped

**`02` PRT-PSH-002**, a cancelled command: `ToolCommand.MainAsync` takes a cancellation token through to `op` and `ssh`; on cancellation `OpCommand` and `SshCommand` kill the child's whole process tree before awaiting its reads, as the agent's `OpRunner` does. The entry point passes none, as before.
- OpCommandTests (integration, the fake `op` waiting): MainAsync_CancelledWhileOpRuns_KillsOp
- SshCommandTests (linux, the fake `ssh` running a waiting script of the test's): MainAsync_CancelledWhileSshRuns_KillsSshAndTheCommandItStarted

Phase 5:

**`06` PRT-ENG-001**, `vendor/` and `nuget.config` (P-036): `vendor/` holds `Janus.Hosting.1.0.0-rc.4.nupkg` and `Hermes.Core.1.0.0-rc.6.nupkg`, copied from `Cura/server/vendor/` with no download, each SHA-256 first checked against its release asset's published digest (`gh api repos/neolorn/<sibling>/releases/tags/<tag>`), and `vendor/SHA256SUMS`; `nuget.config` adds `vendor/` as a second source and maps `Janus.*`, `Hermes.Core` and `Themis.Core` to it alone, every other identifier to nuget.org alone. Criterion 1's part for `vendor/`:
- RepositoryLayoutTests (unit): PRT_ENG_001_AC1_VendoredFilesMatchTheirSums, VendoredFileWhoseShaDiffersIsFound, VendoredFileTheSumsDoNotListIsFound, VendoredFileOfTheListedShaIsAccepted; PRT_ENG_001_AC1_PackageSourcesFollowTheRule, PackageSourcesOutsideTheRuleAreFound, PackageSourceMappingOutsideTheRuleIsFound

**`06` PRT-ENG-009** and **`04` PRT-LIB-001**, the siblings' row: `Janus.Hosting`, `Hermes.Core` and `Themis.Core` are the siblings' packages of the allow-list, each pinned exactly in `Directory.Packages.props`; a packed adapter's dependency on its sibling is checked to be the exact version of `07` section 7. The Hermes adapter's row: `Portunus` and `Hermes.Core`.
- PackageManifestRuleTests (unit): PRT_LIB_001_AC2_AdapterDependingOnItsSiblingNotAtTheExactVersionIsFound, AdapterDependingOnItsSiblingAtTheExactVersionIsAccepted
- The phase 0 tests of PackageAllowListTests, ProjectReferenceTests, GrantTests, RepositoryLayoutTests, PackTests and AssemblyContractTests, now over `Portunus.Hermes`

**`04` PRT-LIB-006**, Hermes, **`03` PRT-ROT-002**, Hermes's rows, **`00` PRT-OVR-002** for Hermes, and P-037: `src/Portunus.Hermes`. `AddPortunusHermesSecrets` calls `AddPortunus` and registers Hermes's `ISecretSource` as a singleton, once (`HermesSecretsServiceCollectionExtensions`). Each name is read at `Portunus:Hermes:Items:<name>`'s item where set, else `Portunus:Hermes:Item`'s (default `Hermes`), each key checked on its raw value and a key outside its rule stopping the start naming it (`HermesConfiguration`). The table's base64 names are decoded (RFC 4648 section 4, padded, no white space) into pinned arrays, the text they came in zeroed (`Base64Text`); the text names, `smsCequens` among them, and any name the table does not list pass as their UTF-8 bytes, an unlisted one logged once at Information; every live version goes in ascending order; any failure answers none, a denied address logged at Warning with the name first, a value not of its form logged as `secrets.secret.forminvalid` with its address (`HermesSecretSource`, `HermesLog`). Built against `Hermes.Core` 1.0.0-rc.6 as released, with no change to it.
- HermesSecretSourceTests (unit, a fixed client): PRT_LIB_006_AC1_TextNameAnswersItsValueAsVersionOne, Base64NameAnswersEachVersionDecodedInAscendingOrder, DeniedAddressAnswersNoneAndLogsTheNameFirst, SecretTheAgentDoesNotHoldAnswersNone, ItemOfTheNameIsReadWhereItsKeyIsSet; PRT_ROT_002_AC1_KeySetOfTwoVersionsCurrentTwoReachesHermesAsOneAndTwoInOrder; PRT_OPS_001_AC1_HermesItemHasItsDefaultWhenAbsent, HermesKeyOutsideItsRuleStopsTheStartNamingIt; ReadAsync_Base64NameWhoseValueIsNotOfItsForm_AnswersNoneAndLogsTheForm, NameTheTableDoesNotList_AnswersItsTextAndLogsItOnce, NameOutsideTheSecretNamesRule_AnswersNone, Base64Name_ZeroesTheTextItWasHanded

## Items not built

The siblings' versions published today, the latest release candidate of each: Janus `v1.0.0-rc.4`, Hermes `v1.0.0-rc.6`, Themis `v1.0.0-rc.2`, the versions of `07` section 7.

Parked by open question 1 (the Janus adapter cannot restore):
- `src/Portunus.Janus` and its tests: `04` PRT-LIB-006's Janus table, its criteria 1 to 3 for Janus, PRT-ROT-002's Janus rows and criterion 1's Janus part, PRT-OVR-002 for Janus; the allow-list's `Janus.Hosting` closure, with each package's licence (P-036).

Waiting on `Themis.Core.1.0.0-rc.2.nupkg`, the owner's download:
- `src/Portunus.Themis` and its tests: PRT-LIB-006's Themis table, PRT-OVR-002 for Themis.

Parked by open question 2:
- `04` PRT-LIB-006 criterion 4 for each adapter.

For these reasons `build/gates/built-items.txt` gains no line of PRT-LIB-006, PRT-ROT-002 or PRT-OVR-002 yet: each criterion is listed once its three siblings are proven.

Still parked from phase 4, waiting on the owner: the fake `op`'s recordings and his check by hand on Windows.

## Criteria checked without a test

- `06` PRT-ENG-005 criterion 1, the Hermes adapter: a base64 value is decoded into a pinned array and the text it came in, the client's array, zeroed in a `finally`; a decoded version of a set that fails part way is zeroed; values handed to Hermes are Hermes's from then on.
- `05` PRT-OPS-005, the adapter's log lines: a name, an address and a code, never a value.
- `06` PRT-ENG-008, tests written first.

## Awaiting the pipeline's run

- The tool's `ssh` cancellation test under `sudo` on the runner.

## Analyser suppressions

- None new.

## Paths that make a string of a value or the token

- None new. The Hermes adapter hands Hermes `ReadOnlyMemory<byte>` values, as its type demands.

## Resolved by rule

- `src/Portunus.Hermes/HermesConfiguration.cs`, the adapter's keys: read from the configuration as strings and checked on their raw values, with no binding through the binder's source generator, since both keys are strings and `04` PRT-LIB-001 gives an adapter no reference but `Portunus` and its sibling. Governs: `05` PRT-OPS-001 with `04` PRT-LIB-001.
- `src/Portunus.Hermes/HermesConfiguration.cs`, an item title and a secret name checked by `SecretAddress`'s own rule, the core's name rules being internal to the packages `06` PRT-ENG-001 grants them to. Governs: `00` section 1.5, `07` section 4.
- `src/Portunus.Hermes/HermesSecretSource.cs`, a name Hermes asks for outside the secret name's rule: none, logged at Warning with the name, since no address can hold it. Governs: `04` PRT-LIB-006, "none for any failure".
- `src/Portunus.Hermes/Base64Text.cs`, the base64 form: the alphabet of RFC 4648 section 4 and `=` alone, a length of a multiple of four, no white space. Governs: `03` PRT-VLT-003, "padded, no whitespace".
- `tests/Portunus.Repository.Tests/PackageRules.cs`, the siblings' exact versions held beside the rule as `07` section 7 gives them. Governs: `04` PRT-LIB-001 criterion 2, P-022, P-036.

## Open questions

1. **Tier 2: `Janus.Hosting` 1.0.0-rc.4 brings packages with known vulnerabilities.** Items: `04` PRT-LIB-006 (Janus), `06` PRT-ENG-010. The restore of `Portunus.Janus` fails with NU1902 and NU1903: `System.Security.Cryptography.Xml` 10.0.11 (GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj, GHSA-r955-8q5j-3h83, high) and `System.Security.Cryptography.Pkcs` 10.0.11 (GHSA-2fmv-rv2c-59x7, moderate; GHSA-846f-f764-76mx, high), which `OpenIddict.Client.DataProtection` and `OpenIddict.Server.DataProtection` 7.7.1 take through `Janus.Hosting`'s dependencies; 10.0.14 of both is published. PRT-ENG-010 makes a known vulnerability fail the build, and no gate may be weakened. Readings:
   - (a) `Directory.Packages.props` pins both at 10.0.14 for the repository through central transitive pinning (`CentralPackageTransitivePinningEnabled`), each joining the allow-list's `Janus.Hosting` closure;
   - (b) Janus takes the patched versions in a release candidate after rc.4, and the Janus adapter waits for it;
   - (c) `Portunus.Janus` references both directly, which its row of PRT-LIB-001 does not allow.

   Smallest fix: (a) one setting and two lines; (b) a Janus release and a new pin in `07`; (c) a row of PRT-LIB-001. Parked: the Janus adapter and the closure's listing.
2. **Tier 2: the host of PRT-LIB-006 criterion 4 in an adapter's tests.** Item: `04` PRT-LIB-006 criterion 4, "in a host built with the adapter's call alone, the sibling's `ISecretSource` resolves to the adapter's, once". `06` PRT-ENG-009 gives `Microsoft.Extensions.Hosting` to `tests/Portunus.Tests` and `tests/Portunus.Testing.Tests` alone, and an adapter's test project takes no container through its references (`Portunus.Hermes.Tests`' lockfile holds `Microsoft.Extensions.DependencyInjection.Abstractions` and no container). Readings:
   - (a) the adapters' test projects join the row of `Microsoft.Extensions.Hosting` (P-025);
   - (b) the criterion is proven on the registration's descriptors alone, which names one singleton of the sibling's `ISecretSource` and no host.

   Smallest fix: (a) one row of PRT-ENG-009; (b) the criterion's wording. Parked: criterion 4 for each adapter.

## Checks

- The commit: the report's own commit, the head of `phase-05`, on `43cd405`; the branch stands on the lead's `2817881`, on `995eb03` of `main`.
- Fast checks on every commit: built in Release with warnings as errors and the analysers; `dotnet format --verify-no-changes`; the unit tests of the projects each commit changes; green.
- Before the push: every unit test, the integration tests, the `linux` kind in the SDK's container, the agent's publish, the commit-message gate and the full-history secret scan, by `run-gates.ps1 -Base main -Head HEAD`.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
