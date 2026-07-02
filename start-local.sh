#!/usr/bin/env bash
set -euo pipefail

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

echo "Starte JudoTournamentManagement API lokal..."
echo "LAN Zugriff ueber Host-IP auf Port 5080 moeglich."

if [[ -z "${Security__AuthTokenHmacSecret:-}" ]]; then
  Security__AuthTokenHmacSecret="$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 48)"
  export Security__AuthTokenHmacSecret
  echo "Security__AuthTokenHmacSecret wurde fuer diese Sitzung zufaellig erzeugt."
fi

"$DOTNET_CMD" run --project "$PROJECT_ROOT/JudoTournamentManagement.Api/JudoTournamentManagement.Api.csproj" --urls "http://0.0.0.0:5080"
