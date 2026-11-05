# 00 — Overview

Specification for a small reusable .NET library that carries the secrets an application
needs (data encryption keys, database logins, outside providers' API keys, certificates)
from the vault they are kept in to the processes that use them, so that no secret sits on
a server's disk. A key agent on each server fetches an environment's secrets once, holds
them in memory only, and hands each process the secrets its declaration names over a
private local connection. Where the vault cannot be reached, the owner pushes the same
secrets from their own computer, through their own password manager, straight into the
agent's memory.

`Portunus` is the library's name (P-001): its namespaces, its project and package
identifiers, its solution file, its configuration section, its registration methods, the
commands of `Portunus.Cli` and `Portunus.Agent` and the names `07` section 7 lists carry
it. No type is named after the library (`06` PRT-ENG-007), and the names that are shared
by two releases (the codes, the wire formats, the socket's file name) are neutral and
never change.

Intended experience for a new project: **lay out the vault → declare each process →
start the agent → register one call.**

**Status:** specification. Everything needed to build is in documents 00 to 08.
`decision-log.md` records why each choice was made and what was rejected; it is
background and never required to build. Requirements cite their source decision so the
reasoning is one lookup away.

---

## 1. Reading this specification

### 1.1 Document set

| Document | Covers |
|---|---|
| **00-overview** | This document: what the library is, its parts, what the first release builds, glossary |
| **01-agent** | The key agent: its start, the fetch from the vault, memory, the sockets, declarations and least privilege, waiting, status |
| **02-push** | The manual way in: `portunus push`, the receive command, the push socket, the three cases and the push-only mode |
| **03-vault-and-rotation** | The vault's layout, the rule every project's vault follows, its check, and how a new version reaches the processes |
| **04-library-contract** | Packages, the public surface, registration, the client, the adapters for the sibling libraries, versioning, the testing package |
| **05-operations** | Configuration, the deployment's duties, health and status, logging, the token |
| **06-engineering-conventions** | Layout, setup, design rules, names, testing, dependencies, version control, gates |
| **07-reference** | Codes, enumerations, configuration keys, limits, wire formats, names |
| **08-threat-model** | Assets, threats and their controls, and what memory does not protect against |
| `decision-log.md` | Rationale. **Supplementary; not required to build** |
| `guide/implementation-plan.md` | The order of work |
| `guide/compromise-checklist.md` | What the owner does when a server is, or may be, compromised (`08` PRT-THR-003) |

`07-reference` is authoritative for every name, code, key, default and limit; where
another chapter names one, it is looked up there.

### 1.2 Requirement format

Every requirement carries an identifier, a normative statement and its source decision.
**Acceptance criteria accompany every requirement that can be tested.** A requirement
that states a deployment's duty, or a fact, carries none, or criteria checked in review.

> **PRT-AGT-001** — The agent SHALL hold every secret in memory only.
>
> *Source: P-002*

Identifiers are stable. A withdrawn requirement's identifier is retired, never reused.
Every identifier starts with `PRT-`, so it never collides with an identifier of another
library the host uses.

### 1.3 Normative language

**SHALL** / **SHALL NOT**: mandatory; a build that does otherwise is non-conformant.
**SHOULD** / **SHOULD NOT**: strong default; departure requires a recorded reason.
**MAY**: genuinely optional. Anything not marked is descriptive context.

### 1.4 Acceptance criteria

Each criterion is objectively checkable and written to be turned into a test. A
requirement whose criteria cannot be checked without human judgment is under-specified
and is raised, never guessed at.

### 1.5 Names and addresses

A **name** (an instance's name, a process's name) is 1 to 64 lower-case ASCII letters and
digits separated by single `-`, starting with a letter. An **item title** and the words
of a vault title follow `03` PRT-VLT-002. A **secret name** follows `03` PRT-VLT-003. An
**address** is an item title, `/`, and a secret name (`Themis/connection`); it names one
secret of one item, a single value or a set of versions (`03` PRT-VLT-004), and is
compared without regard to the case of ASCII letters, as the vault compares names (P-011).
A length in characters counts Unicode scalar values.

*Source: P-001, P-011*

---

## 2. What this is

**PRT-OVR-001** — The library SHALL be **product-agnostic**: it SHALL NOT name, assume or
depend on any host's applications, business or data. Which secrets a host keeps, and what
it does with them, are the host's.

*Source: P-001*

**Acceptance criteria**
1. No identifier, text, default or test fixture of the library names a host product,
   company or application (checked in review).

---

**PRT-OVR-002** — The sibling libraries (`Janus`, `Hermes`, `Themis`) SHALL be supplied
through their own secret seams by adapters of this library, and SHALL never change for
it nor know of it.

**Values (P-013).** Each sibling reads its secrets once at its start through its own
`ISecretSource` and refuses to start without them. An adapter package of this library
implements that seam over the client (`04` PRT-LIB-006); a host that uses none of the
siblings reads its own secrets through the client (`04` PRT-LIB-004).

*Source: P-001, P-013*

**Acceptance criteria**
1. Each adapter builds against the sibling's published `Core` package as released, with
   no change to it (`04` PRT-LIB-001).

### 2.1 Tech stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 |
| Vault | A 1Password vault, read by the 1Password CLI `op` at a pinned version (P-005) |
| Agent's platform | Linux, as a systemd service (P-015) |
| CLI's platforms | Windows, macOS and Linux, as a .NET tool (P-015) |

Versions are pinned in `06` PRT-ENG-009 and `07` section 7.

*Source: P-004, P-005, P-015*

### 2.2 The parts

| Package | What it holds | Who uses it |
|---|---|---|
| `Portunus.Core` | The public types and contracts the other packages share | Every other package, and a host's code that names the types alone |
| `Portunus` | The client a process reads its secrets through: from the agent, or in development from a local file | Every process that reads a secret, directly or through an adapter |
| `Portunus.Janus` | The `Janus.Core.ISecretSource` over the client | A host that uses Janus |
| `Portunus.Hermes` | The `Hermes.Core.ISecretSource` over the client | A host that uses Hermes |
| `Portunus.Themis` | The `Themis.Core.ISecretSource` over the client | A host that uses Themis |
| `Portunus.Testing` | A fixed secret client a host's tests set | A host's tests, and the sample host (`06` PRT-ENG-001) |
| `Portunus.Cli` | A .NET tool, command `portunus`, that pushes an environment's secrets, checks a vault's layout and reads an agent's status | The owner, on their own computer |
| `Portunus.Agent` | The key agent, `portunus-agent`, a self-contained Linux executable with its systemd unit, not a package | The servers |

*Source: P-001, P-013, P-015*

---

## 3. The first release

**PRT-OVR-003** — The first release SHALL build every requirement of `01` to `08`, and
SHALL be designed so that a later release adds a source of secrets other than the agent
and the development file, or an adapter for another library, without a breaking change
to the public surface (`04` PRT-LIB-007).

*Source: P-001, P-017*

---

## 4. Glossary

| Term | Meaning |
|---|---|
| **Owner** | The person who owns the deployment, its vaults and its servers |
| **Environment** | One deployment of one project (`Shop Production`, `Shop Staging`), with one vault |
| **Vault** | The 1Password vault of one environment, titled `<Project> <Environment>` (`03` PRT-VLT-001) |
| **Item** | One entry of the vault: the secrets of one owner of secrets (`03` PRT-VLT-002) |
| **Field** | One value of an item; one field per secret, or per version of a versioned secret |
| **Secret** | What a process reads at one address: a single value, or a set of versions with one current |
| **Set** | Every secret the agent holds at once, taken from one fetch or one push |
| **Service account** | The 1Password identity the agent reads the vault as, through its read-only token |
| **Token** | The service account's token, the one credential on the server's disk (P-010) |
| **Agent** | The long-running service on a server that holds one environment's set (`01`) |
| **Instance** | One agent on one server, named by the environment (`shop-production`) |
| **Process** | One application process that reads secrets from the agent |
| **Declaration** | The agent's list, per process, of the addresses it may read and the user it runs as (`01` PRT-AGT-007) |
| **Fetch** | The agent's read of the vault at its start (`01` PRT-AGT-004) |
| **Push** | The owner's delivery of a set from their own computer into a running agent (`02`) |
| **Adapter** | A package that supplies a sibling library's secret seam over the client (`04` PRT-LIB-006) |
| **Sibling** | Janus, Hermes or Themis: a library of the same owner with its own secret seam |
