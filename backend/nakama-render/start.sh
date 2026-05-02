#!/usr/bin/env sh
set -eu

if [ -n "${NAKAMA_DB_ADDRESS:-}" ]; then
  DB_ADDR="$NAKAMA_DB_ADDRESS"
elif [ -n "${DATABASE_URL:-}" ]; then
  DB_ADDR="${DATABASE_URL#postgres://}"
  DB_ADDR="${DB_ADDR#postgresql://}"
else
  echo "Missing database config. Set NAKAMA_DB_ADDRESS (preferred) or DATABASE_URL."
  exit 1
fi

SOCKET_PORT="${PORT:-7350}"
NAME="${NAKAMA_NODE_NAME:-nakama1}"
SERVER_KEY="${NAKAMA_SERVER_KEY:-dev_server_key_change_me}"
SESSION_KEY="${NAKAMA_SESSION_ENCRYPTION_KEY:-dev_session_key_change_me_32_char}"
REFRESH_KEY="${NAKAMA_SESSION_REFRESH_ENCRYPTION_KEY:-dev_refresh_key_change_me_32_char}"
HTTP_KEY="${NAKAMA_HTTP_KEY:-dev_http_key_change_me}"
CONSOLE_USER="${NAKAMA_CONSOLE_USER:-admin}"
CONSOLE_PASS="${NAKAMA_CONSOLE_PASS:-password}"

echo "[nakama] migrate up"
/nakama/nakama migrate up --database.address "$DB_ADDR"

echo "[nakama] start"
exec /nakama/nakama \
  --name "$NAME" \
  --database.address "$DB_ADDR" \
  --logger.level INFO \
  --socket.port "$SOCKET_PORT" \
  --socket.server_key "$SERVER_KEY" \
  --session.encryption_key "$SESSION_KEY" \
  --session.refresh_encryption_key "$REFRESH_KEY" \
  --runtime.http_key "$HTTP_KEY" \
  --console.username "$CONSOLE_USER" \
  --console.password "$CONSOLE_PASS" \
  --session.token_expiry_sec 86400 \
  --session.refresh_token_expiry_sec 604800