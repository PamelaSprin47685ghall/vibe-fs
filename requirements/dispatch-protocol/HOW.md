# dispatch-protocol — HOW

## 架构机制与调度生命周期

`dispatch-protocol` 规范逻辑提示穿过不可靠传输层时的物理调度模型：

1. **Claim 注册与 PromptKey 派生**：
   在向底层通道发送消息前，`PromptDispatcher` 首先根据 `(SessionId, LogicalRunId, AuthorityRootId, Origin, PayloadDigest, ClaimSequence)` 确定性计算 agent-free 的 `PromptKey`（不哈入任何 agent、peer 或 model；旧版含 agent 的 key 仅在回放历史时单向解码，新调度绝不双写），并持久化 `PluginPromptClaimed` 事实。
   terminal-scoped gate nudge 先以不含 sequence 的 claim scope 进入 dispatcher-owned physical single-flight；同 scope 的并发观察等待同一 Task，只有唯一 writer 派生 sequence、写 claim 并调用 Host。完成后删除 flight；后续 retry 仍由 durable Pending/Accepted/Abandoned projection 决定。

2. **传输交互与回执捕获**：
   - **Await 模式**：调用底层宿主发送接口，同步等待传输层的接收结果（即是否成功入栈），成功则写入 `PluginPromptSubmitted`。
   - **Detached 模式**：持久化 Claim 后立即异步交由宿主入栈并返回 `PromptKey`，不等待 provider 的执行结果。
   - 底层宿主发生传输拒绝时，Claim 转化为 `PluginPromptAbandoned(SendFailed)`。

3. **证据核对与恢复（Recovery Reconciliation）**：
   系统在恢复或对账路径中，读取宿主尾部物理消息，严格比对元数据中的 `PromptKey`（优先匹配 agent-free PromptKey；遇到旧持久化载荷时单向解码历史 agent-bearing PromptKey 进行匹配，绝不回写或双写）：
   - 匹配到物理落地 → 补写 `PluginPromptPhysicalAccepted` 事实；
   - 未找到物理消息 → 保持 `StillPending`，绝不自动补发；
   - 物理读取失败 → 标记 `Unreadable` 并中止，保留现场供人工审计。


   - plugin construction 只装配 dispatcher、physical evidence reader 与 handoff ports；不读取 journal，不启动 recovery。
   - durable substrate activation 成功后才启动 claim reconciliation；它只由 durable claim 或 Host physical evidence 事件推进。
   - `PhysicalAccepted` 建立后，把 exact `(SessionId, PhysicalUserMessageId)`、agent-free `PromptKey` 与原子 `AttemptExecutionProfile`（直接包含从 `IdentitySeed` 派生的固定不可变 `ParticipantIdentityEvidence`，含 participant/role/persona/personaCatalogVersion/provenance evidence；continuation 保持 participant 不变，不存在 peer/effective agent 轮换）交给 `managed-chat-execution`。每次 fresh physical execution 均将固定 role 经 MJS scheduler 路由至 model target，且 capacity exact identity 严格绑定为 `session + physical + role + participant + target + fence`。容量、execution binding、provider start、failure disposition 与 settlement 均由其 owner 处理，dispatch 不保存镜像状态。
   - Host 发送的 `agent` 固定为不可变的 `participant`，`model` 固定为 null；显式外部 agent 仍作为输入保留，并与 participant 做一致性校验。

5. **Host physical identity 解码**：
   `PromptIngressCodec` 只读取 Host 1.18.29 契约中的 `input.messageID` 与 `output.message.id`。空白 carrier 视为缺失；两个非空 carrier 必须保持原始字节完全一致。缺失、冲突、仅有非契约字段时均不生成 `PhysicalUserMessageId`。
   `SessionId` carrier先逐字段解码为`Absent | Invalid | Valid opaque-string`，再跨四个正式source汇总：存在`Invalid`或多个不同`Valid`即拒绝，全部`Valid`原始字节相同才建立identity。嵌套`session`只接受plain JSON record的own data property；PromptKey 与旧版只读 agent carrier 复用相同汇总器，禁止字段优先级掩盖冲突。wire 上新生成的 PromptKey carrier 必须为 agent-free `PromptKey`；旧版含 agent 的 key 仅用于读取旧事实，不得双写。测试从注册的dispatch production Surface穿越Fable边界，并以source × alias × value-kind × multiplicity生成完整partition。

### Nudge 编译边界

`dispatch/session-nudge` 分片独立编译 `Interaction/Repair/Port` 与 `Interaction/Dispatch/OpenCode/SessionNudge`，仅依赖 dispatcher、root-workspace contract、authority ledger 和通用 async 原语。Companion repair 直接消费此窄分片，不必引入同时承载 turn reconciliation 的 ingress 分片；ingress 也引用它，而不再重复编译这些源码。F# 公开符号、aggregate 输入及顺序不变，未知 acceptance、quiescence 消费／归还与 exact-occasion 去重语义保持原样。

