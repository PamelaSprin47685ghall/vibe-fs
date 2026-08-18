# capability-enforcement — 可观察合同

本文件是 `capability-enforcement` 包的唯一 normative 语义合同。每条命题 = 当前世界必须同时成立的事实。
证据指针 → `HOW.md`。边界 → `HOW.md`「边界与弃权」。

## ENF-001：每次 provider attempt 有一个 canonical ToolCapabilitySet，由 CanonicalRole × RequestKind 唯一决定

`AttemptExecutionProfile.ToolCapabilitySet` 是每次请求能力的唯一权威：由 `CanonicalRole`（office
身份）与 `RequestKind` 经 `toolCapabilitiesFor` 决定，经 `buildAttemptExecutionProfile` 唯一构建
（`src/Wanxiangshu/Domain/PromptAuthority.fs`）；唯一调用点是 `Domain/AttemptPlanner.fs` 的 `plan`。
禁止在 profile 之外另造能力字段（PROMPT-008 enforcement 侧）。

含义/动机：历史 js-tools 条款「唯一权威投影 vs 手写矩阵」——任何第二份矩阵必然与权威漂移；
`AttemptExecutionProfile` 注释记录了历史事故：四源拼装（mutable session cache、最后用户消息、Role
map、fallback projection）互相矛盾。

边界：RequestKind 的分型本身（WorkMain/Blogger*/StrengthReplica/…）→ `interaction-authority` /
`dispatch-protocol`；本包只拥有「能力集 = Role × RequestKind 的函数」这一事实。

证据：`requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` `PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority`
（REUSE）+ `tests/agent-permission-gate.test.mjs` `roles.permissions_agree_with_the_host_schema_matrix`（MOVE）。

## ENF-002：provider-visible schema 与 runtime execution gate 读同一 capability truth

两层都必须存在且都从同一 Role→permission 集推导（AGENT-007；历史 shape/agent 条款）：

```text
Host-final Agent permission（StaticTools.permissionObj）   → 无权工具不进 provider-visible schema
ToolRegistry execution gate（rolePredicate + gateExecute） → Host 配置异常时仍拒绝越权执行
```

两层各自的机械推导都读 `Roles.permissions`（或 profile 的 ToolCapabilitySet），无第二份矩阵
（AGENT-006）。

含义/动机：Host 配置可漂（历史 agent 条款「双层 vs 单层可信」）；只信一层会在配置异常时漏工具
或越权执行。

边界：Host 配置的物理写入机制 → `host-boundary`；本包拥有「schema 与 gate 同源」的语义。

证据：`tests/agent-permission-gate.test.mjs` `AGENT_006_role_tool_matrix_reaches_the_host_schema` +
`roles.permissions_agree_with_the_host_schema_matrix`（MOVE）。

## ENF-003：capability projection 可按 office + request contract 收窄，但不得扩大 office entitlement

同一 CanonicalRole 在不同 RequestKind 下可以有不同（更窄的）能力面；任何收窄都不得产生比
`Roles.permissions role` 更大的集合（`toolCapabilitiesFor`：普通 WorkMain = role 全集；StrengthReplica
= 更窄子集；非 eligible 角色 = 空集）。`Fission` 的 office entitlement 由
`intra-participant-parallelism` 的 INTRA-PARTICIPANT-PARALLELISM-012 拥有：V1 恰为
Manager、Coder、Inspector、Browser、Inquiry；本包只证明该 entitlement 从同一 `Roles.permissions`
投影到 Attempt profile、Host schema 与 runtime gate，不维护第二份 Fission role 表。

含义/动机：历史 change（js-capability-projected-tools） 的按 RequestKind 分叉案例——能力
「可以完全不同」但方向只能是收窄。

边界：RequestKind 语义归属见 ENF-001 边界；本包拥有「收窄律」（projection ⊆ entitlement）。

证据：`requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` `PROMPT_008_the_request_kind_is_carried_not_inferred`（REUSE）
+ `tests/agent-permission-gate.test.mjs`（`ROLE_ALLOW` 全表 vs `Roles.permissions` 一致性）。

