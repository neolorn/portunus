# Phase 7: The real `op` on the owner's computer

## Items built

Part 1, before the owner's first run.

**`06` PRT-ENG-008**, the scenarios, as P-043 gives them: `build/check-real-op.ps1`, renamed in git from `build/record-op-scenarios.ps1` in a commit of its own, then changed.
- It refuses an `op` other than 2.41.1 before it asks for anything.
- It asks for the two service accounts' tokens without echo, runs the agent's commands with the test service account's token, the tool's with none, so the 1Password app prompts, and one command with the network disconnected, as P-035's script did.
- It keeps what it records in memory, replaces every 1Password identifier and sign-in address with its placeholder (P-035), and refuses, removing everything, where an identifier is left, an `id` of an unknown form was recorded, or either token is in what `op` wrote. It writes the files under `tmp/check-real-op/` first, reads them again and checks them again.
- It compares the files with the recorded scenarios' files the commit holds (`git show HEAD:`), each RFC 3339 instant set aside and line endings as line feeds: the same set of files, each the same.
- On `changed` it writes the new files in place, removes a recorded file no longer recorded, builds `Portunus.sln`, runs the `integration` and `linux` tests through `build/gates/gate-tests.ps1 -Kinds integration, linux` and prints their result.
- A written scenario (P-028), one marked `"recorded": false`, and a file only it names are neither compared nor touched.
- It ends with one line, `op <version>, commit <sha>, same|changed|failed`, and exits 0 for `same`, 2 for `changed` and 1 for `failed`. A failed run removes `tmp/check-real-op/` and puts back any file it had put in place.

`build/gates/gate-tests.ps1` takes `-Kinds`, the kinds to run of the four, by default every one, as the gate runs them.

## Items not built

Part 2, waiting on the owner's first run, whose files the lead's message hands over:
- The recorded scenarios of `tests/Portunus.Fakes/`, and the tests phases 3 and 4 left waiting on them: `01` PRT-AGT-002 criterion 1's `secrets.agent.opversion` naming a version found and its `secrets.agent.tokenunavailable` through the start; `01` PRT-AGT-003 criteria 1 to 4, PRT-AGT-004 criteria 1 to 3, the fetch's part of PRT-AGT-005 criterion 1, PRT-AGT-001 criterion 4; `02` PRT-PSH-002 criteria 1 to 4, PRT-PSH-003 criterion 4, PRT-PSH-004 criterion 1.
- `08` PRT-THR-001 criterion 1's test over this repository, which those tests let pass.
- The written scenarios `rate-limited` and `wrong-version` (P-028), which open question 1 asks about.

`06` PRT-ENG-011, the release's wait on the owner's run, is checked by the lead when a tag is pushed.

Still parked from phase 4, waiting on the owner: his check by hand on Windows.

## What the owner makes once, then runs before each release

Once, on his own computer:
1. Install `op` 2.41.1 and put it on the `PATH`; turn on the 1Password app's integration with `op`.
2. Install PowerShell 7, Git, the .NET SDK that `global.json` names, and Docker Desktop, and start Docker Desktop.
3. Make a vault named "Sample Test" holding four Secure Note items. Give every field an obviously dummy value such as `sample-value-1`, never a random-looking one, which the secret scan could take for a credential:
   - `Themis`: a field `connection`, and a note.
   - `Janus`: fields `keys.1`, `keys.2`, `keys.3`, and `keys.current` holding `2`, and a note.
   - `Hermes`: a field `token`, and a note.
   - `Ceres`: a field `password`, and no note.
4. Make a vault named "Sample Broken" holding two Secure Note items:
   - `Themis`: fields `connection` and `bad label`, and a note.
   - `Hermes`: a field `token`, and no note.
5. Make a service account named `Portunus Test` that can read items in both vaults and nothing else. Save its token in your own private vault.
6. Make a service account named `Portunus Test Revoked` that can read items in "Sample Test". Save its token in your own private vault, then revoke the token.
7. Keep both vaults, both service accounts and both saved tokens for every later run.

