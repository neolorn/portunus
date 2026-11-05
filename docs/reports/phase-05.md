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
- HermesSecretsServiceCollectionExtensionsTests (unit, a real host, P-039): PRT_LIB_006_AC4_HostBuiltWithTheAdaptersCallAloneResolvesItsSourceOnce

**`06` PRT-ENG-009** and **PRT-ENG-010**, the siblings' row (P-038): `Directory.Packages.props` pins `System.Security.Cryptography.Xml` and `System.Security.Cryptography.Pkcs` at 10.0.14 through `CentralPackageTransitivePinningEnabled`, which `Janus.Hosting` 1.0.0-rc.4 brought at 10.0.11 with known advisories; the setting rule takes that one property beside `ManagePackageVersionsCentrally`. The allow-list lists `Janus.Hosting`'s closure, each package with the licence its package declares (`JanusClosure`): Apache-2.0 (Dapper, OpenIddict and its 17 packages), BSD-3-Clause (Polly and its 4 packages), ISC (libsodium), PostgreSQL (Npgsql, Npgsql.EntityFrameworkCore.PostgreSQL), MIT (every other, Otp.NET by its licence file), and the two pins, MIT; `Portunus.Janus`'s lockfile may take no package outside the table, the closure and the pins.
- PackageAllowListTests (unit): PRT_ENG_009_AC1_JanusAdaptersPackagesAreWithinTheTableOrJanusHostingsClosure, JanusAdaptersPackageOutsideTheClosureIsFound, PatchedPinsBesideJanusHostingAreAccepted
- SettingRuleTests (unit): the test of `Directory.Packages.props`' properties, now with the pinning accepted

**`04` PRT-LIB-006**, Janus, **`03` PRT-ROT-002**, Janus's rows, **`00` PRT-OVR-002** for Janus: `src/Portunus.Janus`, against `Janus.Hosting` 1.0.0-rc.4 as released, whose `Janus.Core` carries `ISecretSource` (P-036). `AddPortunusJanusSecrets` calls `AddPortunus` and registers Janus's `ISecretSource` as a singleton, once; its keys `Portunus:Janus:Item` (default `Janus`) and `Portunus:Janus:Items:<name>` are read and checked as Hermes's are (`JanusConfiguration`). `keyEncryptionKeys` and `fingerprintKeys` are decoded from base64 into pinned arrays, a key-encryption key checked for 16, 24 or 32 bytes before `KeyEncryptionKeys` is built, the texts zeroed; `maintenanceCredential` and `mailServerSecret` pass as their UTF-8 bytes; `socialProvider.<provider>` passes as `ProviderCredential.Secret` where it does not begin with `{`, else as `ProviderCredential.Signed` from the object `{ "issuer", "keyId", "key" }`, each member a string and once, `key` decoded into a pinned array and the object's text zeroed, any other object not of its form (`SignedCredential`). Each failure answers `model.startup.secretunavailable`, `details.key` the name Janus gives the secret, a value not of its form logged as `secrets.secret.forminvalid` with its address (`JanusSecretSource`, `JanusLog`).
- JanusSecretSourceTests (unit, a fixed client): PRT_LIB_006_AC1_KeyEncryptionKeysAnswerEveryVersionAndTheCurrent, FingerprintKeysAnswerEveryVersionAndTheCurrent, MaintenanceCredentialAnswersItsBytes, MailServerSecretAnswersItsBytes, ProviderCredentialOfPlainTextAnswersASecret, ProviderCredentialOfAnObjectAnswersASignedOne, ProviderObjectOfAnotherFormIsRefusedNamingTheSecret, EachFailureIsRefusedNamingTheSecret; PRT_LIB_006_AC2_KeySetWhoseVersionDecodesToTwentyBytesIsRefusedAndLogged; PRT_LIB_006_AC3_ProvidersItemIsReadWhereItsKeyIsSet; PRT_ROT_002_AC1_KeySetOfTwoVersionsCurrentTwoReachesJanusAsCurrentTwoWithBoth; PRT_OPS_001_AC1_JanusItemHasItsDefaultWhenAbsent, JanusKeyOutsideItsRuleStopsTheStartNamingIt; ReadKeyEncryptionKeysAsync_Read_ZeroesTheTextItWasHanded
- JanusSecretsServiceCollectionExtensionsTests (unit, a real host, P-039): PRT_LIB_006_AC4_HostBuiltWithTheAdaptersCallAloneResolvesItsSourceOnce
- The phase 0 tests of ProjectReferenceTests, GrantTests, RepositoryLayoutTests, PackTests and AssemblyContractTests, now over `Portunus.Janus`

## Items not built

The siblings' versions published today, the latest release candidate of each: Janus `v1.0.0-rc.4`, Hermes `v1.0.0-rc.6`, Themis `v1.0.0-rc.2`, the versions of `07` section 7.

