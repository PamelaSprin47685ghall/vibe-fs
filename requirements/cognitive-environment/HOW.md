# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可以重写而不改 WHAT。

## 类型与函数地图（cognitive-environment）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001/003 | `Infrastructure/Resources/PromptResources.fs` → `PromptCatalog`（10 角色各一个 system prompt）、`systemForRole`、`semanticPaths` | 唯一组合源；World → Role Law → Office Library（`libraryPaths` 按角色决定继承卷） |
| 001/005 | `resources/provider/role/*/{en,zh-CN}.md`（11 个角色对） | Role Law 是每 office 一份；Bookkeeper 走 `loadBookkeeperSystemFor`（Common Law + Bookkeeper Role Law） |
| 004 | `PromptResources.fs` `systemForRole`（只拼 System 层）；`resources/provider/role/*/`（无工具枚举） | Tools 面由 ToolRegistry 另生，不进 Role 章节 |
| 006/007/008/009 | `resources/provider/library/{ingress,closing,kolmogorov,scarcity,reviewer/quality-ledger}/**`；`PromptResources.fs` `libraryPaths` | ingress 明言「do not enlarge your authority」；closing 明言书从属于 assignment |
| 005/011 | `resources/provider/role/*/`（无 fast/deep 字样）；`Session/CompanionPrompt.fs` 等使用 `PromptResources` 组合；`requirements/cognitive-environment/tests/cognitive-environment.test.mjs` | 自我模型稳定；`prompt-stability` 测试（byte-stability）归 `prefix-stability`/`participant-identity` |
| 012 | `resources/provider/role/reviewer/*` + `resources/provider/library/reviewer/quality-ledger/*`；`requirements/cognitive-environment/tests/cognitive-environment.test.mjs` | REVIEW-012：双 PERFECT 不入 prompt |
| 013 | `resources/provider/host/pair-programming-guideline/{en,zh-CN}.md`；HOST-013 transform（`Infrastructure/OpenCode/Host/*`）把同一 canonical 正文投影到 wire；`requirements/cognitive-environment/tests/cognitive-environment.test.mjs` | craft 单源；并发文案用持续重算的 ready frontier 表达因果调度，不把 wave/DAG 变成 barrier；`cursor-pair-hint.md`/`pair-parallel-tools.md`/`increase-strength.md` 考古 |

## 关键机制：PromptResources 是唯一组合源

```text
systemForRole(lang, role)
  = compose [ Common Law
            ; Role Law(role)
            ; (若有继承卷) Library ingress
            ;   + libraryPaths(role) 的书
            ;   + Library closing ]
```

- `ensureParity`：每个 semanticPath 必须 EN/zh-CN 成对存在（缺 → fail closed）；
- `libraryPaths`：Manager → [kolmogorov, scarcity]；Coder → [kolmogorov]；Reviewer → [kolmogorov,
  quality-ledger]；Inspector/DevOps → [scarcity]；其余无书；
- 生命周期/Runtime/Mission 材料在其它资源树（`resources/provider/lifecycle/**`、
  `resources/provider/delegation/**`、`resources/provider/host/**`），不进入 SYSTEM 组合。

## 防退化的门禁（MECHANISM，逐 ID 归包）

| 门禁 | 归属 |
|---|---|
| `scripts/checks/prompt-depth-ratchet.mjs` + `prompt-depth-baseline.json` | Role Law 深度 anti-amputation ratchet → 本包（认知义务不得被意外切除）；机制共享 |
| `scripts/checks/semantic-anchors.mjs` → `ROLE_SEMANTIC_ANCHORS` | Role Law cognition anchors（逐 ID 归属见 HOW.md）→ 本包（除 browser/inquiry/reviewer 与 manager consequence 镜像） |
| `scripts/checks/language-parity-gate.mjs` | 双语成对 + placeholder/anchor parity → `provider-language`（结构面）+ 本包（Role Law 内容面，经 Gate C role-law anchors） |

## 历史与弃权

