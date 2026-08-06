# Danh mục kiểm thử thủ công quản lý lớp học

Sử dụng các tài khoản Admin, Giảng viên (Lecturer), Sinh viên (Student) và Cố vấn (Mentor) riêng biệt. Giữ DevTools Network mở và bật tùy chọn "Preserve log". Đối với các trường hợp kiểm thử đồng thời (concurrency), hãy mở hai profile trình duyệt hoặc một cửa sổ ẩn danh.

## A. Hành vi Lưu trữ (Archive) và Chỉ đọc (Read-only)

1. Đăng nhập với vai trò Admin, mở **Quản lý lớp học (Class Management)**, chọn một lớp đang Hoạt động (Active) và bấm **Xem chi tiết (View Detail)**.
2. Ghi nhận giảng viên của lớp, tất cả các dòng lịch học, số lượng sinh viên, số lượng nhóm (team) và phản hồi `rowVersion` trong Network tab.
3. Bấm **Lưu trữ lớp (Archive Class)**, nhập lý do tối thiểu 3 ký tự, xác nhận.
4. Kết quả mong đợi: Hiển thị thông báo thành công (toast), chuyển hướng về danh sách lớp, và lớp đó biến mất khỏi chế độ xem mặc định Active/Draft.
5. Lọc Trạng thái = **Đã lưu trữ (Archived)**. Kết quả mong đợi: Thẻ lớp học đã lưu trữ được hiển thị và có nút **Khôi phục (Restore)** thay vì **Import Sinh viên (Import Students)**.
6. Mở lớp học đã lưu trữ. Kết quả mong đợi: Hiển thị thông báo màu hổ quỳnh "Lớp học đã lưu trữ — chỉ đọc"; không có các bộ điều khiển Thêm, Import, sửa lịch học, sửa giảng viên, sửa cố vấn, thay đổi chuyên ngành hoặc thao tác nhóm. Các nút Export và nút **Sửa nhóm chat (Repair Chats)** của Admin vẫn khả dụng.
7. Trong DevTools, gửi lại (replay) một yêu cầu thêm sinh viên hoặc sửa lịch học trước đó đối với lớp đã lưu trữ. Kết quả mong đợi: Phản hồi mã HTTP 409 với mã `CLASS_ARCHIVED`; việc ẩn UI không phải là ranh giới bảo mật duy nhất.
8. Xác nhận danh sách lớp (roster), các nhóm, cố vấn và lịch sử vẫn hiển thị đầy đủ và số lượng của chúng không bị thay đổi.

## B. Khôi phục thành công và xử lý lỗi xác thực

1. Từ danh sách Đã lưu trữ (Archived), bấm **Khôi phục (Restore)**, nhập lý do, xác nhận.
2. Kết quả mong đợi: Backend xác thực lại môn học, học kỳ, vai trò/trạng thái giảng viên, mối quan hệ giảng viên chính, lịch học, mã/chỉ số lớp và các xung đột; nếu thành công, lớp học sẽ trở về trạng thái trước khi lưu trữ.
3. Mở lớp học và xác nhận tất cả dữ liệu danh sách/nhóm/lịch học vẫn nguyên vẹn và các bộ điều khiển chỉnh sửa xuất hiện trở lại.
4. Kiểm thử trường hợp thất bại: Lưu trữ một lớp học, sau đó chuyển giảng viên của lớp đó sang trạng thái Ngưng hoạt động (Inactive) hoặc tạo một xung đột lịch học cùng học kỳ dưới quyền Admin. Thử Restore lớp đó.
5. Kết quả mong đợi: HTTP 409 `CLASS_RESTORE_INVALID` kèm lý do cụ thể; lớp học vẫn giữ nguyên trạng thái Đã lưu trữ và chỉ đọc.
6. Kiểm thử phiên bản cũ (Stale version): Giữ trang chi tiết lớp đã lưu trữ mở trên hai profile trình duyệt Admin. Khôi phục trên profile A, sau đó gửi thao tác khôi phục đã cũ trên profile B. Kết quả mong đợi: Phản hồi thành công an toàn theo cơ chế idempotent hoặc phản hồi xung đột 409, không bao giờ tạo trùng lặp nhật ký kiểm toán (audit) hoặc trạng thái không hoàn chỉnh.

## C. Xác minh nhật ký kiểm toán (Audit)

1. Trong Swagger môi trường Development, xác thực với vai trò Admin và gọi `GET /api/classes/{id}/audit?page=1&pageSize=25`.
2. Kết quả mong đợi: Các bản ghi `CLASS_ARCHIVED` và `CLASS_RESTORED` chứa thông tin người thực hiện (actor), nhãn thời gian UTC, trạng thái trước/sau và lý do đã nhập.
3. Gọi cùng API này với token của Giảng viên, Sinh viên và Cố vấn. Kết quả mong đợi: HTTP 403.

## D. Đồng bộ và sửa chữa thành viên nhóm chat

