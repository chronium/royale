---
id: BUILD-020
title: Establish C# formatting policy and clean editor sources
track: BUILD
milestone: M6
createdAt: 2026-07-12T08:33:36.5830460Z
modifiedAt: 2026-07-12T08:41:51.9525950Z
---

Add repository C# formatting configuration, expand compressed editor code into readable structure, apply the formatter, and document a reproducible verification command.

## Notes

- 2026-07-12 08:41 UTC - Added a root .editorconfig with standard C# whitespace/newline policy and IDE0055 enforcement, while suppressing formatting diagnostics for vendored third-party sources. Manually expanded compressed declarations/statements in EditorApplication, EditorLaunchOptions, EditorWorkspaceState, and the EDITOR-004 document/source/persistence/launch tests; Roslyn does not split deliberately compressed statements automatically. README now documents the editor-scoped apply/verify command. No wiki page was changed because no existing wiki page owns general repository formatting workflow and README is the current build/test entry point. Validation: editor-scoped `dotnet format ... --verify-no-changes` passed; `dotnet build Royale.slnx -m:1 --no-restore` succeeded with 0 errors and 19 warnings from freshly rebuilt vendored BlurgText/SimpleMesh/WattleScript sources; `dotnet test tests/Royale.Editor.Tests/Royale.Editor.Tests.csproj -m:1 --no-restore --no-build` passed 42/42; `git diff --check` passed. Full-solution format verification remains intentionally out of scope because it reports thousands of pre-existing violations across first-party and vendored/generated code.