# 08 — Threat model

What is worth protecting, who would go after it, which requirement stands in the way, and
what nothing here protects against. The point of the design is revocation: a server's
disk holds at most one credential, which the owner revokes in one action, so a server
copied or lost buys the owner time instead of handing over every key at once.

---

## 1. Assets

| Asset | Where it is | Protected by |
|---|---|---|
| The environment's secrets | The vault; the agent's memory; each process's memory, for what it read | 1Password's own protection; memory only, never swapped or dumped (`01` PRT-AGT-001; `05` PRT-OPS-003); each process reaching only its own declared addresses (`01` PRT-AGT-006, PRT-AGT-007) |
| The service account's token | The server's disk, in `vault` mode alone | Root-owned, handed to the service alone; held by the agent only until its fetch succeeds; revocable at once; MAY be sealed to the server's TPM (`05` PRT-OPS-002); absent in `push` mode (`02` PRT-PSH-005) |
| The declarations | The agent's file | Writable by root alone, which the agent checks (`05` PRT-OPS-001) |
| A set in transit | The local socket; the SSH session of a push | File permissions and the peer's identifier; SSH with a known host key (`01` PRT-AGT-006; `02` PRT-PSH-002) |
| The owner's access to the vaults | The owner's computer, their 1Password app | The app's biometric unlock, per terminal session, for each `op` the tool runs (P-009) |
| The vault's daily quota | The 1Password account | One fetch per agent start; growing pauses; a bounded restart rate (`01` PRT-AGT-003, PRT-AGT-004; `07` section 7) |

---

## 2. Threats and controls

**PRT-THR-001** — Each threat below SHALL be met by the control it names, and each
control SHALL have the test its requirement gives.

| Threat | Control |
|---|---|
| A server's disk is copied (a snapshot, a backup image, a disk taken away) and read for secrets | No secret is written to it (`01` PRT-AGT-001); it holds at most the token, which the owner revokes at once, or nothing in `push` mode; a sealed token opens on no other machine (`05` PRT-OPS-002); this is the checklist's first step (PRT-THR-003) |
| Memory reaches the disk through swap, hibernation or a crash dump | The agent refuses to start with swap active and marks itself not dumpable; the unit's core limit is 0; the deployment's duties cover every process (`01` PRT-AGT-001; `05` PRT-OPS-003) |
| A process, or a container taken over, reads secrets meant for another | One socket per process in a directory of its own, mounted into its container alone; the peer's user identifier checked; each read checked against the declaration (`01` PRT-AGT-006, PRT-AGT-007) |
| A local user who is not root reads the agent's memory | The agent is not dumpable, so no process of its user but root can attach to it or read its memory (`01` PRT-AGT-001) |
| A process asks for an address it was not granted, or pretends to be another | The declaration names its addresses; a connection from another user is closed (`01` PRT-AGT-006, PRT-AGT-007) |
| Someone changes a declaration to grant themselves secrets | The agent refuses a file or directory writable by anyone but root (`05` PRT-OPS-001) |
| A push reaches the wrong server, or one impersonating it | `ssh` with `StrictHostKeyChecking=yes`, the owner's own `known_hosts` (`02` PRT-PSH-002) |
| A push carries another environment's secrets | The agent takes a push only for its own vault's title (`02` PRT-PSH-003) |
| A push overwrites a ready agent's set by mistake | A push into a `ready` agent needs `replace` (`02` PRT-PSH-003); each push is logged with the pusher's user identifier |
| A push shows or leaves a secret on the owner's computer or the server | Nothing of a value on a screen, in a file, an argument or an environment; pinned arrays, zeroed (`02` PRT-PSH-002, PRT-PSH-003; `06` PRT-ENG-005) |
| The vault's quota is spent, so an agent cannot start | One fetch per start, by identifiers (`3 + n` reads); growing pauses, the cap at once on a rate limit; no retry of a broken layout; a bounded restart rate in the unit (`01` PRT-AGT-003, PRT-AGT-004; `07` section 7); a push needs none of it (`02` PRT-PSH-001) |
| A process fails at its start because the agent is not yet ready | It waits, within its bound, saying why (`04` PRT-LIB-004) |
| Another `op` than the one checked runs with the token | `op` installed from 1Password's signed archive at a root-owned path, its version checked at the start (`01` PRT-AGT-002; `05` PRT-OPS-003); the token in that child's environment alone |
| A log leaks a value or the token | Logs carry names, states and counts only, and never `op`'s output, checked in the gate (`05` PRT-OPS-005; `06` PRT-ENG-012) |
| A secret reaches the repository | The secret scan of the whole history before every push and in the pipeline (PRT-THR-004) |

