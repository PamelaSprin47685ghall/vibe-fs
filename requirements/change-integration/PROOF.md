# change-integration — 证明落点

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|---------|
| CHGINT-001 publish lifecycle | `tests/job.test.mjs`（`ORCH_003_a_created_job_persists_the_manager_agent_and_the_worktree_identity`、`ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out`） | MOVE | `node --test requirements/change-integration/tests/job.test.mjs` |
| CHGINT-002 Clean Gate | `tests/git-operations.test.mjs`（`GIT_is_dirty_true_only_on_nonempty_porcelain`、`GIT_ff_merge_refuses_dirty_target_worktree`）；`tests/unit/orchestrator/runtime.test.mjs` `ORCH_007_NeedsReview_preserves_the_active_worktree` | MOVE+REUSE | `node --test requirements/change-integration/tests/git-operations.test.mjs tests/unit/orchestrator/runtime.test.mjs` |
| CHGINT-003 原子边界、事实标记 | `tests/job.test.mjs`（`ORCH_006_only_progress_ever_changes_after_creation`、`ORCH_007_progress_that_needs_no_head_derives_its_action_from_the_fact_alone`、`ORCH_007_every_progress_case_yields_exactly_one_action`） | MOVE | `node --test requirements/change-integration/tests/job.test.mjs` |
| CHGINT-004 唯一短 critical section | `tests/integration-gate.test.mjs`（`GATE_lock_path_is_stable_per_repo_and_branch`、`GATE_second_acquire_on_held_lock_eventually_fails`）；`tests/job.test.mjs` `ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out` | MOVE | `node --test requirements/change-integration/tests/integration-gate.test.mjs requirements/change-integration/tests/job.test.mjs` |
| CHGINT-005 conflict 门外 repair 再重新 claim | `tests/job.test.mjs` `ORCH_003_a_conflict_goes_back_to_the_same_manager_with_the_conflicted_files`；`tests/worktree-resource.test.mjs`（`WORKTREE_create_returns_owned_resource_and_marks_path_identity`、`WORKTREE_release_removes_worktree_and_branch_once`、`WORKTREE_release_aggregates_both_failures`） | MOVE | `node --test requirements/change-integration/tests/job.test.mjs requirements/change-integration/tests/worktree-resource.test.mjs` |
| CHGINT-006 恢复 = 最后事实 fold 唯一动作 | `tests/job.test.mjs`（`ORCH_006_a_terminal_job_stays_in_the_map_so_a_replay_is_recognised`、`ORCH_006_a_terminal_job_accepts_no_further_progress`、`ORCH_006_all_three_terminal_cases_end_the_job`、`ORCH_007_every_progress_case_yields_exactly_one_action`）；`tests/unit/orchestrator/runtime.test.mjs` `ORCH_007_NeedsReview_preserves_the_active_worktree` | MOVE+REUSE | `node --test requirements/change-integration/tests/job.test.mjs tests/unit/orchestrator/runtime.test.mjs` |
| CHGINT-007 PublishClaimed 三分支 | `tests/job.test.mjs`（`ORCH_007_the_three_publish_claim_branches_are_evaluated_in_the_clause_order`、`ORCH_008_an_unreadable_target_head_fails_closed_for_every_head_dependent_case`） | MOVE | `node --test requirements/change-integration/tests/job.test.mjs` |
| CHGINT-008 target ref 冻结 + ff-only CAS | `tests/git-operations.test.mjs`（`GIT_freeze_target_branch_reads_symbolic_ref`、`GIT_freeze_target_branch_refuses_detached_head`、`GIT_freeze_target_branch_blank_stdout_is_detached`、`GIT_get_target_head_missing_branch`、`GIT_ff_merge_refuses_when_repo_on_wrong_branch`、`GIT_ff_merge_refuses_when_target_moved_since_head_read`、`GIT_ff_merge_refuses_non_fast_forward_candidate`、`GIT_ff_merge_ref_moved_lock_diagnostic_maps_to_cas_error`） | MOVE | `node --test requirements/change-integration/tests/git-operations.test.mjs` |
| CHGINT-009 continuation 的 integration identity | `tests/job.test.mjs`（`ORCH_003_only_progress_ever_changes_after_creation`、`ORCH_006_the_worktree_is_located_by_identity_and_the_path_is_only_diagnostic`、`ORCH_003_a_manager_session_resolves_to_its_one_job`）；`tests/unit/orchestrator/host.test.mjs` `HOST_ContinueManagerJob_resumes_a_forked_job_in_its_worktree` | MOVE+REUSE | `node --test requirements/change-integration/tests/job.test.mjs tests/unit/orchestrator/host.test.mjs` |
| CHGINT-010 长 review 不占门 | `tests/job.test.mjs`（`ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out`、`ORCH_005_a_rebased_candidate_publishes_only_while_the_target_has_not_moved`） | MOVE | `node --test requirements/change-integration/tests/job.test.mjs` |
| CHGINT-011 墙内机械不进 horizon | `tests/unit/orchestrator/host.test.mjs`（provider 面仅自然语言后果，如 `HOST_JoinPublished_renders_a_string`）；`tests/job.test.mjs`（墙内事实结构） | REUSE+MOVE | `node --test tests/unit/orchestrator/host.test.mjs requirements/change-integration/tests/job.test.mjs` |
| CHGINT-012 恢复禁扫盘/禁跳步 | `tests/job.test.mjs` `ORCH_007_progress_that_needs_no_head_derives_its_action_from_the_fact_alone`；`tests/unit/orchestrator/runtime.test.mjs` `ORCH_007_NeedsReview_preserves_the_active_worktree` | MOVE+REUSE | `node --test requirements/change-integration/tests/job.test.mjs tests/unit/orchestrator/runtime.test.mjs` |
| CHGINT-013 target 变化 → 旧 witness 作废 | `tests/job.test.mjs` `REVIEW_008_a_moved_target_discards_the_post_rebase_witness`；`tests/git-operations.test.mjs` `GIT_ff_merge_refuses_when_target_moved_since_head_read` | MOVE | `node --test requirements/change-integration/tests/job.test.mjs requirements/change-integration/tests/git-operations.test.mjs` |