Before each release, and now for the first run:
1. Open PowerShell 7 in a clone of the repository, on the commit the lead names, with no change of your own in it (`git status` shows nothing).
2. Run `pwsh build/check-real-op.ps1`.
3. Paste the `Portunus Test` token when it asks, then the `Portunus Test Revoked` token. Neither is shown as you paste it.
4. Approve each prompt of the 1Password app.
5. When it asks, disconnect the computer from every network and press Enter; when it asks again, reconnect and press Enter.
6. Read its last line, which names the `op` version, the commit and one word:
   - `same`: send the lead that line.
   - `changed`: send the lead that line, the line before it with the tests' result, and the files `git status` lists under `tests/Portunus.Fakes/`.
   - `failed`: send the lead what it printed. It prints no value, no token and no identifier, and it leaves nothing behind.

## Criteria checked without a test

- The script, tried against the fake `op` under `tmp/`, in a clone of this repository under `tmp/`, through a wrapper `op` that answers as the vaults, the revoked token and the network cut, its prompts answered by the trial:
  - the first run, no recorded file committed: `changed`, every file new and put in place, the solution built and the `integration` and `linux` tests passed, exit 2;
  - those files committed: `same`, exit 0; with only the instants of `op`'s answers changed: `same`;
  - one answer altered (a field's label), which the agent's and the tool's recordings both hold: `changed`, those two files differ, the tests passed, exit 2;
  - a committed written scenario: `same`, and it is untouched;
  - a token in `op`'s answer, an identifier in a member it does not gather (`uuid`), and an `id` of an unknown form: `failed`, exit 1, `tmp/check-real-op/` removed and nothing of `tests/Portunus.Fakes/` changed;
  - an `op` answering 2.41.0: `failed` before any prompt, exit 1.
- The prompts without echo: `Read-Host -AsSecureString`, as P-035's script did; the trial answered them in its own session, since a secure prompt reads the console alone.
- `06` PRT-ENG-008, tests written first: the script is a tool the owner runs, tried by hand as above, as the recording script was in phase 4.

## Awaiting the pipeline's run

- Nothing beyond the gate of record.

## Analyser suppressions

- None new.

## Paths that make a string of a value or the token

- `build/check-real-op.ps1`: each token, as `OP_SERVICE_ACCOUNT_TOKEN` of `op`'s `ProcessStartInfo`, which demands a string, and as the string it is compared with in what `op` wrote; dropped once the files are checked. As P-035's script did.

## Resolved by rule

- `build/check-real-op.ps1`, the committed files compared: those of the commit (`git show HEAD:`), not the working tree's, since the run checks the commit checked out. Governs: P-043, "compares the files with those committed".
- `build/check-real-op.ps1`, the solution built before the tests on `changed`: the tests start the fakes and the harness, which the solution's build makes, as the gates build it before `gate-tests.ps1` runs. Governs: P-043, "runs the `integration` and `linux` tests on them".
- `build/check-real-op.ps1`, the exit codes: 0, 2 and 1 for `same`, `changed` and `failed`, beside the last line P-043 gives, so a run is told apart by its code too. Governs: P-043, "ends with one line".
- `build/gates/gate-tests.ps1`, `-Kinds`: by default every kind, so the gate runs as before. Governs: P-043 with `06` PRT-ENG-012.

## Open questions

1. **Tier 2: the written scenarios on the first run.** Item: `06` PRT-ENG-008 (P-028, P-043). P-043 leaves the written scenarios "as they are", and the check neither runs nor writes them; P-035's script wrote both, `rate-limited` from the revoked token's error with its status changed to `(429)`, and `wrong-version` from the recorded version changed to 2.41.0. No written scenario is committed yet, so after the first run none exists. Readings: (a) the check writes them on a run where none is committed, from what it recorded, as P-035's script did, and never after; (b) part 2 writes them from the first run's files, as P-028 gives their form, and the check never does. Smallest fix: (a) the lines P-035's script had, behind one condition; (b) none. Parked: nothing of part 1 under (b); under (a), the check changes before the owner's first run.

## Checks

- The commit: the report's own commit, the head of `phase-07`, on `b986b90`; the branch stands on the lead's `cf2c781`.
- Fast checks on every commit; before the push, the full gates by `run-gates.ps1 -Base origin/phase-07 -Head HEAD`.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
