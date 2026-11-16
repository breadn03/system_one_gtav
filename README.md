# System One GTA V

Experimental coordinated vehicle pursuits for GTA V Legacy using a local Laya decision model.

A C# mod shares police sightings, supplies feasible team plans and applies validated assignments. A separate local Python service runs Laya. F9 toggles the decision panel and vehicle markers together; hiding them does not stop coordination.

**Prototype status:** compiles and has offline tests, but has not been tested inside GTA V. Starts in observation mode because initial synthetic tactical accuracy was only 50% for multilingual and 62.5% for typed-decisions. GPU inference was approximately 45–50 ms median without the game running. Experimental control is an explicit INI option; improved police tactics are not yet demonstrated.

- [Setup, build and install](docs/setup.md)
- [Measured results and limitations](docs/benchmarks/legacy-playtest.md)
- [Game playtest checklist](docs/playtest.md)
- [Implementation status and decisions](docs/implementation-status.md)
- [Approved design](docs/superpowers/specs/2026-11-16-police-pursuits-design.md)
- [Implementation plan](docs/superpowers/plans/2026-11-16-police-pursuits.md)

Use the pinned Laya package and checkpoint revision in `config/runtime-manifest.json`. No fork or model weights are committed. Wanted stars still control vanilla police dispatch; the mod coordinates available vehicles.
