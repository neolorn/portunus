# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `SecretAddress`, `Secret`, `AgentStatus`, `Result`, `Result<T>`, `Failure`, `SecretsStartupException`, `ISecretClient`, `AgentState`, `WaitReason` and `SetSource`, the public types of `Portunus.Core`.
- The layout's check of a set, every finding in one pass, ordered by item and field (`03` PRT-VLT-007).
- The wire formats of the requests, the answers and the push, each one exact form, written into and read from pinned arrays (`06` PRT-ENG-006).
- Values in the wire formats in base64url, unpadded, so JSON never escapes them, and a value's escaped string refused (`06` PRT-ENG-005, P-023).
- The layout's check of a live and a retired version of one number, `secrets.layout.fieldduplicate`; a secret holding only retired versions passes (`03` PRT-VLT-005, P-024).
- The secret a set holds at an address, its live versions and its current one, a secret holding only retired versions none (`03` PRT-VLT-004, PRT-VLT-005).
- The development file's reader, each value a field's text read into a pinned array (`03` PRT-VLT-008).
- The client's configuration under `Portunus:Client`, each key checked on its raw value and a value outside its rule refused naming the key (`05` PRT-OPS-001).
- The client's reads from the agent, each waiting while the agent waits or cannot be reached, within `Portunus:Client:WaitTimeout`, and its status (`04` PRT-LIB-004).
- The development file's client of a Development host, refused in any other host, and the client made once from the host's configuration (`04` PRT-LIB-003, PRT-LIB-005).
- The client's health check, `Healthy` while the agent is `ready` and `Degraded` with the reason while it waits or cannot be reached (`05` PRT-OPS-004).
- `FixedSecretClient`, a client a host's tests set, answering what is set, denied or waiting, and keeping every address asked (`04` PRT-LIB-008).
- `AddPortunus`, the one call a host registers the client with: the client made as the host starts, and the health check `portunus` (`04` PRT-LIB-003, `05` PRT-OPS-004).
- `AddPortunusTesting`, which puts one `FixedSecretClient` in place of the client, in either order with `AddPortunus` (`04` PRT-LIB-008).
- `portunus-agent`, the key agent: its start's checks, its configuration and declarations, a socket per declared process and the push socket, its states and status, the fetch through `op` with its pauses, and its stop (`01`; `05` PRT-OPS-001, PRT-OPS-002).
- A connection to a process's socket whose peer's credentials cannot be read, closed without an answer and logged with the peer unknown (`01` PRT-AGT-006, P-032).
- The token read less one final line ending, `\n` or `\r\n`, and a file holding a line ending alone refused as empty (`01` PRT-AGT-002, PRT-AGT-003, P-031).
- `portunus-agent receive`, which carries a push from its standard input to the agent's push socket in memory, at most 2 MiB, and writes the agent's answer (`02` PRT-PSH-003).
- The push's taking: only the agent's own vault, only into a waiting agent unless it says to replace, only a set that passes the layout, and then the fetch's attempts ended and the token dropped (`02` PRT-PSH-003, PRT-PSH-005).
- `portunus`, the .NET tool: `push`, which reads a vault through the owner's own 1Password app, checks its layout and sends it over `ssh` to `portunus-agent receive`; `check`, which writes each finding of a vault's layout; and `status`, which reads an agent's status over `ssh` (`02` PRT-PSH-002, PRT-PSH-004).
- The tool finds `op` and `ssh` only in a `PATH` folder that is an absolute path, never in one that names the working directory (`02` PRT-PSH-002).
- A cancelled `op` or `ssh` command of the tool killed with every process it started (`02` PRT-PSH-002).
- `Portunus.Hermes`, Hermes's `ISecretSource` over the client: each name at its address, the table's base64 names decoded, its text names and any name it does not list passed as UTF-8 text, every live version in ascending order (`04` PRT-LIB-006, `03` PRT-ROT-002, P-037).
