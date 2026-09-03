# provider-attempt-recovery — HOW

## 架构与核心机制

### Cursor 代数与写入口

- **ProviderRequestKind + AgentPairCursor**：`src/Wanxiangshu/Participant/Provider/Attempt/Cursor.fs` 同时拥有可证明的请求种类与模 4 游标（Offset 0..3 映射到 SideA/SideA'/SideB/SideB'），维护连续失败计数与有限自动恢复预算（默认 12）；`Context/Prefix/Candidate.fs` 不再拥有请求种类。
- **FallbackLedger**：唯一写入口。负责对 `ProviderRunIdentity` 进行有界去重，追加 `FallbackCursorAdvanced`、`FallbackSucceeded` 或 `FallbackExhausted`。
- **ConfirmedFailurePort**：`src/Wanxiangshu/Participant/Provider/Attempt/Fallback/ConfirmedFailurePort.fs` 拥有 `ConfirmedFailureOutcome`，端口返回 `Task<Result<ConfirmedFailureOutcome,string>>`；所有消费者穷尽处理 `RecoveryAdvanced | RecoveryExhausted | AlreadyRecorded | NoActiveRun`，且 `NoActiveRun` 必须停止，不能继续恢复。
- **RecoverySlot 槽决策**：`src/Wanxiangshu/Participant/Provider/Attempt/RecoverySlot.fs` 把刚完成的 failure advance + primed Offset 归约为一次 `RecoveryOpportunity`；`nextBloggerRequest` 以 `BloggerSlotDispatchError` 返回 typed dispatch failure。维护子请求失败与主请求失败均收敛为单次失败槽推进，维护成功不清零计数，主业务成功清零计数并把 A′/B′ 归一到同侧 A/B 普通槽。
- **历史 replay 边界**：PAR-004 改为成功关闭 A′/B′ 后，旧 journal 中已落盘的 `FallbackSucceeded → A′→B` / `FallbackSucceeded → B′→A` 仍必须可重放。fold 只吸收这一种“成功归一化后 previousOffset 比 canonical cursor 多一步”的历史形状；新 writer 永远从归一化后的 A/B 写 `A→A′` / `B→B′`，不得继续生产旧形状。

### 恢复编排

1. **Typed recovery licence**：Host 完整快照只提供 exact `ProviderRunIdentity` 与 terminal evidence；`execution-failure-policy` 是 failure class、retry/fallback、breaker 与 budget 后果的唯一裁决者。FallbackController 只消费绑定当前 run/request kind/decision identity 的 provider recovery licence，不解析 terminal 文本，也不为非 provider class 建立 recovery。
2. **Admission 裁决**：只有 licence 明确授权 `AdvanceFallback` 才写 `FallbackCursorAdvanced`；只有授权 continuation 的 `RecoveryAdvanced` 才继续。WorkMain 在新 primed 槽获得一次 X opportunity；BloggerMain 在新 primed 槽且有 frames 时先发送 BloggerSquash。
3. **WorkMain retry 物理所有权**：recovery continuation 先按正常 Prompt admission 发送；只有 `PromptIngress` 已把该 `ProviderRetryAttempt` 持久化为 `PhysicalAccepted` 后，才用 exact `PhysicalUserMessageId` 建立一次性 recovery permit。`messages.transform` 只允许消费 physical id 完全相等的 permit；同 session 的 tool continuation、旧 retry 或普通 user material 均不得误领。
4. **ProviderRun 延迟绑定**：`messages.transform` 发生在 provider inference 之前，只冻结由 authority/cursor/physical id/request kind/prefix choice 构成的 pending attempt plan，不读取未来 assistant run。后续 tool-continuation 可见性或 reconciled turn 提供 `PhysicalUserMessageId + ProviderRunIdentity` 时，把 pending plan 一次性绑定成 `AttemptExecutionProfile`，再执行 prefix promotion / success accounting。
5. **Blogger retry 所有权**：失败 open request 先 abandon；下一 typed request 在物理发送前 materialize，并在 send 后绑定该次 PromptKey。Main→Main、Main→Squash、Squash→Main 共用同一规则。`Fallback/Workflow.fs` 与 `Interaction/Repair/InteractionRepair.fs` 在追加失败账本前都必须先从 Blogger 身份解析 exact main session；禁止把 Blogger session 自身当作主 session 记账。
6. **事件解锁**：WorkMain recovery 只在 linked Blogger 存在 durable open request 时通过 `AgentJournal.awaitChangeFromOrCancel` 订阅 committed journal change；`BlogObservationCommitted`、`BlogObservationsSquashed`、`BloggerRequestAbandoned` 等 fact 到达后重新求值，plugin shutdown 显式注销订阅。无 open producer 立即 retry，不读取 flight/pending，不存在 timeout/polling。
7. **成功记账**：RequestKind 从 typed request / durable receipt / accepted continuation evidence 证明。Squash/repair success 不写 FallbackSucceeded；WorkMain/BloggerMain success 才清零失败计数。
8. **身份隔离**：模 4 游标推进只消耗预算/轮换 provider 目标，不改变身份。`EffectiveAgent` 恒等于 `SelectedAgent` (`Peer = SelectedAgent`)，provider 轮换在模型层发生。每个 attempt 复用同一 durable logical participant run 的 `ParticipantIdentity`、Persona、语言、CanonicalRole、Authority identity 与 system prompt bytes；controller 没有签发新 identity 的能力。
9. **唯一预算投影**：`LogicalRunId` 是稳定 logical operation identity，Host 创建的 fresh `ProviderRunIdentity` 是 physical attempt identity。`FallbackProjection.ConsecutiveFailureCount < AgentPairCursor.DefaultAutoRecoveryBudget(12)` 是唯一自动恢复预算投影；初始请求计入失败序列，第 12 次失败只写 exhaustion，不发送第 13 次物理请求。因此每个 logical operation 满足 `physicalAttempts ≤ 12`，duplicate ProviderRun observation 由 ledger 幂等吸收且不增加该比值。
10. **唯一 licence**：`ExecutionFailurePolicy.decide` 以 exact `LogicalRunId + ProviderRunIdentity + ProviderRequestKind` 生成 sealed `ProviderRecoveryAuthorization` 与稳定 `ProviderRecoveryDecisionId`。`FallbackLedger.recordAuthorizedFailure` 只接受该 licence，并复核 logical run；Host retry signal、dispatcher、repair、session recovery 无签发能力。

