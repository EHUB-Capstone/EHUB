#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

# Fixed staging paths and project name prevent a backup from targeting production.
readonly APP_DIR="/opt/ehub/staging/app"
readonly ENV_FILE="${APP_DIR}/.env.staging"
readonly COMPOSE_FILE="${APP_DIR}/docker-compose.staging.yml"
readonly BACKUP_DIR="/opt/ehub/staging/backups"
readonly RETENTION_DAYS="${EHUB_STAGING_BACKUP_RETENTION_DAYS:-14}"

if [[ ! "${RETENTION_DAYS}" =~ ^[0-9]+$ ]] || (( RETENTION_DAYS < 1 || RETENTION_DAYS > 90 )); then
    echo "EHUB_STAGING_BACKUP_RETENTION_DAYS must be an integer from 1 to 90." >&2
    exit 1
fi

for required_file in "${ENV_FILE}" "${COMPOSE_FILE}"; do
    if [[ ! -f "${required_file}" ]]; then
        echo "Required staging file is missing: ${required_file}" >&2
        exit 1
    fi
done

install -d -m 0750 "${BACKUP_DIR}"
if [[ "$(realpath -- "${BACKUP_DIR}")" != "${BACKUP_DIR}" ]]; then
    echo "The staging backup directory must not resolve outside ${BACKUP_DIR}." >&2
    exit 1
fi

compose=(sudo docker compose --project-name ehub-staging --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}")
"${compose[@]}" config --quiet

if ! "${compose[@]}" ps --services --status running postgres | grep -qx postgres; then
    echo "The staging PostgreSQL container is not running." >&2
    exit 1
fi

timestamp="$(date -u +'%Y%m%dT%H%M%SZ')"
backup_name="ehub-staging-postgres-${timestamp}.dump"
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
    echo "PostgreSQL produced an empty staging backup." >&2
    exit 1
fi

"${compose[@]}" exec -T postgres pg_restore --list < "${temporary_file}" > /dev/null
chmod 0600 "${temporary_file}"
mv -- "${temporary_file}" "${final_file}"
trap - EXIT

(
    cd "${BACKUP_DIR}"
    sha256sum "${backup_name}" > "${backup_name}.sha256"
    chmod 0600 "${backup_name}.sha256"
)

# Only expired staging dumps in this verified directory are removed.
find "${BACKUP_DIR}" -maxdepth 1 -type f \
    \( -name 'ehub-staging-postgres-*.dump' -o -name 'ehub-staging-postgres-*.dump.sha256' \) \
    -mtime "+${RETENTION_DAYS}" -delete

echo "Staging backup created and archive-checked: ${final_file}"
echo "Copy both ${backup_name} and ${backup_name}.sha256 off the VPS now."
