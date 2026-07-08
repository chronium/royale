---
name: royale-pm-workflow
description: Royale PM board and wiki workflow. Use before implementation, task selection, get_next_task/list_tasks/get_task, dependencies, milestones, priorities, task notes, moving tasks, wiki edits, or .pm storage rules.
---

# Royale PM Workflow

Use this skill whenever work touches the PM board, PM wiki, milestones, dependencies, priorities, task lifecycle, or `.pm/` storage.

## Start work from the board

- All feature work must be driven from the PM board through the PM MCP tool.
- Choose an existing task from the board with `get_next_task`, `list_tasks`, or `get_task` before implementation.
- Prefer `get_next_task` when available instead of guessing from raw task lists.
- For implementation work, call `get_next_task` with `readyOnly: true` so blocked tasks are not selected accidentally.
- For planning, sequencing, or diagnosing why a track has no available work, the default `get_next_task` behavior is useful because it may return the best blocked task with its dependency blockers.
- Inspect the returned task's `dependenciesReady`, `waitingOnDependencies`, priority, milestone, and reason before acting.
- Move the selected task to `doing` with `move_task` before changing code.
- Work only on the selected task unless the user explicitly expands the scope.
- If the task is too vague, ask clarifying questions before implementation.

## Dependency discipline

- If `readyOnly: true` returns no task for a filtered track, do not bypass dependencies.
- Either choose a ready task from another track or plan the dependency that unblocks the requested track.
- Use `search_tasks` to find related task IDs, dependency references, or prior decisions before changing board structure.
- Use `validate_project` after PM structure changes, especially dependency, priority, milestone, task order, wiki rename, or wiki delete operations.

## PM MCP tool selection

Prefer the most specific PM MCP tool that fits the operation.

- Use `search_tasks` to find related task IDs, dependency references, or prior decisions before changing board structure.
- Use `update_task_metadata` for task title, description, track, milestone, priority, or dependency changes instead of rewriting full task markdown.
- Use `update_task_markdown` only when task notes or acceptance details need markdown changes that metadata tools cannot express.
- Use `move_task` for state changes. Never move task refs manually.
- Use `set_milestone_priority` and task `priority` metadata to guide sequencing rather than relying on manual ordering alone.
- Use `reorder_tasks` only when an explicit within-track order matters after priority and dependency data are already correct.
- Use `bulk_assign_tasks_to_milestone` for milestone reshuffles instead of editing tasks one by one.
- Use `get_project`, `list_milestones`, `list_tracks`, and `list_states` when checking configured keys rather than inferring them from file names.

## Wiki workflow

The PM wiki is a project source of truth.

Important wiki pages:

- `project-overview` — product goals, MVP scope, technology stack, and non-goals.
- `architecture` — runtime architecture, authority boundaries, data flow, networking, physics, testing, and deployment shape.

Before architecture or gameplay-affecting changes:

- Read the relevant wiki page.
- Search/list wiki pages before creating new pages to avoid duplicate source-of-truth pages.

For wiki edits:

- Use `list_wiki_pages` or `search_wiki_pages` before creating a page.
- Use `outline_wiki_page` followed by `patch_wiki_page` for targeted edits under an existing heading.
- Use `update_wiki_page_markdown` only for broad rewrites where a targeted patch would be less clear.
- Use `rename_wiki_page` and `remove_wiki_page` for wiki restructuring. Never rename or delete wiki files manually.
- Use `create_wiki_page` for new source-of-truth pages and include enough initial structure that later agents can patch them safely.

## PM storage protection

The `.pm/` directory is PM storage, not a normal hand-edited project area.

- Do not manually edit files under `.pm/`.
- Do not manually move task state refs, edit task markdown files, rename or delete wiki files, or change PM metadata through filesystem edits.
- Use PM MCP tools for task creation, task state changes, task markdown updates, wiki page creation, wiki edits, wiki renames, wiki deletes, and project metadata changes.
- Direct `.pm/` reads are allowed for inspection only when useful.
- Direct `.pm/` writes are forbidden.
- If the PM MCP tool lacks an operation needed to update board or wiki state, stop and tell the user which PM MCP capability is missing.

When these instructions say updating task markdown or editing the wiki is allowed, that means through the PM MCP tools only.

## Completion

Before moving a task to `done`:

- Confirm the selected PM task is the work that was actually completed.
- Confirm implementation, tests, and documentation are complete.
- Update the task markdown with useful notes, decisions, blockers, or acceptance details when they materially affect future work.
- Update the wiki if behavior, architecture, setup, protocols, data formats, workflow, or constraints changed.
- If no wiki update was needed, note that in the task or final summary.
- Run `validate_project` after PM structure changes.
- Move the task to `done` with `move_task` only after the above is satisfied.
- If work is blocked, leave the task in `doing` and document the blocker in task markdown.
