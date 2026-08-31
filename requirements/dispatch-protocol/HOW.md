# dispatch-protocol — HOW

## 架构机制与调度生命周期

`dispatch-protocol` 规范逻辑提示穿过不可靠传输层时的物理调度模型：

1. **Claim 注册与 PromptKey 派生**：
   在向底层通道发送消息前，`PromptDispatcher` 首先根据 `(SessionId, LogicalRunId, AuthorityRootId, Origin, EffectiveAgent, PayloadDigest, ClaimSequence)` 确定性计算 `PromptKey`，并持久化 `PluginPromptClaimed` 事实。

2. **传输交互与回执捕获**：
   - **Await 模式**：调用底层宿主发送接口，同步等待传输层的接收结果（即是否成功入栈），成功则写入 `PluginPromptSubmitted`。
   - **Detached 模式**：持久化 Claim 后立即异步交由宿主入栈并返回 `PromptKey`，不等待 provider 的执行结果。
   - 底层宿主发生传输拒绝时，Claim 转化为 `PluginPromptAbandoned(SendFailed)`。

3. **证据核对与恢复（Recovery Reconciliation）**：
   系统在恢复或对账路径中，读取宿主尾部物理消息，严格比对元数据中的 `PromptKey`：
   - 匹配到物理落地 → 补写 `PluginPromptPhysicalAccepted` 事实；
   - 未找到物理消息 → 保持 `StillPending`，绝不自动补发；
   - 物理读取失败 → 标记 `Unreadable` 并中止，保留现场供人工审计。

4. **Durability activation 与 execution handoff**：
   - plugin construction 只装配 dispatcher、physical evidence reader 与 handoff ports；不读取 journal，不启动 recovery。
   - durable substrate activation 成功后才启动 claim reconciliation；它只由 durable claim 或 Host physical evidence 事件推进。
   - `PhysicalAccepted` 建立后，把 exact `(SessionId, PhysicalUserMessageId)`、`PromptKey` 与原子 `AttemptExecutionProfile`（含完整版本化 `ParticipantIdentityEvidence`）交给 `managed-chat-execution`。容量、execution binding、provider start、failure disposition 与 settlement 均由其 owner 处理，dispatch 不保存镜像状态。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DISPATCH-PROTOCOL-001 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-001] DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime` |
| DISPATCH-PROTOCOL-002 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-002] DP_002_submit_records_the_receipt_without_resolving_the_claim`；`requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-002] DP_002_abandon_removes_the_claim_and_leaves_the_active_run_alone`；`requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-002] DP_002_claim_records_payload_digest_and_effective_agent` |
| DISPATCH-PROTOCOL-003 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-003] DP_003_receipt_shape_distinguishes_admission_from_physical_identity` |
| DISPATCH-PROTOCOL-004 | `requirements/dispatch-protocol/tests/ingress-identity.test.mjs::WHAT[DISPATCH-PROTOCOL-004] ingress_accepts_the_exact_nonblank_Host_identity`；`requirements/dispatch-protocol/tests/ingress-identity.test.mjs::WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_missing_or_blank_Host_identity`；`requirements/dispatch-protocol/tests/ingress-identity.test.mjs::WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_conflicting_Host_identity_carriers`；`requirements/dispatch-protocol/tests/ingress-identity.test.mjs::WHAT[DISPATCH-PROTOCOL-004] ingress_ignores_non_contract_identity_decoys`；`requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::WHAT[DISPATCH-PROTOCOL-004] DP_004_physical_acceptance_is_proven_only_by_physical_message` |
| DISPATCH-PROTOCOL-005 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-005] DP_005_prompt_key_is_deterministic_and_moves_with_every_component`；`requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-005] DP_005_claim_scope_names_exactly_session_run_origin_and_payload` |
| DISPATCH-PROTOCOL-006 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-006] DP_006_abandon_keeps_the_claim_sequence_consumed`；`requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-006] DP_006_claim_sequence_advances_on_registration_not_on_resolution` |
| DISPATCH-PROTOCOL-007 | `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::WHAT[DISPATCH-PROTOCOL-007] DP_007_restarts_never_auto_abandon_an_unresolved_broken_tool` |
| DISPATCH-PROTOCOL-008 | `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::WHAT[DISPATCH-PROTOCOL-008] DP_008_unproven_outcome_stays_pending_never_resends` |
| DISPATCH-PROTOCOL-009 | `requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_claims_and_persists_without_physical_accepted`；`requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_sdk_physical_id_does_not_race_chat_message_acceptance`；`requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_returns_even_when_session_send_task_never_settles`；`requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_continuation_same_claim_path`；`requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_await_mode_constructors_exist` |
| DISPATCH-PROTOCOL-010 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-010] DP_010_authority_root_profile_cannot_express_a_model` |
| DISPATCH-PROTOCOL-011 | `requirements/dispatch-protocol/tests/send-format.test.mjs::WHAT[DISPATCH-PROTOCOL-011] PROMPT_006_send_payload_carries_prompt_key_metadata` |
| DISPATCH-PROTOCOL-012 | `requirements/dispatch-protocol/tests/send-format.test.mjs::WHAT[DISPATCH-PROTOCOL-012] DP_012_physical_acceptance_hands_exact_claim_identity_to_managed_execution` |
| DISPATCH-PROTOCOL-013 | `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::WHAT[DISPATCH-PROTOCOL-013] DP_013_construction_waits_for_durability_activation_before_explicit_recovery` |
