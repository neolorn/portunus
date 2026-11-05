# Decision log

Why each choice of the specification was made and what was rejected. Supplementary: the
chapters are complete without it. An entry that changes an earlier one carries
`> **Amended.**` under the earlier entry's heading. An entry whose status is
**proposed** carries the lead's recommendation on a question that is the owner's or the
lead's to settle; the chapters follow it until the entry is accepted or replaced.

The source of intent is the owner's decisions for a secrets library (2026-11-05), as the
lead relayed them: its purpose, the key agent, the manual way in, revocation as the
point, the vault's layout, rotation and the platforms. Every one of them stands; where
this log goes beyond them, the entry says so and why. The facts about 1Password were
checked on 2026-11-05 against its developer documentation, whose address
`developer.1password.com` now redirects to `www.1password.dev`; each entry names the pages.

---

## P-001 — A small secrets library beside the siblings

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** One library, `Portunus`, that carries the secrets every .NET application needs
from where they are kept to where they are used, so no secret sits on a server's disk. It
feeds the siblings' own secret seams through adapters and gives any other host a client of
its own.

- **The owner's purpose:** data encryption keys, database logins, outside providers' API
  keys and certificates, fed from where they are kept to where they are used (`00`).
- **Product-agnostic,** as every library of the owner's is (`00` PRT-OVR-001).
- **The name.** Portunus, the Roman god of keys and doors. The shared names (the codes
  `secrets.*`, the wire formats, the socket's file name) are neutral, so a rename would
  touch no deployment.
- **Small.** One client, one agent, one tool, three adapters, one testing package. About
  Themis's size, at the lead's request.

*Rejected:* a secret source written into each host (every host would solve secret zero
again, and none would share the vault's layout); a library that changes the siblings (the
owner's decision: they never change and know nothing of it).

---

## P-002 — No secret on a server's disk

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** The agent holds every secret in memory only and never writes one to disk. The
host keeps no swap and writes no crash dumps: a deployment requirement, which the agent
checks for swap and enforces for its own dumps.

- **The owner's decision,** as written.
- **Memory that reaches the disk anyway** (swap, hibernation, a core dump) would undo it,
  so the agent refuses to start with swap active, marks itself not dumpable
  (`PR_SET_DUMPABLE`, which also keeps processes of its own user from reading its
  memory), and its unit sets the core limit to 0 (`01` PRT-AGT-001; `05` PRT-OPS-003).
- **Pinned, zeroed arrays.** A secret in a movable array can leave copies when the
  collector compacts; a `string` cannot be cleared. Values are pinned byte arrays, zeroed
  once used (`06` PRT-ENG-005). The token's one conversion to a `string`, for `op`'s
  environment, is accepted: .NET's process API takes nothing else, and the token is the one
  thing on the disk anyway.

*Rejected:* warning on swap instead of refusing (fail closed is the owner's rule for every
library); `mlock` of the set (a P/Invoke more for what no swap already gives).

---

## P-003 — One agent per environment, one fetch per start

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** A long-running agent on each server, one per environment, fetches the
environment's secrets once at its start; the application's processes read from it, never
from the vault, so restarting or crashing a process costs no vault reads.

- **The owner's decisions,** as written, the read budget being the reason (P-006).
- **Never a second fetch while running.** A periodic refresh would spend the quota for
  nothing: the siblings read once at their start (P-013), so a new value matters only to a
  process that starts after it. A restart or a push gives the agent the vault as it now is
  (`03` PRT-ROT-001).
- **An instance per environment,** so a server that carries two environments runs two
  agents with their own vaults and sockets.

*Rejected:* each process reading the vault itself (every restart a fetch; every process a
token); a refresh timer (above).

---

## P-004 — A 1Password vault, read by a read-only service account

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** The vault is a 1Password vault per environment, read by a service account whose
one permission is `read_items` on that vault. Verified: a service account authenticates
the CLI through `OP_SERVICE_ACCOUNT_TOKEN`; its vaults and permissions cannot be changed
after it is made; it cannot be granted the built-in Personal, Private or Employee vault or
the default Shared vault; its token is shown once; revoking the token removes its access at
once; rotating it makes a new token with the same permissions and lets the owner choose
when the old one expires.

- **The owner's decision:** a 1Password vault read through a service account.
- **How a service account reads items** (verified): `op` 2.18.0 or later, the token in
  `OP_SERVICE_ACCOUNT_TOKEN`, with `op item get`, `op read`, `op inject` and `op run`
  supported; `--vault` is required for `op item` where the account reaches more than one
  vault; Connect's variables, where set, take precedence and must be cleared. The agent
  clears its child's environment for that reason (`01` PRT-AGT-003).
- **Permissions** (verified): `read_items`, `write_items` and `share_items`, the latter
  two needing the first; immutable once made. Read-only is therefore fixed at creation,
  which suits a token that may one day be copied.
- **Up to 100 service accounts** per 1Password account: one per environment is far below.

*Sources:* `https://www.1password.dev/service-accounts/`,
`https://www.1password.dev/service-accounts/get-started/`,
`https://www.1password.dev/service-accounts/use-with-1password-cli/`,
`https://www.1password.dev/service-accounts/manage-service-accounts/`.

*Rejected:* one service account for every environment (a copied token of one server would
read every environment's vault).

---

## P-005 — The `op` CLI at a pinned version, not an SDK

> **Amended.** P-043 (accepted): the scenarios are recorded afresh by the owner's run
> against the real `op` before each release.

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** 1Password publishes no official .NET SDK: its SDKs are for Go, JavaScript and
Python, at version 0. The agent and the tool call the 1Password CLI, `op`, the agent at the
exact version 2.41.1, the tool at 2.41.1 or a later 2.x.

- **No .NET SDK** (verified): the SDK page lists Go, JavaScript and Python, says they are
  in version 0 and may break between version 0 releases, and names no .NET or C# SDK.
  Writing against 1Password's private API ourselves would be a client no one supports.
- **Connect rejected** (verified): a Connect server answers from a cache "in your
  infrastructure", which is a copy of the vault on the server, against P-002, and is a
  second service to run.
- **Pinned.** The agent parses `op`'s JSON, whose shape the reference does not document,
  so a new `op` could change what it reads; the agent therefore runs one version and
  refuses another, and a move is a release of this library. 2.41.1 (2026-10-21) is the
  latest stable release older than two weeks at this writing; 2.42.0 came out on
  2026-11-03. The tool runs on the owner's computer, where `op` is updated by the owner's
  own package manager, so it accepts the pinned version or any later 2.x.
- **The JSON read** is the least the agent needs: of a list, each entry's `id`, `title` and
  `vault.id`; of an item, `id`, `title` and each field's `label` and `value`, the field
  labelled `notesPlain` being the note (verified: the secret reference page's example
  addresses an item's notes as `notesPlain`). `--reveal` is passed, since the reference
  says sensitive fields are concealed without it. The fake `op`'s scenarios are recorded
  from the pinned version on a test vault of dummy values (implementation plan, phase 3).
- **Installation** (verified): the release archive
  `https://cache.agilebits.com/dist/1P/op2/pkg/v<version>/op_linux_<arch>_v<version>.zip`,
  its signature checked with `gpg --verify op.sig op` after
  `gpg --receive-keys 3FEF9748469ADBE15DA7CA80AC2D62742012EA22`.
- **No caching daemon.** On Unix-like systems `op` caches by default in a daemon process;
  `OP_CACHE=false` turns it off (verified), so no second process holds the vault's data.

*Sources:* `https://www.1password.dev/sdks/`, `https://www.1password.dev/connect/`,
`https://www.1password.dev/cli/reference/`,
`https://www.1password.dev/cli/reference/management-commands/item/`,
`https://www.1password.dev/cli/secret-reference-syntax/`,
`https://www.1password.dev/cli/get-started/`,
`https://app-updates.agilebits.com/product_history/CLI2`.

*Rejected:* an unofficial .NET client (no support, no audit); the latest `op` at each
install (the JSON could move under the agent); `op read` per secret (three reads each by
name, and one call per field instead of per item).

---

## P-006 — The read budget

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** The owner's plan allows 1,000 service-account requests a day per 1Password
account, shared by every service account of it, and 1,000 reads an hour per token. A
fetch reads each item once by identifier, `3 + n` reads for `n` items, so a whole
environment's start is a few dozen reads at most. A failed fetch waits, doubling its pause
from 30 seconds to an hour, and a rate limit goes to the hour at once.

- **The limits** (verified): per token, an hour's window of 1,000 reads and 100 writes on
  1Password, Families and Teams (10,000 and 1,000 on Business); per account, a day's
  window of 1,000 requests on 1Password and Families, 5,000 on Teams, 50,000 on Business,
  reads and writes together, shared by all its service accounts. Exceeding one answers
  `(429) Too Many Requests` with a suggested retry time; `op service-account ratelimit`
  shows the use. The owner's figure of 1,000 a day matches the 1Password and Families row.
- **Request counts** (verified): `op item get` and `op read` cost 3 reads by name and 1
  with the item's and the vault's identifiers; `op item list` costs 3 with `--vault` by
  name. Hence one list by the vault's title, then one get per item by identifiers: `3 + n`.
  With eight items an agent start is 11 reads; at the 64-item limit, 67.
- **Pauses.** Doubling from `Retry:First` to `Retry:Max`, spread by a fifth so agents
  restarted together do not ask together. The `(429)` text is matched in `op`'s standard
  error and the pause goes to the cap at once; the suggested retry time's form is not
  documented, so it is not parsed. A broken layout is not retried, since only the owner can
  mend it. With the cap at an hour, a failing agent costs at most 24 fetches a day; one
  that cannot reach the network costs none.
- **The unit's restart rate** (5 starts an hour) bounds an agent that crashes after its
  fetch.

*Sources:* `https://www.1password.dev/service-accounts/rate-limits/`,
`https://www.1password.dev/service-accounts/use-with-1password-cli/`.

*Rejected:* parsing the suggested retry time (an undocumented text); `op item list
--vault <id>` with the vault's identifier in configuration (one read saved for a
configuration that would break when the vault is remade).

---

## P-007 — One socket per process, and a declaration per process

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** Each declared process has its own directory holding its own Unix domain socket,
its group the process's, mounted into that process's container alone; the agent checks the
peer's user identifier on every connection, and answers only the addresses the process's
declaration names.

- **The owner's decisions:** a private local connection guarded by file permissions, and
  each process reading only what its declaration names.
- **A directory per process** is what Docker Compose can mount into one container alone;
  a socket shared by every process could only be told apart by user identifiers, which
  images often share. The peer's identifier (`SO_PEERCRED`, read with
  `Socket.GetRawSocketOption`) is the second line, for processes on the host itself.
- **Directories outlive the agent.** A container's bind mount keeps the directory it found;
  a directory deleted and made again at an agent's restart would leave the container
  holding the old one. The agent never deletes them, and the unit keeps its runtime
  directory across restarts (`RuntimeDirectoryPreserve=yes`); only the socket file is made
  again.
- **The declaration is on the agent's side,** in a root-owned file, since a process that
  declared its own grants would grant itself everything. No root process is declared.
- **The agent's user** holds `CAP_CHOWN` alone, to give each directory its process's group.

*Rejected:* one socket for all, told apart by user (images share users); a token per
process (a secret more on disk per process); TCP on the loopback (no file permissions, no
peer identity).

---

## P-008 — Waiting, not failing

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** While the agent holds no set, a read answers `waiting` with its reason, and the
client asks again until the host's bound has passed, logging plainly that it is waiting for
secrets; the agent reports `waiting` or `ready` for health checks.

- **The owner's decisions,** as written.
- **The answer comes at once and the client polls,** rather than the agent holding a
  connection open, so a slow agent never holds a socket per waiting process and the
  client's bound is its own.
- **A denied address is answered at once,** whatever the state, so a declaration's mistake
  shows at the first start rather than after the wait.
- **The client's health check is `Degraded`, never `Unhealthy`,** while the agent waits: a
  running process already holds what it read, and an orchestrator that restarted it would
  only make it wait.

*Rejected:* failing at once (every process would crash-loop while the agent waits for a
push); waiting without bound (a process would hang where the host wanted it to stop).

---

## P-009 — The manual way in

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** `Portunus.Cli`, a .NET tool on the owner's own computer (Windows, macOS, Linux),
reads the environment's vault through the owner's own 1Password app with `op`, unlocked by
the owner's biometrics, and sends it over the system's `ssh` straight into a running
agent's memory; nothing is written to the server's disk or shown. It covers an unreachable
vault, a revoked token, and a deployment with no token on the server at all, which is a
supported mode.

- **The owner's decisions,** as written, the three cases included.
- **Desktop app integration** (verified): on macOS, Touch ID; on Windows,
  Windows Hello; on Linux, the app's "Unlock using system authentication" with PolKit and
  a running PolKit authentication agent. It is turned on in the app's Settings, Developer,
  "Integrate with 1Password CLI". `op signin` lists the app's accounts, and `--account` or
  `OP_ACCOUNT` picks one. An authorization lasts until 10 minutes of inactivity, at most 12
  hours, per terminal window and per account; on Windows a sub-shell needs its own
  authorization; locking the app revokes every authorization. The tool clears
  `OP_SERVICE_ACCOUNT_TOKEN`, the Connect variables and any session variable from `op`'s
  environment so `op` uses the app. Whether each of the tool's `op` commands prompts again
  on Windows, the documentation does not say; the owner checks it by hand in phase 4.
- **The system's `ssh`,** so the owner's own keys, agent, configuration and `known_hosts`
  apply, and the tool carries no SSH stack; `StrictHostKeyChecking=yes`, so a push never
  goes to a host the owner has not accepted. The payload goes on `ssh`'s standard input to
  `portunus-agent receive`, which forwards it to the push socket: nothing on the server's
  disk, nothing in an argument.
- **A push into a ready agent needs `--replace`,** so a push meant for a waiting agent
  never silently replaces a running set.
- **No service-account quota:** a push reads as the owner's own account.

*Sources:* `https://www.1password.dev/cli/app-integration/`,
`https://www.1password.dev/cli/app-integration-security/`.

*Rejected:* an SSH library in the tool (a second SSH stack beside the owner's own, its own
host keys); forwarding the push socket over SSH (a socket path to manage on the owner's
computer, where a remote command's standard input already carries the payload); writing the set to a file on the server for the agent to read
(against P-002).

---

## P-010 — Revocation is the point

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** A copied disk holds at most the service account's token, which the owner revokes
in 1Password in one action, so a compromised server buys time instead of handing over
every key at once. Memory does not protect against someone already inside the running
server, and the specification says so. A compromise checklist ships with the release.
Backups are not this library's, but data backups and the vault are never in the same place.

- **The owner's decisions,** as written.
- **The token on disk** is root-owned and handed to the service as a systemd credential,
  so the agent's user holds no right to the file. The agent holds it only until its fetch
  succeeds.
- **Sealing the token to a TPM** (`systemd-creds`, `LoadCredentialEncrypted=`) goes beyond
  the owner's decisions: it makes a copied disk's token useless elsewhere, at the cost of
  a server bound to its TPM. It is a deployment's choice (`05` PRT-OPS-002), listed for the
  owner.
- **What memory does not protect** is written in `08` PRT-THR-002, root first.
- **The checklist** is `docs/guide/compromise-checklist.md`.

*Rejected:* keeping the token after the fetch for a later refresh (P-003 rules the refresh
out, and a token in memory is one more thing a dump of the agent would hold).

---

## P-011 — The vault's layout, the library's rule

> **Amended.** P-021 (accepted): where a library's connection string and an outside
> provider's credential live.

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** One vault per project and environment, `<Project> <Environment>`; one item per
owner of secrets; one field per secret, labelled exactly as the code names it; versions as
fields with a current marker; a note on each item; company infrastructure that no service
reads kept out. The agent, the push and the development file refuse a set that breaks it.

- **The owner's decisions,** as written.
- **Names** (verified): a secret reference allows ASCII letters, digits, `-`, `_`, `.` and
  whitespace in names, other characters needing identifiers, and is case-insensitive. The
  rule keeps item titles and labels within those characters, so every secret has a plain
  reference, and refuses two labels alike but for case, which a reference could not tell
  apart. Sections are not part of a name, so labels are unique across them.
- **Keys as base64,** since a field holds text; the reading code decodes, as Janus's own
  key document already does.
- **No attachments:** a certificate is its PEM text; documents cost more reads and a
  second path.
- **The note** is 1Password's own `notesPlain` field, read by people, never delivered. A
  missing note is a warning: the agent cannot judge a note, only the owner can.
- **One validator** for every path, so the agent, the push and development agree.

*Source:* `https://www.1password.dev/cli/secret-reference-syntax/`.

*Rejected:* one item per secret (the owner's rule; it also costs a read per secret); a
lookup table from code names to vault names (the owner's rule: labels are the code's
names).

---

## P-012 — Versions and rotation

> **Amended.** P-019 (accepted): retired versions; P-020 (accepted): Hermes's write
> version.

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** A single secret is one field, its past values in 1Password's item history; a
versioned secret keeps each live version as `<name>.<n>` and its current one in
`<name>.current`. A new version is written to the vault first, then reaches the agent by a
restart or a push, then each process by its restart. Each sibling's versions reach it as
it expects.

- **The owner's decisions,** as written.
- **How the siblings read versions** (read from their sources, 2026-11-05): Janus's
  `ISecretSource` answers `KeyEncryptionKeys` and `FingerprintKeys`, each a current version
  and a dictionary of versions, so it reads the marker; Hermes's and Themis's
  `ReadAsync(name)` answer `SecretVersions`, a list of numbered versions, Hermes choosing
  its write version from its own configuration and Themis using the highest. Each sibling
  reads once, at its start. Janus's provider credential is either a static secret or a
  signing credential of an issuer, a key identifier and a PKCS #8 key.
- **Janus's provider credential** is one field, as Janus names it one secret: its value a
  static secret, or a JSON object of the three members, told apart by a leading `{`.
- **The rotation steps** follow each sibling's own procedure (`03` PRT-ROT-002), the
  library guaranteeing only delivery.

*Rejected:* a fixed rotation procedure of the library's own (the siblings' procedures
differ, and are theirs).

---

## P-013 — Adapters for the siblings, which never change

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** `Portunus.Janus`, `Portunus.Hermes` and `Portunus.Themis` each implement their
sibling's `ISecretSource` over the client, registered as a singleton by one call, so the
sibling finds it as it finds any host's source.

- **The owner's decision:** adapter packages, one per sibling, so the libraries never
  change and know nothing of Portunus.
- **Each adapter knows its sibling's names and forms,** which are the sibling's published
  surface; a name the adapter does not know (a later sibling release's) is not guessed at,
  but answered as absent with a warning, so the sibling refuses its start naming it.
- **Failures become the sibling's own refusals**, so the host's start stops with the
  sibling's code naming the secret, after the client has logged why.

*Rejected:* one adapter package for all three (a host would carry all three siblings);
a generic name map in configuration (the sibling's names are fixed in its code; a map
could only be wrong).

---

## P-014 — A local file in development, a fake in tests

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** In development the client reads a local JSON file of the vault's layout,
refused outside a Development host; tests use a fixed client, and the agent's tests a fake
`op`.

- **The owner's decisions,** as written.
- **Refused outside Development,** so a file left in a production configuration stops the
  start rather than serving secrets from a disk.

---

## P-015 — The agent on Linux under systemd, the tool on three platforms

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** The agent is a self-contained single-file Linux executable (x64 and arm64), run
as a systemd template unit per instance, beside Docker Compose applications; the tool is a
.NET tool for Windows, macOS and Linux.

- **The owner's decisions** on platforms.
- **A host service, not a container,** so the host's swap and dump settings, the user
  identifiers the sockets check and the unit's hardening are the host's own, and the agent
  is up before the container runtime starts its containers.
- **Self-contained,** so a server needs no .NET runtime for it.
- **The unit's hardening** (`07` section 7) narrows what the agent can touch: a read-only
  system, no home, one capability.

*Rejected:* the agent as a Compose service (its socket directories and its own memory
settings would depend on each project's Compose file); a .NET tool for the agent (a
runtime to install on every server).

---

## P-016 — How the library is built

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** The siblings' way: .NET 10, packages released together at one version, a public
surface checked by its files and a release gate, tests first, gates as scripts,
Conventional Commits, Keep a Changelog, MinVer, secret scanning and a testing package; no
database, so no migrations; a `linux` test kind for what only a Linux kernel answers.

- **The same conventions as Themis's and Hermes's** (layout, settings, analysers, gates,
  release commit, secret scanning), so an implementer and a reviewer move between them
  without learning a new way.
- **No package feed.** Releases attach the packages and the agent's executables to the
  tag's release; a consumer copies them, as the owner's consumers already do.
- **The siblings' packages in `vendor/`,** as the owner's consumers take them, each file's
  hash listed and checked (P-022).

---

## P-017 — Release candidates until the final 1.0.0

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** As for every library of the owner's: release candidates `1.0.0-rc.N` until he
calls a release final; the compatibility rule binds from the first final release. The
first release is `1.0.0-rc.1`. A release candidate is made on the lead's message, a final
release on the owner's.

---

## P-018 — Logs, and no metrics

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** The library logs names, states and counts, never a value, a token or `op`'s
output, and publishes no meter.

- **Names are not secrets.** Logging an address read by a process gives the owner an audit
  of who read what at each start; logging values never.
- **`op`'s standard error is not logged.** It is matched for its class alone; its text is
  not documented and could, in a later version, carry more than an error.
- **No meter:** the agent's events happen at starts, a handful a day, which the journal
  records better than a counter.

---

## P-019 — Retired versions kept as `.retired`

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** A version its library has retired but a backup still needs is relabelled
`<name>.<n>.retired`: kept in the item, checked, never delivered.

- **Why a choice is needed.** The owner's rule keeps old versions while any data or backup
  needs them; Janus's says a retired key-encryption key version leaves the application's
  key document at once, keeping it for a backup being the deployment's. Both hold only if
  the vault can keep a version without delivering it.
- **The recommendation** keeps one field per version in the item that owns it, as the
  owner's rule asks, and costs the agent one suffix to skip.

*Rejected for the recommendation:* deleting the field and relying on the item's history
(a restore would need the history's right entry, by date); a separate archive item (one
more item per owner of secrets, which the owner's rule does not make).

---

## P-020 — Hermes's write version stays Hermes's configuration

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** The Hermes adapter passes every live version and not the `.current` marker;
Hermes takes its write version from its own configuration, as it expects, and the owner
moves the marker in the same step.

- **Why a choice is needed.** The owner's rule marks the current version in the vault;
  Hermes reads its write version from `Hermes:Secrets:<Key>:WriteVersion`, not from its
  secret source. The adapter cannot set Hermes's configuration: the configuration is read
  before any secret.
- **The recommendation** keeps Hermes unchanged and the vault's layout uniform; the cost
  is two places to move at one step of a rotation.

---

## P-021 — A library's connection string and a provider's credential

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** P-011

**TL;DR.** A library's own database connection lives in the library's item, under the
name the library gives it; `Database` holds the logins that belong to the database rather
than to one component's code (migration logins, the host's own connection); a credential
an outside provider issued lives in that provider's item, an adapter told so per secret.

- **Why a choice is needed.** The owner's examples name `Database` and one item per outside
  provider beside the libraries' items. Every sibling names its connection `connection`, so
  one `Database` item could not hold two of them under their code's names; and Janus
  presents a social provider's credential, which is both Janus's and the provider's.

---

## P-022 — The adapters build against the siblings' released packages, pinned exactly

**Date:** 2026-11-05 · **Status:** accepted

**TL;DR.** The adapters reference `Janus.Core` 1.0.0-rc.4, `Hermes.Core` 1.0.0-rc.6 and
`Themis.Core` 1.0.0-rc.2, the newest release of each on 2026-11-05, as exact versions,
taken from the siblings' release assets into `vendor/` with their hashes.

- **Exact,** so a host that mixes a sibling's version with an adapter built for another
  learns it at restore, not at start. The cost: each sibling release candidate the host
  takes needs a Portunus release.

*Rejected for the recommendation:* a minimum version (a sibling's release candidate may
change its seam, and the host would learn it at start).

## P-023 — The wire formats carry values that never need escaping

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-005, PRT-ENG-006

**TL;DR.** In the library's own wire formats a value is written in base64url (RFC 4648
section 5, unpadded), which JSON never escapes. The library writes those bytes, and reads
them back, itself, through spans over its own pinned arrays, so no helper of the runtime
holds a copy of a value that the library cannot zero. Found at phase 1: the chapters had a
value read through `Utf8JsonReader.CopyString` or `TryGetBytesFromBase64` and written
through `Utf8JsonWriter.WriteBase64String`, and nothing in the platform's documentation
says those helpers make no copy of their own while they unescape or encode.

- **Writing.** The library encodes a value with `Base64Url.EncodeToUtf8` into its pinned
  buffer and writes the result with `Utf8JsonWriter.WriteStringValue(ReadOnlySpan<byte>)`
  into a writer over a pinned array of the bound's size. A base64url text holds no
  character the writer escapes, so the writer copies it straight into its output.
- **Reading.** A value's string is refused where `Utf8JsonReader.ValueIsEscaped` is true,
  as `secrets.request.invalid`, a request outside its format; otherwise the library
  decodes `ValueSpan` with `Base64Url.DecodeFromUtf8` straight into its pinned array.
- **What `op` prints** is not the library's format. A field's text there may be escaped
  (a password holding a quotation mark), so it is read with `CopyString` into a pinned
  array, and a key's base64 field (`03` PRT-VLT-003) is decoded from that array. What the
  runtime may hold while it unescapes is named in the threat model beside what `op`'s own
  process and the kernel's pipe hold: copies outside the library's reach, gone when that
  memory is reused, and none of them on disk (PRT-THR-002).
- **What stays.** The vault's fields keep base64 (RFC 4648 section 4, padded), the form an
  owner pastes; only the library's own formats change.

*Rejected:*
- **Trusting the helpers unexamined:** the spec would promise what it cannot show.
- **A hand-written JSON reader:** a second parser of the same format, for one member kind.

**Propagated to:**
- `06` PRT-ENG-005 (the reading and writing of a value) and PRT-ENG-006 (base64url);
- `07` section 6 (`<b64>` read as base64url);
- `01` PRT-AGT-008 (the read's answer in base64url);
- `08` PRT-THR-002 (the copies outside the library's reach).

## P-024 — A live and a retired version of one number, and a secret only retired

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-VLT-004, PRT-VLT-005

**TL;DR.** A live version and a retired version of the same number in one item
(`keys.2` beside `keys.2.retired`) is `secrets.layout.fieldduplicate`, naming the later
label, since a number names one version. A secret holding only retired versions is
allowed: no process reads it any more, and it stays for a backup's restore. A declaration
naming such a secret finds it unknown, as for a secret the set does not hold. Found at
phase 1: no rule covered either case.

**Propagated to:**
- `03` PRT-VLT-005 (both cases and a criterion).

## P-025 — The client's tests run a real host

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-009

**TL;DR.** `tests/Portunus.Tests` and `tests/Portunus.Testing.Tests` take
`Microsoft.Extensions.Hosting`, already in the table for the agent, so a test of
`AddPortunus` and `AddPortunusTesting` runs a real host's start, its hosted
lifecycle service and its health checks, as a host meets them. Found at phase 2: the test
projects held no service container, and one written by hand could not build what the
options and the health checks register without reflection (PRT-ENG-004).

*Rejected:*
- **The container package alone:** it builds the services but runs no host start, which
  is where the client is made (PRT-LIB-003).
- **A container written by hand:** reflection, which the conventions refuse.

**Propagated to:**
- `06` PRT-ENG-009 (the row).

## P-026 — The agent reads and sets owners through `statx` and `fchownat`

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-004

**TL;DR.** Beside `prctl`, the agent calls two more functions of the C library through a
source-generated `LibraryImport`: `statx`, to read a file's or a directory's owner, group
and mode, and `fchownat` with `AT_SYMLINK_NOFOLLOW`, to set a directory's or a socket's
owner and group. Found at phase 3: the .NET runtime reads and sets a Unix file's mode but
not its owner or group, while PRT-OPS-001 checks that the agent's file is root's and
PRT-AGT-006 sets each directory's group.

- **Why `statx`:** its structure is the same on every architecture the agent ships for
  (`linux-x64`, `linux-arm64`), where `stat`'s is not.
- **Why `fchownat` with `AT_SYMLINK_NOFOLLOW`:** it never follows a link planted where a
  directory was expected.
- **What stays.** No other native call; the peer's credentials are still read with
  `Socket.GetRawSocketOption`; modes are still set through the runtime.

*Rejected:*
- **The deployment setting owners (a unit's `ExecStartPre`, `tmpfiles.d`):** the agent
  must still read owners to check them, which needs `statx` anyway, and a correction it
  cannot make is a start it cannot finish.
- **Running `chown` and `stat` as child processes:** more processes, more parsing, and a
  `PATH` to trust.

**Propagated to:**
- `06` PRT-ENG-004 (the native calls and criterion 2).

## P-027 — How the agent's start and stop are tested

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-001, PRT-ENG-008

**TL;DR.** The `linux` kind runs as root, so owners, groups and `chown` are real. The
agent's start takes its system paths (the configuration's directory, `/proc/swaps`) as
internal constructor parameters that only tests set, while the executable always passes
the fixed real ones. A real `SIGTERM` reaches a harness executable that builds the agent
exactly as the shipped one does, but with a test's paths. Found at phase 3: a test that is
not root cannot own a file as root, a runner may hold swap, and a signal sent to the test
process would end the test run.

- **Root.** On the pipeline's runner, `build/gates/gate-tests.ps1` runs the `linux` kind
  under `sudo`. On another machine it runs it in the SDK's container, whose user is root.
  Every file a test creates as root it removes when it ends.
- **The seams.** The internal start type takes the paths above as parameters, visible to
  the agent's test project and the harness through `InternalsVisibleTo` alone. No
  environment variable, argument or configuration key changes them, and the shipped
  executable's entry point passes the real values. The identity the safety check takes as
  root is never a parameter: it is root.
- **The harness.** `tests/Portunus.Agent.Harness` is a console project, not shipped, that
  references `Portunus.Agent` and builds its host through the same internal method the
  shipped entry point uses (the same signal handling), with the paths a test gives in its
  arguments. A test starts it, sends `SIGTERM` with the `kill` command, and checks its exit
  and its logs (PRT-AGT-009).
- **What stays.** The shipped executable holds no test path, no test switch and nothing a
  test sets.

*Rejected:*
- **A test environment variable read by the shipped executable:** a switch in production
  that weakens the safety check.
- **Signalling the test process:** it ends the run.

**Propagated to:**
- `06` PRT-ENG-001 (the harness project) and PRT-ENG-008 (the `linux` kind as root);
- `04` PRT-LIB-001 (the harness's references).

## P-028 — The rate limit's scenario is written, not recorded

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-008

**TL;DR.** The fake `op`'s rate-limited scenario is written from 1Password's documented
class, a standard error naming `(429)`, and marked in its file as written, not recorded.
Every other scenario is recorded by the owner. Found at phase 3: provoking a real `(429)`
spends the whole account's daily allowance of 1,000 reads, which every service account on
the account shares, and so stops every environment's starts for a day.

- **A version other than the pinned one** (`01` PRT-AGT-002's `secrets.agent.opversion`)
  is written too, from the owner's recording of `op --version` with its version changed,
  and marked the same way: the pinned `op` never answers another version.

**Propagated to:**
- `06` PRT-ENG-008 (the scenarios).

## P-029 — A test runs the fake `op` through a launcher of its own

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-001, PRT-ENG-008

**TL;DR.** A test that runs `op` through the agent writes, in its own temporary directory,
an executable POSIX shell script that sets the fake's scenario variable and starts the
fake's `op` with the arguments it was given, and sets `Portunus:Agent:OpPath` to that
script. Found at phase 3: the agent starts `op` in an environment of three variables
alone (`01` PRT-AGT-003), so the fake never sees a variable the test sets.

- **The script:** `#!/bin/sh`, then
  `PORTUNUS_FAKES_SCENARIO='<scenario file>' exec '<the fake's op>' "$@"`; owned by root,
  mode `0755`, written and removed by the test.
- **The environment's check.** The fake records its environment's names; PRT-AGT-003
  criterion 1 checks them less the fake's own variable, which the script added after the
  agent's start.
- **What stays.** The agent's code, its environment rule and `OpPath`'s rule; the fake's
  variable and its scenario files.

*Rejected:*
- **The agent passing the fake's variable through:** a test switch in the shipped
  executable, which P-027 forbids.
- **The fake reading its scenario from `HOME`:** a test's file in the agent's own
  directory, which the agent makes and owns.

**Propagated to:**
- `06` PRT-ENG-001 (the fakes) and PRT-ENG-008 (the launcher).

## P-030 — The harness is the client of a process's socket in the tests

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-001, PRT-ENG-008

**TL;DR.** `tests/Portunus.Agent.Harness` takes a second role, `client`: it connects to the
socket it is given, sends the request bytes of a file it is given, shuts down its sending
side, and writes the answer's bytes, or that none came, to standard output. A test starts
it under `setpriv --reuid=<uid> --regid=<gid> --clear-groups` as the declared process's
user. Found at phase 3: the `linux` tests run as root, a declaration never names root, and
.NET starts no child as another user, so every connection a test makes to a process's
socket is refused by the peer check.

- **Raw bytes,** so the tests of PRT-AGT-008 send a request over 4 KiB, a slow one, and
  one of a kind the socket does not take. For 64 connections at once, a test starts that
  many clients, or the role takes a count; the implementer's choice.
- **The user** is a number a test picks, which needs no entry in `/etc/passwd`. Where the
  harness's files cannot be read by that user, the test copies them where they can.
- **`setpriv`** is part of `util-linux`, present on the pipeline's runner and in the SDK's
  image; nothing is installed.

*Rejected:*
- **The fakes taking the role:** they stand in for outside programs (`op`, `ssh`); a
  client stands in for a host.
- **`socat` or `nc -U`:** a package to install in the container and on the runner.

**Propagated to:**
- `06` PRT-ENG-001 (the harness) and PRT-ENG-008 (the client).

## P-031 — The token file's final line ending is not the token's

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-AGT-002, PRT-AGT-003

**TL;DR.** The agent reads the token as the file holds it, less one final line ending
(`\n` or `\r\n`), and a file holding nothing else counts as empty
(`secrets.agent.tokenunavailable`). Found at phase 3: a file written by an editor or by
`echo` ends with a line ending, which `op` would take as part of the token.

- **Only one, only at the end.** Any other byte stays, so a token is never altered beyond
  what a text file adds; a token with a space or a second line fails at the vault as it
  is.

*Rejected:*
- **Trimming all white space:** silently changes a file that is wrong in another way.
- **Refusing a final line ending:** fails the most common way of writing the file.

**Propagated to:**
- `01` PRT-AGT-002 (the empty file) and PRT-AGT-003 (the token's reading).

## P-032 — A peer whose credentials cannot be read is refused

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-AGT-006

**TL;DR.** On a process's socket, a connection whose `SO_PEERCRED` cannot be read is
closed without an answer and logged as refused, as one from another user is. Found in the
review of phase 3: the agent served such a connection, unchecked.

- **The log** names the process and gives the peer as unknown.

**Propagated to:**
- `01` PRT-AGT-006 (the peer check).

## P-033 — The tool names an option as it is written

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-PSH-002

**TL;DR.** `secrets.cli.inputinvalid`'s `details.member` names the option as the owner
types it, with its dashes (`--vault`, `--to`, `--instance`, `--account`), as the agent's
refusals name a configuration key as the file writes it. Found at phase 4.

*Rejected:*
- **The bare name (`vault`):** names nothing the owner typed.

**Propagated to:**
- `02` PRT-PSH-002 (the refusal); `07` section 1 (the code's details).

## P-034 — An SSH destination holds at most one `@`

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-PSH-002

**TL;DR.** An `<ssh destination>` holds at most one `@`, so a host alone, or an alias of
the owner's SSH configuration, is a destination, and `ssh` takes the user from that
configuration. Found at phase 4: `07` section 3 read "one `@`".

- **What stays.** The characters, the length and "not starting with `-`", so a destination
  is never read as an option of `ssh`, which also gets it after `--`.

*Rejected:*
- **Exactly one `@`:** refuses the owner's aliases, which carry the user and the key.

**Propagated to:**
- `07` section 3 (the destination's rule).

## P-035 — The recordings carry no identifier of the owner's account

> **Amended.** P-043 (accepted): the script is `build/check-real-op.ps1`, run before each
> release.

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-008

**TL;DR.** `build/record-op-scenarios.ps1` replaces every 1Password identifier it records
(vaults, items, users, accounts) with a placeholder of the same form, the same identifier
always the same placeholder across every file, before it writes anything. Found at
phase 4: the scenario files go into a public repository, and a recording names the
owner's vaults, items and user.

- **The shape stays recorded:** keys, nesting, lengths and alphabets are 1Password's own,
  so the agent still reads what the pinned `op` writes (P-005); only the values of
  identifiers change.
- **A run that finds an identifier left** in what it wrote, or a token, removes every
  file it wrote and exits 1.

*Rejected:*
- **Recording as is:** the owner's account structure in a public repository, for nothing a
  test needs.

**Propagated to:**
- `06` PRT-ENG-008 (the recordings).

## P-036 — The Janus adapter references `Janus.Hosting`

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** P-022, PRT-LIB-001, PRT-ENG-009

**TL;DR.** `Portunus.Janus` references `Janus.Hosting` 1.0.0-rc.4, exactly, the package
that carries Janus's `Janus.Core` assembly and its `ISecretSource`. Found at phase 5:
Janus publishes no `Janus.Core` package; its areas ship inside `Janus.Hosting` (Janus
D-205).

- **Nothing new for a host.** The adapter's only users are Janus's hosts, which reference
  `Janus.Hosting` already, so the adapter brings them no package they lack.
- **Portunus's side.** The dependency allow-list gains `Janus.Hosting`'s closure for that
  one project, checked as every other row is; `vendor/` holds `Janus.Hosting` as Janus's
  release published it; `nuget.config` maps `Janus.*` to `vendor/` alone.
- **What stays.** Exact versions (P-022), and the Janus table of PRT-LIB-006, whose
  type is still `Janus.Core.ISecretSource`, now from `Janus.Hosting`'s assembly.

*Rejected:*
- **Asking Janus to publish `Janus.Core` on its own:** reverses Janus's D-205 for one
  consumer, with a Janus release, where the hosts already hold the assembly.

**Propagated to:**
- `04` PRT-LIB-001 (the adapter's references); `06` PRT-ENG-001 (`vendor/`, `nuget.config`)
  and PRT-ENG-009 (the siblings' row); `07` section 7 (the pinned package).

## P-037 — A name the table does not list is read as text

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-LIB-006

**TL;DR.** The Hermes and Themis adapters read a name their table does not list as UTF-8
text and pass it on, logged once at the Information level with the name, instead of
answering none. Hermes's `smsCequens` (Hermes M-043, its optional package's token) joins
the table's text names. Found at phase 5: a sibling's optional package adds names of its
own, and answering none refuses the host's start wherever the package is used.

- **Why text is safe.** A sibling checks each secret's form at its start; a binary key a
  later release adds, passed on as its base64 text, fails that check by name, so a wrong
  reading stops the start plainly and never goes unseen. A new binary name joins the
  table when its sibling's release is taken.
- **What stays.** The table's forms, the address rule, a denied address answering none
  with its Warning, and the Janus table, whose members are fixed by Janus's interface.

*Rejected:*
- **Adding names one by one and answering none to the rest:** every optional package of a
  sibling would need a Portunus release before a host could use it.

**Propagated to:**
- `04` PRT-LIB-006 (the Hermes and Themis table, and the unlisted name).


## P-038 — Patched versions are pinned where a sibling brings a vulnerable one

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-009, PRT-ENG-010

**TL;DR.** Where a sibling's package brings, transitively, a version with a published
advisory, `Directory.Packages.props` pins the patched version through central transitive
pinning (`CentralPackageTransitivePinningEnabled`), and the allow-list lists it beside
that sibling. Found at phase 5: `Janus.Hosting` 1.0.0-rc.4 brings, through OpenIddict
7.7.1, `System.Security.Cryptography.Xml` and `System.Security.Cryptography.Pkcs`
10.0.11, with advisories; 10.0.14 of both is published.

- **The pins:** `System.Security.Cryptography.Xml` and `System.Security.Cryptography.Pkcs`
  10.0.14, the audit failing the build as before (PRT-ENG-010) if a later advisory
  reaches them.
- **Why here and not only in Janus.** Janus's hosts are ASP.NET Core applications: the
  shared framework supplies both assemblies, so the restore prunes the packages and the
  serviced runtime's copies run (Cura's auth server restores none of them). A class
  library has no shared framework to prune them, so the adapter's restore sees them; the
  pin keeps its own build and its packed dependencies clean, and its hosts lose nothing.
- **What stays.** The audit, its warnings as errors, and the adapter's references
  (P-036): the pins are versions, not references of the adapter's own.

*Rejected:*
- **Waiting for a Janus release that takes the patched versions:** Janus's hosts are not
  exposed, and the adapter would wait on a release that changes nothing for them.
- **References of the adapter's own to both packages:** outside its PRT-LIB-001 row.

**Propagated to:**
- `06` PRT-ENG-009 (the siblings' row) and PRT-ENG-010 (the pin).

## P-039 — The adapters' tests run a real host

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-009

**TL;DR.** The adapters' test projects take `Microsoft.Extensions.Hosting`, as the
client's do (P-025), so PRT-LIB-006 criterion 4 is proven in a host built with the
adapter's call alone. Found at phase 5.

*Rejected:*
- **Proving the criterion on the registration's service descriptors:** the criterion is
  about what a host resolves, which the descriptors alone do not show.

**Propagated to:**
- `06` PRT-ENG-009 (the hosting row).

## P-040 — The sample host is a web worker with the Hermes adapter

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-LIB-001, PRT-ENG-001

**TL;DR.** The sample host takes the ASP.NET Core shared framework through the Web SDK,
as the siblings' sample hosts do, and runs as a generic host with a hosted service; it
registers `AddPortunus` and `AddPortunusHermesSecrets`, reading one secret of its own
through the client and Hermes's `connection` through Hermes's `ISecretSource`. Found at
phase 6: none of the packed packages brings the generic host, and the chapter named the
Themis adapter, whose package is not yet vendored.

- **The framework, not a package.** Every sibling's host runs on the shared framework, so
  the sample host shows the adapters where they are used, and adds no row to the
  allow-list.
- **Hermes, not Themis.** The Hermes adapter is the lightest built one and needs nothing
  vendored that is not already there; the sample host shows one adapter, and which one is
  no part of its point. The Janus adapter's closure would make the sample host carry
  Janus's dependencies for nothing it shows.

*Rejected:*
- **`Microsoft.Extensions.Hosting` as a package:** a row in the allow-list for a host the
  siblings run on the framework.
- **Waiting for the Themis adapter:** holds phase 6 on a file the sample host does not
  need.

**Propagated to:**
- `04` PRT-LIB-001 (the sample host's row); `06` PRT-ENG-001 (the sample host).

## P-041 — Which controls the threat test reads

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-THR-001

**TL;DR.** PRT-THR-001 criterion 1's test reads every requirement its table names, and
finds a test carrying each identifier, save those on a list kept beside the test, each
with its reason: PRT-ENG-012 (the pipeline's own run), PRT-OPS-005 (checked by the
integration tests' run), PRT-THR-003 (checked in review), PRT-OPS-002 and PRT-PSH-001
(no criteria of their own), and PRT-ENG-005 (proven by PRT-AGT-001 criterion 3's tests).
A requirement added to the table is read unless it joins the list, which the review
checks. Found at phase 6.

**Propagated to:**
- `08` PRT-THR-001 (criterion 1).

## P-042 — The sample host's tests start it as the siblings' do

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-001, PRT-ENG-009,
PRT-LIB-001

**TL;DR.** The sample host's entry point is top-level statements in `Program.cs`, the one
file of the repository outside the namespace rule, so the Web SDK generates its public
`Program`; its tests start it through `WebApplicationFactory<Program>`
(`Microsoft.AspNetCore.Mvc.Testing`), replacing the client with `Portunus.Testing`'s
through the factory's service configuration, as the Hermes and Themis samples do. Found at
phase 6: a test reaches the host only through its entry point, the host may hold no grant,
and a public type of its own fails CA1515.

- **The same way as the siblings,** whose sample hosts' tests are written so; the factory
  hosts a generic host as well as a web one, and the sample host serves no request.
- **What stays.** No grant from the sample host, CA1515 everywhere it applies, and the
  namespace rule for every other file.

*Rejected:*
- **A grant to the sample host's tests,** or **CA1515 off in the sample host:** a sample
  host is a consumer, and a consumer holds neither.
- **Starting the entry point by reflection:** the factory already does it, tested.

**Propagated to:**
- `06` PRT-ENG-001 (the sample host and the namespace rule) and PRT-ENG-009 (the test
  package); `04` PRT-LIB-001 (the sample host's test project's row).

## P-043 — The real `op`, checked on the owner's computer before each release

**Date:** 2026-11-05 · **Status:** accepted · **Amends:** PRT-ENG-008, PRT-ENG-011, P-005,
P-035

**TL;DR.** `build/check-real-op.ps1`, in place of `build/record-op-scenarios.ps1`, is run by
the owner on the owner's own computer before each release is tagged. It runs the pinned
`op` against the two sample vaults, records every recorded scenario afresh with P-035's
placeholders, and compares the files with those committed, each instant set aside. The
owner's decision (2026-11-05): a fake alone never proves the real `op` works, so a run
against the real one comes before each release; the fake stays for the pipeline.

- **Why the fake stays** in the pipeline: the pipeline would otherwise hold a live service
  account's token; the revoked, unreachable, rate-limited and wrong-version cases could not
  be produced at each run; and each run would spend the account's shared daily reads
  (P-006, P-028).
- **Why before each release:** the agent reads JSON whose shape `op` does not document
  (P-005). A run against the real `op` catches a change on 1Password's side that the
  pinned version does not, and keeps the fake what the real `op` answers.
- **The run.** It asks for the two service accounts' tokens without echo, as P-035's script
  did; the tool's commands go through the 1Password app; one command runs with the network
  disconnected. It writes the new files under `tmp/` first, removing all of them on a
  finding of P-035, and ends with one line: the `op` version, the commit checked out, and
  `same`, `changed` or `failed`. With `changed`, it writes the new files in place of the
  committed ones and runs the `integration` and `linux` tests on them
  (`build/gates/gate-tests.ps1`), whose result it prints. The written scenarios (P-028)
  are not run, and stay as they are.
- **The release.** A release's tag is pushed only after the owner's run on the release
  commit ended `same`, and the tag's message carries that line. `changed` holds the
  release: the new files come in a `test:` commit, with whatever the agent or the tool needs
  to read them, before a new release commit. 1.0.0-rc.1 was tagged before this entry.
- **What it needs on the owner's computer:** `op` 2.41.1, the 1Password app with its CLI
  integration on, PowerShell 7, and Docker for the `linux` tests. The vaults and service
  accounts of the first run stay for every later run.

*Rejected:*
- **The real `op` alone, run locally, with no fake:** the pipeline would no longer test the
  agent's fetch, the push or their failures, and a change that breaks them would merge.
- **The real `op` in the pipeline:** a live token on the platform, the shared read budget
  spent at each push, and the failure cases not producible there.
- **A recording made once** (P-035 as written): it ages unseen.

**Propagated to:**
- `06` PRT-ENG-008 (the scenarios) and PRT-ENG-011 (the release); the implementation plan
  (phase 7, and its inputs).
