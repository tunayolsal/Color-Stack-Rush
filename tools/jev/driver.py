#!/usr/bin/env python3
"""Local-only direct Jev AI worker. No browser/CDP automation and no credential output."""
from __future__ import annotations

import argparse
import json
import http.client
import os
import threading
import time
from collections import Counter
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.error import HTTPError, URLError

MODEL = "jev-1.13.0"
ENDPOINT = "https://api.typesafe.ai/v1/systemone"
ACTIONS = {
    "left_small": "Drag left by 0.55 world units over 200 ms.",
    "left_large": "Drag left by 1.35 world units over 200 ms.",
    "hold": "Do not drag; keep the current lateral target position, allowing existing smoothing to settle.",
    "right_small": "Drag right by 0.55 world units over 200 ms.",
    "right_large": "Drag right by 1.35 world units over 200 ms.",
}
OBSERVATION_FIELDS = {
    "activeColor", "upcomingColor", "upcomingSeconds", "powerUps", "playerX",
    "forwardSpeed", "horizontalLimit", "blocks", "score", "visible",
    "visibleGaps", "playerPosition",
}
OBJECT_FIELDS = {"kind", "color", "side", "x", "relativeX", "leftEdge", "rightEdge", "ahead", "width", "depth", "viewportX", "viewportY", "observedVelocityX"}
GAP_FIELDS = {"side", "description", "aligned", "leftEdge", "rightEdge", "centreX", "relativeX", "ahead", "playerCentreMin", "playerCentreMax"}
PROFILES = {"normal": 0.0, "extra_150ms": 0.15, "extra_300ms": 0.3, "one_bad_then_recover": 0.0}


def build_payload(snapshot: dict) -> dict:
    """Never send the wrapper's level, run ID, profile, seed or route to the model."""
    observation = snapshot.get("observation")
    if not isinstance(observation, dict) or set(observation) - OBSERVATION_FIELDS:
        raise ValueError("observation_schema")
    visible = observation.get("visible", [])
    if not isinstance(visible, list) or len(visible) > 32:
        raise ValueError("visible_schema")
    for item in visible:
        if not isinstance(item, dict) or set(item) - OBJECT_FIELDS:
            raise ValueError("visible_object_schema")
    gaps = observation.get("visibleGaps", [])
    if not isinstance(gaps, list) or len(gaps) > 32:
        raise ValueError("visible_gap_schema")
    for gap in gaps:
        if not isinstance(gap, dict) or set(gap) - GAP_FIELDS:
            raise ValueError("visible_gap_schema")
    return {
        "model": MODEL,
        "state": round_observation(observation),
        "questions": {"steering": {
            "type": "choice",
            "instructions": (
                "Choose the next short drag to survive Color Stack Rush. Use only the visible objects and current HUD state. "
                "The player automatically moves forward; decisions arrive about four times per second. "
                "Read `visibleGaps`: these are openings computed only between wall pieces visible in this frame. "
                "A gap's `side` is LEFT or RIGHT relative to the player's CURRENT position, or ALIGNED if the player already fits. "
                "Its `description` and `playerCentreMin`/`playerCentreMax` describe the visible space; they are not a hidden route. "
                "For an opening on your RIGHT choose a right drag; for one on your LEFT choose a left drag. "
                "When ALIGNED, prefer hold unless a closer visible object requires a change. "
                "`relativeX` and `side` on blocks are also relative to the player: a positive relativeX means RIGHT, negative means LEFT. "
                "A dark obstacle costs three blocks on contact. Choose a large drag early to reach a side opening, "
                "then a small correction or hold when aligned. Do not drag farther outward when already at an edge. "
                "Prioritize the closest obstacle ahead over farther objects. Matching-color blocks add one block; wrong colors remove two. "
                "An obstacle's observedVelocityX is its lateral motion measured across visible frames; allow extra clearance when it approaches your lane. "
                "Collect matching blocks when doing so does not collide with a closer obstacle. "
                "Do not pursue coins or powerups if that risks missing a necessary opening. "
                "Upcoming color only applies after its displayed countdown. Stay within the horizontal limit. "
                "Do not invent unseen objects or a hidden route. Choose hold if no visible hazard or helpful block needs steering."
            ),
            "criteria": ACTIONS,
        }},
    }


def round_observation(value):
    if isinstance(value, float):
        return round(value, 2)
    if isinstance(value, dict):
        return {key: round_observation(item) for key, item in value.items()}
    if isinstance(value, list):
        return [round_observation(item) for item in value]
    return value


def load_api_key(env_file: Path | None) -> str:
    key = os.environ.get("TYPESAFE_API_KEY", "").strip()
    if key:
        return key
    if env_file is not None:
        # Read a single credential into this process, never copy its file to the repo.
        for line in env_file.read_text(encoding="utf-8-sig").splitlines():
            name, separator, value = line.partition("=")
            if separator and name.strip() == "TYPESAFE_API_KEY":
                return value.strip().strip('"').strip("'")
    raise ValueError("TYPESAFE_API_KEY is not configured")


