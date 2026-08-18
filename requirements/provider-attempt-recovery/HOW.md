# provider-attempt-recovery — 实现模型与约束

非 normative：本文描述当前实现怎么满足 WHAT，不另造 owner。读者可按本节定位代码，再回 WHAT.md
对照命题。

## 模块地图

| 模块 | 角色 | 对应命题 |
|---|---|---|
| `src/Wanxiangshu/Domain/AgentPairCursor.fs` | 纯 A/A/B/B cursor 算术（Offset/预算/verdict/attemptIdentity/effectiveAgent/isRecoverySlot） | PAR-001/002/004/005/006/009 |
| `src/Wanxiangshu/Domain/RecoverySlot.fs` | 纯槽决策（arming、squash/main outcome → SlotDecision、advancesCursor/nextArming） | PAR-008/010/011 |
| `src/Wanxiangshu/Application/Recovery/FallbackLedger.fs` | **唯一写入口**：confirmed failure → dedupe → advance/exhaust → append 事实；`admitConfirmedFailure` 投影 host-facing admission | PAR-003/005/007 |
| `src/Wanxiangshu/Application/Recovery/FallbackEvidence.fs` | 只读查询（currentCursor/currentSide/effectiveAgent/mayContinue） | PAR-004/013 |
| `src/Wanxiangshu/Application/Recovery/ProviderRecoveryWorkflow.fs` | 失败后的恢复编排：记录失败 → 等 coverage material → 决定 continuation；`continueAfterLoopKill` 桥接 degeneration-guard | PAR-003/010/014 |
| `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Projection.fs` / `FallbackFactFold.fs` | 持久事实的 fold 与拒绝条件 | PAR-002/007 |
| `src/Wanxiangshu/Session/EnforcerRepair.fs` | `interrupted=true` 残留的判定 | PAR-012 |

## 一次已确认失败的主路径（代码时序）

```text
Host 粗粒度信号（idle / retry）            // 只唤醒，不裁决（HOST-004 归 host-boundary）
→ Reconciler 从完整 Host snapshot 识别失败的 provider attempt
→ ProviderRecoveryWorkflow.continueAfterConfirmedFailure(turn, error, continuationPrompt)
→ FallbackLedger.recordConfirmedFailure(journal, DefaultAutoRecoveryBudget, session, providerRun, reason)
     → FallbackEvidence.tryCurrentState：无 cursor → NoActiveRun（无事实）
     → applyAdvance 拒绝 AlreadyObserved/AlreadyExhausted/DifferentRun/NoCursor → AlreadyRecorded/NoActiveRun
     → applyAdvance 拒绝 InvalidTransition/InvalidFallbackOffset → Error（fail closed）
     → Ok → append FallbackCursorAdvanced（唯一写入口）
     → recoveryVerdict budget：
         MayContinue → RecoveryAdvanced → awaitRecoveryMaterial → 发 ProviderRetryAttempt continuation
         Exhausted   → append FallbackExhausted → RecoveryExhausted（无自动下一步）
```

关键约束：cursor 推进发生在 **reconcile 出的已确认失败**，不在 Host retry 事件处理器里
（retry 只负责唤醒）；`awaitRecoveryMaterial` 等 coverage 是为 CTX-006 armed 槽争取 material，
超时仍发普通主请求（CTX-011 no-candidate 路径，fail open）。

## 持久事实形状

```fsharp
FallbackCursorAdvanced = { SessionId; LogicalRunId; AuthorityRootUserMessageId
                           ProviderRun; PreviousOffset; NextOffset; ConsecutiveFailureCount; Reason }
FallbackExhausted      = { SessionId; LogicalRunId; AuthorityRootUserMessageId
                           FinalConsecutiveFailureCount; FinalOffset }
FallbackSucceeded     = { SessionId; LogicalRunId; AuthorityRootUserMessageId; ProviderRun }
```

成功写入 `FallbackSucceeded = { SessionId; LogicalRunId; AuthorityRootUserMessageId; ProviderRun }` 事实并由 fold 落到同一 projection（PAR-004）：归零、Offset 不变、dedupe 清空；重复/旧 run 幂等，restart 后 replay 等价。

