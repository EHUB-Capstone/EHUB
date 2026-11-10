"""Synthetic submissions that reproduce the text quirks of PDFs exported from design tools (slides and dashboards).

Everything here is invented: the startups, the people and every number. The shapes come from looking at real checkpoint
files uploaded in production (a marketing pitch deck, a survey dashboard and its slides, a persona deck, a market report),
so the artefacts the summary has to survive are the real ones:

  * Vietnamese words split at the tone mark ("Qu ả n lý"), because the font draws the mark as its own glyph.
  * Stat cards and titles drawn in layers, so their text arrives two or three times ("62,5%62,5%62,5%").
  * A table dumped one cell per line, so a row is no longer a row.
  * Two files of one checkpoint that state the same survey differently (a sample of 47 and 48).
  * Decks that are mostly pictures: a few words per slide.
  * English headings mixed into Vietnamese prose, and emoji used as bullets (kept in .pptx; the Arial used for PDFs has no
    emoji glyphs, so fpdf2 warns and leaves them out there).

`truth` and `facts` follow fixtures.py. Names and numbers are made up on purpose; do not replace them with the real ones.
"""

_MARKED = set("àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ")


def split_marks(text):
    """Draws each accented vowel as its own glyph: "Quản lý" arrives as "Qu ả n lý"."""
    out = []
    for ch in text:
        out.append(f" {ch} " if ch in _MARKED else ch)
    return "".join(out).replace("  ", " ").replace(" ,", ",").replace(" .", ".")


def layers(text, times=3):
    """A title or number drawn in layers (shadow, outline): the text layer holds it several times."""
    return text * times


# ------------------------------------------------------------------ Checkpoint 1 – pitch deck (Vietnamese and English mixed)

REMINDLY_DECK = [
    (layers("REMINDLY"), ["NHÓM 7", "EXE101 · PITCH DECK"]),
    ("OBJECTIVE", ["AWARENESS: Make pharmacy owners notice the problem of missed medicine refills.",
                   "ACQUISITION: Bring qualified owners to the first demo.",
                   "RETENTION: Turn satisfied pharmacies into repeat users and referrers."]),
    ("1. PRIMARY SEGMENT", ["Chủ nhà thuốc nhỏ tại Đà Nẵng, mỗi tháng phục vụ 300–500 khách quen.",
                            "Họ nhớ khách bằng sổ tay và Zalo, nên hay quên nhắc khách mua lại thuốc dùng dài ngày.",
                            "Insight: \"Khách đã tin mình rồi. Mình chỉ không có thời gian nhắc từng người.\""]),
    (layers("GO-TO-MARKET"), [split_marks("Giai đoạn 1: Thử nghiệm với 5 nhà thuốc quen biết, không thu phí."),
                              split_marks("Giai đoạn 2: Mở bán gói 99.000 đồng mỗi tháng cho nhà thuốc đã thử."),
                              split_marks("Giai đoạn 3: Giới thiệu bạn bè, nhận thêm 1 tháng sử dụng miễn phí.")]),
    ("CHANNEL STRATEGY", ["Zalo OA → Reach + Product Demonstration", "Facebook Group chủ nhà thuốc → Community",
                          "Landing Page → CTA: Đăng ký dùng thử"]),
]

# ------------------------------------------------------------------ Checkpoint 2 – survey report + slides that disagree

_COMPANIES = ["Nhà thuốc An Phúc", "Nhà thuốc Minh Châu", "Nhà thuốc Hòa Bình", "Nhà thuốc Phương Nam", "Nhà thuốc Sơn Trà",
              "Nhà thuốc Hải Châu", "Nhà thuốc Liên Chiểu", "Nhà thuốc Cẩm Lệ", "Nhà thuốc Ngũ Hành Sơn", "Nhà thuốc Thanh Khê"]


