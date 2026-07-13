#!/bin/sh
set -eu

if [ "$#" -lt 1 ]; then
    echo "Usage: $0 <map-id> [server arguments...]" >&2
    exit 2
fi

MAP_ID=$1
shift

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPOSITORY_ROOT=$(dirname "$SCRIPT_DIR")
OTEL_EXPORTER_OTLP_ENDPOINT=${OTEL_EXPORTER_OTLP_ENDPOINT:-http://127.0.0.1:4317}
export OTEL_EXPORTER_OTLP_ENDPOINT

cd "$REPOSITORY_ROOT"
exec dotnet run \
    --project src/Royale.Server/Royale.Server.csproj \
    --no-restore \
    --no-build \
    -- \
    --config config/server.development.json \
    --map "$MAP_ID" \
    "$@"
