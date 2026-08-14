# PROOF —— 测试落点表

每条 WHAT 命题恰好一行。类型：`MOVE` / `REUSE`（留在原处，记断言锚点 + SPLIT@cutover）/ `NEW`。

运行：`node --test requirements/cognitive-environment/tests/<file>`；全量由 `node tests/unit/run.mjs` 并入。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| 001 | `tests/cognitive-environment.test.mjs::CE_prompt_015_one_system_prompt_per_role_not_per_tier` | NEW | `node --test requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| 002 | REUSE：`tests/unit/verify/language-parity-gate.test.mjs::gate_c_semantic_anchor_parity_detects_missing_zh_id`（内容必须双语同 ID；SPLIT@cutover 时 anchor-parity 机制面归 `provider-language`，Role Law 内容面归本包）；`tests/unit/resources/prompt-semantic-depth.test.mjs::PROMPT_depth_EN_role_laws_carry_cognition_anchors` + `..._ZH_...` | REUSE | `node --test tests/unit/resources/prompt-semantic-depth.test.mjs` |
| 003 | `tests/cognitive-environment.test.mjs::CE_prompt_015_canonical_composition_common_law_role_law_office_library` | NEW | 同上 |
| 004 | `tests/cognitive-environment.test.mjs::CE_prompt_015_system_prompt_does_not_enumerate_runtime_tool_surface` | NEW | 同上 |
| 005 | `tests/cognitive-environment.test.mjs::CE_prompt_015_one_system_prompt_per_role_not_per_tier`（10 角色各一份 prompt，无 tier 副本）+ `CE_role_law_is_enduring_self_model_without_tier_split_or_hidden_orchestration`（Role Law 无 fast/deep 字样）；COMPANION-004/ENFORCER-030 的 Blogger 单 system 面由 `resources/provider/role/blogger/{en,zh-CN}.md` 唯一成对承载 | NEW | 同上 |
| 006/007 | `tests/cognitive-environment.test.mjs::CE_prompt_016_library_ingress_teaches_knowledge_not_authority` | NEW | 同上 |
| 008/009 | `tests/cognitive-environment.test.mjs::CE_prompt_016_office_library_closing_keeps_books_subordinate_to_assignment`（书从属于 assignment）；`CE_role_law_cognition_anchors_present_in_both_locales`（内容义务） | NEW | 同上 |
| 010 | REUSE：`tests/unit/verify/language-parity-gate.test.mjs::gate_c_repo_scan_is_green`（lifecycle 资源成对，机制面）；语义面由 lifecycle 资源本身承载（无独立 runtime oracle） | REUSE | `node --test tests/unit/verify/language-parity-gate.test.mjs` |
| 011 | REUSE：`requirements/participant-identity/tests/session-persona.test.mjs::FALLBACK_014_system_prompt_id_follows_canonical_role_not_effective_agent_tier`（identity 面归 `participant-identity`；本包取「身份由 office 决定」认知面）；`requirements/provider-language/tests/provider-prose-ownership.test.mjs`（prose 不散落） | REUSE | `node --test requirements/participant-identity/tests/session-persona.test.mjs` |
| 012 | REUSE：`tests/unit/resources/prompt-semantic-depth.test.mjs::PROMPT_depth_Inquiry_Sphinx_capability_requires_Kernel_self_model`（Kernel self-model 面归 `epistemic-reasoning`）；Reviewer prompt 组合面由 `resources/provider/role/reviewer/*` 承载，无独立 runtime oracle | REUSE | `node --test tests/unit/resources/prompt-semantic-depth.test.mjs` |
| 013 | `tests/cognitive-environment.test.mjs::CE_agent_031_pair_hint_teaches_needhelp_as_normal_collaboration` + `CE_pair_hint_teaches_parallel_wave_without_global_concurrency_number`；REUSE：`tests/unit/host/pair-thought-transform.test.mjs::PAIR_HINT_canonical_text_encourages_needhelp_and_parallel_wave_without_global_N`（SPLIT@cutover：正文 craft → 本包；anchor/replay 机制 → `prefix-stability`/`provider-projection`） | NEW + REUSE | `node --test tests/unit/host/pair-thought-transform.test.mjs` |

## 统计

- 命题数：13
- NEW：1 个文件（`cognitive-environment.test.mjs`），9 个断言
- MOVE：0（本包无单-owner 现有测试文件；prompt-semantic-depth 是 SPLIT，留在原处）
- REUSE：6 处（prompt-semantic-depth、language-parity-gate、session-persona、pair-thought-transform、session-flattening、provider-prose-ownership）

## REUSE 文件的 SPLIT@cutover 计划

```text
tests/unit/resources/prompt-semantic-depth.test.mjs
    → 本包（role-law anchors：PROMPT_depth_EN/ZH_role_laws...）
    + action-affordance（tool anchors：PROMPT_depth_tool_anchor_catalog...）
    + office-capability（OFFICE_CAPABILITY 块）
    + epistemic-reasoning（Inquiry/Sphinx 块）
    + work-record（no-universal-closing-report 块）
tests/unit/verify/language-parity-gate.test.mjs
    → provider-language（结构 parity）+ office-capability（Gate F）+ action-affordance（tool anchors）+ 本包（role-law anchor parity）
tests/unit/host/pair-thought-transform.test.mjs
    → 本包（PAIR_HINT_canonical_text...）+ prefix-stability + provider-projection
requirements/participant-identity/tests/session-persona.test.mjs
    → participant-identity（FALLBACK_014）+ provider-language（bind-once）
```

## 本包拥有的 semantic-anchor id（semantic-anchors.mjs）

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
（见其 PROOF.md）。
