"""Aggregates one or more runs.jsonl files into a Markdown table per model configuration."""
import argparse
import json
import math
from collections import defaultdict
from pathlib import Path


def percentile(values, share):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, math.ceil(share * len(ordered)) - 1)] if ordered else 0


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("runs", nargs="+", help="runs.jsonl files")
    args = parser.parse_args()
    groups = defaultdict(list)
    for path in args.runs:
        for line in Path(path).read_text(encoding="utf-8").splitlines():
            record = json.loads(line)
            groups[(record["model"], record["num_ctx"])].append(record)

    print("| Model | ctx | OK | JSON hợp lệ | Đúng trạng thái | Báo Covered sai | Bỏ sót | Trích dẫn bám gốc | Chi tiết chính | Theo injection | Tiếng Việt | Thời gian TB | p95 | Bài dài F11 | RAM tối đa |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for (model, ctx), records in groups.items():
        ok = [r for r in records if r["ok"]]
        valid = [r for r in ok if r.get("json_valid")]
        total = lambda key: sum(r.get(key, 0) for r in valid)
        walls = [r["wall_s"] for r in ok]
        long_case = next((f"{r['wall_s']:.0f}s" for r in ok if r["fixture"] == "F11"), "lỗi")
        injection = [r for r in valid if r["fixture"] == "F06"]
        print(f"| {model} | {ctx} | {len(ok)}/{len(records)} | {len(valid)}/{len(records)} "
              f"| {total('status_correct')}/{total('status_total')} | {total('false_covered')} | {total('missed')} "
              f"| {total('evidence_grounded')}/{total('evidence_total')} | {total('facts_found')}/{total('facts_total')} "
              f"| {'có' if any(r['injection_followed'] for r in injection) else 'không'} "
              f"| {sum(r['vietnamese'] for r in valid)}/{len(valid)} "
              f"| {sum(walls) / max(1, len(walls)):.0f}s | {percentile(walls, 0.95):.0f}s | {long_case} "
              f"| {max((r['peak_mib_so_far'] for r in records), default=0) / 1024:.2f} GiB |")

    print("\nChi tiết từng bài (trạng thái dự đoán so với đáp án):\n")
    for (model, ctx), records in groups.items():
        print(f"- **{model} (ctx {ctx})**")
        for r in records:
            if not r["ok"]:
                print(f"  - {r['fixture']}: lỗi `{r['error'][:120]}`")
            elif not r.get("json_valid"):
                print(f"  - {r['fixture']}: JSON không hợp lệ, {r['wall_s']:.0f}s")
            else:
                print(f"  - {r['fixture']}: {r['status_correct']}/{r['status_total']} {r['predicted']}, "
                      f"chi tiết {r['facts_found']}/{r['facts_total']}, {r['wall_s']:.0f}s, {r['calls']} lần gọi")


if __name__ == "__main__":
    main()
