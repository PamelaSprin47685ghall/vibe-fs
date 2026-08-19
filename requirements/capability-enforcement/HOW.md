# capability-enforcement — 实现模型与约束（非 normative）

## 实现模型

| 层 | 实现 | 说明 |
|----|------|------|
| 域能力 token | `src/Wanxiangshu/Kernel/Roles.fs` `ToolPermission` + `Roles.permissions role` | 唯一 Role→能力集；Kernel 层 Vocabulary |
| 唯一 profile 构建 | `src/Wanxiangshu/Domain/PromptAuthority.fs` `toolCapabilitiesFor` + `buildAttemptExecutionProfile`；调用点 `Domain/AttemptPlanner.fs` `plan` | `AttemptExecutionProfile.ToolCapabilitySet` 是每次请求能力权威；architecture gate 拒绝模块外 record expression |
| js 投影 | `src/Wanxiangshu/Domain/JsCapability.fs`（`ofToolPermission` 唯一映射、`JsFragmentRegistry`）+ `Infrastructure/OpenCode/Tools/ToolRegistry.fs` `JsToolGenerator.generate` | 四层同构（ENF-008）；无手写 `js-*` spec |
| schema/config gate | `src/Wanxiangshu/Tools/StaticTools.fs` `permissionObj` → `OpenCode/Host/ManagedAgentConfig.fs` `applyOwnedFields` → `OpenCode/Host/ManagerConfig.fs` `configureManager` | Host-final permission；config Error 先保 deny owned fields，再 `Diagnostic.fatal`；`external_directory` 唯一写点（ENF-010/011）。managed model 不属于本 gate，改由 `execution-model-routing` 的 MJS scheduler/lease owner。 |
| gate 层 | `src/Wanxiangshu/Infrastructure/OpenCode/Tools/ToolRegistry.fs` `rolePredicate` + `gateExecute` | 逐工具 Role 谓词 + execute 前拒绝；unresolved role → `DeniedUnestablished`（ENF-010） |
| MCP wildcard | `Kernel/StealthBrowserMcp.fs`（Network→`stealth-browser-mcp_*`）、`Kernel/SphinxMcp`（Sphinx→`sphinx_*`） | 域能力 token 留在 `Roles.permissions`，wildcard 只是 schema 键（ENF-007） |
| 静态 gate | `scripts/checks/capability-isomorphism-gate.mjs`（KEEP 本包）、`tool-referential-integrity.mjs`（Gate A）、`js-surface-gate.mjs`（KEEP repository-programming） | 分别防四层漂移 / 同名异义 / 手写 js-* 名 |

## 边界与弃权

### 不归本包（引用其它包）

- office 的 entitled consequence（offices 有什么资格）→ `office-capability`（DEPENDS ON）。
- Role/Persona/Binding 身份轴 → `participant-identity`（DEPENDS ON）。
- action 描述的五问合同（act/时机/负边界/成功后果/参数）→ `action-affordance`。
- internal participant 不进 choice surface 的可见性 → `participant-horizon`。
- 编程面 SDK 语义（sandbox/transaction/anchor/failure algebra）→ `repository-programming`。
- MCP 启动/注入机制 → `host-boundary`。

### GARBAGE / HOW 裁决（不进入 WHAT）

| 内容 | 裁决 | 理由 |
|------|------|------|
| AGENT-006 精确工具名清单（表内容） | HOW（当前矩阵） | 「矩阵是 enforcement 投影」是 WHAT；每个名字本身是当前实现 vocabulary，随能力演进可重画 |
| MCP wildcard 字符串（`stealth-browser-mcp_*` / `sphinx_*`） | HOW | Host schema 键；「域能力 token 唯一」才是 WHAT（ENF-007） |
| `attempt-plan.test.mjs` 中 prefix/probe 断言 | HOW → `prefix-stability` | 该文件是 context family SPLIT；本包只引用 PROMPT-008/AGENT-010 能力断言 |
| AGENT-002 缺一则投影补齐 / AGENT-004 旧名拒绝（`agent-permission-gate` / `managed-agent-config` 中的断言） | HOW（runtime contract / migration ratchet） | COVERAGE：exact catalog = implementation vocabulary，由 config hook 投影到 Host live config，不再要求 `opencode.json` 手写 22 名；legacy reject = 迁移证明 |
| `tool-referential-integrity` 的 LEGACY_FORBIDDEN_NAMES 清单 | HOW | 旧名 ratchet；「同名唯一合同」才是 WHAT（ENF-009） |

