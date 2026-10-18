#!/usr/bin/env bash
set -Eeuo pipefail

# Scheduled PostgreSQL backup: runs the existing environment backup script, then
# uploads the dump and its checksum to the private Cloudflare R2 backup bucket,
# verifies the upload size, and reports the result to Discord.
#
# Usage: backup-and-upload.sh <production|staging> [run|check]
#   run    (default) take a backup, upload it and notify
#   check  verify the configuration, R2 access and Discord, without a backup
#
# Run as root (systemd does this): the underlying backup script uses sudo docker.
umask 077

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
# shellcheck source=scripts/production/backup-common.sh
source "${SCRIPT_DIR}/backup-common.sh"

ehub_select_environment "${1:-}" || exit 2
MODE="${2:-run}"
if [[ "${MODE}" != "run" && "${MODE}" != "check" ]]; then
    echo "Usage: $0 <production|staging> [run|check]" >&2
    exit 2
fi

ehub_load_config
if ! command -v rclone >/dev/null 2>&1; then
    echo "rclone is not installed. Run: sudo apt install -y rclone" >&2
    exit 1
fi

STEP="khoi dong"
LOG_FILE="$(mktemp)"
trap 'rm -f -- "${LOG_FILE}"' EXIT

fail() {
    local code="$1"
    trap - ERR
    ehub_notify "[LOI] EHUB ${EHUB_ENV_NAME}: sao luu that bai o buoc \"${STEP}\" (ma loi ${code}). Xem log: sudo journalctl -u ehub-backup.service -n 100"
    exit "${code}"
}
trap 'fail $?' ERR

if [[ "${MODE}" == "check" ]]; then
    STEP="kiem tra rclone"
    echo "rclone: $(rclone version | head -n 1)"
    STEP="doc bucket R2"
    ehub_rclone lsf "EHUBR2:${R2_BACKUP_BUCKET}" --max-depth 1 >/dev/null
    echo "R2 bucket reachable: ${R2_BACKUP_BUCKET}"
    STEP="gui Discord"
    ehub_notify "[KIEM TRA] EHUB ${EHUB_ENV_NAME}: cau hinh sao luu hop le ($(date '+%F %R'))."
    echo "CHECK_OK"
    exit 0
fi

STARTED="$(date +%s)"

STEP="sao luu cuc bo"
# Capture the exit code inside an "if": the ERR trap would otherwise fire for the
# deliberate "another backup is running" code (75) even with errexit disabled.
if bash "${EHUB_LOCAL_BACKUP_SCRIPT}" >"${LOG_FILE}" 2>&1; then
    code=0
else
    code=$?
fi
cat "${LOG_FILE}"
if [[ "${code}" -eq 75 ]]; then
    ehub_notify "[BO QUA] EHUB ${EHUB_ENV_NAME}: co mot sao luu khac dang chay (co the dang deploy); se chay lai theo lich ke tiep."
    exit 0
fi
if [[ "${code}" -ne 0 ]]; then
    fail "${code}"
fi

STEP="tim file backup moi"
DUMP="$(find "${EHUB_BACKUP_DIR}" -maxdepth 1 -type f -name "${EHUB_BACKUP_PREFIX}-*.dump" \
    -newermt "@${STARTED}" -printf '%T@ %p\n' | sort -nr | head -n 1 | cut -d' ' -f2-)"
[[ -n "${DUMP}" && -s "${DUMP}" && -s "${DUMP}.sha256" ]]
NAME="$(basename -- "${DUMP}")"

STEP="kiem tra checksum cuc bo"
(cd "${EHUB_BACKUP_DIR}" && sha256sum --check --strict "${NAME}.sha256" >/dev/null)

REMOTE="$(ehub_remote_path)"
STEP="tai len R2"
ehub_rclone copyto "${DUMP}" "${REMOTE}/${NAME}"
ehub_rclone copyto "${DUMP}.sha256" "${REMOTE}/${NAME}.sha256"

STEP="kiem tra kich thuoc tren R2"
LOCAL_BYTES="$(stat -c %s "${DUMP}")"
REMOTE_BYTES="$(ehub_rclone size --json "${REMOTE}/${NAME}" | grep -o '"bytes":[0-9]*' | cut -d: -f2)"
[[ "${REMOTE_BYTES}" == "${LOCAL_BYTES}" ]]

DURATION=$(( $(date +%s) - STARTED ))
DISK="$(ehub_disk_percent)"
MESSAGE="[OK] EHUB ${EHUB_ENV_NAME}: sao luu xong. ${NAME} ($(numfmt --to=iec --suffix=B "${LOCAL_BYTES}")) da len R2 sau ${DURATION}s. O dia: ${DISK}%."
if (( DISK >= DISK_ALERT_PERCENT )); then
    MESSAGE+=" [CANH BAO] O dia vuot ${DISK_ALERT_PERCENT}%."
fi
echo "${MESSAGE}"
ehub_notify "${MESSAGE}"
