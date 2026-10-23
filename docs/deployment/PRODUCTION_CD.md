# Deploy production bằng workflow (bán tự động)

Tài liệu này bổ sung cho [STAGING_CD.md](STAGING_CD.md): cùng cơ chế SSH, cùng user
`ehub-deployer`, nhưng key, script và workflow riêng cho production. Production **không
bao giờ tự deploy**: người có quyền bấm Run workflow và nhập đủ SHA. Quy trình thủ công trong
[PRODUCTION_VPS.md](PRODUCTION_VPS.md) vẫn dùng được khi cần xử lý sự cố.

## 1. Cách hoạt động

```text
Run workflow "Deploy Production" (nhánh main, SHA đầy đủ)
   kiểm tra người chạy (ALLOWED_ACTORS) và nhánh main
   SSH bằng key production -> deploy-production-entry.sh -> sudo deploy-production.sh <SHA>
      1. SHA thuộc origin/main
      2. nội dung (git tree) trùng một commit đã deploy thành công lên staging
      3. backup DB + upload R2 (backup-and-upload.sh production run), bắt buộc có file mới
      4. checkout, IMAGE_TAG (12 ký tự đầu của SHA), build backend/frontend
      5. postgres/ollama giữ nguyên (--no-recreate), migrate, tạo lại backend/frontend
      6. health check 127.0.0.1:3000, xác nhận image đang chạy đúng tag
```

Khác với staging:

- **Không tạo lại database và Ollama.** Chỉ backend và frontend được tạo lại, nên mỗi lần
  release chỉ có gián đoạn khoảng vài chục giây khi hai container này khởi động lại. Nâng cấp
  image Postgres hoặc đổi định nghĩa Compose của `postgres`/`ollama` là thao tác thủ công
  riêng, theo runbook.
- **Backup bắt buộc.** Script chạy `backup-and-upload.sh production run` và kiểm tra có file
  `ehub-postgres-*.dump` mới; backup lỗi hoặc bị bỏ qua thì dừng, chưa đổi gì.
- **Không tự rollback.** Migration có thể đã chạy; xem mục 5.
- Dùng chung khóa với deploy staging: nếu staging đang build, lần deploy production chờ tối
  đa 20 phút (và ngược lại), vì hai môi trường chung CPU/RAM.

## 2. Cài đặt trên VPS (làm một lần)

### 2.1 Tạo key riêng cho production (Windows)

```powershell
ssh-keygen -t ed25519 -C "ehub-cd-production" -f "$env:USERPROFILE\.ssh\ehub_cd_production"
Get-Content "$env:USERPROFILE\.ssh\ehub_cd_production.pub"
```

Nhấn Enter hai lần để **để trống passphrase**. Đây phải là cặp key **mới**, khác key của
staging và của `ehubadmin`. Chỉ khóa công khai (`.pub`) được sao chép; khóa riêng chỉ dán
vào GitHub Secrets.

### 2.2 Gửi script lên VPS (từ thư mục gốc repo)

Script staging cũng được gửi lại vì khóa dùng chung đã thay đổi:

```powershell
$EhubVpsHost = Read-Host "VPS IP or hostname"
scp -i "$env:USERPROFILE\.ssh\ehub_vps_ed25519" `
  scripts\production\deploy-production.sh scripts\production\deploy-production-entry.sh `
  scripts\staging\deploy-staging.sh `
  "ehubadmin@${EhubVpsHost}:/tmp/"
```

### 2.3 Cài script, sudoers và key (VPS, bằng `ehubadmin`)

```bash
grep -c $'\r' /tmp/deploy-production.sh /tmp/deploy-production-entry.sh /tmp/deploy-staging.sh
bash -n /tmp/deploy-production.sh && bash -n /tmp/deploy-production-entry.sh && bash -n /tmp/deploy-staging.sh && echo syntax-ok

sudo install -m 0755 -o root -g root /tmp/deploy-production.sh /opt/ehub/deploy/deploy-production.sh
sudo install -m 0755 -o root -g root /tmp/deploy-production-entry.sh /opt/ehub/deploy/deploy-production-entry.sh
sudo install -m 0755 -o root -g root /tmp/deploy-staging.sh /opt/ehub/deploy/deploy-staging.sh
rm -f /tmp/deploy-production.sh /tmp/deploy-production-entry.sh /tmp/deploy-staging.sh
ls -l /opt/ehub/deploy
```

Ba lệnh `grep -c` phải in `0`. Không cài lại script staging thì hai script dùng hai khóa
khác nhau và có thể chạy đồng thời.

Cho phép `ehub-deployer` chạy thêm đúng một lệnh nữa (ghi lại cả hai dòng, không nối thêm):

```bash
printf '%s\n' \
  'ehub-deployer ALL=(root) NOPASSWD: /opt/ehub/deploy/deploy-staging.sh' \
  'ehub-deployer ALL=(root) NOPASSWD: /opt/ehub/deploy/deploy-production.sh' \
  | sudo tee /etc/sudoers.d/ehub-deployer > /dev/null
