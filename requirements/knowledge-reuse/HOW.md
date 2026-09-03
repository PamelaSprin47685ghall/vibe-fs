# knowledge-reuse — HOW

## 架构机制与核心模型

### 1. 观察捕获与重放管线

1. **类型化捕获（Capture）**：
   - 监听 Inspector 工具调用，对 `read` 生成 `FileRead(path, contentHash)`、对 `glob` 生成 `GlobResult(pattern, paths)`、对 `grep` 生成 `GrepResult(pattern, matches)`；
   - 提取的观察经 `Observations.normalize` 执行按路径与内容的稳定去重和排序，折叠为规范的观察集合。

2. **只读重放（Replay）**：
   - `FetchTool.Execute` 首先复用 `CasebookFeature.isEnabled(workspaceRoot)` 检查 marker；未启用时不解析 shelfmark、不构建索引、不触碰事件流；
   - `fetch` 调用首先通过 `CasebookReplay.replayAll` 对当前工作区重放已记录的各条 observation；
   - 比对重放结果：若与原集合完全一致，判定为 `Fresh` 并直接返回原规范答案；若存在差异，判定为 `Stale` 并转入刷新流程。

### 2. Bookkeeper 维护与事务机制

1. **事务 Staging 与 SDK**：
   - `BookkeeperStaging` 提供 `beginTransaction`、`snapshot`、`apply` 与 `take` 操作；
   - `js-bookkeeper(program)` 执行传入的 JS 代码，在沙箱中提供 `setQuestion` 与 `setAnswer` 接口，支持单事务内的原子修改与异常自动回滚。

2. **生命周期与 Finalize**：
   - `Lifecycle` 模块管理草稿收集；在 ReuseScope 关闭时触发恰好一次 `tryFinalizeInspector`，生成归档请求并持久化。

### 3. 持久化与索引投影

1. **统一事件流（Store）**：
   - 归属统一 EventStore 的 `casebook` 流，支持 `InspectorCaseCaptured`、`InspectorCaseRefreshed`、`InspectorCaseAccessed` 与 `InspectorCaseEvicted` 事件；
   - 大文本通过 `PayloadRef` 存储在 blob 存储中，事件体仅保留引用与元数据。

2. **低信任索引（Index）**：
   - `CasebookIndex` 管理 `{ shelfmark, canonicalQuestion }` 快照，按 epoch 缓存冻结；
   - 当检测到可见集合变化或显式失效时推进 epoch，保证同一 epoch 内提示词字节完全稳定。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| KNOWLEDGE-REUSE-001 | `requirements/knowledge-reuse/tests/casebook-store.test.mjs::WHAT[KNOWLEDGE-REUSE-001] CASE001_casebook_is_best_effort_semantic_cache_with_readonly_observation_replay` |
