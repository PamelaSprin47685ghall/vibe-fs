# PROOF — managed-session-lifecycle（测试落点表）

每条 WHAT 命题恰好一行。类型：`MOVE`（本包 tests/ 物理拥有）/ `REUSE`（留在原处，记精确锚点 +
cutover 计划）/ `NEW`（本包新写）。运行命令均为 `node --test <file>`。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| MANAGED-SESSION-001 | `tests/attached-session-runtime.test.mjs` `EXEC_026_get_or_create_creates_and_binds_a_work_child_once`（runtime 是绑定唯一 owner）+ `EXEC_026_remove_and_remove_by_delegate_session_are_the_only_unbind_paths` | NEW | `node --test requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs` |
| MANAGED-SESSION-002 | REUSE `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` `HOST_014_concurrent_first_ensure_is_single_flight_and_creates_one_child`（link 先于 prompt 的 create 路径） | REUSE | `node --test requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` |
| MANAGED-SESSION-003 | REUSE `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` `HOST_015_companion_satellite_recovery_reuses_journal_linked_child_under_flat_root` / `HOST_015_companion_satellite_recovery_creates_an_explicit_replacement_when_the_old_child_is_gone` / `HOST_015_companion_satellite_recovery_fails_closed_when_journal_linked_child_conflicts` / `HOST_015_companion_satellite_recovery_never_adopts_same_agent_sibling_without_journal_link`；REUSE `requirements/session-ontology/tests/session-flattening.test.mjs`（HOST-015 恢复匹配） | REUSE | `node --test requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` / `requirements/session-ontology/tests/session-flattening.test.mjs` |
| MANAGED-SESSION-004 | `tests/host-fork-agent.test.mjs` `HFA_reuse_after_join_sends_prompt_on_same_child`（completion 后复用不 spawn）；REUSE `requirements/delegation/tests/sync-delegate-runtime.test.mjs` `EXEC_026_sync_delegate_reuses_session_after_full_completion` | MOVE + REUSE | `node --test requirements/managed-session-lifecycle/tests/host-fork-agent.test.mjs` / `requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| MANAGED-SESSION-005 | `tests/attached-session-runtime.test.mjs` `EXEC_026_reuse_scope_is_the_serialization_key_across_sessions` + `EXEC_026_get_or_create_reuses_the_existing_binding_and_keeps_the_bound_agent`；`tests/host-fork-agent.test.mjs` `HFA_existing_fork_keeps_deep_agent_when_caller_passes_fast` / `HFA_reuse_keeps_deep_agent` | NEW + MOVE | `node --test requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs` / `.../host-fork-agent.test.mjs` |
| MANAGED-SESSION-006 | REUSE `requirements/managed-session-lifecycle/tests/handle.test.mjs` `EXEC_009_a_retired_handle_answers_retired_forever` / `EXEC_009_a_retired_id_is_distinguishable_from_one_that_never_existed` / `EXEC_009_a_retired_child_session_is_still_recognised_as_a_child` | REUSE | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-007 | REUSE `requirements/managed-session-lifecycle/tests/handle.test.mjs` `EXEC_004_the_first_completion_wins_and_later_ones_are_refused` / `EXEC_004_each_completion_kind_survives_into_the_state`；`tests/host-fork-agent.test.mjs` `HFA_fork_abandoned_handle_is_refused_before_spawn` | REUSE + MOVE | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` / `node --test requirements/managed-session-lifecycle/tests/host-fork-agent.test.mjs` |
| MANAGED-SESSION-008 | REUSE `requirements/managed-session-lifecycle/tests/handle.test.mjs` `EXEC_004_join_may_only_retire_a_handle_that_actually_completed` / `EXEC_009_a_replayed_completion_or_retirement_is_absorbed` | REUSE | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` |
| MANAGED-SESSION-009 | REUSE `requirements/managed-session-lifecycle/tests/handle.test.mjs` `EXEC_009_parent_abort_needs_the_handles_themselves_not_a_count`；REUSE `requirements/managed-session-lifecycle/tests/handle-abandoned.test.mjs`；`tests/host-fork-agent.test.mjs` `HFA_fork_abandoned_handle_is_refused_before_spawn`（abandon 不可复活） | REUSE + MOVE | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` / `.../handle-abandoned.test.mjs` |
| MANAGED-SESSION-010 | `tests/distiller-ownership.test.mjs` `EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible`（HostOwnedHidden 不进 listable） | MOVE | `node --test requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` |
| MANAGED-SESSION-011 | REUSE `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` `HOST_015_companion_satellite_recovery_creates_an_explicit_replacement_when_the_old_child_is_gone` / `HOST_015_companion_satellite_recovery_fails_closed_when_journal_linked_child_conflicts` / `HOST_014_children_query_failure_does_not_guess_or_create` | REUSE | `node --test requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` |
| MANAGED-SESSION-012 | `tests/child-run-projection.test.mjs`（VERIFY_009 全组：`child_run_starts_active` / `child_run_completion_cell_is_single_assignment` / `projection_status_*` / `projection_to_record_*`） | MOVE | `node --test requirements/managed-session-lifecycle/tests/child-run-projection.test.mjs` |
| MANAGED-SESSION-013 | REUSE `requirements/crash-reconciliation/tests/host-fork-restart.test.mjs` `HFR_restart_abandoned_handle_recovered_abandoned` / `HFR_restart_retired_handle_recovered_retired` / `HFR_restart_completed_terminal_re_enlists_child_into_runtime` / `HFR_restart_host_owned_hidden_handle_is_filtered_out` / `HFR_restart_active_handle_recovers_active` / `HFR_restart_multiple_children_recovered_in_link_order` / `HFR_restart_recovery_commit_failure_blocks` | REUSE | `node --test requirements/crash-reconciliation/tests/host-fork-restart.test.mjs` |
| MANAGED-SESSION-014 | REUSE `requirements/delegation/tests/sync-delegate-runtime.test.mjs` `G6_deleted_inspector_child_retires_live_binding_but_survives_for_owner_scope_close` | REUSE | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| MANAGED-SESSION-015 | REUSE `requirements/managed-session-lifecycle/tests/handle.test.mjs` `EXEC_009_a_linked_handle_records_the_child_session_it_drives` / `EXEC_009_only_an_agent_handle_answers_the_agent_question`；REUSE `requirements/managed-session-lifecycle/tests/terminal-policy.test.mjs` `TPOL_tryLinkedChild_finds_child_handle_and_keeps_target_agent` | REUSE | `node --test requirements/managed-session-lifecycle/tests/handle.test.mjs` / `requirements/managed-session-lifecycle/tests/terminal-policy.test.mjs` |

