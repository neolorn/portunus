# 06 — Engineering conventions

How the library is built. Every rule here is checked by the build, a test or a gate,
save the parts marked "checked in review", which the lead's review of each phase checks.

---

## 1. Layout and setup

**PRT-ENG-001** — The repository SHALL hold one solution, `Portunus.sln`, with one project
per part of `04` PRT-LIB-001 under `src/` and one test project per source project under
`tests/`, the fakes' project beside them, a sample host with its test project in a
solution of its own under `samples/`, built only from the packed packages, and the
agent's unit files under `deploy/`.

**Values (P-016).** A source project is `src/<Part>`; its test project
`tests/<Part>.Tests`; the tests over the whole repository (its projects' references and
grants, its layout and shared settings, the secret scan, the vendored packages, the unit
files, the contract tests over the built assemblies, `04` PRT-LIB-001 and PRT-LIB-002,
`08` PRT-THR-004) live in `tests/Portunus.Repository.Tests`, the one test project with no
source project. `tests/Portunus.Fakes` is a console project, not a test project: one
executable that acts as `op` or as `ssh` by the name it is started under, each answering
from a scenario file a test names in an environment variable of the fake's own (PRT-ENG-008),
which a launcher script of the test's sets where the agent starts the fake (P-029).
`tests/Portunus.Agent.Harness` is a console project, not shipped, that builds the agent's
host through the shipped entry point's own internal method with a test's paths (P-027),
and, as `client`, sends a file's bytes on a socket and writes the answer (P-030).
Each project's namespace root is its name; a file's namespace matches its folder; one
public top-level type per file. Projects are flat but for two folders of
`Portunus.Agent`, `Fetch` (the fetch and `op`, `01` section 2) and `Serving` (the sockets,
the declarations, the reads and the push, `01` section 4, `02` PRT-PSH-003); which file
goes in which folder is checked in review. A shipped project's types are internal unless
`04` PRT-LIB-002 makes them public, and its public types sit at its root. A test
project's test classes are public, since xUnit.net runs no other.

`InternalsVisibleTo` is granted only from a source project to its own test project, and
from `Portunus.Core` to `Portunus`, `Portunus.Agent`, `Portunus.Cli` and
`Portunus.Testing`, which share its layout validator, its address and name rules and its
wire formats (`03` PRT-VLT-007; `07` section 6), and from `Portunus` to
`Portunus.Testing`, which removes the client's registration in favour of the fixed one
(`04` PRT-LIB-008). No public member changes for these grants.

`deploy/` holds `portunus-agent@.service`, `token.conf` (the drop-in of `vault` mode) and
`example.json`, an agent's configuration for an invented sample environment naming no real
product or company (checked in review); `07` section 7 gives the unit's settings.
`vendor/` holds the sibling packages the adapters build against (`Janus.Hosting`, which
carries `Janus.Core` (P-036), `Hermes.Core` and `Themis.Core`), at the versions of `07`
section 7, as their releases published them, with `vendor/SHA256SUMS` listing each
file's SHA-256; `nuget.config` maps the identifiers `Janus.*`, `Hermes.Core` and
`Themis.Core` to `vendor/` alone and every other identifier to nuget.org alone, by
`packageSourceMapping`.

The sample host is a worker service with an invented domain naming no real product or
company (checked in review): it registers `AddPortunus` and `AddPortunusThemisSecrets`,
reads one secret of its own and Themis's `connection` through them at its start, and logs
the versions it holds, never a value; its tests run it through `Portunus.Testing`. It
runs in the gate (PRT-ENG-012) from packages packed from the same commit into
`artifacts/packages/`, which `.gitignore` lists, at a version the gate passes it; it alone
has no lockfile and no locked restore. The sample host and its test project are outside
`Portunus.sln`, in `samples/Samples.sln`, and `samples/nuget.config` adds
`artifacts/packages/` for the library's own identifiers and `vendor/` for the siblings',
each mapped alone.

