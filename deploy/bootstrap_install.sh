#!/usr/bin/env bash
set -euo pipefail

# One-command Proxmox/LXC installer for Judo Tournament Management.
#
# Downloads a published GitHub release, verifies its integrity, and hands the
# extracted package to deploy/install_release.sh — turning a fresh Debian/Ubuntu
# container into a running instance with a single command:
#
#   curl -fsSL https://raw.githubusercontent.com/McGyver666/JudoTournamentManager/main/deploy/bootstrap_install.sh \
#     | sudo bash -s -- --hostname tournament.example.com --email admin@example.com
#
# Re-running (default latest, or a newer --version) is the upgrade path: the
# bundled installer is idempotent and preserves app/App_Data/ (the SQLite DB).

REPO="McGyver666/JudoTournamentManager"
ASSET_NAME="release.zip"
CHECKSUM_NAME="release.zip.sha256"

usage() {
  cat <<'EOF'
Usage: curl -fsSL <raw-url>/deploy/bootstrap_install.sh | sudo bash -s -- --hostname NAME [options]

Download the latest (or a pinned) GitHub release and run the bundled installer.

Bootstrap options:
  --version vX.Y.Z   Install a specific tagged release (default: latest published).
  -h, --help         Show this help.

All other options are forwarded unchanged to deploy/install_release.sh, for example:
  --hostname NAME    Public DNS hostname for nginx and the TLS certificate (required).
  --email ADDRESS    Email address used for Let's Encrypt notifications.
  --skip-certbot     Configure HTTP only; do not request a TLS certificate.
  --install-dir PATH Installation directory (default: /opt/judo-tournament).
EOF
}

log()  { printf '==> %s\n' "$*"; }
warn() { printf 'WARNING: %s\n' "$*" >&2; }
die()  { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

VERSION=""
FORWARD_ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --version)
      VERSION="${2:?--version requires a release tag}"
      shift 2
      ;;
    --version=*)
      VERSION="${1#*=}"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      # Everything else (including its value) is forwarded verbatim to the
      # installer, which owns argument validation (e.g. --hostname required).
      FORWARD_ARGS+=("$1")
      shift
      ;;
  esac
done

if [[ $EUID -ne 0 ]]; then
  die "Run as root (it installs packages and system services), e.g. pipe into 'sudo bash'."
fi

work_dir=""
cleanup() {
  if [[ -n "$work_dir" && -d "$work_dir" ]]; then
    rm -rf "$work_dir"
  fi
}
trap cleanup EXIT

# 1) Install our own prerequisites; do not assume they are preinstalled.
ensure_prerequisites() {
  local missing=()
  command -v curl >/dev/null 2>&1 || missing+=(curl)
  command -v unzip >/dev/null 2>&1 || missing+=(unzip)
  dpkg -s ca-certificates >/dev/null 2>&1 || missing+=(ca-certificates)

  if [[ ${#missing[@]} -gt 0 ]]; then
    log "Installing prerequisites: ${missing[*]}"
    export DEBIAN_FRONTEND=noninteractive
    apt-get update
    apt-get install -y "${missing[@]}"
  fi
}

# Extract the browser_download_url of an asset by its exact file name. Parses the
# unauthenticated GitHub REST response without depending on jq. Anchoring on the
# closing quote keeps release.zip from matching release.zip.sha256.
asset_url() {
  local json="$1" name="$2" escaped
  escaped="${name//./\\.}"
  printf '%s\n' "$json" \
    | grep -oE "\"https://[^\"]*/${escaped}\"" \
    | head -n1 \
    | tr -d '"'
}

ensure_prerequisites

# 2) Resolve which release to fetch.
if [[ -n "$VERSION" ]]; then
  api_url="https://api.github.com/repos/${REPO}/releases/tags/${VERSION}"
  log "Resolving release ${VERSION}"
else
  api_url="https://api.github.com/repos/${REPO}/releases/latest"
  log "Resolving latest release"
fi

if ! release_json="$(curl -fsSL -H 'Accept: application/vnd.github+json' "$api_url")"; then
  die "Could not query the GitHub API at ${api_url} (release not found, network error, or rate limit)."
fi

download_url="$(asset_url "$release_json" "$ASSET_NAME")"
[[ -n "$download_url" ]] || die "The resolved release has no '${ASSET_NAME}' asset."
checksum_url="$(asset_url "$release_json" "$CHECKSUM_NAME")"

# 3) Download release.zip into a fresh working directory.
work_dir="$(mktemp -d)"
zip_path="$work_dir/${ASSET_NAME}"
log "Downloading ${ASSET_NAME}"
curl -fsSL -o "$zip_path" "$download_url"

# 4) Verify integrity against the published checksum when it is available.
if [[ -n "$checksum_url" ]]; then
  checksum_path="$work_dir/${CHECKSUM_NAME}"
  log "Verifying checksum"
  curl -fsSL -o "$checksum_path" "$checksum_url"
  expected="$(awk '{print $1; exit}' "$checksum_path")"
  actual="$(sha256sum "$zip_path" | awk '{print $1}')"
  if [[ -z "$expected" ]]; then
    die "The '${CHECKSUM_NAME}' asset is empty; refusing to install."
  fi
  if [[ "$expected" != "$actual" ]]; then
    die "Checksum mismatch (expected ${expected}, got ${actual}); aborting before extraction."
  fi
  log "Checksum verified"
else
  warn "No '${CHECKSUM_NAME}' asset on this release; proceeding on TLS trust without checksum verification."
fi

# 5) Extract and locate the bundled installer.
extract_dir="$work_dir/extract"
mkdir -p "$extract_dir"
unzip -q "$zip_path" -d "$extract_dir"

installer="$(find "$extract_dir" -type f -path '*/deploy/install_release.sh' | head -n1)"
[[ -n "$installer" ]] || die "The release archive does not contain deploy/install_release.sh."
release_dir="$(cd "$(dirname "$installer")/.." && pwd)"

# 6) Delegate to the installer with the extracted release as its source.
log "Running installer from ${release_dir}"
bash "$installer" --source "$release_dir" "${FORWARD_ARGS[@]}"
