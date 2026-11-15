#!/usr/bin/env python3
"""Offline comparison of E-HUB mentor previews against independently reviewed labels.

No network or model calls. Inputs are preview JSON/API envelopes and a labels object:
{"semesterId":"<guid>","labels":[{"teamId":"<guid>","mentorType":"Enterprise",
"acceptableMentorIds":["<guid>"]}]}. Mark fictional examples with "synthetic": true.
Scores are descriptive ranking values, never evidence that semantic matching improved.
"""

from __future__ import annotations

import argparse
import json
import math
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from statistics import mean
from typing import Any
from uuid import UUID


class EvaluationError(ValueError):
    """A safe validation message that never includes input contents."""


ROLES = {"enterprise": "Enterprise", "academic": "Academic"}
SOURCES = {"Allocated", "Manual", "Retained"}
FIELDS = {"experience", "expertise", "startupDomain", "technology", "bio"}
SlotKey = tuple[str, str]


def guid(value: Any, field: str) -> str:
    try:
        parsed = UUID(value) if isinstance(value, str) else None
        if parsed is not None and parsed.int != 0:
            return str(parsed)
    except ValueError:
        pass
    raise EvaluationError(f"Invalid or missing GUID in {field}.")


def role(value: Any) -> str:
    if isinstance(value, str) and value.lower() in ROLES:
        return ROLES[value.lower()]
    raise EvaluationError("mentorType must be Enterprise or Academic.")


def array(document: dict, field: str) -> list:
    value = document.get(field, [])
    if not isinstance(value, list) or any(not isinstance(row, dict) for row in value):
        raise EvaluationError(f"{field} must be an array of objects.")
    return value


def finite_score(value: Any) -> float | None:
    if value is None:
        return None
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not 0 <= value <= 100 or not math.isfinite(value):
        raise EvaluationError("fitScore must be null or a finite number between 0 and 100.")
    return value


@dataclass(frozen=True)
class Slot:
    mentor_id: str | None
    class_id: str
    source: str
    score: float | None = None
    evidence: bool | None = None
    profile_coverage: float | None = None
    replacement_reason: bool = False


@dataclass(frozen=True)
class Preview:
    semester_id: str
    slots: dict[SlotKey, Slot]
    loads: dict[str, tuple[str, int]] | None
    existing_replaced_count: int


