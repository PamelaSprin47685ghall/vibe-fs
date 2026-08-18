# PROOF — host-boundary（测试落点表）

每条 WHAT 命题恰好一行。类型：`MOVE`（本包 tests/ 物理拥有）/ `REUSE`（留在原处，记精确锚点 +
cutover 计划）/ `NEW`（本包新写）。运行命令均为 `node --test <file>`。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| HOST-BOUNDARY-001 | `tests/host001-fragment-events.test.mjs` `HOST_001_fragment_events_die_at_earliest_boundary`（fragment 不进入业务 signal）+ `HOST_001_terminal_message_identity_is_physical_capacity_evidence_not_a_business_signal`（terminal message 仅向 EMR 提供 exact parent physical identity，`tryDecode` 仍为 null）；`tests/signals.test.mjs` `MISC_signals_router_loop_delta_bypasses_adapt`（loop delta 不进入 adapt） | MOVE | `node --test requirements/host-boundary/tests/host001-fragment-events.test.mjs` |
| HOST-BOUNDARY-002 | `tests/host001-fragment-events.test.mjs` `HOST_001_only_coarse_session_lifecycle_signals_cross_the_boundary`（`session.status(idle)` / `session.idle` → 同一 `SessionIdle`，及 retry/deleted/aborted 分型）；`tests/signals.test.mjs` `MISC_signals_try_adapt_idle_retry_deleted_and_failure` / `R3_abort_error_adapts_to_attempt_aborted_not_dropped` / `MISC_signals_try_adapt_ownership_gate` / `MISC_signals_router_register_unregister`（两种 idle encoding + `MessageAbortedError`/`AbortError` → typed signal；ownership gate） | MOVE | `node --test requirements/host-boundary/tests/host001-fragment-events.test.mjs` / `node --test requirements/host-boundary/tests/signals.test.mjs` |
| HOST-BOUNDARY-003 | `tests/host-capability-observation.test.mjs` `HOST_003_host_signal_is_a_typed_wake_never_a_fact_carrier`（RetrySignal 诊断字段，无 message id）；`tests/host001-fragment-events.test.mjs`（typed decode）；`tests/signals.test.mjs` `MISC_signals_session_id_of_all_cases` / `MISC_signals_listen_*` / `MISC_signals_invalid_callback_fails_closed` / `MISC_signals_default_input_resolves_to_local_event_hook` / `MISC_signals_client_events_listen_fallback` / `MISC_signals_server_url_ignored_in_favor_of_local_hook`；`tests/reconcile-supervisor.test.mjs` `HOST_signal_subscribe_*`（typed HostSignal 观察 + 传输选择） | NEW + MOVE | `node --test requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-004 | NEW `tests/host004-turnunknown-boundary.test.mjs`（TurnUnknown 不是 TurnOutcome case；只存在于 SnapshotObservation；outcomeOf 拒绝而非 mint）；REUSE `requirements/structured-workflow/tests/reconcile-program.test.mjs`（`TurnUnknown` 私有观测） | NEW + REUSE | `node --test requirements/host-boundary/tests/host004-turnunknown-boundary.test.mjs` / `node --test requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| HOST-BOUNDARY-005 | `tests/reconcile-idle-early.test.mjs`（因果重读有界；首个早到 idle 耗尽后，第二个真实 coarse signal 可重新 Kick 并恢复 terminal；SnapshotError 有界因果重读：budget>1 → Reread，耗尽 → StopPass；连续错误有界 StopPass）；`tests/reconcile-supervisor.test.mjs` `EXEC_reconcile_*`（single-flight / dirty / 有界因果重读 / generation fence / ClearSession / 重试） | MOVE | `node --test requirements/host-boundary/tests/reconcile-idle-early.test.mjs` / `node --test requirements/host-boundary/tests/reconcile-supervisor.test.mjs` |
| HOST-BOUNDARY-006 | `tests/session-snapshot-locality.test.mjs` `HOST-004 keeps failed session tool state consistent across Parts and ToolParts`（failed 不进 ToolCall） | MOVE | `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-007 | `tests/host-capability-observation.test.mjs` `HOST_006_prevention_requires_compaction_settings_off_and_autocontinue_off` / `HOST_006_first_turn_probe_is_the_only_startup_verdict` / `HOST_006_containment_folds_observation_and_reanchors_newest_unhandled_once` | NEW | `node --test requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-008 | `tests/host010-run-id-equivalence.test.mjs`（bindableRun id ≡ ToolContext.messageID；0/≥2 无合法 run id）；REUSE `requirements/review-assurance/tests/seal-bind.test.mjs`（四条件 fail-closed）；`tests/session-execution-binding.test.mjs`（发送边界拒绝漂移；accepted PromptKey 绑定具体 provider attempt） | NEW + REUSE + MOVE | `node --test requirements/host-boundary/tests/host010-run-id-equivalence.test.mjs` |
| HOST-BOUNDARY-009 | `tests/tool-host-codec.test.mjs`（HOST-011：ToolContext 无 user message id、双半边）；`tests/session-snapshot-locality.test.mjs` `TODO-004 rejects a call id observed in more than one persisted ToolPart`（缺一半边 → Ambiguous） | MOVE | `node --test requirements/host-boundary/tests/tool-host-codec.test.mjs` / `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-010 | REUSE `requirements/host-boundary/tests/shared-state.test.mjs` `SHARED_dictionaries_are_live_singletons_shared_across_importers` / `SHARED_root_workspace_atom_round_trips_and_restores`（含 `ReviewGuardNudges`）；REUSE `requirements/review-assurance/tests/review-guard.test.mjs` `RVGD_nudgeReviewer_cross_instance_reservation_suppresses_twin_send` / `RVGD_nudgeReviewer_fails_without_open_review_barrier` / `RVGD_nudgeReviewer_new_barrier_receives_a_fresh_single_repair_budget`（missing-verdict 以 durable barrier 定域，禁 RuntimeId/trigger provider run；无 barrier fail closed；新 barrier 重置一次预算） | REUSE | `node --test requirements/host-boundary/tests/shared-state.test.mjs`；`node --test requirements/review-assurance/tests/review-guard.test.mjs` |
| HOST-BOUNDARY-011 | `tests/host-message-projection.test.mjs`（HOST_016 全 7 锚点：reasoning/thinking/ellipsis/hash/untouched/sanitizeMessages） | MOVE | `node --test requirements/host-boundary/tests/host-message-projection.test.mjs` |
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

## GAP 记录

- 聚合台账见 `requirements/GAP.md`（GAP-007 CLOSED / GAP-008 CLOSED）。
- `HOST-BOUNDARY-008`：unit encoding 由 `tests/host010-run-id-equivalence.test.mjs` 承接
  （bindableRun id ≡ ToolContext.messageID；0/≥2 无合法 run id）。共时 Host 穿线是不可模拟
  physical contract，由 Long Stroke 入口声明（VERIFICATION-SYSTEM-003），不另立 unit GAP。
- `HOST-BOUNDARY-019` 的 Magic Todo membrane canaries（下表现行清单）已落地实现于
  `tests/magic-todo-membrane-canaries.test.mjs`（production registered surfaces）+
  `tests/magic-todo-host-canaries.test.mjs`（Host SDK snapshot 定位物理子契约）。
  A/B/C/E/G/H/J/K/L/M/N/P/R 由 production surface 测试证明；F/O/Q 为显式 physical/static 边界。
  GAP-008 证据由 Main 在聚合台账 `requirements/GAP.md` 中独立复核。

## Magic Todo V1 membrane canaries（现行 release-gate 清单）

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

### 反例（必须红）

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

## 反向覆盖（OWNED / NEEDS-SPLIT clause → 本包命题）

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

## 包拥有的 gate / anchor

- `scripts/checks/architecture.mjs` 的 `host-boundary` gate（`PURE_DIRS` Kernel/Domain 禁
  `Fable.Core.JsInterop`）→ 本包（MECHANISM 共享 checker；语义为 JS-interop 边界）。
- semantic-anchors.mjs：本包**零 anchor**。
- `requirements/host-boundary/tests/host001-fragment-events.test.mjs` + `host012-tool-part.test.mjs` 的
  verify-family 归属：已 MOVE 至本包；`host012-tool-part` 的 `HOST_012_*` 断言覆盖 tool result
  digest 投影（seal 消费方为 review-assurance；断言本身是 Host wire 解码 → 本包）。

## SPLIT@cutover 清单

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
