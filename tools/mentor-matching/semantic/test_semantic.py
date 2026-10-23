import json
from pathlib import Path
import sys
import unittest

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from server import SCHEMA, score, strings
from prepare_model import verify
from review import page, rows_from_reports


class FakeEncoder:
    identity = {"model": "test", "revision": "test"}

    def encode(self, texts):
        vectors = {"project one": [1, 0], "project two": [0, 1],
                   "one match": [1, 0], "both match": [0.7, 0.7], "unrelated": [-1, 0]}
        return np.asarray([vectors[text] for text in texts], dtype=np.float32)


class SemanticProtocolTests(unittest.TestCase):
    def payload(self):
        return {"schema": SCHEMA, "project": ["project one", "project two"],
                "candidates": [{"id": "0", "evidence": ["one match"]},
                               {"id": "1", "evidence": ["both match"]},
                               {"id": "2", "evidence": []}]}

    def test_agreement_with_two_project_excerpts_beats_one_generic_overlap(self):
        result = score(FakeEncoder(), self.payload())
        self.assertGreater(result["items"][1]["similarity"], result["items"][0]["similarity"])
        self.assertEqual(result["items"][2], {"id": "2", "similarity": 0.0, "evidenceIndex": -1})
        self.assertEqual(result["model"], "test")

    def test_duplicate_candidate_ids_are_rejected(self):
        payload = self.payload()
        payload["candidates"][1]["id"] = "0"
        with self.assertRaises(ValueError):
            score(FakeEncoder(), payload)

    def test_missing_schema_or_project_is_rejected(self):
        for payload in ({}, {"schema": SCHEMA, "project": [], "candidates": []}):
            with self.assertRaises(ValueError):
                score(FakeEncoder(), payload)

    def test_resource_limits_are_enforced_before_inference(self):
        for values in (["x" * 1201], ["x"] * 13, [None], "not-a-list"):
            with self.assertRaises(ValueError):
                strings(values, 12)
        payload = self.payload()
        payload["candidates"] *= 12
        with self.assertRaises(ValueError):
            score(FakeEncoder(), payload)

    def test_private_guid_candidate_ids_are_not_a_supported_protocol(self):
        payload = self.payload()
        payload["candidates"][0]["id"] = "guid-like-identity"
        with self.assertRaises(ValueError):
            score(FakeEncoder(), payload)

    def test_missing_model_file_fails_verification(self):
        self.assertFalse(verify(Path(__file__).with_name("missing-model.onnx"), "0" * 64))

    def test_blind_review_clears_existing_labels_and_escapes_script_content(self):
        rows = [{"projectId": "P1", "projectText": "</script><script>untrusted()</script>",
                 "mentorRef": "M1", "slot": "Business", "mentor": {}, "relevance": 3,
                 "isSynthetic": True, "labelSource": "model-guessed"}]
        html = page(rows)
        self.assertNotIn("<script>untrusted()", html)
        data = html.split("const rows=", 1)[1].split(";\nconst el=", 1)[0]
        parsed = json.loads(data)
        self.assertIsNone(parsed[0]["relevance"])
        self.assertEqual(parsed[0]["labelSource"], "pending-human-review")

    def test_automatic_review_report_import_cannot_export_production_cv_data(self):
        with self.assertRaises(ValueError):
            rows_from_reports([{"isSynthetic": False}])


if __name__ == "__main__":
    unittest.main()
