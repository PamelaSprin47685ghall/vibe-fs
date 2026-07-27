# Migration ledger (Wanxiangshu.Next)

This file tracks behavior takeover and deletion gates for Wanxiangshu.Next.

## Status

- Product SSOT: `AGENTS.md` / `TASK.md`
- Package version: `0.4.0-rc.1` (still `private: true` until public packaging)
- P0 canary gate: `CANARY_REPEAT=3` staggered 16/16 green on `worktree-0-branch`
- Host signal path: idle/retry/deleted only; abort via reconciled assistant snapshot
- Review: journal dual PERFECT with distinct ProviderRunId + ToolCallId; RootUserMessageId recorded when host exposes it
- Fallback durable writer: `session.status=retry` only; empty-tree ReviewGuard no longer bypasses Manager finish
- Companion: self-rebase = LatestB only; epoch switch via pure `shouldSwitchEpoch` + CoveredPrefixDigest verify
- Tools: Manager `fork` (agent/prompt/signal); Orchestrator `fork-manager` (prompt only)
- Inspector: one-shot for Coder; agent config executor-only
- PTY: Manager fork surface only
- Canaries: JSON scripts + shared `canary-driver`; event-driven launch on `[setupScenario] ready`

## Deleted / relocated

| Item | Disposition |
| --- | --- |
| `Session/ReviewGuard.fs` consecutive counter | Tests-only legacy helper inlined in unit tests |
| `Orchestrator.ReviewStages` name | Renamed `PublishStages` |
| `DurableFallback.recordFailure` | Removed from production; tests drive facts directly |
| `Advisor` role | Removed |
| `session.error` → SessionAbort | Removed; abort classified on idle reconcile |
| `pendingEpochSwitch` / self-rebase cold switch | Removed; LatestB only until budget epoch |
| Shared Manager/Orchestrator fork schema | Split: `fork` vs `fork-manager` |

## Open before public 0.4.0

- Publish packaging (`private: false`, LICENSE, CHANGELOG, npm pack smoke)
- Host-native confirmation-prompt text binding if OpenCode exposes stronger root-user identity than current context keys
- Remaining dual-path cleanups called out in architecture gates
