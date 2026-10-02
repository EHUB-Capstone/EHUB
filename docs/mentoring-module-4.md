# Module 4 — Mentor matching và hỗ trợ nhóm dự án

## Phạm vi triển khai

- Một hồ sơ mentor tồn tại xuyên học kỳ. Mentor tự cập nhật loại (`Business` hoặc `IT`), chuyên môn, kinh nghiệm, giới thiệu, tổ chức, LinkedIn và portfolio URL.
- Mentor tải CV hoặc portfolio dạng PDF/DOCX (tối đa 10 MB). Backend kiểm tra phần mở rộng và chữ ký/nội dung cấu trúc file trước khi lưu; chỉ mentor sở hữu, admin và giảng viên có phạm vi học kỳ phù hợp được tải xuống. URL lưu trữ không xuất hiện trong DTO.
- Admin và giảng viên xem danh mục mentor cùng số nhóm đang phụ trách, tổng lần phân công, buổi mentoring và điểm phản hồi trung bình. Danh mục của giảng viên bị giới hạn theo học kỳ/lớp phụ trách.
- Gợi ý mentor cho nhóm theo mô tả dự án, công nghệ, tag, chuyên môn và kinh nghiệm. API chỉ xét mentor có mặt trong danh sách nhân sự học kỳ, đang hoạt động và hiển thị sức chứa. Điểm 0–100 và lý do được trả về để giảng viên chọn; API gợi ý không tự phân công.
- Admin, giảng viên phụ trách hoặc mentor được phân công tạo, sửa, hoàn thành, hủy buổi mentoring và ghi action item. Sinh viên thuộc nhóm được xem buổi và gửi đánh giá 1–5 cùng nhận xét sau khi buổi kết thúc. Admin/giảng viên/mentor được xem đánh giá.
- Phân công mentor tiếp tục dùng luồng transaction, audit và Outbox đã có trong `MentorAssignmentHandler`.

## Quy tắc matching hiện tại

Matching dùng mô hình embedding đa ngôn ngữ BGE-M3 chạy trong Ollama nội bộ. Mô tả dự án và hồ sơ chuyên môn mentor được mã hóa thành vector 1024 chiều; hệ thống tính cosine similarity và kết hợp điểm khớp kỹ năng cụ thể (70% ngữ nghĩa, 30% kỹ năng; tối đa phần kỹ năng khi khớp hai kỹ năng khác nhau). Các trọng số này là giá trị khởi đầu, cần hiệu chỉnh bằng đánh giá của giảng viên. Đây là **điểm xếp hạng**, không phải xác suất thành công hay tỷ lệ phù hợp đã được hiệu chuẩn. Mentor hết sức chứa bị loại khỏi gợi ý trước khi tạo vector. Quyền phân công vẫn thuộc giảng viên/bộ môn.

Nhóm cần có mô tả dự án, công nghệ, lĩnh vực hoặc tag trước khi nhận gợi ý; chỉ tên nhóm không đủ dữ liệu để xếp hạng.

Vector hồ sơ mentor được lưu trong PostgreSQL `mentor_embeddings` bằng pgvector. Hash của nội dung và tên mô hình giúp tạo lại vector khi mentor sửa hồ sơ hoặc đổi mô hình. Vector dự án được tạo khi yêu cầu gợi ý. Cosine được tính chính xác trong backend trên các mentor đã lọc, chưa dùng chỉ mục HNSW. Văn bản được giới hạn 6.000 ký tự, ưu tiên thông tin chuyên môn/công nghệ; mô hình được gọi theo batch 4 hồ sơ. Backend giới hạn tổng thời gian gợi ý 45 giây; cache đã ghi thành công được tái sử dụng khi người dùng thử lại.

Chỉ loại mentor, chuyên môn, kinh nghiệm và giới thiệu được gửi tới Ollama nội bộ; không đọc CV hoặc gửi trường tên/email/URL tài liệu. Nội dung tự do do mentor nhập có thể chứa thông tin cá nhân. Mock frontend chỉ mô phỏng kết quả để phát triển giao diện, không chạy BGE-M3.

### Thiết lập VPS

- Cần PostgreSQL 18 có extension `vector`. Compose production build pgvector 0.8.6 trên nền `postgres:18-alpine` hiện có để giữ cùng hệ thư viện/collation và volume; compose local dùng image pgvector PostgreSQL 18. Backup và kiểm tra volume trước khi thay image. Migration `AddMentorEmbeddings` bật extension và tạo bảng cache; không tự áp dụng lên production.
- Compose có service Ollama nội bộ, không mở cổng ra Internet. Sau khi khởi động service, chạy `docker compose -f docker-compose.production.yml exec ollama ollama pull bge-m3`. Model được giữ trong volume `ehub_ollama_data` qua lần khởi động lại. Máy cần đủ RAM cho Ollama, PostgreSQL và backend; đo tài nguyên trên VPS trước khi bật production.
- Ollama/BGE-M3 chạy nội bộ không thu phí API theo lượt gọi; chi phí nằm ở tài nguyên VPS, băng thông tải model và vận hành. Bản model Ollama `bge-m3` khoảng 1,2 GB để tải xuống; dung lượng RAM cần thiết phụ thuộc máy và tải thực tế.
- Backend dùng `Mentoring__OllamaBaseUrl=http://ollama:11434` và `Mentoring__EmbeddingModel=bge-m3`. Khi chạy backend ngoài Docker, đặt `Mentoring:OllamaBaseUrl` về địa chỉ Ollama nội bộ. Nếu Ollama chưa chạy hoặc chưa tải model, API gợi ý trả `503` với thông báo an toàn; chức năng phân công thủ công vẫn hoạt động.
- Sau khi đổi mô hình embedding, phải cập nhật số chiều vector trong schema nếu mô hình mới không có 1024 chiều. Không dùng lẫn vector từ hai mô hình trong cùng phép so sánh.

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

Migration `ExpandMentorProfilesAndFeedback` thêm metadata hồ sơ và bảng `mentoring_feedback`; mentor cũ có loại `Unspecified` để tránh đoán sai. Migration `AddMentorEmbeddings` thêm extension `vector` và bảng cache embedding. Trước triển khai cần backup PostgreSQL, review `Up`/`Down`, chạy migration trên staging và kiểm tra quyền theo vai trò. Rollback `AddMentorEmbeddings` xóa cache vector, có thể tạo lại từ hồ sơ sau khi triển khai lại; rollback `ExpandMentorProfilesAndFeedback` sẽ **xóa cột metadata và toàn bộ feedback mới**. File đã tải lên Cloudinary cần dọn riêng nếu rollback.

Schema hiện tại chỉ cho một mentor đang hoạt động trên mỗi nhóm. Việc cho mentor doanh nghiệp và mentor IT cùng phụ trách một nhóm cần quyết định nghiệp vụ và migration riêng; không tự thay đổi ràng buộc này.
