# Module 4 — Mentor matching và hỗ trợ nhóm dự án

## Phạm vi triển khai

- Một hồ sơ mentor tồn tại xuyên học kỳ. Loại mentor do admin quản lý (`Enterprise`/`Academic`, hiển thị tương ứng `Business`/`IT` trong hồ sơ hỗ trợ). Mentor tự cập nhật chuyên môn, Skills, kinh nghiệm, giới thiệu, tổ chức, LinkedIn và portfolio URL. Skills dùng trường `technologySkills` hiện có, nhập/sửa thủ công; được phép để trống, tối đa 20 kỹ năng khác nhau, mỗi kỹ năng tối đa 80 ký tự.
- Mentor tải CV hoặc portfolio dạng PDF/DOC/DOCX (tối đa 10 MB). CV được đọc ngay khi upload: học vấn, ngoại ngữ, thành tích, nghiên cứu và kỹ năng chuyên môn đi vào phần giới thiệu; quá trình làm việc đi vào kinh nghiệm. Chuyên môn nhận diện được từ các phần này được bổ sung vào danh sách hiện có. Nội dung `Skills`, `Technical skills`, `Technology skills`, `Professional skills` hoặc `Kỹ năng` được giữ dưới dạng văn bản để matching; không tự điền hay ghi đè danh sách `technologySkills`. Kỹ năng đã lưu và kỹ năng đang nhập dở trên form được giữ nguyên khi upload CV/portfolio. Metadata tài liệu và chuyên môn mới được lưu cùng lần cập nhật hồ sơ; mentor xem và chỉnh lại trên form ngay sau khi upload. Portfolio chỉ được lưu như trước. Backend kiểm tra phần mở rộng và chữ ký/nội dung cấu trúc file trước khi lưu; chỉ mentor sở hữu, admin và giảng viên có phạm vi học kỳ phù hợp được tải xuống. URL lưu trữ không xuất hiện trong DTO.
- Admin và giảng viên xem danh mục mentor cùng số nhóm đang phụ trách, tổng lần phân công, buổi mentoring và điểm phản hồi trung bình. Danh mục của giảng viên bị giới hạn theo học kỳ/lớp phụ trách.
- Gợi ý mentor cho nhóm theo mô tả dự án, công nghệ, tag, chuyên môn và kinh nghiệm. API chỉ xét mentor có mặt trong danh sách nhân sự học kỳ, đang hoạt động và còn vị trí mentor theo loại trên nhóm. Điểm 0–100 và lý do được trả về để giảng viên chọn; API gợi ý không tự phân công.
- Admin, giảng viên phụ trách hoặc mentor được phân công tạo, sửa, hoàn thành, hủy buổi mentoring và ghi action item. Sinh viên thuộc nhóm được xem buổi và gửi đánh giá 1–5 cùng nhận xét sau khi buổi kết thúc. Admin/giảng viên/mentor được xem đánh giá.
- Phân công mentor tiếp tục dùng luồng transaction, audit và Outbox đã có trong `MentorAssignmentHandler`.

## Quy tắc matching hiện tại

Ngoài provider mặc định bên dưới, có chế độ thử nghiệm `Mentoring:MatchingProvider=Trained`: model xếp hạng nhỏ được train trên PC và chạy CPU trong backend, không gọi Ollama. Bộ train, bootstrap model, giới hạn chất lượng và cách bật/khôi phục nằm trong [tools/mentor-matching/README.md](../tools/mentor-matching/README.md). Model đi kèm chỉ học dữ liệu giả lập, chưa được xác nhận ghép tốt hơn hệ thống hiện tại. Cả hai provider đều loại vị trí đã có mentor thật hoặc mentor tạm đang được phân công; không đổi quyền phân công hoặc quy tắc số nhóm.

Đầu vào gợi ý hiện chỉ gồm tên đề tài và mô tả dự án; không yêu cầu trường nhu cầu Business/IT riêng. Provider Trained v2 loại bằng chứng bị phủ định, không suy tag chuyên môn từ học vấn đơn thuần, lọc hồ sơ thiếu chuyên môn khớp và ghi rõ phần khớp một phần. Không đủ bằng chứng có thể trả danh sách rỗng; người phụ trách vẫn chọn/phân công qua luồng hiện có. Model v1 riêng phải train lại bằng schema v2 trước khi sử dụng.

