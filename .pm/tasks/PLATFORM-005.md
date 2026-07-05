---
id: PLATFORM-005
title: Add launch arguments
track: PLATFORM
milestone: M1
createdAt: 2026-07-04T09:21:27.0274330Z
modifiedAt: 2026-07-04T09:22:36.7141680Z
---

Support client connection, offline mode, server port, and map selection through command-line arguments.

## Completion Notes

Implemented strict launch argument parsing for the client and server.

Client flags:

```text
--offline
--connect <host>
--port <port>
--map <map-id>
--screenshot <path>
--screenshot-after-frames <frame-count>
```

Server flags:

```text
--port <port>
--map <map-id>
```

Defaults are offline client mode, port `7777`, and map `graybox` via `ContentCatalog.DefaultMapId`. Unknown flags, missing values, empty values, ports outside `1..65535`, invalid screenshot frame counts, and `--offline --connect <host>` are rejected.

`--connect` only captures and logs the intended remote endpoint. Real transport behavior remains deferred to networking tasks.

Validation run:

```text
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
dotnet run --project src/Royale.Server/Royale.Server.csproj --no-restore -- --port 7778 --map graybox
```

Wiki updated:

```text
architecture/runtime-processes
architecture/networking
```