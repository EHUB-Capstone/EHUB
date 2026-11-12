# Chạy AI phân mentor hiện tại

E-HUB chỉ sử dụng Gemini cho đánh giá AI. Backend tính điểm 70/15/15, chia đều theo học kỳ,
giữ phân công đã có/kế thừa và cho admin sửa tay trước khi chốt.

## Cấu hình server

- `Mentoring:MatchingProvider`: `Gemini` (mặc định khi không cấu hình). Giá trị khác bị từ chối.
- `Mentoring:Gemini:ApiKey`: key phía server, lưu trong ASP.NET Core User Secrets ở local
  hoặc environment/secret storage của VPS. Không đưa vào VITE_*, repository, log hay chat.
- `Mentoring:Gemini:DataTransferEnabled`: mặc định false; chỉ bật sau khi rà soát dữ liệu chuyên môn.
- `Mentoring:Gemini:Model` và `FallbackModels`: chỉ nhận ID được phép trong `GeminiMentorOptions`.
  Mặc định hiện tại là `gemini-3.8-flash`; danh sách dự phòng nằm trong cùng lớp cấu hình.
  Khả năng truy cập, quota và chi phí phụ thuộc project/provider; ứng dụng không xác minh billing.
- `Mentoring:Gemini:MinimumRequestIntervalSeconds`: mặc định 12, dùng chung giữa các request loại.
- `Mentoring:Gemini:RuleCatalogPath`: tùy chọn file alias chỉ phục vụ phát hiện phủ định rõ.
  Mặc định dùng resource `Mentoring/Models/mentor-evidence-catalog.json`, không có trọng số model.

Thiếu cấu hình hợp lệ làm validation provider thất bại. Key không xuất hiện trong URL request;
HTTP client chặn redirect và che header `x-goog-api-key` trong log.

## Local Windows

Từ thư mục repo:

```powershell
& '.\scripts\mentoring\configure-gemini.ps1'
```

Helper nhận key ẩn, gửi qua stdin vào dotnet User Secrets, không ghi key vào file repo,
không in key và không gọi API AI để cấu hình. Khởi động lại backend Development sau đó.

```powershell
docker compose -f docker-compose.local.yml up -d postgres
dotnet run --project backend/src/EHub.Api
```

Frontend chạy bằng `npm run dev` từ `frontend/`. Database, auth và các secret E-HUB khác
vẫn phải được cấu hình theo hướng dẫn local. Full Docker stack phải ghép override AI.

## VPS / Docker

`.env.staging.example`, `.env.production.example` và `backend/.env.example` chỉ chứa placeholder.
Trong môi trường server đã được duyệt, cấu hình:

```text
MENTOR_MATCHING_PROVIDER=Gemini
GEMINI_API_KEY=<server-secret-store>
GEMINI_MODEL=gemini-3.8-flash
GEMINI_FALLBACK_MODELS=gemini-3.5-flash-lite,gemini-3.1-flash-lite
GEMINI_DATA_TRANSFER_ENABLED=true
GEMINI_MINIMUM_REQUEST_INTERVAL_SECONDS=12
MENTOR_RULE_EXPERIENCE_WEIGHT=35
MENTOR_RULE_EXPERTISE_WEIGHT=25
MENTOR_RULE_STARTUP_DOMAIN_WEIGHT=15
MENTOR_RULE_TECHNOLOGY_WEIGHT=20
MENTOR_RULE_BIO_WEIGHT=5
```

Không gửi cấu hình chứa key thật cho AI. `GEMINI_DATA_TRANSFER_ENABLED=true` là lựa chọn
chỉ thực hiện sau khi dữ liệu được rà soát; masking không bảo đảm ẩn danh.

```bash
docker compose --env-file .env.staging -f docker-compose.staging.yml -f docker-compose.gemini.yml config --quiet
docker compose --env-file .env.production -f docker-compose.production.yml -f docker-compose.gemini.yml config --quiet
```

Các script deploy staging/production luôn ghép override Gemini và giữ các bước backup,
migration, kiểm tra SHA/image, khóa deploy và healthcheck. Khi thực sự chạy deploy, script chỉ
gỡ container model đã nghỉ thuộc đúng Compose project; không xóa database hoặc volume.
Thay đổi cấu hình không có nghĩa là được phép triển khai lên VPS; dùng quy trình duyệt release hiện có.

## Dữ liệu, chấm điểm và phân công

