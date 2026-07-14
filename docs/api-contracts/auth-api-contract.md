# Hợp Đồng Giao Tiếp API Xác Thực (Auth API Contracts)

Tài liệu này định nghĩa cấu trúc dữ liệu gửi lên (Request Payload) và dữ liệu trả về (Response Payload) cho tất cả các endpoint liên quan đến phân hệ Xác thực của hệ thống E-HUB.

---

## 1. Định dạng Phản hồi Chung (General Response Formats)

Tất cả các API đều phản hồi theo cấu trúc thống nhất:

### Thành công:
```json
{
  "success": true,
  "message": "Thông điệp thành công.",
  "code": null,
  "data": {},
  "errors": null
}
```

### Lỗi nghiệp vụ hoặc Lỗi hệ thống:
```json
{
  "success": false,
  "message": "Mô tả lỗi tổng quan.",
  "code": "AUTH_ERROR_CODE",
  "data": null,
  "errors": null
}
```

### Lỗi Validation nhập liệu (HTTP 400):
```json
{
  "success": false,
  "message": "Validation failed",
  "code": "COMMON_VALIDATION_ERROR",
  "data": null,
  "errors": [
    {
      "field": "majorCode",
      "message": "Major is required for Student role.",
      "code": "AUTH_STUDENT_MAJOR_REQUIRED"
    }
  ]
}
```

---

## 2. Danh sách Chi tiết Endpoints

### 2.1. Đăng ký tài khoản (Register)
*   **Endpoint:** `POST /api/auth/register`
*   **Mô tả:** Đăng ký tài khoản công khai cho các vai trò Student, Lecturer, Mentor.

#### Request Body (Sinh viên):
```json
{
  "fullName": "Nguyen Van A",
  "email": "anv@fpt.edu.vn",
  "password": "Password123",
  "confirmPassword": "Password123",
  "role": "Student",
  "majorCode": "BIT_SE"
}
```

#### Request Body (Giảng viên / Mentor):
```json
{
  "fullName": "Tran Van B",
  "email": "lecturer@fpt.edu.vn",
  "password": "Password123",
  "confirmPassword": "Password123",
  "role": "Lecturer",
  "majorCode": null
}
```

#### Response (Sinh viên - Tự động kích hoạt & Cấp token):
```json
{
  "success": true,
  "message": "Register successfully",
  "code": null,
  "data": {
    "status": "Active",
    "requiresApproval": false,
    "message": "Register successfully",
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "7ea81b9...",
    "expiresAt": "2026-07-14T10:30:00Z",
    "user": {
      "id": "b6b2c305-bc8d-4b3a-9d68-3579f53ab111",
      "fullName": "Nguyen Van A",
      "email": "anv@fpt.edu.vn",
      "roles": ["Student"],
      "status": "Active",
      "majorCode": "BIT_SE"
    }
  },
  "errors": null
}
```

#### Response (Giảng viên / Mentor - Chờ duyệt):
```json
{
  "success": true,
  "message": "Your account has been registered and is pending admin approval.",
  "code": null,
  "data": {
    "status": "PendingApproval",
    "requiresApproval": true,
    "message": "Your account has been registered and is pending admin approval.",
    "accessToken": null,
    "refreshToken": null,
    "expiresAt": null,
    "user": {
      "id": "0f20e019-bd8a-44fb-9aef-3e8b36e93223",
      "fullName": "Tran Van B",
      "email": "lecturer@fpt.edu.vn",
      "roles": ["Lecturer"],
      "status": "PendingApproval",
      "majorCode": null
    }
  },
  "errors": null
}
```

---

### 2.2. Đăng nhập Email & Password (Login)
*   **Endpoint:** `POST /api/auth/login`
*   **Mô tả:** Đăng nhập thông thường.

#### Request Body:
```json
{
  "email": "anv@fpt.edu.vn",
  "password": "Password123"
}
```

#### Response (Thành công):
```json
{
  "success": true,
  "message": "Login successfully",
  "code": null,
  "data": {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "7ea81b9...",
    "expiresAt": "2026-07-14T10:30:00Z",
    "user": {
      "id": "b6b2c305-bc8d-4b3a-9d68-3579f53ab111",
      "fullName": "Nguyen Van A",
      "email": "anv@fpt.edu.vn",
      "roles": ["Student"],
      "status": "Active",
      "majorCode": "BIT_SE"
    }
  },
  "errors": null
}
```

---

### 2.3. Đăng nhập Google (Google Login)
*   **Endpoint:** `POST /api/auth/google`
*   **Mô tả:** Truyền Google ID Token được cung cấp từ SDK của client lên Backend.

#### Request Body:
```json
{
  "idToken": "google_id_token_here"
}
```

#### Response (Thành công - Đã có tài khoản trước đó):
*(Cấu hình phản hồi thành công tương tự như `/api/auth/login`)*

#### Response (Thất bại - Chưa có tài khoản):
```json
{
  "success": false,
  "message": "Account is not registered. Please create an account first.",
  "code": "AUTH_ACCOUNT_NOT_REGISTERED",
  "data": null,
  "errors": null
}
```

---

### 2.4. Xem thông tin User hiện tại (Get Me)
*   **Endpoint:** `GET /api/auth/me`
*   **Mô tả:** Lấy thông tin phiên làm việc hiện hành của người dùng. Yêu cầu truyền Header: `Authorization: Bearer <access_token>`.

#### Response:
```json
{
  "success": true,
  "message": "Success",
  "code": null,
  "data": {
    "id": "b6b2c305-bc8d-4b3a-9d68-3579f53ab111",
    "fullName": "Nguyen Van A",
    "email": "anv@fpt.edu.vn",
    "roles": ["Student"],
    "status": "Active",
    "majorCode": "BIT_SE"
  },
  "errors": null
}
```

---

### 2.5. Làm mới Access Token (Refresh Token)
*   **Endpoint:** `POST /api/auth/refresh-token`
*   **Mô tả:** Sử dụng Refresh Token để lấy Access Token mới.

#### Request Body:
```json
{
  "refreshToken": "7ea81b9..."
}
```

#### Response (Thành công):
*(Trả về AccessToken, RefreshToken mới và thông tin User tương tự AuthResponse)*

---

### 2.6. Đăng xuất (Logout)
*   **Endpoint:** `POST /api/auth/logout`
*   **Mô tả:** Hủy Refresh Token hiện hành trong CSDL.

#### Request Body:
```json
{
  "refreshToken": "7ea81b9..."
}
```

#### Response:
```json
{
  "success": true,
  "message": "Logged out successfully",
  "code": null,
  "data": null,
  "errors": null
}
```
