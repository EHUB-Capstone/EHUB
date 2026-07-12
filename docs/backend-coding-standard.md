# E-HUB Backend Coding Standard

Tài liệu này định nghĩa quy chuẩn code cho lập trình viên phát triển Backend E-HUB nhằm đảm bảo mã nguồn sạch, đồng bộ và bảo mật.

---

## 1. Nguyên tắc Kiến trúc Lõi (Clean Architecture Rules)

1.  **Dependency Rule:**
    *   Mối quan hệ tham chiếu bắt buộc: `Api -> Application -> Domain`.
    *   Không cho phép tham chiếu ngược (ví dụ: `Domain` không được reference bất cứ thứ gì khác; `Application` không được tham chiếu tới `Infrastructure`).
2.  **Controller Rules:**
    *   Controller chỉ đóng vai trò nhận HTTP request, chuyển tiếp sang tầng Application xử lý và trả về API Response chuẩn.
    *   Nghiêm cấm viết business logic hoặc truy vấn DB trực tiếp trong Controller.
    *   Không gọi `DbContext` trực tiếp trong Controller.
3.  **Entity Protection:**
    *   Không bao giờ trả trực tiếp Entity (các lớp định nghĩa DB trong tầng Domain) ra API response.
    *   Tất cả dữ liệu ra/vào API bắt buộc phải đi qua các lớp DTO (khai báo trong `EHub.Contracts`).

---

## 2. Quy tắc đặt tên (Naming Conventions)

*   **Tên Class/Interface:** Sử dụng PascalCase (ví dụ: `UserRepository`, `CreateProjectCommand`).
*   **Tên Interface:** Bắt buộc bắt đầu bằng chữ `I` (ví dụ: `IUserRepository`).
*   **Tên Method:** Sử dụng PascalCase (ví dụ: `GetByIdAsync`, `Handle`).
*   **Tên Biến/Tham số:** Sử dụng camelCase (ví dụ: `userId`, `projectDescription`).
*   **Tên Hằng số:** Sử dụng PascalCase hoặc UPPER_SNAKE_CASE trong trường hợp đặc biệt.
*   **Tính bất đồng bộ:** Bất kỳ phương thức nào có kiểu trả về là `Task` hoặc `ValueTask` phải kết thúc bằng hậu tố `Async` (ví dụ: `SaveChangesAsync`, `LoginAsync`).

---

## 3. Quản lý Lỗi & Exception Handling

1.  **Global Exception:**
    *   Không sử dụng khối lệnh `try-catch` tràn lan trong các Controller hoặc Application services.
    *   Tất cả lỗi unhandled sẽ được bắt tự động bởi `ExceptionHandlingMiddleware` trong layer `EHub.Api`.
2.  **Custom Exceptions:**
    *   Khi xảy ra lỗi logic nghiệp vụ hoặc không tìm thấy dữ liệu, hãy throw các Exception cụ thể (`NotFoundException`, `BusinessRuleException`, `ValidationException`) thay vì throw `Exception` chung chung.
3.  **Mã lỗi nghiệp vụ (Error Codes):**
    *   Trả kèm theo mã code dạng chuỗi (ví dụ: `AUTH_INVALID_CREDENTIALS`) để Frontend hiển thị thông báo lỗi thân thiện cho người dùng.

---

## 4. Kiểm soát Dữ liệu Đầu vào (Validation)

*   Sử dụng thư viện **FluentValidation** để kiểm tra tính hợp lệ của request trước khi xử lý nghiệp vụ.
*   Các file Validator được đặt trong layer `EHub.Application/Validators/` hoặc đi kèm theo từng Feature.
*   Không thực hiện validation thủ công bằng câu lệnh `if` phức tạp trong service.

---

## 5. Bảo mật & Bảo vệ Thông tin nhạy cảm (Security Baseline)

1.  **Mã hóa mật khẩu:**
    *   Không bao giờ lưu mật khẩu dưới dạng văn bản thuần (plain text). Bắt buộc sử dụng hàm băm một chiều (ví dụ: BCrypt, Argon2).
2.  **Thông tin nhạy cảm:**
    *   Không bao giờ ghi nhận (Log) thông tin nhạy cảm như Mật khẩu, JWT token, Refresh token, API secret key.
3.  **Hardcode Secrets:**
    *   Tuyệt đối không hardcode chuỗi kết nối Database, JWT secret key, Cloudinary keys hoặc bất kỳ mật khẩu nào vào code (`appsettings.json`). Bắt buộc đọc từ biến môi trường (`.env` hoặc Environment Variables).
4.  **Bảo vệ Endpoint:**
    *   Các endpoint cần xác thực phải được gắn thẻ `[Authorize]` hoặc các Policy cụ thể.

---

## 6. Định dạng Phản hồi (API Response Standard)

Tất cả API Response phải tuân thủ cấu trúc của `ApiResponse` trong `EHub.Contracts/Common`:

*   **Response thành công:**
    ```json
    {
      "success": true,
      "message": "Success",
      "data": { ... },
      "errors": null
    }
    ```
*   **Response lỗi:**
    ```json
    {
      "success": false,
      "message": "Validation failed",
      "code": "VALIDATION_ERROR",
      "data": null,
      "errors": [
        {
          "field": "Email",
          "message": "Email is not valid"
        }
      ]
    }
    ```
