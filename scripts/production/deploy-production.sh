#!/usr/bin/env bash
# Deploys ONE reviewed commit of origin/main to the production stack on this VPS.
#
# Install a reviewed copy as root-owned /opt/ehub/deploy/deploy-production.sh and allow
# ehub-deployer to run exactly that path through sudo (see docs/deployment/PRODUCTION_CD.md).
# Never run this script from the git checkout it deploys.
#
# Usage: deploy-production.sh <40-character-commit-sha> [skip-staging-gate]
#
# Gates, in order: the commit must be on origin/main; its content (git tree) must be identical
# to a commit that was deployed to staging (skip-staging-gate bypasses only this, for a hotfix
# or a rollback); a fresh database backup must exist locally and in R2 before anything changes.
#
# Unlike staging, postgres is never recreated. Only backend and frontend are replaced;
# later compose calls use --no-deps to protect the running database.
# There is no automatic rollback: a migration may already have been applied, and rolling back
# code onto a newer schema is unsafe (see docs/deployment/PRODUCTION_VPS.md section 8).
#
# AI matching uses Gemini with backend scoring. Set its server-side key and reviewed-data transfer in the environment.
# Optional checkpoint summaries keep their independent Disabled/Ollama switch.
# Exit codes: 0 success, 64 bad usage/SHA, 75 another deployment held the lock too long,
#             77 not root, anything else = failed stage (see EHUB_DEPLOY_RESULT).
set -Eeuo pipefail
umask 077
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C

# Fixed production paths and project name prevent this script from touching staging.
readonly APP_DIR="/opt/ehub/app"
readonly ENV_FILE="${APP_DIR}/.env.production"
readonly COMPOSE_FILE="${APP_DIR}/docker-compose.production.yml"
readonly BACKUP_UPLOAD_SCRIPT="${APP_DIR}/scripts/production/backup-and-upload.sh"
readonly BACKUP_DIR="/opt/ehub/backups"
readonly BACKUP_PREFIX="ehub-postgres"
readonly STAGING_APP_DIR="/opt/ehub/staging/app"
readonly STAGING_DEPLOY_LOG="/var/log/ehub-staging-deploy.log"
# Shared with deploy-staging.sh: one build/migration at a time on this VPS.
readonly LOCK_FILE="/run/lock/ehub-deploy.lock"
readonly LOCK_WAIT_SECONDS=1200
readonly LOG_FILE="/var/log/ehub-production-deploy.log"
readonly RUN_LOG_DIR="/var/log/ehub-production-deploy.d"
readonly RUN_LOG_RETENTION_DAYS=30
readonly LOOPBACK_URL="http://127.0.0.1:3000"
readonly PRODUCTION_HOST="e-hub.com.vn"
readonly HEALTH_ATTEMPTS=24
readonly HEALTH_INTERVAL_SECONDS=5
readonly COMPOSE_WAIT_SECONDS=300
readonly MIN_FREE_GB=8
readonly COMPOSE_PROJECT="ehub-production"
readonly GEMINI_OVERRIDE="${APP_DIR}/docker-compose.gemini.yml"

STAGE="init"
TARGET_SHA=""
PROVIDER="Gemini"
SUMMARY="Disabled"
IMAGE_TAG=""
SKIP_STAGING_GATE="false"
GATE_RESULT="unknown"
PREVIOUS_SHA="unknown"
MIRROR_PID=""

record() {
    printf '%s %s\n' "$(date -u +'%Y-%m-%dT%H:%M:%SZ')" "$*" >> "${LOG_FILE}"
}

stage() {
    STAGE="$1"
    echo "==> [${STAGE}]"
}

# BEGIN docker-cleanup (identical in deploy-staging.sh and deploy-production.sh; keep both in sync)
# Disk hygiene after a SUCCESSFUL release. Every release leaves build cache and one SHA-tagged image
# behind (about 1 GB a day with staging auto-deploy); in November 2026 that took the 48 GB disk from
# 69% to 81%. This only removes (1) build cache older than BUILD_CACHE_KEEP_HOURS, (2) dangling images,
# and (3) release tags of THIS script's own image repositories that are not among the last N
# successful releases in LOG_FILE. It never uses -f (docker refuses to remove an image a container
# uses), never touches containers, volumes or other repositories, and a failure here must not turn
# a successful release into a failed one.
readonly BUILD_CACHE_KEEP_HOURS=72
CLEANUP_KEPT_SHAS=()

