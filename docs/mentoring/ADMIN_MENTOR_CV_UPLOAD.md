# Admin thêm CV cho mentor

## Thao tác

1. Vào **User Management → Import → Import Mentors** để nhập danh sách mentor từ Excel như hiện tại. Mentor phải có tài khoản trước khi nhận CV; hoàn tất email của các hồ sơ ở **Needs information** nếu cần.
   File chỉ có mentor doanh nghiệp vẫn giữ sheet `Mentor IT_FA26` với hàng tiêu đề và không có người. Tương tự, có thể chỉ nhập mentor IT. Cả hai sheet phải đúng tiêu đề, và toàn bộ file phải có ít nhất một mentor. Có thể chỉ dùng cột STT, họ tên và email để giữ các trường tùy chọn đang có khi cập nhật tài khoản.
2. Chọn nút riêng **Upload mentor CVs** trong User Management.
3. Chọn một hoặc nhiều file CV, hoặc kéo thả nhiều file vào danh sách. Hỗ trợ toàn bộ tài liệu PDF, Word DOC và DOCX; tối đa 10 MB mỗi file, 50 file và 100 MB cho mỗi danh sách.
4. Hệ thống đọc email bên trong mỗi CV và đối chiếu với email tài khoản mentor đã import. Nếu chỉ có một hồ sơ khớp, dòng đó tự chọn mentor và hiện **Matched by email from CV** cùng email đã đọc. Kiểm tra từng dòng trước khi lưu. Tên file không được dùng để tự chọn mentor.
   - Không tìm thấy email, email chưa có mentor tương ứng hoặc email trỏ đến nhiều mentor: để trống và yêu cầu chọn thủ công bằng tên/email.
   - Đối chiếu toàn bộ email, không phân biệt hoa/thường. Không bỏ dấu chấm hoặc phần `+alias` của Gmail; không ghép chỉ dựa trên domain hoặc tên.
   - Nếu đọc file lỗi, có thể **Read emails again** hoặc chọn mentor thủ công. Trong lúc đọc, thao tác upload và sửa hàng bị khóa để phản hồi cũ không ghi đè lựa chọn của admin.
5. Nếu đã có CV, đánh dấu xác nhận thay thế ở dòng tương ứng. Chỉ một CV được gắn cho mỗi mentor trong một danh sách.
6. Bấm **Upload N CVs** để lưu. Danh sách báo kết quả từng file; thành công được giữ nguyên nếu file khác lỗi. Sửa hoặc bỏ file lỗi, rồi tải lại phần chưa thành công. Nút **Refresh mentor list** cập nhật CV hiện tại trước khi xác nhận thay thế lại.

Chọn file sẽ gửi CV đến API đọc email để xem trước, chưa lưu tài liệu hoặc thay đổi hồ sơ. Chỉ nút **Upload** lưu CV. CV không tạo tài khoản hoặc tự đổi loại mentor. Không hỗ trợ nhập ZIP; chọn trực tiếp các file CV bên trong thư mục.

## Dữ liệu và API

Toàn bộ tài liệu được lưu qua dịch vụ lưu trữ CV hiện có. Các mục học vấn/giới thiệu/kinh nghiệm chuyên môn được trích vào Bio/Experience; chuyên môn được bổ sung vào Expertise theo cơ chế đang dùng. Loại mentor và kỹ năng công nghệ nhập tay được giữ lại. Nội dung cá nhân, liên hệ và chữ ký không được đưa vào phần chuyên môn dùng để đối chiếu. Đây là cập nhật hồ sơ đầu vào cho lần phân công AI tiếp theo; không huấn luyện lại trọng số model khi upload.

API mỗi file: `POST /api/mentoring/profiles/{mentorId}/documents/cv`, multipart `file` và `replaceExisting` (mặc định `false`), trả `ApiResponse<MentorProfileResponse>`. Chỉ admin được gọi; target được tra theo hồ sơ đã có tài khoản. CV cũ cần xác nhận rõ ràng. Backend kiểm tra kích thước thực đọc, định dạng/signature và khả năng trích nội dung trước khi thay đổi dữ liệu. Xung đột `xmin` trả 409, giữ file cũ và dọn file mới chưa lưu thành công. File gốc tiếp tục tải xuống bằng endpoint có kiểm tra quyền hiện tại.

