# delegation — 实现模型与约束

非 normative。WHAT 是唯一权威；本文件解释实现模型、物理落点与历史裁决。

## 实现模型

### 委托面：fork / commission / inspect / establish-behavior / repair-behavior

| 面 | owner 角色 | 语义 | 物理实现 |
|----|-----------|------|---------|
| `fork` | Manager | mission 内 witness；Byname 承接 charge；可选 attachment / tool-call estimate | `Session/ForkRuntime.fs`（ChildRun map）、`Domain/ForkChildPayload.fs`（首 prompt） |
| `commission` | Orchestrator | 独立集成之路；calling 在场=新路，缺省=续做；可选 tool-call estimate | `Application/Orchestration/*.fs`、`Infrastructure/Git/WorktreeResource.fs` |
| `inspect` / `establish-behavior` / `repair-behavior` | SyncDelegate callers | 同步委托；普通 completion → bounded WorkRecord；可选 tool-call estimate | `Session/{SyncDelegateRuntime,SyncDelegateWorkflow,SyncDelegateWait,SyncDelegateCallStore}.fs` |

### LWR 跨边界 wire：方向不对称

```text
父 → 子（fork child 首 prompt）  commissioner_record / attached_work_record = '''…'''
子 → 父（join completed）         SyntheticToml.comment(LWR)  →  # Chronicle / # Recent work …
```

LWR 段标题字面始终是纯文本（`LifecycleWorkRecord.render`）；`# ` 只在子→父 join 的
`JoinResultRenderer` 经 `SyntheticToml.comment` 注入。禁止把父→子的「字段信封」裁决
反向套到 join，也禁止把 join 的 `# LWR` 裁决反向套到 fork child payload。

### fork child 首 prompt：CommissionerRecord / Attachment（DELEG-019 / DELEG-021）

`ForkChildPayload.render`（`src/Wanxiangshu/Execution/Delegation/Fork/Payload.fs`）：

- **instructions**：Assignment + Base +（可选）CommissionerRecord *instruction* +（可选）Attachment
  *instruction* +（可选）Requirements *instruction*。
- **body**：可选 `content` → **`commissioner_record = <renderString LWR>`** →
  **`attached_work_record = <renderString LWR>`** → `[[root_requirement]]` table array。

父→子路径上，LWR 必须是 TOML 数据字段的值，**不得** `record.Split('\n')` 后再塞进 `instructions`
（那会经 `SyntheticToml.document` → `comment` 变成 `# Opening` / `# Chronicle`），也不得当裸 prose dump。
已退役名 `parent_work_record` 不得再出现。

### join completed：子→父 `# LWR`（DELEG-013 / EXEC-004）

`JoinResultRenderer.renderAgentCompleted`（`Fork/OpenCode/JoinResultRenderer.fs`）：

- **instructions**：自然语言后果（「`<byname>` has returned.」）。
- **body**：非空 WorkRecord → **`SyntheticToml.comment payload.WorkRecord`**（entry-local `# LWR`）。
- **禁止** `work_record = …` / 其它 TOML 字段包裹子→父 LWR；空 LWR 只发 framing，不发空 comment。

（NEEDHELP advice 的 `consultation_record` 是另一 projection surface，见 DELEG-018；
不改变 join 的 `# LWR` 合同。）

### fork attachment（DELEG-021）

`attach` 在 parent `HandleProjection` 以 Byname 定位 sibling/retired child，再调用唯一
`LifecycleWorkRecord(includeOpening=true)` projector。`ForkChildPayload` 只接收 `Attachment: string option`，
渲染为 `attached_work_record` 字段（位于 `commissioner_record` 之后、`[[root_requirement]]` 之前）；
不解析 LWR、不复制 Journal projection。new fork 与 idle reuse 可物化；已有 ActiveLogicalRun 的 busy reuse 不物化，只返回自然语言 deferred 说明。若 Detached 首 prompt 已交给 Host 但 `chat.message` 尚未 physical-accept，该 person 只有 pending run、没有可证明的 ActiveLogicalRun：此时直接返回“当前还不能接新 charge”，不等待 acceptance、不发送 busy nudge、也不物化 attachment。unknown/self 在任何 send 前拒绝。

