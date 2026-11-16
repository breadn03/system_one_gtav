# Setup: GTA V Legacy pursuit prototype

This is an experimental prototype, not yet verified inside GTA V. It starts in observation mode: the UI shows real Laya choices but vanilla police retain control. `ControlEnabled=true` opts into experimental vehicle orders. The measured model decisions are not yet reliably good enough to call this an improved police AI.

## Verified development setup

- Located Legacy executable: version 1.0.3889.0, Steam app 271590.
- RTX 3080 with 10240 MiB VRAM, NVIDIA driver 621.24; user-reported Ryzen 5600X / 32 GB RAM.
- Python 3.11 in `.venv`; PyTorch 2.9.1+cu128; Laya 0.4.34.
- Local multilingual checkpoint runs on CUDA at the exact revision in `config/runtime-manifest.json`.
- Compiles against SHVDN v3 API from `v3.7.0-nightly.206`. DLL SHA256 is recorded in the manifest. SHVDN requires .NET Framework 4.8 and the supported Visual C++ v14 redistributable. .NET 8 is used for development tests, not as the game's script runtime.

Sources: [Script Hook V build support](https://www.dev-c.com/gtav/scripthookv/), [SHVDN requirements and nightly compatibility](https://github.com/scripthookvdotnet/scripthookvdotnet), [pinned SHVDN archive](https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases/tag/v3.7.0-nightly.206), [Laya implementation](https://github.com/NandhaKishorM/laya), [Laya checkpoints](https://huggingface.co/convaiinnovations/laya).

## Python service

From this repository or unpacked package root, PowerShell:

```powershell
py -3.11 -m venv .venv
.venv/Scripts/python.exe -m pip install -r service/requirements.lock.txt --extra-index-url https://download.pytorch.org/whl/cu128
$env:PYTHONPATH = Join-Path $PWD 'service'
$env:HF_HOME = Join-Path $PWD 'artifacts/huggingface'
$env:USE_TF = '0'
.venv/Scripts/python.exe -m pursuit_service.app
```

The dependencies and weights already exist in the development checkout. Reuse its `.venv` rather than reinstalling. First use on another machine downloads the pinned model. Subsequent runs can use the cached checkpoint offline; `HF_HUB_OFFLINE=1` explicitly requests offline loading. Keep the service terminal open while playing. It binds only `127.0.0.1:8765`; there is one inference worker, no cloud fallback and no need for a Laya fork.

In a second terminal, `Invoke-RestMethod http://127.0.0.1:8765/health` returns `loading`, `ready` or `failed`. `ready` means weights loaded, not that tactics are accurate. Logs contain initialization failures. Ctrl+C stops the service; valid game orders remain in effect until replaced or the pursuit ends.

`PURSUIT_MANIFEST` can point to an alternate local manifest. Keep package and model revisions pinned. Multilingual supports the configured 8192 limit; actual snapshots are much shorter. The typed-decisions comparison uses `subfolder="typed-decisions"`, `max_len=1024`, `head_max_len=512`. That checkpoint has less capacity for large team plans. A response whose inputs/options exceed budgets is rejected rather than silently truncated.

## Build from source

The build references the downloaded SDK under `artifacts/sdk/shvdn`; these files are ignored, not vendored. If missing, download the linked matched SHVDN archive and extract there. Confirm the API DLL hash against the manifest. Do not mix ASI/API files from different release archives.

```powershell
dotnet build src/Pursuit.Legacy -c Release
powershell -NoProfile -File scripts/package.ps1
```

The package is written to `artifacts/package`; choose `-OutputDirectory artifacts/package-next` for another build because existing destinations are not overwritten. Package contents include the three mod DLLs, example INI, service source, pinned requirements and documentation, but no game SDK or model weights. The packaging script does not install anything into GTA V.

## Install into Legacy

Close GTA V first. Use this with single-player Legacy, not GTA Online or Enhanced. Work from a clean, backed-up install; keep a list of files you add so uninstalling removes only this mod's files.

1. Match Script Hook V to your exact executable build. The checked official release supports 1.0.3889.0. Copy its `ScriptHookV.dll` and supplied ASI loader into the folder containing `GTA5.exe`, following its README. The trainer is not needed.
2. Install the matched SHVDN archive's loader, internal DLL, v3 API DLL and configuration as directed by its README. The selected archive includes `ScriptHookVDotNet-Loader.asi`, `ScriptHookVDotNet.dll`, `ScriptHookVDotNet3.dll` and `ScriptHookVDotNet.ini`. Keep the archive's release files together. Older stable SHVDN 3.6.0 alone is not suitable for this game build.
3. Copy the package's `scripts` contents into the game's `scripts` folder: `Pursuit.Legacy.dll`, `Pursuit.Core.dll`, `Newtonsoft.Json.dll` and `Pursuit.ini`. Check before overwriting an existing shared dependency; this prototype was prepared for a clean install.
4. Start the Python service, wait for `/health` to say `ready`, then launch Legacy into single-player free roam. Get a wanted level while driving.
5. Press F9 to show the panel and vehicle markers together. Press it again to hide both. PageUp/PageDown page through unit rows. Change `ToggleKey` and `MarkerRange` in `Pursuit.ini` as needed.
6. Observation mode shows proposed decisions in the panel and labels vehicles `VANILLA`. To try applied assignments, set `ControlEnabled=true` in the game's `scripts/Pursuit.ini` and reload the script/restart the game. Applied markers show accepted roles. This mode is experimental; use the playtest checklist.

The project has not installed files into your game or launched it. Initial loader compatibility is checked from documented build support and compilation; successful in-game loading and behavior still need verification.

## What the prototype actually does

Observes loaded, siren-active police cars with living police drivers, retains units it has already observed, and shares gated line-of-sight reports. It uses one lead, at most one support vehicle and route coverage for spare units; wanted stars continue to determine vanilla dispatch. No global spawn or vehicle roster changes occur. The protocol has resource bounds (128 units / 64 sampled road nodes / up to 8 plans); these are not police spawn limits. Over-budget inference is rejected.

Road probes come from last sightings, heading, plausible forward/side routes and current police locations. Nearest nodes alone are insufficient: native road travel distance must be finite and plausible. Intercept destinations also need a plausible road route from the suspect's last reported position and compatible heading; other nodes remain containment destinations. Native routing depends on streamed road data, and this prototype does not have a complete city-wide directed junction graph, traffic prediction or guaranteed intercept timing. Route feasibility and opposite-carriageway behavior need in-game testing.

Accepted orders are coordinate driving tasks. They never pass a hidden player handle to a chase native. Lead/support targets follow shared observations and stop updating from hidden movement after contact is lost. When retained assignments become invalid or lack a lead after reacquisition, a deterministic feasible fallback replaces them without waiting for Laya. The panel distinguishes fallback execution from a model choice. Vanilla dispatch, unassigned officers, high-priority engine events and police outside the managed set can still use engine knowledge; perfect information fairness across every vanilla officer is not claimed. On foot, the mod ends its vehicle session and returns units to the game.

Decision logs are bounded to 8 MiB at `scripts/Pursuit.decisions.jsonl`. They contain observations, supplied options, model output and execution status, not invented internal reasoning. Copy logs for debugging; remove/archive them to resume logging after the bound is reached. A model probability is not a validated chance of success.

## Verify / uninstall

Use [playtest.md](playtest.md). For source tests:

```powershell
$env:PYTHONPATH = Join-Path $PWD 'service'
.venv/Scripts/python.exe -m pytest service/tests -q
dotnet test tests/Pursuit.Core.Tests
dotnet build src/Pursuit.Legacy -c Release
.venv/Scripts/python.exe -m pursuit_service.benchmark
```

The packaging test requires a Release build first. Read [measured results](benchmarks/legacy-playtest.md) before interpreting benchmark accuracy.

To uninstall, close the game and remove this mod's three DLLs, INI and log from `scripts`, then stop the service. Remove loader dependencies only if you installed them exclusively for this mod. No game archives, wanted-star settings or saves are modified by this project.