## 历史与弃权

以下事实来自历史 why/fallback 与归档 changes 考古，均为决策记录，不是现行命题：

- **Offset 表示**：拒 byte/int 裸计数（0–255 皆可构造，side 对非法字节无分支）；拒 decode 抛
  `invalidOp`（持久化损坏是可预见失败）；选 `Result<FallbackOffset, FallbackOffsetDecodeError>`。
- **armed 标志**：拒把 armed 写盘或仅凭持久化奇数 Offset 判定（上次主请求成功时 Offset 可停奇数）；
  选内存局部 `armedByFailure`，崩溃后归零（安全侧）。
- **成功写归零事实**：选 owner-owned `FallbackSucceeded` durable fact（单一 ledger 写入口、fold 归零、Offset 不变、dedupe 清空、幂等）；拒分散 Host snapshot 派生 overlay。
- **侧循环判死 vs 预算判死**：拒侧上限（换侧是合法恢复策略）；判死收敛到有界预算。
- **Host Attempt vs 领域计数**：拒混用（量纲不同，重启会错误清零/耗尽）。
- **预算固定 vs 动态**：拒按模型/上下文调阈值（不可测，特例森林）；固定有限正整数。
- **切边**：拒随 fallback 重写 Persona/prompt/language（伪造新身份、打碎 KV-cache 前缀）；
  只换 EffectiveAgent；cursor/Side/Offset/count 不投影给 provider。
- **FALLBACK-011 槽算法与 FALLBACK-012 armed 合取**：维护子请求失败即槽失败；armed 只由真实失败
  推进产生。算法细节在历史 how/fallback 条款已并入上文模块地图，不再另列。

## GARBAGE / 弃权裁决

- **当前 AABB 名字 / Offset 表示 / 具体预算数值**（`fast-coder`/`deep-coder`、Fork0..3、12）：
  HOW，不是规范命题（boundary card DOES NOT OWN）。换名字/表示/默认值不改变 PAR 命题。
- **budget 的配置渠道**：本包只要求「有限正整数、必要时可配置」，不拥有配置系统。
- **Cursor wire 附着（Pair Hint）**：属 `provider-projection`，本包只消费 `effectiveAgent`。

## 依赖

DEPENDS ON：`participant-identity`（换执行者 ≠ 换人；身份字节 guarantee 由 identity 包提供）、`execution-model-routing`（EffectiveAgent 对应的 session model lease）、`interaction-authority`（continuation 的 wire/authority 语义）。理由：PAR-013 的「只换 EffectiveAgent，再由 MJS scheduler/lease 解析物理执行」消费前两者；PAR-014 的「continuation 只在该 Run 内」消费 interaction authority。

## 验证与测试落点

