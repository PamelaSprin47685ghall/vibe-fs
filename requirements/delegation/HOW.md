# delegation — HOW

## 架构机制

### 编译边界证明

`delegation-compile-boundary.test.mjs` 复用 `readCompileShardInventory` 与 `buildSubsystemInventory`，按 compile shard 定位工程，验证 Delegation 分片及 persistence 投影的 subsystem 归属，不再读取 locality kind 或按旧 owner 文件名筛选工程。DELEG-028 的现行数量预算、60% ceiling 与增长 ratchet 原值保留；合同修订前不自行撤销。既有证明标题及登记保持不变。

基于 `14eab559a` 的验证：相关 Delegation 与 subsystem 测试 8/8 通过；临时移除 Contract 的 legacy 元数据、改用显式 subsystem/shard 声明仍通过，错误 subsystem 与无环 Process 依赖分别被归属和物理隔离断言拒绝。另一个 Process 引用反例先被 DAG 检查拒绝，不计作物理隔离证明；临时工程改动均恢复。这是静态证明读取路径的迁移，不是新独立 Fable 编译或业务行为等价证明，也不关闭 GAP-033。

### 委托接口分流与权能门禁

DELEG-020 约束：委托语义不依赖当前工具名字面值（`fork`、`commission`、`inspect`、`establish-behavior`、`repair-behavior`），改名不动 WHAT 语义定义。

系统定义三类委托途径，由角色权能门禁严格限制：

1. **异步见证委托（`fork` / `resume`）**：由 Manager 在使命内部调用。`fork` 必填 calling 创建具有独立 Byname 的新子执行者；`resume` 按 Byname 续做既有子执行者并复用其历史，传入 calling 被类型化拒绝。两者均支持附加历史背景（attachment）与建议性工具调用估算。
2. **独立道路委托（`commission`）**：由 Orchestrator 调用，负责开启或续做独立集成道路，支持多道路并行推进。
3. **同步委托（`inspect` / `establish-behavior` / `repair-behavior`）**：由业务角色在单轮内发起阻塞式子任务，经由 `SyncDelegate` 管道调度，完成取证或局部修复。

### durable 能力注入（DELEG-029）

2026-09-12：`m6-slice-boundary.test.mjs` 不再以退役 `legacyKind` 标签裁决 recovery 的引用。仍验证本域 port 的真实源码归属、禁止外部 journal／routing union，并沿 ProjectReference 检查传递源码不得带入 durable composition、物理 process 或具体 Host 容器。全去 kind 的正例通过；伪标 contract 的真实 journal、timing adapter 及同域 port 间接带入 journal 的反例均拒绝。该检查是本条 recovery 边界的证明，不补齐尚缺的 PTY 行为证明。

delegation 业务运行时（recovery、fork、fold、sync）不持有 durable store 的具体句柄，也不把领域 fact 包进外层 routing union。能力与包装位置分工如下：

