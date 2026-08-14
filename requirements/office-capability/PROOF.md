# office-capability — 测试落点表

每条 WHAT 命题恰好一行落点。类型：`NEW` = 本包新写；`REUSE` = 留原处，记精确断言锚点与
cutover 拆分计划。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|----------|
| OFF-001 | `tests/office-capability-integrity.test.mjs` `OFF_001_office_capability_is_consequence_not_tool_whitelist` | NEW | `node --test requirements/office-capability/tests/office-capability-integrity.test.mjs` |
| OFF-002 | `tests/office-capability-integrity.test.mjs` `OFF_002_managed_catalog_forkable_offices_are_exactly_the_five_canonical_offices` + `OFF_002_each_office_role_law_carries_its_entitled_consequence` | NEW | 同上 |
| OFF-003 | `tests/office-capability-integrity.test.mjs` `OFF_003_two_calling_names_differ_in_persona_and_depth_not_authority` | NEW | 同上 |
| OFF-004 | `tests/office-capability-integrity.test.mjs` `OFF_001_...`（consequence 非 whitelist） | NEW | 同上 |
| OFF-005 | `tests/office-capability-integrity.test.mjs` `OFF_005_each_office_consequence_hits_manager_law_and_fork_description_in_both_locales` + `tests/unit/verify/language-parity-gate.test.mjs` `gate_f_catalog_names_five_forkable_offices` / `gate_f_office_capability_fixture_is_green` / `gate_f_missing_manager_coder_projection_is_red` | NEW + REUSE（SPLIT：language-parity-gate = provider-language 结构 parity + office-capability 语义；Gate F 断言归本包，SPLIT@cutover） | 分别 `node --test` |
| OFF-006 | `tests/office-capability-integrity.test.mjs` `OFF_006_offices_are_not_interchangeable_general_purpose_agents` + `tests/eval/provider-office-boundary/office-boundary-eval.test.mjs`（4 oracle 全绿） | NEW + REUSE（SPLIT：eval family = office-capability + participant-horizon；office oracle 归本包） | `node --test tests/eval/provider-office-boundary/office-boundary-eval.test.mjs` |
| OFF-007 | `scripts/checks/semantic-anchors.mjs` `ROLE_SEMANTIC_ANCHORS.manager.no-personal-repository-witness`（经 `tests/unit/verify/language-parity-gate.test.mjs` `gate_c_semantic_anchor_parity_*` 双语文档命中） | REUSE（锚点 id 本包拥有；命中验证共享 Gate C 机制） | `node --test tests/unit/verify/language-parity-gate.test.mjs` |
| OFF-008 | `tests/eval/provider-office-boundary` `coder-inspect-ownership` + `tests/office-capability-integrity.test.mjs`（coder consequence 双语文档） | REUSE + NEW | 分别 `node --test` |
| OFF-009 | `tests/eval/provider-office-boundary` `inspector-refuses-repair` + `tests/integration/plugin/manager-tool-contract.test.mjs` `EXEC_002_inspect_tool_description_forbids_mutation_and_execution` | REUSE（integration/plugin SPLIT：capability-enforcement + office-capability + delegation 等） | `node --test tests/integration/plugin/manager-tool-contract.test.mjs` |
| OFF-010 | `tests/eval/provider-office-boundary` `devops-does-not-choose-among-valid-behaviors` | REUSE | 同上 eval 命令 |
| OFF-011 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` `ROLE_ALLOW.Reviewer`（= read/glob/grep/judge/auto-injected） | REUSE（矩阵断言 owner = capability-enforcement；office consequence 交叉引用，SPLIT@cutover 记双方 PROOF） | `node --test requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| OFF-012 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` `ROLE_ALLOW.Orchestrator`（= commission/join/horizon/auto-injected） | REUSE（同上） | 同上 |
| OFF-013 | `tests/office-capability-integrity.test.mjs`（browser consequence 双语文档命中）；provenance 细节由 `external-investigation` 承担 | NEW | 同 OFF-001 命令 |
| OFF-014 | `tests/office-capability-integrity.test.mjs`（inquiry consequence 双语文档命中）+ `tests/unit/agent/inquiry-permissions.test.mjs`（Inquiry 工具面，REUSE） | NEW + REUSE | 分别 `node --test` |

## 新写文件

| 文件 | 结果 |
|------|------|
| `requirements/office-capability/tests/office-capability-integrity.test.mjs` | 6 pass / 0 fail |

## 计数

WHAT 命题 14；落点 14（NEW 9 行 × REUSE 8 行，含组合行）；GAP 0。

## semantic anchor id 清单（`scripts/checks/semantic-anchors.mjs`，本包拥有）

- `OFFICE_CAPABILITY_ANCHORS`：全部 5 id —— `coder-mutation`、`inspector-existing-facts`、
  `devops-execution`、`browser-external-provenance`、`inquiry-reasoning`。
- `OFFICE_CAPABILITY_NEGATIVES`：`managerEnRequired` / `managerZhRequired`（offices 不可互换）、
  `forkForbidden`（fork 不得写成 commission a witness）。
- `ROLE_SEMANTIC_ANCHORS.manager`：`no-personal-repository-witness`（Manager non-consequence）。
- `ROLE_SEMANTIC_ANCHORS.coder`：`no-execution`、`shell-boundary`（Coder non-consequence）。
- `ROLE_SEMANTIC_ANCHORS.inspector`：`existing-fact`（consequence）、`consequence-not-verdict`
  （non-consequence）。
- `ROLE_SEMANTIC_ANCHORS.devops`：`operational-closure`（consequence）、`mechanical-meaning`、
  `coder-report-not-evidence`（non-consequence）。

不归本包（已由其它包声明或归属认知层）：`entrust-by-consequence` / `choose-by-return` /
`no-omnipotent-charge` → `delegation`（其 WHAT 已声明）；browser 组 provenance 锚点 →
`external-investigation`；inquiry 组 → `epistemic-reasoning`；manager 其余锚点（planning/obligations
等）→ `cognitive-environment`；`inspector-is-witness` / `do-not-ask-inspect-and-fix` →
`action-affordance`（inspect 工具合同镜）。
