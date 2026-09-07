#!/usr/bin/env bash
set -euo pipefail

# One-command Proxmox/LXC installer for Shiai Manager.
#
# Downloads a published GitHub release, verifies its integrity, and hands the
# extracted package to deploy/install_release.sh — turning a fresh Debian/Ubuntu
# or RHEL-compatible container into a running instance with a single command
# once the fetch prerequisites are present on the host:
#
#   sudo apt-get update
#   sudo apt-get install -y curl ca-certificates unzip
#
# On RHEL-compatible systems, use the equivalent dnf command:
#
#   sudo dnf install -y curl ca-certificates unzip
#
# Then run:
#
#   curl -fsSL https://raw.githubusercontent.com/McGyver666/Shiai-Manager/main/deploy/bootstrap_install.sh \
#     | sudo bash -s -- --hostname tournament.example.com --email admin@example.com
#
# Re-running (default latest, or a newer --version) is the upgrade path: the
# bundled installer is idempotent and preserves app/App_Data/ (the SQLite DB).
# The release pipeline publishes separate linux-x64 and linux-arm64 assets; the
# host architecture is detected below so an incompatible apphost is never run.

REPO="McGyver666/Shiai-Manager"
HOST_RUNTIME=""
ASSET_NAME=""
CHECKSUM_NAME=""

usage() {
  cat <<'EOF'
Usage: sudo bash bootstrap_install.sh --hostname NAME [options]

Download the latest (or a pinned) GitHub release and run the bundled installer.
On a fresh host, install curl and ca-certificates before piping this script from a URL.
The script installs unzip through the host's package manager when it is missing.
The matching linux-x64 or linux-arm64 release asset is selected automatically.

Bootstrap options:
  --version vX.Y.Z   Install a specific tagged release (default: latest published).
  -h, --help         Show this help.

All other options are forwarded unchanged to deploy/install_release.sh, for example:
  --hostname NAME    Public DNS hostname for nginx and the TLS certificate (required).
  --email ADDRESS    Email address used for Let's Encrypt notifications.
  --skip-certbot     Configure HTTP only; do not request a TLS certificate.
  --install-dir PATH Installation directory (default: /opt/shiai-manager).
EOF
}

log()  { printf '==> %s\n' "$*"; }
warn() { printf 'WARNING: %s\n' "$*" >&2; }
die()  { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

OS_ID=""
PACKAGE_MANAGER=""

detect_host_runtime() {
  local machine
  machine="$(uname -m)"
  case "$machine" in
    x86_64|amd64)
      HOST_RUNTIME="linux-x64"
      ;;
    aarch64|arm64)
      HOST_RUNTIME="linux-arm64"
      ;;
    *)
      die "Unsupported Linux architecture '$machine'. Supported architectures are x86_64 and ARM64 (aarch64)."
      ;;
  esac

  ASSET_NAME="release-${HOST_RUNTIME}.zip"
  CHECKSUM_NAME="${ASSET_NAME}.sha256"
}

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

# 1) Select the host package manager and install our own prerequisites; do not
# assume they are preinstalled.
select_rhel_package_manager() {
  if command -v dnf >/dev/null 2>&1; then
    PACKAGE_MANAGER="dnf"
  elif command -v yum >/dev/null 2>&1; then
    PACKAGE_MANAGER="yum"
  else
    die "RHEL-compatible systems require dnf or yum."
  fi
}

detect_package_manager() {
  if [[ ! -r /etc/os-release ]]; then
    die "Cannot determine the Linux distribution: /etc/os-release is missing."
  fi

  # shellcheck disable=SC1091
  # Preserve any user-supplied VERSION (e.g. from --version) because
  # /etc/os-release exports a VERSION/VERSION_ID that would overwrite it.
  saved_VERSION="${VERSION-}"
  . /etc/os-release
  OS_ID="${ID:-}"
  # Restore our original VERSION to avoid clobbering the release tag.
  VERSION="${saved_VERSION}"

  case "$OS_ID" in
    debian|ubuntu)
      PACKAGE_MANAGER="apt-get"
      ;;
    rhel|fedora|centos|rocky|almalinux|ol)
      select_rhel_package_manager
      ;;
    *)
      case "${ID_LIKE:-}" in
        *debian*)
          PACKAGE_MANAGER="apt-get"
          ;;
        *rhel*|*fedora*|*centos*)
          select_rhel_package_manager
          ;;
        *)
          die "Unsupported Linux distribution '$OS_ID'. Supported families are Debian/Ubuntu and RHEL-compatible distributions."
          ;;
      esac
      ;;
  esac
}

