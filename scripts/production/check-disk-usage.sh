#!/usr/bin/env bash
set -Eeuo pipefail

# Hourly disk guard. Stays silent below DISK_ALERT_PERCENT (default 75). From there it posts a
# warning to Discord at most once per DISK_WARN_COOLDOWN_HOURS (default 24); from
# DISK_CRITICAL_PERCENT (default 90) it posts a [KHAN CAP] message at most once per
# DISK_CRITICAL_COOLDOWN_HOURS (default 3). The cooldown stamps live in /run, so a reboot only
# causes one extra message. It only reports; it never deletes anything.
#
# Usage: check-disk-usage.sh <production|staging>
umask 077

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
# shellcheck source=scripts/production/backup-common.sh
source "${SCRIPT_DIR}/backup-common.sh"

ehub_select_environment "${1:-}" || exit 2
ehub_load_config

STAMP_DIR="${EHUB_DISK_STAMP_DIR:-/run}"
DISK="$(ehub_disk_percent)"

if (( DISK < DISK_ALERT_PERCENT )); then
    echo "OK: disk ${DISK}% (warning threshold ${DISK_ALERT_PERCENT}%)."
    exit 0
fi

if (( DISK >= DISK_CRITICAL_PERCENT )); then
    LEVEL=critical
    COOLDOWN_MINUTES=$(( DISK_CRITICAL_COOLDOWN_HOURS * 60 ))
    MESSAGE="[KHAN CAP] EHUB ${EHUB_ENV_NAME}: o dia da dung ${DISK}% (nguong ${DISK_CRITICAL_PERCENT}%). Xu ly ngay: PostgreSQL co the dung khi o day. Xem docs/deployment/BACKUP_RESTORE.md muc 5 (don build cache va image cu)."
else
    LEVEL=warning
    COOLDOWN_MINUTES=$(( DISK_WARN_COOLDOWN_HOURS * 60 ))
    MESSAGE="[CANH BAO] EHUB ${EHUB_ENV_NAME}: o dia da dung ${DISK}% (nguong ${DISK_ALERT_PERCENT}%). Xem docs/deployment/BACKUP_RESTORE.md muc 5 (don build cache va image cu)."
fi

STAMP="${STAMP_DIR}/ehub-disk-alert-${EHUB_ENV_NAME}-${LEVEL}"
if [[ -f "${STAMP}" && -n "$(find "${STAMP}" -mmin "-${COOLDOWN_MINUTES}" 2>/dev/null)" ]]; then
    echo "${LEVEL}: disk ${DISK}%, alert already sent within the cooldown; staying quiet."
    exit 0
fi

echo "${MESSAGE}" >&2
ehub_notify "${MESSAGE}"
: > "${STAMP}"
exit 1
