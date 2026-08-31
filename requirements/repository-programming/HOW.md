# repository-programming — HOW

## 架构模型与执行流

`repository-programming` 实现了从静态权限到可编程动态沙箱的完整投影链路：

```text
AttemptExecutionProfile.ToolCapabilitySet
  ↓
JsToolGenerator (生成 js-<role> 工具定义、基类、描述与示例)
  ↓
ToolRegistry (验证被调用工具名属于当前生成的合法 surface)
  ↓
JsSandbox (启动隔离执行环境，注入只读与事务 Staging 原语)
  ↓
执行 JsProgram.run() → 收集返回值、ReadSet 与 Staged WriteSet
  ↓
JSON 兼容性与合法性校验 (失败 → INVALID_RETURN_VALUE，零提交)
  ↓
事务预检 Preflight (路径合规、UTF-8、冲突检测、同路径单意图)
  ↓
WriteSet 非空: EventStore.appendPrepared → 顺序写入磁盘 → EventStore.appendCommitted
WriteSet 为空: 跳过提交
  ↓
Synthetic TOML 渲染器 (# ok / # failed + [data] / [fs])
```

## 核心机制

### 1. 投影与四层同构

- **代码生成**：根据 `ToolCapabilitySet`（Read, Write, Edit, Glob, Grep）按需拼接 `JsProgram` 基类方法声明、工具说明文本与 canonical examples。
- **运行时拦截**：沙箱内部通过绑定代理将 `file`, `glob`, `grep`, `rewrite`, `write` 路由至受控实现。未被授予的方法在基类中完全不存在，若通过反射强行调用则由底层代理 fail closed。

### 2. 沙箱隔离与资源边界

- 用户代码通过隔离机制调用，禁止注入 `require`, `process`, `fs`, `fetch` 等具有 ambient OS authority 的对象。
- 每次调用配置硬性执行 deadline 与输出缓冲区上限；同步无限循环或异步超时均由宿主环境强制终止并回收。

### 3. 事务生命周期与持久化

- **Staging**：所有 `rewrite` 与 `write` 只在内存维护 `StagedMutation` 列表，不修改实际文件。
- **Preflight**：在落盘前核验目标文件指纹是否与初次读取一致；若外部发生变更，立即报告 `FILE_CHANGED` 并中止。
- **EventStore 闭环**：多文件提交前先持久化 `JsTransactionPrepared` 事件；落盘成功后追加 `JsTransactionCommitted`。进程若在两事件之间中断，未完成事务仅作审计记录，重启后不自动回滚或补齐。

### 4. 工具描述的行为引导

