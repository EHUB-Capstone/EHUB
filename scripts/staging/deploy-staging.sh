#!/usr/bin/env bash
# Deploys ONE reviewed commit of origin/develop to the staging stack on this VPS.
#
# Install a reviewed copy as root-owned /opt/ehub/deploy/deploy-staging.sh and allow
# ehub-deployer to run exactly that path through sudo (see docs/deployment/STAGING_CD.md).
# Never run this script from the git checkout it deploys: the checkout changes on every
# release, while the installed copy only changes after a person re-installs it.
#
# Usage: deploy-staging.sh <40-character-commit-sha>
# Exit codes: 0 success, 64 bad usage/SHA, 75 another deployment held the lock too long,
#             77 not root, anything else = failed stage (see EHUB_DEPLOY_RESULT).
# A failure never rolls back automatically: a migration may already have been applied.
set -Eeuo pipefail
umask 077
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C

# Fixed staging paths and project name prevent this script from touching production.
readonly APP_DIR="/opt/ehub/staging/app"
readonly ENV_FILE="${APP_DIR}/.env.staging"
readonly COMPOSE_FILE="${APP_DIR}/docker-compose.staging.yml"
readonly BACKUP_SCRIPT="${APP_DIR}/scripts/staging/backup-postgres.sh"
# Shared with deploy-production.sh: both stacks live on one VPS, so only one build/migration
# may run at a time. A second deployment waits for the first instead of failing.
readonly LOCK_FILE="/run/lock/ehub-deploy.lock"
readonly LOCK_WAIT_SECONDS=1200
readonly LOG_FILE="/var/log/ehub-staging-deploy.log"
readonly RUN_LOG_DIR="/var/log/ehub-staging-deploy.d"
readonly RUN_LOG_RETENTION_DAYS=30
readonly LOOPBACK_URL="http://127.0.0.1:3001"
readonly STAGING_HOST="staging.e-hub.com.vn"
readonly HEALTH_ATTEMPTS=24
readonly HEALTH_INTERVAL_SECONDS=5
readonly COMPOSE_WAIT_SECONDS=300
readonly MIN_FREE_GB=8

STAGE="init"
TARGET_SHA=""
PREVIOUS_SHA="unknown"
OWNER=""
OWNER_HOME=""
MIRROR_PID=""

record() {
    printf '%s %s\n' "$(date -u +'%Y-%m-%dT%H:%M:%SZ')" "$*" >> "${LOG_FILE}"
}

stage() {
    STAGE="$1"
    echo "==> [${STAGE}]"
}

finish() {
    # Give the mirror a moment to forward the last lines, then stop it.
    if [[ -n "${MIRROR_PID}" ]]; then
        sleep 1
        kill "${MIRROR_PID}" 2>/dev/null || true
    fi
}

on_error() {
    local exit_code=$?
    trap - ERR
    record "FAILED stage=${STAGE} sha=${TARGET_SHA:-none} previous=${PREVIOUS_SHA} exit=${exit_code}"
    echo "EHUB_DEPLOY_RESULT=failed stage=${STAGE} sha=${TARGET_SHA:-none} previous=${PREVIOUS_SHA}" >&2
    echo "No automatic rollback was attempted. Inspect staging before deploying again." >&2
    exit "${exit_code}"
}