1. Sử dụng một lớp học có một giảng viên, các sinh viên đang hoạt động, ít nhất một nhóm chính thức và một phân công cố vấn đang hoạt động.
2. Với vai trò Admin, bấm **Sửa nhóm chat (Repair Chats)**. Kết quả mong đợi: Thông báo toast liệt kê các nhóm đã được tạo, các thành viên được thêm/kích hoạt lại và các tư cách thành viên quá hạn đã kết thúc.
3. Bấm lại nút này lần nữa mà không thay đổi dữ liệu. Kết quả mong đợi: Tất cả các bộ đếm đều bằng 0 (thao tác repair mang tính idempotent).
4. Phân công lại giảng viên; sau khi worker poll dữ liệu từ outbox (thường dưới 10 giây), chạy lại Repair. Kết quả mong đợi: Giảng viên cũ ngưng hoạt động trong tư cách thành viên chat lớp/nhóm và giảng viên mới được kích hoạt.
5. Thêm một sinh viên, cho sinh viên đó rời lớp, sau đó ghi danh lại. Sau mỗi hành động, chờ worker hoặc chạy Repair. Kết quả mong đợi: Tư cách thành viên được thêm, kết thúc với `LeftAt`, sau đó kích hoạt lại mà không tạo thêm các dòng trùng lặp.
6. Thay đổi thành viên/trưởng nhóm và phân công/thay đổi/kết thúc cố vấn. Kết quả mong đợi: Nhóm TeamGroup chỉ bao gồm các thành viên nhóm hiện tại đang hoạt động, giảng viên hiện tại và cố vấn hiện tại đang hoạt động.
7. Lưu trữ lớp học. Kết quả mong đợi: Các nhóm chat của lớp/nhóm có `is_read_only=true`; khi khôi phục, thuộc tính này đổi lại thành false.
8. Gọi `POST /api/classes/{id}/repair-chat-memberships` dưới vai trò Giảng viên. Kết quả mong đợi: HTTP 403.

## E. Khả năng chống chịu của Hệ thống thông báo / Outbox

1. Gửi/duyệt đề xuất nhóm, gửi/duyệt định hướng dự án và phân công cố vấn.
2. Kết quả mong đợi: Giao dịch nghiệp vụ ghi một dòng vào outbox; worker tạo đúng thông báo cho người nhận và đánh dấu sự kiện là Processed (Đã xử lý).
3. Khởi động lại API khi một sự kiện đang ở trạng thái Pending. Kết quả mong đợi: Sự kiện được thử lại và xử lý; quy tắc duy nhất `SourceEventId + RecipientUserId` ngăn chặn tạo thông báo trùng lặp.
4. Kiểm tra log để tìm các trường có cấu trúc `OutboxEventId`, `OutboxEventType`, `ClassId` và số lần thử (attempt count). Các sự kiện thất bại sau số lần thử tối đa phải hiển thị là `Failed` và được ghi log ở mức Error.

## F. Kiểm thử thoái lùi giao diện (UI Component Regression)

1. ClassManagement: Bộ lọc, phân trang, thẻ Đã lưu trữ, Xem chi tiết, tính năng hiển thị Restore và Import hoạt động đúng theo trạng thái/vai trò.
2. ClassDetail: Các nút nhỏ thu gọn không bị đè chữ/xuống dòng sai ở độ phân giải 1366px và màn hình di động; trạng thái đã lưu trữ hiển thị chế độ chỉ đọc.
3. EditScheduleModal: Chấp nhận nhiều dòng lịch học; từ chối nếu trùng ngày/ca học; payload chỉ chứa `schedules` và `rowVersion`.
4. ImportStudentsModal: Chấp nhận file `.xls` và `.xlsx` dung lượng lên đến 10 MB; từ chối đuôi file không hỗ trợ trước khi tải lên; các lỗi MIME/chữ ký file từ backend được hiển thị qua `parseApiError`.

## G. API, Đồng thời (Concurrency) và Hiệu năng

1. Gọi các endpoint vòng đời lớp học khi không có token (401), với vai trò không phải Admin (403), thiếu/sai lý do hoặc rowVersion (400), rowVersion bị cũ (409), và dữ liệu Admin hợp lệ (200).
2. Gửi đồng thời hai yêu cầu Restore với cùng một rowVersion. Kết quả mong đợi: Chỉ có một lần chuyển trạng thái và duy nhất một bản ghi audit `CLASS_RESTORED`.
3. Chạy lại các bài kiểm thử lịch học, phân công, ghi danh đồng thời, duyệt đề xuất và rollback import từ danh mục an toàn trước đó.
4. Tạo dữ liệu mẫu (seed) ít nhất 10,000 bản ghi ghi danh trên các lớp. Yêu cầu trang danh sách với kích thước 20 và 100 kết hợp bộ lọc tìm kiếm, chuyên ngành và trạng thái. Kết quả mong đợi: Các câu lệnh SQL có giới hạn, không bị lặp truy vấn theo từng dòng (N+1 query loop), phân trang ổn định và kế hoạch thực thi cơ sở dữ liệu chấp nhận được nhờ sử dụng chỉ mục ghi danh/nhóm.
5. Build các tài nguyên production phía frontend. Kết quả mong đợi: Tạo ra các chunk theo route; không còn tình trạng duy nhất một bundle JavaScript ban đầu nặng khoảng 2.33 MB.
