# E-HUB Frontend

Nguồn mã phía giao diện người dùng (Client-side Web App) của nền tảng Quản lý Dữ liệu Khởi nghiệp Học thuật E-HUB. Được xây dựng trên nền tảng **React 19**, **TypeScript** và **Vite**, sử dụng **Tailwind CSS**, **React Router**, **React Query** và **Axios** để phát triển giao diện web hiện đại, có phân quyền theo vai trò và kết nối với E-HUB Backend API.

---

## 1. Cấu trúc Dự án (Project Structure)

*   `src/api`: Các hàm gọi API và cấu hình HTTP client kết nối với Backend.
*   `src/assets`: Hình ảnh, logo và tài nguyên tĩnh dùng trong giao diện.
*   `src/components`: Component tái sử dụng, bao gồm layout, UI controls, auth, class, workspace và evaluation.
*   `src/constants`: Các hằng số dùng chung như danh sách ngành học, lịch học hoặc cấu hình hiển thị.
*   `src/context`: React Context cho các trạng thái dùng toàn ứng dụng như xác thực và giao diện.
*   `src/features`: Các module chức năng có logic riêng, ví dụ Execution Board và Data Bank.
*   `src/hooks`: Custom React hooks dùng lại giữa nhiều màn hình.
*   `src/lib`: Cấu hình thư viện dùng chung, ví dụ React Query client.
*   `src/pages`: Các trang chính của hệ thống theo nhóm vai trò: Admin, Lecturer, Mentor, Student, Auth, Workspace và Shared.
*   `src/routes`: Thành phần bảo vệ route và điều hướng theo trạng thái đăng nhập/phân quyền.
*   `src/utils`: Hàm tiện ích dùng chung cho format dữ liệu, className và hiển thị.
*   `public`: Tài nguyên public được phục vụ trực tiếp bởi Vite.

---

## 2. Cách bắt đầu (Getting Started)

1.  **Cài đặt dependencies:**
    Tại thư mục `frontend/`, chạy:
    ```bash
    npm install
    ```

2.  **Chạy môi trường phát triển:**
    ```bash
    npm run dev
    ```
    Mặc định Vite sẽ mở ứng dụng tại `http://localhost:5173`.

3.  **Kiểm tra TypeScript:**
    ```bash
    npm run type-check
    ```

4.  **Kiểm tra lint:**
    ```bash
    npm run lint
    ```

5.  **Build bản production:**
    ```bash
    npm run build
    ```

6.  **Xem thử bản production build:**
    ```bash
    npm run preview
    ```

---

## 3. Công nghệ chính (Tech Stack)

*   **React + TypeScript:** Xây dựng giao diện theo component với kiểm tra kiểu dữ liệu tĩnh.
*   **Vite:** Dev server và build tool tốc độ cao cho frontend.
*   **React Router:** Quản lý routing giữa các trang.
*   **TanStack React Query:** Quản lý server state, caching và đồng bộ dữ liệu API.
*   **Axios:** Gửi HTTP request tới backend.
*   **Tailwind CSS:** Xây dựng giao diện bằng utility classes.
*   **Socket.IO Client:** Hỗ trợ các tính năng realtime khi cần.
*   **Recharts:** Hiển thị biểu đồ, thống kê và dashboard.
*   **Lucide React:** Bộ icon dùng trong giao diện.
