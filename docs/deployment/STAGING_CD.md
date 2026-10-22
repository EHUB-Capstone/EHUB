# Deploy tự động lên staging (CD)

Tài liệu này mô tả cách VPS nhận lệnh deploy staging từ GitHub Actions và cách cài
đặt phần phía VPS. Quy trình deploy thủ công trong [STAGING_VPS.md](STAGING_VPS.md)
vẫn là nguồn tham chiếu khi cần xử lý sự cố.

## 1. Cách hoạt động

```text
GitHub Actions --SSH (key riêng, chỉ chạy 1 lệnh)--> ehub-deployer
   deploy-staging-entry.sh  (forced command, do root sở hữu)
      sudo -n /opt/ehub/deploy/deploy-staging.sh <SHA>   (sudoers cho đúng đường dẫn này)
         1. kiểm tra SHA 40 ký tự hex và thuộc origin/develop
         2. backup DB staging (bằng bản checkout đang chạy, trước khi thay)
         3. checkout --detach SHA, cập nhật IMAGE_TAG trong .env.staging
         4. build, khởi động postgres/ollama, migrate, khởi động backend/frontend
         5. health check loopback :3001, xác nhận image đang chạy đúng SHA
```

Các nguyên tắc an toàn:

- `ehub-deployer` không có sudo tổng quát, không có shell và không chuyển tiếp cổng.
  Key SSH bị khóa vào đúng một lệnh (`restrict,command=...`).
- Script triển khai chỉ nhận **một SHA đầy đủ** thuộc `origin/develop`. Nó cố định
  đường dẫn staging và project `ehub-staging`, nên không thể chạm vào production.
- Script chạy từ **bản đã cài** `/opt/ehub/deploy/` (root sở hữu), không chạy từ
  thư mục checkout. Muốn đổi script phải cài lại thủ công sau khi PR được duyệt.
- Mọi output ghi vào `/var/log/ehub-staging-deploy.d/<thời gian>-<sha>.log`. SSH chỉ
  nhận bản sao qua `tail`, nên mất kết nối không làm deploy chết giữa chừng migration.
- Một lần deploy tại một thời điểm trên cả VPS (`flock`, khóa `/run/lock/ehub-deploy.lock`,
  dùng chung với deploy production vì hai môi trường chung CPU/RAM). Lần đến sau **chờ tối
  đa 20 phút** cho lần trước xong, rồi mới báo mã thoát `75`.
- **Không tự rollback.** Khi một bước lỗi, script dừng, ghi `FAILED stage=...` và để
  nguyên hiện trạng, vì migration có thể đã chạy.

Mã thoát: `0` thành công, `64` sai cú pháp/SHA, `75` deploy khác giữ khóa quá 20 phút, `77` không phải
root, giá trị khác là bước bị lỗi (xem dòng `EHUB_DEPLOY_RESULT=failed stage=...`).

## 2. Điều kiện cần có trên VPS

- Checkout staging ở `/opt/ehub/staging/app` thuộc về `ehubadmin` (không phải root) và
  `ehubadmin` chạy được `git fetch origin develop` (đã có quyền đọc repo).
- `/opt/ehub/staging/app/.env.staging` tồn tại với đúng một dòng `IMAGE_TAG=`.
- `docker compose version` có `up --wait` và `--wait-timeout` (Compose 2.20 trở lên).
- Còn trống ít nhất 8 GB trên ổ chứa Docker (script tự kiểm tra).

Kiểm tra nhanh:

```bash
docker compose version
sudo -u ehubadmin git -C /opt/ehub/staging/app fetch origin develop && echo fetch-ok
grep -c '^IMAGE_TAG=' /opt/ehub/staging/app/.env.staging
```

Dòng cuối phải in `1`. Không in nội dung `.env.staging`.

## 3. Cài đặt (làm một lần)

### 3.1 Tạo cặp SSH key riêng cho CD (máy Windows của bạn)

```powershell
ssh-keygen -t ed25519 -C "ehub-cd-staging" -f "$env:USERPROFILE\.ssh\ehub_cd_staging" -N '""'
Get-Content "$env:USERPROFILE\.ssh\ehub_cd_staging.pub"
```

