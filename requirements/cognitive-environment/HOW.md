# cognitive-environment — HOW

## 架构与核心机制

`cognitive-environment` 通过标准化的提示词资源加载器组装模型视野：

```text
PromptResources.systemForRole (语言 lang, 角色 role)
       │
       ├──► Common Law (通用世界观)
       ├──► Role Law (role 专属自我模型)
       └──► Office Library (依据 libraryPaths 引入对应的继承卷与闭卷)
```

1. **五层流水线组合**：
   - `PromptResources` 作为唯一提示词源，强制执行 EN/zh-CN 双语成对存在与锚点一致性检查（`ensureParity`）。
   - System Prompt 仅包含身份与知识层，Tools 描述由 ToolRegistry 独立注入，Runtime 与 Mission 材料通过会话上下文传递。

2. **Pair Hint 注入机制**：
   - 结对提示词通过 HOST-013 机制以合成 `skill` 内容的形式注入模型输入前沿，保证协作纪律、就绪前沿无阻塞并发，并仅用一句高显著性提示提醒复杂非线性工作可转入 `assume` jq 画板；完整画板方法与持久化语义留在工具描述，避免每轮重复灌输。
   - 对特定白名单模型的局部辅助提示（如 Blogger 的 chronicle-direct text nudge）仅在当次 transform 阶段临时注入并随后清除。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| COGNITIVE-ENVIRONMENT-001 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-001] CE_prompt_015_one_system_prompt_per_role` |
| COGNITIVE-ENVIRONMENT-002 | `requirements/cognitive-environment/tests/semantic-anchor-parity.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-002] gate_c_semantic_anchor_parity_detects_missing_zh_id` |
| COGNITIVE-ENVIRONMENT-003 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-003] CE_prompt_015_canonical_composition_common_law_role_law_office_library` |
| COGNITIVE-ENVIRONMENT-004 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-004] CE_prompt_015_system_prompt_does_not_enumerate_runtime_tool_surface` |
| COGNITIVE-ENVIRONMENT-005 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-005] CE_prompt_015_no_tier_split_duplicates` |
| COGNITIVE-ENVIRONMENT-006 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-006] CE_prompt_016_library_ingress_books_do_not_enlarge_authority` |
| COGNITIVE-ENVIRONMENT-007 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-007] CE_prompt_016_library_ingress_teaches_craft_within_existing_authority` |
| COGNITIVE-ENVIRONMENT-008 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-008] CE_prompt_016_office_library_closing_books_older_than_assignment` |
| COGNITIVE-ENVIRONMENT-009 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-009] CE_prompt_016_office_library_closing_work_not_forced_to_resemble_book` |
| COGNITIVE-ENVIRONMENT-010 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-010] CE_010_lifecycle_texts_orient_without_educating_or_replacing_system_prompt` |
| COGNITIVE-ENVIRONMENT-011 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-011] CE_011_transient_texts_do_not_rewrite_role_self_model` |
| COGNITIVE-ENVIRONMENT-012 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-012] CE_012_relay_assessment_prompt_carries_ledger_without_process_mechanics` |
| COGNITIVE-ENVIRONMENT-013 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-013] CE_pair_hint_teaches_continuous_ready_frontier_without_batch_barriers` |
| COGNITIVE-ENVIRONMENT-014 | `requirements/guidance-delivery/tests/pair-calibration.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-014] CE_014_tool_estimate_is_explicitly_advisory_in_both_provider_languages` |
| COGNITIVE-ENVIRONMENT-015 | `requirements/cognitive-environment/tests/blogger-chronicle-text.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_TEXT_is_companion_only_ephemeral_assistant_text_injection` |
| COGNITIVE-ENVIRONMENT-016 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs::WHAT[COGNITIVE-ENVIRONMENT-016] CE_016_pair_hint_retains_brief_trigger_without_repeating_full_psychological_contract` |
