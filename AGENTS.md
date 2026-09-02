# Quy chuẩn phát triển E-HUB dành cho thành viên và AI

File này áp dụng cho toàn bộ monorepo E-HUB. Trước khi yêu cầu AI phân tích, sửa hoặc tạo code, thành viên phải yêu cầu AI đọc file này và tuân thủ các quy tắc bên dưới.

## 1. Nguyên tắc làm việc bắt buộc

1. Đọc yêu cầu, kiểm tra code hiện tại, các file liên quan và test trước khi sửa.
2. Chỉ thay đổi đúng phạm vi nhiệm vụ; không tự ý refactor, đổi tên contract hoặc format file không liên quan.
3. Không ghi đè hoặc xóa thay đổi đang có của thành viên khác.
4. Khi yêu cầu chỉ là phân tích/review thì không tự ý sửa code.
5. Không đoán nghiệp vụ. Nếu code, API contract, test và tài liệu mâu thuẫn, phải nêu rõ điểm chưa chắc chắn trước khi thay đổi lớn.
6. Mọi code do AI tạo phải được thành viên phụ trách đọc lại và hiểu được trước khi tạo Pull Request.

## 2. Bối cảnh và cấu trúc dự án

E-HUB sử dụng:

- Frontend: React 19, TypeScript, Vite, Tailwind CSS.
- Backend: ASP.NET Core Web API, .NET 10, Entity Framework Core 10.
- Database: PostgreSQL.
- Source code: một GitHub monorepo, frontend và backend nằm trong hai thư mục riêng.

### Backend Clean Architecture

- `EHub.Domain`: Entity, Enum và trạng thái nghiệp vụ cốt lõi.
- `EHub.Shared`: `Result`, error, constant và tiện ích dùng chung.
- `EHub.Contracts`: Request/Response DTO của API.
- `EHub.Application`: Use case, handler, validator và interface.
- `EHub.Infrastructure`: EF Core, PostgreSQL, repository, email, identity, storage và background job.
- `EHub.Api`: Controller, middleware, policy, cấu hình và dependency composition.

Quy tắc dependency:

```text
Application    -> Domain + Contracts + Shared
Infrastructure -> Application + Domain + Shared
Api            -> Application + Infrastructure + Contracts + Shared
```

Không được để `Domain` phụ thuộc EF Core/API, không để `Application` phụ thuộc `Infrastructure`, và không truy cập `DbContext` trực tiếp trong Controller.

### Frontend

- `src/pages`: màn hình và điều phối cấp route.
- `src/components`: component tái sử dụng.
- `src/features`: module chức năng độc lập.
- `src/api`: toàn bộ lời gọi API.
- `src/types`: kiểu request/response và dữ liệu hiển thị.
- `src/hooks`, `src/context`: logic và state dùng chung.
- `src/utils`: hàm tiện ích thuần.
- `src/mocks`: mock phục vụ development/test, không phải nguồn sự thật của API production.

## 3. Quy chuẩn Backend

### Controller và API contract

- Controller chỉ nhận request, lấy thông tin người dùng hiện tại, gọi Application handler và trả `ApiResponse<T>`.
- Không đặt business logic hoặc câu truy vấn EF Core trong Controller.
- Không trả trực tiếp Domain Entity ra API; phải dùng DTO trong `EHub.Contracts`.
- Khi thay đổi contract phải cập nhật đồng bộ backend contract, frontend type, API client, mock và test liên quan.

### Application và xử lý lỗi

- Feature mới đặt theo cấu trúc `EHub.Application/Features/<Module>/<UseCase>` và làm theo feature gần nhất.
- Lỗi nghiệp vụ dự kiến dùng `Result`/`Result<T>` cùng `ErrorCodes` hiện có.
- Không trả stack trace hoặc raw exception cho frontend.
- FluentValidation kiểm tra cấu trúc request; quy tắc phụ thuộc database, trạng thái và quyền được kiểm tra trong handler.
- Truyền `CancellationToken` cho các lời gọi async; dùng `AsNoTracking()` cho truy vấn chỉ đọc khi phù hợp.
- Handler/service mới phải được đăng ký trong `DependencyInjection.cs` đúng layer.

### Xác thực và phân quyền

- Dùng `SystemPolicies`, `SystemRoles` và `ICurrentUserService`; không tin `userId` hoặc role do request tự gửi lên.
- Frontend ẩn nút không phải là bảo mật. Backend vẫn phải kiểm tra role và quyền trên tài nguyên.
- Phải chống IDOR: có ID của Class, Team, Project hoặc Submission không có nghĩa là được phép xem/sửa nó.
- Kiểm tra đúng các quan hệ như Lecturer được phân công, Student thuộc lớp/team, Mentor được gán và chủ sở hữu dữ liệu.
- Với dữ liệu có `rowVersion`/PostgreSQL `xmin`, phải giữ cơ chế optimistic concurrency hiện tại.

