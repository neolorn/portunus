# 04 — Library contract

What a host references, what it calls, what the adapters give the sibling libraries, and
what the library promises not to break. Every value named `Portunus:...` is configuration
(`07` section 4).

---

**PRT-LIB-001** — The library SHALL ship as the parts of `00` section 2.2, and each
project SHALL reference only what this item allows it.

**Values (P-013, P-015, P-016).**

| Project | May reference |
|---|---|
| `Portunus.Core` | The base class library only |
| `Portunus` | `Portunus.Core`; the Microsoft.Extensions abstractions of `06` PRT-ENG-009 |
| `Portunus.Janus` | `Portunus`; `Janus.Hosting`, which carries `Janus.Core`, at the version of `07` section 7 (P-036) |
| `Portunus.Hermes` | `Portunus`; `Hermes.Core` at the version of `07` section 7 |
| `Portunus.Themis` | `Portunus`; `Themis.Core` at the version of `07` section 7 |
| `Portunus.Testing` | `Portunus.Core`, `Portunus` |
| `Portunus.Cli` (a .NET tool, command `portunus`) | `Portunus.Core` |
| `Portunus.Agent` (an executable, `portunus-agent`) | `Portunus.Core`; the hosting packages of `06` PRT-ENG-009 |
| `tests/Portunus.Repository.Tests` | Every shipped project, whose built assemblies its contract tests read (`06` PRT-ENG-004), and the test packages of `06` PRT-ENG-009 |
| `tests/Portunus.Fakes` | `Portunus.Core` |
| `tests/Portunus.Agent.Harness` | `Portunus.Agent`, whose internals it sees through `InternalsVisibleTo` (P-027) |
| Any other test project | Its source project, `Portunus.Testing`, and the test packages of `06` PRT-ENG-009 |
| The sample host and its test project | The packed packages above, and for its tests the sample host itself and the test packages of `06` PRT-ENG-009 |

Every shipped project references `Microsoft.CodeAnalysis.PublicApiAnalyzers` and every
packed project `MinVer`, both as build-only references. The packages depend on one
another, and each adapter on its sibling's `Core` package, at exactly the version packed
or named (`[v]`), so NuGet reports a host's mix of versions at its restore rather than
letting it fail at run time. No project but an adapter names a sibling library, and no
sibling library changes for this one (`00` PRT-OVR-002).

*Source: P-013, P-015, P-016*

**Acceptance criteria**
1. A test reads each project's references and fails on one this table does not allow.
2. A test packs the packages from one commit and fails on a dependency of one of them on
   another of the library's packages, or of an adapter on its sibling's package, that is
   not the exact version (`[v]`).

---

**PRT-LIB-002** — The public surface SHALL be exactly what the packages' public API files
list, each public type SHALL have the shape below, and a change to it SHALL be visible in
review.

**Values (P-013, P-016).** Data types are records with `init` members, a non-nullable
member being `required`, a list or a dictionary never null; every asynchronous member
takes a `CancellationToken` as its last parameter and answers a `ValueTask`. An
enumeration is a C# `enum` whose members are named and ordered as `07` section 2 gives,
each carrying its wire spelling by `JsonStringEnumMemberName`. A record that carries a
value overrides `PrintMembers` to print none of it.

**`Portunus.Core`:**

