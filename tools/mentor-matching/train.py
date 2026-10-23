"""Train a portable CPU mentor ranker. No cloud calls or real CVs are read by --bootstrap."""

import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import re
import time
import unicodedata

HERE = Path(__file__).resolve().parent
FEATURES = [
    "experience_topic_coverage", "expertise_topic_coverage", "domain_topic_coverage",
    "technology_topic_coverage", "bio_topic_coverage", "experience_token_coverage",
    "profile_token_coverage", "project_topic_count_normalized",
]


def normalize(value):
    value = value.replace("đ", "d").replace("Đ", "d").lower()
    value = "".join(c for c in unicodedata.normalize("NFD", value) if unicodedata.category(c) != "Mn")
    return " ".join(value.split())


def evidence_text(text):
    text = re.sub(r"\b(?:ai|Ai)(?=\s+(?:sở hữu|được dùng|được sử dụng|sẽ mua|chịu trả))", "người nào", text or "")
    value = normalize((text or "").replace("\r", ";").replace("\n", ";"))
    value = re.sub(r"\b(?:khong chi|khong nhung|not only)\b", " ", value)
    clauses = re.split(r"[;\r\n!?]+|\.(?=\s|$)|\b(?:nhung|tuy nhien|but|however|instead|ma can|chi can|va can)\b|,(?=\s*(?:can|need|co kinh nghiem|experienced)\b)", value)
    result = []
    excluded = []
    for clause in clauses:
        if re.search(r"\b(?:nam ngoai (?:nhu cau|pham vi)|ngoai pham vi|out of scope|not required|not needed|khong (?:can thiet|duoc yeu cau))\b", clause):
            excluded.append(clause)
            continue
        denial = re.search(r"\b(?:khong|chua|chang|no|not|without|never|lack|lacks|lacking)\b", clause)
        affirmative = clause[:denial.start()] if denial else clause
        if denial:
            excluded.append(clause[denial.start():])
        if affirmative.strip():
            result.append(affirmative.strip())
    return " ".join(result), " ".join(excluded)


def positive(text):
    return evidence_text(text)[0]


def tokens(text, stop_words):
    result = []
    seen = set()
    for match in re.finditer(r"[a-z0-9+#.]+", normalize(text)):
        token = match.group().strip(".")
        if (len(token) < 3 and token not in ("c#", "c++")) or token in stop_words:
            continue
        if token in seen or not re.search(r"[a-z0-9]", token):
            continue
        result.append(token)
        seen.add(token)
        if len(result) == 256:
            break
    return set(result)


def topic_ids(text, topics):
    text = normalize(text)
    occurrences = [(topic["id"], match.start(), match.end()) for topic in topics for alias in topic["aliases"]
                   for match in re.finditer(r"(?<![a-z0-9+#.])" + re.escape(normalize(alias)) + r"(?![a-z0-9+#])", text)]
    matches, farthest_end = set(), -1
    for id, start, end in sorted(occurrences, key=lambda item: (item[1], -item[2])):
        if end <= farthest_end:
            continue
        matches.add(id)
        farthest_end = end
    return matches | {topic["parent"] for topic in topics if topic["id"] in matches and topic.get("parent")}


def clipped(value, limit):
    # Match .NET UTF-16 clipping, including non-BMP characters.
    encoded = (value or "").encode("utf-16-le", errors="surrogatepass")[:limit * 2]
    if encoded and 0xD800 <= int.from_bytes(encoded[-2:], "little") <= 0xDBFF:
        encoded = encoded[:-2]
    return encoded.decode("utf-16-le", errors="surrogatepass")


def joined(values):
    return clipped(" ".join(clipped((x or "").strip(), 256) for x in values[:64]), 6000)


def without_excluded_topics(text, excluded, catalog):
    for topic in catalog["topics"]:
        if topic["id"] not in excluded:
            continue
        for alias in sorted((normalize(a) for a in topic["aliases"]), key=len, reverse=True):
            text = re.sub(r"(?<![a-z0-9+#.])" + re.escape(alias) + r"(?![a-z0-9+#])", " ", text)
    return text


