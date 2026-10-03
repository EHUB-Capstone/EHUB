# EHUB staging trên cùng VPS với production

Hướng dẫn này triển khai `https://staging.e-hub.com.vn` để nhóm kiểm thử trước
khi phát hành lên `https://e-hub.com.vn`. Các lệnh phải chạy từng bước; nếu một
bước lỗi, xử lý trước khi chuyển bước. Không chạy toàn bộ tài liệu như một script.
Không dùng cấu hình Vercel/Render/Neon trong `STAGING_ENVIRONMENT.md` cho mô hình này.

## 1. Cấu trúc và các file đã chuẩn bị

```text
Browser -> Cloudflare -> host Nginx :443 -> staging frontend 127.0.0.1:3001
        -> frontend Nginx /api -> staging backend :8080 -> staging PostgreSQL :5432
```

| Thành phần | Production | Staging |
| --- | --- | --- |
| Source trên VPS | `/opt/ehub/app` | `/opt/ehub/staging/app` |
| Compose project | `ehub-production` | `ehub-staging` |
| Frontend loopback | `127.0.0.1:3000` | `127.0.0.1:3001` |
| Backend/frontend images | `ehub-backend`, `ehub-frontend` | `ehub-staging-backend`, `ehub-staging-frontend` |
| PostgreSQL volume | `ehub-production-postgres-data` | `ehub-staging-postgres-data` |
| Docker network | `ehub-production-private` | `ehub-staging-private` |
| Env thật | `.env.production` | `.env.staging` |
| Backup | `/opt/ehub/backups` | `/opt/ehub/staging/backups` |

Project, tên image, volume, network và cổng staging được cố định trong Compose.
Không có biến `BACKEND_IMAGE`, `FRONTEND_IMAGE` hoặc `FRONTEND_HOST_PORT` để
ghi đè chúng. Backend và PostgreSQL không publish cổng host. PostgreSQL 18
mount volume tại `/var/lib/postgresql` giống production.

| File | Vai trò |
| --- | --- |
| `docker-compose.staging.yml` | Ba dịch vụ staging, healthcheck, restart và giới hạn log |
| `.env.staging.example` | Mẫu cấu hình an toàn; env thật được tạo trên VPS |
| `deploy/nginx/staging.e-hub.com.vn.bootstrap.conf` | HTTP ACME và thông báo khởi tạo, chưa phục vụ ứng dụng |
| `deploy/nginx/staging.e-hub.com.vn.conf` | HTTPS proxy tới staging, log riêng, noindex và health nội bộ |
| `scripts/staging/backup-postgres.sh` | Dump PostgreSQL staging, kiểm tra archive, checksum, retention |

Staging dùng lại Dockerfile và frontend Nginx hiện có. Không cần thêm API
subdomain: frontend gọi `/api` trên cùng hostname staging. Giữ API mocks và
Socket.IO transport tắt như cấu hình production hiện tại. Backend vẫn chạy
background jobs, vì vậy phải tách cả database và storage bên ngoài.

## 2. Hoàn tất source trên Windows

Nhánh triển khai: `feature/staging-vps`. Review các file và diff, commit/push
theo quy trình của nhóm, tạo PR vào `develop`, chờ review và CI đạt rồi merge.
Không deploy một checkout chứa thay đổi chưa commit.

CI hiện có được mở rộng để kiểm tra Compose staging, Bash và cả hai site Nginx
được nạp cùng nhau. Các giá trị `.example` chỉ để kiểm tra cấu trúc; không dùng
chúng để khởi động ứng dụng. CI không SSH, publish image hoặc deploy VPS.

Ghi lại full SHA của commit đã merge. Hai môi trường sẽ deploy bằng SHA cụ thể;
frontend được build riêng do Google Client ID và feature flags được nhúng lúc build.

## 3. Chuẩn bị thủ công các dịch vụ bên ngoài

