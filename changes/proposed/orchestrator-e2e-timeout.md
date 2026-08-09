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
