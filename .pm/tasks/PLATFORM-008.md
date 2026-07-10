---
id: PLATFORM-008
title: Add JSON launch configuration profiles
track: PLATFORM
milestone: M6
priority: high
dependsOn:
- PLATFORM-005
- BOT-003
createdAt: 2026-07-10T07:37:30.7800820Z
modifiedAt: 2026-07-10T07:54:48.9008910Z
---

Add explicit `--config <path>` support to the client and dedicated server with strict System.Text.Json profiles. Merge built-in defaults, the selected profile, then explicit CLI arguments. Commit production and development profiles for each executable, copy only applicable profiles to build and publish output, retain CLI overrides, reject missing/malformed/unknown/duplicate configuration, and document/test the configuration contracts.

## Notes

- 2026-07-10 07:54 UTC - Implemented strict System.Text.Json launch profiles for client and server with explicit `--config`, working-directory-relative paths, defaults/profile/CLI precedence, duplicate/missing/malformed/unknown-field rejection, comments and trailing commas, strict null/value validation, and post-merge cross-field validation. Added four committed production/development profiles and project-specific build/publish copying. Updated README, architecture/runtime-processes, royale-build-validation, and royale-source-control-implementation. Validation passed: focused server tests (157), focused client tests (267), full `dotnet build Royale.slnx -m:1 --no-restore` (0 warnings/errors), full `dotnet test Royale.slnx -m:1 --no-restore --no-build` (816 tests), server/client publish-output profile separation, and OTLP development-profile smoke with CLI `--run-ticks 302` override (8 bots filled after the configured five-second wait). Human follow-up: launch the development server and development client profiles together and confirm the client connects without endpoint CLI arguments.