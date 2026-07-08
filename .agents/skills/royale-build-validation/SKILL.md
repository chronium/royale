---
name: royale-build-validation
description: Royale build, restore, test, CI, packaging, validation, .NET 10, shadercross, SDL_shadercross, native package validation, or commands for checking completed work.
---

# Royale Build and Validation

Use this skill for restore/build/test commands, CI, packaging, validation selection, .NET SDK issues, shader compilation, native package validation, and completion checks.

## .NET CLI usage

The project targets .NET 10.

In Codex or sandboxed sessions, run .NET commands in single-node mode and without restore once dependencies have already been restored:

```bash
dotnet build <solution>.slnx -m:1 --no-restore
dotnet test <solution>.slnx -m:1 --no-restore
```

After fetching SDL3-CS from source, restore with the desktop target property so the fetched binding does not require Android workloads:

```bash
dotnet restore <solution>.slnx -p:CI_DONT_TARGET_ANDROID=1
```

Any command that requires NuGet package access or restore may require an elevated or network-enabled shell in sandboxed environments.

Server smoke tests with `OTEL_EXPORTER_OTLP_ENDPOINT` set may hang inside the Codex sandbox even when the same command succeeds normally outside it. If an OTLP-enabled `dotnet run` smoke appears stuck with no server output, treat it as likely sandbox networking/exporter behavior: stop the stuck process, rerun the same command with an elevated shell, and record whether the elevated run passes before diagnosing application code.

Do not invent build, lint, format, or test commands. If no command exists yet, say so. Once commands are introduced, document them in `AGENTS.md`/skills and in the wiki when appropriate.

## Optional helper scripts

This skill includes optional helpers:

```text
.agents/skills/royale-build-validation/scripts/validate-dotnet.sh
.agents/skills/royale-build-validation/scripts/restore-desktop.sh
```

Use them only when shell scripts are appropriate for the environment.

- `validate-dotnet.sh` discovers a single `.slnx` or `.sln` and runs build/test with `-m:1 --no-restore`.
- `restore-desktop.sh` discovers a single `.slnx` or `.sln` and runs restore with `-p:CI_DONT_TARGET_ANDROID=1`.
- They intentionally fail when multiple solution files exist so the agent does not guess.

## Shadercross

Client shader builds require the `shadercross` executable to be available on `PATH`.

The client project compiles HLSL sources under `src/Royale.Client/Shaders/` to:

- SPIR-V outputs (`.spv`).
- Metal outputs (`.msl`).

The client also copies the original HLSL files for Direct3D/DXIL-facing development until a DXIL flow is explicitly chosen.

`SDL_shadercross` is a local build tool dependency and is not vendored through `thirdparty`.

Do not invent a DXIL flow unless the project owner explicitly chooses it.

## Native dependency validation

The project uses SDL3, SDL GPU, Box3D, and ImGui-related bindings or integration.

For native/build work:

- Keep native dependency layout explicit and consistent across supported runtime identifiers.
- Pin native dependency versions once they are chosen.
- Keep Box3D bindings focused on the API surface needed by the game.
- Verify native memory layouts for C# bindings with tests.
- Package only the native libraries required by each artifact.
- The Linux server package must not depend on SDL GPU, ImGui, textures, client shaders, or graphics initialization.
- If a native dependency decision is unclear, ask before assuming.

## Test expectations

Add tests at the level where behavior lives.

Expected test areas include:

- Protocol serialization and version handling.
- Input buffering and sequence comparisons.
- Match-state transitions.
- Safe-zone interpolation and damage.
- Weapon fire cadence and damage rules.
- Box3D structure layouts and binding behavior.
- Player movement collision cases.
- Headless server simulation.
- In-process client/server integration.
- Consecutive match reset behavior.

For cross-platform or native work, document what was verified locally and what remains to be verified on other platforms.

## Validation checklist

Before marking a task complete, verify:

- The selected PM task is the work that was actually completed.
- The implementation follows the documented architecture.
- Ambiguous behavior was clarified with the user instead of assumed.
- Relevant tests were added or updated.
- Relevant build and test commands were run, or unavailable commands were explicitly noted.
- The wiki was updated if source-of-truth documentation changed.
- No direct `.pm/` storage edits were made.
- Native and cross-platform implications were considered.
- The server remains free of client rendering and UI dependencies.
- No unrelated changes or generated artifacts were introduced.

## Reporting

In the final response or task note, report:

- Exactly which validation commands ran.
- Whether they passed or failed.
- Any environment reason validation could not run.
- Any human validation still needed for rendering, game feel, platform-specific behavior, audio/visual feedback, or UI/debug tooling.
