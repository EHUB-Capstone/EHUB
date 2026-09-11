# EHUB production VPS deployment

This runbook covers the controlled pilot deployment of EHUB to Ubuntu 24.04 at
`https://e-hub.com.vn`. It intentionally contains no real credential values.

## 1. Architecture and exposure rules

The production request path is:

```text
Browser -> Cloudflare -> host Nginx :443 -> frontend 127.0.0.1:3000
        -> frontend Nginx -> backend :8080 -> PostgreSQL :5432
```

- Host Nginx is the only public application entry point on ports 80 and 443.
- The frontend container binds only to VPS loopback at `127.0.0.1:3000`.
- The backend and PostgreSQL publish no host ports. They communicate only on
  the private Docker network `ehub-production-private`.
- PostgreSQL persists data in the named volume
  `ehub-production-postgres-data`.
- Docker log rotation uses the `local` driver with five 10 MB files per
  container, preventing unbounded container logs on the 50 GB VPS disk.
- Containers use `restart: unless-stopped`; PostgreSQL, backend and frontend
  have dependency/readiness checks.

Docker-published ports can bypass UFW policy. Never add a public `ports`
mapping to PostgreSQL or the backend, and preserve the loopback-only frontend
binding.

## 2. Deployment files

- `docker-compose.production.yml`: production services, network, volume,
  health checks, restart policy and logging limits.
- `.env.production.example`: safe inventory of production settings.
- `deploy/nginx/e-hub.com.vn.bootstrap.conf`: temporary HTTP configuration
  used to obtain the first TLS certificate.
- `deploy/nginx/e-hub.com.vn.conf`: final HTTPS reverse-proxy configuration.
- `deploy/nginx/cloudflare-real-ip.conf`: official Cloudflare proxy ranges used
  to recover the original visitor address safely.
- `scripts/production/backup-postgres.sh`: creates, validates and checksums a
  PostgreSQL dump before it is copied away from the VPS.

The real `/opt/ehub/app/.env.production` file is ignored by Git and must remain
mode 600. Never print it to terminal logs, GitHub Actions or AI chat.

## 3. Required production configuration

Prepare these values without sending them through chat or committing them:

| Group | Required values | Purpose |
| --- | --- | --- |
| Release | `IMAGE_TAG` | Exact reviewed commit SHA used for both images |
| PostgreSQL | database, user, random password | Private application database |
| JWT/OTP | two independent random secrets | Authentication and OTP hashing |
| Google | web client ID | Google Sign-In on the production origin |
| Email | sender, SMTP host/user/app password | OTP and password-reset email |
| Cloudinary | cloud name, API key, API secret | Avatar and project file storage |
| Bootstrap admin | email, one-time password, name | First administrator only |
| Feature switches | explicit true/false values | Pages included in the pilot build |

Google Identity Services must allow the production origin
`https://e-hub.com.vn`. The `www` hostname redirects to the canonical apex
domain and should not be used as the application origin.

## 4. Prepare an approved release on the VPS

Do this only after the deployment changes pass Pull Request review and CI.
Run as `ehubadmin`:

```bash
sudo apt update
sudo apt install -y git openssl nginx certbot
sudo install -d -m 0750 -o ehubadmin -g ehubadmin /opt/ehub
sudo install -d -m 0750 -o ehubadmin -g ehubadmin /opt/ehub/backups
git clone https://github.com/EHUB-Capstone/EHUB.git /opt/ehub/app
cd /opt/ehub/app
```

Replace `REVIEWED_COMMIT_SHA` with the exact 40-character SHA shown in the
approved Pull Request, then verify that the checkout prints the same value:

```bash
git checkout --detach REVIEWED_COMMIT_SHA
git rev-parse HEAD
```

Do not deploy a moving branch such as `develop` directly. A detached reviewed
SHA makes the deployed source and rollback target unambiguous.

Create the VPS-only environment file:

```bash
cp .env.production.example .env.production
chmod 600 .env.production
nano .env.production
```

Generate four independent values and copy each result directly into the
appropriate field. Do not reuse one value for multiple secrets:

```bash
openssl rand -hex 32
openssl rand -hex 64
openssl rand -hex 64
openssl rand -hex 32
```

Use them respectively for `POSTGRES_PASSWORD`, `JWT_SECRET`,
`REGISTRATION_OTP_HASH_KEY`, and the one-time `ADMIN_SEED_PASSWORD`. Set
`IMAGE_TAG` to the reviewed short SHA printed by:

