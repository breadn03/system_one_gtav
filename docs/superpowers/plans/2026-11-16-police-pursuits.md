# Coordinated Police Pursuits Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Build a GTA V Legacy pursuit commander powered by local Laya, with an in-game decision panel and vehicle role markers controlled by one toggle.

**Architecture:** A C# script observes police knowledge, constructs feasible complete team plans, and sends immutable snapshots to a separate Python process. Laya chooses among those plans; the game validates and applies the selected assignments. Game-thread execution and rendering never wait for inference.

**Tech Stack:** C#, .NET Framework 4.8 game adapter, SHVDN v3 API, Python service, Laya package, PyTorch CUDA when compatible, loopback HTTP/JSON. Use a .NET Standard 2.0 pure core and .NET 8 tests to separate game dependencies from offline checks. Exact loader, Python and package versions are resolved and pinned in Task 1, not assumed compatible.

**Spec:** `docs/superpowers/specs/2026-11-16-police-pursuits-design.md`

## Global constraints

- GTA V Legacy single-player; initially no installed mods.
- RTX 3080, Ryzen 5600X and 32 GB RAM; benchmark alongside the game before promising response times.
- Wanted stars determine dispatch scale and vehicle types; do not replace dispatch or introduce a production unit cap.
- Predictions use sightings, radio reports and map knowledge, not hidden suspect position or future route.
- Local scripts execute immediate vehicle behavior. One active inference request; coalesce updates.
- Preserve valid assignments or fall back on inference failure. Discard obsolete responses.
- Pin Laya package version and model revision. No fork, vendored runtime or committed model weights.
- One toggle hides/shows both the panel and every vehicle marker; coordination continues while hidden.
- No on-foot tactics, dialogue, personalities, surrender, custom spawns, spike strips, helicopter tactics or roadblocks in this first delivery.
- Never label model probability as the measured probability of tactical success.

## Review focus

1. Hidden player turns after visual contact is lost must not alter the request until a new report arrives (Tasks 2 and 5).
2. A decision completed after a pursuit ends or an entity handle is reused must not affect a new pursuit (Tasks 2, 4 and 5).
3. Model downtime, malformed output and late responses must not stall the game or discard valid assignments (Tasks 3 and 4).
4. An intercept point near an overpass or opposing carriageway must not be assumed reachable from proximity alone (Task 5).
5. Toggling the overlay off during pending inference or unit despawn must hide all markers immediately (Task 6).

## File boundaries

- `contracts/pursuit.schema.json` and `contracts/examples/`: versioned wire contract and shared fixtures.
- `src/Pursuit.Core/`: pure observation memory, plan validation, asynchronous decision scheduling, display state; no GTA references.
- `src/Pursuit.Legacy/`: SHVDN entry point, observation adapter, route candidates, task executor, rendering.
- `service/pursuit_service/`: Python contract validation, Laya adapter, loopback HTTP and benchmark CLI.
- `tests/Pursuit.Core.Tests/`, `service/tests/`: automated behavior and cross-language fixture tests.
- `benchmarks/scenarios.jsonl`: human-labelled tactical examples with acceptable alternatives.
- `config/`: example configuration and exact dependency/model manifest; no machine-specific game path in Git.
- `docs/setup.md`, `docs/playtest.md`, `docs/benchmarks/`: installation, manual acceptance and measured evidence.

Builder tuning defaults: overlay initially hidden, configurable F9 toggle; 1 second periodic decision refresh and 2 second response expiry as starting values, not latency promises. Significant knowledge or availability changes invalidate pending plans; routine ticks do not. Use session-local monotonic elapsed milliseconds for report age and expiry. Tune after Task 7.

## Task 1: Establish a reproducible compatible runtime

**Files:** Create `docs/setup.md`, `config/runtime-manifest.json`, `.gitignore`; modify `README.md`.

**Interfaces:** Manifest records exact game build, matched Script Hook V/SHVDN release and API assembly hash, Python version, Laya/Torch/CUDA package versions, Hugging Face repository and immutable model revision, device and configured context limit. No silently floating versions.

