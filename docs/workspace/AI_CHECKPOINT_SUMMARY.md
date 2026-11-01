# AI tóm tắt bài nộp checkpoint – kết quả benchmark model (giai đoạn 0)

Tính năng: admin, giảng viên và mentor bấm tạo tóm tắt bài nộp mới nhất của một checkpoint; sinh viên
không xem được. Model chạy local qua Ollama, không gửi dữ liệu ra ngoài. Tài liệu này ghi kết quả đo
để chọn model trước khi làm backend. Công cụ đo: [tools/checkpoint-summary/benchmark](../../tools/checkpoint-summary/benchmark/README.md).

## Điều kiện đo (01/11/2026)

- PC Intel i5-1135G7, Docker Desktop. Container `ollama/ollama:0.34.0` giới hạn **2 CPU / 3 GB, không swap**,
  giống ngân sách Ollama trên VPS. `OLLAMA_NUM_PARALLEL=1`, `OLLAMA_MAX_LOADED_MODELS=1`.
- 11 bài nộp **giả lập** tiếng Việt (PDF, DOCX, PPTX, 1–2 file mỗi bài), gồm một bài chứa prompt
  injection (F06), một bài gần như trống (F09) và một bài dài 35.731 ký tự (F11). Có đáp án cho
  44 yêu cầu checkpoint.
- Pipeline giống thiết kế backend: đọc text → che email/số điện thoại/MSSV → file dài tóm tắt từng
  đoạn → một lần gọi cuối trả JSON theo schema. Nhiệt độ 0,2.
- Thời gian đo trên PC; VPS có thể chậm hơn. Phải đo lại trên staging trước khi bật production.

## RAM: model nào vừa 3 GB

| Model | ctx 8k, KV mặc định | ctx 8k, KV q8_0 + flash attention | ctx 4k, KV q8_0 |
|---|---|---|---|
| qwen3:4b-instruct-2507-q4_K_M (2,5 GB) | Bị kill | Bị kill | Chạy, nhưng 6/11 bài bị kill khi prompt dài |
| qwen3.5:2b-q4_K_M (1,9 GB) | – | Chạy, đỉnh 2,98 GiB; F11 bị kill | – |
| qwen2.5:3b (1,9 GB) | – | Chạy, đỉnh 2,92 GiB | – |

Mọi lỗi HTTP 500 trong lượt đo đều là tiến trình model bị kill do vượt giới hạn RAM (log Ollama:
`signal: killed`). Container Ollama không chết, nhưng request đó thất bại.

## Kết quả chất lượng và thời gian

| Model | Chạy được | Đúng trạng thái yêu cầu | Báo Covered sai | Chi tiết chính có trong tóm tắt | Theo injection | Thời gian TB | p95 | F11 |
|---|---|---|---|---|---|---|---|---|
| qwen3.5:2b, ctx 8k | 10/11 | 30/40 (75%) | 3 | 37/39 (95%) | Không | 83 s | 208 s | Bị kill |
| qwen2.5:3b, ctx 8k | 11/11 | 31/44 (70%) | 2 | 32/44 (73%) | Không | 232 s | 1.148 s | 19 phút, 1/4 đúng |
| qwen3:4b, ctx 4k | 5/11 (3 JSON hợp lệ) | 10/12 | 0 | 9/10 | Không | 236 s | 352 s | Bị kill |

Chạy lại qwen3.5:2b với đoạn 3.000 ký tự: F04 202 s, F07 270 s (đỉnh 2,92 GiB), F11 vẫn bị kill.
Đoạn nhỏ hơn cũng làm F04 mất chi tiết (2/5 so với 4/5).

Nhận xét:

- **Tóm tắt nội dung** của qwen3.5:2b tốt: đúng tiếng Việt, giữ số liệu, không làm theo injection,
  không bịa nội dung cho bài gần như trống (F09 đúng 4/4). Có một lần tự cộng số liệu (F04 ghi
  "162 đối tượng" = 150 khảo sát + 12 phỏng vấn); prompt cần cấm tự tính số liệu mới.
