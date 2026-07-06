---
id: PLATFORM-006
title: Set default client window resolution
track: PLATFORM
milestone: M2
createdAt: 2026-07-06T13:59:11.3794810Z
modifiedAt: 2026-07-06T13:59:56.9943310Z
---

Change the default client window size to 1920x1080 so the game is easier to inspect on ultrawide desktop displays. Keep the window resizable and high-DPI behavior unchanged.

## Notes

- 2026-07-06 13:59 UTC - Changed the default SDL client window size from 1280x720 to named defaults of 1920x1080. Window remains resizable and high-DPI. No wiki update was needed because this is a local client launch default, not an architecture, workflow, or platform support contract change. Validation: `dotnet build Royale.slnx -m:1 --no-restore` passed; `dotnet test Royale.slnx -m:1 --no-restore` passed with 277 tests.