# work-record — HOW

## 架构与核心机制

LifecycleWorkRecord（LWR）提供跨边界传递的单一结构化工作记录：
- **Opening**：记录初始委托与约束（支持 BlindPlan constitutive material）。
- **Chronicle**：已由压缩机制沉淀的 frame 列表。
- **Recent work**：压缩游标之后的原始未覆盖 suffix，其中最后一条助手文本作为正式陈述。

### 物化机制

1. **Full-lifecycle 物化**：从全局快照与 XTrace 游标物化完整生命周期记录。
2. **Bounded 物化**：根据指定的 `BoundedRange`（`[StartInclusive, EndExclusive)`）过滤 frames 与 trace 范围，计算局部 coverage，并默认将 `includeOpening` 置为 false。带 `ProviderRun` 的 run-bounded 投影把范围起点推进到本 invocation 首个 assistant part，确保 caller 已知的 user charge 不进入子→父 LWR。
3. **分向投影控制**：通过 `includeOpening` 参数控制是否在最终 Markdown 中渲染 Opening 节，段落为空时整段省略，段落标识由外层 wire 注入。

## 依赖关系

DEPENDS ON:
- `semantic-trace`
- `context-compression`
- `participant-horizon`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| WORK-RECORD-001 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-001] LWR_same_record_projected_two_ways_shares_work_facts` |
| WORK-RECORD-002 | `requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs::WHAT[WORK-RECORD-002] COMPANION_015_bounded_chronicle_excludes_prior_invocation_y_frames` |
| WORK-RECORD-003 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-003] LWR_y_frames_cover_prefix_and_x_supplies_only_suffix` |
| WORK-RECORD-004 | `requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs::WHAT[WORK-RECORD-004] COMPANION_015_bounded_chronicle_heading_omitted_when_invocation_has_no_y` |
| WORK-RECORD-005 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-005] LWR_gap_starts_at_record_coverage_not_prefix_cutoff` |
| WORK-RECORD-006 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-006] LWR_child_opening_excludes_parent_work_record_envelope` |
| WORK-RECORD-007 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-007] LWR_parent_to_child_includes_opening` |
| WORK-RECORD-008 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-008] LWR_opening_prompt_is_byte_exact_and_appears_exactly_once` |
| WORK-RECORD-009 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-009] LWR_t1_commitment_call_result_is_constitutive_opening_material` |
| WORK-RECORD-010 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-010] LWR_materialization_is_deterministic` |
| WORK-RECORD-011 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-011] LWR_last_assistant_text_is_in_recent_work_not_a_closing_report` |
| WORK-RECORD-012 | `requirements/work-record/tests/lwr-prose-claim-no-schema.test.mjs::WHAT[WORK-RECORD-012] LWR_prose_claim_never_renders_fixed_report_headings` |
| WORK-RECORD-013 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-013] LWR_gap_excludes_raw_tool_call_and_result_but_keeps_text_and_reasoning` |
| WORK-RECORD-014 | `requirements/work-record/tests/lwr-record-coverage-vs-prefix-coverage.test.mjs::WHAT[WORK-RECORD-014] LWR_recent_work_can_start_mid_turn_at_record_coverage` |
| WORK-RECORD-015 | `requirements/work-record/tests/lifecycle-work-record.test.mjs::WHAT[WORK-RECORD-015] LWR_work_record_start_is_structural_floor_not_stage` |
| WORK-RECORD-016 | `requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs::WHAT[WORK-RECORD-016] COMPANION_015_bounded_review_consumes_request_range_not_session_head` |