- **Đối chiếu yêu cầu chưa đáng tin để hiện "đạt/không đạt"**: model có xu hướng báo Covered cho mọi
  yêu cầu. Ở các ca sai, trích dẫn là câu có thật nhưng thuộc mục khác, nên kiểm tra trích dẫn bám
  văn bản gốc (khớp nguyên văn hoặc ≥ 60% cụm 3 từ) không loại được lỗi này.
- Ngược lại, khi qwen3.5:2b báo **NotFound** thì yêu cầu đó thực sự không có trong tài liệu (0 ca bỏ sót).
- qwen2.5:3b chậm gần 3 lần và mất nhiều chi tiết hơn; qwen3:4b không chạy ổn định trong 3 GB.

## Kết luận

1. Chọn **qwen3.5:2b-q4_K_M**, ctx 8.192, `OLLAMA_FLASH_ATTENTION=1`, `OLLAMA_KV_CACHE_TYPE=q8_0`,
   `think: false`, nhiệt độ 0,2, container Ollama riêng cho tóm tắt.
2. **Giới hạn đầu vào 20.000 ký tự mỗi bài** (đã chốt). 3 GB không đủ cho tài liệu khoảng 35 nghìn
   ký tự. Phần vượt giới hạn không được tóm tắt; giao diện phải báo rõ "chỉ tóm tắt 20.000 ký tự
   đầu". Đo lại với giới hạn này (đoạn 6.000 ký tự, raw 4.000):

   | Bài | Ký tự đọc | Lần gọi | Thời gian | RAM đỉnh | Trạng thái đúng | Chi tiết chính |
   |---|---|---|---|---|---|---|
   | F04 | 4.755 | 2 | 133 s | 2,45 GiB | 4/4 | 5/5 |
   | F07 | 7.930 | 3 | 300 s | 2,62 GiB | 4/4 | 4/5 |
   | F11 | 20.000 (cắt từ 35.731) | 5 | 506 s | **2,96 GiB** | 4/4 | 4/5 |

   F11 chạy được nhưng RAM chỉ còn khoảng 37 MiB so với giới hạn 3 GB, và mất 8,4 phút. Vì vậy
   timeout mỗi bài phải khoảng 10 phút thay vì 6 phút; giới hạn ký tự đặt bằng cấu hình để hạ
   xuống nếu staging bị kill. Nếu VPS còn RAM, nâng container tóm tắt lên 3,5 GB để có khoảng an toàn.
3. Giao diện **không hiện Covered/Partial là "đạt"**. Mỗi yêu cầu hiện "Có nội dung liên quan" kèm
   câu trích để người xem tự kiểm tra, hoặc "Chưa thấy trong tài liệu" khi model báo NotFound.
4. Backend vẫn loại trích dẫn không bám văn bản gốc, nhưng đây không phải biện pháp chống báo sai.
   Kiểm tra mức liên quan giữa yêu cầu và trích dẫn bằng `bge-m3` là việc thử nghiệm tiếp, chưa có số liệu.
5. Với khoảng 1–4 phút mỗi bài trên PC, hàng đợi xử lý từng bài một và giao diện cần hiện trạng thái chờ.

Kết quả thô (`runs.jsonl`) nằm trong `artifacts/checkpoint-summary/results/`, đã Git ignore.

## Backend (PR1)

### Cách hoạt động

- Bản tóm tắt gắn với **team + checkpoint** (bảng `checkpoint_summaries`, migration `AddCheckpointSummaries`).
  Mỗi lần upload tạo một `Submission` mới, nên "bản mới nhất" là toàn bộ file và link đang hiển thị trên
  màn hình checkpoint, cộng câu trả lời theo yêu cầu của submission mới nhất.
- `POST .../teams/{teamId}/checkpoints/{n}/ai-summary` chỉ đưa vào hàng đợi (trả 202), không chờ model.
  Bấm lại khi đang chờ, hoặc khi nội dung không đổi, không tạo việc mới. Hàng đợi toàn hệ thống tối đa 30.
