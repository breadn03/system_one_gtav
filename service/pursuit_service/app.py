import asyncio
import logging
import json
import os
import threading
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
from starlette.concurrency import run_in_threadpool

from .contracts import DecisionResponse, Snapshot
from .model import DecisionEngine

LOG = logging.getLogger("pursuit")


def create_app(*, engine=None, factory=None, model_identity=None):
    state = {"engine": engine, "status": "ready" if engine else "unavailable", "error": None}
    gate = threading.Lock()

    async def load():
        state["status"] = "loading"
        try:
            state["engine"] = await run_in_threadpool(factory)
            state["status"] = "ready"
        except Exception:
            LOG.exception("Laya initialization failed")
            state["error"] = "model initialization failed; see service log"
            state["status"] = "failed"

    @asynccontextmanager
    async def lifespan(app):
        task = asyncio.create_task(load()) if factory else None
        yield
        if task:
            await task

    app = FastAPI(title="Local Pursuit Commander", lifespan=lifespan)

    @app.middleware("http")
    async def bound_request(request: Request, call_next):
        if request.method == "POST":
            body = bytearray()
            async for chunk in request.stream():
                body.extend(chunk)
                if len(body) > 1024 * 1024:
                    return JSONResponse({"detail": "request too large"}, status_code=413)
            def reject_constant(value):
                raise ValueError("non-standard JSON number")
            try:
                json.loads(body, parse_constant=reject_constant)
            except ValueError:
                return JSONResponse({"detail": "invalid JSON"}, status_code=400)
            request._body = bytes(body)
        return await call_next(request)

    @app.get("/health")
    def health():
        return {"status": state["status"], "error": state["error"], "model": model_identity}

    @app.post("/decide", response_model=DecisionResponse)
    async def decide(snapshot: Snapshot):
        if state["engine"] is None:
            raise HTTPException(503, "model is " + state["status"])
        if not gate.acquire(blocking=False):
            raise HTTPException(409, "one inference already active")
        def compute():
            # The thread owns this lock until GPU work actually finishes, even if the
            # HTTP request or its asyncio wrapper is cancelled more than once.
            try:
                return state["engine"].choose(snapshot)
            finally:
                gate.release()

        try:
            worker = asyncio.create_task(run_in_threadpool(compute))
            worker.add_done_callback(lambda done: done.exception() if not done.cancelled() else None)
            result = await asyncio.shield(worker)
            return result.validate_for(snapshot)
        except Exception:
            LOG.exception("Pursuit decision rejected")
            raise HTTPException(503, "model decision failed; retain valid orders")

    return app


def main():
    import uvicorn
    root = Path(__file__).resolve().parents[2]
    os.environ.setdefault("HF_HOME", str(root / "artifacts/huggingface"))
    os.environ.setdefault("USE_TF", "0")
    manifest = Path(os.environ.get("PURSUIT_MANIFEST", str(root / "config/runtime-manifest.json")))
    metadata = json.loads(manifest.read_text(encoding="utf-8-sig"))
    app = create_app(factory=lambda: DecisionEngine.from_manifest(manifest), model_identity={"laya": metadata["laya"], **metadata["model"]})
    uvicorn.run(app, host="127.0.0.1", port=8765, workers=1)


if __name__ == "__main__":
    main()