Input gồm tên/mô tả dự án, mã nội bộ tạm và năm phần chuyên môn đã lọc định danh:
kinh nghiệm, expertise/tags, lĩnh vực, công nghệ và giới thiệu/học vấn.
Không gửi CV gốc, thông tin liên hệ hoặc ghi chú/feedback của nhóm đã hướng dẫn.

Mỗi request chỉ chấm một loại mentor. AI trả tối đa hai ứng viên/dự án; mỗi ứng viên
chỉ có `id` và `criteria` gồm năm cặp `score`/`evidence`. Điểm dương phải trích nguyên văn
2–160 ký tự từ đúng trường tương ứng. Backend kiểm tra score nguyên 0–100, nguồn trích,
phủ định và dữ liệu thiếu/bị cắt. Không yêu cầu hoặc dùng điểm AI tổng thể.

```text
L = (35×KinhNghiem + 25×ChuyenMonTag + 15×LinhVuc + 20×CongNghe + 5×GioiThieuHocVan) / 100
H = 3 × min(5, SoNhomDaHuongDanCoBangChung)
A = 3 × min(5, SoDuAnCoThanhTichHopLe)
DiemCuoi = min(100, round(0.70×L + H + A))
```

Năm trọng số trường hồ sơ có thể cấu hình từ 0–100, chuẩn hóa theo tổng; không được đồng thời bằng 0.
Tỷ trọng tổng hiện tại cố định 70/15/15. Số buổi hoàn thành không tham gia điểm; trường đếm
trong response cũ còn được giữ để tương thích client. Đây là chính sách nghiệp vụ, chưa phải
trọng số tối ưu được hiệu chỉnh từ kết quả admin chốt hoặc tỷ lệ chính xác được kiểm chứng.

Backend ưu tiên tải thấp nhất trên cả học kỳ trong từng loại, sau đó mới xét điểm phù hợp.
N nhóm/M mentor có mục tiêu floor(N/M)–ceil(N/M); preview một lớp vẫn tính tải lớp khác.
Vị trí đã có mentor, kế thừa và sửa tay được giữ nguyên. Sửa tay có thể lệch mục tiêu;
admin được xem cảnh báo và chốt. Mentor ngoài shortlist có thể được ghép theo tải với 0 điểm
và cờ rà soát. Không tạo điểm dương khi chưa có bằng chứng.

Preview chưa lưu assignment. Chỉ admin accept mới lưu assignment, audit và Outbox cùng transaction.
Các kiểm tra quyền, loại mentor, team/class scope và optimistic concurrency tiếp tục áp dụng.

## Giới hạn và lỗi

- Hai loại chia sẻ deadline tổng 90 giây; HTTP client từng request tối đa 55 giây.
  Frontend đợi 110 giây; Nginx cho endpoint AI đợi 120 giây.
- Cache điểm chuyên môn trong bộ nhớ 2 phút, tối đa 32 bộ input; bonus lịch sử vẫn tính lại.
- Lỗi transport, timeout, output sai hoặc một loại thất bại làm toàn bộ preview báo lỗi an toàn.
- HTTP 404 bỏ qua model không có; 429 chỉ chuyển model khi xác nhận quota riêng của model.
  Quota chung/không rõ làm dừng, không quay key/project để né quota. 503 được báo là dịch vụ chưa sẵn sàng.
- Không âm thầm thay đánh giá AI bằng một thuật toán cục bộ khác.
- Chạy model trong test bằng phản hồi giả lập chỉ chứng minh logic và API, không chứng minh
  chất lượng hoặc độ sẵn sàng của dịch vụ AI thật.

## Kiểm thử

Backend, từ `backend/`:

```powershell
dotnet build EHub.slnx
dotnet test EHub.slnx --filter "FullyQualifiedName!~IntegrationTests"
dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj
```

Frontend, từ `frontend/`:

```powershell
npm run lint
npm run type-check
npm test
npm run build
```

Kiểm tra cấu hình bằng key giả và file example; không nạp secret thật để in Compose:

```powershell
$env:GEMINI_API_KEY = 'synthetic-compose-validation'
docker compose --env-file .env.staging.example -f docker-compose.staging.yml -f docker-compose.gemini.yml config --quiet
docker compose --env-file .env.production.example -f docker-compose.production.yml -f docker-compose.gemini.yml config --quiet
```

Không có migration trong lần dọn code AI. Mapping/schema vector lịch sử vẫn được giữ để
tương thích database và các migration đã merge; runtime mới không tạo hoặc sử dụng vector để xếp hạng.
