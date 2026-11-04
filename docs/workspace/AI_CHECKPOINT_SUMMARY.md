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

## Frontend (PR2)

- Card `CheckpointAiSummaryCard` nằm ngay trên phần Feedback của màn hình checkpoint, chỉ render khi vai trò là
  admin, giảng viên hoặc mentor (`canViewCheckpointSummary`). Ẩn trên giao diện không thay cho kiểm tra quyền ở backend.
- Hàng tiêu đề là nút đóng/mở có `aria-expanded`. Trạng thái dùng `Badge` tone semantic; nhóm yêu cầu dùng
  `Card tone="inset"`. Nút "Generate AI summary" chỉ hiện khi chưa có tóm tắt, tóm tắt lỗi hoặc đã cũ, khóa khi đang gửi.
- Khi `Pending/Processing`, card hỏi lại mỗi 15 giây; timer và request được hủy khi đổi checkpoint hoặc rời màn hình.
- Nhãn đối chiếu: "Related content found", "AI not sure", "Not found in documents" (không dùng đạt/không đạt).
  Luôn hiện cảnh báo AI có thể sai và không dùng để chấm điểm, cùng thời điểm tạo và model.
- Lỗi API được đổi thành câu dễ hiểu theo mã lỗi (tắt tính năng, hàng đợi đầy, chưa có nội dung, giới hạn mỗi giờ).
- Mock (`VITE_ENABLE_API_MOCKS=true`): bấm tạo chuyển Queued → Generating → Ready sau khoảng 12 giây; sinh viên nhận 403.
- Kiểm thử: `tests/checkpointSummary.test.ts` (quyền xem, điều kiện hỏi lại và cho phép tạo, nhãn không mang nghĩa
  đạt/không đạt, thông báo lỗi, đường dẫn API, luồng mock và 403 cho sinh viên, dọn timer/request).
  Đã chạy thử bằng dev server với mock: giảng viên tạo tóm tắt, card tự cập nhật sang Ready; kiểm tra dark mode và
  màn hình 375px không có cuộn ngang. Chưa chạy giao diện với backend thật.

## Tối ưu thời gian (prompt v2)

Thử nghiệm local với tài liệu thật cho thấy một bản tóm tắt mất 8m49s: 4 lần gọi model tóm tắt từng đoạn
(1m38, 1m26, 1m48, 0m56) và 1 lần tổng hợp (2m49). Ollama đã dùng đúng 2 luồng CPU nên không chỉnh được ở đó;
thời gian nằm ở số lần gọi và số token model phải đọc/sinh (khoảng 12 token/giây khi sinh trên 2 vCPU).

Thay đổi:

1. **Một lần gọi model.** Tài liệu dài được rút gọn **không dùng model**: chọn câu có số liệu, cụm từ của yêu cầu
   checkpoint, từ khóa nghiệp vụ, tiêu đề và phần mở đầu; câu gần trùng (nhật ký lặp lại) bị đẩy xuống; giữ thứ tự gốc;
   tổng khoảng 6.000 ký tự chia cho các file theo độ dài.
2. **Model không viết câu trích dẫn và tóm tắt từng file.** Backend tự chọn câu trích cho mỗi yêu cầu: câu trong tài
   liệu có chung cụm hai âm tiết với tên yêu cầu, nếu là tiêu đề thì lấy câu đầu tiên dưới tiêu đề. Câu trích luôn là
   văn bản thật. `num_predict` giảm còn 1000.
3. **Worker nhận việc ngay** qua tín hiệu khi có yêu cầu mới (trước đó chờ vòng lặp 10 giây); giao diện hỏi lại mỗi 5 giây.

Benchmark 11 bài giả lập, qwen3.5:2b, 2 CPU / 3 GB:

| | Trước (map-reduce) | Sau (một lần gọi) |
|---|---|---|
| Thời gian trung bình | 83 s | 43 s |
| p95 | 208 s | 62 s |
| Bài 20.000 ký tự (F11) | 506 s | 58 s |
| Bài 7.900 ký tự (F07) | 300 s | 62 s |
| Đúng trạng thái yêu cầu | 30/40 | 38/44 |
| JSON hợp lệ | 10/11 | 11/11 |
| RAM tối đa | 2,98 GiB | 2,93 GiB |

Đánh đổi: bài dài chỉ được model đọc phần đã chọn lọc, nên một số chi tiết phụ không vào ý chính (benchmark: 34/44 chi
tiết đặc trưng xuất hiện so với 37/39 trước đó, đo trước khi nâng số ý chính từ 4 lên 6). Giao diện ghi rõ khi tài liệu
được rút gọn. Số đo trên PC; VPS cần đo lại trên staging.

## Hủy tóm tắt và nguồn của từng nội dung (giai đoạn A)