def _survey_rows(count):
    """A survey table as a PDF text layer gives it: one cell per line."""
    rows = []
    for number in range(1, count + 1):
        rows += [str(number), _COMPANIES[number % len(_COMPANIES)], "Nhà thuốc độc lập" if number % 3 else "Chuỗi 3–5 cửa hàng",
                 "Dưới 10 nhân sự", "Ghi sổ tay hoặc Excel", "Trên 30 phút" if number % 4 == 0 else "2 đến 10 phút"]
    return rows


SURVEY_REPORT_SECTIONS = [
    ("BÁO CÁO KHẢO SÁT: NHU CẦU NHẮC LỊCH MUA THUỐC", [
        "Dữ liệu nghiên cứu từ 48 nhà thuốc tại Đà Nẵng | Phân tích thực trạng và đề xuất giải pháp",
        "TỔNG SỐ PHẢN HỒI 48 | SẴN SÀNG DÙNG THỬ 91,7% | MẤT HƠN 10 PHÚT TÌM HỒ SƠ KHÁCH 76,0% | RỦI RO CAO NHẤT 4,2/5,0",
    ]),
    ("KẾT LUẬN VÀ ĐỀ XUẤT TỪ KHẢO SÁT", [
        "• 76,0% nhà thuốc mất hơn 10 phút để tìm lại đơn thuốc cũ của khách, rủi ro lớn nhất là bán nhầm liều khi không tra được lịch sử (4,2/5,0).",
        "• Tính năng cần thiết nhất: nhắc khách mua lại thuốc dùng dài ngày (4,6/5,0) và cảnh báo thuốc sắp hết hạn trong kho (4,4/5,0).",
        "• 91,7% nhà thuốc sẵn sàng dùng thử sớm; mức ngân sách phổ biến nhất là 100.000 đến 300.000 đồng mỗi tháng.",
        "• Rào cản lớn nhất là thói quen ghi sổ tay (31 lượt) và lo ngại lộ dữ liệu khách (27 lượt).",
    ]),
    ("🎯 CHỌN LOẠI HÌNH NHÀ THUỐC", [
        "💡 Hướng dẫn tương tác: Chọn loại hình tại menu thả xuống bên trái để xem phân tích riêng cho từng nhóm, hoặc chọn 'Tất cả' để xem toàn bộ 48 nhà thuốc.",
        "⚡ Tự động cập nhật biểu đồ và số liệu",
    ]),
    ("DANH SÁCH CHI TIẾT NHÀ THUỐC THEO BỘ LỌC (TẤT CẢ LOẠI HÌNH - 48 NHÀ THUỐC)",
     ["STT", "Tên nhà thuốc", "Loại hình", "Quy mô nhân sự", "Phương thức lưu trữ", "Thời gian tra cứu"] + _survey_rows(14)),
]

SURVEY_DECK_SECTIONS = [
    (layers("Check Point 2:", 3), [layers("MARKET", 3), layers("RESEARCH", 3), "Presented by: GROUP 7", "Instructor: Mr. Demo"]),
    (split_marks("Tổng quan phương pháp và mẫu khảo sát"), [
        "Thời gian thực hiện: từ 02/10/2026 đến 12/10/2026.",
        "Phương pháp: thu thập định lượng trực tiếp qua Google Sheets.",
        "Quy mô mẫu hợp lệ: N = 47 chủ nhà thuốc tại Đà Nẵng.",
        "Thang đo Likert 5 mức độ để đánh giá mức độ hài lòng và mức độ cần thiết của tính năng.",
    ]),
    (split_marks("Cơ cấu nhà thuốc tham gia khảo sát"), [
        "Nhà thuốc độc lập: chiếm 63,8% (30 nhà thuốc).", "Chuỗi 3–5 cửa hàng: chiếm 29,8% (14 nhà thuốc).",
        "Khác: chiếm 6,4% (3 nhà thuốc).",
    ]),
    ("KEY NUMBERS", [layers("3.4 / 5", 3), "Mức độ hài lòng với cách ghi sổ hiện tại",
                     layers("62,5%", 3), "Mức độ hài lòng với phần mềm đang dùng",
                     layers("78,7%", 3), "Chủ nhà thuốc mất hơn 10 phút tìm lại đơn thuốc cũ của khách"]),
]

