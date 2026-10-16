# Sao lưu và khôi phục PostgreSQL của EHUB

Tài liệu này ghi lại các quyết định về sao lưu và khôi phục, và sẽ được bổ sung
khi từng giai đoạn triển khai hoàn thành. Phần hiện trạng thực tế luôn nằm ở
mục 2; không coi một mục là đã có nếu trạng thái chưa ghi "Đã hoạt động".

## 1. Quyết định (chốt ở Giai đoạn 0)

| Hạng mục | Quyết định |
| --- | --- |
| Mục tiêu mất dữ liệu tối đa (RPO) | 24 giờ |
| Mục tiêu thời gian khôi phục (RTO) | Vài giờ; con số thật sẽ đo ở lần restore thử đầu tiên |
| Lịch backup tự động | Hằng ngày khoảng 02:00 giờ Việt Nam (Asia/Ho_Chi_Minh) |
| Backup khi phát hành | Vẫn giữ bước backup (có dừng dịch vụ) trước mỗi lần deploy |
| Nơi lưu trên VPS | `/opt/ehub/backups`, giữ 14 ngày (cơ chế hiện có) |
| Nơi lưu ngoài VPS | Bucket Cloudflare R2 riêng `ehub-db-backups`, riêng tư, không bật truy cập công khai |
| Thời gian giữ trên R2 | 30 ngày (quy tắc vòng đời của bucket) |
| Quyền truy cập | API token chỉ có quyền Object Read & Write trên đúng bucket backup; không dùng chung token của bucket bài nộp |
| Mã hóa | Không mã hóa phía client. Dựa vào mã hóa lưu trữ của R2, bucket riêng tư, token hạn chế và 2FA cho tài khoản Cloudflare |
| Laptop | Không bắt buộc giữ bản sao; không yêu cầu mã hóa ổ đĩa (mentor đã đồng ý) |
| Cảnh báo | Discord, kênh riêng tư `ehub-backup` (webhook tên "EHUB Backup"): báo thành công (ngắn) và báo lỗi; thêm cảnh báo khi backup quá 26 giờ hoặc ổ đĩa vượt 75% |
| Phạm vi tự động | Production trước. Staging giữ backup thủ công khi deploy |
| Test restore | Mỗi tháng một lần và sau thay đổi schema lớn; dùng bản tải từ R2 |

## 2. Trạng thái triển khai

| Hạng mục | Trạng thái |
| --- | --- |
| Backup thủ công trước mỗi lần deploy (`scripts/production/backup-postgres.sh`) | Đã hoạt động |
| Bucket R2 backup và token (bucket riêng tư, quy tắc 30 ngày, token chỉ Object Read & Write trên bucket backup) | Đã có |
| Discord webhook kênh `ehub-backup` | Đã có (đã thử gửi tin) |
| Backup tự động hằng ngày | Chưa triển khai |
| Đẩy bản sao lên R2 | Chưa triển khai |
| Cảnh báo Discord | Chưa triển khai |
| Restore thử | Chưa thực hiện |

## 3. Biến cấu hình trên VPS

Các giá trị bí mật nằm trong `/opt/ehub/backup.env` (quyền 600, chủ sở hữu root,
ngoài Git). Mẫu giá trị giả: `deploy/backup/backup.env.example`. Không đưa token,
khóa truy cập hay URL webhook vào Git, chat hoặc log.

## 4. Các mục sẽ bổ sung

- Cách chạy backup tự động và đọc thông báo (Giai đoạn 4).
- Quy trình restore và bảng ghi kết quả test (Giai đoạn 5).