- [ ] Inventory installed Python interpreters, .NET tooling and NVIDIA driver/VRAM. Current observed tools are Python 3.14.2 and .NET SDK 8.0.200; do not assume the Python environment supports all required wheels.
- [ ] Locate the user's Legacy installation using known launcher locations; if absent or unreadable, request the exact installation path/build. Read its executable version without modifying it.
- [ ] Verify the exact game build against official Script Hook V support and the selected SHVDN release. Use API DLLs and ASI loader from one matched release. Document manual installation and rollback steps; do not copy unrelated mods or modify game archives.
- [ ] In a repo-local virtual environment, resolve a compatible Python/Laya/PyTorch combination. Prefer a supported Python 3.12 interpreter if 3.14 dependency support fails; do not change the global interpreter. Download the selected checkpoint separately, record its revision, and run an actual CUDA choice request after warmup.
- [ ] Verify SHVDN assembly references expose required vehicle-task, entity, line-of-sight, road-node and drawing APIs. Record exact signatures or native hashes used in `docs/setup.md`. Treat unsupported route/task capabilities as a gate to revise Task 5, not permission to fake them.
- [ ] Record command outputs and exact versions in the manifest. Add ignore rules for `.venv`, model/cache directories, binaries, SDK downloads, logs, benchmark outputs and machine-specific configuration. Commit only tracked documentation/configuration when Git permissions allow.

**Acceptance:** A recorded local model choice request succeeds, and a documented loader/API pair supports the installed Legacy build. Offline Tasks 2–4 can proceed without game access, but game integration cannot be called validated until the game-side checks pass.

## Task 2: Define shared knowledge and complete candidate plans

**Files:** Create `contracts/pursuit.schema.json`, `contracts/examples/{visible,lost-contact,invalid}.json`, `src/Pursuit.Core/{Pursuit.Core.csproj,Contracts.cs,Knowledge.cs,PlanValidator.cs}`, `tests/Pursuit.Core.Tests/{Pursuit.Core.Tests.csproj,KnowledgeTests.cs,ContractTests.cs}` and `service/pursuit_service/contracts.py` with `service/tests/test_contracts.py`. Fold solution and Python project/test setup into this task.

**Interfaces:** `Knowledge.Update(Observation[] reports, long nowMs) -> KnownSuspect`; `PlanValidator.Validate(Snapshot snapshot, CandidatePlan plan) -> ValidationResult`. JSON schema version 1 defines `session_id`, `revision`, `captured_ms`, `wanted_level`, `known_suspect`, `units`, `candidates`. Unit identity combines a native handle with a session-local generation. Reports include observer identity, position, heading, speed, vehicle description and observation time. No live hidden-player field exists.

Each `CandidatePlan` has an opaque `id`, brief explanation of observable facts and an array of assignments `{unit_id, role, destination_node_id}`. Allowed roles are lead, support, intercept and contain; every assignment references a current eligible unit. A plan covers every managed unit exactly once. Intercept/contain destinations must be among supplied reachable road candidates. Reject rather than silently truncate oversized snapshots/options.

- [ ] Write failing tests named `HiddenMovementDoesNotChangeKnowledge`, `OlderReportCannotReplaceNewer`, `HandleReuseChangesIdentity`, `InvalidPlanIsRejected` and Python `test_shared_contract_examples`. Assert identical knowledge/requests for identical reports despite different hidden player routes; reject duplicate units, unknown roles/destinations and non-finite coordinates.
- [ ] Run `dotnet test tests/Pursuit.Core.Tests` and `python -m pytest service/tests/test_contracts.py`; confirm failures are missing behavior, then implement the listed interfaces and schema validation.
- [ ] Re-run both commands; all cases pass and both languages accept/reject the same shared examples. Commit the task when available.

## Task 3: Benchmark actual Laya decisions and expose a local service

**Files:** Create `service/pursuit_service/{model.py,app.py,benchmark.py}`, `service/tests/{test_model.py,test_app.py}`, `benchmarks/scenarios.jsonl`, `config/service.example.json` and dependency lockfiles under `service/`.

**Interfaces:** `DecisionEngine.choose(snapshot: Snapshot) -> DecisionResponse`. Response echoes `session_id` and `revision`, includes selected candidate ID, finite per-option probabilities, measured inference duration and status. `GET /health` reports loading/ready/failed plus pinned model identity; `POST /decide` accepts schema-v1 snapshots. Bind to `127.0.0.1:8765`, never all interfaces. No cloud fallback.

- [ ] Write failing tests for response IDs, unknown choices, malformed/non-finite output, insufficient token budget, unavailable model and service queue saturation. Use a fake engine for deterministic transport tests; add an opt-in real-model smoke test so fake results are never reported as model validation.
- [ ] Run `python -m pytest service/tests`; verify the expected missing behavior, then implement the adapter and HTTP endpoints. Load one checkpoint before ready. Use a single inference worker with bounded admission; reject concurrent work as busy instead of accumulating a queue. Apply token accounting to state plus option descriptions; report overflow explicitly.
- [ ] Add at least 24 human-labelled scenarios spanning lead replacement, feasible/infeasible intercepts, lost visual, blocked routes and additional units. Each case includes acceptable candidate IDs and report facts. Keep a held-out subset out of prompt tuning.
- [ ] Run `python -m pytest service/tests`; then `python -m pursuit_service.benchmark --scenarios ../benchmarks/scenarios.jsonl --output ../artifacts/laya-benchmark.json` from `service/`. Report warm/cold latency, p50/p95, token lengths, device, peak allocated GPU memory, accuracy and invalid-choice rate. Compare multilingual and typed-decisions using their actual supported budgets; overflow is a reported limitation, not a dropped case.
- [ ] Record the chosen checkpoint and limits in the manifest. Do not present the model as a competent tactical commander if it fails basic held-out cases: report results and revise the candidate encoding or propose fine-tuning before the live-control milestone. Commit code and a concise measured report; exclude weights.

