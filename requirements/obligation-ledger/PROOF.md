# obligation-ledger — PROOF

行为合同：`WHAT.md`（OBLIGATION-LEDGER-001..026）。实现模型：`HOW.md`。

## 测试资产

### 本包 tests/（`requirements/obligation-ledger/tests/`）

| 文件 | 来源 | 类型 | 断言数 |
|---|---|---|---|
| `magic-todo.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo.test.mjs` | domain 纯函数 | 7 |
| `magic-todo-after.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo-after.test.mjs` | domain 纯函数 + static | 4 |
| `magic-todo-projection.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` | fold 代数 | 12 |
| `magic-todo-event-store.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs` | EventStore 恢复 | 1 |
| `magic-todo-provider-boundary.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` | static（provider surface / 源码） | 8 |
| `magic-todo-host-codec.test.mjs` | MOVE `requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs` | codec / definition | 3 |
| `opening-floor.test.mjs` | MOVE `requirements/obligation-ledger/tests/opening-floor.test.mjs` | T1 / Opening floor | 6 |
| `prefix-epoch-cutoff.test.mjs` | NEW | desired cutoff 纯推导 | 2 |

合计 43 断言；每个文件 `node --test` 单独跑绿。

### REUSE（留在原处；跨包 SPLIT@cutover）

| 文件 | 锚点 | 本包拥有的断言 | SPLIT@cutover |
|---|---|---|---|
| `tests/unit/reconciliation/magic-todo-membrane.test.mjs` | `TODO-004*` / `HOST-019*` / `HOST-021*` / `TODO-005*` / `TODO-006*` 11 个 test | admission、materialization、lag-1 等待、REVISE 不回滚、infra fatal 分型 | physical success 分型 → `effect-accounting`；snapshot 定位 → `host-boundary` |
| `tests/integration/plugin/magic-todo-sink-canary.test.mjs` | `HOST_023_canary_D_reviewing_sink_table_event_api_model`、`HOST_023_canary_I_reviewing_fifth_status_consumers_and_sink_freeze` | sink 永不反推 canonical；reviewing 降级 in_progress（compatibility 冻结） | sink 字段形态是 HOW；canonical 侧唯一 owner 在本包 |
| `tests/unit/plugin/magic-todo-host-canaries.test.mjs` | `MAGIC_TODO_CANARY_B_definition_replaces_description_parameters_jsonSchema_original_decoder_unchanged`、`MAGIC_TODO_CANARY_B_definition_jsonSchema_ternary_keeps_schema_when_both_replaced`、`MAGIC_TODO_CANARY_C_obligations_project_to_original_v1_decoder_shape`、`MAGIC_TODO_CANARY_F_after_does_not_run_when_executor_throws`、`MAGIC_TODO_CANARY_F_after_runs_when_executor_succeeds` | definition 三处同步；compatibility 投影；physical-success 才 Accepted | canary A′/H 定位与 carrier → `host-boundary` |
| `tests/unit/glory/lifecycle.test.mjs` | `GLORY_074_t1_revelation_hook`、`GLORY_010_LifeOpened_opens_the_first_life`、`GLORY_021_WorkActivated_fixes_the_protected_prefix_end_once` | T1 revelation 属 Opening；WorkActivated inert decode | 其余 finality / participant-horizon / provider-language 断言归各自包 |

## 命题 → 落点

