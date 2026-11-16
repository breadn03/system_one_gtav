"""Explicit, real-model benchmark. No test double or cloud fallback."""
import argparse
import json
import os
import statistics
import time
from pathlib import Path

from .contracts import Snapshot
from .model import DecisionEngine


def main():
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser()
    parser.add_argument("--scenarios", type=Path, default=root / "benchmarks/scenarios.jsonl")
    parser.add_argument("--output", type=Path, default=root / "artifacts/laya-benchmark.json")
    parser.add_argument("--manifest", type=Path, default=root / "config/runtime-manifest.json")
    args = parser.parse_args()
    os.environ.setdefault("HF_HOME", str(root / "artifacts/huggingface"))
    os.environ.setdefault("USE_TF", "0")
    import torch
    rows = [json.loads(line) for line in args.scenarios.read_text().splitlines() if line.strip()]
    started = time.perf_counter()
    engine = DecisionEngine.from_manifest(args.manifest)
    load_ms = (time.perf_counter() - started) * 1000
    first_start = time.perf_counter()
    engine.choose(Snapshot.model_validate(rows[0]["snapshot"]))
    first_ms = (time.perf_counter() - first_start) * 1000
    if torch.cuda.is_available():
        torch.cuda.reset_peak_memory_stats()
    results = []
    for row in rows:
        snapshot = Snapshot.model_validate(row["snapshot"])
        started = time.perf_counter()
        try:
            answer = engine.choose(snapshot)
            state, _ = engine.encode(snapshot)
            results.append({"id": row["id"], "split": row["split"], "correct": answer.candidate_id in row["acceptable_ids"],
                            "response": answer.model_dump(), "elapsed_ms": (time.perf_counter()-started)*1000,
                            "state_tokens": len(engine.agent.tok.encode(state, add_special_tokens=False))})
        except Exception as exc:
            results.append({"id": row["id"], "split": row["split"], "correct": False, "error": str(exc), "elapsed_ms": (time.perf_counter()-started)*1000})
    times = sorted(r["elapsed_ms"] for r in results)
    held = [r for r in results if r["split"] == "heldout"]
    report = {"manifest": json.loads(args.manifest.read_text()), "torch": torch.__version__, "device": str(engine.agent.device),
              "load_ms": load_ms, "first_call_ms": first_ms, "cases": len(results),
              "accuracy": sum(r["correct"] for r in results)/len(results),
              "heldout_accuracy": sum(r["correct"] for r in held)/len(held) if held else None,
              "invalid_rate": sum("error" in r for r in results)/len(results),
              "p50_ms": statistics.median(times), "p95_ms": times[max(0, int(len(times)*.95+.999)-1)],
              "peak_allocated_mib": torch.cuda.max_memory_allocated()/1024**2 if torch.cuda.is_available() else None,
              "limitations": "Small synthetic scenarios with template variants; not an in-game tactical validation. Labels established before model results. Heldout families are lost-contact and blocked approaches.",
              "results": results}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, allow_nan=False)+"\n")
    print(json.dumps({k:v for k,v in report.items() if k not in ("results", "manifest")}, indent=2))


if __name__ == "__main__":
    main()
