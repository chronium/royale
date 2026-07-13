#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPOSITORY_ROOT=$(dirname "$SCRIPT_DIR")
OTEL_EXPORTER_OTLP_ENDPOINT=${OTEL_EXPORTER_OTLP_ENDPOINT:-http://127.0.0.1:4317}
export OTEL_EXPORTER_OTLP_ENDPOINT

cd "$REPOSITORY_ROOT"
exec dotnet run \
    --project src/Royale.Client/Royale.Client.csproj \
    --no-restore \
    --no-build \
    -- \
    --config config/client.production.json \
    --offline \
    "$@"
