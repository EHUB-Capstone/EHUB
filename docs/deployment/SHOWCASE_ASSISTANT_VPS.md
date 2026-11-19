# Bật chatbot Showcase trên VPS

Chatbot Hubby dùng Gemini thật để tìm và giải thích các dự án đã công bố trên Showcase.
Tính năng mặc định tắt. **Máy chủ không giữ key AI nào cho chatbot**: mỗi người dùng tự dán API key
Gemini của mình vào khung chat. Key chỉ nằm trong bộ nhớ của tab trình duyệt, được gửi kèm từng câu
hỏi qua HTTPS và chỉ dùng cho câu hỏi đó; backend không lưu và không ghi log key.

## Cấu hình Docker Compose

Ghép `docker-compose.gemini.yml` với Compose staging hoặc production hiện có. Trong file env
riêng trên VPS, đặt các biến không nhạy cảm sau:

```dotenv
SHOWCASE_ASSISTANT_ENABLED=true
SHOWCASE_ASSISTANT_MODEL=gemini-3.5-flash-lite
SHOWCASE_ASSISTANT_FALLBACK_MODELS=gemini-3.1-flash-lite
SHOWCASE_ASSISTANT_REQUESTS_PER_USER_PER_MINUTE=8
```

Không còn biến `SHOWCASE_ASSISTANT_API_KEY`, và chatbot không dùng key mentor `GEMINI_API_KEY`. Nếu
file env cũ còn biến đó thì xóa đi; backend bỏ qua nó. Không ghi key của người dùng vào repo, `VITE_*`,
lệnh shell, log hoặc chat.

Các biến Compose được chuyển sang tên cấu hình .NET tương ứng:

| Biến trong file env của Compose | Biến môi trường trong backend |
| --- | --- |
| `SHOWCASE_ASSISTANT_ENABLED` | `Showcase__Assistant__Enabled` |
| `SHOWCASE_ASSISTANT_MODEL` | `Showcase__Assistant__Model` |
| `SHOWCASE_ASSISTANT_FALLBACK_MODELS` | `Showcase__Assistant__FallbackModels` |
| `SHOWCASE_ASSISTANT_REQUESTS_PER_USER_PER_MINUTE` | `ShowcaseAssistantRateLimiting__RequestsPerUserPerMinute` |

`backend/.env.example` dùng tên biến .NET ở cột phải cho cách chạy backend trực tiếp. Nếu nền
tảng deploy cấp environment variables trực tiếp cho container, dùng các tên .NET đó. ASP.NET
Core User Secrets chỉ phục vụ local; VPS cần cấu hình riêng bằng environment/secret storage.

## Deploy và nạp cấu hình mới

Deploy backend và frontend từ cùng commit đã review theo quy trình hiện có. Nếu pipeline lấy
nhánh `develop`, merge nhánh `AI` trước khi deploy. Chatbot không có migration mới; các migration
của tính năng khác vẫn theo quy trình khởi tạo database hiện có.

Ví dụ kiểm tra cấu hình và tạo lại container backend staging sau khi đã cập nhật image/env:

```bash
cd /opt/ehub/staging/app
docker compose --env-file .env.staging -f docker-compose.staging.yml -f docker-compose.gemini.yml config --quiet
docker compose --env-file .env.staging -f docker-compose.staging.yml -f docker-compose.gemini.yml up -d --no-deps --force-recreate backend
```

Production dùng `.env.production` và `docker-compose.production.yml` thay cho hai file staging.
Chỉ `docker restart` không nạp lại biến môi trường mới. Không chạy `docker compose config`
không có `--quiet` để tránh in các secret đã nội suy.

Route `/api/showcase/assistant/chat` dùng riêng ngân sách AI: frontend Nginx chờ 120 giây,
Nginx host staging/production chờ 210 giây, đủ cho client 90 giây và tối đa ba model × 25 giây.
Các API thông thường vẫn chờ 60 giây. Build/deploy frontend mới để nạp cấu hình Nginx bên trong
container; cập nhật file host tương ứng trong `deploy/nginx/`, chạy `nginx -t` rồi reload Nginx
theo quy trình deploy hiện có. Chỉ cập nhật backend không thay cấu hình proxy host đang chạy.

## Kiểm tra sau deploy

Đăng nhập rồi mở `/showcase`, chọn **Hỏi Hubby** và hỏi về một dự án đã công bố trong database
của môi trường đó. API `POST /api/showcase/assistant/chat` yêu cầu đăng nhập, có hạn mức theo
người dùng và yêu cầu header `X-Gemini-Api-Key` chứa key của chính người hỏi (thiếu thì trả
`SHOWCASE_ASSISTANT_KEY_REQUIRED`, Google từ chối thì trả `SHOWCASE_ASSISTANT_KEY_INVALID`, đều HTTP 400).
Model nhận câu hỏi, các lượt hội thoại gần nhất và thông tin dự án đã công bố, dưới tài khoản Google
của người dùng; chỉ bật khi chấp nhận việc gửi các dữ liệu này đến Google.

Card dự án được dựng lại từ database theo mã model chọn. Bản nháp và dự án ẩn không nằm trong
catalog. Nếu chưa có dự án Published, chatbot báo chưa có dữ liệu và không gọi AI.

- Thông báo chưa bật: kiểm tra `SHOWCASE_ASSISTANT_ENABLED` và việc container đã được tạo lại sau khi
  thay env.
- "Google không chấp nhận API key này" hoặc "đã hết hạn mức": key của người dùng sai, bị thu hồi, bị
  giới hạn hoặc hết hạn mức; họ tự thay key khác. Đây không phải lỗi máy chủ.
- Lỗi tạm thời: chờ rồi thử lại; không có phản hồi giả lập thay thế trong code sản phẩm.

Mười dự án mẫu từng dùng để test local được lưu trong database local riêng, không nằm trong
Git và không tự xuất hiện trên VPS sau deploy. Cần công bố dự án của môi trường đó bằng luồng
quản lý Showcase bình thường. Xem thêm [hướng dẫn Gemini hiện tại](GEMINI_MENTOR_VPS.md).

## Local Windows

Chạy helper rồi khởi động lại backend Development:

```powershell
& '.\scripts\showcase\configure-showcase-assistant.ps1'
```

Helper chỉ bật chatbot và chọn model trong User Secrets của `backend/src/EHub.Api`. Nó không hỏi
key, không sửa key mentor và không gọi AI; người dùng dán key của mình vào khung chat khi dùng.