- Key **không có passphrase** vì GitHub Actions không thể nhập passphrase.
- File `.pub` là khóa công khai, được phép sao chép. File `ehub_cd_staging` (không có
  đuôi) là **khóa riêng**: chỉ dán vào GitHub Secrets ở giai đoạn sau, không gửi cho ai,
  không đưa vào chat hay repo. Đây phải là một cặp key mới, không dùng lại key của `ehubadmin`.

### 3.2 Gửi hai script lên VPS (từ thư mục gốc repo trên Windows)

Khi các script chưa được merge vào `develop`, dùng `scp` để cài bản đã review:

```powershell
$EhubVpsHost = Read-Host "VPS IP or hostname"
scp -i "$env:USERPROFILE\.ssh\ehub_vps_ed25519" `
  scripts\staging\deploy-staging.sh scripts\staging\deploy-staging-entry.sh `
  "ehubadmin@${EhubVpsHost}:/tmp/"
```

### 3.3 Cài script, user và quyền (VPS, đăng nhập bằng `ehubadmin`)

```bash
# Script phải là LF, không có ký tự CR (kết quả mong đợi: 0 và 0)
grep -c $'\r' /tmp/deploy-staging.sh /tmp/deploy-staging-entry.sh
bash -n /tmp/deploy-staging.sh && bash -n /tmp/deploy-staging-entry.sh && echo syntax-ok

sudo install -d -m 0755 -o root -g root /opt/ehub/deploy
sudo install -m 0755 -o root -g root /tmp/deploy-staging.sh /opt/ehub/deploy/deploy-staging.sh
sudo install -m 0755 -o root -g root /tmp/deploy-staging-entry.sh /opt/ehub/deploy/deploy-staging-entry.sh
rm -f /tmp/deploy-staging.sh /tmp/deploy-staging-entry.sh
```

Thư mục cha `/opt/ehub` thuộc `ehubadmin` với quyền `0750`, nên người khác không đi qua
được tới `/opt/ehub/deploy`. Sau khi tạo user ở bước kế tiếp, cấp quyền **đi qua** (không
đọc, không liệt kê) cho đúng `ehub-deployer`:

```bash
sudo setfacl -m u:ehub-deployer:x /opt/ehub
sudo -u ehub-deployer test -x /opt/ehub/deploy/deploy-staging-entry.sh && echo can-execute
sudo -u ehub-deployer ls /opt/ehub 2>&1 | head -n 1
```

Dòng đầu phải in `can-execute`; dòng thứ hai phải báo `Permission denied`. Nếu thiếu
`setfacl`: `sudo apt install -y acl`. Không dùng `chmod o+x /opt/ehub`, vì như vậy mọi
user đều đi qua được.

Tạo user không có mật khẩu đăng nhập:

```bash
sudo adduser --disabled-password --gecos "" ehub-deployer
```

Cho phép user này chạy **đúng một** lệnh với quyền root:

```bash
echo 'ehub-deployer ALL=(root) NOPASSWD: /opt/ehub/deploy/deploy-staging.sh' \
  | sudo tee /etc/sudoers.d/ehub-deployer > /dev/null
sudo chmod 0440 /etc/sudoers.d/ehub-deployer
sudo visudo -cf /etc/sudoers.d/ehub-deployer
sudo -l -U ehub-deployer
```

`visudo -cf` phải báo `parsed OK`, và `sudo -l -U` chỉ liệt kê đúng một lệnh trên.

Gắn khóa công khai với lệnh bị khóa. Thư mục `.ssh` và file do root sở hữu để
`ehub-deployer` không tự sửa được:

```bash
sudo install -d -m 0755 -o root -g root /home/ehub-deployer/.ssh
read -r -p "Dán MỘT dòng khóa công khai (ehub_cd_staging.pub): " EHUB_CD_PUBKEY
printf 'restrict,command="/opt/ehub/deploy/deploy-staging-entry.sh" %s\n' "$EHUB_CD_PUBKEY" \
  | sudo tee /home/ehub-deployer/.ssh/authorized_keys > /dev/null
sudo chown root:root /home/ehub-deployer/.ssh/authorized_keys
sudo chmod 0644 /home/ehub-deployer/.ssh/authorized_keys
unset EHUB_CD_PUBKEY
sudo ssh-keygen -l -f /home/ehub-deployer/.ssh/authorized_keys
```

