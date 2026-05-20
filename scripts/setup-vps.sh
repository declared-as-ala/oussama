#!/usr/bin/env bash
set -euo pipefail

# QualiFlow VPS setup
# Run from the project root on a fresh Ubuntu/Debian VPS:
#   sudo bash scripts/setup-vps.sh

APP_DIR="/opt/qualiflow"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ "$(id -u)" -ne 0 ]; then
  echo "Please run as root: sudo bash scripts/setup-vps.sh"
  exit 1
fi

echo "==> Installing Docker if needed..."
if ! command -v docker >/dev/null 2>&1; then
  apt-get update
  apt-get install -y ca-certificates curl gnupg lsb-release rsync
  install -m 0755 -d /etc/apt/keyrings

  . /etc/os-release
  curl -fsSL "https://download.docker.com/linux/${ID}/gpg" | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
  chmod a+r /etc/apt/keyrings/docker.gpg

  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/${ID} $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    > /etc/apt/sources.list.d/docker.list

  apt-get update
  apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  systemctl enable --now docker
else
  echo "Docker is already installed."
fi

if ! command -v rsync >/dev/null 2>&1; then
  apt-get update
  apt-get install -y rsync
fi

echo "==> Copying project to ${APP_DIR}..."
mkdir -p "${APP_DIR}"
rsync -a --delete \
  --exclude '.git' \
  --exclude '.env' \
  --exclude 'node_modules' \
  --exclude 'dist' \
  --exclude 'bin' \
  --exclude 'obj' \
  "${REPO_ROOT}/" "${APP_DIR}/"

echo "==> Creating production .env if missing..."
if [ ! -f "${APP_DIR}/.env" ]; then
  cat > "${APP_DIR}/.env" <<'EOF'
APP_URL=http://YOUR_DOMAIN_OR_SERVER_IP
HTTP_PORT=80

POSTGRES_DB=qualiflowdb
POSTGRES_USER=qualiflow
POSTGRES_PASSWORD=CHANGE_ME_STRONG_DB_PASSWORD

JWT_SECRET=CHANGE_ME_GENERATE_WITH_OPENSSL_RAND_BASE64_48
JWT_ISSUER=QualiFlow
JWT_AUDIENCE=QualiFlowUsers
JWT_EXPIRY=1440

SMTP_ENABLED=false
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=
SMTP_PASS=
SMTP_FROM=
SMTP_FROM_NAME=Qualiflow
SMTP_ENABLE_SSL=true

RABBITMQ_ENABLED=true
RABBITMQ_URI=
RABBITMQ_HOST=qualiflow-rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USER=qualiflow
RABBITMQ_PASS=CHANGE_ME_STRONG_RABBITMQ_PASSWORD
RABBITMQ_VIRTUAL_HOST=/
RABBITMQ_QUEUE=qualiflow.notifications

OPENROUTER_API_KEY=
OPENROUTER_MODEL=llama-3.3-70b-versatile
OPENROUTER_URL=https://api.groq.com/openai/v1
OPENROUTER_TIMEOUT_SECONDS=30
OPENROUTER_TEMPERATURE=0.2
OPENROUTER_MAX_OUTPUT_TOKENS=1024

ONESIGNAL_ENABLED=false
ONESIGNAL_APP_ID=
ONESIGNAL_API_KEY=
ONESIGNAL_API_BASE_URL=https://api.onesignal.com
ONESIGNAL_DEFAULT_LANGUAGE=fr

NOTIFICATION_REALTIME_ENABLED=true
NOTIFICATION_DEDUPE_WINDOW_MINUTES=5

SUBSCRIPTION_MONITOR_ENABLED=true
SUBSCRIPTION_MONITOR_POLLING_INTERVAL_MINUTES=60

SUPPORT_ASSISTANT_NAME="Assistant Technique QualiFlow"
SUPPORT_ASSISTANT_EMAIL=support.qualiflow@gmail.com
SUPPORT_ASSISTANT_PHONE="+216 70 000 000"

STORAGE_ROOT_PATH=StorageFiles
STORAGE_ALLOWED_EXTENSIONS=.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.jpg,.jpeg,.png
STORAGE_MAX_FILE_SIZE_MB=20
STORAGE_ORGANIZATION_LOGO_MAX_FILE_SIZE_MB=5
STORAGE_PROFILE_PHOTO_MAX_FILE_SIZE_MB=3
STORAGE_PDF_HEADER_ENABLED=true
STORAGE_WORD_HEADER_ENABLED=true
STORAGE_ORGANIZATION_LOGOS_PATH=StorageFiles/organization-logos
STORAGE_PROFILE_PHOTOS_PATH=StorageFiles/profile-photos
STORAGE_DEFAULT_LOGO_PATH=../front/src/assets/logo.png

REGISTRY=qualiflow
TAG=production
EOF
  chmod 600 "${APP_DIR}/.env"
  echo "Created ${APP_DIR}/.env"
  echo "Edit it before starting:"
  echo "  nano ${APP_DIR}/.env"
  exit 0
fi

echo "==> Starting QualiFlow..."
cd "${APP_DIR}"
docker compose up -d --build

echo ""
echo "Done. Check status with:"
echo "  cd ${APP_DIR}"
echo "  docker compose ps"
echo "  docker compose logs -f backend"