- **Cancel**: `POST .../ai-summary/cancel` (Admin, Lecturer, Mentor có quyền với nhóm). Bản tóm tắt đang chờ hoặc
  đang chạy chuyển sang `Cancelled` (lưu dạng chuỗi, không cần migration). Nếu worker trong cùng tiến trình đang
  chạy, lần gọi model bị hủy qua `CancellationToken` nên Ollama dừng sinh chữ ngay. Bản tóm tắt trước (nếu có) vẫn
  hiển thị; hủy không tính là một lần thất bại. Với nhiều instance API, lần chạy ở instance khác vẫn chạy hết nhưng
  kết quả bị bỏ vì dòng dữ liệu không còn ở trạng thái `Processing`.
- **Nguồn của câu trích**: mỗi yêu cầu `Related` ghi tên file (hoặc "Câu trả lời theo yêu cầu") chứa câu trích.
- **Nguồn của ý chính**: backend gán mỗi ý chính cho file có nhiều cụm hai âm tiết trùng nhất (tối thiểu 2, không
  gán khi hai file hòa); model không phải ghi nguồn nên không tốn thêm token.
- **Trích nhanh từng file**: tối đa 3 câu đầy đủ (không phải tiêu đề) có điểm cao nhất của mỗi file, giữ nguyên văn,
  chọn bằng cùng bộ chấm điểm câu, không dùng model.
- **File mới**: file không có trong bản tóm tắt trước được gắn nhãn "New"; khi bản tóm tắt đã cũ, API trả `newFiles`
  là các file nộp sau lần tóm tắt.
- Mỗi lần tạo vẫn đọc lại toàn bộ file đang hiển thị và chỉ gọi model một lần; phần đọc lại file cũ chỉ tốn vài giây.
- Contract đổi: `keyPoints` là danh sách `{ text, source }`; file có `isNew` và `highlights` (bỏ `summary`);
  yêu cầu có `source`; response có `newFiles`. Bản tóm tắt đã lưu theo định dạng cũ được coi là cần tạo lại.

## Tóm tắt theo từng file (prompt v3)

Thử trên giao diện cho thấy bản tóm tắt gộp nhiều file chưa đủ tốt: các file chia nhau một ngân sách 6.000 ký tự và model
2B phải trộn tài liệu khác nhau vào vài câu. Thiết kế mới:

- **Mỗi file một bản tóm tắt**, lưu ở bảng `submission_file_summaries` (một dòng cho mỗi file version; migration
  `ReplaceCheckpointSummariesWithFileSummaries` xóa bảng `checkpoint_summaries` cũ và tạo bảng mới, `Down` làm ngược lại).
  Một file version đã nộp không đổi nên chỉ tóm tắt một lần; tóm tắt lại chỉ khi lần trước lỗi, bị hủy, hoặc prompt đổi.
- **Người xem chọn file** (mặc định chọn các file chưa có tóm tắt). `POST .../ai-summary` nhận `{ fileIds }` (1–5 file
  khác nhau, phải thuộc checkpoint của nhóm). Hàng đợi chung tối đa 30 file; worker xử lý **từng file một**, theo thứ tự
  yêu cầu và trong một yêu cầu thì **version mới nhất trước**. Giao diện hiện số file đang chờ phía trước.
- **Mỗi file dùng ngân sách riêng**: đọc tối đa `MaxInputCharacters` (30.000) ký tự, đưa cho model tối đa
  `FilePromptCharacters` (12.000, chỉnh được từ 2.000 đến 18.000) ký tự sau khi chọn câu quan trọng. Đo trên PC với
  qwen3.5:2b, 2 CPU / 3 GB: F04 70 s, F07 122 s, bài 35.000 ký tự 154 s; RAM tối đa 2,52 GiB (lượt đo này máy chậm hơn
  khoảng 2 lần so với lần đo trước).
- **Nội dung trong dropdown của từng file**: tổng quan, ý chính, các yêu cầu tìm thấy trong file kèm câu trích, câu quan
  trọng nguyên văn, câu hỏi gợi ý. Bảng "Checkpoint requirements" được tổng hợp từ các file đã tóm tắt mà không gọi thêm
  model: Related nếu có file chứa câu nhắc tới yêu cầu (kèm tên file), rồi Unclear, NotFound, NotChecked khi chưa có file nào.
- **Cancel** (`POST .../ai-summary/cancel`) dừng các file đang chờ và hủy lần gọi model của file đang chạy; file đã xong giữ
  nguyên. Nút Cancel dùng kiểu viền đỏ, icon X. Giao diện không hiển thị tên model (vẫn lưu trong database để truy vết).
- Câu trả lời theo yêu cầu do nhóm gõ trên màn hình không còn được đưa vào AI; chúng đã hiển thị trực tiếp trên màn hình
  checkpoint.

## Tăng tốc: tự xếp hàng, xem nhanh, câu trả lời ngắn (prompt v4)

Mục tiêu: người xem gần như không phải chờ. Bốn thay đổi (A–D):

