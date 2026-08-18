# HOW — repository-programming 的实现模型与约束

> 非 normative。描述当前实现如何满足 WHAT；实现可整体替换（`17-repository.md` INDEPENDENT CHANGE：换 embedded language/IR 而合同不变）。

## 模块地图（当前实现）

### Domain（纯决策；零 Host I/O）

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Domain/JsCapability.fs` | `JsCapability = Read | Write | Edit | Glob | Grep`；`JsCapabilityFragment`（member 名/description/example/runtime binding 单一事实源）；`JsFragmentRegistry` |
| `src/Wanxiangshu/Domain/JsSurface.fs` | `JsSurface` 类型；`JsToolGenerator`：`membersFor` / `toolNameFor`（`"js-" + roleName.ToLowerInvariant()`）/ `generate`（投影主函数）/ `isGeneratedToolName` / `memberBinding` |
| `src/Wanxiangshu/Domain/JsDescription.fs` | `JsCanonicalDescription`：description 组装规则 + 资源路径常量 |
| `src/Wanxiangshu/Domain/JsFailure.fs` | `JsFailure` 代数（23 个 case）+ 稳定码映射（`JsFailure.code`）；`AnchorFailure` |
| `src/Wanxiangshu/Domain/JsAnchor.fs` | `AnchorSpec = Exact | Regex`；`AnchorDeclaration`；`AnchorRules`（有序匹配/拒绝规则） |
| `src/Wanxiangshu/Domain/JsTransaction.fs` | `JsStagedMutation = Rewrite | Create`；`JsTransaction`（staging 纯逻辑）；`JsTransactionId`；`JsDurableMutation` / `JsTransactionPrepared` / `JsTransactionCommitted`；`JsTransactionFacts` |

### Infrastructure（Host 适配；唯一 I/O 点）

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Infrastructure/JsToolsBindings.fs` | `createApi(root, staging)`：注入 sandbox 的 `file`/`glob`/`grep`/`rewrite`/`write` 实现；`resolveInside` 做 path containment（escape → `PATH_DENIED`） |
| `src/Wanxiangshu/Infrastructure/JsUtf8Fs.fs` | strict UTF-8 解码（`INVALID_UTF8`） |
| `src/Wanxiangshu/Infrastructure/JsGlobFs.fs` | gitignore/wildmatch glob 实现（全量枚举、跳过 `.git`/symlink） |
| `src/Wanxiangshu/Infrastructure/JsMutationFs.fs` | 磁盘 mutation 原语（rewrite/create、compare-before-effect） |
| `src/Wanxiangshu/Infrastructure/JsAnchorFs.fs` | 锚点解析的 fs 侧实现 |
| `src/Wanxiangshu/Infrastructure/JsToolsTransactionStore.fs` | EventStore 适配：`TransactionStream = "js-tools/transactions"`；`PreparedEventType = "JsTransactionPrepared"` / `CommittedEventType = "JsTransactionCommitted"`；`appendPrepared` / `appendCommitted`；recovery 读取 |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/JsToolWorkflow.fs` | `JsToolsData`（JSON 值树 parse + REPOSITORY-PROGRAMMING-011 校验，`validateArray`/`ofJsValue`）；`JsToolWorkflow.run`（sandbox → 收 ReadSet/WriteSet → preflight → prepare → commit）；`JsToolsResult.render`（REPOSITORY-PROGRAMMING-016 两份文档） |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/JsToolHost.fs` | `BuiltinToolDescriptionHook`（`validateRecommendation` fail-closed、`annotate`）；`JsDescriptionAssets`（双语 prose 装载）；`JsToolSpec.create`（生成 spec） |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/FileMutationTools.fs` | `mvSpec` / `rmSpec`（POSIX 语义 + 本地化 consequence） |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/ToolRegistry.fs` | `rolePredicate`（spec 可见性）+ 执行 gate（invoked name 属于当前 surface） |

