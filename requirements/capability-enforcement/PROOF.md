# capability-enforcement — 测试落点表

每条 WHAT 命题恰好一行落点。类型：`MOVE` = 本包 tests/ 下文件（物理移入）；`REUSE` = 留原处，记
精确断言锚点与 cutover 拆分计划。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|----------|
| ENF-001 | `requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` `PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority` + `tests/agent-permission-gate.test.mjs` `roles.permissions_agree_with_the_host_schema_matrix` | REUSE（context family SPLIT：prefix-stability/obligation 等）+ MOVE | 分别 `node --test` |
| ENF-002 | `tests/agent-permission-gate.test.mjs` `AGENT_006_role_tool_matrix_reaches_the_host_schema` + `roles.permissions_agree_with_the_host_schema_matrix` | MOVE | `node --test requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-003 | `requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` `PROMPT_008_the_request_kind_is_carried_not_inferred` + `tests/agent-permission-gate.test.mjs`（ROLE_ALLOW 一致性） | REUSE + MOVE | 分别 `node --test` |
| ENF-004 | `requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` `AGENT_010_the_tier_does_not_reach_the_system_prompt_or_the_tool_set` + `tests/agent-permission-gate.test.mjs` `AGENT_002_gate_accepts_distinct_models_and_writes_owned_fields` | REUSE + MOVE | 分别 `node --test` |
| ENF-005 | `requirements/speculative-investigation/tests/runtime.test.mjs` `STRENGTH_004_replica_host_tool_map_denies_everything_then_allows_exact_readonly` + `requirements/speculative-investigation/tests/host-canary-k0.test.mjs` `STRENGTH_004_005_policy_execution_gate_denies_write_edit_executor_fork_join_network` | REUSE（SPLIT@cutover：strength family KEEP `speculative-investigation`；replica 收窄断言 enforcement 侧归本包） | `node --test requirements/speculative-investigation/tests/runtime.test.mjs` |
| ENF-006 | `tests/agent-permission-gate.test.mjs` `ROLE_ALLOW`（Distiller: []、Blogger: [chronicle]） | MOVE | `node --test requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-007 | `requirements/capability-enforcement/tests/stealth-browser-mcp-wildcard.test.mjs` `AGENT_026_browser_only_wildcard_permission` + `requirements/capability-enforcement/tests/sphinx-mcp-wildcard.test.mjs` `AGENT_030_inquiry_only_wildcard_permission` | REUSE（SPLIT：文件含 host-boundary 注入断言；wildcard 断言归本包） | 分别 `node --test` |
| ENF-008 | `tests/capability-isomorphism-gate.test.mjs` 全部（含 `capability_iso_repo_scan_is_green`）+ `scripts/checks/capability-isomorphism-gate.mjs` | MOVE + KEEP(gate) | `node --test requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs` |
| ENF-009 | `requirements/capability-enforcement/tests/tool-referential-integrity.test.mjs` `gate_a_*` + `scripts/checks/tool-referential-integrity.mjs` | REUSE（SPLIT@cutover：Gate A = action-affordance 语义合同 + capability-enforcement 名称/结构） | `node --test requirements/capability-enforcement/tests/tool-referential-integrity.test.mjs` |
| ENF-010 | `tests/agent-permission-gate.test.mjs` `AGENT_007_bash_stays_denied_even_when_the_gate_fails` + `AGENT_007_validation_error_is_still_reported` + `tests/managed-agent-config.test.mjs` `MACFG_configureManager_validation_failure_is_process_fatal_after_deny_fields_land` + `requirements/capability-enforcement/tests/inquiry-permissions.test.mjs` `Inquiry_rolePredicate_inspector_allow_and_host_native_read_gap` + `requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs` `AGENT_007_unresolved_role_denies_all_tools` | MOVE + REUSE（×2） | 分别 `node --test` |
| ENF-011 | `tests/agent-permission-gate.test.mjs` `AGENT_019_external_directory_overrides_host_default_ask` + `tests/managed-agent-config.test.mjs` `MACFG_applyOwnedFields_writes_owned_keys_and_never_touches_model` | MOVE | `node --test requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-012 | `tests/capability-isomorphism-gate.test.mjs` `capability_iso_tool_registry_requires_generator` + `scripts/checks/js-surface-gate.mjs` | MOVE + REUSE（KEEP repository-programming 应用） | 分别 `node --test` / `node scripts/checks/js-surface-gate.mjs` |

## 移动文件

| 源 | 目标 | 结果 |
|----|------|------|
| `requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs` | `requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs` | 7 pass / 0 fail |
| `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` | `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` | 16 pass / 0 fail |
| `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` | 9 pass / 0 fail |

## 计数

WHAT 命题 12；落点 12（MOVE 9 行 × REUSE 9 行，含组合行）；GAP 0。

## 跨包注记

- `managed-agent-config.test.mjs` 的 `MACFG_validate_rejects_duplicate_pair_model`（peer pair model
  互异）语义 owner 是 `participant-identity`（PID-007）；文件因主导 owner 是 enforcement 而物理移入
  本包，SPLIT@cutover 记录于双方 PROOF.md。
- `agent-permission-gate.test.mjs` 的 `ROLE_ALLOW.Reviewer` / `ROLE_ALLOW.Orchestrator` 被
  `office-capability`（OFF-011/012）交叉引用为矩阵投影证据；矩阵断言 owner 是本包。

## semantic anchor id 清单（`scripts/checks/semantic-anchors.mjs`）

本包 **不拥有** 任何 ROLE_SEMANTIC_ANCHORS / OFFICE_CAPABILITY_ANCHORS id：语义锚点是 cognition /
office 内容（`cognitive-environment` / `office-capability` / `action-affordance` /
`epistemic-reasoning` 等逐 id 声明）。capability-enforcement 的可执行 proof 是静态 gate 与 dist 层
权限测试（`capability-isomorphism-gate.mjs`、`agent-permission-gate` 等），不经语义锚点。