### delegated tool estimate（DELEG-022）

持久事实只有 `DelegatedToolEstimateReplaced(SessionId, ExpectedToolCalls)` 与
`DelegatedToolCallObserved(SessionId, ToolCallId)`。`DelegatedToolEstimateProjection` 纯 fold：replace →
`Remaining=X, CountedCalls=∅`；observe → duplicate/zero no-op，否则 `Remaining-1` 并记录 call id。
`CountedCalls` 最大长度 ≤ 本次 X；remaining=0 后 Host 不再 append observation，因此不会随 session 生命周期
无限增长。projection 挂在 `SessionAgentProjection`，按 SessionId O(1) 读取；禁止从 XTrace/transcript 派生。

estimate 在 delegated prompt/nudge 物理发送前 durable append：fork/reuse 在 child session 已解析后；
commission 在 Manager session 已创建/解析后；SyncDelegate 在 `GetOrCreate` 后、`SendPrompt` 前。省略参数不
append replace fact。SyncDelegate semantic batch 对全部显式值求和；无显式值 = None。

全局 `tool.execute.before` 是真实 tool invocation 的唯一 observation seam：有 session + callID 且该 session
存在 estimate/remaining>0 时 append `DelegatedToolCallObserved`。synthetic HOST-013 pair 不经过 execute hook，
天然不计数。该 hook 只记事实，不决定工具是否继续执行。

### SyncDelegate 核心类型（`Kernel/SyncDelegate.fs`）

- `SyncDelegateRole = Inspector | Coder`；`DedicatedDelegateKey = { Scope: ReuseScopeId; Role }`。
- `SyncDelegateBatch = { ProviderRun; CallOrder: ToolCallId list; CurrentCall }`——同一 ProviderRun 的
  同 role calls 按 Host tool-call 顺序构成一个语义 batch（DELEG-008）。OpenCode 边界同时保留两份
  Host 观察：`message.part.updated` 的本地 ordered projection 与 `ISessionSnapshotPort` 的 message snapshot；
  两者都是同一 call list 的暂时前缀，按前缀兼容关系选择更完整者，禁止把任一滞后的单源前缀直接封口。
- `SyncDelegateInvocationResult = WorkRecord of string | MergedInto of ToolCallId`——canonical 得正文、
  siblings 得引用（DELEG-012）。
- `tierForOwner = identity`（fast→fast、deep→deep）；`agentNameFor role tier` 生成 `fast-inspector` 等
  墙内名（DELEG-010）。
- `delegateRoleToAttachment`：`Inspector → SyncInspector`、`Coder → SyncCoder`（HOST-008 的
  Work+Attached 登记；AttachmentKind 归属 `managed-session-lifecycle`/`session-ontology`）。

### 同步委托 CE 单栈（历史 how/execution EXEC-026/031）

```text
eventPrefix = observedHostToolParts(providerRun, role) // ordered, ToolCallId de-duped
snapshotPrefix = syncCallsInHostMessage(providerRun, role)
expected = longerCompatiblePrefix(eventPrefix, snapshotPrefix)
admit current invocation against expected
when all expected members present:
  reserve (immediateCallerReuseScope, role)
  delegate = attachedSessions.GetOrCreate(ownerReuseScopeId, role)
  prepared = members |> map prepareProviderPrompt        // provider order
  request = concat charges / concat prepared prompts
  Send(delegate, request)
  completion = await ordinary Assistant Completion
  workRecord = materializeBoundedWorkRecord(InvocationStart..InvocationEnd, includeOpening=false)
  canonical = expected[0] → workRecord；siblings → merged-reference
```

