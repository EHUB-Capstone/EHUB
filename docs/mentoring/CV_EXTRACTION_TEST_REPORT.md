# Kiểm thử trích xuất CV mentor

Đã bổ sung luồng **mentor upload CV → đọc văn bản → lấy thông tin chuyên môn → lưu hồ sơ → AI gợi ý mentor**. Form cập nhật ngay dữ liệu trả về để mentor xem và chỉnh lại. Không đổi schema hoặc dạng response của API upload.

**Kết quả cuối ngày 14/10/2026: build đạt, 600/600 unit/application đạt, 426/426 integration đạt; 0 lỗi, 0 skipped. Ca BGE-M3 thật đã chạy thành công với luồng Skills nhập thủ công hiện tại.**

## Cập nhật 14/10/2026: Skills do mentor nhập thủ công

Skills tiếp tục dùng trường API `TechnologySkills`. Mentor nhập/sửa qua API cập nhật hồ sơ hiện có; upload CV không thêm, gộp hay ghi đè danh sách này. CV không cần mục Skills. Các mục riêng `Skills`, `Technical skills`, `Technology skills`, `Computer skills`, `Vi tính` và `Kỹ năng` được bỏ qua, kể cả nội dung nằm sau dấu hai chấm. CV chỉ có các mục này không đủ thông tin để trích xuất hồ sơ.

Học vấn/giới thiệu và kinh nghiệm làm việc vẫn được trích như trước, kể cả khi nội dung chuyên môn nhắc tên công nghệ. `Expertise` tiếp tục nhận diện lĩnh vực chuyên môn từ những phần đó; không nhận diện từ mục Skills đã loại bỏ. Không đổi contract hoặc schema.

Các ca kiểm thử mới kiểm tra CV không có Skills, CV chỉ có Skills, nhập/sửa Skills bằng API, validation danh sách và quyền 401/403. Ca tích hợp lưu Skills thủ công, upload CV, thay CV có mục Skills rồi upload file lỗi và đọc lại hồ sơ để kiểm tra dữ liệu được giữ nguyên.

### Kết quả cuối sau khi sửa test — 14/10/2026

Backend chạy từ `backend/`, frontend từ `frontend/`. Integration dùng Docker local và PostgreSQL/pgvector tạm của Testcontainers.

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build EHub.slnx` | Đạt; 5 warning hiện có, 0 error |
| `dotnet test EHub.slnx --filter "FullyQualifiedName!~IntegrationTests"` | **459 application + 141 unit đạt** |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj` với cấu hình opt-in Ollama bên dưới | **426/426 đạt, 0 lỗi, 0 skipped**, khoảng 3 phút 14 giây |
| Test riêng `AppDbContext_WakesOutboxOnlyAfterTheContainingCommit` | **1/1 đạt** trước lượt full integration cuối |
| `npm run lint` | Đạt; 74 warning hiện có, 0 error |
| `npm run type-check` | Đạt |
| `npm test` | **408/408 đạt** |
| `npm run build` | Đạt; 0 warning |
| `git diff --check` | Đạt |

Ba lỗi test đã được sửa mà không thay đổi nghiệp vụ để làm test xanh:

- `DirectUploadComplete_NotifiesAssignedLecturerForNewAndResubmittedVersions`: giới hạn truy vấn Outbox theo class của fixture, đồng thời kiểm tra team/checkpoint/submission trong payload khớp hai lần upload. Test không còn đếm event của ca khác dùng chung database.
- `CheckpointSchedule_QueuesStudentNoticeAndOnlyCurrentDeadlineReminder`: cập nhật expectation thành `/student/workspace/{teamId}` theo deep link hiện tại; giữ kiểm tra loại notification và danh sách người nhận.
- `AppDbContext_WakesOutboxOnlyAfterTheContainingCommit`: đăng ký mapping `UseVector()` cho DbContext tự tạo và lấy connection từ `factory.TestConnectionString`. Không lấy lại từ `Database.GetConnectionString()` sau khi connection đã mở, vì provider có thể loại bỏ thông tin xác thực khỏi chuỗi trả về. Giữ đầy đủ assertion wake sau lưu trực tiếp, chưa wake trước commit và wake sau commit.

TRX lượt full cuối ghi `LiveBgeM3ReceivesExtractedExperienceAndStoresVector` là **Passed**. Ca này upload CV không có mục Skills, gọi gợi ý bằng Ollama `bge-m3` thật và xác nhận vector 1.024 chiều được lưu PostgreSQL. Storage tài liệu vẫn dùng bộ nhớ trong test; đây không phải kiểm thử Cloudinary thật. Các số liệu điểm xếp hạng trong phần ngày 13/10 bên dưới là kết quả lịch sử, không phải điểm đo lại của phiên bản hiện tại.