cleanup_load_kept_shas() {
    local keep_count="$1" sha
    mapfile -t CLEANUP_KEPT_SHAS < <(
        sed -n 's/.* SUCCESS sha=\([0-9a-f]\{40\}\) .*/\1/p' "${LOG_FILE}" | tac | awk '!seen[$0]++' | head -n "${keep_count}"
    )
    # The commit just deployed and the one before it (the rollback point) are always protected.
    for sha in "${TARGET_SHA}" "${PREVIOUS_SHA}"; do
        if [[ "${sha}" =~ ^[0-9a-f]{40}$ ]]; then
            CLEANUP_KEPT_SHAS+=("${sha}")
        fi
    done
}

# $1 = regex of release tags that may be removed, remaining arguments = image repositories.
cleanup_release_images() {
    local tag_regex="$1" repo tag sha protected removed=0 protected_count=0
    shift
    for repo in "$@"; do
        while read -r tag; do
            [[ "${tag}" =~ ${tag_regex} ]] || continue
            protected="false"
            for sha in "${CLEANUP_KEPT_SHAS[@]}"; do
                if [[ "${tag}" == "${sha:0:${#tag}}" ]]; then
                    protected="true"
                    break
                fi
            done
            if [[ "${protected}" == "true" ]]; then
                protected_count=$(( protected_count + 1 ))
                continue
            fi
            if docker rmi "${repo}:${tag}" > /dev/null 2>&1; then
                removed=$(( removed + 1 ))
            else
                echo "kept (still in use): ${repo}:${tag}"
            fi
        done < <(docker images "${repo}" --format '{{.Tag}}')
    done
    echo "release image tags: removed ${removed}, protected ${protected_count}"
}

