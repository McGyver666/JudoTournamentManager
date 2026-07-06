#!/usr/bin/env bash
set -euo pipefail

ENABLE_TLS="0"
HTTPS_PORT="7080"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --enable-tls)
      ENABLE_TLS="1"
      shift
      ;;
    --https-port)
      HTTPS_PORT="$2"
      shift 2
      ;;
    --skip-frontend-build)
      # Linux script currently starts backend directly; keep flag for CLI compatibility.
      shift
      ;;
    *)
      echo "Unbekannter Parameter: $1" >&2
      echo "Verwendung: ./start-local.sh [--skip-frontend-build] [--enable-tls] [--https-port 7080]" >&2
      exit 1
      ;;
  esac
done

SKIP_FRONTEND_BUILD=false
for arg in "$@"; do
  case "$arg" in
    --skip-frontend-build)
      SKIP_FRONTEND_BUILD=true
      ;;
    *)
      ;;
  esac
done

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET_LOCAL="$PROJECT_ROOT/.dotnet/dotnet"

if [[ -x "$DOTNET_LOCAL" ]]; then
  DOTNET_CMD="$DOTNET_LOCAL"
elif command -v dotnet >/dev/null 2>&1; then
  DOTNET_CMD="dotnet"
else
  echo "Keine .NET SDK-Installation gefunden. Erwartet wurde '.dotnet/dotnet' oder 'dotnet' im PATH." >&2
  exit 1
fi

FRONTEND_ROOT="$PROJECT_ROOT/frontend"
if [[ "$SKIP_FRONTEND_BUILD" != "true" ]]; then
  if [[ ! -f "$FRONTEND_ROOT/package.json" ]]; then
    echo "Frontend-Ordner nicht gefunden: '$FRONTEND_ROOT'. Nutze --skip-frontend-build, wenn nur das API gestartet werden soll." >&2
    exit 1
  fi

  if ! command -v npm >/dev/null 2>&1; then
    echo "npm wurde nicht gefunden. Node.js installieren oder mit --skip-frontend-build nur das API starten." >&2
    exit 1
  fi

  echo "Baue Frontend (Angular) nach wwwroot ..."
  (cd "$FRONTEND_ROOT" && npm run build)
else
  echo "Frontend-Build uebersprungen (--skip-frontend-build)."
fi

echo "Starte JudoTournamentManagement API lokal..."
URLS="http://0.0.0.0:5080"

if [[ "$ENABLE_TLS" == "1" ]]; then
  CERT_DIR="$PROJECT_ROOT/JudoTournamentManagement.Api/App_Data/certs"
  CERT_PATH="$CERT_DIR/dev-lan-cert.pfx"
  mkdir -p "$CERT_DIR"

  if [[ -z "${JUDO_DEV_TLS_CERT_PASSWORD:-}" ]]; then
    JUDO_DEV_TLS_CERT_PASSWORD="$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 48)"
    export JUDO_DEV_TLS_CERT_PASSWORD
    echo "JUDO_DEV_TLS_CERT_PASSWORD wurde fuer diese Sitzung zufaellig erzeugt."
  fi

  "$DOTNET_CMD" dev-certs https -ep "$CERT_PATH" -p "$JUDO_DEV_TLS_CERT_PASSWORD" >/dev/null

  export ASPNETCORE_Kestrel__Certificates__Default__Path="$CERT_PATH"
  export ASPNETCORE_Kestrel__Certificates__Default__Password="$JUDO_DEV_TLS_CERT_PASSWORD"
  URLS="http://0.0.0.0:5080;https://0.0.0.0:${HTTPS_PORT}"
  echo "TLS aktiviert. HTTPS URL: https://localhost:${HTTPS_PORT}"
fi

echo "LAN Zugriff ueber Host-IP moeglich. URLs: ${URLS}"

if [[ -z "${Security__AuthTokenHmacSecret:-}" ]]; then
  Security__AuthTokenHmacSecret="$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 48)"
  export Security__AuthTokenHmacSecret
  echo "Security__AuthTokenHmacSecret wurde fuer diese Sitzung zufaellig erzeugt."
fi

"$DOTNET_CMD" run --project "$PROJECT_ROOT/JudoTournamentManagement.Api/JudoTournamentManagement.Api.csproj" --urls "$URLS"
