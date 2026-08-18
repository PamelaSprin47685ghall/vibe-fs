# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可重写而不改 WHAT。

## 类型与函数地图（dispatch-protocol）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001/002 | `Application/Prompting/PromptDispatcher.fs`（`Runtime`：`RegisterAuthority`、`AcceptHumanRoot`、`Abandon`、`AcceptContinuation`、`AcceptAgentOwnerRoot`）+ `Application/Prompting/PromptDispatcherSend.fs`（`RecordSendOutcome`） | 唯一写入口；四态事实由 `Runtime.Persist` 落 `PluginPromptClaimed/Submitted/PhysicalAccepted/Abandoned` |
| 003/004 | `Domain/PromptAuthority.fs` → `PromptClaim`（`Receipt: TransportReceipt option`）；`PromptAuthorityRun.submitClaim`；`Kernel/Identity.fs` → `TransportReceipt.isAdmissionShaped` | receipt 只记不解决；admission 形态可判别 |
| 005/006 | `Domain/PromptAuthority.fs` → `claimScopeDigest`、`nextClaimSequence`、`derivePromptKey`；`PromptDispatcherSend.deriveKey` | 确定性幂等身份；序列在注册时消费 |
| 007/008 | `Interaction/Dispatch/Recovery.fs` → detached `reconcile` / `reconcileClaim` / `findPhysical`（tail window 内 `role=user` + PromptKey metadata 匹配） | Proven / StillPending(hasReceipt) / Unreadable；普通 plugin lifecycle 不调用 |
| 009 | `PromptDispatcher.AwaitMode`（Await/Detached）+ `OpenCodePort.SdkClientPort/HttpPort.SendPrompt` async enqueue observer | Detached 在 claim + local invocation receipt 后调用 `prompt_async` 即返回，不等 model slot / Host Promise / PhysicalAccepted；异步 rejection → 先结算调用方拥有的 pending effect（若有），再 process fatal + 不重发。需要在当前协议分支判断 transport refusal 的 guard/repair/sync interaction 使用 Await；Await 只等 `SendPrompt` transport result，不等 provider run/slot/terminal |
| 010 | `Interaction/Authority/Model.fs` → `AuthorityExecutionProfile` 无 model 字段；`Interaction/Dispatch/Send.fs` 发送 options 恒 `Model = None`；`Sessions.fs` send 栈不 acquire model | Root/dispatch 均不能选 model；`chat.message` execution admission 才 acquire |
| 011 | `PromptClaim.ClaimedAtRuntimeStartCount` / `RuntimeStartCount` 仅保留历史兼容与审计；restart-count abandon policy 已退役 | 重启次数不再产生业务 terminal |

事实折叠：`PromptAuthorityLedger.foldPromptClaimed/Submitted/PhysicalAccepted/Abandoned` 是
`PromptAuthorityProjection` 的纯 fold。`PromptDispatcher.Runtime` 无可变 authority 状态——每次读
走 fold（`ProjectionFor`），journal 是唯一 writer。

## 关键机制

### 四态事实链（PROMPT-005）

```text
Claimed → Submitted → PhysicalAccepted
Claimed → Abandoned
Claimed → Submitted               （若 crash 后无法证明，则保持 pending；不由重启自动补 terminal）
```

`RecordSendOutcome` 对 `AdmittedWithReceipt` 只写 Submitted（claim 保持 pending，等 `chat.message`）；`AdmittedWithPhysicalMessage` 写 Submitted 再立刻 Accept；`Retryable/Fatal` 写 Abandoned(SendFailed)；`AcceptanceUnknown` **什么都不写**——保持 pending让恢复去找，abandon 会许可重发。

