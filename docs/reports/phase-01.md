# Phase 1: Rules and formats

## Items built

Every test below is in `tests/Portunus.Core.Tests`, but for the contract tests, which are in `tests/Portunus.Repository.Tests`. Each was written before the code it proves.

**`00` section 1.5**: the rules of a name (an instance's or a process's), a vault title, an item title, a secret name and an address, internal to `Portunus.Core` (`Names`), and `SecretAddress`.
- NamesTests: IsName_NameOfTheRule_IsTrue, IsName_NameOutsideTheRule_IsFalse, IsName_SixtyFiveCharacters_IsFalse, IsItemTitle_TitleOfTheRule_IsTrue, IsSecretName_NameOfTheRule_IsTrue, IsSecretName_NameEndingInAVersionsMark_IsFalse
- SecretAddressTests: Parse_AddressOfTheRule_AnswersItsParts, Parse_AddressOfTheRule_IsWrittenAsItWasRead, Parse_AddressOutsideTheRule_Throws, Parse_Null_Throws, Constructor_PartOutsideItsRule_Throws, With_PartOutsideItsRule_Throws, Equals_PartsAlikeButForCase_IsTrue, TryParse_AddressOfTheRule_AnswersIt

**`03` PRT-VLT-001 to PRT-VLT-005 and PRT-VLT-007**: the layout validator, `LayoutValidator`, internal to `Portunus.Core`. Criteria PRT-VLT-002 1 and 2 (its warning), PRT-VLT-003 1, PRT-VLT-004 1, PRT-VLT-005 1 (its error), PRT-VLT-007 1.
- LayoutValidatorTests: PRT_VLT_007_AC1_VaultTitleOutsideTheRuleIsAnError, ThreeRulesBrokenGiveThreeFindingsInOrder, FindingsOfOneItemComeInOrderOfTheirLabels; PRT_VLT_002_AC1_ItemTitleOutsideTheRuleIsAnError, ItemTitleOfSixtyFiveCharactersIsAnError, TwoItemTitlesAlikeButForCaseAreAnError; PRT_VLT_002_AC2_ItemWithoutANoteIsAWarningAlone; PRT_VLT_003_AC1_LabelOutsideTheRuleIsAnError, SecretNameOfSixtyFiveCharactersIsAnError, TwoLabelsAlikeButForCaseInDifferentSectionsAreAnError, EmptyValueIsAnError, ValueOfSixtyFourKibAndOneByteIsAnError; PRT_VLT_004_AC1_CurrentMissingIsAnError, CurrentNotANumberOrNamingAVersionNotPresentIsAnError, CurrentStandingWithoutVersionsIsAnError, FieldBesideItsFirstVersionIsAnError, VersionZeroOneIsAnError; PRT_VLT_005_AC1_CurrentNamingARetiredVersionIsAnError; Check_SetOfTheRule_HasNoFinding, Check_VaultTitleOfTheRule_IsAccepted, Check_SixtyFiveItems_IsTooMany, Check_HundredAndTwentyNineFields_IsTooMany, Check_SixtyFiveLiveVersions_IsTooMany, Check_ValuesOverOneMibTogether_IsTooLarge, Check_FieldBesideARetiredVersion_IsMixed, Check_LabelsOfAnotherCase_BelongToOneSecret, Check_NullSet_Throws, FirstError_FindingsWithAWarningFirst_AnswersTheFirstError

**`04` PRT-LIB-002** (the types of `Portunus.Core`): criteria 1, 2 and 3.
- SecretTests: PRT_LIB_002_AC3_FormattingASecretWritesNoneOfItsValues; CurrentValue_CurrentAmongTheVersions_AnswersItsValue, CurrentValue_CurrentNotAmongTheVersions_Throws
- AssemblyContractTests (repository): PRT_LIB_002_AC2_CoreDeclaresEveryTypeAndMemberOfItsTable, and from phase 0 PRT_LIB_002_AC2_EveryPublicTypeIsOneTheTablesGive, PublicApiFilesListExactlyThePublicTypes
- Criterion 1: the phase 0 tests of PublicSurfaceBuildTests, which now build `Portunus.Core` with its types.

**`06` PRT-ENG-003**: `Result`, `Result<T>` and `Failure`, as the siblings', and `SecretsStartupException`; every reader of a wire format answers its refusals as failures. Criterion 1: phase 2 (below).

**`06` PRT-ENG-005**: the wire formats' values in pinned arrays, zeroed by their owners (`PinnedBufferWriter`, `PinnedDocument`, `PinnedValues`). Criterion 1 checked in review (below); criterion 2: the later phases.
- PinnedBufferWriterTests: Advance_WithinTheBound_KeepsWhatWasWritten, GetMemory_AskingBeyondTheBoundButWritingWithin_KeepsWhatWasWritten, Advance_PastTheBound_CountsWhatItDoesNotKeep, Dispose_PinnedDocument_ZeroesItsBytes

