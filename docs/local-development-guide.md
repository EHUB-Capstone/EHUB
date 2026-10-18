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
Mở Terminal tại thư mục gốc của repo (ví dụ: `D:\FA26\EHUB`) và chạy lệnh:
```bash
docker compose -f docker-compose.local.yml pull postgres
docker compose -f docker-compose.local.yml up -d postgres
```
*(Lưu ý: File `docker-compose.local.yml` chỉ chứa credential chạy local development giả lập để cả nhóm dùng chung. Tuyệt đối không dùng các thông tin này cho môi trường staging hoặc production).*

Sau đó kiểm tra PostgreSQL đã dùng đúng image, đang chạy và đã qua healthcheck:
```bash
docker compose -f docker-compose.local.yml ps postgres
docker ps --filter name=ehub-postgres --format "{{.Image}} {{.Status}}"
```

Kết quả cần có image `pgvector/pgvector:0.8.6-pg18` và trạng thái `healthy`. Nếu trạng thái là `starting`, đợi vài giây rồi chạy lại lệnh; nếu là `unhealthy` hoặc container không xuất hiện, xem phần Troubleshooting bên dưới.

Kiểm tra extension `vector` đã có trong image:
```bash
docker exec ehub-postgres psql -U ehub_user -d ehub_db -c "select name, default_version from pg_available_extensions where name='vector';"
```

Kết quả mong đợi là một dòng có `vector`. Backend sẽ tạo extension này khi migration yêu cầu; việc extension xuất hiện trong danh sách xác nhận image PostgreSQL hỗ trợ pgvector.

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
    docker compose -f docker-compose.local.yml ps postgres
    docker ps --filter name=ehub-postgres --format "{{.Image}} {{.Status}}"
    ```
*   **Xem logs của container database để phát hiện lỗi khởi động/mật khẩu:**
    ```bash
    docker compose -f docker-compose.local.yml logs --tail=100 postgres
    ```
*   **Kiểm tra database có thực sự nhận kết nối từ bên trong container không:**
    ```bash
    docker exec ehub-postgres pg_isready -U ehub_user -d ehub_db
    docker exec ehub-postgres psql -U ehub_user -d ehub_db -c "select 1;"
    ```

### Lỗi kết nối database

Khi backend báo `Connection refused`, `No connection could be made`, `password authentication failed`, hoặc `database ... does not exist`, xử lý theo thứ tự sau:

1. Kiểm tra Docker Desktop đang chạy, sau đó chạy các lệnh kiểm tra trạng thái và log ở trên. Nếu service không chạy, khởi động lại bằng:
   ```bash
   docker compose -f docker-compose.local.yml up -d postgres
   ```
2. Khi chạy backend **trực tiếp trên Windows**, User Secrets phải dùng `Host=localhost;Port=5432`. Cài lại cấu hình local nếu cần:
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ehub_db;Username=ehub_user;Password=ehub_password" --project backend/src/EHub.Api
   ```
   Khi chạy backend **trong Docker Compose**, cấu hình phải dùng `Host=postgres;Port=5432`; không thay bằng `localhost` vì `localhost` lúc đó là chính container backend.
3. Nếu log nêu cổng `5432` đang được sử dụng hoặc tên container `ehub-postgres` đã tồn tại, xác định container đang chiếm bằng `docker ps -a`. Nếu đó là container PostgreSQL local cũ và dữ liệu đã được sao lưu hoặc không cần giữ, dừng/xóa **đúng container đó** rồi khởi động lại service Compose:
   ```bash
   docker stop ehub-postgres
   docker rm ehub-postgres
   docker compose -f docker-compose.local.yml up -d postgres
   ```
   `docker rm` không tự xóa volume, nhưng cần kiểm tra volume cũ có phải `ehub_postgres_data` trước khi thực hiện nếu dữ liệu quan trọng.
4. Nếu PostgreSQL kết nối được nhưng backend lỗi migration như `relation "..." does not exist`, schema và lịch sử EF Core migration đang không đồng bộ. Với database development không cần giữ dữ liệu, làm mới database bằng các lệnh ở phần kế tiếp. Nếu cần giữ dữ liệu, dừng lại để kiểm tra bảng `__EFMigrationsHistory` và migration tạo bảng bị thiếu; không tự tạo/sửa bảng bằng pgAdmin.

> [!NOTE]
> Không dùng `docker run postgres` cho database E-HUB. Hãy dùng Docker Compose để giữ đúng image pgvector, tên container, volume, mạng và healthcheck của repo.
*   **Reset toàn bộ Database local về trạng thái ban đầu (Cảnh báo: Lệnh này sẽ xóa sạch dữ liệu local hiện tại):**
    ```bash
    docker compose -f docker-compose.local.yml down -v
    docker compose -f docker-compose.local.yml up -d postgres
    ```

---

## 🖥️ KẾT NỐI VÀ XEM CƠ SỞ DỮ LIỆU BẰNG PGADMIN 4

Để xem cấu trúc bảng và truy vấn dữ liệu local, bạn có thể lựa chọn 1 trong 2 phương án dưới đây tùy thuộc vào thói quen phát triển của mình:

---

### Phương án 1: Sử dụng pgAdmin 4 cài trên máy Windows (Local Desktop)
Đây là cách truyền thống nếu bạn thích chạy ứng dụng pgAdmin độc lập trên hệ điều hành Windows của mình.