The repository's other files: at its root the solution, `Directory.Build.props`,
`Directory.Packages.props`, `global.json`, `.editorconfig`, `.gitignore`, `nuget.config`,
`CHANGELOG.md` and `RELEASE-NOTES.md` (the release notes of `05` PRT-OPS-003: one section
per release, headed by its version and written in its release commit, or before it in a
`docs:` commit of the same pull request, holding the deployment's duties as they stand at
that release and the sibling versions the adapters are built against); the pipeline's
definition under `.github/workflows/`; the gates' scripts under `build/gates/`, with the
secret scanner's configuration there, `build/gates/release-baseline.json` (`04`
PRT-LIB-007) and `build/gates/built-items.txt`, the acceptance criteria proven so far by
a test, one per line as `PRT-AGT-004 AC2`, which the test-name gate reads and each phase
extends. The secret scanner is gitleaks 8.30.1, its download checked against its
published SHA-256, which the fetch script itself holds (checked in review), and left
under `tmp/tools/` by one step of `build/gates/` that the secret-scanning and the test
gates both run first. Its configuration extends gitleaks' default and adds no entry,
since no test needs a credential-shaped literal: every secret a test needs is made at run
time. It runs with `--ignore-gitleaks-allow`, with `--gitleaks-ignore-path` naming a
folder that holds no file, and with no `--baseline-path`; and it fails where a
`.gitleaksignore` is tracked or sits at the scanned repository's root. The gates' scripts
are PowerShell 7 scripts, and the pipeline runs on GitHub's `ubuntu-24.04` runner using no
action but `actions/checkout` and `actions/setup-dotnet` (the SDK of `global.json`), each
at its latest release when the workflow is written and pinned by that release's full
commit SHA (all checked in review).

*Source: P-016*

**Acceptance criteria**
1. A test fails on a project outside this layout, a grant outside this rule, an entry
   added to the scanner's configuration, or a file of `vendor/` whose SHA-256 differs from
   `vendor/SHA256SUMS` or that it does not list.
2. The sample host builds from the packages packed from the same commit, and its tests
   pass.

---

**PRT-ENG-002** — Every project SHALL target .NET 10 with the settings of one committed
`Directory.Build.props`, and the SDK SHALL be fixed in `global.json`.

**Values (P-016).** `Nullable` enable, `ImplicitUsings` disable, `TreatWarningsAsErrors`
true, `MSBuildTreatWarningsAsErrors` true, `AnalysisLevel` latest-all,
`EnforceCodeStyleInBuild` true, `Deterministic` true, `GenerateDocumentationFile` true,
`EnableConfigurationBindingGenerator` true, `NuGetAudit` true with `NuGetAuditMode` all;
none overridden by a project. `Portunus.Agent` alone sets `AllowUnsafeBlocks` true, which
the `LibraryImport` generator's code needs (PRT-ENG-004). The agent's runtime identifier,
self-contained and single-file publication are arguments of the release's publish step
(PRT-ENG-011), never properties of a project. `global.json` names a .NET 10 SDK with
`rollForward` `latestPatch`. Package versions live in `Directory.Packages.props` (Central
Package Management) with a committed lockfile per project, restored in locked mode in the
pipeline (the sample host aside, PRT-ENG-001). Of the analysers' rules, `.editorconfig`
turns off only these, each with a comment line above it giving the reason: CA1707 under
`tests/` and in the sample host's test project (a test's name carries its criterion's
identifier, PRT-ENG-008); CA1515 in the same places (xUnit.net runs only public test
classes); CA1812 under `src/`, under `tests/` and in the sample host (an internal class
the dependency injection container makes is made, though no code calls `new` on it);
CA1303 everywhere (exception messages are for developers and never shown to people);
CA1032 for `SecretsStartupException` (it always carries a code and details); and CS1591
and CA2007 under `tests/` and in the sample host's test project. Every project carries
`[assembly: CLSCompliant(false)]`, written once in `Directory.Build.props`. Formatting is
defined once in `.editorconfig`, the output of `dotnet new editorconfig` for the SDK of
`global.json`, unchanged, with the turn-offs above added (checked in review), and checked
by `dotnet format --verify-no-changes`.

*Source: P-016*

