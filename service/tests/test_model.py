import json
from pathlib import Path

import pytest

from pursuit_service.contracts import Snapshot
from pursuit_service.model import DecisionEngine, BudgetError


def sample():
    path = Path(__file__).parents[2] / "contracts/examples/visible.json"
    return Snapshot.model_validate_json(path.read_text())


class Tokenizer:
    def encode(self, text, add_special_tokens=False):
        return list(text.split())


class Agent:
    tok = Tokenizer()

    def __init__(self, result=None):
        self.result = result or {"answers": {"plan": {"choice": "A", "probabilities": {"A": 0.75, "B": 0.25}}}, "usage": {"truncated": False}}

    def predict(self, state, questions, **kwargs):
        assert "Police observations" in state
        assert set(questions["plan"]["criteria"]) == {"A", "B"}
        return self.result


def test_returns_validated_choice_and_request_identity():
    result = DecisionEngine(Agent(), max_len=8192, head_max_len=1024).choose(sample())
    assert result.candidate_id == "A"
    assert result.session_id == "example-session"
    assert result.revision == 1
    assert result.probabilities == {"A": 0.75, "B": 0.25}
    assert result.inference_ms >= 0


@pytest.mark.parametrize("fault", ["unknown", "missing_probability", "nan", "truncation", "collapsed_options"])
def test_rejects_unusable_model_output(fault):
    agent = Agent()
    answer = agent.result["answers"]["plan"]
    if fault == "unknown":
        answer["choice"] = "C"
    elif fault == "missing_probability":
        del answer["probabilities"]["B"]
    elif fault == "nan":
        answer["probabilities"]["A"] = float("nan")
    elif fault == "truncation":
        agent.result["usage"]["truncated"] = True
    else:
        agent.result["usage"]["options"] = {"plan": {"total": 2, "distinct": 1}}
    with pytest.raises(ValueError):
        DecisionEngine(agent, max_len=8192, head_max_len=1024).choose(sample())


def test_rejects_state_and_option_overflow_instead_of_silent_truncation():
    with pytest.raises(BudgetError):
        DecisionEngine(Agent(), max_len=40, head_max_len=30).choose(sample())
    data = sample().model_dump()
    data["candidates"][0]["description"] = "long " * 250
    with pytest.raises(BudgetError):
        DecisionEngine(Agent(), max_len=8192, head_max_len=256).choose(Snapshot.model_validate(data))
