# office-capability — HOW

## 架构与核心机制

`office-capability` 作为领域语义事实，由唯一 typed owner、静态门禁、提示词投影与运行时矩阵共同承载：

```text
Office Consequence Model (语义唯一事实源)
       │
       ├──► Foundation/OfficeCapability.fs (ToolPermission + exhaustive Role matrix)
       ├──► 提示词投影 (Manager Role Law, fork description, 各 Office 自我模型)
       ├──► 静态门禁 (Gate F: scanOfficeCapabilityIntegrity 校验双语跨角色一致性)
       └──► 运行时权限投影 (经 capability-enforcement 落地为 Host schema 与执行 Gate)
```

1. **单一语义所有权与投影**：
   - `Foundation/OfficeCapability.fs` 唯一定义 `ToolPermission`、`permissions` 与 `isAllowed`。`Foundation/Roles.fs`/`RolesSurface.fs` 只拥有 identity vocabulary，不含 capability matrix。
   - `Participant/Persona/OfficeCapabilitySurface.fs` 把 typed consequence 投影为 JS-native label array；跨 owner 测试不读取 F# DU representation。
   - 域模型定义五大可 fork 职位的 Entitled Consequence 与 Non-consequence 清单。
   - 同一后果事实通过 `semantic-anchors.mjs` 中的 `OFFICE_CAPABILITY_ANCHORS` 锚点绑定，由 Gate F 确保 Manager、fork 工具描述及各角色 Role Law 中双语表达完全一致。

2. **不可互换性防护**：
   - 提示词与工具描述中明确携带各 Office 的负边界（negatives）。
   - 跨 Office 的越权调用在决策面被边界镜像（caller-facing boundary mirrors）拦截，在执行面被 ToolRegistry 门禁阻断。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| OFF-001 | `requirements/office-capability/tests/office-capability-integrity.test.mjs::WHAT[OFF-001] OFF_001_office_capability_is_consequence_not_tool_whitelist` |
| OFF-002 | `requirements/office-capability/tests/office-capability-integrity.test.mjs::WHAT[OFF-002] OFF_002_managed_catalog_forkable_offices_are_exactly_the_five_canonical_offices`；`requirements/office-capability/tests/office-capability-integrity.test.mjs::WHAT[OFF-002] OFF_002_each_office_role_law_carries_its_entitled_consequence` |
| OFF-003 | `requirements/office-capability/tests/office-capability-integrity.test.mjs::WHAT[OFF-003] OFF_003_two_calling_names_differ_in_persona_and_depth_not_authority` |
| OFF-004 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs::WHAT[OFF-004] capability_is_consequence_model_not_tool_whitelist_transcription` |
| OFF-005 | `requirements/office-capability/tests/office-capability-integrity.test.mjs::WHAT[OFF-005] OFF_005_each_office_consequence_hits_manager_law_and_fork_description_in_both_locales` |
| OFF-006 | `requirements/office-capability/tests/office-capability-integrity.test.mjs::WHAT[OFF-006] OFF_006_offices_are_not_interchangeable_general_purpose_agents` |
| OFF-007 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs::WHAT[OFF-007] manager_has_no_personal_repository_witness` |
| OFF-008 | `requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs::WHAT[OFF-008] office_boundary_eval_coder_inspect_ownership_case_is_red_and_green`；`requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs::WHAT[OFF-008] office_boundary_eval_coder_inspect_oracle_is_charge_text_not_a_filter_module`；`requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs::WHAT[OFF-008] office_boundary_eval_oracles_are_not_wired_into_production_tools` |
| OFF-009 | `requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs::WHAT[OFF-009] office_boundary_eval_inspector_refuses_repair_case_is_red_and_green` |
| OFF-010 | `requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs::WHAT[OFF-010] office_boundary_eval_devops_does_not_choose_case_is_red_and_green` |
| OFF-011 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs::WHAT[OFF-011] reviewer_consequence_is_readonly_judgement_not_repair` |
| OFF-012 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs::WHAT[OFF-012] orchestrator_commissions_manager_roads_not_phases` |
| OFF-013 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs::WHAT[OFF-013] browser_consequence_is_external_facts_with_provenance_not_local_repo` |
| OFF-014 | `requirements/office-capability/tests/office-capability-role-law-contract.test.mjs::WHAT[OFF-014] inquiry_consequence_is_semantic_understanding_not_evidence_minting` |
