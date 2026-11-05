# 05 — Operations

Configuration, the token, the deployment's part, health and status, and what the library
logs. Every value named `Portunus:...` is configuration (`07` section 4).

---

**PRT-OPS-001** — Every behaviour a deployment may want to change SHALL be configuration,
with the default and the rule `07` section 4 gives, validated before it is used, and
holding no secret.

**Values (P-008, P-016).** The client's and the adapters' keys are in the host's own
configuration, under `Portunus:Client`, `Portunus:Janus`, `Portunus:Hermes` and
`Portunus:Themis`; the agent's are in its file, `/etc/portunus/<instance>.json`, under
`Portunus:Agent`. Each key is checked on its raw value before binding, so a value outside
its rule stops the start naming the key as `07` spells it (the client, `04`
PRT-LIB-003; the agent, `01` PRT-AGT-002), and then bound through .NET's options, by the
configuration binder's source generator. Every key takes effect at the start. No key
holds a secret: the token is a file (PRT-OPS-002), and the secrets are the vault's.

The agent's file declares which process reads which secret, so it is guarded as a
secret's grant is: the agent refuses to start, with `secrets.agent.configurationunsafe`,
`details.key` `file`, where the file is not owned by root or can be written by its group
or by others, and with `details.key` the directory where a directory above it can.

*Source: P-007, P-008, P-016*

**Acceptance criteria**
1. Every key of `07` section 4 has its default when absent and stops the start when
   outside its rule.
2. An agent's file writable by its group stops the start with
   `secrets.agent.configurationunsafe`.

---

**PRT-OPS-002** — The service account's token SHALL be the one credential on the server's
disk, readable by the agent's service alone, and SHALL never be held in configuration,
an environment variable of the agent, an argument or a log.

**Values (P-010).** In `vault` mode the token is the file `/etc/portunus/<instance>.token`,
owned by root, mode `0400`, which the unit's drop-in hands the service as a systemd
credential (`LoadCredential=token:...`), so the agent reads it as the file `token` in the
directory `CREDENTIALS_DIRECTORY` names, the default of `Portunus:Agent:TokenFile`,
without holding a right to the file in `/etc/portunus/` itself. A deployment MAY instead seal the token with
`systemd-creds` to the server's TPM and hand it with `LoadCredentialEncrypted=`, so a
copied disk holds a token that opens on no other machine (P-010). The agent passes the
token to `op` alone, in that child's environment (`01` PRT-AGT-003). In `push` mode there
is no token at all (`02` PRT-PSH-005).

*Source: P-010*

---

**PRT-OPS-003** — The deployment SHALL meet what the library cannot do alone, and the
release notes SHALL state it.

**Values (P-002, P-005, P-007, P-010, P-015).**
- **No swap, no crash dumps.** The server keeps no active swap area (a compressed memory
  device included) and does not hibernate, since memory written out holds every secret
  the agent and the processes hold; the agent refuses to start with swap active (`01`
  PRT-AGT-001). No crash dump is written of any process that holds secrets: the agent's
  unit sets `LimitCORE=0` and the agent marks itself not dumpable; each container that
  reads secrets sets its core limit to 0 (Compose `ulimits: core: 0`), and .NET's own
  dumps are not turned on (`DOTNET_DbgEnableMiniDump` unset).
- **The agent.** The executable is installed at `/usr/local/bin/portunus-agent` from the
  release, its SHA-256 checked against the release's `SHA256SUMS`; the user and group
  `portunus` exist, with no login and no home; the unit `portunus-agent@.service` and, in
  `vault` mode, its drop-in `token.conf` are installed from the release (`06`
  PRT-ENG-001); the unit is enabled per instance and ordered before the container
  runtime's service, so each process's directory exists before a container mounts it
  (`01` PRT-AGT-006).
