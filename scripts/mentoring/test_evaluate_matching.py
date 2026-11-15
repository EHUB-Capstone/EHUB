"""Standard-library tests; all fixtures are fictional and no network is used."""

import contextlib
import copy
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path


DIRECTORY = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("evaluate_matching", DIRECTORY / "evaluate-matching.py")
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class MatchingEvaluationTests(unittest.TestCase):
    def setUp(self):
        self.baseline = MODULE.load_json(DIRECTORY / "examples" / "matching-baseline.json")
        self.candidate = MODULE.load_json(DIRECTORY / "examples" / "matching-candidate.json")["data"]
        self.labels = MODULE.load_json(DIRECTORY / "examples" / "matching-labels.json")

    def evaluate(self):
        return MODULE.evaluate(self.baseline, self.candidate, self.labels)

    def test_fictional_example_compares_same_labeled_ai_slots(self):
        report = self.evaluate()
        self.assertEqual("synthetic_demonstration", report["benchmarkStatus"])
        self.assertEqual(2, report["comparison"]["pairedAiLabeledSlotCount"])
        self.assertEqual(0, report["comparison"]["baselinePairedAiGold"]["matchRate"])
        self.assertEqual(1, report["comparison"]["candidatePairedAiGold"]["matchRate"])
        self.assertEqual(1, report["comparison"]["pairedAiGoldMatchRateDelta"])
        self.assertEqual(2, report["comparison"]["mentorChangedSlotCount"])
        self.assertTrue(any("do not establish real matching quality" in warning for warning in report["warnings"]))

    def test_accepts_api_envelopes_and_ready_job_exports(self):
        api = {"success": True, "data": self.candidate}
        job = {"success": True, "data": {"status": "Ready", "preview": self.candidate}}
        self.assertEqual(self.evaluate(), MODULE.evaluate(self.baseline, api, self.labels))
        self.assertEqual(self.evaluate(), MODULE.evaluate(self.baseline, job, self.labels))

    def test_rejects_failed_api_or_unready_job(self):
        for document in ({"success": False, "data": self.candidate}, {"status": "Processing", "preview": self.candidate}):
            with self.subTest(document_type=list(document)), self.assertRaises(MODULE.EvaluationError):
                MODULE.evaluate(self.baseline, document, self.labels)

    def test_missing_fit_details_are_unknown_even_with_positive_scores(self):
        baseline = self.evaluate()["baseline"]
        self.assertEqual(0, baseline["evidenceAssessmentKnownCount"])
        self.assertEqual(2, baseline["evidenceAssessmentUnknownCount"])
        self.assertIsNone(baseline["evidenceSupportRateAmongKnown"])
        self.assertEqual(2, baseline["profileCoverageUnknownCount"])
        self.assertIsNone(baseline["meanProfileFieldCoverageAmongKnown"])
        self.assertEqual(55, baseline["meanFitScoreDescriptiveOnly"])

    def test_known_evidence_and_profile_coverage_use_their_own_denominators(self):
        self.candidate["assignments"][0]["fitDetails"] = {
            "hasEvidence": False, "availableProfileFields": ["experience", "technology"]
        }
        self.candidate["assignments"][1]["fitDetails"] = None
        metrics = self.evaluate()["candidate"]
        self.assertEqual(1, metrics["evidenceAssessmentKnownCount"])
        self.assertEqual(1, metrics["evidenceAssessmentUnknownCount"])
        self.assertEqual(0, metrics["evidenceSupportRateAmongKnown"])
        self.assertEqual(1, metrics["needsReviewCount"])
        self.assertEqual(0.5, metrics["needsReviewRate"])
        self.assertEqual(1, metrics["profileCoverageKnownCount"])
        self.assertEqual(1, metrics["profileCoverageUnknownCount"])
        self.assertEqual(0.4, metrics["meanProfileFieldCoverageAmongKnown"])

    def test_zero_legacy_score_requires_review_without_inventing_evidence(self):
        self.baseline["assignments"][0]["fitScore"] = 0
        metrics = self.evaluate()["baseline"]
        self.assertEqual(1, metrics["needsReviewCount"])
        self.assertEqual(2, metrics["evidenceAssessmentUnknownCount"])
        self.assertIsNone(metrics["evidenceSupportRateAmongKnown"])

    def test_manual_retained_and_existing_rows_are_separate_and_reason_text_is_not_exported(self):
        self.candidate["assignments"][0].update(source="Manual", replaceReason="PRIVATE_FEEDBACK_CONTENT")
        report = self.evaluate()
        metrics = report["candidate"]
        self.assertEqual(1, metrics["manualCount"])
        self.assertEqual(1, metrics["retainedCount"])
        self.assertEqual(1, metrics["existingKeptCount"])
        self.assertEqual(1, metrics["allocatedCount"])
        self.assertEqual(1, metrics["manualReplacementReasonCount"])
        self.assertEqual(1, report["comparison"]["pairedAiLabeledSlotCount"])
        self.assertNotIn("PRIVATE_FEEDBACK_CONTENT", json.dumps(report))

    def test_replaced_existing_slot_is_not_counted_twice(self):
        old = self.candidate["existingAssignments"][0]
        old["replaced"] = True
        replacement = copy.deepcopy(old)
        replacement.update(source="Manual", mentorProfileId=self.candidate["assignments"][2]["mentorProfileId"],
                           replaceReason="Synthetic replacement explanation")
        self.candidate["assignments"].append(replacement)
        self.candidate["mentorLoads"][2]["totalAfter"] = 2
        self.candidate["mentorLoads"][3]["totalAfter"] = 0
        metrics = self.evaluate()["candidate"]
        self.assertEqual(4, metrics["slotCount"])
        self.assertEqual(1, metrics["existingReplacedCount"])
        self.assertEqual(0, metrics["existingKeptCount"])
        self.assertEqual(1, metrics["manualCount"])

    def test_unfilled_slots_reduce_coverage_and_count_as_unmatched_gold_labels(self):
        row = self.candidate["assignments"].pop(0)
        self.candidate["unfilled"].append({key: row[key] for key in ("teamId", "classId", "mentorType")})
        self.candidate["mentorLoads"][1]["totalAfter"] = 0
        metrics = self.evaluate()["candidate"]
        self.assertEqual(1, metrics["unfilledSlotCount"])
        self.assertEqual(0.75, metrics["assignmentCoverage"])
        self.assertEqual(0.5, metrics["goldAllLabeledSlots"]["matchRate"])

    def test_empty_labels_do_not_become_a_semantic_benchmark(self):
        self.labels["labels"] = []
        self.labels["synthetic"] = False
        self.candidate["assignments"][0]["fitScore"] = 100
        report = self.evaluate()
        self.assertEqual("not_a_semantic_benchmark", report["benchmarkStatus"])
        self.assertIsNone(report["comparison"]["pairedAiGoldMatchRateDelta"])
        self.assertIsNone(report["baseline"]["goldAllLabeledSlots"]["matchRate"])
        self.assertTrue(any("Higher fit scores do not prove" in warning for warning in report["warnings"]))

    def test_labels_for_only_retained_rows_do_not_measure_ai_quality(self):
        self.labels["labels"] = [{"teamId": self.candidate["assignments"][2]["teamId"], "mentorType": "Academic",
                                  "acceptableMentorIds": [self.candidate["assignments"][2]["mentorProfileId"]]}]
        report = self.evaluate()
        self.assertEqual("not_a_semantic_benchmark", report["benchmarkStatus"])
        self.assertEqual(0, report["comparison"]["pairedAiLabeledSlotCount"])
        self.assertEqual(1, report["candidate"]["goldAllLabeledSlots"]["matchRate"])

    def test_unlabeled_rows_never_enter_the_gold_denominator(self):
        self.labels["labels"] = self.labels["labels"][:1]
        report = self.evaluate()
        self.assertEqual(1, report["baseline"]["goldAllLabeledSlots"]["labeledSlotCount"])
        self.assertEqual(1, report["comparison"]["pairedAiLabeledSlotCount"])

    def test_accepts_multiple_expert_approved_mentors(self):
        self.labels["labels"][0]["acceptableMentorIds"].append(self.baseline["assignments"][0]["mentorProfileId"])
        report = self.evaluate()
        self.assertEqual(0.5, report["baseline"]["goldAllLabeledSlots"]["matchRate"])
        self.assertEqual(1, report["candidate"]["goldAllLabeledSlots"]["matchRate"])

    def test_rejects_mismatched_semester_or_label_semester(self):
        other = "99999999-0000-0000-0000-000000000001"
        self.candidate["semesterId"] = other
        with self.assertRaisesRegex(MODULE.EvaluationError, "different semesters"):
            self.evaluate()
        self.candidate["semesterId"] = self.baseline["semesterId"]
        self.labels["semesterId"] = other
        with self.assertRaisesRegex(MODULE.EvaluationError, "different semester"):
            self.evaluate()

    def test_rejects_different_team_or_class_scopes(self):
        for row in self.candidate["assignments"] + self.candidate["existingAssignments"]:
            row["classId"] = "99999500-0000-0000-0000-000000000001"
        with self.assertRaisesRegex(MODULE.EvaluationError, "different team, class"):
            self.evaluate()
        self.setUp()
        removed = self.candidate["existingAssignments"].pop()["teamId"]
        self.candidate["assignments"] = [row for row in self.candidate["assignments"] if row["teamId"] != removed]
        self.candidate["teamCount"] = 1
        with self.assertRaisesRegex(MODULE.EvaluationError, "different team, class"):
            self.evaluate()

    def test_rejects_different_mentor_pools(self):
        self.candidate["mentorLoads"].append({"mentorProfileId": "99991001-0000-0000-0000-000000000001",
                                            "mentorType": "Enterprise", "totalAfter": 0})
        with self.assertRaisesRegex(MODULE.EvaluationError, "different mentor rosters"):
            self.evaluate()

    def test_rejects_duplicate_slots_and_overlapping_existing_rows(self):
        self.candidate["assignments"].append(copy.deepcopy(self.candidate["assignments"][0]))
        with self.assertRaisesRegex(MODULE.EvaluationError, "Duplicate final"):
            self.evaluate()
        self.setUp()
        self.candidate["existingAssignments"].append(copy.deepcopy(self.candidate["assignments"][0]))
        with self.assertRaisesRegex(MODULE.EvaluationError, "Duplicate final"):
            self.evaluate()

    def test_rejects_unknown_duplicate_or_wrong_type_gold_labels(self):
        for mutation in ("unknown-slot", "unknown-mentor", "duplicate-label", "wrong-type"):
            with self.subTest(mutation=mutation):
                self.setUp()
                if mutation == "unknown-slot": self.labels["labels"][0]["teamId"] = "99999999-0000-0000-0000-000000000001"
                elif mutation == "unknown-mentor": self.labels["labels"][0]["acceptableMentorIds"] = ["99999999-0000-0000-0000-000000000001"]
                elif mutation == "duplicate-label": self.labels["labels"].append(copy.deepcopy(self.labels["labels"][0]))
                else: self.labels["labels"][0]["acceptableMentorIds"] = [self.candidate["assignments"][2]["mentorProfileId"]]
                with self.assertRaises(MODULE.EvaluationError): self.evaluate()

    def test_rejects_invalid_scores_and_profile_coverage(self):
        for value in (float("nan"), float("inf"), -1, 101, True, "90", 10 ** 1000):
            with self.subTest(value_type=type(value).__name__):
                self.candidate["assignments"][0]["fitScore"] = value
                with self.assertRaises(MODULE.EvaluationError): self.evaluate()
        self.setUp()
        self.candidate["assignments"][0]["fitDetails"]["availableProfileFields"] = ["experience", "experience"]
        with self.assertRaises(MODULE.EvaluationError): self.evaluate()

    def test_rejects_inconsistent_semester_load_or_slot_types(self):
        self.candidate["mentorLoads"][0]["totalAfter"] = 0
        with self.assertRaisesRegex(MODULE.EvaluationError, "smaller than"):
            self.evaluate()
        self.setUp()
        self.candidate["assignments"][0]["mentorProfileId"] = self.candidate["assignments"][2]["mentorProfileId"]
        with self.assertRaisesRegex(MODULE.EvaluationError, "different type"):
            self.evaluate()

    def test_legacy_missing_roster_reports_unknown_loads(self):
        self.baseline.pop("mentorLoads")
        self.candidate.pop("mentorLoads")
        report = self.evaluate()
        self.assertEqual("unavailable", report["baseline"]["loadRangeByMentorType"]["Enterprise"]["source"])
        self.assertIsNone(report["baseline"]["loadRangeByMentorType"]["Enterprise"]["range"])
        self.assertTrue(any("comparability is unknown" in warning for warning in report["warnings"]))

    def test_json_loader_rejects_duplicate_keys_and_non_finite_numbers(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "synthetic.json"
            for content in ('{"field":1,"field":2}', '{"field":NaN}'):
                path.write_text(content, encoding="utf-8")
                with self.assertRaises(MODULE.EvaluationError): MODULE.load_json(path)

    def test_cli_writes_json_report_and_refuses_to_overwrite_inputs(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "report.json"
            arguments = ["--baseline", str(DIRECTORY / "examples" / "matching-baseline.json"),
                         "--candidate", str(DIRECTORY / "examples" / "matching-candidate.json"),
                         "--labels", str(DIRECTORY / "examples" / "matching-labels.json"), "--output", str(output)]
            with contextlib.redirect_stdout(io.StringIO()): self.assertEqual(0, MODULE.main(arguments))
            self.assertEqual("synthetic_demonstration", MODULE.load_json(output)["benchmarkStatus"])
            arguments[-1] = arguments[1]
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as result:
                MODULE.main(arguments)
            self.assertEqual(2, result.exception.code)

    def test_cli_validation_errors_never_echo_input_contents(self):
        self.baseline["assignments"][0]["mentorProfileId"] = "PRIVATE_INPUT_CONTENT"
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "preview.json"
            source.write_text(json.dumps(self.baseline), encoding="utf-8")
            stderr = io.StringIO()
            with contextlib.redirect_stderr(stderr), self.assertRaises(SystemExit):
                MODULE.main(["--baseline", str(source), "--candidate", str(source),
                             "--labels", str(DIRECTORY / "examples" / "matching-labels.json"),
                             "--output", str(Path(directory) / "report.json")])
            self.assertNotIn("PRIVATE_INPUT_CONTENT", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
