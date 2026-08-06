# Orchestrator — 证明

行为：`what/orchestrator.md`。边界：`shape/orchestrator.md`。程序：`how/orchestrator.md`。

## Clean / 工具

| 证明 | 条款 |
|------|------|
| dirty workspace 拒绝用户消息 | ORCH-002 |
| 仅 fork-manager + join；manager 仅 fast/deep-manager | ORCH-001 |

## Job 生命周期

| 证明 | 条款 |
|------|------|
| 一 Job 一 worktree 一 Manager，恢复不换 Manager | ORCH-003 |
| Integration Gate 仅 CAS 窗口 | ORCH-005 |
| 事实含 ManagerAgent 与 witness ID | ORCH-006 |

## 恢复

| 证明 | 期望 | 条款 |
|------|------|------|
| PublishClaimed 三分支顺序固定 | 已 ff / 可 ff / 过期回环 | ORCH-007 |
| GetTargetHead 失败 | fail closed，不 fallback HEAD | ORCH-008 |
| 禁止用磁盘状态代替最后事实 | ORCH-007 |

代表：`tests/unit/orchestrator/*`、join 相关 unit、publish canary。
