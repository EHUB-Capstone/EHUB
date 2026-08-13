# Ma trận phân quyền quản lý lớp học

Backend là nguồn sự thật duy nhất về phân quyền. Việc ẩn hoặc hiện thao tác trên giao diện chỉ hỗ trợ trải nghiệm và không thay thế kiểm tra quyền tại API.

Trong bảng dưới đây, “Lecturer phụ trách” là giảng viên có `UserId` trùng với `PrimaryLecturerId` hiện tại của lớp. Lecturer khác không có quyền quản lý lớp đó.

| Thao tác | Admin | Lecturer phụ trách | Lecturer khác | Student | Mentor được phân công |
|---|---:|---:|---:|---:|---:|
| Xem danh sách và chi tiết lớp | Tất cả lớp | Lớp được phân công | Không | Qua API dành cho Student | Qua API dành cho Mentor |
| Tạo hoặc import lớp | Có | Có | Có | Không | Không |
| Thay đổi lịch học | Có | Có, khi lớp đang hoạt động | Không | Không | Không |
| Phân công, thay đổi hoặc gỡ Lecturer phụ trách | Có | Không | Không | Không | Không |
| Thêm, import, drop hoặc ghi danh lại Student | Có | Có, khi lớp đang hoạt động | Không | Không | Không |
| Verify, sửa, lock hoặc unlock major tại enrollment | Có | Có, khi lớp đang hoạt động | Không | Không | Không |
| Tạo và quản lý Team chính thức | Có | Có, khi lớp đang hoạt động | Không | Chỉ gửi Team Proposal | Không |
| Duyệt Team Proposal | Có | Có, khi lớp đang hoạt động | Không | Không | Không |
| Assign, reassign hoặc end Mentor assignment | Có | Có, khi lớp đang hoạt động | Không | Không | Chỉ xem assignment của mình |
| Duyệt Project Direction | Có | Có, đối với lớp được phân công | Không | Leader gửi nội dung | Không |
| Archive hoặc restore lớp | Có | Có | Không | Không | Không |
| Xem audit log của lớp | Có | Có | Không | Không | Không |
| Repair Chat Memberships của lớp | Có | Có | Không | Không | Không |
| Xóa vĩnh viễn lớp trong production | Không | Không | Không | Không | Không |

Các lớp đã archive vẫn giữ roster, team, project direction, lịch sử mentor, audit và chat. Các API thay đổi dữ liệu phải từ chối thao tác trên lớp đã archive, ngoại trừ các nghiệp vụ được thiết kế riêng cho trạng thái này như restore và Repair Chat Memberships.

Khi Admin thay đổi Lecturer phụ trách, Lecturer cũ mất quyền quản lý ngay và Lecturer mới nhận quyền theo `PrimaryLecturerId`. Mentor không được truy cập route `/lecturer` hoặc màn hình quản lý lớp; dữ liệu của Mentor chỉ lấy từ `MentorAssignment` đang hoạt động và giới hạn trong Team được phân công.