OpenCode `prompt_async` adapter 的 Detached receipt 是**本地 enqueue invocation receipt**，不是 HTTP/SDK Promise 已 settle 的证明。adapter 同步调用 `promptAsync` 后立即返回 receipt，同时旁路观察其 Promise；若 Promise 后来 rejection，调用方已无法安全判断是否部分落地，因此直接 `FatalProcess/Diagnostic.fatal`，保留 pending claim，绝不重发。managed model capacity 完全不在该发送调用栈：物理 user message 到达 `chat.message` 后才进入 scheduler demand。

### PromptKey 组成（PROMPT-011）

```text
PromptKey = digest(SessionId, LogicalRunId, AuthorityRootUserMessageId,
                   Origin, EffectiveAgent, PayloadDigest, ClaimSequence)
```

`ClaimSequence` 以 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 为 scope（`claimScopeDigest`
是 `\u001f` join 串，非 hash，测试可读组件），在 claim 注册时消费——abandon 后同 payload 再发
得到新序号新 key，不会撞同一个幂等锚。Key 进 Host metadata（`PromptMetadataCodec`），不占对话字节。

### 证据核对库（PROMPT-011）

`reconcile` 现在是 detached library，不在 plugin init、普通 turn/tool 或 teardown 自动运行。显式调用时只读目标 Session 尾部 `RecoveryTailWindow` 条，找 `role=user` 且 metadata PromptKey 完全一致的消息：

```text
找到 → 按 claim.Origin 补写 PhysicalAccepted（显式证明）
未找到 → StillPending（绝不重发、绝不按 restart count 自动 abandon）
读失败 → Unreadable（不改 claim）
```

`RuntimeStartCount` 与 `ClaimedAtRuntimeStartCount` 可继续作为历史审计字段，但不再是 recovery budget。进程重启本身没有权力替旧 tool 写 terminal。

## 约束

- 无第二 writer：`Abandon` 是唯一 abandon 写点（recovery 与 send-fail 共用），禁止在
  `RecordSendOutcome` 内另造事实形状。
- 证据核对只证明或保持 pending，从不 resend、从不因 restart count abandon；`reconcile` 没有发送端口。
- Detached async enqueue 的 eventual rejection 是 acceptance-unknown invariant，当前进程 fatal；不得降级成 Retryable 后自动第二次发送。
- `SendPrompt` / fork / repair 不 acquire managed model lease；capacity authority 只在 execution-model-routing 的 `chat.message` admission。
- 普通 plugin lifecycle 不接 `RecoveryGate`/reconcile；显式 session resume 由 CRASH-018 `/continue` 承担。

## 历史与弃权

- **精确常数**：`RecoveryTailWindow=50` 保留为物理证据读取上界；`RecoveryAttemptBudget` 行为已退役，restart count 不再驱动 abandon。
- **`postPromptFireAndForget` 旁路**：GARBAGE——已被 `AwaitMode.Detached` 取代，禁止重建。
- **PROMPT-012 残留**：Student/Teacher 已删（GARBAGE）；「插件 user-shaped message 一律经
  PROMPT-005，不得直接 `prompt_async`」保留为 DISPATCH-PROTOCOL-011（corrective §3.4 closed-world
  producer invariant 的发送侧）。
- **自动恢复执行时序**：已退役；constructor/post-init/普通 hook/tool 均不是 recovery trigger。
- **claim 恢复的存储级重放顺序**：`loadJournalEnvelopes` 按 `compareSortKey`（同 RuntimeId 按
  LocalSeq，异 RuntimeId 按 ObservedAt）排序后整体重放，`ClaimedAtRuntimeStartCount` 在重放位置
  重新盖章。测试用远未来 boot 日期固定顺序（见 recovery 测试头注释）。

## 不归我（DOES NOT OWN）

- interaction 是否有 authority
- generic effect-accounting law（Requested/Accepted 分型）
- provider representation、attempt recovery
- `RecoveryTailWindow=50` 精确物理证据窗口（HOW）；restart-count recovery budget 已退役

## 验证与测试落点

### 运行方式