def extract_features(project, mentor, catalog):
    project, excluded_project = evidence_text(clipped(project, 6000))
    entries = mentor.get("experiences", [])[:64]
    experience = clipped("\n".join([clipped(mentor.get("experience"), 6000)] + [
        clipped(e.get("area"), 256) + " " + clipped(e.get("notes"), 2000) for e in entries]), 6000)
    raw_fields = [experience, joined(mentor.get("expertise", []) + mentor.get("tags", [])),
              joined(mentor.get("startupDomains", [])), joined(mentor.get("technologySkills", [])),
              clipped(mentor.get("bio"), 6000)]
    fields = [positive(field) for field in raw_fields]
    role = mentor.get("mentorType", "Business")
    allowed = {t["id"] for t in catalog["topics"] if t.get("role", "Both") in (role, "Both", "Domain")}
    project_topics = (topic_ids(project, catalog["topics"]) - topic_ids(excluded_project, catalog["topics"])) & allowed
    core = {t["id"] for t in catalog["topics"] if t["id"] in project_topics and t.get("role") != "Domain"}
    if not core and role == "Business":
        core = project_topics
    denied = set().union(*(topic_ids(evidence_text(raw_fields[i])[1], catalog["topics"]) for i in (0, 4)))
    field_topics = [topic_ids(field, catalog["topics"]) - denied for field in fields]
    coverage = [len(project_topics & topics) / len(project_topics) if project_topics else 0.0 for topics in field_topics]
    stops = {normalize(word) for word in catalog["stopWords"]}
    project_tokens = tokens(without_excluded_topics(project, topic_ids(excluded_project, catalog["topics"]), catalog), stops)
    fields = [without_excluded_topics(field, denied, catalog) for field in fields]
    for field in (fields[0], " ".join(fields)):
        coverage.append(len(project_tokens & tokens(field, stops)) / len(project_tokens) if project_tokens else 0.0)
    if not core & set().union(*field_topics):
        coverage = [0.0] * 7
    return coverage + [min(len(project_topics) / 8.0, 1.0)]


def bootstrap(catalog, curriculum):
    """Explicitly simulated relevance, not empirical suitability or lecturer judgment."""
    topics = {t["id"]: t for t in catalog["topics"]}
    business = [key for key, topic in topics.items() if topic["role"] in ("Business", "Both")]
    technical = [key for key, topic in topics.items() if topic["role"] in ("IT", "Both")]
    rows = []
    for index, scenario in enumerate(curriculum["scenarios"]):
        needs = scenario["needs"]
        pool = business if scenario["slot"] == "Business" else technical
        domain = scenario.get("domain")

        def phrase(topic, offset=0):
            aliases = topics[topic]["aliases"]
            return aliases[(index + offset) % len(aliases)]

        project = scenario["context"] + " Nhu cầu cố vấn: " + "; ".join(phrase(t) for t in needs) + "."
        for candidate_index, candidate in enumerate(pool):
            # A named stack can satisfy a general backend need; a general backend
            # profile is only partial evidence when a specific stack is required.
            relevant = candidate in needs or topics[candidate].get("parent") in needs
            partial = candidate in {topics[need].get("parent") for need in needs}
            mentor = {"mentorType": scenario["slot"], "expertise": [], "technologySkills": [], "startupDomains": [], "tags": [],
                      "bio": "", "experience": "Đã trực tiếp phụ trách " + phrase(candidate, 1) + "."}
            if domain and candidate_index % 2 == 0:
                mentor["startupDomains"] = [phrase(domain)]
            # Some evidence is narrative-only, so tags cannot be the sole training signal.
            if candidate_index % 3 != 0:
                mentor["expertise"] = [phrase(candidate, 2)]
            if scenario["slot"] == "IT" and candidate_index % 2 == 0:
                mentor["technologySkills"] = [phrase(candidate, 2)]
            rows.append({"projectId": scenario["id"], "slot": scenario["slot"],
                         "projectText": project, "mentorRef": "SIM-" + candidate,
                         "mentor": mentor, "relevance": 2 if relevant else 1 if partial else 0,
                         "labelSource": "synthetic-curriculum", "isSynthetic": True})
        # Strong candidate has work evidence for both needs. The contrastive case below
        # has the same terms only in education, making provenance matter to the learner.
        variants = [("experienced", 3), ("education-only", 1), ("negated-experience", 0), ("generic-overlap", 0)]
        if scenario["slot"] == "IT":
            variants.append(("declared-skills", 2))
        for variant, relevance in variants:
            evidence = "; ".join(phrase(t, 2) for t in needs)
            mentor = {"mentorType": scenario["slot"], "expertise": [], "technologySkills": [], "startupDomains": [], "tags": [],
                      "bio": "Được đào tạo về " + evidence if variant == "education-only" else "",
                      "experience": "Đã trực tiếp phụ trách " + evidence if variant == "experienced" else ""}
            if variant == "negated-experience":
                mentor["experience"] = "Không có kinh nghiệm " + ", ".join(phrase(t, 2) for t in needs) + "."
                mentor["expertise"] = [phrase(t, 2) for t in needs]  # stale inferred tags must not override denial
            if variant == "generic-overlap":
                mentor["experience"] = "Hỗ trợ các nhóm làm sản phẩm và quy trình, phối hợp công việc với đối tác."
            if variant == "declared-skills":
                mentor["technologySkills"] = [phrase(t, 1) for t in needs]
            if variant == "experienced" and domain:
                mentor["startupDomains"] = [phrase(domain)]
            rows.append({"projectId": scenario["id"], "slot": scenario["slot"], "projectText": project,
                         "mentorRef": "SIM-" + variant, "mentor": mentor, "relevance": relevance,
                         "labelSource": "synthetic-curriculum", "isSynthetic": True})
    return rows


