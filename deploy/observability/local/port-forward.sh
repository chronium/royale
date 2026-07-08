#!/usr/bin/env bash
set -euo pipefail

namespace="${1:-royale-observability}"
pids=()

cleanup() {
  if ((${#pids[@]} > 0)); then
    kill "${pids[@]}" 2>/dev/null || true
  fi
}

trap cleanup EXIT INT TERM

start_forward() {
  local service="$1"
  local local_port="$2"
  local remote_port="$3"

  echo "Forwarding ${service} ${local_port}:${remote_port} in namespace ${namespace}"
  kubectl -n "${namespace}" port-forward "svc/${service}" "${local_port}:${remote_port}" &
  pids+=("$!")
}

start_forward grafana 3000 3000
start_forward prometheus 9090 9090
start_forward loki 3100 3100
start_forward tempo 3200 3200
start_forward otel-collector 4317 4317
start_forward otel-collector 4318 4318

cat <<EOF

Local observability forwards are starting:

  Grafana:        http://127.0.0.1:3000
  Prometheus:     http://127.0.0.1:9090
  Loki:           http://127.0.0.1:3100
  Tempo:          http://127.0.0.1:3200
  OTLP gRPC:      http://127.0.0.1:4317
  OTLP HTTP:      http://127.0.0.1:4318

Set the server endpoint with:

  OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317

Press Ctrl-C to stop all port-forwards.
EOF

while true; do
  for pid in "${pids[@]}"; do
    if ! kill -0 "${pid}" 2>/dev/null; then
      wait "${pid}"
      exit $?
    fi
  done

  sleep 1
done
