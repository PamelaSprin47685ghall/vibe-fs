# office-capability — 实现模型与约束（非 normative）

## 实现模型

| 面 | 实现 | 说明 |
|----|------|------|
| canonical 模型（normative 定义处） | 本包 WHAT OFF-002/005（历史 ARCH-017 立法） | 五分法 + 每 office entitled consequence / non-consequence 表；权威在语义层，不在代码单点 |
| 语义锚点目录 | `scripts/checks/semantic-anchors.mjs` `OFFICE_CAPABILITY_ANCHORS`（5 id）+ `OFFICE_CAPABILITY_NEGATIVES` | Gate F 的 oracle；id 归本包（见 HOW.md 清单） |
| Gate F 机制 | `scripts/checks/language-parity-gate.mjs` `scanOfficeCapabilityIntegrity` | 读 `role/manager/{en,zh-CN}.md` + `tool/fork/description/{en,zh-CN}.md`，五个 consequence 同 ID 双语命中；negatives 检查 |
| 五分法域事实 | `src/Wanxiangshu/Domain/ManagedAgentCatalog.fs` `managerForkableRoles` | `[Coder; Inspector; DevOps; Browser; Inquiry]`——与 ARCH-017 表一致 |
| 投影 1：Manager Role Law | `resources/provider/role/manager/{en,zh-CN}.md`「Entrust by consequence / 按后果托付」 | 世界观：按后果选择 office；negatives（不可互换）同文档 |
| 投影 2：fork description | `resources/provider/tool/fork/description/{en,zh-CN}.md` | 调用瞬间的可行动选择；calling 名只差 persona/depth |
| 投影 3：各 office Role Law | `resources/provider/role/{coder,inspector,devops,browser,inquiry}/{en,zh-CN}.md` | 自我模型：consequence + non-consequence |
| 投影 4：caller-facing tool | `resources/provider/tool/inspect/description/{en,zh-CN}.md` | 调用方必须看见的边界镜（Inspector 是见证者） |
| 行为 oracle | `tests/eval/provider-office-boundary/` | 4 个合成 trace oracle（office-boundary-eval），不接生产 filter |

## 边界与弃权

### 不归本包（引用其它包）

- 身份轴（Role/Persona/Binding）→ `participant-identity`（DEPENDS ON）。
- 矩阵/gate 同构与权限投影 → `capability-enforcement`。
- 委托动作语义（entrust by consequence 的调用律）→ `delegation`（锚点 `entrust-by-consequence` /
  `choose-by-return` / `no-omnipotent-charge` 已由 delegation 声明 owner，本包不重复声明）。
- 外部事实的 provenance 合同细节 → `external-investigation`；认识状态求解 → `epistemic-reasoning`。
- 什么信息有资格被看见 → `participant-horizon`；act 五问 → `action-affordance`。

### GARBAGE / HOW 裁决（不进入 WHAT）

| 内容 | 裁决 | 理由 |
|------|------|------|
| 「当前五 Office 必须永久保持五分法」 | HOW（可重构证据） | boundary card DOES NOT OWN：五分法是当前证据，可随重画边界变化 |
| Persona 双名（Navigator/Researcher 等 calling 名） | HOW | 命名除非成为 public contract；「两个名只差 persona/depth」才是 WHAT（OFF-003） |
| 各 office Role Law 的散文 craft 内容（非 consequence 部分） | HOW → `cognitive-environment` | 自我模型/世界观的长期认知内容归认知层；本包只拥有其中 consequence/non-consequence 事实 |
| Gate F 的 fixture 参数（`{0,120}`/`{0,80}` 跨度） | HOW | 正则实现细节；「同 ID 双语命中」才是 WHAT |

## 历史（考古摘要）

- 历史 ARCH-017：office capability model 立法，含「单一语义所有权，多处投影」表。
- 历史 why/architecture「Office 认知」备选与被拒：拒「fork 枚举 calling 名即足够」；真实事故
  （Coder 按 inspect tooltip 把修复交给 Inspector）催生边界镜投影。
- 历史 AGENT-009/011…016/023：各 office 的 consequence / non-consequence 明细条款。
- 历史 PROMPT-021：critical semantic redundancy——关键区别出现在每个会改变行动的决策面。
- 语义锚点 Gate F 现状：`OFFICE_CAPABILITY_ANCHORS` 5 id 双语文档命中由 `language-parity-gate.test.mjs`
  `gate_f_*` fixture 测试与 `scripts/check.mjs` 的 `language-parity-gate.mjs` 双重守护。

## 验证与测试落点