Chuẩn bị credential riêng tư; không gửi `.env`, password, key hoặc token vào
chat và không commit chúng. Không sao chép dữ liệu cá nhân production làm fixture.

### Google Identity Services

1. Tạo OAuth client loại **Web application** dành cho staging.
2. Thêm Authorized JavaScript origin `https://staging.e-hub.com.vn`, không kèm path.
3. Nếu OAuth project yêu cầu test users, thêm tài khoản Google của nhóm.
4. Đặt Client ID đó tại `GOOGLE_CLIENT_ID` trong env staging; backend và frontend
   build cùng dùng giá trị này. Đổi Client ID cần build lại frontend.

### SMTP và Brevo

Staging vẫn yêu cầu `Email:Provider=Smtp` với cấu hình SMTP hoạt động. Dùng
tài khoản gửi staging và địa chỉ test của nhóm. Nếu dùng SMTP sandbox, xác nhận
OTP trong inbox sandbox; đó chưa phải kiểm tra giao nhận tới Gmail thật.
Credential khác nhưng cùng tài khoản provider vẫn có thể chia sẻ quota.

Mẫu mặc định `EMAIL_ENABLE_BREVO_FALLBACK=false`. Để kiểm thử fallback, điền
`BREVO_FROM_EMAIL`, `BREVO_SMTP_USERNAME`, `BREVO_SMTP_PASSWORD` riêng cho staging
rồi bật `true`. Sender phải được xác thực; giữ host `smtp-relay.brevo.com`,
port `587`, `StartTls`. Compose không thể xác thực credential hoặc điều kiện
quota; cần smoke test gửi email và kiểm tra logs.

### Cloudinary

Dùng cloud/product environment riêng và đúng bộ cloud name/API key/API secret
của staging. Chỉ dùng folder riêng với credential production không cung cấp
cùng mức tách biệt. Không đặt API secret vào biến `VITE_*`.

### Cloudflare R2

1. Tạo bucket `ehub-submissions-staging` (hoặc tên staging riêng), giữ private.
2. Tạo token **Object Read & Write**, scoped chỉ tới bucket staging.
3. Điền account ID, access key ID, secret và tên bucket trong env staging.
4. Trong bucket Settings -> CORS Policy, đặt:

```json
[
  {
    "AllowedOrigins": ["https://staging.e-hub.com.vn"],
    "AllowedMethods": ["PUT"],
    "AllowedHeaders": ["Content-Type"],
    "ExposeHeaders": ["ETag"],
    "MaxAgeSeconds": 3600
  }
]
```

Luồng upload tài liệu R2 đi trực tiếp từ browser; không tăng giới hạn body
Nginx 12 MB cho tính năng này. Downloads dùng presigned GET navigation theo
luồng hiện tại. Kiểm tra upload/preview/download sau khi website có HTTPS.

## 4. Đăng nhập và ghi nhận production

**PowerShell Windows**: dùng khóa SSH/IP VPS hiện tại. Các giá trị trong prompt
được nhập trên máy của bạn, không đưa passphrase vào câu lệnh:

```powershell
$EhubVpsHost = Read-Host "VPS IP or hostname"
ssh -i "$env:USERPROFILE\.ssh\ehub_vps_ed25519" "ehubadmin@$EhubVpsHost"
```

**VPS Bash**, sau khi đăng nhập:

```bash
cd /opt/ehub/app
git rev-parse HEAD
sudo docker compose --env-file .env.production -f docker-compose.production.yml ps
sudo nginx -t
sudo ufw status verbose
sudo ss -lntup
bash scripts/production/backup-postgres.sh
```

