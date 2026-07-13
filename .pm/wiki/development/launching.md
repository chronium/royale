---
title: Development Launch Scripts
createdAt: 2026-07-13T06:30:02.0969700Z
modifiedAt: 2026-07-13T17:02:01.8811810Z
---

## Purpose

The `launch/` directory contains thin POSIX shell wrappers for starting development processes from any working directory. They use existing build output with `--no-restore --no-build`; restore and build the solution before launching.

## Commands

```sh
launch/editor.sh
launch/server.sh <map-id> [server arguments...]
launch/client-offline.sh [client arguments...]
launch/client-connected.sh [client arguments...]
```

`editor.sh` restores the editor's normal recent-project/default-map startup behavior. `server.sh` uses `config/server.development.json` and requires the map ID as its first positional argument. `client-offline.sh` uses the offline client profile. `client-connected.sh` uses the connected development profile, which defaults to `127.0.0.1:7777` and `graybox`.

Every script forwards remaining arguments to the underlying application, so ordinary launch options override profile values. For example:

```sh
launch/server.sh prototype-arena --port 7788
launch/client-offline.sh --map prototype-arena
launch/client-connected.sh --connect 192.0.2.10 --port 7788 --map prototype-arena
launch/editor.sh --project /path/to/arena.royaleproject
```

## Runtime content overrides

Both client and server accept `--map-file <path>` and `--asset-root <path>`. `--map-file` loads a standalone runtime map document; when `--map` is also present, its ID must match the loaded document. `--asset-root` points directly to the audience-specific directory containing `model-assets.json`. Without overrides, map lookup and packaged `AppContext.BaseDirectory/assets` behavior are unchanged.

The server emits the stable line `ROYALE_SERVER_READY map=<id> port=<port>` after UDP binding and authoritative simulation, collision, navigation, and spawn content initialization have succeeded. Development launch supervisors may use this marker before starting a connected client.

The editor's managed Save and Launch workflow invokes the existing scripts with explicit matching map file, client/server asset roots, host, and port. Paths are passed as discrete process arguments, so spaces do not require editor-side shell quoting.

## Observability

The wrappers export `OTEL_EXPORTER_OTLP_ENDPOINT`, defaulting to `http://127.0.0.1:4317`. An existing environment value takes precedence. In Codex, server launches with OTLP must run in an elevated shell because sandboxed OTLP shutdown may hang.