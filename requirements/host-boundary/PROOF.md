# PROOF — host-boundary（测试落点表）

每条 WHAT 命题恰好一行。类型：`MOVE`（本包 tests/ 物理拥有）/ `REUSE`（留在原处，记精确锚点 +
cutover 计划）/ `NEW`（本包新写）。运行命令均为 `node --test <file>`。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| HOST-BOUNDARY-001 | `tests/host001-fragment-events.test.mjs` `HOST_001_fragment_events_die_at_earliest_boundary`（fragment 在 codec 边界丢弃） | MOVE | `node --test requirements/host-boundary/tests/host001-fragment-events.test.mjs` |
| HOST-BOUNDARY-002 | `tests/host001-fragment-events.test.mjs` `HOST_001_only_coarse_session_lifecycle_signals_cross_the_boundary`（`session.status(idle)` / `session.idle` → 同一 `SessionIdle`，及 retry/deleted/aborted 分型）；REUSE `requirements/host-boundary/tests/signals.test.mjs`（两种 idle encoding + `MessageAbortedError`/`AbortError` → typed signal） | MOVE + REUSE | 同上 / `node --test requirements/host-boundary/tests/signals.test.mjs` |
| HOST-BOUNDARY-003 | `tests/host-capability-observation.test.mjs` `HOST_003_host_signal_is_a_typed_wake_never_a_fact_carrier`（RetrySignal 诊断字段，无 message id）；`tests/host001-fragment-events.test.mjs`（typed decode） | NEW + MOVE | `node --test requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-004 | REUSE `requirements/structured-workflow/tests/reconcile-program.test.mjs`（`TurnUnknown` 私有观测）+ `requirements/host-boundary/tests/signals.test.mjs` | REUSE | `node --test requirements/structured-workflow/tests/reconcile-program.test.mjs` / `requirements/host-boundary/tests/signals.test.mjs` |
| HOST-BOUNDARY-005 | REUSE `requirements/host-boundary/tests/reconcile-idle-early.test.mjs`（因果重读有界；首个早到 idle 耗尽后，第二个真实 coarse signal 可重新 Kick 并恢复 terminal）；REUSE `requirements/structured-workflow/tests/reconcile-program.test.mjs` | REUSE | `node --test requirements/host-boundary/tests/reconcile-idle-early.test.mjs` |
| HOST-BOUNDARY-006 | `tests/session-snapshot-locality.test.mjs` `HOST-004 keeps failed session tool state consistent across Parts and ToolParts`（failed 不进 ToolCall） | MOVE | `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-007 | `tests/host-capability-observation.test.mjs` `HOST_006_prevention_requires_compaction_settings_off_and_autocontinue_off` / `HOST_006_first_turn_probe_is_the_only_startup_verdict` / `HOST_006_containment_folds_observation_and_reanchors_newest_unhandled_once` | NEW | `node --test requirements/host-boundary/tests/host-capability-observation.test.mjs` |
| HOST-BOUNDARY-008 | NEW `tests/host010-run-id-equivalence.test.mjs`（bindableRun id ≡ ToolContext.messageID encoding；0/≥2 无合法 run id）；REUSE `requirements/review-assurance/tests/seal-bind.test.mjs`（四条件 fail-closed）；REUSE `requirements/host-boundary/tests/session-execution-binding.test.mjs`（发送边界拒绝漂移）；共时 Host 穿线仍由 Long Stroke 物理契约承担 | NEW + REUSE | `node --test requirements/host-boundary/tests/host010-run-id-equivalence.test.mjs` |
| HOST-BOUNDARY-009 | REUSE `requirements/host-boundary/tests/tool-host-codec.test.mjs`（HOST-011：ToolContext 无 user message id、双半边）；`tests/session-snapshot-locality.test.mjs` `TODO-004 rejects a call id observed in more than one persisted ToolPart`（缺一半边 → Ambiguous） | REUSE + MOVE | `node --test requirements/host-boundary/tests/tool-host-codec.test.mjs` / `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-010 | REUSE `requirements/host-boundary/tests/shared-state.test.mjs` `SHARED_dictionaries_are_live_singletons_shared_across_importers` / `SHARED_root_workspace_atom_round_trips_and_restores`（含 `ReviewGuardNudges`）；REUSE `requirements/review-assurance/tests/review-guard.test.mjs` `RVGD_nudgeReviewer_cross_instance_reservation_suppresses_twin_send`（guard key 禁 RuntimeId，防 ReviewerVerdictRequired 双发） | REUSE | `node --test requirements/host-boundary/tests/shared-state.test.mjs`；`node --test requirements/review-assurance/tests/review-guard.test.mjs` |
| HOST-BOUNDARY-011 | `tests/host-message-projection.test.mjs`（HOST_016 全 7 锚点：reasoning/thinking/ellipsis/hash/untouched/sanitizeMessages） | MOVE | `node --test requirements/host-boundary/tests/host-message-projection.test.mjs` |
| HOST-BOUNDARY-012 | `tests/session-snapshot-locality.test.mjs` `TODO-004 resolves a tool callback through its persisted assistant run and Host ToolPart` / `TODO-004 rejects a call id observed in more than one persisted ToolPart` | MOVE | `node --test requirements/host-boundary/tests/session-snapshot-locality.test.mjs` |
| HOST-BOUNDARY-013 | `tests/needhelp-sensor.test.mjs`（HOST_027 全 5 锚点：sentinel strip / codec 关联 / 跨碎片触发 / case-variant+visible-text 不触发 / 每 run 一次） | MOVE | `node --test requirements/host-boundary/tests/needhelp-sensor.test.mjs` |
| HOST-BOUNDARY-014 | REUSE `requirements/host-boundary/tests/host-hooks.test.mjs`（仅现有 Hook；无 Host patch 路径）；`scripts/checks/architecture.mjs`（`host-boundary` gate：Kernel/Domain 禁 Fable.Core.JsInterop） | REUSE | `node --test requirements/host-boundary/tests/host-hooks.test.mjs` / `node scripts/checks/architecture.mjs` |
| HOST-BOUNDARY-015 | REUSE `requirements/host-boundary/tests/tool-result-bound.test.mjs`（ARCH-012 有界留尾） | REUSE | `node --test requirements/host-boundary/tests/tool-result-bound.test.mjs` |
| HOST-BOUNDARY-016 | `tests/events-port.test.mjs`（EVT 全 5 锚点：同 run 去重 / 无 run 不去重 / failed+aborted 不去重 / sticky replay / disposal） | MOVE | `node --test requirements/host-boundary/tests/events-port.test.mjs` |
| HOST-BOUNDARY-017 | `tests/host-session-context.test.mjs`（HOST_CTX 全 6 锚点：read 提取 + roleOf 解析/alias 拒绝）；REUSE `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` `MACFG_applyOwnedFields_writes_owned_keys_and_never_touches_model`（external_directory 归属字段） | MOVE + REUSE | `node --test requirements/host-boundary/tests/host-session-context.test.mjs` / `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` |
| HOST-BOUNDARY-018 | REUSE `requirements/host-boundary/tests/host-hooks.test.mjs`（ARCH-003 只用现有 Hook/SDK） | REUSE | `node --test requirements/host-boundary/tests/host-hooks.test.mjs` |
| HOST-BOUNDARY-019 | 本包全部 canary（MOVE/NEW 表）；`tests/host-capability-observation.test.mjs`（HostContractUnsupported 显式失败）；REUSE 本文件 Magic Todo membrane canary 清单（H/A/C 未落地 → GAP，见下） | NEW + REUSE | 见各行 |
| HOST-BOUNDARY-020 | `tests/session-snapshot-locality.test.mjs`（Ambiguous）、`tests/host001-fragment-events.test.mjs`（codec 丢弃）、`tests/needhelp-sensor.test.mjs`（armed 唯一） | MOVE | 见各行 |
| HOST-BOUNDARY-021 | `tests/plugin-load-purity.test.mjs`：plugin load graph 禁 Host session API/recovery/workspace mutation；ordinary join 只认 current-process handle/Fission binding；Open Fission/Assistance/JS pending state 保持 broken，不由普通入口恢复 | NEW | `node --test requirements/host-boundary/tests/plugin-load-purity.test.mjs` |

## GAP 记录

- 聚合台账见 `requirements/GAP.md`（GAP-007 CLOSED / GAP-008 OPEN）。
- `HOST-BOUNDARY-008`：unit encoding 由 `tests/host010-run-id-equivalence.test.mjs` 承接
  （bindableRun id ≡ ToolContext.messageID；0/≥2 无合法 run id）。共时 Host 穿线是不可模拟
  physical contract，由 Long Stroke 入口声明（VERIFICATION-SYSTEM-003），不另立 unit GAP。
- `HOST-BOUNDARY-019` 的 Magic Todo membrane canaries（下表 A..R）尚未落地实现（尚无
  production membrane 或对应 canary 文件）——GAP-008：release gate 清单，由 obligation-ledger
  团队 + host-boundary 的 H（定位）/A（时序）/C（原地 mutation）在实现后补。

## Magic Todo V1 membrane canaries（A..R，release-gate 清单）

2026-08-14 cutover 自旧 proof 归档迁入。语义交叉：`obligation-ledger`（TODO-002..013）与
`host-boundary`（HOST-017..025，历史编号）。**任一 blocking canary 未证明 → 禁止写
production membrane；禁止改 Host core 绕过。**

| ID | 证明 | 期望 | 条款 | 级别 |
|----|------|------|------|------|
| A | deferred materialization + before 原地 mutation 达 executor | before 不等待 snapshot/Journal IO；`pending + {}` 在 deferred prepare 中等待，同一 physical ToolPart materialize 后 canonical input == captured live args，digest 取 materialized input；executor 见 V1 compatibility list；after 必须 await prepare 才可 Accepted | HOST-019 | **blocking** |
| B | 同时替换 parameters + jsonSchema | provider 见 V2；原 executor 仍跑 V1 decoder | HOST-018 | blocking |
| C | non-enumerable compatibility view | 原 V1 decoder 可读 `todos`；`Object.keys`/`JSON.stringify`/Host persistence 仍只见 provider `obligations` | HOST-019/020 | blocking |
| D | `status="reviewing"` 经 TodoTable → todo.updated → API → TUI | 全容忍 → passthrough；否则冻结 sink→`in_progress` | HOST-023、TODO-003 | blocking（策略冻结） |
| E | after 改写 `output.output` | 本次模型可见 ∧ 下一 provider history **同字节** | HOST-021、TODO-005/013 | blocking |
| F | execute throw | 记录 after 是否运行；协议不依赖其运行 | HOST-021 | 冻结观测 |
| G | after 运行瞬间 | 冻结 ToolPart 是否已 durable completed；Accepted 仍走双路径 | HOST-022、TODO-004 | blocking（防误绑） |
| H | 仅 sessionID+callID | 完整 SDK snapshot **唯一**定位 ToolPart / assistant / run / ordinal / XTrace range | HOST-025、HOST-011 | **blocking** |
| I | 第五态消费者回归 | 承接 D；UI 不稳则强制 compatibility `in_progress` | HOST-023 | blocking if D flaky |
| J | live Accepted | executor 成功→after → `TodoWriteAccepted` 与 Prepared digest 对齐 | HOST-022 | blocking |
| K | recovery Accepted | 无 after 时 snapshot completed ToolPart → 同一 digests Accepted | HOST-022、TODO-012 | blocking |
| L | Prepared+失败 | 不 Accepted；sink 乐观 Pk 不构成 checkpoint；下次 before Journal 覆盖 sink | HOST-022、TODO-007 | blocking |
| M | REVISE 消费后 reconcile | Host TodoTable == settled current；**零**新 checkpoint/review facts | HOST-023、TODO-005/007 | blocking |
| N | V2 runner | 无 hook parity 时 MagicTodo Manager Attempt **construction fail closed**；零裸 `SessionTodo.update` | HOST-024、TODO-004 | **blocking** |
| O | 无 Host core / 同名覆盖 | builtin executor 仍为 sink；无 OpenCode 源码修改；无 plugin 同名 tool 夺权 | HOST-017 | 静态/集成 |
| P | bridge 非真相 | crash 后忽略 Map；只从 Journal 恢复；failure cleanup 无残留 key | HOST-021、TODO-012 | blocking |
| Q | description 面 | 含 tagged/reviewing/lag/multi-reject；**不含** reviewer/session/barrier/witness/2N | HOST-018、TODO-013 | 静态 |
| R | multi-todowrite | 同 assistant message 两个不同 callID → 全部拒绝、无 winner | HOST-020、TODO-004 | blocking |

代表落点（实现后）：`requirements/host-boundary/tests/magic-todo-membrane-canary*.test.mjs`、
integration plugin hook 契约、e2e Manager todowrite unhappy-path。未落地前以本表为
release gate 清单（对齐历史 magic-todo change §47 Host canary 门禁）。

### 反例（必须红）

```text
before 等待 snapshot/Journal 导致 executor 被 IO 阻塞        → A 红
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
6. `requirements/crash-reconciliation/tests/session-quiescence-gate.test.mjs`：整体归 causal-wait（QuiescencePermit）。
7. `tests/unit/host/pair-thought-*.test.mjs`：归 prefix-stability / provider-projection（HOST-013）。
8. `requirements/review-assurance/tests/review-guard.test.mjs`：归 review-assurance。
9. `tests/unit/verify/` 目录：host001-fragment-events / host012-tool-part 已 MOVE；其余 verify
   文件与其它包交叉。
