# 02 — The push

The manual way in: the owner reads an environment's secrets through their own 1Password
app on their own computer and sends them over SSH straight into a running agent's memory.
Nothing is written to the server's disk and nothing is shown on screen. The same tool
checks a vault's layout and reads an agent's status. Every limit is `07` section 5; the
wire formats are `07` section 6.

---

## 1. When a push is used

**PRT-PSH-001** — A push SHALL fill or replace a running agent's set in each of these
cases, with no change to the agent's configuration but where the last one says:

| Case | What the agent is doing | What the owner does |
|---|---|---|
| The vault is unreachable from the server | `waiting`, retrying its fetch (`01` PRT-AGT-004) | Pushes; the agent stops retrying and is `ready` |
| The token was revoked after a compromise (`08` PRT-THR-003) | `waiting` with `refused`, or `ready` from an earlier fetch | Pushes into a `waiting` agent, or into a restarted one, until a new token is in place |
| The deployment keeps no token on the server at all | `waiting` with `awaitingpush` after every start | Pushes after every start of the agent (PRT-PSH-005) |
| A new version must reach a running agent without a fetch (`03` PRT-ROT-001) | `ready` | Pushes with `--replace` |

A push reads as the owner's own 1Password account, so it spends nothing of the service
account's quota (P-006, P-009).

*Source: P-009*

---

## 2. Reading and sending

**PRT-PSH-002** — `portunus push` SHALL read the vault through the owner's own 1Password
app, unlocked by the owner, and SHALL send the set over SSH without writing it to a disk
or showing it.

**Values (P-009).** `portunus push --vault <vault title> --to <ssh destination>
--instance <instance> [--account <account>] [--replace]`. The command:
1. runs `op --version`, refusing with `secrets.cli.opversion`, `details.found`, a version
   below the pinned one of `07` section 7 or of another major version;
2. runs `op item list --vault <vault title> --format json`, then for each item
   `op item get <item identifier> --vault <vault identifier> --format json --reveal`,
   each with `--account` where the option is given, in its own environment less
   `OP_SERVICE_ACCOUNT_TOKEN`, `OP_CONNECT_HOST`, `OP_CONNECT_TOKEN` and every
   `OP_SESSION_` variable, so `op` signs in through the desktop app and its biometric
   prompt (Touch ID, Windows Hello, or the system's authentication on Linux), never with a
   token; a command that fails refuses with `secrets.cli.opfailed`, `details.step`
   `list` or `get`;
3. reads each command's output as bytes into pinned arrays, as the agent does (`01`
   PRT-AGT-003), and checks the set against `03` PRT-VLT-001 to PRT-VLT-006, refusing with
   the first finding's code of `07` section 1, `details.item` and `details.field` (names,
   never values), and sending nothing;
