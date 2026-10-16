# Sao lưu và khôi phục PostgreSQL của EHUB

Tài liệu này mô tả quyết định, cách cài đặt, vận hành và khôi phục sao lưu
PostgreSQL. Phần hiện trạng thực tế luôn nằm ở mục 2; không coi một mục là đã
có nếu trạng thái chưa ghi "Đã hoạt động".

## 1. Quyết định

| Hạng mục | Quyết định |
| --- | --- |
| Mục tiêu mất dữ liệu tối đa (RPO) | 24 giờ |
| Mục tiêu thời gian khôi phục (RTO) | Vài giờ; con số thật sẽ đo ở lần restore thử đầu tiên |
| Lịch backup tự động | Hằng ngày khoảng 02:00 giờ Việt Nam (Asia/Ho_Chi_Minh), systemd timer |
| Backup khi phát hành | Vẫn giữ bước backup (có dừng dịch vụ) trước mỗi lần deploy |
| Nơi lưu trên VPS | `/opt/ehub/backups`, giữ 14 ngày (cơ chế hiện có) |
| Nơi lưu ngoài VPS | Bucket Cloudflare R2 riêng `ehub-db-backups`, riêng tư, không bật truy cập công khai |
| Thời gian giữ trên R2 | 30 ngày (quy tắc vòng đời của bucket) |
| Quyền truy cập | API token chỉ có quyền Object Read & Write trên đúng bucket backup; không dùng chung token của bucket bài nộp |
| Mã hóa | Không mã hóa phía client. Dựa vào mã hóa lưu trữ của R2, bucket riêng tư, token hạn chế và 2FA cho tài khoản Cloudflare |
| Laptop | Không bắt buộc giữ bản sao; không yêu cầu mã hóa ổ đĩa (mentor đã đồng ý) |
| Cảnh báo | Discord, kênh riêng tư `ehub-backup` (webhook tên "EHUB Backup"): báo thành công (ngắn) và báo lỗi; thêm cảnh báo khi backup quá 26 giờ hoặc ổ đĩa vượt 75% |
| Phạm vi tự động | Production. Staging chỉ dùng để thử script (không bật timer) |
| Test restore | Mỗi tháng một lần và sau thay đổi schema lớn; dùng bản tải từ R2 |

## 2. Trạng thái triển khai

| Hạng mục | Trạng thái |
| --- | --- |
| Backup thủ công trước mỗi lần deploy (`scripts/production/backup-postgres.sh`) | Đã hoạt động |
| Bucket R2 backup và token (riêng tư, quy tắc 30 ngày, chỉ Object Read & Write) | Đã có |
| Discord webhook kênh `ehub-backup` | Đã có (đã thử gửi tin) |
| Script backup tự động, tải lên R2, báo Discord, kiểm tra backup quá cũ | Đã soạn trong repo; chưa chạy trên VPS |
| Thử script trên staging | Chưa thực hiện |
| systemd timer trên production | Chưa bật |
| Restore thử | Chưa thực hiện |

Cập nhật bảng này sau mỗi bước hoàn thành trên máy thật.

## 3. Thành phần trong repo

| File | Vai trò |
| --- | --- |
| `scripts/production/backup-postgres.sh`, `scripts/staging/backup-postgres.sh` | Tạo bản dump, kiểm tra bằng `pg_restore --list`, tạo checksum, xóa bản cũ hơn 14 ngày. Có khóa để hai backup không chạy chồng nhau (mã thoát 75 nếu đang bận) |
| `scripts/production/backup-and-upload.sh <production\|staging> [run\|check]` | `run`: backup, tải dump và checksum lên R2, kiểm tra kích thước, báo Discord. `check`: kiểm tra cấu hình, quyền đọc bucket và gửi tin thử |
| `scripts/production/check-backup-freshness.sh <production\|staging>` | Báo Discord khi backup mới nhất (VPS hoặc R2) quá hạn, khi không đọc được R2, hoặc ổ đĩa vượt ngưỡng; im lặng nếu mọi thứ ổn |
| `scripts/production/backup-common.sh` | Hàm dùng chung (đọc cấu hình, gửi Discord, gọi rclone) |
| `deploy/systemd/ehub-backup.{service,timer}` | Chạy backup mỗi ngày 02:00 (có chạy bù nếu VPS tắt đúng giờ đó) |
| `deploy/systemd/ehub-backup-freshness.{service,timer}` | Kiểm tra mỗi ngày 09:00 |
| `deploy/backup/backup.env.example` | Mẫu cấu hình giá trị giả |

Các giá trị bí mật nằm trong `/opt/ehub/backup.env` (quyền 600, chủ sở hữu root,
ngoài Git). Script từ chối chạy nếu file không đúng quyền. Không đưa token, khóa
truy cập hay URL webhook vào Git, chat hoặc log. Đối tượng trên R2 nằm theo tiền tố
`production/` và `staging/`.

## 4. Cài đặt trên VPS

Chỉ làm sau khi bản phát hành chứa các script đã được deploy (thư mục
`/opt/ehub/app` có `scripts/production/backup-and-upload.sh`). Chạy với quyền
`ehubadmin`, từ VPS (Bash).

### 4.1. Cài rclone và tạo file cấu hình bí mật
```bash
sudo apt install -y rclone
sudo install -m 600 -o root -g root /dev/null /opt/ehub/backup.env
sudo nano /opt/ehub/backup.env
sudo stat -c '%a %U' /opt/ehub/backup.env
```
Dán các giá trị theo mẫu `deploy/backup/backup.env.example` (giá trị chứa ký tự đặc
biệt thì đặt trong dấu nháy kép). Lệnh `stat` phải in `600 root`.