def parse_preview(document: Any) -> Preview:
    if not isinstance(document, dict):
        raise EvaluationError("Preview must be a JSON object.")
    if "semesterId" not in document and "data" in document:
        if document.get("success") is False or not isinstance(document["data"], dict):
            raise EvaluationError("API envelope must contain a successful preview.")
        document = document["data"]
    if "semesterId" not in document and "preview" in document:
        if document.get("status") != "Ready" or not isinstance(document["preview"], dict):
            raise EvaluationError("An allocation job must be Ready before evaluation.")
        document = document["preview"]
    semester = guid(document.get("semesterId"), "semesterId")
    slots: dict[SlotKey, Slot] = {}
    team_classes: dict[str, str] = {}

    def add(row: dict, source: str) -> None:
        key = (guid(row.get("teamId"), "teamId"), role(row.get("mentorType")))
        class_id = guid(row.get("classId"), "classId")
        if key in slots:
            raise EvaluationError("Duplicate final team/mentor-type slot in preview.")
        if team_classes.setdefault(key[0], class_id) != class_id:
            raise EvaluationError("A team cannot belong to different classes in one preview.")
        details = row.get("fitDetails")
        if details is not None and not isinstance(details, dict):
            raise EvaluationError("fitDetails must be null or an object.")
        evidence = details.get("hasEvidence") if details else None
        if evidence is not None and not isinstance(evidence, bool):
            raise EvaluationError("fitDetails.hasEvidence must be a boolean.")
        coverage = None
        if details is not None and "availableProfileFields" in details:
            available = details["availableProfileFields"]
            if not isinstance(available, list) or any(not isinstance(value, str) or value not in FIELDS for value in available):
                raise EvaluationError("availableProfileFields contains an invalid field.")
            if len(available) != len(set(available)):
                raise EvaluationError("availableProfileFields contains duplicate fields.")
            coverage = len(available) / len(FIELDS)
        reason = row.get("replaceReason")
        if reason is not None and not isinstance(reason, str):
            raise EvaluationError("replaceReason must be null or text.")
        slots[key] = Slot(
            None if source == "Unfilled" else guid(row.get("mentorProfileId"), "mentorProfileId"),
            class_id, source, finite_score(row.get("fitScore")), evidence, coverage,
            source == "Manual" and bool(reason and reason.strip()),
        )

    for row in array(document, "assignments"):
        source = row.get("source", "Allocated")
        if not isinstance(source, str) or source not in SOURCES:
            raise EvaluationError("Unknown assignment source.")
        add(row, source)
    replaced = 0
    existing_keys: set[SlotKey] = set()
    for row in array(document, "existingAssignments"):
        key = (guid(row.get("teamId"), "teamId"), role(row.get("mentorType")))
        if key in existing_keys:
            raise EvaluationError("Duplicate existing team/mentor-type slot in preview.")
        existing_keys.add(key)
        if not isinstance(row.get("replaced", False), bool):
            raise EvaluationError("existingAssignments.replaced must be a boolean.")
        if row.get("replaced", False):
            replaced += 1
        else:
            add(row, "Existing")
    for row in array(document, "unfilled"):
        add(row, "Unfilled")
    if not slots:
        raise EvaluationError("Preview contains no evaluable slots.")
    count = document.get("teamCount")
    if count is not None and (isinstance(count, bool) or not isinstance(count, int) or count != len(team_classes)):
        raise EvaluationError("teamCount does not match the exported slot scope.")
    for team in team_classes:
        if any((team, mentor_type) not in slots for mentor_type in ROLES.values()):
            raise EvaluationError("Preview must include assigned or unfilled slots for both mentor types.")

    loads = None
    if "mentorLoads" in document:
        loads = {}
        for row in array(document, "mentorLoads"):
            mentor = guid(row.get("mentorProfileId"), "mentorLoads.mentorProfileId")
            mentor_type = role(row.get("mentorType"))
            total = row.get("totalAfter")
            if isinstance(total, bool) or not isinstance(total, int) or total < 0:
                raise EvaluationError("mentorLoads.totalAfter must be a nonnegative integer.")
            if mentor in loads:
                raise EvaluationError("Duplicate mentor in mentorLoads.")
            loads[mentor] = (mentor_type, total)
        for (_, mentor_type), slot in slots.items():
            if slot.mentor_id is not None and slot.mentor_id in loads and loads[slot.mentor_id][0] != mentor_type:
                raise EvaluationError("An assigned mentor has a different type in mentorLoads.")
        scope_counts = Counter(slot.mentor_id for slot in slots.values() if slot.mentor_id is not None)
        if any(total < scope_counts[mentor] for mentor, (_, total) in loads.items()):
            raise EvaluationError("A semester mentor load cannot be smaller than their assignments in this scope.")
    return Preview(semester, slots, loads, replaced)