- **A. Tự xếp hàng sau khi upload.** Khi sinh viên upload xong, `CheckpointFileUploadHandler` thêm một dòng
  `submission_file_summaries` (`IsAutomatic = true`, Pending) trong cùng `SaveChanges` với file. Worker vẫn chạy từng file
  một nên RAM không đổi; hàng tự động ưu tiên thấp hơn: yêu cầu bấm tay của giảng viên/mentor luôn chạy trước, và bấm
  chọn một file đang chờ tự động sẽ nâng nó lên (`IsAutomatic = false`). Giới hạn hàng: 30 yêu cầu bấm tay,
  200 file tự động (`CheckpointSummaryLimits`). Tắt bằng `Summarization:AutoSummarize = false`.
- **B. Câu trả lời tối thiểu.** Model chỉ viết `overview` (2 câu), `keyPoints` (tối đa 4) và `questions` (tối đa 2).
  Prompt tối đa `FilePromptCharacters = 5000` ký tự, `ContextLength = 4096`, `num_predict = 600`. File dài hơn được rút
  gọn bằng cách chọn câu quan trọng (không dùng model).
- **C. Xem nhanh không cần AI.** `CheckpointSummaryEvidence` chọn câu trích cho từng yêu cầu của checkpoint và từng tiêu chí
  rubric (`Kind = Requirement | Criterion`), dùng cặp từ không dấu và danh mục chủ đề/biệt danh của model mentor (bỏ qua
  câu phủ định như "chưa có kế hoạch gọi vốn"). `CheckpointSummaryPreviewService` (poll 2 giây) lưu kết quả vào `PreviewJson`
  nên màn hình hiện ngay khi file còn đang chờ hoặc đang chạy. Không có kết quả `Unclear` nữa: chỉ Related (có trích dẫn),
  NotFound, NotChecked.
- **D. Giữ model nóng.** `keep_alive` mặc định 30 phút (`Summarization:KeepAlive`) và `WarmUpAsync` nạp model khi worker khởi
  động. Cần container Ollama riêng cho tính năng này khi triển khai (provider mentor `Trained` không chạy Ollama).

Cấu hình mới: `ContextLength` 4096, `FilePromptCharacters` 5000, `KeepAlive` "30m", `AutoSummarize` true. Khởi động sẽ báo
lỗi nếu `FilePromptCharacters / 3 + 1200` vượt `ContextLength`. Migration: `AddFileSummaryAutoQueueAndPreview`
(`is_automatic`, `preview_json`).

## Bật/tắt theo môi trường

Tính năng không bắt buộc cho golive và chủ yếu phục vụ demo đồ án trên staging. Mỗi môi trường có một công tắc
trong file env trên VPS:

| Biến | Giá trị | Ý nghĩa |
|---|---|---|
| `CHECKPOINT_SUMMARY_PROVIDER` | `Disabled` (mặc định khi thiếu) / `Ollama` | Compose chuyển thành `Summarization__Provider` |
| `CHECKPOINT_SUMMARY_MODEL` | mặc định `qwen3.5:2b-q4_K_M` | Model Ollama dùng để tóm tắt |
| `CHECKPOINT_SUMMARY_AUTO` | mặc định `true` | Tự xếp hàng file sau khi upload |

Khi `Disabled`:
- Backend không đăng ký `CheckpointSummaryGenerationService` và `CheckpointSummaryPreviewService`, upload không
  xếp hàng, `GET .../ai-summary` chỉ trả `enabled: false` (không trả file hay bản tóm tắt cũ), yêu cầu tóm tắt và
  Cancel trả `WORKSPACE_SUMMARY_DISABLED`.
- Frontend không vẽ thẻ "AI summary". Không cần biến `VITE_*`, nên đổi công tắc không cần build lại frontend.
- Bảng `submission_file_summaries` vẫn được migration tạo, chỉ là rỗng. Dữ liệu cũ (nếu từng bật) được giữ nhưng
  bị ẩn; bật lại thì hiện lại.

Khi `Ollama`: script deploy khởi động container `ollama` (kể cả khi mentor dùng `Trained`) và tải model nếu chưa
có. Container giữ giới hạn sẵn có 2 vCPU / 3 GB.

Khuyến nghị: staging `Ollama` để demo; production `Disabled` cho golive. Script deploy là bản root-owned cài tại
`/opt/ehub/deploy/`, nên phải cài lại bản staging sau khi merge; production chỉ cần cài lại khi muốn bật.

## Chất lượng đầu ra (prompt v5)

Đối chiếu bản tóm tắt `Review 2.pdf` (39 slide, 136 ảnh, khoảng 100 ký tự chữ mỗi trang) với chữ gốc cho thấy:
- 4 ý chính có 1 ý đúng, 1 ý lấy mục lục làm "giai đoạn", 2 ý sai ("3 module", "đăng ký tài khoản doanh nhân").
- Bỏ sót phần phản hồi Review 1.
- Câu trích bị vỡ dấu ("TIÊ P").