sudo chmod 0440 /etc/sudoers.d/ehub-deployer
sudo visudo -cf /etc/sudoers.d/ehub-deployer
sudo -l -U ehub-deployer
```

`visudo` phải báo `parsed OK` và `sudo -l` chỉ liệt kê đúng hai lệnh trên. Nếu `visudo`
báo lỗi, xóa file ngay (`sudo rm /etc/sudoers.d/ehub-deployer`).

Thêm khóa công khai thứ hai (**thêm vào cuối**, giữ nguyên dòng của staging):

```bash
read -r -p "Dán MỘT dòng khóa công khai ehub_cd_production.pub: " EHUB_CD_PUBKEY
printf 'restrict,command="/opt/ehub/deploy/deploy-production-entry.sh" %s\n' "$EHUB_CD_PUBKEY" \
  | sudo tee -a /home/ehub-deployer/.ssh/authorized_keys > /dev/null
unset EHUB_CD_PUBKEY
sudo ssh-keygen -l -f /home/ehub-deployer/.ssh/authorized_keys
sudo cut -c1-90 /home/ehub-deployer/.ssh/authorized_keys
```

`ssh-keygen -l` phải in **hai** dòng (một cho `ehub-cd-staging`, một cho
`ehub-cd-production`), và `cut` phải cho thấy mỗi dòng bắt đầu bằng `restrict,command=` với
đúng script entry tương ứng.

### 2.4 Kiểm tra từ Windows (chưa deploy gì)

```powershell
# Không có lệnh, hoặc lệnh khác: phải bị từ chối (mã 64)
ssh -i "$env:USERPROFILE\.ssh\ehub_cd_production" ehub-deployer@$EhubVpsHost
$LASTEXITCODE
ssh -i "$env:USERPROFILE\.ssh\ehub_cd_production" ehub-deployer@$EhubVpsHost "ls"
$LASTEXITCODE

# Diễn tập toàn bộ đường đi (SSH -> sudo -> script) mà không đổi gì: SHA không tồn tại.
ssh -i "$env:USERPROFILE\.ssh\ehub_cd_production" ehub-deployer@$EhubVpsHost "0000000000000000000000000000000000000000"
$LASTEXITCODE
```

Hai lệnh đầu in `The first argument must be a full 40-character hexadecimal commit SHA.`
và mã `64`. Lệnh thứ ba phải dừng với `EHUB_DEPLOY_RESULT=failed stage=verify-commit` (mã
khác 0): script đã chạy tới bước kiểm tra commit nhưng chưa backup, chưa checkout, chưa
đụng vào production. Kiểm tra thêm `git -C /opt/ehub/app rev-parse HEAD` không đổi.

## 3. Cấu hình GitHub (người có quyền Admin repo)

1. Settings → Environments → New environment → `production`. Nếu gói GitHub cho phép, đặt
   Deployment branches → Selected branches → `main`.
2. Secrets của environment `production`:

   | Tên | Giá trị |
   | --- | --- |
   | `PRODUCTION_SSH_PRIVATE_KEY` | Toàn bộ file `ehub_cd_production` (khóa riêng) |
   | `PRODUCTION_SSH_HOST` | IP hoặc hostname của VPS |
   | `PRODUCTION_SSH_KNOWN_HOSTS` | `<IP> ssh-ed25519 AAAA...` (như mục 6.2 của STAGING_CD.md) |
   | `DISCORD_DEPLOY_WEBHOOK` | URL webhook của `#ehub-deploy` (cùng giá trị với staging) |

   Copy khóa riêng không hiển thị: `Get-Content -Raw "$env:USERPROFILE\.ssh\ehub_cd_production" | Set-Clipboard`.
3. Người được phép deploy nằm trong `.github/workflows/deploy-production.yml`, biến
   `ALLOWED_ACTORS` (hiện là `phanhoaian1203`, nhiều người cách nhau bằng khoảng trắng).
   Đây là chốt chống bấm nhầm trong nhóm có quyền Write, **không phải** rào bảo mật duy nhất:
   người sửa workflow ở nhánh khác vẫn bị chặn ở phía VPS (key chỉ chạy một script, script
   chỉ nhận SHA thuộc `origin/main` đã chạy trên staging).

## 4. Release production

1. Staging đã nghiệm thu, release PR vào `main` đã merge (xem `RELEASE_PROCESS.md`).
2. Lấy SHA đầy đủ của commit merge trên `main`:

   ```powershell
   git fetch origin
   git rev-parse origin/main
   ```
3. Chọn lúc ít người dùng (backend/frontend khởi động lại vài chục giây). Có thể kiểm tra
   trước cấu hình backup: trên VPS `sudo bash /opt/ehub/app/scripts/production/backup-and-upload.sh production check`.
4. Actions → **Deploy Production** → Run workflow, **chọn nhánh `main`**, dán SHA, để
   `skip_staging_gate` tắt → Run.
