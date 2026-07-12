# E-HUB Local Development Guide (Hướng dẫn Chạy dự án Local)

Tài liệu này giúp các thành viên mới trong nhóm cài đặt nhanh môi trường phát triển local cho E-HUB Backend chỉ trong 5 phút.

---

## ⚡ HƯỚNG DẪN NHANH DÀNH CHO THÀNH VIÊN MỚI (QUICK START)

Khi bạn clone dự án này từ GitHub về lần đầu tiên, hãy làm theo tuần tự các bước sau:

### 1. Bật Docker Desktop
*   Mở phần mềm **Docker Desktop** trên máy của bạn và đợi chú cá voi ở góc trái chuyển sang màu xanh lá cây (**Engine Running**).

### 2. Tắt PostgreSQL của Windows (NẾU CÓ)
Nếu máy bạn đã cài sẵn PostgreSQL từ trước, nó sẽ tranh chấp cổng `5432` với Docker.
*   **Cách tắt:** Nhấn phím Windows $\rightarrow$ gõ **`Services`** $\rightarrow$ Tìm dịch vụ bắt đầu bằng tên **`postgresql...`** (ví dụ: `postgresql-x64-16`) $\rightarrow$ Click chuột phải chọn **`Stop`**.

### 3. Khởi chạy Database Docker
Mở Terminal tại thư mục gốc của dự án (`d:\EHUB\EHUB\`) và chạy lệnh:
```bash
docker compose -f docker-compose.local.yml up -d
```
*(Lưu ý: File `docker-compose.local.yml` chỉ chứa credential chạy local development giả lập để cả nhóm dùng chung. Tuyệt đối không dùng các thông tin này cho môi trường staging hoặc production).*

### 4. Thiết lập Chuỗi kết nối (User Secrets)
Bạn có thể thiết lập chuỗi kết nối local trực tiếp từ thư mục gốc của dự án bằng lệnh:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ehub_db;Username=ehub_user;Password=ehub_password" --project backend/src/EHub.Api
```
*(Mẹo: Chuỗi kết nối này được lưu trữ trong thư mục User Profile của hệ điều hành, đảm bảo an toàn phù hợp cho môi trường development và không bị Git commit nhầm).*

### 5. Chạy dự án Backend
*   **Sử dụng IDE:** Mở Solution bằng Visual Studio hoặc Rider, chọn dự án khởi chạy là `EHub.Api` và nhấn nút **Run (F5)**.
*   **Sử dụng Terminal:** Chạy lệnh tại thư mục `backend/src/EHub.Api`:
    ```bash
    dotnet run
    ```
*   **Tự động tạo bảng & Seed dữ liệu (Chỉ chạy ở Development):** Backend đã được cấu hình tự động chạy Migration khi khởi động ở môi trường `Development`. Do đó, ngay khi app chạy, hệ thống sẽ tự động đồng bộ cấu trúc bảng và nạp sẵn dữ liệu Role.

---

## 🛠️ HỖ TRỢ XỬ LÝ LỖI (TROUBLESHOOTING)

Trong quá trình chạy local, nếu gặp lỗi liên quan đến Database, bạn có thể sử dụng các lệnh sau:

*   **Kiểm tra xem container database có đang chạy hay không:**
    ```bash
    docker ps
    ```
*   **Xem logs của container database để phát hiện lỗi khởi động/mật khẩu:**
    ```bash
    docker logs ehub-postgres
    ```
*   **Reset toàn bộ Database local về trạng thái ban đầu (Cảnh báo: Lệnh này sẽ xóa sạch dữ liệu local hiện tại):**
    ```bash
    docker compose -f docker-compose.local.yml down -v
    docker compose -f docker-compose.local.yml up -d
    ```

---

## 🖥️ KIỂM TRA DỮ LIỆU BẰNG PGADMIN 4

Để kết nối và xem cấu trúc bảng trên giao diện pgAdmin 4:
1.  Mở pgAdmin 4 $\rightarrow$ Chuột phải vào **Servers** $\rightarrow$ **Register** $\rightarrow$ **Server...**
2.  Tab **General**: Đặt tên bất kỳ (ví dụ: `EHub Local Docker`).
3.  Tab **Connection**: Điền các thông tin sau:
    *   **Host name/address:** `localhost`
    *   **Port:** `5432`
    *   **Maintenance database:** `ehub_db`
    *   **Username:** `ehub_user`
    *   **Password:** `ehub_password`
4.  Nhấn **Save**. 
5.  *Mẹo nhỏ:* Nếu bạn đang mở sẵn pgAdmin 4 trong khi chạy code backend lần đầu, hãy click chuột phải vào thư mục `Tables` trong pgAdmin và chọn **`Refresh`** (hoặc nhấn **F5**). Bạn sẽ thấy hệ thống tự động tạo ra **4 bảng nghiệp vụ Auth** (`users`, `roles`, `user_roles`, `refresh_tokens`) và **1 bảng hệ thống** của EF Core (`__EFMigrationsHistory`).

---

## 🛠️ LÀM VIỆC VỚI DATABASE MIGRATIONS (KHI THAY ĐỔI CƠ SỞ DỮ LIỆU)

> [!WARNING]
> **Quy tắc làm việc của Team:**
> 1. Tuyệt đối không sửa cấu trúc bảng (thêm/xóa cột, đổi kiểu dữ liệu...) trực tiếp bằng pgAdmin 4. Mọi thay đổi bắt buộc phải qua EF Core Migration.
> 2. Khi bạn thay đổi code thực thể (Entity) và muốn cập nhật Database, hãy chạy lệnh tạo Migration tại thư mục `backend/`:
>    ```bash
>    dotnet ef migrations add <TenMigrationNganGon> --project src/EHub.Infrastructure --startup-project src/EHub.Api --output-dir Persistence/Migrations
>    ```
> 3. Commit tất cả các file trong thư mục `Migrations/` lên GitHub để các thành viên khác kéo về cùng đồng bộ.

---

## 🧪 CHẠY UNIT TESTS

Để chạy kiểm thử tự động kiểm tra xem code của bạn có làm hỏng các logic cũ không, di chuyển vào thư mục `backend/` và gõ:
```bash
dotnet test
```