| KNOWLEDGE-REUSE-002 | `requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs::WHAT[KNOWLEDGE-REUSE-002] lifecycle_notePrompt_noteAnswer_tryFinalize_creates_case_once` |
| KNOWLEDGE-REUSE-003 | `requirements/knowledge-reuse/tests/casebook-capture.test.mjs::WHAT[KNOWLEDGE-REUSE-003] CASE003_read_capture_is_typed_and_hashed`；`requirements/knowledge-reuse/tests/casebook-capture.test.mjs::WHAT[KNOWLEDGE-REUSE-003] CASE003_glob_capture_parses_rendered_paths`；`requirements/knowledge-reuse/tests/casebook-capture.test.mjs::WHAT[KNOWLEDGE-REUSE-003] CASE003_grep_capture_keeps_match_lines`；`requirements/knowledge-reuse/tests/casebook-capture.test.mjs::WHAT[KNOWLEDGE-REUSE-003] CASE003_unknown_tool_yields_nothing`；`requirements/knowledge-reuse/tests/casebook-capture.test.mjs::WHAT[KNOWLEDGE-REUSE-003] S63_executor_reading_positives`；`requirements/knowledge-reuse/tests/casebook-capture.test.mjs::WHAT[KNOWLEDGE-REUSE-003] S63_executor_reading_negatives_skip_safely` |
| KNOWLEDGE-REUSE-004 | `requirements/knowledge-reuse/tests/fetch-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-004] CASE004_fetch_uses_shelfmark_and_replays_before_refreshing`；`requirements/knowledge-reuse/tests/fetch-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-004] CASE009_fetch_never_writes_the_subject` |
| KNOWLEDGE-REUSE-005 | `requirements/knowledge-reuse/tests/casebook-store.test.mjs::WHAT[KNOWLEDGE-REUSE-005] CASE004_005_freshness_check_is_hint_not_proof_reads_Current_only` |
| KNOWLEDGE-REUSE-006 | `requirements/knowledge-reuse/tests/js-bookkeeper-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_surface_is_program_only_and_has_case_sdk`；`requirements/knowledge-reuse/tests/js-bookkeeper-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_program_reshapes_question_and_answer_atomically`；`requirements/knowledge-reuse/tests/js-bookkeeper-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_zero_mutation_is_legal`；`requirements/knowledge-reuse/tests/js-bookkeeper-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_duplicate_set_rolls_back_the_whole_program`；`requirements/knowledge-reuse/tests/js-bookkeeper-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_program_failure_rolls_back_staged_mutation`；`requirements/knowledge-reuse/tests/js-bookkeeper-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_unbound_session_cannot_change_a_case` |
| KNOWLEDGE-REUSE-007 | `requirements/knowledge-reuse/tests/casebook-store.test.mjs::WHAT[KNOWLEDGE-REUSE-007] CASE007_captured_refreshed_round_trip_through_integrator_Current`；`requirements/knowledge-reuse/tests/casebook-store.test.mjs::WHAT[KNOWLEDGE-REUSE-007] CASE007_accessed_and_evicted_are_integrated_without_feature_history_scan`；`requirements/knowledge-reuse/tests/casebook-store.test.mjs::WHAT[KNOWLEDGE-REUSE-007] CASE007_store_has_no_loadEvents_project_or_history_reader` |
| KNOWLEDGE-REUSE-008 | `requirements/knowledge-reuse/tests/casebook-domain.test.mjs::WHAT[KNOWLEDGE-REUSE-008] CASE008_fold_accessed_and_evicted_derives_access_order`；`requirements/knowledge-reuse/tests/casebook-domain.test.mjs::WHAT[KNOWLEDGE-REUSE-008] CASE008_lru_evict_keeps_most_recently_accessed` |
| KNOWLEDGE-REUSE-009 | `requirements/knowledge-reuse/tests/casebook-store.test.mjs::WHAT[KNOWLEDGE-REUSE-009] CASE009_marker_gates_the_surface`；`requirements/knowledge-reuse/tests/fetch-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-009] CASE009_fetch_execution_rejects_a_workspace_without_the_marker` |
| KNOWLEDGE-REUSE-010 | `requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs::WHAT[KNOWLEDGE-REUSE-010] lifecycle_cleanupInspector_never_publishes_case`；`requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs::WHAT[KNOWLEDGE-REUSE-010] lifecycle_missing_answer_is_noop_finalize` |
| KNOWLEDGE-REUSE-011 | `requirements/knowledge-reuse/tests/fetch-tool.test.mjs::WHAT[KNOWLEDGE-REUSE-011] CASE011_fetch_single_flight_serializes_same_shelfmark` |
| KNOWLEDGE-REUSE-012 | `requirements/knowledge-reuse/tests/casebook-index.test.mjs::WHAT[KNOWLEDGE-REUSE-012] CASEBOOK_index_exposes_shelfmark_and_canonical_question_only`；`requirements/knowledge-reuse/tests/casebook-index.test.mjs::WHAT[KNOWLEDGE-REUSE-012] CASEBOOK_shelfmark_is_stable_and_not_the_session_identity`；`requirements/knowledge-reuse/tests/casebook-index.test.mjs::WHAT[KNOWLEDGE-REUSE-012] CASEBOOK_invalidate_then_refresh_advances_epoch`；`requirements/knowledge-reuse/tests/casebook-index.test.mjs::WHAT[KNOWLEDGE-REUSE-012] CASEBOOK_visible_set_change_advances_epoch` |
| KNOWLEDGE-REUSE-013 | `requirements/knowledge-reuse/tests/m6-fatal-boundary.test.mjs::WHAT[KNOWLEDGE-REUSE-013] Casebook fatal follows durable cut settlement and one injected fuse` |