## ENF-004：execution tier 不改变同 office 的 authority：permissions(fast-ROLE) = permissions(deep-ROLE)

`permissions(fast-ROLE) = permissions(deep-ROLE)`（AGENT-010）；不得出现 fast 只读、deep 才可写。
结构性保证：`systemPromptIdFor` 与 `toolCapabilitiesFor` 都只依赖 CanonicalRole，不依赖 tier
（AGENT-001/010）。

含义/动机：tier 是 ExecutionBinding（`participant-identity`），不是 authority；换深度不得换权限
（历史 agent 条款「fast/deep 随换模型演化成两套产品」是反面案例）。

边界：tier 的身份轴语义 → `participant-identity`；本包拥有「权限相等」的执行证明。

证据：`requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` `AGENT_010_the_tier_does_not_reach_the_system_prompt_or_the_tool_set`（REUSE）
+ `tests/agent-permission-gate.test.mjs` `AGENT_002_gate_accepts_distinct_models_and_writes_owned_fields`
（fast/deep 同 allow list，MOVE）。

## ENF-005：request-specific replica/leaf 可进一步收窄：StrengthReplica 只 {Read; Glob; Grep}

`toolCapabilitiesFor Role.StrengthReplica` = `{Read; Glob; Grep}`（eligible role）或空集（非 eligible）
（STRENGTH-004 / PROMPT-008）；replica 的 runtime tool map 先 deny 一切再精确放行这三个只读面，
Host-native 只读之外的一切工具在 replica 内 fail-closed。

含义/动机：replica 是「零影响 speculation」的执行面（`speculative-investigation`），capability 收窄
是 enforce 不扩权；历史 agent 条款 Semble 选型：假 read 污染 primary 可见历史。

边界：replica 的预算/推进/提升 → `speculative-investigation`；本包拥有「replica 能力面精确收窄」
这一 enforcement 律。

证据：`requirements/speculative-investigation/tests/runtime.test.mjs` `STRENGTH_004_replica_host_tool_map_denies_everything_then_allows_exact_readonly`（REUSE，SPLIT@cutover：
strength family KEEP speculative-investigation，本断言 enforcement 侧归本包）+ `requirements/speculative-investigation/tests/host-canary-k0.test.mjs`
`STRENGTH_004_005_policy_execution_gate_denies_write_edit_executor_fork_join_network`。

## ENF-006：internal-only participants/actions 不进无资格 participant 的工具面

Blogger 业务工具面恰为 `{chronicle}`、Distiller 为空、Bookkeeper 仅 `js-bookkeeper`；其它角色不得获得
这些业务面（ENFORCER-010/011、AGENT-006 表）。Host-owned `skill` 工具不属于角色业务 capability，所有角色
保持可用；injection-only guidance（HOST-013 pair hint 与 Blogger 临时 chronicle-direct nudge）可借用 synthetic
`skill({ name: "" })` wire。真实 active empty-name skill call 必须改写为
DENIED，非空 skill name 不得被 HOST-013 拦截、隐藏或改写。

含义/动机：内部路径（运行时合成）不得被模型当作可选工具（历史 agent 条款「内部 Agent 从 public
enum 消失」）；admission（不进 choice surface）归 `participant-horizon`，本包拥有 schema/gate 拒绝。

边界：可见性过滤的认知面 → `participant-horizon`（AGENT-008）；本包拥有工具面拒绝的执行面。

证据：`tests/agent-permission-gate.test.mjs` `ROLE_ALLOW`（Distiller: []、Blogger: [chronicle]）+ `HOST_skill_remains_allowed_for_every_managed_role`；`tests/auto-injected-tool.test.mjs` 的 empty-name DENIED / non-empty pass-through。

## ENF-007：Host-native/MCP/plugin 等不同技术来源的 actions 服从同一 semantic capability policy