Matching dùng mô hình embedding đa ngôn ngữ BGE-M3 chạy trong Ollama nội bộ. Mô tả dự án và hồ sơ chuyên môn mentor được mã hóa thành vector 1024 chiều; hệ thống tính cosine similarity và kết hợp điểm khớp kỹ năng cụ thể (70% ngữ nghĩa, 30% kỹ năng; tối đa phần kỹ năng khi khớp hai kỹ năng khác nhau). Các trọng số này là giá trị khởi đầu, cần hiệu chỉnh bằng đánh giá của giảng viên. Đây là **điểm xếp hạng**, không phải xác suất thành công hay tỷ lệ phù hợp đã được hiệu chuẩn. Loại mentor đã có phân công đang hoạt động trên nhóm bị loại khỏi gợi ý. Không còn giới hạn MaxTeams; số nhóm đang phụ trách được hiển thị để tham khảo. Quyền phân công vẫn thuộc giảng viên/bộ môn.

Nhóm cần có mô tả dự án, công nghệ, lĩnh vực hoặc tag trước khi nhận gợi ý; chỉ tên nhóm không đủ dữ liệu để xếp hạng.

Vector hồ sơ mentor được lưu trong PostgreSQL `mentor_embeddings` bằng pgvector. Hash của nội dung và tên mô hình giúp tạo lại vector khi mentor sửa hồ sơ hoặc đổi mô hình. Vector dự án được tạo khi yêu cầu gợi ý. Cosine được tính chính xác trong backend trên các mentor đã lọc, chưa dùng chỉ mục HNSW. Văn bản được giới hạn 6.000 ký tự, ưu tiên thông tin chuyên môn/công nghệ; mô hình được gọi theo batch 4 hồ sơ. Backend giới hạn tổng thời gian gợi ý 45 giây; cache đã ghi thành công được tái sử dụng khi người dùng thử lại.

CV được trích văn bản tại backend, không gửi file gốc đến AI. Bộ phân tích theo tiêu đề Việt/Anh lấy các mục chuyên môn; bỏ mục thông tin cá nhân, người thân, sở thích và chữ ký, đồng thời loại email/điện thoại/URL trong văn bản chuyên môn. Các tag phổ biến được nhận diện theo từ khóa; không dùng mô hình sinh văn bản để suy đoán công ty, số năm kinh nghiệm hoặc loại mentor. Loại mentor, chuyên môn, kinh nghiệm và giới thiệu đã lưu được gửi tới Ollama nội bộ khi staff yêu cầu gợi ý. Không gửi các trường tên/email/URL tài liệu. Nội dung mentor tự nhập vẫn có thể chứa thông tin cá nhân. Mock frontend chỉ mô phỏng trích xuất và kết quả matching để phát triển giao diện, không đọc file hoặc chạy BGE-M3.

### Trích xuất CV khi upload

