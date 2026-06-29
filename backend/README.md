# E-HUB Backend

Nguồn mã phía máy chủ (Server-side API) của nền tảng Quản lý Dữ liệu Khởi nghiệp Học thuật E-HUB. Được xây dựng trên nền tảng **.NET 10 (LTS)** và **PostgreSQL** áp dụng kiến trúc **Clean Architecture**.

---

## 1. Cấu trúc Dự án (Project Layers)

*   `src/EHub.Api`: REST API, Controllers, Middleware, configurations (Entry point).
*   `src/EHub.Application`: Use cases, interfaces dịch vụ, validators.
*   `src/EHub.Infrastructure`: EF Core, PostgreSQL mappings, JWT, Cloudinary.
*   `src/EHub.Domain`: Entities, Enums, Rules nghiệp vụ lõi (không phụ thuộc layer ngoài).
*   `src/EHub.Contracts`: Request/Response DTOs làm hợp đồng API.
*   `src/EHub.Shared`: Result pattern, common errors, constants chung.
*   `tests/EHub.UnitTests`: Unit testing cho Domain & Application.
*   `tests/EHub.IntegrationTests`: Integration testing cho API & DB.

---

## 2. Cách bắt đầu (Getting Started)

1.  **Chạy Database Docker Local:**
    Tại thư mục gốc, khởi chạy database:
    ```bash
    docker compose -f docker-compose.local.yml up -d
    ```
2.  **Cấu hình Biến môi trường:**
    Copy file `.env.example` thành `.env` và tùy chỉnh nếu cần.
3.  **Restore & Build:**
    ```bash
    dotnet restore EHub.slnx
    dotnet build EHub.slnx
    ```
4.  **Cập nhật Database Schema:**
    ```bash
    dotnet ef database update --project src/EHub.Infrastructure --startup-project src/EHub.Api
    ```
5.  **Chạy ứng dụng Web API:**
    ```bash
    dotnet run --project src/EHub.Api
    ```
    Mở trình duyệt: `http://localhost:5000/swagger` để xem tài liệu API.
