# Phase 8: The owner's check by hand, and the threat test

## Items built

**`08` PRT-THR-001** criterion 1, its test over this repository, which waited on the recorded scenarios: every requirement the table of threats and controls names, save the six on the list kept beside the test (P-041), has a test of this repository named after it.
- ThreatModelTests (unit): PRT_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingItsRequirement

`build/gates/built-items.txt` holds the criterion.

**`06` PRT-ENG-008**, the owner's check by hand of a push on Windows, as P-044 gives it: `build/check-push-by-hand.ps1` and `build/check-push/`.
- `build/check-push/`: the container's `Dockerfile`, on the SDK's image of `global.json` with `openssh-server` from that image's own sources, the host keys the package makes removed; `sshd.conf`, the login `owner` by key alone; `sample.json`, the agent's configuration (`Sample Test`, source `push`, push group 4244, one process); `swaps`, an empty swaps file; `start.sh`, which makes the host key, starts `sshd` and runs the agent through the harness as root; `portunus-agent`, the agent's commands through the harness for the `ssh` session.
- `build/check-push-by-hand.ps1`: it stops before anything starts where no public key is found, a program it needs is not on the `PATH`, the owner's `known_hosts` already names `127.0.0.1`, port 22 of `127.0.0.1` is in use, or Docker does not answer. It runs `op --version`, builds the tool, publishes `tests/Portunus.Agent.Harness` for `linux-x64` from a copy under `tmp/`, builds and starts the container on `127.0.0.1:22`, waits for the agent's push socket, adds the container's host key to the owner's `known_hosts` as `127.0.0.1`, and runs `check`, `push`, `status`, the same push and the push with `--replace`, each shown and kept. It then reads the vault's values through `op` into memory, compares them with every command's output and error and with the agent's log, checks the log for the three pushes with their outcomes and number of items, and asks the owner's two questions. At the end, whatever the outcome, it removes the container and its image, the host key (`ssh-keygen -R`), the owner's `known_hosts.old` put back or the one written removed, and `tmp/check-push/`. Its line: `op <version>, commit <sha>, passed|failed at <step>, <n> prompts, the agent run through tests/Portunus.Agent.Harness with an empty swaps file`; exit 0 or 1.

## Items not built

- The owner's run of `build/check-push-by-hand.ps1` (P-009, P-044), and its line in this report: waiting on the owner. macOS and Linux stay not yet checked by hand.

## What the owner does

