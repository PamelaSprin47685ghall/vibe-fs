# PROOF — 测试落点表

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（物理移入本包）/ `NEW`（本包新写）/ `REUSE`（留在原处，记录锚点与 cutover 计划）。
> 单跑：`WANXIANGSHU_PROVIDER_LANGUAGE=en node --test requirements/repository-programming/tests/<file>`（与 `requirements/verification-system/tests/run.mjs` 一致设 en；shell 若导出其它语言值会改变本地化文案断言）。
> 全套：`node requirements/verification-system/tests/run.mjs`；L0 门：`node scripts/check.mjs`。

## 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| `REPOSITORY-PROGRAMMING-001` | `js-surface.test.mjs` → `JS001_generate_none_when_no_filesystem_capability` / `JS001_role_projection_is_exactly_roles_permissions_intersection` / `JS001_non_fs_permissions_never_produce_members` | MOVE | `node --test requirements/repository-programming/tests/js-surface.test.mjs` |
| `REPOSITORY-PROGRAMMING-002` | `js-surface.test.mjs` → `JS004_capability_exactness_plus_one_ultra_example_coder` / `JS004_absent_capability_is_absent_in_all_four_layers` / `JS004_member_gate_binds_present_members_only` | MOVE | 同上 |
| `REPOSITORY-PROGRAMMING-003` | `js-surface.test.mjs` → `JS002_generation_is_deterministic_and_names_js_role` / `JS002_same_capabilities_share_mechanics_but_role_shapes_the_ultra_example` / `JS004_fast_deep_profiles_generate_identical_surfaces` | MOVE | 同上 |
| `REPOSITORY-PROGRAMMING-004` | `js-surface.test.mjs` → `JS001_generated_name_gate_rejects_forged_names` | MOVE | 同上 |
| `REPOSITORY-PROGRAMMING-005` | `js-surface.test.mjs` → `JS004_absent_capability_is_absent_in_all_four_layers` + `JS002_description_embeds_spec_base_class_rules_and_one_ultra_example`（`_api` absent）；`js-tool-host.test.mjs` → `JS003_hook_must_not_recommend_invisible_tools` | MOVE | `node --test requirements/repository-programming/tests/js-tool-host.test.mjs` |
| `REPOSITORY-PROGRAMMING-006` | `js-sandbox.test.mjs` → 全部 8 个 test（`JS011_api_is_the_only_authority_in_the_context` / `JS054_1_sync_infinite_loop_is_killed_by_vm_timeout` / `JS054_1_async_deadline_proxy_aborts_api_calls_after_deadline` / `JS054_2_output_bound_rejects_oversized_results` 等）；交叉 `js-bindings.test.mjs` → `JS011_sandbox_program_uses_bindings_end_to_end` | MOVE | `node --test requirements/repository-programming/tests/js-sandbox.test.mjs` |
| `REPOSITORY-PROGRAMMING-007` | `js-tools-fs.test.mjs` → `JS005_readUtf8_reads_and_classifies` / `JS006_findAnchor_ordered_string_and_regex` / `JS006_requireUnique_refuses_ambiguous_anchors`；`js-anchors.test.mjs` → 全部 3 个 test；`js-workflow.test.mjs` → `JS005_offset_anchor_clips_to_closed_file_range` / `JS005_offset_N_is_string_index_not_line_number` / `JS006_019_missing_anchor_is_typed_and_names_the_pattern` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-anchors.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-008` | `js-tools-fs.test.mjs` → `JS007_glob_deterministic_enumeration` / `JS007_glob_gitignore_skips_git_and_ignored`；交叉 `js-bindings.test.mjs` → `JS007_bindings_path_boundary_denies_escape` / `JS007_bindings_glob_lists_matching_paths` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-bindings.test.mjs` |
| `REPOSITORY-PROGRAMMING-009` | `js-tools-fs.test.mjs` → `JS020_grep_returns_line_column_and_skips_ignored`；交叉 `js-bindings.test.mjs` → `JS010_bindings_grep_returns_matches` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-bindings.test.mjs` |
| `REPOSITORY-PROGRAMMING-010` | `js-transaction.test.mjs` → `JS008_009_rewrite_requires_existing_target_create_requires_missing` / `JS026_same_path_once_rejects_duplicate_mutation_targets`；交叉 `js-bindings.test.mjs` → `JS008_012_bindings_rewrite_stages_without_touching_disk` / `JS009_012_bindings_write_stages_create` | MOVE | `node --test requirements/repository-programming/tests/js-transaction.test.mjs requirements/repository-programming/tests/js-bindings.test.mjs` |
| `REPOSITORY-PROGRAMMING-011` | `js-workflow.test.mjs` → `JS010_array_null_is_invalid_and_does_not_commit` / `JS010_mixed_object_array_is_invalid`；`js-sandbox.test.mjs` → `JS010_circular_return_is_invalid_return_value` | MOVE | `node --test requirements/repository-programming/tests/js-workflow.test.mjs requirements/repository-programming/tests/js-sandbox.test.mjs` |
| `REPOSITORY-PROGRAMMING-012` | `js-tools-transaction-store.test.mjs` → `JS012_prepare_then_commit_leaves_no_uncommitted` / `JS012_append_failure_surfaces_prepare_failed_path`；交叉 `js-bindings.test.mjs` → `JS008_012_bindings_rewrite_stages_without_touching_disk`（staging 不碰盘）；`js-workflow.test.mjs` → `JS012_workflow_with_store_persists_prepare_and_commit` | MOVE | `node --test requirements/repository-programming/tests/js-tools-transaction-store.test.mjs requirements/repository-programming/tests/js-bindings.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-013` | `js-tools-fs.test.mjs` → `JS013_commitPlan_all_or_nothing` / `JS013_commitPlan_aborts_before_write_when_snapshot_fails` / `JS013_commitPlan_rolls_back_written_files_on_write_failure`；`js-transaction.test.mjs` → `JS013_preflight_orders_rules_and_short_circuits` / `JS013_015_commit_and_rollback_plans_are_exact`；`js-workflow.test.mjs` → `JS085_workflow_reads_and_commits_rewrite` / `JS085_workflow_commits_create_and_reports` / `JS085_workflow_file_missing_fails_the_program` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-transaction.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-014` | `js-transaction.test.mjs` → `JS014_stale_rewrite_is_a_conflict_with_no_retry`；`js-workflow.test.mjs` → `JS085_workflow_preflight_blocks_stale_rewrite_without_touching_disk` | MOVE | `node --test requirements/repository-programming/tests/js-transaction.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-015` | `js-tools-fs.test.mjs` → `JS015_rollbackPlan_restores_originals_and_removes_creates`；`js-tools-transaction-store.test.mjs` → `JS015_prepared_without_committed_is_a_recovery_candidate` / `JS015_recover_undoes_only_what_we_wrote` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-tools-transaction-store.test.mjs` |
| `REPOSITORY-PROGRAMMING-016` | `js-workflow.test.mjs` → `JS016_result_renders_stable_toml_shapes` / `JS010_016_query_object_has_data_and_no_fs` / `JS010_016_primitive_return_uses_data_field` | MOVE | `node --test requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-017` | `js-parallel-contract.test.mjs`（NEW）→ `JS018_generated_surface_teaches_parallel_safety_for_edits_and_reads` / `JS018_consecutive_transactions_re_snapshot_committed_state_no_lost_update` / `JS018_interleaved_reads_are_immutable_snapshots_not_mutation_aliases`；交叉 REUSE `tests/integration/plugin/`（Host 串行执行面，SPLIT@cutover 下表） | NEW + REUSE | `node --test requirements/repository-programming/tests/js-parallel-contract.test.mjs` |
| `REPOSITORY-PROGRAMMING-018` | `js-anchors.test.mjs` → `JS019_failure_codes_are_stable_and_unique`；`js-sandbox.test.mjs` → `JS019_invalid_javascript_is_invalid_program` / `JS019_program_throw_is_program_failed`；`js-workflow.test.mjs` → `JS006_019_missing_anchor_is_typed_and_names_the_pattern` | MOVE | `node --test requirements/repository-programming/tests/js-anchors.test.mjs requirements/repository-programming/tests/js-sandbox.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-019` | `js-workflow.test.mjs` → `JS010_array_null_is_invalid_and_does_not_commit`（非法 return 零提交）/ `JS085_workflow_program_error_fails_without_commit` / `JS085_workflow_preflight_blocks_stale_rewrite_without_touching_disk`（commit 失败不给成功结果） | MOVE | `node --test requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-020` | `file-mutation-tools.test.mjs` → 全部 11 个 test（`FILEMUT_mv_moves_a_file` / `FILEMUT_mv_renames_a_directory_with_contents` / `FILEMUT_rm_removes_a_file` / `FILEMUT_rm_refuses_a_non_empty_directory` / `FILEMUT_mv_rename_failure_surfaces_os_message` 等）；交叉 REUSE `requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs`（plugin 级 `AGENT_017_mv_*` / `AGENT_018_rm_*` + 角色门禁 `AGENT_016_*`） | MOVE + REUSE | `node --test requirements/repository-programming/tests/file-mutation-tools.test.mjs` |
| `REPOSITORY-PROGRAMMING-021` | `js-surface-gate.test.mjs` → `JS_SURFACE_GATE_handwritten_tokens_use_inquiry_not_meditator` / `JS_SURFACE_GATE_rejects_handwritten_js_coder_outside_permission_matrix` / `JS_SURFACE_GATE_allows_permission_matrix_enumeration`；门禁本体 REUSE `scripts/checks/js-surface-gate.mjs`（`node scripts/check.mjs` 内运行） | MOVE + REUSE | `node --test requirements/repository-programming/tests/js-surface-gate.test.mjs`；`node scripts/checks/js-surface-gate.mjs` |

## 统计

```text
WHAT 命题：21（REPOSITORY-PROGRAMMING-001..021）
落点：   MOVE 20 个命题（19 个纯 MOVE + 017/020/021 带 REUSE 交叉）
        NEW  1（js-parallel-contract.test.mjs ×3 test，覆盖 017）
        REUSE 3（scripts/checks/js-surface-gate.mjs、requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs、integration Host 串行面）
