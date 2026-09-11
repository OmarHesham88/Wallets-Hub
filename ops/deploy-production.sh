#!/usr/bin/env bash
set -euo pipefail

app_dir="$HOME/walletshub"
backup_dir="$HOME/walletshub-backups"
compose_file="docker-compose.production.yml"

install -d "$app_dir"
test -f "$app_dir/.env.production" || {
  echo "$app_dir/.env.production is required"
  exit 1
}

if test -f "$app_dir/$compose_file"; then
  echo "Creating pre-deploy backup"
  install -d -m 700 "$backup_dir"
  backup="$backup_dir/pre-deploy-$(date -u +%Y%m%dT%H%M%SZ).sql.gz"
  cd "$app_dir"
  docker compose --env-file .env.production -f "$compose_file" exec -T walletshub-db sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' | gzip -9 > "$backup"
  keys="${backup%.sql.gz}.keys.tar.gz"
  docker run --rm --volume walletshub_walletshub-keys:/keys:ro alpine:3.22 tar -czf - -C /keys . > "$keys"
  gzip -t "$backup"
  gzip -t "$keys"
  test -s "$backup"
  test -s "$keys"
  find "$backup_dir" -type f \( -name '*.sql.gz' -o -name '*.keys.tar.gz' \) -mtime +30 -delete
  echo "Pre-deploy backup verified"
fi

echo "Installing release files and images"
find "$app_dir" -mindepth 1 -maxdepth 1 ! -name .env.production ! -name .initial-admin-password -exec rm -rf -- {} +
tar -xzf /tmp/walletshub-source.tar.gz -C "$app_dir"
gzip -dc /tmp/walletshub-images.tar.gz | docker load

cd "$app_dir"
docker compose --env-file .env.production -f "$compose_file" up -d --no-build --remove-orphans --force-recreate

api_container="$(docker compose --env-file .env.production -f "$compose_file" ps -q walletshub-api)"
web_container="$(docker compose --env-file .env.production -f "$compose_file" ps -q walletshub-web)"
test -n "$api_container"
test -n "$web_container"
test "$(docker inspect --format '{{.Image}}' "$api_container")" = "$(docker image inspect --format '{{.Id}}' walletshub-api:latest)"
test "$(docker inspect --format '{{.Image}}' "$web_container")" = "$(docker image inspect --format '{{.Id}}' walletshub-web:latest)"
echo "Released containers are running the expected images"

caddy_container="$(docker ps --filter label=com.docker.compose.service=caddy --format '{{.ID}}' | head -n 1)"
test -n "$caddy_container"
docker exec "$caddy_container" caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile

for attempt in $(seq 1 36); do
  status="$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' https://servicehub.ink/wallets/capture-health || true)"
  if test "$status" = "200"; then
    echo "Public Wallets Hub release verified"
    rm -f /tmp/walletshub-images.tar.gz /tmp/walletshub-source.tar.gz /tmp/walletshub-deploy.sh
    exit 0
  fi
  echo "Public verification attempt $attempt returned HTTP $status"
  sleep 5
done

docker compose --env-file .env.production -f "$compose_file" ps
exit 1
