# Migration ledger (Wanxiangshu.Next)

This file tracks behavior takeover and deletion gates. It is not a release note
and does not claim 0.4.0 readiness.

## Status

- Product SSOT: `AGENTS.md` / `next/Doc/SSOT.md`
- Current package version remains `0.3.0` / private until PR 11–12 complete
- Host signal path: idle/retry/deleted only; abort via reconciled assistant snapshot
- Review confirmation: journal / ReviewerHost (not in-memory consecutive counters)
- Fallback durable writer: `session.status=retry` → RetrySignalHandler only
- Inspector: one-shot tool for Coder; agent config executor-only
- PTY: Manager fork surface only

## Deleted / relocated

| Item | Disposition |
| --- | --- |
| `Session/ReviewGuard.fs` consecutive counter | Tests-only legacy helper inlined in unit tests |
| `Orchestrator.ReviewStages` name | Renamed `PublishStages` |
| `DurableFallback.recordFailure` | Removed from production; tests drive facts directly |
| `Advisor` role | Removed |
| `session.error` → SessionAbort | Removed; abort classified on idle reconcile |

## Open before 0.4.0-rc.1

- True ≤100ms full-parallel canary spawn on SEA hosts (or proven equivalent)
- Publish packaging (`private: false`, version bump, LICENSE, CHANGELOG)
- Clean tarball install smoke
- Remaining dual-path cleanups called out in architecture gates
