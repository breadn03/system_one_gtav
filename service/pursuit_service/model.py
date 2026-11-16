"""Laya boundary: bounded inputs, supplied plan choices, validated outputs."""
import json
import os
import time
from pathlib import Path

from .contracts import DecisionResponse, Snapshot


class BudgetError(ValueError):
    pass


class DecisionEngine:
    def __init__(self, agent, *, max_len: int, head_max_len: int):
        if not 0 < head_max_len < max_len <= 8192:
            raise ValueError("invalid context budgets")
        self.agent, self.max_len, self.head_max_len = agent, max_len, head_max_len

    @classmethod
    def from_manifest(cls, path: Path):
        from laya import Agent
        config = json.loads(path.read_text(encoding="utf-8-sig"))
        model = config["model"]
        if len(model["revision"]) != 40 or any(c not in "0123456789abcdef" for c in model["revision"]):
            raise ValueError("model must be pinned to a commit SHA")
        agent = Agent(model["repository"], subfolder=model["subfolder"] or None,
                      revision=model["revision"], device=config["device"])
        return cls(agent, max_len=model["max_len"], head_max_len=model["head_max_len"])

    def encode(self, snapshot: Snapshot):
        k = snapshot.known_suspect
        state = ["Police observations", f"Wanted stars: {snapshot.wanted_level}. Visual contact: {snapshot.visual_contact}.",
                 f"Last report age: {snapshot.captured_ms-k.observed_ms} ms; vehicle: {k.vehicle_description}; position {k.position.x:.0f},{k.position.y:.0f},{k.position.z:.0f}; heading {k.heading:.0f}; speed {k.speed:.0f} m/s."]
        for unit in snapshot.units:
            state.append(f"Unit {unit.id}: {unit.position.x:.0f},{unit.position.y:.0f},{unit.position.z:.0f}.")
        for road in snapshot.roads:
            state.append(f"Road {road.id}: {road.description}; {road.position.x:.0f},{road.position.y:.0f},{road.position.z:.0f}; reachable by {','.join(road.reachable_unit_ids)}.")
        criteria = {p.id: p.description + " " + "; ".join(f"{a.unit_id} {a.role} at {a.destination_node_id}" for a in p.assignments) for p in snapshot.candidates}
        questions = {"plan": {"type": "choice", "instructions": "Choose the most useful coordinated police vehicle plan using only the reports. Maintain contact when possible, use reachable interception routes, and avoid sending all units to follow directly.", "criteria": criteria}}
        return "\n".join(state), questions

    def choose(self, snapshot: Snapshot) -> DecisionResponse:
        state, questions = self.encode(snapshot)
        count = lambda text: len(self.agent.tok.encode(text, add_special_tokens=False))
        if count(state) + 8 > self.max_len - self.head_max_len:
            raise BudgetError("state exceeds configured context budget")
        # Conservative bound; the SDK's own usage checks below catch encoding-specific loss.
        option_budget = (self.head_max_len - count(questions["plan"]["instructions"]) - 32) // len(snapshot.candidates)
        if option_budget <= 0 or any(count(v) + 8 > option_budget for v in questions["plan"]["criteria"].values()):
            raise BudgetError("option descriptions exceed head budget")
        started = time.perf_counter()
        result = self.agent.predict(state, questions, max_len=self.max_len, head_max_len=self.head_max_len)
        elapsed = (time.perf_counter() - started) * 1000
        usage = result.get("usage", {})
        if usage.get("truncated") or usage.get("options"):
            raise BudgetError("Laya reported truncated state or indistinguishable options")
        answer = result["answers"]["plan"]
        response = DecisionResponse(session_id=snapshot.session_id, revision=snapshot.revision,
                                    candidate_id=answer["choice"], probabilities=answer["probabilities"], inference_ms=elapsed)
        return response.validate_for(snapshot)
