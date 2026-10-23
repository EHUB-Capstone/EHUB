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
#
# AI provider switch: the line MENTOR_MATCHING_PROVIDER=Ollama|Hybrid in .env.staging (a missing
# line means Ollama). Hybrid adds docker-compose.hybrid.yml and the mentor-semantic container,
# which shares the backend's network namespace and is therefore recreated with the backend.
# Switching back to Ollama removes a leftover mentor-semantic container.
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
readonly COMPOSE_PROJECT="ehub-staging"
# Hybrid mentor matching (see docs/deployment/HYBRID_MENTOR_VPS.md).
readonly SEMANTIC_OVERRIDE="${APP_DIR}/docker-compose.hybrid.yml"
readonly SEMANTIC_PREPARE_MODEL="${APP_DIR}/tools/mentor-matching/semantic/prepare_model.py"
readonly SEMANTIC_HEALTH_ATTEMPTS=24
readonly SEMANTIC_HEALTH_INTERVAL_SECONDS=5
readonly MIN_AVAILABLE_MEMORY_MB=1536

STAGE="init"
TARGET_SHA=""
PROVIDER="Ollama"
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
    record "FAILED stage=${STAGE} sha=${TARGET_SHA:-none} previous=${PREVIOUS_SHA} provider=${PROVIDER} exit=${exit_code}"
    echo "EHUB_DEPLOY_RESULT=failed stage=${STAGE} sha=${TARGET_SHA:-none} previous=${PREVIOUS_SHA} provider=${PROVIDER}" >&2
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

# The AI provider is chosen per environment in the VPS-only env file. It is read, never sourced.
provider_lines="$(grep -c '^MENTOR_MATCHING_PROVIDER=' "${ENV_FILE}" || true)"
if (( provider_lines > 1 )); then
    echo "${ENV_FILE} must contain at most one MENTOR_MATCHING_PROVIDER= line." >&2
    false
fi
if (( provider_lines == 1 )); then
    provider_value="$(grep '^MENTOR_MATCHING_PROVIDER=' "${ENV_FILE}" | cut -d= -f2-)"
    provider_value="$(printf '%s' "${provider_value}" | tr -d "\"' \t\r")"
else
    provider_value=""
fi
case "${provider_value,,}" in
    ""|ollama) PROVIDER="Ollama" ;;
    hybrid) PROVIDER="Hybrid" ;;
    *)
        echo "MENTOR_MATCHING_PROVIDER must be Ollama or Hybrid." >&2
        false
        ;;
esac
echo "AI provider: ${PROVIDER}"

if [[ "${PROVIDER}" == "Hybrid" ]]; then
    # The VPS has no swap: refuse to start another memory consumer when RAM is already short.
    available_mb="$(awk '/^MemAvailable:/ { printf "%d", $2 / 1024 }' /proc/meminfo)"
    if (( available_mb < MIN_AVAILABLE_MEMORY_MB )); then
        echo "Only ${available_mb} MB of memory is available; Hybrid needs at least ${MIN_AVAILABLE_MEMORY_MB} MB." >&2
        false
    fi
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
record "START sha=${TARGET_SHA} previous=${PREVIOUS_SHA} provider=${PROVIDER}"

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

compose=(docker compose --project-name "${COMPOSE_PROJECT}" --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}")
if [[ "${PROVIDER}" == "Hybrid" ]]; then
    if [[ ! -f "${SEMANTIC_OVERRIDE}" || ! -f "${SEMANTIC_PREPARE_MODEL}" ]]; then
        echo "This commit has no Hybrid support files (docker-compose.hybrid.yml, semantic service)." >&2
        false
    fi
    compose+=(-f "${SEMANTIC_OVERRIDE}")
fi

stage "validate-compose"
"${compose[@]}" config --quiet

stage "build"
"${compose[@]}" build postgres
"${compose[@]}" build backend
"${compose[@]}" build frontend
if [[ "${PROVIDER}" == "Hybrid" ]]; then
    "${compose[@]}" build mentor-semantic
fi

stage "start-dependencies"
"${compose[@]}" up -d --wait --wait-timeout "${COMPOSE_WAIT_SECONDS}" postgres ollama

stage "embedding-model"
models="$("${compose[@]}" exec -T ollama ollama list)"
if ! grep -q '^bge-m3[: ]' <<< "${models}"; then
    "${compose[@]}" exec -T ollama ollama pull bge-m3
