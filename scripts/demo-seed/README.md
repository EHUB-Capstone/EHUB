# Dữ liệu mẫu để demo (staging)

Script thêm dữ liệu **hoàn toàn giả** vào PostgreSQL để demo hệ thống. Không đổi schema (không cần migration),
không sửa hay xóa dòng đang có, chạy lại không tạo trùng.

| File | Vai trò |
| --- | --- |
| `demo-seed.sql` | Thêm dữ liệu demo (1 transaction, lỗi thì rollback toàn bộ) |
| `demo-cleanup.sql` | Xóa đúng dữ liệu demo do `demo-seed.sql` tạo |
| `make-password-hash.ps1` | Tạo bcrypt hash cho mật khẩu demo trên máy Windows |

## Dữ liệu được tạo

- Học kỳ: dùng học kỳ có sẵn (mặc định `FA2026`, đổi bằng `-v demo_semester_code=...`).
- 16 tài khoản: `demo.lecturer01..02`, `demo.mentor01..02`, `demo.student01..12` (đuôi `@example.com`).
  Tất cả dùng chung **mật khẩu demo do bạn chọn**; mật khẩu không nằm trong repo.
- 2 lớp `EXE101_90` (giảng viên 01) và `EXE201_91` (giảng viên 02), mỗi lớp 6 sinh viên (`DEMO0001..0012`).
- 6 team (`DEMO-A1..A3`, `DEMO-B1..B3`), mỗi team 2 sinh viên và 1 project.
- 4 bài nộp Checkpoint 1, 1 đánh giá đã công bố (MediTrack, 41/50), 3 phân công mentor, 6 buổi mentoring.

Nhận diện dữ liệu demo: email `demo.*@example.com`, mã sinh viên `DEMO*`, team `DEMO-*`, lớp `EXE101_90`/`EXE201_91`.

## Quy trình

1. **Windows**: tạo hash mật khẩu demo (cần Docker Desktop đang chạy).

   ```powershell
   .\scripts\demo-seed\make-password-hash.ps1
   ```

   Nhập mật khẩu (ít nhất 12 ký tự) và lưu lại ở nơi riêng tư. Script in ra hash dạng `$2a$11$...`.

2. **Windows**: chép script lên VPS (ngoài thư mục git của staging để không làm bẩn worktree).

   ```powershell
   scp -i "$env:USERPROFILE\.ssh\ehub_vps_ed25519" .\scripts\demo-seed\demo-seed.sql ehubadmin@<IP-VPS>:/home/ehubadmin/demo-seed.sql
   ```

3. **VPS**: backup trước, rồi chạy (khai báo `STAGING_COMPOSE` như `docs/deployment/STAGING_VPS.md` mục 6).

   ```bash
   cd /opt/ehub/staging/app && bash scripts/staging/backup-postgres.sh
   HASH='<dán hash ở bước 1>'
   "${STAGING_COMPOSE[@]}" exec -T -e DEMO_HASH="$HASH" postgres sh -c \
     'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 --single-transaction -v demo_password_hash="$DEMO_HASH" -f -' \
     < /home/ehubadmin/demo-seed.sql
   ```

   Thành công in `NOTICE: Da seed: 16 user, ...`. In `Du lieu demo da ton tai` nghĩa là đã seed trước đó.

4. Đăng nhập thử bằng `demo.lecturer01@example.com` và mật khẩu demo.

## Dọn dẹp

```bash
"${STAGING_COMPOSE[@]}" exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 --single-transaction -f -' \
  < /path/to/demo-cleanup.sql
```

Nếu trong lúc demo hệ thống đã tạo thêm dữ liệu tham chiếu tới tài khoản demo (thông báo, chat...), khóa ngoại
sẽ chặn việc xóa và toàn bộ được rollback; khi đó hãy xử lý phần dữ liệu phát sinh trước.

## Lưu ý bảo mật

- Staging truy cập được từ internet: dùng mật khẩu demo mạnh, không dùng lại mật khẩu thật.
- Email `@example.com` không nhận được thư; quên mật khẩu/OTP của tài khoản demo sẽ không hoạt động.
- Không đưa mật khẩu demo hoặc hash vào chat, commit hay log.
