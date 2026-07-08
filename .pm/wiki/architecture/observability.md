---
title: Observability
createdAt: 2026-07-07T17:35:52.6989260Z
modifiedAt: 2026-07-08T06:04:14.7171760Z
---

## Overview

Observability for the dedicated server should make server behavior visible without changing gameplay authority or making local development depend on external infrastructure.

The initial observability stack is intended for development and testing. It should help inspect logs, metrics, traces, networking behavior, connected players, simulation health, and match lifecycle state.

The planned stack is:

- OpenTelemetry in the server
- OpenTelemetry Collector for ingestion and routing
- Prometheus for metrics
- Loki for logs
- Tempo for traces
- Grafana for dashboards and approved development actions

Client observability can be added later, but the first implementation should focus on the dedicated server.

## Server OpenTelemetry v1

`OBS-001` adds the dedicated server OpenTelemetry foundation in `Royale.Diagnostics`.

The server uses `RoyaleTelemetry.CreateServer(LogLevel.Information)` during startup. This bootstrap owns the logger factory, trace provider, and meter provider lifetime. ZLogger console output remains enabled on every server run, including when OTLP export is disabled or unavailable.

Default telemetry identity:

- Service name: `royale-server`, unless `OTEL_SERVICE_NAME` is configured.
- Activity source name: `Royale.Server`.
- Meter name: `Royale.Server`.

OTLP export is disabled by default. Export is enabled only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set to a non-empty absolute URI and `OTEL_SDK_DISABLED` is not `true` case-insensitively. When `OTEL_SDK_DISABLED=true`, no OTLP log exporter, trace provider, or meter provider is created even if an endpoint is present.

The OpenTelemetry SDK/exporter continues to read standard environment variables such as protocol, headers, service/resource attributes, and signal-specific endpoint settings. The bootstrap sets a 1 second local-development default for OTLP exporter and processor shutdown timeouts so an unreachable collector does not stall short server runs. Explicit timeout environment variables override that default, including `OTEL_EXPORTER_OTLP_TIMEOUT`, `OTEL_EXPORTER_OTLP_LOGS_TIMEOUT`, `OTEL_EXPORTER_OTLP_TRACES_TIMEOUT`, `OTEL_EXPORTER_OTLP_METRICS_TIMEOUT`, `OTEL_BSP_EXPORT_TIMEOUT`, `OTEL_BLRP_EXPORT_TIMEOUT`, and `OTEL_METRIC_EXPORT_TIMEOUT`.

The server emits one bounded startup/run/shutdown activity named `royale.server.run`. It is tagged with port, map, tick rate, headless mode, run mode, and ticks run when available. Per-tick tracing and concrete gameplay metrics are intentionally out of scope for `OBS-001`; `OBS-002` owns server metrics and structured gameplay events.

## Core Server Metrics and Events

`OBS-002` adds the first dedicated-server gameplay and networking instrumentation in `Royale.Server`. `ServerObservability` owns OpenTelemetry instruments from `RoyaleTelemetry.ServerMeter` and server loggers. `Royale.Network` exposes small observer callbacks for handshake rejects and invalid accepted-input packets, but it does not depend on `Royale.Diagnostics` or server telemetry types.

Current metric names are:

- `royale.server.players.connected` observable gauge
- `royale.server.players.active` observable gauge
- `royale.server.match.living_players` observable gauge
- `royale.server.match.phase` observable gauge with `phase`
- `royale.server.inputs.queue_depth` observable gauge
- `royale.server.tick.duration` histogram in milliseconds
- `royale.server.snapshots.sent` counter
- `royale.server.packets.received` counter with `message_type`, `delivery`, and `channel`
- `royale.server.packets.invalid` counter with `reason`
- `royale.server.connections.accepted` counter
- `royale.server.connections.disconnected` counter with `reason`
- `royale.server.handshakes.rejected` counter with `reason`

The local Prometheus path sanitizes OpenTelemetry metric names for PromQL. Observed server smoke metric names include:

- `royale_server_players_connected`
- `royale_server_players_active`
- `royale_server_match_living_players`
- `royale_server_match_phase`
- `royale_server_inputs_queue_depth`
- `royale_server_tick_duration_milliseconds_bucket`
- `royale_server_tick_duration_milliseconds_count`
- `royale_server_tick_duration_milliseconds_sum`

Counters are expected to appear with Prometheus counter suffixes, for example `royale_server_snapshots_sent_total` and `royale_server_packets_received_total`, once the corresponding gameplay or networking event has occurred.

Metric labels must remain low-cardinality. Peer IDs, connection IDs, player IDs, endpoint addresses, positions, health, ammunition, and other per-player values are not allowed as metric labels. Peer, connection, and player IDs are allowed in structured server logs when they are needed to debug a lifecycle event.

Structured server event logs currently cover peer connect/disconnect, accepted clients, handshake rejects, invalid accepted-input packets or commands, snapshot batches, and observed match phase changes. The dedicated server keeps these events server-owned; client rendering, SDL, GPU, and ImGui code remain outside the server observability path.