- **capability port 由 delegation 拥有**：`Execution/Delegation/JournalPort.fs` 声明 `AgentJournalPort`，四个成员正是运行时需要的全部操作——`AppendExecutionFact: SessionId -> ExecutionFactCases -> Task<Result<unit, string>>`、`HandleProjection: SessionId -> AgentLinkageProjection`、`ReadBlob: BlobRef -> Task<Result<string, string>>`、`WriteBlob: string -> Task<Result<BlobRef * BlobDigest, string>>`。capability 由 composition 注入，构造时必填；`None` 只表示该调用方确实没有 durable store（既有降级语义），不是可选默认。
- **durable composition 实现并包装**：`Composition/Durable/AgentJournalPortAdapter.fs` 的 `AgentJournalPortAdapter.fromAgentJournal` 把 `AgentJournal` 适配成上述 port（`AgentFact.Execution cases` 包装、`JournalAppendFailure.describe` 展平失败文案、`WriteBlob` 的 receipt 投影为 `BlobRef * BlobDigest`）；`Execution/Delegation/Fact.fs` 的 `ExecutionFact.*`/`DelegationFact.*` 桥接由 `composition-durable-fact` 编译，因此「把 delegation case 包进 `AgentFact`」只发生在 durable composition。
- **边界适配**：`delegation-host-adapter`（`Fork/Host/{Agent,ChildDispatch,Join,RunLifecycle}.fs`）、`delegation-runtime-surface`（`Fork/Host/Restart.fs`、`Handle/JournalSurface.fs`）与 `execution-fission-opencode-host`（`Execution/Fission/OpenCode/Host.fs`）在持有 journal 的位置调用 `AgentJournalPortAdapter.fromAgentJournal`，把 port 传给运行时；每次操作绑定一次，不在 drain 循环内重复构造。
- **linkage 词汇由 delegation 自己拥有**：`Execution/Delegation/LinkageProjection.{fsi,fs}`（`AgentLinkageProjection`、`HandleRecord`、`HandleLifecycle`、`HandleCompletion`、`HandleTransitionRejection`、`module HandleProjection`）与 `Execution/Delegation/DelegatedToolEstimateProjection.{fsi,fs}` 由 delegation contract 分片 `delegation-linkage-projection` 编译，只引用 `identity`、`foundation-roles`、`delegation-sync-contract`。它们曾在 `composition-durable-projection`（persistence）内被编译，原因是 `2ee76f2dc` 为打断 fold 环把文件放进了 spine；现在同一个环由「delegation 自有纯 contract 分片 + spine 单向声明引用它」避免，spine 通过窄合同消费这些类型，不再自己编译它们。
- **直接引用与闭包现状**：`delegation-recovery-runtime`、`delegation-journal-port`、`delegation-ledger`、`delegation-host-adapter`、`execution-delegation-hostturnobservedsurface`、`execution-fission-opencode-host`、`execution-session-opencode-horizontool`、`git-integrationgate` 这 8 个分片已删除对 `composition-durable-projection` 的直接 ProjectReference；`delegation-recovery-runtime` 的跨 subsystem 引用现在全部是 contract kind。闭包只在 spine 是唯一路径的地方真正变小（`delegation-journal-port` 110 → 12 个 `.fs`），其余分片仍可经其他真实路径到达 spine（recovery 136 经 `delegation-fold`；fission host 经 `opencode-host-sessionexecutionbinding`；ledger 经 `persistence-journal-agentjournal`）。
- **delegation 拥有自己的 fold 状态与 decision（2026-09-12 落地）**：`Execution/Delegation/DelegationProjection.{fsi,fs}` 声明 `DelegationSessionState`（每会话的 `Handles`/`ToolEstimate`）、`DelegationProjectionChange`（`ReplaceSessionState`/`IndexChildHandle`/`MoveHandoffFrontier`/`TerminatedChildHandle` 四种纯变更）与闭合拒绝 `DelegationFoldRejection`，同样编译在 `delegation-linkage-projection`。`ExecutionFactFold.fold` 现在只收 `(SessionId -> DelegationSessionState option)` 与 `ExecutionFactCases`，`DelegationFactFold.fold` 再加 `(string -> int64 option)` 的 handoff frontier 读取，二者返回 `Result<DelegationProjectionChange list, DelegationFoldRejection>`：linkage、estimate、handoff frontier 与 child handle index 都是 delegation-owned 纯 fold/decision。
- **durable composition 是唯一装配点**：`Composition/Durable/DelegationProjectionBridge.{fsi,fs}`（分片 `composition-durable-fold`）把 `AgentProjectionSet` 切成上述切片、按顺序应用四种变更（`ReplaceSessionState` 与 `AgentProjection.update` 的「缺失即按 `emptySession` 创建」语义一致；`IndexChildHandle` 保持 PERSIST-008 的 `Map.add` 键索引；`TerminatedChildHandle` 是原 `closeCompletedChildAuthority` 的**原样搬迁**，把 child session 的 `PromptAuthority` best-effort 关闭），并把 `DelegationFoldRejection` 渲染成 `FoldRejection.reject (fact, message)`——诊断文本逐字未变。`Composition/Durable/Fold.fs` 的 `AgentFact.Execution`/`AgentFact.Delegation` 两个分支与 JS 面 `Handle/FoldSurface.foldApply` 都走这个 bridge。
- **直接引用与闭包现状（2026-09-12 用 owner compile 的 production `.fs` 闭包实测）**：`delegation-fold` 20（此前闭包含整个 durable spine）、`delegation-recovery-runtime` **47**（此前 193，经 `delegation-fold` 到达 spine 的路径已被切断）、`delegation-linkage-projection` 12、`delegation-journal-port` 13（新增了这套词汇文件）、`delegation-host-adapter` 278、`delegation-pty-adapter` 279（两者按 charter 仍组装 spine，未变）。行为符号审计里 `delegation-fold → composition-durable-projection`（27 处）与 `delegation-fold → interaction-authority-fact`（13 处）两对已消失，全仓 4 → 2 → **0** 对。DELEG-028 的 ratchet 按实测收紧为 host 278 / pty 279 / recovery 47，测试 `delegation-compile-boundary.test.mjs` 同步断言 fold 不得引用 foreign composition/adapter/runtime 分片、其闭包不得包含 spine 源文件、其源码不得命名 `AgentProjectionSet`/`PromptAuthority`/`FoldRejection`，且 `DelegationProjection.fs` 由 delegation contract 分片、bridge 由 composition 分片编译。
- **残留与原因**：真正剩下的同形缺口不再在 delegation 侧，而是 (a) 其它 8 个域 fold 分片（`change-fold`、`interaction-concern-fold`、`interaction-attention-fold`、`enforcer-institutionallearning-fold`、`execution-fission-fold`、`interaction-authority-fold`、`context-companion-companionfactfold`、`participant-provider-attempt-planner`）仍以 `AgentProjectionSet` 为签名并向 spine 声明引用，(b) `FoldRejection` 只有 `{ Fact: string; Reason: string }` 却让约 16 个生产/消费分片引用 `composition-durable-projection`。这两条按同一判据（DURABLE-EVENTS-023「领域只产生 owner-owned fold state 与 closed rejection」）继续收敛，属于 multi-shift 工作。- DELEG-029 关于 PTY adapter（不得读取 `HostForkRuntime`、Fork runtime、Process implementation、Gate、`Dictionary`、registry、TCS）的一半仍未实现：`Execution/Delegation/Fork/Host/Pty.fs` 目前扩展 `HostForkRuntime` 并使用 `Dictionary`，该半句没有对应证明，缺口见本文件 GAP 段。

