#!/usr/bin/env bash
# SSH forced command for the ehub-deployer key (authorized_keys: restrict,command="...").
# It can only hand the client's single argument to the installed deployment script;
# deploy-staging.sh then accepts nothing except a 40-character commit SHA.
set -Eeuo pipefail

exec sudo -n /opt/ehub/deploy/deploy-staging.sh "${SSH_ORIGINAL_COMMAND:-}"
