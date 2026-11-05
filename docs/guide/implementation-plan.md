# Implementation plan

The order in which the specification is built. What each item means is in `docs/spec/`;
this plan only orders it.

## 1. How to read the specification

Read `00-overview`, `06-engineering-conventions` and `04-library-contract` in full before
the first phase, then `03-vault-and-rotation` before phase 1, `01-agent` before phase 3
and `02-push` before phase 4. `07-reference` is authoritative for every name, code, key,
default and limit. `decision-log.md` is rationale.

## 2. Working rules

- Each phase is built with tests first, one logical change per commit, the fast checks on
  every commit, the commit-message gate and the secret scan before each push, and the full
  gate (`06` PRT-ENG-012) in the pipeline on every push, its run on the last commit pushed
  being the gate of record, then a report in the shape of section 5. A phase begins only
  when the one before it has a clean report and its pull request is merged into `main`
  after the lead's review.
- A phase builds the part of each item it names; where an item is split across phases, the
  table says which part each builds. A criterion that needs an item of a later phase is
  proven in that phase, and the report of the earlier one names it as waiting there.
- Each phase implements, in code, the rows of `07` that its items use: the codes, keys,
  enumerations, limits, wire formats and names.
- A decision-log entry whose status is **proposed** is built as the chapters give it; where
  the lead's answer replaces it before its phase, the chapters change first.
- A question about the specification parks its item and what depends on it; the rest of
  the phase goes on.

## 3. Release 1