def parse_labels(document: Any, baseline: Preview, candidate: Preview) -> tuple[dict[SlotKey, set[str]], bool]:
    if not isinstance(document, dict):
        raise EvaluationError("Labels must be a JSON object.")
    if guid(document.get("semesterId"), "labels.semesterId") != baseline.semester_id:
        raise EvaluationError("Labels refer to a different semester.")
    synthetic = document.get("synthetic", False)
    if not isinstance(synthetic, bool):
        raise EvaluationError("labels.synthetic must be a boolean.")
    labels = {}
    for row in array(document, "labels"):
        key = (guid(row.get("teamId"), "labels.teamId"), role(row.get("mentorType")))
        if key in labels:
            raise EvaluationError("Duplicate label for a team/mentor-type slot.")
        if key not in baseline.slots or key not in candidate.slots:
            raise EvaluationError("A label refers to a slot outside the compared scope.")
        acceptable = row.get("acceptableMentorIds")
        if not isinstance(acceptable, list) or not acceptable:
            raise EvaluationError("Each label needs at least one acceptableMentorId.")
        ids = [guid(value, "labels.acceptableMentorIds") for value in acceptable]
        if len(set(ids)) != len(ids):
            raise EvaluationError("A label contains duplicate acceptable mentor IDs.")
        for preview in (baseline, candidate):
            if preview.loads is not None:
                if any(mentor not in preview.loads or preview.loads[mentor][0] != key[1] for mentor in ids):
                    raise EvaluationError("A label contains an unknown mentor or the wrong mentor type for this roster.")
        labels[key] = set(ids)
    return labels, synthetic


def ratio(numerator: int, denominator: int) -> float | None:
    return numerator / denominator if denominator else None


def gold_metrics(preview: Preview, labels: dict[SlotKey, set[str]], keys: set[SlotKey]) -> dict:
    matched = sum(preview.slots[key].mentor_id in labels[key] for key in keys)
    return {"labeledSlotCount": len(keys), "matchedSlotCount": matched, "matchRate": ratio(matched, len(keys))}


def preview_metrics(preview: Preview, labels: dict[SlotKey, set[str]]) -> dict:
    counts = Counter(slot.source for slot in preview.slots.values())
    allocated = [slot for slot in preview.slots.values() if slot.source == "Allocated"]
    scores = [slot.score for slot in allocated if slot.score is not None]
    evidence_known = [slot for slot in allocated if slot.evidence is not None]
    coverage_known = [slot.profile_coverage for slot in allocated if slot.profile_coverage is not None]
    review = sum(slot.score == 0 or slot.evidence is False for slot in allocated)
    load_ranges = {}
    for mentor_type in ROLES.values():
        values = [total for kind, total in (preview.loads or {}).values() if kind == mentor_type]
        load_ranges[mentor_type] = {
            "source": "semesterMentorLoads" if preview.loads is not None else "unavailable",
            "mentorCount": len(values) if preview.loads is not None else None,
            "minimum": min(values) if values else None,
            "maximum": max(values) if values else None,
            "range": max(values) - min(values) if values else None,
        }
    return {
        "slotCount": len(preview.slots), "filledSlotCount": len(preview.slots) - counts["Unfilled"],
        "assignmentCoverage": ratio(len(preview.slots) - counts["Unfilled"], len(preview.slots)),
        "unfilledSlotCount": counts["Unfilled"],
        "allocatedCount": counts["Allocated"], "manualCount": counts["Manual"],
        "retainedCount": counts["Retained"], "existingKeptCount": counts["Existing"],
        "existingReplacedCount": preview.existing_replaced_count,
        "manualReplacementReasonCount": sum(slot.replacement_reason for slot in preview.slots.values()),
        "needsReviewCount": review, "needsReviewRate": ratio(review, len(allocated)),
        "evidenceAssessmentKnownCount": len(evidence_known),
        "evidenceAssessmentUnknownCount": len(allocated) - len(evidence_known),
        "supportedAllocatedCount": sum(slot.evidence is True for slot in evidence_known),
        "evidenceSupportRateAmongKnown": ratio(sum(slot.evidence is True for slot in evidence_known), len(evidence_known)),
        "profileCoverageKnownCount": len(coverage_known),
        "profileCoverageUnknownCount": len(allocated) - len(coverage_known),
        "meanProfileFieldCoverageAmongKnown": mean(coverage_known) if coverage_known else None,
        "fitScoreCount": len(scores), "meanFitScoreDescriptiveOnly": mean(scores) if scores else None,
        "goldAllLabeledSlots": gold_metrics(preview, labels, set(labels)),
        "loadRangeByMentorType": load_ranges,
    }


