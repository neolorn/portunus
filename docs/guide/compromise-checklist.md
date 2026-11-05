# Compromise checklist

What the owner does when a server running an agent is, or may be, compromised: its disk
copied, its backups taken, or someone inside it. In that order. Each step names what it
protects and what it cannot. The reasons are `docs/spec/08-threat-model.md`.

## 1. Revoke the token, now

1. In 1Password: Developer, Service accounts, the environment's service account, Token,
   "Revoke Token". Access ends at once.
2. A running agent keeps the set it holds, so the applications keep running. Its next
   start fetches nothing: it waits, `refused`, until a push or a new token.
3. In `push` mode there is no token on the server; go to step 2.

A copied disk held at most this token. Revoking it stops every read from now on. It does
not undo a read already made with it.

## 2. Learn what was read

1. Open the service account's item usage report (Developer, Service accounts, the
   account, "View Item Usage Report"): which items were read, when, and how.
2. Compare it with the agent's starts in the server's journal
   (`journalctl -u portunus-agent@<instance>`): each start reads the list and each item
   once.
3. Any read that is not an agent start is the attacker's. Then every secret of the vault
   is read.
4. Decide which case holds:
   - **The disk was copied and the token was not used**: no secret left the vault.
     Rotate the outside keys and logins anyway (step 3), since the token's window was
     open; the data keys were not exposed (step 4).
   - **The token was used, or someone was inside the running server**: every secret of
     the vault, and every secret any process on it held, is exposed. Do every step.

## 3. Rotate the outside keys and the database logins

For each item of the environment's vault, as its note says:
1. Make the new value at the provider or the database.
2. Write it in the vault (single secrets in place; history keeps the old value).
3. Deliver it: `portunus push --replace` into the running agent, or a new agent on a
   clean server (step 5).
4. Restart the processes that read it.
5. Revoke the old value at the provider, and change or drop the old database login.

Rotate everything the vault holds that an outsider can use alone first: payment and
messaging providers' keys, social sign-in credentials, mail accounts, database logins
reachable from outside.

## 4. What the data keys mean

The data keys (Janus's key-encryption and fingerprint keys, Hermes's content and hash
keys) protect the data the libraries encrypt in the database. Everything else in the
database is as readable as the database files themselves, and a copied disk holds those
files.
- **Keys not exposed**: the encrypted fields of a copied database stay closed. Rotate the
  keys on the libraries' normal schedule.
- **Keys exposed**: any copy of the data the attacker holds, the database files, a
  backup, an export, can be opened with those keys, whatever is rotated afterwards.
  Rotating (Janus's `rotate-kek` and `rotate-fingerprint-key`, Hermes's new versions)
  protects data written and copies taken after it, not before. Treat the data in those
  copies as disclosed; what that obliges is the business owner's, with their advisers.
- Keep each old version, relabelled `.retired` once its library retires it, while any
  backup still holds data under it. Delete it only when the last such backup is gone.

## 5. Get the environment running again

1. Rebuild or clean the server before it holds secrets again; a compromised server is not
   trusted with a new token.
2. Make a new service account, `read_items` on the environment's vault alone, and place its
   token as the deployment's duties say; or run the agent in `push` mode and push after
   every start until then.
3. Check the vault: `portunus check --vault "<Project> <Environment>"`.
4. Start the agent and the processes; `portunus status` reads `ready`.

## 6. Backups and the vault

Data backups and the vault are never in the same place. Check that no backup sat in a
vault, and no export of a vault sat beside the backups. If one did, both were exposed
together: treat the backup's data as opened (step 4).

## 7. Record it

Write down what happened, when it was found, what was read, what was rotated and when, and
which backups hold data under which key versions.
