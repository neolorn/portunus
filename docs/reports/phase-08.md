# Phase 8: The owner's check by hand, and the threat test

## Items built

**`08` PRT-THR-001** criterion 1, its test over this repository, which waited on the recorded scenarios: every requirement the table of threats and controls names, save the six on the list kept beside the test (P-041), has a test of this repository named after it.
- ThreatModelTests (unit): PRT_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingItsRequirement

`build/gates/built-items.txt` holds the criterion.

## Items not built

- **`06` PRT-ENG-008**, `build/check-push-by-hand.ps1` and `build/check-push/` (P-044): parked by open questions 1 and 2, each of which stops the push the script makes before it reaches the agent. Nothing of the script or of the container's files is built.
- The owner's check by hand on Windows (P-009), its run of that script and its line: waiting on the script.

## Criteria checked without a test

- The two findings of the open questions, here, on the owner's Windows PC, where he will run the script:
  - a container of the SDK's image of `global.json` (`mcr.microsoft.com/dotnet/sdk:10.0.403`), run by Docker Desktop, reads in `/proc/swaps` a swap area of Docker Desktop's own virtual machine, `/dev/sdc`, a partition of 8 GiB;
  - the tool's rule for `--to`, `ToolArguments.IsDestination`, read against `07` section 3: `ssh://owner@127.0.0.1:2222` holds `:` and `/`, which the rule refuses.

## Awaiting the pipeline's run

- Nothing beyond the gate of record.

## Analyser suppressions

- None new.

## Paths that make a string of a value or the token

- None new.

## Resolved by rule

- None.

## Open questions

1. **Tier 3: the agent's swap check in the container of P-044.** Item: `06` PRT-ENG-008 (P-044), the script's container. P-044 runs "the agent published for `linux-x64` from the clone" in push-only mode in a container of the SDK's image, as its root. `01` PRT-AGT-001 has the agent read `/proc/swaps` and refuse to start, with `secrets.agent.swapactive`, where any swap area is active. A container shares the kernel of Docker Desktop's virtual machine, whose `/proc/swaps` lists an active area on the owner's PC (above), so the agent in the container refuses its start, and every push of the script fails before it reaches an agent. The `linux` tests do not meet this: they give the agent a swaps file of their own through the harness (P-027). Parked: the script and its container, whole.
2. **Tier 2: the push's destination in P-044.** Item: `06` PRT-ENG-008 (P-044); `07` section 3; `02` PRT-PSH-002. P-044 runs `portunus push ... --to ssh://owner@127.0.0.1:2222` and `portunus status` the same way, and adds the host key as `[127.0.0.1]:2222`. `07` section 3 gives an `<ssh destination>` as ASCII letters, digits, `.`, `-`, `_` and at most one `@` (P-034), which the tool checks before anything runs, so it refuses that destination with `secrets.cli.inputinvalid`, `details.member` `--to`; the tool takes no port of its own, and P-044 rejects a temporary `ssh` configuration. Readings: (a) the container listens on `127.0.0.1:22` and the script pushes `--to owner@127.0.0.1`, its host key added as `127.0.0.1`, where nothing else on the PC listens on port 22; (b) `07` section 3's rule takes an `ssh://` destination with a port, and the tool passes it to `ssh` as it does any other; (c) the script adds a `Host` entry for the container to the owner's own `ssh` configuration for the run, and removes it at the end, as it does the host key. Smallest fix: (a) P-044's port and host-key form; (b) a row of `07` section 3, the tool's rule and its tests; (c) P-044's line on the owner's configuration. Parked: the script and its container, whole.

Also found in reading P-044, for when the script is built: the SDK's image at 10.0.403 is Ubuntu 24.04, not Debian, so its own package sources give Ubuntu's `openssh-server`; and `ssh-keygen -R` keeps the file before it removes the key as `known_hosts.old` beside the owner's `known_hosts`, replacing one already there.

## Checks

- The commit: the report's own commit, the head of `phase-08`, on the lead's `c82b9ad`.
- Fast checks on every commit; before the push, the full gates by `run-gates.ps1 -Base origin/main -Head HEAD`.
- The gate of record: the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