### 载荷渲染与方向不对称

- **父 → 子（初始提示词注入）**：父会话向子会话传递上下文时，`ForkChildPayload` 将任务正文渲染为 `instructions`，将 `commissioner_record` 与 `attached_work_record` 作为 TOML 数据字段嵌入 body，杜绝将背景解析为指令或混入注释。
- **子 → 父（完成项回传）**：子会话结束并回传结果时，`JoinResultRenderer` 仅将物化的 WorkRecord 以 entry-local 注释形式注入 wire，严禁包裹为字段式 DTO。

### 同步委托批次与串行化

- **批次聚拢**：宿主在处理同单次运行中指向同一角色的多个同步委托时，按工具调用顺序合并为一个语义 batch，拼接 charge 后单次调度。
- **单栈执行与结果分发**：串行化键为直接调用方的 `ReuseScope`。仅第一位 canonical 调用方获得完整 WorkRecord，其余 sibling 调用方获得引用句柄。
- **普通完成收口**：被委托方普通 Assistant 结束即触发返回，无独立 return 协议通道。

### Reusable work unit

- 业务控制流只存在于 F# CE：`prepareHandoff → dispatch → await own completion → checkpointCompletedHandoff`。禁止 `Stage/Phase/ActiveWorkUnit`、显式 transition API 等第二运行时或 durable program counter。
- durable truth 只记录已经发生的事实：某个 logical route 的一次已完成 handoff 确实让 callee 看到了 parent XTrace 截止到哪个 cursor。projection 仅把这些 completion facts 积分成 `latestDeliveredThrough(route)`；它不拥有执行位置。
- 新调用从 `latestDeliveredThrough(route)` 到当前 parent XTrace head 物化 delta；route 首次调用取完整 parent LWR。logical route = fork/resume Byname 或 caller scope 下的 dedicated SyncDelegate role，绝不以 physical child `SessionId` 作为连续性身份。
- invocation-local 的 child start cursor、expected Authority Root、waiter/subscription 属于物理 correlation resource，可跨 callback 保存；它们不得 durable 化为 workflow stage。
- Host sticky terminal 可以继续服务 late observer/recovery；delegation CE 只接受与本次 dispatch 的 causal identity 匹配的 completion/failure。run-scoped `Completed/Failed/Aborted` 都保留 Authority Root；不能把“订阅之后”当作身份。
- Host 派发成功且 Submitted 回执已持久化即确认 fork/resume 放置。Pending 只表示 PhysicalAccepted 尚未到达，不是发送结果未知；保留原 acceptance callback，由后续真实消息绑定 Authority Root，completion 仍交 join/horizon。
- 首 prompt 的 Host acceptance 若为 unknown，`PromptAuthority` 的 durable Pending claim 是唯一恢复所有者：fork run 保持 Active、terminal observer 保持绑定、不得合成 `HandleCompleted`、不得自动重发。调用面返回明确的“可能已接受”后果，阻止调用方用第二个 child 猜测性补偿。
- fork 新 participant 与 resume same-road continuation 都是异步 assignment：返回只由本次 dispatch 成败决定；SyncDelegate 是同步 CE：等待本调用 completion、物化 bounded callee LWR、再 checkpoint completed handoff。
- 新 charge 遇到仍在运行的同 route 调用直接拒绝。Busy nudge 只服务同一 LogicalRun 的内部 continuation，彻底退出 assignment 工具路径。

