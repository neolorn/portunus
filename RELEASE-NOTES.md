# Release notes

## 1.0.0-rc.1

The first release candidate of 1.0.0. It is the first release, so it changes and removes
nothing since a release before it. Until a final release, a release candidate may change or
remove anything; from the first final release on, a release that changes or removes a
public member, a code, a configuration key, a value of an enumeration, a member of a wire
format, or a command or an option of the two executables is a major release.

### What it ships

- **The packages**, each at this version and each depending on the others it uses at
  exactly this version: `Portunus.Core`, `Portunus`, `Portunus.Janus`, `Portunus.Hermes`,
  `Portunus.Themis`, `Portunus.Testing` and `Portunus.Cli`, the .NET tool `portunus`.
- **The agent**, `portunus-agent`, self-contained and single-file, as
  `portunus-agent-linux-x64` and `portunus-agent-linux-arm64`.
- **The agent's unit files**: `portunus-agent@.service`, its drop-in `token.conf` for
  `vault` mode, and `example.json`, an agent's configuration for an invented environment.
- **`SHA256SUMS`**, the SHA-256 of the two executables and the three unit files.
- **`compromise-checklist.md`**, what the owner does when a server is, or may be,
  compromised.

No package is published to a feed: a host takes the packages from this release.

### The siblings' versions

Each adapter is built against its sibling's package as released, with no change to it, and
depends on it at exactly this version, so a host's restore reports a mix of versions:

- `Portunus.Janus`: `Janus.Hosting` 1.0.0-rc.4, which carries `Janus.Core`.
- `Portunus.Hermes`: `Hermes.Core` 1.0.0-rc.6.
- `Portunus.Themis`: `Themis.Core` 1.0.0-rc.2.

A move of a sibling's version is a release of this library.

### What the deployment does

The library cannot do these alone; the deployment does them.

- **No swap, no crash dumps.** The server keeps no active swap area, a compressed memory
  device included, and does not hibernate: memory written out holds every secret the agent
  and the processes hold. The agent refuses to start with swap active. No crash dump is
  written of any process that holds secrets: the agent's unit sets `LimitCORE=0` and the
  agent marks itself not dumpable; each container that reads secrets sets its core limit
  to 0 (Compose `ulimits: core: 0`); and .NET's own dumps are not turned on
  (`DOTNET_DbgEnableMiniDump` unset).
- **The agent.** The executable is installed at `/usr/local/bin/portunus-agent` from this
  release, its SHA-256 checked against `SHA256SUMS`. The user and group `portunus` exist,
  with no login and no home. The unit `portunus-agent@.service` and, in `vault` mode, its
  drop-in `token.conf` are installed from this release. The unit is enabled per instance
  and ordered before the container runtime's service, so each process's directory exists
  before a container mounts it. Its configuration is `/etc/portunus/<instance>.json`,
  writable by root alone, as is every directory above it; the agent refuses it otherwise.
- **The token.** In `vault` mode the service account's token is the file
  `/etc/portunus/<instance>.token`, owned by root, mode `0400`, which `token.conf` hands
  the service as a systemd credential. It is the one credential on the server's disk. A
  deployment may instead seal it with `systemd-creds` to the server's TPM and hand it with
  `LoadCredentialEncrypted=`, so a copied disk holds a token that opens on no other
  machine. In `push` mode there is no token.
- **`op`.** In `vault` mode, `op` 2.41.1 is installed at `Portunus:Agent:OpPath` (by
  default `/usr/local/bin/op`) from 1Password's release archive for the server's
  architecture,
  `https://cache.agilebits.com/dist/1P/op2/pkg/v2.41.1/op_linux_<arch>_v2.41.1.zip`,
  `<arch>` `amd64` or `arm64`, its signature `op.sig` checked with `gpg --verify` against
  1Password's signing key `3FEF9748469ADBE15DA7CA80AC2D62742012EA22`. The agent refuses
  another version.
- **The vault.** One vault per environment, titled `<Project> <Environment>`, and one
  service account per vault with `read_items` on it alone. The vault is laid out as the
  library's rule gives, checked with `portunus check` before the agent first starts on it.
- **The quota.** Every service account of one 1Password account shares that account's
  daily request limit, 1,000 requests a day, reads and writes together, on the plan as
  1Password documented it on 2026-11-05. The deployment counts `3 + n` reads per agent
  start, `n` its vault's items, for every agent of every environment the account serves,
  so its starts in a day stay well inside it.
- **Processes.** Each process runs as the non-root user its declaration names, and its
  container mounts its own directory of the instance and nothing else of it. Its
  configuration names the socket's path inside the container (`Portunus:Client:Socket`),
  and its start's wait (`Portunus:Client:WaitTimeout`) is within the host's own start
  timeout and the orchestrator's. The agent takes its declarations as the deployment's
  statement of which process may read which secret, and decides by them alone: which user
  a container runs as, which directory is mounted into which container, and who belongs to
  the push group are the deployment's to set. Two processes run as one user with one
  directory mounted into both are, to the agent, one process.
- **The push.** The owner's login on the server is a member of `Portunus:Agent:PushGroup`,
  and the server's host key is in the owner's `known_hosts` before the first push, since
  the tool refuses an unknown one.
- **Old versions.** A key version stays in the vault while any data or backup needs it,
  retired versions relabelled `.<n>.retired`. Deleting one is the owner's, once no backup
  holds data under it.
- **Backups.** Data backups and the vault are never in the same place: no backup is kept
  in a vault, and no export of a vault beside the backups, so one breach never yields both
  the data and its keys.

### What keeping secrets in memory does not protect against

Memory protects secrets from a copied disk, never from someone already inside the running
server.

- **Root on the server** reads every process's memory, the agent's included, connects to
  every socket, reads the token file, and changes the declarations. Nothing in this
  library stands against root.
- **A process's own user** holds what that process read, in that process's memory, and can
  connect to that process's socket. The agent's checks limit it to that process's declared
  addresses, not to less.
- **The processes themselves** keep what they read in their own memory, as the sibling
  libraries' own rules say; this library cannot clear it.
- **Copies outside the library's reach**: what `op`'s own process holds, what the kernel
  holds in a pipe or a socket's buffer, and what the runtime may hold while it unescapes a
  field's text from `op`'s output. None is on disk, and each is gone when that memory is
  reused.
- **The owner's computer**, while the 1Password app is unlocked, is open to a process with
  administrator rights on it, as 1Password states for its command line tool's integration;
  a push is as safe as that computer.
- **A token copied and used before the owner revokes it** reads the whole vault. Revoking
  it stops further reads; what was read stays read.

### When a server is, or may be, compromised

Follow `compromise-checklist.md`, attached to this release: revoke the token first; read
the service account's item usage report to learn whether the token read the vault after
the moment of the compromise; rotate the outside providers' keys and the database logins;
weigh what the data keys mean, since a key read with a copy of the data opens that copy
whatever is rotated after; and bring the agent back by a push or a new service account.