**Acceptance criteria**
1. No project file sets a target framework, a listed property (`AllowUnsafeBlocks` in
   `Portunus.Agent` aside) or a package version, and `Directory.Packages.props` sets no
   property but `ManagePackageVersionsCentrally`; no project file,
   `Directory.Build.props` or `Directory.Packages.props` turns a warning off or keeps it
   from failing the build, by setting, whatever its value, a property among `NoWarn`,
   `WarningsNotAsErrors`, `WarningLevel`, `MSBuildWarningsAsMessages`,
   `MSBuildWarningsNotAsErrors`, `RunAnalyzers`, `RunAnalyzersDuringBuild`,
   `EnableNETAnalyzers`, `CodeAnalysisRuleSet`, `NuGetAuditLevel`, `AnalysisMode`, and the
   `AnalysisLevel` or `AnalysisMode` of one category of rules, by an item's metadata or by
   an item (`NuGetAuditSuppress`, an analyser configuration file); and no `.editorconfig`
   or `.globalconfig` but the root `.editorconfig` is tracked or sits in the working tree
   outside `tmp/` and the projects' `bin/` and `obj/`.
2. A warning fails the build, the compiler's, an analyser's, NuGet's, MSBuild's or the
   SDK's alike; a locked restore that differs from the lockfile fails.

---

## 2. Design

**PRT-ENG-003** — An expected outcome SHALL be returned as a result, never thrown; a
fault SHALL be thrown.

**Values (P-016).** `Result` and `Result<T>` (`Portunus.Core`) carry success or a failure
with its code and details (`07` section 1). A refusal that the agent's state, the vault, a
declaration or a moment can cause is a failure: an address denied or unknown, a wait that
ran out, a push refused. A mistake only the calling code can make is a fault: a null
argument, an address outside its rule; it throws `ArgumentException` or
`InvalidOperationException`, and a broken invariant throws. The executables answer every
refusal as one JSON line on standard error with exit code 1, and a fault that carries no
code as `{ "code": "secrets.fault", "details": {} }` the same way, having zeroed what they
hold. A `catch` never turns a fault into a success, and an exception is never swallowed.

*Source: P-016*

**Acceptance criteria**
1. No public operation throws for a code of `07` section 1; a "(startup)" code is thrown
   only as a `SecretsStartupException` while the host starts (`04` PRT-LIB-003).

---

**PRT-ENG-004** — Every type SHALL be sealed unless designed for inheritance, every
asynchronous method SHALL take a `CancellationToken`, data crossing a boundary SHALL be
immutable, and there SHALL be no static mutable state and no reflection.

**Values (P-016).** Records for data, `init` members, read-only collections; JSON through
source-generated contexts and `Utf8JsonReader` and `Utf8JsonWriter` only (PRT-ENG-006);
configuration through the binder's source generator and source-generated options
validation (`05` PRT-OPS-001); logging through source-generated `LoggerMessage` methods
(`05` PRT-OPS-005); the clock through `TimeProvider`; randomness for the pauses'
spread through an injected `Random` seeded once; the built-in dependency injection
container only; hosting through the generic host. Native code is called only by the agent,
only for `prctl` (`01` PRT-AGT-001) and for `statx` and `fchownat` with
`AT_SYMLINK_NOFOLLOW` (`01` PRT-AGT-006, `05` PRT-OPS-001, P-026), and only through a
source-generated `LibraryImport`;
the peer's credentials are read with `Socket.GetRawSocketOption`. Every public type and
member of a shipped project carries XML documentation stating its contract, the failures
it answers and the faults it throws.

*Source: P-016*

**Acceptance criteria**
1. A contract test reads the built assemblies' metadata (`System.Reflection.Metadata`, in
   tests only) and fails on a public class neither sealed nor abstract, and on a public
   asynchronous method without a `CancellationToken`; the build fails on a shipped
   project's public member without documentation.
2. A test fails on a lookup or call of members by name (`System.Reflection` member
   lookups, `Activator`, `Type.GetType`) outside generated code, and on a `DllImport` or a
   `LibraryImport` outside the agent's three (P-026).

---