| 历史材料 | 裁决 | 记录位置 |
|---|---|---|
| Common Law / Role Law / Office Library 这些**名字** | **HOW/证据**：boundary card 明言「当前 Common Law / Role Law / Office Library 是证据，不要求保留名称」。包身份是「长期认知分层」，结构可整体重写 | WHY.md |
| `fast-*` / `deep-*` 当前 machine names、22-agent catalog、Persona 名字表 | **GARBAGE**（HANDOFF §12）：不进入永久 WHAT；本包只取「fast/deep 共享同一 Role Law」命题（005） | WHY.md 被拒方案 |
| Student/Teacher/Meditator/Executor absence ratchet | **GARBAGE**（CHANGES-AUDIT：universal.md / ce-student-teacher-collapse / Student & Teacher.md）：迁移沉积，新世界基线稳定后删除 | WHY.md |
| `pair-parallel-tools.md` 的 metrics（Coalescing Rate、Round Trips） | **HOW**：度量是验证机制，不是产品命题；本包取 craft 正文（013） | WHAT 013 |
| `increase-strength.md` §5-§9（detection/abort/escalation/consultation） | **不归本包**：fast→deep escalation 与 consultation 的 authority/调度语义归 `interaction-authority` / `delegation` / `provider-attempt-recovery`；本包只取 §3 的 craft 面 | WHAT 反向覆盖 |
| `PromptRestoration.md` 的 Gate 0/批量迁移日程 | **HOW/GARBAGE**（实施记录）：本包取最终态纪律（组合、Role Law 厚度、无工具清单）；语言管辖归 `provider-language` | WHY.md |
| NEEDHELP 的 consultation/authority/wire 各部分 | **边界如实标注**（HANDOFF §10.2 WATCH）：craft → 本包；consultation → `delegation`；authority continuity → `interaction-authority`；wire injection → `provider-projection` / `prefix-stability` | WHAT 反向覆盖 |

## 依赖说明

INDEX.md 依赖骨架：`cognitive-environment → participant-identity, office-capability`。
- `participant-identity`：Role/Persona 稳定是「自我模型不漂移」的前提；
- `office-capability`：本包只**引用** authority facts（005/007 说「authority 不随知识流动」，不定义
  authority 本身）。

## 验证与测试落点

每条 WHAT 命题恰好一行。类型：`MOVE` / `REUSE`（留在原处，记断言锚点 + SPLIT@cutover）/ `NEW`。

