#!/usr/bin/env bash
set -uo pipefail

# Offline tests for the disk-hygiene changes. Nothing here touches Docker, Discord or the real
# disk: docker, df and curl are mocked. Run from the repository root:
#   bash scripts/production/tests/test-disk-hygiene.sh

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
WORK="$(mktemp -d)"
trap 'rm -rf -- "${WORK}"' EXIT
PASS=0
FAIL=0

ok() { PASS=$((PASS + 1)); echo "  ok   $1"; }
bad() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
check() { # check "<description>" <command...>
    local description="$1"
    shift
    if "$@" > /dev/null 2>&1; then ok "${description}"; else bad "${description}"; fi
}

extract_block() {
    sed -n '/^# BEGIN docker-cleanup/,/^# END docker-cleanup/p' "$1"
}

echo "== cleanup block"
extract_block "${ROOT}/scripts/staging/deploy-staging.sh" > "${WORK}/staging.block"
extract_block "${ROOT}/scripts/production/deploy-production.sh" > "${WORK}/production.block"
check "block exists in the staging script" test -s "${WORK}/staging.block"
check "staging and production blocks are identical" cmp -s "${WORK}/staging.block" "${WORK}/production.block"

# Releases oldest -> newest in the deploy log: sha1 .. sha7 (40 hex chars each).
sha() { printf '%s' "$1" | awk '{ s=$0; while (length(s) < 40) s = s $0; print substr(s, 1, 40) }'; }
make_log() { # make_log <file> <count>
    local i
    : > "$1"
    for (( i = 1; i <= $2; i++ )); do
        printf '2026-11-0%dT10:00:00Z SUCCESS sha=%s previous=%s provider=Trained\n' \
            "${i}" "$(sha "a${i}")" "$(sha "a$((i - 1))")" >> "$1"
        printf '2026-11-0%dT09:00:00Z FAILED stage=build sha=%s previous=x provider=Trained exit=1\n' \
            "${i}" "$(sha "f${i}")" >> "$1"
    done
}

run_cleanup() { # run_cleanup <block> <log> <target> <previous> <keep> <regex> <repos...>; docker is mocked
    local block="$1" log="$2" target="$3" previous="$4" keep="$5" regex="$6"
    shift 6
    (
        LOG_FILE="${log}"
        TARGET_SHA="${target}"
        PREVIOUS_SHA="${previous}"
        CALLS="${WORK}/calls"
        : > "${CALLS}"
        docker() {
            case "$1 $2" in
                "images "*)
                    # repo tags: 7 SHA tags (a1..a7), one "local" tag and one "<none>" tag
                    local repo="$2" i
                    for (( i = 1; i <= 7; i++ )); do
                        if [[ "${TAG_STYLE}" == "short" ]]; then sha "a${i}" | cut -c1-12; else sha "a${i}"; fi
                    done
                    echo local
                    echo "<none>"
                    ;;
                "rmi "*)
                    echo "rmi $2" >> "${CALLS}"
                    [[ "$2" != *"${BUSY_TAG:-__none__}" ]]
                    ;;
                "builder prune") echo "builder prune $*" >> "${CALLS}" ;;
                "image prune") echo "image prune" >> "${CALLS}" ;;
                *) echo "UNEXPECTED docker $*" >> "${CALLS}"; return 1 ;;
            esac
        }
        # shellcheck disable=SC1090
        source "${block}"
        cleanup_after_release "${keep}" "${regex}" "$@" > "${WORK}/out" 2>&1
        echo "exit=$?" >> "${WORK}/out"
    )
}

make_log "${WORK}/log7" 7
SHA_A7="$(sha a7)"
SHA_A6="$(sha a6)"

echo "== staging: keep 5 full-SHA tags"
TAG_STYLE=full run_cleanup "${WORK}/staging.block" "${WORK}/log7" "${SHA_A7}" "${SHA_A6}" 5 '^[0-9a-f]{40}$' \
    ehub-staging-backend ehub-staging-frontend
check "removes the 2 oldest tags of each repository (4 rmi)" test "$(grep -c '^rmi ' "${WORK}/calls")" = 4
check "removes a1 and a2 only" bash -c "grep '^rmi ehub-staging-backend:' '${WORK}/calls' | grep -q '$(sha a1)' && grep '^rmi ehub-staging-backend:' '${WORK}/calls' | grep -q '$(sha a2)' && ! grep '^rmi' '${WORK}/calls' | grep -q -E '$(sha a3)|$(sha a4)|$(sha a5)|$(sha a6)|$(sha a7)'"
check "never touches the tag 'local' or <none>" bash -c "! grep -E 'rmi .*(local|none)' '${WORK}/calls'"
check "prunes build cache older than 72h" grep -q 'builder prune builder prune --all -f --filter until=72h' "${WORK}/calls"
check "prunes dangling images" grep -q '^image prune' "${WORK}/calls"
check "finishes with exit=0" grep -q '^exit=0' "${WORK}/out"

echo "== production: keep 3 short tags, only its own repositories"
TAG_STYLE=short run_cleanup "${WORK}/production.block" "${WORK}/log7" "${SHA_A7}" "${SHA_A6}" 3 '^[0-9a-f]{12,40}$' \
    ehub-backend ehub-frontend
check "removes 4 oldest tags per repository (8 rmi)" test "$(grep -c '^rmi ' "${WORK}/calls")" = 8
check "keeps the current, previous and one more release" bash -c "! grep '^rmi' '${WORK}/calls' | grep -q -E '$(sha a5 | cut -c1-12)|$(sha a6 | cut -c1-12)|$(sha a7 | cut -c1-12)'"
check "does not manage ehub-mentor-semantic" bash -c "! grep -q mentor-semantic '${WORK}/calls'"

