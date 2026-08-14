# PROOF — host-boundary（测试落点表）

每条 WHAT 命题恰好一行。类型：`MOVE`（本包 tests/ 物理拥有）/ `REUSE`（留在原处，记精确锚点 +
cutover 计划）/ `NEW`（本包新写）。运行命令均为 `node --test <file>`。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| HOST-BOUNDARY-001 | `tests/host001-fragment-events.test.mjs` `HOST_001_fragment_events_die_at_earliest_boundary`（fragment 在 codec 边界丢弃） | MOVE | `node --test requirements/host-boundary/tests/host001-fragment-events.test.mjs` |
| HOST-BOUNDARY-002 | `tests/host001-fragment-events.test.mjs` `HOST_001_only_coarse_session_lifecycle_signals_cross_the_boundary`（idle/retry/deleted/aborted 分型）；REUSE `requirements/host-boundary/tests/signals.test.mjs`（`MessageAbortedError`/`AbortError` → typed `AttemptAborted`） | MOVE + REUSE | 同上 / `node --test requirements/host-boundary/tests/signals.test.mjs` |
| HOST-BOUNDARY-003 | `tests/host-capability-observation.test.mjs` `HOST_003_host_signal_is_a_typed_wake_never_a_fact_carrier`（RetrySignal 诊断字段，无 message id）；`tests/host001-fragment-events.test.mjs`（typed decode） | NEW + MOVE | `node --test requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-004 | REUSE `requirements/structured-workflow/tests/reconcile-program.test.mjs`（`TurnUnknown` 私有观测）+ `requirements/host-boundary/tests/signals.test.mjs` | REUSE | `node --test requirements/structured-workflow/tests/reconcile-program.test.mjs` / `requirements/host-boundary/tests/signals.test.mjs` |
| HOST-BOUNDARY-005 | REUSE `requirements/host-boundary/tests/reconcile-idle-early.test.mjs`（因果重读 ≤3、无第二信号恢复）；REUSE `requirements/structured-workflow/tests/reconcile-program.test.mjs` | REUSE | `node --test requirements/host-boundary/tests/reconcile-idle-early.test.mjs` |
| HOST-BOUNDARY-006 | `tests/session-snapshot-locality.test.mjs` `HOST-004 keeps failed session tool state consistent across Parts and ToolParts`（failed 不进 ToolCall） | MOVE | `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-007 | `tests/host-capability-observation.test.mjs` `HOST_006_prevention_requires_compaction_settings_off_and_autocontinue_off` / `HOST_006_first_turn_probe_is_the_only_startup_verdict` / `HOST_006_containment_folds_observation_and_reanchors_newest_unhandled_once` | NEW | `node --test requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-008 | REUSE `tests/unit/review/*`（seal 绑定）+ `archive/docs/proof/host.md` canary（`ReviewVerdictRecorded.ProviderRun == ProviderInputSealed.ProviderRun` journal 代理等式）；REUSE `requirements/participant-identity/tests/session-execution-binding.test.mjs`（发送边界拒绝漂移） | REUSE | `node --test requirements/participant-identity/tests/session-execution-binding.test.mjs` |
| HOST-BOUNDARY-009 | REUSE `requirements/host-boundary/tests/tool-host-codec.test.mjs`（HOST-011：ToolContext 无 user message id、双半边）；`tests/session-snapshot-locality.test.mjs` `TODO-004 rejects a call id observed in more than one persisted ToolPart`（缺一半边 → Ambiguous） | REUSE + MOVE | `node --test requirements/host-boundary/tests/tool-host-codec.test.mjs` / `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-010 | REUSE `requirements/host-boundary/tests/shared-state.test.mjs` `SHARED_dictionaries_are_live_singletons_shared_across_importers` / `SHARED_root_workspace_atom_round_trips_and_restores`（HOST-012 共享面；`SHARED_pending_seal_record...` 归 review-assurance） | REUSE | `node --test requirements/host-boundary/tests/shared-state.test.mjs` |
| HOST-BOUNDARY-011 | `tests/host-message-projection.test.mjs`（HOST_016 全 7 锚点：reasoning/thinking/ellipsis/hash/untouched/sanitizeMessages） | MOVE | `node --test requirements/host-boundary/tests/host-message-projection.test.mjs` |
| HOST-BOUNDARY-012 | `tests/session-snapshot-locality.test.mjs` `TODO-004 resolves a tool callback through its persisted assistant run and Host ToolPart` / `TODO-004 rejects a call id observed in more than one persisted ToolPart` | MOVE | `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-013 | `tests/needhelp-sensor.test.mjs`（HOST_027 全 5 锚点：sentinel strip / codec 关联 / 跨碎片触发 / case-variant+visible-text 不触发 / 每 run 一次） | MOVE | `node --test requirements/host-boundary/tests/needhelp-sensor.test.mjs` |
| HOST-BOUNDARY-014 | REUSE `requirements/host-boundary/tests/host-hooks.test.mjs`（仅现有 Hook；无 Host patch 路径）；`scripts/checks/architecture.mjs`（`host-boundary` gate：Kernel/Domain 禁 Fable.Core.JsInterop） | REUSE | `node --test requirements/host-boundary/tests/host-hooks.test.mjs` / `node scripts/checks/architecture.mjs` |
| HOST-BOUNDARY-015 | REUSE `requirements/host-boundary/tests/tool-result-bound.test.mjs`（ARCH-012 有界留尾） | REUSE | `node --test requirements/host-boundary/tests/tool-result-bound.test.mjs` |
| HOST-BOUNDARY-016 | `tests/events-port.test.mjs`（EVT 全 5 锚点：同 run 去重 / 无 run 不去重 / failed+aborted 不去重 / sticky replay / disposal） | MOVE | `node --test requirements/host-boundary/tests/events-port.test.mjs` |
| HOST-BOUNDARY-017 | `tests/host-session-context.test.mjs`（HOST_CTX 全 6 锚点：read 提取 + roleOf 解析/alias 拒绝）；REUSE `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` `MACFG_applyOwnedFields_writes_owned_keys_and_never_touches_model`（external_directory 归属字段） | MOVE + REUSE | `node --test requirements/host-boundary/tests/host-session-context.test.mjs` / `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` |
| HOST-BOUNDARY-018 | REUSE `requirements/host-boundary/tests/host-hooks.test.mjs`（ARCH-003 只用现有 Hook/SDK） | REUSE | `node --test requirements/host-boundary/tests/host-hooks.test.mjs` |
| HOST-BOUNDARY-019 | 本包全部 canary（MOVE/NEW 表）；`tests/host-capability-observation.test.mjs`（HostContractUnsupported 显式失败）；REUSE `archive/docs/proof/host.md` membrane canary 清单（H/A/C 未落地 → GAP，见下） | NEW + REUSE | 见各行 |
| HOST-BOUNDARY-020 | `tests/session-snapshot-locality.test.mjs`（Ambiguous）、`tests/host001-fragment-events.test.mjs`（codec 丢弃）、`tests/needhelp-sensor.test.mjs`（armed 唯一） | MOVE | 见各行 |

## GAP 记录

- 聚合台账见 `requirements/GAP.md`（GAP-007/008）。
- `HOST-BOUNDARY-008` 的 HOST-010 因果读 canary（`archive/docs/proof/host.md`「绑定与身份」）目前主要靠
  review 家族 + journal 代理等式（REUSE），transform 内存 id ≡ ToolContext.messageID 的共时等价
  由 e2e canary 承担（不在 unit 范围）——GAP 标记为「e2e 承担」，cutover 时若 e2e 不迁移则需补
  unit oracle。
- `HOST-BOUNDARY-019` 的 Magic Todo membrane canaries（`archive/docs/proof/host.md` A..R）尚未落地实现
  （`tests/unit/host/magic-todo-membrane-canary*.test.mjs` 不存在）——GAP：release gate 清单，
  由 obligation-ledger 团队 + host-boundary 的 H（定位）/A（时序）/C（原地 mutation）在实现后补。

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
6. `requirements/crash-reconciliation/tests/session-quiescence-gate.test.mjs`：整体归 causal-wait（QuiescencePermit）。
7. `tests/unit/host/pair-thought-*.test.mjs`：归 prefix-stability / provider-projection（HOST-013）。
8. `requirements/review-assurance/tests/review-guard.test.mjs`：归 review-assurance。
9. `tests/unit/verify/` 目录：host001-fragment-events / host012-tool-part 已 MOVE；其余 verify
   文件与其它包交叉。