| Type | Shape |
|---|---|
| `SecretAddress` | `readonly record struct SecretAddress(string Item, string Name)`, made through `static SecretAddress Parse(string address)` or its constructor, each throwing `ArgumentException` for a part outside its rule (`00` section 1.5); `ToString()` answers `<Item>/<Name>`; equality ignores the case of ASCII letters (`03` PRT-VLT-003) |
| `Secret` | `int Current`, `IReadOnlyDictionary<int, ReadOnlyMemory<byte>> Versions` (every live version by its number, `Current` among them), `ReadOnlyMemory<byte> CurrentValue` (the current version's value); it prints its version numbers and no value |
| `AgentStatus` | `AgentState State`, `WaitReason? Reason` (while `Waiting`), `DateTimeOffset Since`, `SetSource? Source`, `DateTimeOffset? LoadedAt` and `int Items` (while `Ready`) (`01` PRT-AGT-005) |
| `Result`, `Result<T>`, `Failure` | As the siblings': `Result`: `bool IsSuccess`, `Failure? Failure`, and the factories `static Result Success()`, `static Result Fail(Failure failure)`, `static Result<T> Success<T>(T value)` and `static Result<T> Fail<T>(Failure failure)`; `Result<T>`: `bool IsSuccess`, `T? Value`, `Failure? Failure`; `Failure`: `string Code` of `07` section 1 and `IReadOnlyDictionary<string, string> Details` |
| `SecretsStartupException` | The exception a "(startup)" code of `07` section 1 is thrown in when the host starts: `string Code`, `IReadOnlyDictionary<string, string> Details` |
| `ISecretClient` | `ReadAsync(SecretAddress address, ...)` answers `Result<Secret>`; `StatusAsync(...)` answers `Result<AgentStatus>` (PRT-LIB-004) |
| Enumerations | `AgentState`, `WaitReason`, `SetSource`, with the members of `07` section 2 |

**Registration, adapters and testing:**

| Package | Type | Members |
|---|---|---|
| `Portunus` | `SecretsServiceCollectionExtensions` | `AddPortunus(IServiceCollection services, IConfiguration configuration)` answers the `IServiceCollection` (PRT-LIB-003) |
| `Portunus.Janus` | `JanusSecretsServiceCollectionExtensions` | `AddPortunusJanusSecrets(IServiceCollection services, IConfiguration configuration)` answers the `IServiceCollection` (PRT-LIB-006) |
| `Portunus.Hermes` | `HermesSecretsServiceCollectionExtensions` | `AddPortunusHermesSecrets(IServiceCollection services, IConfiguration configuration)`, likewise |
| `Portunus.Themis` | `ThemisSecretsServiceCollectionExtensions` | `AddPortunusThemisSecrets(IServiceCollection services, IConfiguration configuration)`, likewise |
| `Portunus.Testing` | `FixedSecretClient`, `SecretsTestingServiceCollectionExtensions` | The members of PRT-LIB-008 |

Everything else is internal: the client's connection and its file source, the adapters'
seams, the layout validator and the wire formats (`06` PRT-ENG-001). `Portunus.Cli` and
`Portunus.Agent` are executables and have no public types.

*Source: P-013, P-016*

**Acceptance criteria**
1. The build fails when a public member is added or removed without its public API file
   changing.
2. The public API files list each type and member of the tables above and of
   PRT-LIB-008, with the types given, and no other public type.
3. Formatting a `Secret` whose versions hold known bytes writes none of them.

---

**PRT-LIB-003** — A host SHALL register the client with one call, and the client's
configuration SHALL be checked before any secret is read.

**Values (P-008, P-014).** `services.AddPortunus(configuration)` binds the section
`Portunus:Client` (through the configuration binder's source generator) and registers
`ISecretClient` as a singleton, an `IHostedLifecycleService` whose `StartingAsync` makes
the client, and the health check `portunus` (`05` PRT-OPS-004). Calling it twice
registers once. The client checks its configuration when it is first made, by that
service or by the first adapter or host code that asks for it, whichever comes first, so
a sibling that reads its secrets in its own `StartingAsync` meets a checked client: a key
outside its rule throws a `SecretsStartupException` with
`secrets.startup.configurationinvalid`, `details.key`; the `file` source in a host whose
`IHostEnvironment` is not Development, or that registers none, with
`secrets.startup.notdevelopment`; a development file that cannot be read, is not JSON or
breaks the layout, with `secrets.startup.fileinvalid`, `details.item` and `details.field`
where the layout names them (PRT-LIB-005).

*Source: P-008, P-014*

**Acceptance criteria**
1. A host with valid configuration reads a secret after the one call.
2. A configuration value outside its rule stops the start naming the key.
3. The `file` source in a Production host stops the start with
   `secrets.startup.notdevelopment`.

---

**PRT-LIB-004** — The client SHALL read each secret from the agent at the moment it is
asked, SHALL wait while the agent has no set or cannot be reached, within the bound the
host sets, and SHALL then answer a failure rather than wait on.

**Values (P-007, P-008).** `ReadAsync(address)` connects to `Portunus:Client:Socket`,
sends one `read` request (`07` section 6) and reads one answer, the whole exchange
bounded by `Portunus:Client:Timeout`. It answers:
- the `Secret` where the agent answers one; its arrays are new, the caller's own, and the
  client zeroes every buffer of its own once copied;
- `secrets.secret.unknown` or `secrets.secret.denied` where the agent answers it, at
  once;
- `secrets.request.version` where the agent speaks another protocol version.

Where the agent answers `waiting`, or the socket is missing, refuses the connection,
closes it without an answer or does not answer within `Portunus:Client:Timeout`, the
client waits `Portunus:Client:PollInterval` and asks again, until
`Portunus:Client:WaitTimeout` has passed since the call began; it then answers
`secrets.agent.waittimeout`, `details.reason` the last reason the agent gave, or
`unreachable`. While it waits it logs "waiting for secrets" at the Warning level, with
the reason and the time waited, at the first wait and then at most every 30 seconds, so a
process that is not yet serving shows plainly why (`05` PRT-OPS-005). A `WaitTimeout` of
0 asks once. The client holds nothing between calls: a sibling reads once at its start
and keeps what it read (P-013). `StatusAsync` asks the agent's status once, bounded by
`Portunus:Client:Timeout`, and answers `secrets.agent.unreachable` where it cannot.

*Source: P-007, P-008, P-013*

**Acceptance criteria**
1. With a fake agent answering `waiting` for 10 seconds and then the secret, a read with
   `WaitTimeout` 1 minute answers the secret after about 10 seconds, having logged the
   wait once (a test clock).
2. With no socket, a read answers `secrets.agent.waittimeout` with `unreachable` once
   `WaitTimeout` has passed, and not before.
3. A denied address answers `secrets.secret.denied` at once, without waiting.
4. No log line of these tests holds a value the test used.

---

**PRT-LIB-005** — In development the client SHALL read a local file of the vault's layout
instead of the agent, and SHALL refuse to do so anywhere else.

**Values (P-014).** Where `Portunus:Client:Source` is `file`, the client reads
`Portunus:Client:File` (`03` PRT-VLT-008) once, when it is made, into pinned arrays,
checks it with the layout validator, and answers each read from it as the agent would,
every address allowed, an absent one answering `secrets.secret.unknown`; `StatusAsync`
answers `Ready` with the source `vault`, loaded when the file was read. It is refused
outside Development (PRT-LIB-003).

*Source: P-014*

**Acceptance criteria**
1. A Development host with a file holding `Themis/connection` reads it, and a read of an
   address the file lacks answers `secrets.secret.unknown`.

---

**PRT-LIB-006** — Each adapter SHALL supply its sibling's `ISecretSource` over the client,
reading each secret at the address the vault's layout gives it, in the form the sibling
expects, and SHALL answer the sibling's own refusal where it cannot.

**Values (P-012, P-013, P-020).** `AddPortunus<Library>Secrets(configuration)` calls
`AddPortunus` and registers the sibling's `ISecretSource` as a singleton, so the sibling
finds it as it finds any host's source. A secret's address is the item
`Portunus:<Library>:Items:<secret name>` where that key is set (an outside provider's
item, `03` PRT-VLT-002), else `Portunus:<Library>:Item` (default the library's name), and
the field named as the sibling names the secret. A value the table calls base64 is
decoded as `03` PRT-VLT-003 gives; a value that does not decode, or breaks the form the
sibling's type demands before it is built, is not passed on, and is logged at the Warning
level as `secrets.secret.forminvalid` with its address. Versions are passed in ascending
order of number.

**Janus** (`Janus.Core.ISecretSource`; Janus OPS-SEC-001):

| Member | Address's field | Answers |
|---|---|---|
| `ReadKeyEncryptionKeysAsync` | `keyEncryptionKeys`, a versioned set | `KeyEncryptionKeys(current, versions)`, each version base64 of 16, 24 or 32 bytes |
| `ReadFingerprintKeysAsync` | `fingerprintKeys`, a versioned set | `FingerprintKeys(current, versions)`, each version base64 (Janus checks its length) |
| `ReadMaintenanceCredentialAsync` | `maintenanceCredential` | Its UTF-8 bytes |
| `ReadMailServerSecretAsync` | `mailServerSecret` | Its UTF-8 bytes |
| `ReadProviderCredentialAsync(provider)` | `socialProvider.<provider>` | `ProviderCredential.Secret(bytes)` for a value that does not begin with `{`; for one that does, `ProviderCredential.Signed(issuer, keyId, key)` from the JSON object `{ "issuer", "keyId", "key" }`, `key` base64 of the PKCS #8 private key, any other object refused as not of its form |

Any failure answers Janus's `Result` failure `model.startup.secretunavailable`,
`details.key` the name Janus gives it (`keyEncryptionKeys`, `fingerprintKeys`,
`maintenanceCredential`, `mailServerSecret`, `socialProvider.<provider>`), which Janus
answers by refusing its start where it needs the secret.

**Hermes** (`Hermes.Core.ISecretSource.ReadAsync(name)`; Hermes MSG-OPS-008) and
**Themis** (`Themis.Core.ISecretSource.ReadAsync(name)`; Themis APR-OPS-008):

| Library | Names read as base64 | Names read as UTF-8 text |
|---|---|---|
| Hermes | `contentKeys`, `hashKeys`, `tokenKeys`, `smsReportKey`, `pushVapidKeys` | `connection`, `listenConnection`, `emailSubmission`, `smsCequens` (Hermes M-043) |
| Themis | None | `connection` |

Each answers `SecretVersions` of the address's live versions, a single secret as version
1; the `.current` marker is not passed, since Hermes takes its write version from its
own configuration (`03` PRT-ROT-002). It answers none (the sibling's "host holds no such
secret") for any failure, so the sibling refuses its start naming the secret where it
needs it; a denied address is logged at the Warning level with the name first, so the
cause is plain. A name the table does not list (one a later release or an optional
package of the sibling added) is read as UTF-8 text and passed on, logged once at the
Information level with the name; the sibling's own start check refuses it by name where
its form is not text (P-037).

*Source: P-012, P-013, P-020*

**Acceptance criteria**
1. Through a fixed client, each member above answers the sibling's type holding the
   values set, and each failure answers the sibling's refusal naming the secret.
2. A Janus key set whose version decodes to 20 bytes answers
   `model.startup.secretunavailable` naming `keyEncryptionKeys`, and logs
   `secrets.secret.forminvalid` with the address.
3. With `Portunus:Janus:Items:socialProvider.example` set to `Example`, the provider's
   credential is read at `Example/socialProvider.example`.
4. In a host built with the adapter's call alone, the sibling's `ISecretSource` resolves
   to the adapter's, once.

---

**PRT-LIB-007** — The library SHALL follow Semantic Versioning 2.0.0, and SHALL release
candidates of 1.0.0 until its first final release: from that release on, a release that
changes or removes a public member, a code, a configuration key, a value of an
enumeration, a member of a wire format, or a command or an option of the two executables
is a major release.

**Values (P-016, P-017).** A wire format (`07` section 6) carries its protocol version;
within one, members are only added, and a reader ignores a member it does not know, so a
client and an agent of different releases work together where their protocol versions
match. A change no reader of the version before could take is a new protocol version,
and an agent serves both for a major release. The release gate's baseline is
`build/gates/release-baseline.json`, written in each release commit beside
`PublicAPI.Shipped.txt`: the codes, the configuration keys, the enumerations' spellings,
the wire formats' members and the executables' commands and options. The gate compares
the current set with the baseline as the tag of the newest final release holds it, on a
pull request as on a release tag, a release candidate's tag never being one; before the
first final release the baseline is empty. An addition is minor; a change or a removal is
major.

**Release candidates (P-017).** A version is final only on the owner's word. Until then
each release of it is a release candidate, `X.Y.Z-rc.N`, `N` counting from 1 for each
version. The first final release is 1.0.0, so every release before it is `1.0.0-rc.N`,
and nothing in this rule binds those: a release candidate of 1.0.0 may change or remove
anything. The release notes of each release candidate name every change and removal since
the release before it. From the first final release on, a release candidate `X.Y.Z-rc.N`
is held to what `X.Y.Z` needs against the newest final release. A move of a sibling's
version an adapter is built against (`07` section 7) is a release of this library.

*Source: P-016, P-017*

**Acceptance criteria**
1. The release gate compares the public surface, the codes, the configuration keys, the
   enumerations, the wire formats and the executables' commands and options with the
   newest final release's, and fails a release whose version is below what changed
   needs; before the first final release its baseline is empty, so nothing is a break.
2. With no final release tagged, a pull request and a release candidate's tag that
   remove a public member pass the release gate; with a final release `1.0.0` tagged, the
   same removal fails it in a pull request carrying neither `!` nor `BREAKING CHANGE:`.

---

**PRT-LIB-008** — `Portunus.Testing` SHALL give a host's tests, and the sample host, a
fixed client that answers what the test sets, so a test runs a host and its siblings with
no agent.

**Values (P-014).**

| Type | Members |
|---|---|
| `FixedSecretClient` | An `ISecretClient`, `Ready` and answering `secrets.secret.unknown` until set; `Set(SecretAddress address, Secret secret)`, `Remove(SecretAddress address)`, `Deny(SecretAddress address)` (answering `secrets.secret.denied`), `Wait(WaitReason reason)` (answering `secrets.agent.waittimeout` with that reason, and `StatusAsync` `Waiting`) and `Release()` (ready again); `IReadOnlyList<SecretAddress> Reads`, every address asked, in order |
| `SecretsTestingServiceCollectionExtensions` | `AddPortunusTesting(IServiceCollection services, Action<FixedSecretClient>? configure = null)` answers the `IServiceCollection`; it registers one `FixedSecretClient` as a singleton, resolvable by its own type and as `ISecretClient`, in place of the client, `configure` running on it at once, in either order with `AddPortunus` or an adapter's call |

*Source: P-014*

**Acceptance criteria**
1. A host test with `AddPortunusThemisSecrets` and `AddPortunusTesting` sets
   `Themis/connection`, and the `Themis.Core.ISecretSource` it resolves answers it for
   `connection`, in either order of the two calls.
