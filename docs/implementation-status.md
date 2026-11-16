# Implementation status

The source prototype and packaging are implemented on `feature/laya-pursuits`. Actual in-game acceptance remains pending; no GTA installation files have been changed. This is not a claim that the requested improvement has been demonstrated.

| Plan task | Evidence | Remaining |
| --- | --- | --- |
| 1. Runtime | Legacy 1.0.3889.0 found; published Script Hook V support checked; matched SHVDN API compiles; pinned Laya checkpoint runs on RTX 3080 CUDA | Actual loader/game startup |
| 2. Contracts and knowledge | Shared fixtures, strict required fields, finite values, copied sightings, unit generations and complete-plan validation tested | Real observation realism |
| 3. Laya service | Bounded local HTTP service, token checks, real GPU smoke and 24-case benchmarks for two checkpoints | Better domain accuracy; simultaneous game load |
| 4. Scheduling | Bounded active request, coalescing, session isolation, meaningful revision, expiry, failure and cancellation checks | Delayed response during a live pursuit |
| 5. Game adapter | Compiled observer, route checks, candidate plans, deterministic fallback and coordinate tasks | All actual vehicle behavior and dispatch playtests |
| 6. Overlay | Compiled panel, actual destination/role labels, submitted alternatives, accepted-plan age, shared toggle and markers | Rendering, visibility and input verification in game |
| 7. Package | Packaging tests, offline suites and Release build; package excludes SDK/weights | In-game checklist and performance comparison |

## Decisions made during implementation

- Used a feature branch in the existing checkout to retain the approved documents, rather than creating a separate worktree. Cost: filesystem isolation is not provided.
- Used installed Python 3.11 in `.venv` instead of installing 3.12. Cost: this interpreter must be available when reproducing the environment.
- Defaulted to observation-only mode after the measured weak tactical choices. Cost: police behavior remains vanilla until experimental control is explicitly enabled in INI. This is preferable to silently describing an unvalidated model as an improvement.
- Retained multilingual as the initial configured checkpoint because its context accommodates larger team plans, while documenting that typed-decisions scored better overall on the small test. Cost: the default does not maximize measured synthetic accuracy, and neither checkpoint is established as best for pursuits.
- Used finite native road travel estimates plus reported-heading constraints to verify proposed interception routes; a full directed city map is not available in this implementation. Cost: unloaded nodes, overpasses, carriageway direction and intercept timing still need game tests.
- Used meaningful situation revisions: roster/contact/wanted/vehicle changes, accumulated 60-metre reported movement or a 30-degree reported turn invalidate a decision. Current route/unit references are also revalidated before accepting output. Cost: these initial thresholds may need tuning; a merely newer timestamp does not invalidate a still-feasible plan.
- Added deterministic assignment fallback when retained orders lose a unit/route or lack a required lead. Cost: fallback choices are scripted, not model decisions; the UI explicitly labels them.
- Consolidated the initial connected implementation into one feature commit rather than committing unbuildable intermediate files. Cost: history has less task-by-task granularity. Tests were still run in failing/passing stages during implementation.

## Independent review

A fresh reviewer inspected source/contracts/tests and identified six important issues: routine revision churn, repeated cancellation escaping exclusivity, absent deterministic fallback, throttled contact-loss transitions, stale driver/vehicle bindings, and missing suspect-to-intercept connectivity checks. They were addressed with regression coverage for the underlying logic and fresh full test/build runs. Actual native behavior is still subject to the playtest checklist.

The four initially minor findings were treated as required correctness/contract work: submitted/rejected options and accepted age are now retained; displayed destinations follow issued tasks; health reports pinned model identity; and missing required C# wire fields are rejected. The remaining limitations are game evidence and model quality, not hidden deferred review findings.

The reviewer did not judge game-native behavior, frame-time impact, or tactical generalization beyond measured cases; those remain explicitly unverified. Packaging/documentation were completed after the review and verified locally. The reviewer did not run the whole suite; the implementer did.

## Verification record

- C# unit suite: 33 passed, 0 failed.
- Python suite: 31 passed, 0 failed; one deprecation warning comes from the installed Starlette/AnyIO test-client integration.
- Legacy Release build: 0 warnings, 0 errors.
- Real C# `DecisionClient` -> loopback Python service -> Laya CUDA inference -> validated C# response: succeeded. First measured HTTP inference was approximately 409 ms / 473 ms end to end; synthetic warmed requests were around 45–50 ms without the game running.
- Model startup, weights, device and immutable revision were checked; no cloud inference was used.
- The temporary verification service was stopped afterwards.
- In-game loading, driving, overlay rendering, subjective fun and simultaneous-game performance: NOT RUN.

See [setup](setup.md), [measured model results](benchmarks/legacy-playtest.md) and [playtest checklist](playtest.md).
