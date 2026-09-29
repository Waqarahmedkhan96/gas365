#!/usr/bin/env bash
# Compressed logical backup of the Gas365 database, pruned locally and optionally copied off-site with rclone.
# Schedule nightly with cron, e.g.: 30 2 * * * /opt/gas365/deploy/backup.sh nightly >> /var/log/gas365-backup.log 2>&1
set -euo pipefail
cd "$(dirname "$0")"
set -a
# The server-only .env is not part of the repository.
# shellcheck source=/dev/null
. ./.env
set +a
LABEL="${1:-manual}"
mkdir -p backups
FILE="gas365-$(date -u +%Y%m%dT%H%M%SZ)-$LABEL.dump"
docker compose -f compose.production.yaml --env-file .env exec -T postgres \
  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --compress=9 --file="/backups/$FILE"
# A dump is only kept if PostgreSQL can read its table of contents.
docker compose -f compose.production.yaml --env-file .env exec -T postgres pg_restore --list "/backups/$FILE" > /dev/null
find backups -name 'gas365-*.dump' -mtime +"${BACKUP_KEEP_DAYS:-30}" -delete
if [ -n "${BACKUP_REMOTE:-}" ]; then rclone copy "backups/$FILE" "$BACKUP_REMOTE"; fi
if [ -n "${BACKUP_HEARTBEAT_URL:-}" ]; then curl -fsS -m 10 "$BACKUP_HEARTBEAT_URL" > /dev/null; fi
echo "Backup written: backups/$FILE"
