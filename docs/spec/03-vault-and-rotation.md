# 03 — The vault and rotation

The rule every project's vault follows, so every vault looks the same and the agent needs
no lookup table; how the rule is checked; and how a new version of a secret reaches the
processes. The rule is the library's own: the agent, the push and the development file
all read by it, and refuse a set that breaks it. Every limit is `07` section 5.

---

## 1. The layout

**PRT-VLT-001** — Each project and environment SHALL have one vault, titled
`<Project> <Environment>`, read by a service account of its own whose only permission is
to read that vault's items.

**Values (P-004, P-011).** A vault title is two words separated by one space, each 1 to
32 ASCII letters, digits and `-`, starting with a letter (`Shop Production`,
`Shop Staging`); titles are compared without regard to the case of ASCII letters. The
service account is created with the permission `read_items` on that vault alone, so its
token reads one environment and writes nothing; since a service account's vaults and
permissions cannot be changed once it is made, a change is a new service account (P-004).
A service account cannot be granted a built-in Personal, Private or Employee vault or the
default Shared vault, so the vault is one made for the environment. Development reads a
local file of the same layout instead of a vault, and tests a fake (PRT-VLT-008).

*Source: P-004, P-011*

**Acceptance criteria**
1. A push or a check naming a vault title outside the rule is refused before `op` runs
   (`02` PRT-PSH-002).

---

**PRT-VLT-002** — A vault SHALL hold one item per owner of secrets, the component whose
code reads them, and never one item per secret nor one per process.

**Values (P-011, P-021).** The items are: one per sibling library the environment uses
(`Janus`, `Hermes`, `Themis`), holding every secret that library reads by its own name,
its database connection included; one for the host application itself, titled with the
project's name (`Shop`); `Database`, holding the logins that belong to the database
rather than to one component's code (the migration logins a deployment's pipeline reads,
the host's own connection where the host names it so); and one per outside provider,
holding the credentials that provider issued (an SMS provider's API key, a social
provider's client credential), whichever component presents them, an adapter being told
which item holds such a secret (`04` PRT-LIB-006). An item title is 1 to 64 ASCII
letters, digits, spaces, `-`, `_` and `.`, starting with a letter, with no space at its
end and no two in a row; no two items of a vault have titles alike without regard to
case. Each item carries a note, its `notesPlain` field, saying what it holds, where each
value comes from and how to rotate it; the note is never a secret and never delivered.

*Source: P-011, P-021*

**Acceptance criteria**
1. An item title outside the rule, and two titles alike but for case, are errors of the
   check (PRT-VLT-007).
2. An item without a note is a warning of the check, and the agent takes it.

---

**PRT-VLT-003** — Each secret SHALL be one field, labelled exactly as the code that reads
it names it, so the agent and the adapters need no table to find it.

**Values (P-011).** A secret name is 1 to 64 ASCII letters, digits, `.`, `-` and `_`,
starting with a letter, with no two `.` in a row and none at its end
(`connection`, `keyEncryptionKeys`, `socialProvider.example`); the label of a field is
the secret's name, or for a version the name and its suffix (PRT-VLT-004). No two fields
of an item have labels alike without regard to case, since the vault's own references
ignore it (P-011); an item's sections are not part of a name, so the rule spans every
section of it. A value is the field's text, 1 byte to 64 KiB of UTF-8; a value that is
bytes and not text (a key) is written in base64 (RFC 4648 section 4, padded, no
whitespace), and the code that reads it decodes it (`04` PRT-LIB-006). A certificate or a
private key is its PEM text in a field; a file attached to an item is not read. A vault
holds at most 64 items, an item at most 128 fields, and the values of a set together at
most 1 MiB.

*Source: P-011*

**Acceptance criteria**
1. A label outside the rule, two labels alike but for case in different sections of one
   item, an empty value and a value of 64 KiB and one byte are errors of the check.

---

**PRT-VLT-004** — A secret with one live value SHALL be one field; a secret with several
live versions SHALL keep each version as its own field, with a field marking the current
one.

**Values (P-012).** A single secret (an outside API key, a database password) is the
field `<name>`; its past values are left to the item's history in 1Password, and the
agent delivers it as version 1, current 1. A versioned secret (a data key) is the fields
`<name>.<n>`, `n` a version number from 1 to 9999 written without leading zeros, one per
live version, and `<name>.current`, whose value is the decimal number of the current
version, which is one of them; it holds 1 to 64 live versions, and no field `<name>`
stands beside them. A name never ends in `.` and digits, nor in `.current` or `.retired`,
which mark versions. An address names either form by `<name>` (`00` section 1.5), and
answers the single value, or every live version with the current one.

*Source: P-012*

**Acceptance criteria**
1. A set whose `.current` is missing, is not a number, or names a version not present, is
   an error; so are a field `<name>` beside `<name>.1`, and a version `01`.
2. A read of a versioned secret's address answers every live version and the current
   one; a read of a single secret answers version 1, current 1.

---

**PRT-VLT-005** — An old version SHALL stay in the vault while any data or backup needs
it, and a version its library has retired SHALL stay without being delivered.

**Values (P-012, P-019).** A version stays a live field until the library that uses it
no longer reads it (a rotation complete, `04` PRT-LIB-006). A version the library has
retired, which a backup taken under it still needs, is relabelled `<name>.<n>.retired`:
it stays in the item for a restore, is checked as a version is, and is never delivered
nor counted among the live versions. The current version is never retired. A version is
deleted only once no backup holds data under it; when is the owner's (`05` PRT-OPS-003).
A live and a retired version of the same number is `secrets.layout.fieldduplicate`,
naming the later label; a secret holding only retired versions is allowed, delivers
nothing, and a declaration naming it finds it unknown (P-024).

*Source: P-012, P-019, P-024*

**Acceptance criteria**
1. A read of a set holding `.1.retired`, `.2` and `.current` naming 2 answers version 2
   alone; `.current` naming a retired version is an error.
2. `.2` beside `.2.retired` is `fieldduplicate`; a secret holding only `.1.retired`
   passes the check and is not delivered (P-024).

---

**PRT-VLT-006** — A vault SHALL hold only what a declared process or the deployment's
pipeline reads; company infrastructure that no service reads SHALL stay out of these
vaults.

**Values (P-011).** The owner's account logins, a registrar's, a server provider's
console, the owner's own recovery codes and the like live in the owner's other vaults,
which no service account is granted. Data backups are never kept in a vault, and a vault's
export is never kept beside the data backups (`08` PRT-THR-003).

*Source: P-010, P-011*

---

## 2. The check

**PRT-VLT-007** — The rule SHALL be checked by one validator that the agent's fetch, the
agent's push, the tool's push and check, and the development file all use, answering
each finding with its code, its item and its field.

**Values (P-011).** The validator is internal to `Portunus.Core` (`06` PRT-ENG-001). Its
findings are the codes `secrets.layout.*` of `07` section 1: each is an **error**, which
refuses the set, but `secrets.layout.nonote`, a **warning**, which only `portunus check`
reports. It reads names and counts, never a value's meaning, save that a `.current`
value is a number naming a version. It answers every finding of a set in one pass,
ordered by item title, then field label, ordinal, so the owner mends a vault in one round.

*Source: P-011*

**Acceptance criteria**
1. Each rule of PRT-VLT-001 to PRT-VLT-005 broken alone gives its code, naming its item
   and field, and a set breaking three gives three findings in that order.

---

**PRT-VLT-008** — Development SHALL read a local file of the vault's layout, and tests a
fake, never a vault.

**Values (P-014).** The development file is one JSON document,
`{ "vault": "<vault title>", "items": { "<item title>": { "<label>": "<value>", ... } } }`,
read by the client where `Portunus:Client:Source` is `file` in a host whose environment is
Development (`04` PRT-LIB-005), and checked by the validator. It lives on the developer's
computer, outside every repository. Tests use `Portunus.Testing`'s fixed client (`04`
PRT-LIB-008) or, for the agent, the fake `op` (`06` PRT-ENG-008).

