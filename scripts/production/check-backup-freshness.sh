#!/usr/bin/env bash
set -Eeuo pipefail

# Alerts on Discord when the newest backup (on the VPS or in R2) is older than
# BACKUP_MAX_AGE_HOURS, when R2 cannot be read, or when disk usage is high. It
# stays silent when everything is fine. The scheduled backup job reports its own
# success; this job is the safety net for a backup that silently did not run.
#
# Usage: check-backup-freshness.sh <production|staging>
umask 077

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
# shellcheck source=scripts/production/backup-common.sh
source "${SCRIPT_DIR}/backup-common.sh"

ehub_select_environment "${1:-}" || exit 2
ehub_load_config
if ! command -v rclone >/dev/null 2>&1; then
    echo "rclone is not installed. Run: sudo apt install -y rclone" >&2
    exit 1
fi

trap 'code=$?; trap - ERR; ehub_notify "[LOI] EHUB ${EHUB_ENV_NAME}: job kiem tra sao luu gap loi (ma ${code}). Xem log: journalctl -u ehub-backup-freshness.service -n 100"; exit "${code}"' ERR

NOW="$(date +%s)"
LIMIT=$(( BACKUP_MAX_AGE_HOURS * 3600 ))
PROBLEMS=()

NEWEST_LOCAL="$(find "${EHUB_BACKUP_DIR}" -maxdepth 1 -type f -name "${EHUB_BACKUP_PREFIX}-*.dump" \
    -printf '%T@\n' 2>/dev/null | sort -nr | head -n 1 | cut -d. -f1 || true)"
if [[ -z "${NEWEST_LOCAL}" ]]; then
    PROBLEMS+=("khong co ban sao luu nao tren VPS")
elif (( NOW - NEWEST_LOCAL > LIMIT )); then
    PROBLEMS+=("ban sao luu moi nhat tren VPS da $(( (NOW - NEWEST_LOCAL) / 3600 )) gio")
fi

if REMOTE_JSON="$(ehub_rclone lsjson --files-only --include '*.dump' "$(ehub_remote_path)" 2>/dev/null)"; then
    NEWEST_REMOTE="$(printf '%s\n' "${REMOTE_JSON}" | grep -o '"ModTime":"[^"]*"' | cut -d'"' -f4 | sort | tail -n 1 || true)"
    if [[ -z "${NEWEST_REMOTE}" ]]; then
        PROBLEMS+=("khong co ban sao luu nao tren R2")
    else
        REMOTE_EPOCH="$(date -d "${NEWEST_REMOTE}" +%s)"
        if (( NOW - REMOTE_EPOCH > LIMIT )); then
            PROBLEMS+=("ban sao luu moi nhat tren R2 da $(( (NOW - REMOTE_EPOCH) / 3600 )) gio")
        fi
    fi
else
    PROBLEMS+=("khong doc duoc thu muc sao luu tren R2 (chua co file hoac loi ket noi/quyen)")
fi

DISK="$(ehub_disk_percent)"
if (( DISK >= DISK_ALERT_PERCENT )); then
    PROBLEMS+=("o dia da dung ${DISK}% (nguong ${DISK_ALERT_PERCENT}%)")
fi

if (( ${#PROBLEMS[@]} > 0 )); then
    MESSAGE="[CANH BAO] EHUB ${EHUB_ENV_NAME}: $(printf '%s; ' "${PROBLEMS[@]}")"
    echo "${MESSAGE}" >&2
    ehub_notify "${MESSAGE}"
    exit 1
fi

echo "OK: backup moi nhat con trong han ${BACKUP_MAX_AGE_HOURS} gio, o dia ${DISK}%."
