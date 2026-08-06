# Ma trận phân quyền quản lý lớp học

Phân quyền ở Backend là nguồn dữ liệu chuẩn duy nhất (source of truth). Việc ẩn nút trên giao diện chỉ là biện pháp hỗ trợ trải nghiệm người dùng và không bao giờ thay thế cho việc kiểm tra quyền tại API.

| Thao tác | Admin | Giảng viên | Sinh viên | Cố vấn được phân công |
|---|---:|---:|---:|---:|
| Xem danh sách tất cả các lớp, bao gồm lớp Đã lưu trữ | Có | Có | Không | Không |
| Xem chi tiết vận hành lớp học | Có | Có | Lớp đang học thông qua các endpoint sinh viên | Không có route giảng viên |
| Tạo/Import một lớp học | Có | Có | Không | Không |
| Thay đổi lịch học | Có | Có (lớp chưa lưu trữ) | Không | Không |
| Phân công/Thay đổi giảng viên phụ trách | Có | Có (lớp chưa lưu trữ) | Không | Không |
| Thêm/Import/Cho rời/Ghi danh lại sinh viên | Có | Có (lớp chưa lưu trữ) | Không | Không |
| Xác minh/Khoá chuyên ngành ghi danh | Có | Có (lớp chưa lưu trữ) | Không | Không |
| Tạo/Quản lý một nhóm (team) chính thức | Có | Có (lớp chưa lưu trữ) | Chỉ gửi đề xuất | Không |
| Duyệt đề xuất nhóm | Có | Có | Không | Không |
| Phân công/Kết thúc phân công cố vấn | Có | Có (lớp chưa lưu trữ) | Không | Chỉ xem phân công của mình |
| Gửi định hướng dự án | Không | Chỉ duyệt cho lớp được phân công | Trưởng nhóm | Không |
| Lưu trữ/Khôi phục lớp học | Có | Có | Không | Không |
| Kiểm tra nhật ký audit của lớp học | Có | Có | Không | Không |
| Sửa chữa thành viên nhóm chat | Có | Có | Không | Không |
| Xóa vĩnh viễn lớp học ở môi trường production | Không vai trò nào | Không vai trò nào | Không vai trò nào | Không vai trò nào |

Các lớp học đã lưu trữ vẫn giữ nguyên danh sách sinh viên, các nhóm, định hướng dự án, lịch sử cố vấn, nhật ký audit và lịch sử chat. Mỗi API thay đổi dữ liệu (mutation API) phải độc lập từ chối các thao tác trên lớp đã lưu trữ ngay cả khi client tự gọi trực tiếp.

Cố vấn không bao giờ được cấp quyền truy cập vào `/lecturer` hoặc `/classes/:id`. Dữ liệu sử dụng của cố vấn phải được trích xuất từ các bản ghi `MentorAssignment` đang hoạt động và chỉ áp dụng cho nhóm được phân công.
