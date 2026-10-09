# Hướng Dẫn Tích Hợp Xác Thực Frontend & Backend (Auth Integration Guide)

Tài liệu này hướng dẫn lập trình viên Frontend cách kết nối với hệ thống API Xác thực của Backend E-HUB.

---

## 1. Hướng Dẫn Lưu Trữ Token (Token Storage Strategy)

Sau khi gọi thành công các API Đăng nhập (`/login`, `/google`) hoặc Đăng ký thành công vai trò Student:
*   **Access Token:**
    *   Lưu trong bộ nhớ tạm (Memory - ví dụ: biến React State hoặc Pinia/Redux Store) để tránh bị tấn công XSS.
    *   Truyền kèm trong Header của mọi request API yêu cầu xác thực:
        `Authorization: Bearer <access_token>`
*   **Refresh Token:**
    *   Lưu trong LocalStorage/SessionStorage hoặc HttpOnly Cookie (nếu cấu hình cookie được hỗ trợ).
    *   Được dùng để tự động gia hạn Access Token khi hết hạn mà không bắt người dùng phải đăng nhập lại.

---

## 2. Quản Lý Trạng Thái Chờ Duyệt (Pending Approval Flow)

Đối với các vai trò Giảng viên (Lecturer) và Mentor:
1.  **Khi đăng ký:**
    *   Màn hình đăng ký thành công sẽ nhận được phản hồi `requiresApproval: true` và `status: "PendingApproval"`.
    *   Frontend cần hiển thị thông báo: *"Tài khoản của bạn đã được đăng ký thành công và đang chờ Ban quản trị phê duyệt. Vui lòng kiểm tra lại sau."* và chuyển hướng về màn hình đăng nhập.
2.  **Khi đăng nhập:**
    *   Nếu tài khoản chưa được duyệt, API đăng nhập sẽ trả lỗi HTTP 403 kèm mã lỗi `AUTH_ACCOUNT_PENDING_APPROVAL`.
    *   Frontend bắt lỗi này để hiển thị thông báo chờ duyệt tương tự trên form đăng nhập.

---

## 3. Quản Lý Luồng Đăng Nhập Google (Google Sign-In integration)

1.  **Bước 1: Lấy Token từ Google SDK**
    *   Frontend sử dụng thư viện Google Sign-In SDK (ví dụ `@react-oauth/google` hoặc tương đương) để kích hoạt popup đăng nhập Google.
    *   Sau khi người dùng đăng nhập thành công, SDK sẽ trả về một chuỗi **`credential`** (đây là Google `IdToken`).
2.  **Bước 2: Gửi IdToken lên Backend**
    *   Frontend gọi API `POST /api/auth/google` truyền `idToken` vừa lấy được.
3.  **Bước 3: Xử lý phản hồi từ Backend**
    *   Email Google đã xác minh chưa có tài khoản được backend tạo thành Student Active và cấp phiên đăng nhập ngay.
    *   Student đăng nhập thành công được chuyển tới danh sách lớp ngay cả khi chưa có chuyên ngành. Student chọn chuyên ngành tại hồ sơ hoặc dropdown trên chính hàng của mình trong lớp.
    *   Dropdown trong lớp gọi `PUT /api/auth/update-major`; trang hồ sơ tiếp tục dùng `PUT /api/auth/update-profile`. Cả hai luồng dùng chung chính sách backend và cập nhật `majorCode`/`major` trong AuthContext từ phản hồi.
    *   Tài khoản hiện có giữ nguyên vai trò và kiểm tra trạng thái; các vai trò ngoài Student giữ điều hướng hiện tại. Không dùng endpoint `complete-google-register`.