*Source: P-014*

---

## 3. Rotation

**PRT-ROT-001** — A new version SHALL be written to the vault first, then reach the agent
by its restart or by a push, then reach each process by its restart.

**Values (P-012, P-013).** The agent fetches only at its start (`01` PRT-AGT-003), so a
restart of it, costing one fetch's reads (P-006), or a push with `--replace`, costing
none (`02` PRT-PSH-003), gives it the vault as it now is. The siblings read their
secrets once, at their start (P-013), so a process holds what it read until it restarts;
a process that starts after the agent took the new set reads it. The steps:
- **A single secret** (an outside API key, a database password): make the new value at
  the provider or the database, where it can live beside the old one for a while; write
  it in the field; deliver it; restart the processes that read it; then revoke the old
  value at the provider or the database.
- **A versioned secret**: add `<name>.<n+1>`; move `<name>.current` as the library's rule
  says (PRT-ROT-002); deliver; restart the processes; run the library's own rotation
  where it has one; relabel a version the library retires (PRT-VLT-005).

*Source: P-012, P-013*

---

**PRT-ROT-002** — A sibling library's versions SHALL reach it as that library expects,
each adapter reading the versions and the current marker as the table says.

**Values (P-012, P-013, P-020).**

| Library | Secret | What the adapter answers | The library's own rule, as the vault follows it |
|---|---|---|---|
| Janus | `keyEncryptionKeys` | `KeyEncryptionKeys(current, versions)`, from `.current` and each live version | Add the new version as current, keeping the previous; deliver; restart; `rotate-kek`; `rotate-kek --retire`; relabel the previous `.retired` (Janus OPS-SEC-003) |
| Janus | `fingerprintKeys` | `FingerprintKeys(current, versions)`, likewise | Add the new version with `.current` unchanged; deliver; restart every process; move `.current`; deliver; restart; `rotate-fingerprint-key`; retire; relabel (Janus OPS-SEC-003) |
| Hermes | `contentKeys`, `hashKeys`, `tokenKeys`, `smsReportKey`, `pushVapidKeys` | `SecretVersions` of every live version, in ascending order; `.current` is not passed, since Hermes takes its write version from its own configuration | Add the new version with Hermes's write version held at the old one; deliver; restart every process; move Hermes's configured write version and `.current` together; restart (Hermes MSG-OPS-008) |
| Hermes, Themis | `connection` and the other single secrets | `SecretVersions` of one version, 1 | A password changed in place is an ordinary change, read at the next start (Hermes MSG-OPS-008; Themis APR-OPS-008) |

*Source: P-012, P-013, P-020*

**Acceptance criteria**
1. A Janus key set of versions 1 and 2 with `.current` 2 reaches Janus as current 2 with
   both versions; the same set reaches Hermes as versions 1 and 2, in that order.

---

**PRT-ROT-003** — The service account's token SHALL be rotated in 1Password and reach the
agent by its restart, and a token thought exposed SHALL be revoked.

**Values (P-004, P-010).** Rotating a service account's token in 1Password makes a new
token with the same permissions and lets the owner choose when the old one stops
working; the owner places the new token in the token file (`05` PRT-OPS-003) and
restarts the agent, which reads it at its start. Revoking a token removes its access at
once, a running agent keeping the set it already holds (`01` PRT-AGT-003); what follows a
revocation is `08` PRT-THR-003 and the compromise checklist.

*Source: P-004, P-010*