## 最终 production 路径

- `src/Wanxiangshu/Participant/Provider/Attempt/Cursor.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Planner.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/RecoverySlot.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/ConfirmedFailurePort.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/CursorSurface.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Evidence.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Fact.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Facts.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/FallbackFactFold.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Ledger.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Projection.fs`
- `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs`

`Fallback/CursorSurface.fs` 是唯一 recovery JS proof surface；两个旧 proof surface 已删除，不是合同路径。

## 依赖关系

DEPENDS ON:
- `participant-identity`
- `execution-failure-policy`
- `execution-model-routing`
- `interaction-authority`
- `context-compression`
- `prefix-stability`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PAR-001 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-001] FALLBACK_001_an_active_authority_root_must_close_before_replacement`；`requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-001] FALLBACK_001_the_authority_root_fact_is_what_creates_the_cursor` |
| PAR-002 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-002] FALLBACK_002_an_offset_outside_zero_to_three_is_not_a_cursor_position` |
| PAR-003 | `requirements/provider-attempt-recovery/tests/fallback-ledger.test.mjs::WHAT[PAR-003] PAR_FALLBACK_003_same_failure_observed_twice_advances_once` |
| PAR-004 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-004] FALLBACK_004_failure_advances_the_offset_and_spends_one_unit_of_budget`；`requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-004] FALLBACK_004_success_resets_the_budget_and_normalizes_to_the_same_side_main_slot` |
| PAR-005 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-005] FALLBACK_005_the_default_automatic_recovery_budget_is_twelve`；`requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-005] FALLBACK_005_the_verdict_is_taken_after_the_failure_so_the_twelfth_is_final` |
| PAR-006 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-006] FALLBACK_006_the_side_sequence_table_is_unbounded_by_construction` |
| PAR-007 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-007] FALLBACK_007_each_rejection_names_a_different_cause` |
| PAR-008 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs::WHAT[PAR-008] PAR_008_an_invalid_terminal_earns_at_most_one_repair_and_never_advances` |
| PAR-009 | `requirements/provider-attempt-recovery/tests/cursor.test.mjs::WHAT[PAR-009] FALLBACK_010_the_domain_count_is_reachable_only_through_a_confirmed_failure` |
| PAR-010 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs::WHAT[PAR-010] PAR_010_a_failed_squash_fails_the_slot_without_sending_the_main_request`；`requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs::WHAT[PAR-010] PAR_010_a_successful_squash_keeps_the_count_and_continues_to_the_main_request` |
| PAR-011 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs::WHAT[PAR-011] PAR_011_fallback_advance_returns_the_fresh_opportunity_and_workflow_never_rebuilds_it_from_cursor_parity`；`requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs::WHAT[PAR-011] PAR_011_workmain_recovery_arms_only_after_exact_provider_retry_physical_acceptance` |
| PAR-012 | `requirements/provider-attempt-recovery/tests/abort-residue.test.mjs::WHAT[PAR-012] PAR_012_an_interrupted_tool_call_is_not_a_confirmed_failure` |
| PAR-013 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs::WHAT[PAR-013] FALLBACK_002_the_cursor_changes_only_the_effective_agent`；`requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs::WHAT[PAR-013] attempts derive their system prompt and tools from the participant identity Role` |
| PAR-014 | `requirements/provider-attempt-recovery/tests/fallback-ledger.test.mjs::WHAT[PAR-014] PAR_014_a_continuation_has_a_unique_accounted_and_budgeted_occasion` |
| PAR-015 | `requirements/provider-attempt-recovery/tests/fallback-aabb-confluence.test.mjs::WHAT[PAR-015] THEOREM_fallback_independent_sessions_commute_pure_projection` |
| PAR-016 | `requirements/provider-attempt-recovery/tests/attempt-plan-profile.test.mjs::WHAT[PAR-016] PAR_016_success_accounting_requires_proven_request_kind` |
| PAR-017 | `requirements/context-compression/tests/companion-recovery-slot.test.mjs::WHAT[PAR-017] PAR_017_blogger_retry_abandons_then_materializes_then_binds_new_prompt` |
| PAR-018 | `requirements/context-compression/tests/companion-recovery-slot.test.mjs::WHAT[PAR-018] recovery_continuation_waits_only_on_durable_open_producer_events` |

P0 recovery re-entry proof：`requirements/structured-workflow/tests/recovery-reentry.test.mjs`；hard gate：`scripts/checks/p0-recovery-join.mjs`。该 gate 同时约束 Blogger failure 在追加 ledger 前解析 exact main session，以及 `NoActiveRun` 不得继续 recovery。
| PAR-019 | `requirements/provider-attempt-recovery/tests/retry-owner.test.mjs::WHAT[PAR-019] one policy owner licenses every provider recovery attempt`；`requirements/verification-system/tests/retry-owner.test.mjs::WHAT[PAR-019] rejects nested physical retry owner`；`requirements/verification-system/tests/retry-owner.test.mjs::WHAT[PAR-019] rejects retry classification from diagnostic text` |