## Per-Player Debug State

`OBS-003` exposes authoritative per-player debug state through bounded structured server logs, not through protocol snapshots, HTTP debug surfaces, or Prometheus metric labels.

`Royale.Server` owns the debug projection with one read-only state value per authoritative player. The projection includes server tick, optional network peer ID, connection ID, player ID, position, velocity, yaw, pitch, health, max health, alive state, weapon ID, magazine ammo, reserve ammo, reload state, last processed input sequence, and queued input count.

`NetworkServerRuntime` emits one structured log event per player at a fixed low cadence through `ServerObservability`. The default cadence is once per second at the 60 Hz server tick rate. No events are emitted when there are no players. These logs are intended for Loki/Grafana inspection of authoritative state while keeping gameplay authority inside the server runtime.

The metrics cardinality rule is explicit: per-player values, including peer ID, connection ID, player ID, position, health, ammunition, and input sequence values, must not be added as Prometheus metric labels. If new per-player diagnostics are needed before an explicit debug endpoint exists, prefer additional bounded structured logs or test artifacts.

## Sandbox Validation Note

When `OTEL_EXPORTER_OTLP_ENDPOINT` is set, short `dotnet run` server smoke tests may hang inside the Codex sandbox even if the same command works outside the sandbox. Agents should not immediately treat this as an application bug.

If an OTLP-enabled server smoke has no server output and does not complete, stop the stuck process, rerun the same command with an elevated shell, and use the elevated result for validation. This preserves the useful check that an unreachable or absent collector does not break normal server startup while accounting for sandbox networking/exporter behavior.

## Goals

The observability system should make it easy to inspect:

- Connected-player count
- Simulation tick timing
- Snapshot send rate
- Packet receive/send counts
- Invalid packet counts
- Input queue depth
- Connection and handshake events
- Match phase
- Living-player count
- Server errors and warnings
- Traceable lifecycle flows such as startup, handshake, disconnect, and match reset

Observability should not become an authority path for gameplay state.

## Metrics Policy

Prometheus metrics must stay low-cardinality.

Good metric dimensions include stable values such as subsystem, packet type, outcome, match phase, or transport direction.

Avoid using player identifiers, connection identifiers, endpoint addresses, positions, health values, or other high-cardinality values as Prometheus labels.

Per-player state such as position, health, ammunition, connection identity, and last processed input should be exposed through structured logs, explicit debug endpoints, or scenario artifacts instead of high-cardinality metrics.

## Logs

Logs should remain structured and subsystem-aware.

Important events include:

- Server start and stop
- Map load
- Client connect and disconnect
- Handshake accept and reject
- Protocol errors
- Invalid packets
- Match phase changes
- Player damage and death
- Admin command execution

Logs must avoid leaking credentials, admin tokens, private endpoints, or sensitive request data.

## Traces

Tracing should focus on bounded flows rather than every simulation tick.

Useful traces include:

- Server startup
- Client handshake
- Disconnect handling
- Match reset
- Snapshot production and send batches, if sampled or bounded

Tracing should not add meaningful overhead to fixed-timestep simulation.

## Kubernetes Deployment Shape

`OBS-004` adds a Kustomize-managed local development observability stack under `deploy/observability/`.

Committed layout:

```text
deploy/
  observability/
    base/
      config/
        prometheus.yml
        loki.yaml
        tempo.yaml
        otel-collector.yaml
      grafana/
        provisioning/
          datasources/
            datasources.yaml
          dashboards/
            dashboards.yaml
        dashboards/
          royale-server-overview.json
          royale-networking.json
          royale-logs-and-traces.json
      namespace.yaml
      grafana.yaml
      prometheus.yaml
      loki.yaml
      tempo.yaml
      otel-collector.yaml
      kustomization.yaml
    local/
      config/
        prometheus.yml.example
        loki.yaml.example
        tempo.yaml.example
        otel-collector.yaml.example
      .gitignore
      kustomization.yaml.example
      README.md
```

The base deploys into the `royale-observability` namespace. It uses separate Deployments and Services for Grafana, Prometheus, Loki, Tempo, and the OpenTelemetry Collector. Service and Deployment manifests do not embed application configuration payloads directly; `base/kustomization.yaml` generates ConfigMaps from standalone committed files with stable names.

Generated ConfigMaps:

- `prometheus-config` from `base/config/prometheus.yml`
- `loki-config` from `base/config/loki.yaml`
- `tempo-config` from `base/config/tempo.yaml`
- `otel-collector-config` from `base/config/otel-collector.yaml`
- `grafana-datasource-provisioning` from `base/grafana/provisioning/datasources/datasources.yaml`
- `grafana-dashboard-provisioning` from `base/grafana/provisioning/dashboards/dashboards.yaml`
- `grafana-dashboards` from `base/grafana/dashboards/*.json`

Grafana mounts provisioning as read-only ConfigMaps:

- `/etc/grafana/provisioning/datasources` for datasource provisioning
- `/etc/grafana/provisioning/dashboards` for dashboard provider provisioning
- `/var/lib/grafana/dashboards` for committed dashboard JSON files

