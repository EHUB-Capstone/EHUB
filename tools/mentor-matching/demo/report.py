"""Render measured two-field matching results, without training or changing inputs."""
import hashlib
import json
from pathlib import Path

DIRECTORY = Path(__file__).resolve().parent
ROOT = DIRECTORY.parents[2]


def read(name):
    return json.loads((DIRECTORY / name).read_text(encoding="utf-8-sig"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def table(headers, rows):
    def cell(value):
        return str(value).replace("|", "\\|").replace("\n", " ")
    return ["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |",
            *("| " + " | ".join(cell(value) for value in row) + " |" for row in rows), ""]


def shortlist(items):
    return "; ".join(f"{x['id']} ({x['fitScore']})" for x in items[:3]) or "Không gợi ý"


def verify(result):
    assert result["isSynthetic"] and result["model"]["unchanged"]
    assert digest(DIRECTORY / "mentors.json") == result["inputs"]["mentorsSha256"]
    assert digest(DIRECTORY / result["inputs"]["projectsFile"]) == result["inputs"]["projectsSha256"]
    assert digest(ROOT / result["model"]["path"]) == result["model"]["sha256After"]
    assert result["implementation"]["sourcesUnchangedDuringRun"]
    for path, expected in result["implementation"]["sourceHashes"].items():
        assert digest(ROOT / path) == expected, f"Source changed: {path}; rebuild and rerun demo"


def main():
    mentors = read("mentors.json")
    regression, holdout, baseline = read("results.json"), read("holdout-results.json"), read("baseline/results.json")
    verify(regression)
    verify(holdout)
    projects = regression["projects"] + holdout["projects"]
    by_id = {p["id"]: p for p in projects}
    old = {p["id"]: p for p in baseline["projects"]}
    s, h, b = regression["summary"], holdout["summary"], baseline["summary"]
    lines = ["# Kiểm tra model v2 — chỉ tên đề tài và mô tả", "",
             f"Chạy {len(mentors['mentors'])} CV hư cấu với {len(projects)} dự án giả lập qua reader/parser và handler backend thật.",
             f"Bộ cũ: Top 1 khớp {s['standardTop1ExpectationMatches']}/{s['standardDeclaredRoleComparisons']} vị trí thuộc 8 dự án thông thường; ca khó đạt {s['edgeChecksPassed']}/{s['edgeCheckCount']} (bản trước {b['edgeChecksPassed']}/{b['edgeCheckCount']}).",
             f"Bộ mô tả mới: Top 1 khớp {h['standardTop1ExpectationMatches']}/{h['standardDeclaredRoleComparisons']} vị trí có kỳ vọng; {h['edgeChecksPassed']}/{h['edgeCheckCount']} kiểm tra bổ sung đạt.", "",
             "Đây là kỳ vọng tác giả trên tình huống tự tạo, không phải độ chính xác thực tế hoặc đánh giá độc lập. Các file demo không được trainer đọc. Bộ mới nằm ngoài curriculum nhưng do cùng tác giả thiết kế; không coi đây là kiểm định mù.", "",
             "## Đầu vào và phương pháp", "",
             "Handler chỉ lấy tên đề tài và mô tả (Project.Name/Description, hoặc TeamName/Description khi chưa có nội dung tương ứng). Không đưa Technology, StartupField, Problem, Solution, tag, trường nhu cầu riêng hoặc đáp án kỳ vọng vào model.",
             "12 ca cũ giữ nguyên nội dung: các đoạn nhu cầu trước đây được gộp vào description để kiểm tra hồi quy. 10 ca mới dùng mô tả tự nhiên, bao gồm trường hợp không nêu công nghệ, chỉ có nhu cầu kinh doanh, và mô tả quá chung chung.", "",
             "Model lọc phủ định rõ ràng tiếng Việt/Anh, giữ 'không chỉ… mà còn…', phân biệt chuyên môn cụ thể với chủ đề cha và ưu tiên cụm dài như React Native. Tag cũ không lấn át câu phủ định trong CV; học vấn vẫn ở Bio nhưng không tự tạo tag chuyên môn khi tải CV mới.",
             "Bộ đọc CV giữ lại câu nghề nghiệp như phát triển ứng dụng trên điện thoại hoặc email marketing cho ngân hàng. Chỉ các nhãn liên hệ tương ứng được xem là thông tin riêng tư; câu nghề nghiệp không bị loại chỉ vì có từ điện thoại/email/ngân hàng.",
             "Ứng viên chỉ trùng từ chung hoặc không khớp chuyên môn bị loại. Mô tả kinh doanh không tự tạo nhu cầu IT từ ngành nghề. Lý do trả về ghi cả phần khớp và chuyên môn chưa tìm thấy bằng chứng.", "",
             "## So sánh các lỗi trước đây", ""]
    lines += table(["Tình huống", "Trước", "Sau"], [
        ("P09 — không có chuyên gia lên men", f"Trả {len(old['P09']['business']) + len(old['P09']['it'])} ứng viên", f"Trả {len(by_id['P09']['business']) + len(by_id['P09']['it'])} ứng viên"),
        ("P10 — không cần AI, cần .NET", shortlist(old["P10"]["it"]), shortlist(by_id["P10"]["it"])),
        ("P05 — logistics nông sản", shortlist(old["P05"]["business"]), shortlist(by_id["P05"]["business"])),
        ("P11 — tài chính thực tế", shortlist(old["P11"]["business"]), shortlist(by_id["P11"]["business"]))])
    lines += ["Điểm 0–100 là điểm xếp hạng, không phải xác suất đúng. Không so sánh mức tăng điểm giữa các model như mức tăng độ chính xác. Thứ tự dùng giá trị chưa làm tròn; số nguyên trên UI có thể bằng nhau.", "", "## Danh sách mentor", ""]
    lines += table(["ID", "Tên hư cấu", "Loại", "Nội dung chuyên môn"], ((m["id"], m["name"], m["type"], m["summary"]) for m in mentors["mentors"]))
    for label, result in (("12 tình huống hồi quy", regression), ("10 mô tả mới ngoài curriculum", holdout)):
        lines += [f"## {label}", ""]
        lines += table(["Đề tài", "Mô tả duy nhất đưa vào model", "Business Top 3", "IT Top 3"], (
            (p["id"] + " — " + p["name"], p["description"] or "Thiếu mô tả → validation", shortlist(p["business"]), shortlist(p["it"])) for p in result["projects"]))
    lines += ["## Kiểm tra ca khó", ""]
    lines += table(["Dự án", "Kiểm tra", "Kết quả", "Quan sát"], ((p["id"], c["name"], "Đạt kỳ vọng mô phỏng" if c["passed"] else "CHƯA ĐẠT", c["detail"]) for p in projects for c in p["checks"]))
    for p in projects:
        for role in ("business", "it"):
            exp = p[role + "Expectation"]
            if exp["hasDeclaredExpectation"] and not exp["top1Matches"]:
                lines += [f"- CHƯA ĐẠT {p['id']}/{role}: thực tế {exp['actualTop1']}; kỳ vọng {', '.join(exp['expectedIds'])}.", ""]
    lines += ["## Ví dụ lý do trả về", ""]
    for pid, mid in (("P10", "I01"), ("P10", "I06"), ("H05", "I02")):
        item = next((x for x in by_id[pid]["it"] if x["id"] == mid), None)
        if item:
            lines += [f"**{pid} / {mid}: {item['fitScore']} điểm**", "", *(f"- {reason}" for reason in item["reasons"]), ""]
    lines += ["## Phạm vi và giới hạn", "",
              "Đây là mô hình xếp hạng tuyến tính đã train, dùng xử lý văn bản theo quy tắc và danh mục chủ đề hữu hạn. Chưa hiểu đầy đủ mọi cách diễn đạt, ngữ cảnh nhiều câu, mức độ ưu tiên hoặc điều kiện bắt buộc. Có thể bỏ sót mô tả ít từ chuyên môn hoặc ngoài danh mục.",
              "Ứng viên khớp một phần vẫn có thể xuất hiện; phần thiếu bằng chứng được ghi trong reasons. Ví dụ có kinh nghiệm mobile chưa chứng minh biết React Native. Không dùng điểm cao thay việc xác nhận năng lực. Chưa có nhãn ghép thật hoặc so sánh BGE-M3.", "",
              "## Bằng chứng chạy", "",
              f"- CV thành công: {s['successfulCvUploads']}/{s['cvUploadCount']} và {h['successfulCvUploads']}/{h['cvUploadCount']} ở hai bộ.",
              f"- Marker riêng tư giả bị loại: {s['allPrivateMarkersExcluded']} / {h['allPrivateMarkersExcluded']}; chỉ là kiểm tra trên các mẫu này.",
              f"- Embedding/Ollama calls: {regression['pipeline']['embeddingCalls']} / {holdout['pipeline']['embeddingCalls']}.",
              "- Demo dùng EF Core InMemory và storage giả; không qua HTTP/UI hoặc database thật. Các test API được ghi riêng trong báo cáo kiểm thử backend.",
              f"- Model SHA256: `{regression['model']['sha256After']}`; giữ nguyên trong hai lần đánh giá.",
              "- JSON lưu hash đầu vào, mã nguồn liên quan và assembly đã nạp. report.py từ chối kết quả cũ khi nguồn/đầu vào/model thay đổi. baseline/ giữ dữ liệu và kết quả v1.",
              "- Bộ cũ: [projects.json](projects.json), [results.json](results.json). Bộ mới: [holdout-projects.json](holdout-projects.json), [holdout-results.json](holdout-results.json). CV: [mentors.json](mentors.json).", "",
              "## Chạy lại từ gốc repo", "", "```powershell",
              "dotnet restore tools/mentor-matching/demo/Demo.csproj --disable-parallel -m:1",
              "dotnet build tools/mentor-matching/demo/Demo.csproj --no-restore -m:1 -p:UseSharedCompilation=false",
              "dotnet run --project tools/mentor-matching/demo/Demo.csproj --no-build --no-restore",
              "dotnet run --project tools/mentor-matching/demo/Demo.csproj --no-build --no-restore -- . holdout-projects.json",
              "python tools/mentor-matching/demo/report.py", "```", ""]
    path = DIRECTORY / "README.md"
    path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Report: {path}")


if __name__ == "__main__":
    main()