- **`op`.** In `vault` mode, `op` is installed at `Portunus:Agent:OpPath` at the version
  of `07` section 7, from 1Password's release archive for the server's architecture, its
  signature `op.sig` checked with `gpg --verify` against 1Password's signing key of `07`
  section 7; the agent refuses another version (`01` PRT-AGT-002).
- **The vault.** One vault per environment and one service account per vault with
  `read_items` on it alone (`03` PRT-VLT-001); the vault laid out as `03` gives, checked
  with `portunus check` before the agent first starts on it.
- **The quota.** Every service account of one 1Password account shares that account's
  daily request limit (`07` section 7, P-006): the deployment counts `3 + n` reads per
  agent start, `n` its vault's items, for every agent of every environment the account
  serves, so its starts in a day stay well inside it.
- **Processes.** Each process runs as the non-root user its declaration names, and its
  container mounts its own directory of the instance and nothing else of it
  (`01` PRT-AGT-006, PRT-AGT-007); its configuration names the socket's path inside the
  container (`Portunus:Client:Socket`); its start's wait (`Portunus:Client:WaitTimeout`)
  is within the host's own start timeout and the orchestrator's.
- **The push.** The owner's login on the server is a member of
  `Portunus:Agent:PushGroup`; the server's host key is in the owner's `known_hosts`
  before the first push, since the tool refuses an unknown one (`02` PRT-PSH-002).
- **Old versions.** A key version stays in the vault while any data or backup needs it,
  retired versions relabelled (`03` PRT-VLT-005); deleting one is the owner's, once no
  backup holds data under it.
- **Backups.** Data backups and the vault are never in the same place: no backup is kept
  in a vault, and no export of a vault beside the backups (`08` PRT-THR-003).

*Source: P-002, P-005, P-006, P-007, P-010, P-015*

**Acceptance criteria**
1. The release notes of each release (`RELEASE-NOTES.md`, `06` PRT-ENG-001) state every
   duty above (checked in review).
2. A test reads the unit and its drop-in and fails where one lacks a line of `07`
   section 7's unit settings.

---

**PRT-OPS-004** — The agent and the client SHALL each report their state where a health
check can read it, without a name of a secret or a value.

**Values (P-008).** The agent answers `status` (`01` PRT-AGT-005), and
`portunus-agent status <instance>` gives it to a server's monitoring by its exit code.
The client's health check `portunus`, registered by `AddPortunus`, asks `StatusAsync`:
`Healthy` where the agent is `ready`; `Degraded` where it is `waiting` or cannot be
reached, its data holding the reason; never `Unhealthy`, since a running process already
holds what it read, and an orchestrator that restarted it for the agent's state would
only make it wait. In the `file` source it reads `Healthy`.

*Source: P-008*

**Acceptance criteria**
1. The health check reads `Healthy`, `Degraded` with the reason, and `Degraded` with
   `unreachable`, as the fake agent answers `ready`, `waiting` or nothing.

---

**PRT-OPS-005** — The library SHALL log what it does without writing any value or the
token, and SHALL publish no metric.

**Values (P-018).** Log entries go through source-generated `LoggerMessage` methods. They
may name an instance, a vault's or an item's title, a field's label, an address, a
process's name, a user identifier, a state, a reason, a code, a count and an `op`
command's name and exit code; never a value, a token, a payload, or an `op` command's
standard output or standard error. The agent logs to the journal through the systemd
console format: its start and any refusal, each fetch attempt with the reads it may spend,
its outcome and the pause after it, the move to `ready`, each push taken or refused, each
read with its process, address and outcome, each connection refused by the peer check,
and its stop. The client logs its waits and each failure it answers; an adapter, each
secret it could not pass on (`04` PRT-LIB-006). The tool writes its outcome on standard
output and its refusals on standard error, never a value. The library publishes no meter:
the agent's events happen at starts alone, which the journal records (P-018).

*Source: P-018*

**Acceptance criteria**
1. Every integration test captures the log lines of the agent, the client, the adapters
   and the tool it runs, and fails where one holds a value or a token the test used
   (`06` PRT-ENG-012).