每条 WHAT 命题恰好一行落点。类型：`NEW` = 本包新写；`REUSE` = 留原处，记精确断言锚点与
cutover 拆分计划。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|----------|
| OFF-001 | `tests/office-capability-integrity.test.mjs` `WHAT[OFF-001] OFF_001_office_capability_is_consequence_not_tool_whitelist` | NEW | `node --test requirements/office-capability/tests/office-capability-integrity.test.mjs` |
| OFF-002 | `tests/office-capability-integrity.test.mjs` `WHAT[OFF-002] OFF_002_managed_catalog_forkable_offices_are_exactly_the_five_canonical_offices` + `WHAT[OFF-002] OFF_002_each_office_role_law_carries_its_entitled_consequence`；`tests/prompt-semantic-depth.test.mjs` `WHAT[OFF-002] PROMPT_depth_office_capability_catalog_covers_five_offices` | NEW + REUSE | 分别 `node --test` |
| OFF-003 | `tests/office-capability-integrity.test.mjs` `WHAT[OFF-003] OFF_003_two_calling_names_differ_in_persona_and_depth_not_authority` | NEW | 同上 |
| OFF-004 | `tests/office-capability-role-law-contract.test.mjs` `WHAT[OFF-004] capability_is_consequence_model_not_tool_whitelist_transcription`（manager law 按承诺不按键、不按内部工具） | NEW | `node --test requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-005 | `tests/office-capability-integrity.test.mjs` `WHAT[OFF-005] OFF_005_each_office_consequence_hits_manager_law_and_fork_description_in_both_locales`；`tests/office-capability-gate.test.mjs` `WHAT[OFF-005] gate_f_catalog_names_five_forkable_offices` / `gate_f_office_capability_fixture_is_green` / `gate_f_missing_locale_leaf_is_red` / `gate_f_missing_manager_coder_projection_is_red`；`tests/prompt-semantic-depth.test.mjs` `WHAT[OFF-005] PROMPT_depth_office_capability_hits_manager_and_fork` | NEW + REUSE（Gate F 断言归本包） | 分别 `node --test` |
| OFF-006 | `tests/office-capability-integrity.test.mjs` `WHAT[OFF-006] OFF_006_offices_are_not_interchangeable_general_purpose_agents`；`tests/office-capability-gate.test.mjs` `WHAT[OFF-006] gate_f_manager_must_forbid_interchangeable_offices` / `gate_f_fork_must_not_commission_another_witness`；`tests/eval/provider-office-boundary/office-boundary-eval.test.mjs` `WHAT[OFF-006] office_boundary_eval_corpus_has_id_setup_oracles_and_synthetic_traces` / `office_boundary_eval_manager_mixed_mission_case_is_red_and_green` | NEW + REUSE（eval 四 oracle 全绿） | `node --test requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs` |
| OFF-007 | `tests/office-capability-role-law-contract.test.mjs` `WHAT[OFF-007] manager_has_no_personal_repository_witness`（`no-personal-repository-witness` 双语命中）；REUSE `scripts/checks/semantic-anchors.mjs` `ROLE_SEMANTIC_ANCHORS.manager.no-personal-repository-witness`（锚点 id 本包拥有，共享 Gate C 机制验证） | NEW + REUSE | `node --test requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-008 | `tests/eval/provider-office-boundary/office-boundary-eval.test.mjs` `WHAT[OFF-008] office_boundary_eval_coder_inspect_ownership_case_is_red_and_green` / `office_boundary_eval_coder_inspect_oracle_is_charge_text_not_a_filter_module` / `office_boundary_eval_oracles_are_not_wired_into_production_tools` + `tests/office-capability-integrity.test.mjs`（coder law 双语文档携带 consequence） | REUSE + NEW | 分别 `node --test` |
| OFF-009 | `tests/eval/provider-office-boundary/office-boundary-eval.test.mjs` `WHAT[OFF-009] office_boundary_eval_inspector_refuses_repair_case_is_red_and_green` + `requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs` `EXEC_002_inspect_tool_description_forbids_mutation_and_execution` | REUSE（integration/plugin SPLIT：capability-enforcement + office-capability + delegation 等） | `node --test requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs` |
| OFF-010 | `tests/eval/provider-office-boundary/office-boundary-eval.test.mjs` `WHAT[OFF-010] office_boundary_eval_devops_does_not_choose_case_is_red_and_green` | REUSE | 同上 eval 命令 |
| OFF-011 | `tests/office-capability-role-law-contract.test.mjs` `WHAT[OFF-011] reviewer_consequence_is_readonly_judgement_not_repair` + `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` `ROLE_ALLOW.Reviewer`（= read/glob/grep/judge；Host-owned skill utility 不属于 office capability） | NEW + REUSE（矩阵断言 owner = capability-enforcement；office consequence 交叉引用，SPLIT@cutover 记双方 PROOF） | `node --test requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` |
| OFF-012 | `tests/office-capability-role-law-contract.test.mjs` `WHAT[OFF-012] orchestrator_commissions_manager_roads_not_phases` + `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` `ROLE_ALLOW.Orchestrator`（= commission/join/horizon；Host-owned skill utility 不属于 office capability） | NEW + REUSE（同上） | 同上 |
| OFF-013 | `tests/office-capability-role-law-contract.test.mjs` `WHAT[OFF-013] browser_consequence_is_external_facts_with_provenance_not_local_repo`；provenance 细节由 `external-investigation` 承担 | NEW | 同 OFF-004 命令 |
| OFF-014 | `tests/office-capability-role-law-contract.test.mjs` `WHAT[OFF-014] inquiry_consequence_is_semantic_understanding_not_evidence_minting` + `requirements/capability-enforcement/tests/inquiry-permissions.test.mjs`（Inquiry 工具面，REUSE） | NEW + REUSE | 分别 `node --test` |

### 新写文件

| 文件 | 结果 |
|------|------|
| `requirements/office-capability/tests/office-capability-integrity.test.mjs` | 6 pass / 0 fail |
| `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs` | 6 pass / 0 fail |

### 计数

WHAT 命题 14；落点 14（NEW + REUSE 组合行）；GAP 0。每命题 ≥1 个 active test。

### semantic anchor id 清单（`scripts/checks/semantic-anchors.mjs`，本包拥有）

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