- 工具描述内嵌风险中断与失败反思规则，明确要求模型在定位代码时优先声明有序锚点（`file(matches)`），禁止将大范围字符串截取或正则替换作为默认重构手段。
- 引导模型在返回前对关键规模和不变量进行断言，保证异常情况下 staging 自动废弃，杜绝污染工作区。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REPOSITORY-PROGRAMMING-001 | `requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-001] JS001_generate_none_when_no_filesystem_capability`；`requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-001] JS001_role_projection_is_exactly_roles_permissions_intersection` |
| REPOSITORY-PROGRAMMING-002 | `requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-002] JS004_capability_exactness_plus_one_ultra_example_coder`；`requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-002] JS004_absent_capability_is_absent_in_all_four_layers`；`requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-002] JS004_lying_generator_counterexample_is_rejected` |
| REPOSITORY-PROGRAMMING-003 | `requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-003] JS002_generation_is_deterministic_and_names_js_role`；`requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-003] JS004_fast_deep_profiles_generate_identical_surfaces` |
| REPOSITORY-PROGRAMMING-004 | `requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-004] JS001_generated_name_gate_rejects_forged_names` |
| REPOSITORY-PROGRAMMING-005 | `requirements/repository-programming/tests/js-tool-host.test.mjs::WHAT[REPOSITORY-PROGRAMMING-005] JS003_hook_must_not_recommend_invisible_tools`；`requirements/repository-programming/tests/js-tool-host.test.mjs::WHAT[REPOSITORY-PROGRAMMING-005] JS073_spec_carries_generated_name_and_honest_description` |
| REPOSITORY-PROGRAMMING-006 | `requirements/repository-programming/tests/js-sandbox.test.mjs::WHAT[REPOSITORY-PROGRAMMING-006] JS011_api_is_the_only_authority_in_the_context`；`requirements/repository-programming/tests/js-sandbox.test.mjs::WHAT[REPOSITORY-PROGRAMMING-006] JS054_1_sync_infinite_loop_is_killed_by_vm_timeout`；`requirements/repository-programming/tests/js-sandbox.test.mjs::WHAT[REPOSITORY-PROGRAMMING-006] JS054_1_async_deadline_proxy_aborts_api_calls_after_deadline`；`requirements/repository-programming/tests/js-sandbox.test.mjs::WHAT[REPOSITORY-PROGRAMMING-006] JS054_2_output_bound_rejects_oversized_results` |
| REPOSITORY-PROGRAMMING-007 | `requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-007] JS005_readUtf8_reads_and_classifies`；`requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-007] JS006_findAnchor_ordered_string_and_regex`；`requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-007] JS006_requireUnique_refuses_ambiguous_anchors` |
| REPOSITORY-PROGRAMMING-008 | `requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-008] JS007_glob_deterministic_enumeration`；`requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-008] JS007_glob_gitignore_skips_git_and_ignored` |
| REPOSITORY-PROGRAMMING-009 | `requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-009] JS020_grep_returns_line_column_and_skips_ignored` |
| REPOSITORY-PROGRAMMING-010 | `requirements/repository-programming/tests/js-transaction.test.mjs::WHAT[REPOSITORY-PROGRAMMING-010] JS026_same_path_once_rejects_duplicate_mutation_targets`；`requirements/repository-programming/tests/js-transaction.test.mjs::WHAT[REPOSITORY-PROGRAMMING-010] JS008_009_rewrite_requires_existing_target_create_requires_missing` |
| REPOSITORY-PROGRAMMING-011 | `requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-011] JS010_array_null_is_invalid_return_value`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-011] JS010_mixed_object_array_is_invalid` |
| REPOSITORY-PROGRAMMING-012 | `requirements/repository-programming/tests/js-tools-transaction-store.test.mjs::WHAT[REPOSITORY-PROGRAMMING-012] JS012_prepare_then_commit_updates_only_integrator_Current` |
| REPOSITORY-PROGRAMMING-013 | `requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-013] JS013_commitPlan_all_or_nothing`；`requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-013] JS013_commitPlan_aborts_before_write_when_snapshot_fails`；`requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-013] JS013_commitPlan_rolls_back_written_files_on_write_failure`；`requirements/repository-programming/tests/js-transaction.test.mjs::WHAT[REPOSITORY-PROGRAMMING-013] JS013_commit_plan_is_exact` |
| REPOSITORY-PROGRAMMING-014 | `requirements/repository-programming/tests/js-transaction.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_stale_rewrite_is_a_conflict_with_no_retry`；`requirements/repository-programming/tests/js-transaction.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_preflight_covers_read_only_snapshots_and_create_absence`；`requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_commitPlan_rejects_a_create_race_without_overwriting`；`requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_commitPlan_rejects_a_stale_rewrite_without_overwriting`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_workflow_rejects_a_changed_read_only_dependency`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_workflow_rejects_a_create_target_added_after_staging`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_workflow_tracks_every_file_scanned_by_grep` |
| REPOSITORY-PROGRAMMING-015 | `requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_rollbackPlan_is_CAS_and_preserves_third_party_changes`；`requirements/repository-programming/tests/js-transaction.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_rollback_plan_is_exact`；`requirements/repository-programming/tests/js-tools-transaction-store.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_prepared_without_committed_is_interrupted_tool_evidence`；`requirements/repository-programming/tests/js-tools-transaction-store.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_reopening_store_never_undoes_an_interrupted_tool`；`requirements/repository-programming/tests/js-tools-transaction-store.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_store_source_has_no_manual_history_reader` |
| REPOSITORY-PROGRAMMING-016 | `requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-016] JS016_result_renders_stable_toml_shapes`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-016] JS010_016_query_object_has_data_and_no_fs`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-016] JS010_016_primitive_return_uses_data_field` |
| REPOSITORY-PROGRAMMING-017 | `requirements/repository-programming/tests/js-parallel-contract.test.mjs::WHAT[REPOSITORY-PROGRAMMING-017] JS018_generated_surface_teaches_parallel_safety_for_edits_and_reads`；`requirements/repository-programming/tests/js-parallel-contract.test.mjs::WHAT[REPOSITORY-PROGRAMMING-017] JS018_consecutive_transactions_re_snapshot_committed_state_no_lost_update`；`requirements/repository-programming/tests/js-parallel-contract.test.mjs::WHAT[REPOSITORY-PROGRAMMING-017] JS018_interleaved_reads_are_immutable_snapshots_not_mutation_aliases` |
| REPOSITORY-PROGRAMMING-018 | `requirements/repository-programming/tests/js-anchors.test.mjs::WHAT[REPOSITORY-PROGRAMMING-018] JS019_failure_codes_are_stable_and_unique` |
| REPOSITORY-PROGRAMMING-019 | `requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-019] JS085_workflow_preflight_blocks_stale_rewrite_without_touching_disk`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-019] JS085_workflow_program_error_fails_without_commit`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-019] JS019_invalid_return_value_commits_nothing` |
| REPOSITORY-PROGRAMMING-020 | `requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_specs_carry_names_descriptions_and_arguments`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_moves_a_file`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_renames_a_directory_with_contents`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_missing_source_returns_error`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_requires_source_and_destination`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_removes_a_file`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_removes_an_empty_directory`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_refuses_a_non_empty_directory`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_missing_path_returns_error`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_rename_failure_surfaces_os_message` |
| REPOSITORY-PROGRAMMING-021 | `requirements/repository-programming/tests/js-surface-gate.test.mjs::WHAT[REPOSITORY-PROGRAMMING-021] JS_SURFACE_GATE_handwritten_tokens_use_inquiry_not_meditator`；`requirements/repository-programming/tests/js-surface-gate.test.mjs::WHAT[REPOSITORY-PROGRAMMING-021] JS_SURFACE_GATE_rejects_handwritten_js_coder_outside_permission_matrix`；`requirements/repository-programming/tests/js-surface-gate.test.mjs::WHAT[REPOSITORY-PROGRAMMING-021] JS_SURFACE_GATE_allows_permission_matrix_enumeration` |
| REPOSITORY-PROGRAMMING-022 | `requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-022] JS_description_teaches_tool_choice_through_paid_failure_memory` |
