---
id: PLATFORM-009
title: Extend development server join window
track: PLATFORM
createdAt: 2026-07-10T12:57:57.3623790Z
modifiedAt: 2026-07-10T12:59:01.5068790Z
---

Increase the committed development server profile waiting window from 5 seconds to 60 seconds so agent-driven port checks and client startup do not miss roster admission. Keep the development preparation window at 5 seconds and update tests and wiki documentation.

## Notes

- 2026-07-10 12:59 UTC - Changed `config/server.development.json` to use `waitingSeconds: 60` while retaining `preparationSeconds: 5`. Updated the launch-profile test and `architecture/runtime-processes` wiki documentation. Validation: `dotnet test tests/Royale.Server.Tests/Royale.Server.Tests.csproj -m:1 --no-restore` passed all 164 tests.