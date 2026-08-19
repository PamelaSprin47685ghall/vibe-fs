# HOW — host-boundary（实现模型与约束；非 normative）

## 实现模型

### 信号边界（`Infrastructure/OpenCode/Codec/` + `Signals/`）

```fsharp
type HostSignal =                    // Infrastructure/OpenCode/Signals/HostSignal.fs
    | SessionIdle of SessionId
    | ProviderRetry of RetrySignal   // { SessionId; Attempt: string; Reason: string } 诊断 only
    | ProviderFailure of sessionId: SessionId * reason: string
    | SessionDeleted of sessionId: SessionId * parentSessionId: SessionId option
    | AttemptAborted of SessionId    // typed 物理 wake；非 ProviderFailure
```

`HostEventCodec.isHostSignalEvent / tryDecode` 在最早边界丢弃 fragment
（`message.updated` / `part.delta` / `session.updated` / `chat.message` 非 terminal）；
`session.status(status.type="idle")` 与 OpenCode 1.18 的独立 `session.idle` 都归一为 `SessionIdle`
wake，重复编码只触发一次新的 bounded reconcile pass。ProviderRunIdentity 始终只由完整 snapshot
判定；`ProviderFailure` 只在 signal admission 时冻结 current physical + failure reason，并与 snapshot
exact current assistant 合取补足 failure finality。
`HostSignalSubscribe`（one-shot silence deadline，无事件不触发）。`AttemptAborted` 撤销
idle-derived continuation 能力（QuiescenceGate 属 causal-wait，本包只拥有信号 admission）。

### 快照投影（`Infrastructure/OpenCode/Host/SessionSnapshotPort.fs`）

`projectMessages` 保持 Parts/ToolParts 一致投影；`locateToolCall` 按 callID 唯一解析
ToolPart/assistant/run/ordinal（0/≥2 → `Ambiguous` fail-closed）。`HostSessionContext.read`
从 raw event 提取 `(sessionId, agent)`；`roleOf` 经 `AgentRoleIdentity`。

Provider-run 因果绑定把“identity 判断”与“Host projector 可见性”分开：
`ProviderRunBinding.observeBindableRun` 只把纯 `NoBindableRun` 分类成 `ProjectionNotVisibleYet`；
`AmbiguousRun` / `NotLatestRun` 保持非重试 rejection。`Context/Prefix/Wire.fs` 的 armed retry
在这个 typed 暂态上做事件驱动的 bounded reread：重读等待由 `MessageVisibilityHub` 挂在
session 的 `message.updated` 信号上（事件 = 快路径），ITimerPort deadline 只做无信号时的
backstop（预算仍由 `projectionCatchupMaxReads/DelayMilliseconds` 封顶）；后续 snapshot 一旦出现唯一 incomplete assistant
即继续；预算耗尽仍以 `NoBindableRun` fail closed。这里的等待只让**同一已发布物理事实**变得
可见，不用时间推导业务状态，也不改变 bindableRun 的四条件。

### Reconciler（`Composition/Turn/Scheduler.fs`）

`Scheduler` 持有 queued/active/generation/wake：同 session 信号合并、最多一个 drain、
generation 隔离（HOST-004）。`maxCausalRereads = 3`；`maxConsecutiveErrors = 5`。
无 wake 记录默认 `RetryWake`（无 idle rights，安全侧）。业务决策在 `ReconcileProgram`（纯）。

### compaction gate（`Domain/HostCompactionPolicy.fs` + `HostCompactionGate/Observer`）

- prevention keys：`compaction.auto`（含 overflow）、`compaction.prune`（COMPANION-009：
  物理删行不可收容）、`compaction.autocontinue`；`autoContinueEnabled = false`。
- `judgeFirstTurn`：setting unavailable → `SettingUnavailable`；首轮 pseudo-run > 0 →
  `CompactedDespiteSettings`；否则 `Satisfied`。失败 → `HostContractUnsupported` 启动失败。
- containment：`isContainableCompaction`（折叠后的 bool，无来源区分）；`nextReanchor` 只返回
  最新未处理 run（一次重锚即 epoch+1 / coverage 归零）。

### 多实例（`Infrastructure/OpenCode/Host/SharedState.fs` + `PluginRuntimeScope.fs`）

