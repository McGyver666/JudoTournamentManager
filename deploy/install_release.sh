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
apt-get install -y nginx openssl rsync curl
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

# --- Initial admin account ---------------------------------------------------
# On a fresh install the database has no users, so the operator has no way to
# log in. Create the first admin via the app's own bootstrap endpoint (which
# only succeeds when no user exists yet) and print the credentials once. On
# upgrades the endpoint returns 409 and nothing is printed.

# Picks one random character from the given alphabet using /dev/urandom so the
# password draws on a cryptographically secure source rather than $RANDOM.
pick_char() {
  local alphabet="$1" len idx
  len=${#alphabet}
  idx=$(( $(od -An -N4 -tu4 /dev/urandom | tr -d ' ') % len ))
  printf '%s' "${alphabet:idx:1}"
}

# Generates a 20-character password that satisfies the project password policy
# (>= 12 chars, at least 3 of upper/lower/digit/special). One character of each
# class is guaranteed, then the remainder is filled and the whole string is
# shuffled. The special alphabet avoids characters that would need JSON or shell
# escaping so the value can be embedded safely below.
generate_admin_password() {
  local upper='ABCDEFGHIJKLMNOPQRSTUVWXYZ'
  local lower='abcdefghijklmnopqrstuvwxyz'
  local digit='0123456789'
  local special='!@#%^*-_=+.'
  local all="$upper$lower$digit$special"
  local pw="" i
  pw+="$(pick_char "$upper")"
  pw+="$(pick_char "$lower")"
  pw+="$(pick_char "$digit")"
  pw+="$(pick_char "$special")"
  for ((i = 0; i < 16; i++)); do
    pw+="$(pick_char "$all")"
  done
  printf '%s' "$pw" | fold -w1 | shuf | tr -d '\n'
}

seed_initial_admin() {
  local api="http://127.0.0.1:5080"

  # Wait for the service to answer before attempting to seed. A fresh install
  # compiles the app in ExecStartPre, so allow a generous window.
  local ready=false i
  for ((i = 0; i < 180; i++)); do
    if curl -fsS -o /dev/null "$api/health" 2>/dev/null; then
      ready=true
      break
    fi
    sleep 1
  done
  if [[ "$ready" != true ]]; then
    echo "WARNING: The application did not become healthy in time; the initial admin was not created." >&2
    echo "         Once it is running, open the app and complete the first-run admin setup." >&2
    return 0
  fi

  local password
  password="$(generate_admin_password)"

  # Pass the JSON body via a 0600 temp file so the password never appears in the
  # process list (curl arguments are world-readable via /proc).
  local body_file
  body_file="$(mktemp)"
  chmod 600 "$body_file"
  printf '{"userName":"admin","password":"%s"}' "$password" > "$body_file"

  local http_code
  http_code="$(curl -sS -o /dev/null -w '%{http_code}' \
    -X POST "$api/api/auth/bootstrap-admin" \
    -H 'Content-Type: application/json' \
    --data @"$body_file" 2>/dev/null || echo 000)"
  rm -f "$body_file"

  case "$http_code" in
    201)
      # Print to stdout as the final output so it is visible even when the
      # installer is driven by the one-command bootstrap (curl | sudo bash).
      echo
      echo "============================================================"
      echo "Initial admin credentials (save these now):"
      echo "  Username: admin"
      echo "  Password: $password"
      echo "============================================================"
      echo
      ;;
    409)
      echo "An admin account already exists; existing credentials are unchanged."
      ;;
    *)
      echo "WARNING: Could not create the initial admin (HTTP $http_code); no credentials were printed." >&2
      echo "         Open the app and complete the first-run admin setup manually." >&2
      ;;
  esac
}

seed_initial_admin

