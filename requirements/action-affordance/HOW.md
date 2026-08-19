# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可以重写而不改 WHAT。

## 类型与函数地图（action-affordance）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001 | `resources/provider/tool/<name>/description/{en,zh-CN}.md`（含 `assume`）；success/failure/arg 分文件（如 `tool/run/arg-command`、`tool/assume/committed`、`tool/fork/description`） | 每个非平凡 verb 的合同正文。`assume` 的底层“无新信息不重开判断”认知事实归 `cognitive-environment`，本包拥有调用边界上的 act/negative affordance/return consequence 镜像。 |
| 002 | `resources/provider/tool/{fork,commission,inspect,run,query-shell,establish-behavior,repair-behavior,...}/description/{en,zh-CN}.md` | 现行高风险 minimum set 与 Gate C anchor catalog 保持原有约束；`assume` 由 001 的非平凡 verb 合同约束覆盖，不人为扩大 Gate C minimum set。 |
| 003/004/005/006 | `resources/provider/tool/{inspect,repair-behavior,establish-behavior,run,query-shell}/description/*.md` | 具体负边界/后果/参数语义 |
| 007/008 | `scripts/checks/tool-referential-integrity.mjs`（Gate A：`scanRepo` / `extractToolSpecNames` / `LEGACY_FORBIDDEN_NAMES`）；`src/Wanxiangshu/Infrastructure/OpenCode/Tools/*Tool.fs` | 同 name 唯一 owner；semantic contract 面归本包，schema 执行面归 `capability-enforcement` |
| 009/010 | `resources/provider/tool/{fork,commission}/description/*.md`（五 Office 后果 + `calling` 语义）；`OFFICE_CAPABILITY_ANCHORS`（Gate F，canonical 归 `office-capability`） | mirror 完整性 |
| 011/012 | `TOOL_DESCRIPTION_ANCHORS`（`scripts/checks/semantic-anchors.mjs`）；`requirements/office-capability/tests/eval/provider-office-boundary/oracles.mjs`（`evaluateCoderInspectOwnership`） | caller 面 mirror |
| 013 | `resources/provider/lifecycle/magic-todo/todowrite-description/*.md`；`scripts/checks/language-parity-gate.mjs`（`scanToolDescriptionAnchorParity` / `scanToolDescriptionAnchorCatalog`） | description 覆盖纪律 + 双语 anchor |

## 关键机制：description 资源是合同的家

每个高风险动词的合同住在 `resources/provider/tool/<name>/description/{en,zh-CN}.md`。
ToolRegistry / OpenCode `Tool.Def` 只把已本地化的 description 抬上 wire（PROMPT-019：
`ToolHostCodec 接收已按 SessionProviderLanguage 本地化的 Description`）；`ToolHostCodec` 只拥有布局与
转义，不拥有 prose 语义。

```text
semantic owner（本包/office-capability/…）
  → resources/provider/tool/<name>/description/{en,zh-CN}.md
  → ProviderResources 装载（成对、缺 locale fail closed）
  → SyntheticToml / ToolHostCodec（布局/转义 only）
```

## 防退化的门禁（MECHANISM，逐 ID 归包）

| 门禁 | 归属 |
|---|---|
| `scripts/checks/semantic-anchors.mjs` → `TOOL_DESCRIPTION_ANCHORS` | 7 个高风险 description 的双语认知锚点 → 本包（逐 ID 清单见 HOW.md） |
| `scripts/checks/language-parity-gate.mjs` → `scanToolDescriptionAnchorParity` / `scanToolDescriptionAnchorCatalog` | anchor 机制共享；语义断言唯一 owner = 本包 |
| `scripts/checks/tool-referential-integrity.mjs` | Gate A 机制；「semantic act 同一」→ 本包，「schema/name 唯一」→ `capability-enforcement` |

## 历史与弃权

| 历史材料 | 裁决 | 记录位置 |
|---|---|---|
| 当前高风险 verb 名单与 allowlist（`fork, commission, inspect, run, query-shell, establish-behavior, repair-behavior, fetch, join, horizon, judge, suicide, fission, chronicle, js-*`） | **证据，非永久 ontology**（boundary card DOES NOT OWN：「当前动作名清单与高风险 allowlist」）；「高风险 verb 必须有合同」是命题（002），名单本身可重构 | WHAT 002 |
| `LEGACY_FORBIDDEN_NAMES`（verdict/list/executor/return/fork-pty/...） | **迁移 ratchet**：已删工具名的 absence 证明迁移完成；新世界基线稳定后 DELETE（PROOF-MAP §92）。schema/name 面归 `capability-enforcement` | HOW + PROOF |
| `js-capability-projected-tools.md` / `js-tools-toml-result.md` | **不归本包**（`repository-programming` / `provider-projection` / `capability-enforcement`）：JS 工具面是 repository programming 面；本包只取「`js-*` 属高风险 verb、需要合同」的宽命题 | WHAT 002 |
| 历史 why/js-tools 的 JS-001..020 | 不归本包（repository-programming 的 HOW） | — |
| exact `calling` 枚举值（navigator/researcher/coordinator/lead/...） | 证据；命题是「calling 是 capability 选择，不是裸 enum」（009） | WHAT 009 |