Ghi lại SHA, trạng thái production và tên backup. Chép dump/checksum production
ra ngoài VPS theo [PRODUCTION_VPS.md](PRODUCTION_VPS.md#7-postgresql-backup-outside-the-vps).
VPS đã có Git, Docker Compose, OpenSSL, Nginx và Certbot; không cài lại hoặc
thay đổi firewall chung trong bước tạo staging nếu không cần.

## 5. Tạo checkout staging và chọn release

**VPS Bash**, thực hiện clone chỉ lần đầu, khi checkout staging chưa tồn tại:

```bash
sudo install -d -m 0750 -o ehubadmin -g ehubadmin /opt/ehub/staging
sudo install -d -m 0750 -o ehubadmin -g ehubadmin /opt/ehub/staging/backups
git clone https://github.com/EHUB-Capstone/EHUB.git /opt/ehub/staging/app
cd /opt/ehub/staging/app
git fetch origin develop
read -r -p "Paste full reviewed commit SHA: " EHUB_STAGING_SHA
```

Kiểm tra input và commit. Nếu bất kỳ lệnh nào không thành công thì dừng lại:

```bash
[[ "$EHUB_STAGING_SHA" =~ ^[0-9a-fA-F]{40}$ ]]
git show --no-patch --format='%H %s' "$EHUB_STAGING_SHA"
git merge-base --is-ancestor "$EHUB_STAGING_SHA" origin/develop
git checkout --detach "$EHUB_STAGING_SHA"
git rev-parse HEAD
```

Full SHA phải khớp commit đã review. Private repository cần quyền Git đọc đã
thiết lập trên VPS; không nhúng access token trong URL. Production checkout
vẫn ở `/opt/ehub/app`.

## 6. Điền cấu hình thật trên VPS

```bash
cd /opt/ehub/staging/app
cp .env.staging.example .env.staging
chmod 600 .env.staging
nano .env.staging
```

Chỉ copy mẫu lần đầu; lần sau mở env hiện có để giữ credential. Đặt `IMAGE_TAG`
bằng full SHA của `git rev-parse HEAD`, thay mọi placeholder bằng giá trị riêng
của staging, giữ database/user staging, JWT issuer/audience và storage staging.

Tạo PostgreSQL password và mật khẩu admin bằng hai giá trị ngẫu nhiên riêng
(ví dụ `openssl rand -hex 32` cho mỗi giá trị); tạo JWT và OTP bằng hai giá trị
khác nhau (`openssl rand -hex 64` cho mỗi giá trị). Lưu mật khẩu admin riêng tư.
Không sao chép env production, không chạy `source .env.staging`, không in env
hoặc config đã expand vào terminal/chat. Trong nano: Ctrl+O, Enter để lưu,
Ctrl+X để thoát.

Khai báo mảng Compose trong phiên SSH hiện tại:

```bash
STAGING_COMPOSE=(
  sudo docker compose
  --project-name ehub-staging
  --env-file /opt/ehub/staging/app/.env.staging
  -f /opt/ehub/staging/app/docker-compose.staging.yml
)
"${STAGING_COMPOSE[@]}" config --quiet
```

Không có output và exit code 0 là kiểm tra cấu trúc đạt, chưa chứng minh các
dịch vụ bên ngoài hoạt động. Nếu mở phiên SSH mới, khai báo lại mảng này.
Không sử dụng mảng `COMPOSE` còn sót từ thao tác production.

## 7. Build, migration và khởi động

Build lần lượt ngoài thời gian demo; giữ phiên SSH thứ hai để xem `free -h`,
`docker stats --no-stream`, `df -h`. Giới hạn container không giới hạn build.

```bash
"${STAGING_COMPOSE[@]}" build backend
"${STAGING_COMPOSE[@]}" build frontend
"${STAGING_COMPOSE[@]}" up -d postgres
"${STAGING_COMPOSE[@]}" ps
"${STAGING_COMPOSE[@]}" run --rm backend --initialize-database
```

Chờ PostgreSQL staging healthy. Initializer phải kết thúc thành công; nó áp
dụng migrations đã commit, seed danh mục và admin đầu tiên, rồi thoát. Lỗi
migration phải dừng deployment. Không đổi schema thủ công và không chạy
initializer như command startup lâu dài của backend.

Sau khi thành công, mở `.env.staging` và chỉ xóa giá trị sau
`ADMIN_SEED_PASSWORD=`. Tài khoản và password hash vẫn nằm trong database.
Tiếp tục:

```bash
"${STAGING_COMPOSE[@]}" config --quiet
"${STAGING_COMPOSE[@]}" up -d backend frontend
"${STAGING_COMPOSE[@]}" ps
```

Ba dịch vụ staging phải healthy. Kiểm tra loopback trước khi mở domain:

```bash
curl --fail --silent --show-error --header 'Host: staging.e-hub.com.vn' --write-out '\nHTTP %{http_code}\n' http://127.0.0.1:3001/healthz
curl --fail --silent --show-error --header 'Host: staging.e-hub.com.vn' --write-out '\nHTTP %{http_code}\n' http://127.0.0.1:3001/health/live
curl --fail --silent --show-error --header 'Host: staging.e-hub.com.vn' --write-out '\nHTTP %{http_code}\n' http://127.0.0.1:3001/health/ready
sudo ss -lntup
```

Mong đợi HTTP 200 và readiness có PostgreSQL healthy. Chỉ cổng 3001 bind
loopback; 5432 và 8080 không được public. Khi lỗi, xem bounded logs:

```bash
"${STAGING_COMPOSE[@]}" logs --tail 100 backend
"${STAGING_COMPOSE[@]}" logs --tail 100 frontend
```

## 8. DNS và HTTP bootstrap

**Cloudflare dashboard**: trong zone `e-hub.com.vn`, thêm A record `staging`
trỏ tới IP VPS, TTL Auto. Không đổi nameserver hoặc bản ghi production.
Với HTTP-01 trực tiếp, để riêng record staging DNS-only trong lúc cấp cert
nếu firewall cho phép truy cập HTTP. Nếu firewall chỉ nhận Cloudflare, giữ
proxy và kiểm tra đường ACME, hoặc dùng DNS-01; không mở rộng firewall một
cách tự động. Không thêm AAAA nếu VPS không phục vụ IPv6 tương ứng.

**VPS Bash**:

```bash
cd /opt/ehub/staging/app
sudo install -d -m 0755 /var/www/certbot
sudo cp deploy/nginx/staging.e-hub.com.vn.bootstrap.conf /etc/nginx/sites-available/staging.e-hub.com.vn
sudo ln -s /etc/nginx/sites-available/staging.e-hub.com.vn /etc/nginx/sites-enabled/staging.e-hub.com.vn
sudo nginx -t
```

Nếu path/symlink đã tồn tại, kiểm tra trước khi thay thế. Chỉ reload sau khi
`nginx -t` thành công:

```bash
sudo systemctl reload nginx
```

**PowerShell Windows**:

```powershell
Resolve-DnsName staging.e-hub.com.vn -Type A
curl.exe --fail --show-error http://staging.e-hub.com.vn/
```

Khi DNS-only, A record trả IP VPS. HTTP root trả `EHUB staging initialization`;
ứng dụng chưa được mở qua HTTP. ACME path trả 404 khi không có challenge là bình thường.

## 9. HTTPS và Cloudflare proxy

**VPS Bash**: cấp certificate riêng, không dùng lại cert chỉ chứa production/www:

```bash
read -r -p "Certificate notification email: " EHUB_STAGING_CERT_EMAIL
sudo certbot certonly --webroot -w /var/www/certbot \
  --cert-name staging.e-hub.com.vn -d staging.e-hub.com.vn \
  --email "$EHUB_STAGING_CERT_EMAIL" --agree-tos \
  --deploy-hook 'nginx -t && systemctl reload nginx'
unset EHUB_STAGING_CERT_EMAIL
sudo certbot certificates
```

Cert phải có hostname staging và đúng lineage `staging.e-hub.com.vn`. Nếu đã
có cert hợp lệ với đúng tên/domains, giữ cert hiện có. Chỉ cài cấu hình HTTPS
sau khi cert và Cloudflare real-IP snippet tồn tại:

```bash
sudo test -f /etc/nginx/snippets/ehub-cloudflare-real-ip.conf
sudo cp deploy/nginx/staging.e-hub.com.vn.conf /etc/nginx/sites-available/staging.e-hub.com.vn
sudo nginx -t
```

Nếu snippet chưa có, dùng `deploy/nginx/cloudflare-real-ip.conf` đã review
theo runbook production; không ghi đè snippet đang dùng nếu không cần.
Chỉ reload khi test thành công:

```bash
sudo systemctl reload nginx
curl --fail --silent --show-error --resolve staging.e-hub.com.vn:443:127.0.0.1 --output /dev/null --write-out 'HTTP %{http_code}\n' https://staging.e-hub.com.vn/
sudo certbot renew --cert-name staging.e-hub.com.vn --dry-run --run-deploy-hooks
systemctl list-timers --all
```

Mong đợi website HTTP 200 và dry-run thành công. Xác nhận timer Certbot đã có
(hoặc cơ chế tương đương của bản cài đặt); deploy hook reload Nginx sau renewal.
Không dùng `curl -k` để bỏ qua chứng chỉ.

**Cloudflare dashboard**: bật Proxied cho record staging và xác nhận SSL/TLS
Full (strict). Không đổi cả zone về Flexible. Mở `https://staging.e-hub.com.vn`
bằng browser để kiểm tra tuyến qua Cloudflare.

Nếu dùng Cloudflare Access, áp policy cho đúng hostname staging và email nhóm;
giữ ACME challenge hoạt động cho renewal và kiểm tra kiểm soát truy cập origin.
Access không tự bảo vệ đường truy cập trực tiếp tới IP origin. Header noindex
chỉ ngăn index, không phải cơ chế phân quyền.

Public `/healthz`, `/health/live`, `/health/ready` chủ động trả 403 qua host
Nginx; dùng loopback checks ở bước 7. Không tắt rule này để làm public curl đạt.
Không cấu hình Cache Everything cho API/auth hoặc trang chứa dữ liệu người dùng.

## 10. Kiểm thử và nghiệm thu

- Admin staging đăng nhập bằng email/mật khẩu bootstrap đã lưu; tạo người dùng test.
- Đăng ký/OTP, resend, password reset và Google Login trên hostname staging.
- Lecturer import danh sách giả; sinh viên được import đăng nhập theo luồng hiện tại.
- Các role, quyền tài nguyên, import/export major và nghiệp vụ đang nghiệm thu.
- Avatar Cloudinary; tài liệu R2 upload, preview PDF/DOCX, download và delete.
- Notification/outbox và cleanup jobs không lỗi lặp lại trong logs.
- Tạo lớp `STAGING-TEST-ONLY`; production không có lớp này, và objects nằm ở storage staging.
- Production vẫn healthy, đăng nhập và các thao tác demo vẫn hoạt động.
- Chạy lại `free -h`, `sudo docker stats --no-stream`, `df -h` trong tác vụ đại diện.

Hai môi trường chung CPU/RAM/disk và điểm lỗi VPS. Dữ liệu container tách biệt
không có nghĩa là chịu tải độc lập. Không load-test trên VPS đang phục vụ lớp.

## 11. Backup staging và chép ra ngoài VPS

**VPS Bash**, chạy dưới user `ehubadmin`, không dùng `sudo bash`:

```bash
cd /opt/ehub/staging/app
bash scripts/staging/backup-postgres.sh
ls -lh /opt/ehub/staging/backups
```

Script dùng project staging cố định, không in password, tạo custom archive và
checksum, chỉ xóa dump staging cũ hơn 14 ngày trong thư mục staging đã kiểm tra.
Có thể điều chỉnh retention 1-90 ngày bằng `EHUB_STAGING_BACKUP_RETENTION_DAYS`
khi chạy script. File mode 600 cho phép user tạo backup chép chúng bằng SCP.

**PowerShell Windows**, trong tab riêng ngoài SSH, nhập đúng tên file script vừa báo:

```powershell
$EhubVpsHost = Read-Host "VPS IP or hostname"
$EhubStagingBackup = Read-Host "Exact staging .dump filename"
$EhubStagingBackupDirectory = Join-Path $env:USERPROFILE "EHUB-Backups\staging"
New-Item -ItemType Directory -Force -Path $EhubStagingBackupDirectory | Out-Null
scp -i "$env:USERPROFILE\.ssh\ehub_vps_ed25519" "ehubadmin@${EhubVpsHost}:/opt/ehub/staging/backups/$EhubStagingBackup" $EhubStagingBackupDirectory
scp -i "$env:USERPROFILE\.ssh\ehub_vps_ed25519" "ehubadmin@${EhubVpsHost}:/opt/ehub/staging/backups/$EhubStagingBackup.sha256" $EhubStagingBackupDirectory
Get-FileHash -Algorithm SHA256 (Join-Path $EhubStagingBackupDirectory $EhubStagingBackup)
Get-Content (Join-Path $EhubStagingBackupDirectory "$EhubStagingBackup.sha256")
```

Hai hash phải khớp. Chỉ công nhận backup sau khi đã thử restore vào database
kiểm tra riêng. Database dump không chứa bytes của Cloudinary/R2; cần giữ
objects tương ứng ở storage. Backup trước mỗi migration/release, ít nhất hằng
ngày khi dữ liệu test cần giữ; lên lịch tự động sau khi đã thử quy trình thủ công.

## 12. Cập nhật và rollback

1. Ghi lại SHA và image tag đang chạy, backup staging và chép offsite.
2. Fetch `develop`, chọn full SHA đã review, xác nhận worktree sạch rồi checkout detached.
3. Cập nhật `IMAGE_TAG` trong env; giữ credential và feature flags đã xác nhận.
4. Khai báo lại `STAGING_COMPOSE` nếu là phiên SSH mới; validate/build lần lượt.
5. Nếu release có migration, dừng staging frontend/backend trước initializer
   để code cũ không ghi vào schema đang đổi. Production vẫn chạy.
6. Chỉ khởi động release mới khi initializer thành công; kiểm tra health và smoke tests.

```bash
"${STAGING_COMPOSE[@]}" config --quiet
"${STAGING_COMPOSE[@]}" build backend
"${STAGING_COMPOSE[@]}" build frontend
"${STAGING_COMPOSE[@]}" stop frontend backend
"${STAGING_COMPOSE[@]}" run --rm backend --initialize-database
"${STAGING_COMPOSE[@]}" up -d --force-recreate backend frontend
"${STAGING_COMPOSE[@]}" ps
```

Chạy từng lệnh và dừng khi lỗi. Không dùng `down -v`, xóa volume hoặc sửa
migration đã merge. Rollback code chỉ khi schema còn tương thích; nếu không,
chọn forward fix hoặc restore dump đã kiểm tra với quyết định rõ về dữ liệu
mất sau backup. Giữ objects R2/Cloudinary khi rollback.

Nhóm ghi nhận SHA đã pass staging, flags và kết quả test. Sau đó triển khai
cùng SHA lên production bằng runbook production với credential/build riêng.
Không copy database staging lên production và không tự động cập nhật production
theo HEAD đang thay đổi của `develop`.

## 13. Điều kiện hoàn thành

1. Ba container staging healthy và đúng image/SHA.
2. HTTPS và tuyến Cloudflare hoạt động; certificate renewal có timer và dry-run đạt.
3. Google/SMTP/Cloudinary/R2 staging hoạt động với dữ liệu test.
4. Database, volume, network, storage và logs tách biệt; production vẫn hoạt động.
5. Backup/checksum đã chép offsite và có restore drill.
6. Nhóm ghi lại người triển khai, SHA, tên backup và kết quả smoke tests.
