# Nâng cấp chất lượng ghép mentor

Giữ model/provider hiện có, công thức 70/15/15 và bước admin chốt. Không thêm model embedding,
dịch vụ ngoài, dependency solver hoặc schema/migration trong lần nâng cấp này.

## Dữ liệu hồ sơ và nhu cầu dự án

- Bộ đọc CV giữ nội dung vai trò/dự án chuyên môn, chuẩn hóa công nghệ và lĩnh vực được nêu rõ
  bằng catalog cục bộ. Đây là trích xuất bảo thủ; khả năng đánh giá ngữ nghĩa nằm ở model hiện có.
  Không suy kinh nghiệm thực hành từ bằng cấp hoặc câu phủ định.
- Upload bổ sung vào các field hiện có, giữ nội dung nhập tay, chống trùng và giữ các giới hạn hiện có.
  `Experiences.Notes` lưu đoạn trích chuyên môn `CV: …`; dữ kiện giữ lại có thể đến từ CV trước đó,
  không phải cam kết tất cả thuộc file CV mới nhất. Tối đa 20 nhãn mỗi field và 40 hàng kinh nghiệm.
- CV cũ cần được upload lại để tự bổ sung các mảng công nghệ/lĩnh vực. Không tự tải lại tài liệu
  đã lưu hoặc gửi CV gốc ra API. Hồ sơ nhập tay hiện có vẫn dùng được.
- Trước khi gửi model, E-HUB lọc định danh và chọn đoạn liên quan tới các dự án trong lô thay vì
  luôn lấy đầu hồ sơ. Bộ chọn đoạn dùng từ khóa/ngữ cảnh nguồn để tiết kiệm input, không tính điểm fit.
  Nó không phải tìm kiếm embedding và có thể bỏ sót cách diễn đạt đồng nghĩa hoặc khác ngôn ngữ.
- Nguồn chuyên môn được xử lý trong giới hạn 20.000 ký tự mỗi field, sau đó rút gọn theo ngân sách
  kinh nghiệm 1.200, chuyên môn 450, lĩnh vực 250, công nghệ 350, giới thiệu 250 ký tự. Dự án tối đa
  2.400 ký tự. Masking không bảo đảm ẩn danh; không coi đây là đọc đầy đủ mọi CV hoặc mọi mô tả dài.
- Model được yêu cầu phân tích mục tiêu, giai đoạn, lĩnh vực và hỗ trợ cần thiết, chỉ theo văn bản đã cung cấp.
  Backend tạo `needOptions` bằng các đoạn nguồn đã kiểm tra phủ định, gắn mã `N01`… riêng trong từng dự án.
  Response `needs` chỉ chọn tối đa bốn mã đó; backend tra lại nội dung gốc để trả giao diện. Model không phải
  chép lại câu tiếng Việt đúng từng ký tự. Mã không thuộc dự án bị từ chối; câu trích nguyên văn của phiên bản
  cũ vẫn đọc được nếu qua kiểm tra nguồn/phủ định. Không tạo giai đoạn hoặc yêu cầu thiếu dữ liệu.
  Nếu các lựa chọn giải thích làm payload vượt 240.000 ký tự, backend giảm số lựa chọn, giữ nguyên mã
  còn lại và dữ liệu dự án/hồ sơ dùng chấm điểm. Nếu vẫn vượt giới hạn thì dừng trước khi gọi API.
- Tối đa năm ứng viên có bằng chứng cho mỗi dự án/mỗi loại. Năm tiêu chí vẫn cần điểm nguyên
  0–100 và mã đoạn nguồn thuộc đúng mentor/field. Backend kiểm tra cả đoạn đã chọn và nguồn đầy đủ
  trong giới hạn xử lý, để việc rút gọn không biến câu phủ định thành bằng chứng dương.
- Không có bằng chứng không tự sinh điểm. Mentor ngoài shortlist vẫn có thể được ghép theo chính sách
  chia hết và mang điểm 0/cờ rà soát. Việc không được shortlist không chứng minh mentor không có năng lực.

## Tối ưu phân công có ràng buộc

Mỗi loại được giải riêng. Chỉ các vị trí còn trống trong phạm vi đã chọn được tối ưu; tải của các lớp
khác cùng học kỳ vẫn được tính. Phân công hiện có, kế thừa, sửa tay và các vị trí admin để trống được khóa.

1. Tính mức tải hiện có của từng mentor hợp lệ có tài khoản. Chọn phân bố tải cân bằng nhất khả thi
   bằng water-filling. Khi không có phân công được bảo toàn gây lệch, mỗi người có floor(N/M) hoặc ceil(N/M) nhóm.
2. Với mức nước L, mentor i cần ít nhất `max(0, L - tải_i)` nhóm mới; phần dư cho phép tăng thêm một
   ở các mentor đang dưới hoặc bằng L. Phân công quá tải đã có không bị chuyển đi để ép cân bằng.
3. Dùng min-cost flow để tối đa tổng `RankingValue` chưa làm tròn trong các hạn ngạch đó. Chi phí cặp
   là `100 - RankingValue`; cặp chưa có bằng chứng có điểm 0. Seed chỉ xử lý các phương án hòa.
4. Sắp xếp chuẩn theo ID trước khi xử lý hòa để cùng input/seed không phụ thuộc thứ tự query.
   Solver có cancellation token và không gọi model hoặc ghi database.

Mục tiêu theo thứ tự là cân tải, rồi tối đa tổng điểm. Đây là tối ưu trên dữ liệu/các điểm đã được model
cung cấp; không phải chứng minh phương án tốt nhất trong thực tế. Một trường hợp 2 nhóm/2 mentor
trong test có điểm `[[90,85],[89,20]]`: ghép lần lượt có thể được 110, tối ưu được 174 và mỗi người vẫn một nhóm.
Test dùng thêm vét cạn ở các bài toán nhỏ và kiểm tra 300 dự án/60 mentor thuộc hai loại.

