# Orchestrator — 所有权与边界

## ORCH-003：Job · worktree · Manager 一对一

一个 `ManagerJob` 生命周期内：

- 不重新创建 worktree  
- 不更换 Manager  
- 冲突仍返回**同一个** Manager  

Manager Agent 必须以 `fast-manager` 或 `deep-manager` 持久化，恢复时不得降级为裸 `Role.Manager`。

## ORCH-005：Integration Gate 只保护 ref mutation

短 CAS Integration Gate：

- **只**覆盖目标 ref 的 mutation 窗口  
- **不**在 LLM Review 或冲突修复期间持有  
- 多 Job 可并行 rebase / review  
- target 变化时**绝不**复用旧 post-rebase witness  

Gate 不得提前到 `runManagerJob` 入口「先锁后干活」。

## 外部效果所有权

worktree 创建与 publish 遵循 PERSIST-009：Requested/Claimed → 幂等执行 → Accepted/Published。  
PublishClaimed 恢复的唯一解释权在 Orchestrator 恢复路径（ORCH-007），不在通用 effect 总线。