**PRT-ENG-005** — A secret's value, the token and every buffer holding either SHALL be a
pinned byte array, zeroed once used, and SHALL never become a `string` but where an API
the library does not own demands one.

**Values (P-002).** Arrays come from `GC.AllocateArray<byte>(length, pinned: true)` and
are zeroed with `CryptographicOperations.ZeroMemory` in a `finally`. In the library's
own formats a value is base64url, encoded with `Base64Url.EncodeToUtf8` into a pinned
buffer and written with `Utf8JsonWriter.WriteStringValue` into a writer over a pinned
array of the bound's size (`07` section 5), and read by refusing an escaped string and
decoding `ValueSpan` with `Base64Url.DecodeFromUtf8` into a pinned array (P-023); a
field's text in `op`'s output is read with `Utf8JsonReader.CopyString` into a pinned
array; a child process's output is read from its base stream. The only
`string` a secret becomes is the token in `op`'s environment (`01` PRT-AGT-003), a
`ProcessStartInfo` demanding one, and a value an adapter hands a sibling as the sibling's
type demands. `Secret` hands its caller arrays of its own; zeroing those is the caller's.

*Source: P-002, P-023*

**Acceptance criteria**
1. No path of the agent, the client, the tool or an adapter makes a `string` of a value
   or the token outside the places above, and every array holding one is pinned and
   zeroed in a `finally` (checked in review, each such path named in the phase report).
2. After a fetch, a push and a read, the buffers the test can reach through internal
   hooks hold zeros (`01` PRT-AGT-001 criterion 3).

---

**PRT-ENG-006** — Every wire format SHALL be one exact JSON form, bounded in size, carrying
its protocol version.

**Values (P-016).** UTF-8 JSON without indentation, members in camel case and in the order
of `07` section 6, a member that is none left out, an enumeration member by its spelling
(`07` section 2), a value in base64url (RFC 4648 section 5, unpadded; P-023),
an instant in RFC 3339 with an
offset of zero; a reader ignores a member it does not know and refuses a document over
its bound. The formats are internal to `Portunus.Core` and shared by the agent, the
client, the tool and the fakes.

*Source: P-016*

**Acceptance criteria**
1. A test holds each format's form for a fixed value as a literal and fails where the
   writer writes it otherwise.

---

## 3. Names

**PRT-ENG-007** — Codes SHALL be lower case, dot-separated, starting with `secrets.`;
configuration keys SHALL be the paths of `07` section 4 under `Portunus`; enumerations
SHALL be spelled as `07` spells them; and no type SHALL be named after the library.

**Values (P-001, P-016).** The library's name appears only in namespaces, project and
package identifiers, the solution file (`Portunus.sln`), the configuration section, the
registration methods (`AddPortunus`, `AddPortunusTesting`, `AddPortunusJanusSecrets`,
`AddPortunusHermesSecrets`, `AddPortunusThemisSecrets`), the commands `portunus` and
`portunus-agent`, and the names of `07` section 7 that it lists as changing with it.

*Source: P-001, P-016*

**Acceptance criteria**
1. A test fails on a code the library answers that `07` section 1 does not list, a
   configuration key it binds that section 4 does not list, a spelling that differs from
   `07`, and a public type whose name holds the library's name.

---

## 4. Testing

**PRT-ENG-008** — Tests SHALL be written first (checked in review), use xunit.v3 on
Microsoft.Testing.Platform and hand-written fakes, and name the acceptance criterion they
prove.

