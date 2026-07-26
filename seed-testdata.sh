#!/usr/bin/env bash
set -euo pipefail

BASE_URL="http://localhost:5080"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-url)
      BASE_URL="${2:-}"
      shift 2
      ;;
    -h|--help)
      echo "Usage: ./seed-testdata.sh [--base-url http://localhost:5080]"
      exit 0
      ;;
    *)
      echo "Unknown parameter: $1" >&2
      echo "Usage: ./seed-testdata.sh [--base-url http://localhost:5080]" >&2
      exit 1
      ;;
  esac
done

if [[ "${ASPNETCORE_ENVIRONMENT:-}" == "Production" ]]; then
  echo "Dieses Skript darf nicht in Production ausgefuehrt werden." >&2
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl wurde nicht gefunden." >&2
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 wurde nicht gefunden. Es wird zum sicheren Erzeugen und Lesen von JSON benoetigt." >&2
  exit 1
fi

API_BASE_URL="${BASE_URL%/}/api"
AUTHORIZATION_HEADER_VALUE=""
HTTP_STATUS=""
HTTP_BODY=""
ADMIN_PASSWORD_WAS_PROMPTED=false

if [[ -n "${JUDO_TEST_PASSWORD:-}" ]]; then
  ADMIN_PASSWORD="$JUDO_TEST_PASSWORD"
  ADMIN_PASSWORD_SOURCE="JUDO_TEST_PASSWORD"
else
  ADMIN_PASSWORD="$(python3 - <<'PY'
import secrets
print(secrets.token_hex(16) + "!A1")
PY
)"
  ADMIN_PASSWORD_SOURCE="zufaellig generiert"
fi

request_json() {
  local method="$1"
  local url="$2"
  local body="$3"
  local response_file

  response_file="$(mktemp)"
  if [[ -n "$AUTHORIZATION_HEADER_VALUE" ]]; then
    if ! HTTP_STATUS="$(
      curl -sS -o "$response_file" -w "%{http_code}" \
        -X "$method" "$url" \
        -H "Content-Type: application/json" \
        -H "$AUTHORIZATION_HEADER_VALUE" \
        --data "$body"
    )"; then
      HTTP_STATUS="000"
    fi
  else
    if ! HTTP_STATUS="$(
      curl -sS -o "$response_file" -w "%{http_code}" \
        -X "$method" "$url" \
        -H "Content-Type: application/json" \
        --data "$body"
    )"; then
      HTTP_STATUS="000"
    fi
  fi

  HTTP_BODY="$(<"$response_file")"
  rm -f "$response_file"
}

invoke_api() {
  local method="$1"
  local url="$2"
  local body="$3"

  request_json "$method" "$url" "$body"
  if [[ "$HTTP_STATUS" =~ ^2 ]]; then
    printf '%s' "$HTTP_BODY"
    return 0
  fi

  echo "Request failed: $method $url" >&2
  echo "HTTP $HTTP_STATUS" >&2
  if [[ -n "$HTTP_BODY" ]]; then
    echo "$HTTP_BODY" >&2
  fi
  exit 1
}

read_password() {
  local prompt="$1"
  local password

  if [[ -t 0 ]]; then
    IFS= read -r -s -p "$prompt: " password
    printf '\n' >&2
  else
    printf '%s: ' "$prompt" >&2
    IFS= read -r password
  fi

  printf '%s' "$password"
}

prompt_for_admin_password() {
  local message="$1"

  echo "$message"
  ADMIN_PASSWORD="$(read_password "Bitte Admin-Passwort fuer Login eingeben")"
  if [[ -z "$ADMIN_PASSWORD" ]]; then
    echo "Kein Admin-Passwort eingegeben. Seed abgebrochen." >&2
    exit 1
  fi

  ADMIN_PASSWORD_WAS_PROMPTED=true
}

json_login_payload() {
  python3 - "$1" <<'PY'
import json
import sys

print(json.dumps({"userName": "admin", "password": sys.argv[1]}))
PY
}

json_tournament_payload() {
  python3 - <<'PY'
import json

print(json.dumps({
    "name": "UI Testturnier 2026",
    "date": "2026-09-20",
    "venue": "Sporthalle Musterstadt",
    "organizer": "JC Musterstadt",
}))
PY
}

