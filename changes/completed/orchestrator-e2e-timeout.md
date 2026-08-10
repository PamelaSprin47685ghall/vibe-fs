> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

# Orchestrator e2e canary timeouts (pre-existing)

**Status:** Proposed  
**Priority:** P1 / release gate  
**Scope:** `orchestrator-publish` / `orchestrator-unhappy-path` / `orchestrator-restart-publish`  
**发现来源:** `ce-student-teacher-collapse` 封板时 `npm run test:e2e`；在 **clean master**（无该 Change）上单独复现同样失败。

## 现象

三 canary 均 watchdog 超时，典型 blocked expectations：

- `orchestrator-publish`：`orch.2` / `manager.3` / `manager.4`
- `orchestrator-unhappy-path`：`manager.3`
- `orchestrator-restart-publish`：`barrier-reviewer.0`

`student-teacher`、`devops-mechanical-repair-loop`、`manager-*`、reviewer、executor 等其余 canary 在同次 suite 绿。

## 非目标

- 不归因于 Student–Teacher CE collapse（clean master 复现）
- 不通过把 `check:release` 缩水成 targeted canary 来冲绿

## 验收

- 上述三 canary 单独与 `npm run test:e2e` 全绿
- 最好 `npm run check:release`（e2e `--repeat 3`）全绿


# Active work

| Phase | Item | Status |
|---|---|---|
| 0–5 | Causal CE + instrumentation (see causal-ce-observability) | DONE |
| 6 | Root-cause repair of 3 orchestrator canaries | DONE — SharedState.BloggerFlights; evidence `evidence/orchestrator-frontier/ROOT-CAUSE.md` |
| 9 | Full verification | DONE |

All three canaries green after restart (publish / unhappy-path / restart-publish).

---

## Final outcome

Resolved via causal frontier (not timeout inflation).

Root cause: companion blogger flights were per-plugin-instance while blogger sessions live under `RootWorkspace`; BlogTool on the root instance saw `HasFlight=false`, aborted, and Finality hung on `journal-work-log`.

Fix: `SharedState.BloggerFlights` + harness blogger blocking renewals. Evidence: `evidence/orchestrator-frontier/`.

`orchestrator-publish` / `orchestrator-unhappy-path` / `orchestrator-restart-publish` green; full `npm run check` + `npm run test:e2e` green.
