#!/usr/bin/env bash
set -euo pipefail

app_dir="$HOME/walletshub"
backup_dir="$HOME/walletshub-backups"

cd "$app_dir"
test -f .env.production
install -d -m 700 "$backup_dir"

backup="$backup_dir/daily-$(date -u +%Y%m%dT%H%M%SZ).sql.gz"
docker compose --env-file .env.production -f docker-compose.production.yml exec -T walletshub-db sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' | gzip -9 > "$backup"
keys="${backup%.sql.gz}.keys.tar.gz"
docker run --rm --volume walletshub_walletshub-keys:/keys:ro alpine:3.22 tar -czf - -C /keys . > "$keys"

gzip -t "$backup"
gzip -t "$keys"
test "$(gzip -dc "$backup" | wc -c)" -gt 100
test -s "$keys"
find "$backup_dir" -type f \( -name '*.sql.gz' -o -name '*.keys.tar.gz' \) -mtime +30 -delete
echo "Daily database and encryption-key backups verified"
