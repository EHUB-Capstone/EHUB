#!/usr/bin/env bash
# SSH forced command for the production ehub-deployer key (authorized_keys: restrict,command="...").
# The client's words are split into an array (no globbing, no shell parsing) and handed to the
# installed deployment script, which accepts only "<40-character-sha> [skip-staging-gate]".
set -Eeuo pipefail

read -r -a ehub_args <<< "${SSH_ORIGINAL_COMMAND:-}"
exec sudo -n /opt/ehub/deploy/deploy-production.sh "${ehub_args[@]}"