模块级单例：`SessionParents` / `VerdictSessions` / `SessionDirectories`（身份注册表）；
每实例：`AgentJournal` / Companions 缓存 / `OwnedSessions` / `UserMessageBindings` / hook 订阅。
共享表操作不跨 `await`（单一 event loop 所有权）。

### 事件 port（`Infrastructure/OpenCode/Host/Events.js`）

`Events_HostEventPort`：per-provider-run Completed dedupe、非 Completed 不 dedupe、late
subscriber sticky replay、listener disposal。

### Hook fatal membrane

`PluginHostInterop` 先用 `curriedHook` / `pairedHook` 做 Fable→Host arity adapter，再对**已经适配成二参 callable** 的函数套 `fatalHook`。顺序不能反：把 guard 包在原始 F# 函数外会改变 Fable emitted arity，曾实测把 paired hook 变成 curried no-op。guard 同时捕获 synchronous throw 与 returned Promise rejection，调用 `Diagnostic.fatal(operation,result)` 后 rethrow；`config` / `event` 用 `fatalSync`，`dispose` 用 `fatalTask`。`SpikePlugin.initSpikePlugin` 也有 init fatal boundary。

`Diagnostic.fatal` 的物理 kill 下沉到 `Foundation.FatalProcess.kill`，使低层 durable journal 也能使用同一个 process fuse：live `AgentJournal` semantic cut 直接 `FatalProcess.trip("journal-semantic-cut", ...)`，不依赖异常恰好一路冒到 Host hook。**只有 Wanxiangshu 测试 harness 显式设置的 `WANXIANGSHU_NO_FATAL_EXIT=1` 可以屏蔽物理 kill**；不得信任 `NODE_TEST_CONTEXT` 或其它宿主/Node/Bun 环境变量，因为它们可能被真实 OpenCode 进程继承并把 production fatal 意外降级成普通异常。未显式 opt-out 时 fatal 必须终止整个进程，而不是把 Error 交还 Host loop 继续执行。

### 其它

- `HostMessageProjection.sanitizeMessages`（HOST-016）在 PairProgrammingThought 之后执行；reasoning/thinking-only
  assistant 只补 `"."` text 占位，绝不复制 reasoning 原文；真正空 assistant 仍补 `"..."`；连续两条 user 消息之间插入 `role="assistant"`、text 为 `"."` 的 assistant 消息。Finality review 不再对 provider message bytes 建 seal。
- `HostDigest.sha256Hex`：全仓唯一 sha256（durable digest 单点定义）。
- `NeedHelpSensor`（`Host/NeedHelpSensor.fs`）：rolling suffix + reasoning PartId 集 + armed
  identity（SessionId × ProviderRun）；`NeedHelpEventCodec` 先登记 `part.type=reasoning` 再适配
  `field=text` delta；legacy direct reasoning-field 仅 codec 兼容。
- `Tools/ToolContext.fs`：`{ SessionId; Workspace; Cancellation }`（execute 双半边身份经
  `ToolHostCodec` 组装；before/after 只见 sessionID+callID）。

## Plugin Load / Activation 分界（HOST-BOUNDARY-021）

`server(input)` 返回 hooks 之前只组装 capability。该路径不得访问 Host session API，不做 durable semantic recovery，不修改 workspace/Git，不产生业务 durable fact。

Load Phase 可以检查模块、静态资源、配置与 durable bytes 的结构可读性；历史中已经有 canonical cut/reset 的坏 fact 由 replay 正常积分，不应在 init 再触发新 fatal。普通 hook/tool 也不得承担“上一进程工具恢复”：Fission/Assistance/js-* 等未完成执行保持坏记录。未来 session resume 必须由显式 `/continue` 进入并把 restart/broken-tool 事实公开给 LLM。Activation Phase 若**新 live append**产生 semantic cut，则 durable-events 的 process fuse 立即 fatal；这不是 startup recovery，也不是“feature 可降级”。

## 历史与弃权

- **碎片积分被拒**（why/host.md §4）：流式碎片顺序/形状随 Host 版本漂移 → 选「粗粒度唤醒 +
  完整 snapshot」。
- **busy/running 进业务信号被拒**（cache.md §16）：transport 状态机不搬进 Domain →
  process-local QuiescenceGate（归 causal-wait）。
- **重复读 snapshot 证明 idle 被拒**（cache.md §15）：观测稳定 ≠ 静止资格 → permit 随 wake 携带、
  发送前再 TryConsume（归 causal-wait）。