`ToolPermission.Network` → Host schema 键 `stealth-browser-mcp_*`，仅 Browser allow（AGENT-026）；
`ToolPermission.Sphinx` → `sphinx_*`，仅 Inquiry allow（AGENT-030）。技术 gate 可以多层（MCP 注入、
schema wildcard、registry），semantic source 只能一个（`Roles.permissions` 中的域能力 token）。

含义/动机：MCP 是 Host 集成机制（`host-boundary`），但它的 permission 语义必须与插件工具同一政策；
历史 shape/agent 条款 三张所有权表（stealth-browser / Sphinx / Semble）都写「禁止第二套 role→MCP 表」。

边界：MCP 启动/注入机制 → `host-boundary`；域能力 token（Network/Sphinx）属于 `Roles.permissions`
（Kernel）；本包拥有「schema wildcard 与 gate 服从同一 policy」的 enforcement。

证据：`requirements/capability-enforcement/tests/stealth-browser-mcp-wildcard.test.mjs` `AGENT_026_browser_only_wildcard_permission`
+ `requirements/capability-enforcement/tests/sphinx-mcp-wildcard.test.mjs` `AGENT_030_inquiry_only_wildcard_permission`（REUSE，SPLIT：
文件同时含 host-boundary 注入断言）。

## ENF-008：js-* 编程面四层同构：capability → base-class member → description → example → runtime gate

对每个 JS filesystem capability：没有该 capability → 生成的基类无对应方法、工具描述不出现该方法、
canonical examples 不出现该方法、伪造底层调用 runtime gate 仍 fail-closed（JS-001/004）。生成唯一
经 `JsToolGenerator.generate`（读 `Roles.permissions` / profile capability set），无手写 `js-*`
ToolSpec 路径；`JsFragmentRegistry` 是成员唯一出生点。

含义/动机：历史 js-tools 条款「If a method is present, the capability exists. If a method is
absent, it does not.」——模型不需要读权限矩阵；运行时拒绝 = 把错误留给调用之后。

边界：JS SDK 的语义（transaction/sandbox/anchors/failure algebra）→ `repository-programming`
（应用方）；本包拥有同构律（COVERAGE OVERLAP 修复：律唯一归本包）。

证据：`tests/capability-isomorphism-gate.test.mjs` 全部（MOVE，7 tests）+ `scripts/checks/capability-isomorphism-gate.mjs`
（gate 本体，KEEP 本包）。

## ENF-009：工具名引用完整性：same tool name → 唯一 schema owner + 唯一 semantic contract

`same tool name ⇒ same semantic act / argument schema / argument meaning / lifecycle consequence /
return semantics / important failure semantics`（ARCH-007 / Gate A）。role visibility 与永不同时出现
不削弱该不变量；`join` 可在 Manager 与 Orchestrator 共享当且仅当语义合同完全同一。

含义/动机：同名不同义让模型在一个名字下学到两套合同；历史 agent 条款「工具名：fork-manager/list/
inspector(tool) 保留 vs commission/horizon/inspect」被拒（DTO 名冒充动词）。

边界：工具描述的语义合同内容 → `action-affordance`；本包拥有「名称唯一 + schema 结构唯一」的
enforcement（结构侧）。

证据：`requirements/capability-enforcement/tests/tool-referential-integrity.test.mjs` `gate_a_*`（REUSE，SPLIT：Gate A =
action-affordance 语义合同 + capability-enforcement 名称/结构）+ `scripts/checks/tool-referential-integrity.mjs`。

## ENF-010：双层 fail-closed：Role 未定 → 工具集空/拒绝执行；Host 配置异常仍写 deny 默认

- Role 或 profile 无法确定 → 模型可见插件工具集为空；ToolRegistry `gateExecute` 对 unresolved role
  返回 deny（`Path.DeniedUnestablished`），禁止「role 未定时暂时允许 Inspector」类放行
  （历史 shape/agent 条款 AGENT-007）。