def evaluate(baseline_document: Any, candidate_document: Any, labels_document: Any) -> dict:
    baseline, candidate = parse_preview(baseline_document), parse_preview(candidate_document)
    if baseline.semester_id != candidate.semester_id:
        raise EvaluationError("Previews refer to different semesters.")
    if baseline.slots.keys() != candidate.slots.keys() or any(
        baseline.slots[key].class_id != candidate.slots[key].class_id for key in baseline.slots
    ):
        raise EvaluationError("Previews contain different team, class or mentor-type scopes.")
    if baseline.loads is not None and candidate.loads is not None:
        if {key: value[0] for key, value in baseline.loads.items()} != {key: value[0] for key, value in candidate.loads.items()}:
            raise EvaluationError("Previews use different mentor rosters; compare the same eligible pool.")
    labels, synthetic = parse_labels(labels_document, baseline, candidate)
    paired = {key for key in labels if baseline.slots[key].source == candidate.slots[key].source == "Allocated"}
    old_gold, new_gold = gold_metrics(baseline, labels, paired), gold_metrics(candidate, labels, paired)
    status = "synthetic_demonstration" if synthetic else "labeled_sample_only"
    if not labels or not paired:
        status = "not_a_semantic_benchmark"
    return {
        "schemaVersion": 1, "semesterId": baseline.semester_id, "benchmarkStatus": status,
        "warnings": [
            "Synthetic labels demonstrate the tool only; they do not establish real matching quality." if synthetic else
            "Only independently reviewed labels can assess matching quality; this report describes the labeled sample.",
            "Higher fit scores do not prove better matching. Rates are fractions from 0 to 1, not calibrated confidence.",
            "Manual choices, retained mentors and existing assignments are counted separately from paired AI proposals.",
            "Replacement reasons are counted for later review; this tool does not train a model or include their text.",
        ] + (["No comparable labeled AI slots: this is not a semantic benchmark."] if not paired else [])
          + (["Mentor-pool comparability is unknown because a preview has no mentorLoads roster."]
             if baseline.loads is None or candidate.loads is None else []),
        "baseline": preview_metrics(baseline, labels), "candidate": preview_metrics(candidate, labels),
        "comparison": {
            "pairedAiLabeledSlotCount": len(paired), "baselinePairedAiGold": old_gold, "candidatePairedAiGold": new_gold,
            "pairedAiGoldMatchRateDelta": new_gold["matchRate"] - old_gold["matchRate"] if paired else None,
            "mentorChangedSlotCount": sum(baseline.slots[key].mentor_id != candidate.slots[key].mentor_id for key in baseline.slots),
        },
    }


def load_json(path: Path) -> Any:
    def pairs(items: list[tuple[str, Any]]) -> dict:
        result = {}
        for key, value in items:
            if key in result:
                raise EvaluationError("Input contains a duplicate JSON field.")
            result[key] = value
        return result

    def invalid_constant(_: str) -> None:
        raise EvaluationError("Input contains a non-finite JSON number.")

    with path.open(encoding="utf-8-sig") as stream:
        return json.load(stream, object_pairs_hook=pairs, parse_constant=invalid_constant)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    for argument in ("baseline", "candidate", "labels", "output"):
        parser.add_argument(f"--{argument}", required=True, type=Path)
    args = parser.parse_args(argv)
    try:
        inputs = (args.baseline, args.candidate, args.labels)
        if args.output.resolve() in {path.resolve() for path in inputs}:
            raise EvaluationError("Output must not overwrite an input file.")
        report = evaluate(*(load_json(path) for path in inputs))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    except (EvaluationError, json.JSONDecodeError, UnicodeError) as error:
        message = str(error) if isinstance(error, EvaluationError) else "Input is not valid UTF-8 JSON."
        parser.exit(2, f"Evaluation failed: {message}\n")
    except OSError as error:
        parser.exit(2, f"Evaluation failed: cannot read input or write report ({type(error).__name__}).\n")
    print(f"Report written: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
