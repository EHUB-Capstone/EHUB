# Module 4 — Hồ sơ mentor, hỗ trợ nhóm và phân mentor AI

- Hồ sơ mentor dùng xuyên học kỳ; admin quản lý loại Enterprise/Academic, tương ứng Business/IT.
- Mentor cập nhật chuyên môn, Skills, kinh nghiệm, giới thiệu, tổ chức và liên kết nghề nghiệp.
- CV/portfolio PDF, DOC, DOCX tối đa 10 MB được kiểm tra loại, cấu trúc và quyền trước khi lưu.
  CV chỉ trích nội dung nghề nghiệp, giữ kỹ năng nhập tay và loại mentor; không đưa dữ liệu cá nhân
  thành fixture production hay gửi CV gốc cho AI. Admin có thể nhập Excel rồi upload nhiều CV,
  đối chiếu email để gắn đúng tài khoản.
- Danh mục mentor và phiên mentoring tiếp tục dùng quyền theo học kỳ/lớp/nhóm, trạng thái,
  concurrency và storage hiện có. Giảng viên phải được phân công; mentor phải đúng nhóm.
- Gợi ý mentor và phân hàng loạt dùng Gemini cùng chính sách backend 70/15/15.
  Chấm riêng hai loại và chỉ vị trí trống; giữ mentor hiện có, kế thừa và sửa tay.
- Phân tự động ưu tiên tải thấp nhất trên cả học kỳ rồi điểm phù hợp; admin có thể sửa lệch
  khoảng mục tiêu. Preview không ghi phân công; accept mới lưu assignment, audit và Outbox.
- Số buổi hoàn thành không cộng điểm. Lịch sử mỗi nhóm có bằng chứng +3 (tối đa 15);
  mỗi dự án có thành tích hợp lệ trong thời gian phụ trách +3 (tối đa 15).
- Balanced/Random tiếp tục là chế độ chia không dùng AI.

Thuật toán, màn hình và API preview:
[AI_MENTOR_ALLOCATION.md](mentoring/AI_MENTOR_ALLOCATION.md).

Cấu hình server, bảo mật dữ liệu, Docker và kiểm thử:
[GEMINI_MENTOR_VPS.md](deployment/GEMINI_MENTOR_VPS.md).

Quản lý hồ sơ và upload CV:
[MENTOR_PROFILE_MANAGEMENT.md](mentoring/MENTOR_PROFILE_MANAGEMENT.md),
[ADMIN_MENTOR_CV_UPLOAD.md](mentoring/ADMIN_MENTOR_CV_UPLOAD.md).