Grafana provisions these datasource UIDs:

- `royale-prometheus` -> `http://prometheus:9090`
- `royale-loki` -> `http://loki:3100`
- `royale-tempo` -> `http://tempo:3200`

Grafana provisions the `Royale` folder with these starter dashboards:

- `Royale Server Overview` (`royale-server-overview`)
- `Royale Networking` (`royale-networking`)
- `Royale Logs and Traces` (`royale-logs-traces`)

`Royale Server Overview` covers connected players, active players, living players, match phase, server tick duration, input queue depth, and snapshots sent. `Royale Networking` covers packet receive rates, invalid packets, accepted/disconnected connections, and rejected handshakes. `Royale Logs and Traces` uses the `service_name="royale-server"` Loki stream and a Tempo TraceQL search for `resource.service.name="royale-server"`, with dashboard links into Explore.

Grafana, Prometheus, Loki, and Tempo use default-StorageClass PVCs by default:

- Grafana: `grafana-data`, `1Gi`
- Prometheus: `prometheus-data`, `5Gi`
- Loki: `loki-data`, `5Gi`
- Tempo: `tempo-data`, `5Gi`

Pinned base images:

- `grafana/grafana:13.1.0`
- `prom/prometheus:v3.13.0`
- `grafana/loki:3.7.3`
- `grafana/tempo:2.10.7`
- `otel/opentelemetry-collector-contrib:0.156.0`

Service ports:

- Grafana: HTTP `3000`
- Prometheus: HTTP `9090`
- Loki: HTTP `3100`, gRPC `9095`
- Tempo: HTTP `3200`, OTLP gRPC `4317`, OTLP HTTP `4318`
- OpenTelemetry Collector: OTLP gRPC `4317`, OTLP HTTP `4318`, Prometheus scrape endpoint `9464`, health check `13133`

Collector routing:

- Metrics: OTLP receiver to Prometheus exporter on `:9464`; Prometheus scrapes `otel-collector:9464`.
- Traces: OTLP receiver to Tempo over OTLP gRPC at `tempo:4317`.
- Logs: OTLP receiver to Loki over OTLP HTTP at `http://loki:3100/otlp`, relying on Loki `3.7.3` OTLP ingestion support.

The local overlay is intentionally machine-specific. `deploy/observability/local/.gitignore` ignores `kustomization.yaml`, copied local config definitions under `local/config/`, secret YAML files, local patch directories, and other local override files. Developers should copy `kustomization.yaml.example` to `kustomization.yaml`. To replace generated ConfigMaps locally, copy the matching `local/config/*.example` file to the ignored non-example filename and uncomment the corresponding `configMapGenerator behavior: replace` block in the local kustomization. Storage classes, resource limits, local port policy, secrets, and local config definitions stay uncommitted.

Validation commands:

```sh
kubectl kustomize deploy/observability/base
cp deploy/observability/local/kustomization.yaml.example deploy/observability/local/kustomization.yaml
kubectl kustomize deploy/observability/local
kubectl apply --dry-run=client -k deploy/observability/local
```

Optional local-cluster use:

```sh
kubectl apply -k deploy/observability/local
kubectl -n royale-observability rollout status deployment/grafana
kubectl -n royale-observability get pods,svc,pvc
kubectl -n royale-observability port-forward svc/grafana 3000:3000
kubectl -n royale-observability port-forward svc/otel-collector 4317:4317
kubectl -n royale-observability port-forward svc/prometheus 9090:9090
kubectl -n royale-observability port-forward svc/loki 3100:3100
kubectl -n royale-observability port-forward svc/tempo 3200:3200
```

To validate end-to-end local telemetry, run a finite server smoke with OTLP export enabled:

```sh
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317 \
dotnet run --project src/Royale.Server/Royale.Server.csproj --no-restore -- --map graybox --run-ticks 300
```

After one Prometheus scrape interval, query Prometheus for `royale_server_players_connected` and `royale_server_tick_duration_milliseconds_count`, query Loki with `{service_name="royale-server"}`, and query Tempo with TraceQL `{resource.service.name="royale-server"}`. The dashboards should appear in Grafana without manual import.

## Grafana Actions

Grafana actions may be used for development operations, but only through an explicit server-side admin command surface.

Possible actions include:

- Toggle simulated latency
- Toggle simulated jitter
- Toggle simulated packet loss
- Toggle simulated packet delay
- Force-start a development match
- Reset a match

The server must validate and log every admin command.

The admin command surface must be development-only or explicitly authenticated. Grafana must not become an implicit gameplay authority path.

## Task Plan

The OBS track owns this work:

- `OBS-001` adds the OpenTelemetry server foundation.
- `OBS-002` adds core server metrics and structured events.
- `OBS-003` exposes per-player debug state safely.
- `OBS-004` adds the local Kubernetes observability stack.
- `OBS-005` adds Grafana datasources and starter dashboards.
- `OBS-006` adds the secured development admin command surface.
- `OBS-007` wires Grafana actions to approved admin commands.