`message.updated finish` 不参与 batch 封口：真实 Host 在 tool execute 返回后才发布该 finish；等待它会让
sync tool 自己阻塞自己的完成。Long Stroke 的 streamed 3×`inspect` 回归固定此边界。

### Charge / ProviderPrompt 分离

- `SyncDelegatePromptRequest = { Charge; ProviderPrompt }`（`Domain/SyncDelegatePrompt.fs`）。
- 无 warm-start 时两者字节相同；有 AGENT-032 keywords 时只 enrich `ProviderPrompt`（DELEG-019）。
- `SyncDelegatePrompt.IdleNudge = "delegation/sync-idle"`：SyncDelegate turn 失败未完成时的 idle nudge。

### NEEDHELP consultation 委托（AGENT-031 / HOST-027）

`deep-*` 命中 `[NEEDHELP]` → assistance abort（不写 FallbackCursor、不进 ProviderFailure）→ 等
`IdleRevisit` transport fence → 创建真实 `deep-inquiry` consultation child（freeze frontier →
`CommissionerRecord` = `LifecycleWorkRecord(includeOpening=true)`）→ 完成 → `includeOpening=false`
WorkRecord → typed `NeedHelpAdvice` continuation 返回原 binding。single-flight + 有限额度（资源策略，
数值不向 provider 暴露）。sentinel 在 XTrace capture 前剥离。

### 委托失败与恢复时序契约（DELEG-023）

- `SyncDelegateRuntime.HandleTurn` 仅处理 `TurnCompleted`（捕获 terminal + 物化 bounded WorkRecord + 完成 `call.Answer`）。
- `TurnFailed` / `TurnInProgress` / `TurnNeedsContinuation` 保持 child-local，返回 `false` 且不弹出调用，放行至 `OrdinaryTurnWorkflow` 触发 AABB / ProviderRetry continuation。
- `SyncDelegateWorkflow.invoke` 通过 `SubscribeTerminal` 监听终端结果：仅在 `TerminalOutcome.Failed`（恢复预算耗尽）或 `TerminalOutcome.Aborted` 时才向调用方返回失败。
- `AssistanceHost.handleConsultationTurn` 对 `TurnFailed` 返回 `NotAssistance`，放行至普通恢复流程；仅在终端失败时通过 `SubscribeTerminal` 交付失败建议。

### Join 有界批次（`Session/CompletionMailbox.fs`、`Session/ForkRuntime.fs`）

- `WaitForSignal(interrupt)` / `DrainAgentWakes`（agent 路径仅 Pulse，无 payload）/ `DrainPtyCompletions`。
- 批次上限 `MaxJoinBatch`；稳定排序；逐项 CAS；中断前再 drain（EXEC-018/019）。

## 物理落点（CURRENT EVIDENCE）

- 类型：`Kernel/SyncDelegate.fs`、`Domain/{SyncDelegatePrompt,ForkChildPayload}.fs`。
- Wiring：`Session/{SyncDelegateRuntime,SyncDelegateWorkflow,SyncDelegateWait,SyncDelegateCallStore,ForkRuntime}.fs`。
- Resource：`resources/provider/tool/{fork,commission,inspect,sync-delegate}/`、`resources/provider/delegation/**`。
- Tests：包内 `tests/fork-child-payload.test.mjs`；REUSE 清单见 HOW.md。

## 边界与弃权（非 normative）

- **GARBAGE——Student/Teacher/`return`/Meditator**：`Role.Student|Teacher`、Learn/Compile/SKILL、
  `StudentQaStore`、独立 `return` 工具、`Returned → Completion` 双 await、`completion_text` /
  `SyncDelegateReturnCompletion` magic、`tdd`、`list` DTO、legacy `meditator` 身份：已 clean-break 删除，
  不进入未来 WHAT（EXEC-027 空缺、AGENT-020/022 空缺、历史 how/execution「已删除算法面」、
  CHANGES-AUDIT：universal.md / ce-student-teacher-collapse.md 的 GARBAGE 裁决）。