# ------------------------------------------------------------------ Checkpoint 2 – market research report (A4, long tables)

LINGOQUEST_REPORT = [
    ("LINGOQUEST — MARKET RESEARCH REPORT", [
        "Nền tảng luyện nói tiếng Anh cho học sinh cấp 2 bằng nhân vật AI | Trung tâm ngoại ngữ tại Đà Nẵng | Việt Nam | 2023–2030",
        "Mục tiêu nghiên cứu: đánh giá khả năng gia nhập thị trường và xác định MVP. Ngày cập nhật 12/10/2026.",
        "Lưu ý bằng chứng: báo cáo dùng dữ liệu giáo dục nói chung làm proxy. Chưa có thống kê công khai đủ tin cậy cho riêng trung tâm "
        "ngoại ngữ tại Đà Nẵng; TAM/SAM/SOM và insight sơ cấp cần được kiểm chứng qua phỏng vấn và pilot.",
    ]),
    ("1. Executive Summary", [
        "Market proxy", "Chi tiêu giáo dục ngoại ngữ của hộ gia đình tại Đà Nẵng ước khoảng 2.100 tỷ đồng năm 2025.",
        "Tăng trưởng", "Nhu cầu luyện nói tăng khoảng 14% mỗi năm theo số liệu của các trung tâm lớn.",
        "Cạnh tranh", "Nhiều ứng dụng từ vựng mạnh, nhưng ít giải pháp luyện nói có phản hồi theo giáo trình trung tâm.",
        "Phân khúc ưu tiên", "Trung tâm có 150–400 học viên cấp 2 và ít nhất 3 giáo viên bản ngữ hoặc IELTS 7.0.",
    ]),
    ("2. Market Size & Growth", [
        "Chỉ số proxy", "2023", "2024", "2025", "2030",
        "Chi tiêu ngoại ngữ Việt Nam", "USD 1,9 tỷ", "USD 2,2 tỷ", "Chưa dùng làm base", "USD 3,6 tỷ",
        "Học viên cấp 2 tại Đà Nẵng", "Chưa chuẩn hóa", "Hơn 62.000", "64.500", "Không ngoại suy",
    ]),
    ("3. Pricing hypothesis", [
        "Gói trung tâm: 4.000.000 đồng mỗi tháng cho tối đa 200 học viên.",
        "Gói học viên lẻ: 79.000 đồng mỗi tháng, hoàn tiền trong 7 ngày đầu.",
        "Điểm hòa vốn dự kiến khi có 25 trung tâm trả phí; chưa có khách trả phí nào tại thời điểm viết báo cáo.",
    ]),
]

# ------------------------------------------------------------------ Checkpoint 1 – persona and journey deck (wide tables, emoji)

TASKNOW_DECK = [
    ("TaskNow", ["M I C R O - T A S K   M A R K E T P L A C E", "C H E C K   P O I N T   1",
                 "Kết nối người cần việc nhỏ và người rảnh tay tại Đà Nẵng, nhanh và minh bạch."]),
    ("02. KHÁCH HÀNG MỤC TIÊU", ["Quy mô thị trường: khoảng 90.000 sinh viên tại Đà Nẵng.",
                                 "Vấn đề cốt lõi: lịch học thay đổi theo kỳ nên khó nhận ca cố định; tìm việc qua nhóm Facebook dễ bị bùng tiền.",
                                 "Mức sẵn sàng trả phí: 19.000–59.000 đồng mỗi tháng nếu việc phù hợp."]),
    ("03. CUSTOMER PERSONA", ["📌 Đặc điểm", "🔄 Hành vi", "🎯 Mục tiêu",
                              "Nam (21 tuổi), sinh viên năm 3, cần việc 2–4 tiếng mỗi ngày theo lịch học.",
                              "Hay xem nhóm Facebook tìm việc, nhạy cảm về giá và ngại trả phí trước.",
                              "Nhận việc đúng kỹ năng, nhận tiền ngay, tích điểm uy tín."]),
    ("04. USER JOURNEY", ["Thành phần", "1. Phát sinh nhu cầu", "2. Tìm kiếm", "3. Đăng ký", "4. Trải nghiệm", "5. Đánh giá",
                          "Hành động", "Cần tiền trang trải;", "Thấy mã QR tại quán trà sữa.", "Tải app, tạo hồ sơ, nhận 100 token.",
                          "Dùng token ứng tuyển ca 2 tiếng.", "Được chấm 5 sao; mua gói 29k."]),
]