def validate_rows(rows):
    if not rows:
        raise ValueError("No labelled rows; blank or unknown relevance is not a negative label.")
    seen = set()
    projects = {}
    for row in rows:
        if not isinstance(row.get("projectId"), str) or not row["projectId"].strip():
            raise ValueError("Every row needs an anonymous projectId for group splitting.")
        if not isinstance(row.get("projectText"), str) or not row["projectText"].strip():
            raise ValueError("Every row needs projectText.")
        if not isinstance(row.get("mentor"), dict):
            raise ValueError("Every row needs a mentor snapshot.")
        if type(row.get("relevance")) is not int or row["relevance"] not in range(4):
            raise ValueError("relevance must be an explicit integer 0..3, never inferred from non-selection.")
        if row.get("slot") not in ("IT", "Business"):
            raise ValueError("slot must be IT or Business; it groups rows, not a learned input feature.")
        if not row.get("mentorRef") or not row.get("labelSource") or type(row.get("isSynthetic")) is not bool:
            raise ValueError("mentorRef, labelSource and isSynthetic provenance are required.")
        key = (row["projectId"], row["slot"], row["mentorRef"])
        if key in seen:
            raise ValueError("Duplicate project/slot/mentor row.")
        seen.add(key)
        # Repeated projects/versions must use the same group ID; the whole ID stays in one split.
        projects.setdefault(row["projectId"], set()).add(row["relevance"])
    if len(projects) < 5:
        raise ValueError("At least 5 separate project groups are required for train/validation/test splitting.")


