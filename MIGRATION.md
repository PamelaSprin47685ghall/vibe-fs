# Agent DSL Migration Ledger

`AGENTS.md` is the sole constitution. Conflict precedence:

1. The latest explicit correction in `AGENTS.md`.
2. The Agent DSL design derived from that correction.
3. Earlier `SSOT.md` discussion.
4. `next/Doc/kiss-docs/`.
5. Current `next/` implementation.
6. Legacy `src/`, tests, and README.

## Freeze

The legacy `src/`, `tests/`, old integration, and Mux/OMP/Mimocode paths are frozen. Only host fixes that prevent migration scenarios from running and fixes preventing data loss are allowed. New Agent DSL behavior belongs in `next/`; the OpenCode TestKit remains host-only and imports neither production tree.

## Durable facts correction

Event sourcing, CQRS, and per-runtime NDJSON remain required for durable domain facts. They must not persist call stacks, workflow stages, leases, owners, queues, or resource handles. Runtime recovery folds stable NDJSON prefixes into in-memory projections; live Tasks, Channels, listeners, PTYs, and semaphores remain process-local.

## Behavior ledger

| Behavior ID | SSOT clause | Legacy source | Disposition | New proof | Owner | Deletion gate |
| --- | --- | --- | --- | --- | --- | --- |
| AG-JOIN-FAST-COMPLETION | completion enters mailbox before join | Subagent/subsession tests | Port Behavior | `tests-next/Session/ForkRuntimeTests.fs` | Agent/ForkRuntime | new child-session E2E |
| AG-LISTENER-BEFORE-SEND | listener precedes prompt | subagent spawn tests | Port Behavior | OpenCode Host Spike | Host/OpenCode | spike passes |
| AG-CURRENT-TAIL-PRESERVED | B plus raw tail | context/projection tests | Port Behavior | `tests-next/Tools/MessageTransformTests.fs` | Host/Projection | real projection E2E |
| BLOG-BUSY-SKIPS | Blogger busy never blocks X | companion scenarios | Port Behavior | `tests-next/Session/CompanionTests.fs` | Agent/Companion | real OpenCode Blogger E2E |
| PROC-EXACT-THREE-X-DEADLINE | one process deadline | executor tests | Port Behavior | `tests-next/Process/ProcessBudgetTests.fs` | Process | process stress gate |
| FB-FOURTH-SESSION-DEAD | cumulative A/B failures | fallback tests | Port Behavior | `tests-next/Session/SessionFallbackTests.fs` | Agent/Fallback | durable fallback fold |
| REVIEW-DOUBLE-PERFECT | two same-tree verdicts | review tests | Port Behavior | `tests-next/Session/ReviewGuardTests.fs` | Review | durable verdict fold |
| ORCH-PUBLISH-SERIAL | serialized rebase/review/ff | worktree tests | Port Behavior | `tests-next/Integration/OrchestratorTests.fs` | Orchestrator | real Git E2E |
| TESTKIT-STRICT-FIFO | strict mock request order | `testkit/opencode` | Keep TestKit | `testkit/opencode` gates | TestKit | extracted harness gates |

Each legacy test has exactly one disposition: Keep TestKit, Port Behavior, or Obsolete. A test without a Behavior ID does not block legacy deletion.
