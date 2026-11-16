import copy
import json
from pathlib import Path

import pytest
from pydantic import ValidationError

from pursuit_service.contracts import Snapshot, DecisionResponse

EXAMPLES = Path(__file__).parents[2] / "contracts" / "examples"


def snapshot():
    return json.loads((EXAMPLES / "visible.json").read_text())


def test_shared_contract_examples():
    for name in ("visible", "lost-contact"):
        assert Snapshot.model_validate_json((EXAMPLES / f"{name}.json").read_text()).schema_version == 1
    with pytest.raises(ValidationError):
        Snapshot.model_validate_json((EXAMPLES / "invalid.json").read_text())


@pytest.mark.parametrize("mutation", ["duplicate", "unknown_unit", "unknown_destination", "unknown_role", "nan", "future_report", "extra_hidden_position", "unreachable", "incomplete", "duplicate_candidate"])
def test_invalid_plan_is_rejected(mutation):
    data = snapshot()
    assignment = data["candidates"][0]["assignments"][0]
    if mutation == "duplicate":
        data["candidates"][0]["assignments"].append(copy.deepcopy(assignment))
    elif mutation == "unknown_unit":
        assignment["unit_id"] = "missing"
    elif mutation == "unknown_destination":
        assignment["destination_node_id"] = "missing"
    elif mutation == "unknown_role":
        assignment["role"] = "teleport"
    elif mutation == "nan":
        data["known_suspect"]["position"]["x"] = float("nan")
    elif mutation == "future_report":
        data["known_suspect"]["observed_ms"] = 999999
    elif mutation == "extra_hidden_position":
        data["hidden_player_position"] = {"x": 1, "y": 2, "z": 3}
    elif mutation == "unreachable":
        data["roads"][0]["reachable_unit_ids"] = []
    elif mutation == "incomplete":
        data["candidates"][0]["assignments"] = []
    else:
        data["candidates"].append(copy.deepcopy(data["candidates"][0]))
    with pytest.raises(ValidationError):
        Snapshot.model_validate(data)


def test_response_rejects_nonfinite_probability():
    with pytest.raises(ValidationError):
        DecisionResponse(session_id="s", revision=1, candidate_id="p", probabilities={"p": float("nan")}, inference_ms=1)


def test_unverified_suspect_route_cannot_be_used_to_intercept():
    data = snapshot()
    data["roads"][0]["suspect_route_verified"] = False
    data["candidates"][0]["assignments"][0]["role"] = "intercept"
    with pytest.raises(ValidationError):
        Snapshot.model_validate(data)
