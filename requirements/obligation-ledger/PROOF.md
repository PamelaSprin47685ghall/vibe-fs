# obligation-ledger — PROOF

行为合同：`WHAT.md`（OBLIGATION-LEDGER-001..026）。实现模型：`HOW.md`。

## 测试资产

### 本包 tests/（`requirements/obligation-ledger/tests/`）

| 文件 | 来源 | 类型 | 断言数 |
|---|---|---|---|
| `magic-todo.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo.test.mjs` | domain 纯函数 | 7 |
| `magic-todo-after.test.mjs` | MOVE + REWRITE | domain 纯函数 + dedicated work-unit static contract | 5 |
| `magic-todo-projection.test.mjs` | MOVE + REWRITE | O(1) fold 代数 + commitment/reviewer reverse locator | 15 |
| `magic-todo-event-store.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs` | EventStore 恢复 | 1 |
| `magic-todo-provider-boundary.test.mjs` | MOVE + REWRITE | static（provider surface / planComplete relation） | 9 |
| `magic-todo-host-codec.test.mjs` | MOVE + REWRITE | codec / definition | 3 |
| `opening-floor.test.mjs` | MOVE + REWRITE | T1 / Opening floor | 6 |
| `prefix-epoch-cutoff.test.mjs` | NEW + REWRITE | committed lag-1 locator | 2 |
| `obligation-ledger-workflow-contract.test.mjs` | NEW | Direct CE / O(1) projection / 无第二 runtime 静态合同 | 3 |

本目录顶层实际为 12 个 test 文件，当前 runner **71/71 GREEN**；其中还包括 `lifecycle-opening.test.mjs`（2）、`magic-todo-host-canaries.test.mjs`（6）与 `magic-todo-membrane.test.mjs`（12）。

### REUSE（留在原处；跨包 SPLIT@cutover）

| 文件 | 锚点 | 本包拥有的断言 | SPLIT@cutover |
|---|---|---|---|
| `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` | `TODO-004*` / `HOST-019*` / `HOST-021*` / `TODO-005*` / `TODO-006*` 11 个 test | admission、materialization、lag-1 等待、REVISE 不回滚、infra fatal 分型 | physical success 分型 → `effect-accounting`；snapshot 定位 → `host-boundary` |
| `requirements/obligation-ledger/tests/integration/plugin/magic-todo-sink-canary.test.mjs` | `HOST_023_canary_D_reviewing_sink_table_event_api_model`、`HOST_023_canary_I_reviewing_fifth_status_consumers_and_sink_freeze` | sink 永不反推 canonical；reviewing 降级 in_progress（compatibility 冻结） | sink 字段形态是 HOW；canonical 侧唯一 owner 在本包 |
| `tests/unit/plugin/magic-todo-host-canaries.test.mjs` | `MAGIC_TODO_CANARY_B_definition_replaces_description_parameters_jsonSchema_original_decoder_unchanged`、`MAGIC_TODO_CANARY_B_definition_jsonSchema_ternary_keeps_schema_when_both_replaced`、`MAGIC_TODO_CANARY_C_obligations_project_to_original_v1_decoder_shape`、`MAGIC_TODO_CANARY_F_after_does_not_run_when_executor_throws`、`MAGIC_TODO_CANARY_F_after_runs_when_executor_succeeds` | definition 三处同步；compatibility 投影；physical-success 才 Accepted | canary A′/H 定位与 carrier → `host-boundary` |
| `requirements/finality/tests/lifecycle.test.mjs` | `GLORY_074_t1_revelation_hook`、`GLORY_010_LifeOpened_opens_the_first_life`、`GLORY_021_WorkActivated_fixes_the_protected_prefix_end_once` | T1 revelation 属 Opening；WorkActivated inert decode | 其余 finality / participant-horizon / provider-language 断言归各自包 |

## 命题 → 落点