| # | Phase | Items | Proves |
|---|---|---|---|
| 0 | Repository and gates | The docs committed first and pushed to the empty default branch, then its protection set; `06` all but the sample host and `RELEASE-NOTES.md` (phase 6), `deploy/` and `tests/Portunus.Fakes` (phase 3), `vendor/` (phase 5), the agent's publish gate of PRT-ENG-012 (phase 3), the release compatibility gate of PRT-ENG-012 and the release step of PRT-ENG-011 (phase 6), the gates as scripts; `04` PRT-LIB-001 (the rows of `Portunus.Core`, `Portunus`, `Portunus.Testing` and the test projects), PRT-LIB-002 (those projects and their empty surface files; `Portunus.Agent` comes in phase 3, `Portunus.Cli` in phase 4 and the adapters in phase 5, each with its entry point or its first type); `00` PRT-OVR-001 criterion 1; `08` PRT-THR-004 | An empty solution passes every gate of `06` PRT-ENG-012 that applies to it, locally, and in the pipeline on its pull request |
| 1 | Rules and formats | `00` section 1.5; `03` PRT-VLT-001 to PRT-VLT-007 (the validator, internal to `Portunus.Core`); `04` PRT-LIB-002 (the types of `Portunus.Core`); `06` PRT-ENG-003, PRT-ENG-005, PRT-ENG-006, PRT-ENG-007; `07` sections 1 (the layout codes), 2, 5 and 6 | Every rule of the layout, broken alone, gives its finding naming its item and field, in one pass and in order; every wire format matches its literal; an address and a name refuse what their rules refuse; `Secret` prints no value |
| 2 | Client and testing | `04` PRT-LIB-003, PRT-LIB-004, PRT-LIB-005, PRT-LIB-008; `03` PRT-VLT-008; `05` PRT-OPS-001 (the client's keys), PRT-OPS-004 (the health check), PRT-OPS-005 (the client's logs); `06` PRT-ENG-008 (the fake agent) | A host registers with one call; a key outside its rule stops the start naming it; a read waits while the fake agent answers `waiting` or cannot be reached, says so, and answers the secret or the timeout within its bound; a denied address answers at once; the development file serves a Development host and is refused elsewhere; the fixed client serves a host's tests |
| 3 | The agent | `01` all; `03` PRT-ROT-003; `05` PRT-OPS-001 (the agent's keys and its file's safety), PRT-OPS-002, PRT-OPS-003 (criterion 2, the unit and its drop-in), PRT-OPS-005 (the agent's logs); `04` PRT-LIB-001 (the agent's row); `06` PRT-ENG-001 (`deploy/`, `tests/Portunus.Fakes` with the fake `op`), PRT-ENG-008 (the `linux` kind), PRT-ENG-012 (the agent's publish gate) | An agent refuses each start outside its rules; fetches once with `3 + n` reads and never again; waits with growing pauses, the cap on a rate limit, no retry of a broken layout; serves each process its declared addresses alone, on its own socket, refusing another user; keeps its directories across restarts; holds nothing on disk and zeroes what it replaces; stops cleanly |
| 4 | The push and the tool | `02` all; `04` PRT-LIB-001 (the tool's row); `06` PRT-ENG-001 (the fake `ssh`) | A push reads through the fake `op`, refuses a broken vault before `ssh` runs, reaches the agent through `receive`, fills a waiting agent, replaces a ready one only with `--replace`, refuses another vault's set, and shows no value; `check` and `status` answer as given; a `push` agent waits for a push after every start |
| 5 | The adapters | `00` PRT-OVR-002; `04` PRT-LIB-006, PRT-LIB-001 (the adapters' rows); `03` PRT-ROT-001, PRT-ROT-002; `06` PRT-ENG-001 (`vendor/`), PRT-ENG-009 (the siblings' packages) | Each sibling's `ISecretSource` answers its secrets from the fixed client in the sibling's own types and forms, its refusals naming the secret; a provider's item is read where configured; Janus gets its current version and Hermes every version in order |
| 6 | Release gate, sample host and release 1.0.0-rc.1 | `04` PRT-LIB-007 (the compatibility gate; criteria 1 and 2); `06` PRT-ENG-001 (the sample host, and its gate of PRT-ENG-012), PRT-ENG-011 (the release commit's checks, criteria 2 and 3; the release job, criterion 4); `04` PRT-LIB-001 for the sample host's row; `05` PRT-OPS-003 and `RELEASE-NOTES.md`; `08` PRT-THR-001, PRT-THR-002, PRT-THR-003, PRT-THR-005, the checklist attached; `00` PRT-OVR-003. The release commit `chore: release 1.0.0-rc.1` comes in the same pull request once the review has passed, on the lead's message, and the tag `v1.0.0-rc.1` after its merge | The sample host builds from the packages packed from the same commit and its tests pass in the gate; a release commit outside its rule fails the gate; version 1.0.0-rc.1 of every package, both agent executables, the unit files, the checklist and `SHA256SUMS` on the tag's release, marked a pre-release, with its changelog and release notes |

**Waiting on inputs.**
- Phase 0: the name the packages carry as their authors, which the lead's first message
  gives (`06` PRT-ENG-011).
- Phase 3: the fake `op`'s scenarios, recorded by the owner with `op` 2.41.1 from a test
  vault of dummy values and a test service account, which the lead's message hands over:
  the JSON of `op item list` and of `op item get` for items of each layout case, and the
  standard error and exit code of a rate-limited call, of a revoked token and of an
  unreachable network (P-005, P-006). No scenario holds a real secret.
- Phase 4: the owner's check by hand, on the owner's Windows PC (macOS and Linux named in
  the report as not yet checked by hand until the owner has one to check on), that a push
  prompts through the 1Password app and that none of its `op` commands writes a value to
  the terminal, named in the report as checked by the owner (P-009).
- Phase 5: the siblings' packages for `vendor/` at the versions of `07` section 7, which the
  lead's message names with their release tags (P-022).
- Phase 6's release commit and tag: the lead's message to release a release candidate,
  after the review; a final release, the owner's (P-017).

## 4. Phase report

`docs/reports/phase-NN.md`, holding only:
- the items built, each with its tests' names;
- the items not built, each with its reason, and the criteria proven and the behaviour
  built in a later phase;
- every criterion checked without a test, with how it was checked;
- every criterion that only the pipeline's run can check, named as awaiting that run,
  whose result the message naming the pull request gives;
- every analyser suppression, with its justification;
- every path that makes a `string` of a value or the token, with why (`06` PRT-ENG-005);
- **Resolved by rule**;
- **Open questions**: every question the phase asked, answered or not, with the items it
  parked and the entry that answered it;
- the commit the report names, the fast checks and the checks before the push, green, the
  gate of record being the pipeline's `gates` run on the report's own commit, whose
  identifier and result the run's last message gives (`06` PRT-ENG-012).
