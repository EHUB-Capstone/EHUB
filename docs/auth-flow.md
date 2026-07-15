# Luồng Nghiệp Vụ Xác Thực & Phân Quyền (Auth Flow)

Tài liệu này đặc tả toàn bộ quy trình nghiệp vụ liên quan đến Đăng ký, Đăng nhập, Duyệt tài khoản và Xác thực qua mạng xã hội Google cho dự án E-HUB.

---

## 1. Vai trò Người dùng (Public Roles)

Hệ thống cho phép đăng ký công khai (Public Register) 3 vai trò:
*   **Student** (Sinh viên)
*   **Lecturer** (Giảng viên)
*   **Mentor** (Cố vấn doanh nghiệp)

> [!WARNING]
> **Lưu ý bảo mật:** Tuyệt đối không cho phép đăng ký công khai vai trò **Admin**. Tài khoản Admin chỉ được khởi tạo qua seed data hệ thống hoặc được cấp bởi một Admin hiện tại.

---

## 2. Luồng Trạng thái Tài khoản (User Status Flow)

Mỗi vai trò sau khi đăng ký thành công sẽ nhận được trạng thái tương ứng:

```
[ Đăng Ký Công Khai ]
        │
        ├──> Student ────────────────> [ UserStatus.Active ] ──> Được phép Đăng nhập ngay
        │
        ├──> Lecturer / Mentor ──────> [ UserStatus.PendingApproval ] ──> Đăng nhập bị chặn (Chờ duyệt)
```

*   **Student:** Kích hoạt tự động (`Active`), cấp token ngay sau đăng ký để tự động đăng nhập.
*   **Lecturer / Mentor:** Trạng thái chờ duyệt (`PendingApproval`). Cần Admin phê duyệt thông qua hệ thống quản trị để chuyển sang `Active`.

---

## 3. Quyền Đăng nhập Theo Trạng thái

Khi người dùng thực hiện Đăng nhập (Local hoặc Google):
*   **`Active`:** Đăng nhập thành công, cấp Access Token & Refresh Token.
*   **`PendingApproval`:** Từ chối đăng nhập $\rightarrow$ Trả lỗi HTTP 403 kèm mã lỗi `AUTH_ACCOUNT_PENDING_APPROVAL` ("Tài khoản đang chờ duyệt").
*   **`Inactive`:** Từ chối đăng nhập $\rightarrow$ Trả lỗi HTTP 403 kèm mã lỗi `AUTH_USER_INACTIVE` ("Tài khoản đã bị vô hiệu hóa").
*   **`Blocked`:** Từ chối đăng nhập $\rightarrow$ Trả lỗi HTTP 403 kèm mã lỗi `AUTH_USER_BLOCKED` ("Tài khoản đã bị khóa").
*   **`Rejected`:** Từ chối đăng nhập $\rightarrow$ Trả lỗi HTTP 403 kèm mã lỗi `AUTH_ACCOUNT_REJECTED` ("Yêu cầu đăng ký bị từ chối").

---

## 4. Danh sách Mã Chuyên ngành (Major Codes)

Đối với vai trò **Student**, người dùng bắt buộc phải chọn chuyên ngành và hệ thống lưu trữ dưới dạng mã chuyên ngành (Code) thay vì tên hiển thị (Display Name) để đảm bảo tính ổn định:

### Nhóm 1: Bachelor of Business Administration (BBA)
*   `BBA_HM`: Hospitality Management (Quản trị Khách sạn)
*   `BBA_IB`: International Business (Kinh doanh Quốc tế)
*   `BBA_MC`: Marketing & Communication (Truyền thông Marketing)
*   `BBA_MKT`: Marketing (Marketing)
*   `BEN`: Business English (Tiếng Anh Thương mại)
*   `BBA_TM`: Tourism Management (Quản trị Du lịch)

### Nhóm 2: Bachelor of Information Technology (BIT)
*   `BIT_AI`: Artificial Intelligence (Trí tuệ Nhân tạo)
*   `BIT_GD`: Graphic Design (Thiết kế Đồ họa)
*   `BIT_IA`: Information Assurance (An toàn Thông tin)
*   `BIT_SE`: Software Engineering (Kỹ thuật Phần mềm)

---

## 5. Quy tắc Mật khẩu (Password Rules)
*   Bắt buộc điền mật khẩu (`Password`) và xác nhận mật khẩu (`ConfirmPassword`).
*   Độ dài tối thiểu của mật khẩu: **8 ký tự**.
*   Trường `ConfirmPassword` bắt buộc phải trùng khớp hoàn toàn với `Password`.

---

## 6. Luồng Đăng nhập Google (Google Login Flow)
*   Frontend gửi `IdToken` do Google SDK cung cấp lên API Backend.
*   Backend gọi API Google để xác thực và lấy thông tin Email, FullName.
*   **Kiểm tra tính tồn tại:**
    *   Nếu email **đã tồn tại** $\rightarrow$ Kiểm tra trạng thái tài khoản (`Active` cấp token, `Pending` chặn).
    *   Nếu email **chưa tồn tại** $\rightarrow$ Không tự động tạo tài khoản. Trả về mã lỗi `AUTH_ACCOUNT_NOT_REGISTERED` để Frontend hướng dẫn người dùng chọn vai trò/chuyên ngành và thực hiện hoàn tất đăng ký.