### Process

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Process/JsSandbox.fs` | `wrapProgram`（base class + model source → framed program）；`run` / `runSurface`；`classifySyncError`；sentinel 前缀 `__jsProgramFailed` / `__jsHostFailed` / `__jsInvalidReturn`；deadline 超时 kill；输出 bound |

### 静态门禁

`scripts/checks/js-surface-gate.mjs`：`HANDWRITTEN_ROLE_TOOL_TOKENS`（js-coder 等字面量）→ 扫描 `src/Wanxiangshu/**`；唯一合法静态枚举 = `src/Wanxiangshu/Tools/StaticTools.fs`（权限矩阵 schema 层）。`requirements/repository-programming/tests/js-surface-gate.test.mjs` 原为门禁的单元 oracle，已随本包 MOVE 为 `js-surface-gate.test.mjs`。G3 debt 考古 token（js-student/js-teacher 等 `FORBIDDEN_TOKENS`）已随 CLN-Z 退役。

## 主流程（唯一实现序）

```text
resolve Attempt → immutable profile（AttemptExecutionProfile.ToolCapabilitySet）
→ JsToolGenerator.generate → js-* surface（name/schema/description/base class/examples/bindings）
→ ToolRegistry gate（invoked name ∈ 当前 surface → 否则 fail closed）
→ JsSandbox：注入 runtime bindings（JsToolsBindings.createApi）+ wrapProgram
→ 执行 class Js { async run() { ... } }
→ 收集 return + ReadSet + WriteSet
→ parse JSON 值树 → JS-010 校验（非法 → INVALID_RETURN_VALUE，零提交）
→ preflight：路径/UTF-8/同路径单意图/capability 边界/快照验证（FILE_CHANGED → fail closed，不隐式 retry）
→ WriteSet 非空：JsToolsTransactionStore.appendPrepared（EventStore durable）→ apply mutations（canonical 排序）→ appendCommitted → 暴露成功结果
→ WriteSet 空（纯查询）：无 commit，暴露已校验 return
→ JsToolsResult.render：Synthetic TOML 两份文档（#ok/#failed + [data]/[fs]）
```

## description 的「交过学费」工具选择层

`resources/provider/tool/js-program/` 不只解释 syntax；它负责在模型做选择的那一刻把高层 primitive 的正确性边界说透（REPOSITORY-PROGRAMMING-022）。文案按「显著性中断 → 权威定位 → 鲜活损失 → 数字锚定 → 因果 → 强二分 → If/Then 行动 → stop rule → 近因重复」组织：先阻断自动驾驶，再讲理，再把下一次动作钉住。这里的“心理手段”只用于**提高正确合同被想起和执行的概率**，不得替代证据：Host 的权威来自真实 ownership；数字来自真实事故或当前 program 可计算的不变量；二分只在 policy 已经定义穷尽选择时使用；模糊措辞只能用于制造危险感，不能模糊技术事实。

- `header/{en,zh-CN}.md`：description 第一屏先给风险中断，不让模型把后面的规则当普通参考资料扫过去；紧跟可识别的危险信号（手算 offset、用 grep 猜结构、准备第二轮修第一轮）。
- `rules-read/{en,zh-CN}.md`：用一次真实感强的失败链说明「结构化重排 → ordered anchors + `text()`」，并明确 grep 只能找候选、不能替代结构切片；手写 `indexOf`/`substring` 不是默认定位策略。
- `rules-mutation/{en,zh-CN}.md`：说明「先构造最终文本、每 path 一次 mutation」；若结果规模/结构明显异常，program 应在 return 前 throw，让 staging 丢弃，而不是 commit 后再写第二轮清残骸。
- `footer/{en,zh-CN}.md`：把经验泛化成总原则——生成 API 已拥有某层边界时，自己重写一份低层版本不是更聪明，而是主动拆掉护栏；只有高层 primitive 确实表达不了任务时才下降一层。

行为塑形的固定手法：

- **真实权威**：反复强调「Host owns the boundary / transaction」，让模型知道这不是个人偏好，而是执行语义所有权。
- **数字锚定 + 损失厌恶**：保留 `≈8k → ≈31k` 与「第二、第三轮只为修第一轮」这种代价，不写抽象的“可能有风险”。
- **强二分**：高层 primitive 已拥有边界时，只有「使用」或「证明表达不了后下降一层」两个合格选择；熟悉、方便、想炫技都不构成第三条路。
- **承诺一致性**：program 在开始 mutation 前先把“我要保护哪些不变量”写进代码，return 前必须兑现；异常即 throw。
- **反自我辩护**：专门点破「开头看起来正常」「再 replace 一次就好」「我自己实现更灵活」这些最常见的自我安慰。
- **首因 + 近因**：header 第一屏惊醒；footer 最后一屏再次压缩成一句可复述的铁律。

文案必须保持惊醒 → 权威 → 事故 → 损失 → 根因 → 二选一 → 下一次动作 → 停止规则 → 再提醒；至少有一条「如果你正准备 X → 立刻 Y」implementation intention 和一条异常 stop rule。禁止退化回「Best practice: prefer anchors」式无痛摘要；精确事故数字与比喻可换，但要保留能让模型记住代价的具体性。

## 依赖（DEPENDS ON，逐条理由）

| 依赖 | 理由 |
|---|---|
| `office-capability` | `ToolCapabilitySet` 由 office consequence 建立（ARCH-017）；本包只消费该集合，不裁决权限（REPOSITORY-PROGRAMMING-001）。 |
| `capability-enforcement` | capability → schema → runtime gate 同构/同源律由它拥有；本包应用该律到编程面（REPOSITORY-PROGRAMMING-002/021）。 |
| `effect-accounting` | transaction 的效果分型（Prepared/Committed/Unknown）跨 prompt/repository 共用 law oracle；durable prepare 语义消费它。 |
| `durable-events` | 唯一 EventStore substrate：`JsTransactionPrepared`/`Committed` facts + owned payloads；本包不建 feature store（REPOSITORY-PROGRAMMING-012/015）。 |
| `participant-horizon` | provider 可见 surface 不泄漏 Host 内部（公开基类无 `_api`；sandbox 不暴露 host internals；错误不回显 sandbox 内部）——信息准入边界。 |

## 历史与弃权

### 被拒方案（详见历史 change（js-capability-projected-tools、js-tools-toml-result）、历史 why/js-tools 条款）

- 五套独立 js-* RPC；万能基类 + prose warning；手写 role→JS 矩阵；alias/clean-break 替换 builtin；模型 JS 拿 ambient OS authority；事务先写盘再执行；结果 commit 后才发现不可用；walk-then-filter glob；`**`→`.*`；grep 仅靠 `glob()+file()+RegExp`；JSON stringify 进 TOML 字符串；`status` discriminator；程序对象扁平到文档根 + 保留字；逗号拼接路径；失败带半截 `[data]`；统一 `kind`/`origin`/`ok` 信封；js-tools 私有 TOML 方言（第二套 `"""`、null 哨兵）；从结果 TOML 反向解析控制流。全部按「为什么被拒」记录在 `WHY.md` §历史拒绝方案与各 change 文件。

### 判定为 HOW（非 normative；不入 WHAT）

- builtin `read`/`edit`/`write`/`glob`/`grep`/`patch` 与 `js-*` 的**共存**是当前产品形态（JS-003/017），「builtin 是否长期 coexist」本包不拥有（`17-repository.md` DOES NOT OWN）。
- `js-*` 具体工具名（`js-coder` 等）、base class 的 JS 语法形态、`JsProgram` 类名 → 当前实现词汇。
- bound 常数：glob maxEntries、grep maxMatches、sandbox deadline/memory/output 数值、`new Function` 细节 → HOW。
- Synthetic TOML 的引号/换行/delimiter/裸字段排序/值树编码 → `provider-projection`（ARCH-010）。
- `MaxKeywords=8`/`TopKPerKeyword=4` 等 warm-start 常数 → `repository-investigation`（AGENT-032 HOW，HANDOFF §12）。
- 10s join 预算等其它常数 → 各自 owner 的 HOW。

### 判定为 GARBAGE（migration/clean-break 沉积，不进入永久 WHAT）

- `js-student`/`js-teacher`/`StudentLearnJs`/`StudentCompileJs`/`StudentTeacherJs`：G3 rebase debt，已删领域（`PROMPT-012` absence）。`FORBIDDEN_TOKENS` absence ratchet 已随 CLN-Z 退役（阶段 3：设计本身使旧世界不可表达）。
- 旧结果面 golden（`status = "ok"` / `result = "{...}"` / 逗号拼接 `written`）：`js-tools-toml-result.md` 已 clean-break，旧字符串结果不迁移。

### 不归本包（COVERAGE 交叉确认）

- capability 同构/同源律 → `capability-enforcement`（`capability-isomorphism-gate.mjs`、`agent-permission-gate`）。
- provider-projection 部分（`js-tools-toml-result.md` 的值树进 SyntheticToml）→ `provider-projection`。
- Git shared-ref integration（`PublishClaimed` 三分支 CAS）→ `change-integration`。

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（物理移入本包）/ `NEW`（本包新写）/ `REUSE`（留在原处，记录锚点与 cutover 计划）。
> 单跑：`WANXIANGSHU_PROVIDER_LANGUAGE=en node --test requirements/repository-programming/tests/<file>`（与 `requirements/verification-system/tests/run.mjs` 一致设 en；shell 若导出其它语言值会改变本地化文案断言）。
> 全套：`node requirements/verification-system/tests/run.mjs`；L0 门：`node scripts/check.mjs`。

### 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| `REPOSITORY-PROGRAMMING-001` | `js-surface.test.mjs` → `JS001_generate_none_when_no_filesystem_capability` / `JS001_role_projection_is_exactly_roles_permissions_intersection` / `JS001_non_fs_permissions_never_produce_members` | MOVE | `node --test requirements/repository-programming/tests/js-surface.test.mjs` |
| `REPOSITORY-PROGRAMMING-002` | `js-surface.test.mjs` → `JS004_capability_exactness_plus_one_ultra_example_coder` / `JS004_absent_capability_is_absent_in_all_four_layers` / `JS004_member_gate_binds_present_members_only` / `JS004_lying_generator_counterexample_is_rejected` | MOVE | 同上 |
| `REPOSITORY-PROGRAMMING-003` | `js-surface.test.mjs` → `JS002_generation_is_deterministic_and_names_js_role` / `JS002_same_capabilities_share_mechanics_but_role_shapes_the_ultra_example` / `JS004_fast_deep_profiles_generate_identical_surfaces` / `JS010_each_filesystem_role_gets_exactly_one_distinct_ultra_example` | MOVE | 同上 |
| `REPOSITORY-PROGRAMMING-004` | `js-surface.test.mjs` → `JS001_generated_name_gate_rejects_forged_names` | MOVE | 同上 |
| `REPOSITORY-PROGRAMMING-005` | `js-surface.test.mjs` → `JS002_description_embeds_spec_base_class_rules_and_one_ultra_example`（`_api` absent）/ `JS010_description_never_dilutes_the_ultra_example` / `JS_description_retains_no_unsubstituted_placeholders`；`js-tool-host.test.mjs` → `JS003_builtin_fallback_descriptions_are_left_untouched` / `JS003_hook_must_not_recommend_invisible_tools` / `JS073_spec_carries_generated_name_and_honest_description` | MOVE | `node --test requirements/repository-programming/tests/js-tool-host.test.mjs` |
| `REPOSITORY-PROGRAMMING-006` | `js-sandbox.test.mjs` → 全部 8 个 test（`JS011_api_is_the_only_authority_in_the_context` / `JS054_1_sync_infinite_loop_is_killed_by_vm_timeout` / `JS054_1_async_deadline_proxy_aborts_api_calls_after_deadline` / `JS054_2_output_bound_rejects_oversized_results` 等）；交叉 `js-bindings.test.mjs` → `JS011_sandbox_program_uses_bindings_end_to_end` | MOVE | `node --test requirements/repository-programming/tests/js-sandbox.test.mjs` |
| `REPOSITORY-PROGRAMMING-007` | `js-tools-fs.test.mjs` → `JS005_readUtf8_reads_and_classifies` / `JS006_findAnchor_ordered_string_and_regex` / `JS006_requireUnique_refuses_ambiguous_anchors`；`js-anchors.test.mjs` → `JS006_empty_anchor_declaration_is_refused` / `JS006_non_positive_occurrence_is_refused`；`js-workflow.test.mjs` → `JS005_offset_anchor_clips_to_closed_file_range` / `JS005_offset_N_is_string_index_not_line_number` / `JS006_missing_anchor_reason_names_declaration_path_and_pattern`；`js-bindings.test.mjs` → `JS005_bindings_file_reads_utf8`；`file-tools.test.mjs` → `FILETOOLS_read_returns_content_for_existing_file` / `FILETOOLS_read_reports_missing_file` / `FILETOOLS_read_accepts_a_bare_string_payload` / `FILETOOLS_read_falls_back_to_raw_payload_when_not_json` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-anchors.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs requirements/repository-programming/tests/js-bindings.test.mjs requirements/repository-programming/tests/file-tools.test.mjs` |
| `REPOSITORY-PROGRAMMING-008` | `js-tools-fs.test.mjs` → `JS007_glob_deterministic_enumeration` / `JS007_glob_gitignore_skips_git_and_ignored`；交叉 `js-bindings.test.mjs` → `JS007_bindings_path_boundary_denies_escape` / `JS007_bindings_glob_lists_matching_paths` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-bindings.test.mjs` |
| `REPOSITORY-PROGRAMMING-009` | `js-tools-fs.test.mjs` → `JS020_grep_returns_line_column_and_skips_ignored`；交叉 `js-bindings.test.mjs` → `JS010_bindings_grep_returns_matches` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-bindings.test.mjs` |
| `REPOSITORY-PROGRAMMING-010` | `js-transaction.test.mjs` → `JS008_009_rewrite_requires_existing_target_create_requires_missing` / `JS026_same_path_once_rejects_duplicate_mutation_targets`；交叉 `js-bindings.test.mjs` → `JS008_012_bindings_rewrite_requires_existing_target` / `JS009_012_bindings_write_stages_create`；`file-tools.test.mjs` → `FILETOOLS_write_creates_file_and_reports_size` / `FILETOOLS_write_refuses_unparseable_payload` / `FILETOOLS_edit_replaces_exact_match` / `FILETOOLS_edit_reports_missing_file` / `FILETOOLS_edit_reports_absent_old_string` / `FILETOOLS_edit_refuses_unparseable_payload` | MOVE | `node --test requirements/repository-programming/tests/js-transaction.test.mjs requirements/repository-programming/tests/js-bindings.test.mjs requirements/repository-programming/tests/file-tools.test.mjs` |
| `REPOSITORY-PROGRAMMING-011` | `js-workflow.test.mjs` → `JS010_array_null_is_invalid_return_value` / `JS010_mixed_object_array_is_invalid`；`js-sandbox.test.mjs` → `JS010_circular_return_is_invalid_return_value` | MOVE | `node --test requirements/repository-programming/tests/js-workflow.test.mjs requirements/repository-programming/tests/js-sandbox.test.mjs` |
| `REPOSITORY-PROGRAMMING-012` | `js-tools-transaction-store.test.mjs` → `JS012_prepare_then_commit_updates_only_integrator_Current`；交叉 `js-bindings.test.mjs` → `JS008_012_bindings_rewrite_stages_without_touching_disk` / `JS009_012_bindings_write_leaves_disk_untouched`（staging 不碰盘）；`js-workflow.test.mjs` → `JS012_workflow_with_store_persists_prepare_and_commit` | MOVE | `node --test requirements/repository-programming/tests/js-tools-transaction-store.test.mjs requirements/repository-programming/tests/js-bindings.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-013` | `js-tools-fs.test.mjs` → `JS013_commitPlan_all_or_nothing` / `JS013_commitPlan_aborts_before_write_when_snapshot_fails` / `JS013_commitPlan_rolls_back_written_files_on_write_failure`；`js-transaction.test.mjs` → `JS013_preflight_orders_rules_and_short_circuits` / `JS013_commit_plan_is_exact`；`js-workflow.test.mjs` → `JS085_workflow_reads_and_commits_rewrite` / `JS085_workflow_commits_create_and_reports` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-transaction.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-014` | `js-transaction.test.mjs` → `JS014_stale_rewrite_is_a_conflict_with_no_retry`；`js-workflow.test.mjs` → `JS085_workflow_preflight_blocks_stale_rewrite_without_touching_disk`（preflight 不碰盘；标签归 019，此处为交叉证据） | MOVE | `node --test requirements/repository-programming/tests/js-transaction.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-015` | `js-tools-fs.test.mjs` → `JS015_rollbackPlan_restores_originals_and_removes_creates`；`js-tools-transaction-store.test.mjs` → `JS015_prepared_without_committed_is_interrupted_tool_evidence` / `JS015_reopening_store_never_undoes_an_interrupted_tool` / `JS015_store_source_has_no_manual_history_reader`；`js-transaction.test.mjs` → `JS015_rollback_plan_is_exact` | MOVE | `node --test requirements/repository-programming/tests/js-tools-fs.test.mjs requirements/repository-programming/tests/js-tools-transaction-store.test.mjs requirements/repository-programming/tests/js-transaction.test.mjs` |
| `REPOSITORY-PROGRAMMING-016` | `js-workflow.test.mjs` → `JS016_result_renders_stable_toml_shapes` / `JS010_016_query_object_has_data_and_no_fs` / `JS010_016_primitive_return_uses_data_field`；`js-tool-host.test.mjs` → `JS073_spec_executes_program_and_renders_result` | MOVE | `node --test requirements/repository-programming/tests/js-workflow.test.mjs requirements/repository-programming/tests/js-tool-host.test.mjs` |
| `REPOSITORY-PROGRAMMING-017` | `js-parallel-contract.test.mjs`（NEW）→ `JS018_generated_surface_teaches_parallel_safety_for_edits_and_reads` / `JS018_consecutive_transactions_re_snapshot_committed_state_no_lost_update` / `JS018_interleaved_reads_are_immutable_snapshots_not_mutation_aliases`；交叉 REUSE `tests/integration/plugin/`（Host 串行执行面，SPLIT@cutover 下表） | NEW + REUSE | `node --test requirements/repository-programming/tests/js-parallel-contract.test.mjs` |
| `REPOSITORY-PROGRAMMING-018` | `js-anchors.test.mjs` → `JS019_failure_codes_are_stable_and_unique`；`js-sandbox.test.mjs` → `JS019_invalid_javascript_is_invalid_program` / `JS019_program_throw_is_program_failed`；`js-workflow.test.mjs` → `JS019_missing_anchor_uses_stable_code` / `JS085_workflow_file_missing_fails_the_program` | MOVE | `node --test requirements/repository-programming/tests/js-anchors.test.mjs requirements/repository-programming/tests/js-sandbox.test.mjs requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-019` | `js-workflow.test.mjs` → `JS019_invalid_return_value_commits_nothing`（非法 return 零提交）/ `JS085_workflow_program_error_fails_without_commit` / `JS085_workflow_preflight_blocks_stale_rewrite_without_touching_disk`（commit 失败不给成功结果） | MOVE | `node --test requirements/repository-programming/tests/js-workflow.test.mjs` |
| `REPOSITORY-PROGRAMMING-020` | `file-mutation-tools.test.mjs` → 全部 11 个 test（`FILEMUT_mv_moves_a_file` / `FILEMUT_mv_renames_a_directory_with_contents` / `FILEMUT_rm_removes_a_file` / `FILEMUT_rm_refuses_a_non_empty_directory` / `FILEMUT_mv_rename_failure_surfaces_os_message` 等）；交叉 REUSE `requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs`（plugin 级 `AGENT_017_mv_*` / `AGENT_018_rm_*` + 角色门禁 `AGENT_016_*`） | MOVE + REUSE | `node --test requirements/repository-programming/tests/file-mutation-tools.test.mjs` |
| `REPOSITORY-PROGRAMMING-021` | `js-surface-gate.test.mjs` → `JS_SURFACE_GATE_handwritten_tokens_use_inquiry_not_meditator` / `JS_SURFACE_GATE_rejects_handwritten_js_coder_outside_permission_matrix` / `JS_SURFACE_GATE_allows_permission_matrix_enumeration`；门禁本体 REUSE `scripts/checks/js-surface-gate.mjs`（`node scripts/check.mjs` 内运行） | MOVE + REUSE | `node --test requirements/repository-programming/tests/js-surface-gate.test.mjs`；`node scripts/checks/js-surface-gate.mjs` |
| `REPOSITORY-PROGRAMMING-022` | `js-surface.test.mjs` → `JS_description_teaches_tool_choice_through_paid_failure_memory`（惊醒→真实权威→数字损失→根因→强二分→动作→stop rule；anchor/grep/mutation/invariant guard） | NEW | `node --test requirements/repository-programming/tests/js-surface.test.mjs` |

### 统计

```text
WHAT 命题：22（REPOSITORY-PROGRAMMING-001..022）
落点：   MOVE 20 个命题（19 个纯 MOVE + 017/020/021 带 REUSE 交叉）
        NEW  2（js-parallel-contract.test.mjs ×3 test，覆盖 017；js-surface.test.mjs 新增 022）
        REUSE 3（scripts/checks/js-surface-gate.mjs、requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs、integration Host 串行面）
GAP：    0
```

### 移动文件清单（源 → 目标，均单独跑绿）

| 源 | 目标 | 断言数 | 单跑结果 |
|---|---|---|---|
| `requirements/repository-programming/tests/js-surface.test.mjs` | `requirements/repository-programming/tests/js-surface.test.mjs` | 15 pass | `node --test` 绿 |
| `requirements/repository-programming/tests/js-bindings.test.mjs` | `requirements/repository-programming/tests/js-bindings.test.mjs` | 9 pass | 绿 |
| `requirements/repository-programming/tests/js-sandbox.test.mjs` | `requirements/repository-programming/tests/js-sandbox.test.mjs` | 8 pass | 绿 |
| `requirements/repository-programming/tests/js-anchors.test.mjs` | `requirements/repository-programming/tests/js-anchors.test.mjs` | 3 pass | 绿 |
| `requirements/repository-programming/tests/js-tools-fs.test.mjs` | `requirements/repository-programming/tests/js-tools-fs.test.mjs` | 10 pass | 绿 |
| `requirements/repository-programming/tests/js-transaction.test.mjs` | `requirements/repository-programming/tests/js-transaction.test.mjs` | 6 pass | 绿 |
| `requirements/repository-programming/tests/js-tools-transaction-store.test.mjs` | `requirements/repository-programming/tests/js-tools-transaction-store.test.mjs` | 4 pass | 绿 |
| `requirements/repository-programming/tests/js-workflow.test.mjs` | `requirements/repository-programming/tests/js-workflow.test.mjs` | 16 pass | 绿 |
| `requirements/repository-programming/tests/js-tool-host.test.mjs` | `requirements/repository-programming/tests/js-tool-host.test.mjs` | 4 pass | 绿 |
| `requirements/repository-programming/tests/file-mutation-tools.test.mjs` | `requirements/repository-programming/tests/file-mutation-tools.test.mjs` | 11 pass | 绿 |
| `requirements/repository-programming/tests/file-tools.test.mjs` | `requirements/repository-programming/tests/file-tools.test.mjs` | 11 pass | 绿 |
| `requirements/repository-programming/tests/js-surface-gate.test.mjs` | `requirements/repository-programming/tests/js-surface-gate.test.mjs` | 3 pass | 绿 |

适配说明：4 个文件（`js-surface`/`js-bindings`/`js-tool-host`/`js-workflow`）原直接 `import { ofArray } from '../../../dist/fable_modules/.../Set.js'`——该直接 import 是 test-boundary 门（新增 requirements scope）禁止的遗留项；迁移时改写为经 sanctioned 适配层 `requirements/verification-system/tests/support/domain.mjs` 的 `FsSet.ofArray`（同一 comparer 语义），消除 4 条 baseline 遗留，门仍绿。`../support/domain.mjs` 深度修正为 `../../../requirements/verification-system/tests/support/domain.mjs`。

### semantic anchor 归属（semantic-anchors.mjs）

本包在 `scripts/checks/semantic-anchors.mjs` 中 **拥有 0 个 anchor id**。`ROLE_SEMANTIC_ANCHORS`/`TOOL_DESCRIPTION_ANCHORS` 中 inspector/bookkeeper 锚点归 `repository-investigation`/`knowledge-reuse`；js-* 编程面不通过 prompt 锚点证明——它由生成 surface oracle（`js-surface.test.mjs` 四层 exactness）+ 静态门禁（`js-surface-gate.mjs`）证明。

### SPLIT@cutover 计划（现有测试的 owner 拆分）

| 现有文件 | 当前 owner 混合 | cutover 动作 |
|---|---|---|
| `requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs` | `repository-programming`（mv/rm POSIX 语义断言：`AGENT_017_*`/`AGENT_018_*`）+ `office-capability`/`capability-enforcement`（角色门禁：`AGENT_016_mv_and_rm_are_denied_for_non_coder_roles`、`AGENT_016_mv_and_rm_are_denied_when_the_role_is_unresolved`） | **SPLIT**：POSIX 语义断言并入本包（integration 层）；角色门禁断言归 `office-capability`（consequence）/`capability-enforcement`（gate） |
| `tests/integration/plugin/`（Host 工具调用串行执行面） | Host 串行执行 = `host-boundary`（物理执行面）+ 本包（编程面合同） | **SPLIT**：模型侧合同断言并入本包；Host 物理串行语义归 `host-boundary` |
| `scripts/checks/js-surface-gate.mjs` | MECHANISM（共享 checker）；语义唯一归本包 | 门禁机制留在 `scripts/checks/`；其断言 owner 记为本包；cutover 后可移入本包 tests 或保留共享（机制可共享、断言不双 owner） |
