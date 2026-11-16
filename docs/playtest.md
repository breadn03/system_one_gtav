# In-game acceptance checklist

Status: NOT RUN. Build/test success is not game validation. Start with observation mode and free roam, with other police mods absent. Record executable/loader versions, model revision, graphics settings and resolution.

- [ ] Confirm SHVDN loads `Pursuit.Legacy` without errors in its log.
- [ ] With no wanted level, F9 shows an idle panel; no stale vehicle markers remain.
- [ ] During a vehicle pursuit, F9 shows/hides the panel and every vehicle marker together. Repeat during pending inference and after a unit disappears. A held key must not flicker the display.
- [ ] Observation mode leaves vanilla tasks alone and labels cars `VANILLA`; proposed decisions are not represented as applied roles.
- [ ] With experimental control enabled, one lead and at most one support are assigned, and spare units move toward different reachable routes. Check actual destinations, not just model selections.
- [ ] Crash/disable the lead; verify replacement and disappearance of its role marker.
- [ ] Drive through junctions and freeway exits; check route choice, task persistence and spacing. Test opposing carriageways and stacked roads. Unsupported or unverified routes must not be accepted as reachable solely because they are nearby.
- [ ] Lose contact, turn unseen, and compare with a repeat following the same observed route but a different hidden turn. Inspect requests for identical police knowledge, allowing police positions/time to be controlled or normalized. Audit managed tasks for use of fixed last-seen/predicted coordinates.
- [ ] Regain contact; police update from the new report rather than remaining stuck on the old road.
- [ ] Stop/restart the service mid-pursuit; game frames continue, valid orders persist and failures are visible. A late response must not apply after wanted level reaches zero and a new pursuit starts.
- [ ] Test wanted levels 1–5 with naturally dispatched units. Record available and managed units, obstruction, duplicate route assignments and role switching. The mod must not change dispatch progression.
- [ ] Leave the vehicle, die, end a pursuit and reload the script; owned tasks/event flags are released, with no stale assignments in the next session.
- [ ] Compare frame times and GPU memory with model off/on and UI hidden/visible. Record warm p50/p95 decision times under simultaneous game load.
- [ ] Compare fun, challenge and ability to escape with vanilla; a higher arrest rate alone is not evidence of better behavior.

Retain decision logs for each case, clearly noting failures and the extent of unmanaged vanilla police knowledge. Tune only after observing actual behavior. Do not mark this checklist passed from offline tests.