- **GARBAGE——fork-manager 工具面**：旧 `fork-manager` / `list` / `verdict` / `blog` / `executor`(工具) /
  `fork-pty` 名：GrandRewrite clean-break，无 alias（历史 how/execution 条款）。
- **HOW——具体数值**：`MaxJoinBatch=32`、`DevOpsJoinTimeoutMs=10_000`、`ReduceFanIn=8`、
  `AwaitAgentTimeoutMs=600_000`：有界性才是 WHAT。
- **HOW——工具名**：`fork`/`commission`/`inspect`/`establish-behavior`/`repair-behavior` 是当前选择
  （DELEG-020）；改名不动 WHAT。
- **HOW——Dedicated reuse 机制**：`(OwnerReuseScopeId, role) → at most one live Session` 的复用实现、
  retire/dispose 时序 → `managed-session-lifecycle` 拥有；本包只拥有语义 batch / serialization /
  canonical 分型。
- **不复制** `work-record`（WorkRecord 三段标题、Opening 捕获、includeOpening 语义）、
  `participant-horizon`（准入 filter 全法则）、`interaction-authority`（Esc/ingress authority 语义）的命题。

## 验证与测试落点

规则：每条 WHAT 命题恰好一行落点。`MOVE` = 已物理移入本包 `tests/`；`REUSE` = 留在原处，
cutover 时按 `SPLIT@cutover` 拆分；`NEW` = 新写。运行命令：`node --test <file>`。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|---------|
| DELEG-001 委托=charge+office+owner+returned consequence | `scripts/checks/semantic-anchors.mjs` `ROLE_SEMANTIC_ANCHORS.manager`（`entrust-by-consequence`/`choose-by-return`/`no-omnipotent-charge` 双语言命中）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`manager_role_law_entrusts_by_consequence_not_persona`——真实 manager Role Law 双语文档锚点命中） | REUSE + NEW | `node scripts/checks/semantic-anchors.mjs`; `node --test requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-002 calling 只差 persona/depth 不差 authority | anchor `persona-not-authority`（fork 组，双语言命中）；`requirements/participant-identity/tests/catalog.test.mjs`（`participant-identity` 交叉，persona/binding 分离）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`calling_names_differ_in_persona_depth_not_authority`——真实 fork description 双语文档锚点命中） | REUSE + NEW | `node scripts/checks/semantic-anchors.mjs`; `node --test requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-003 独立 road vs same-road continuation | `requirements/delegation/tests/fork-tool.test.mjs::FORK_road_with_calling_is_independent_and_omitted_calling_continues_byname` | NEW | `node --test requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-004 commission ≠ fork | anchor `office-not-witness`（fork 组）；`scripts/checks/tool-referential-integrity.mjs`（same-name-same-contract）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`commission_and_fork_are_distinct_contracts_not_witness`——office-not-witness 锚点 + fork/commission 角色门不同名） | REUSE + NEW | `node scripts/checks/tool-referential-integrity.mjs`; `node --test requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-005 机器拓扑不进委托面 | `requirements/delegation/tests/join-v2-wire.test.mjs::JOIN_V2_completed_agent_is_natural_language_plus_work_record`; `requirements/delegation/tests/join-v2-wire.test.mjs::JOIN_V2_rendered_wire_is_parseable_without_legacy_fields` | REUSE | `node --test requirements/delegation/tests/join-v2-wire.test.mjs` |
| DELEG-006 fork 成功仅 Byname；续做沿用 binding | `requirements/delegation/tests/fork-tool.test.mjs::FORK_continuation_reuses_bound_managed_agent_and_does_not_rebind_tier` | NEW | `node --test requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-007 SyncDelegate DAG 无环 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs`（嵌套 `DevOps→Coder→Inspector` 无 deadlock 场景；DAG 静态证明 gate `scripts/checks/dsl-ownership.mjs` 交叉）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`sync_delegate_edges_are_the_allowed_dag_only`——Roles 权限表决定委托边 Inquiry/Coder/DevOps→Inspector、DevOps→Coder，反向边拒绝，DFS 环检测证明无环） | REUSE + NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-008 batch 由 Host tool-call 集合决定 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_provider_tool_call_collection_preserves_role_order`、`SYNC_RUNTIME_unknown_role_and_outcome_fail_closed_at_every_entry` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-009 key=immediate caller ReuseScope；overlap fail closed | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_same_reuse_scope_serializes_distinct_provider_runs_but_distinct_scopes_are_independent` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-010 tier 确定性映射 | `requirements/delegation/tests/sync-delegate.test.mjs`（`EXEC_026_tierForOwner_is_identity_for_fast_and_deep`、`EXEC_026_agentNameFor_covers_fast_deep_times_inspector_coder`）；`requirements/delegation/tests/sync-delegate-runtime.test.mjs`（`EXEC_026_sync_delegate_fast_tier_nails_inspector_and_coder_agent_names`、`EXEC_026_sync_delegate_reuse_keeps_deep_inspector_when_owner_later_fast`） | REUSE | `node --test requirements/delegation/tests/sync-delegate.test.mjs requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-011 无 return 通道；ordinary completion 收口 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_ordinary_completion_settles_batch_without_return_channel` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-012 canonical 得 WorkRecord、siblings 引用 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_first_provider_call_receives_canonical_record_and_sibling_receives_reference` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-013 Join 有界批次/稳定排序/逐项 CAS；子→父 LWR=`# LWR` | `requirements/delegation/tests/join-v2-mailbox.test.mjs`（`EXEC_018_max_join_batch_is_32`、`EXEC_018_thirty_three_completions_split_across_two_drains`、`EXEC_018_drained_batch_has_unique_agent_ids`）；`requirements/delegation/tests/join-v2-wire.test.mjs`（硬锁：`EXEC_004_child_to_parent_lwr_is_hashed_comment_not_toml_field`、`EXEC_004_work_record_lines_are_hash_prefixed_including_malicious`、`EXEC_004_work_record_is_not_a_toml_field_when_lwr_present`——子→父禁止 `work_record =`，必须 `SyntheticToml.comment`；`JOIN_V2_malformed_role_run_or_kind_is_rejected_without_success_wire`） | REUSE | `node --test requirements/delegation/tests/join-v2-mailbox.test.mjs requirements/delegation/tests/join-v2-wire.test.mjs` |
| DELEG-014 commission 批量 join 同界 | `requirements/delegation/tests/join-v2-mailbox.test.mjs` `EXEC_019_verdict_mailbox_try_join_batch_preserves_publish_fifo`；`requirements/delegation/tests/join-v2-wire.test.mjs` `EXEC_019_orchestrator_batch_is_natural_language_only` | REUSE | `node --test requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-015 join 中断 = Interrupted 非 ForkError | `requirements/delegation/tests/join-v2-mailbox.test.mjs`（`EXEC_017_wait_for_signal_user_message_returns_user_message_arrived`、`EXEC_017_user_message_interrupt_does_not_cancel_mailbox`、`EXEC_017_join_attempt_old_signal_does_not_bleed_into_next_join`）；`requirements/delegation/tests/join-v2-wire.test.mjs` `EXEC_017_interrupted_wire_is_natural_language_not_error` | REUSE | `node --test requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-016 horizon pull-only snapshot | `requirements/delegation/tests/join-v2-wire.test.mjs` `EXEC_004_join_prefers_durable_byname_over_machine_agent_name`；horizon 无 watcher 断言见 `tests/unit/host/` horizon 面（cross-check） | REUSE | `node --test requirements/delegation/tests/join-v2-wire.test.mjs` |
| DELEG-017 返回只改认识不转 authority | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_work_record_is_evidence_and_does_not_transfer_authority` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-018 NEEDHELP consultation = 真实 child 委托 | `requirements/delegation/tests/assistance-host.test.mjs::ASSISTANCE_HOST_needhelp_escalation_keeps_the_same_authority_root`、`ASSISTANCE_HOST_needhelp_advice_is_not_a_provider_retry`（JS-native authority surface）；`requirements/host-boundary/tests/needhelp-sensor.test.mjs`（sentinel 识别，SPLIT：识别归 `interaction-authority`）；`requirements/verification-system/tests/e2e/entry.test.mjs`（真实 Long Stroke 的 consultation/advice 路由） | REUSE + NEW | `node --test requirements/delegation/tests/assistance-host.test.mjs requirements/host-boundary/tests/needhelp-sensor.test.mjs`；Long Stroke e2e |
| DELEG-019 fork child 首 prompt typed 载荷（父→子 LWR=TOML field） | `tests/fork-child-payload.test.mjs`（`FORK_CHILD_PAYLOAD_*` 全组；硬锁：`FORK_CHILD_PAYLOAD_commissioner_record_is_toml_data_field`、`FORK_CHILD_PAYLOAD_commissioner_lwr_is_toml_field_not_hashed_instructions`——父→子 LWR 在 `commissioner_record` 字段、禁止 `# Opening`/`# Chronicle`/裸 prose/`parent_work_record`）；`tests/handle-exe008-child-background.test.mjs` `EXEC_008_child_background_uses_latest_durable_snapshot`；方向互补见 DELEG-013 join `# LWR` 硬锁 | MOVE | `node --test requirements/delegation/tests/fork-child-payload.test.mjs requirements/delegation/tests/handle-exe008-child-background.test.mjs` |
| DELEG-020 语义不依赖工具名 | 命题结构本身（HOW.md「历史与弃权」）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`delegation_semantics_do_not_depend_on_current_tool_names`——HOW 声明工具名是当前选择、改名不动 WHAT，且五个当前名字真实存在） | NEW | `node --test requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-021 fork attachment | `tests/fork-attachment.test.mjs`（background / TOML field / blank / anti-assignment）；`tests/fork-tool.test.mjs` `DELEG_021_*`（unknown/self 前置拒绝、fresh LWR attachment；physical-accepted ActiveLogicalRun 才可 busy nudge，Detached 尚未 `chat.message` 时立即 deferred、不等待 acceptance、不物化 attachment） | NEW + REUSE | `node --test requirements/delegation/tests/fork-attachment.test.mjs requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-022 delegated expected tool calls | `tests/delegated-tool-estimate-surface.test.mjs`（pure replace/decrement/idempotence/saturation + no scan/mutable，JSON-shaped state）；`tests/delegated-tool-estimate-facts.test.mjs`（durable fold）；`tests/delegation-tool-contract.test.mjs`（五个 surface + no maxSteps）；`tests/fork-tool.test.mjs` `DELEG_022_*`（invalid / replace / omitted retain）；`tests/sync-delegate-tools.test.mjs` `DELEG_022_*`（batch sum / reusable omission retain）；交叉 `requirements/guidance-delivery/tests/pair-calibration.test.mjs` | NEW + REUSE（FROZEN 2026-08-14，surface 迁移保持 oracle 语义） | 实现后不改 oracle |
| DELEG-023 委托失败仅在恢复路径耗尽后报告 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_transient_turn_failure_stays_child_local_until_exhausted` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |

### GAP

| GAP | 待建命题 | 缺口 | 状态 | 关闭条件 |
|---|---|---|---|---|
| GAP-011 | DELEG-021 fork attachment | 正式 WHAT + 独立 frozen oracle + production wiring 均已落地 | CLOSED | `tests/fork-attachment.test.mjs` + `tests/fork-tool.test.mjs`；按用户要求 frozen 后未执行；full build 被 unrelated Fission parse error 阻塞 |
| GAP-012 | DELEG-022 delegated expected tool calls | 正式 WHAT + 独立 frozen oracle + typed facts/fold/surfaces/HOST-013 wiring 均已落地；无 transcript/XTrace scan、业务 mutable counter、enforcement | CLOSED | `tests/delegated-tool-estimate*.test.mjs` + `tests/delegation-tool-contract.test.mjs` + fork/SyncDelegate reuse/batch oracle；按用户要求 frozen 后未执行；相关静态 gates 绿，full build 被 unrelated Fission parse error 阻塞 |

### 移动文件清单

| 源 | 目标 | 结果 |
|----|------|------|
| `requirements/delegation/tests/fork-child-payload.test.mjs` | `requirements/delegation/tests/fork-child-payload.test.mjs` | `node --test` 绿（21/21；import 深度已适配） |

### SPLIT@cutover 清单（REUSE 文件拆分计划）

- `requirements/delegation/tests/sync-delegate-runtime.test.mjs`：DELEG-008..012 断言归本包；create/reuse/abort/retire/
  级联断言 → `managed-session-lifecycle`；attachment/SessionOwnership 断言 → `session-ontology`。
- `requirements/delegation/tests/sync-delegate-ce-collapse.test.mjs`：EXEC-031 单栈断言归本包；dispose/cancel scope →
  `managed-session-lifecycle`。
- `requirements/delegation/tests/fork-tool.test.mjs`：FORK_* 语义断言归本包；tool spec/registry 断言 →
  `capability-enforcement`；office 后果表 → `office-capability`。
- `requirements/delegation/tests/sync-delegate-tools.test.mjs`：inspect/establish/repair 合同断言归本包；warm-start
  prepare 断言 → `knowledge-reuse`/`repository-investigation`。
- `tests/unit/execution/join-*.test.mjs`：join batch/wire 归本包；`join-aborted-not-terminal` →
  `effect-accounting`；`timer-port`/`devops-join-timeout` → `time-capability`；`handle*` →
  `managed-session-lifecycle`；`join-recovery-*` → `crash-reconciliation`；`join-guard` →
  `interaction-authority`/`finality`；join wire 的 WorkRecord 物化 → `work-record`。
- `tests/unit/host/{needhelp-sensor,assistance-host}.test.mjs`：consultation 委托语义归本包；sentinel 识别/
  assistance abort 分型 → `interaction-authority`；advice prompt 渲染 → `provider-projection`/`prefix-stability`。
- `tests/unit/orchestrator/{host,runtime}.test.mjs`：commission 委托面断言归本包；job/rebase/publish/
  CAS/recovery 断言 → `change-integration`；review barrier → `review-assurance`。

### 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.manager`：`entrust-by-consequence`、`choose-by-return`、`no-omnipotent-charge`、
`returned-record`。
`ROLE_SEMANTIC_ANCHORS.orchestrator`：`owns-roads`、`same-road-continuation`、`independent-destination`。
`TOOL_DESCRIPTION_ANCHORS.fork`：`office-not-witness`、`create-and-continue`（`persona-not-authority` 的
personhood 部分由 `participant-identity` 交叉声明）。
`TOOL_DESCRIPTION_ANCHORS.inspect`：`repository-fact`、`causal-readonly`、`no-code-changes`、
`no-behavioral-execution`、`no-implement-or-repair`。
`TOOL_DESCRIPTION_ANCHORS.commission`：`independent-road`、`not-lifecycle-stage`。
`TOOL_DESCRIPTION_ANCHORS.establish-behavior`：`coder-writes-source`、`not-execution-evidence`。
`TOOL_DESCRIPTION_ANCHORS.repair-behavior`：`meaning-decided`、`not-passing-proof`。
