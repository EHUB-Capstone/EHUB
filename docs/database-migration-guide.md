# E-HUB Database Migration Guide

Tài liệu này hướng dẫn cách làm việc với Entity Framework Core Migrations và PostgreSQL trong dự án E-HUB.

---

## 1. Môi trường Database Local (Docker)

Chúng tôi sử dụng Docker Compose để chạy PostgreSQL cục bộ. 
*   **Khởi động database:**
    ```bash
    docker compose -f docker-compose.local.yml up -d
    ```
*   **Dừng database:**
    ```bash
    docker compose -f docker-compose.local.yml down
    ```
*   **Thông tin kết nối (môi trường local):**
    *   **Host:** `localhost`
    *   **Port:** `5432`
    *   **Database:** `ehub_db`
    *   **User:** `ehub_user`
    *   **Password:** `ehub_password`

---

## 2. Quy tắc làm việc với Migration (Migration Rules)

> [!IMPORTANT]
> **Quy tắc bắt buộc đối với tất cả thành viên:**
> 1.  **Nghiêm cấm sửa DB thủ công:** Không sử dụng các công cụ trực quan (như PgAdmin, DBeaver) để thay đổi cấu trúc bảng, kiểu dữ liệu hay quan hệ. Mọi thay đổi bắt buộc phải thông qua code C# (Entities) và được tạo Migration tự động.
> 2.  **Commit Migrations:** Các file migration sinh ra trong thư mục `src/EHub.Infrastructure/Persistence/Migrations/` bắt buộc phải được commit lên GitHub.
> 3.  **Tạo migration khi dự án build pass:** Luôn chạy lệnh `dotnet build` trước khi tạo migration mới để đảm bảo không lỗi biên dịch.
> 4.  **Không chỉnh sửa file migration cũ:** Nếu migration cũ đã được merge vào nhánh `develop`, tuyệt đối không dùng lệnh `dotnet ef migrations remove` hoặc chỉnh sửa trực tiếp file đó. Bạn phải tạo một migration mới để thay đổi cấu trúc DB.

---

## 3. Các lệnh EF Core CLI thường dùng

*Lưu ý: Thực hiện các lệnh này tại thư mục `backend/` hoặc thư mục gốc của repo (nhưng phải truyền đúng đường dẫn dự án).*

### 3.1. Thêm một Migration mới
Khi bạn thay đổi, thêm mới thực thể C# trong layer `Domain` và cấu hình Fluent API trong `Infrastructure`:
```bash
dotnet ef migrations add <TenMigration> --project src/EHub.Infrastructure --startup-project src/EHub.Api --output-dir Persistence/Migrations
```
*Ví dụ:*
```bash
dotnet ef migrations add AddSemesterTable --project src/EHub.Infrastructure --startup-project src/EHub.Api --output-dir Persistence/Migrations
```

### 3.2. Cập nhật Database local
Để apply các file migration mới nhất vào PostgreSQL local của bạn:
```bash
dotnet ef database update --project src/EHub.Infrastructure --startup-project src/EHub.Api
```

### 3.3. Xóa Migration cuối cùng (Chưa push/merge)
Nếu bạn vừa tạo một migration local nhưng phát hiện lỗi thiết kế và **chưa push/merge lên Git**:
```bash
dotnet ef migrations remove --project src/EHub.Infrastructure --startup-project src/EHub.Api
```

### 3.4. Xuất file script SQL để review
Trước khi deploy lên môi trường Staging/Production, xuất script SQL từ các migrations để kiểm tra:
```bash
dotnet ef migrations script --project src/EHub.Infrastructure --startup-project src/EHub.Api --output artifacts/sql/update-schema.sql
```