def split_projects(rows, seed):
    ids = sorted({r["projectId"] for r in rows})
    random.Random(seed).shuffle(ids)
    test_count = max(1, len(ids) // 5)
    validation_count = max(1, len(ids) // 5)
    test_ids, validation_ids = set(ids[:test_count]), set(ids[test_count:test_count + validation_count])
    return ([r for r in rows if r["projectId"] not in test_ids | validation_ids],
            [r for r in rows if r["projectId"] in validation_ids],
            [r for r in rows if r["projectId"] in test_ids])


def sigmoid(value):
    return 1.0 / (1.0 + math.exp(-max(-60.0, min(60.0, value))))


def predict(features, weights, bias):
    if not any(features[:7]):
        return 0.0
    return sigmoid(bias + sum(x * w for x, w in zip(features, weights)))


def grouped(rows):
    groups = {}
    for row in rows:
        groups.setdefault((row["projectId"], row["slot"]), []).append(row)
    return groups


def evaluate(rows, weights, bias):
    ndcg, recall = [], []
    for group in grouped(rows).values():
        ranked = sorted(group, key=lambda r: (-predict(r["features"], weights, bias), r["mentorRef"]))
        ideal = sorted((r["relevance"] for r in group), reverse=True)[:5]
        dcg = sum((2 ** r["relevance"] - 1) / math.log2(i + 2) for i, r in enumerate(ranked[:5]))
        idcg = sum((2 ** rel - 1) / math.log2(i + 2) for i, rel in enumerate(ideal))
        relevant = sum(r["relevance"] >= 2 for r in group)
        if idcg:
            ndcg.append(dcg / idcg)
        if relevant:
            recall.append(sum(r["relevance"] >= 2 for r in ranked[:3]) / relevant)
    return {"ndcgAt5": sum(ndcg) / len(ndcg) if ndcg else None,
            "recallAt3": sum(recall) / len(recall) if recall else None,
            "groups": len(grouped(rows)), "rows": len(rows),
            "groupsWithoutRelevantMentor": sum(not any(r["relevance"] >= 2 for r in g) for g in grouped(rows).values())}


def fit(train_rows, validation_rows, seed, epochs=160):
    randomizer = random.Random(seed)
    weights, bias = [0.0] * len(FEATURES), 0.0
    pairs = [(a, b) for group in grouped(train_rows).values() for a in group for b in group
             if a["relevance"] > b["relevance"]]
    if not pairs:
        raise ValueError("Training needs groups with different explicit relevance grades.")
    best = None
    for epoch in range(epochs):
        rate = 0.08 / (1 + epoch / 50)
        randomizer.shuffle(pairs)
        for a, b in pairs:
            difference = [x - y for x, y in zip(a["features"], b["features"])]
            error = 1 - sigmoid(sum(w * d for w, d in zip(weights, difference)))
            weights = [max(0.0, w + rate * (error * d - 0.002 * w)) if i < 7
                       else w + rate * (error * d - 0.002 * w)
                       for i, (w, d) in enumerate(zip(weights, difference))]
        # Learn the display-score intercept and reinforce per-row evidence. Scores
        # are NOT calibrated real-world probabilities, especially on simulated labels.
        order = list(train_rows)
        randomizer.shuffle(order)
        for row in order:
            features = row["features"]
            error = row["relevance"] / 3.0 - sigmoid(bias + sum(w * x for w, x in zip(weights, features)))
            bias += rate * error * 0.2
            weights = [max(0.0, w + rate * error * x * 0.2) if i < 7 else w + rate * error * x * 0.2
                       for i, (w, x) in enumerate(zip(weights, features))]
        metric = evaluate(validation_rows, weights, bias)["ndcgAt5"]
        if metric is None:
            raise ValueError("Validation needs at least one relevant mentor.")
        if best is None or metric > best[0] + 1e-12:
            best = (metric, list(weights), bias, epoch + 1)
    return best[1], best[2], best[3]


def train(rows, catalog, seed=17, epochs=160):
    validate_rows(rows)
    # Do not serialize private source data into the deployable model.
    for row in rows:
        row["mentor"]["mentorType"] = row["slot"]
        row["features"] = extract_features(row["projectText"], row["mentor"], catalog)
    train_rows, validation_rows, test_rows = split_projects(rows, seed)
    weights, bias, best_epoch = fit(train_rows, validation_rows, seed, epochs)
    synthetic = all(row["isSynthetic"] for row in rows)
    model = {"version": 1, "featureSchema": "ehub-mentor-fit-v2",
             "trainingSource": "synthetic-bootstrap" if synthetic else "local-labelled-data",
             "suitabilityValidated": False, "featureNames": FEATURES, "weights": weights, "bias": bias,
             "topics": catalog["topics"], "stopWords": catalog["stopWords"]}
    report = {"seed": seed, "trainingSource": model["trainingSource"], "suitabilityValidated": False,
              "bestEpoch": best_epoch, "train": evaluate(train_rows, weights, bias),
              "validation": evaluate(validation_rows, weights, bias), "test": evaluate(test_rows, weights, bias),
              "splitProjectIds": {name: sorted({r["projectId"] for r in part})
                                  for name, part in (("train", train_rows), ("validation", validation_rows), ("test", test_rows))},
              "limitations": ["Bootstrap metrics measure fictional curriculum labels, not real mentor suitability.",
                              "Metrics sort unrounded scores; exact ties use anonymous mentorRef, while the API uses display name.",
                              "No comparison with the existing BGE-M3 70/30 recommender was performed.",
                              "This model learns field importance over a finite topic vocabulary; it is not an LLM or semantic encoder.",
                              "A high rank score is not a calibrated probability; no evidence produces score zero."]}
    model["trainingMetadata"] = {"seed": seed, "trainingRows": len(train_rows), "bestEpoch": best_epoch,
                                 "dataFingerprint": hashlib.sha256(json.dumps(rows, ensure_ascii=True, sort_keys=True).encode()).hexdigest()}
    return model, report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--bootstrap", action="store_true", help="Train on fictional startup curriculum only.")
    source.add_argument("--data", type=Path, help="Local JSONL snapshots with explicit relevance and label provenance; never auto-export real CVs.")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--seed", type=int, default=17)
    parser.add_argument("--epochs", type=int, default=160)
    parser.add_argument("--parity-output", type=Path, help="Synthetic feature fixtures for .NET/Python parity testing.")
    args = parser.parse_args()
    if not 1 <= args.epochs <= 5000:
        parser.error("epochs must be 1..5000")
    if args.parity_output and not args.bootstrap:
        parser.error("Parity fixtures may only be emitted from the synthetic bootstrap.")
    catalog = json.loads((HERE / "topic-catalog.json").read_text(encoding="utf-8"))
    rows = bootstrap(catalog, json.loads((HERE / "curriculum.json").read_text(encoding="utf-8"))) if args.bootstrap else [
        json.loads(line) for line in args.data.read_text(encoding="utf-8").splitlines() if line.strip()]
    started = time.perf_counter()
    model, report = train(rows, catalog, args.seed, args.epochs)
    report["trainingSeconds"] = round(time.perf_counter() - started, 3)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(model, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    report_path = args.output.with_suffix(".report.json")
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if args.parity_output:
        fixtures = [{"projectText": row["projectText"], "mentor": row["mentor"], "features": row["features"]}
                    for row in rows[::max(1, len(rows) // 20)]]
        # Exercise punctuation and normalization beyond the curriculum templates.
        for project, mentor in [
            ("C# API, C++ và ASP.NET cho bán hàng", {"mentorType": "IT", "technologySkills": ["C#", "C++", "ASP.NET"], "experience": "Thiết kế API"}),
            ("Java không phải JavaScript", {"mentorType": "IT", "technologySkills": ["JavaScript"]}),
            ("ĐỊNH GIÁ SẢN PHẨM — dòng tiền", {"experience": "Quản lý tài chính", "bio": "Được đào tạo về kế toán"}),
            ("Nội dung chưa có bằng chứng", {}),
            ("Không cần AI, nhưng cần C#/.NET và PostgreSQL.\nCổng AI", {"mentorType": "IT", "experience": "Backend Node.js và PostgreSQL. Không có kinh nghiệm AI.", "expertise": ["AI"]}),
            ("AI và computer vision", {"mentorType": "IT", "experience": "Không có kinh nghiệm AI, machine learning hoặc computer vision.", "expertise": ["AI"]}),
            ("Cần marketing và sales", {"experience": "Không chỉ marketing mà còn bán hàng."}),
            ("React Native cho điện thoại", {"mentorType": "IT", "experience": "React và TypeScript"}),
            ("React Native cho điện thoại", {"mentorType": "IT", "experience": "React Native và Flutter"}),
            ("Marketing.\nTài chính", {"experience": "Không có kinh nghiệm tài chính\nMarketing", "expertise": ["Finance"]}),
            ("AI is not required. Need backend.", {"mentorType": "IT", "experience": "Backend. No AI."}),
            ("Cần logistics nông sản", {"experience": "Vận hành du lịch"}),
            ("Nghiên cứu thị trường giáo dục", {"mentorType": "IT", "experience": "Flutter trong giáo dục"}),
        ]:
            fixtures.append({"projectText": project, "mentor": mentor, "features": extract_features(project, mentor, catalog)})
        args.parity_output.write_text(json.dumps(fixtures, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"model": str(args.output), "report": str(report_path), "source": model["trainingSource"],
                      "realWorldSuitabilityValidated": False, "test": report["test"], "trainingSeconds": report["trainingSeconds"]}))


if __name__ == "__main__":
    main()
