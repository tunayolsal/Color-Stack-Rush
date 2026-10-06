import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import driver


def snapshot():
    return {"runId": 7, "snapshotId": 2, "phase": "Playing", "level": 1000, "profile": "normal",
            "observation": {"activeColor": "Pink", "playerX": 0, "blocks": 4, "visible": []}}


class DriverTests(unittest.TestCase):
    def test_only_visible_observation_is_sent(self):
        value = snapshot()
        payload = driver.build_payload(value)
        self.assertEqual(payload["model"], "jev-1.13.0")
        self.assertEqual(payload["state"], value["observation"])
        self.assertNotIn("profile", payload["state"])
        self.assertNotIn("level", payload["state"])
        value["observation"]["SafeX"] = 1.9
        with self.assertRaises(ValueError):
            driver.build_payload(value)

    def test_action_space_is_bounded(self):
        self.assertEqual(set(driver.ACTIONS), {"left_small", "left_large", "hold", "right_small", "right_large"})

    def test_api_error_has_no_retry_and_no_credential(self):
        with tempfile.TemporaryDirectory() as directory:
            worker = driver.Worker("never-log-this-credential", Path(directory))
            with patch.object(worker, "provider_request", side_effect=driver.URLError("timeout")) as call:
                result = worker.decide(snapshot())
            self.assertEqual(call.call_count, 1)
            self.assertEqual(result["error"], "provider_connection_or_timeout")
            self.assertNotIn("never-log-this-credential", (Path(directory) / "decisions.jsonl").read_text())

    def test_one_inflight_and_budget_prevent_calls(self):
        with tempfile.TemporaryDirectory() as directory:
            worker = driver.Worker("not-a-real-key", Path(directory), max_calls=1)
            worker.total_calls = 1
            with patch.object(worker, "provider_request") as call:
                self.assertEqual(worker.decide(snapshot())["error"], "decision_budget_exhausted")
                self.assertEqual(call.call_count, 0)
                worker.inflight.acquire()
                try:
                    self.assertEqual(worker.decide(snapshot())["error"], "another_decision_in_flight")
                finally:
                    worker.inflight.release()
                self.assertEqual(call.call_count, 0)

    def test_unexpected_model_is_not_substituted(self):
        class Response:
            def __enter__(self): return self
            def __exit__(self, *_): pass
            def read(self): return json.dumps({"model": "jev-ultrafast", "answers": {"steering": {"type": "choice", "choice": "hold"}}}).encode()
        with tempfile.TemporaryDirectory() as directory:
            worker = driver.Worker("not-a-real-key", Path(directory))
            with patch.object(worker, "provider_request", return_value=json.loads(Response().read())):
                result = worker.decide(snapshot())
            self.assertEqual(result["error"], "invalid_model_response")

    def test_persistent_connection_reused_without_retry(self):
        class Response:
            status, will_close = 200, False
            def read(self): return b'{"model":"jev-1.13.0"}'
        class Connection:
            calls = 0
            def request(self, *_args, **_kwargs): self.calls += 1
            def getresponse(self): return Response()
            def close(self): pass
        with tempfile.TemporaryDirectory() as directory:
            worker = driver.Worker("not-a-real-key", Path(directory))
            connection = Connection()
            with patch.object(driver.http.client, "HTTPSConnection", return_value=connection) as create:
                worker.provider_request({})
                worker.provider_request({})
            self.assertEqual(create.call_count, 1)
            self.assertEqual(connection.calls, 2)

    def test_relative_gap_presentation_rounded_and_whitelisted(self):
        value = snapshot()
        value["observation"]["visibleGaps"] = [{"side": "right", "relativeX": 3.80000019, "leftEdge": .6500009,
                                                   "rightEdge": 3.1500001, "ahead": 20.01111, "aligned": False}]
        value["observation"]["playerPosition"] = "left edge"
        state = driver.build_payload(value)["state"]
        self.assertEqual(state["visibleGaps"][0]["relativeX"], 3.8)
        value["observation"]["visibleGaps"][0]["suggestedAction"] = "right_large"
        with self.assertRaises(ValueError): driver.build_payload(value)

    def test_suite_result_and_minimal_status(self):
        with tempfile.TemporaryDirectory() as directory:
            worker = driver.Worker("not-a-real-key", Path(directory))
            worker.observe_suite(snapshot())
            worker.ack({"runId": 7, "snapshotId": 2, "applied": True, "action": "right_large"})
            self.assertEqual(worker.status()["current"], {"level": 1000, "profile": "normal"})
            self.assertEqual(worker.status()["lastAction"], "right_large")
            worker.suite_result({"model": "jev-1.13.0", "finished": True, "normalWins": 6, "runs": [{"level": 1}]})
            self.assertTrue(worker.status()["finished"])
            self.assertIsNone(worker.status()["current"])
            self.assertTrue((Path(directory) / "unity-summary.json").exists())


if __name__ == "__main__":
    unittest.main()
