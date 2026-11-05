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
# AI provider switch: MENTOR_MATCHING_PROVIDER=Ollama|Trained|Hybrid in .env.staging (a missing
# line means Ollama). Hybrid adds docker-compose.hybrid.yml and the mentor-semantic container,
# which shares the backend's network namespace and is therefore recreated with the backend.
# Trained adds docker-compose.trained.yml with a model packaged in the backend image; it does
# not start Ollama or pull an embedding model. Leaving Hybrid removes mentor-semantic.
#
# AI checkpoint summaries: CHECKPOINT_SUMMARY_PROVIDER=Disabled|Ollama in the env file (a missing line
# means Disabled). Ollama starts the ollama container even with the Trained provider and pulls the
# summary model (CHECKPOINT_SUMMARY_MODEL, default qwen3.5:2b-q4_K_M). Compose passes the same value
# to the backend, which then shows the feature and runs its background workers; Disabled hides it.
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
readonly TRAINED_OVERRIDE="${APP_DIR}/docker-compose.trained.yml"
readonly TRAINED_MODEL_SOURCE="${APP_DIR}/backend/src/EHub.Infrastructure/Mentoring/Models/allocation-model.json"
readonly SEMANTIC_PREPARE_MODEL="${APP_DIR}/tools/mentor-matching/semantic/prepare_model.py"
readonly SEMANTIC_HEALTH_ATTEMPTS=24
readonly SEMANTIC_HEALTH_INTERVAL_SECONDS=5
readonly MIN_AVAILABLE_MEMORY_MB=1536

STAGE="init"
TARGET_SHA=""
PROVIDER="Ollama"
SUMMARY="Disabled"
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
    trained) PROVIDER="Trained" ;;
    hybrid) PROVIDER="Hybrid" ;;
    *)
        echo "MENTOR_MATCHING_PROVIDER must be Ollama, Trained or Hybrid." >&2
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

# Ollama is needed for mentor embeddings (every provider but Trained) or for summaries.
NEEDS_OLLAMA="false"
if [[ "${PROVIDER}" != "Trained" || "${SUMMARY}" == "Ollama" ]]; then
    NEEDS_OLLAMA="true"
fi

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
record "START sha=${TARGET_SHA} previous=${PREVIOUS_SHA} provider=${PROVIDER} summary=${SUMMARY}"

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
elif [[ "${PROVIDER}" == "Trained" ]]; then
    if [[ ! -f "${TRAINED_OVERRIDE}" ]]; then
        echo "This commit has no Trained support file (docker-compose.trained.yml)." >&2
        false
    fi
    compose+=(-f "${TRAINED_OVERRIDE}")
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
dependencies=(postgres)
if [[ "${NEEDS_OLLAMA}" == "true" ]]; then
    dependencies+=(ollama)
fi
"${compose[@]}" up -d --wait --wait-timeout "${COMPOSE_WAIT_SECONDS}" "${dependencies[@]}"

if [[ "${PROVIDER}" != "Trained" ]]; then
    stage "embedding-model"
    models="$("${compose[@]}" exec -T ollama ollama list)"
    if ! grep -q '^bge-m3[: ]' <<< "${models}"; then
        "${compose[@]}" exec -T ollama ollama pull bge-m3
    fi
fi

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

# /health/ready does not load the mentor model. A checkout under umask 077 once produced an image in
# which only root could read it: every health check passed while AI allocation failed. This check runs
# as the backend's own unprivileged user and compares the model with the one in the reviewed commit.
check_trained_model() {
    local expected model_path actual
    if [[ ! -f "${TRAINED_MODEL_SOURCE}" ]]; then
        echo "This commit has no packaged mentor model (${TRAINED_MODEL_SOURCE})." >&2
        return 1
    fi
    expected="$(sha256sum "${TRAINED_MODEL_SOURCE}" | cut -d' ' -f1)"
    if ! model_path="$("${compose[@]}" exec -T backend printenv Mentoring__TrainedModelPath)" \
            || [[ -z "${model_path}" ]]; then
        echo "The backend has no Mentoring__TrainedModelPath; the Trained override is not applied." >&2
        return 1
    fi
    if ! actual="$("${compose[@]}" exec -T backend sha256sum "${model_path}" | cut -d' ' -f1)"; then
        echo "The backend user cannot read the mentor model ${model_path}; AI allocation would fail." >&2
        return 1
    fi
    if [[ "${actual}" != "${expected}" ]]; then
        echo "The mentor model in the backend (${actual:0:12}) differs from the commit (${expected:0:12})." >&2
        return 1
    fi
    echo "ok mentor model (sha256 ${expected:0:12})"
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

if [[ "${PROVIDER}" == "Trained" ]]; then
    stage "verify-ai-model"
    check_trained_model
fi

trap - ERR
record "SUCCESS sha=${TARGET_SHA} previous=${PREVIOUS_SHA} provider=${PROVIDER}"
echo "EHUB_DEPLOY_RESULT=success sha=${TARGET_SHA} previous=${PREVIOUS_SHA} provider=${PROVIDER}"

# Housekeeping runs after the result line and must never change the release outcome. Staging keeps
# the last 5 releases (it deploys on every push to develop); mentor-semantic is only built for Hybrid.
if ! cleanup_after_release 5 '^[0-9a-f]{40}$' ehub-staging-backend ehub-staging-frontend ehub-mentor-semantic; then
    echo "WARNING: post-release cleanup failed; the release itself succeeded." >&2
fi