### 4.2. Thử trên staging trước (không bật timer)
Sau khi staging đã deploy bản có các script:
```bash
sudo bash /opt/ehub/staging/app/scripts/production/backup-and-upload.sh staging check
sudo bash /opt/ehub/staging/app/scripts/production/backup-and-upload.sh staging run
```
`check` phải in `CHECK_OK` và kênh Discord nhận tin "[KIEM TRA]". `run` phải tạo bản
backup, tải lên R2 dưới `staging/` và Discord nhận tin "[OK]". Kiểm tra trên dashboard
Cloudflare: bucket `ehub-db-backups` có thư mục `staging/` chứa file `.dump` và `.sha256`.

### 4.3. Bật trên production
```bash
sudo bash /opt/ehub/app/scripts/production/backup-and-upload.sh production check
sudo bash /opt/ehub/app/scripts/production/backup-and-upload.sh production run
sudo cp /opt/ehub/app/deploy/systemd/ehub-backup.service /opt/ehub/app/deploy/systemd/ehub-backup.timer \
    /opt/ehub/app/deploy/systemd/ehub-backup-freshness.service /opt/ehub/app/deploy/systemd/ehub-backup-freshness.timer \
    /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now ehub-backup.timer ehub-backup-freshness.timer
systemctl list-timers 'ehub-*'
```
`list-timers` phải hiện hai timer với thời điểm chạy kế tiếp. Chạy thử đường systemd một lần:
```bash
sudo systemctl start ehub-backup.service
journalctl -u ehub-backup.service -n 50 --no-pager
```

## 5. Vận hành hằng ngày

| Tin trên Discord | Ý nghĩa | Việc cần làm |
| --- | --- | --- |
| `[OK] ... sao luu xong ...` | Backup đã tạo, tải lên R2 và kiểm tra kích thước | Không |
| `[BO QUA] ...` | Có backup khác đang chạy (thường là lúc deploy) | Không, lần sau sẽ chạy lại |
| `[LOI] ... buoc "<tên bước>" ...` | Backup thất bại ở bước đó | `journalctl -u ehub-backup.service -n 100`, sửa rồi chạy lại |
| `[CANH BAO] ... ban sao luu ... da N gio` | Quá 26 giờ không có backup mới | Kiểm tra `systemctl list-timers` và log |
| `[CANH BAO] ... o dia ...` | Ổ đĩa vượt 75% | Dọn build cache (xem PRODUCTION_VPS.md mục 9) |

Lệnh hữu ích:
```bash
systemctl list-timers 'ehub-*'
systemctl status ehub-backup.service --no-pager
sudo bash /opt/ehub/app/scripts/production/check-backup-freshness.sh production
```
Nếu không thấy tin nhắn "[OK]" nào trong một ngày thì cũng là dấu hiệu lỗi; job kiểm tra lúc 09:00 sẽ báo.

## 6. Khôi phục và test restore

Một bản backup chỉ được coi là dùng được sau khi restore thử thành công. Khôi phục thật
trên production là thao tác phá hủy dữ liệu hiện tại, cần người thứ hai kiểm tra tên
database và tên file (xem PRODUCTION_VPS.md mục 8).

### 6.1. Restore thử vào database tạm trên laptop
1. Tải bản backup: dashboard Cloudflare → R2 → `ehub-db-backups` → `production/` →
   chọn file `.dump` mới nhất và file `.sha256` tương ứng → Download.
2. So checksum (PowerShell, trong thư mục chứa file):
```powershell
$dump = "<ten-file>.dump"
$expected = ((Get-Content "$dump.sha256" -TotalCount 1) -split '\s+')[0]
$actual = (Get-FileHash $dump -Algorithm SHA256).Hash
if ($expected -ine $actual) { throw "Hash khong khop" } else { "Hash khop" }
```
3. Restore vào container tạm (phải dùng image có pgvector vì dump chứa kiểu `vector`):
```powershell
docker run -d --name ehub-restore-drill -e POSTGRES_PASSWORD=drill-only -e POSTGRES_DB=ehub_restore pgvector/pgvector:0.8.6-pg18
docker cp $dump ehub-restore-drill:/tmp/restore.dump
docker exec ehub-restore-drill pg_restore -U postgres -d ehub_restore --no-owner --no-acl --exit-on-error /tmp/restore.dump
docker exec ehub-restore-drill psql -U postgres -d ehub_restore -c "ANALYZE;"
docker exec ehub-restore-drill psql -U postgres -d ehub_restore -c "select count(*) from \"__EFMigrationsHistory\";"
docker exec ehub-restore-drill psql -U postgres -d ehub_restore -c "select relname, n_live_tup from pg_stat_user_tables order by n_live_tup desc limit 10;"
docker rm -f ehub-restore-drill
```
4. Xóa file dump khỏi laptop sau khi xong (chứa dữ liệu cá nhân, không đưa vào Git hay chat).
5. Ghi kết quả vào bảng dưới.

### 6.2. Nhật ký test restore

| Ngày | Nguồn (R2 hay VPS) | Tên file | Thời gian restore | Kết quả | Số bản ghi đối chiếu | Người thực hiện |
| --- | --- | --- | --- | --- | --- | --- |
| | | | | | | |

Lịch: mỗi tháng một lần và sau thay đổi schema lớn.