Kiểm tra trình duyệt với mock API cũng đạt: nhập Skills chưa lưu → upload CV không có Skills → lưu → màn hình xem giữ cả CV và Skills → mở sửa nạp đúng Skills. Danh sách trùng báo validation và giữ form; Cancel khôi phục dữ liệu đã lưu; xóa hết Skills lưu thành danh sách rỗng.

### Chạy đầy đủ ca BGE-M3 thật

Cần Docker local sẵn sàng cho Testcontainers và Ollama local đã chạy tại `127.0.0.1:11434`, có model `bge-m3`. Lần kiểm thử này dùng runtime/model cached sẵn; không tải model mới. Warmup bằng câu hoàn toàn giả xác nhận 1 vector × 1.024 chiều trước khi chạy suite.

Chạy trong một cửa sổ PowerShell mới từ `backend/`; các biến dưới đây chỉ áp dụng cho tiến trình đó, không ghi cấu hình hệ thống hoặc secret:

```powershell
$env:EHUB_TEST_REAL_OLLAMA = '1'
$env:Mentoring__OllamaBaseUrl = 'http://127.0.0.1:11434'
$env:Mentoring__EmbeddingModel = 'bge-m3'
dotnet build EHub.slnx
dotnet test EHub.slnx --filter "FullyQualifiedName!~IntegrationTests"
dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj
```

`RealOllamaFact` vẫn chủ đích yêu cầu opt-in `EHUB_TEST_REAL_OLLAMA=1`; không bỏ test hoặc thay lời gọi model thật bằng mock. Chạy lệnh mặc định khi chưa bật opt-in vẫn skip ca cần Ollama. Lượt full cuối ở trên đã bật opt-in và thực thi đủ 426 ca.

### Lịch sử ngày 14/10/2026 trước khi sửa ba test

| Lệnh | Kết quả trước sửa |
| --- | --- |
| `dotnet build EHub.slnx` | Đạt; lần chạy đó 0 warning, 0 error |
| `dotnet test EHub.slnx --filter "FullyQualifiedName!~IntegrationTests"` | **459 application + 141 unit đạt** |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj` chưa bật opt-in Ollama | **422 đạt, 3 lỗi, 1 skipped / 426**, khoảng 4 phút 36 giây |

Ở lượt lịch sử này, ba lỗi là truy vấn Outbox chung, expectation link cũ và thiếu mapping pgvector của DbContext test; ca model thật chưa được bật. Các vấn đề này đã được xử lý và chạy lại thành công trong kết quả cuối bên trên.

## Kết quả minh họa ngày 13/10/2026 (trước cập nhật Skills)

Dùng CV hoàn toàn giả có bố cục phiếu nhân viên: thông tin cá nhân, người thân, quá trình đào tạo, kinh nghiệm làm việc và kỹ năng. Không sử dụng thông tin cá nhân từ mẫu người dùng cung cấp.

| Bước | Kết quả |
| --- | --- |
| Mentor upload `cv.docx` | HTTP 200 |
| Học vấn vào phần giới thiệu | `Đại học Demo - Tài chính` |
| Kinh nghiệm vào hồ sơ | `Demo Growth - Giám đốc Marketing`, `Chiến lược marketing và điều phối dự án` |
| Chuyên môn nhận diện | Marketing, Finance, Project management, Strategy; giữ chuyên môn thủ công hiện có |
| Công nghệ nhận diện ở phiên bản cũ | Python, SQL; cập nhật 14/10 đã bỏ hành vi này |
| Thông tin cá nhân/người thân/chữ ký | Các mục này không xuất hiện trong văn bản gửi tới dịch vụ embedding ở ca test |
| Staff yêu cầu gợi ý cho dự án marketing/finance | HTTP 200 |
| Model thật | Ollama `bge-m3`, điểm xếp hạng **66/100**, vector **1.024 chiều** đã lưu PostgreSQL |
| Thời gian ca test model thật | Khoảng 6 giây cho toàn bộ ca test; không phải phép đo riêng thời gian API |
| Chưa đăng nhập / sai role upload | HTTP 401 / 403 |
| Mentor khác tải CV | HTTP 403 |
| CV lỗi | HTTP 400; giữ file CV và kinh nghiệm đã lưu trước đó |

Điểm 66 là điểm xếp hạng trong dữ liệu thử này, không phải xác suất hoặc đánh giá chất lượng CV. Ca model thật dùng PostgreSQL/pgvector tạm của Testcontainers và BGE-M3 local; **file storage được thay bằng bộ nhớ**, chưa kiểm tra upload thật lên Cloudinary. Một ca khác dùng HTTP double của Ollama để kiểm tra chính xác nội dung được gửi.

## Kiểm tra đã chạy ngày 13/10/2026

Chạy backend từ `backend/`, frontend từ `frontend/`.

| Lệnh | Kết quả |
| --- | --- |
| `dotnet restore EHub.slnx -m:1 -nr:false` | Đạt, tải PdfPig 0.1.16 |
| `dotnet build EHub.slnx --no-restore -m:1 -nr:false` | Đạt; warning nullable/Testcontainers ở các file hiện có |
| `dotnet test EHub.slnx --no-restore -m:1 -nr:false --filter "FullyQualifiedName!~IntegrationTests"` | **448 application + 137 unit** đạt, gồm 11 ca trích xuất CV |
| `$env:EHUB_TEST_REAL_OLLAMA='1'; dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~Mentoring"` | **15/15 đạt**, có ca gọi BGE-M3 thật |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build` | **384 đạt, 5 lỗi, 1 skipped**; ca model thật mặc định skipped, đã chạy riêng thành công ở trên |
| `npm run lint -- src tests --ignore-pattern node_modules --ignore-pattern dist` | Đạt, còn warning hiện có |
| `npm run type-check` | Đạt |
| `npm test` | **375/375 đạt** |
| `npm run build` | Đạt |
| `git diff --check` | Đạt |

