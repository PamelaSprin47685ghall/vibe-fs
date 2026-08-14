# Orchestrator — 所有权与边界

行为见 `what/orchestrator.md`。工具矩阵见 AGENT-006/009/015。

## Provider 面所有权（ORCH-001）

| 面 | Owner | 边界 |
|----|-------|------|
| `commission` / `join` / `horizon` | Orchestrator tool surface | 唯一 provider 动词集；旧名 `fork-manager` / `list` 非法、无 alias |
| `commission` vs Manager `fork` | 不同 contract → 不同名（ARCH-006/007） | 独立集成之路 ≠ 使命内证人 |
| Byname 承接 charge | provider-visible 成功后果 | **禁止**暴露 `job_id` / worktree / `reused` / agent / role / tier / fallback_peer |
| 墙内 Job / worktree / session | Journal + Orchestrator 恢复路径 | 可读写；**不得**投影回 Orchestrator provider horizon |

Orchestrator 不以自身权限执行 Manager Job 的仓库、冲突解决或 Git 工作；这些进入后续编排流程。

## ORCH-003：Job · worktree · Manager 一对一

一个 `ManagerJob` 生命周期内：

- 不重新创建 worktree  
- 不更换 Manager  
- 冲突仍返回**同一个** Manager  

Manager Agent 必须以 `fast-manager` 或 `deep-manager` 持久化，恢复时不得降级为裸 `Role.Manager`。  
Persona（Integrator/Director）属 session 创建绑定（AGENT-028）；本域不重绑 Persona。

## ORCH-005：Integration Gate 只保护 ref mutation

短 CAS Integration Gate：

- **只**覆盖目标 ref 的 mutation 窗口  
- **不**在 LLM Review 或冲突修复期间持有  
- 多 Job 可并行 rebase / review  
- target 变化时**绝不**复用旧 post-rebase witness  

Gate 不得提前到 `runManagerJob` 入口「先锁后干活」。  
Gate / Clean Gate / target head 属墙内机械；不得以 `status`/`error` DTO 或 UUID 塞进 provider horizon（ARCH-014）。

## 外部效果所有权

worktree 创建与 publish 遵循 PERSIST-009：Requested/Claimed → 幂等执行 → Accepted/Published。  
PublishClaimed 恢复的唯一解释权在 Orchestrator 恢复路径（ORCH-007），不在通用 effect 总线。  
目标 branch 在 `commission` 时冻结（ORCH-008）；读 head 失败 → fail closed，不得静默落到 `HEAD`。
