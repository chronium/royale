---
id: BUILD-001
title: Create the .NET solution
track: BUILD
milestone: M0
createdAt: 2026-07-04T09:21:21.5829070Z
modifiedAt: 2026-07-04T09:22:33.6199340Z
---

Create the client, server, shared simulation, protocol, content, Box3D binding, wrapper, and test projects targeting .NET 10.

## Notes

- Created `Royale.slnx` with client, server, shared simulation, protocol, content, Box3D binding, Box3D wrapper, and focused xUnit test projects.
- Pinned the SDK with `global.json` at `10.0.301`; shared `net10.0`, nullable, implicit usings, and deterministic build settings live in `Directory.Build.props`.
- Centralized xUnit test package versions in `Directory.Packages.props`.
- Verified the server project references only `Royale.Simulation`, `Royale.Protocol`, `Royale.Content`, and `Royale.Box3D`; no client, rendering, SDL, or ImGui dependency was introduced.
- No wiki update was needed because the architecture wiki already describes this solution shape as a possible layout and explicitly allows exact project naming and count to evolve.

## Validation

- `dotnet restore Royale.slnx -m:1`
- `dotnet build Royale.slnx -m:1 --no-restore`
- `dotnet test Royale.slnx -m:1 --no-restore`