Lệnh cuối phải in fingerprint của đúng khóa vừa tạo. Nếu máy chủ SSH đang giới hạn
người dùng, kiểm tra thêm:

```bash
sudo sshd -T | grep -i -E '^(allowusers|allowgroups|passwordauthentication|pubkeyauthentication)'
```

Nếu có `allowusers` hoặc `allowgroups`, phải thêm `ehub-deployer`. Không bật
`passwordauthentication` chỉ để thử.

### 3.4 Kiểm tra từ Windows (chưa deploy thật)

```powershell
# Không có lệnh: phải bị từ chối, in "must be a full 40-character...", mã thoát 64
ssh -i "$env:USERPROFILE\.ssh\ehub_cd_staging" ehub-deployer@$EhubVpsHost
$LASTEXITCODE

# Lệnh khác: cũng phải bị từ chối với mã thoát 64
ssh -i "$env:USERPROFILE\.ssh\ehub_cd_staging" ehub-deployer@$EhubVpsHost "ls"
$LASTEXITCODE

```

Trên VPS, xác nhận khóa đã bị hạn chế (dòng phải bắt đầu bằng `restrict,command=`):

```bash
sudo cut -c1-80 /home/ehub-deployer/.ssh/authorized_keys
```

### 3.5 Chạy thử một lần deploy thật

1. Ghi SHA staging đang chạy: `git -C /opt/ehub/staging/app rev-parse HEAD`.
2. Lấy full SHA của commit `develop` muốn deploy (đã nghiệm thu, đã có trên GitHub).
3. Chạy ngoài giờ demo, từ Windows:

```powershell
ssh -i "$env:USERPROFILE\.ssh\ehub_cd_staging" ehub-deployer@$EhubVpsHost "<FULL_40_CHAR_SHA>"
```

4. Dòng cuối phải là `EHUB_DEPLOY_RESULT=success sha=... previous=...`. Mở staging và
   chạy smoke test thủ công như `STAGING_VPS.md` mục 7.

Nên mở thêm phiên SSH thứ hai bằng `ehubadmin` để xem `free -h` và `docker stats --no-stream`
trong lúc build: việc build dùng chung CPU/RAM với production.

## 4. Vận hành

- Nhật ký tóm tắt: `sudo tail -n 20 /var/log/ehub-staging-deploy.log`
  (mỗi lần có `START`, rồi `SUCCESS` hoặc `FAILED stage=...`).
- Nhật ký chi tiết từng lần: `sudo ls -t /var/log/ehub-staging-deploy.d | head` rồi
  `sudo less <tên file>`. Tự xóa file cũ hơn 30 ngày.
- Khi lỗi: xem `FAILED stage=`. Nếu `migrate` hoặc sau đó lỗi, **không** chạy lại mù quáng:
  kiểm tra log migration và dùng backup staging mới nhất do script vừa tạo
  (`/opt/ehub/staging/backups`). Không sửa schema thủ công.
- Cập nhật script sau khi PR được merge: lặp lại 3.2 và 3.3 phần `install` (chỉ hai
  file script). Không cần tạo lại user, sudoers hay key.
- Thu hồi quyền deploy: xóa dòng trong `/home/ehub-deployer/.ssh/authorized_keys`
  (hoặc xóa file) và rotate key mới nếu nghi ngờ lộ khóa riêng.

## 5. Giới hạn đã biết

- Script triển khai build và chạy mã của commit `develop` bằng quyền Docker của root,
  nên độ an toàn phụ thuộc vào việc PR vào `develop` được duyệt. Forced command chỉ
  bảo vệ khỏi người lấy được key, không thay thế việc review code.
- Bản backup chạy bằng `scripts/staging/backup-postgres.sh` của checkout **hiện tại**
  (trước khi thay bằng commit mới), đúng như thiết kế.
- Build trên VPS chưa được giới hạn tài nguyên riêng. Cần đo tải production trong lúc
  build (Giai đoạn 3 của kế hoạch) trước khi coi đây là cấu hình lâu dài.
- Script chưa kiểm tra mô hình `bge-m3` hoạt động thật, chỉ bảo đảm model có trong
  `ollama list` (giống runbook thủ công).

## 6. GitHub Actions: deploy tự động (Giai đoạn 2)

Workflow: `.github/workflows/deploy-staging.yml`. Nó không checkout code; chỉ chờ CI, rồi
SSH vào VPS bằng key `ehub_cd_staging` và gửi đúng một SHA.