```bash
node --test requirements/dispatch-protocol/tests/fire-and-forget.test.mjs   # MOVE（原 requirements/dispatch-protocol/tests/fire-and-forget.test.mjs）
node --test requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs  # NEW
node --test requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs  # NEW
## 全量：node requirements/verification-system/tests/run.mjs（自动包含 requirements/**/tests/*.test.mjs）
```

### 命题 → 落点

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| R1 | DISPATCH-PROTOCOL-002/003/004 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_002_submit_records_the_receipt_without_resolving_the_claim` + `DP_002_abandon_removes_the_claim_and_leaves_the_active_run_alone` + `DP_002_claim_records_payload_digest_and_effective_agent` + `DP_003_receipt_shape_distinguishes_admission_from_physical_identity`；`requirements/dispatch-protocol/tests/send-format.test.mjs::HOST_004_stale_idle_repair_is_abandoned_at_the_final_physical_send_boundary`（claim 已 durable 但 stale idle permit 在最终物理边界变成 Superseded：Host SendPrompt=0、PendingClaims=0、ClaimSequence 保留） | NEW | `node --test requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs requirements/dispatch-protocol/tests/send-format.test.mjs` |
| R2 | DISPATCH-PROTOCOL-005/006/010 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_005_prompt_key_is_deterministic_and_moves_with_every_component` + `DP_005_claim_scope_names_exactly_session_run_origin_and_payload` + `DP_010_authority_root_profile_cannot_express_a_model` | NEW | 同上 |
| R3 | DISPATCH-PROTOCOL-006/007 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_006_claim_sequence_advances_on_registration_not_on_resolution` + `DP_006_abandon_keeps_the_claim_sequence_consumed` + `DP_007_runtime_start_stamp_is_audit_only_not_restart_recovery_authority` | NEW | 同上 |
| R4 | DISPATCH-PROTOCOL-007/008 | `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::DP_008_unproven_outcome_stays_pending_never_resends`（StillPending + 绝不重发）+ `DP_004_physical_acceptance_is_proven_only_by_physical_message`（Proven）+ `DP_007_restarts_never_auto_abandon_an_unresolved_broken_tool` | NEW | `node --test requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs` |
| R5 | DISPATCH-PROTOCOL-009 | `requirements/dispatch-protocol/tests/fire-and-forget.test.mjs`：`PROMPT_007_detached_claims_and_persists_without_physical_accepted`、`PROMPT_007_detached_returns_even_when_session_send_task_never_settles`（连 `ISessionHostPort.SendPrompt` Task settle 都不等）、`PROMPT_007_detached_sdk_physical_id_does_not_race_chat_message_acceptance`（SDK early id 不抢写 PhysicalAccepted）、`PROMPT_007_detached_continuation_same_claim_path`、`PROMPT_007_await_mode_constructors_exist`；cross-package REUSE `requirements/execution-model-routing/tests/open-code-port-routing.test.mjs`（EMR-004 sdk prompt async enqueue）+ `requirements/delegation/tests/fork-tool.test.mjs`（fork 不等 slot/run） | MOVE + REUSE | `node --test requirements/dispatch-protocol/tests/fire-and-forget.test.mjs requirements/execution-model-routing/tests/open-code-port-routing.test.mjs requirements/delegation/tests/fork-tool.test.mjs` |
| R6 | DISPATCH-PROTOCOL-001/011 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime`（全部 user-shaped send 成员都是 `PromptDispatcher.Runtime` 成员，无 FireAndForget 旁路）+ `requirements/dispatch-protocol/tests/send-format.test.mjs::PROMPT_006_send_payload_carries_prompt_key_metadata`（Metadata/PromptKey 锚；Model=None 半边见 R2/R10） | REUSE | `node --test requirements/dispatch-protocol/tests/send-format.test.mjs` |
| R7 | DISPATCH-PROTOCOL-007（claim 释放可重试） | `requirements/dispatch-protocol/tests/join-guard.test.mjs::JNGD_nudge_releases_the_key_when_send_fails_and_retries`（send 失败 → Abandon(SendFailed) 释放 key；JoinGuard continuation 语义归 interaction-authority） | REUSE | `node --test requirements/dispatch-protocol/tests/join-guard.test.mjs` |
| R8 | DISPATCH-PROTOCOL-002（historical stamp） | `requirements/dispatch-protocol/tests/runtime-start-watermark.test.mjs::PROMPT_011_RuntimeStarted_advances_a_workspace_watermark_not_every_session`（纯 fold：stamp 仍可审计；restart-budget API 不存在） | REUSE | `node --test requirements/dispatch-protocol/tests/runtime-start-watermark.test.mjs` |
| R9 | DISPATCH-PROTOCOL-001/005/006（claim 唯一写入口 + 幂等身份） | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_005_claim_scope_names_exactly_session_run_origin_and_payload` + `DP_005_prompt_key_is_deterministic_and_moves_with_every_component` + `DP_006_claim_sequence_advances_on_registration_not_on_resolution` + `DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime` | REUSE | `node --test requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs` |
| R10 | DISPATCH-PROTOCOL-007（restart 不产生 terminal） | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_007_runtime_start_stamp_is_audit_only_not_restart_recovery_authority` + `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::DP_007_restarts_never_auto_abandon_an_unresolved_broken_tool` | NEW | 两文件各自命令 |

