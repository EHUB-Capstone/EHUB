#!/usr/bin/env bash
# Shared helpers for the automated PostgreSQL backup scripts.
# Source this file from backup-and-upload.sh and check-backup-freshness.sh;
# do not run it directly. It never prints the R2 credentials or the Discord
# webhook URL.

EHUB_ROOT="${EHUB_ROOT:-/opt/ehub}"
EHUB_BACKUP_ENV_FILE="${EHUB_BACKUP_ENV_FILE:-${EHUB_ROOT}/backup.env}"

# Fixed per-environment paths, like the existing backup scripts, so a run for one
# environment can never touch the other.
ehub_select_environment() {
    case "${1:-}" in
        production)
            EHUB_ENV_NAME=production
            EHUB_APP_DIR="${EHUB_ROOT}/app"
            EHUB_BACKUP_DIR="${EHUB_ROOT}/backups"
            EHUB_BACKUP_PREFIX=ehub-postgres
            ;;
        staging)
            EHUB_ENV_NAME=staging
            EHUB_APP_DIR="${EHUB_ROOT}/staging/app"
            EHUB_BACKUP_DIR="${EHUB_ROOT}/staging/backups"
            EHUB_BACKUP_PREFIX=ehub-staging-postgres
            ;;
        *)
            echo "Usage: $0 <production|staging> ..." >&2
            return 2
            ;;
    esac
    EHUB_REMOTE_PREFIX="${EHUB_ENV_NAME}"
    EHUB_LOCAL_BACKUP_SCRIPT="${EHUB_APP_DIR}/scripts/${EHUB_ENV_NAME}/backup-postgres.sh"
}

# Loads the secret file. It must be private to the user running the job (root
# under systemd), so that nobody else can read the R2 token or the webhook URL.
ehub_load_config() {
    local file="${EHUB_BACKUP_ENV_FILE}" mode owner name value

    if [[ ! -f "${file}" ]]; then
        echo "Missing ${file}. Copy deploy/backup/backup.env.example and fill it in." >&2
        return 1
    fi

    mode="$(stat -c '%a' "${file}")"
    owner="$(stat -c '%U' "${file}")"
    if [[ "${mode}" != "600" || "${owner}" != "$(id -un)" ]]; then
        echo "${file} must be mode 600 and owned by $(id -un) (found ${mode} ${owner})." >&2
        return 1
    fi

    set -a
    # shellcheck disable=SC1090
    source "${file}"
    set +a

    for name in R2_ACCOUNT_ID R2_BACKUP_BUCKET R2_BACKUP_ACCESS_KEY_ID \
        R2_BACKUP_SECRET_ACCESS_KEY DISCORD_WEBHOOK_URL; do
        value="${!name:-}"
        if [[ -z "${value}" || "${value}" == replace-with-* ]]; then
            echo "${name} is not set in ${file}." >&2
            return 1
        fi
    done

    BACKUP_MAX_AGE_HOURS="${BACKUP_MAX_AGE_HOURS:-26}"
    DISK_ALERT_PERCENT="${DISK_ALERT_PERCENT:-75}"
    if [[ ! "${BACKUP_MAX_AGE_HOURS}" =~ ^[0-9]+$ || ! "${DISK_ALERT_PERCENT}" =~ ^[0-9]+$ ]]; then
        echo "BACKUP_MAX_AGE_HOURS and DISK_ALERT_PERCENT must be whole numbers." >&2
        return 1
    fi
}

# Posts a short plain-text message to the Discord channel. A failed notification
# must never fail the backup itself, so this always returns 0. The webhook URL
# is passed to curl on stdin so it does not appear in the process list.
ehub_notify() {
    local text="${1:0:1800}" payload
    text="${text//\\/\\\\}"
    text="${text//\"/\\\"}"
    text="${text//$'\n'/\\n}"
    payload="{\"content\":\"${text}\"}"

    if ! printf 'url = "%s"\n' "${DISCORD_WEBHOOK_URL}" |
        curl --silent --show-error --fail --max-time 15 --config - \
            --header 'Content-Type: application/json' \
            --data "${payload}" --output /dev/null; then
        echo "WARNING: Discord notification could not be sent." >&2
    fi
    return 0
}

# Runs rclone against the backup bucket using only environment variables, so no
# credentials are written to disk. no_check_bucket is required because the token
# is limited to object read/write on this single bucket.
ehub_rclone() {
    (
        export RCLONE_CONFIG=/dev/null
        export RCLONE_CONFIG_EHUBR2_TYPE=s3
        export RCLONE_CONFIG_EHUBR2_PROVIDER=Cloudflare
        export RCLONE_CONFIG_EHUBR2_ACCESS_KEY_ID="${R2_BACKUP_ACCESS_KEY_ID}"
        export RCLONE_CONFIG_EHUBR2_SECRET_ACCESS_KEY="${R2_BACKUP_SECRET_ACCESS_KEY}"
        export RCLONE_CONFIG_EHUBR2_ENDPOINT="https://${R2_ACCOUNT_ID}.r2.cloudflarestorage.com"
        export RCLONE_CONFIG_EHUBR2_ACL=private
        export RCLONE_CONFIG_EHUBR2_NO_CHECK_BUCKET=true
        exec rclone "$@"
    )
}

ehub_remote_path() {
    printf 'EHUBR2:%s/%s' "${R2_BACKUP_BUCKET}" "${EHUB_REMOTE_PREFIX}"
}

ehub_disk_percent() {
    df --output=pcent / | tail -n 1 | tr -dc '0-9'
}