class Worker:
    def __init__(self, key: str, output: Path, timeout: float = 1.5, max_calls: int = 8192):
        self._key = key
        self.output = output.resolve()
        self.output.mkdir(parents=True, exist_ok=True)
        self.timeout, self.max_calls = timeout, max_calls
        self.inflight = threading.Lock()
        self.write_lock = threading.Lock()
        self.calls = Counter()
        self.total_calls = 0
        self.results: list[dict] = []
        self.last_acks: dict[int, dict] = {}
        self.connection = None
        self.status_lock = threading.Lock()
        self._status = {"model": MODEL, "current": None, "finishedRuns": 0, "finished": False, "normalWins": 0, "lastAction": "hold"}
        self.current_suite = None

    def provider_request(self, payload: dict) -> dict:
        if self.connection is None:
            self.connection = http.client.HTTPSConnection("api.typesafe.ai", timeout=self.timeout)
        try:
            self.connection.request("POST", "/v1/systemone", body=json.dumps(payload, separators=(",", ":")).encode(),
                                    headers={"Authorization": "Bearer " + self._key, "Content-Type": "application/json", "Connection": "keep-alive"})
            response = self.connection.getresponse()
            body = response.read()
            status = response.status
            if response.will_close:
                self.close_connection()
            if status < 200 or status >= 300:
                raise HTTPError(ENDPOINT, status, "Provider HTTP failure", None, None)
            return json.loads(body)
        except Exception:
            # Do not replay this frame. The NEXT fresh frame may reconnect.
            self.close_connection()
            raise

    def close_connection(self):
        if self.connection is not None:
            self.connection.close()
            self.connection = None

    def status(self) -> dict:
        with self.status_lock:
            return dict(self._status)

    def observe_suite(self, snapshot: dict) -> None:
        suite = snapshot.get("suiteId", "legacy")
        with self.status_lock:
            if suite != self.current_suite:
                self.current_suite = suite
                self.results = []
                self._status.update(finishedRuns=0, finished=False, normalWins=0, lastAction="hold")
            self._status["current"] = {"level": snapshot.get("level"), "profile": snapshot.get("profile")}

    def write(self, name: str, record: dict) -> None:
        with self.write_lock:
            with (self.output / name).open("a", encoding="utf-8") as handle:
                handle.write(json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n")

    @staticmethod
    def response(snapshot: dict, **values) -> dict:
        observation = snapshot.get("observation", {})
        return {"runId": snapshot.get("runId", -1), "snapshotId": snapshot.get("snapshotId", -1),
                "suiteId": snapshot.get("suiteId"),
                "phase": snapshot.get("phase", ""), "activeColor": observation.get("activeColor", ""),
                "action": "hold", "model": MODEL, "latencyMs": 0, "inputTokens": 0, "confidence": 0, "error": "", **values}

    def decide(self, snapshot: dict) -> dict:
        if not self.inflight.acquire(blocking=False):
            return self.response(snapshot, error="another_decision_in_flight")
        started = time.monotonic()
        result = self.response(snapshot)
        try:
            run_id = snapshot.get("runId")
            if not isinstance(run_id, int) or snapshot.get("phase") != "Playing" or snapshot.get("profile") not in PROFILES:
                raise ValueError("snapshot_schema")
            payload = build_payload(snapshot)
            self.observe_suite(snapshot)
            run_key = (snapshot.get("suiteId", "legacy"), run_id)
            if self.calls[run_key] >= 256 or self.total_calls >= self.max_calls:
                raise ValueError("decision_budget_exhausted")
            self.calls[run_key] += 1
            self.total_calls += 1
            # One request over a persistent TLS connection; never retry a stale frame.
            provider = self.provider_request(payload)
            answer = provider.get("answers", {}).get("steering", {})
            if provider.get("model") != MODEL or answer.get("type") != "choice" or answer.get("choice") not in ACTIONS:
                raise ValueError("invalid_model_response")
            result.update(action=answer["choice"], model=provider["model"], confidence=answer.get("confidence", 0),
                          inputTokens=provider.get("usage", {}).get("input_tokens", 0))
            additional = PROFILES[snapshot["profile"]]
            if additional:
                time.sleep(additional)
            result["probabilities"] = answer.get("probabilities", {})
        except HTTPError as error:
            result["error"] = "provider_http_" + str(error.code)
        except (TimeoutError, URLError, OSError, http.client.HTTPException):
            result["error"] = "provider_connection_or_timeout"
        except (ValueError, TypeError, KeyError) as error:
            result["error"] = str(error) if isinstance(error, ValueError) and str(error) in {
                "snapshot_schema", "observation_schema", "visible_schema", "visible_object_schema",
                "decision_budget_exhausted", "invalid_model_response", "visible_gap_schema"} else "invalid_response"
        except Exception:
            result["error"] = "worker_failure"
        finally:
            result["latencyMs"] = round((time.monotonic() - started) * 1000, 2)
            self.write("decisions.jsonl", {"snapshot": snapshot, "modelState": payload["state"] if "payload" in locals() else None, "answer": result})
            self.inflight.release()
        return result

    def ack(self, record: dict) -> None:
        self.last_acks[record.get("runId", -1)] = record
        self.write("actions.jsonl", record)
        if record.get("applied"):
            with self.status_lock:
                self._status["lastAction"] = record.get("action", "hold")

    def result(self, record: dict) -> None:
        self.results.append(record)
        self.write("runs.jsonl", record)
        normal = [item for item in self.results if item.get("profile") == "normal"]
        wins = [item for item in normal if item.get("completed")]
        with self.status_lock:
            self._status.update(finishedRuns=len(self.results), normalWins=len(wins))
        summary = {
            "model": MODEL, "runs": self.results, "apiCalls": self.total_calls,
            "inputTokens": sum(item.get("inputTokens", 0) for item in self.results),
            "normalWins": len(wins), "normalGoalMet": len(wins) >= 6 and {1, 4}.issubset({item.get("level") for item in wins}),
            "note": "Real Jev AI decisions from visible JSON. Not visual AI or evidence of human difficulty.",
        }
        (self.output / "driver-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"Jev run: level={record.get('level')} profile={record.get('profile')} win={record.get('completed')} "
              f"stars={record.get('stars')} hits={record.get('obstacleHits')} stale={record.get('stale')}", flush=True)

    def suite_result(self, record: dict) -> None:
        if record.get("model") != MODEL or not isinstance(record.get("runs"), list):
            raise ValueError("invalid_suite_result")
        self.write("suites.jsonl", record)
        (self.output / "unity-summary.json").write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
        with self.status_lock:
            self._status.update(current=None, finished=bool(record.get("finished")), finishedRuns=len(record["runs"]), normalWins=record.get("normalWins", 0))


def handler_factory(worker: Worker, web_root: Path | None):
    class Handler(SimpleHTTPRequestHandler):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, directory=str(web_root or worker.output), **kwargs)

        def log_message(self, *_):
            pass

        def do_GET(self):
            if self.path == "/api/health":
                self.send_json({"model": MODEL, "localOnly": True, "calls": worker.total_calls})
            elif self.path == "/api/status":
                self.send_json(worker.status())
            elif web_root is None:
                self.send_json({"error": "no_test_web_build_configured"}, 404)
            else:
                super().do_GET()

        def do_POST(self):
            # Browser test build, when used, must share this loopback origin.
            origin = self.headers.get("Origin")
            if origin is not None and origin != "http://" + self.headers.get("Host", ""):
                self.send_json({"error": "cross_origin_denied"}, 403)
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if length <= 0 or length > 65536:
                    raise ValueError()
                body = json.loads(self.rfile.read(length))
                if not isinstance(body, dict):
                    raise ValueError()
            except (ValueError, TypeError):
                self.send_json({"error": "invalid_request"}, 400)
                return
            if self.path == "/api/decision":
                self.send_json(worker.decide(body))
            elif self.path == "/api/action-ack":
                worker.ack(body)
                self.send_json({"ok": True})
            elif self.path == "/api/run-result":
                worker.result(body)
                self.send_json({"ok": True})
            elif self.path == "/api/suite-result":
                try:
                    worker.suite_result(body)
                    self.send_json({"ok": True})
                except ValueError:
                    self.send_json({"error": "invalid_suite_result"}, 400)
            else:
                self.send_json({"error": "not_found"}, 404)

        def send_json(self, body: dict, status: int = 200):
            content = json.dumps(body, separators=(",", ":")).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(content)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(content)
    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--env-file", type=Path, help="Optional existing ignored credential file, read only by this process")
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--port", type=int, default=8877)
    parser.add_argument("--timeout", type=float, default=1.5)
    parser.add_argument("--max-calls", type=int, default=8192)
    parser.add_argument("--web-root", type=Path, help="Optional local DEVELOPMENT test build; never use the public release build")
    args = parser.parse_args()
    try:
        key = load_api_key(args.env_file)
    except (ValueError, OSError):
        parser.error("TYPESAFE_API_KEY is unavailable; no API calls were made")
    worker = Worker(key, args.output, args.timeout, min(8192, args.max_calls))
    server = ThreadingHTTPServer(("127.0.0.1", args.port), handler_factory(worker, args.web_root))
    print(f"Direct Jev AI worker ready at http://127.0.0.1:{args.port}; model={MODEL}; max_calls={worker.max_calls}; retries=0", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        worker.close_connection()
        server.server_close()


if __name__ == "__main__":
    main()