Once, on his Windows PC, beside what phase 7 had him make (`op` 2.41.1 with the 1Password app's integration, Docker Desktop, PowerShell 7, the vault "Sample Test"):
1. Make sure Windows' own OpenSSH client answers in PowerShell 7: `ssh -V` runs `C:\Windows\System32\OpenSSH\ssh.exe` (`(Get-Command ssh).Source` names it).
2. Have the public key of an SSH key you sign in with as a file: `~\.ssh\id_ed25519.pub` or `~\.ssh\id_rsa.pub`, or any other `.pub` file you name with `-Key`, such as one of those beside your keys in `~\.ssh`.

To run it:
1. Start Docker Desktop. Make sure nothing on the PC listens on port 22 of `127.0.0.1` and your `known_hosts` names no `127.0.0.1`; the script checks both and stops, saying which, before it starts anything.
2. Open PowerShell 7 in a clone of the repository, on the commit the lead names, with no change of your own in it (`git status` shows nothing).
3. Run `pwsh build/check-push-by-hand.ps1`, adding `-Key <path of your .pub file>` where your key is not one of the two above.
4. Wait while it builds the tool, the agent and the container: the first build downloads Ubuntu's `openssh-server` into the container, and takes a few minutes.
5. Approve each prompt of the 1Password app (Windows Hello), and count them all, a prompt to use your SSH key included. It names each command before it runs it.
6. Answer its two questions: how many prompts there were, and whether anything that looked like a secret value appeared on screen.
7. Send the lead its last line.

## Criteria checked without a test

- `build/check-push-by-hand.ps1`, run here on the owner's PC with no 1Password prompt and no real vault: the fakes' `op` first on the `PATH`, answering as the recording of "Sample Test" read by the tool, both for the tool and for the script's own reads; an `ssh.cmd` first on the `PATH` running Windows' own `ssh.exe` with a throwaway key alone (`-i`, `IdentitiesOnly=yes`, `IdentityAgent=none`, `BatchMode=yes`), its public key given with `-Key`; the owner's answers given to the prompts. Everything else ran as the owner's run will: the build, the publish, the container (Ubuntu 24.04, `openssh-server` from its sources), `sshd` on `127.0.0.1:22`, the agent through the harness in push-only mode, the owner's own `known_hosts`, and the real `ssh` carrying each push:
  - answers `0` and `no`: `check` exits 0 with Ceres's `secrets.layout.nonote`; the push is accepted with 4 items; `status` answers `ready` from `push`; the same push is refused with `secrets.push.ready`; `--replace` is accepted with 4 items; the agent's log holds the three pushes; `op 2.41.1, commit a652b54, passed, 0 prompts, the agent run through tests/Portunus.Agent.Harness with an empty swaps file`, exit 0;
  - answer `yes`: `failed at a value on the screen, as the owner saw it`, exit 1;
  - a field's value made `accepted`, which the push's answer holds: `failed at a value in the tool's output`, the agent's log found holding it too, exit 1;
  - a key with a passphrase, which `ssh` waited on with no console: each push failed and ended once its `ssh` was stopped, `failed at portunus push`, and the end still ran;
  - after each run the container and its image are gone, `tmp/check-push/` is removed, and the owner's `known_hosts` and `known_hosts.old` are byte for byte as they were before.
- The stops before anything starts: no public key (this PC has neither default file), and port 22 of `127.0.0.1` in use (a listener the trial opened), each with its sentence, the line and exit 1; `ssh-keygen -F 127.0.0.1` finds a plain and a hashed entry in a file under `tmp/`, and none in the owner's.
- `06` PRT-ENG-008, tests written first: the script is a tool the owner runs, tried by hand as above, as the phase 7 script was.

## Awaiting the pipeline's run

- Nothing beyond the gate of record.

## Analyser suppressions

- None new.

## Paths that make a string of a value or the token

- `build/check-push-by-hand.ps1`: each value of "Sample Test", read through `op item get --reveal` into a string of the script's memory, as PowerShell holds text, and its base64url form, compared with the commands' output and error and the agent's log, then dropped; never shown or written (P-044).

## Resolved by rule

- `build/check-push-by-hand.ps1`, the owner's `known_hosts` after `ssh-keygen -R`: Windows' `ssh-keygen` writes the file again with CRLF line endings, so where it then holds the owner's entries alone, as they stood, his own bytes are put back; otherwise the step fails. Governs: P-044, "Nothing else of the owner's configuration is touched".
- `build/check-push/sshd.conf`, `MaxAuthTries 64`: the owner's `ssh` offers every key its agent holds before the one authorized, and the default 6 would end the login first. Governs: P-044, the login `owner` whose `authorized_keys` holds the owner's own key.
- `build/check-push/Dockerfile`, the host keys the package makes are removed, and `start.sh` makes an Ed25519 one at each start, the one the script adds. Governs: P-044, the container's host key added and removed.
- `build/check-push-by-hand.ps1`, the script's own reads of the vault: `op item list` and `op item get <item> --vault <vault> --format json --reveal`, in the tool's environment and order of options; the notes and each set's `.current`, a version's number, are not compared, as in phase 7's searches. Governs: P-044, "reads them with `op item get --reveal` into memory alone".
- `build/check-push-by-hand.ps1`, the `ssh` the tool will run is named before the run, and the container's log is shown where the agent does not listen within a minute, before any push. Governs: P-044, the check through Windows' own `ssh`, and its first failing step.

## Open questions

1. **Tier 3: the agent's swap check in the container of P-044.** Asked on `phase-08`. Answered by 8c0042e (P-044 amended): the agent runs through `tests/Portunus.Agent.Harness` with an empty swaps file, the one difference from the shipped agent, which the line names. Built so.
2. **Tier 2: the push's destination in P-044.** Asked on `phase-08`. Answered by 8c0042e (P-044 amended): reading (a), `127.0.0.1:22` and `owner@127.0.0.1`, the stops on port 22 and on an entry naming `127.0.0.1`, and `known_hosts.old` put back or removed. Built so.

## Checks

- The commit: the report's own commit, the head of `phase-08b`, on the lead's `8c0042e`; the threat test cherry-picked from `phase-08`'s 4d1eef2.
- Fast checks on every commit; before the push, the full gates by `run-gates.ps1 -Base origin/main -Head HEAD`.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
