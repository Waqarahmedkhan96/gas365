#!/usr/bin/env bash
# Deploys an image tag: pull, back up, migrate, start, verify health; rolls back to the previous tag on failure.
# Usage: ./deploy.sh <image-tag>
set -euo pipefail
cd "$(dirname "$0")"
TAG="${1:?Usage: deploy.sh <image-tag>}"
COMPOSE=(docker compose -f compose.production.yaml --env-file .env)
PREVIOUS="$(cat .deployed-tag 2>/dev/null || true)"

deploy_tag() {
  sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$1/" .env
  "${COMPOSE[@]}" pull web api migrate
  # Migrations are additive and backward compatible, so the previous API version keeps working if a later step fails.
  "${COMPOSE[@]}" up -d --wait postgres
  "${COMPOSE[@]}" run --rm migrate
  "${COMPOSE[@]}" up -d --wait --remove-orphans api web caddy
}

./backup.sh pre-deploy
if deploy_tag "$TAG" && curl -fsS "https://$(grep '^DOMAIN=' .env | cut -d= -f2)/health/ready" | grep -Eq '"status":"(Healthy|Degraded)"'; then
  echo "$TAG" > .deployed-tag
  echo "Deployed $TAG"
else
  echo "Deployment of $TAG failed" >&2
  if [ -n "$PREVIOUS" ]; then
    echo "Rolling back to $PREVIOUS" >&2
    deploy_tag "$PREVIOUS"
  fi
  exit 1
fi
