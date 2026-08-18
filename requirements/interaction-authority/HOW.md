# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可以重写而不改 WHAT。
> 新工程师用它把命题对到代码。

## 类型与函数地图（interaction-authority）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001/002 | `Kernel/Identity.fs` → `PhysicalUserMessageId.promoteToAuthorityRoot`、`TransportReceipt`（`isAdmissionShaped`） | 唯一 crossing；`TransportReceipt` 与物理 id 类型不同 |
| 003/004/011 | `Domain/PromptAuthority.fs` → `AuthorityExecutionProfile`、`PromptAuthorityProjection`；`Domain/PromptAuthorityRun.fs` → `createAuthorityRoot`、`registerAuthority`、`claimContinuation` | root 固定 profile；continuation 继承 run/root |
| 005 | `Domain/PromptAuthority.fs` → `PromptOrigin`、`RootAuthorityKind`、`ContinuationKind`、`originLabel`、`tryParseContinuationKind` | 闭世界枚举 |
| 006 | `Domain/PromptAuthority.fs` → `parseAgentNameTyped` / `AgentNameRejection`；`Domain/ManagedAgentCatalog.fs` → `legacyAgentNames`、`peerNameOf` | 显式 agent 才可成 root |
| 007/008/009/017 | `Domain/PromptAuthorityRun.fs` → `resolveKnownOrigin`（accepted → claimed → compaction → AgentOwnerRoot → UnknownOrigin）；`Application/Prompting/PromptIngress.fs` → `resolveOrigin`（唯一可授予 HumanRoot 的边界） | 纯函数永不返回 HumanRoot；ingress 只在 ActiveProfile 缺席 + 显式有效 agent 时授予 |
| 010 | `Interaction/Authority/Model.fs` → `repairFamilyPayloadDigest/repairFamilyAlreadyClaimed`（ordinary LogicalRun+family）、`repairPayloadDigest/repairAlreadyClaimed`（Blogger `BloggerRequestId + terminal + kind` special case）、`idlePayloadDigest/idleAlreadyClaimed`（Manager `Life + condition + terminal ProviderRun` exact occasion）；ClaimSequence 提供 durable occasion，feature recovery 再结合 Pending/Accepted/Abandoned dispatch lifecycle | 自动 continuation occasion durable；bounded repair 与 unbounded-per-terminal Manager encouragement 各自保留正确次数语义 |
| 011 | `Domain/PromptAuthority.fs` → `AttemptExecutionProfile`、`buildAttemptExecutionProfile`（唯一 builder） | authority 子记录原子携带 |
| 012/013 | `Domain/PromptAuthority.fs` → `ContinuationKind.NeedHelpEscalation | NeedHelpAdvice`；`Infrastructure/OpenCode/Host/AssistanceHost.fs` | assistance 续推同 run；该同步交互只 Await Host transport result（便于本调用判断拒绝），不等 provider execution/slot；abort 不推进 fallback |
| 014 | `Execution/Delegation/Fork/OpenCode/JoinGuard.fs`、`Mission/Manager/Idle.fs`；`ContinuationKind.JoinGuard | ManagerIdleEncouragement` | join/idle 续推 = continuation；JoinGuard Await transport result，以便拒绝时释放 reservation；Manager idle process key + durable claim 都绑定 exact terminal ProviderRun，同 terminal 幂等、fresh terminal 无限可继续 |
| 015 | `Session/JoinInterruptRegistry.fs`（`UserMessageArrived`）；`PromptIngressCodec`（ExternalUserIngressPulse 候选） | wake 低权限；ingress 不给 authority |
| 016 | `Domain/PromptAuthorityRun.fs` → `acceptClaim`（root 不入 continuation map） | root ≠ continuation |

事实折叠：`Interaction/Authority/PromptFactFold.fs` 把 `PluginPromptClaimed/Submitted/PhysicalAccepted/Abandoned` 与
`AuthorityRootAccepted` fold 进 `AgentProjectionSet`；`Interaction/Authority/Ledger.fs` 是
`PromptAuthorityProjection` 的纯 fold（`foldAuthorityRootAccepted`、`foldPromptClaimed`…）。authority
状态没有第二份内存拷贝——`PromptDispatcher.Runtime` 无可变 authority 字段，每次读都走 fold
（`ProjectionFor`）。

## 关键接线：HumanRoot 只能在 ingress 授予

`PromptIngress.handle` 是「物理用户消息成为 authority」的唯一入口（PROMPT-004）：