- `GET .../ai-summary` trả trạng thái `Disabled | None | Pending | Processing | Ready | Failed`, `isStale`
  khi nhóm đã đổi file/link sau lần tóm tắt, `inputTruncated` khi tài liệu dài hơn giới hạn ký tự.
  Frontend hỏi lại định kỳ khi đang chờ; không dùng realtime để không có đường đẩy nội dung tới sinh viên.
- Job nền `CheckpointSummaryGenerationService` xử lý từng bài một: đọc file mới nhất trước tới khi đủ
  20.000 ký tự, che email/số điện thoại/MSSV, file dài hơn 4.000 ký tự được tóm tắt theo đoạn 6.000 ký tự,
  rồi gọi model lần cuối với JSON schema. Lease 15 phút, timeout 10 phút mỗi bài, thử lại sau 1 và 5 phút,
  tối đa 3 lần. File không đọc được (scan, ảnh, mật khẩu) thất bại ngay, không thử lại.
- Không tải nội dung link: chỉ gửi tên link và tên miền.

### Quyền

- Policy `CheckpointReviewers` (Admin, Lecturer, Mentor) trên cả hai endpoint; handler kiểm tra thêm quan hệ
  với nhóm qua `CheckpointWorkspaceAccess`: giảng viên của lớp, mentor có phân công Active. Sinh viên luôn bị 403.
- Rate limit 10 lần tạo mỗi giờ cho mỗi người dùng. `UseRateLimiter` được chuyển xuống sau `UseAuthentication`
  để policy theo người dùng đọc được danh tính; các policy đăng nhập vẫn chia theo IP như trước.

### Đối chiếu yêu cầu

Mỗi yêu cầu nhận `Related`, `NotFound` hoặc `Unclear`, không bao giờ là đạt/không đạt:

- `Related`: câu trích có trong tài liệu (≥ 60% cụm 3 từ khớp) **và** chứa ít nhất một cụm 2 âm tiết của tên yêu cầu.
- `Unclear`: model báo có nhưng câu trích không có trong tài liệu (không hiển thị câu đó) hoặc không nhắc tới
  yêu cầu (vẫn hiển thị câu trích để người xem tự đánh giá).
- `NotFound`: model báo không có.

Áp quy tắc này lên kết quả benchmark qwen3.5:2b: 0 ca báo `Related` cho yêu cầu nhóm chưa làm (trước là 3),
nhưng chỉ 6/30 yêu cầu nhóm có làm được xếp `Related`, phần còn lại là `Unclear`. Ưu tiên không báo sai.

### Cấu hình

`Summarization:Provider` (`Disabled` mặc định hoặc `Ollama`), `BaseUrl`, `Model` (`qwen3.5:2b-q4_K_M`),
`ContextLength` (8192), `MaxInputCharacters` (20000), `JobTimeoutMinutes` (10), `RequestsPerUserPerHour` (10).
Không có secret. Container Ollama riêng và công tắc deploy thuộc PR3.

### Kiểm thử

- ApplicationTests: che thông tin liên hệ, chia đoạn, kiểm tra trích dẫn và mức liên quan, kiểm tra JSON của
  model, chống đóng thẻ dữ liệu trong prompt, đọc PDF/DOCX/PPTX (đúng thứ tự slide, chặn DTD, giới hạn ký tự).
- IntegrationTests (PostgreSQL): xếp hàng một lần cho giảng viên/mentor; 403 cho sinh viên, giảng viên lớp khác,
  mentor không phụ trách; không có nội dung (422), tắt tính năng, hàng đợi đầy; tạo bản tóm tắt, che liên hệ,
  `Related/Unclear/NotFound`, đánh dấu cũ khi nộp thêm; thử lại có backoff rồi `Failed`; file không đọc được;
  chia đoạn, cắt theo giới hạn và lấy lại lease hết hạn; API 401/403/200/503/429.
- Test opt-in với model thật: `EHUB_TEST_REAL_SUMMARY=1` và `EHUB_TEST_SUMMARY_OLLAMA_URL`. Đã chạy với
  container benchmark qwen3.5:2b: đạt (tóm tắt tiếng Việt, không làm theo injection, không báo `Related`
  cho yêu cầu gọi vốn không có trong tài liệu).
