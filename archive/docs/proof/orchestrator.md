# Orchestrator — 证明

行为：`what/orchestrator.md`。边界：`shape/orchestrator.md`。程序：`how/orchestrator.md`。

## Clean / 工具

| 证明 | 期望 | 条款 |
|------|------|------|
| dirty workspace 拒绝用户消息 | 无自动 stash/commit | ORCH-002 |
| provider 面仅 `commission` / `join` / `horizon`；manager 仅 fast/deep-manager | 旧名 `fork-manager` / `list` 非法、无 alias | ORCH-001、AGENT-015 |
| `commission` ≠ Manager `fork`（不同硬语义 → 不同名） | Gate A | ORCH-001、ARCH-007/016 |
| `commission` 成功只见 Byname 承接 charge | 无 job_id / worktree / reused / agent / role / tier / fallback_peer | ORCH-001、EXEC-029/030 |
| Orchestrator provider 永不看见 ManagerJobId / worktree | Gate B leak vocabulary | ORCH-001、ARCH-014/016 |

## Job 生命周期

| 证明 | 条款 |
|------|------|
| 一 Job 一 worktree 一 Manager，恢复不换 Manager | ORCH-003 |
| Integration Gate 仅 CAS 窗口 | ORCH-005 |
| 事实含 ManagerAgent 与 witness ID（墙内；不投影回 horizon） | ORCH-006 |

## 恢复

| 证明 | 期望 | 条款 |
|------|------|------|
| PublishClaimed 三分支顺序固定 | 已 ff / 可 ff / 过期回环 | ORCH-007 |
| GetTargetHead 失败 | fail closed，不 fallback HEAD | ORCH-008 |
| 禁止用磁盘状态代替最后事实 | ORCH-007 |
| 恢复可用墙内 job_id / worktree；不得回灌 Orchestrator provider | ORCH-007、ARCH-014 |

代表：`tests/unit/orchestrator/*`、join 相关 unit、publish canary；旧 substring `fork-manager`/`list()` 合同（如 `orchestrator-reuse-contract`）删除，改语义 invariant + Gate A/B。
