#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 /absolute/path/to/walletshub-backup.sql.gz" >&2
  exit 2
fi

backup="$(realpath "$1")"
backup_root="$(realpath "$HOME/walletshub-backups")"
[[ "$backup" == "$backup_root"/* ]] || { echo "Backup must be inside $backup_root" >&2; exit 2; }
[[ -f "$backup" ]] || { echo "Backup not found: $backup" >&2; exit 2; }
gzip -t "$backup"
keys="${backup%.sql.gz}.keys.tar.gz"
[[ -f "$keys" ]] || { echo "Matching data-protection key backup not found: $keys" >&2; exit 2; }
gzip -t "$keys"

cd "$HOME/walletshub"
read -r -p "This replaces the Wallets Hub database from $(basename "$backup"). Type RESTORE: " confirmation
[[ "$confirmation" == "RESTORE" ]] || { echo "Cancelled."; exit 1; }

docker compose --env-file .env.production -f docker-compose.production.yml stop walletshub-api walletshub-web
docker compose --env-file .env.production -f docker-compose.production.yml exec -T walletshub-db sh -c 'dropdb -U "$POSTGRES_USER" --if-exists "$POSTGRES_DB" && createdb -U "$POSTGRES_USER" "$POSTGRES_DB"'
gzip -dc "$backup" | docker compose --env-file .env.production -f docker-compose.production.yml exec -T walletshub-db sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" "$POSTGRES_DB"'
docker compose --env-file .env.production -f docker-compose.production.yml run -T --rm --no-deps walletshub-keys-init sh -c 'find /keys -mindepth 1 -maxdepth 1 -type f -delete'
gzip -dc "$keys" | docker compose --env-file .env.production -f docker-compose.production.yml run -T --rm --no-deps walletshub-keys-init tar -xf - -C /keys
docker compose --env-file .env.production -f docker-compose.production.yml run -T --rm --no-deps walletshub-keys-init chown -R 1654:1654 /keys
docker compose --env-file .env.production -f docker-compose.production.yml up -d walletshub-api walletshub-web
echo "Restore complete. Verify https://servicehub.ink/wallets/login"