Sáu thay đổi:

1. **Làm sạch chữ** (`CheckpointLayoutText`):
   - Nối âm tiết bị tách ở dấu thanh ("TIÊ P" → "TIÊP", "hế t" → "hết").
   - Bỏ tiêu đề lặp do slide động.
   - Nối dòng bị ngắt thành câu (chỉ với PDF/PPTX).
2. **Nhận biết file ít chữ:** PDF dưới 300 ký tự/trang hoặc slide dưới 80 ký tự/slide, từ 3 trang trở lên. Giao diện hiện cảnh báo, và prompt báo AI rằng nó chỉ thấy tiêu đề. Extractor nay trả thêm số trang/slide (`ExtractedText`).
3. **Kiểm tra ý chính có trong file** (`CheckpointSummaryGrounding`):
   - Bỏ ý chính có con số không có trong file.
   - Bỏ ý chính mà không đủ nửa số từ, hoặc ít nhất 2 thuật ngữ đặc trưng, có trong file.
   - Câu đầu của tổng quan luôn giữ, các câu sau cũng được kiểm tra.
   - Cách này bắt được chi tiết bịa, không bắt được việc hiểu sai những từ có thật.
4. **Prompt v5:** ưu tiên góp ý và trạng thái (DONE/PENDING/IN PROGRESS), kết quả, số liệu; mục lục không phải nội dung; không đoán.
5. **Câu then chốt:** phạt câu viết hoa toàn bộ, câu chứa mã sinh viên/email đã che và trang bìa; ưu tiên câu trọn vẹn có nhiều số liệu.
6. **Giao diện:** khi mọi yêu cầu/tiêu chí đều "Not found" thì thu thành một dòng.

Chạy lại `Review 2.pdf` với model thật (qwen3.5:2b, 2 vCPU), model trả lời trong 64 giây:
- 3 ý chính giữ lại: backup đúng; phản hồi Review 1 đã xử lý đúng; tính năng cảnh báo trùng ý tưởng và kho lưu trữ gần đúng (thêm tên "AI Advisor" không có trong file).
- Ý "giai đoạn" lấy từ mục lục bị bộ kiểm tra loại.
- Không còn câu trích vỡ dấu.
- Còn thiếu: kết quả restore. Câu hỏi gợi ý chưa được kiểm tra.

## Độ phủ và độ ổn định (prompt v6)

Đối chiếu `EXE2 - Check point 1.pdf` (pitch deck tiếng Anh, 24 trang, khoảng 12.800 ký tự) cho thấy bản rút gọn 5.000 ký tự
gửi cho AI chỉ còn bảng số liệu: phần vấn đề, giải pháp, đối thủ đều bị bỏ, nên AI ghép sai số (lấy quy mô thị trường
2 triệu làm mục tiêu) và rubric sai 4/5. Sáu thay đổi:

1. **Rút gọn theo phần:** mỗi phần (giữa hai tiêu đề, trên slide gần bằng một slide) giữ trước câu tốt nhất, sau đó
   mới thêm câu theo điểm (`CheckpointSummaryText.Select`, `IsTitle`).
2. **Từ khóa song ngữ** (problem, pain, customer, solution, market, competitor, revenue, pricing…). Dòng chỉ toàn số
   (dòng bảng) được cộng ít điểm hơn.
3. **Rubric khớp theo cả mô tả tiêu chí:**
   - Tên tiêu chí phải khớp đủ các từ dài ("BUSINESS WORKFLOW" không phải Business Model).
   - Tiêu đề không bao giờ được trích; nó cộng điểm cho các dòng nội dung ở hai bên.
   - Khi điểm bằng nhau, ưu tiên câu đầy đủ.
4. **Prompt v6:** hiện mô tả tiêu chí; mỗi ý chính ứng với một mục khác nhau trong danh sách cần xem; không ghép số của
   các phần khác nhau.
5. **Số liệu mâu thuẫn** (`CheckpointFigureConflicts`, không dùng AI):
   - Tỷ lệ chuyển đổi, biên lợi nhuận, churn, cổ phần: số % phải nằm trong 15 ký tự quanh từ khóa.
   - Tỷ trọng ngân sách: dòng có % và số tiền, cùng tên mục, không có năm/giai đoạn.
   - Hiện ở mục "Figures that disagree" kèm trích dẫn từng chỗ.
6. **Temperature 0, seed 42:** cùng file ra cùng kết quả.

Kết quả với model thật (qwen3.5:2b, 2 vCPU), mỗi file chạy 2 lần, cả hai lần ra **giống hệt nhau**:

