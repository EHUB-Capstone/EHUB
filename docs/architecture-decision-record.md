# Architecture Decision Record (ADR) - E-HUB Backend

Tài liệu này ghi nhận các quyết định kiến trúc và công nghệ cốt lõi được thống nhất cho dự án E-HUB Backend.

---

## ADR-001: Lựa chọn .NET 10 LTS làm Target Framework

*   **Trạng thái:** Đã phê duyệt (Approved)
*   **Bối cảnh:** Nhóm đang cân nhắc giữa .NET 8 (LTS) và .NET 10 (LTS). .NET 8 kết thúc hỗ trợ vào ngày 10/11/2026, trong khi .NET 10 là bản LTS mới nhất được Microsoft hỗ trợ chính thức đến ngày 14/11/2028.
*   **Quyết định:** Sử dụng **.NET 10 (LTS)** làm Framework thống nhất cho tất cả các project trong solution.
*   **Hệ quả:**
    *   Tận dụng vòng đời hỗ trợ dài hạn (đến cuối năm 2028).
    *   Tận dụng các tính năng mới của C# 14 và .NET 10.
    *   Tất cả dev trong nhóm bắt buộc cài đặt .NET 10 SDK (máy local đã cài sẵn `.NET 10.0.204`).

---

## ADR-002: Áp dụng kiến trúc Clean Architecture

*   **Trạng thái:** Đã phê duyệt (Approved)
*   **Bối cảnh:** Hệ thống E-HUB có nhiều phân hệ phức tạp như Quản lý Học thuật, Lưu trữ Dự án, Đánh giá Rubric, Mentor Matching và Cổng Ươm tạo. Việc viết code lẫn lộn giữa logic nghiệp vụ và mã truy xuất database sẽ gây khó khăn lớn cho việc bảo trì, làm việc nhóm và viết kiểm thử (Unit Test).
*   **Quyết định:** Phân tách hệ thống thành 6 layers theo mô hình Clean Architecture:
    1.  `EHub.Domain`: Nghiệp vụ cốt lõi (Entities, Enums, Rules). Không phụ thuộc layer nào khác.
    2.  `EHub.Shared`: Kiểu dữ liệu dùng chung (Result, Constants, Errors).
    3.  `EHub.Contracts`: Định nghĩa Request/Response DTO cố định làm API contract.
    4.  `EHub.Application`: Use cases, service interfaces, validators, CQRS/MediatR (nếu có). Phụ thuộc vào `Domain`, `Contracts`, `Shared`.
    5.  `EHub.Infrastructure`: Hiện thực các interfaces (EF Core PostgreSQL DbContext, Repositories, JWT, Cloudinary). Phụ thuộc vào `Application`, `Domain`, `Shared`.
    6.  `EHub.Api`: RESTful API Controllers, Global Middlewares, App configurations. Phụ thuộc vào tất cả các layer còn lại.
*   **Hệ quả:**
    *   Bảo vệ logic nghiệp vụ (Domain & Application) khỏi sự thay đổi của công nghệ bên ngoài.
    *   Quy định rõ ràng dependency một chiều: Không cho phép tham chiếu ngược (ví dụ Application không được tham chiếu trực tiếp tới Infrastructure hay DbContext).

---

## ADR-003: Định nghĩa vị trí của Repository Interfaces

*   **Trạng thái:** Đã phê duyệt (Approved)
*   **Bối cảnh:** Team cần quyết định đặt Repository Interface trong tầng `Domain` hay tầng `Application`.
*   **Quyết định:** Đặt Repository Interfaces tại tầng **`EHub.Application/Common/Interfaces/Persistence/`**. Hiện thực hóa chúng tại tầng **`EHub.Infrastructure/Persistence/Repositories/`**.
*   **Lý do:** Tầng Application chứa logic của use case, là nơi trực tiếp sử dụng Repository để lấy và lưu dữ liệu. Đặt tại Application giúp phân định rõ ràng ranh giới nghiệp vụ sử dụng dữ liệu, đồng thời tránh làm phình to layer Domain.
*   **Chiến lược sử dụng:**
    *   Sử dụng cơ chế hybrid: Sử dụng interface chung `IApplicationDbContext` cho các tác vụ CRUD đơn giản của các thực thể nhỏ để đẩy nhanh tiến độ làm MVP (tránh tạo file thừa).
    *   Chỉ tạo Repository chuyên biệt (ví dụ `IUserRepository`, `IProjectRepository`, `ISubmissionRepository`) cho các module có nghiệp vụ phức tạp, truy vấn nâng cao hoặc xử lý phân quyền phức tạp.

---

## ADR-004: Sử dụng PostgreSQL và Entity Framework Core làm hệ quản trị CSDL & ORM

*   **Trạng thái:** Đã phê duyệt (Approved)
*   **Quyết định:** Sử dụng PostgreSQL làm CSDL chính và Entity Framework Core 10 làm ORM để ánh xạ đối tượng.
*   **Migration Rule:**
    *   Mọi thay đổi cấu trúc DB bắt buộc phải đi qua EF Core Migrations. Nghiêm cấm thay đổi DB thủ công trên PGAdmin hoặc các công cụ trực quan.
    *   Các file Migration được tạo ra phải được commit lên GitHub.
    *   Trước khi deploy lên môi trường Production, bắt buộc phải generate SQL script từ migration để review trước.

---

## ADR-005: DevOps local bằng Docker Compose

*   **Trạng thái:** Đã phê duyệt (Approved)
*   **Quyết định:** Đóng gói PostgreSQL chạy local trong container thông qua file `docker-compose.local.yml`. Các lập trình viên trong nhóm chỉ cần cài Docker Desktop và khởi chạy DB bằng lệnh `docker compose up -d`. Chưa container hóa frontend/backend ở tuần đầu tiên.
*   **CI/CD:** Dựng sẵn pipeline GitHub Actions CI cho Backend (`backend-ci.yml`) và Frontend (`frontend-ci.yml`) để tự động kiểm tra cú pháp, build và test code trước khi merge vào nhánh `develop`.
