---
title: Diagnostics
createdAt: 2026-07-05T19:44:39.3163150Z
modifiedAt: 2026-07-10T18:49:40.4156720Z
---

## Logging

Royale uses `Microsoft.Extensions.Logging` with ZLogger as the concrete logging implementation for M1 client and server processes.

The shared logging policy lives in `src/Royale.Diagnostics`. Client and server entry points create their console logger factories through `RoyaleLogging.CreateConsoleLoggerFactory(LogLevel minimumLevel)` so both processes use the same formatting and filtering behavior.

The initial sink is stdout through ZLogger console logging. File logging, JSON-lines output, external log shipping, and runtime log-level configuration are intentionally out of scope until launch arguments or server configuration require them.

Expected readable console shape:

```text
2026-07-05 16:45:12.345 [INF] Royale.Server: Royale server skeleton ready. Protocol 1, map default, tick 60 Hz, headless True.
```

Each line includes:

* UTC timestamp
* three-character log level
* logger category, used as the subsystem
* rendered message

Application code should use ZLogger extension methods such as `ZLogInformation`, `ZLogWarning`, and `ZLogCritical` at call sites. Do not pass interpolated strings to normal `LogInformation` methods. Avoid per-frame logging; high-frequency diagnostics should go through debug overlays or sampled metrics until a runtime logging policy exists.

## Current Lifecycle Logs

The server currently logs startup skeleton details through the shared logger factory.

The client currently logs startup beginning, SDL video initialization, SDL window creation, SDL GPU device creation, shutdown, and fatal startup errors.

## Client Telemetry Overlay

The SDL client starts with a read-only `Telemetry` ImGui window visible. Press `F3` to hide or show the complete diagnostics surface. Hiding telemetry also hides the separate `Training Dummy` diagnostics window, while ImGui frame processing and rendering remain active.

The resizable window contains collapsible sections:

* **Frame** — frame milliseconds/FPS.
* **Renderer** — active and launch camera modes, launch position/look-at overrides, render-view mode, relative mouse capture, loaded map and static-content counts, loaded model-asset count, and screenshot status, target frame, completed frames, and wrapped output path.
* **Simulation** — fixed ticks this frame, total client tick, latest authoritative server tick and tick difference, pending/replayed prediction inputs, reconciliation count, and last correction distance.
* **Player** — offline or latest-authoritative position, velocity, look, health/alive state, grounded state when the offline/prediction controller provides it, and weapon/ammunition state. Offline kill/respawn and weapon-feedback diagnostics remain here.
* **Physics** — offline or prediction collision-world availability, static collider count, and prediction active/seeded state.
* **Server** — latest snapshot tick, match phase/start tick, player/living counts, winner, and safe-zone state.
* **Network** — one-way latency, RTT, smoothed latency jitter, transport packet/byte/loss totals, MTU, input/snapshot/error counters, and remote interpolation state.
* **Connection** — offline/connect mode, endpoint, peer, handshake status, accepted identifiers, and the last disconnect or socket error.

Offline mode omits the Server and Network sections. Connect mode reports explicit transport-connection, handshake-acceptance, and first-snapshot waiting states instead of displaying default or fabricated server values.

Field ownership remains explicit. Frame values are client-owned. Renderer values come from the active camera controller, immutable launch options, render-view controller, SDL window, loaded map, model-asset cache, and completed frame count. Offline player and physics values come from the local controller and loaded map. Prediction and interpolation values come from `NetworkClientRuntime`. Packet totals, RTT, MTU, and loss are game-owned projections of optional transport diagnostics. Server, match, player gameplay state, weapon ammunition, and safe-zone values come only from `ClientNetworkState.LatestSnapshot`; the overlay does not query the server simulation or Grafana.

The `Training Dummy` window remains separate specialized offline tooling. Latency/loss simulation controls, historical charts, and renderer timing remain outside this overlay.

### Sprint diagnostics

Player telemetry reports effective sprint state as `Sprinting: yes` or `Sprinting: no` for both offline simulation and authoritative network snapshots. Server player debug records and bounded structured logs expose the same effective `Sprinting` boolean alongside stance and capsule height. Wattle server-debug and snapshot wrappers expose `sprinting` for deterministic inspection. Sprint does not introduce per-player OpenTelemetry metric labels.

### Crouch diagnostics

Player telemetry reports `Standing` or `Crouched` and the active capsule height for both offline state and authoritative network snapshots. Bounded server player-debug records and structured logs expose the authoritative stance and capsule height. WattleScript snapshot/debug wrappers expose `crouched`, stable `stance` text, and `capsuleHeight`.