ensure_prerequisites() {
  local missing=()
  command -v curl >/dev/null 2>&1 || missing+=(curl)
  command -v unzip >/dev/null 2>&1 || missing+=(unzip)
  if [[ "$PACKAGE_MANAGER" == "apt-get" ]]; then
    dpkg -s ca-certificates >/dev/null 2>&1 || missing+=(ca-certificates)
  else
    rpm -q ca-certificates >/dev/null 2>&1 || missing+=(ca-certificates)
  fi

  if [[ ${#missing[@]} -gt 0 ]]; then
    log "Installing prerequisites: ${missing[*]}"
    if [[ "$PACKAGE_MANAGER" == "apt-get" ]]; then
      export DEBIAN_FRONTEND=noninteractive
      apt-get update
      apt-get install -y "${missing[@]}"
    else
      "$PACKAGE_MANAGER" install -y "${missing[@]}"
    fi
  fi
}

# Extract the browser_download_url of an asset by its exact file name. Parses the
# unauthenticated GitHub REST response without depending on jq. Anchoring on the
# closing quote keeps a zip asset from matching its checksum asset.
asset_url() {
  local json="$1" name="$2" escaped
  escaped="${name//./\\.}"
  printf '%s\n' "$json" \
    | grep -oE "\"https://[^\"]*/${escaped}\"" \
    | head -n1 \
    | tr -d '"'
}

resolve_asset_urls() {
  local json="$1"
  download_url="$(asset_url "$json" "$ASSET_NAME")"
  checksum_url="$(asset_url "$json" "$CHECKSUM_NAME")"

  # Keep existing x64 releases installable while ARM64 requires the new
  # architecture-specific asset.
  if [[ -z "$download_url" && "$HOST_RUNTIME" == "linux-x64" ]]; then
    local legacy_asset="release.zip"
    local legacy_checksum="release.zip.sha256"
    download_url="$(asset_url "$json" "$legacy_asset")"
    checksum_url="$(asset_url "$json" "$legacy_checksum")"
    if [[ -n "$download_url" ]]; then
      ASSET_NAME="$legacy_asset"
      CHECKSUM_NAME="$legacy_checksum"
      warn "Using legacy x64 release asset '${ASSET_NAME}'."
    fi
  fi
}

detect_host_runtime
detect_package_manager
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
  if [[ -z "$VERSION" ]]; then
    fallback_url="https://api.github.com/repos/${REPO}/releases"
    if release_list_json="$(curl -fsSL -H 'Accept: application/vnd.github+json' "$fallback_url")"; then
      if [[ "$release_list_json" == "[]" ]]; then
        die "No published GitHub Releases are available for ${REPO}. The one-command installer requires a published GitHub Release with ${ASSET_NAME} and its checksum. Publish a release first or use the manual install path from the repository source."
      fi

      resolve_asset_urls "$release_list_json"
      if [[ -n "$download_url" ]]; then
        warn "GitHub's 'latest' endpoint is not available for this repository state; falling back to the newest release entry in the releases list."
        release_json="$release_list_json"
      else
        die "No published GitHub Release asset '${ASSET_NAME}' is available for ${REPO}. Publish a release with the matching Linux architecture or use the manual install path from the repository source."
      fi
    else
      die "Could not query the GitHub API at ${fallback_url} (network error or rate limit)."
    fi
  else
    die "Could not query the GitHub API at ${api_url} (release not found, network error, or rate limit)."
  fi
fi

resolve_asset_urls "$release_json"
[[ -n "$download_url" ]] || die "The resolved release has no compatible '${ASSET_NAME}' asset for ${HOST_RUNTIME}."

# 3) Download the architecture-specific release into a fresh working directory.
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
