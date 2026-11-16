# Laya coordinated vehicle pursuits

Status: core design approved by the user; subsequent setup and overlay decisions recorded below.

## Intent and agreed behavior

Make GTA V vehicle pursuits more fun, realistic and challenging through coordinated tactics. Use a local System One model, Laya, as a shared commander. GTA V's wanted-star system continues to determine response scale and vehicle types; the commander coordinates the available units rather than replacing that progression.

Police predictions use sightings, shared radio reports and expected knowledge of the road map. They do not receive the player's hidden current position or future route.

The user selected a shared Laya commander with immediate scripted reactions in each vehicle. On-foot combat is outside this first project.

## Approaches

1. One commander controls broad assignments, with minimal local logic: consistent plans, but immediate reactions risk depending too heavily on model timing.
2. A separate model decision process for every car: individual initiative, but greater inference cost and conflicting assignments.
3. Shared commander plus local scripted reactions: coordinated assignments with responsive driving. Selected by the user; recommended because broad tactics and immediate vehicle control need different timing.

## Proposed behavior

Assign roles to eligible units: lead pursuit, supporting pursuit, interception, and containment of likely escape routes. Keep direct followers limited enough to leave manoeuvring space; additional units can cover routes rather than pile into the lead car. The exact limits are builder tuning decisions, not an agreed fixed number of police vehicles.

Reconsider assignments after significant events: a junction or route change is observed, visual contact is lost or regained, a unit becomes unavailable, or a route proves blocked. Also refresh plans periodically. Retain valid assignments to avoid repeated role switching.

When contact is lost, retain the last observed position, observation time, vehicle description and direction. Expand plausible routes as the report ages. Map knowledge supports route prediction but does not establish that the suspect took a particular route. New sightings correct the shared picture.

Example: a lead unit follows a suspect toward a freeway exit. A spare unit covers the exit while a supporting unit remains available to replace the lead. After losing visual contact, officers cover plausible routes from the last report instead of following hidden player coordinates.

## Decision and execution boundary

Scripts collect police observations and unit status, maintain shared knowledge, and supply a compact snapshot to Laya. The commander selects structured orders from an allowed vocabulary. Scripts validate those orders and translate accepted orders into supported vehicle tasks.

Local scripts own steering/task execution, immediate traffic and collision reactions, and safe continuation between model responses. Requested manoeuvres are bounded by what the chosen GTA integration can reliably execute; sophisticated driving is not assumed to follow automatically from a model order.

Validate unit identifiers, permitted roles, destinations, route feasibility and report freshness. Discard stale responses when a newer situation invalidates them. Limit inference to one active decision request with coalesced updates. If inference is slow, unavailable or invalid, preserve still-valid orders or fall back to ordinary pursuit behavior without blocking the game loop.

The model's pursuit snapshot must exclude hidden player information. Any engine-level fallback behavior must be checked for omniscient pursuit; after visual contact is lost, use last-seen/search tasks where supported. Known integration limits must be disclosed rather than claiming perfect information fairness.

## First prototype and subsequent scope

Builder proposal: begin with follow, support and intercept roles using a small controlled set of units to verify the command loop. This is a test setup, not a production cap. Then exercise star-dependent unit counts and containment behavior. Keep the game's existing dispatch/spawn progression initially. Do not introduce a separate spawn director, custom vehicle roster, roadblocks, spike strips or helicopter tactics in the first prototype.

## Constraints and discovery

Subsequent confirmed setup: GTA V Legacy single-player, initially no installed mods; RTX 3080, Ryzen 5600X and 32 GB RAM. Use Laya as a pinned package dependency with a pinned model revision, not a source fork or vendored repository. Keep downloaded weights out of Git. Proposed integration is a C# game mod and separate local Python decision process; exact compatible game, script loader and runtime versions remain to be verified. Evaluate multilingual with up to 8,192 tokens as a candidate, without treating context size as proof of tactical quality.

At the initial design discussion the repository contained only a README and license, and the integration/runtime/hardware were unknown. Subsequent setup is recorded above; current implementation and measured evidence are tracked in `docs/implementation-status.md`. Fast and tactically useful decisions during actual gameplay remain to be validated.

Before implementation planning can be finalized, establish the target game environment (single-player mod or a multiplayer framework), available game/mod versions, supported task and road-network APIs, and the local Laya runtime/interface and hardware. No framework or model capability is assumed by this design.

## Validation and success

Measure end-to-end decision latency under representative unit counts and game load, including high wanted levels. Select refresh intervals, snapshot size and timeouts from those measurements. The game must remain responsive when the model stalls.

Use repeatable pursuit scenarios: freeway exits, sudden observed turns, blocked approaches, lead-unit crashes, loss of sight, and reacquisition. Inspect actual assignments and vehicle behavior to verify distinct useful roles, successful lead replacement, feasible interception, limited role churn and fewer units obstructing one another.

Verify information fairness by changing the hidden player route after visual contact is lost: police orders must remain grounded in the same reports until new evidence arrives. Compare coordinated and ordinary pursuits in playtests for challenge and enjoyment; arrest rate alone is not sufficient.

## Builder decisions and non-goals

The builder chooses implementation language and framework after environment discovery, snapshot schema, action encoding, routing representation, model prompt or adapter, event thresholds, logging, tuning and practical integration fallbacks. Training or fine-tuning is not assumed necessary; evaluate the existing model first.

Non-goals: dialogue generation, officer personalities, surrender systems, on-foot tactics, and changes to wanted-star progression. Avoid perfect prediction, universal aggressive pursuit, excessive task churn, and promises about model speed without measurement.

## In-game decision overlay

The user requested a toggleable in-game UI showing police decisions from Laya, with markers on police vehicles visible only while that UI is on. One shared visibility setting controls the panel and every vehicle marker. Turning it off immediately hides both; pursuit coordination continues independently. Default hidden with a configurable toggle key is a builder proposal.

The panel should show police knowledge and last-report age, visual-contact status, unit roles and destinations, the options submitted to Laya, its selected option and returned probabilities, decision latency, plan age, order acceptance or rejection, and fallback status. Mark probabilities as model outputs, not validated tactical success rates. Label actual observations separately from predicted routes.

Vehicle markers identify managed units and their currently accepted roles, rather than a proposed role that has not been applied. Only draw for existing on-screen vehicles within a configurable display range; remove stale markers when a unit disappears. No markers are drawn while the panel is hidden. An expanded panel for alternatives is a builder proposal, not necessary for the initial overlay.

Use recorded request, response and execution data. Do not invent model explanations or present a generated narrative as internal reasoning. Track pending decisions separately from accepted assignments, and retain the last completed decision if a new request is running. The display must not block inference or game execution, and must not expose hidden suspect coordinates as police knowledge.

Validate toggling both panel and markers together, continuing pursuit with the UI hidden, unit despawn, off-screen units, no active pursuit, pending/failed inference, and stale or rejected orders. Include the overlay in the first playable prototype so assignments and execution can be inspected during testing.
