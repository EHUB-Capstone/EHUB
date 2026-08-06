# Hướng dẫn migration và rollback cho quản lý lớp học

## Trước khi áp dụng

1. Tải bản sao lưu (backup) của cơ sở dữ liệu PostgreSQL mục tiêu và ghi lại migration hiện đang được áp dụng.
2. Xác nhận phiên bản ứng dụng và bộ migration được build từ cùng một commit.
3. Dừng các background worker trên tất cả các instance ngoại trừ một instance trong quá trình chạy migration.
4. Xem xét kỹ migration `20260806062355_AddClassLifecycleAndChatRepair`. Migration này thêm các cột `classes.status_before_archive`, `chat_groups.is_read_only`, chuẩn hóa các dòng chat cũ trùng lặp bằng cách xóa mềm (soft-delete) các bản ghi trùng, sau đó bắt buộc duy trì tính duy nhất cho các nhóm chat và tư cách thành viên chat lớp/nhóm.

## Áp dụng tại môi trường local

Từ thư mục `backend`:

```powershell
dotnet ef database update --project src/EHub.Infrastructure --startup-project src/EHub.Api
```

Development API cũng sẽ tự động áp dụng các migration chưa chạy khi khởi động. Đối với môi trường Production, triển khai bắt buộc phải sử dụng một job migration riêng biệt; không phụ thuộc vào việc ứng dụng tự chạy migration khi khởi động.

## Xác minh sau khi áp dụng

1. Mở `/swagger` trong môi trường Development và kiểm tra các endpoint `POST /api/classes/{id}/archive`, `POST /restore`, `GET /audit`, và `POST /repair-chat-memberships`.
2. Xác nhận migration mới nhất đã xuất hiện trong bảng `__EFMigrationsHistory`.
3. Xác nhận có tối đa một `ClassGroup` đang hoạt động cho mỗi lớp và một `TeamGroup` đang hoạt động cho mỗi nhóm.
4. Xác nhận các sự kiện Pending trong outbox đã được xử lý và không có sự kiện nào bị ở trạng thái Failed mà không có cảnh báo.
5. Chạy danh mục kiểm thử Lưu trữ/Khôi phục và sửa nhóm chat trong file `MANUAL_TEST_CHECKLIST.md`.

## Rollback (Quay phát)

Quay phát các file binary của ứng dụng trước nếu mã nguồn mới đã được triển khai, sau đó từ thư mục `backend` chạy lệnh:

```powershell
dotnet ef database update 20260805081746_AddOutboxNotificationIdempotency --project src/EHub.Infrastructure --startup-project src/EHub.Api
```

Migration down sẽ xóa hai cột mới và khôi phục các chỉ mục (index) không duy nhất. Các dòng chat cũ trùng lặp đã bị xóa mềm cố tình không được tự động kích hoạt lại vì việc đó có thể tái xuất hiện các nguồn dữ liệu không rõ ràng (ambiguous sources of truth). Chỉ khôi phục chúng từ bản sao lưu trước khi migration sau khi đã kiểm tra lại dữ liệu.

## Khôi phục lại từ đầu (Destructive Reset) tại local

Tính năng xóa vĩnh viễn lớp học cố tình không được cung cấp trên các API môi trường Production. Để xóa toàn bộ cơ sở dữ liệu Docker tại local và tạo lại dữ liệu mẫu, hãy chạy lệnh sau từ thư mục gốc của repository:

```powershell
.\scripts\dev-reset-local-database.ps1 -IUnderstandThisDeletesLocalData
```

Công cụ này chỉ áp dụng cho `docker-compose.local.yml`, yêu cầu tham số xác nhận rõ ràng, và từ chối chạy nếu môi trường ASP.NET không phải là Development. Lệnh này sẽ xóa TOÀN BỘ dữ liệu cơ sở dữ liệu local, chứ không phải chỉ xóa một lớp học.