| | EXE2 - Check point 1.pdf | Review 2.pdf |
|---|---|---|
| Thời gian gọi model | 23–35 giây | 23–25 giây |
| Ý chính | Giải pháp, thị trường, mô hình kinh doanh (ý "vấn đề" đúng nhưng bị bộ kiểm tra loại) | Phản hồi Review 1, các tính năng được góp ý, kho ý tưởng, backup |
| Rubric | 4/5 có trích dẫn (Presentation không có trong file, đúng) | 0/5 (đúng: slide review phần mềm) |
| Mâu thuẫn số liệu | 4: conversion 15%/30%, ngân sách AI/Cloud, Team, Administration | Không có |

Còn lại:
- Ý "thị trường" vẫn lấy 2 triệu (SAM) làm mục tiêu thu hút.
- Bộ kiểm tra đôi khi loại ý đúng khi AI diễn giải tiếng Việt từ slide tiếng Anh.
- Trích dẫn cho Problem Clarity và Solution Fit còn là dòng ngắn.
- Ý chính của Review 2 chưa ghi trạng thái DONE/PENDING.

## Tóm tắt theo đúng loại tài liệu (prompt v7)

Trên giao diện, v6 ép `Review 2.pdf` (slide review phần mềm) vào khung Giải pháp / Thị trường / Mô hình kinh doanh, và
model 2B bịa một "mô hình kinh doanh". Với `EXE2 - Check point 1.pdf`, nó vẫn lấy quy mô thị trường 2 triệu làm mục tiêu.
Năm thay đổi:

- **A. Chỉ dùng khung rubric khi file bám rubric:** phần không dùng AI phải thấy từ 2 tiêu chí trở lên có nội dung liên
  quan. Ngược lại, AI không được thấy danh sách tiêu chí, và được yêu cầu tóm tắt nội dung chính của chính tài liệu,
  không gắn nhãn.
- **B. Mục "Status reported in the file"** (`CheckpointStatusLines`, không dùng AI):
  - Dòng ghi trạng thái bằng chữ in hoa (DONE / PENDING / IN PROGRESS / TODO / HOÀN THÀNH…). Một dòng "DONE" đứng riêng
    lấy dòng giải thích ngay sau nó.
  - Hàng bảng ngắn có cột trạng thái ("Beta Launch In Progress …").
  - Khi file có dòng trạng thái, prompt yêu cầu AI ghi rõ trạng thái.
- **C. Bộ kiểm tra ý chính:**
  - Bỏ nhãn đầu ý ("Giải pháp:") trước khi kiểm tra.
  - Loại câu lấy quy mô thị trường làm mục tiêu: một con số có đơn vị (triệu, million…) mà trong file chỉ xuất hiện ở câu
    về TAM/SAM/"estimated"/"potential users", trong khi câu của AI nói "mục tiêu"/"thu hút"/"năm đầu".
- **D. Bằng chứng rubric có ngữ cảnh:** khi câu khớp chỉ là dòng ngắn, ghép thêm tối đa 3 dòng sau (khoảng 220 ký tự).
- **E. Câu hỏi:** file chủ yếu là hình thì không có câu hỏi; câu hỏi lấy quy mô thị trường làm mục tiêu thì bị bỏ.

Ngoài ra đã sửa một lỗi: trước đây mọi file PPTX đều bị báo với AI là "phần lớn là hình ảnh". Nay chỉ file ít chữ mới bị
báo như vậy.

## Đối chiếu với mục không dùng AI (prompt v8)

Sau v7, mỗi file còn một ý sai tinh vi. Năm thay đổi, đều không gọi thêm model:

1. **Ghép đề nghị với trạng thái** (`CheckpointStatusLines`): "1.Thêm chức năng…" đi với "1. PENDING: …". Mỗi dòng
   trạng thái có thêm trường `request`, hiện trên giao diện.
2. **Chữ in hoa → chữ thường đầu câu** trong mục trạng thái. Chỉ giữ nguyên các từ viết tắt đã biết (AI, MVP, API…) hoặc
   có chữ số; từ như "THU", "XIN" là từ thường.
3. **Loại ý chính mâu thuẫn với trạng thái:** ý nói "đã/hoàn thành/triển khai/xác nhận" về việc mà file ghi PENDING hoặc
   IN PROGRESS. Phải trùng từ khóa đủ nhiều: ít nhất 4 từ và ít nhất 40% từ của dòng trạng thái đó, để không loại
   nhầm khi chỉ trùng vài từ chung ("kỳ", "tự động", "ý tưởng").
4. **Chú thích số liệu mâu thuẫn trong ý chính:** ý nêu một giá trị của số liệu đang mâu thuẫn được thêm
   "(tài liệu cũng ghi 15%)".
5. **Loại con số lấy từ câu trong ngoặc kép** (tiêu đề bài đăng, khẩu hiệu): "5 lỗi formatting sinh viên gặp mỗi tuần"
   là tiêu đề video, không phải kết quả khảo sát.