**`06` PRT-ENG-006** and **`07` section 6** (all but the development file, phase 2): criterion 1.
- RequestFormatTests: PRT_ENG_006_AC1_ReadRequestIsWrittenInItsForm, StatusRequestIsWrittenInItsForm, PushRequestIsWrittenInItsForm; TryWritePush_PayloadOverTwoMib_AnswersFalse, Read_ReadRequest_AnswersItsAddress, Read_MembersInAnotherOrderAndOnesItDoesNotKnow_AnswersTheRequest, Read_StatusRequest_AnswersIt, Read_PushRequest_AnswersItsSetAndValues, Read_PushRequestWithAnEscapedValue_AnswersTheValue, Read_AnotherProtocolVersion_AnswersTheVersionFailureNamingThisOne, Read_DocumentOutsideItsForm_IsInvalid, Read_DocumentOverItsBound_IsInvalid
- AnswerFormatTests: PRT_ENG_006_AC1_AnswerWhileWaitingIsWrittenInItsForm, StatusWhileReadyIsWrittenInItsForm, SecretIsWrittenInItsForm, FailureIsWrittenInItsForm; WriteFailure_SeveralDetails_WritesThemInOrdinalOrder, WriteWaiting_InstantAtAnotherOffsetWithAFraction_IsWrittenAtOffsetZeroToTheSecond, WriteWaiting_EachReason_IsWrittenByItsSpellingAndReadBack, Read_StatusWhileReady_AnswersIt, Read_Secret_AnswersItsVersionsAndValues, Read_Failure_AnswersItsStateAndFailure, Read_AnotherProtocolVersion_AnswersTheVersionFailureNamingTheAgents, Read_DocumentOutsideEveryForm_IsInvalid, Read_DocumentOverTwoMib_IsInvalid
- PushAnswerFormatTests: PRT_ENG_006_AC1_PushAnswerIsWrittenInItsForm; Read_EachForm_AnswersIt, Read_AnotherProtocolVersion_AnswersTheVersionFailure, Read_DocumentOutsideBothForms_IsInvalid

**`06` PRT-ENG-007** and **`07` sections 1 and 2**: criterion 1 for the codes and the spellings, besides the public type's name of phase 0.
- AssemblyContractTests (repository): PRT_ENG_007_AC1_EveryCodeTheLibraryAnswersIsOneTheReferenceLists, EveryEnumerationIsSpelledAsTheReferenceGivesIt
- AssemblyContractProbeTests (repository): PRT_ENG_007_AC1_CodeTheReferenceDoesNotListIsFound, EnumerationOutsideTheReferenceIsFound

**`07` section 5**: the limits the items above use (`Limits`): a request, an answer and a push's payload, items, fields, a value, a set, live versions and a version's number.

## Items not built

- `03` PRT-VLT-001 criterion 1, a push or a check refused before `op` runs: phase 4, with the tool.
- `03` PRT-VLT-002 criterion 2, the agent taking an item without a note: phase 3.
- `03` PRT-VLT-004 criterion 2 and PRT-VLT-005 criterion 1, a read's answer: phase 3, with the agent's reads.
- `03` PRT-VLT-008 and the development file's format of `07` section 6: phase 2.
- `06` PRT-ENG-003 criterion 1: phase 2, where the client's operations and its start, which answer and throw the codes, come.
- `06` PRT-ENG-005 criterion 2, the buffers after a fetch, a push and a read: phases 3 and 4.
- `06` PRT-ENG-007 criterion 1 for the configuration keys: phase 2, with the first key bound.
- `07` section 1: the codes the client, the agent and the tool answer come with them; phase 1 holds the layout codes and the requests' two.

## Criteria checked without a test

- `06` PRT-ENG-005 criterion 1, reviewed in each path that holds a value: a reader of a request or an answer decodes each value from its document straight into a pinned array of `PinnedValues` (`Base64Value`), an escaped value first unescaped by `Utf8JsonReader.CopyString` into a pinned array zeroed in a `finally`; a writer writes each value with `Utf8JsonWriter.WriteBase64String` into `PinnedBufferWriter`, whose arrays are pinned and zeroed when it grows, when it is disposed, and for what it does not keep, beyond its bound; a document is a `PinnedDocument`, zeroed by its disposal. No value becomes a `string`. Open question 2 names what this review could not see.
- `06` PRT-ENG-008, tests written first: each item's tests were written before the code they prove, and run failing on it first.

## Awaiting the pipeline's run

- None of phase 1's criteria; the gate of record is the `gates` run on this commit, which the message naming the pull request gives.

## Analyser suppressions

