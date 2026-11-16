# Measured local model results

Measured on the user's RTX 3080 10 GB with CUDA-enabled PyTorch 2.9.1, Laya 0.4.34 and immutable model revision `e333cb7c823cd1787276228260f56d02fbf5180b`. GTA V was not running. These are small synthetic choice tests, not measurements of game behavior.

| Checkpoint | Correct / 24 | Held-out families / 8 | Median request | p95 request | Peak allocated GPU memory |
| --- | ---: | ---: | ---: | ---: | ---: |
| Multilingual, configured 8192 | 12 | 5 | 44.7 ms | 57.3 ms | 1511 MiB |
| Typed-decisions, configured 1024 | 15 | 7 | 50.3 ms | 62.9 ms | 2322 MiB |

No malformed/overflow responses occurred in these cases. First calls took approximately 432 ms and 457 ms; checkpoint initialization took approximately 6.9 seconds and 29 seconds respectively, with the latter including initial download/cache overhead. These startup numbers are not a controlled comparison. Allocated memory excludes other applications, driver/runtime overhead and the game.

Twenty-four examples cover six scenario templates with four variants each: lead selection, interception assignments, lost contact, lead replacement, congested approaches and spare-unit coverage. Options are alternated to reduce fixed-position bias. Lost-contact and congested-approach families were labelled held out before the first benchmark. Only eight held-out template variants are far too few to establish generalization, and labels express preferred tactics rather than a unique objectively correct outcome.

Both checkpoints remain weak for this task. Typed-decisions scored better overall but failed every spare-unit coverage example; multilingual achieved chance-level overall performance. Multilingual remains the default candidate because its larger context fits broader team snapshots, not because it proved tactically best. Do not infer that more context will fix these failures. The typed checkpoint also emitted an upstream temperature warning for 11+ options; these cases used two options, but probabilities should not be treated as domain-calibrated.

The prototype therefore starts in observation mode. Use logged examples to improve candidate encoding and collect domain labels; evaluate prompt changes on fresh held-out cases. Pursuit-specific fine-tuning may be required. Experimental control is available via INI for manual testing, with validation and fallbacks, but improved tactics have not been demonstrated.

Raw development reports are `artifacts/laya-benchmark.json` and `artifacts/laya-typed-benchmark.json` (ignored generated files). Reproduce using `python -m pursuit_service.benchmark`; for the typed comparison, use a local manifest with the same model revision, `subfolder="typed-decisions"`, `max_len=1024` and `head_max_len=512` via `--manifest` and a separate `--output`.

## Game playtests

Not run. The game has not been modified or launched by this project. Loader runtime compatibility, overlay visibility, actual routing/driving, pursuit enjoyment and simultaneous-game performance remain unverified. Follow `docs/playtest.md` and record evidence before claiming the project improves police behavior.