GAP：    0
```

## 移动文件清单（源 → 目标，均单独跑绿）

| 源 | 目标 | 断言数 | 单跑结果 |
|---|---|---|---|
| `requirements/repository-programming/tests/js-surface.test.mjs` | `requirements/repository-programming/tests/js-surface.test.mjs` | 14 pass | `node --test` 绿 |
| `requirements/repository-programming/tests/js-bindings.test.mjs` | `requirements/repository-programming/tests/js-bindings.test.mjs` | 7 pass | 绿 |
| `requirements/repository-programming/tests/js-sandbox.test.mjs` | `requirements/repository-programming/tests/js-sandbox.test.mjs` | 8 pass | 绿 |
| `requirements/repository-programming/tests/js-anchors.test.mjs` | `requirements/repository-programming/tests/js-anchors.test.mjs` | 3 pass | 绿 |
| `requirements/repository-programming/tests/js-tools-fs.test.mjs` | `requirements/repository-programming/tests/js-tools-fs.test.mjs` | 10 pass | 绿 |
| `requirements/repository-programming/tests/js-transaction.test.mjs` | `requirements/repository-programming/tests/js-transaction.test.mjs` | 5 pass | 绿 |
| `requirements/repository-programming/tests/js-tools-transaction-store.test.mjs` | `requirements/repository-programming/tests/js-tools-transaction-store.test.mjs` | 4 pass | 绿 |
| `requirements/repository-programming/tests/js-workflow.test.mjs` | `requirements/repository-programming/tests/js-workflow.test.mjs` | 14 pass | 绿 |
| `requirements/repository-programming/tests/js-tool-host.test.mjs` | `requirements/repository-programming/tests/js-tool-host.test.mjs` | 3 pass | 绿 |
| `requirements/repository-programming/tests/file-mutation-tools.test.mjs` | `requirements/repository-programming/tests/file-mutation-tools.test.mjs` | 11 pass | 绿 |
| `requirements/repository-programming/tests/js-surface-gate.test.mjs` | `requirements/repository-programming/tests/js-surface-gate.test.mjs` | 3 pass | 绿 |

适配说明：4 个文件（`js-surface`/`js-bindings`/`js-tool-host`/`js-workflow`）原直接 `import { ofArray } from '../../../dist/fable_modules/.../Set.js'`——该直接 import 是 test-boundary 门（新增 requirements scope）禁止的遗留项；迁移时改写为经 sanctioned 适配层 `requirements/verification-system/tests/support/domain.mjs` 的 `FsSet.ofArray`（同一 comparer 语义），消除 4 条 baseline 遗留，门仍绿。`../support/domain.mjs` 深度修正为 `../../../requirements/verification-system/tests/support/domain.mjs`。

## semantic anchor 归属（semantic-anchors.mjs）

本包在 `scripts/checks/semantic-anchors.mjs` 中 **拥有 0 个 anchor id**。`ROLE_SEMANTIC_ANCHORS`/`TOOL_DESCRIPTION_ANCHORS` 中 inspector/bookkeeper 锚点归 `repository-investigation`/`knowledge-reuse`；js-* 编程面不通过 prompt 锚点证明——它由生成 surface oracle（`js-surface.test.mjs` 四层 exactness）+ 静态门禁（`js-surface-gate.mjs`）证明。

## SPLIT@cutover 计划（现有测试的 owner 拆分）

| 现有文件 | 当前 owner 混合 | cutover 动作 |
|---|---|---|
| `requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs` | `repository-programming`（mv/rm POSIX 语义断言：`AGENT_017_*`/`AGENT_018_*`）+ `office-capability`/`capability-enforcement`（角色门禁：`AGENT_016_mv_and_rm_are_denied_for_non_coder_roles`、`AGENT_016_mv_and_rm_are_denied_when_the_role_is_unresolved`） | **SPLIT**：POSIX 语义断言并入本包（integration 层）；角色门禁断言归 `office-capability`（consequence）/`capability-enforcement`（gate） |
| `tests/integration/plugin/`（Host 工具调用串行执行面，REPOSITORY-PROGRAMMING-017 的 Host 侧） | Host 串行执行 = `host-boundary`（物理执行面）+ 本包（编程面合同） | **SPLIT**：模型侧合同断言并入本包；Host 物理串行语义归 `host-boundary` |
| `scripts/checks/js-surface-gate.mjs` | MECHANISM（共享 checker）；语义唯一归本包（REPOSITORY-PROGRAMMING-021） | 门禁机制留在 `scripts/checks/`；其断言 owner 记为本包；cutover 后可移入本包 tests 或保留共享（机制可共享、断言不双 owner） |