1. Khởi động phần mềm **pgAdmin 4** trên Windows.
2. Click chuột phải vào **Servers** $\rightarrow$ **Register** $\rightarrow$ **Server...**
3. Tab **General**: Đặt tên bất kỳ (Ví dụ: `EHub Local Database`).
4. Tab **Connection**: Điền các thông số kết nối:
   *   **Host name/address:** **`localhost`** (hoặc `127.0.0.1`)
       > [!NOTE]
       > Vì pgAdmin chạy trực tiếp trên Windows, nó có thể giao tiếp trực tiếp với cổng `5432` đang được mapping từ container ra máy Host thông qua `localhost`.
   *   **Port:** `5432`
   *   **Maintenance database:** `ehub_db`
   *   **Username:** `ehub_user`
   *   **Password:** `ehub_password`
5. Nhấn **Save**.

---

### Phương án 2: Sử dụng pgAdmin 4 Extension trong Docker Desktop
Cách này giúp bạn quản lý trực tiếp DB bên trong giao diện **Docker Desktop** mà không cần cài đặt thêm pgAdmin 4 riêng trên máy Windows.

#### Bước 1: Cài đặt Extension
1. Mở **Docker Desktop**.
2. Chọn mục **Extensions** ở cột menu bên trái.
3. Tìm kiếm từ khóa **`pgAdmin4`** (Open Source management tool for PostgreSQL) và nhấn **Install**. Giao diện pgAdmin4 sẽ hiển thị trực tiếp trong menu bên trái.

#### Bước 2: Cấu hình kết nối
1. Mở tiện ích **pgAdmin4** trên Docker Desktop.
2. Click chuột phải vào **Servers** $\rightarrow$ **Register** $\rightarrow$ **Server...**
3. Tab **General**: Đặt tên bất kỳ (Ví dụ: `EHub Local Docker`).
4. Tab **Connection**: Điền các thông số kết nối:
   *   **Host name/address:** **`host.docker.internal`**
       > [!IMPORTANT]
       > **LƯU Ý QUAN TRỌNG:** Bạn **bắt buộc** phải dùng `host.docker.internal` thay vì `localhost` hay `127.0.0.1`. Lý do là vì pgAdmin 4 lúc này đang chạy biệt lập trong một container sandbox của Docker Desktop, nếu điền `localhost` nó sẽ kết nối tới chính nó thay vì trỏ ra cổng `5432` của máy Host để vào container CSDL.
   *   **Port:** `5432`
   *   **Maintenance database:** `ehub_db`
   *   **Username:** `ehub_user`
   *   **Password:** `ehub_password`
5. Nhấn **Save**.

---

### Cách Xem cấu trúc bảng & Chạy câu lệnh truy vấn
Sau khi đã kết nối thành công theo một trong hai phương án trên:
*   Mở cây thư mục bên trái: **Servers** $\rightarrow$ *[Tên Server bạn đã đặt]* $\rightarrow$ **Databases** $\rightarrow$ **ehub_db** $\rightarrow$ **Schemas** $\rightarrow$ **public** $\rightarrow$ **Tables**.
*   **Mở cửa sổ truy vấn SQL:** Click chuột phải vào bảng bất kỳ $\rightarrow$ Chọn **Query Tool** $\rightarrow$ Nhập lệnh SQL (Ví dụ: `SELECT * FROM public.users`) $\rightarrow$ Click nút **Play** (hoặc nhấn **F5**) để chạy.

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

---

## 🐳 CHẠY TOÀN BỘ DỰ ÁN BẰNG DOCKER COMPOSE (FULL STACK)

Dự án hiện tại hỗ trợ hai phương án chạy bằng Docker Compose tại tệp `docker-compose.local.yml`:

### Bước 1: Chuẩn bị biến môi trường
Trước khi chạy, sao chép tệp cấu hình mẫu ở thư mục gốc của dự án:
*   Copy tệp `.env.example` thành `.env` (tệp `.env` đã được bỏ qua trong Git).
*   Điền các thông số local hoặc giữ nguyên giá trị placeholder.

### Bước 2: Lựa chọn chế độ khởi chạy

#### 🔹 Phương án A: Chỉ khởi chạy Database (Mặc định)
Dành cho trường hợp bạn muốn chạy Database bằng Docker, còn Backend và Frontend thì chạy trực tiếp trên IDE (Visual Studio, Rider) hoặc qua terminal:
```bash
docker compose -f docker-compose.local.yml up -d
```
*(Chỉ có duy nhất container `ehub-postgres` được bật).*

#### 🔹 Phương án B: Khởi chạy toàn bộ hệ thống (Full Stack)
Khởi chạy đồng thời cả Database, Backend (API) và Frontend (UI) trong mạng ảo nội bộ của Docker:
```bash
docker compose -f docker-compose.local.yml --profile full up --build -d
```

### Bước 3: Địa chỉ truy cập
Khi chạy ở chế độ Full Stack, bạn có thể truy cập các dịch vụ qua các đường dẫn sau:
*   **Giao diện Frontend (React):** `http://localhost:3000`
*   **Trang tài liệu API (Swagger):** `http://localhost:5226/swagger`
*   **Trang kiểm tra sức khỏe hệ thống (Health Check):** `http://localhost:5226/health`
*   **PostgreSQL Database:** `localhost:5432` (kết nối bằng pgAdmin)

### Bước 4: Tắt các container
*   Để tắt hệ thống (ở chế độ Full Stack):
    ```bash
    docker compose -f docker-compose.local.yml --profile full down
    ```
*   Để tắt hệ thống và **xóa sạch dữ liệu cục bộ** trong database (Reset DB):
    ```bash
    docker compose -f docker-compose.local.yml --profile full down -v
    ```