*Source: P-002, P-006, P-007, P-009, P-010*

**Acceptance criteria**
1. Every control above that the library itself carries has a test carrying the
   identifier of the requirement that names it; the deployment's duties are `05`
   PRT-OPS-003's.

---

**PRT-THR-002** — The specification and the release notes SHALL state plainly what keeping
secrets in memory does not protect against.

**Values (P-010).** Memory protects secrets from a copied disk, never from someone already
inside the running server:
- **Root on the server** reads every process's memory, the agent's included, connects to
  every socket, reads the token file, and changes the declarations. Nothing in this
  library stands against root.
- **A process's own user** holds what that process read, in that process's memory, and
  can connect to that process's socket; the agent's checks limit it to that process's
  declared addresses, not to less.
- **The processes themselves** keep what they read in their own memory, as the sibling
  libraries' own rules say; this library cannot clear it.
- **The owner's computer**, while the 1Password app is unlocked, is open to a process
  with administrator rights on it, as 1Password states for its CLI's integration; a push
  is as safe as that computer.
- **A token copied and used before the owner revokes it** reads the whole vault. Revoking
  it stops further reads; what was read stays read (PRT-THR-003).

*Source: P-010*

---

**PRT-THR-003** — The release SHALL ship a checklist for a compromise, and the
deployment's rule on backups SHALL keep the vault and the data backups apart.

**Values (P-010).** `docs/guide/compromise-checklist.md`, which the release notes name and
attach: revoke the token first; read the service account's item usage report to learn
whether the token read the vault after the moment of the compromise; rotate the outside
providers' keys and the database logins; what the data keys mean, since a key read with a
copy of the data opens that copy whatever is rotated after; and how the agent goes on, by
a push or a new service account. Backups are not this library's, but its rule is: data
backups and the vault are never in the same place, so one breach never yields both the
data and its keys (`03` PRT-VLT-006; `05` PRT-OPS-003).

*Source: P-010*

**Acceptance criteria**
1. The release attaches the checklist, and its notes name it (checked in review).

---

**PRT-THR-004** — The repository's whole history SHALL be scanned for secrets before every
push, by the implementer (the first push included), and by the pipeline on every pull
request, every push to the default branch and every release tag (`06` PRT-ENG-012), and a
finding SHALL stop the push it comes before and block the merge; no finding is silenced
but by gitleaks' default configuration (`06` PRT-ENG-001). The repository may be public,
so nothing that reaches it is private.

*Source: P-016*

**Acceptance criteria**
1. A commit carrying a credential-shaped string fails the scan; the test builds that
   string at run time, in a repository it makes under `tmp/`, so no such string is ever
   written in the repository's own history.
2. The secret-scanning gate's script fails on the same string on a line that carries
   gitleaks' inline allow marker, and where a `.gitleaksignore` is tracked or sits at the
   scanned repository's root, whatever it lists.

---

**PRT-THR-005** — The agent SHALL take its declarations as the deployment's statement of
which process may read which secret, and SHALL decide only by them.

**Values (P-007).** Which user a container runs as, which directory is mounted into which
container, and who belongs to the push group are the deployment's to set, as root does. A
deployment that runs two processes as one user and mounts one directory into both has
told the agent they are one process.

*Source: P-007*
