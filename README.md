# E-HUB

Nền tảng quản lý dữ liệu khởi nghiệp học thuật E-HUB. Hệ thống hỗ trợ nhà trường quản lý học kỳ, môn học, lớp, nhóm sinh viên, dự án khởi nghiệp, các checkpoint đánh giá, mentor và khu trưng bày dự án (Showcase).

## Chức năng chính

- **Tài khoản và phân quyền:** đăng ký có xác thực OTP, đăng nhập bằng email hoặc Google, quên và đặt lại mật khẩu. Có bốn vai trò: Administrator, Lecturer, Mentor, Student. Lecturer và Mentor cần Administrator phê duyệt.
- **Quản lý học vụ:** học kỳ, môn học, lộ trình, checkpoint và rubric; lớp học, nhập danh sách sinh viên từ Excel, phân công giảng viên.
- **Nhóm và dự án:** lập nhóm, hồ sơ dự án, đề xuất (proposal) có lịch sử phiên bản, workspace của nhóm.
- **Checkpoint:** nộp tài liệu, phản hồi, đánh giá theo rubric, bảng tiến độ. Có tóm tắt checkpoint bằng AI (tùy chọn, dùng Ollama).
- **Mentoring:** hồ sơ mentor, ghép mentor với nhóm bằng quy tắc chấm điểm kết hợp AI (Gemini), lịch buổi mentoring.
- **Dashboard, xếp hạng, Data Bank, Showcase** và trợ lý AI cho Showcase.

Chi tiết từng màn hình và quyền truy cập nằm trong `docs/EHub_SRS_Screen_Descriptions_and_Authorization.md`.

## Công nghệ

| Phần | Công nghệ |
|---|---|
| Frontend | React 19, TypeScript, Vite, Tailwind CSS, TanStack Query |
| Backend | ASP.NET Core Web API, .NET 10, Entity Framework Core 10, kiến trúc Clean Architecture |
| Cơ sở dữ liệu | PostgreSQL 18 với extension pgvector |
| Lưu trữ tệp | Cloudinary (ảnh đại diện), Cloudflare R2 (tài liệu nộp) |
| AI | Google Gemini (ghép mentor, trợ lý Showcase), Ollama (tóm tắt checkpoint, tùy chọn) |
| Triển khai | Docker Compose, Nginx, GitHub Actions, VPS (staging và production) |

## Cấu trúc thư mục

```text
EHUB/
├── backend/            Mã nguồn API (.NET): src/, tests/, EHub.slnx, Dockerfile
├── frontend/           Mã nguồn giao diện (React + Vite): src/, tests/, Dockerfile, nginx.conf
├── docs/               Tài liệu: SRS, ERD, kiến trúc, API contract, hướng dẫn triển khai, git-flow
├── deploy/             Cấu hình Nginx, systemd, sao lưu cho VPS
├── docker/             Dockerfile PostgreSQL có pgvector
├── scripts/            Script triển khai, sao lưu, dữ liệu demo, công cụ đánh giá ghép mentor
├── tools/              Công cụ phụ trợ (benchmark tóm tắt checkpoint)
├── .github/workflows/  CI/CD
├── docker-compose.local.yml        Môi trường phát triển local
├── docker-compose.staging.yml      Môi trường staging
├── docker-compose.production.yml   Môi trường production
├── docker-compose.gemini.yml       Cấu hình bổ sung cho ghép mentor bằng Gemini
└── AGENTS.md           Quy chuẩn phát triển cho thành viên và AI
```

Các project trong `backend/src`: `EHub.Api`, `EHub.Application`, `EHub.Infrastructure`, `EHub.Domain`, `EHub.Contracts`, `EHub.Shared`. Quy tắc phụ thuộc giữa các lớp được mô tả trong `AGENTS.md`.

## Yêu cầu cài đặt

- Docker Desktop
- .NET SDK 10
- Node.js (xem phiên bản trong `frontend/package.json` và `frontend/Dockerfile`) và npm
- Git

## Chạy dự án trên máy local

1. **Tạo file môi trường.** Copy `.env.example` thành `.env` ở thư mục gốc và điền giá trị local. Không commit file `.env`.

