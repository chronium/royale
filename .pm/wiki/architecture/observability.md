---
title: Observability
createdAt: 2026-07-07T17:35:52.6989260Z
modifiedAt: 2026-07-07T17:54:42.2914090Z
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

The local development observability stack should use separate Kubernetes Deployments and Services, not a single all-in-one pod.

Expected workloads are:

- Grafana Deployment and Service
- Prometheus Deployment, Service, and persistent storage where needed
- Loki Deployment, Service, and persistent storage where needed
- Tempo Deployment, Service, and persistent storage where needed
- OpenTelemetry Collector Deployment and Service

These resources should be deployable as one unit through Kustomize.

Repository layout should follow this shape unless a task explicitly changes it:

```text
deploy/
  observability/
    base/
      namespace.yaml
      otel-collector.yaml
      prometheus.yaml
      loki.yaml
      tempo.yaml
      grafana.yaml
      dashboards/
      datasources/
      kustomization.yaml
    local/
      kustomization.yaml.example
```

Machine-specific overlays, secrets, hostnames, storage classes, local ports, and private configuration should not be committed.

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
