# interaction-authority — HOW

## 架构机制与权限状态机

`interaction-authority` 通过纯函数式事实折叠维护唯一的权威状态投影：

1. **Ingress 授权把关**：
   `PromptIngress.handle` 是外部物理消息升级为权限实体的唯一入口。仅当当前无活跃 Profile 且消息显式指定合法 managed agent 时，Ingress 才向 `participant-identity` owner 请求为 exact root 准备 typed `ParticipantIdentityEvidence`。Authority 校验 exact keys/owner witness 后只执行一次 durable append：`AuthorityRootAccepted { root keys; ExpectedClosureKind; ParticipantIdentityEvidence; initial execution selection }`。该单一 fact 同时安装 identity 与接受 root；append 未提交时不发布 identity、root 或 profile，不存在可孤立的 identity write。活跃 run、缺失 evidence 或 evidence/run 不匹配一律 `UnknownOrigin`。

2. **来源判定管线（Resolution Pipeline）**：
   按固定顺序扫描 durable authority facts：
   - 物理确认接收的消息（`AcceptedContinuationIds`）→ 对应 Continuation 与原 identity evidence
   - 挂起的 PromptKey Claim → 已登记的意图来源
   - Host 压缩/合成提示 → HostInternal
   - 已注册 `AgentOwnerRoot` + exact typed owner-derived identity evidence → child/attached/InternalLeaf Root
   - 证明合法的物理用户输入 + identity owner 返回的 fresh evidence → HumanRoot
   - 未命中任何规则 → fail-closed `UnknownOrigin`

   Session cache、Host physical parent、agent 名称拆解与消息字段形态不进入 resolution。

3. **权威事实折叠（Authority Fold）**：
   identity 与 authority 投影严格从同一 `AuthorityRootAccepted` 重放 `(SessionId, LogicalRunId, AuthorityRootId, ExpectedClosureKind, ParticipantIdentityEvidence, execution selection)`，内存不维护独立可变 authority/identity 副本。它向 Host 与 execution 发布 exact profile view，而不复制身份解析规则；stable SelectedAgent/PeerAgent 来自 evidence，当前 EffectiveAgent/provider/model/lease 来自 execution binding。

4. **Durable closure interpreter**：
   acceptance 时由闭合 lifecycle 分类写定唯一 `ExpectedClosureKind`。terminal interpreter 只接受与该 kind 及 exact root keys 匹配的 typed durable outcome：HumanRoot Manager `LifeCompleted`、其他 HumanRoot `ManagedLogicalRunTerminal`、AgentOwner child Work `ChildLogicalRunTerminal`、AgentOwner attached Work `AttachedLogicalRunTerminal`、AgentOwner InternalLeaf `InternalLeafTerminal`；每个 terminal 穷尽其 lifecycle 的 Completed/Cancelled/Failed 合法结果。它幂等追加唯一 `AuthorityLogicalRunClosed`；只有该 append 确认后的 fold 才清空 active run/claims、释放 identity binding并归档 profile。terminal-source durable 而 closure 未确认时，reconciliation 重放 source 并重试同一 append；association removal、cancel request、idle/timeout、wall clock 与 Host observation 不进入 closure decision。同一 SessionId 的 fresh root 必须先观察 exact closure，不能读取归档 identity。

5. **Gate nudge 因果分层**：
    普通 nudge 的 durable occasion key 为 `gate kind + exact ProviderRunIdentity`（Manager idle 额外携带 Life/condition，Reviewer guard 额外携带 barrier）。是否“已经 admission”只由该 exact payload 的 Pending claim 或 AcceptedDispatch 证明；`ClaimSequence` 仅用于为重试生成新的 PromptKey，不能把已 Abandoned 的明确未发送尝试永久算作提醒完成。Repair owner 根据 typed provenance 与 `TurnUnknown | TurnInProgress | TurnNeedsContinuation | terminal` 分类决定：首次缺陷→发送一次；nudge 飞行态→等待；nudge 自身形成 fresh invalid terminal→重新提醒；普通旧 turn 的重复观察→幂等吸收。只有 Blogger nudge→AABB 这类显式升级协议保留独立的有界 repair state machine，不得把它的预算语义泛化到普通 gate nudge。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| INTERACTION-AUTHORITY-001 | `requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-001] IA_001_physical_message_promotes_to_authority_root` |