| 命题 | 落点测试（文件 + 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| O-1 001 | `tests/magic-todo.test.mjs` `TODO-002 canonical obligation wire...`（doesNotMatch id/status/priority/reviewing）；`tests/magic-todo-provider-boundary.test.mjs` `TODO-003 clean break removes the legacy todo ontology...` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo.test.mjs` |
| O-2 002 | `tests/magic-todo.test.mjs` `TODO-002 canonical obligation wire...`；`tests/magic-todo-host-codec.test.mjs` `TODO-002 decodes the clean-break obligations wire`；`tests/magic-todo-projection.test.mjs` `TODO-005 Accepted supersedes Current immediately...` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs` |
| O-3 003 | `tests/magic-todo-provider-boundary.test.mjs` `TODO-003 clean break...`；`tests/magic-todo.test.mjs` wire doesNotMatch | MOVE | 见 O-1 |
| O-4 004 | `tests/magic-todo-provider-boundary.test.mjs`：Pre-T1 `planComplete=false` 明确允许 concrete planning work；effective true 后才启用 completion-counterfactual mission-debt 纪律；Host 无 planning 关键词分类 | REWRITE | `node --test requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` |
| O-5 005 | 同文件：placeholder/TBD 等无 concrete owed work 的空槽位在 false/true 两侧都非法；具体 planning task 在 false 侧合法 | REWRITE | 见 O-4 |
| O-6 006 | `tests/magic-todo.test.mjs` `TODO-002 rejects blank and duplicate obligation names as call syntax` | MOVE | 见 O-1 |
| O-7 007 | `tests/magic-todo.test.mjs` `TODO-004 rejects different todowrite calls in one assistant message as syntax/protocol error` | MOVE | 见 O-1 |
| O-8 008 | `tests/magic-todo.test.mjs` `TODO-004 pure replay identity checker detects corruption...`；`tests/magic-todo-projection.test.mjs` `TODO-004 rejects Accepted when it names another Prepared envelope`、`TODO-004 rejects a replay whose frozen prepared identity differs` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| O-9 009 | `tests/magic-todo-provider-boundary.test.mjs` `TODO-004 failure triage keeps red for syntax and kills OpenCode on infrastructure faults`；REUSE `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` `TODO-004 missing process-review runtime is infrastructure-fatal, not provider red`；REUSE canaries `MAGIC_TODO_CANARY_F_after_does_not_run_when_executor_throws` | MOVE + REUSE | 见 O-4；membrane 见 SPLIT@cutover |
| O-10 010 | `tests/magic-todo-projection.test.mjs` `TODO-005 Accepted supersedes Current immediately and REVISE conclusion cannot roll it back`；`tests/magic-todo.test.mjs` `TODO-005 fresh admission freezes Base and Submitted without a merge preview`（Prepared 不改 Current；无 RevisePreview） | MOVE | 见 O-8 |
| O-11 011 | `tests/magic-todo-projection.test.mjs` REVISE conclusion cannot roll it back（同上）；`tests/magic-todo-provider-boundary.test.mjs` `TODO-005 production checkpoint path has no reviewer settlement owner`、`TODO-005 provider wording says Accepted becomes Current without reviewer settlement` | MOVE | 见 O-8 / O-4 |
| O-12 012 | `tests/magic-todo-projection.test.mjs` `TODO-006 rejects a conclusion with no matching assignment`；`tests/magic-todo.test.mjs` `TODO-004 replays an identical obligation checkpoint even while its review is outstanding`（replay 不新增 review）；REUSE membrane `TODO-006 T1 accept succeeds then T2 prepare is a lag-1 wait, not a fail-closed Admission` | MOVE + REUSE | 见 O-8 |
| O-13 013 | `tests/magic-todo-projection.test.mjs` `TODO-006 rejects a new prepare until the preceding review concludes`、`TODO-006 treats an exact durable conclusion replay as idempotent`；REUSE membrane `TODO-006 T2 prepare succeeds once T1 process review is Concluded` | MOVE + REUSE | 见 O-8 |
| O-14 014 | `tests/magic-todo-projection.test.mjs` `TODO-012 legacy conclusion locator remains replayable but is not a Current writer`（VerdictKnown→Concluded 两段式；不写 Current）；REUSE membrane `TODO-006 T2 prepare succeeds once T1 process review is Concluded`（ConsumableReview gate） | MOVE + REUSE | 见 O-8 |
| O-15 015 | REUSE `requirements/obligation-ledger/tests/integration/plugin/magic-todo-sink-canary.test.mjs` `HOST_023_canary_D...`、`HOST_023_canary_I...`；`tests/magic-todo-host-codec.test.mjs` `TODO-007 projects obligations into a non-enumerable V1 compatibility view` | MOVE + REUSE | 见 O-2 |
| O-16 016 | `tests/opening-floor.test.mjs`：accepted false checkpoints 仍保持 dynamic Opening；第一次 accepted true 的 call/result 才是 constitutive T1；`tests/magic-todo-projection.test.mjs`：FirstPlanCommitment once-set、true 后 raw false effective 仍 true；provider-boundary：false/true/不可回退文案 | REWRITE + REUSE | `node --test requirements/obligation-ledger/tests/opening-floor.test.mjs requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| O-17 017 | `tests/opening-floor.test.mjs` `AC15 Pre-T1: effectiveOpeningFloor tracks XTrace head...`、`AC15 Pre-T1: no CurrentLife → no floor`、`AC15 static: BloggerCoordinator + CompanionTransform zero ProtectedPrefixEnd refs`；REUSE lifecycle `GLORY_021_WorkActivated_fixes_the_protected_prefix_end_once` | MOVE + REUSE | 见 O-16 |
| O-18 018 | `tests/obligation-ledger-workflow-contract.test.mjs`：Application workflow 是直接 `task {}` / `let!` / `match`，无 Command/Reply/Interpreter/Stage；生产热路径不得通过 Accepted history 或 `ByLife |> Map.tryPick` 全表扫描推导 commitment/finality/opening/cutoff/reviewer authority；`tests/magic-todo-projection.test.mjs`：每次 fold O(1) 更新 FirstPlanCommitment/LatestCommitted/PreviousCommitted，并在 dedicated enlist/replacement 增量维护 ReviewerLifeBySession；`tests/magic-todo-event-store.test.mjs`：Boot Fold 后得到同一 projection | NEW + REWRITE | `node --test requirements/obligation-ledger/tests/obligation-ledger-workflow-contract.test.mjs requirements/obligation-ledger/tests/magic-todo-projection.test.mjs requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs` |
| O-19 019 | `tests/magic-todo-projection.test.mjs` `TODO-011 rejects a legacy seed after the first Magic provider request` | MOVE | 见 O-8 |
| O-20 020 | `tests/magic-todo-after.test.mjs`：dedicated 首个 assignment 使用 OwnerRoot、同 checkpoint 重试 AwaitHead、后续 checkpoint Continuation；新增 static/behavior proof：新 assignment 复用 logical reviewer 时允许为已 Retired 的旧 work-unit 重新 link Active，但 checkpoint 已 Assigned 后不得复活 handle；`tests/magic-todo-projection.test.mjs`：assignment 前必须已有 dedicated enlistment | REWRITE | `node --test requirements/obligation-ledger/tests/magic-todo-after.test.mjs` |
| O-21 021 | `tests/prefix-epoch-cutoff.test.mjs`：false planning checkpoints 不产生 committed predecessor；T1 无 prior；T1 后每次 Accepted（即使 raw false）使用 O(1) PreviousCommitted locator；EvidenceKind 仍为 TodoCheckpoint | REWRITE | `node --test requirements/obligation-ledger/tests/prefix-epoch-cutoff.test.mjs` |
| O-22 022 | `tests/magic-todo.test.mjs` `OBLIGATION-LEDGER-022 blocks Finality until plan commitment, not merely until any checkpoint`（false planning checkpoint 不授予 Finality 资格；drain 执行见 `requirements/finality/PROOF.md`） | REWRITE | 见 O-1 |
| O-23 023 | `tests/magic-todo-provider-boundary.test.mjs` `TODO-005 provider wording says Accepted becomes Current without reviewer settlement`（含 `manager-guideline/en.md`、`zh-CN.md` 断言） | MOVE | 见 O-4 |
| O-24 024 | `tests/magic-todo-host-codec.test.mjs` `TODO-002 replaces description, parameters, and jsonSchema with obligations`；REUSE canaries `MAGIC_TODO_CANARY_B_definition_replaces_description_parameters_jsonSchema_original_decoder_unchanged`、`MAGIC_TODO_CANARY_B_definition_jsonSchema_ternary_keeps_schema_when_both_replaced` | MOVE + REUSE | 见 O-2 |
| O-25 025 | REUSE membrane `HOST-019 before returns without waiting for snapshot or Journal IO`、`HOST-019 prepare rejects a pending ToolPart whose provider input is still empty`、`HOST-019 before materializes the exact provider input`、`HOST-019 materialization fails closed when the provider input differs`、`HOST-019 materialized snapshot input must still match tool.execute.before args` | REUSE | membrane SPLIT@cutover |
| O-26 026 | REUSE membrane `TODO-005 REVISE is feedback only: next checkpoint sees the report and Current never rolls back`、`HOST-021 snapshot infrastructure failure takes the process-fatal path, never a todowrite red path`；REUSE canaries `MAGIC_TODO_CANARY_F_after_runs_when_executor_succeeds` | REUSE | membrane SPLIT@cutover |

## 覆盖统计

- 命题 26 / 落点 26；本次是同包语义重构，不新增 phase/status 命题。
- GAP：0。O-16 monotone commitment、O-18 Direct CE/O(1) projection/reverse locator 均已有 RED→GREEN proof。
- 本包顶层 12 个 test 文件，当前 **71/71 GREEN**；另有 sink integration 2/2 与 review-assurance 的 consumable-review 9/9 交叉证明。
- REUSE 文件的 cutover 拆分：membrane（effect-accounting / host-boundary）、host-canaries（host-boundary）、sink（HOW）、lifecycle（finality / participant-horizon / provider-language）——见上表 SPLIT@cutover。

## semantic anchor id（semantic-anchors.mjs，MECHANISM 逐 ID 归包）

本包声明拥有 `scripts/checks/semantic-anchors.mjs` 中 manager 角色的下列 anchor id
（`ROLE_SEMANTIC_ANCHORS.manager`；机制文件在 cutover 时按此声明标注 owner）：

- `obligations` —— Manager 义务账词汇（OBLIGATION-LEDGER-001/002）
- `planning-table-or-entrusted` —— BlindPlan Pre-T1 Planning Table / T1 entrustment（OBLIGATION-LEDGER-016/017）