```text
resolveOrigin（journal 已知 provenance）
  → UnknownOrigin 时：ExplicitAgent 有效 AND ActiveProfile=None（首个外部 prompt）→ HumanRoot
  → 其余一律 UnknownOrigin（fail-closed）
```

`Runtime.AcceptHumanRoot` 再校验显式 agent，随后 `RegisterAuthority` 写 `AuthorityRootAccepted` 事实。
`AcceptAgentOwnerRoot` 要求 claim 是 pending AgentOwnerRoot（`claimAgentOwnerRoot`），且先写
`PluginPromptPhysicalAccepted` 再 `RegisterAuthority`——PhysicalAccepted 不能排在 root 生效之后
（PROMPT-005 顺序）。

## 约束

- `PromptDispatcher.Runtime` 不持有 authority 状态：防内存拷贝与 journal 分叉（PROMPT-005 的
  durability 前提）。
- continuation 归属只读 `ActiveLogicalRun`（不回退 `LastAuthorityProfile`）。
- root 的 `registerAuthority` 清空 run-scoped 映射（PendingClaims/AcceptedContinuationIds/ClaimSequences），使 PERSIST-008 有界。
- ordinary repair 的 claim scope payload 只含 repair family；LogicalRunId 已是 scope 组件，所以同 run 后续 terminal 不会重置预算。Blogger exact-one special digest = `BloggerRequestId + terminal ProviderRunIdentity + repair kind`：request axis 隔离长寿命 Blogger session 上连续的工作请求，terminal axis 只负责同 terminal 幂等。`blogger-missing-tool` nudge 每 request 一次；进入 AABB 后，每个新的 invalid terminal 可有新的 `blogger-aabb` occasion，但是否继续由 shared fallback projection/budget 限界，而不是用“已有任意 AABB claim”当 exhaustion。
- Manager idle digest = `LifeId + conditionKey`；condition 由 Manager 是否已有 plan commitment 决定，ProviderRunIdentity 不参与自动 encouragement budget。

## 历史与弃权

- **legacy agent 名单与精确错误文案**（`ManagedAgentCatalog.legacyAgentNames`、
  `formatLegacyNameNotSupported`）：COVERAGE 判 AGENT-004 为 GARBAGE（migration ratchet）。
  本包保留 WHAT「HumanRoot 必须显式 managed agent + 拒绝是 typed 的」（AGENT-005），精确名单与
  文案只在 HOW 记录，不升格为命题。`student-teacher-absence.mjs` 等 absence ratchet 已随新世界
  基线稳定删除（CLN-Z；PROOF-MAP DELETE 清单）。
- **`PromptAuthority.fromString` / ManagerGuard 历史 journal 解析**：COVERAGE 判 HOW——仅用于
  解析历史 journal 行，生产不再发送 ManagerGuard continuation（GLORY-070）。ManagerGuard 仍是
  `ContinuationKind` 成员（可解析），但不再作为新发送的 origin。
- **PROMPT-012（Student/Teacher）**：GARBAGE——编号永久空缺，无 alias、无 deprecated 路径。
  「插件 user-shaped message 仍经 PROMPT-005」保留给 `dispatch-protocol`。
- **QuiescencePermit 资格机制**：`cache.md` 的 idle-only auto-continue 资格（SessionQuiescenceGate）归 `causal-wait`（HOST-004 分片）；本包只拥有「idle 续推是 continuation、自动预算必须稳定有界」。permit 只防同一次 idle race，不替代跨 terminal/restart 的 durable budget。
- **`AttemptExecutionProfile` record 字段集**：HOW（HANDOFF §18.4 integration structure），
  不是未来 WHAT；「字段不可从碎片拼装」才是 WHAT（INTERACTION-AUTHORITY-011）。

## 不归我（DOES NOT OWN）

- transport claim / submission / physical acceptance 协议 → [`dispatch-protocol`](../dispatch-protocol/WHAT.md)
- provider projection、attempt recovery、Persona 定义
- `AttemptExecutionProfile` 的当前 record 布局（HOW）
- Companion 关联、SessionPersona 重绑、Model=None 发送海关（分别归 `session-ontology` / `participant-identity` / `dispatch-protocol`）

## 验证与测试落点

### 运行方式

```bash
node --test requirements/interaction-authority/tests/authority-root.test.mjs
node --test requirements/interaction-authority/tests/continuation-origin.test.mjs
## 全量：node requirements/verification-system/tests/run.mjs（自动包含 requirements/**/tests/*.test.mjs）
```