运行命令：单文件 `node --test requirements/provider-attempt-recovery/tests/<file>.test.mjs`；
整包即被 `node requirements/verification-system/tests/run.mjs` 自动发现（requirements 树）。落点类型：MOVE = 从旧
`tests/unit` 物理移入本包；REUSE = 留在原处（多 owner 或共享 checker），记锚点与 cutover 拆分；
NEW = 本包新写。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| PAR-001 Fallback 属 Logical Run；新 Root 新 cursor | `tests/cursor.test.mjs`：`FALLBACK_001_the_authority_root_fact_is_what_creates_the_cursor`、`FALLBACK_001_an_advance_with_no_accepted_root_stops_the_replay`、`FALLBACK_001_a_new_authority_root_replaces_the_cursor_entirely`；`tests/fallback-aabb-confluence.test.mjs`：`THEOREM_fold_independent_sessions_confluent_across_interleavings` | MOVE | `node --test requirements/provider-attempt-recovery/tests/cursor.test.mjs` |
| PAR-001（无 Run 不推进） | `tests/fallback-ledger.test.mjs`：`PAR_FALLBACK_001_no_active_run_advances_nothing_and_writes_no_fact` | NEW | `node --test requirements/provider-attempt-recovery/tests/fallback-ledger.test.mjs` |
| PAR-002 modulo-4 封闭 DU / 非法字节 fail-closed | `tests/cursor.test.mjs`：`FALLBACK_002_a_fresh_cursor_starts_at_offset_zero_with_no_budget_spent`、`FALLBACK_002_offset_is_modulo_four_and_never_stops_advancing`、`FALLBACK_002_each_offset_maps_to_a_fixed_side_and_a_fixed_agent`、`FALLBACK_002_an_offset_outside_zero_to_three_is_not_a_cursor_position` | MOVE | 同上 |
| PAR-003 唯一写入口 / 同失败只推进一次 | `tests/cursor.test.mjs`：`FALLBACK_003_the_same_attempt_observed_twice_advances_the_cursor_once`、`FALLBACK_003_the_dedupe_window_is_bounded_so_the_projection_cannot_grow_with_history`、`FALLBACK_003_a_duplicate_line_is_absorbed_because_replay_produces_it`；`tests/fallback-aabb-confluence.test.mjs`：`THEOREM_fallback_exactly_once_same_provider_run_advances_once`、`THEOREM_fold_duplicate_absorbed_not_double_counted`、`THEOREM_fallback_precedence_one_winner_for_one_cursor` | MOVE | 同上 |
| PAR-003（ledger 级去重） | `tests/fallback-ledger.test.mjs`：`PAR_FALLBACK_003_same_failure_observed_twice_advances_once` | NEW | 同上 |
| PAR-004 推进不变量（失败 +1 / 成功归零写入 FallbackSucceeded 事实） | `tests/cursor.test.mjs`：`FALLBACK_004_failure_advances_the_offset_and_spends_one_unit_of_budget`、`FALLBACK_004_success_resets_the_budget_but_NOT_the_offset`、`FALLBACK_004_success_leaves_a_parked_odd_offset_in_place`、`FALLBACK_004_recording_success_clears_the_dedupe_window_too`、`FALLBACK_004_success_is_a_durable_fact_that_zeroes_the_count`、`ENFORCER_063_success_clears_failures_after_multiple_advances_without_touching_offset` | MOVE | 同上 |
| PAR-005 有限预算 / Exhausted 停止自动请求 | `tests/cursor.test.mjs`：`FALLBACK_005_the_default_automatic_recovery_budget_is_twelve`、`FALLBACK_005_the_verdict_is_taken_after_the_failure_so_the_twelfth_is_final`、`FALLBACK_005_a_configured_budget_is_honoured_and_never_infinite`、`FALLBACK_005_exhaustion_is_stored_rather_than_re_derived_from_the_count`、`FALLBACK_005_may_continue_answers_the_projection_level_question`、`FALLBACK_005_an_advance_after_exhaustion_is_absorbed_not_applied` | MOVE | 同上 |
| PAR-005（host-facing admission） | `tests/fallback-ledger.test.mjs`：`PAR_FALLBACK_005_twelfth_failure_admission_is_recovery_exhausted`、`PAR_FALLBACK_005_admission_continues_while_budget_remains` | NEW | 同上 |
| PAR-005（跨包交叉：预算终点） | `requirements/degeneration-guard/tests/loop-sensor.test.mjs`：`LOOP_008_budget_exhaustion_is_final_and_writes_the_exhausted_fact` | MOVE（跨包引用） | `node --test requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| PAR-006 侧序列无界 | `tests/cursor.test.mjs`：`FALLBACK_006_the_side_sequence_table_is_unbounded_by_construction` | MOVE | 同上 cursor |
| PAR-007 fold 拒绝条件 | `tests/cursor.test.mjs`：`FALLBACK_007_a_valid_advance_moves_the_durable_cursor`、`FALLBACK_007_the_next_offset_must_be_the_modulo_four_successor`、`FALLBACK_007_the_count_must_advance_by_exactly_one_or_restart_at_one_after_success`、`FALLBACK_007_each_rejection_names_a_different_cause`、`FALLBACK_007_a_stale_previous_offset_is_refused_even_when_the_step_is_valid`、`FALLBACK_007_a_corrupt_transition_stops_the_replay_instead_of_being_absorbed`、`FALLBACK_007_an_advance_naming_another_run_is_absorbed_not_applied`、`FALLBACK_007_a_replayed_journal_reaches_the_same_cursor`、`FALLBACK_007_a_replayed_journal_with_intervening_success_streak_restart_reaches_the_same_cursor`；`tests/fallback-aabb-confluence.test.mjs`：`THEOREM_drop_ephemeral_preserves_fallback_cursor` | MOVE | 同上 |
| PAR-008 空/XML-only 不计入 | `tests/attempt-plan-profile.test.mjs`：`PAR_008_an_invalid_terminal_earns_at_most_one_repair_and_never_advances`、`CTX_012_only_a_probe_attempt_with_a_usable_terminal_may_promote`；`requirements/context-compression/tests/recovery-slot.test.mjs`：`FALLBACK_008_an_invalid_terminal_earns_exactly_one_repair`；`requirements/interaction-authority/tests/authority-root.test.mjs`：`IA_010_one_terminal_provider_run_earns_exactly_one_repair` | NEW + REUSE | `node --test requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs`；`node --test requirements/context-compression/tests/recovery-slot.test.mjs`；`node --test requirements/interaction-authority/tests/authority-root.test.mjs` |
| PAR-009 Host Attempt ≠ 领域计数 | `tests/cursor.test.mjs`：`FALLBACK_010_the_domain_count_is_reachable_only_through_a_confirmed_failure`、`FALLBACK_010_the_dedupe_identity_names_the_run_the_root_and_the_attempt` | MOVE | 同上 cursor |
| PAR-010 槽内维护子请求 | `tests/attempt-plan-profile.test.mjs`：`PAR_010_a_failed_squash_fails_the_slot_without_sending_the_main_request`、`PAR_010_a_successful_squash_keeps_the_count_and_continues_to_the_main_request`、`PAR_010_a_failed_main_fails_the_slot_and_advances_exactly_once`、`PAR_010_only_a_business_main_success_clears_the_failure_count`；`requirements/context-compression/tests/recovery-slot.test.mjs`：`FALLBACK_011_only_a_business_main_success_clears_the_failure_count`、`CTX_007_a_failed_squash_fails_the_slot_without_sending_the_main_request`、`CTX_007_a_successful_main_commits_and_does_not_move_the_cursor`、`CTX_007_a_failed_main_fails_the_slot_for_every_kind`、`CTX_008_only_a_failed_slot_advances_the_cursor` | NEW + REUSE | `node --test requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs`；`node --test requirements/context-compression/tests/recovery-slot.test.mjs`（SPLIT@cutover：CTX 锚点归 context-compression，FALLBACK-011/008 归本包） |
| PAR-011 armed 合取 / parked-cursor | `tests/attempt-plan-profile.test.mjs`：`PAR_011_recovery_requires_arming_a_primed_offset_and_material`、`PAR_011_arming_is_a_control_flow_fact_not_a_position`、`CTX_012_an_attempt_without_a_probe_cannot_promote_even_on_success`；`requirements/context-compression/tests/recovery-slot.test.mjs`：`FALLBACK_012_a_new_sequence_always_starts_unarmed`、`FALLBACK_012_only_a_failure_advance_arms_the_next_slot`、`FALLBACK_012_arming_is_lost_across_a_restart_and_the_safe_side_is_unarmed`、`FALLBACK_012_the_facade_offers_no_way_to_derive_arming_from_an_offset`、`FALLBACK_012_the_next_slot_is_armed_exactly_when_this_one_failed`、`FALLBACK_012_parked_cursor_does_not_trigger_compression_acceptance_trace`、`FALLBACK_012_at_least_one_real_failure_separates_any_two_squashes` | NEW + REUSE | `node --test requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs`；`node --test requirements/context-compression/tests/recovery-slot.test.mjs`（SPLIT@cutover 同上） |
| PAR-012 abort 清理残留不计入 | `tests/abort-residue.test.mjs`：`PAR_012_an_interrupted_tool_call_is_not_a_confirmed_failure`、`PAR_012_a_tool_error_without_interrupted_is_the_confirmed_failure`；`requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs`：`LOOP_006_interrupted_blog_repairs_without_advancing_primary_cursor`、`ENFORCER_065_tool_execution_error_blog_advances_primary_cursor_once`；e2e：`requirements/verification-system/tests/e2e/entry.test.mjs`（waitFact FallbackCursorAdvanced eq=4） | NEW + REUSE | `node --test requirements/provider-attempt-recovery/tests/abort-residue.test.mjs`；`node --test requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs`（SPLIT@cutover：ENFORCER 域锚点归 behavior-diagnosis） |
| PAR-013 换 Peer 不换身份字节 | `tests/attempt-plan-profile.test.mjs`：`FALLBACK_002_the_cursor_is_the_only_thing_that_moves_the_effective_agent`；`requirements/prefix-stability/tests/system-prompt-stability.test.mjs`：`PROMPT_STABILITY_fallback_peer_switch_keeps_persona_and_system_prompt_bytes` | NEW + REUSE | `node --test requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs`；`node --test requirements/prefix-stability/tests/system-prompt-stability.test.mjs`（SPLIT@cutover：身份字节 guarantee 归 participant-identity/provider-language/prefix-stability） |
| PAR-014 continuation 时序与次数 | `tests/fallback-ledger.test.mjs`：`PAR_014_a_continuation_has_a_unique_accounted_and_budgeted_occasion`；`tests/cursor.test.mjs`：`FALLBACK_005_may_continue_answers_the_projection_level_question`；`requirements/interaction-authority/tests/continuation-origin.test.mjs`：`IA_004_a_continuation_never_replaces_the_authority_root`、`IA_005_every_continuation_kind_is_representable_and_none_is_a_root` | NEW + MOVE + REUSE | `node --test requirements/provider-attempt-recovery/tests/fallback-ledger.test.mjs`；cursor 同上；`node --test requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| PAR-015 StrengthReplica 不进 owner controller | `tests/fallback-aabb-confluence.test.mjs`：`THEOREM_fallback_independent_sessions_commute_pure_projection`（不同 Session 的 run 互不干扰）；`tests/cursor.test.mjs`：`FALLBACK_010_the_dedupe_identity_names_the_run_the_root_and_the_attempt`（identity 含 SessionId → replica session 的 run 在机制上不可能推进/清零 owner cursor） | MOVE（机制锚点） | `node --test requirements/provider-attempt-recovery/tests/fallback-aabb-confluence.test.mjs`；同上 cursor。SPLIT@cutover：replica 侧 STRENGTH-004/019 的规范测试归 `speculative-investigation` |