# $1 = number of releases to keep, $2 = removable tag regex, remaining = image repositories.
cleanup_after_release() {
    local keep_count="$1" tag_regex="$2" disk_before disk_after
    shift 2
    echo "==> [cleanup]"
    disk_before="$(df --output=pcent / | tail -n 1 | tr -dc '0-9')"
    docker builder prune --all -f --filter "until=${BUILD_CACHE_KEEP_HOURS}h" > /dev/null \
        || echo "WARNING: build cache prune failed; ignored." >&2
    docker image prune -f > /dev/null || echo "WARNING: dangling image prune failed; ignored." >&2
    cleanup_load_kept_shas "${keep_count}"
    if (( ${#CLEANUP_KEPT_SHAS[@]} < 2 )); then
        echo "Fewer than 2 releases known from ${LOG_FILE}; leaving release image tags untouched."
    else
        cleanup_release_images "${tag_regex}" "$@"
    fi
    disk_after="$(df --output=pcent / | tail -n 1 | tr -dc '0-9')"
    echo "disk usage: ${disk_before}% -> ${disk_after}%"
}
# END docker-cleanup

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
    echo "No automatic rollback was attempted. Inspect production before doing anything else." >&2
    exit "${exit_code}"
}

usage() {
    echo "Usage: deploy-production.sh <40-character-commit-sha> [skip-staging-gate]" >&2
    exit 64
}

if [[ $# -lt 1 || $# -gt 2 ]]; then
    usage
fi
TARGET_SHA="${1,,}"
if [[ ! "${TARGET_SHA}" =~ ^[0-9a-f]{40}$ ]]; then
    echo "The first argument must be a full 40-character hexadecimal commit SHA." >&2
    exit 64
fi
if [[ $# -eq 2 ]]; then
    if [[ "$2" != "skip-staging-gate" ]]; then
        echo "The only allowed second argument is: skip-staging-gate" >&2
        exit 64
    fi
    SKIP_STAGING_GATE="true"
fi
IMAGE_TAG="${TARGET_SHA:0:12}"

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

# Runs git as the owner of the given checkout so root never creates files in a working tree.
git_in() {
    local directory="$1" owner owner_home
    shift
    owner="$(stat -c '%U' "${directory}")"
    owner_home="$(getent passwd "${owner}" | cut -d: -f6)"
    runuser -u "${owner}" -- env HOME="${owner_home}" GIT_TERMINAL_PROMPT=0 git -C "${directory}" "$@"
}

stage "preconditions"
for required in "${APP_DIR}/.git" "${ENV_FILE}" "${COMPOSE_FILE}" "${BACKUP_UPLOAD_SCRIPT}"; do
    if [[ ! -e "${required}" ]]; then
        echo "Required production path is missing: ${required}" >&2
        false
    fi
done
if [[ "$(stat -c '%U' "${APP_DIR}")" == "root" ]]; then
    echo "The production checkout must be owned by its regular operator, not root." >&2
    false
fi
if [[ "${SKIP_STAGING_GATE}" == "false" && ! -e "${STAGING_APP_DIR}/.git" ]]; then
    echo "The staging checkout is missing, so the staging gate cannot be checked." >&2
    false
fi

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
    ""|gemini) PROVIDER="Gemini" ;;
    *)
        echo "Only MENTOR_MATCHING_PROVIDER=Gemini is supported. Update the environment before deploying." >&2
        false
        ;;
esac
echo "AI provider: ${PROVIDER}"

# Read the same way as the mentor provider: at most one line, never sourced.
read_env_value() {
    local name="$1" lines value
    lines="$(grep -c "^${name}=" "${ENV_FILE}" || true)"
    if (( lines > 1 )); then
        echo "${ENV_FILE} must contain at most one ${name}= line." >&2
        return 1
    fi
    if (( lines == 1 )); then
        value="$(grep "^${name}=" "${ENV_FILE}" | cut -d= -f2-)"
        printf '%s' "${value}" | tr -d "\"' \t\r"
    fi
    return 0
}
summary_value="$(read_env_value CHECKPOINT_SUMMARY_PROVIDER)"
case "${summary_value,,}" in
    ""|disabled) SUMMARY="Disabled" ;;
    ollama) SUMMARY="Ollama" ;;
    *)
        echo "CHECKPOINT_SUMMARY_PROVIDER must be Disabled or Ollama." >&2
        false
        ;;
esac
SUMMARY_MODEL="$(read_env_value CHECKPOINT_SUMMARY_MODEL)"
SUMMARY_MODEL="${SUMMARY_MODEL:-qwen3.5:2b-q4_K_M}"
if [[ ! "${SUMMARY_MODEL}" =~ ^[A-Za-z0-9._:/-]+$ ]]; then
    echo "CHECKPOINT_SUMMARY_MODEL contains unexpected characters." >&2
    false
fi
echo "AI checkpoint summaries: ${SUMMARY}"


stage "verify-commit"
git_in "${APP_DIR}" fetch --quiet origin "+refs/heads/main:refs/remotes/origin/main"
git_in "${APP_DIR}" cat-file -e "${TARGET_SHA}^{commit}"
git_in "${APP_DIR}" merge-base --is-ancestor "${TARGET_SHA}" origin/main

dirty="$(git_in "${APP_DIR}" status --porcelain --untracked-files=no)"
if [[ -n "${dirty}" ]]; then
    echo "The production checkout has uncommitted tracked changes; refusing to deploy." >&2
    false
fi
PREVIOUS_SHA="$(git_in "${APP_DIR}" rev-parse HEAD)"

# The content (tree) of the release must equal a commit that ran on staging. Comparing trees
# rather than SHAs is required because the release merge commit has a different SHA.
stage "staging-gate"
if [[ "${SKIP_STAGING_GATE}" == "true" ]]; then
    GATE_RESULT="skipped"
    echo "WARNING: the staging gate was skipped on request (hotfix or rollback)."
else
    target_tree="$(git_in "${APP_DIR}" rev-parse "${TARGET_SHA}^{tree}")"
    candidates="$({
        git_in "${STAGING_APP_DIR}" rev-parse HEAD
        grep -o 'SUCCESS sha=[0-9a-f]\{40\}' "${STAGING_DEPLOY_LOG}" 2>/dev/null | sed 's/.*sha=//' || true
    } | sort -u)"
    GATE_RESULT=""
    for candidate in ${candidates}; do
        candidate_tree="$(git_in "${STAGING_APP_DIR}" rev-parse --verify --quiet "${candidate}^{tree}" || true)"
        if [[ "${candidate_tree}" == "${target_tree}" ]]; then
            GATE_RESULT="staging-match:${candidate:0:12}"
            break
        fi
    done
    if [[ -z "${GATE_RESULT}" ]]; then
        GATE_RESULT="failed"
        echo "No commit deployed to staging has the same content as ${TARGET_SHA}." >&2
        echo "Deploy it to staging first, or use skip-staging-gate for a hotfix or rollback." >&2
        false
    fi
    echo "Staging gate passed: same content as staging commit ${GATE_RESULT#staging-match:}"
fi
record "START sha=${TARGET_SHA} previous=${PREVIOUS_SHA} gate=${GATE_RESULT} provider=${PROVIDER} summary=${SUMMARY}"

# The backup (local dump plus verified R2 upload) runs from the already-deployed checkout.
# backup-and-upload.sh exits 0 without a backup when another backup is running, so a dump
# newer than this stage is checked explicitly.
stage "backup"
backup_started="$(date +%s)"
bash "${BACKUP_UPLOAD_SCRIPT}" production run
fresh_dump="$(find "${BACKUP_DIR}" -maxdepth 1 -type f -name "${BACKUP_PREFIX}-*.dump" \
    -newermt "@${backup_started}" | head -n 1)"
if [[ -z "${fresh_dump}" ]]; then
    echo "No new database backup was created; refusing to change production." >&2
    false
