---
id: TEST-001
title: Integrate WattleScript into the test host
track: TEST
milestone: M4
priority: medium
dependsOn:
- BUILD-018
createdAt: 2026-07-05T15:17:23.5211820Z
modifiedAt: 2026-07-07T04:34:20.8812590Z
---

Add WattleScript to a dedicated gameplay test host without introducing it as a runtime dependency of the client, server, or authoritative simulation.

## Completion Notes

Implemented the initial gameplay test host as `tests/Royale.Gameplay.Tests` and added it to `Royale.slnx` under `/tests/`.

The project references the pinned WattleScript interpreter source at `thirdparty/repos/wattlescript/src/WattleScript.Interpreter/WattleScript.Interpreter.csproj`.

`WattleScenarioScriptHost` creates `Script` with `CoreModules.Preset_HardSandboxWattle`, sets `script.Options.Syntax = ScriptSyntax.Wattle`, and executes script source with `DoString`.

Smoke tests cover Wattle syntax execution, simple value return, failure of the same Wattle-specific snippet under default Lua syntax, and absence of obvious IO/system access through `io` and unsafe `os.execute`.

Validation run:

* `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1`
* `dotnet build Royale.slnx -m:1 --no-restore`
* `dotnet test Royale.slnx -m:1 --no-restore`
* `rg "WattleScript.Interpreter|wattlescript" -n src tests --glob '*.csproj' --glob '*.props' --glob '*.targets'`

Dependency isolation confirmed: the WattleScript project reference appears only in `tests/Royale.Gameplay.Tests`; no project under `src/` references WattleScript.