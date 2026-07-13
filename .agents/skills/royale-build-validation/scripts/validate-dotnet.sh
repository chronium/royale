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
  echo "Run dotnet build/test explicitly with the intended solution." >&2
  exit 1
fi

solution="${solutions[0]}"
echo "Building $solution"
dotnet build "$solution" --no-restore

echo "Testing $solution"
dotnet test "$solution" --no-restore
