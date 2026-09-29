#!/usr/bin/env bash
# Copies the versioned deployment files to the server and runs deploy.sh there. The server keeps its own .env.
# Required environment: TAG, HOST, DEPLOY_USER, DEPLOY_PATH, SSH_KEY, KNOWN_HOSTS.
set -euo pipefail
install -m 700 -d ~/.ssh
printf '%s\n' "$SSH_KEY" > ~/.ssh/deploy_key && chmod 600 ~/.ssh/deploy_key
printf '%s\n' "$KNOWN_HOSTS" > ~/.ssh/known_hosts
target="$DEPLOY_USER@$HOST"
scp -i ~/.ssh/deploy_key deploy/compose.production.yaml deploy/Caddyfile deploy/deploy.sh deploy/backup.sh deploy/restore.sh "$target:$DEPLOY_PATH/"
ssh -i ~/.ssh/deploy_key "$target" "cd '$DEPLOY_PATH' && chmod +x deploy.sh backup.sh restore.sh && ./deploy.sh '$TAG'"