```bash
git rev-parse --short=12 HEAD
```

Validate interpolation without rendering the completed configuration:

```bash
sudo docker compose --env-file .env.production -f docker-compose.production.yml config --quiet
```

Do not use `docker compose config` without `--quiet`, because the expanded
output contains secrets.

## 5. Build, migrate and initialize

For the first pilot, reviewed source is built directly on the VPS:

```bash
sudo docker compose --env-file .env.production -f docker-compose.production.yml build
sudo docker compose --env-file .env.production -f docker-compose.production.yml up -d postgres
sudo docker compose --env-file .env.production -f docker-compose.production.yml run --rm backend --initialize-database
```

The one-time initializer applies committed EF Core migrations and idempotent
reference-data seeders, then exits. Never make it the backend's normal startup
command. A migration failure must stop the release; do not edit the production
schema manually in DBeaver.

After successful initialization, remove only the value after
`ADMIN_SEED_PASSWORD=` from `.env.production`. The one-time container has
already exited, so no running service needs that credential. Then run:

```bash
sudo docker compose --env-file .env.production -f docker-compose.production.yml config --quiet
sudo docker compose --env-file .env.production -f docker-compose.production.yml up -d backend frontend
sudo docker compose --env-file .env.production -f docker-compose.production.yml ps
```

All three services must be running and healthy. Before installing host Nginx,
verify the loopback entry point:

```bash
curl --fail --silent --show-error http://127.0.0.1:3000/healthz
curl --fail --silent --show-error http://127.0.0.1:3000/health/live
curl --fail --silent --show-error http://127.0.0.1:3000/health/ready
sudo ss -lntup
```

Expected: all curls return HTTP 200; port 3000 listens only on `127.0.0.1`;
ports 5432 and 8080 do not appear as public host listeners.

## 6. Configure domain, Nginx, TLS and Cloudflare

At Cloudflare, add an `A` record for `e-hub.com.vn` pointing to the VPS and a
`CNAME` record for `www` pointing to `e-hub.com.vn`. Keep them DNS-only during
the initial certificate step. Do not put the VPS address in repository files.

Install the bootstrap site and make HTTP reachable:

```bash
cd /opt/ehub/app
sudo install -d -m 0755 /var/www/certbot
sudo cp deploy/nginx/e-hub.com.vn.bootstrap.conf /etc/nginx/sites-available/e-hub.com.vn
sudo ln -sfn /etc/nginx/sites-available/e-hub.com.vn /etc/nginx/sites-enabled/e-hub.com.vn
sudo nginx -t
sudo systemctl reload nginx
sudo ufw allow 'Nginx Full'
sudo ufw status verbose
```

Wait until both DNS names resolve to the VPS, confirm that HTTP reaches the
site, and then request the certificate. Enter the team's real operations email
when prompted by the first command:

```bash
read -r -p "Certificate notification email: " EHUB_CERT_EMAIL
sudo certbot certonly --webroot -w /var/www/certbot \
  -d e-hub.com.vn -d www.e-hub.com.vn \
  --email "${EHUB_CERT_EMAIL}" --agree-tos --no-eff-email
unset EHUB_CERT_EMAIL
```

Install the reviewed Cloudflare visitor-IP ranges and final HTTPS site:

```bash
sudo cp deploy/nginx/cloudflare-real-ip.conf /etc/nginx/snippets/ehub-cloudflare-real-ip.conf
sudo cp deploy/nginx/e-hub.com.vn.conf /etc/nginx/sites-available/e-hub.com.vn
sudo nginx -t
sudo systemctl reload nginx
curl --fail --silent --show-error https://e-hub.com.vn/
sudo certbot renew --dry-run
```

Compare `deploy/nginx/cloudflare-real-ip.conf` with Cloudflare's official IP
list before installing it. Cloudflare changes these ranges infrequently but
publishes changes before using them.

After direct HTTPS works, turn both DNS records to **Proxied**, set Cloudflare
SSL/TLS mode to **Full (strict)**, and retest. A proxied DNS record alone does
not prevent a known origin IP from being contacted directly. Restricting ports
80/443 to current Cloudflare ranges is a separate hardening step and should be
performed only after the proxied route and certificate renewal are proven; do
not risk locking out the first deployment by doing it prematurely.