### Join 消费与中断

Join 机制从所有者的完成信箱中按稳定排序逐项 CAS 消费可用结果，单次消费上限受 `MaxJoinBatch` 约束。外部打断信号与超时仅产生 `Interrupted` 结果，确保子会话的执行与既有权能不受破坏。

CompletionMailbox、Change VerdictMailbox 与 HostForkJoin 的 journal／fission 竞争结果使用单次调用内的 typed `Choice`，各 arm 保留原来的单次 `.then` 与注册顺序；`Promise.race` 只透传该类型，不编码数字标签或读取无类型字段。verdict 与 journal join 仍在竞争后先重新 drain，再解释中断；局部中断不取消 child，mailbox waiter 的释放责任不变。`join-wake-owner.test.mjs` 实际执行无 journal 的 PTY join，不能冒充 journal／fission 分支证明；后两者由真实 focused Fable 编译覆盖类型闭包，端到端范围以唯一 Long Stroke 实际经过的路径为限。

`verdict-mailbox.test.mjs` 经正式 Change Surface 构造真实 VerdictMailbox，证明已就绪／同一同步回合到达的 verdict 优先于中断、中断后的新 waiter 不被旧 waiter 吞掉唤醒，以及 FIFO 上限与余量。删除旧 `join-v2-mailbox.test.mjs`、`host-fork-join-algebra.test.mjs` 的九个重复 renderer／源码 token 检查：DELEG-019 约束 child prompt，不授权将这些检查称为等待竞争证明；有效 batch／中断后果仍由真实 join probe 与 `join-completion.test.mjs` 覆盖。

### Contract/Runtime 编译边界

- `Delegation.Contract` 汇集稳定 command/result、fact、payload、route 与 completion evidence；`Delegation.Fold` 只消费 contract 计算投影；`Delegation.Ledger` 在显式 Composition locality 中连接 `AgentJournal`。
- Sync/Fork/Recovery CE 分居三个 Runtime locality；Host 与 PTY 的物理调用分居两个 Adapter locality。Runtime/Adapter 只能依赖 contract/fold，普通 consumer 不能引用它们。
- `scripts/checks/owner-projects.mjs` 固定 locality kind、方向与 closure budget；`scripts/compile-owner.mjs` 只编译目标 ProjectReference closure 的单一 flat projection。

### JoinAttemptRegistry 窄编译边界

在 `e5794c8f4` 上将既有 `JoinInterruptRegistry.fs/.fsi` 从 recovery runtime 移入 `delegation/join-attempt-registry` 编译分片；源码路径、公开签名、aggregate 顺序和 registry 行为不变，仅删除无用 namespace 引入。新分片只引用 Identity、causal-wait contract 与 AsyncSupport，由原 recovery 分片和真实 `PluginSessionScope` consumer 显式引用。它仍属于 delegation，不把 session／attempt 语义伪装成平台原语。

