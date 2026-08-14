# PROOF —— 测试落点表

每条 WHAT 命题恰好一行。类型：`MOVE` / `REUSE`（留在原处，记断言锚点 + SPLIT@cutover）/ `NEW`。

运行：`node --test requirements/action-affordance/tests/<file>`；全量由 `node requirements/verification-system/tests/run.mjs` 并入。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| 001 | `tests/action-affordance.test.mjs::AA_prompt_020_tool_descriptions_carry_contract_anchors_in_both_locales`（五问经 anchor 全覆盖） | NEW | `node --test requirements/action-affordance/tests/action-affordance.test.mjs` |
| 002 | `tests/action-affordance.test.mjs::AA_prompt_020_high_risk_verbs_have_semantic_anchor_catalog`；REUSE：`requirements/action-affordance/tests/tool-description-anchors.test.mjs::gate_c_tool_description_anchor_catalog_requires_high_risk_verbs`（anchor-parity 机制面归 `provider-language`） | NEW + REUSE | 同上 / `node --test requirements/action-affordance/tests/tool-description-anchors.test.mjs` |
| 003 | `tests/action-affordance.test.mjs::AA_prompt_020_inspect_contract_names_the_not_performed_act` | NEW | 同上 |
| 004 | `tests/action-affordance.test.mjs::AA_prompt_020_repair_behavior_contract_defines_mechanical` | NEW | 同上 |
| 005 | `tests/action-affordance.test.mjs::AA_prompt_020_establish_behavior_contract_separates_mutation_from_execution` | NEW | 同上 |
| 006 | `tests/action-affordance.test.mjs::AA_prompt_020_run_contract_grounds_command_as_act_with_bounded_consequence` | NEW | 同上 |
| 007 | `tests/action-affordance.test.mjs::AA_arch_006_007_distinct_semantics_have_distinct_names_and_contracts` | NEW | 同上 |
| 008 | REUSE：`requirements/action-affordance/tests/tool-referential-integrity.test.mjs::gate_a_duplicate_tool_name_is_red`（same name = 唯一 semantic contract；schema/name 执行面 SPLIT@cutover → `capability-enforcement`）；`tests/action-affordance.test.mjs::AA_arch_006_007_distinct_semantics_have_distinct_names_and_contracts` | REUSE + NEW | `node --test requirements/action-affordance/tests/tool-referential-integrity.test.mjs` |
| 009 | `tests/action-affordance.test.mjs::AA_prompt_020_fork_contract_answers_whom_work_is_entrusted_to`（两个 calling 名只差 persona/深度）；REUSE：`requirements/office-capability/tests/office-capability-gate.test.mjs::gate_f_catalog_names_five_forkable_offices`（canonical 面归 `office-capability`） | NEW + REUSE | 同上 |
| 010 | `tests/action-affordance.test.mjs::AA_prompt_020_fork_contract_answers_whom_work_is_entrusted_to`（五 Office 后果） | NEW | 同上 |
| 011 | `tests/action-affordance.test.mjs::AA_prompt_021_callers_see_the_boundary_mirror_not_just_callee_role_law` | NEW | 同上 |
| 012 | REUSE：`requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs::office_boundary_eval_corpus_has_id_setup_oracles_and_synthetic_traces`（case `coder-inspect-ownership`：inspect charge 不得要求 mutation；SPLIT@cutover：caller 面 → 本包，consequence 面 → `office-capability`，可看性 → `participant-horizon`）；`tests/action-affordance.test.mjs::AA_prompt_021_callers_see_the_boundary_mirror_not_just_callee_role_law` | REUSE + NEW | `node --test requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs` |
| 013 | `tests/action-affordance.test.mjs::AA_prompt_020_success_returns_establish_bounded_consequence`（description 语义）；REUSE：`requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs::TODO-002 Manager Role Law rejects meta-work without owning tool timing`（todowrite description 覆盖纪律面，SPLIT@cutover 按断言归本包 / `obligation-ledger` / `participant-horizon`）；`requirements/action-affordance/tests/tool-description-anchors.test.mjs::gate_c_tool_description_anchor_parity_detects_missing_zh_id`（双语同 ID） | NEW + REUSE | 同上 |

## 统计

- 命题数：13
- NEW：1 个文件（`action-affordance.test.mjs`），10 个断言
- MOVE：0（tool-referential-integrity.test.mjs 是 SPLIT：本包 + `capability-enforcement`）
- REUSE：3 处（language-parity-gate、tool-referential-integrity、magic-todo-provider-boundary）+ 1 处 eval（office-boundary）

## REUSE 文件的 SPLIT@cutover 计划

```text
requirements/action-affordance/tests/tool-description-anchors.test.mjs
    → 本包（tool-description anchor catalog + parity）
    + provider-language（结构 parity / placeholder / identifier）
    + office-capability（Gate F）
requirements/action-affordance/tests/tool-referential-integrity.test.mjs
    → 本包（semantic act contract 同一）+ capability-enforcement（schema/name 唯一）+ DELETE（legacy name ratchet）
requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs
    → 本包（todowrite description 合同）+ obligation-ledger（admission）+ participant-horizon（hidden surface）
tests/eval/provider-office-boundary/**
    → 本包（coder-inspect-ownership caller 面）+ office-capability + participant-horizon
```

## 本包拥有的 semantic-anchor id（semantic-anchors.mjs）

`TOOL_DESCRIPTION_ANCHORS` 全归本包（23 个 id）：

```text
inspect:             repository-fact, causal-readonly, no-code-changes,
                     no-behavioral-execution, no-implement-or-repair
fork:                office-not-witness, coder-mutation, inspector-existing-facts,
                     devops-execution, browser-external-provenance, inquiry-reasoning,
                     persona-not-authority, create-and-continue
establish-behavior:  coder-writes-source, not-execution-evidence
repair-behavior:     meaning-decided, not-passing-proof
query-shell:         observation-not-execution, not-build-test
run:                 command-is-act, economic-commitment
commission:          independent-road, not-lifecycle-stage
```

不归本包：`ROLE_SEMANTIC_ANCHORS`（→ `cognitive-environment` 及其它 canonical owner，见
`cognitive-environment/PROOF.md`）、`OFFICE_CAPABILITY_ANCHORS` / `OFFICE_CAPABILITY_NEGATIVES`
（→ `office-capability`）。