Chạy lại hai file thật với model thật (qwen3.5:2b, 2 vCPU), đầu vào giống giao diện:
- **Review 2.pdf** (51 s): ý "Đã xác nhận thêm chức năng trao đổi… đồng thời triển khai AI" bị loại, vì file ghi PENDING và
  IN PROGRESS. Ý đúng về kho lưu trữ và backup được giữ.
- **EXE2 - Check point 1.pdf** (61 s): ý "5 lỗi định dạng mỗi tuần" bị loại. Ý tài chính được chú thích
  "(tài liệu cũng ghi 15%)".

## Hiểu rubric chính thức (prompt v9)

File `EXE Rubric.xlsx` (EXE101: 4 checkpoint, 21 tiêu chí; EXE201: 3 checkpoint, 13 tiêu chí) được import vào `rubric_criteria`:
cột `Description` để trống, toàn bộ nội dung nằm trong 4 mức (`LevelsJson`: Excellent/Good/Fair/Poor). Trước đây AI chỉ đọc
`Description` nên với rubric import nó chỉ có tên tiêu chí. Bốn thay đổi (không gọi thêm model):

1. **Mức Excellent làm mô tả tiêu chí** (`CheckpointCriterionProfile.ExcellentText`) khi `Description` trống. Mô tả viết tay
   vẫn được ưu tiên. `ChecklistItem` có thêm trọng số (`Weight`), loại tiêu chí (`Assessment`) và `Excellent`.
2. **Phân loại tiêu chí** theo tên (`CheckpointCriterionProfile.Classify`):
   - `Document`: file có thể cho thấy (roadmap, persona, business model, báo cáo bán hàng…).
   - `Product`: đánh giá trên sản phẩm hoặc demo (MVP, Product Completeness, Product Output).
   - `Live`: đánh giá trực tiếp hoặc theo thời gian (Presentation, Q&A, Demo Skills, Teamwork, Pitch, Progress & Effort).
   Tiêu chí `Live` không được tìm trong file, không bị báo "Not found" và không đưa vào lời nhắc của AI; tiêu chí `Product`
   mà file không nhắc tới hiện "Check the product".
3. **Trọng số**: hiện cạnh mỗi tiêu chí và xếp nặng trước (giao diện và lời nhắc). Phần "this file mentions none of the N
   criteria" chỉ tính tiêu chí `Document`. Khi ghép kết quả của nhiều file, kết quả của file được tóm tắt với rubric cũ
   (cùng vị trí nhưng khác tên tiêu chí) bị bỏ qua, nên không gán nhầm bằng chứng cho tiêu chí mới.
4. **Dấu hiệu theo mức Excellent** (`CheckpointCriterionCues`): tách "Clear, realistic roadmap; well-prioritized core features
   linked to user needs" thành Roadmap / Prioritization / Core features / User needs, rồi kiểm tra từng dấu hiệu có trong
   file không. Từ điển song ngữ Anh–Việt cho 28 khái niệm (persona, CJM, roadmap/lộ trình, MVP, canvas, TAM/SAM/SOM,
   sales, bills, feedback, retention…). Đây là có/không có dấu hiệu, không phải điểm.

Kết quả với rubric EXE201 Checkpoint 1 chính thức và 2 file thật (qwen3.5:2b, 2 vCPU, 63–75 giây):
- **EXE2 - Check point 1.pdf**: Roadmap có (Milestone Q3/2026 … Complete MVP Version 2), Core features có, Prioritization
  không; Persona có (Primary segment), CJM **không**; Marketing plan có. Demo Skills & Q&A: "Assessed live".
- **Review 2.pdf**: không có dấu hiệu nào của rubric này, nên AI tóm tắt nội dung riêng của tài liệu.

Còn lại: câu hỏi do AI sinh đôi khi hỏi điều file đã trả lời (số sinh viên khảo sát, doanh thu năm đầu). Hướng tiếp theo:
sinh câu hỏi từ các dấu hiệu chưa thấy (ví dụ "File chưa nhắc tới CJM").

## Bài học từ production: EXE101 Checkpoint 2

Nộp 4 file cùng lúc (khảo sát, báo cáo nghiên cứu thị trường, 2 bộ slide). Phát hiện và sửa:

- **File 62,2 MB không được tóm tắt ("Not summarised"):** giới hạn tóm tắt là 30 MB trong khi giới hạn upload là
  100 MB, và file bị bỏ qua âm thầm. Đọc chữ file này chỉ mất 0,5 giây, tốn thêm khoảng 65 MB bộ nhớ (xấp xỉ kích thước
  file), mỗi lần một file. Nay giới hạn tóm tắt bằng giới hạn upload; `R2SubmissionObjectStorage.DownloadAsync` đọc thẳng vào
  một mảng khi biết độ dài, không còn giữ file hai lần. File cũ đã nộp từ trước vẫn ở "Not summarised" cho tới khi chọn
  tóm tắt tay.