## 历史（考古摘要）

- 历史 change（js-capability-projected-tools）：四层同构立法（§2.1）；「不新增第二份
  Authority」——generator 只读 `AttemptExecutionProfile.ToolCapabilitySet`（§3）；手写矩阵被拒（§1）。
- 历史 why/js-tools 条款：「If a method is present, the capability exists. If a method is absent,
  it does not.」四层同构；万能基类 + prose warning 被拒。
- 历史 why/agent 条款：「双层权限 vs 单层可信」被拒（Host 配置可漂）；external_directory 固定 allow
  元权限 vs 塞矩阵被拒；内部 Agent 从 public enum 消失。
- 历史 shape/agent 条款 AGENT-007/019：双层边界与唯一写点。
- COVERAGE OVERLAP 修复：同构/同源律唯一归 capability-enforcement；repository-programming 只应用
  （因此 `js-surface-gate.mjs` 的语义 oracle 属于本包律的应用）。

## 验证与测试落点

每条 WHAT 命题恰好一行落点。类型：`MOVE` = 本包 tests/ 下文件（物理移入）；`REUSE` = 留原处，记
精确断言锚点与 cutover 拆分计划。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|----------|
| ENF-001 | `requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` `PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority` + `tests/agent-permission-gate.test.mjs` `roles.permissions_agree_with_the_host_schema_matrix` | REUSE（context family SPLIT：prefix-stability/obligation 等）+ MOVE | 分别 `node --test` |
| ENF-002 | `tests/agent-permission-gate.test.mjs` `AGENT_006_role_tool_matrix_reaches_the_host_schema` + `roles.permissions_agree_with_the_host_schema_matrix` | MOVE | `node --test requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-003 | `requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` `PROMPT_008_the_request_kind_is_carried_not_inferred` + `tests/agent-permission-gate.test.mjs`（ROLE_ALLOW 一致性） | REUSE + MOVE | 分别 `node --test` |
| ENF-004 | `requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` `AGENT_010_the_tier_does_not_reach_the_system_prompt_or_the_tool_set` + `tests/agent-permission-gate.test.mjs` `AGENT_010_fast_and_deep_agents_carry_the_same_allow_set`（fast/deep 同 allow list，MOVE）+ managed owned-field projection oracle | REUSE + MOVE；旧“distinct models”前提已废弃，model routing 见 `execution-model-routing` EMR-008 | 分别 `node --test` |
| ENF-005 | `requirements/speculative-investigation/tests/runtime.test.mjs` `STRENGTH_004_replica_host_tool_map_denies_everything_then_allows_exact_readonly` + `requirements/speculative-investigation/tests/host-canary-k0.test.mjs` `STRENGTH_004_005_policy_execution_gate_denies_write_edit_executor_fork_join_network` | REUSE（SPLIT@cutover：strength family KEEP `speculative-investigation`；replica 收窄断言 enforcement 侧归本包） | `node --test requirements/speculative-investigation/tests/runtime.test.mjs` |
| ENF-006 | `tests/agent-permission-gate.test.mjs` `ROLE_ALLOW` + `HOST_skill_remains_allowed_for_every_managed_role` + `ASSUME_is_a_non_authority_utility_for_interactive_roles_only`；`tests/auto-injected-tool.test.mjs` `AUTOINJ_skill_wire_stays_host_owned_and_is_not_plugin_registered` / `AUTOINJ_active_empty_skill_call_is_denied_without_touching_real_skill_names` | MOVE + NEW | `node --test requirements/capability-enforcement/tests/agent-permission-gate.test.mjs requirements/capability-enforcement/tests/auto-injected-tool.test.mjs` |
| ENF-007 | `requirements/capability-enforcement/tests/stealth-browser-mcp-wildcard.test.mjs` `AGENT_026_browser_only_wildcard_permission` + `requirements/capability-enforcement/tests/sphinx-mcp-wildcard.test.mjs` `AGENT_030_inquiry_only_wildcard_permission` | REUSE（SPLIT：文件含 host-boundary 注入断言；wildcard 断言归本包） | 分别 `node --test` |
| ENF-008 | `tests/capability-isomorphism-gate.test.mjs` 全部（含 `capability_iso_repo_scan_is_green`）+ `scripts/checks/capability-isomorphism-gate.mjs` | MOVE + KEEP(gate) | `node --test requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs` |
| ENF-009 | `requirements/capability-enforcement/tests/tool-referential-integrity.test.mjs` `gate_a_*` + `scripts/checks/tool-referential-integrity.mjs` | REUSE（SPLIT@cutover：Gate A = action-affordance 语义合同 + capability-enforcement 名称/结构） | `node --test requirements/capability-enforcement/tests/tool-referential-integrity.test.mjs` |
| ENF-010 | `tests/agent-permission-gate.test.mjs` `AGENT_007_bash_stays_denied_even_when_the_gate_fails` + `AGENT_007_validation_error_is_still_reported` + `AGENT_002_gate_accepts_distinct_models_and_writes_owned_fields` + `AGENT_004_legacy_agent_name_fails_validation` + `tests/managed-agent-config.test.mjs` `MACFG_configureManager_legacy_agent_is_fatal_after_owned_fields_land` + `MACFG_validate_rejects_null_config_and_legacy_agent` + `MACFG_validate_rejects_legacy_agent_present` + `requirements/capability-enforcement/tests/inquiry-permissions.test.mjs` `Inquiry_rolePredicate_inspector_allow_and_host_native_read_gap` + `requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs` `AGENT_007_unresolved_role_denies_all_tools` + `ASSUME_commits_an_abstracted_judgment_without_granting_new_authority` + `tests/fork-tool.test.mjs` `FORK_orchestrator_missing_authority_is_refused_without_session_identity` + `tests/bash-honeypot-tool.test.mjs`（`BASHHONEY_*` 两 test，AGENT-023/ENF-010 同一律 Coder 面） | MOVE + REUSE（×2） | 分别 `node --test` |
| ENF-011 | `tests/agent-permission-gate.test.mjs` `AGENT_019_external_directory_overrides_host_default_ask` + `AGENT_002_missing_agent_is_projected_on_configure` + `AGENT_002_owned_writes_never_touch_the_model_binding` + `tests/managed-agent-config.test.mjs` owned-field projection assertions（`MACFG_validate_accepts_empty_agent_map_and_projects_full_catalog`、`MACFG_applyOwnedFields_*`、`MACFG_configureFromHostConfig_*`） | MOVE；model authority 迁移由 `execution-model-routing` GAP-016 证明 | `node --test requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-012 | `tests/capability-isomorphism-gate.test.mjs` `capability_iso_tool_registry_requires_generator` + `scripts/checks/js-surface-gate.mjs` | MOVE + REUSE（KEEP repository-programming 应用） | 分别 `node --test` / `node scripts/checks/js-surface-gate.mjs` |

