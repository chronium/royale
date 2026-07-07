---
id: BUILD-018
title: Add WattleScript third-party dependency
track: BUILD
milestone: M4
createdAt: 2026-07-07T04:34:18.1486670Z
modifiedAt: 2026-07-07T04:34:18.1486670Z
---

Pin WattleScript under `thirdparty` using the project fetch-script workflow, without submodules. Add a depth-1 clone script for the selected WattleScript repository and commit, document the pin in the third-party wiki, and ensure cloned repositories and generated artifacts remain ignored.