Waiting on `Themis.Core.1.0.0-rc.2.nupkg`, the owner's download:
- `src/Portunus.Themis` and its tests: PRT-LIB-006's Themis table, its criteria 1 and 4 for Themis, PRT-OVR-002 for Themis.

`build/gates/built-items.txt` gains PRT-LIB-006 criteria 2 and 3 and PRT-ROT-002 criterion 1, whose siblings are Janus and Hermes alone; PRT-LIB-006 criteria 1 and 4 and PRT-OVR-002 criterion 1 are listed once Themis's adapter proves them too.

Still parked from phase 4, waiting on the owner: the fake `op`'s recordings and his check by hand on Windows.

## Criteria checked without a test

- `06` PRT-ENG-005 criterion 1, the adapters: a base64 value is decoded into a pinned array and the text it came in, the client's array, zeroed in a `finally`; a decoded version of a set that fails part way is zeroed; values handed to a sibling are the sibling's from then on; Janus's provider object is zeroed once its key is decoded into an array of its own, its issuer and key identifier, which are no secret, made strings as `ProviderCredential.Signed` demands.
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
- `tests/Portunus.Repository.Tests/SettingRules.cs`, `Directory.Packages.props`' properties: `CentralPackageTransitivePinningEnabled` taken beside `ManagePackageVersionsCentrally`, the one setting the pins need. Governs: `06` PRT-ENG-009, "pins the patched version by central transitive pinning" (P-038).
- `src/Portunus.Janus/JanusSecretSource.cs`, a provider whose name makes no secret name (`socialProvider.<provider>` outside the rule): `model.startup.secretunavailable` naming it, as any failure. Governs: `04` PRT-LIB-006, "any failure answers".
- `src/Portunus.Janus/SignedCredential.cs`, the signing credential's object: each of its three members a string, each once, and no other. Governs: `04` PRT-LIB-006, "any other object refused as not of its form".

## Open questions

1. **Tier 2: `Janus.Hosting` 1.0.0-rc.4 brings packages with known vulnerabilities.** Items: `04` PRT-LIB-006 (Janus), `06` PRT-ENG-010. The restore of `Portunus.Janus` fails with NU1902 and NU1903: `System.Security.Cryptography.Xml` 10.0.11 (GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj, GHSA-r955-8q5j-3h83, high) and `System.Security.Cryptography.Pkcs` 10.0.11 (GHSA-2fmv-rv2c-59x7, moderate; GHSA-846f-f764-76mx, high), which `OpenIddict.Client.DataProtection` and `OpenIddict.Server.DataProtection` 7.7.1 take through `Janus.Hosting`'s dependencies; 10.0.14 of both is published. PRT-ENG-010 makes a known vulnerability fail the build, and no gate may be weakened. Readings:
   - (a) `Directory.Packages.props` pins both at 10.0.14 for the repository through central transitive pinning (`CentralPackageTransitivePinningEnabled`), each joining the allow-list's `Janus.Hosting` closure;
   - (b) Janus takes the patched versions in a release candidate after rc.4, and the Janus adapter waits for it;
   - (c) `Portunus.Janus` references both directly, which its row of PRT-LIB-001 does not allow.

   Smallest fix: (a) one setting and two lines; (b) a Janus release and a new pin in `07`; (c) a row of PRT-LIB-001. Answered by P-038: (a), as built.
2. **Tier 2: the host of PRT-LIB-006 criterion 4 in an adapter's tests.** Item: `04` PRT-LIB-006 criterion 4, "in a host built with the adapter's call alone, the sibling's `ISecretSource` resolves to the adapter's, once". `06` PRT-ENG-009 gives `Microsoft.Extensions.Hosting` to `tests/Portunus.Tests` and `tests/Portunus.Testing.Tests` alone, and an adapter's test project takes no container through its references (`Portunus.Hermes.Tests`' lockfile holds `Microsoft.Extensions.DependencyInjection.Abstractions` and no container). Readings:
   - (a) the adapters' test projects join the row of `Microsoft.Extensions.Hosting` (P-025);
   - (b) the criterion is proven on the registration's descriptors alone, which names one singleton of the sibling's `ISecretSource` and no host.

   Smallest fix: (a) one row of PRT-ENG-009; (b) the criterion's wording. Answered by P-039: (a), as built.

## Checks

- The commit: the report's own commit, the head of `phase-05`, on `8bcfa7f`; the branch stands on the lead's `361bf2a` and `2817881`, on `995eb03` of `main`.
- Fast checks on every commit: built in Release with warnings as errors and the analysers; `dotnet format --verify-no-changes`; the unit tests of the projects each commit changes; green.
- Before the push: every unit test, the integration tests, the `linux` kind in the SDK's container, the agent's publish, the commit-message gate and the full-history secret scan, by `run-gates.ps1 -Base main -Head HEAD`.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
