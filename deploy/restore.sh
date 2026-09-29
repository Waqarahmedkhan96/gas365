#!/usr/bin/env bash
# Restores a backup into a NEW database (never over the live one), then reports migrations and key row counts.
# Usage: ./restore.sh backups/<file>.dump <target-database>
# Promote only after checking the report: stop api/web, point POSTGRES_DB at the target and redeploy.
set -euo pipefail
cd "$(dirname "$0")"
set -a
# The server-only .env is not part of the repository.
# shellcheck source=/dev/null
. ./.env
set +a
FILE="${1:?Usage: restore.sh <backup.dump> <target-database>}"
TARGET="${2:?Usage: restore.sh <backup.dump> <target-database>}"
if [ "$TARGET" = "$POSTGRES_DB" ]; then echo "Refusing to restore over the live database $POSTGRES_DB" >&2; exit 1; fi
PSQL=(docker compose -f compose.production.yaml --env-file .env exec -T postgres psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER")
"${PSQL[@]}" -d postgres -c "CREATE DATABASE \"$TARGET\""
docker compose -f compose.production.yaml --env-file .env exec -T postgres \
  pg_restore -U "$POSTGRES_USER" -d "$TARGET" --no-owner --exit-on-error "/backups/$(basename "$FILE")"
"${PSQL[@]}" -d "$TARGET" -c 'SELECT count(*) AS migrations FROM "__EFMigrationsHistory";' \
  -c 'SELECT (SELECT count(*) FROM "Tenants") AS tenants, (SELECT count(*) FROM "SaleEntries") AS sale_entries, (SELECT count(*) FROM "StockMovements") AS stock_movements, (SELECT count(*) FROM "FinanceEntries") AS finance_entries;'
echo "Restored into $TARGET"
