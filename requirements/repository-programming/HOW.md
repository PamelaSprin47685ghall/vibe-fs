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

- **代码生成**：根据 `ToolCapabilitySet`（Read, Write, Edit, Glob, Grep）按需拼接 `JsProgram` 基类方法声明、工具说明文本与 canonical examples。一个 capability 可以投影有固定顺序的同权成员族；当前 `Edit` 投影 `edit`、`rewrite`，两者共享同一权限判断与底层 `js.edit` binding。Edit-only surface 不公开 `file()`，但 `edit()` 可通过注入 API 的私有 snapshot read 完成规划，并把该读取登记进 ReadSet。
- **运行时拦截**：沙箱内部通过绑定代理将 `file`, `glob`, `grep`, `rewrite`, `write` 路由至受控实现；`edit` 是生成 SDK 内的纯规划层，先经既有 `js.read` 取得不可变快照，完成定位与验证后再恰好调用一次既有 `js.edit` staging executor。未被授予的方法在基类与描述中均完全不存在，若通过反射强行调用则由底层代理 fail closed。

### 2. 沙箱隔离与资源边界

- 用户代码通过隔离机制调用，禁止注入 `require`, `process`, `fs`, `fetch` 等具有 ambient OS authority 的对象。
- 每次调用配置硬性执行 deadline 与输出缓冲区上限；同步无限循环或异步超时均由宿主环境强制终止并回收。

### 3. 事务生命周期与持久化

- **Staging**：`edit`、`rewrite` 与 `write` 最终都只在内存维护 `StagedMutation` 列表，不修改实际文件；其中一个 `edit` 调用至多形成一个 `Rewrite` intent。
- **Preflight**：提交/回滚计划先将每个逻辑路径解析一次为私有 typed mutation；预检、逐项重验、物理写入、失败分类与 CAS 回滚复用同一 resolved path。在落盘前核验目标文件指纹是否与初次读取一致；若外部发生变更，立即报告 `FILE_CHANGED` 并中止。
- **EventStore 闭环**：多文件提交前先持久化 `JsTransactionPrepared` 事件；落盘成功后追加 `JsTransactionCommitted`。进程若在两事件之间中断，未完成事务仅作审计记录，重启后不自动回滚或补齐。

### 4. 渐进式编辑代数

- **Easy path — `edit(path, changes)`**：普通 replace / insert / delete / all 只声明当前 `find` 与最终 `put`。单个 object 自动包装为数组；`oldText/newText`、`search/replace` 仅作为无歧义恢复别名。未知字段、奇异 object、空 string 或零宽 RegExp 立即 `INVALID_EDIT`，避免弱模型的参数拼写错误被静默吞掉。
- **同一快照规划**：数组中的每个 change 都寻址调用开始时的同一不可变文本，而不是前一 change 产生的中间文本。缺省模式必须唯一；`all: true` 明确承担多重性并取代 RegExp `g`，但 positional `y` 保持 sticky，绝不为提高命中率而扩大写入证据。全部命中解析完成并证明互不重叠后，才按 offset 逆序在内存构造目标文本并单次 staging。
- **Hard path — `rewrite(path, newText)`**：结构重排、计算式输出、capture-dependent 变换与任意生成逻辑仍可直接提交完整目标文件，不牺牲既有表达上限。Read 同时可用时，文档再教授 `file(matches)`、ordered anchors 与 `text()` 作为可信结构切片；Read 不可用时绝不推荐不存在的成员。
- **换行与 no-op**：一致 CRLF 文件可接受模型以 LF 引用，并在结果中恢复 CRLF；混合换行保持逐字节精确。结果等于原文时返回冻结的 `changed: false` 报告，不产生 mutation intent。

### 5. 保守失败恢复

- `INVALID_EDIT`、`EDIT_NOT_FOUND`、`EDIT_AMBIGUOUS`、`EDIT_OVERLAP` 进入稳定失败代数；任何一个 change 失败时，本次 `edit` 零 staging，整个 program 后续异常仍由既有事务语义丢弃更早路径的 staging。
- 近似逻辑只生成诊断：通过有界 token 定位与现有子串 span 评分，返回 attempted find、有限带行号窗口、有限候选以及可选 copy-ready change。它永远不参与 mutation plan；copy-ready `find` 必须是当前文件真实存在的精确子串。
- 诊断预算独立于文件行长与 `put` 大小：窗口、候选数、字段名、path 与 payload 均有上界；预算不足时省略建议而非放大失败。控制语由 ProviderResources 双语加载，稳定 code 与 API token 保持协议原样。
- `edit` 的内部读取进入既有 ReadSet。规划后若第三方改变目标，Preflight 仍返回 `FILE_CHANGED`，不会用旧快照覆盖新内容。