## Task 4: Schedule decisions without blocking or applying stale orders

**Files:** Create `src/Pursuit.Core/{DecisionCoordinator.cs,DecisionClient.cs,DecisionTrace.cs}`, `tests/Pursuit.Core.Tests/DecisionCoordinatorTests.cs`.

**Interfaces:** `IDecisionClient.DecideAsync(Snapshot snapshot, CancellationToken cancellationToken) -> Task<DecisionResponse>`; `DecisionCoordinator.Update(Snapshot snapshot, long nowMs) -> void`; `TryTakeDecision(long nowMs) -> DecisionResponse`; `Reset(string sessionId) -> void`. `DecisionTrace` retains request options, response, elapsed time and acceptance/fallback status for UI/logging.

- [ ] Write failing tests with controllable completion: only one request active; latest update coalesced; timeout retains valid assignments; rejected/expired/session-mismatched results never apply; a previous session response cannot control reused entities. Ensure timeout does not create another concurrent model computation; honor server busy responses.
- [ ] Run `dotnet test tests/Pursuit.Core.Tests`; implement scheduling, HTTP transport and immutable trace snapshots without any GTA/native calls from worker threads.
- [ ] Re-run the suite. Exercise connection refusal and deliberately delayed HTTP responses; the update call must return without awaiting inference and outstanding work must remain bounded. Commit the task.

## Task 5: Connect observations, road candidates and vehicle tasks in Legacy

**Files:** Create `src/Pursuit.Legacy/{Pursuit.Legacy.csproj,PursuitScript.cs,PoliceObserver.cs,RoadCandidates.cs,CandidatePlans.cs,VehicleOrders.cs}`, `src/Pursuit.Core/AssignmentPolicy.cs`, `tests/Pursuit.Core.Tests/AssignmentPolicyTests.cs`, `config/Pursuit.example.ini`, `docs/playtest.md`.

**Interfaces:** `PoliceObserver.Capture(long nowMs) -> Observation[]`; `RoadCandidates.Find(KnownSuspect suspect, UnitState[] units) -> RoadCandidate[]`; `CandidatePlans.Build(KnownSuspect suspect, UnitState[] units, RoadCandidate[] roads, Assignment[] previous) -> CandidatePlan[]`; `VehicleOrders.Apply(CandidatePlan plan) -> ExecutionResult`. All game/native access stays on the SHVDN script thread.

- [ ] Write failing pure-core tests for invalidated routes, lead replacement, preserved valid assignments and distinct unit destinations. Test stacked roads using disconnected graph fixtures: geometric closeness must not establish reachability. Retain plans or deterministically select a feasible fallback when no model order is usable.
- [ ] Run `dotnet test tests/Pursuit.Core.Tests`; implement the pure assignment policy, then game adapters against the verified API. Read only sightings from eligible officers using validated visibility, distance and occlusion checks; game/player data used to establish a sighting cannot bypass that gate into shared knowledge.
- [ ] Manage actual available police drivers/vehicles; do not spawn replacements or intercept vanilla dispatch configuration. Track handles plus generations, dead/stuck/unavailable drivers and task ownership. Start with a controlled test pursuit, then naturally dispatched units across wanted levels; test setup size is not a production cap.
- [ ] Construct a small bounded set of complete plans, rather than asking independent conflicting per-car questions or enumerating every possible team combination. Include continuation, lead replacement and feasible intercept/contain alternatives where relevant. Use reachable road topology, direction and route travel estimates; if only nearest-node queries are available, disable unverified interception and document that limitation until connectivity is obtained.
- [ ] Execute supported drive/follow tasks, avoid resetting unchanged tasks each tick, keep support spacing and route destinations distinct. After contact loss, remove live entity-target chase tasks from managed units and use last-seen/predicted destinations. On failure retain valid orders or use last-report movement; vanilla behavior is acceptable only with verified visual contact. State explicitly that unmanaged vanilla units may retain engine knowledge.
- [ ] Build with `dotnet build src/Pursuit.Legacy -c Release`. In game, test a visible turn, a crash disabling the lead, loss/reacquisition and an unreachable intercept. Log decisions and actual applied tasks; success requires useful movement and no hidden-target tracking by managed units. Restore owned tasks/resources on unload and end sessions when wanted level reaches zero. Commit the task.