## Điểm, giải thích và quyền chốt

```text
L = (35×KinhNghiem + 25×ChuyenMon + 15×LinhVuc + 20×CongNghe + 5×GioiThieu) / 100
H = 3 × min(5, SoNhomDaHuongDanCoBangChung)
A = 3 × min(5, SoDuAnCoThanhTichHopLe)
DiemCuoi = min(100, round(0.70×L + H + A))
```

Giữ chính sách thành tích/lịch sử hiện có, không cộng số buổi hoàn thành. Điểm dùng để xếp hạng,
không phải xác suất thành công. Khi có phủ định rõ về chuyên môn dự án yêu cầu, điểm và các tiêu chí
được bác bỏ; không hiển thị đoạn nguồn đó như bằng chứng phù hợp đã được xác nhận.

Response mỗi hàng có thêm `fitDetails` và `alternatives` (mặc định null/[] để đọc được preview cũ).
`availableProfileFields` phản ánh có thông tin nguồn, kể cả thông tin không liên quan hoặc phủ định;
không đồng nhất thiếu dữ liệu với điểm phù hợp thấp. Đoạn nguồn hiển thị nguyên văn, React escape mặc định.
Tên provider/model bị ẩn trong lời giải thích hệ thống, không sửa nội dung chứng cứ nghề nghiệp.

Ứng viên thay thế tối đa ba người có bằng chứng, cùng loại và còn hợp lệ trong pool của preview.
Đổi lựa chọn chỉ thay state trong editor. Dự kiến tải được tính từ tổng tải sau preview hiện tại;
Apply tạo preview mới và kiểm tra lại trên server. Confirm mới lưu assignment, audit và Outbox cùng
transaction. Ownership admin, quyền tài nguyên, stale preview và xmin tiếp tục áp dụng.

Output quá dài (`MAX_TOKENS`) được thử model dự phòng rồi chia đôi lô trong deadline tổng, tới tối thiểu
một dự án. Không chấp nhận JSON thiếu, không tạo preview một phần; tiến độ tính theo lô gốc đã hoàn tất.
Khi các model dự phòng đều trả `InvalidResponse`, mỗi lô gốc được chia bổ sung tối đa một lần để thử với
ít dự án hơn. Ngân sách này dùng chung cho cả các nhánh chia do output quá dài; lỗi có hệ thống vẫn dừng.
Các lô con thành công được cache như lô thường. Lỗi xác thực/quyền, quota chung và hủy request không kích
hoạt cơ chế chia do kết quả sai. Việc phục hồi không bỏ kiểm tra ID, điểm hoặc bằng chứng.
Giới hạn request, cache, quota chung/model và retry an toàn vẫn theo tài liệu deploy.

## Đánh giá chất lượng độc lập

Chạy hoàn toàn offline bằng JSON preview cũ/mới và nhãn mentor/giảng viên đã rà soát:

```powershell
python -B scripts/mentoring/evaluate-matching.py --baseline baseline.json --candidate candidate.json --labels labels.json --output report.json
python -B -m unittest discover -s scripts/mentoring -p test_evaluate_matching.py
```

File nhãn có `semesterId` và `labels`, mỗi nhãn chứa `teamId`, `mentorType` và `acceptableMentorIds`.
Tool nhận cả preview JSON, ApiResponse và job Ready; kiểm tra cùng kỳ, slot, lớp và loại mentor.
Nhãn trùng/sai pool hoặc scope không khớp bị từ chối. Không có nhãn thì không phải benchmark ngữ nghĩa.

Báo cáo gồm tỷ lệ ghép nằm trong tập mentor được người đánh giá chấp nhận, số vị trí thiếu/rà soát,
độ phủ dữ liệu/bằng chứng đã biết, tải theo loại và số thay đổi. So sánh AI dùng chung các slot Allocated
được gán nhãn ở cả hai bản; thủ công/kế thừa/đang có được đếm riêng. Điểm fit trung bình chỉ là mô tả,
không phải bằng chứng chất lượng tăng. Preview cũ thiếu metadata được ghi unknown.

Lý do admin sửa mentor được giữ trong luồng preview/audit hiện có và chỉ đếm trong báo cáo này.
Tool không tự huấn luyện Gemini, không gửi dữ liệu hoặc nội dung lý do ra ngoài. Khi dùng dữ liệu thật,
đánh giá nhãn độc lập với phương án AI và kiểm soát quyền đối với file export.

Ví dụ trong `scripts/mentoring/examples/matching-*.json` đều giả lập, chỉ minh họa tool. Không dùng
độ cải thiện của ví dụ hoặc test thuật toán để tuyên bố chất lượng ghép thực tế. Cần nhãn thật được rà soát
và đánh giá lặp lại trước khi khẳng định hiệu quả.

## Rollout và kiểm tra

Build/deploy backend và frontend cùng phiên bản. Không cần migration, key mới hoặc model mới cho nâng cấp này.
Nếu muốn bổ sung dữ liệu từ CV đã upload trước đó, admin upload lại rồi kiểm tra các field và nguồn trích.
Không tự apply bất kỳ migration nào trên staging/production trong bước kiểm thử local.

Chạy build/full backend tests với Docker PostgreSQL tạm, lint/type-check/full tests/build frontend,
Python evaluator tests và kiểm tra UI bằng fixture giả lập. Kiểm thử transport/schema/optimizer không thay thế
đánh giá chất lượng thực tế của API model và dữ liệu trường đang sử dụng.
