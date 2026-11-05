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