- **"Target Customer Definition" luôn "Not found":** các file tiếng Việt dùng "phân khúc", "khách hàng mục tiêu",
  "beachhead". Thêm khái niệm "Target customer" (tách khỏi "Persona") và từ khóa khảo sát (khảo sát, bảng hỏi, người
  trả lời, mẫu) cho "Research".
- **Ba tiêu chí cùng trích một câu:** bằng chứng của tiêu chí sau bị giảm điểm nếu câu đã được trích cho tiêu chí
  trước, nên câu gần nhất kế tiếp được chọn. Khi ghép nhiều file, chọn file nhắc tới nhiều dấu hiệu nhất.
- **Trạng thái "Done" sai:** "Đo lường hiệu quả thay vì chỉ hoàn thành khóa học" bị coi là dòng trạng thái. Hàng bảng chỉ
  nhận trạng thái tiếng Anh; tiếng Việt chỉ nhận khi viết hoa như nhãn.
- **Câu then chốt là hướng dẫn dashboard:** câu có emoji hoặc "hướng dẫn / menu / bên trái" bị trừ điểm.

## Vòng sửa sau lần test thứ hai (không đổi prompt)

Bốn file thật (khảo sát logistics, slide, báo cáo Rolly, slide F&B) cho thấy phần tóm tắt dùng được nhưng bằng chứng theo
rubric còn yếu. Các sửa sau đều chạy không cần mô hình:

- **Mâu thuẫn "Not found" + "Mentioned":** nếu dấu hiệu (cue) đã thấy thì tiêu chí là Related và dùng câu của cue làm bằng
  chứng. Dòng mục lục (sau "Table of Contents") không còn tính là đã nhắc tới.
- **Chọn bằng chứng:** dòng toàn chữ hoa dài (nhãn biểu đồ dính nhau, tên báo cáo) còn 40% điểm; tiêu đề mà dòng sau chỉ là
  ô bảng (dưới 3 từ) còn 60%; ngữ cảnh gắn thêm dừng ở ô bảng. Với tiêu chí chỉ hỏi về vấn đề, câu khuyến nghị
  ("Strategic implication", "đề xuất") còn 40%; với tiêu chí không hỏi đối thủ, dòng đối thủ ("giải pháp thay thế") còn 50%.
- **Câu hỏi gợi ý:** bỏ câu có con số không có trong file hoặc có đối chiếu file không nêu ("trong khi các doanh nghiệp khác…").
- **Sửa chữ PDF:** chuẩn hóa NFC trước khi nối âm tiết (dấu rời như "bằ ng" mới nối được), nối "gi á trị" và "đầ u".
- **Số liệu lệch giữa các file:** mỗi file lưu tối đa 40 câu có phần trăm hoặc cỡ mẫu (`figures`). Khi trả kết quả, so các
  file với nhau: cỡ mẫu khác nhau (N = 51 và 52 doanh nghiệp) và cùng một câu (trùng 4 từ liền nhau, ví dụ "bộ chứng từ cũ")
  nhưng phần trăm khác (84,3% và 82,7%). Kết quả nằm ở `conflicts` của response, mỗi giá trị kèm tên file. Đây là gợi ý để
  hỏi nhóm, không kết luận file nào đúng. Bản tóm tắt cũ chưa có `figures` nên chỉ so được sau khi tóm tắt lại.

## Vòng sửa thứ ba: ưu tiên độ chính xác của bằng chứng

Chạy thẳng bộ đọc PDF của app trên bốn file thật (khảo sát, slide, báo cáo Rolly, slide F&B) cho thấy phần lớn lỗi nằm ở chữ
đầu vào chứ không ở mô hình. Nguyên tắc mới: **thà không trích còn hơn trích sai**.

- **Dòng không bao giờ làm bằng chứng:** câu hỏi, dòng nhãn biểu đồ (từ 3 con số trở lên, mỗi con số theo sau một nhãn viết hoa),
  dải chữ hoa, hướng dẫn dashboard (emoji, "menu", "bên trái"), câu khuyến nghị với tiêu chí chỉ hỏi về vấn đề, hàng bảng đối
  thủ với tiêu chí không hỏi đối thủ. Các dòng này cũng không tính là "Mentioned".
- **Tiêu đề không có nội dung:** tiêu đề mà dòng ngay sau là ô bảng, đáp án biểu đồ hoặc mục đánh số khác không được trích.
  Nếu file có nhắc tới ("Mentioned") mà không có đoạn đáng trích, tiêu chí vẫn là Related nhưng không có trích dẫn; giao diện
  ghi "Mentioned in the file, but no single passage stands out to quote".
- **Một câu không trích hai lần** cho hai tiêu chí khi không còn câu nào khác.
- **Dòng dẫn vào danh sách** (kết thúc bằng dấu hai chấm) được trích cùng các dòng sau nó.
- **Chữ PDF:** nối dòng khi dòng trước kết thúc bằng "bằng/và/của…" hoặc dòng sau bắt đầu bằng "&"; sửa lại âm tiết sau khi nối
  dòng ("gi" + "á" + "trị" → "giá trị"). Trước đây "gi á trị" còn sót vì chỉ sửa từng dòng riêng lẻ.