| INTERACTION-AUTHORITY-002 | `requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-002] IA_002_transport_receipt_shape_is_not_authority_evidence` |
| INTERACTION-AUTHORITY-003 | `requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-003] IA_003_malformed_profile_role_tier_and_root_kind_fail_closed`；`requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-003] IA_003_root_carries_resolved_participant_identity`；`requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-003] IA_003_closed_root_replacement_clears_run_scoped_state`；`requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-003] IA_003_root_remains_the_source_for_continuations`；`requirements/interaction-authority/tests/authority-acceptance-identity.test.mjs::WHAT[INTERACTION-AUTHORITY-003] HumanRoot persists identity before provider work and returns the exact profile`；`requirements/interaction-authority/tests/authority-execution-profile.test.mjs::WHAT[INTERACTION-AUTHORITY-003] valid authority profiles carry one atomic participant identity`；`requirements/interaction-authority/tests/authority-root-identity-upgrade.test.mjs::WHAT[INTERACTION-AUTHORITY-003] current schema-v2 authority bytes round-trip canonically` |
| INTERACTION-AUTHORITY-004 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::WHAT[INTERACTION-AUTHORITY-004] IA_004_continuation_inherits_run_and_root` |
| INTERACTION-AUTHORITY-005 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::WHAT[INTERACTION-AUTHORITY-005] IA_005_every_continuation_kind_is_parseable_and_not_root` |
| INTERACTION-AUTHORITY-006 | `requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-006] IA_006_bare_and_unknown_agent_names_are_refused`；`requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-006] IA_006_agent_owner_root_claim_rejects_legacy_name` |
| INTERACTION-AUTHORITY-007 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::WHAT[INTERACTION-AUTHORITY-007] IA_007_unknown_origin_changes_no_projection_state` |
| INTERACTION-AUTHORITY-008 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::WHAT[INTERACTION-AUTHORITY-008] IA_008_resolution_order_is_accepted_then_claimed_then_compaction_then_root`；`requirements/interaction-authority/tests/continuation-origin.test.mjs::WHAT[INTERACTION-AUTHORITY-008] IA_008_accepted_continuation_outranks_compaction` |
| INTERACTION-AUTHORITY-009 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::WHAT[INTERACTION-AUTHORITY-009] IA_009_pure_resolution_never_infers_human_root` |
| INTERACTION-AUTHORITY-010 | `requirements/interaction-authority/tests/join-guard.test.mjs::WHAT[INTERACTION-AUTHORITY-010] duplicate_idle_continuation_admission_is_not_terminal_failure` |
| INTERACTION-AUTHORITY-011 | `requirements/interaction-authority/tests/chat-params-hook.test.mjs::WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_parented_session_requires_provider_model_binding`；`requirements/interaction-authority/tests/chat-params-hook.test.mjs::WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_acceptance_establishes_binding_without_rewriting_host_model`；`requirements/interaction-authority/tests/chat-params-hook.test.mjs::WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_uses_the_resolved_provider_model_id_not_the_mutated_user_message_model`；`requirements/interaction-authority/tests/chat-params-hook.test.mjs::WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_accepts_the_real_provider_model_shape_with_message_variant`；`requirements/interaction-authority/tests/chat-params-hook.test.mjs::WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_leaves_temperature_untouched_when_model_capability_disables_it`；`requirements/interaction-authority/tests/chat-params-hook.test.mjs::WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_agentless_root_does_not_invent_binding` |
| INTERACTION-AUTHORITY-012 | `requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-012] IA_005_degeneration_guard_is_continuation` |
| INTERACTION-AUTHORITY-013 | `requirements/interaction-authority/tests/authority-root.test.mjs::WHAT[INTERACTION-AUTHORITY-013] continuation preserves logical run and root authority profile` |
| INTERACTION-AUTHORITY-014 | `requirements/interaction-authority/tests/join-guard-execution.test.mjs::WHAT[INTERACTION-AUTHORITY-014] EXEC_016_join_guard_is_a_continuation`；`requirements/interaction-authority/tests/join-guard-execution.test.mjs::WHAT[INTERACTION-AUTHORITY-014] EXEC_016_join_guard_instruction_requires_join_before_finish` |
| INTERACTION-AUTHORITY-015 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::WHAT[INTERACTION-AUTHORITY-015] IA_009_ingress_gates_human_root_on_active_run_and_explicit_agent`；`requirements/interaction-authority/tests/authority-acceptance-identity.test.mjs::WHAT[INTERACTION-AUTHORITY-015] matching external user ingress continues without replacing the active authority run` |
| INTERACTION-AUTHORITY-016 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::WHAT[INTERACTION-AUTHORITY-016] IA_016_accepted_root_claim_stays_out_of_continuation_map` |
| INTERACTION-AUTHORITY-017 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::WHAT[INTERACTION-AUTHORITY-017] IA_017_claimed_key_without_active_run_stays_unknown` |
| INTERACTION-AUTHORITY-018 | `requirements/interaction-authority/tests/logical-run-close.test.mjs::WHAT[INTERACTION-AUTHORITY-018] IA_018_human_root_closure_clears_active_run_and_retains_history`；`requirements/interaction-authority/tests/logical-run-close.test.mjs::WHAT[INTERACTION-AUTHORITY-018] IA_018_agent_owner_root_is_not_closed_by_life_completion` |
| INTERACTION-AUTHORITY-019 | `requirements/interaction-authority/tests/repair-lifecycle.test.mjs::WHAT[INTERACTION-AUTHORITY-019] repair claim does not turn an in-flight repair into exhaustion`；`requirements/interaction-authority/tests/repair-lifecycle.test.mjs::WHAT[INTERACTION-AUTHORITY-019] fresh invalid repair terminals re-open the gate reminder` |
