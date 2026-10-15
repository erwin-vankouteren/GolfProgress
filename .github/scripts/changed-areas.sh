#!/usr/bin/env bash
# Decides which CI areas a change touches (#59).
#
#   changed-areas.sh BASE HEAD    -> prints api=true|false, web=..., android=..., ios=...
#   changed-areas.sh --all        -> every area true (manual runs, new branches)
#
# Each line is a GitHub Actions output (key=value). An area runs when any changed file
# matches its patterns. Shared inputs run every area that depends on them: the OpenAPI
# spec and the design tokens feed all three clients, and the CI workflow itself runs
# everything.
set -euo pipefail

areas=(api web android ios)

# Extended regexes over repository-relative paths.
declare -A patterns=(
  [api]='^(src/|tests/|api/|\.config/dotnet-tools\.json$|global\.json$|GolfProgress\.sln$|\.github/workflows/api\.yml$)'
  [web]='^(clients/web/|design/|api/openapi\.json$|\.github/workflows/web\.yml$)'
  [android]='^(clients/android/|design/|api/openapi\.json$|\.github/workflows/android\.yml$)'
  [ios]='^(clients/ios/|design/|api/openapi\.json$|\.github/workflows/ios\.yml$)'
)

# Changes to these run every area.
everything='^(\.github/workflows/ci\.yml$|\.github/scripts/|\.gitattributes$)'

emit_all() {
  for area in "${areas[@]}"; do echo "$area=true"; done
}

if [[ "${1:-}" == "--all" ]]; then
  emit_all
  exit 0
fi

base="${1:?usage: changed-areas.sh BASE HEAD | --all}"
head="${2:?usage: changed-areas.sh BASE HEAD | --all}"

# A push that creates a branch has no previous commit to compare with.
if [[ "$base" =~ ^0+$ ]]; then
  emit_all
  exit 0
fi

changed="$(git diff --name-only "$base" "$head")"

if grep -Eq "$everything" <<< "$changed"; then
  emit_all
  exit 0
fi

for area in "${areas[@]}"; do
  if grep -Eq "${patterns[$area]}" <<< "$changed"; then
    echo "$area=true"
  else
    echo "$area=false"
  fi
done
