#!/usr/bin/env bash
set -euo pipefail

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
echo "LAN Zugriff ueber Host-IP auf Port 5080 moeglich."

if [[ -z "${Security__AuthTokenHmacSecret:-}" ]]; then
  Security__AuthTokenHmacSecret="$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 48)"
  export Security__AuthTokenHmacSecret
  echo "Security__AuthTokenHmacSecret wurde fuer diese Sitzung zufaellig erzeugt."
fi

"$DOTNET_CMD" run --project "$PROJECT_ROOT/JudoTournamentManagement.Api/JudoTournamentManagement.Api.csproj" --urls "http://0.0.0.0:5080"