json_tatami_payload() {
  python3 - "$1" "$2" <<'PY'
import json
import sys

print(json.dumps({"name": sys.argv[1], "displayOrder": int(sys.argv[2])}))
PY
}

json_club_payload() {
  python3 - "$1" <<'PY'
import json
import re
import sys

name = sys.argv[1]
slug = re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")
print(json.dumps({
    "name": name,
    "contactName": f"Kontakt {name}",
    "contactEmail": f"kontakt@{slug}.example",
    "contactPhone": "+49 555 0100",
}))
PY
}

json_athletes_payload() {
  python3 - "$@" <<'PY'
import json
import random
import sys
from datetime import datetime

clubs = sys.argv[1:]
male_first_names = [
    "Ben", "Elias", "Finn", "Jonas", "Leon", "Luca", "Mats", "Noah", "Nico", "Paul",
    "Anton", "David", "Emil", "Felix", "Jan", "Karl", "Luis", "Milan", "Oskar", "Timo",
    "Aron", "Bennet", "Hannes", "Jannis", "Levi", "Linus", "Mika", "Moritz", "Theo", "Yusuf",
]
female_first_names = [
    "Anna", "Clara", "Ella", "Emma", "Frieda", "Hannah", "Ida", "Lea", "Lena", "Lina",
    "Maja", "Mia", "Nele", "Paula", "Sofia", "Zoe", "Amelie", "Greta", "Juna", "Mila",
    "Charlotte", "Elif", "Helena", "Johanna", "Luisa", "Marie", "Nora", "Sara", "Thea", "Yara",
]
last_names = [
    "Becker", "Bergmann", "Fischer", "Franke", "Hoffmann", "Kaiser", "Klein", "Koch", "Krause", "Krueger",
    "Lehmann", "Mayer", "Neumann", "Richter", "Schmidt", "Schneider", "Scholz", "Schubert", "Vogel", "Wagner",
    "Baumann", "Brandt", "Engel", "Friedrich", "Hartmann", "Jung", "Keller", "Koenig", "Lange", "Lorenz",
    "Peters", "Roth", "Schreiber", "Simon", "Weber", "Weiss", "Werner", "Winkler", "Wolf", "Zimmermann",
]
age_groups = [
    ("U11", 30, [2016, 2017], 22.0, 40.0),
    ("U13", 68, [2014, 2015], 28.0, 48.0),
    ("U15", 52, [2012, 2013], 35.0, 60.0),
]
genders = ["Female"] * 53 + ["Male"] * 97
random.shuffle(genders)
timestamp = datetime.now().strftime("%Y%m%d%H%M")
athletes = []
number = 0

for _, count, birth_years, min_weight, max_weight in age_groups:
    for _ in range(count):
        gender = genders[number]
        first_name = random.choice(female_first_names if gender == "Female" else male_first_names)
        athletes.append({
            "clubId": clubs[number % len(clubs)],
            "firstName": first_name,
            "lastName": random.choice(last_names),
            "birthYear": random.choice(birth_years),
            "gender": gender,
            "licenseId": f"LIZ-{timestamp}-{number + 1:03d}",
            "weightKg": round(random.randint(int(min_weight * 10), int(max_weight * 10)) / 10, 1),
            "grade": random.randint(2, 8),
        })
        number += 1

random.shuffle(athletes)
print(json.dumps({"athletes": athletes}))
PY
}

json_value() {
  python3 -c 'import json, sys
data = json.load(sys.stdin)
value = data
for part in sys.argv[1].split("."):
    value = value[part]
print(value)' "$1"
}

json_count() {
  python3 -c 'import json, sys
print(len(json.load(sys.stdin)))'
}

echo "=== Seeding Judo Tournament Management test data ==="
echo "Base URL: $BASE_URL"
echo "Admin-Passwort Quelle: $ADMIN_PASSWORD_SOURCE"

echo
echo "[0/4] Bootstrapping admin user..."
request_json "POST" "$API_BASE_URL/auth/bootstrap-admin" "$(json_login_payload "$ADMIN_PASSWORD")"
if [[ "$HTTP_STATUS" == "201" ]]; then
  echo "Created initial admin user 'admin'."
elif [[ "$HTTP_STATUS" == "000" ]]; then
  echo "Admin bootstrap failed: API is unreachable at $API_BASE_URL." >&2
  if [[ -n "$HTTP_BODY" ]]; then
    echo "$HTTP_BODY" >&2
  fi
  exit 1