## 移动文件清单

| 源 | 目标 | 结果 |
|----|------|------|
| `tests/unit/git/integration-gate.test.mjs` | `requirements/change-integration/tests/integration-gate.test.mjs` | `node --test` 绿 |
| `tests/unit/git/git-operations.test.mjs` | `requirements/change-integration/tests/git-operations.test.mjs` | `node --test` 绿 |
| `tests/unit/git/worktree-resource.test.mjs` | `requirements/change-integration/tests/worktree-resource.test.mjs` | `node --test` 绿 |
| `tests/unit/orchestrator/job.test.mjs` | `requirements/change-integration/tests/job.test.mjs` | `node --test` 绿 |

（4 文件合计 74 断言全绿；import 深度已适配为 `../../../tests/unit/support` + `../../../dist`。）

## SPLIT@cutover 清单（REUSE 文件拆分计划）

- `tests/unit/orchestrator/host.test.mjs`：job/worktree/发布断言归本包（CHGINT-006/009/011）；commission
  委托面 → `delegation`；reverify/review barrier → `review-assurance`；engine/HostForkRuntime →
  `managed-session-lifecycle`。
- `tests/unit/orchestrator/runtime.test.mjs`：`NeedsReview` 保留 worktree 断言归本包；PERSIST-009
  事实顺序断言 → `effect-accounting`；恢复 → `crash-reconciliation`。
- `tests/unit/git/hook-dispatcher.test.mjs`：整文件 → `durable-events`（store ref 的 pre-push /
  reference-transaction hooks，非本包发布面）。
- `tests/unit/execution/` join-recovery 系列：Orchestrator 恢复交叉断言 → `crash-reconciliation`
  （本包引用其 fold 结果，不复制命题）。

## 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.orchestrator`：`shared-gate`、`host-vs-orchestrator`
（`owns-roads`/`same-road-continuation`/`independent-destination` 归 `delegation`）。