### 移动文件

| 源 | 目标 | 结果 |
|----|------|------|
| `requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs` | `requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs` | 7 pass / 0 fail |
| `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` | `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` | 16 pass / 0 fail |
| `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` | 9 pass / 0 fail |

### 计数

WHAT 命题 12；capability 本体落点仍 12；ManagedAgentConfig 的 model-authority 迁移交叉等待 `execution-model-routing` GAP-016。

### 跨包注记

- `managed-agent-config.test.mjs` 的旧 `MACFG_validate_rejects_duplicate_pair_model` 断言已被新语义废弃；实现迁移时必须删除/反转该 oracle。peer 本体只校验名称存在与对称（PID-007），物理 model 可相同（EMR-008）。
- `agent-permission-gate.test.mjs` 的 `ROLE_ALLOW.Reviewer` / `ROLE_ALLOW.Orchestrator` 被
  `office-capability`（OFF-011/012）交叉引用为矩阵投影证据；矩阵断言 owner 是本包。

### semantic anchor id 清单（`scripts/checks/semantic-anchors.mjs`）

本包 **不拥有** 任何 ROLE_SEMANTIC_ANCHORS / OFFICE_CAPABILITY_ANCHORS id：语义锚点是 cognition /
office 内容（`cognitive-environment` / `office-capability` / `action-affordance` /
`epistemic-reasoning` 等逐 id 声明）。capability-enforcement 的可执行 proof 是静态 gate 与 dist 层
权限测试（`capability-isomorphism-gate.mjs`、`agent-permission-gate` 等），不经语义锚点。
