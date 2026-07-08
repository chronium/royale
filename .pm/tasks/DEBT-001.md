---
id: DEBT-001
title: Investigate Grafana Tempo datasource gRPC log noise
track: DEBT
milestone: M5
priority: low
dependsOn:
- OBS-005
createdAt: 2026-07-08T05:50:07.1311580Z
modifiedAt: 2026-07-08T05:50:10.5207390Z
---

Grafana provisions the Royale Tempo datasource and Tempo TraceQL dashboard successfully, and Tempo answers trace search requests with HTTP 200, but Grafana logs repeated info-level gRPC connection messages against `tempo:3200` when the trace dashboard is opened: `error reading server preface: http2: failed reading the frame payload: http2: frame too large`. Determine whether the Tempo datasource should use a different Grafana setting, endpoint, protocol option, or dashboard query configuration to avoid noisy logs while preserving working trace search.