### 6. 工具描述的行为引导

- Edit surface 的第一屏先给 action-first 决策阶梯：普通精确修改先 `edit`，完整计算结果才 `rewrite`，Read 同时存在时结构重组才升级到 `file(matches) + text() + rewrite()`。随后才给风险中断、失败反思与完整细则，避免较弱模型读完事故叙事仍不知道第一行代码。
- replace / insert / delete / all 都有 copy-ready canonical 代码；Coder Ultra Example 用一个 program 展示跨文件 grep + 每路径单次 edit。示例只教授 `{ find, put, all? }`，恢复别名留在细则中，避免产生多个竞争语法。
- 引导模型在返回前对关键规模和不变量进行断言，保证异常情况下 staging 自动废弃，杜绝污染工作区。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REPOSITORY-PROGRAMMING-001 | `requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-001] JS001_generate_none_when_no_filesystem_capability`；`requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-001] JS001_role_projection_is_exactly_roles_permissions_intersection` |
| REPOSITORY-PROGRAMMING-002 | `requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-002] JS004_capability_exactness_plus_one_ultra_example_coder`；`requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-002] JS004_absent_capability_is_absent_in_all_four_layers`；`requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-002] JS004_edit_guidance_never_names_missing_read_or_write_members`；`requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-002] JS004_lying_generator_counterexample_is_rejected` |
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
| REPOSITORY-PROGRAMMING-014 | `requirements/repository-programming/tests/js-transaction.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_stale_rewrite_is_a_conflict_with_no_retry`；`requirements/repository-programming/tests/js-transaction.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_preflight_covers_read_only_snapshots_and_create_absence`；`requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_commitPlan_rejects_a_create_race_without_overwriting`；`requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_commitPlan_rejects_a_stale_rewrite_without_overwriting`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_workflow_rejects_a_changed_read_only_dependency`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_workflow_rejects_a_create_target_added_after_staging`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS014_workflow_tracks_every_file_scanned_by_grep`；`requirements/repository-programming/tests/js-edit.test.mjs::WHAT[REPOSITORY-PROGRAMMING-014] JS_EDIT_target_read_is_observed_and_external_change_wins` |
| REPOSITORY-PROGRAMMING-015 | `requirements/repository-programming/tests/js-tools-fs.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_rollbackPlan_is_CAS_and_preserves_third_party_changes`；`requirements/repository-programming/tests/js-transaction.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_rollback_plan_is_exact`；`requirements/repository-programming/tests/js-tools-transaction-store.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_prepared_without_committed_is_interrupted_tool_evidence`；`requirements/repository-programming/tests/js-tools-transaction-store.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_reopening_store_never_undoes_an_interrupted_tool`；`requirements/repository-programming/tests/js-tools-transaction-store.test.mjs::WHAT[REPOSITORY-PROGRAMMING-015] JS015_store_source_has_no_manual_history_reader` |
| REPOSITORY-PROGRAMMING-016 | `requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-016] JS016_result_renders_stable_toml_shapes`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-016] JS010_016_query_object_has_data_and_no_fs`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-016] JS010_016_primitive_return_uses_data_field` |
| REPOSITORY-PROGRAMMING-017 | `requirements/repository-programming/tests/js-parallel-contract.test.mjs::WHAT[REPOSITORY-PROGRAMMING-017] JS018_generated_surface_teaches_parallel_safety_for_edits_and_reads`；`requirements/repository-programming/tests/js-parallel-contract.test.mjs::WHAT[REPOSITORY-PROGRAMMING-017] JS018_consecutive_transactions_re_snapshot_committed_state_no_lost_update`；`requirements/repository-programming/tests/js-parallel-contract.test.mjs::WHAT[REPOSITORY-PROGRAMMING-017] JS018_interleaved_reads_are_immutable_snapshots_not_mutation_aliases` |
| REPOSITORY-PROGRAMMING-018 | `requirements/repository-programming/tests/js-anchors.test.mjs::WHAT[REPOSITORY-PROGRAMMING-018] JS019_failure_codes_are_stable_and_unique` |
| REPOSITORY-PROGRAMMING-019 | `requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-019] JS085_workflow_preflight_blocks_stale_rewrite_without_touching_disk`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-019] JS085_workflow_program_error_fails_without_commit`；`requirements/repository-programming/tests/js-workflow.test.mjs::WHAT[REPOSITORY-PROGRAMMING-019] JS019_invalid_return_value_commits_nothing` |
| REPOSITORY-PROGRAMMING-020 | `requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_specs_carry_names_descriptions_and_arguments`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_moves_a_file`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_renames_a_directory_with_contents`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_missing_source_returns_error`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_requires_source_and_destination`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_removes_a_file`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_removes_an_empty_directory`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_refuses_a_non_empty_directory`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_rm_missing_path_returns_error`；`requirements/repository-programming/tests/file-mutation-tools.test.mjs::WHAT[REPOSITORY-PROGRAMMING-020] FILEMUT_mv_rename_failure_surfaces_os_message` |
| REPOSITORY-PROGRAMMING-021 | `requirements/repository-programming/tests/js-surface-gate.test.mjs::WHAT[REPOSITORY-PROGRAMMING-021] JS_SURFACE_GATE_handwritten_tokens_use_inquiry_not_meditator`；`requirements/repository-programming/tests/js-surface-gate.test.mjs::WHAT[REPOSITORY-PROGRAMMING-021] JS_SURFACE_GATE_rejects_handwritten_js_coder_outside_permission_matrix`；`requirements/repository-programming/tests/js-surface-gate.test.mjs::WHAT[REPOSITORY-PROGRAMMING-021] JS_SURFACE_GATE_allows_permission_matrix_enumeration` |
| REPOSITORY-PROGRAMMING-022 | `requirements/repository-programming/tests/js-surface.test.mjs::WHAT[REPOSITORY-PROGRAMMING-022] JS_description_is_action_first_then_teaches_paid_failure_memory` |
| REPOSITORY-PROGRAMMING-023 | `requirements/repository-programming/tests/js-edit.test.mjs::WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_exact_batch_replaces_inserts_and_deletes_with_one_rewrite`；`JS_EDIT_only_surface_has_private_snapshot_read_without_public_file_member`；`JS_EDIT_accepts_single_object_and_unambiguous_common_aliases`；`JS_EDIT_all_applies_every_non_overlapping_string_or_regexp_match`；`JS_EDIT_preserves_a_consistent_CRLF_file_when_callers_author_LF`；`JS_EDIT_every_change_addresses_the_original_snapshot_and_failure_is_atomic`；`JS_EDIT_noop_succeeds_without_a_rewrite_intent`；`JS_EDIT_later_file_failure_discards_earlier_file_staging` |
| REPOSITORY-PROGRAMMING-024 | `requirements/repository-programming/tests/js-edit.test.mjs::WHAT[REPOSITORY-PROGRAMMING-024] JS_EDIT_near_match_is_copy_ready_diagnostic_but_never_write_authority`；`JS_EDIT_ambiguous_match_returns_candidates_and_two_safe_next_moves`；`JS_EDIT_overlap_and_invalid_shape_have_stable_codes_and_zero_commit`；`JS_EDIT_rejects_invalid_path_unknown_fields_and_exotic_change_objects`；`JS_EDIT_diagnostics_are_bounded_and_echo_the_attempted_find`；`JS_EDIT_copy_ready_fix_uses_the_exact_candidate_subspan`；`JS_EDIT_failure_control_language_is_localized` |
| REPOSITORY-PROGRAMMING-025 | `requirements/repository-programming/tests/m6-fatal-boundary.test.mjs::WHAT[REPOSITORY-PROGRAMMING-025] transaction fatal preserves rollback or cut settlement and one injected fuse` |

## GAP 状态

- **GAP-030 — CLOSED**：普通局部修改曾只能由模型手工重建完整文件，且 mismatch 缺少可复制、保守、有界的恢复反馈。现由 `js-edit.test.mjs` 与 capability-projected surface oracle 独立承载；closing feature commit `e54e51ed5`。