None added. No suppression in code.

## Paths that make a string of a value or the token

None: no path of phase 1 makes a `string` of a value. The strings the formats read and write are addresses, titles, labels, codes, details, spellings and instants.

## Resolved by rule

- `src/Portunus.Core/LayoutValidator.cs`, the field a finding names: the label it is about, but for a finding about a versioned secret as a whole (`currentmissing`, `toomany` of its versions), which names the secret's name, the form its address takes. Governs: `03` PRT-VLT-007 with `00` section 1.5. A finding names its item and field, and the field a missing `.current` concerns is the secret its address names.
- `src/Portunus.Core/LayoutValidator.cs`, a field `<name>` beside `<name>.<n>.retired` alone: `mixed`. Governs: `03` PRT-VLT-005. A retired version "is checked as a version is", and a version beside `<name>` is `mixed`.
- `src/Portunus.Core/VersionNumber.cs`, a `.current` value: a number of the version rule, 1 to 9999 in decimal digits without leading zeros, nothing around it. Governs: `03` PRT-VLT-004. A version number is written without leading zeros, and `.current` holds "the decimal number of the current version".
- `src/Portunus.Core/FieldLabel.cs` and `LayoutValidator.cs`, case: the suffixes `.current` and `.retired` are read without regard to the case of ASCII letters, and the fields of one secret are gathered so. Governs: `03` PRT-VLT-003 and P-011. Names are compared without regard to case, as the vault compares them.
- `src/Portunus.Core/LayoutValidator.cs`, two titles or labels alike: the finding names each but the first in ordinal order. Governs: `03` PRT-VLT-007. The findings are in ordinal order, so the first of the alike is the one kept.
- `src/Portunus.Core/WireJson.cs`, a failure's details: written in the ordinal order of their names. Governs: `06` PRT-ENG-006. A format is one exact form, and `07` section 6 gives the details no order.
- `src/Portunus.Core/Instant.cs`, an instant: written to the second, a fraction left out, and read only in that form. Governs: `06` PRT-ENG-006 with `07` section 6, whose instants are `2026-11-05T09:00:00Z`.
- `src/Portunus.Core/RequestFormat.cs`, `AnswerFormat.cs`, `PushAnswerFormat.cs`, a document refused as outside its form: a member named twice, anything after the document's object, and a known member that is not of the form the document's other members make. Governs: `06` PRT-ENG-006. A format is one exact form; only a member the reader does not know is left out.
- `src/Portunus.Core/RequestFormat.cs`, `AnswerFormat.cs`, `PushAnswerFormat.cs`, another protocol version: answered as `secrets.request.version` whatever else the document holds, the agent's reader naming its own version and the client's the agent's. Governs: `07` section 1, whose detail `version` is the agent's.
- `src/Portunus.Core/RequestFormat.cs`, a push's payload over 2 MiB: the writer answers that it wrote nothing it keeps, and the tool answers `secrets.push.toolarge` in phase 4. Governs: `02` PRT-PSH-003, where the code is the tool's.

## Open questions

1. **Tier 2: a live and a retired version of one number, and retired versions alone.** Item: `03` PRT-VLT-004, PRT-VLT-005 and PRT-VLT-007. The check meets `keys.2` beside `keys.2.retired`, and `keys.1.retired` with no live version and no `.current`; no rule names either, so the check reports nothing for them. The code needs to know whether each is an error and its code. Readings: (a) both are allowed: the retired field is kept for a restore, never delivered; (b) the first is `secrets.layout.fieldduplicate` (one version labelled twice), the second allowed; (c) both are errors, the second as `secrets.layout.currentmissing`. Smallest fix: (a) none; (b) one finding in `LayoutValidator.SecretFindings`; (c) that, and a second. Parked: nothing is built for the two cases.
2. **Tier 3: copies inside `System.Text.Json`.** Item: `06` PRT-ENG-005. The chapter has values written with `Utf8JsonWriter.WriteBase64String` and read with `Utf8JsonReader.CopyString`, and the code does so into pinned arrays. Whether either makes a copy of a value in an array it rents or allocates itself, which no code of the library could zero, is not confirmed from the source in front of us. Parked: nothing; the formats are built as the chapter gives them.

## Checks

- The commit: the report's own commit, the head of `phase-01`, on `aa06c7f`, itself on `main` at `9d4a6bc`.
- Fast checks on every commit: each commit of `phase-01` built in Release with warnings as errors, with its analysers, and its unit tests run, in a clone of its own: green; `dotnet format Portunus.sln --verify-no-changes`: green.
- Before the push: every unit test, the commit-message gate over `main..HEAD` and the whole history, the full-history secret scan, and every integration and contract test: green; `run-gates.ps1 -Base main -Head HEAD` on the report's own commit: every gate passed.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
