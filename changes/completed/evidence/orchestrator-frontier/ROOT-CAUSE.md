# Orchestrator canary root cause (Phase 6) — after-restart proof

Date: 2026-08-10

## Canaries

| Canary | Evidence | Result |
|---|---|---|
| orchestrator-publish | `orchestrator-publish-v9.log` | PASS |
| orchestrator-unhappy-path | `orchestrator-unhappy-path-v9.log` | PASS |
| orchestrator-restart-publish | `orchestrator-restart-publish-v9.log` | PASS |

## Frontier (before fix)

```text
FRONTIER: waiting for external producer journal-work-log
FinalityController wait=finality-blessing-records
unmatched: orch.2, manager.3, manager.4
```

## Causal chain

1. Manager suicide → Finality → reviewer terminals resolve (ConfirmedReviewWitness).
2. Finality awaits blessing records = materialize of reviewer **work log**.
3. Reviewer companion blogger: `BloggerRequestMaterialized` but **no** `BlogEntryCommitted`.
4. OpenCode DB: blog tool `status=error` / `Tool execution aborted` / `interrupted:true`.
5. Host log: `cancel session.id=<blogger>` immediately after stream start.

## Root cause

Blogger sessions are intentionally created under `SharedState.RootWorkspace` (survive worktree release).

Worktree plugin instance materializes the companion request and calls `SetCurrentRequest` on **its** `PluginRuntimeScope.bloggerFlights`.

BlogTool executes on the **root** plugin instance (blogger directory = root) and checks `HasFlight` on **root** scope → miss → AbortSession → no commit → Finality hangs on journal-work-log.

Same HOST-012 class of bug as `SessionParents` / `VerdictSessions`.

## Fix

- Move physical blogger flight registry to `SharedState.BloggerFlights` (process-local, shared across plugin instances).
- Do not clear shared flights on one instance `Dispose` (test isolation via `clearBloggerFlightsForTests`).
- Harness: blogger expectation consumption renews watchdog (`blocking` when `turnId === 'blogger'`).

## After

Three orchestrator canaries green without timeout bumps or expectation weakening.