fi

# One-time initializer: applies committed EF Core migrations and idempotent seeders, then exits.
stage "migrate"
"${compose[@]}" run --rm -T --no-deps backend --initialize-database

# mentor-semantic shares the backend's network namespace, so it only works while attached to the
# backend container that exists right now. It is therefore always removed first and, for Hybrid,
# created again after the backend; this never relies on Compose recreating it implicitly.
remove_semantic_containers() {
    local ids
    local -a id_list
    ids="$(docker ps -aq \
        --filter "label=com.docker.compose.project=${COMPOSE_PROJECT}" \
        --filter "label=com.docker.compose.service=mentor-semantic")"
    if [[ -n "${ids}" ]]; then
        echo "Removing mentor-semantic container(s) so none stays attached to an old backend."
        mapfile -t id_list <<< "${ids}"
        docker rm -f "${id_list[@]}" > /dev/null
    fi
}

stage "start-application"
remove_semantic_containers
# --no-deps: postgres and ollama were converged in "start-dependencies" and are not touched again.
if [[ "${PROVIDER}" == "Hybrid" ]]; then
    "${compose[@]}" up -d --wait --no-deps --wait-timeout "${COMPOSE_WAIT_SECONDS}" backend mentor-semantic frontend
else
    "${compose[@]}" up -d --wait --no-deps --wait-timeout "${COMPOSE_WAIT_SECONDS}" backend frontend
fi

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

# The backend readiness endpoint does not cover the semantic service, so it is checked on its own
# from inside the backend's network namespace: ready, and the model revision the commit pins.
check_semantic() {
    local expected_revision attempt body
    expected_revision="$(sed -n 's/^REVISION = "\([0-9a-f]\{40\}\)".*/\1/p' "${SEMANTIC_PREPARE_MODEL}")"
    if [[ -z "${expected_revision}" ]]; then
        echo "Could not read the pinned model revision from prepare_model.py." >&2
        return 1
    fi
    for (( attempt = 1; attempt <= SEMANTIC_HEALTH_ATTEMPTS; attempt++ )); do
        if body="$("${compose[@]}" exec -T backend curl --fail --silent --max-time 5 \
                http://127.0.0.1:8091/health 2> /dev/null)" \
            && grep -Eq '"ready"[[:space:]]*:[[:space:]]*true' <<< "${body}" \
            && grep -Eq "\"revision\"[[:space:]]*:[[:space:]]*\"${expected_revision}\"" <<< "${body}"; then
            echo "ok mentor-semantic (model revision ${expected_revision:0:12})"
            return 0
        fi
        sleep "${SEMANTIC_HEALTH_INTERVAL_SECONDS}"
    done
    echo "mentor-semantic is not ready with the pinned model after ${SEMANTIC_HEALTH_ATTEMPTS} attempts." >&2
    return 1
}

stage "health-check"
check_endpoint /healthz
check_endpoint /health/live
check_endpoint /health/ready
if [[ "${PROVIDER}" == "Hybrid" ]]; then
    check_semantic
fi

stage "verify-running-images"
for service in backend frontend; do
    container_id="$("${compose[@]}" ps -q "${service}")"
    running_image="$(docker inspect --format '{{.Config.Image}}' "${container_id}")"
    if [[ "${running_image}" != "ehub-staging-${service}:${TARGET_SHA}" ]]; then
        echo "Running ${service} image is ${running_image}, expected the requested commit." >&2
        false
    fi
done
if [[ "${PROVIDER}" == "Hybrid" ]]; then
    container_id="$("${compose[@]}" ps -q mentor-semantic)"
    running_image="$(docker inspect --format '{{.Config.Image}}' "${container_id}")"
    if [[ "${running_image}" != "ehub-mentor-semantic:${TARGET_SHA}" ]]; then
        echo "Running mentor-semantic image is ${running_image}, expected the requested commit." >&2
        false
    fi
fi

trap - ERR
record "SUCCESS sha=${TARGET_SHA} previous=${PREVIOUS_SHA} provider=${PROVIDER}"
echo "EHUB_DEPLOY_RESULT=success sha=${TARGET_SHA} previous=${PREVIOUS_SHA} provider=${PROVIDER}"
