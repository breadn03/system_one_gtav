import threading
import asyncio
import json
from concurrent.futures import ThreadPoolExecutor

from fastapi.testclient import TestClient
from fastapi import HTTPException
import pytest

from pursuit_service.app import create_app
from pursuit_service.contracts import DecisionResponse
from test_model import sample


class Engine:
    def __init__(self, entered=None, release=None):
        self.entered, self.release = entered, release

    def choose(self, snapshot):
        if self.entered:
            self.entered.set()
            assert self.release.wait(5)
        return DecisionResponse(session_id=snapshot.session_id, revision=snapshot.revision, candidate_id="A", probabilities={"A": 0.75, "B": 0.25}, inference_ms=1)


def test_service_returns_request_bound_choice_and_health():
    with TestClient(create_app(engine=Engine())) as client:
        assert client.get("/health").json()["status"] == "ready"
        result = client.post("/decide", json=sample().model_dump())
        assert result.status_code == 200
        assert result.json()["candidate_id"] == "A"
        assert result.json()["revision"] == 1


def test_service_rejects_invalid_snapshot():
    with TestClient(create_app(engine=Engine())) as client:
        assert client.post("/decide", json={"hidden_player_position": 1}).status_code == 422


def test_no_engine_returns_unavailable():
    with TestClient(create_app()) as client:
        assert client.get("/health").json()["status"] == "unavailable"
        assert client.post("/decide", json=sample().model_dump()).status_code == 503


def test_saturation_rejects_instead_of_queuing():
    entered, release = threading.Event(), threading.Event()
    with TestClient(create_app(engine=Engine(entered, release))) as client, ThreadPoolExecutor() as pool:
        first = pool.submit(client.post, "/decide", json=sample().model_dump())
        assert entered.wait(3)
        try:
            assert client.post("/decide", json=sample().model_dump()).status_code == 409
        finally:
            release.set()
        assert first.result().status_code == 200


def test_model_error_is_reported_without_killing_service():
    class FailingEngine:
        def choose(self, snapshot):
            raise ValueError("invalid choice")
    with TestClient(create_app(engine=FailingEngine())) as client:
        for _ in range(2):
            assert client.post("/decide", json=sample().model_dump()).status_code == 503


def test_repeated_request_cancellation_keeps_actual_worker_exclusive():
    async def scenario():
        entered, release = threading.Event(), threading.Event()
        app = create_app(engine=Engine(entered, release))
        endpoint = next(r.endpoint for r in app.routes if r.path == "/decide")
        request = asyncio.create_task(endpoint(sample()))
        assert await asyncio.to_thread(entered.wait, 3)
        request.cancel()
        await asyncio.sleep(0)
        request.cancel()
        await asyncio.sleep(0.05)
        try:
            with pytest.raises(HTTPException) as exc:
                await endpoint(sample())
            assert exc.value.status_code == 409
        finally:
            release.set()
            with pytest.raises(asyncio.CancelledError):
                await request
            await asyncio.sleep(0.1)
    asyncio.run(scenario())


def test_health_includes_pinned_identity():
    with TestClient(create_app(engine=Engine(), model_identity={"revision": "abc"})) as client:
        assert client.get("/health").json()["model"]["revision"] == "abc"


def test_nonstandard_json_numbers_are_rejected_cleanly():
    data = sample().model_dump()
    data["known_suspect"]["position"]["x"] = float("nan")
    with TestClient(create_app(engine=Engine()), raise_server_exceptions=False) as client:
        result = client.post("/decide", content=json.dumps(data), headers={"Content-Type": "application/json"})
        assert result.status_code == 400


def test_oversized_request_is_rejected_before_model():
    with TestClient(create_app(engine=Engine())) as client:
        assert client.post("/decide", content=b"x"*(1024*1024+1)).status_code == 413