## 依赖说明

INDEX.md 依赖骨架：`action-affordance → office-capability, participant-horizon`。
- `office-capability`：本包镜像五 Office consequence（010/011），canonical 只有 ARCH-017 一处；
- `participant-horizon`：合同要出现在 decision surface，先决条件是该 surface 有资格存在（什么可看）。

## 验证与测试落点

每条 WHAT 命题恰好一行。类型：`MOVE` / `REUSE`（留在原处，记断言锚点 + SPLIT@cutover）/ `NEW`。

运行：`node --test requirements/action-affordance/tests/<file>`；全量由 `node requirements/verification-system/tests/run.mjs` 并入。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| 001 | `tests/action-affordance.test.mjs::AA_prompt_020_tool_descriptions_carry_contract_anchors_in_both_locales`（高风险 minimum set 五问经 anchor 全覆盖）+ `AA_assume_contract_answers_act_fit_boundary_return_and_argument`（`assume` 的 act/fit/negative affordance/return/argument 五问）；`tests/prompt-semantic-depth.test.mjs::PROMPT_depth_EN_tool_descriptions_carry_cognition_anchors` / `PROMPT_depth_ZH_tool_descriptions_carry_matching_cognition_anchors`（双语文档逐 id 命中） | NEW | `node --test requirements/action-affordance/tests/action-affordance.test.mjs` |
| 002 | `tests/action-affordance.test.mjs::AA_prompt_020_high_risk_verbs_have_semantic_anchor_catalog`；`tests/prompt-semantic-depth.test.mjs::PROMPT_depth_tool_anchor_catalog_covers_high_risk_verbs`；`tests/tool-description-anchors.test.mjs::gate_c_tool_description_anchor_catalog_requires_high_risk_verbs`（anchor-parity 机制面归 `provider-language`） | NEW | 同上 |
| 003 | `tests/action-affordance.test.mjs::AA_prompt_020_inspect_contract_names_the_not_performed_act` | NEW | 同上 |
| 004 | `tests/action-affordance.test.mjs::AA_prompt_020_repair_behavior_contract_defines_mechanical` | NEW | 同上 |
| 005 | `tests/action-affordance.test.mjs::AA_prompt_020_establish_behavior_contract_separates_mutation_from_execution` | NEW | 同上 |
| 006 | `tests/action-affordance.test.mjs::AA_prompt_020_run_contract_grounds_command_as_act_with_bounded_consequence`（含 query-shell observation, not execution 断言） | NEW | 同上 |
| 007 | `tests/action-affordance.test.mjs::AA_arch_006_007_distinct_semantics_have_distinct_names` | NEW | 同上 |
| 008 | REUSE：`tests/tool-referential-integrity.test.mjs::gate_a_duplicate_tool_name_is_red`（same name = 唯一 semantic contract；schema/name 执行面 SPLIT@cutover → `capability-enforcement`）；`tests/tool-referential-integrity.test.mjs::gate_a_extracts_tool_spec_record_names`；`tests/action-affordance.test.mjs::AA_arch_007_same_tool_name_means_same_contract`（commission 合同面） | REUSE + NEW | `node --test requirements/action-affordance/tests/tool-referential-integrity.test.mjs` |
| 009 | `tests/action-affordance.test.mjs::AA_prompt_020_fork_calling_names_differ_in_persona_not_authority`（两个 calling 名只差 persona/深度） | NEW | 同上 |
| 010 | `tests/action-affordance.test.mjs::AA_prompt_020_fork_contract_answers_whom_work_is_entrusted_to`（五 Office 后果逐个断言） | NEW | 同上 |
| 011 | `tests/action-affordance.test.mjs::AA_prompt_021_callers_see_the_boundary_mirror_not_just_callee_role_law` | NEW | 同上 |
| 012 | `tests/action-affordance.test.mjs::AA_prompt_020_inspect_caller_forbidden_charge_is_named` | NEW | 同上 |
| 013 | `tests/action-affordance.test.mjs::AA_prompt_020_success_returns_establish_bounded_consequence`（description 语义）；`tests/tool-description-anchors.test.mjs::gate_c_tool_description_anchor_parity_detects_missing_zh_id`（双语同 ID） | NEW + REUSE | 同上 |

### 统计

- 命题数：13
- NEW：1 个文件（`action-affordance.test.mjs`），13 个 test（含拆分出的 008/009/012 面）
- MOVE：0（tool-referential-integrity.test.mjs 是 SPLIT：本包 + `capability-enforcement`）
- REUSE：3 处（language-parity-gate、tool-referential-integrity、magic-todo-provider-boundary）+ 1 处 eval（office-boundary）

### REUSE 文件的 SPLIT@cutover 计划

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

### 本包拥有的 semantic-anchor id（semantic-anchors.mjs）

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
`cognitive-environment/HOW.md`）、`OFFICE_CAPABILITY_ANCHORS` / `OFFICE_CAPABILITY_NEGATIVES`
（→ `office-capability`）。
