# AI phân mentor: chính sách hiện tại

## Luồng xử lý

1. Admin chọn học kỳ và toàn bộ lớp hoặc một phạm vi lớp. Chỉ xét nhóm/lớp đang hoạt động
   và mentor hợp lệ tham gia học kỳ, đúng loại Enterprise hoặc Academic.
2. Giữ vị trí đã có mentor, sửa tay và mentor kế thừa hợp lệ. Chỉ chấm vị trí còn thiếu.
3. Backend gửi tên/mô tả dự án và tóm tắt chuyên môn đã lọc định danh tới Gemini.
   Mỗi loại mentor được chấm bằng các lô tối đa 20 dự án; phản hồi JSON gọn dùng mã bằng chứng ngắn để tránh cắt JSON.
   Giao diện theo dõi tác vụ nền, hiển thị số lô đã chấm và mở preview khi cả hai loại hoàn tất.
   Tác vụ xử lý tối đa 300 dự án cần chấm mỗi loại, trong ngân sách 15 phút.
   Không gửi CV gốc hoặc ghi chú/feedback lịch sử.
4. Backend chuẩn bị các đoạn bằng chứng nguyên văn dài 2–80 ký tự, có mã riêng trong từng trường hồ sơ.
   AI trả tối đa hai ứng viên mỗi dự án trong mỗi loại, cùng năm điểm 0–100 và mã bằng chứng
   cho từng điểm dương. Backend ánh xạ mã về đúng đoạn nguồn, kiểm tra cấu trúc, điểm, nguồn bằng chứng
   và các phủ định rõ trong hồ sơ. Đây là shortlist, không phải ma trận mọi cặp mentor/dự án.
5. Backend tính điểm theo chính sách bên dưới. Khi phân công, ưu tiên mentor đang ít nhóm
   nhất trong loại đó trên cả học kỳ, rồi so điểm phù hợp trong tập cùng tải.
6. Admin xem đề xuất, sửa tay hoặc để trống vị trí. Chỉ admin accept mới lưu phân công,
   audit và Outbox cùng transaction; preview không ghi assignment.

## Công thức 70/15/15

```text
L = (35×KinhNghiem + 25×ChuyenMonTag + 15×LinhVuc + 20×CongNghe + 5×GioiThieuHocVan) / 100
H = 3 × min(5, SoNhomDaHuongDanCoBangChung)
A = 3 × min(5, SoDuAnCoThanhTichHopLe)
DiemCuoi = min(100, round(0.70×L + H + A))
```

- Năm đầu vào của L do AI đánh giá theo ngữ nghĩa. Backend giữ các trọng số và phép tính.
- Lịch sử tối đa 15 điểm; thành tích tối đa 15 điểm. Số buổi hoàn thành không cộng điểm.
- Nhóm lịch sử phải có bằng chứng hướng dẫn hợp lệ trong thời gian mentor được phân công.
- Thành tích là dự án được ghi nhận Potential, Internally Funded, Externally Funded hoặc Awarded
  trong thời gian phụ trách. Mỗi dự án chỉ tính một lần. Dữ liệu kế thừa hoặc chỉ sửa ghi chú
  không tạo thêm thành tích cho mentor mới.
- Không có bằng chứng phù hợp thì điểm hồ sơ và hai bonus đều bằng 0. Vẫn có thể ghép theo
  tải và gắn cờ để admin rà soát. Điểm phục vụ xếp hạng, không phải xác suất thành công.
- Ví dụ hồ sơ 90, hai nhóm hướng dẫn và hai dự án có thành tích: 63 + 6 + 6 = 75 điểm.

## Chia đều theo học kỳ và sửa tay

- Tính tổng N nhóm đang hoạt động trong các lớp đang hoạt động của học kỳ và M mentor hợp lệ
  riêng cho từng loại. Mục tiêu là floor(N/M) hoặc ceil(N/M) nhóm mỗi người.
- Ví dụ 10 nhóm/3 mentor cùng loại: 3–3–4. Tải đã có ở lớp ngoài phạm vi vẫn được tính.
- Chia tự động ưu tiên tải thấp nhất trước điểm phù hợp. Sửa tay, phân công đã có và kế thừa
  được giữ nguyên, kể cả khi làm tải chênh lệch; bản xem trước hiển thị cảnh báo để admin quyết định.
- Response `balance` có tổng nhóm, số mentor, khoảng mục tiêu và khoảng tải sau đề xuất.
- Mentor chưa có tài khoản không nằm trong tập AI tự động; vẫn có thể được admin chọn tay
  theo điều kiện và quyền hiện có.
- Người ít tải nhất có thể nằm ngoài shortlist AI. Backend giữ điểm 0 và yêu cầu rà soát,
  không tự tạo điểm dương để làm đẹp kết quả chia đều.

## Màn hình

Subject Management → Lecturers & Mentors by Semester → AI preview · all classes.
Có thể quản lý theo lớp, xem theo nhóm/mentor, sửa từng vị trí và chấp nhận đề xuất.
Thành tích được cập nhật tại Project Data → tên dự án → Achievements → Save changes.

## Cấu hình và kiểm thử

Chỉ còn một provider AI: Gemini. Cấu hình server-side, quota, dữ liệu gửi và lệnh kiểm thử:
[GEMINI_MENTOR_VPS.md](../deployment/GEMINI_MENTOR_VPS.md).

Giữ nguyên Balanced/Random là các chế độ chia không dùng AI. Các endpoint vẫn kiểm tra
đăng nhập, admin, quyền lớp/nhóm, concurrency và dữ liệu đã thay đổi sau preview.