## 7. PostgreSQL backup outside the VPS

A Docker volume protects data from ordinary container recreation, but it is
not a backup: VPS loss, disk corruption or operator error can remove both the
database and its volume. An accepted backup must exist on another device or
storage provider.

Create and archive-check a dump on the VPS immediately before every migration
or release and at least daily while real users are active:

```bash
cd /opt/ehub/app
bash scripts/production/backup-postgres.sh
ls -lh /opt/ehub/backups
```

The script never prints the database password. It creates a PostgreSQL custom
archive plus a SHA-256 sidecar and removes only EHUB backup files older than 14
days. This local copy is only a transfer staging area.

From Windows PowerShell, copy the two files named by the script to a folder
outside the VPS. Replace `BACKUP_FILE_NAME` with the exact filename; do not use
a wildcard so that the copied release backup is unambiguous:

```powershell
$EhubVpsHost = "YOUR_VPS_IP"
$EhubBackup = "BACKUP_FILE_NAME"
$EhubBackupDirectory = "$env:USERPROFILE\EHUB-Backups"
New-Item -ItemType Directory -Force -Path $EhubBackupDirectory
scp -i "$env:USERPROFILE\.ssh\ehub_vps_ed25519" "ehubadmin@${EhubVpsHost}:/opt/ehub/backups/$EhubBackup" $EhubBackupDirectory
scp -i "$env:USERPROFILE\.ssh\ehub_vps_ed25519" "ehubadmin@${EhubVpsHost}:/opt/ehub/backups/$EhubBackup.sha256" $EhubBackupDirectory
Get-FileHash -Algorithm SHA256 "$EhubBackupDirectory\$EhubBackup"
Get-Content "$EhubBackupDirectory\$EhubBackup.sha256"
```

The hexadecimal hash values must match. Keep at least one additional encrypted
copy in managed cloud/object storage; a backup stored only on the laptop is a
single point of failure. Automating the offsite upload requires choosing that
provider first and must not be implemented with credentials in Git.

Do not consider backup complete until a restore drill into a disposable
database has succeeded. A production restore is destructive and is therefore
not exposed as an unattended script in this pilot runbook.

## 8. Release and rollback procedure

Before changing a running release, record the current SHA and create/copy an
offsite backup:

```bash
cd /opt/ehub/app
git rev-parse HEAD
bash scripts/production/backup-postgres.sh
```

For a new reviewed release, fetch and check out its exact SHA, update
`IMAGE_TAG`, validate, build, run the one-time initializer, and recreate the
application services. Do not delete the named database volume.

If only application code is faulty and the migration is backward-compatible,
roll back to the recorded SHA, restore its `IMAGE_TAG`, rebuild, and recreate
backend/frontend. If a release applied an incompatible schema migration, do not
point old code at the new schema and do not run an ad-hoc migration downgrade.
Stop writes, preserve logs, and choose one of these reviewed incident actions:

1. deploy a forward fix compatible with the current schema; or
2. restore the verified pre-release dump, accepting loss of data written after
   that backup.

Production database restoration must be performed with a second team member
checking the selected database and backup filename. This prevents a routine
application rollback from becoming accidental production data loss.

## 9. Routine diagnostics and prohibitions

Use bounded logs and status checks:

```bash
sudo docker compose --env-file .env.production -f docker-compose.production.yml ps
sudo docker compose --env-file .env.production -f docker-compose.production.yml logs --tail 200 backend
sudo docker system df
df -h
```

Stop application containers without deleting data:

```bash
sudo docker compose --env-file .env.production -f docker-compose.production.yml stop frontend backend
```

Never run `docker compose down -v` in production; `-v` deletes the PostgreSQL
volume. Never expose ports 5432/8080, commit `.env.production`, use production
personal data as test fixtures, or claim a backup is valid before checksum and
restore verification.

## 10. Remaining go-live gates

1. Pull Request review and all blocking CI checks pass.
2. Exact reviewed SHA deployed; container and application health checks pass.
3. HTTPS and Cloudflare Full (strict) pass; Google production origin updated.
4. Admin, lecturer and student smoke tests pass without mock APIs.
5. Pre-release PostgreSQL dump exists outside the VPS and its hash matches.
6. Team records the deployed SHA, backup filename and responsible operator.
7. After the first manual process is proven, publish immutable images to GHCR
   and add an approval-controlled deployment workflow instead of SSH auto-deploy.