## Task 6: Add the shared overlay and vehicle-marker toggle

**Files:** Create `src/Pursuit.Core/OverlayState.cs`, `src/Pursuit.Legacy/PursuitOverlay.cs`, `tests/Pursuit.Core.Tests/OverlayStateTests.cs`; modify `PursuitScript.cs`, `Pursuit.example.ini` and `docs/playtest.md`.

**Interfaces:** `OverlayState.Toggle() -> void`; `OverlayState.Visible` defaults false. `PursuitOverlay.Draw(OverlayState state, DecisionTrace trace, UnitState[] units, Assignment[] acceptedAssignments) -> void` draws only while visible. Key-down toggles once per press; one shared visibility state controls all rendering.

- [ ] Write failing tests: toggle hides panel and markers together; hidden display never stops coordination; pending selection does not overwrite accepted role labels; despawn clears labels; no active pursuit has an explicit idle state.
- [ ] Run `dotnet test tests/Pursuit.Core.Tests`; implement display-state generation and non-blocking SHVDN rendering. Show contact/report age, wanted level, accepted unit roles/destinations, latest options and model probabilities, pending state, inference duration, plan age, applied/rejected order and fallback status. Use factual event labels, not invented model reasoning.
- [ ] Draw markers only for valid on-screen managed vehicles within a configurable range (initial 150 metres). Defaults are hidden and F9; support key/range configuration. Clamp/scroll the panel so additional units do not push critical status off-screen. No suspect marker based on hidden position.
- [ ] Re-run core tests and build. In game, toggle repeatedly during pending/failed inference and despawn; verify every marker disappears immediately with the panel and returns showing actual accepted roles. Include this overlay in the first playable package. Commit the task.

## Task 7: Package and compare coordinated pursuits against vanilla

**Files:** Create `scripts/package.ps1`, `docs/benchmarks/legacy-playtest.md`; modify `README.md`, `docs/setup.md`, `docs/playtest.md`, config defaults and tuning tests only when evidence justifies changes.

**Interfaces:** `scripts/package.ps1 -OutputDirectory <path>` assembles the mod, example config and service setup instructions within the workspace, without installing to a game directory or bundling game/SDK files or model weights.

- [ ] Run Python service tests, core tests and Release build once; package only after they pass. Validate package contents and configuration loading with missing/invalid local overrides.
- [ ] Run the game and Laya together on the stated PC. Record exact build, graphics settings, resolution, GPU memory usage, frame-time distribution and decision p50/p95; compare overlay hidden/visible and model disabled/enabled.
- [ ] Repeat freeway-exit, sudden-turn, blocked-route, crash, loss/reacquisition scenarios and normal wanted levels 1–5. Record available/managed unit counts, duplicate destinations, assignment churn, obstruction, successful lead replacement and feasible intercept execution. Confirm dispatch remains governed by stars.
- [ ] Run paired lost-contact trials with identical police reports and different hidden player routes; compare serialized snapshots and selected-plan inputs. Audit managed game tasks separately for hidden-target pursuit.
- [ ] Kill/restart the model service mid-pursuit, inject a delayed response, end/restart the pursuit and reload the script. Verify bounded requests, session isolation, valid fallback movement, immediate UI toggle and cleanup.
- [ ] Record subjective challenge/enjoyment feedback alongside telemetry. Tune periodic refresh, expiry, role spacing and candidate budgets from evidence. If speed or tactical quality fails, disclose it and revise rather than claiming success from offline tests.
- [ ] Document known limits, install/uninstall steps, checkpoint selection and reproduction commands. Commit when permissions allow; report any game checks that require user operation as unverified, not passed.

## Execution and evidence

Recommend inline execution because this is a single connected integration whose contracts must stay consistent. Finish each task's verification before the next dependent task. Game interaction and visual playtests may require the user; offline work continues independently until that evidence is needed. Current workspace permissions previously prevented `.git/index.lock` creation, so file edits can be saved even if commits cannot be made.

Author self-review: requirements map to Tasks 1–7; the five review-focus cases have explicit owning checks; shared interfaces and file responsibilities are defined above. Remaining uncertainties are explicit runtime and real-game evidence gates, not assumed capabilities. This is an implementation plan, not evidence that the software or model integration already works.

## Primary references

- Laya model/checkpoint APIs and limits: https://huggingface.co/convaiinnovations/laya
- Laya implementation and platform setup: https://github.com/NandhaKishorM/laya
- SHVDN loader/runtime requirements: https://github.com/scripthookvdotnet/scripthookvdotnet
- Script Hook V build compatibility: https://www.dev-c.com/gtav/scripthookv/
