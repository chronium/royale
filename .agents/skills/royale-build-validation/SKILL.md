---
name: royale-build-validation
description: Build, test, restore, run, package, or validate Royale. Use for .NET 10 commands, launch profiles, OTLP-enabled server runs, shadercross, CI, native packaging, platform validation, or completion evidence.
---

# Royale Build And Validation

## .NET Commands

Dependencies already restored:

```bash
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```

Restore, including fetched SDL3-CS without Android workloads:

```bash
dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1
```

Restore or commands requiring package/network access may need an elevated shell. Do not invent lint, formatting, or test commands that the repository does not configure.

Optional helpers under this skill discover the solution and apply the same rules:

- `scripts/validate-dotnet.sh`
- `scripts/restore-desktop.sh`

## Runtime Validation

Use explicit profiles:

- Server: `--config config/server.development.json`
- Client: `--config config/client.development.json`
- Explicit CLI arguments override profile values.

When starting a server for owner validation, enable OTLP and use an elevated shell from the start; sandboxed OTLP runs are known to hang:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317 dotnet run --project src/Royale.Server/Royale.Server.csproj --no-restore --no-build -- --config config/server.development.json
```

For a finite smoke, add `--run-ticks 302`.

## Validation Selection

- Pure documentation/instruction changes: syntax/metadata checks, PM validation, and relevant repository checks; a full game test run is optional unless code changed.
- Managed code: build plus tests for the affected area; use the full solution when shared contracts or multiple projects changed.
- Protocol/simulation/shared state: serialization/unit tests plus client/server or in-process integration coverage.
- Native bindings: layout/lifecycle tests and the supported local runtime.
- Rendering/input/game feel: automated tests plus explicit owner validation.
- Packaging/platform work: inspect artifact contents and record each platform actually verified.

Report exact commands and outcomes. Distinguish a product failure from a sandbox, missing dependency, unsupported platform, or unavailable GUI limitation.

## Native And Shader Constraints

- `shadercross` is a required executable on `PATH`; `SDL_shadercross` is not vendored.
- Client shader outputs include SPIR-V and Metal; do not invent a DXIL pipeline without an owner decision.
- Keep native versions pinned and validate C# ABI layouts.
- Server artifacts must exclude SDL GPU, ImGui, textures, client shaders, and graphics initialization.
