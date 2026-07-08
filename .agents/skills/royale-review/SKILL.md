---
name: royale-review
description: Royale code review guidance. Use when reviewing diffs, PRs, completed agent work, regressions, authority leaks, protocol changes, native interop, missing tests, missing wiki updates, or cross-platform risks.
---

# Royale Review Guidance

Use this skill when reviewing code changes, PR-style diffs, completed agent work, regressions, or architectural changes.

## Review priorities

Prioritize findings in this order:

1. Correctness and server authority.
2. Data safety and protocol compatibility.
3. Cross-platform behavior.
4. Test coverage.
5. Wiki accuracy.
6. Dependency direction.
7. Debuggability and observability.
8. Performance based on measurement.
9. Simplicity and maintainability.
10. Visual and interaction polish.

## Red flags to actively look for

Flag:

- Client authority leaks.
- Undocumented protocol changes.
- Rendering dependencies in server code.
- Speculative engine abstractions.
- Missing wiki updates.
- Missing tests.
- Unsafe native interop.
- Unclear ownership.
- Changes made without asking when requirements were ambiguous.
- Direct `.pm/` writes.
- Generated artifacts committed accidentally.
- Native package changes that pull client dependencies into the Linux server package.
- In-process transport shortcuts that bypass the real client/server message flow.
- Optimizations not backed by measurements.

## Area-specific review checks

For gameplay/simulation:

- Server remains authoritative over simulation, movement validation, combat, health, ammo, safe zone, match phases, eliminations, winners, and reset.
- Fixed tick behavior is preserved.
- Catch-up ticks are bounded.
- Prediction/reconciliation remains understandable.
- Gameplay changes have tests and wiki updates.

For networking/protocol:

- Clients send input commands and connection messages.
- Servers send authoritative snapshots and events.
- Message versioning, identity, sequencing, and acknowledgements are adequate.
- Protocol-incompatible clients/servers fail clearly.
- Real, in-process, test, and simulated-loss transports preserve the same message flow.

For client/rendering/native:

- Rendering/UI dependencies do not reach server packages.
- ImGui remains development tooling.
- Native dependency layout is explicit and platform-aware.
- C# binding memory layouts are tested.
- Shader workflow remains documented.
- Human visual/input/platform validation is requested when automated tests cannot cover behavior.

For build/validation:

- Commands are not invented.
- Relevant build/test commands ran or the reason they could not run is clear.
- Restore/build/test workflow changes are documented.
- Cross-platform and native implications are called out.

For PM/wiki:

- Work corresponds to the selected PM task.
- Task state was moved through PM MCP tools.
- Task notes capture meaningful decisions/blockers.
- Wiki source-of-truth pages were updated when behavior, architecture, protocol, build, diagnostics, or gameplay rules changed.
- `.pm/` was not hand-edited.

## Review response style

- Lead with concrete issues, not praise.
- Tie each issue to a specific behavior, file, or risk.
- Explain why it matters for Royale's architecture or MVP.
- Suggest the smallest correction when clear.
- Distinguish blocking correctness issues from follow-up improvements.
- Avoid requesting broad rewrites unless there is a concrete correctness or maintainability reason.