else
  echo "Admin bootstrap was not possible (HTTP $HTTP_STATUS)."
  if [[ -n "$HTTP_BODY" ]]; then
    echo "$HTTP_BODY"
  fi

  if [[ -z "${JUDO_TEST_PASSWORD:-}" ]]; then
    prompt_for_admin_password "Please enter the existing admin password to continue."
  else
    echo "Continuing with JUDO_TEST_PASSWORD."
  fi
fi

echo
echo "Logging in as admin..."
request_json "POST" "$API_BASE_URL/auth/login" "$(json_login_payload "$ADMIN_PASSWORD")"
if [[ ! "$HTTP_STATUS" =~ ^2 ]]; then
  if [[ "$ADMIN_PASSWORD_WAS_PROMPTED" == "false" && -z "${JUDO_TEST_PASSWORD:-}" ]]; then
    prompt_for_admin_password "Login with bootstrapped password failed. Please enter the existing admin password."
    request_json "POST" "$API_BASE_URL/auth/login" "$(json_login_payload "$ADMIN_PASSWORD")"
  fi
fi

if [[ ! "$HTTP_STATUS" =~ ^2 ]]; then
  echo "Login failed: HTTP $HTTP_STATUS" >&2
  if [[ -n "$HTTP_BODY" ]]; then
    echo "$HTTP_BODY" >&2
  fi
  exit 1
fi

BEARER_TOKEN="$(printf '%s' "$HTTP_BODY" | json_value "accessToken")"
AUTHORIZATION_HEADER_VALUE="Authorization: Bearer $BEARER_TOKEN"
echo "Logged in successfully. Token acquired."

echo
echo "[1/4] Creating tournament..."
tournament_response="$(invoke_api "POST" "$API_BASE_URL/tournaments" "$(json_tournament_payload)")"
TOURNAMENT_ID="$(printf '%s' "$tournament_response" | json_value "id")"
TOURNAMENT_NAME="$(printf '%s' "$tournament_response" | json_value "name")"
echo "Created tournament '$TOURNAMENT_NAME' ($TOURNAMENT_ID)"

echo
echo "[2/4] Creating tatamis..."
for index in 0 1; do
  name="Matte $((index + 1))"
  tatami_response="$(invoke_api "POST" "$API_BASE_URL/tournaments/$TOURNAMENT_ID/tatamis" "$(json_tatami_payload "$name" "$index")")"
  tatami_name="$(printf '%s' "$tatami_response" | json_value "name")"
  echo "Created tatami '$tatami_name'"
done

echo
echo "[3/4] Creating clubs..."
club_names=(
  "JC Musterhausen"
  "Judo-Team Beispielstadt"
  "PSV Testdorf"
  "Judo Akademie Neustadt"
)
club_ids=()

for club_name in "${club_names[@]}"; do
  club_response="$(invoke_api "POST" "$API_BASE_URL/tournaments/$TOURNAMENT_ID/clubs" "$(json_club_payload "$club_name")")"
  club_id="$(printf '%s' "$club_response" | json_value "id")"
  created_club_name="$(printf '%s' "$club_response" | json_value "name")"
  club_ids+=("$club_id")
  echo "Created club '$created_club_name'"
done

echo
echo "[4/4] Creating athletes..."
athletes_payload="$(json_athletes_payload "${club_ids[@]}")"
imported_athletes="$(invoke_api "POST" "$API_BASE_URL/tournaments/$TOURNAMENT_ID/athletes/import?allowDuplicate=true" "$athletes_payload")"
imported_count="$(printf '%s' "$imported_athletes" | json_count)"
echo "Imported $imported_count athletes in one batch."
echo "Distribution: 30 U11, 68 U13, 52 U15; 53 female, 97 male; 4 clubs."

echo
echo "Seed complete."
echo "Tournament ID: $TOURNAMENT_ID"
echo "Open the UI and select 'UI Testturnier 2026'."
echo
echo "-----------------------------------------"
echo "Admin Credentials:"
echo "  Username: admin"
if [[ "$ADMIN_PASSWORD_WAS_PROMPTED" == "true" ]]; then
  echo "  Password: existing password entered interactively"
else
  echo "  Password: $ADMIN_PASSWORD"
fi
echo "-----------------------------------------"
