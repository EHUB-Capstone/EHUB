import json
from pathlib import Path
import unittest

import train


class MentorTrainingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        folder = Path(__file__).resolve().parent
        cls.catalog = json.loads((folder / "topic-catalog.json").read_text(encoding="utf-8"))
        cls.curriculum = json.loads((folder / "curriculum.json").read_text(encoding="utf-8"))

    def rows(self):
        return train.bootstrap(self.catalog, self.curriculum)

    def test_all_project_versions_and_slots_stay_in_one_partition(self):
        rows = self.rows()
        duplicate = dict(rows[0], slot="IT", mentorRef="SIM-cross-slot")
        rows.append(duplicate)
        parts = train.split_projects(rows, 17)
        ids = [{r["projectId"] for r in part} for part in parts]
        self.assertFalse(ids[0] & ids[1] or ids[0] & ids[2] or ids[1] & ids[2])
        self.assertEqual(sum(len(part) for part in parts), len(rows))

    def test_unknown_grade_is_never_treated_as_negative(self):
        rows = self.rows()
        rows[0]["relevance"] = None
        with self.assertRaises(ValueError):
            train.validate_rows(rows)

    def test_whole_skill_names_preserve_programming_symbols(self):
        topics = [{"id": "java", "aliases": ["java"]}, {"id": "csharp", "aliases": ["c#"]},
                  {"id": "cpp", "aliases": ["c++"]}]
        self.assertEqual(train.topic_ids("JavaScript", topics), set())
        self.assertEqual(train.topic_ids("C# và C++", topics), {"csharp", "cpp"})
        self.assertEqual(train.topic_ids("Java.", topics), {"java"})

    def test_work_experience_is_separate_from_education(self):
        project = "Cần hỗ trợ marketing"
        education = train.extract_features(project, {"bio": "Được đào tạo về marketing"}, self.catalog)
        experience = train.extract_features(project, {"experience": "Phụ trách marketing"}, self.catalog)
        self.assertEqual(education[0], 0)
        self.assertEqual(education[4], 1)
        self.assertEqual(experience[0], 1)
        self.assertEqual(experience[4], 0)

    def test_private_and_workload_fields_cannot_affect_features(self):
        profile = {"experience": "Marketing"}
        extra = dict(profile, fullName="PRIVATE", email="private@example.test", activeTeamCount=99,
                     type="IT", organization="PRIVATE", years=90)
        self.assertEqual(train.extract_features("Marketing", profile, self.catalog),
                         train.extract_features("Marketing", extra, self.catalog))

    def test_no_evidence_cannot_receive_score_from_intercept(self):
        self.assertEqual(train.predict([0] * 7 + [1], [10] * 8, 10), 0)

    def test_negative_cv_and_stale_tag_do_not_provide_ai_evidence(self):
        for phrase in ("Không có kinh nghiệm AI, machine learning hoặc computer vision.",
                       "No experience with AI or computer vision."):
            features = train.extract_features("AI và computer vision", {
                "mentorType": "IT", "experience": phrase, "expertise": ["AI"]}, self.catalog)
            self.assertEqual(features[:7], [0] * 7)

    def test_project_exclusion_overrides_a_topic_in_the_title(self):
        features = train.extract_features("Không cần AI, cần backend.\nCổng AI", {
            "mentorType": "IT", "experience": "AI"}, self.catalog)
        self.assertEqual(features[:7], [0] * 7)

    def test_not_only_is_affirmative(self):
        self.assertIn("marketing", train.positive("Không chỉ marketing mà còn sales"))
        self.assertIn("marketing", train.positive("Not only marketing but also sales"))

    def test_vietnamese_who_owns_is_not_artificial_intelligence(self):
        self.assertNotIn("ai", train.topic_ids(train.positive("Cần làm rõ ai sở hữu mẫu tranh số"), self.catalog["topics"]))
        self.assertIn("ai", train.topic_ids(train.positive("AI sở hữu mô hình nhận diện hình ảnh"), self.catalog["topics"]))

    def test_longer_phrase_does_not_invent_react_web_expertise(self):
        topics = train.topic_ids("React Native", self.catalog["topics"])
        self.assertIn("react_native", topics)
        self.assertIn("mobile", topics)
        self.assertNotIn("react", topics)
        self.assertNotIn("frontend", topics)

    def test_business_description_does_not_invent_technical_need(self):
        features = train.extract_features("Nghiên cứu thị trường giáo dục", {
            "mentorType": "IT", "experience": "Ứng dụng di động giáo dục"}, self.catalog)
        self.assertEqual(features[:7], [0] * 7)

    def test_bootstrap_training_is_deterministic_and_does_not_claim_validation(self):
        first, report = train.train(self.rows(), self.catalog, epochs=5)
        second, _ = train.train(self.rows(), self.catalog, epochs=5)
        self.assertEqual(first, second)
        self.assertEqual(first["trainingSource"], "synthetic-bootstrap")
        self.assertFalse(first["suitabilityValidated"])
        self.assertEqual(report["test"]["rows"], sum(r["projectId"] in report["splitProjectIds"]["test"] for r in self.rows()))


if __name__ == "__main__":
    unittest.main()
