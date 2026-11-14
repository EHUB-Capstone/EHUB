# AI Summary v12: bằng chứng, phiên bản và phục hồi khi lỗi

Giữ nguyên `qwen3.5:2b-q4_K_M`, Ollama local, một lượt gọi model mỗi file và các giới hạn tài nguyên hiện có.
Backend vẫn quyết định bằng chứng và quyền truy cập. Model không chấm điểm sinh viên.

## Nhận định có nguồn

- Mọi câu tổng quan, kể cả câu đầu, đều đi qua kiểm tra nguồn. Câu bị loại không được phục hồi khi không còn câu nào hợp lệ.
- Mỗi nhận định phải được hỗ trợ bởi một đoạn cụ thể. Chọn tối đa 12 câu ứng viên theo độ trùng từ rồi kiểm tra số liệu,
  đơn vị tiền, quy mô thị trường và từ ngữ trên từng câu. Không chấp nhận việc lấy từ/số ở các đoạn khác nhau để tạo một sự kiện mới.
- Đoạn trích tối đa 300 ký tự. `overviewEvidence` và `keyPointEvidence` chứa nhận định, câu nguồn và trang/slide nếu xác định được.
  Chỉ backend tạo các trường này; không dùng trích dẫn hoặc số trang do model tự viết.
- Giao diện cho mở nguồn, sao chép hoặc đưa câu trích vào góp ý. Danh sách bằng chứng thu gọn để tránh làm phần tóm tắt quá dài.
- Nếu tổng quan của model không kiểm chứng được, dùng câu then chốt đã lấy từ tài liệu và đặt `overviewFromSource`.
  Nếu không có câu trích phù hợp, hiển thị rõ chưa đủ bằng chứng. Không dựng một tổng quan thay thế bằng suy đoán.
- Dòng trạng thái và cảnh báo số liệu của backend tiếp tục được ưu tiên hơn phần diễn giải của model.

Đây là kiểm tra bằng luật và nguồn trích, không phải bảo đảm hiểu đúng ngữ nghĩa. Một diễn giải hợp lý có thể bị loại,
đặc biệt khi dịch giữa Anh–Việt hoặc khi bằng chứng nằm trong một đoạn dài. Người review vẫn cần đối chiếu nguồn.

## Làm mới đúng khi cần

`needsRefresh` chỉ bật cho bản `Ready` khi phiên bản phân tích cũ, JSON không đọc được hoặc checklist thay đổi.
Checklist được đánh dấu bằng SHA-256 của yêu cầu, tên/mô tả tiêu chí, trọng số, loại đánh giá và nội dung Excellent.
Metadata `_checklistFingerprint` nằm trong JSON đã có. Mỗi kết quả mới có `_generationId` riêng để phân biệt cả các lần
tạo lại dùng cùng một prompt và rubric.

- `GET` chỉ thông báo cần cập nhật, không tự gọi model.
- Bản cũ vẫn mở được, có cảnh báo và nút **Tạo lại**. Không dùng bằng chứng của checklist cũ trong bảng tổng hợp hiện tại.
- Yêu cầu tạo lại vẫn qua phân quyền, validation, rate limit và giới hạn hàng đợi hiện có.
- Bản đúng phiên bản/checklist không tạo thêm công việc. `Pending`/`Processing` không bị khởi động lại vì bấm nhiều lần.
- Bản cũ thiếu metadata được coi là cần cập nhật. Phản hồi cũ vẫn được giữ trong dữ liệu thống kê, nhưng không hiển thị
  như đánh giá của người dùng cho bản vừa tạo lại; người review có thể đánh giá bản mới.
- `generationId` được gửi cùng phản hồi và khi rút phản hồi (query parameter của DELETE). Nếu người dùng đang xem bản đã bị thay thế, API trả `409`; phản hồi cũ về trễ
  cũng không được gắn vào bản mới trong giao diện. Client cũ chưa gửi mã vẫn được hỗ trợ trong thời gian rollout.

Frontend chấp nhận thiếu các trường mới trong thời gian deploy từng phần. Cần deploy backend và frontend để dùng đầy đủ nút làm mới và nguồn nhận định.

### Migration và rollback

`20261112185043_AddSummaryFeedbackGeneration` thêm duy nhất cột UUID nullable `summary_generation_id` vào
`checkpoint_summary_feedback`. Không backfill: không thể xác định chắc bản tóm tắt nào đã được đánh giá trong dữ liệu cũ.
`Up` giữ các verdict cũ; `Down` chỉ bỏ cột này. Không sửa migration đã merge. Khi rollback, đưa ứng dụng về bản không
dùng cột này trước khi chạy `Down`; rollback mất liên kết generation nhưng không xóa verdict.
Migration được chạy trên database tạm của integration tests. Chưa apply lên staging/production.

## Retry và circuit breaker

Thời gian chờ được tính từ **lúc lỗi thực sự trả về**, thay vì lúc job bắt đầu. Ví dụ job timeout sau 10 phút thì
backoff 1 phút bắt đầu sau phút thứ 10; cooldown 2 phút cũng còn đủ 2 phút khi circuit mở.
Nếu đồng hồ lùi, thời điểm kết thúc được chặn dưới bằng thời điểm bắt đầu. Lease, kiểm tra quyền sở hữu và các điều kiện
ghi kết quả giữ nguyên. Không thay đổi số lần thử hay ngân sách CPU/RAM.

## Kiểm thử và giới hạn xác minh

Các ca hồi quy dùng dữ liệu giả kiểm tra: số người bịa, đổi đồng thành USD, nhầm quy mô thị trường thành mục tiêu,
ghép số từ hai đoạn, câu trích/trang, đổi nội dung rubric nhưng giữ tên, đổi prompt, refresh lặp, phản hồi sau refresh,
timeout dài và thời gian nghỉ của circuit. Các ca chống bịa ban đầu đã được chạy đỏ trước khi sửa.

Chạy theo `AGENTS.md`: build solution, application/unit tests, integration tests PostgreSQL, lint/type-check/test/build frontend.
Integration test dùng storage/model giả để xác định hành vi; test Qwen thật và benchmark bộ file cần opt-in riêng.
Không suy ra độ chính xác của model hoặc thời gian trên VPS từ số test xanh. Kết quả lệnh của đợt nâng cấp nằm trong
`artifacts/summary-audit/` trên máy thực hiện, không commit vào Git.

### Xác minh local ngày 13/11/2026

| Lệnh (trong thư mục tương ứng) | Kết quả |
| --- | --- |
| `dotnet build EHub.slnx --no-restore -m:1` | Đạt, còn cảnh báo hiện có |
| `dotnet test EHub.slnx --no-build --no-restore --filter "FullyQualifiedName!~IntegrationTests"` | 1.200 application + 141 unit đạt; 1 benchmark opt-in bỏ qua |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build --no-restore` | 668 đạt; 2 test cần model thật bỏ qua |
| `npm run lint` / `npm run type-check` | Đạt; lint còn cảnh báo hiện có |
| `npm test` | 635 đạt |
| `npm run build` | Đạt |

Lượt tích hợp đầu không tạo được database vì Docker Engine local chưa chạy. Sau khi khởi động Docker, toàn bộ lượt chạy lại đạt;
migration mới được apply trên PostgreSQL tạm của Testcontainers. Không apply lên database người dùng hoặc VPS.
Cache model E-HUB đã kiểm tra không có đúng Qwen cần cho lượt thử thật; không tải model khác để thay thế.

OCR, hỏi đáp tài liệu và bộ nhớ tiến độ dự án chưa được bổ sung trong v12. Các bước này cần bộ kiểm định và đo tài nguyên riêng.
