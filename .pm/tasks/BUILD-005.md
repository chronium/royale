---
id: BUILD-005
title: Add WattleScript automation harness
track: BUILD
milestone: M4
createdAt: 2026-07-05T12:35:08.5145500Z
modifiedAt: 2026-07-05T12:35:40.0380580Z
---

Add WattleScript as a pinned third-party dependency and create a sandboxed Wattle-mode automation harness for scripted gameplay testing. The harness should expose explicit test-only commands for driving in-process client/server scenarios, waiting simulation ticks, inspecting state, and asserting outcomes without becoming a gameplay scripting system or weakening server authority.