Lệnh build mặc định chạy song song và một lần chạy test trong sandbox không hoàn thành; đã chạy lại bằng MSBuild một worker và quyền chạy test local thích hợp. Kết quả trong bảng lấy từ những lần chạy hoàn tất. Lint được giới hạn vào source/test vì lệnh mặc định quét cả `node_modules`/`dist` trong môi trường này.

Năm lỗi integration chung nằm ở các ca sau:

- `AdminCanCreateUnassignedClassThenAssignItToLecturer`: bước gán giảng viên không thành công.
- `BulkCreate_AssignsDifferentLecturersToExplicitClassIndices`: nhận 409 thay vì 200.
- `AdminCreatedAssignedClass_StaysDraftUntilAssignedLecturerAddsSchedule`: tạo lớp không thành công.
- `DirectUploadComplete_NotifiesAssignedLecturerForNewAndResubmittedVersions`: truy vấn Outbox chung nhận 10 event thay vì 2; ca này đạt khi chạy tách riêng.
- `AppDbContext_WakesOutboxOnlyAfterTheContainingCommit`: DbContext riêng của test thiếu cấu hình mapping pgvector.

Đã chạy tách riêng năm ca, không chạy các test CV mới: **4 lỗi vẫn tái hiện, 1 đạt**. Chưa sửa các ca lớp học/Outbox ngoài phạm vi CV.

## Phạm vi hỗ trợ

- PDF có văn bản và DOCX được đọc trực tiếp. Test có PDF thật và DOCX tiếng Việt dạng bảng/text run.
- DOC cũ được chuyển qua LibreOffice. Máy test và image backend local cũ chưa có LibreOffice, nên đường chuyển DOC được kiểm tra bằng converter double; chưa chạy chuyển DOC thật ở máy này. Dockerfile backend hiện tại đã khai báo LibreOffice; backend chạy ngoài Docker cần cài/cấu hình executable.
- PDF scan/ảnh cần OCR trước; chưa bổ sung OCR. File mã hóa, lỗi cấu trúc, quá 40 trang PDF hoặc quá 60.000 ký tự bị từ chối. DOCX giới hạn XML giải nén 2 MiB và cấm DTD/liên kết XML ngoài.
- Trích xuất theo tiêu đề mục Việt/Anh và nhận diện chuyên môn phổ biến theo từ khóa, không dùng mô hình sinh văn bản để đoán số năm/công ty/loại mentor. Cần tiêu đề mục rõ ràng. Nội dung học vấn/giới thiệu được lưu trong giới thiệu, quá trình làm việc trong kinh nghiệm; giữ giới hạn 2.000/4.000 ký tự hiện có. Mục Skills riêng bị bỏ qua.
- Loại mentor, tổ chức, liên kết, Skills (`TechnologySkills`), tag thủ công và các experience entry hiện có được giữ. Chỉ danh sách chuyên môn (`Expertise`) được gộp; phần giới thiệu/kinh nghiệm được thay khi CV có mục tương ứng. Mentor cần kiểm tra lại kết quả trên form.
- Khi staff gọi gợi ý, BGE-M3 dùng metadata đã lưu từ CV. Cache nhận diện thay đổi nội dung bằng hash như trước. Không cần gọi AI trong request upload.

Các báo cáo TRX nằm trong `backend/tests/*/TestResults/`: `cv-regression.trx`, `mentor-cv-integration.trx`, `cv-full-regression.trx`, `cv-unrelated-isolated.trx`. Log frontend ở `frontend/TestResults/`. Đây là output kiểm thử được Git ignore.