5. Theo dõi trong tab Actions và Discord `#ehub-deploy`. Thành công khi có ✅ kèm SHA
   release trước (ghi lại làm điểm rollback). Sau đó smoke test thủ công như
   `PRODUCTION_VPS.md` mục 10 (đăng nhập, upload, gợi ý mentor).

Thông báo lỗi tương ứng bước dừng:

| `stage` | Ý nghĩa | Production đã bị đổi chưa? |
| --- | --- | --- |
| `preconditions`, `verify-commit`, `staging-gate` | SHA sai, không thuộc `main`, hoặc nội dung chưa chạy trên staging | Chưa |
| `backup` | Backup hoặc upload R2 lỗi, hoặc không có file mới | Chưa |
| `checkout`, `set-image-tag`, `validate-compose`, `build` | Lỗi trước khi dừng dịch vụ | Chưa chạy dịch vụ mới; code đã checkout sang SHA mới |
| `start-dependencies`, `embedding-model` | Postgres/Ollama không lên | Dịch vụ cũ vẫn chạy |
| `migrate` | Migration lỗi | **Có thể đã áp dụng một phần.** Dừng, đọc log, không sửa DB thủ công |
| `start-application`, `health-check`, `verify-running-images` | Migration đã chạy, ứng dụng mới lỗi | **Có.** Xem mục 5 |

## 5. Hotfix và rollback

- **Hotfix** (commit trên `main` chưa chạy trên staging) và **rollback** (deploy lại SHA
  trước) chạy cùng workflow với ô **`skip_staging_gate` bật**. Script vẫn yêu cầu SHA thuộc
  `origin/main` và vẫn backup trước khi đổi. Discord và log ghi `gate=skipped`.
- Rollback code chỉ an toàn khi bản lỗi **không** có migration không tương thích ngược. Nếu
  có, **không** chạy code cũ lên schema mới: làm theo mục 8 của `PRODUCTION_VPS.md`
  (sửa tiến hoặc khôi phục backup, cần người thứ hai kiểm tra).
- Sau hotfix, mở PR `main` vào `develop` (xem `RELEASE_PROCESS.md` mục 5).

## 6. Dấu vết và nhật ký

- Discord `#ehub-deploy` và phần Summary của lần chạy: SHA mới, SHA trước, người chạy, cổng
  staging (`staging-match:<sha>` hoặc `skipped`).
- Trên VPS: `sudo tail -n 20 /var/log/ehub-production-deploy.log` (tóm tắt) và
  `/var/log/ehub-production-deploy.d/` (log chi tiết từng lần, giữ 30 ngày).
- Các thông tin trên thay cho việc ghi tay vào bảng ở mục 8 của `RELEASE_PROCESS.md`; vẫn
  có thể ghi bảng đó nếu nhóm muốn báo cáo.

## 7. Giới hạn đã biết

- Cổng staging dựa trên `/var/log/ehub-staging-deploy.log` (chỉ ghi các lần deploy bằng
  script) cộng commit staging đang chạy. Bản chỉ deploy tay lên staging trước đây không có
  trong log; nếu cần, dùng `skip_staging_gate` có chủ đích.
- Ghi chú về bằng chứng kiểm thử: logic script và workflow được kiểm tra bằng `docker`,
  `ssh`, `curl`, `gh` giả; chưa có lần chạy production thật cho tới khi nhóm release lần đầu.
- Không có blue-green: gián đoạn ngắn khi backend/frontend khởi động lại là chấp nhận được
  ở quy mô pilot hiện tại.

## 8. Công tắc provider AI (Ollama hoặc Hybrid)

Giống staging: dòng `MENTOR_MATCHING_PROVIDER=Ollama|Hybrid` trong `/opt/ehub/app/.env.production`
(không có dòng nghĩa là `Ollama`). **Production giữ `Ollama`** cho tới khi pilot Hybrid trên
staging được chấp nhận ([HYBRID_MENTOR_VPS.md](HYBRID_MENTOR_VPS.md) mục 7). Khi bật `Hybrid`:

- Script từ chối nếu RAM khả dụng dưới 1536 MB (VPS không có swap), trước cả bước backup.
- `postgres` và `ollama` vẫn **không bao giờ** bị build hay tạo lại. Bước migrate và bước
  khởi động ứng dụng dùng `--no-deps`, vì Compose tạo lại container database nếu định nghĩa
  của nó lệch so với container đang chạy (điều này đã được kiểm chứng bằng Docker Compose thật).
- `mentor-semantic` được gỡ và tạo lại cùng backend ở mỗi lần deploy; sức khỏe, revision model
  và image của nó được kiểm tra riêng. Thất bại ở các kiểm tra này làm workflow đỏ.
- Quay lui: đặt `Ollama` (hoặc xóa dòng), chạy Deploy Production với SHA đang chạy và tích
  `skip_staging_gate` chỉ khi SHA đó chưa từng chạy trên staging. Script gỡ container
  `mentor-semantic` còn lại.
- Discord ghi "AI provider: ..." trong tin thành công và thất bại.
- Sau khi sửa script này, cài lại bản mới vào `/opt/ehub/deploy/` (mục 2.3).
