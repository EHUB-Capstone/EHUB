# Bật chatbot Showcase trên VPS

Chatbot Hubby dùng Gemini thật để tìm và giải thích các dự án đã công bố trên Showcase.
Tính năng mặc định tắt. **Hệ thống không có key AI nào của riêng nó cho chatbot**: mỗi người dùng tự dùng
API key Gemini của mình và chọn nơi giữ key khi nhập:

- **Trang này** (mặc định): key chỉ nằm trong bộ nhớ của trang, mất khi tải lại. Key được gửi kèm từng câu
  hỏi qua HTTPS và chỉ dùng cho câu hỏi đó; backend không ghi log key.
- **Tab này**: key được giữ trong bộ nhớ tạm của tab (sessionStorage, riêng cho từng tài khoản, tối đa một
  ngày) đến khi họ đóng tab, đăng xuất hoặc đổi key.
- **Tài khoản** (tùy chọn, chỉ có khi máy chủ được cấu hình khóa mã hóa, xem mục
  [Lưu key vào tài khoản](#lưu-key-vào-tài-khoản-tùy-chọn)): key được lưu **mã hóa** trên máy chủ và tự dùng
  mỗi khi người dùng hỏi, trên mọi thiết bị và sau khi đăng nhập lại.

Key không bao giờ nằm trong localStorage hay cookie. Cuộc trò chuyện được giữ trong tab (không kèm key) để
không mất khi tải lại trang, và bị xóa khi đăng xuất.

## Cấu hình Docker Compose

Ghép `docker-compose.gemini.yml` với Compose staging hoặc production hiện có. Trong file env
riêng trên VPS, đặt các biến không nhạy cảm sau:

```dotenv
SHOWCASE_ASSISTANT_ENABLED=true
SHOWCASE_ASSISTANT_MODEL=gemini-3.5-flash-lite
SHOWCASE_ASSISTANT_FALLBACK_MODELS=gemini-3.1-flash-lite
SHOWCASE_ASSISTANT_REQUESTS_PER_USER_PER_MINUTE=8
SHOWCASE_ASSISTANT_KEY_REQUESTS_PER_USER_PER_MINUTE=30
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
| `SHOWCASE_ASSISTANT_KEY_REQUESTS_PER_USER_PER_MINUTE` | `ShowcaseAssistantRateLimiting__KeyRequestsPerUserPerMinute` |
| `SHOWCASE_ASSISTANT_KEY_ENCRYPTION_ACTIVE_ID` | `Showcase__Assistant__KeyEncryption__ActiveKeyId` |
| `SHOWCASE_ASSISTANT_KEY_ENCRYPTION_KEY_1` … `_3` | `Showcase__Assistant__KeyEncryption__Keys__1` … `__3` |

Hai biến cuối **là bí mật**: chỉ đặt trong secret store/file env riêng trên VPS (xem mục lưu key bên dưới),
không đặt trong ví dụ ở trên.

`backend/.env.example` dùng tên biến .NET ở cột phải cho cách chạy backend trực tiếp. Nếu nền
tảng deploy cấp environment variables trực tiếp cho container, dùng các tên .NET đó. ASP.NET
Core User Secrets chỉ phục vụ local; VPS cần cấu hình riêng bằng environment/secret storage.

## Deploy và nạp cấu hình mới

Deploy backend và frontend từ cùng commit đã review theo quy trình hiện có. Nếu pipeline lấy
nhánh `develop`, merge nhánh `AI` trước khi deploy. Việc lưu key vào tài khoản có **một migration**
(`AddShowcaseAssistantUserKeys`): nó chỉ tạo bảng `showcase_assistant_user_keys` (khóa chính `user_id`, khóa
ngoại tới `users` có xóa theo tầng), không đổi bảng nào khác; `Down` chỉ xóa bảng đó. Áp dụng theo quy trình
khởi tạo database hiện có; không tự áp dụng bằng tay lên staging/production. Các migration của tính năng
khác vẫn theo quy trình hiện có.

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

## Lưu key vào tài khoản (tùy chọn)

Người dùng không muốn dán lại key mỗi lần đăng nhập có thể chọn **Tài khoản** khi nhập key. Máy chủ mã hóa
key bằng AES-256-GCM rồi lưu vào bảng `showcase_assistant_user_keys` (mỗi tài khoản một dòng). Tính năng chỉ
bật khi máy chủ có **khóa mã hóa**; chưa cấu hình thì giao diện không hiện lựa chọn này và `PUT` trả
`503 SHOWCASE_ASSISTANT_KEY_STORAGE_UNAVAILABLE`. Máy chủ không bao giờ lưu key chưa mã hóa.

### Mức bảo vệ (nói thẳng)

| Nguy cơ | Kết quả |
| --- | --- |
| Lộ database hoặc file sao lưu database | **Được bảo vệ**: chỉ có bản mã hóa và 4 ký tự cuối; thiếu khóa mã hóa thì vô dụng. |
| Người dùng này đọc key của người dùng khác | **Được bảo vệ**: mọi thao tác chỉ theo tài khoản đang đăng nhập; bản mã hóa gắn với chủ sở hữu nên chép sang dòng của người khác cũng không giải mã được. |
| API lộ key | **Được bảo vệ**: không có endpoint nào trả key, chỉ trả "có/không" và 4 ký tự cuối. |
| XSS trên trang web | Script không đọc được key (trang không giữ key sau khi lưu) nhưng vẫn có thể *dùng* chat dưới phiên đang đăng nhập. |
| Người có cả quyền VPS lẫn database (quản trị hệ thống) | **Không được bảo vệ**: họ đọc được khóa mã hóa trong môi trường và giải mã được key. Người dùng nên dùng key của một dự án Google **không bật thanh toán** và có thể xóa key bất cứ lúc nào. |

### Cấu hình

1. Tạo khóa mã hóa 32 byte ngẫu nhiên, mã hóa Base64, **trên máy của người quản trị**, không dùng lại khóa của
   môi trường khác (ví dụ `openssl rand -base64 32`). Không dán nó vào repo, chat hay log.
2. Đặt vào secret store/file env riêng của VPS:
   `SHOWCASE_ASSISTANT_KEY_ENCRYPTION_ACTIVE_ID=1` và `SHOWCASE_ASSISTANT_KEY_ENCRYPTION_KEY_1=<khóa>`.
3. Tạo lại container backend như ở trên. Backend kiểm tra cấu hình lúc khởi động: khóa sai độ dài, không phải
   Base64, toàn số không, trùng id hoặc id đang dùng không có trong danh sách sẽ làm backend **không khởi động**
   (thay vì lặng lẽ chạy sai). Để trống cả hai biến thì tính năng tắt.
4. **Không để khóa mã hóa trong cùng nơi với bản sao lưu database.** Sao lưu database chỉ chứa dữ liệu đã mã
   hóa; nếu khóa nằm chung gói sao lưu thì mất toàn bộ lợi ích.

### Xoay khóa và mất khóa

- Xoay khóa: thêm khóa mới vào chỗ trống kế tiếp (`..._KEY_2`), đặt `..._ACTIVE_ID=2` và **giữ khóa cũ trong
  danh sách**. Key lưu mới dùng khóa mới; key lưu trước đó vẫn đọc được bằng khóa cũ. Hệ thống không tự mã hóa
  lại key cũ: người dùng lưu lại key thì được mã hóa bằng khóa mới.
- Bỏ một khóa khỏi danh sách (hoặc mất khóa): các key mã hóa bằng nó không đọc được nữa. Giao diện coi như
  chưa lưu key và hỏi lại; người dùng chỉ cần nhập lại. Không có lỗi 5xx, và không có dữ liệu nào khác bị ảnh
  hưởng. Không dùng lại một id cho khóa khác, vì lúc đó key lưu dưới id đó cũng không đọc được.
- Khôi phục database vào môi trường **khác** (khóa khác) có cùng hệ quả: người dùng nhập lại key.

### Vòng đời dữ liệu và API

- Người dùng xóa key bất cứ lúc nào trong khung chat (có hỏi xác nhận); xóa tài khoản trong quản lý người dùng
  cũng xóa key đã lưu của tài khoản đó. Đăng xuất **không** xóa key đã lưu: đó là mục đích của việc lưu.
- Khi Google từ chối key đã lưu (sai, bị thu hồi, hết hạn mức), key **không** bị xóa tự động vì có thể chỉ là
  hết hạn mức tạm thời; người dùng được hỏi key mới hoặc tự xóa key cũ.
- API (đều cần đăng nhập, mọi vai trò, hạn mức `KeyRequestsPerUserPerMinute` theo người dùng):
  `GET`, `PUT`, `DELETE /api/showcase/assistant/key`. Không endpoint nào trả key; `PUT` nhận key trong body
  (không bao giờ trong URL). Chat dùng key trong header `X-Gemini-Api-Key` nếu có, nếu không thì dùng key
  đã lưu của chính người hỏi.
- Không ghi log key, bản mã hóa hay khóa mã hóa. Khi không giải mã được một key đã lưu, backend chỉ ghi một
  cảnh báo cố định, không kèm dữ liệu.

### Kiểm tra sau khi bật

`GET /api/showcase/assistant/key` (đã đăng nhập) trả `storageAvailable: true`. Lưu một key thử rồi gọi lại:
chỉ thấy `hasKey: true` và 4 ký tự cuối. Hỏi Hubby khi không gửi header key: câu hỏi vẫn chạy bằng key đã lưu.

## Local Windows

Chạy các helper rồi khởi động lại backend Development:

```powershell
& '.\scripts\showcase\configure-showcase-assistant.ps1'
& '.\scripts\showcase\configure-showcase-key-encryption.ps1'
```

Helper thứ nhất chỉ bật chatbot và chọn model trong User Secrets của `backend/src/EHub.Api`. Helper thứ hai
tạo khóa mã hóa ngẫu nhiên trong User Secrets (không in ra, không ghi vào repo) để lựa chọn **Tài khoản**
hoạt động trên máy local; nếu đã có khóa thì không đổi gì, vì thay khóa sẽ làm các key đã lưu không đọc được.
Cả hai không hỏi key, không sửa key mentor và không gọi AI; người dùng dán key của mình vào khung chat khi dùng.
