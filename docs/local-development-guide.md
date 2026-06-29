# E-HUB Local Development Guide

Tài liệu này hướng dẫn cách cài đặt và chạy môi trường phát triển local cho dự án E-HUB Backend.

---

## 1. Yêu cầu Hệ thống (Prerequisites)

Trước khi bắt đầu, hãy đảm bảo máy tính của bạn đã cài đặt các công cụ sau:
1.  **SDK .NET 10.0** (Tải về từ trang chủ Microsoft).
2.  **Docker Desktop** (Dùng để chạy PostgreSQL database local).
3.  **Git** (Quản lý phiên bản).
4.  **IDE:** Visual Studio 2022 (v17.10 trở lên) hoặc JetBrains Rider hoặc VS Code (cài đặt C# Dev Kit extension).

---

## 2. Các bước Thiết lập Môi trường Local

### Bước 1: Khởi động Database PostgreSQL bằng Docker
Chúng tôi sử dụng Docker Compose để chuẩn hóa CSDL local cho cả nhóm.
1.  Mở terminal tại thư mục gốc của dự án.
2.  Chạy lệnh sau để khởi động PostgreSQL container chạy nền:
    ```bash
    docker compose -f docker-compose.local.yml up -d
    ```
3.  Để kiểm tra xem database đang hoạt động tốt hay không:
    ```bash
    docker compose -f docker-compose.local.yml ps
    ```
    *(PostgreSQL sẽ lắng nghe ở cổng `5432` trên localhost).*

### Bước 2: Cấu hình Biến môi trường
1.  Truy cập vào thư mục `backend/`.
2.  Sao chép file `.env.example` thành `.env`:
    ```bash
    cp .env.example .env
    ```
3.  Chỉnh sửa các giá trị cấu hình trong file `.env` nếu cần thiết (mặc định file mẫu đã được cấu hình khớp với Docker Compose database).
    *   *Lưu ý:* Tuyệt đối không commit file `.env` chứa mật khẩu thực lên GitHub. File này đã được thêm vào `.gitignore`.

### Bước 3: Chạy Database Migrations
Để đồng bộ cấu trúc bảng từ code vào database:
1.  Mở terminal tại thư mục `backend/`.
2.  Chạy lệnh update database:
    ```bash
    dotnet ef database update --project src/EHub.Infrastructure --startup-project src/EHub.Api
    ```
    *(Nếu chưa cài công cụ dotnet-ef tool, hãy cài đặt bằng lệnh: `dotnet tool install --global dotnet-ef`)*.

---

## 3. Khởi chạy Ứng dụng & Kiểm thử API

### Chạy ứng dụng bằng dotnet CLI
1.  Di chuyển vào thư mục `backend/`.
2.  Chạy lệnh:
    ```bash
    dotnet run --project src/EHub.Api
    ```
3.  Mở trình duyệt truy cập:
    *   Swagger UI: `http://localhost:5000/swagger` hoặc `https://localhost:5001/swagger` (hoặc cổng cụ thể hiển thị trên log console).
    *   Health check endpoint: `http://localhost:5000/health`

### Chạy Unit Test
Để kiểm tra tính đúng đắn của logic:
1.  Tại thư mục `backend/` chạy lệnh:
    ```bash
    dotnet test
    ```