fi
echo "Backup ready: $(basename -- "${fresh_dump}")"

stage "checkout"
git_in "${APP_DIR}" checkout --quiet --detach "${TARGET_SHA}"
if [[ "$(git_in "${APP_DIR}" rev-parse HEAD)" != "${TARGET_SHA}" ]]; then
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
if ! awk -v tag="${IMAGE_TAG}" '/^IMAGE_TAG=/ { print "IMAGE_TAG=" tag; next } { print }' \
        "${ENV_FILE}" > "${temporary_env}"; then
    rm -f -- "${temporary_env}"
    false
fi
mv -f -- "${temporary_env}" "${ENV_FILE}"

compose=(docker compose --project-name "${COMPOSE_PROJECT}" --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}")
if [[ ! -f "${GEMINI_OVERRIDE}" ]]; then
    echo "This commit has no Gemini support file (docker-compose.gemini.yml)." >&2
    false
fi
compose+=(-f "${GEMINI_OVERRIDE}")

stage "validate-compose"
"${compose[@]}" config --quiet

stage "build"
"${compose[@]}" build backend
"${compose[@]}" build frontend

# --no-recreate: a running database is left alone even if its Compose definition changed.
stage "start-dependencies"
dependencies=(postgres)
if [[ "${SUMMARY}" == "Ollama" ]]; then
    dependencies+=(ollama)
fi
"${compose[@]}" up -d --no-recreate --wait --wait-timeout "${COMPOSE_WAIT_SECONDS}" "${dependencies[@]}"


if [[ "${SUMMARY}" == "Ollama" ]]; then
    stage "summary-model"
    models="$("${compose[@]}" exec -T ollama ollama list)"
    if ! awk 'NR > 1 { print $1 }' <<< "${models}" | grep -qxF "${SUMMARY_MODEL}"; then
        "${compose[@]}" exec -T ollama ollama pull "${SUMMARY_MODEL}"
    fi
fi

# One-time initializer: applies committed EF Core migrations and idempotent seeders, then exits.
stage "migrate"
"${compose[@]}" run --rm -T --no-deps backend --initialize-database

# Retire only known AI containers in this stack. Database containers and volumes stay intact.
remove_retired_ai_containers() {
    local service ids
    local -a id_list
    for service in ollama mentor-semantic; do
        # The summary runtime is still needed when that independent feature is enabled.
        if [[ "${service}" == "ollama" && "${SUMMARY}" == "Ollama" ]]; then continue; fi
        ids="$(docker ps -aq \
            --filter "label=com.docker.compose.project=${COMPOSE_PROJECT}" \
            --filter "label=com.docker.compose.service=${service}")"
        if [[ -n "${ids}" ]]; then
            echo "Removing retired AI container(s): ${service}"
            mapfile -t id_list <<< "${ids}"
            docker rm -f "${id_list[@]}" > /dev/null
        fi
    done
}

stage "start-application"
remove_retired_ai_containers
# --no-deps keeps the running database untouched while the application is replaced.
"${compose[@]}" up -d --wait --no-deps --wait-timeout "${COMPOSE_WAIT_SECONDS}" backend frontend

check_endpoint() {
    local path="$1" attempt
    for (( attempt = 1; attempt <= HEALTH_ATTEMPTS; attempt++ )); do
        if curl --fail --silent --max-time 10 --output /dev/null \
                --header "Host: ${PRODUCTION_HOST}" "${LOOPBACK_URL}${path}"; then
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
services_to_verify=(backend frontend)
for service in "${services_to_verify[@]}"; do
    container_id="$("${compose[@]}" ps -q "${service}")"
    running_image="$(docker inspect --format '{{.Config.Image}}' "${container_id}")"
    if [[ "${running_image}" != *":${IMAGE_TAG}" ]]; then
        echo "Running ${service} image is ${running_image}, expected tag ${IMAGE_TAG}." >&2
        false
    fi
done


trap - ERR
record "SUCCESS sha=${TARGET_SHA} previous=${PREVIOUS_SHA} gate=${GATE_RESULT} provider=${PROVIDER}"
echo "EHUB_DEPLOY_RESULT=success sha=${TARGET_SHA} previous=${PREVIOUS_SHA} gate=${GATE_RESULT} provider=${PROVIDER}"

# Housekeeping runs after the result line and must never change the release outcome. Production keeps
# the last 3 releases: current, the rollback point and one more. It deliberately does not manage
# ehub-mentor-semantic, whose repository name is shared with staging.
if ! cleanup_after_release 3 '^[0-9a-f]{12,40}$' ehub-backend ehub-frontend; then
    echo "WARNING: post-release cleanup failed; the release itself succeeded." >&2
fi
