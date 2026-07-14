#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="/opt/judo-tournament"
HOSTNAME=""
EMAIL=""
RUN_CERTBOT=true

usage() {
  cat <<'EOF'
Usage: sudo ./deploy/install_release.sh --hostname example.com [options]

Install the extracted release folder on a Debian/Ubuntu host. Run this script
from the release folder, or provide its path with --source.

Options:
  --hostname NAME       Public DNS hostname for nginx and the TLS certificate.
  --email ADDRESS       Email address used for Let's Encrypt notifications.
  --source DIRECTORY    Extracted release folder (default: parent of deploy/).
  --install-dir PATH    Installation directory (default: /opt/judo-tournament).
  --skip-certbot        Configure HTTP only; do not request a TLS certificate.
  -h, --help            Show this help.
EOF
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --hostname)
      HOSTNAME="${2:?Missing hostname}"
      shift 2
      ;;
    --email)
      EMAIL="${2:?Missing email address}"
      shift 2
      ;;
    --source)
      SOURCE_DIR="${2:?Missing source directory}"
      shift 2
      ;;
    --install-dir)
      INSTALL_DIR="${2:?Missing installation directory}"
      shift 2
      ;;
    --skip-certbot)
      RUN_CERTBOT=false
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ $EUID -ne 0 ]]; then
  echo "Run this installer as root, for example: sudo $0 --hostname example.com" >&2
  exit 1
fi

if [[ -z "$HOSTNAME" ]]; then
  echo "--hostname is required." >&2
  usage >&2
  exit 1
fi

SOURCE_DIR="$(cd "$SOURCE_DIR" && pwd)"
if [[ ! -x "$SOURCE_DIR/app/JudoTournamentManagement.Api" ]] || [[ ! -f "$SOURCE_DIR/deploy/judo-tournament.service" ]]; then
  echo "'$SOURCE_DIR' is not a release folder (app and deploy files are required)." >&2
  exit 1
fi

if ! command -v systemctl >/dev/null 2>&1; then
  echo "systemd is required. Enable nesting/systemd support for this LXC container first." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y nginx openssl rsync
if [[ "$RUN_CERTBOT" == true ]]; then
  apt-get install -y certbot python3-certbot-nginx
fi

if ! id judo >/dev/null 2>&1; then
  useradd --system --create-home --home-dir "$INSTALL_DIR" --shell /usr/sbin/nologin judo
fi

systemctl stop judo-tournament.service 2>/dev/null || true
install -d -o judo -g judo "$INSTALL_DIR/app/App_Data" "$INSTALL_DIR/deploy"

# App_Data contains the SQLite database and is intentionally excluded so that
# upgrades do not overwrite tournament data.
rsync -a --delete --exclude 'app/App_Data/' "$SOURCE_DIR/" "$INSTALL_DIR/"
install -d -o judo -g judo "$INSTALL_DIR/app/App_Data"
chown -R judo:judo "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/app/JudoTournamentManagement.Api"

if [[ ! -f /etc/default/judo-tournament ]]; then
  SECRET="$(openssl rand -base64 48 | tr -d '\n')"
  printf 'Security__AuthTokenHmacSecret=%s\n' "$SECRET" > /etc/default/judo-tournament
  chmod 600 /etc/default/judo-tournament
fi

cp "$INSTALL_DIR/deploy/judo-tournament.service" /etc/systemd/system/judo-tournament.service

# Start with HTTP so Certbot can complete its ACME challenge. Certbot replaces
# this server block with a TLS-enabled one when it is run below.
cat > /etc/nginx/sites-available/judo-tournament <<EOF
server {
    listen 80;
    server_name $HOSTNAME;

    client_max_body_size 20m;

    location / {
        proxy_pass http://127.0.0.1:5080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Port \$server_port;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_read_timeout 300s;
        proxy_send_timeout 300s;
    }
}
EOF
ln -sfn /etc/nginx/sites-available/judo-tournament /etc/nginx/sites-enabled/judo-tournament
nginx -t
systemctl enable --now nginx
systemctl reload nginx

systemctl daemon-reload
systemctl enable --now judo-tournament.service

if [[ "$RUN_CERTBOT" == true ]]; then
  CERTBOT_ARGS=(--nginx -d "$HOSTNAME" --non-interactive --agree-tos --redirect)
  if [[ -n "$EMAIL" ]]; then
    CERTBOT_ARGS+=(--email "$EMAIL")
  else
    CERTBOT_ARGS+=(--register-unsafely-without-email)
  fi
  certbot "${CERTBOT_ARGS[@]}"
fi

echo
echo "Deployment complete."
echo "Application health: http://$HOSTNAME/health"
echo "Service status:     systemctl status judo-tournament --no-pager"