4. runs the system's `ssh` (found on the `PATH`; `secrets.cli.sshmissing` where none is)
   as `ssh -T -o StrictHostKeyChecking=yes -- <ssh destination> portunus-agent receive
   <instance>`, writes the push payload (`07` section 6) to its standard input, closes it,
   and reads the agent's answer from its standard output; `ssh`'s own prompts (a key's
   passphrase, the owner's security key) reach the owner's terminal through its own
   terminal handling, never through the payload;
5. zeroes every array and writes one JSON line on standard output: the agent's answer
   (accepted, and the number of items) or the refusal, exiting 0 or 1.

Nothing of a value reaches the screen, a file, an argument or an environment variable on
either computer. `--vault` and `--instance` are checked against their rules (`00`
section 1.5; `03` PRT-VLT-001) before anything runs, `secrets.cli.inputinvalid`,
`details.member`, since the instance is part of the remote command.

*Source: P-005, P-009*

**Acceptance criteria**
1. With the fake `op` and the fake `ssh` (`06` PRT-ENG-008), a push runs the commands
   above with those arguments and that environment, and the fake agent receives the
   payload holding every field of the vault.
2. A vault breaking a rule of `03` is refused naming the item and the field, and `ssh`
   never runs.
3. Nothing the push writes on standard output or standard error, and no file under the
   test's directories, holds a value the test used.
4. An `op` below the pinned version is refused with `secrets.cli.opversion`.

---

**PRT-PSH-003** — `portunus-agent receive` SHALL carry the payload from its standard
input to the agent's push socket in memory, and the agent SHALL take a push only for its
own vault, only into a `waiting` agent unless the push says to replace, and only whole.

**Values (P-009).** `portunus-agent receive <instance>`, run by the SSH session as the
owner's login on the server, which is a member of `Portunus:Agent:PushGroup`, reads its
standard input to its end, at most 2 MiB (more refuses with `secrets.push.toolarge`), into
a pinned array, sends it on the instance's push socket (`01` PRT-AGT-006), writes the
agent's answer as one line on standard output, zeroes the array and exits 0 where the
agent took the push and 1 otherwise; where it cannot reach the socket it writes
`secrets.push.unreachable` on standard error and exits 1. The agent answers a push:
- `secrets.push.wrongvault` where the payload's vault title is not
  `Portunus:Agent:Vault`, compared as `03` PRT-VLT-001 compares titles;
- `secrets.push.ready` where it is `ready` and the payload does not set `replace`;
- `secrets.push.layoutinvalid`, `details.item` and `details.field`, where the set breaks
  a rule of `03`, which the agent checks again whatever the tool checked;
- otherwise, it takes the set whole, at once, as its fetch would (`01` PRT-AGT-003),
  ends any retries, zeroes and drops any token it holds, moves to `ready` with the source
  `push`, and answers accepted with the number of items.

Each push, taken or refused, is logged with the peer's user identifier, the outcome and
the number of items, never a value (`05` PRT-OPS-005).

*Source: P-009*

**Acceptance criteria**
1. A push into a `waiting` agent makes it `ready` with the source `push`, and a read
   answers the pushed value.
2. A push into a `ready` agent without `replace` is refused with `secrets.push.ready`
   and changes nothing; with it, the set is replaced and a read answers the new value.
3. A push carrying another vault's title is refused with `secrets.push.wrongvault`.
4. A push into a `vault` agent waiting between attempts ends its attempts: the fake `op`
   is not run again.
5. A payload over 2 MiB is refused by `receive` with `secrets.push.toolarge`, and the
   agent receives nothing.

---

**PRT-PSH-004** — The tool SHALL check a vault's layout and read an agent's status, each
without showing a value.

**Values (P-009, P-011).** `portunus check --vault <vault title> [--account <account>]`
reads the vault as a push does (PRT-PSH-002 steps 1 to 3) and writes each finding of `03`
PRT-VLT-007 as one JSON line `{ "code", "item", "field" }`, the warnings included, and
exits 1 where any finding is an error and 0 otherwise. `portunus status --to <ssh
destination> --instance <instance>` runs `portunus-agent status <instance>` over `ssh` as
a push does and writes its answer, exiting as it does (`01` PRT-AGT-005).

*Source: P-009, P-011*

**Acceptance criteria**
1. `check` on a vault with one error and one warning writes both findings and exits 1;
   on one with a warning alone, exits 0.
2. `status` exits 0, 3 and 1 as the agent's status command does.

---

## 3. The push-only mode

**PRT-PSH-005** — A deployment SHALL be able to keep no token on the server at all: an
agent whose `Portunus:Agent:Source` is `push` SHALL never read a token or run `op`, and
SHALL wait for a push after every start.

**Values (P-009, P-010).** Such an agent starts `waiting` with `awaitingpush` (`01`
PRT-AGT-005), and the processes that start meanwhile wait within their bound (`04`
PRT-LIB-004). A copied disk of such a server holds no credential to the vault at all; the
price is that every start of the agent, a reboot included, needs the owner. It spends
nothing of the service account's quota. Moving between the modes is a change of the
configuration and a restart of the agent.

*Source: P-009, P-010*

**Acceptance criteria**
1. An agent in `push` mode with no token file starts, runs no `op`, reads `waiting` with
   `awaitingpush`, and is `ready` after a push.
