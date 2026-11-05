# 07 — Reference

Authoritative for every name, code, key, default and limit. Where another chapter names
one, this chapter's spelling and value govern.

---

## 1. Codes

A failure carries one code and its details (`04` PRT-LIB-002). A "(startup)" code is thrown
in a `SecretsStartupException` while the host starts; an "(agent)" code is written by
`portunus-agent run` as it refuses to start (`01` PRT-AGT-002); a "(tool)" code is written
by `portunus` or `portunus-agent receive` on standard error; a "(layout)" code is a
finding of the layout validator (`03` PRT-VLT-007), answered by the agent and the tool as
the code of a refused set and listed by `portunus check`; every other code is answered by
the client or the agent as a failure. A fault of either executable is written as
`secrets.fault` with no details (`06` PRT-ENG-003).

| Code | Details | Answered where |
|---|---|---|
| `secrets.startup.configurationinvalid` (startup) | `key` | A client's or an adapter's configuration value outside its rule (`04` PRT-LIB-003) |
| `secrets.startup.notdevelopment` (startup) | | The `file` source in a host that is not Development (`04` PRT-LIB-003) |
| `secrets.startup.fileinvalid` (startup) | `item`, `field`, where the layout names them | The development file cannot be read, is not JSON or breaks the layout (`04` PRT-LIB-005) |
| `secrets.agent.configurationunsafe` (agent) | `key` (`file`, or the directory) | The agent's file or a directory above it is writable by anyone but root (`05` PRT-OPS-001) |
| `secrets.agent.configurationinvalid` (agent) | `key` | An agent's configuration value outside its rule (`01` PRT-AGT-002) |
| `secrets.agent.declarationinvalid` (agent) | `process`, `member` | A declaration outside its rule (`01` PRT-AGT-007) |
| `secrets.agent.swapactive` (agent) | | A swap area is active (`01` PRT-AGT-001) |
| `secrets.agent.opversion` (agent) | `found` | `op` is not the pinned version (`01` PRT-AGT-002) |
| `secrets.agent.tokenunavailable` (agent) | | The token file cannot be read, is empty or too large (`01` PRT-AGT-002) |
| `secrets.secret.unknown` | | The agent is `ready` and its set holds no secret at the address (`01` PRT-AGT-008) |
| `secrets.secret.denied` | | The process's declaration does not name the address (`01` PRT-AGT-007) |
| `secrets.secret.forminvalid` | `address` | Logged by an adapter: a value that does not decode or is not of the sibling's form (`04` PRT-LIB-006); never answered |
| `secrets.agent.waittimeout` | `reason` (a `WaitReason` spelling, or `unreachable`) | The client waited `Portunus:Client:WaitTimeout` (`04` PRT-LIB-004) |
| `secrets.agent.unreachable` | | `StatusAsync` could not reach the agent (`04` PRT-LIB-004) |
| `secrets.request.invalid` | | A request outside its format, or of a kind the socket does not take (`01` PRT-AGT-008) |
| `secrets.request.version` | `version` (the agent's) | A request of another protocol version (`01` PRT-AGT-008) |
| `secrets.push.wrongvault` | | A push for another vault (`02` PRT-PSH-003) |
| `secrets.push.ready` | | A push into a `ready` agent without `replace` (`02` PRT-PSH-003) |
| `secrets.push.layoutinvalid` | `item`, `field` | A pushed set breaks the layout; its first finding named (`02` PRT-PSH-003) |
| `secrets.push.toolarge` (tool) | | A payload over its limit (`02` PRT-PSH-003) |
| `secrets.push.unreachable` (tool) | | `receive` cannot reach the push socket, or the tool cannot reach the server (`02` PRT-PSH-002, PRT-PSH-003) |
| `secrets.cli.inputinvalid` (tool) | `member` | An option missing or outside its rule (`02` PRT-PSH-002) |
| `secrets.cli.opversion` (tool) | `found` | The computer's `op` is below the pinned version or of another major version (`02` PRT-PSH-002) |
| `secrets.cli.opfailed` (tool) | `step` (`list`, `get`) | An `op` command failed (`02` PRT-PSH-002) |
| `secrets.cli.sshmissing` (tool) | | No `ssh` on the `PATH` (`02` PRT-PSH-002) |
| `secrets.fault` (agent, tool) | | A fault that carries no code, in either executable (`06` PRT-ENG-003) |
| `secrets.layout.vaulttitle` (layout) | | A vault title outside its rule (`03` PRT-VLT-001) |
| `secrets.layout.itemtitle` (layout) | `item` | An item title outside its rule (`03` PRT-VLT-002) |
| `secrets.layout.itemduplicate` (layout) | `item` | Two item titles alike without regard to case (`03` PRT-VLT-002) |
| `secrets.layout.fieldname` (layout) | `item`, `field` | A label outside the secret name's rule, its suffixes included (`03` PRT-VLT-003, PRT-VLT-004) |
| `secrets.layout.fieldduplicate` (layout) | `item`, `field` | Two labels of an item alike without regard to case (`03` PRT-VLT-003) |
| `secrets.layout.valueempty` (layout) | `item`, `field` | An empty value (`03` PRT-VLT-003) |
| `secrets.layout.valuetoolarge` (layout) | `item`, `field` | A value over 64 KiB (`03` PRT-VLT-003) |
| `secrets.layout.mixed` (layout) | `item`, `field` | A field `<name>` beside `<name>.<n>` (`03` PRT-VLT-004) |
| `secrets.layout.currentmissing` (layout) | `item`, `field` | Live versions with no `.current` (`03` PRT-VLT-004) |
| `secrets.layout.currentinvalid` (layout) | `item`, `field` | A `.current` that is not a number, names no live version, or stands without versions (`03` PRT-VLT-004, PRT-VLT-005) |
| `secrets.layout.toomany` (layout) | `item`, `field`, where they apply | Over 64 items, 128 fields in an item, or 64 live versions of a set (`03` PRT-VLT-003, PRT-VLT-004) |
| `secrets.layout.settoolarge` (layout) | | The set's values over 1 MiB together (`03` PRT-VLT-003) |
| `secrets.layout.nonote` (layout, a warning) | `item` | An item with no note; reported by `portunus check` alone (`03` PRT-VLT-002) |

---

## 2. Enumerations and closed values

Members in this order; the spelling is the one written in every wire format and log.

| Enumeration | Members (spelling) |
|---|---|
| `AgentState` | `Waiting` (`waiting`), `Ready` (`ready`) |
| `WaitReason` | `Fetching` (`fetching`), `Unreachable` (`unreachable`), `RateLimited` (`ratelimited`), `Refused` (`refused`), `LayoutInvalid` (`layoutinvalid`), `AwaitingPush` (`awaitingpush`) |
| `SetSource` | `Vault` (`vault`), `Push` (`push`) |

| Closed value | Values |
|---|---|
| `Portunus:Client:Source` | `agent`, `file` |
| `Portunus:Agent:Source` | `vault`, `push` |
| A request's `kind` | `read`, `status`, `push` |

---

## 3. Commands and exit codes

| Command | Options | Exit codes |
|---|---|---|
| `portunus-agent run <instance>` | None | 0 after a clean stop; 1 for a refusal of its start |
| `portunus-agent receive <instance>` | None; the payload on standard input | 0 where the agent took the push; 1 otherwise |
| `portunus-agent status <instance>` | None | 0 `ready`; 3 `waiting`; 1 unreachable |
| `portunus push` | `--vault <vault title>`, `--to <ssh destination>`, `--instance <instance>`, `--account <account>` (optional), `--replace` (optional) | 0 accepted; 1 otherwise |
| `portunus check` | `--vault <vault title>`, `--account <account>` (optional) | 0 no error finding; 1 otherwise |
| `portunus status` | `--to <ssh destination>`, `--instance <instance>` | As `portunus-agent status` |

An `<ssh destination>` is 1 to 255 characters of ASCII letters, digits, `.`, `-`, `_` and
at most one `@`, not starting with `-` (P-034); an `<account>`, 1 to 255 of ASCII letters, digits, `.`,
`-`, `_` and `@`, not starting with `-`.

---

## 4. Configuration

A duration is written in .NET's `TimeSpan` form (`00:00:05`). Every key takes effect at
the start (`05` PRT-OPS-001).

**The client and the adapters**, in the host's configuration:

| Key | Default | Rule | Used by |
|---|---|---|---|
| `Portunus:Client:Source` | `agent` | Section 2 | `04` PRT-LIB-003, PRT-LIB-005 |
| `Portunus:Client:Socket` | `/run/portunus/agent.sock` | An absolute path of at most 107 bytes | `04` PRT-LIB-004 |
| `Portunus:Client:File` | None | An absolute path; required where the source is `file` | `04` PRT-LIB-005 |
| `Portunus:Client:Timeout` | `00:00:05` | 1 second to 1 minute | `04` PRT-LIB-004 |
| `Portunus:Client:WaitTimeout` | `00:05:00` | 0 to 1 hour | `04` PRT-LIB-004 |
| `Portunus:Client:PollInterval` | `00:00:02` | 1 second to 1 minute | `04` PRT-LIB-004 |
| `Portunus:Janus:Item`, `Portunus:Hermes:Item`, `Portunus:Themis:Item` | `Janus`, `Hermes`, `Themis` | An item title (`03` PRT-VLT-002) | `04` PRT-LIB-006 |
| `Portunus:Janus:Items:<secret name>`, and alike for Hermes and Themis | None | An item title; `<secret name>` a name the adapter reads | `04` PRT-LIB-006 |

**The agent**, in `/etc/portunus/<instance>.json`:

| Key | Default | Rule | Used by |
|---|---|---|---|
| `Portunus:Agent:Vault` | None; required | A vault title (`03` PRT-VLT-001) | `01` PRT-AGT-003; `02` PRT-PSH-003 |
| `Portunus:Agent:Source` | `vault` | Section 2 | `01` PRT-AGT-002; `02` PRT-PSH-005 |
| `Portunus:Agent:Root` | `/run/portunus` | An absolute path, so that `<root>/<instance>/<process>/agent.sock` is at most 107 bytes | `01` PRT-AGT-006 |
| `Portunus:Agent:TokenFile` | The file `token` in the directory `CREDENTIALS_DIRECTORY` names | An absolute path; read in `vault` mode alone | `01` PRT-AGT-003; `05` PRT-OPS-002 |
| `Portunus:Agent:OpPath` | `/usr/local/bin/op` | An absolute path | `01` PRT-AGT-003 |
| `Portunus:Agent:OpTimeout` | `00:01:00` | 5 seconds to 5 minutes | `01` PRT-AGT-003 |
| `Portunus:Agent:Retry:First` | `00:00:30` | 5 seconds to 10 minutes | `01` PRT-AGT-004 |
| `Portunus:Agent:Retry:Max` | `01:00:00` | From `Retry:First` to 1 day | `01` PRT-AGT-004 |
| `Portunus:Agent:PushGroup` | None; required | A group identifier, 1 to 4,294,967,294 | `01` PRT-AGT-006; `02` PRT-PSH-003 |
| `Portunus:Agent:Processes:<process>:Uid` | None; required | A user identifier, 1 to 4,294,967,294 | `01` PRT-AGT-007 |
| `Portunus:Agent:Processes:<process>:Gid` | None; required | A group identifier, 1 to 4,294,967,294 | `01` PRT-AGT-006, PRT-AGT-007 |
| `Portunus:Agent:Processes:<process>:Secrets` | None; required | 1 to 256 addresses, no two alike | `01` PRT-AGT-007 |

---

## 5. Limits

| What | Limit |
|---|---|
| A request on a socket | 4 KiB, received within 5 seconds (`01` PRT-AGT-008) |
| An answer, and a push's payload | 2 MiB (`01` PRT-AGT-008; `02` PRT-PSH-003) |
| Connections served at once | 64 (`01` PRT-AGT-008) |
| One `op` command's output | 4 MiB (`01` PRT-AGT-003) |
| The token file | 4 KiB (`01` PRT-AGT-002) |
| Items in a vault; fields in an item | 64; 128 (`03` PRT-VLT-003) |
| A value; the values of a set together | 64 KiB; 1 MiB (`03` PRT-VLT-003) |
| Live versions of a set; a version's number | 64; 1 to 9999 (`03` PRT-VLT-004) |
| Declarations of an instance; addresses in one | 64; 256 (`01` PRT-AGT-007) |
| The agent's stop | Reads under way finish within 5 seconds (`01` PRT-AGT-009) |
| The client's wait log | At the first wait, then at most every 30 seconds (`04` PRT-LIB-004) |

---

## 6. Wire formats

Protocol version 1. Members in the order shown; `<b64>` is a value in base64url (RFC 4648
section 5, unpadded, P-023); an instant
is RFC 3339 with an offset of zero (`06` PRT-ENG-006).

| Format | Form |
|---|---|
| Read request | `{"version":1,"kind":"read","address":"Themis/connection"}` |
| Status request | `{"version":1,"kind":"status"}` |
| Answer while `waiting` | `{"version":1,"state":"waiting","reason":"ratelimited","since":"2026-11-05T09:00:00Z"}` |
| Status while `ready` | `{"version":1,"state":"ready","since":"...","source":"vault","loadedAt":"...","items":4}` |
| A secret | `{"version":1,"state":"ready","secret":{"current":2,"versions":{"1":"<b64>","2":"<b64>"}}}` |
| A failure | `{"version":1,"state":"<state>","failure":{"code":"secrets.secret.denied","details":{}}}` |
| Push request | `{"version":1,"kind":"push","vault":"Shop Production","replace":false,"items":[{"title":"Themis","fields":[{"label":"connection","value":"<b64>"}]}]}`; an item's note is not sent |
| Push answer | `{"version":1,"accepted":true,"items":4}`, or `{"version":1,"accepted":false,"failure":{"code":"...","details":{...}}}` |
| Development file | `03` PRT-VLT-008 |

---

## 7. Names and pinned versions

| Name | Value | Changes with the library's name |
|---|---|---|
| Code prefix | `secrets.` | No |
| Protocol version | `1` | No |
| A socket's file | `agent.sock` | No |
| The push directory | `push` | No |
| Version suffixes | `.<n>`, `.current`, `.<n>.retired` (`03` PRT-VLT-004, PRT-VLT-005) | No |
| The note field | `notesPlain`, 1Password's own | No |
| Commands | `portunus` (`Portunus.Cli`), `portunus-agent` (`Portunus.Agent`) | Yes |
| The agent's files | `/etc/portunus/<instance>.json`, `/etc/portunus/<instance>.token` | Yes |
| The agent's user and group | `portunus` | Yes |
| Unit and drop-in | `portunus-agent@.service`; `token.conf` | Yes |
| Health check | `portunus` | Yes |

| Pinned | Value |
|---|---|
| `op` | 2.41.1 (released 2026-10-21), from `https://cache.agilebits.com/dist/1P/op2/pkg/v2.41.1/op_linux_<arch>_v2.41.1.zip`, `<arch>` `amd64` or `arm64` |
| `op`'s signing key | `3FEF9748469ADBE15DA7CA80AC2D62742012EA22`, as 1Password publishes it for `gpg --verify op.sig op` |
| The tool's `op` | 2.41.1 or a later 2.x |
| `Janus.Hosting` (carrying `Janus.Core`, P-036) | 1.0.0-rc.4 |
| `Hermes.Core` | 1.0.0-rc.6 |
| `Themis.Core` | 1.0.0-rc.2 |

**The unit's settings** (`05` PRT-OPS-003). `portunus-agent@.service`: `Type=notify`,
`User=portunus`, `Group=portunus`, `ExecStart=/usr/local/bin/portunus-agent run %i`,
`RuntimeDirectory=portunus/%i`, `RuntimeDirectoryMode=0711`,
`RuntimeDirectoryPreserve=yes`, `AmbientCapabilities=CAP_CHOWN`,
`CapabilityBoundingSet=CAP_CHOWN`, `NoNewPrivileges=yes`, `LimitCORE=0`,
`ProtectSystem=strict`, `ProtectHome=yes`, `PrivateTmp=yes`, `Restart=on-failure`,
`RestartSec=60`, `StartLimitIntervalSec=3600`, `StartLimitBurst=5`,
`Before=docker.service`, `WantedBy=multi-user.target`. `token.conf`:
`LoadCredential=token:/etc/portunus/%i.token`.

**The vault's quota** (P-006, as 1Password documents it on 2026-11-05, for the owner's
plan): per service account token, 1,000 reads and 100 writes an hour; per 1Password
account, 1,000 requests a day, reads and writes together, shared by every service account
of the account. A fetch costs `3 + n` reads, `n` the vault's items (`01` PRT-AGT-003).