### 包拥有的 semantic anchor id

`scripts/checks/semantic-anchors.mjs` 无本包语义 ID（该 catalog 只装 Role Law / office / tool
cognition anchors）；本包为空清单。

### cutover 待办（SPLIT@cutover）

1. `requirements/context-compression/tests/recovery-slot.test.mjs`：FALLBACK-008/011/012 断言迁入本包；
   CTX-006/007/008/010/012 断言归 `context-compression`；文件按 owner 拆分后删除原文件。
2. `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs`：PAR-012 两条锚点迁入本包或保留为
   cross-check（ENFORCER 域其余锚点归 `behavior-diagnosis`）。
3. `authority.test.mjs` 已拆（Wave 2a）：PAR-008/014 锚点现位于
   `requirements/interaction-authority/tests/authority-root.test.mjs`（IA_010）与
   `requirements/interaction-authority/tests/continuation-origin.test.mjs`（IA_004/005）；其余归
   `interaction-authority` / `dispatch-protocol`。
4. `requirements/prefix-stability/tests/system-prompt-stability.test.mjs`：PAR-013 锚点保留为 cross-check（身份字节
   owner 是 `participant-identity`/`provider-language`/`prefix-stability`）。
5. `requirements/verification-system/tests/e2e/entry.test.mjs`：e2e 由 lead 在 cutover 阶段归位。