### 6.1 Khi nào nó chạy

| Tình huống | Hành vi |
| --- | --- |
| Push vào `develop` có thay đổi trong `backend/`, `frontend/`, `docker/`, `docker-compose.staging.yml` hoặc `scripts/staging/backup-postgres.sh` | Chạy nếu biến `STAGING_AUTO_DEPLOY` bằng `true` |
| Push chỉ sửa docs hoặc file khác | Không chạy |
| Run thủ công (Actions → Deploy Staging → Run workflow, chọn nhánh `develop`) | Luôn chạy; ô `sha` để trống = đầu nhánh `develop` |
| Run thủ công từ nhánh khác `develop` | Bị bỏ qua |

Trước khi deploy, workflow chờ tối đa 40 phút cho hai check **Build and Test Backend** và
**Type Check and Build Frontend** của đúng commit đó. Check lỗi, bị hủy hoặc không xuất
hiện trong 3 phút thì không deploy. Nếu đổi tên job CI, phải sửa tên trong workflow.
Ô `skip_ci_check` chỉ dùng để deploy lại một commit cũ đã biết là tốt.

Các lần chạy xếp hàng một lần một (`concurrency`). Nếu có nhiều push liên tiếp, lần chờ
cũ hơn bị thay bằng lần mới hơn nên cuối cùng staging luôn là commit mới nhất.

### 6.2 Thiết lập (người có quyền Admin repo)

1. **Environment:** Settings → Environments → New environment → tên `staging`. Nếu gói
   GitHub cho phép, mở Deployment branches and tags → Selected branches → thêm `develop`.
2. **Secrets của environment `staging`** (Add environment secret):

   | Tên | Giá trị |
   | --- | --- |
   | `STAGING_SSH_PRIVATE_KEY` | Toàn bộ nội dung file `ehub_cd_staging` (khóa riêng, gồm dòng BEGIN và END) |
   | `STAGING_SSH_HOST` | IP hoặc hostname của VPS |
   | `STAGING_SSH_KNOWN_HOSTS` | Một dòng khóa host của VPS, xem bên dưới |
   | `DISCORD_DEPLOY_WEBHOOK` | URL webhook của kênh `#ehub-deploy` |

   Copy khóa riêng vào clipboard mà không hiển thị lên màn hình:

   ```powershell
   Get-Content -Raw "$env:USERPROFILE\.ssh\ehub_cd_staging" | Set-Clipboard
   ```

   Lấy khóa host trên VPS (thay `<IP>` bằng đúng giá trị của `STAGING_SSH_HOST`):

   ```bash
   echo "<IP> $(awk '{print $1" "$2}' /etc/ssh/ssh_host_ed25519_key.pub)"
   ```

   Dán nguyên dòng in ra (dạng `<IP> ssh-ed25519 AAAA...`). Workflow đặt
   `StrictHostKeyChecking=yes`, nên khóa host sai sẽ bị từ chối thay vì tin tưởng ngầm.
3. **Biến công tắc:** Settings → Secrets and variables → Actions → tab **Variables** →
   New repository variable → `STAGING_AUTO_DEPLOY`. Mặc định (chưa tạo, hoặc khác `true`) là
   **không tự deploy**. Chỉ đặt `true` sau khi lần chạy thủ công đầu tiên thành công.

### 6.3 Đóng băng staging khi nghiệm thu

- Đóng băng: đổi `STAGING_AUTO_DEPLOY` thành `false` (push vào `develop` sẽ không deploy).
- Mở lại: đổi thành `true`. Các commit đã đẩy trong lúc đóng băng **không** được deploy bù;
  chạy thủ công một lần để cập nhật lên đầu `develop`.
- Chạy thủ công vẫn dùng được khi đang đóng băng.

### 6.4 Đọc kết quả

Trạng thái có trong tab Actions, trong phần Summary của lần chạy, và trong Discord
`#ehub-deploy` (✅ thành công, ❌ lỗi kèm tên bước, ⛔ không deploy vì CI, ⚠️ bị hủy). Nếu
Discord báo mất kết nối: deploy có thể vẫn đang chạy trên VPS; xem
`/var/log/ehub-staging-deploy.log` trước khi chạy lại.
