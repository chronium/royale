#!/usr/bin/env bash
set -euo pipefail

mapfile -t solutions < <(find . -maxdepth 3 \( -name '*.slnx' -o -name '*.sln' \) -not -path './bin/*' -not -path './obj/*' | sort)

if [[ ${#solutions[@]} -eq 0 ]]; then
  echo "No .slnx or .sln file found within depth 3." >&2
  exit 1
fi

if [[ ${#solutions[@]} -gt 1 ]]; then
  echo "Multiple solution files found; refusing to guess:" >&2
  printf '  %s
' "${solutions[@]}" >&2
  echo "Run dotnet restore explicitly with the intended solution." >&2
  exit 1
fi

solution="${solutions[0]}"
echo "Restoring $solution with desktop target property"
dotnet restore "$solution" -p:CI_DONT_TARGET_ANDROID=1
