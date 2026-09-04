# change-integration — HOW

## 架构机制

### Relay + deterministic artifact admission 发布循环

1. **等待 Relay outcome**：`OrchestratorProgram` 只消费 `IncumbencyRetired` / `QualityCandidateAccepted` / exceptional terminal。无有效证书的 retirement 只请求普通 successor。
2. **确定性 artifact admission**：有效证书先与当前 `WorkspaceSnapshotId` 对齐，再检查 unmerged entries、candidate 与 target head。任何 binding change 都先 `InvalidateCertificate`。
3. **rebase / conflict 都回普通 successor**：rebase 成功记录 `RebasedCandidateReady` 后请求 `PostRebaseIndependentAssessment`；冲突记录 `ConflictDetected` 后请求 `RebaseConflict` / `ArtifactAdmissionUnmerged` successor。没有 ResumeManager/Reviewer 分支。
4. **短门禁 CAS 发布**：只有已经在当前 target head 上有新证书的 rebased candidate 才进入 `IntegrationGate`。门内重读 target、写 `PublishClaimed` 并 ff-only；CAS miss 释放门禁后 invalidation→rebase→successor。

### 门禁与工作树资源管理

- **IntegrationGate 互斥**：基于文件锁实现的轻量互斥机制，仅覆盖目标分支指针更新窗口，不侵占 Relay assessment、Manager work、rebase 或冲突处理。
- **WorktreeResource 生命周期**：为每个任务分配由 `ManagerJobId` 绑定的独立工作树，完成任务后原子清理，崩溃恢复时按持久化事实精准收拢或复用。projection 不 fold 成唯一「最新 case」，SW-003 vs SW-009 消歧保证恢复重入直接由事实与当前外部 head 判定。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| CHGINT-001 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-001] ORCH_003_a_created_job_persists_the_manager_agent_and_the_worktree_identity` |
| CHGINT-002 | `requirements/change-integration/tests/git-operations.test.mjs::WHAT[CHGINT-002] GIT_is_dirty_true_only_on_nonempty_porcelain` |
| CHGINT-003 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-003] ORCH_007_each_durable_fact_has_one_projection_slot` |
| CHGINT-004 | `requirements/change-integration/tests/integration-gate.test.mjs::WHAT[CHGINT-004] GATE_acquire_and_release_round_trips` |
| CHGINT-005 | `requirements/change-integration/tests/gate-scope.test.mjs::WHAT[CHGINT-005] rebase conflict records machine fact and requests ordinary successor`；`requirements/change-integration/tests/gate-scope.test.mjs::WHAT[CHGINT-005] artifact conflict requests ordinary successor outside the gate` |
| CHGINT-006 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-006] ORCH_007_projection_keeps_independent_facts_instead_of_latest_stage` |
| CHGINT-007 | `requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-007] ORCH_007_the_three_publish_claim_branches_are_evaluated_in_the_clause_order` |
| CHGINT-008 | `requirements/change-integration/tests/git-operations.test.mjs::WHAT[CHGINT-008] GIT_ff_merge_happy_path_advances_to_candidate` |
| CHGINT-009 | `requirements/change-integration/tests/host.test.mjs::WHAT[CHGINT-009] same-road charge advances Relay authority instead of resuming an old Manager`；`requirements/change-integration/tests/job.test.mjs::WHAT[CHGINT-009] ORCH_006_the_worktree_is_located_by_identity_and_the_path_is_only_diagnostic` |
| CHGINT-010 | `requirements/change-integration/tests/gate-scope.test.mjs::WHAT[CHGINT-010] rebase work holds the gate only for the ff mutation`；`requirements/change-integration/tests/gate-scope.test.mjs::WHAT[CHGINT-010] conflict resolution never acquires the publish gate` |
| CHGINT-011 | `requirements/change-integration/tests/host.test.mjs::WHAT[CHGINT-011] HOST_JoinPublishedAvailable_engine_init_failure_is_an_error_result` |
| CHGINT-012 | `requirements/change-integration/tests/runtime.test.mjs::WHAT[CHGINT-012] nonterminal durable evidence preserves the Road worktree across recovery` |
| CHGINT-013 | `requirements/change-integration/tests/gate-scope.test.mjs::WHAT[CHGINT-013] CAS miss invalidates certificate rebases and requests successor after releasing the gate`；`requirements/change-integration/tests/orchestrator-conflict-confluence.test.mjs::WHAT[CHGINT-013] THEOREM_stale_target_invalidates_the_rebased_binding` |
| CHGINT-014 | `requirements/change-integration/tests/gate-scope.test.mjs::WHAT[CHGINT-014] stale certificate never reaches publish gate`；`requirements/change-integration/tests/gate-scope.test.mjs::WHAT[CHGINT-014] Git conflict facts override model-perfect publication` |