echo "== a tag still used by a container is skipped, not fatal"
BUSY_TAG="$(sha a1)" TAG_STYLE=full run_cleanup "${WORK}/staging.block" "${WORK}/log7" "${SHA_A7}" "${SHA_A6}" 5 '^[0-9a-f]{40}$' \
    ehub-staging-backend
check "still exits 0 and removes the other old tag" bash -c "grep -q '^exit=0' '${WORK}/out' && grep -q '$(sha a2)' '${WORK}/calls'"
check "reports the busy tag as kept" grep -q 'kept (still in use)' "${WORK}/out"

echo "== fail-safe: unknown history removes no image tag"
: > "${WORK}/emptylog"
TAG_STYLE=full run_cleanup "${WORK}/staging.block" "${WORK}/emptylog" "${SHA_A7}" "unknown" 5 '^[0-9a-f]{40}$' ehub-staging-backend
check "no rmi when fewer than 2 releases are known" bash -c "! grep -q '^rmi' '${WORK}/calls'"
check "build cache is still pruned" grep -q 'builder prune' "${WORK}/calls"

echo "== disk guard"
mkdir -p "${WORK}/bin" "${WORK}/stamps"
cat > "${WORK}/bin/df" <<'EOF'
#!/usr/bin/env bash
printf 'Use%%\n %s%%\n' "$(cat "${MOCK_DISK_FILE}")"
EOF
cat > "${WORK}/bin/curl" <<'EOF'
#!/usr/bin/env bash
cat > /dev/null
printf '%s\n' "$*" >> "${MOCK_CURL_LOG}"
EOF
chmod +x "${WORK}/bin/df" "${WORK}/bin/curl"
cat > "${WORK}/backup.env" <<'EOF'
R2_ACCOUNT_ID=acct
R2_BACKUP_BUCKET=bucket
R2_BACKUP_ACCESS_KEY_ID=key
R2_BACKUP_SECRET_ACCESS_KEY=secret
DISCORD_WEBHOOK_URL=https://example.invalid/hook
EOF
chmod 600 "${WORK}/backup.env"
export MOCK_DISK_FILE="${WORK}/disk" MOCK_CURL_LOG="${WORK}/curl.log"

guard() { # guard <percent> [extra env assignments...]; prints the exit code
    echo "$1" > "${MOCK_DISK_FILE}"
    shift
    env PATH="${WORK}/bin:${PATH}" EHUB_BACKUP_ENV_FILE="${WORK}/backup.env" EHUB_DISK_STAMP_DIR="${WORK}/stamps" "$@" \
        bash "${ROOT}/scripts/production/check-disk-usage.sh" production > "${WORK}/guard.out" 2>&1
    echo $?
}
sent() { test -f "${MOCK_CURL_LOG}" && wc -l < "${MOCK_CURL_LOG}" || echo 0; }

rm -f "${MOCK_CURL_LOG}"
code="$(guard 70)"
[[ "${code}" == 0 && "$(sent)" == 0 ]] && ok "70%: silent, exit 0, nothing sent" || bad "70%: silent (exit ${code}, sent $(sent))"
code="$(guard 81)"
[[ "${code}" == 1 && "$(sent)" == 1 ]] && ok "81%: one warning sent" || bad "81%: one warning sent (exit ${code}, sent $(sent))"
grep -q 'CANH BAO' "${MOCK_CURL_LOG}" && ok "81%: message is a warning" || bad "81%: message is a warning"
code="$(guard 81)"
[[ "${code}" == 0 && "$(sent)" == 1 ]] && ok "81% again: cooldown keeps it quiet" || bad "81% again: cooldown (exit ${code}, sent $(sent))"
code="$(guard 92)"
[[ "${code}" == 1 && "$(sent)" == 2 ]] && grep -q 'KHAN CAP' "${MOCK_CURL_LOG}" && ok "92%: critical alert is not blocked by the warning cooldown" || bad "92%: critical alert (exit ${code}, sent $(sent))"
code="$(guard 92)"
[[ "${code}" == 0 && "$(sent)" == 2 ]] && ok "92% again: critical cooldown keeps it quiet" || bad "92% again: critical cooldown (exit ${code}, sent $(sent))"
touch -d '4 hours ago' "${WORK}/stamps/ehub-disk-alert-production-critical" 2>/dev/null
code="$(guard 92)"
[[ "${code}" == 1 && "$(sent)" == 3 ]] && ok "92% after 4 h: critical alert repeats" || bad "92% after 4 h: repeat (exit ${code}, sent $(sent))"
code="$(guard 95 DISK_CRITICAL_PERCENT=70)"
[[ "${code}" != 0 ]] && grep -q 'must be greater' "${WORK}/guard.out" && ok "critical threshold below warning threshold is rejected" || bad "critical threshold validation (exit ${code})"
code="$(guard 95 DISK_WARN_COOLDOWN_HOURS=abc)"
[[ "${code}" != 0 ]] && grep -q 'must be a whole number' "${WORK}/guard.out" && ok "non-numeric setting is rejected" || bad "numeric validation (exit ${code})"

echo "== syntax"
for f in scripts/staging/deploy-staging.sh scripts/production/deploy-production.sh scripts/production/check-disk-usage.sh \
    scripts/production/backup-common.sh; do
    check "bash -n ${f}" bash -n "${ROOT}/${f}"
done

echo
echo "passed ${PASS}, failed ${FAIL}"
(( FAIL == 0 ))
