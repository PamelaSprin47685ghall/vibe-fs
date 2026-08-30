# action-affordance — HOW

## 架构与实现机制

1. **描述资源作为契约载体**：
   - 动作契约文本统一定义于 `resources/provider/tool/<name>/description/{en,zh-CN}.md`。
   - `ToolRegistry` 与 OpenCode `Tool.Def` 仅负责加载已本地化的描述文本，`ToolHostCodec` 负责布局与转义，不拥有散文语义。

2. **双语语义锚点与防退化门禁**：
   - 高风险动词的核心约束通过双语认知锚点进行机械化保护（如 `TOOL_DESCRIPTION_ANCHORS`）。
   - 静态检查门禁保证多语言描述中语义锚点严格成对、无遗漏。

3. **边界镜像机制**：
   - Canonical 权限后果由 `office-capability` 唯一定义；本包在调用边界（如 `fork`、`inspect` 描述）中镜像其负边界与职能分工，防止跨角色误用。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| ACTION-AFFORDANCE-001 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-001] AA_prompt_020_tool_descriptions_carry_contract_anchors_in_both_locales` |
| ACTION-AFFORDANCE-002 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-002] AA_prompt_020_high_risk_verbs_have_semantic_anchor_catalog` |
| ACTION-AFFORDANCE-003 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-003] AA_prompt_020_inspect_contract_names_the_not_performed_act` |
| ACTION-AFFORDANCE-004 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-004] AA_prompt_020_repair_behavior_contract_defines_mechanical` |
| ACTION-AFFORDANCE-005 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-005] AA_prompt_020_establish_behavior_contract_separates_mutation_from_execution` |
| ACTION-AFFORDANCE-006 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-006] AA_prompt_020_run_contract_grounds_command_as_act_with_bounded_consequence` |
| ACTION-AFFORDANCE-007 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-007] AA_arch_006_007_distinct_semantics_have_distinct_names` |
| ACTION-AFFORDANCE-008 | `requirements/action-affordance/tests/tool-referential-integrity.test.mjs::WHAT[ACTION-AFFORDANCE-008] gate_a_extracts_tool_spec_record_names` |
| ACTION-AFFORDANCE-009 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-009] AA_prompt_020_fork_calling_names_differ_in_persona_not_authority` |
| ACTION-AFFORDANCE-010 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-010] AA_prompt_020_fork_contract_answers_whom_work_is_entrusted_to` |
| ACTION-AFFORDANCE-011 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-011] AA_prompt_021_callers_see_the_boundary_mirror_not_just_callee_role_law` |
| ACTION-AFFORDANCE-012 | `requirements/action-affordance/tests/action-affordance.test.mjs::WHAT[ACTION-AFFORDANCE-012] AA_prompt_020_inspect_caller_forbidden_charge_is_named` |
| ACTION-AFFORDANCE-013 | `requirements/action-affordance/tests/tool-description-anchors.test.mjs::WHAT[ACTION-AFFORDANCE-013] gate_c_tool_description_anchor_parity_detects_missing_zh_id` |
