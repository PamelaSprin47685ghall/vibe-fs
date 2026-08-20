# change-integration — HOW

## 架构机制

### 变基发布循环（rebaseReviewPublishLoop）

1. **变基与冲突处理**：从目标 ref 获取最新 head 并尝试在对应 worktree 内变基。发生冲突时产出 `ConflictDetected` 事实并在门禁外交付原 Manager 解决，解决后重新进入循环。
2. **变基后双重验证**：变基成功后在同一 worktree 内触发 post-rebase review，生成不可变的审查证据。
3. **短门禁 CAS 发布**：获取 `IntegrationGate` 短锁，重新读取目标 head。若 head 与预期一致，执行快进推送（ffMerge）并记录 `Published` 事实；若 head 发生移动，释放锁并作废旧审查证据，回到循环起点重新变基。

### 门禁与工作树资源管理

- **IntegrationGate 互斥**：基于文件锁实现的轻量互斥机制，仅覆盖目标分支指针更新的几毫秒窗口，不侵占 LLM 执行与代码分析时间。
- **WorktreeResource 生命周期**：为每个任务分配由 `ManagerJobId` 绑定的独立工作树，完成任务后原子清理，崩溃恢复时按持久化事实精准收拢或复用。projection 不 fold 成唯一「最新 case」，SW-003 vs SW-009 消歧保证恢复重入直接由事实与当前外部 head 判定。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| CHGINT-001 | `requirements/change-integration/tests/job.test.mjs` |
| CHGINT-002 | `requirements/change-integration/tests/git-operations.test.mjs` |
| CHGINT-003 | `requirements/change-integration/tests/job.test.mjs` |
| CHGINT-004 | `requirements/change-integration/tests/integration-gate.test.mjs` |
| CHGINT-005 | `requirements/change-integration/tests/worktree-resource.test.mjs` |
| CHGINT-006 | `requirements/change-integration/tests/job.test.mjs` |
| CHGINT-007 | `requirements/change-integration/tests/job.test.mjs` |
| CHGINT-008 | `requirements/change-integration/tests/git-operations.test.mjs` |
| CHGINT-009 | `requirements/change-integration/tests/job.test.mjs` |
| CHGINT-010 | `requirements/change-integration/tests/job.test.mjs` |
| CHGINT-011 | `requirements/change-integration/tests/host.test.mjs` |
| CHGINT-012 | `requirements/change-integration/tests/job.test.mjs` |
| CHGINT-013 | `requirements/change-integration/tests/orchestrator-conflict-confluence.test.mjs` |
