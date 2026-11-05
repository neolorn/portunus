# 01 — The key agent

The long-running service on each server that holds one environment's secrets: how it
starts, how it fetches the set from the vault once, how it keeps it in memory only, how
each process reaches it and reads no more than its declaration names, how a process waits
while the agent has nothing yet, and how the agent reports its state. Every value named
`Portunus:Agent:...` is the agent's configuration (`07` section 4); every limit is `07`
section 5; the wire formats are `07` section 6.

---

## 1. One agent, in memory only

**PRT-AGT-001** — The agent SHALL hold every secret in memory only, and SHALL never write
one to a file, a log, a temporary file, a pipe it does not own or a process's arguments.

**Values (P-002).** Each value is held in a byte array allocated pinned
(`GC.AllocateArray<byte>(length, pinned: true)`), so the collector never leaves a copy
behind when it moves objects; it never becomes a `string`, save the token's one
conversion of PRT-AGT-004; and it is overwritten with zeros
(`CryptographicOperations.ZeroMemory`) when the set that holds it is replaced, once no
read under way still uses it, and when the agent stops. Every buffer a value passes
through (the output of `op`, a push's payload, an answer) is pinned and zeroed the same
way once used. At its start, before it reads anything, the agent:
1. marks itself not dumpable (`prctl(PR_SET_DUMPABLE, 0)`, through a source-generated
   `LibraryImport`), so no core dump of it is written and no process of its own user but
   root can read its memory;
2. reads `/proc/swaps` and refuses to start, with `secrets.agent.swapactive`, where any
   swap area is active, a compressed memory device included.

The host's own duties (no swap, no crash dumps, no hibernation) are `05` PRT-OPS-003.

*Source: P-002*

**Acceptance criteria**
1. A start with an active swap area listed in the `/proc/swaps` the test gives exits 1
   with `secrets.agent.swapactive`, and reads nothing.
2. In a running agent, `prctl(PR_GET_DUMPABLE)` answers 0.
3. After a set is replaced and after the agent stops, the arrays of the set before hold
   zeros (a test reads them through an internal hook of the set's holder).
4. A test that runs a fetch, a push and reads, and then searches every file the agent's
   user can write under the test's directories and every log line captured, finds no
   value the test used.

---

**PRT-AGT-002** — An agent SHALL serve one environment on one server, named by its
instance, and SHALL check its configuration and its declarations before it fetches or
listens.

**Values (P-003, P-015).** `portunus-agent run <instance>` (the systemd unit's command,
`05` PRT-OPS-003) reads its configuration from `/etc/portunus/<instance>.json` and the
section `Portunus:Agent` of it (`07` section 4). The configuration holds no secret: the
vault's title, the source (`vault`, or `push` for the push-only mode of `02`
PRT-PSH-005), the declarations, the paths and the pauses. Each key is checked on its raw
value before binding, as `05` PRT-OPS-001 gives. The start is refused, nothing fetched and
no socket made, by writing one JSON line `{ "code": "<code>", "details": { ... } }` on
standard error and exiting 1, where:
- the file, or a directory above it, can be written by anyone but root:
  `secrets.agent.configurationunsafe`, `details.key` (`05` PRT-OPS-001);
- a key is outside its rule: `secrets.agent.configurationinvalid`, `details.key`;
- a declaration is outside PRT-AGT-007's rule: `secrets.agent.declarationinvalid`,
  `details.process` and `details.member`;
- swap is active: `secrets.agent.swapactive` (PRT-AGT-001);
- in `vault` mode, `op --version` does not answer the pinned version of `07` section 7:
  `secrets.agent.opversion`, `details.found` the version answered, or empty where `op`
  does not run;
- in `vault` mode, the token file cannot be read, is empty or is over 4 KiB:
  `secrets.agent.tokenunavailable`.

Two instances MAY run on one server, each with its own configuration, vault, sockets and
unit (`portunus-agent@<instance>.service`).

*Source: P-002, P-003, P-005, P-015*

**Acceptance criteria**
1. Each refusal above exits 1 with its code and details on one line of standard error,
   makes no socket and starts no `op` but `op --version`.
2. A start in `push` mode reads no token file and runs no `op`.

---

## 2. The fetch

**PRT-AGT-003** — In `vault` mode the agent SHALL fetch the environment's set from the
vault once, at its start, through the pinned `op` and the service account's token, and
SHALL never fetch again while it runs.

**Values (P-004, P-005, P-006).** A fetch runs these commands, in this order, each with
`--format json` and `--config` naming a directory under the agent's runtime directory, in
an environment holding nothing but `OP_SERVICE_ACCOUNT_TOKEN` (the token), `OP_CACHE`
`false` and `HOME` the same directory, so no `OP_CONNECT_*` variable reaches it and `op`
starts no caching daemon:
1. `op item list --vault <vault title>`: the items, each with its identifier and title,
   and the vault's identifier;
2. for each item, `op item get <item identifier> --vault <vault identifier> --reveal`:
   its fields.

With the vault and each item named by identifier after the first command, a fetch of a
vault of `n` items makes `3 + n` read requests of the service account's quota (P-006).
Each command is bounded by `Portunus:Agent:OpTimeout`, its whole process tree killed
when it passes it. Its standard output is read as bytes from the base stream, never
through a text reader, into a pinned buffer of at most 4 MiB; its standard error is read
for its class alone (PRT-AGT-004) and never logged. Of the list's JSON the agent reads
each entry's `id`, `title` and `vault.id`; of an item's, `id`, `title` and each field's
`label` and `value`, the field labelled `notesPlain` being the item's note and never a
secret (P-011). Values are copied from the JSON as their UTF-8 bytes
(`Utf8JsonReader.CopyString`), never as strings. The whole set is then checked against
`03` PRT-VLT-001 to PRT-VLT-006: where it passes, it becomes the agent's set at once,
whole; where it fails, nothing of it is kept.

The token is read from `Portunus:Agent:TokenFile` into a pinned array at the start,
converted to a `string` only to set the child process's environment, as .NET's process
API requires, and zeroed with the array, and dropped, once a fetch succeeds or a push
fills the agent (`02` PRT-PSH-003). A running agent holds no token after that, and a
restart is the only way it fetches again.

*Source: P-004, P-005, P-006, P-011*

**Acceptance criteria**
1. With the fake `op` (`06` PRT-ENG-008) holding a vault of four valid items, the agent
   runs exactly one list and four gets, each with the arguments and environment above,
   and reports `ready` with the four items.
2. A vault of which one item breaks a rule of `03` leaves the agent `waiting` with the
   reason `layoutinvalid`, holding nothing of the vault.
3. After a fetch succeeds the agent runs no further `op` command, whatever the time that
   passes, and holds no token.
4. An `op` that passes `Portunus:Agent:OpTimeout` is killed, and the attempt counts as
   failed with the reason `unreachable`.

---

**PRT-AGT-004** — A fetch that fails SHALL be tried again after a pause that grows with
each failure, never in a fast loop, and a rate limit SHALL lengthen the pause at once.

**Values (P-006).** The first pause is `Portunus:Agent:Retry:First`; each later one is
twice the one before, at most `Portunus:Agent:Retry:Max`; each is moved by a random
amount of up to a fifth of itself either way, so several agents restarted together do
not ask together. The failed attempt's class gives the agent's reason while it waits
(`07` section 2): standard error holding `(429)` is `ratelimited`, and the next pause is
`Portunus:Agent:Retry:Max` at once; `(401)` or `(403)` is `refused` (a token revoked or
not granted the vault); any other failure, an `op` timeout included, is `unreachable`. A
fetch whose set breaks the layout (`layoutinvalid`) is not tried again: the vault will not
mend itself, and another try would only spend the quota; the agent waits for a push or a
restart. A push that fills the agent ends the retries (`02` PRT-PSH-003).

*Source: P-006*

**Acceptance criteria**
1. With the fake `op` failing, the attempts start at 0, `First`, then `First` plus
   `2 × First` and so on, each within a fifth of its pause, never closer than `First`,
   and every pause after the cap is within a fifth of `Max` (a test clock).
2. A failure whose standard error holds `(429)` makes the next pause `Max`, and the
   status reads `ratelimited`.
3. A set that breaks the layout is fetched once and never again.

---

## 3. States and status

**PRT-AGT-005** — The agent SHALL be either `waiting`, holding no set, with a reason, or
`ready`, holding one, and SHALL answer its state to any connection that asks.

**Values (P-008).** A `vault` agent starts `waiting` with the reason `fetching`, and
moves to the reason of each failed attempt between attempts; a `push` agent starts
`waiting` with `awaitingpush`. It moves to `ready` when a fetch or a push gives it a set,
and never back to `waiting` while it runs. A `status` request (`07` section 6), on any of
its sockets, answers the state, the reason while `waiting`, the instant the state began,
and while `ready` the set's source (`vault` or `push`), the instant it was loaded and its
number of items; never a title, a field name or a value. `portunus-agent status
<instance>` asks it on the push socket and writes the answer as one JSON line on
standard output, exiting 0 where the agent is `ready`, 3 where it is `waiting`, and 1
where it cannot reach it (a refusal line on standard error), for a health check or an
operator.

*Source: P-008*

**Acceptance criteria**
1. A `status` request answers `waiting` with `fetching` during a fetch, the failure's
   reason between attempts, and `ready` with the source, instant and item count after.
2. `portunus-agent status` exits 0, 3 and 1 in the three cases.

---

## 4. Sockets, declarations and reads

**PRT-AGT-006** — The agent SHALL listen on one Unix domain socket per declared process,
in a directory of that process's own, and on one push socket, each guarded by file
permissions and, for a process's socket, by the peer's user identifier.

**Values (P-007).** Under `Portunus:Agent:Root` (`07` section 4), the instance's
directory holds:
- for each declared process, the directory `<process>/`, owned by the agent's user, its
  group the process's declared group, mode `0710`, holding `agent.sock`, its group the
  same, mode `0660`;
- the directory `push/`, its group `Portunus:Agent:PushGroup`, mode `0710`, holding
  `agent.sock`, mode `0660` (`02` PRT-PSH-002).

The agent creates a directory that is missing and corrects the owner, group and mode of
one that exists, which a container runtime may have made first, and never deletes one, so
a directory a container has mounted stays the one the container sees across the agent's
restarts; it removes a stale `agent.sock` it finds and binds a new one, and removes its
own sockets when it stops. A process reaches its socket by having its directory, and
nothing else of the instance, mounted into its container, or by its path on the host. On
every connection to a process's socket the agent reads the peer's credentials
(`SO_PEERCRED`) and closes the connection without an answer where the peer's user
identifier is not the declared one, logging the refusal with the process's name and the
peer's identifier.

*Source: P-007*

**Acceptance criteria**
1. After a start, each declared process's directory and socket have the owner, group and
   mode above, and a directory found with other ones is corrected.
2. A connection to a process's socket from a user other than the declared one is closed
   without an answer and logged.
3. A restart of the agent leaves each directory in place, by inode, and a client holding
   the directory reaches the new socket.

---

**PRT-AGT-007** — Each process SHALL read only the addresses its declaration names.

**Values (P-007).** A declaration, under `Portunus:Agent:Processes:<process>`, holds the
process's name (the key, a name of `00` section 1.5, never `push`), its `Uid` and `Gid`
(1 to 4,294,967,294, never 0, so no root process is declared), and its `Secrets`, 1 to 256
addresses (`00` section 1.5), no two alike. An address names a single secret or a set of
versions by its base name, never a version's own field (`03` PRT-VLT-004); a set's
address covers every version of it and its current marker, never a field the layout marks
retired (`03` PRT-VLT-005). The instance holds 1 to 64 declarations. Two processes SHOULD
run as different users, so the peer check tells them apart; the directory each mounts
does in any case. A declaration names addresses, not values, and is not a secret.

*Source: P-007*

**Acceptance criteria**
1. Each rule above, broken, refuses the start with `secrets.agent.declarationinvalid`
   naming the process and the member.
2. A read of an address its declaration does not name is answered
   `secrets.secret.denied`, whatever the agent's state and whether or not the set holds
   it.

---

**PRT-AGT-008** — A read SHALL answer the secret at its address while the agent is
`ready`, and SHALL answer `waiting`, at once, while it is not.

**Values (P-008).** A connection carries one request and one answer (`07` section 6): the
agent reads at most 4 KiB, until the peer shuts down its sending side, within 5 seconds,
or closes the connection without an answer; it then answers and closes. A `read`, its
address named by the declaration, answers:
- while `waiting`: the state and its reason, so the client waits (`04` PRT-LIB-004);
- while `ready`, where the set holds the address: the secret, its current version and
  each version's value in base64url (P-023; a single value is version 1, current 1);
- while `ready`, where it does not: `secrets.secret.unknown`.

A request outside `07` section 6, or of a kind the socket does not take (a `push` on a
process's socket, a `read` on the push socket), answers `secrets.request.invalid`; one
of another protocol version, `secrets.request.version`. At most 64 connections are served
at once; one beyond them is closed without an answer. Each read is logged with the
process's name, the address and the outcome, never a value (`05` PRT-OPS-005).

*Source: P-007, P-008*

**Acceptance criteria**
1. A declared read while `waiting` answers `waiting` and its reason without delay.
2. A declared read while `ready` answers the value bytes the vault holds, for a single
   secret and for a set of three versions with its current one.
3. A request of 4 KiB and one byte, and one not finished within 5 seconds, are closed
   without an answer.
4. A `push` request on a process's socket answers `secrets.request.invalid`.

---

**PRT-AGT-009** — The agent SHALL stop cleanly on the service manager's signal, leaving
no secret behind it in memory it still owns.

**Values (P-002).** On `SIGTERM` the agent stops accepting connections, lets the reads
under way finish within 5 seconds, removes its sockets, zeroes the set and any token it
still holds, and exits 0. A process that already read its secrets keeps them: the
siblings read once at their start (P-013), so the agent's restart costs a running
process nothing, and a process that starts while the agent is down waits (`04`
PRT-LIB-004).

*Source: P-002, P-013*

**Acceptance criteria**
1. After `SIGTERM` the agent exits 0 within 10 seconds, its sockets removed and its set
   zeroed (PRT-AGT-001 criterion 3).