## 反向覆盖（OWNED / NEEDS-SPLIT clause → 本包命题）

- `HOST-009`（OWNED）→ MANAGED-SESSION-001/002/003/011。
- `HOST-015`（restore matching 部分）→ MANAGED-SESSION-003/013。
- `EXEC-006`（OWNED）→ MANAGED-SESSION-012。
- `EXEC-009`（OWNED）→ MANAGED-SESSION-006/007/008/009/015。
- `EXEC-014`（hidden handle 部分）→ MANAGED-SESSION-010。
- `EXEC-017`（cascade cancel 部分）→ MANAGED-SESSION-009。
- `EXEC-026`（runtime ownership 部分）→ MANAGED-SESSION-001/005/014。
- `EXEC-028`（lifecycle 部分）→ MANAGED-SESSION-004/005。
- `REVIEW-010/019`（fail-closed 消费）→ MANAGED-SESSION-003/011（交叉引用，不复制）。
- `REVIEW-015`（dedicated create/retire ≠ Dispose）→ MANAGED-SESSION-014。

## 包拥有的 gate / anchor

- `scripts/checks/session-ownership-ratchet.mjs` 问卷的
  `reusable / cancel / retire / handle / crashReconcile` 字段 → 本包（与 session-ontology 共享
  同一 gate；一个 assertion 一个 owner，字段级划界）。verify 测试已随 session-ontology MOVE
  （`requirements/session-ontology/tests/session-ownership-ratchet.test.mjs`），本包 REUSE。
- semantic-anchors.mjs：本包**零 anchor**。

## SPLIT@cutover 清单

1. `requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs`（import fable → 禁止移动）：恢复/reuse/
   replacement 断言归本包；`HOST_014_SatelliteKind_is_Companion_only` 归 session-ontology。
   cutover 时拆文件并移除 fable import。
2. `requirements/crash-reconciliation/tests/host-fork-restart.test.mjs`：handle 投影恢复断言归本包；
   `HFR_restart_legacy_false_abort_*` 归 effect-accounting（EXEC-021/022）。cutover 时拆分。
3. `requirements/managed-session-lifecycle/tests/host-fork-runtime.test.mjs`：InstallRun/FailRun/CancelAgent/ForkRuntime
   面归本包；`JoinWithPermit/AwaitAgentWithPermit/validatePermit` 归 crash-reconciliation
   （EXEC-023）与 causal-wait；join/await 调用代数归 delegation。cutover 时拆分。
4. `requirements/delegation/tests/sync-delegate-runtime.test.mjs`：reuse/retire/scope-close 断言归本包；
   batch/canonical/WorkRecord 断言归 delegation / work-record。cutover 时拆分。
5. `requirements/managed-session-lifecycle/tests/terminal-policy.test.mjs`：`tryLinkedChild` / `mainSealedForBlogger` /
   `outstandingBackground`（durable 部分）归本包；`isTopLevelManager` 归 interaction-authority
   （AuthorityRoot 事实）；`roleName` 归 session-ontology/host-boundary。cutover 时拆分。
6. `requirements/session-ontology/tests/session-flattening.test.mjs`（import fable）：`abort_children_cascade`
   归本包；物理扁平断言归 session-ontology。cutover 时移除 fable import 后 MOVE。
7. `requirements/managed-session-lifecycle/tests/handle.test.mjs` 等 execution/ 家族文件：本包 REUSE 其 handle 断言；
   join/fork/PTY 断言归 delegation/process-execution；cutover 时按断言拆分（该目录 owner 多元，
   本包不 MOVE）。
