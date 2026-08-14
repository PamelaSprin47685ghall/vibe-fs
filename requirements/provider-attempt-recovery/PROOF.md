# provider-attempt-recovery — 测试落点

运行命令：单文件 `node --test requirements/provider-attempt-recovery/tests/<file>.test.mjs`；
整包即被 `node tests/unit/run.mjs` 自动发现（requirements 树）。落点类型：MOVE = 从旧
`tests/unit` 物理移入本包；REUSE = 留在原处（多 owner 或共享 checker），记锚点与 cutover 拆分；
NEW = 本包新写。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| PAR-001 Fallback 属 Logical Run；新 Root 新 cursor | `tests/cursor.test.mjs`：`FALLBACK_001_the_authority_root_fact_is_what_creates_the_cursor`、`FALLBACK_001_an_advance_with_no_accepted_root_stops_the_replay`、`FALLBACK_001_a_new_authority_root_replaces_the_cursor_entirely` | MOVE | `node --test requirements/provider-attempt-recovery/tests/cursor.test.mjs` |
| PAR-001（无 Run 不推进） | `tests/fallback-ledger.test.mjs`：`PAR_FALLBACK_001_no_active_run_advances_nothing_and_writes_no_fact` | NEW | `node --test requirements/provider-attempt-recovery/tests/fallback-ledger.test.mjs` |
| PAR-002 modulo-4 封闭 DU / 非法字节 fail-closed | `tests/cursor.test.mjs`：`FALLBACK_002_a_fresh_cursor_starts_at_offset_zero_with_no_budget_spent`、`FALLBACK_002_offset_is_modulo_four_and_never_stops_advancing`、`FALLBACK_002_each_offset_maps_to_a_fixed_side_and_a_fixed_agent`、`FALLBACK_002_an_offset_outside_zero_to_three_is_not_a_cursor_position` | MOVE | 同上 |
| PAR-003 唯一写入口 / 同失败只推进一次 | `tests/cursor.test.mjs`：`FALLBACK_003_the_same_attempt_observed_twice_advances_the_cursor_once`、`FALLBACK_003_the_dedupe_window_is_bounded_so_the_projection_cannot_grow_with_history`、`FALLBACK_003_a_duplicate_line_is_absorbed_because_replay_produces_it` | MOVE | 同上 |
| PAR-003（ledger 级去重） | `tests/fallback-ledger.test.mjs`：`PAR_FALLBACK_003_same_failure_observed_twice_advances_once` | NEW | 同上 |
| PAR-004 推进不变量（失败 +1 / 成功归零不写事实） | `tests/cursor.test.mjs`：`FALLBACK_004_failure_advances_the_offset_and_spends_one_unit_of_budget`、`FALLBACK_004_success_resets_the_budget_but_NOT_the_offset`、`FALLBACK_004_success_leaves_a_parked_odd_offset_in_place`、`FALLBACK_004_recording_success_clears_the_dedupe_window_too`、`FALLBACK_007_success_writes_no_fact_so_no_journal_line_zeroes_the_count`、`ENFORCER_063_success_clears_failures_after_multiple_advances_without_touching_offset` | MOVE | 同上 |
| PAR-005 有限预算 / Exhausted 停止自动请求 | `tests/cursor.test.mjs`：`FALLBACK_005_the_default_automatic_recovery_budget_is_twelve`、`FALLBACK_005_the_verdict_is_taken_after_the_failure_so_the_twelfth_is_final`、`FALLBACK_005_a_configured_budget_is_honoured_and_never_infinite`、`FALLBACK_005_exhaustion_is_stored_rather_than_re_derived_from_the_count`、`FALLBACK_005_may_continue_answers_the_projection_level_question`、`FALLBACK_005_an_advance_after_exhaustion_is_absorbed_not_applied` | MOVE | 同上 |
| PAR-005（host-facing admission） | `tests/fallback-ledger.test.mjs`：`PAR_FALLBACK_005_twelfth_failure_admission_is_recovery_exhausted`、`PAR_FALLBACK_005_admission_continues_while_budget_remains` | NEW | 同上 |
| PAR-005（跨包交叉：预算终点） | `tests/../degeneration-guard/tests/loop-sensor.test.mjs`：`LOOP_008_budget_exhaustion_is_final_and_writes_the_exhausted_fact` | MOVE（跨包引用） | `node --test requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| PAR-006 侧序列无界 | `tests/cursor.test.mjs`：`FALLBACK_006_the_side_sequence_table_is_unbounded_by_construction` | MOVE | 同上 cursor |
| PAR-007 fold 拒绝条件 | `tests/cursor.test.mjs`：`FALLBACK_007_a_valid_advance_moves_the_durable_cursor`、`FALLBACK_007_the_next_offset_must_be_the_modulo_four_successor`、`FALLBACK_007_the_count_must_advance_by_exactly_one_or_restart_at_one_after_success`、`FALLBACK_007_each_rejection_names_a_different_cause`、`FALLBACK_007_a_stale_previous_offset_is_refused_even_when_the_step_is_valid`、`FALLBACK_007_a_corrupt_transition_stops_the_replay_instead_of_being_absorbed`、`FALLBACK_007_an_advance_naming_another_run_is_absorbed_not_applied`、`FALLBACK_007_a_replayed_journal_reaches_the_same_cursor`、`FALLBACK_007_a_replayed_journal_with_intervening_success_streak_restart_reaches_the_same_cursor` | MOVE | 同上 |
| PAR-008 空/XML-only 不计入 | `tests/unit/context/recovery-slot.test.mjs`：`FALLBACK_008_an_invalid_terminal_earns_exactly_one_repair`；`tests/unit/prompt/authority.test.mjs`：`FALLBACK_008_one_terminal_provider_run_earns_exactly_one_repair` | REUSE | `node --test tests/unit/context/recovery-slot.test.mjs`；`node --test tests/unit/prompt/authority.test.mjs`（SPLIT@cutover：authority 属 interaction-authority/dispatch-protocol） |
| PAR-009 Host Attempt ≠ 领域计数 | `tests/cursor.test.mjs`：`FALLBACK_010_the_domain_count_is_reachable_only_through_a_confirmed_failure`、`FALLBACK_010_the_dedupe_identity_names_the_run_the_root_and_the_attempt` | MOVE | 同上 cursor |
| PAR-010 槽内维护子请求 | `tests/unit/context/recovery-slot.test.mjs`：`FALLBACK_011_only_a_business_main_success_clears_the_failure_count`、`CTX_007_a_failed_squash_fails_the_slot_without_sending_the_main_request`、`CTX_007_a_successful_main_commits_and_does_not_move_the_cursor`、`CTX_007_a_failed_main_fails_the_slot_for_every_kind`、`CTX_008_only_a_failed_slot_advances_the_cursor` | REUSE | `node --test tests/unit/context/recovery-slot.test.mjs`（SPLIT@cutover：CTX 锚点归 context-compression，FALLBACK-011/008 归本包） |
| PAR-011 armed 合取 / parked-cursor | `tests/unit/context/recovery-slot.test.mjs`：`FALLBACK_012_a_new_sequence_always_starts_unarmed`、`FALLBACK_012_only_a_failure_advance_arms_the_next_slot`、`FALLBACK_012_arming_is_lost_across_a_restart_and_the_safe_side_is_unarmed`、`FALLBACK_012_the_facade_offers_no_way_to_derive_arming_from_an_offset`、`FALLBACK_012_the_next_slot_is_armed_exactly_when_this_one_failed`、`FALLBACK_012_parked_cursor_does_not_trigger_compression_acceptance_trace`、`FALLBACK_012_at_least_one_real_failure_separates_any_two_squashes` | REUSE | `node --test tests/unit/context/recovery-slot.test.mjs`（SPLIT@cutover 同上） |
| PAR-012 abort 清理残留不计入 | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`：`LOOP_006_interrupted_blog_repairs_without_advancing_primary_cursor`、`ENFORCER_065_tool_execution_error_blog_advances_primary_cursor_once`；e2e：`tests/e2e/cases/fallback-aabb-trace.test.mjs`（waitFact FallbackCursorAdvanced eq=4） | REUSE | `node --test tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`（SPLIT@cutover：ENFORCER 域锚点归 behavior-diagnosis） |
| PAR-013 换 Peer 不换身份字节 | `tests/unit/invariants/prompt-stability.test.mjs`：`PROMPT_STABILITY_fallback_peer_switch_keeps_persona_and_system_prompt_bytes` | REUSE | `node --test tests/unit/invariants/prompt-stability.test.mjs`（SPLIT@cutover：身份字节 guarantee 归 participant-identity/provider-language/prefix-stability） |
| PAR-014 continuation 时序与次数 | `tests/cursor.test.mjs`：`FALLBACK_005_may_continue_answers_the_projection_level_question`；`tests/unit/prompt/authority.test.mjs`：`PROMPT_003_a_continuation_never_replaces_the_authority_root`、`PROMPT_003_every_continuation_kind_is_representable_and_none_is_a_root` | MOVE + REUSE | cursor 同上；authority SPLIT@cutover（wire 属 dispatch-protocol） |
| PAR-015 StrengthReplica 不进 owner controller | `tests/cursor.test.mjs`：`FALLBACK_010_the_dedupe_identity_names_the_run_the_root_and_the_attempt`（identity 含 SessionId → replica session 的 run 在机制上不可能推进/清零 owner cursor） | MOVE（机制锚点） | 同上 cursor。SPLIT@cutover：replica 侧 STRENGTH-004/019 的规范测试归 `speculative-investigation` |

## 包拥有的 semantic anchor id

`scripts/checks/semantic-anchors.mjs` 无本包语义 ID（该 catalog 只装 Role Law / office / tool
cognition anchors）；本包为空清单。

## cutover 待办（SPLIT@cutover）

1. `tests/unit/context/recovery-slot.test.mjs`：FALLBACK-008/011/012 断言迁入本包；
   CTX-006/007/008/010/012 断言归 `context-compression`；文件按 owner 拆分后删除原文件。
2. `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`：PAR-012 两条锚点迁入本包或保留为
   cross-check（ENFORCER 域其余锚点归 `behavior-diagnosis`）。
3. `tests/unit/prompt/authority.test.mjs`：PAR-008/014 的 FALLBACK 锚点迁出，其余归
   `interaction-authority` / `dispatch-protocol`。
4. `tests/unit/invariants/prompt-stability.test.mjs`：PAR-013 锚点保留为 cross-check（身份字节
   owner 是 `participant-identity`/`provider-language`/`prefix-stability`）。
5. `tests/e2e/cases/fallback-aabb-trace.test.mjs`：e2e 由 lead 在 cutover 阶段归位。