## 4. Bảo mật và thông tin nhạy cảm

### Tuyệt đối không commit hoặc cung cấp cho AI

- Nội dung `.env`, `secrets.json`, connection string thật.
- JWT/OTP secret, Google client secret, SMTP password, Cloudinary API secret.
- Discord webhook, access token, refresh token, cookie, GitHub token.
- Private key, certificate chứa private key hoặc credential VPS.
- Dữ liệu cá nhân hoặc dữ liệu production dùng làm test fixture.

Không đọc hoặc in nội dung các file chứa secret ra terminal/log/chat AI. Nếu secret từng bị push lên GitHub, chỉ xóa file là chưa đủ: phải thu hồi hoặc rotate secret đó.

### Nơi lưu cấu hình đúng

- Local backend: ASP.NET Core User Secrets của project `backend/src/EHub.Api`.
- Staging/production: environment variables hoặc secret storage của GitHub/VPS.
- `appsettings.json` và các file `*.example`: chỉ chứa cấu hình an toàn hoặc placeholder giả.
- Biến `VITE_*` xuất hiện trong bundle trình duyệt nên được xem là thông tin công khai, không được chứa secret.

Ví dụ thiết lập local:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local-value>" --project backend/src/EHub.Api
```

Ngoài ra:

- Không log password, token, authorization header, cookie, OTP/reset token hoặc connection string.
- Không tắt authentication, authorization, rate limit, CORS hoặc TLS validation để làm cho code chạy.
- File upload phải kiểm tra quyền, kích thước, loại/signature và nội dung trước khi xử lý.
- Không nối trực tiếp input người dùng vào SQL; sử dụng EF Core/parameterized query.

## 5. Database và migration

- Không chỉnh schema thủ công bằng pgAdmin/DBeaver.
- Thay đổi schema phải tạo EF Core migration mới và commit đầy đủ migration cùng model snapshot được EF sinh ra.
- Không sửa hoặc xóa migration đã merge vào `develop`/`main`.
- Phải review `Up`, `Down`, index, foreign key, nullability, default và data backfill.
- Không tự apply migration lên staging/production nếu nhiệm vụ không cho phép triển khai.
- Thay đổi constant, validation hoặc danh sách lựa chọn trong code mà không đổi schema thì không cần migration.

Lệnh tạo migration từ thư mục gốc:

```powershell
dotnet ef migrations add <MeaningfulName> --project backend/src/EHub.Infrastructure --startup-project backend/src/EHub.Api --output-dir Persistence/Migrations
```

## 6. Quy chuẩn Frontend

- Gọi API thông qua `src/api/axiosClient.ts` và module trong `src/api`; không tạo Axios/fetch rải rác.
- Access token tiếp tục lưu trong memory; refresh token dùng cookie hiện tại. Không đưa token trở lại `localStorage`.
- Không thêm `any` hoặc `@ts-nocheck` mới nếu không có lý do bắt buộc và được ghi chú.
- Tận dụng component UI và design token hiện có trước khi tạo component mới.
- Màn hình gọi API phải có loading, empty, error và retry phù hợp.
- Form phải có validation, khóa double submit và giữ dữ liệu khi API thất bại.
- Thao tác xóa/khôi phục/nguy hiểm phải có confirm và phản hồi thành công/thất bại.
- Giao diện phải responsive, có label/ARIA cần thiết và không hiển thị raw exception.
- Không dùng `dangerouslySetInnerHTML` với dữ liệu server nếu chưa sanitize.
- Cleanup timer, listener, SignalR/Socket subscription và request khi component unmount.

## 7. Test và điều kiện hoàn thành

Backend, chạy từ `backend/`:

```powershell
dotnet build EHub.slnx
dotnet test EHub.slnx --filter "FullyQualifiedName!~IntegrationTests"
dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj
```

Frontend, chạy từ `frontend/`:

```powershell
npm run lint
npm run type-check
npm test
npm run build
```

Trước khi báo hoàn thành, AI và thành viên phải kiểm tra:

- Đúng acceptance criteria và không sửa ngoài phạm vi.
- Đúng dependency Clean Architecture.
- Backend đã kiểm tra authentication, role và quyền tài nguyên.
- Không chứa secret, dữ liệu cá nhân hoặc log nhạy cảm.
- Contract backend/frontend và mock đã đồng bộ nếu API thay đổi.
- Có test cho success, validation, forbidden và các edge case quan trọng.
- Nếu đổi schema thì có migration và kế hoạch rollback.
- Nêu rõ các lệnh đã chạy và kết quả thật; không được nói test pass nếu chưa chạy.

