"""Create an offline blind review form. No model score or expected answer is shown."""
import argparse
import json
from pathlib import Path
import random


def rows_from_reports(reports):
    rows = []
    seen = set()
    for report in reports:
        if report.get("isSynthetic") is not True:
            raise ValueError("Automatic report import accepts synthetic demo reports only. Supply redacted snapshots with --data for real reviews.")
        mentors = report["uploads"]
        for project in report["projects"]:
            if not project["description"].strip():
                continue
            for mentor in mentors:
                key = (project["id"], mentor["mentorId"])
                if key in seen or not mentor["succeeded"]:
                    continue
                seen.add(key)
                rows.append({"projectId": project["id"], "projectText": project["description"] + "\n" + project["name"],
                             "mentorRef": mentor["mentorId"], "slot": mentor["type"], "mentor": {
                                 "mentorType": mentor["type"], "experience": mentor["experience"] or "",
                                 "bio": mentor["bio"] or "", "expertise": mentor["expertise"],
                                 "startupDomains": mentor["startupDomains"], "technologySkills": mentor["technologySkills"]},
                             "relevance": None, "labelSource": "pending-human-review", "isSynthetic": True})
    random.Random(17).shuffle(rows)
    return rows


def page(rows):
    if not rows or any(type(row.get("isSynthetic")) is not bool or row.get("slot") not in ("IT", "Business")
                       or not row.get("projectId") or not row.get("mentorRef") or not isinstance(row.get("mentor"), dict)
                       or not str(row.get("projectText", "")).strip() for row in rows):
        raise ValueError("Need explicit anonymized snapshots and synthetic provenance for every pair")
    # Never pre-fill a model/author expectation as if a person had reviewed it.
    rows = [dict(row, relevance=None, labelSource="pending-human-review") for row in rows]
    payload = json.dumps(rows, ensure_ascii=False).replace("<", "\\u003c").replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")
    return TEMPLATE.replace("__ROWS__", payload)


