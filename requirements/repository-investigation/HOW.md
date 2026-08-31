# repository-investigation — HOW

## 架构机制与核心模型

### 1. 证据漏斗与取证边界

1. **Evidence Funnel 机制**：
   - Inspector 角色遵循证据漏斗模型：`fact → cheapest adequate observation → evidence → consequence`；
   - 只读约束在工具层面强制生效：Inspector 仅配备静态读取工具（如 `read`、`glob`、`grep` 及只读 `query-shell`），不具备文件修改或破坏性执行权限；
   - `query-shell` 严格执行命令负清单（禁止 `build`、`test`、`lint`、`typecheck`、应用启动与迁移等），仅允许 `git status`、`git diff`、`stat` 等静态元数据查询。

2. **定位与溯源编码**：
   - 提取的证据必须记录规范化定位符：文件路径、精确行区间与内容指纹，确保后续重放与复核具备确定性基准。

### 2. Warm-Start 并行管线与 Fail-Open 语义

1. **关键词归一化与并行检索**：
   - `normalizeKeywords` 接收显式关键词文本，按 LF 分行、trim、去重并截断至上限（默认 8 条）；
   - 针对各关键词通过 `Parallel.mapBounded` 并行调用 Semble stdio MCP 检索服务，完成后按原始关键词序与局部得分恢复确定性排序。

2. **提示词安全合成与边界保护**：
   - 检索命中条目经 `stableDedupeHints`（按路径、起止行与正文）稳定去重；
   - 渲染阶段执行双重硬界限制（最大提示条目数与字节上限），超限时按整条 hint 剔除，保证数据结构完整；
   - 检索过程中的任何单项失败、超时或服务未就绪均安全 fail-open，返回原始任务描述，不阻断主线流程。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REPOSITORY-INVESTIGATION-001 | `requirements/repository-investigation/tests/repository-warm-start.test.mjs::WHAT[REPOSITORY-INVESTIGATION-001] AGENT_032_renderer_keeps_charge_authoritative_and_hints_do_not_replace_evidence` |
| REPOSITORY-INVESTIGATION-002 | `requirements/repository-investigation/tests/semble-mcp.test.mjs::WHAT[REPOSITORY-INVESTIGATION-002] AGENT_027_parse_text_and_tool_result`；`requirements/repository-investigation/tests/semble-mcp.test.mjs::WHAT[REPOSITORY-INVESTIGATION-002] AGENT_027_search_fixture_stdio_roundtrip` |
| REPOSITORY-INVESTIGATION-003 | `requirements/repository-investigation/tests/investigation-resource-laws.test.mjs::WHAT[REPOSITORY-INVESTIGATION-003] INVESTIGATE_inspector_role_law_layers_reasoning_below_evidence_acquisition` |
| REPOSITORY-INVESTIGATION-004 | `requirements/repository-investigation/tests/investigation-resource-laws.test.mjs::WHAT[REPOSITORY-INVESTIGATION-004] INVESTIGATE_inspector_role_law_has_evidence_funnel_and_stop_rule` |
| REPOSITORY-INVESTIGATION-005 | `requirements/repository-investigation/tests/investigation-resource-laws.test.mjs::WHAT[REPOSITORY-INVESTIGATION-005] INVESTIGATE_inspect_law_pins_causal_readonly_witness_not_editor`；`requirements/repository-investigation/tests/investigation-resource-laws.test.mjs::WHAT[REPOSITORY-INVESTIGATION-005] INVESTIGATE_query_shell_law_is_observation_not_execution_and_inspector_only` |
| REPOSITORY-INVESTIGATION-006 | `requirements/repository-investigation/tests/repository-warm-start.test.mjs::WHAT[REPOSITORY-INVESTIGATION-006] AGENT_032_renderer_keeps_hostile_hint_bytes_as_toml_data_and_dedupes_stably` |
| REPOSITORY-INVESTIGATION-007 | `requirements/repository-investigation/tests/repository-warm-start.test.mjs::WHAT[REPOSITORY-INVESTIGATION-007] AGENT_032_keywords_normalize_stable_exact_dedupe_and_cap_at_eight`；`requirements/repository-investigation/tests/repository-warm-start.test.mjs::WHAT[REPOSITORY-INVESTIGATION-007] AGENT_032_zero_keywords_is_byte_exact_zero_work` |
| REPOSITORY-INVESTIGATION-008 | `requirements/repository-investigation/tests/repository-warm-start.test.mjs::WHAT[REPOSITORY-INVESTIGATION-008] AGENT_032_nonconsumer_nonempty_keywords_fail_and_missing_workspace_skips` |
| REPOSITORY-INVESTIGATION-009 | `requirements/repository-investigation/tests/repository-warm-start.test.mjs::WHAT[REPOSITORY-INVESTIGATION-009] AGENT_032_renderer_enforces_24_hint_and_64KiB_bounds_by_whole_entries`；`requirements/repository-investigation/tests/repository-warm-start.test.mjs::WHAT[REPOSITORY-INVESTIGATION-009] AGENT_032_searches_all_independent_keywords_in_one_parallel_wave_and_restores_ordinal_order` |
