# semantic-trace — HOW

## 架构机制

### XTrace 核心模型与单调游标

1. **游标与半开区间**：`XTraceCursor` 维护全局单调递增的序列号。提供 `sliceBetween [start, endExclusive)` 与 `sliceFrom [start, head)` 支持对历史前沿的精确半开切片。
2. **同源多投影**：
   - `forOpening`：保留初始任务及宪章性承诺，作为 OpeningMaterial 永久封存；
   - `forWorkRecord`：过滤原始工具调用及结果，保留正文与推理，用于物化 LifecycleWorkRecord；
   - `flatten`：将消息与部件平铺为带角色的标准语义流，供下游增量记忆（Blogger）消费。
   - `XTraceMaterialization.currentProjection`：从当前 reanchor generation 的 durable part/blob 重建 canonical X semantic projection。Blogger coverage、crash/retry main rebuild 与 XWire cutoff digest 只使用此入口，不读取本次 `messages.transform` 已被其它功能改写后的 presentation。

### 捕获管线与重锚持久化

- **幂等捕获与 Provenance 分段**：`XTraceCapture` 以物理 `host-part-id` 结合消息与运行标识构造溯源标识 `g:N/msg:<id>/host-part:<id>`，防止数组下标偏移造成重复录入。
- **Retry transport membrane**：`XTracePipeline` 从 Host metadata 读取 `ProviderRetryAttempt` origin。该物理 row 继续参与 decodable-message / stable-id 坐标枚举，但捕获时写入空 semantic parts；因此后续真实 Host message 的 canonical turn 不会因过滤而重编号，同时 retry 控制文本不会进入 durable X。
- **Compaction 隔离**：宿主触发 `ContextReanchored` 时仅重置物理前缀纪元，XTrace 的已持久化部件、Opening 记录与 `RecordCoverage` 保持完全存活，保证因果历史不发生丢失。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| SEMANTIC-TRACE-001 | `requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] PERSIST_010_opening_is_captured_verbatim_and_idempotent`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] PERSIST_010_opening_preserves_authoritative_requirement_order`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] PERSIST_010_parts_append_in_strict_cursor_order`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] PERSIST_010_terminal_is_captured_once_and_idempotent`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] PERSIST_010_a_second_provider_run_gets_a_distinct_terminal_occurrence_for_reuse`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] PERSIST_010_identical_terminal_bytes_are_fresh_when_provider_run_changes`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] PERSIST_010_one_provider_run_cannot_publish_two_different_terminals`；`requirements/semantic-trace/tests/x-trace-fold.test.mjs::WHAT[SEMANTIC-TRACE-001] PERSIST_010_xtrace_facts_survive_NDJSON_and_still_fold` |
| SEMANTIC-TRACE-002 | `requirements/semantic-trace/tests/x-trace-capture.test.mjs::WHAT[SEMANTIC-TRACE-002] COMPANION_012_text_maps_to_semantic_text`；`requirements/semantic-trace/tests/x-trace-capture.test.mjs::WHAT[SEMANTIC-TRACE-002] COMPANION_012_reasoning_maps_to_semantic_reasoning`；`requirements/semantic-trace/tests/x-trace-capture.test.mjs::WHAT[SEMANTIC-TRACE-002] COMPANION_012_tool_call_drops_the_call_id`；`requirements/semantic-trace/tests/x-trace-capture.test.mjs::WHAT[SEMANTIC-TRACE-002] COMPANION_012_tool_result_drops_the_call_id`；`requirements/semantic-trace/tests/x-trace-capture.test.mjs::WHAT[SEMANTIC-TRACE-002] COMPANION_012_activity_is_dropped_not_mapped`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-002] ProviderRetryAttempt_is_transport_control_not_durable_X_semantics` |
| SEMANTIC-TRACE-003 | `requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-003] XTRACE_cursor_is_strictly_monotonic` |
| SEMANTIC-TRACE-004 | `requirements/semantic-trace/tests/x-trace-provider-run-provenance.test.mjs::WHAT[SEMANTIC-TRACE-004] SEMANTIC_TRACE_provider_run_segments_fold_projection`；`requirements/semantic-trace/tests/x-trace-provider-run-provenance.test.mjs::WHAT[SEMANTIC-TRACE-004] SEMANTIC_TRACE_reanchor_opens_a_new_provenance_generation` |
| SEMANTIC-TRACE-005 | `requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-005] XTRACE_render_is_deterministic_and_never_emits_provenance`；`requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-005] XTRACE_empty_render_is_empty_string` |
| SEMANTIC-TRACE-006 | `requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-006] XTRACE_slice_between_is_half_open_and_order_preserving`；`requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-006] XTRACE_slice_from_takes_suffix_to_head`；`requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-006] XTRACE_head_is_after_last_item_and_origin_for_empty` |
| SEMANTIC-TRACE-007 | `requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-007] XTRACE_flatten_is_the_single_semantic_source`；`requirements/semantic-trace/tests/x-trace.test.mjs::WHAT[SEMANTIC-TRACE-007] XTRACE_forWorkRecord_drops_raw_tools_keeps_text_reasoning_media`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-007] COMPANION_007_capture_projection_is_idempotent_across_transforms`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-007] XTrace_materialization_is_the_canonical_X_view_not_the_latest_request_presentation` |
| SEMANTIC-TRACE-008 | `requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs::WHAT[SEMANTIC-TRACE-008] SEMANTIC_TRACE_appendable_xtrace_facts_are_exactly_three`；`requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs::WHAT[SEMANTIC-TRACE-008] SEMANTIC_TRACE_unknown_or_speculative_facts_leave_xtrace_untouched` |
| SEMANTIC-TRACE-009 | `requirements/semantic-trace/tests/x-trace-compaction-survival.test.mjs::WHAT[SEMANTIC-TRACE-009] SEMANTIC_TRACE_reanchor_preserves_xtrace_parts_and_opening`；`requirements/semantic-trace/tests/x-trace-compaction-survival.test.mjs::WHAT[SEMANTIC-TRACE-009] SEMANTIC_TRACE_reanchor_does_not_reset_the_cursor_sequence` |
| SEMANTIC-TRACE-010 | `requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-010] COMPANION_003_capture_opening_takes_authoritative_requirements`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-010] COMPANION_003_opening_capture_is_idempotent_for_the_same_text`；`requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs::WHAT[SEMANTIC-TRACE-010] COMPANION_003_parent_work_record_renders_the_opening_exactly_once` |