## 验证与测试落点
| 命题 | 落点测试 |
|---|---|
| DISPATCH-PROTOCOL-001 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-001] DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime` |
| DISPATCH-PROTOCOL-002 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-002] DP_002_submit_records_the_receipt_without_resolving_the_claim`；`requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-002] DP_002_abandon_removes_the_claim_and_leaves_the_active_run_alone`；`requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-002] DP_002_claim_records_payload_digest_and_participant` |
| DISPATCH-PROTOCOL-003 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-003] DP_003_receipt_shape_distinguishes_admission_from_physical_identity` |
| DISPATCH-PROTOCOL-004 | `requirements/dispatch-protocol/tests/ingress-identity.test.mjs::WHAT[DISPATCH-PROTOCOL-004] ingress_accepts_the_exact_nonblank_Host_identity`；`requirements/dispatch-protocol/tests/ingress-identity.test.mjs::WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_missing_or_blank_Host_identity`；`requirements/dispatch-protocol/tests/ingress-identity.test.mjs::WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_conflicting_Host_identity_carriers`；`requirements/dispatch-protocol/tests/ingress-identity.test.mjs::WHAT[DISPATCH-PROTOCOL-004] ingress_ignores_non_contract_identity_decoys`；`requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::WHAT[DISPATCH-PROTOCOL-004] DP_004_physical_acceptance_is_proven_only_by_physical_message` |
| DISPATCH-PROTOCOL-005 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-005] DP_005_prompt_key_is_deterministic_and_moves_with_every_component`；`requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-005] DP_005_claim_scope_names_exactly_session_run_origin_and_payload` |
| DISPATCH-PROTOCOL-006 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-006] DP_006_abandon_keeps_the_claim_sequence_consumed`；`requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-006] DP_006_claim_sequence_advances_on_registration_not_on_resolution` |
| DISPATCH-PROTOCOL-007 | `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::WHAT[DISPATCH-PROTOCOL-007] DP_007_restarts_never_auto_abandon_an_unresolved_broken_tool` |
| DISPATCH-PROTOCOL-008 | `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::WHAT[DISPATCH-PROTOCOL-008] DP_008_unproven_outcome_stays_pending_never_resends`；`requirements/dispatch-protocol/tests/send-format.test.mjs::WHAT[DISPATCH-PROTOCOL-008] DP_008_concurrent_exact_gate_nudges_share_one_claim_and_send` |
| DISPATCH-PROTOCOL-009 | `requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_claims_and_persists_without_physical_accepted`；`requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_sdk_physical_id_does_not_race_chat_message_acceptance`；`requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_returns_even_when_session_send_task_never_settles`；`requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_continuation_same_claim_path`；`requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_await_mode_constructors_exist` |
| DISPATCH-PROTOCOL-010 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::WHAT[DISPATCH-PROTOCOL-010] DP_010_authority_root_profile_cannot_express_a_model` |
| DISPATCH-PROTOCOL-011 | `requirements/dispatch-protocol/tests/send-format.test.mjs::WHAT[DISPATCH-PROTOCOL-011] PROMPT_006_send_payload_carries_prompt_key_metadata` |
| DISPATCH-PROTOCOL-012 | `requirements/dispatch-protocol/tests/send-format.test.mjs::WHAT[DISPATCH-PROTOCOL-012] DP_012_physical_acceptance_hands_exact_claim_identity_to_managed_execution`；`requirements/dispatch-protocol/tests/binding-recovery.test.mjs::WHAT[DISPATCH-PROTOCOL-012] recovered turn binding restores durable participant and role when process-local role is absent` |
| DISPATCH-PROTOCOL-013 | `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::WHAT[DISPATCH-PROTOCOL-013] DP_013_construction_waits_for_durability_activation_before_explicit_recovery` |
| DISPATCH-PROTOCOL-014 | `requirements/dispatch-protocol/tests/m6-fatal-boundary.test.mjs::WHAT[DISPATCH-PROTOCOL-014] dispatch fatal preserves exact claim truth and one injected fuse` |
| DISPATCH-PROTOCOL-015 | `requirements/dispatch-protocol/tests/ingress-wire-algebra.test.mjs::WHAT[DISPATCH-PROTOCOL-015] ingress identity carrier algebra is exact conflict closed and byte preserving`；`requirements/dispatch-protocol/tests/ingress-wire-algebra.property.test.mjs::WHAT[DISPATCH-PROTOCOL-015] ingress identity property rejects every malformed or ambiguous carrier world` |
