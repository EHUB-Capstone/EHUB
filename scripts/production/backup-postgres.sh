#!/usr/bin/env bash
set -Eeuo pipefail

# PostgreSQL backups contain personal/project data. Keep them readable only by
# root and the EHUB administrator group while they wait to be copied offsite.
umask 027

readonly APP_DIR="/opt/ehub/app"
readonly ENV_FILE="${APP_DIR}/.env.production"
readonly COMPOSE_FILE="${APP_DIR}/docker-compose.production.yml"
readonly BACKUP_DIR="/opt/ehub/backups"
readonly RETENTION_DAYS="${EHUB_BACKUP_RETENTION_DAYS:-14}"

if [[ ! "${RETENTION_DAYS}" =~ ^[0-9]+$ ]] || (( RETENTION_DAYS < 1 || RETENTION_DAYS > 90 )); then
    echo "EHUB_BACKUP_RETENTION_DAYS must be an integer from 1 to 90." >&2
    exit 1
fi

for required_file in "${ENV_FILE}" "${COMPOSE_FILE}"; do
    if [[ ! -f "${required_file}" ]]; then
        echo "Required deployment file is missing: ${required_file}" >&2
        exit 1
    fi
done

install -d -m 0750 "${BACKUP_DIR}"

compose=(sudo docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}")
"${compose[@]}" config --quiet

if ! "${compose[@]}" ps --services --status running postgres | grep -qx postgres; then
    echo "The production PostgreSQL container is not running." >&2
    exit 1
fi

timestamp="$(date -u +'%Y%m%dT%H%M%SZ')"
backup_name="ehub-postgres-${timestamp}.dump"
temporary_file="$(mktemp "${BACKUP_DIR}/.${backup_name}.tmp.XXXXXX")"
final_file="${BACKUP_DIR}/${backup_name}"

cleanup() {
    rm -f -- "${temporary_file}"
}
trap cleanup EXIT

"${compose[@]}" exec -T postgres sh -eu -c \
    'exec pg_dump --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --format=custom --no-owner --no-acl' \
    > "${temporary_file}"

if [[ ! -s "${temporary_file}" ]]; then
    echo "PostgreSQL produced an empty backup." >&2
    exit 1
fi

# Listing a custom archive catches truncated or invalid pg_dump output before
# it is accepted as a backup. A full restore drill is still required later.
"${compose[@]}" exec -T postgres pg_restore --list < "${temporary_file}" > /dev/null

chmod 0640 "${temporary_file}"
mv -- "${temporary_file}" "${final_file}"
trap - EXIT

(
    cd "${BACKUP_DIR}"
    sha256sum "${backup_name}" > "${backup_name}.sha256"
    chmod 0640 "${backup_name}.sha256"
)

# The target is a fixed project backup directory; never broaden these patterns.
find "${BACKUP_DIR}" -maxdepth 1 -type f \
    \( -name 'ehub-postgres-*.dump' -o -name 'ehub-postgres-*.dump.sha256' \) \
    -mtime "+${RETENTION_DAYS}" -delete

echo "Backup created and archive-checked: ${final_file}"
echo "Copy both ${backup_name} and ${backup_name}.sha256 off the VPS now."