运行：`node --test requirements/cognitive-environment/tests/<file>`；全量由 `node requirements/verification-system/tests/run.mjs` 并入。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| 001 | `tests/cognitive-environment.test.mjs::CE_prompt_015_one_system_prompt_per_role`（10 个公共 office 各一份 canonical prompt） | NEW | `node --test requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| 002 | REUSE：`requirements/cognitive-environment/tests/semantic-anchor-parity.test.mjs::gate_c_semantic_anchor_parity_detects_missing_zh_id`（内容必须双语同 ID；SPLIT@cutover 时 anchor-parity 机制面归 `provider-language`，Role Law 内容面归本包）；`requirements/cognitive-environment/tests/prompt-semantic-depth.test.mjs::PROMPT_depth_EN_role_laws_carry_cognition_anchors` + `..._ZH_...` | REUSE | `node --test requirements/cognitive-environment/tests/prompt-semantic-depth.test.mjs` |
| 003 | `tests/cognitive-environment.test.mjs::CE_prompt_015_canonical_composition_common_law_role_law_office_library` | NEW | 同上 |
| 004 | `tests/cognitive-environment.test.mjs::CE_prompt_015_system_prompt_does_not_enumerate_runtime_tool_surface` | NEW | 同上 |
| 005 | `tests/cognitive-environment.test.mjs::CE_prompt_015_no_tier_split_duplicates`（10 角色各一份 prompt，无 tier 副本）+ `CE_role_law_is_enduring_self_model_without_tier_split_or_hidden_orchestration`（Role Law 无 fast/deep 字样）；COMPANION-004/ENFORCER-030 的 Blogger 单 system 面由 `resources/provider/role/blogger/{en,zh-CN}.md` 唯一成对承载 | NEW | 同上 |
| 006 | `tests/cognitive-environment.test.mjs::CE_prompt_016_library_ingress_books_do_not_enlarge_authority`（书不扩大 authority、不覆盖 Common Law） | NEW | 同上 |
| 007 | `tests/cognitive-environment.test.mjs::CE_prompt_016_library_ingress_teaches_craft_within_existing_authority`（craft 可教、authority 不随知识授予） | NEW | 同上 |
| 008 | `tests/cognitive-environment.test.mjs::CE_prompt_016_office_library_closing_books_older_than_assignment`（书从属于 assignment）+ `CE_role_law_cognition_anchors_present_in_both_locales`（Audience 绑 semantic role 的内容义务）+ `tests/semantic-anchor-parity.test.mjs::gate_c_semantic_anchor_catalog_requires_every_role_law`（每个 role 目录必须在 catalog 内） | NEW + REUSE | 同上 |
| 009 | `tests/cognitive-environment.test.mjs::CE_prompt_016_office_library_closing_work_not_forced_to_resemble_book`（不强迫工作长成书的样子） | NEW | 同上 |
| 010 | `tests/cognitive-environment.test.mjs::CE_010_lifecycle_texts_orient_without_educating_or_replacing_system_prompt`（六种生命周期 + runtime 文本只 orient：不 educate、不叠第二套 envelope、不触发 system prompt 替换） | NEW | `node --test requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| 011 | `tests/cognitive-environment.test.mjs::CE_011_transient_texts_do_not_rewrite_role_self_model`（瞬时文本不重写身份 self-model、不暴露 fast/deep 机器身份） | NEW | `node --test requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| 012 | `tests/cognitive-environment.test.mjs::CE_012_reviewer_prompt_carries_role_law_and_ledger_without_process_mechanics`（Reviewer prompt 不灌输 PERFECT 流程机制）；REUSE：`requirements/cognitive-environment/tests/prompt-semantic-depth.test.mjs::PROMPT_depth_Inquiry_Sphinx_capability_requires_Kernel_self_model`（Kernel self-model 面归 `epistemic-reasoning`）+ `PROMPT_depth_no_universal_closing_report_schema_in_role_laws`（固定 report schema 不进认知层）；Reviewer prompt 组合面由 `resources/provider/role/reviewer/*` 承载 | REUSE | `node --test requirements/cognitive-environment/tests/prompt-semantic-depth.test.mjs` |
| 013 | `tests/cognitive-environment.test.mjs::CE_agent_031_pair_hint_teaches_needhelp_as_normal_collaboration` + `CE_pair_hint_teaches_continuous_ready_frontier_without_batch_barriers` + `CE_pair_hint_encourages_filling_concurrency_slots` + `CE_pair_hint_teaches_abstract_then_commit_without_wavering` + `CE_pair_hint_reserves_empty_skill_name_without_disabling_real_skills`；REUSE：`requirements/cognitive-environment/tests/pair-thought-transform.test.mjs::PAIR_HINT_canonical_text_encourages_needhelp_and_continuous_ready_frontier_without_global_N`（SPLIT@cutover：正文 craft → 本包；anchor/replay 机制 → `prefix-stability`/`provider-projection`） | NEW + REUSE | `node --test requirements/cognitive-environment/tests/cognitive-environment.test.mjs requirements/cognitive-environment/tests/pair-thought-transform.test.mjs` |
| 014 | `requirements/guidance-delivery/tests/pair-calibration.test.mjs` `CE_014_tool_estimate_is_explicitly_advisory_in_both_provider_languages` | REUSE（FROZEN 2026-08-14） | **按用户要求冻结后未执行**；实现后不改 oracle |

| COGNITIVE-ENVIRONMENT-001 | `tests/cognitive-environment.test.mjs::CE_prompt_015_one_system_prompt_per_role` | NEW | `node --test requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-003 | `tests/cognitive-environment.test.mjs::CE_prompt_015_canonical_composition_common_law_role_law_office_library` | NEW | 同上 |
| COGNITIVE-ENVIRONMENT-004 | `tests/cognitive-environment.test.mjs::CE_prompt_015_system_prompt_does_not_enumerate_runtime_tool_surface` | NEW | 同上 |
| COGNITIVE-ENVIRONMENT-005 | `tests/cognitive-environment.test.mjs::CE_prompt_015_no_tier_split_duplicates` | NEW | 同上 |


- 命题数：14
- NEW：1 个文件（`cognitive-environment.test.mjs`），15 个断言 test
- MOVE：0（本包无单-owner 现有测试文件；prompt-semantic-depth 是 SPLIT，留在原处）
- REUSE：6 处（prompt-semantic-depth、language-parity-gate、session-persona、pair-thought-transform、session-flattening、provider-prose-ownership）

### REUSE 文件的 SPLIT@cutover 计划

```text
requirements/cognitive-environment/tests/prompt-semantic-depth.test.mjs
    → 本包（role-law anchors：PROMPT_depth_EN/ZH_role_laws...）
    + action-affordance（tool anchors：PROMPT_depth_tool_anchor_catalog...）
    + office-capability（OFFICE_CAPABILITY 块）
    + epistemic-reasoning（Inquiry/Sphinx 块）
    + work-record（no-universal-closing-report 块）
requirements/cognitive-environment/tests/semantic-anchor-parity.test.mjs
    → provider-language（结构 parity）+ office-capability（Gate F）+ action-affordance（tool anchors）+ 本包（role-law anchor parity）
requirements/prefix-stability/tests/pair-thought-transform.test.mjs
    → 本包（PAIR_HINT_canonical_text...）+ prefix-stability + provider-projection
requirements/participant-identity/tests/session-persona.test.mjs
    → participant-identity（FALLBACK_014）+ provider-language（bind-once）
```

### 本包拥有的 semantic-anchor id（semantic-anchors.mjs）

`semantic-anchors.mjs` 是 MECHANISM（共享 catalog）；本包按「Role Law 层 = enduring self-model」原则
逐 ID 声明 owner：

```text
ROLE_SEMANTIC_ANCHORS（本包拥有）
  manager:   arms-length-planning, planning-table-or-entrusted, obligations, order-of-ten,
             waiting-by-dependency, no-personal-repository-witness, anti-defeatism,
             opportunity-cost, returned-record
  coder:     written-world, no-execution, consume-runtime-evidence, tests-are-source,
             coherent-not-smallest, shell-boundary, clean-handoff, inspector-is-witness,
             do-not-ask-inspect-and-fix
  inspector: causal-readonly, existing-fact, evidence-funnel, locatability,
             consequence-not-verdict, semantic-stopping
  devops:    operational-closure, act-vs-observation, mechanical-meaning,
             coder-report-not-evidence, continuing-process, signal-not-exit, failure-can-be-work
  orchestrator: owns-roads, same-road-continuation, independent-destination, shared-gate,
             host-vs-orchestrator
  blogger:   occurrence-selection, not-instrumentation, tip-ontology, repetition-legal
  distiller: distinguishing, fragment-humility, merge-conflicts, locatable-to-unseen-reader,
             no-invented-causality
  bookkeeper: reusable-knowledge, one-case, question-may-change, zero-mutation, transcript-is-data

不归本包（各自 canonical owner 的 Role Law 镜像）
  manager:   entrust-by-consequence, choose-by-return, no-omnipotent-charge  → office-capability
             （ARCH-017 "Manager Role Law: worldview — 按后果选择 Office"）
  browser.* （8 个）→ external-investigation
  inquiry.* （7 个）→ epistemic-reasoning
  reviewer.*（5 个）→ review-judgement
  OFFICE_CAPABILITY_ANCHORS / OFFICE_CAPABILITY_NEGATIVES → office-capability
```

注：被镜像的 canonical fact 仍归其 owner（PROMPT-021 单一语义所有权，多处呈现）；本包拥有的是
「Role Law 层承载该 craft/自我模型」的断言。TOOL_DESCRIPTION_ANCHORS 全归 `action-affordance`
（见其 HOW.md）。