API xem trước: `POST /api/mentoring/profiles/cv-match`, multipart `file`, chỉ admin. Trả `ApiResponse<MentorCvMatchResponse>` với `status` (`Matched`, `NoEmail`, `NoMatch`, `Ambiguous`), `mentorProfileId` (chỉ có khi khớp duy nhất) và `detectedEmails` (tối đa 20). Đọc Word/PDF bằng reader có giới hạn dung lượng, signature và nội dung hiện có. Email lấy từ văn bản gốc, kể cả phần liên hệ bị bộ trích chuyên môn loại bỏ. Query chỉ đọc, join với `User.Email`, không lưu CV, không tạo tài khoản, không đổi hồ sơ hoặc gọi storage. Trên 20 email khác nhau không tự ghép từ danh sách đã cắt ngắn. Không trả văn bản CV, đường dẫn lưu trữ hoặc raw exception; không log nội dung liên hệ.

Luồng nhiều file gửi tuần tự để giới hạn tài nguyên và hiển thị lỗi riêng từng file. Không có transaction chung bao phủ cả danh sách. Kết quả trả về không chứa đường dẫn storage hoặc raw exception. Request bị hủy khi component unmount; file đã thành công không gửi lại trong lần retry. Sau một lượt tải, danh sách mentor được làm mới để phát hiện trường hợp phản hồi timeout nhưng server đã lưu.

Không đổi schema database hoặc tạo migration. Luồng import Excel và mentor tự upload CV giữ hành vi hiện tại.

## Kiểm thử

- `AdminMentorCvUploadTests`: quyền, target có tài khoản, chỉ cập nhật đúng mentor, xác nhận thay thế, dữ liệu thực vượt giới hạn, định dạng không hợp lệ và dọn file mới khi save lỗi.
- `AdminMentorCvUploadIntegrationTests`: API thật/PostgreSQL, 401/403/404, multipart/file validation, trích dữ liệu chuyên môn, giữ kỹ năng nhập tay/loại mentor, tải nguyên file gốc, thay thế rõ ràng và xung đột `xmin`.
- `AdminMentorCvMatchTests`: email chính xác không phân biệt hoa/thường, không suy từ tên file/Gmail alias, không email/không khớp/nhiều mentor, giới hạn email, quyền, validation, hủy request và chỉ đọc.
- `AdminMentorCvMatchIntegrationTests`: API thật/PostgreSQL và DOCX có phần liên hệ; kiểm tra kết quả khớp, 401/403, validation và không đổi hồ sơ/tài khoản/Outbox hoặc gọi storage.
- `mentorCvUpload.test.ts`: giới hạn file/danh sách, không suy từ tên file, chọn từ kết quả email duy nhất, không trùng target, chặn upload khi đang đọc, xử lý không khớp/lỗi/mentor đã mất, giữ hàng lỗi và retry bỏ qua thành công.
- `adminMentorCvUpload.test.ts`: đồng bộ mock về quyền, email preview, target và thay thế; mock mô phỏng trích xuất bằng văn bản fixture, không thay cho kiểm tra nội dung Word/PDF ở backend.

Chỉ dùng tài liệu giả lập để kiểm thử. Dữ liệu CV thật và danh sách liên hệ không được đưa vào test fixture hoặc commit.

## Kết quả ngày 30/10/2026

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build EHub.slnx --no-restore` (từ `backend/`) | Thành công, 0 lỗi; còn cảnh báo dependency sẵn có. |
| `dotnet test EHub.slnx --no-build --no-restore --filter "FullyQualifiedName!~IntegrationTests"` | 141 UnitTests và 721 ApplicationTests đạt. |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build --no-restore` | 517 đạt, 2 test model cục bộ bỏ qua, không thất bại. |
| `npm run lint` (từ `frontend/`) | Exit 0; còn cảnh báo cũ ngoài phạm vi. |
| `npm run type-check` | Đạt. |
| `npm test` | 506 đạt, không thất bại hoặc bỏ qua. |
| `npm run build` | Thành công sau thay đổi cuối cùng. |
| `git diff --check` | Sạch. |

Thử UI bổ sung bằng API thật và PostgreSQL riêng, lưu tài liệu trong bộ nhớ của demo: chọn năm DOCX giả lập. File `CV Mentor M002.docx` chứa email viết hoa của M001 tự chọn M001; file tên bất kỳ chứa email M002 tự chọn M002. CV có hai email mentor, email chưa import và không có email đều để trống, hiện lý do và chặn upload. Chọn thủ công M003 cho CV nhiều email, bỏ hai file chưa có target và upload ba CV thành công. Sau reload, cả ba mentor vẫn có đúng tên CV và cần xác nhận trước khi thay thế. Bằng chứng ở `artifacts/mentor-ui-demo/cv-email-match/` đã được Git ignore. Các kiểm tra upload/thay thế/partial success trước đó ở `artifacts/mentor-ui-demo/cv-upload/` vẫn được lưu.
