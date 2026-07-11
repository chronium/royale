---
name: royale-pm-workflow
description: Manage Royale PM tasks and wiki through PM MCP. Use for task selection, planning, dependencies, priorities, milestones, state transitions, task notes, project validation, wiki search/edit/rename/delete, or any requested change under .pm.
---

# Royale PM Workflow

## Select Work

- Use `get_next_task(readyOnly: true)` for implementation candidates; inspect its reason, priority, milestone, and dependency status.
- Use unfiltered `get_next_task`, `list_tasks`, `get_task`, or `search_tasks` when planning, comparing candidates, or diagnosing blockers.
- Do not bypass dependencies. Select another ready task or plan the dependency that unblocks the requested work.
- Planning and review do not move task state. Implementation moves the selected or newly created task to `doing` before file edits.

If requested work has no task, search first, then create a narrowly scoped task in the appropriate track and milestone. Ask only when track, milestone, scope, or acceptance behavior is a genuine owner decision.

## Mutate Through PM MCP

Use the narrowest available tool:

- `move_task` for state.
- `update_task_metadata` for title, description, track, milestone, priority, or dependencies.
- `append_task_note` for dated decisions, validation, blockers, and completion evidence.
- `update_task_markdown` only when metadata and notes cannot represent the required task content.
- Priority and dependency metadata should drive sequencing before manual task ordering.
- Use bulk operations for milestone assignment or task creation when applicable.

Never edit `.pm/` files directly. Direct reads are allowed for diagnosis only. If MCP lacks the required mutation, stop and identify the missing tool instead of modifying storage manually.

## Maintain The Wiki

The wiki is authoritative for established behavior and constraints.

- Search/list before creating pages.
- Read the relevant page before changing architecture, gameplay, protocol, content formats, build/setup, deployment, diagnostics, or workflow.
- Prefer `outline_wiki_page` plus `patch_wiki_page` for targeted changes.
- Use full-page replacement only for coherent broad rewrites.
- Use MCP rename/delete operations; never manipulate wiki files directly.

Update documentation in the same task that changes the contract. If no wiki update is needed, record why in the task note or final report.

## Complete Work

Before `done`:

1. Confirm the implementation matches the selected task.
2. Record decisions, validation commands/results, limitations, and human-validation needs.
3. Update the wiki where the source of truth changed.
4. Run `validate_project` after PM structure or wiki changes.
5. Move the task to `done` only when no required work remains.

Blocked work stays in `doing` with a concrete blocker note.
