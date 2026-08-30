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
| CHGINT-001 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-001] ORCH_003_a_created_job_persists_the_manager_agent_and_the_worktree_identity` |
| CHGINT-002 | `requirements/change-integration/tests/git-operations.test.mjs::WHAT[CHGINT-002] GIT_is_dirty_true_only_on_nonempty_porcelain` |
| CHGINT-003 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-003] ORCH_007_each_durable_fact_has_one_projection_slot` |
| CHGINT-004 | `requirements/change-integration/tests/integration-gate.test.mjs::WHAT[CHGINT-004] GATE_acquire_and_release_round_trips` |
| CHGINT-005 | `requirements/change-integration/tests/worktree-resource.test.mjs::WHAT[CHGINT-005] WORKTREE_create_returns_owned_resource_and_marks_path_identity` |
| CHGINT-006 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-006] ORCH_007_projection_keeps_independent_facts_instead_of_latest_stage` |
| CHGINT-007 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-007] ORCH_007_the_three_publish_claim_branches_are_evaluated_in_the_clause_order` |
| CHGINT-008 | `requirements/change-integration/tests/git-operations.test.mjs::WHAT[CHGINT-008] GIT_ff_merge_happy_path_advances_to_candidate` |
| CHGINT-009 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-009] ORCH_006_the_worktree_is_located_by_identity_and_the_path_is_only_diagnostic` |
| CHGINT-010 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-010] ORCH_005_a_rebased_candidate_publishes_only_while_the_target_has_not_moved` |
| CHGINT-011 | `requirements/change-integration/tests/host.test.mjs::WHAT[CHGINT-011] HOST_JoinPublishedAvailable_engine_init_failure_is_an_error_result` |
| CHGINT-012 | `requirements/change-integration/tests/runtime.test.mjs::WHAT[CHGINT-012] ORCH_007_NeedsReview_preserves_the_active_worktree` |
| CHGINT-013 | `requirements/change-integration/tests/orchestrator-conflict-confluence.test.mjs::WHAT[CHGINT-013] THEOREM_stale_target_on_rebased_candidate_discards_witness` |