新分片声明递归闭包为 4 项目／8 个 `.fs/.fsi` 输入，独立 Fable 编译通过 46 parsed sources（`5612752d421a`）。包含 GitSubject、registry 与 session／recovery scope 签名反向消费者的 flat 并集通过 1430 parsed sources／1392 items（`f31b44cd590f`）。编译证明不代替 no-future-latch、取消和逐项 CAS 的完整行为证明；现有 Join／中断证明入口保留。`ChildRecoveryWorkflow` 仍真实匹配 `MessagePart.Text`，本批保留 recovery 的宽 Host contract 引用，未以摘要调用为由误删它。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DELEG-001 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-001] manager_role_law_entrusts_by_consequence_not_persona` |
| DELEG-002 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-002] calling_names_differ_in_persona_depth_not_authority` |
| DELEG-003 | `requirements/delegation/tests/fork-tool.test.mjs::WHAT[DELEG-003] FORK_road_with_calling_is_independent_and_omitted_calling_continues_byname`；`requirements/delegation/tests/fork-tool.test.mjs::WHAT[DELEG-003] FORK_TOOL_requires_calling_and_resume_rejects_calling` |
| DELEG-004 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-004] commission_and_fork_are_distinct_contracts_not_witness` |
| DELEG-005 | `requirements/delegation/tests/join-v2-wire.test.mjs::WHAT[DELEG-005] JOIN_V2_rendered_wire_is_parseable_without_legacy_fields` |
| DELEG-006 | `requirements/delegation/tests/fork-tool.test.mjs::WHAT[DELEG-006] FORK_continuation_reuses_bound_managed_agent_and_does_not_rebind_tier` |
| DELEG-007 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-007] sync_delegate_edges_are_the_allowed_dag_only` |
| DELEG-008 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-008] SYNC_RUNTIME_provider_tool_call_collection_preserves_role_order` |
| DELEG-009 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-009] SYNC_RUNTIME_same_reuse_scope_serializes_distinct_provider_runs_but_distinct_scopes_are_independent` |
| DELEG-010 | `requirements/delegation/tests/sync-delegate.test.mjs::WHAT[DELEG-010] EXEC_026_agentNameFor_returns_bare_inspector_coder` |
| DELEG-011 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-011] SYNC_RUNTIME_ordinary_completion_settles_batch_without_return_channel` |
| DELEG-012 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-012] SYNC_RUNTIME_first_provider_call_receives_canonical_record_and_sibling_receives_reference` |
| DELEG-013 | `requirements/delegation/tests/join-completion.test.mjs::WHAT[DELEG-013] JOIN_COMPLETION_completed_is_rendered_as_entry_local_work_record` |
| DELEG-014 | `requirements/delegation/tests/join-completion.test.mjs::WHAT[DELEG-014] JOIN_COMPLETION_batch_preserves_order_and_bounded_work_records`；`requirements/change-integration/tests/verdict-mailbox.test.mjs::WHAT[DELEG-014] VERDICT_MAILBOX_ready_or_racing_verdict_beats_interrupt`；`requirements/change-integration/tests/verdict-mailbox.test.mjs::WHAT[DELEG-014] VERDICT_MAILBOX_idle_empty_returns_empty_sentinel`；`requirements/change-integration/tests/verdict-mailbox.test.mjs::WHAT[DELEG-014] VERDICT_MAILBOX_batch_is_capped_at_maxjoinbatch_fifo_with_exact_remainder` |
| DELEG-015 | `requirements/delegation/tests/join-completion.test.mjs::WHAT[DELEG-015] JOIN_COMPLETION_interrupted_is_not_fork_error`；`requirements/change-integration/tests/verdict-mailbox.test.mjs::WHAT[DELEG-015] VERDICT_MAILBOX_pending_interrupt_stays_interrupted_then_next_publish_delivers_exactly_once`；`requirements/delegation/tests/join-wake-owner.test.mjs::WHAT[DELEG-015] JOIN_WAKE_interrupt_stays_distinct_from_failure_and_releases_lock`；`requirements/delegation/tests/join-wake-owner.test.mjs::WHAT[DELEG-015] JOIN_WAKE_cancel_and_error_paths_release_lock_for_next_join` |
| DELEG-016 | `requirements/delegation/tests/join-v2-wire.test.mjs::WHAT[DELEG-016] JOIN_V2_empty_batch_is_plain_empty_wire` |
| DELEG-017 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-017] SYNC_RUNTIME_work_record_is_evidence_and_does_not_transfer_authority` |
| DELEG-019 | `requirements/delegation/tests/fork-child-payload.test.mjs::WHAT[DELEG-019] FORK_CHILD_PAYLOAD_full_shape_puts_all_instructions_before_reference_data` |
| DELEG-020 | `requirements/delegation/tests/delegation-structure-contract.test.mjs::WHAT[DELEG-020] delegation_semantics_do_not_depend_on_current_tool_names` |
| DELEG-021 | `requirements/delegation/tests/fork-attachment.test.mjs::WHAT[DELEG-021] DELEG_021_attachment_is_background_between_commissioner_and_requirements`；`requirements/delegation/tests/fork-attachment.test.mjs::WHAT[DELEG-021] DELEG_021_attachment_text_cannot_replace_the_assignment` |
| DELEG-022 | `requirements/delegation/tests/delegated-tool-estimate-surface.test.mjs::WHAT[DELEG-022] DELEG_022_replace_sets_exact_remaining_and_clears_prior_counted_calls`；`requirements/delegation/tests/delegated-tool-estimate-surface.test.mjs::WHAT[DELEG-022] DELEG_022_each_distinct_real_tool_call_decrements_once_and_saturates_at_zero`；`requirements/delegation/tests/delegated-tool-estimate-surface.test.mjs::WHAT[DELEG-022] DELEG_022_projection_is_incremental_not_a_transcript_or_xtrace_scan` |
| DELEG-023 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-023] SYNC_RUNTIME_transient_turn_failure_stays_child_local_until_exhausted` |
| DELEG-024 | `requirements/delegation/tests/fork-tool.test.mjs::WHAT[DELEG-024] FORK_TOOL_same_byname_reuse_dispatches_immediately_and_leaves_completion_to_join`；`requirements/delegation/tests/reusable-handoff.test.mjs::WHAT[DELEG-024] reusable handoff advances one durable parent delta window at a time`；`requirements/delegation/tests/reusable-handoff.test.mjs::WHAT[DELEG-024] reusable prompt carries the new charge and parent delta as data`；`requirements/delegation/tests/reusable-handoff.test.mjs::WHAT[DELEG-024] bounded child result never widens to an earlier invocation` |
| DELEG-025 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-025] SYNC_RUNTIME_late_failure_from_previous_authority_root_cannot_fail_reused_call`；`requirements/delegation/tests/reusable-work-unit.test.mjs::WHAT[DELEG-025] reusable fork terminal failure is guarded by the accepted authority root`；`requirements/host-boundary/tests/events-port.test.mjs::WHAT[DELEG-025] EVT_run_scoped_failure_preserves_authority_root_across_host_event_port` |
| DELEG-026 | `requirements/delegation/tests/reusable-work-unit.test.mjs::WHAT[DELEG-026] reusable delegation has no durable program-counter/state-machine vocabulary`；`requirements/delegation/tests/reusable-work-unit.test.mjs::WHAT[DELEG-026] fork admission bookkeeping that can fail happens before dispatch`；`requirements/delegation/tests/fork-tool.test.mjs::WHAT[DELEG-026] FORK_TOOL_acceptance_unknown_never_claims_charge_was_not_placed`；`requirements/delegation/tests/fork-tool.test.mjs::WHAT[DELEG-026] FORK_TOOL_transport_receipt_confirms_placement_without_fabricating_physical_acceptance` |
| DELEG-027 | `requirements/delegation/tests/reusable-work-unit.test.mjs::WHAT[DELEG-027] active fork assignment never becomes BusyAgentNudge` |
| DELEG-028 | `requirements/delegation/tests/delegation-compile-boundary.test.mjs::WHAT[DELEG-028] Delegation contract excludes workflow Host PTY and recovery sources`；`requirements/delegation/tests/delegation-compile-boundary.test.mjs::WHAT[DELEG-028] Delegation focused localities stay within compile budgets` |
| DELEG-029 | `requirements/delegation/tests/m6-slice-boundary.test.mjs::WHAT[DELEG-029] delegation runtime consumes only the delegation-owned journal port` |
| DELEG-030 | `requirements/delegation/tests/m6-slice-boundary.test.mjs::WHAT[DELEG-030] delegation invariant fatal preserves settlement and one injected fuse` |

## GAP

- `GAP-027`（CLOSED）：旧 reusable handoff 以 physical child `SessionId` 持有 cursor，并在 prompt 已 dispatch 后追加可失败 bookkeeping；旧 sticky terminal 还能跨 invocation 重放，fork idle reuse 又会立即返回旧/全生命周期结果，active new charge 还会混入 `BusyAgentNudge`。现已收口为 direct F# CE：logical-route frontier 只由 completed-handoff fact 推导；same-road fork/SyncDelegate 都执行 `prepare delta → dispatch → await own causal completion → bounded callee LWR → checkpoint`；fresh-only terminal observation 与 Authority Root 共同阻断上一轮 Completed/Failed；active assignment 明确拒绝；HostForkRuntime 的 bounded WorkRecord projector 为必需 capability，不能再构造“可完成但无 invocation delta”的 runtime。真实 fork tool 与 inspector/coder reuse 回归均已覆盖，authoritative runner 3405/3405 green。