**Values (P-016).** Each test carries the trait `Category` with one of `unit` (nothing
outside the process), `integration` (something outside it: the fakes run as child
processes, a real Unix domain socket, `git` and the secret scanner, or the SDK's build,
pack and publish, writing under `tmp/`), `linux` (what only a Linux kernel answers: the
peer's credentials, file owners and modes, `prctl`, `/proc/swaps`, `SIGTERM`) and
`contract` (the public surface and the reference tables); the sample host's tests are a
fifth kind in their own project. The pipeline runs each kind on its own, by that trait; a
run that finds no test of its kind passes (Microsoft.Testing.Platform's exit code 8 is
accepted), and a test of `tests/` that carries none of the four kinds, or more than one,
fails the gate. The `linux` kind runs as root (P-027): under `sudo` on the pipeline's
runner, and on another machine in a container of the .NET SDK's image at the SDK of
`global.json`, as its root, which `build/gates/gate-tests.ps1` starts where it is asked
for that kind. The agent's start-level tests set its system paths only through internal
parameters, and its signal tests run `tests/Portunus.Agent.Harness` (P-027). A test that
runs `op` through the agent sets `Portunus:Agent:OpPath` to a launcher script of its own,
which sets the fake's variable and starts the fake's `op` (P-029). A test that connects to
a process's socket starts the harness as `client` under `setpriv` as the declared user
(P-030). The fake `op`'s rate-limited scenario is written from the documented `(429)`
class, and its scenario of a version other than the pinned one from the recorded version
with the version changed, each marked as not recorded; every other scenario is recorded
by the owner (P-028). An
acceptance-criterion test is named `ITEM_ACn_Outcome`
(`PRT_AGT_007_AC2_UndeclaredAddressIsDenied`); every other test
`Method_Scenario_Outcome`. No mocking package. The fakes are hand-written: the fake `op`
and the fake `ssh` of `tests/Portunus.Fakes` (PRT-ENG-001), whose `op` scenarios hold the
pinned `op`'s output as recorded from a test vault of dummy values (P-005), every
1Password identifier replaced by a stable placeholder of its form (P-035), and whose `ssh`
runs the command it is given on the same machine; a fake agent in the client's test
project, answering recorded answers on a real socket; and `Portunus.Testing`'s own types.
Time is a test `TimeProvider`. Every criterion a test can decide has one; the rest are
named in the phase report with how they were checked.

*Source: P-005, P-016*

**Acceptance criteria**
1. No mocking package is referenced.
2. Every testable criterion of a built item has a test carrying its identifier.
3. A test of `tests/` that carries no `Category` trait, more than one, or a value other
   than the four, fails the tests gate.

---

## 5. Dependencies

**PRT-ENG-009** — These SHALL be the only third-party packages, each referenced only by
the projects `04` PRT-LIB-001 allows.

| Purpose | Package | Licence |
|---|---|---|
| Hosting abstractions (`Portunus`) | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Diagnostics.HealthChecks` | MIT |
| Hosting (`Portunus.Agent`) | `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.Systemd`, `Microsoft.Extensions.Configuration.Json` | MIT |
| The siblings (the adapters) | `Janus.Hosting` and its dependencies from nuget.org, each listed in the allow-list (P-036); `Hermes.Core`, `Themis.Core`; the siblings' own from `vendor/` at the versions of `07` section 7 | The owner's own; each dependency's own, as listed |
| Public surface | `Microsoft.CodeAnalysis.PublicApiAnalyzers` (build-only) | MIT |
| Versioning | `MinVer` (build-only) | Apache 2.0 |
| Tests | `xunit.v3` | Apache 2.0 |
| A host in the client's tests (`tests/Portunus.Tests`, `tests/Portunus.Testing.Tests`) | `Microsoft.Extensions.Hosting`, the agent's own row's package (P-025) | MIT |

Every package is taken at its latest stable release when added, the Microsoft packages at
the release that matches the SDK's .NET 10 runtime, and its listing checked then (checked
in review). A sibling's package moves only with a release of this library (`04`
PRT-LIB-007). Adding a package is a decision-log entry. `op` and `ssh` are not packages:
the agent and the tool run the ones the computer holds, `op` at its pinned version (`07`
section 7).

*Source: P-005, P-016, P-025*

**Acceptance criteria**
1. The set of direct package references is within this table; a pull request adding
   another fails a check.

---

**PRT-ENG-010** — A package with a known vulnerability SHALL fail the build,
vulnerability alerting SHALL be on for the repository, and a security update SHALL be
taken as soon as it lands (checked in review).

**Values (P-016).** NuGet's audit of every package, transitive ones included, runs in each
restore (PRT-ENG-002), and its warnings fail the build. A security release of `op` is
taken by a release of this library that moves its pinned version (`07` section 7).

*Source: P-005, P-016*

**Acceptance criteria**
1. A restore resolving a package version with a published advisory fails the build.

---

## 6. Version control and gates

**PRT-ENG-011** — Work SHALL be trunk-based with short-lived branches reaching the
default branch by pull request (save the first, `docs:` commit), commit messages SHALL
follow Conventional Commits 1.0.0, a `CHANGELOG.md` SHALL follow Keep a Changelog 1.1.0,
and MinVer SHALL derive the version from the tag.

**Values (P-016, P-017).**
- **Commit message:** a type of `feat`, `fix`, `refactor`, `perf`, `test`, `docs`,
  `build`, `ci`, `chore`; an optional scope naming the part's area (`core`, `client`,
  `agent`, `cli`, `janus`, `hermes`, `themis`, `testing`, `deploy`, `sample`); a
  description in the imperative (checked in review), beginning with a lower-case letter
  (a name it mentions keeps its own spelling), with no full stop, the first line at most
  72 characters; a body only where the diff cannot explain itself (checked in review), as
  dash fragments of at most 72 characters, each a reason or a non-obvious consequence; a
  footer carrying the items the commit implements (`Implements: PRT-AGT-004`) and nothing
  else; `!` or `BREAKING CHANGE:` for a break of `04` PRT-LIB-007.
- **Changelog:** every commit that changes behaviour, one of type `feat`, `fix` or `perf`
  or marked `!`, adds its line under `Unreleased` in the same commit (`Added` while
  nothing is released).
- **History:** no force push to the default branch, and no merge of a red check. A commit
  that is not pushed and that no other branch has merged MAY be amended or rebased to meet
  a gate; history that is pushed or merged is never rewritten. `PublicAPI.Shipped.txt`
  changes only in a release commit, as `build/gates/release-baseline.json` does.
- **Release commit:** the commit that makes release `V`, a final release `X.Y.Z` or a
  release candidate `X.Y.Z-rc.N`, has the message `chore: release V` and changes no file
  but these: each `PublicAPI.Shipped.txt`, to its parent's entries and those of the
  `PublicAPI.Unshipped.txt` beside it, less each entry that file marks `*REMOVED*`,
  `#nullable enable` first and the rest in ordinal order; each `PublicAPI.Unshipped.txt`,
  to nothing but `#nullable enable`; `build/gates/release-baseline.json`, to the release's
  own set as the release gate reads it at that commit; `CHANGELOG.md`, whose `Unreleased`
  entries move under a new heading `## [V] - YYYY-MM-DD`, the commit's date in UTC, below
  an empty `## [Unreleased]`; and `RELEASE-NOTES.md`. In its pull request only `docs:`
  commits that change nothing outside `docs/` follow it.
- **Releases:** a release is a tag `v<V>` on the release commit, pushed once the pull
  request's merge has brought that commit into the default branch, by the implementer or,
  where the machine's permissions refuse it the push, by the owner; a release candidate on
  the lead's message, a final release on the owner's (P-017). The pipeline's release job
  runs only for such a tag, after the gates job has passed on it, and is the one job that
  can write to the repository's contents: it packs every package at the tag's version,
  publishes `Portunus.Agent` self-contained and single-file for `linux-x64` and
  `linux-arm64` as `portunus-agent-linux-x64` and `portunus-agent-linux-arm64`, writes
  `SHA256SUMS` over those two files and the three of `deploy/`, and creates the tag's
  release with the runner's GitHub CLI, a release candidate's marked as a pre-release, its
  notes the tag's `RELEASE-NOTES.md`, the packages, the two executables, the three files
  of `deploy/` and `SHA256SUMS` attached; it publishes to no package feed. Each package
  carries its identifier, its description (the text of its "What it holds" cell in `00`
  section 2.2, without markup or references), as its authors the name the lead's first
  message of phase 0 gives, and the repository's address, and `Portunus.Cli` also
  `PackAsTool` and `ToolCommandName` (`portunus`); every other package property is left
  unset, but `packageTypes` on `Portunus.Cli`, which the SDK writes of its own accord. A
  package's dependency on another of the library's packages is the exact version packed
  (`[v]`): `Directory.Build.props` holds one target, run after NuGet's
  `_GetProjectReferenceVersions`, that sets the `ProjectVersion` of each of NuGet's
  `_ProjectReferencesWithVersions` items to `[v]`, and `04` PRT-LIB-001's criterion 2 is
  what catches an SDK that changes those names; an adapter's dependency on its sibling is
  written `[v]` in `Directory.Packages.props`.

*Source: P-016, P-017*

**Acceptance criteria**
1. A commit message outside the rule, and a behaviour-changing commit without a changelog
   line, fail a check.
2. A commit that changes the content of an existing `PublicAPI.Shipped.txt` or
   `build/gates/release-baseline.json`, or deletes one, fails a check, save the release
   commit.
3. A commit `chore: release V` that changes a file outside the release commit's value, or
   one of its files otherwise than the value gives, or that a commit other than a `docs:`
   commit changing only `docs/` follows in its pull request, fails a check. A version
   outside `X.Y.Z` and `X.Y.Z-rc.N`, `N` from 1, fails it.
4. A release tag's run creates the tag's release, holding every package at the tag's
   version, both executables, the files of `deploy/` and `SHA256SUMS`, a release
   candidate's marked as a pre-release, only once its gates have passed, and no other job
   of the pipeline can write to the repository's contents.

---

**PRT-ENG-012** — The pipeline SHALL block on every gate below, and each gate SHALL be a
script the implementer runs locally with the same result, save the log gate, which the
integration tests' run checks.

| Gate | Source |
|---|---|
| Build and analysers, warnings as errors | PRT-ENG-002, PRT-ENG-004 |
| `dotnet format --verify-no-changes` | PRT-ENG-002 |
| Locked restore and the vulnerability audit | PRT-ENG-002, PRT-ENG-010 |
| Unit, integration, `linux` and contract tests | PRT-ENG-008 |
| Public surface files up to date | `04` PRT-LIB-002 |
| Release compatibility of the public surface, codes, configuration keys, enumerations, wire formats and the executables' commands and options | `04` PRT-LIB-007 |
| Dependency allow-list, and `vendor/` matching `vendor/SHA256SUMS` | PRT-ENG-001, PRT-ENG-009 |
| Project references and grants | PRT-ENG-001; `04` PRT-LIB-001 |
| The agent publishes self-contained and single-file for `linux-x64` and `linux-arm64` | PRT-ENG-011 |
| No value or token in any log line or output of the integration and `linux` tests: each such test captures its log lines and the standard output and error of what it runs, and asserts at its end that none holds a value the test used; it is checked by those tests' run and has no script of its own | `05` PRT-OPS-005 |
| Secret scanning of the whole history (gitleaks) | `08` PRT-THR-004 |
| Commit message format, changelog line, and `PublicAPI.Shipped.txt` and the release baseline unchanged outside a release commit | PRT-ENG-011 |
| Acceptance-criterion test names present for each criterion of `build/gates/built-items.txt` | PRT-ENG-008 |
| The sample host builds from the packed packages, its code passes `dotnet format --verify-no-changes` on `samples/Samples.sln`, and its tests pass | PRT-ENG-001, PRT-ENG-002 |

The gates live as scripts under `build/gates/`, which the pipeline runs in one job,
`gates` (the one status check branch protection requires), on every pull request, every
push to the default branch and every release tag, the release job waiting for the last;
the commit-message gate checks the commits a pull request or a push brings, and for a
release tag the commits since the previous release tag (the whole history for the
first), save the merge commits the platform writes, while the secret scan reads the
whole history each time; a gate whose part is built in a later phase has its script
added by that phase, and the job runs every script present. Before it pushes, the
implementer runs the fast checks, every unit test, the commit-message gate and the secret
scan, and the integration and `linux` tests it writes or its change touches; the
pipeline's `gates` run on the last commit pushed, the report's own commit, is the gate of
record, which the implementer waits for in the same run and names with its result.

*Source: P-016*

**Acceptance criteria**
1. Each gate runs on pull request, and any failing blocks the merge.
2. Each gate's script, run locally on a commit, gives the result the pipeline gives on it.