统计由 verification runner/requirement trace 直接枚举；本表不维护独立 test-count 真理源。

### authority.test.mjs SPLIT（本包半边）已执行（Wave 2a）：锚点并入 claim-lifecycle / send-format

双 owner 文件：**REUSE + SPLIT@cutover**（完整 22 测试归属表见
[`interaction-authority/HOW.md`](../interaction-authority/HOW.md)）。本包 cutover 时接收：

| authority.test.mjs 测试 | cutover 动作 |
|---|---|
| `PROMPT_001_a_transport_receipt_can_never_become_an_authority_root` | 接收 `isAdmissionShaped` 形态断言（crossing 缺席断言留 interaction-authority） |
| `PROMPT_002_authority_root_profile_cannot_express_a_model` | 接收（Model=None 无字段） |
| `PROMPT_005_submit_records_the_receipt_without_resolving_the_claim` | 接收 |
| `PROMPT_005_abandon_removes_the_claim_and_leaves_the_active_run_alone` | 接收（「active run 不变」断言移交 interaction-authority） |
| `PROMPT_011_claim_scope_names_exactly_session_run_origin_and_payload` | 接收 |
| `PROMPT_011_claim_sequence_advances_on_registration_not_on_resolution` | 接收 |
| `PROMPT_011_prompt_key_is_deterministic_and_moves_with_every_component` | 接收 |
| `PROMPT_011_recovery_budget_is_folded_from_plugin_starts_not_written` | 接收（精确常数断言 → HOW/弃权，cutover 收敛为「有界预算」） |

### semantic anchors

`scripts/checks/semantic-anchors.mjs` 只有 Role/tool/office catalog（owner =
`cognitive-environment` / `action-affordance` / `office-capability`）。**dispatch-protocol 拥有
0 个 semantic anchor id**：发送语义由类型（`PromptClaim`、`PromptKey`）与行为测试承载。

### GAP 与 cutover 待办

- 无 GAP：11 条命题全部有落点。
- SPLIT@cutover：`authority.test.mjs` 按上表物理拆分并删除原文件；`send-format.test.mjs`、
  `host/join-guard.test.mjs`、`journal/envelope.test.mjs` 的跨 owner 断言在文件级拆分时收敛。
- 已知限制：存储级 `loadJournalEnvelopes` 按 ObservedAt 排序整体重放，导致恢复预算的
  restart-counting 依赖 envelope 顺序；包内 recovery 测试用远未来 boot 日期固定顺序
  （见测试头注释）。纯 fold 级 budget 语义由 `journal/envelope.test.mjs`（REUSE）独立覆盖。