- **无界退避轮询被拒**（reconciler-event-driven-de-polling.md）：30s 墙钟预算 = 以时间推进做
  A 类探测 → 有界因果重读（≤3）+ 事件驱动。
- **canary 不可弯曲**（canary-unbend.md）：canary 是生产前置证明，不得为绿而弯曲（归
  verification-system 纪律；本包消费其结果）。
- **HOST-013 全部**：归 prefix-stability 等（见 WHAT 弃权）；本包不复制。
- **Magic Todo membrane canaries**：现行清单（见 HOW.md）中本包只拥有 H（唯一
  定位）、A/C（before 时序/原地 mutation）的 Host 观察面；canonical 语义归各 feature owner。
- **`external_directory`**：AGENT-019 唯一 enforcement 写点归 capability-enforcement；Host 路径
  边界机制是 host-boundary 交叉（本包只记录观察面）。

## DEPENDS ON

无产品语义依赖（INDEX.md：`host-boundary → 无`）。本包 PROVIDES 其它 packages 可依赖的物理
ports 与 observation reliability。

## 验证与测试落点

每条 WHAT 命题恰好一行。类型：`MOVE`（本包 tests/ 物理拥有）/ `REUSE`（留在原处，记精确锚点 +
cutover 计划）/ `NEW`（本包新写）。运行命令均为 `node --test <file>`。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| HOST-BOUNDARY-001 | `tests/host001-fragment-events.test.mjs` `HOST_001_fragment_events_die_at_earliest_boundary`（fragment 不进入业务 signal）+ `HOST_001_terminal_message_identity_is_physical_capacity_evidence_not_a_business_signal`（terminal message 仅向 EMR 提供 exact parent physical identity，`tryDecode` 仍为 null）；`tests/signals.test.mjs` `MISC_signals_router_loop_delta_bypasses_adapt`（loop delta 不进入 adapt） | MOVE | `node --test requirements/host-boundary/tests/host001-fragment-events.test.mjs` |
| HOST-BOUNDARY-002 | `tests/host001-fragment-events.test.mjs` `HOST_001_only_coarse_session_lifecycle_signals_cross_the_boundary`（`session.status(idle)` / `session.idle` → 同一 `SessionIdle`，及 retry/deleted/aborted 分型）；`tests/signals.test.mjs` `MISC_signals_try_adapt_idle_retry_deleted_and_failure` / `R3_abort_error_adapts_to_attempt_aborted_not_dropped` / `MISC_signals_try_adapt_ownership_gate` / `MISC_signals_router_register_unregister`（两种 idle encoding + `MessageAbortedError`/`AbortError` → typed signal；ownership gate） | MOVE | `node --test requirements/host-boundary/tests/host001-fragment-events.test.mjs` / `node --test requirements/host-boundary/tests/signals.test.mjs` |
| HOST-BOUNDARY-003 | `tests/host-capability-observation.test.mjs` `HOST_003_host_signal_is_a_typed_wake_never_a_fact_carrier`（RetrySignal 诊断字段，无 message id）；`tests/host001-fragment-events.test.mjs`（typed decode）；`tests/signals.test.mjs` `MISC_signals_session_id_of_all_cases` / `MISC_signals_listen_*` / `MISC_signals_invalid_callback_fails_closed` / `MISC_signals_default_input_resolves_to_local_event_hook` / `MISC_signals_client_events_listen_fallback` / `MISC_signals_server_url_ignored_in_favor_of_local_hook`；`tests/reconcile-supervisor.test.mjs` `HOST_signal_subscribe_*`（typed HostSignal 观察 + 传输选择） | NEW + MOVE | `node --test requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-004 | NEW `tests/host004-turnunknown-boundary.test.mjs`（TurnUnknown 不是 TurnOutcome case；只存在于 SnapshotObservation；outcomeOf 拒绝而非 mint）；REUSE `requirements/structured-workflow/tests/reconcile-program.test.mjs`（`TurnUnknown` 私有观测） | NEW + REUSE | `node --test requirements/host-boundary/tests/host004-turnunknown-boundary.test.mjs` / `node --test requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| HOST-BOUNDARY-005 | `tests/reconcile-idle-early.test.mjs`（NEW：旧 terminal 不得跨 current physical fallback；exact terminal `message.updated` edge 只驱动一次额外 snapshot read；edge/pending race 不丢 wake；IdleWake 的 QuiescencePermit 跨 projection re-kick 保留；FailureWake + snapshot exact current assistant 即刻物化 `TurnFailed`，无需等待 terminal projection/idle；current assistant 尚不可见时仍 pending；无 counter-driven reread）；`tests/reconcile-supervisor.test.mjs` `EXEC_reconcile_*`（SnapshotError/NoTurn 不自轮询；只有 IdleWake 可把普通 nonterminal current assistant 交给 repair；terminal 仍 Publish；single-flight / generation fence / ClearSession） | NEW + MOVE | `node --test requirements/host-boundary/tests/reconcile-idle-early.test.mjs requirements/host-boundary/tests/reconcile-supervisor.test.mjs` |
| HOST-BOUNDARY-006 | `tests/session-snapshot-locality.test.mjs` `HOST-004 keeps failed session tool state consistent across Parts and ToolParts`（failed 不进 ToolCall） | MOVE | `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-007 | `tests/host-capability-observation.test.mjs` `HOST_006_prevention_requires_compaction_settings_off_and_autocontinue_off` / `HOST_006_first_turn_probe_is_the_only_startup_verdict` / `HOST_006_containment_folds_observation_and_reanchors_newest_unhandled_once` | NEW | `node --test requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-008 | `tests/host010-run-id-equivalence.test.mjs`（bindableRun id ≡ ToolContext.messageID；首读 `NoBindableRun` 可作为 projector visibility gap 有界重试并在后续唯一 child 出现时恢复；Ambiguous/NotLatest 不可重试；0/≥2 最终无合法 run id）；`tests/message-visibility.test.mjs`（事件驱动重读：message.updated 唤醒 waiter 并取消 deadline backstop；无信号由 deadline 收口；跨 session 信号不误唤醒；settled waiter 不留注册表）；REUSE `requirements/review-assurance/tests/seal-bind.test.mjs`（四条件 fail-closed）；`tests/session-execution-binding.test.mjs`（发送边界拒绝漂移；accepted PromptKey 绑定具体 provider attempt） | NEW + REUSE + MOVE | `node --test requirements/host-boundary/tests/host010-run-id-equivalence.test.mjs` / `node --test requirements/host-boundary/tests/message-visibility.test.mjs` |
| HOST-BOUNDARY-009 | `tests/tool-host-codec.test.mjs`（HOST-011：ToolContext 无 user message id、双半边）；`tests/session-snapshot-locality.test.mjs` `TODO-004 rejects a call id observed in more than one persisted ToolPart`（缺一半边 → Ambiguous） | MOVE | `node --test requirements/host-boundary/tests/tool-host-codec.test.mjs` / `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-010 | REUSE `requirements/host-boundary/tests/shared-state.test.mjs` `SHARED_dictionaries_are_live_singletons_shared_across_importers` / `SHARED_root_workspace_atom_round_trips_and_restores`（含 `ReviewGuardNudges`）；REUSE `requirements/review-assurance/tests/review-guard.test.mjs` `RVGD_nudgeReviewer_cross_instance_reservation_suppresses_twin_send` / `RVGD_nudgeReviewer_fails_without_open_review_barrier` / `RVGD_nudgeReviewer_new_barrier_receives_a_fresh_single_repair_budget`（missing-verdict 以 durable barrier 定域，禁 RuntimeId/trigger provider run；无 barrier fail closed；新 barrier 重置一次预算） | REUSE | `node --test requirements/host-boundary/tests/shared-state.test.mjs`；`node --test requirements/review-assurance/tests/review-guard.test.mjs` |
| HOST-BOUNDARY-011 | `tests/host-message-projection.test.mjs`（HOST_016 全 8 锚点：reasoning/thinking/ellipsis/hash/untouched/sanitizeMessages/consecutiveUsers） | MOVE | `node --test requirements/host-boundary/tests/host-message-projection.test.mjs` |
| HOST-BOUNDARY-012 | `tests/session-snapshot-locality.test.mjs` `TODO-004 resolves a tool callback through its persisted assistant run and Host ToolPart`；`tests/magic-todo-host-canaries.test.mjs` `CANARY_H_journal_xtrace_uniquely_completes_host_carrier`（sessionID+callID 唯一定位 provider run / ToolPart / ordinal / XTrace range） | MOVE | `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` / `node --test requirements/host-boundary/tests/magic-todo-host-canaries.test.mjs` |
| HOST-BOUNDARY-013 | `tests/needhelp-sensor.test.mjs`（HOST_027 全 5 锚点：sentinel strip / codec 关联 / 跨碎片触发 / case-variant+visible-text 不触发 / 每 run 一次） | MOVE | `node --test requirements/host-boundary/tests/needhelp-sensor.test.mjs` |
| HOST-BOUNDARY-014 | `requirements/host-boundary/tests/host-hooks.test.mjs::HOST_009_hook_invariant_exceptions_cross_a_fatal_membrane_before_rethrow`; `requirements/host-boundary/tests/host-hooks.test.mjs::HOST_009_inherited_NODE_TEST_CONTEXT_never_disables_production_fatal`; `requirements/host-boundary/tests/host-hooks.test.mjs::HOST_009_every_registered_hook_has_a_fixture_here`; `requirements/host-boundary/tests/host-hooks.test.mjs::HOST_009_every_hook_accepts_its_arguments_positionally`; `requirements/host-boundary/tests/host-hooks.test.mjs::HOST_009_the_tool_registry_is_a_registry_not_a_triggered_hook`（production owners: `dist/OpenCode/Host/PluginHostInterop.js` fatalHook + `dist/OpenCode/Host/Diagnostic.js` fatal + `src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs` registered hook set；无 support fixture）；`scripts/checks/architecture.mjs` | MOVE + REUSE | `node --test requirements/host-boundary/tests/host-hooks.test.mjs requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs` / `node scripts/checks/architecture.mjs` |
| HOST-BOUNDARY-015 | REUSE `requirements/host-boundary/tests/tool-result-bound.test.mjs`（ARCH-012 有界留尾） | REUSE | `node --test requirements/host-boundary/tests/tool-result-bound.test.mjs` |
| HOST-BOUNDARY-016 | `tests/events-port.test.mjs`（EVT 全 5 锚点：同 run 去重 / 无 run 不去重 / failed+aborted 不去重 / sticky replay / disposal）；`tests/join-completion.test.mjs`（sticky 重放 + Failed 不去重）；`tests/reconcile-supervisor.test.mjs` `EXEC_events_sticky_terminal_bounded`（容量 256 FIFO） | MOVE | `node --test requirements/host-boundary/tests/events-port.test.mjs` |
| HOST-BOUNDARY-017 | `tests/host-session-context.test.mjs`（HOST_CTX 全 6 锚点：read 提取 + roleOf 解析/alias 拒绝）；`tests/events-port.test.mjs`（`notifyCompleted` 对 unknown/空白/缺失 role 返回 `false` 且不投递，canonical role 才投递）；`tests/sphinx-mcp-launch.test.mjs` / `tests/stealth-browser-mcp-launch.test.mjs`（MCP Host adapter launch 判定 / apply 保留 / configure 注入；production JS-native surfaces `dist/OpenCode/Host/SphinxMcpConfigSurface.js` + `dist/OpenCode/Host/StealthBrowserMcpConfigSurface.js`，无 Fable DU 跨边界）；managed config 单向 projection 复用 capability-enforcement；model non-authority 由 `execution-model-routing` EMR-008 接管 | MOVE + REUSE | `node --test requirements/host-boundary/tests/host-session-context.test.mjs requirements/host-boundary/tests/events-port.test.mjs requirements/capability-enforcement/tests/managed-agent-config.test.mjs`；model 部分见 execution-model-routing PROOF |
| HOST-BOUNDARY-018 | `tests/host018-no-fork.test.mjs`（fsproj 无 OpenCode package/project reference；SpikePlugin 只组装 wiring；interop 只 import `@opencode-ai/plugin`）；REUSE `tests/host-hooks.test.mjs`（ARCH-003 只用现有 Hook/SDK） | NEW + REUSE | `node --test requirements/host-boundary/tests/host018-no-fork.test.mjs` / `node --test requirements/host-boundary/tests/host-hooks.test.mjs` |
| HOST-BOUNDARY-019 | `tests/host-capability-observation.test.mjs`（HostContractUnsupported 显式失败 + HostDigest 单一确定性 sha256）；`requirements/interaction-authority/tests/chat-params-hook.test.mjs` + `tests/host-hooks.test.mjs`（`CHAT_MESSAGE_routes_managed_model_then_CHAT_PARAMS_only_validates` + `CHAT_MESSAGE_new_physical_material_supersedes_old_capacity_without_idle`：managed request model mutation 真正进入 provider 输入、exact PhysicalUserMessageId supersession、chat.params temperature pin）；`tests/magic-todo-membrane-canaries.test.mjs`（Magic Todo V1 membrane canaries A–R：A openLife + compatibility injection 不等待 snapshot IO / B definition 双 schema 替换 / C non-enumerable compatibility view / E after 改写 output / G dual-path Accepted / H sessionId+callId 唯一 durable identity / J live Accepted digest 对齐 / K recovery Accepted / L Prepared 不 Accepted / M REVISE feedback-only / N zero bare SessionTodo.update 静态 / O no plugin todowrite override 静态 / P bridge 非真相 Journal 恢复 / Q description face 静态 / R multi-todowrite 全拒 / F physical integration boundary）；`tests/magic-todo-host-canaries.test.mjs` `CANARY_H_journal_xtrace_uniquely_completes_host_carrier` / `CANARY_H_journal_mapping_fails_closed_on_host_part_mismatch`（Host SDK snapshot 定位物理子契约）；REUSE `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs`（membrane 语义交叉 TODO-002..013）；managed request model mutation 由 `requirements/verification-system/tests/e2e/support/managed-model-routing-canary.mjs` 证明真实 provider wire | NEW + REUSE + PHYSICAL | `node --test requirements/host-boundary/tests/magic-todo-membrane-canaries.test.mjs` / `node --test requirements/host-boundary/tests/magic-todo-host-canaries.test.mjs` / `node --test requirements/host-boundary/tests/host-capability-observation.test.mjs`；model canary：`node requirements/verification-system/tests/e2e/support/managed-model-routing-canary.mjs` |
| HOST-BOUNDARY-020 | `tests/session-snapshot-locality.test.mjs`（Ambiguous）、`tests/host001-fragment-events.test.mjs`（codec 丢弃）、`tests/needhelp-sensor.test.mjs`（armed 唯一）、`tests/host012-tool-part.test.mjs`（wire 解码 fail-closed 投影）、`tests/magic-todo-host-canaries.test.mjs` `CANARY_H_journal_mapping_fails_closed_on_host_part_mismatch`、`tests/xwire.test.mjs`（`XWIRE_covered_prefix_digest_is_sha256`、`XWIRE_*_throws`、`XWIRE_missing_prefix_epoch_fail_closed` / `XWIRE_malformed_prefix_epoch_fail_closed` / `XWIRE_missing_frozen_prefix_body_fail_closed` 与 stale probe promotion guard；观察不足 fail closed） | MOVE | 见各行 |
| HOST-BOUNDARY-021 | `tests/plugin-load-purity.test.mjs`：plugin load graph 禁 Host session API/recovery/workspace mutation；ordinary join 只认 current-process handle/Fission binding；Open Fission/Assistance/JS pending state 保持 broken，不由普通入口恢复；`tests/xwire.test.mjs`（无业务意图不恢复；armed 后才推进 probe/reconcile）；`requirements/execution-model-routing/tests/scheduler-module-config.test.mjs` 证明唯一允许的缺失用户配置 create-if-absent 不覆盖已有文件 | NEW + REUSE | `node --test requirements/host-boundary/tests/plugin-load-purity.test.mjs requirements/execution-model-routing/tests/scheduler-module-config.test.mjs` |

### GAP 记录

- 聚合台账见 `requirements/GAP.md`（GAP-007 CLOSED / GAP-008 CLOSED）。
- `HOST-BOUNDARY-008`：unit encoding 由 `tests/host010-run-id-equivalence.test.mjs` 承接
  （bindableRun id ≡ ToolContext.messageID；0/≥2 无合法 run id）。共时 Host 穿线是不可模拟
  physical contract，由 Long Stroke 入口声明（VERIFICATION-SYSTEM-003），不另立 unit GAP。
- `HOST-BOUNDARY-019` 的 Magic Todo membrane canaries（下表现行清单）已落地实现于
  `tests/magic-todo-membrane-canaries.test.mjs`（production registered surfaces）+
  `tests/magic-todo-host-canaries.test.mjs`（Host SDK snapshot 定位物理子契约）。
  A/B/C/E/G/H/J/K/L/M/N/P/R 由 production surface 测试证明；F/O/Q 为显式 physical/static 边界。
  GAP-008 证据由 Main 在聚合台账 `requirements/GAP.md` 中独立复核。

### Magic Todo V1 membrane canaries（现行 release-gate 清单）

2026-08-14 cutover 自旧 proof 归档迁入。语义交叉：`obligation-ledger`（TODO-002..013）与
`host-boundary`（HOST-017..025，历史编号）。**任一 blocking canary 未证明 → 禁止写
production membrane；禁止改 Host core 绕过。**

| ID | 证明 | 期望 | 条款 | 级别 |
|----|------|------|------|------|
| A | openLife + compatibility injection 不等待 snapshot IO | `openLife` 是 journal append（无 snapshot port）；V1 compatibility injection（`host.replaceCompatibilityArgs`）是同步纯函数；`pending + {}` 在 deferred prepare 中等待，同一 physical ToolPart materialize 后 canonical input == captured live args，digest 取 materialized input；executor 见 V1 compatibility list；after 必须 await prepare 才可 Accepted | HOST-019 | **blocking** |
| B | 同时替换 parameters + jsonSchema | provider 见 V2；原 executor 仍跑 V1 decoder | HOST-018 | blocking |
| C | non-enumerable compatibility view | 原 V1 decoder 可读 `todos`；`Object.keys`/`JSON.stringify`/Host persistence 仍只见 provider `obligations` | HOST-019/020 | blocking |
| E | after 改写 `output.output` | 本次模型可见 ∧ 下一 provider history **同字节** | HOST-021、TODO-005/013 | blocking |
| F | execute throw | 记录 after 是否运行；协议不依赖其运行 | HOST-021 | 冻结观测 |
| G | after 运行瞬间 | 冻结 ToolPart 是否已 durable completed；Accepted 仍走双路径 | HOST-022、TODO-004 | blocking（防误绑） |
| H | 仅 sessionID+callID | 完整 SDK snapshot **唯一**定位 ToolPart / assistant / run / ordinal / XTrace range | HOST-025、HOST-011 | **blocking** |
| J | live Accepted | executor 成功→after → `TodoWriteAccepted` 与 Prepared digest 对齐 | HOST-022 | blocking |
| K | recovery Accepted | 无 after 时 snapshot completed ToolPart → 同一 digests Accepted | HOST-022、TODO-012 | blocking |
| L | Prepared+失败 | 不 Accepted；sink 乐观 Pk 不构成 checkpoint；下次 before Journal 覆盖 sink | HOST-022、TODO-007 | blocking |
| M | REVISE 消费后 reconcile | Host TodoTable == settled current；**零**新 checkpoint/review facts | HOST-023、TODO-005/007 | blocking |
| N | V2 runner | 无 hook parity 时 MagicTodo Manager Attempt **construction fail closed**；零裸 `SessionTodo.update` | HOST-024、TODO-004 | **blocking** |
| O | 无 Host core / 同名覆盖 | builtin executor 仍为 sink；无 OpenCode 源码修改；无 plugin 同名 tool 夺权 | HOST-017 | 静态/集成 |
| P | bridge 非真相 | crash 后忽略 Map；只从 Journal 恢复；failure cleanup 无残留 key | HOST-021、TODO-012 | blocking |
| Q | description 面 | 含 tagged/lag/multi-reject；**不含** reviewer/session/barrier/witness/2N | HOST-018、TODO-013 | 静态 |
| R | multi-todowrite | 同 assistant message 两个不同 callID → 全部拒绝、无 winner | HOST-020、TODO-004 | blocking |

代表落点（已实现）：`requirements/host-boundary/tests/magic-todo-membrane-canaries.test.mjs`（A–R
production surface 证明）、`requirements/host-boundary/tests/magic-todo-host-canaries.test.mjs`（H
Host SDK snapshot 定位物理子契约）、integration plugin hook 契约、e2e Manager todowrite
unhappy-path。canary 文件完整性测试防止静默缩减。

#### 反例（必须红）

```text
openLife 等待 snapshot/Journal 导致 executor 被 IO 阻塞        → A 红
pending `{}` 被降级受理或用于 ProviderInputDigest            → A 红
before mutation 改写最终 historical ToolPart.input           → A 红 → 停
after 不 await deferred prepare 就 Accepted                  → A/J 红
after 回写“修复”被污染的历史 input                           → 仍停（不可补救）
V2 settle 静默写 TodoTable                              → N 红
REVISE settlement 后 sink 永久留否决 Pk                 → M 红
bridge / Host TodoTable 当 canonical 恢复源             → P/L 红
plugin tool 名 todowrite 覆盖 builtin                   → O 红
```

### 反向覆盖（OWNED / NEEDS-SPLIT clause → 本包命题）

- `HOST-001`（OWNED）→ HOST-BOUNDARY-001/002。
- `HOST-002`（OWNED）→ HOST-BOUNDARY-002/004。
- `HOST-003`（OWNED）→ HOST-BOUNDARY-003。
- `HOST-004`（Reconciler 快照观测部分）→ HOST-BOUNDARY-004/005/006。
- `HOST-006`（观测 gate 部分）→ HOST-BOUNDARY-007。
- `HOST-010`（唯一性 + 因果读部分）→ HOST-BOUNDARY-008/020。
- `HOST-011`（OWNED）→ HOST-BOUNDARY-009/012/020。
- `HOST-012`（OWNED）→ HOST-BOUNDARY-010/016。
- `HOST-016`（OWNED）→ HOST-BOUNDARY-011。
- `HOST-019`（Hook 时序 barrier）→ HOST-BOUNDARY-012/020。
- `HOST-020`（原地 mutation 观察）→ HOST-BOUNDARY-012/017（GAP 见上）。
- `HOST-025`（OWNED）→ HOST-BOUNDARY-012/020。
- `HOST-027`（reasoning sensor 部分）→ HOST-BOUNDARY-013。
- `ARCH-002` → HOST-BOUNDARY-001/003；`ARCH-003` → HOST-BOUNDARY-014/018；
  `ARCH-012` → HOST-BOUNDARY-015。
- `PROMPT-008`（物理 identity 可信取得）→ HOST-BOUNDARY-008/009（bind-once 本体交叉
  provider-language）。

### 包拥有的 gate / anchor

- `scripts/checks/architecture.mjs` 的 `host-boundary` gate（`PURE_DIRS` Kernel/Domain 禁
  `Fable.Core.JsInterop`）→ 本包（MECHANISM 共享 checker；语义为 JS-interop 边界）。
- semantic-anchors.mjs：本包**零 anchor**。
- `requirements/host-boundary/tests/host001-fragment-events.test.mjs` + `host012-tool-part.test.mjs` 的
  verify-family 归属：已 MOVE 至本包；`host012-tool-part` 的 `HOST_012_*` 断言覆盖 tool result
  digest 投影（seal 消费方为 review-assurance；断言本身是 Host wire 解码 → 本包）。

### SPLIT@cutover 清单

1. `requirements/host-boundary/tests/shared-state.test.mjs`：`SHARED_dictionaries...` / `SHARED_root_workspace...`
   归本包；`SHARED_pending_seal_record_carries_the_binding_candidate` 归 review-assurance
   （REVIEW-010 PendingSeal shape）。cutover 时拆分。
2. `requirements/participant-identity/tests/session-execution-binding.test.mjs`：InjectedSessionPort 发送边界拒绝漂移
   归本包（PROMPT-008 物理身份）；SessionPersona 重绑归 interaction-authority / participant-
   identity；SessionProviderLanguage bind-once 归 provider-language。cutover 时拆分。
3. `requirements/interaction-authority/tests/chat-params-hook.test.mjs`：chat.params 观察适配归本包；binding 语义归
   interaction-authority。cutover 时拆分。
4. `requirements/delegation/tests/assistance-host.test.mjs`：sensor/armed occasion 边界归本包；authority 语义
   归 interaction-authority；consultation child 归 delegation。cutover 时拆分。
5. `requirements/capability-enforcement/tests/managed-agent-config.test.mjs`：owned-fields / external_directory 边界归
   本包；inventory/model 校验归 capability-enforcement。cutover 时拆分。
6. `requirements/crash-reconciliation/tests/quiescence-surface.test.mjs`：整体归 causal-wait（QuiescencePermit）。
7. `tests/unit/host/pair-thought-*.test.mjs`：归 prefix-stability / provider-projection（HOST-013）。
8. `requirements/review-assurance/tests/review-guard.test.mjs`：归 review-assurance。
9. `tests/unit/verify/` 目录：host001-fragment-events / host012-tool-part 已 MOVE；其余 verify
   文件与其它包交叉。