TEMPLATE = """<!doctype html>
<html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>E-HUB · Đánh giá ghép mentor</title>
<style>
body{margin:0;background:#f8fafc;color:#0f172a;font:16px system-ui,sans-serif}main{max-width:900px;margin:32px auto;padding:24px}
article{background:white;border:1px solid #cbd5e1;border-radius:16px;padding:24px;margin:18px 0}h1{font-size:26px}h2{font-size:20px}
pre{white-space:pre-wrap;font:inherit;line-height:1.6}button,select{font:inherit;border-radius:8px;border:1px solid #94a3b8;padding:10px;margin:4px}
button{cursor:pointer;background:white}button.primary{background:#f56a2a;border-color:#f56a2a;color:white}nav{display:flex;flex-wrap:wrap;gap:8px;align-items:center}
#status{color:#475569}select{max-width:100%}label{display:block;margin:12px 0}textarea{display:block;box-sizing:border-box;width:100%;font:inherit;padding:10px;min-height:72px}
</style><main>
<h1>Đánh giá ghép mentor</h1>
<p>Chấm theo mô tả dự án và bằng chứng nghề nghiệp. Phiếu không hiện điểm AI hoặc đáp án kỳ vọng. Chỉ những cặp bạn chấm mới được xuất làm nhãn.</p>
<p>3 = phù hợp làm mentor chính · 2 = phù hợp hỗ trợ · 1 = liên quan ít · 0 = không phù hợp. Bỏ trống nếu chưa đủ thông tin; bỏ trống không có nghĩa là 0.</p>
<p id="provenance"></p><nav><label>Cặp cần đánh giá <select id="pair"></select></label><button id="ungraded">Cặp chưa chấm tiếp theo</button></nav>
<p id="status" role="status"></p>
<article><h2>Dự án</h2><pre id="project"></pre></article>
<article><h2 id="mentor-title"></h2><h3>Kinh nghiệm</h3><pre id="experience"></pre><h3>Giới thiệu và kỹ năng</h3><pre id="skills"></pre>
<label>Mức phù hợp <select id="grade"><option value="">Chưa đánh giá</option><option value="3">3 · Mentor chính</option><option value="2">2 · Hỗ trợ phù hợp</option><option value="1">1 · Liên quan ít</option><option value="0">0 · Không phù hợp</option></select></label>
<label>Lý do đánh giá (tùy chọn)<textarea id="note" maxlength="1000"></textarea></label></article>
<nav><button id="previous">Trước</button><button id="next">Tiếp</button><button class="primary" id="export">Xuất nhãn đã chấm (.jsonl)</button></nav>
<p>Phiếu chạy trên máy, không gửi nội dung lên mạng. Hãy xuất file để lưu kết quả trước khi đóng trang. Các phiên bản gần giống của một dự án phải dùng cùng projectId khi chia tập train/test.</p>
</main><script>
const rows=__ROWS__;
const el=id=>document.getElementById(id);
let current=0;
rows.forEach((row,i)=>{const option=document.createElement('option');option.value=String(i);option.textContent=`${row.projectId} · ${row.slot} · ${row.mentorRef}`;el('pair').append(option)});
el('provenance').textContent=rows.every(r=>r.isSynthetic)?'Đây là dữ liệu demo giả lập. Việc chấm demo không trở thành kiểm chứng trên dự án thực tế.':'Dữ liệu do người vận hành cung cấp. Chỉ dùng bản đã ẩn thông tin cá nhân, tên, liên hệ và dữ liệu nhạy cảm.';
function render(){const row=rows[current];el('pair').value=String(current);el('project').textContent=row.projectText;el('mentor-title').textContent=`Mentor ${row.mentorRef} · ${row.slot}`;el('experience').textContent=row.mentor.experience||'(Chưa có kinh nghiệm được khai báo)';el('skills').textContent=[row.mentor.bio,...(row.mentor.expertise||[]),...(row.mentor.technologySkills||[]),...(row.mentor.startupDomains||[])].filter(Boolean).join('\\n');el('grade').value=row.relevance===null?'':String(row.relevance);el('note').value=row.reviewNote||'';el('status').textContent=`Cặp ${current+1}/${rows.length} · Đã chấm ${rows.filter(r=>r.relevance!==null).length} cặp`}
el('pair').addEventListener('change',()=>{current=Number(el('pair').value);render()});
el('grade').addEventListener('change',()=>{rows[current].relevance=el('grade').value===''?null:Number(el('grade').value);render()});
el('note').addEventListener('input',()=>{rows[current].reviewNote=el('note').value});
el('previous').addEventListener('click',()=>{current=Math.max(0,current-1);render()});
el('next').addEventListener('click',()=>{current=Math.min(rows.length-1,current+1);render()});
el('ungraded').addEventListener('click',()=>{for(let n=1;n<=rows.length;n++){const i=(current+n)%rows.length;if(rows[i].relevance===null){current=i;break}}render()});
el('export').addEventListener('click',()=>{const labelled=rows.filter(r=>r.relevance!==null).map(r=>({...r,labelSource:r.isSynthetic?'human-reviewed-synthetic-demo':'human-review',reviewedAtUtc:new Date().toISOString()}));if(!labelled.length){el('status').textContent='Chưa có cặp nào được đánh giá.';return}const url=URL.createObjectURL(new Blob([labelled.map(r=>JSON.stringify(r)).join('\\n')+'\\n'],{type:'application/x-ndjson'}));const a=document.createElement('a');a.href=url;a.download='mentor-reviewed-labels.jsonl';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)});
render();
</script></html>"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--reports", type=Path, nargs="+", help="Synthetic .NET demo reports only")
    source.add_argument("--data", type=Path, help="Local JSONL with explicitly redacted anonymous snapshots and isSynthetic flags")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.reports:
        rows = rows_from_reports([json.loads(path.read_text(encoding="utf-8")) for path in args.reports])
    else:
        rows = [json.loads(line) for line in args.data.read_text(encoding="utf-8").splitlines() if line.strip()]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(page(rows), encoding="utf-8")
    print(f"Created offline blind review: {len(rows)} pairs, {len({r['projectId'] for r in rows})} projects; zero labels pre-filled.")


if __name__ == "__main__":
    main()