| 命题 | 落点测试（文件 + 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| O-1 001 | `tests/magic-todo.test.mjs` `TODO-002 canonical obligation wire...`（doesNotMatch id/status/priority/reviewing）；`tests/magic-todo-provider-boundary.test.mjs` `TODO-003 clean break removes the legacy todo ontology...` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo.test.mjs` |
| O-2 002 | `tests/magic-todo.test.mjs` `TODO-002 canonical obligation wire...`；`tests/magic-todo-host-codec.test.mjs` `TODO-002 decodes the clean-break obligations wire`；`tests/magic-todo-projection.test.mjs` `TODO-005 Accepted supersedes Current immediately...` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo-host-codec.test.mjs` |
| O-3 003 | `tests/magic-todo-provider-boundary.test.mjs` `TODO-003 clean break...`；`tests/magic-todo.test.mjs` wire doesNotMatch | MOVE | 见 O-1 |
| O-4 004 | `tests/magic-todo-provider-boundary.test.mjs` `TODO-002 Manager Role Law rejects meta-work without owning tool timing`、`TODO-002 disguised investigative meta-todo is explicitly rejected before first checkpoint` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs` |
| O-5 005 | `tests/magic-todo-provider-boundary.test.mjs` `TODO-002 placeholder obligation is rejected by handoff completeness, not just meta-work wording`（含 Host 不得关键词分类静态断言） | MOVE | 见 O-4 |
| O-6 006 | `tests/magic-todo.test.mjs` `TODO-002 rejects blank and duplicate obligation names as call syntax` | MOVE | 见 O-1 |
| O-7 007 | `tests/magic-todo.test.mjs` `TODO-004 rejects different todowrite calls in one assistant message as syntax/protocol error` | MOVE | 见 O-1 |
| O-8 008 | `tests/magic-todo.test.mjs` `TODO-004 pure replay identity checker detects corruption...`；`tests/magic-todo-projection.test.mjs` `TODO-004 rejects Accepted when it names another Prepared envelope`、`TODO-004 rejects a replay whose frozen prepared identity differs` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo-projection.test.mjs` |
| O-9 009 | `tests/magic-todo-provider-boundary.test.mjs` `TODO-004 failure triage keeps red for syntax and kills OpenCode on infrastructure faults`；REUSE `tests/unit/reconciliation/magic-todo-membrane.test.mjs` `TODO-004 missing process-review runtime is infrastructure-fatal, not provider red`；REUSE canaries `MAGIC_TODO_CANARY_F_after_does_not_run_when_executor_throws` | MOVE + REUSE | 见 O-4；membrane 见 SPLIT@cutover |
| O-10 010 | `tests/magic-todo-projection.test.mjs` `TODO-005 Accepted supersedes Current immediately and REVISE conclusion cannot roll it back`；`tests/magic-todo.test.mjs` `TODO-005 fresh admission freezes Base and Submitted without a merge preview`（Prepared 不改 Current；无 RevisePreview） | MOVE | 见 O-8 |
| O-11 011 | `tests/magic-todo-projection.test.mjs` REVISE conclusion cannot roll it back（同上）；`tests/magic-todo-provider-boundary.test.mjs` `TODO-005 production checkpoint path has no reviewer settlement owner`、`TODO-005 provider wording says Accepted becomes Current without reviewer settlement` | MOVE | 见 O-8 / O-4 |
| O-12 012 | `tests/magic-todo-projection.test.mjs` `TODO-006 rejects a conclusion with no matching assignment`；`tests/magic-todo.test.mjs` `TODO-004 replays an identical obligation checkpoint even while its review is outstanding`（replay 不新增 review）；REUSE membrane `TODO-006 T1 accept succeeds then T2 prepare is a lag-1 wait, not a fail-closed Admission` | MOVE + REUSE | 见 O-8 |
| O-13 013 | `tests/magic-todo-projection.test.mjs` `TODO-006 rejects a new prepare until the preceding review concludes`、`TODO-006 treats an exact durable conclusion replay as idempotent`；REUSE membrane `TODO-006 T2 prepare succeeds once T1 process review is Concluded` | MOVE + REUSE | 见 O-8 |
| O-14 014 | `tests/magic-todo-projection.test.mjs` `TODO-012 legacy conclusion locator remains replayable but is not a Current writer`（VerdictKnown→Concluded 两段式；不写 Current）；REUSE membrane `TODO-006 T2 prepare succeeds once T1 process review is Concluded`（ConsumableReview gate） | MOVE + REUSE | 见 O-8 |
| O-15 015 | REUSE `tests/integration/plugin/magic-todo-sink-canary.test.mjs` `HOST_023_canary_D...`、`HOST_023_canary_I...`；`tests/magic-todo-host-codec.test.mjs` `TODO-007 projects obligations into a non-enumerable V1 compatibility view` | MOVE + REUSE | 见 O-2 |
| O-16 016 | `tests/opening-floor.test.mjs` `AC15/AC16 Post-T1: WorkRecordStart nails after T1 call+result`、`AC16: T1 constitutive body renders in Opening, not Recent`、`AC16: XTrace.forOpening keeps T1 tools...`；REUSE `tests/unit/glory/lifecycle.test.mjs` `GLORY_074_t1_revelation_hook`；`tests/magic-todo-provider-boundary.test.mjs` `TODO-002/TODO-015 first todowrite is a finished mission account...` | MOVE + REUSE | `node --test requirements/obligation-ledger/tests/opening-floor.test.mjs` |
| O-17 017 | `tests/opening-floor.test.mjs` `AC15 Pre-T1: effectiveOpeningFloor tracks XTrace head...`、`AC15 Pre-T1: no CurrentLife → no floor`、`AC15 static: BloggerCoordinator + CompanionTransform zero ProtectedPrefixEnd refs`；REUSE lifecycle `GLORY_021_WorkActivated_fixes_the_protected_prefix_end_once` | MOVE + REUSE | 见 O-16 |
| O-18 018 | `tests/magic-todo-event-store.test.mjs` `TODO-012 persists typed prepared identity through AgentJournal and EventStore boot`（恢复只从 durable facts）；`tests/magic-todo-projection.test.mjs` `TODO-012 folds a typed Magic Todo envelope into the one canonical projection`、`TODO-012 rejects forward Magic Todo payloads without throwing through boot fold`、`TODO-012 stores typed Magic Todo bytes in the canonical Fact envelope` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs` |
| O-19 019 | `tests/magic-todo-projection.test.mjs` `TODO-011 rejects a legacy seed after the first Magic provider request` | MOVE | 见 O-8 |
| O-20 020 | `tests/magic-todo-after.test.mjs` 全部 4 个 test（T1 AgentOwnerRoot / 重试 AwaitHead / T2+ Continuation / 二次 fork 静态拒绝）；`tests/magic-todo-projection.test.mjs` `TODO-008 rejects process assignment before dedicated enlistment` | MOVE | `node --test requirements/obligation-ledger/tests/magic-todo-after.test.mjs` |
| O-21 021 | `tests/prefix-epoch-cutoff.test.mjs`（NEW）`OBLIGATION-LEDGER-021 desired cutoff is the previous Accepted checkpoint...`、`...the rebase evidence kind is TodoCheckpoint` | NEW | `node --test requirements/obligation-ledger/tests/prefix-epoch-cutoff.test.mjs` |
| O-22 022 | `tests/magic-todo.test.mjs` `TODO-014 blocks first unblessed suicide without an accepted checkpoint`（账本侧 fail-closed；drain 执行见 `requirements/finality/PROOF.md`） | MOVE | 见 O-1 |
| O-23 023 | `tests/magic-todo-provider-boundary.test.mjs` `TODO-005 provider wording says Accepted becomes Current without reviewer settlement`（含 `manager-guideline/en.md`、`zh-CN.md` 断言） | MOVE | 见 O-4 |
| O-24 024 | `tests/magic-todo-host-codec.test.mjs` `TODO-002 replaces description, parameters, and jsonSchema with obligations`；REUSE canaries `MAGIC_TODO_CANARY_B_definition_replaces_description_parameters_jsonSchema_original_decoder_unchanged`、`MAGIC_TODO_CANARY_B_definition_jsonSchema_ternary_keeps_schema_when_both_replaced` | MOVE + REUSE | 见 O-2 |
| O-25 025 | REUSE membrane `HOST-019 before returns without waiting for snapshot or Journal IO`、`HOST-019 prepare rejects a pending ToolPart whose provider input is still empty`、`HOST-019 before materializes the exact provider input`、`HOST-019 materialization fails closed when the provider input differs`、`HOST-019 materialized snapshot input must still match tool.execute.before args` | REUSE | membrane SPLIT@cutover |
| O-26 026 | REUSE membrane `TODO-005 REVISE is feedback only: next checkpoint sees the report and Current never rolls back`、`HOST-021 snapshot infrastructure failure takes the process-fatal path, never a todowrite red path`；REUSE canaries `MAGIC_TODO_CANARY_F_after_runs_when_executor_succeeds` | REUSE | membrane SPLIT@cutover |

## 覆盖统计

- 命题 26 / 落点 26（MOVE 19 行、REUSE 7 行、NEW 1 行——同一命题可多落点，此处按「含 NEW/REUSE 的命题数」计）。
- GAP：0。
- 移动文件：8 个（7 MOVE + 1 NEW），每个 `node --test` 单独跑绿（共 43 断言）。
- REUSE 文件的 cutover 拆分：membrane（effect-accounting / host-boundary）、host-canaries（host-boundary）、sink（HOW）、lifecycle（finality / participant-horizon / provider-language）——见上表 SPLIT@cutover。

## semantic anchor id（semantic-anchors.mjs，MECHANISM 逐 ID 归包）

本包声明拥有 `scripts/checks/semantic-anchors.mjs` 中 manager 角色的下列 anchor id
（`ROLE_SEMANTIC_ANCHORS.manager`；机制文件在 cutover 时按此声明标注 owner）：

- `obligations` —— Manager 义务账词汇（OBLIGATION-LEDGER-001/002）
- `planning-table-or-entrusted` —— BlindPlan Pre-T1 Planning Table / T1 entrustment（OBLIGATION-LEDGER-016/017）