if [[ $# -ne 1 ]]; then
    echo "Usage: deploy-staging.sh <40-character-commit-sha>" >&2
    exit 64
fi

TARGET_SHA="${1,,}"
if [[ ! "${TARGET_SHA}" =~ ^[0-9a-f]{40}$ ]]; then
    echo "The argument must be a full 40-character hexadecimal commit SHA." >&2
    exit 64
fi

if (( EUID != 0 )); then
    echo "This script must run as root through the restricted sudo rule." >&2
    exit 77
fi

if [[ ! -e "${LOG_FILE}" ]]; then
    install -m 0640 -o root -g root /dev/null "${LOG_FILE}"
fi

exec 8> "${LOCK_FILE}"
if ! flock -n 8; then
    echo "Another deployment is running; waiting up to ${LOCK_WAIT_SECONDS} seconds for it to finish."
    if ! flock -w "${LOCK_WAIT_SECONDS}" 8; then
        echo "Another deployment still holds the lock after ${LOCK_WAIT_SECONDS} seconds." >&2
        exit 75
    fi
fi

# A dropped SSH connection (CI runner or network) must never kill a release halfway through a
# migration. All output goes to a per-run log file; the client only receives a mirror of that
# file through `tail`, which may die with the connection without affecting this script.
trap '' HUP
install -d -m 0750 -o root -g root "${RUN_LOG_DIR}"
find "${RUN_LOG_DIR}" -maxdepth 1 -type f -name '*.log' -mtime "+${RUN_LOG_RETENTION_DAYS}" -delete
run_log="${RUN_LOG_DIR}/$(date -u +'%Y%m%dT%H%M%SZ')-${TARGET_SHA:0:12}.log"
install -m 0640 -o root -g root /dev/null "${run_log}"
exec 3>&1
exec >> "${run_log}" 2>&1
tail -n +1 -f "${run_log}" >&3 2>/dev/null &
MIRROR_PID=$!
trap finish EXIT
trap on_error ERR
echo "Run log: ${run_log}"

stage "preconditions"
for required in "${APP_DIR}/.git" "${ENV_FILE}" "${COMPOSE_FILE}" "${BACKUP_SCRIPT}"; do
    if [[ ! -e "${required}" ]]; then
        echo "Required staging path is missing: ${required}" >&2
        false
    fi
done

OWNER="$(stat -c '%U' "${APP_DIR}")"
if [[ "${OWNER}" == "root" ]]; then
    echo "The staging checkout must be owned by its regular operator, not root." >&2
    false
fi
OWNER_HOME="$(getent passwd "${OWNER}" | cut -d: -f6)"

# Git runs as the checkout owner so root never creates files in the working tree.
git_as_owner() {
    runuser -u "${OWNER}" -- env HOME="${OWNER_HOME}" GIT_TERMINAL_PROMPT=0 git -C "${APP_DIR}" "$@"
}

docker_root="$(docker info --format '{{.DockerRootDir}}')"
free_gb="$(df --output=avail -BG "${docker_root}" | tail -n 1 | tr -dc '0-9')"
if (( free_gb < MIN_FREE_GB )); then
    echo "Only ${free_gb} GB free on the Docker disk; at least ${MIN_FREE_GB} GB is required." >&2
    false
fi

stage "verify-commit"
git_as_owner fetch --quiet origin "+refs/heads/develop:refs/remotes/origin/develop"
git_as_owner cat-file -e "${TARGET_SHA}^{commit}"
git_as_owner merge-base --is-ancestor "${TARGET_SHA}" origin/develop

dirty="$(git_as_owner status --porcelain --untracked-files=no)"
if [[ -n "${dirty}" ]]; then
    echo "The staging checkout has uncommitted tracked changes; refusing to deploy." >&2
    false
fi
PREVIOUS_SHA="$(git_as_owner rev-parse HEAD)"
record "START sha=${TARGET_SHA} previous=${PREVIOUS_SHA}"

# The backup runs from the already-deployed checkout, before it is replaced.
stage "backup"
bash "${BACKUP_SCRIPT}"

stage "checkout"
git_as_owner checkout --quiet --detach "${TARGET_SHA}"
if [[ "$(git_as_owner rev-parse HEAD)" != "${TARGET_SHA}" ]]; then
    echo "Checkout did not land on the requested commit." >&2
    false
fi

stage "set-image-tag"
tag_lines="$(grep -c '^IMAGE_TAG=' "${ENV_FILE}" || true)"
if [[ "${tag_lines}" != "1" ]]; then
    echo "${ENV_FILE} must contain exactly one IMAGE_TAG= line." >&2
    false
fi
temporary_env="$(mktemp "${ENV_FILE}.tmp.XXXXXX")"
chown --reference="${ENV_FILE}" "${temporary_env}"
chmod --reference="${ENV_FILE}" "${temporary_env}"
if ! awk -v tag="${TARGET_SHA}" '/^IMAGE_TAG=/ { print "IMAGE_TAG=" tag; next } { print }' \
        "${ENV_FILE}" > "${temporary_env}"; then
    rm -f -- "${temporary_env}"
    false
fi
mv -f -- "${temporary_env}" "${ENV_FILE}"

compose=(docker compose --project-name ehub-staging --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}")

stage "validate-compose"
"${compose[@]}" config --quiet

stage "build"
"${compose[@]}" build postgres
"${compose[@]}" build backend
"${compose[@]}" build frontend

stage "start-dependencies"
"${compose[@]}" up -d --wait --wait-timeout "${COMPOSE_WAIT_SECONDS}" postgres ollama

stage "embedding-model"
models="$("${compose[@]}" exec -T ollama ollama list)"
if ! grep -q '^bge-m3[: ]' <<< "${models}"; then
    "${compose[@]}" exec -T ollama ollama pull bge-m3
fi

# One-time initializer: applies committed EF Core migrations and idempotent seeders, then exits.
stage "migrate"
"${compose[@]}" run --rm -T backend --initialize-database

stage "start-application"
"${compose[@]}" up -d --wait --wait-timeout "${COMPOSE_WAIT_SECONDS}" backend frontend

check_endpoint() {
    local path="$1" attempt
    for (( attempt = 1; attempt <= HEALTH_ATTEMPTS; attempt++ )); do
        if curl --fail --silent --max-time 10 --output /dev/null \
                --header "Host: ${STAGING_HOST}" "${LOOPBACK_URL}${path}"; then
            echo "ok ${path}"
            return 0
        fi
        sleep "${HEALTH_INTERVAL_SECONDS}"
    done
    echo "Health check failed after ${HEALTH_ATTEMPTS} attempts: ${path}" >&2
    return 1
}

stage "health-check"
check_endpoint /healthz
check_endpoint /health/live
check_endpoint /health/ready

stage "verify-running-images"
for service in backend frontend; do
    container_id="$("${compose[@]}" ps -q "${service}")"
    running_image="$(docker inspect --format '{{.Config.Image}}' "${container_id}")"
    if [[ "${running_image}" != "ehub-staging-${service}:${TARGET_SHA}" ]]; then
        echo "Running ${service} image is ${running_image}, expected the requested commit." >&2
        false
    fi
done

trap - ERR
record "SUCCESS sha=${TARGET_SHA} previous=${PREVIOUS_SHA}"
echo "EHUB_DEPLOY_RESULT=success sha=${TARGET_SHA} previous=${PREVIOUS_SHA}"