2. **Bật cơ sở dữ liệu:**
   ```bash
   docker compose -f docker-compose.local.yml up -d postgres
   ```
   Nếu máy đã cài PostgreSQL riêng, hãy tắt dịch vụ đó để tránh trùng cổng `5432`.

3. **Cấu hình chuỗi kết nối cho backend** (lưu trong User Secrets, không nằm trong repo):
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<chuoi-ket-noi-local>" --project backend/src/EHub.Api
   ```
   Giá trị local và các bước kiểm tra database nằm trong `docs/local-development-guide.md`.

4. **Chạy backend:**
   ```bash
   cd backend
   dotnet run --project src/EHub.Api
   ```
   Ở môi trường Development, backend tự chạy migration và nạp dữ liệu vai trò khi khởi động. Mở `/swagger` để xem tài liệu API.

5. **Chạy frontend:**
   ```bash
   cd frontend
   npm install
   npm run dev
   ```
   Giao diện chạy ở địa chỉ Vite hiển thị (mặc định `http://localhost:5173`).

### Chạy toàn bộ bằng Docker Compose

```bash
docker compose -f docker-compose.local.yml --profile full up --build -d
```

| Dịch vụ | Địa chỉ |
|---|---|
| Frontend | http://localhost:3000 |
| API (Swagger) | http://localhost:5226/swagger |
| Health check | http://localhost:5226/health |
| PostgreSQL | localhost:5432 |

Tắt hệ thống: `docker compose -f docker-compose.local.yml --profile full down` (thêm `-v` để xóa dữ liệu local).

## Kiểm thử

Backend, chạy từ `backend/`:
```bash
dotnet build EHub.slnx
dotnet test EHub.slnx --filter "FullyQualifiedName!~IntegrationTests"
dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj
```

Frontend, chạy từ `frontend/`:
```bash
npm run lint
npm run type-check
npm test
npm run build
```

## Cơ sở dữ liệu và migration

Mọi thay đổi schema phải đi qua EF Core migration, không sửa trực tiếp bằng pgAdmin hay DBeaver. Từ thư mục gốc:
```bash
dotnet ef migrations add <TenMigration> --project backend/src/EHub.Infrastructure --startup-project backend/src/EHub.Api --output-dir Persistence/Migrations
```
Hướng dẫn chi tiết: `docs/database-migration-guide.md`. Sơ đồ ERD và thiết kế dữ liệu: `docs/ERD` và `docs/DB-DESIGN`.

## Môi trường triển khai

| Môi trường | Tên miền | File cấu hình |
|---|---|---|
| Staging | staging.e-hub.com.vn | `docker-compose.staging.yml`, mẫu `.env.staging.example` |
| Production | e-hub.com.vn | `docker-compose.production.yml`, mẫu `.env.production.example` |

Giá trị thật của staging và production chỉ lưu trên VPS hoặc trong secret của GitHub. Quy trình triển khai, sao lưu và phát hành nằm trong `docs/deployment`.

## Bảo mật cấu hình

- Không commit `.env`, `secrets.json`, khóa API, mật khẩu SMTP hay chuỗi kết nối thật.
- Các biến `VITE_*` nằm trong bundle trình duyệt nên không được chứa bí mật.
- File `*.example` chỉ chứa giá trị giả.

## Quy trình làm việc với Git

Dự án dùng biến thể Git Flow: `develop` là nhánh tích hợp, `main` là nhánh production. Nhánh công việc đặt tên `feature/*`, `fix/*`, `docs/*`, `chore/*` và tạo từ `develop`. Không push trực tiếp vào `develop` hoặc `main`; mọi thay đổi đi qua Pull Request có CI và ít nhất một phê duyệt. Xem `docs/git-flow`.

## Tài liệu tham khảo

| Nội dung | Vị trí |
|---|---|
| Quy chuẩn phát triển | `AGENTS.md` |
| Hướng dẫn chạy local | `docs/local-development-guide.md` |
| Luồng xác thực | `docs/auth-flow.md` |
| Kiến trúc hệ thống | `docs/system-architecture`, `docs/system-architecture-c4` |
| Đặc tả màn hình và phân quyền | `docs/EHub_SRS_Screen_Descriptions_and_Authorization.md` |
| Ghép mentor bằng AI | `docs/mentoring` |
| Tóm tắt checkpoint bằng AI | `docs/workspace` |
| Triển khai staging và production | `docs/deployment` |