- Hồ sơ chuyên môn và nộp CV nằm trong tab `Mentor profile & CV` của trang Profile chung (`/profile`), chỉ hiển thị cho role Mentor. Mentor có thêm tab thông tin cá nhân (`/profile?tab=personal`); các role khác tiếp tục dùng thông tin tài khoản của mình. Đường dẫn cũ `/mentor/profile` chuyển về `/profile`, không còn mục Mentor Profile riêng trên thanh bên. API hồ sơ/nộp CV vẫn yêu cầu quyền Mentor.
- Hồ sơ đã có thông tin mở ở chế độ xem: chuyên môn/công nghệ dạng nhãn, giới thiệu/kinh nghiệm dạng nội dung đọc, tài liệu hiển thị tên file và nút tải xuống. `Edit profile` mở form; lưu thành công đóng form và đưa màn hình về đầu bản đã lưu. `Cancel` bỏ các chỉnh sửa chưa lưu và khôi phục dữ liệu đã lưu gần nhất. Lưu thất bại giữ form/dữ liệu để sửa và thử lại. Hồ sơ trống mở form nộp CV ngay; upload CV vẫn lưu kết quả trích xuất tại backend và để form mở cho mentor kiểm tra.
- Sau khi đăng nhập email hoặc Google, mentor có hồ sơ chuyên môn trống được chuyển thẳng đến `/profile` để nộp CV/nhập thông tin. Có CV hoặc nội dung chuyên môn (giới thiệu, kinh nghiệm, chuyên môn, lĩnh vực, công nghệ, tag hoặc experience entry) thì vào Mentor Overview (`/mentor`). Chỉ thông tin tài khoản/tổ chức hoặc file portfolio chưa được xem là hồ sơ chuyên môn. Nếu tải hồ sơ lỗi, mở trang hồ sơ để hiển thị lỗi và cho phép retry.
- PDF có văn bản: đọc bằng [PdfPig](https://github.com/UglyToad/PdfPig). PDF scan/ảnh chưa có OCR và file đặt mật khẩu bị từ chối với thông báo để người dùng đổi file.
- DOCX: đọc các paragraph và bảng trong `word/document.xml`, giữ Unicode và nối text run. Không đọc liên kết ngoài; cấm DTD và giới hạn XML giải nén 2 MiB.
- DOC cũ: dùng LibreOffice có sẵn trong image backend để chuyển sang PDF rồi đọc văn bản. Backend chạy ngoài Docker cần cài LibreOffice hoặc cấu hình `DocumentPreview:LibreOfficeExecutablePath`. Nếu converter chưa sẵn sàng, API trả `503`; mentor có thể chuyển sang DOCX/PDF và thử lại.
- Tối đa 40 trang PDF và 60.000 ký tự văn bản. Cần tiêu đề mục rõ ràng như `QUÁ TRÌNH ĐÀO TẠO`, `KINH NGHIỆM LÀM VIỆC`, `KỸ NĂNG` hoặc `Education`, `Work experience`, `Technical skills`; chấp nhận đánh số La Mã/số thường, kể cả tiêu đề cùng dòng với nội dung. Kỹ năng chuyên môn IT hoặc kinh doanh được giữ trong phần giới thiệu. Mục tin học phổ thông `Vi tính`, `Computer skills`, `Computer literacy`, `Office skills`, `Tin học văn phòng` bị bỏ qua; không dùng Word/Excel làm bằng chứng chuyên môn IT. Với tiêu đề gộp `Ngoại ngữ & Vi tính`, phần ngoại ngữ được giữ đến tiêu đề con `Vi tính`. Nếu thành tích và sở thích chung một mục, cần tiêu đề con riêng `Thành tích`/`Giải thưởng`; nội dung không phân biệt được bị bỏ qua để tránh đưa sở thích vào hồ sơ. Nội dung được giới hạn theo các trường sẵn có: giới thiệu 2.000 ký tự, kinh nghiệm 4.000 ký tự, tối đa 20 tag mỗi danh sách. Mentor nên kiểm tra lại kết quả trên form.
- Không sửa loại mentor, tổ chức, liên kết, Skills (`technologySkills`), tag thủ công hoặc các experience entry do người dùng quản lý. Phần giới thiệu/kinh nghiệm được cập nhật khi CV có mục tương ứng; mục thiếu giữ giá trị cũ. Tag chuyên môn được gộp vào danh sách hiện tại. Skills được chỉnh trong ô riêng trên form và lưu cùng hồ sơ; upload tài liệu không làm mất giá trị đã nhập. Không thay đổi schema hoặc API response.
- Nếu đọc/trích xuất/upload thất bại thì CV và hồ sơ cũ được giữ lại. Nếu lưu database thất bại, file mới được dọn như luồng upload hiện có. Hash nội dung hồ sơ khiến vector được tạo lại ở lần gợi ý tiếp theo; không cần chạy AI ngay trong request upload.

Chạy test với model thật (Ollama local đã tải `bge-m3`):

```powershell
$env:EHUB_TEST_REAL_OLLAMA = '1'
dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --filter "FullyQualifiedName~MentorCvUploadIntegrationTests"
```

Chạy từ `backend/`. Test tạo PostgreSQL tạm bằng Testcontainers, dùng tài khoản/CV hoàn toàn giả và thay file storage bằng bộ nhớ. Ca model thật được đánh dấu skipped khi chưa bật biến trên; các ca thông thường dùng HTTP double của Ollama để kiểm tra dữ liệu gửi đi và quyền API.

### Thiết lập VPS

- Cần PostgreSQL 18 có extension `vector`. Compose production build pgvector 0.8.6 trên nền `postgres:18-alpine` hiện có để giữ cùng hệ thư viện/collation và volume; compose local dùng image pgvector PostgreSQL 18. Backup và kiểm tra volume trước khi thay image. Migration `IntegrateMentoringAiSupport` bật extension và tạo bảng cache; không tự áp dụng lên production.
- Compose có service Ollama nội bộ, không mở cổng ra Internet. Sau khi khởi động service, chạy `docker compose -f docker-compose.production.yml exec ollama ollama pull bge-m3`. Model được giữ trong volume `ehub_ollama_data` qua lần khởi động lại. Máy cần đủ RAM cho Ollama, PostgreSQL và backend; đo tài nguyên trên VPS trước khi bật production.
- Ollama/BGE-M3 chạy nội bộ không thu phí API theo lượt gọi; chi phí nằm ở tài nguyên VPS, băng thông tải model và vận hành. Bản model Ollama `bge-m3` khoảng 1,2 GB để tải xuống; dung lượng RAM cần thiết phụ thuộc máy và tải thực tế.
- Backend dùng `Mentoring__OllamaBaseUrl=http://ollama:11434` và `Mentoring__EmbeddingModel=bge-m3`. Khi chạy backend ngoài Docker, đặt `Mentoring:OllamaBaseUrl` về địa chỉ Ollama nội bộ. Nếu Ollama chưa chạy hoặc chưa tải model, API gợi ý trả `503` với thông báo an toàn; chức năng phân công thủ công vẫn hoạt động.
- Sau khi đổi mô hình embedding, phải cập nhật số chiều vector trong schema nếu mô hình mới không có 1024 chiều. Không dùng lẫn vector từ hai mô hình trong cùng phép so sánh.

### Staging

`docker-compose.staging.yml` cũng có pgvector và Ollama/BGE-M3. Staging giữ
database volume `ehub-staging-postgres-data` và lưu model riêng trong
`ehub-staging-ollama-data`, trên network `ehub-staging-private`; không publish
cổng PostgreSQL, backend hoặc Ollama ra host. Image PostgreSQL staging được
build bằng Dockerfile pgvector hiện có, với tag riêng để không retag production.
Backend staging dùng `Mentoring__OllamaBaseUrl=http://ollama:11434` và
`Mentoring__EmbeddingModel=bge-m3`.

Theo [runbook staging](deployment/STAGING_VPS.md#7-build-migration-và-khởi-động),
build image PostgreSQL, khởi động PostgreSQL/Ollama, tải `bge-m3`, rồi chạy
initializer và ứng dụng bằng Compose staging. Backup trước khi nâng cấp
database đang có; model pull thành công và gợi ý mentor hoạt động mới xác nhận
AI đã sẵn sàng. Ollama staging có giới hạn 2 CPU / 3 GiB RAM riêng; kiểm tra
tổng tài nguyên của cả staging và production trên VPS dùng chung.

### VPS Linux 4 vCPU / RAM 8 GB đang chạy E-HUB

Compose production giới hạn riêng Ollama ở 2 CPU và 3 GiB RAM, không cho container dùng thêm swap. Đây là ngân sách ban đầu để thử nghiệm, không phải lượng RAM đã đo hoặc bảo đảm BGE-M3 chạy được với mọi đầu vào. Một model được nạp tại một thời điểm, cấu hình một yêu cầu xử lý đồng thời và hàng đợi tối đa 4 yêu cầu; khi quá tải API gợi ý có thể trả 503. Các biến này theo [tài liệu Ollama](https://docs.ollama.com/faq). Chế độ cloud được tắt.

Sau khi đưa file Compose mới lên VPS, chạy từ thư mục dự án. Thay đường dẫn ví dụ bằng file cấu hình production đang dùng tại máy; không gửi nội dung file này vào chat:

```bash
free -h
df -h /var/lib/docker
docker stats --no-stream
docker compose --env-file /path/to/production.env -f docker-compose.production.yml config --quiet
docker compose --env-file /path/to/production.env -f docker-compose.production.yml up -d --no-deps ollama
docker compose --env-file /path/to/production.env -f docker-compose.production.yml exec ollama ollama pull bge-m3
docker compose --env-file /path/to/production.env -f docker-compose.production.yml exec ollama ollama list
```

Các lệnh trên chỉ khởi động Ollama và tải model; chưa cập nhật backend, PostgreSQL hoặc chạy migration. Giữ nguyên cách cấp biến môi trường của hệ thống đang vận hành nếu không dùng `--env-file`. Nếu Docker dùng data-root khác, kiểm tra dung lượng tại đường dẫn đó.

Sau khi triển khai backend và migration theo quy trình bên dưới, kiểm tra matching bằng dữ liệu thử nghiệm: hồ sơ chưa có cache, lần gọi lại có cache, hồ sơ dài và vài yêu cầu đồng thời. Theo dõi `docker stats`, thời gian trả lời và trạng thái `OOMKilled` của container. Backend có deadline 45 giây, nên máy CPU có thể hết hạn ở lần tạo cache đầu tiên. Nếu không đạt, cần điều chỉnh kích thước batch/chuẩn bị cache trước hoặc tăng tài nguyên sau khi đo; không tăng giới hạn RAM vượt phần tài nguyên còn trống. Hiện tại chưa có job chuẩn bị cache tự động.

## Quyền truy cập

| Tác vụ | Admin | Giảng viên phụ trách | Mentor được phân công | Sinh viên thuộc nhóm |
| --- | --- | --- | --- | --- |
| Danh mục/gợi ý | Có | Trong phạm vi lớp/học kỳ | Không | Không |
| Phân công mentor | Có | Có | Không | Không |
| Tạo/sửa/kết thúc buổi | Có | Có | Có | Không |
| Xem buổi | Có | Có | Có | Có |
| Gửi đánh giá buổi | Không | Không | Không | Sau buổi hoàn thành |
| Xem đánh giá | Có | Có | Có | Không |

Mọi quyền tài nguyên được kiểm tra tại handler bằng người dùng hiện tại và quan hệ lớp, nhóm, học kỳ hoặc phân công. Không lấy `userId`/role từ request body.

## API mới

Các endpoint nằm dưới `/api/mentoring`: `GET/PUT /profile`, `POST /profile/documents/{cv|portfolio}`, `GET /profiles/{id}/documents/{kind}`, `GET /directory`, `GET /teams/{id}/recommendations`, `GET/POST /sessions`, `PUT /sessions/{id}`, `POST /sessions/{id}/complete`, `POST /sessions/{id}/cancel`, `POST /sessions/{id}/action-items`, `PUT/GET /sessions/{id}/feedback`.

## Triển khai và rollback

Migration mới `IntegrateMentoringAiSupport` được EF sinh trên schema develop mới, bổ sung metadata tài liệu, bảng feedback và vector. Hai migration AI chưa merge trước đây đã được thay thế để tránh trùng cột `mentor_type`; không sửa migration đã có trong develop.

Áp dụng cho database mới hoặc database đang theo lịch sử develop. Database demo riêng đã chạy hai migration AI cũ không được tự động nâng cấp bằng bản này: giữ database đó làm bản lưu và dùng database local mới khi thử bản gộp. Nếu cần chuyển dữ liệu demo cũ, phải có bước chuyển đổi riêng sau khi backup; không xóa volume để giải quyết lỗi.

Trước production cần backup và chạy migration trên staging. Rollback migration mới sẽ xóa cache vector, feedback và metadata tài liệu mới; file Cloudinary không tự bị xóa. Không tự apply migration production.

Theo develop mới, mỗi nhóm có tối đa một mentor Enterprise và một mentor Academic đang hoạt động. Tạo buổi mentoring có trường `mentorAssignmentId`: bắt buộc chọn khi staff làm việc với nhóm có hai mentor; mentor chỉ được tạo cho phân công của chính mình. Backend từ chối đổi phân công khi sửa buổi đã tạo.