### 命题 → 落点

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| R1 | INTERACTION-AUTHORITY-001 | `requirements/interaction-authority/tests/authority-root.test.mjs::IA_001_physical_message_promotes_to_authority_root` | NEW | `node --test requirements/interaction-authority/tests/authority-root.test.mjs` |
| R1b | INTERACTION-AUTHORITY-002 | `requirements/interaction-authority/tests/authority-root.test.mjs::IA_002_transport_receipt_shape_is_not_authority_evidence` | NEW | 同上 |
| R2 | INTERACTION-AUTHORITY-003 | `requirements/interaction-authority/tests/authority-root.test.mjs::IA_003_malformed_profile_role_tier_and_root_kind_fail_closed` + `IA_003_root_derives_peer_role_and_tier_from_selected_agent` + `IA_003_new_root_clears_run_scoped_state` + `IA_003_root_remains_the_source_for_continuations` | NEW | 同上 |
| R3 | INTERACTION-AUTHORITY-004 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::IA_004_continuation_inherits_run_and_root` | NEW | `node --test requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| R4 | INTERACTION-AUTHORITY-016 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::IA_016_accepted_root_claim_stays_out_of_continuation_map` + `requirements/interaction-authority/tests/authority-root.test.mjs::IA_016_agent_owner_root_has_no_run_before_physical_acceptance` | NEW | 两文件各自命令 |
| R5 | INTERACTION-AUTHORITY-005 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::IA_005_every_continuation_kind_is_parseable_and_not_root`（全部 ContinuationKind 可解析、无一是 Root；HumanRoot 不可解析为 continuation） | NEW | `node --test requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| R6 | INTERACTION-AUTHORITY-006 | `requirements/interaction-authority/tests/authority-root.test.mjs::IA_006_bare_and_unknown_agent_names_are_refused` + `IA_006_agent_owner_root_claim_rejects_legacy_name` | NEW | `node --test requirements/interaction-authority/tests/authority-root.test.mjs` |
| R7 | INTERACTION-AUTHORITY-007/INTERACTION-AUTHORITY-008/INTERACTION-AUTHORITY-017 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::IA_008_resolution_order_is_accepted_then_claimed_then_compaction_then_root` + `IA_017_claimed_key_without_active_run_stays_unknown` + `IA_007_unknown_origin_changes_no_projection_state` | NEW | `node --test requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| R8 | INTERACTION-AUTHORITY-009 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::IA_009_pure_resolution_never_infers_human_root`（resolveKnownOrigin 永不返回 HumanRoot；active HumanRoot 不抬升未知消息） | NEW | `node --test requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| R9 | INTERACTION-AUTHORITY-010 | `requirements/interaction-authority/tests/join-guard.test.mjs::PROMPT_010_generic_repair_family_is_bounded_once_per_run`（ordinary repair 同 LogicalRun+family 只发一次）；`requirements/interaction-authority/tests/authority-root.test.mjs::IA_010_terminal_repair_identity_is_exactly_once`（Blogger special occasion 同时包含 BloggerRequestId + terminal + kind；同 terminal/kind 换 request 得 fresh budget）；cross-package REUSE `requirements/interaction-authority/tests/projection-algebra-repair.test.mjs::PROJ_008_InsertRepair_uses_the_production_instruction`（transform 无 physical nudge sender）。idle authority 在最终物理 SendPrompt 边界的 freshness/Abandoned 语义归 `dispatch-protocol`，见其 PROOF R1。 | NEW + REUSE | `node --test requirements/interaction-authority/tests/join-guard.test.mjs requirements/interaction-authority/tests/authority-root.test.mjs requirements/interaction-authority/tests/projection-algebra-repair.test.mjs` |
| R10 | INTERACTION-AUTHORITY-012 | `requirements/interaction-authority/tests/assistance-host.test.mjs::AGENT_031_needhelp_is_same_session_deep_peer_continuation`（sends[0].agent = deep peer；fallback offset/failures 不变；AcceptedContinuationIds 含 NeedHelpEscalation 不含 ProviderRetryAttempt）+ `requirements/interaction-authority/tests/authority-root.test.mjs::IA_005_needhelp_kinds_are_continuations` | NEW | `node --test requirements/interaction-authority/tests/assistance-host.test.mjs` |
| R11 | INTERACTION-AUTHORITY-013 | `requirements/interaction-authority/tests/assistance-host.test.mjs::AGENT_031_deep_binding_uses_consultation_continuation_without_new_root`（fast→deep 同 Session 续推；consultation 部分归 delegation） | NEW | `node --test requirements/interaction-authority/tests/assistance-host.test.mjs` |
| R12 | INTERACTION-AUTHORITY-014 | `requirements/interaction-authority/tests/join-guard-execution.test.mjs::EXEC_016_join_guard_is_a_continuation` + `EXEC_016_join_guard_instruction_requires_join_before_finish`；`requirements/interaction-authority/tests/join-guard.test.mjs::JNGD_nudge_contract_fails_closed_without_durable_authority`（JoinGuard 是 continuation；nudge 无 durable authority 时 fail-closed） | NEW | `node --test requirements/interaction-authority/tests/join-guard-execution.test.mjs requirements/interaction-authority/tests/join-guard.test.mjs` |
| R13 | INTERACTION-AUTHORITY-015 | `requirements/interaction-authority/tests/continuation-origin.test.mjs::IA_009_ingress_gates_human_root_on_active_run_and_explicit_agent`；cross-package REUSE `requirements/delegation/tests/join-v2-mailbox.test.mjs`（wake 机制归 delegation；ingress 不给 authority 归本包） | REUSE | `node --test requirements/interaction-authority/tests/continuation-origin.test.mjs requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| R14 | INTERACTION-AUTHORITY-011 | `requirements/interaction-authority/tests/authority-root.test.mjs::PROMPT_011_logical_run_id_is_stable_and_input_sensitive`（LogicalRunId 是 runtime+session+root 的确定函数）+ `requirements/interaction-authority/tests/chat-params-hook.test.mjs::CHAT_PARAMS_parented_session_requires_provider_model_binding` + `CHAT_PARAMS_acceptance_establishes_binding_without_rewriting_host_model` + `CHAT_PARAMS_uses_the_resolved_provider_model_id_not_the_mutated_user_message_model` + `CHAT_PARAMS_accepts_the_real_provider_model_shape_with_message_variant` + `CHAT_PARAMS_agentless_root_does_not_invent_binding`（执行身份不得从 journal/message 碎片拼装；provider-facing `Model.id` 与 message variant 共同形成实际 observation；「无 model 字段」断言归 dispatch-protocol 的 DP-010） | NEW | `node --test requirements/interaction-authority/tests/authority-root.test.mjs` / `node --test requirements/interaction-authority/tests/chat-params-hook.test.mjs` |
| R15 | INTERACTION-AUTHORITY-010/012（idle permit + exact-terminal durable occasion） | `requirements/interaction-authority/tests/idle-continuation-authority.test.mjs::HOST_004_manager_idle_claim_is_exactly_once_per_terminal` + `HOST_004_process_dedupe_key_is_per_terminal`（permit + terminal claim 防同一次 idle race；新的 ProviderRun 即使 condition 不变也必须获得新的 automatic encouragement） | REUSE | `node --test requirements/interaction-authority/tests/idle-continuation-authority.test.mjs` |
| R16 | INTERACTION-AUTHORITY-004（repair=continuation 判定） | `requirements/interaction-authority/tests/completed-turn-classifier.test.mjs::RECON_repair_role_table_respects_host_tool_work`（TurnOutcome 分类归 host-boundary；「repair 是 continuation 而非 fallback/新 root」归本包） | REUSE | `node --test requirements/interaction-authority/tests/completed-turn-classifier.test.mjs` |
| R17 | INTERACTION-AUTHORITY-008/009（来源解析 family 补充） | `requirements/interaction-authority/tests/continuation-origin.test.mjs::IA_008_resolution_order_is_accepted_then_claimed_then_compaction_then_root` + `IA_008_accepted_continuation_outranks_compaction`（accepted 优先于 compaction 的顺序语义）+ `IA_009_pure_resolution_never_infers_human_root` | NEW | `node --test requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| R18 | INTERACTION-AUTHORITY-018 | `requirements/interaction-authority/tests/logical-run-close.test.mjs::IA_018_human_root_closure_clears_active_run_and_retains_history` + `IA_018_agent_owner_root_is_not_closed_by_life_completion` | NEW | `node --test requirements/interaction-authority/tests/logical-run-close.test.mjs` |

统计：19 行落点；HumanRoot terminal closure 由 `LifeCompleted` fold 派生，无第二 durable close fact；AgentOwnerRoot 反例单独锁定。

### authority.test.mjs 断言级 SPLIT（PROOF-MAP mandatory split #1）已执行（Wave 2a）：锚点并入本包 authority-root / continuation-origin

双 owner 文件：**REUSE + SPLIT@cutover**。cutover 时按下列清单物理拆成两文件后删除原文件；
拆出的 interaction-authority 部分与 `requirements/interaction-authority/tests/` 现有 NEW 文件合并去重。

| authority.test.mjs 测试 | 归属 |
|---|---|
| `PROMPT_001_authority_root_id_is_reachable_only_by_promoting_a_physical_message` | interaction-authority |
| `PROMPT_001_a_transport_receipt_can_never_become_an_authority_root` | 拆分：`isAdmissionShaped` 形态断言 → dispatch-protocol；crossing 缺席断言 → interaction-authority |
| `PROMPT_002_authority_root_profile_cannot_express_a_model` | dispatch-protocol（Model=None 无字段） |
| `PROMPT_002_root_derives_peer_role_and_tier_from_the_selected_agent_alone` | interaction-authority |
| `AGENT_004_005_bare_legacy_agent_names_are_refused` | interaction-authority（typed 拒绝）；精确 legacy 名单级断言 → 迁移 ratchet（HOW），cutover 时收敛为「显式 managed agent 才可成 root」 |
| `PROMPT_002_a_new_root_replaces_the_profile_and_clears_everything_run_scoped` | interaction-authority |
| `PROMPT_003_a_continuation_never_replaces_the_authority_root` | interaction-authority |
| `PROMPT_003_every_continuation_kind_is_representable_and_none_is_a_root` | interaction-authority |
| `PROMPT_005_submit_records_the_receipt_without_resolving_the_claim` | dispatch-protocol |
| `PROMPT_005_abandon_removes_the_claim_and_leaves_the_active_run_alone` | dispatch-protocol（claim 生命周期）；「active run 不变」断言 → interaction-authority |
| `PROMPT_011_stable_logical_run_id_is_a_function_of_runtime_session_and_root` | interaction-authority（root 建立 run 的身份派生） |
| `PROMPT_011_claim_scope_names_exactly_session_run_origin_and_payload` | dispatch-protocol |
| `PROMPT_011_claim_sequence_advances_on_registration_not_on_resolution` | dispatch-protocol |
| `PROMPT_011_prompt_key_is_deterministic_and_moves_with_every_component` | dispatch-protocol |
| `PROMPT_011_recovery_budget_is_folded_from_plugin_starts_not_written` | dispatch-protocol（budget 派生；精确常数 = HOW） |
| `FALLBACK_008_one_terminal_provider_run_earns_exactly_one_repair` | interaction-authority 的 Blogger-request + terminal-scoped primitive（exact-one protocol 以 BloggerRequestId 隔离连续请求，并以 terminal 判 same-terminal re-entry）；ordinary generic repair 已收敛为 LogicalRun+repair-family 一次，「invalid terminal 不计入 A/B 失败推进」半边仍归 provider-attempt-recovery |
| `PROMPT_009_resolution_order_is_accepted_then_claimed_then_compaction_then_root` | interaction-authority |
| `PROMPT_004_009_an_accepted_id_outranks_host_compaction` | interaction-authority |
| `PROMPT_004_a_human_root_is_never_inferred_by_a_pure_function` | interaction-authority |
| `PROMPT_004_ingress_does_not_promote_UnknownOrigin_to_HumanRoot_while_run_active` | interaction-authority |
| `PROMPT_009_accepting_an_authority_root_claim_does_not_enter_the_continuation_map` | interaction-authority |
| `PROMPT_002_agent_owner_root_claims_reject_bare_legacy_names_too` | interaction-authority |

### semantic anchors

`scripts/checks/semantic-anchors.mjs` 当前只含 Role cognition / tool description / office capability
三类 catalog（owner = `cognitive-environment` / `action-affordance` / `office-capability`）。
**interaction-authority 拥有 0 个 semantic anchor id**：authority 判定由类型（`PromptOrigin`、
`AuthorityExecutionProfile`）与行为测试承载，不经 prompt 正则。

### GAP 与 cutover 待办

- 无 GAP：17 条命题全部有落点（NEW 或 REUSE 锚点）。
- SPLIT@cutover：`authority.test.mjs` 按上表物理拆分并删除原文件；`assistance-host.test.mjs`、
  `join-guard.test.mjs`（host+execution）、`idle-continuation-authority.test.mjs`、
  `completed-turn-classifier.test.mjs`、`join-v2-mailbox.test.mjs` 的跨 owner 断言在文件级拆分时
  收敛到各自 owner（见各 REUSE 行的边界注记）。
- 迁移 ratchet 退休：`student-teacher-absence.mjs`、legacy 名单级断言（AGENT-004）已随新世界基线稳定删除（CLN-Z；PROOF-MAP DELETE 清单）。