# ------------------------------------------------------------------ Checkpoint 2 – mostly pictures

PICTURE_DECK = [
    (layers("DEMO", 2), ["Page 01"]),
    ("RESULT", ["120", "78%", "Page 02"]),
    ("SCREENS", ["Home", "Booking", "Payment", "Page 03"]),
    ("THANK YOU", ["Q&A"]),
]

EXPORT_QUIRK_SUBMISSIONS = [
    {
        "id": "F12", "checkpoint": 1, "title": "Remindly – nhắc khách mua lại thuốc (pitch deck song ngữ)",
        "files": [{"name": "Remindly_PitchDeck.pptx", "kind": "pptx", "sections": REMINDLY_DECK}],
        "links": [],
        "truth": ["Covered", "Partial", "NotFound", "NotFound"],
        "facts": [["nhà thuốc"], ["99.000", "99 nghìn"], ["zalo"], ["300–500", "300-500"]],
    },
    {
        "id": "F13", "checkpoint": 2, "title": "Remindly – khảo sát: báo cáo ghi 48 nhà thuốc, slide ghi N = 47",
        "files": [{"name": "Remindly_KhaoSat_BaoCao.pdf", "kind": "pdf", "sections": SURVEY_REPORT_SECTIONS},
                  {"name": "Remindly_KhaoSat_Slide.pdf", "kind": "pdf", "sections": SURVEY_DECK_SECTIONS}],
        "links": [],
        # The survey is covered; there is no MVP, the budget range only hints at a price, and no post-test metrics.
        "truth": ["Covered", "NotFound", "Partial", "NotFound"],
        "facts": [["48 nhà thuốc", "48"], ["76,0%", "76%"], ["47"], ["100.000", "300.000"]],
        "figure_conflicts": ["Survey sample size"],
    },
    {
        "id": "F14", "checkpoint": 2, "title": "LingoQuest – báo cáo nghiên cứu thị trường (bảng dài, bằng chứng gián tiếp)",
        "files": [{"name": "LingoQuest_MarketResearch.pdf", "kind": "pdf", "sections": LINGOQUEST_REPORT}],
        "links": [],
        # Evidence is a proxy and no customer has paid: the survey and the metrics must not be reported as covered.
        "truth": ["Partial", "NotFound", "Covered", "NotFound"],
        "facts": [["79.000"], ["4.000.000"], ["25 trung tâm"], ["proxy"]],
    },
    {
        "id": "F15", "checkpoint": 1, "title": "TaskNow – persona và hành trình người dùng (bảng rộng, emoji)",
        "files": [{"name": "TaskNow_CP1.pptx", "kind": "pptx", "sections": TASKNOW_DECK}],
        "links": [],
        "truth": ["Covered", "Partial", "NotFound", "NotFound"],
        "facts": [["90.000"], ["sinh viên"], ["token"], ["29k", "29.000"]],
    },
    {
        "id": "F16", "checkpoint": 2, "title": "BookEasy – bản demo gần như chỉ có hình",
        "files": [{"name": "BookEasy_Demo.pptx", "kind": "pptx", "sections": PICTURE_DECK}],
        "links": [],
        # Almost no text: nothing should be reported as covered from a bare "120" and "78%".
        "truth": ["NotFound", "NotFound", "NotFound", "NotFound"],
        "facts": [["78%"]],
    },
]