- Host `config` hook 校验失败（如 managed catalog/owned-field 投影结构非法；模型 scheduler 模块/ABI 校验归 `execution-model-routing`）仍必须先写入 owned `mode`/`permission`/`prompt`，使 managed agent 不回落 Host 默认（`"*": "allow"` 会把 bash 开放给每个角色）；随后必须 `Diagnostic.fatal` 终止整个 OpenCode 进程。配置 gate 的 Error 不得降格为可被 Host 捕获后继续运行的异常。

含义/动机：真实回归有两层：校验失败 short-circuit 曾导致权限写失败，bash 对所有 managed role 开放；
后续仅抛可捕获异常又允许 Host 继续，使缺失可信 inventory 延迟表现为无关的 PROMPT-006 binding failure。
配置非法时唯一合法状态是「deny 已落地 + 进程死亡」。

边界：bash 本身是 Host 内置工具（`host-boundary`）；本包拥有「managed 面 fail-closed 不回落默认」。
`bash-honeypot` 仅 Coder 且不执行 shell（AGENT-023）是同一律的 Coder 面。

证据：`tests/agent-permission-gate.test.mjs` `AGENT_007_bash_stays_denied_even_when_the_gate_fails` +
`AGENT_007_validation_error_is_still_reported`（MOVE）+ `tests/managed-agent-config.test.mjs`
`MACFG_configureManager_legacy_agent_is_fatal_after_owned_fields_land`（MOVE）+
`requirements/capability-enforcement/tests/inquiry-permissions.test.mjs`
`Inquiry_rolePredicate_inspector_allow_and_host_native_read_gap`（REUSE）+ `requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs`
`AGENT_007_unresolved_role_denies_all_tools`（REUSE）。

## ENF-011：external_directory=allow 是 Host 路径边界元权限：每 managed agent 显式写入、唯一生产写点

`external_directory = "allow"` 写入每个 managed agent 的 Host-final permission（排在 Host 默认
`ask` 之后，flat merge + findLast 求值为 allow）；唯一生产写点是 `StaticTools.permissionObj` →
`ManagedAgentConfig.applyOwnedFields`（AGENT-019）。禁止：省略覆盖、编入 `Roles.permissions` /
`ToolPermission` / AGENT-006、用全局 permission 顶替 agent 级写入、借本条放宽角色工具。

含义/动机：历史 agent 条款「external_directory 固定 allow vs 塞进角色工具矩阵」——塞矩阵会污染
AGENT-006 语义边界并诱导「工具白名单 = 一切权限」的错心智。

边界：external_directory 的路径边界机制本身 → `host-boundary`；本包拥有「唯一 enforcement 写点 +
不进角色矩阵」。

证据：`tests/agent-permission-gate.test.mjs` `AGENT_019_external_directory_overrides_host_default_ask`（MOVE）
+ `tests/managed-agent-config.test.mjs` 的 owned-field projection 断言（model authority 已迁至 `execution-model-routing` EMR-008，GAP-016）。

## ENF-012：工具名投影唯一写入口 = CanonicalRole → permission；禁止第二套旧名表/手写矩阵

矩阵唯一写入口仍是 `CanonicalRole → permission` 投影（历史 shape/agent 条款 AGENT-007）；旧工具名
（fork-manager/list/inspector(工具)/verdict/blog/executor(工具)/fork-pty/edit-qa/return）非法、无
alias；`js-*` 工具名只能由 generator 运行时产生，生产源码中任何字面量 per-role `js-*` 名 = 手写
变体，fail-closed（`scripts/checks/js-surface-gate.mjs`）。

含义/动机：第二套旧名表/手写矩阵必然与权威漂移（历史 agent 条款「矩阵唯一写入口」）；工具名
是动词，DTO 名冒充动词强迫模型解码机器拓扑。

边界：`js-surface-gate.mjs` 文件 owner = `repository-programming`（应用）；本包拥有「唯一写入口」律
（同源律的应用边界见 COVERAGE OVERLAP 修复）。

证据：`tests/capability-isomorphism-gate.test.mjs` `capability_iso_tool_registry_requires_generator`
（handwritten-js-tool-spec 拒绝，MOVE）+ `scripts/checks/js-surface-gate.mjs`（REUSE：repository-programming
应用）。