- **Câu then chốt:** bỏ dòng "N năm kinh nghiệm" (hồ sơ người được phỏng vấn).
- **Từ khóa khách hàng mục tiêu:** thêm "tệp N doanh nghiệp/khách hàng…", "customer base".
- **Kiểm tra tóm tắt:** khoảng số ("15-30") phải xuất hiện đúng cặp trong file; file chủ yếu là hình đòi 75% từ của ý chính có
  trong chữ đã đọc (thường 50%).
- **Số liệu lệch giữa các file:** phần trăm được gắn với cụm từ đứng gần nhất; dòng liệt kê nhiều phần trăm không còn bị
  ghép nhầm (cảnh báo sai "84,3% so với 98%"). Cùng một cặp giá trị giữa hai file chỉ báo một lần.

## Rà soát mọi checkpoint EXE101 và EXE201 (prompt v10)

Chạy 29 file (7 checkpoint, rubric thật từ `EXE Rubric.xlsx`, mô hình `qwen3.5:2b-q4_K_M`). File thật chỉ có ở EXE101 CP2 và
EXE201 CP1; phần còn lại là file mô phỏng. Phát hiện và sửa:

- **Mô hình bịa nội dung theo rubric:** khi file chỉ khớp một phần, prompt đưa cả danh sách tiêu chí và mô hình viết cho cả mục
  không có (canvas được viết "phương pháp nghiên cứu"). Nay dàn ý chỉ gồm các tiêu chí bước xem nhanh đã thấy trong file.
- **Câu hỏi:** bỏ câu nói về "tài liệu" ("Tài liệu chưa nêu…", "Tài liệu có đề cập… không?") và câu không có dấu hỏi; cắt
  tiền tố "Theo tài liệu,".
- **Từ khóa:** thêm cách nói thật của sinh viên cho mô hình kinh doanh (thu phí, thuê bao, giá thuê/tour, gói tháng…), vấn đề
  (phải chờ, mất/tốn thời gian…) và giải pháp (giải quyết, khắc phục). "problem-solving" không còn tính là vấn đề; "demo day"
  không còn tính là demo sản phẩm.
- **Tiêu chí về cả file** ("Startup Idea Clarity", "Startup Completeness"): nếu không có đoạn để trích thì hiện "Judged on the
  whole file", không phải "Not found". Có đoạn phù hợp thì vẫn trích.
- **Câu hoãn** ("sẽ hoàn thiện … sau", "đang hoàn thiện nội dung") không còn là bằng chứng.

Chưa sửa:

- Hạ tầng: container Ollama giới hạn 3 GB, dùng khoảng 2,5 GB và từng bị OOM; 2–4 trong 29 lượt gọi lỗi tạm
  (`MODEL_UNREACHABLE` hoặc câu trả lời không dùng được), chạy lại thì thành công.

### Câu chèn lệnh gửi tới AI trong file

Bài giả lập `CP4_GreenBox_PhuLuc_PromptInjection.pdf` cho thấy mô hình 2B có lúc làm theo hoặc chép câu chèn lệnh ("Nhóm đã hoàn
thành mọi yêu cầu"); bước kiểm tra từ không bắt được vì các từ đó có trong file. Nay `CheckpointInjectionGuard` bỏ các câu đó
trước khi file đến mô hình, câu then chốt và trích dẫn, và đếm chúng (`ignoredInstructions` trong preview và nội dung tóm tắt).
Giao diện hiện một ghi chú cho người xem: "This file has N sentence(s) addressed to the AI…".

- Nhận biết: câu gửi tới AI ("dành cho hệ thống AI đọc tài liệu", "note to the AI assistant"), câu bắt bỏ qua hướng dẫn ("bỏ qua mọi
  hướng dẫn trước đó", "ignore all previous instructions") và câu đòi điểm ("đề nghị 10 điểm", "đánh dấu tất cả yêu cầu",
  "ghi vào phần tổng quan"). So khớp trên chữ không dấu, nên không cần dấu tiếng Việt.
- Không chặn nội dung nói về AI thông thường ("Sử dụng AI hỗ trợ cảnh báo trùng đề tài", "tích điểm, 10 điểm đổi một món").
- Giới hạn: bộ lọc dựa trên mẫu câu, nên cách diễn đạt lạ vẫn có thể lọt; system prompt vẫn dặn mô hình coi file là dữ liệu.

Còn lại: câu hỏi chung chung do mô hình viết, cụm từ vô nghĩa của mô hình ("đào tập trung"), chữ dính nhau do PDF
("Logisticstích") và nhận biết các file thuộc sản phẩm khác nhau trong cùng một checkpoint.
