# capability-enforcement — HOW

## 架构与核心机制

`capability-enforcement` 通过单向权限派生与双层门禁阻断，确保模型视野与执行拦截的同构：

```text
Roles.permissions (Kernel 层单一真相源)
       │
       ├──► ManagedAgentConfig (Host Schema 投影: StaticTools.permissionObj)
       ├──► JsToolGenerator (四层同构生成: 基类方法 / Description / Examples / Gate)
       ├──► AttemptExecutionProfile (单次请求能力集 ToolCapabilitySet)
       └──► ToolRegistry.gateExecute (运行时前置执行拦截: DeniedUnestablished / DeniedRole)
```

1. **同源派生与 Schema 投影**：
   - 托管 Agent 配置初始化时，从 `Roles.permissions` 生成对应角色的工具白名单，写入 Host 原生配置，屏蔽无权工具的 Schema。
   - `external_directory = "allow"` 作为基础设施元权限由统一写入口注入，不混入业务权限。

2. **运行时 Gate 拦截**：
   - `ToolRegistry` 在执行工具前核验当前执行角色的合法性与权限。未决角色直接阻断（`DeniedUnestablished`）。
   - 投机副本（StrengthReplica）执行前建立独立只读工具白名单拦截非只读调用。

3. **四层同构保证**：
   - `JsToolGenerator` 依据当前请求的 `ToolCapabilitySet` 动态合成工具定义代码。
   - 静态检查器 `capability-isomorphism-gate.mjs` 在构建期验证生成的类型成员、描述文本、示例与门禁的一致性。

4. **权威合同与静态边界**：
   - `scripts/checks/authority-contracts.json` 是正向 exact-symbol manifest。每行同时记录 class、owner、WHAT、scope、freshness、multiplicity、consume、durability 以及声明/发行 source anchor；它不是按名字放行的 allowlist。其中 `owner` 是源码 subsystem 身份（与 `scripts/checks/subsystems.json` 解析一致），`whatOwners` 是 requirement package 命题归属；两者不得混用。
   - `authority-boundary.mjs` 仅检查源码文本与正向 manifest：拒绝 stale anchor、未分类敏感声明、可见 foreign issuance、bool 一次性消费与显式 Capability codec/JSON 持久化。源码归属只取唯一 resolved subsystem（显式 `WanxiangshuSubsystem` 优先，否则经 `subsystems.json` 的 legacy 映射解析），旧 `WanxiangshuSemanticOwner` 不再参与 verdict；WHAT 包存在性只看 requirement trace 的 package 集合，不看源码 subsystem 集合。FCS symbol/application/control-flow 输入及其专用断言已删除；文本门禁不证明推断类型、跨函数数据流或 admission 支配关系，这些必须由编译器边界与真实 owner 行为证明。
   - `Evidence / Decision / Witness / Capability / Receipt / PhysicalHandle` 使用同一六类 DSL；`Vocabulary` 是显式正向分类，确保 `JsCapability` 这类非权威名词不会被名称启发式误判。

5. **Quiescence typed owner gate**：
   - `SessionQuiescenceGate.ObserveIdle` 在 current physical attempt 的 idle edge 上发行 opaque `QuiescencePermit`。
   - `TryConsume` / `TryRelease` 返回 `Result<unit, QuiescencePermitFailure>`；owner mismatch、重复、attempt supersede、revoke、无 fresh idle 各自保持稳定 typed 分支并且 Error 零效果。
   - JS `QuiescenceSurface` 只暴露 typed result view。重启恢复 durable facts 后仍由普通 attempt composition 重新 `ObserveIdle`，不编码或复活旧 permit。

6. **复用既有离任与集成证明**：
   - 离任准入与资源闭包继续由 `RETIRE-001` ~ `RETIRE-008` 的 IncumbencyId、WorkspaceSnapshotId 与 recursive live resources closure 合同建立。
   - 确定性发布与集成门禁由 `CHGINT-001` ~ `CHGINT-006` 对有效 quality candidate 的 typed admission 发行；durable `PublicationCommitted` 是结果，不另造第二套审查权威。

`ToolRegistry` 直接消费 Inspector、Fetch、Bookkeeper、Coder、文件变换与生成式 JS 工具的 typed admission／spec，删除模块查找、缺失模块时的备用权限表和静默漏注册路径。注册层只装配既有 provider 合同；`tool-spec-contracts.test.mjs` 与 `internal-leaf-tool-authority.test.mjs` 继续验证公开角色权限和无 attached transaction 时的内部工具拒绝，不以 source token 或生成 JavaScript 布局证明权限正确。

## 验证与测试落点

ENF-015、ENF-016 原来的门禁测试仅提供手写 compiler evidence，已随被禁路径删除，不能作为真实 owner 行为证明。替代证明尚未闭合，记录于 GAP-031；不得以文本检测通过或空 evidence 宣称这两项已验证。

| 命题 | 落点测试 |
|---|---|
| ENF-001 | `requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs::WHAT[ENF-001] PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority` |
| ENF-002 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs::WHAT[ENF-002] AGENT_006_role_tool_matrix_reaches_the_host_schema`；`requirements/capability-enforcement/tests/tool-spec-contracts.test.mjs::WHAT[ENF-002] TOOLSPEC_delegation_tools_have_owner_defined_admission` |
| ENF-003 | `requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs::WHAT[ENF-003] PROMPT_008_the_request_kind_is_carried_not_inferred` |
| ENF-004 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs::WHAT[ENF-004] AGENT_010_canonical_agents_carry_stable_allow_sets` |
| ENF-005 | `requirements/capability-enforcement/tests/strength-replica-tool-map.test.mjs::WHAT[ENF-005] STRENGTH_004_replica_host_tool_map_denies_everything_then_allows_exact_readonly` |
| ENF-006 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs::WHAT[ENF-006] HOST_skill_remains_allowed_for_every_managed_role`；`requirements/capability-enforcement/tests/internal-leaf-tool-authority.test.mjs::WHAT[ENF-006] internal_leaf_tool_declares_attachment_authority_not_a_public_office` |
| ENF-007 | `requirements/capability-enforcement/tests/stealth-browser-mcp-wildcard.test.mjs::WHAT[ENF-007] AGENT_026_wildcard_matrix_mechanism` |
| ENF-008 | `requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs::WHAT[ENF-008] capability_iso_repo_scan_is_green` |
| ENF-009 | `requirements/capability-enforcement/tests/tool-referential-integrity.test.mjs::WHAT[ENF-009] gate_a_repo_scan_is_green` |
| ENF-010 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs::WHAT[ENF-010] AGENT_002_gate_accepts_distinct_models_and_writes_owned_fields` |
| ENF-011 | `requirements/capability-enforcement/tests/managed-agent-config.test.mjs::WHAT[ENF-011] MACFG_applyOwnedFields_writes_owned_keys_and_never_touches_model` |
| ENF-012 | `requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs::WHAT[ENF-012] capability_iso_tool_registry_requires_generator` |
| ENF-013 | `requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-013] all six authority classes require exact positive contracts while JsCapability remains vocabulary`；`requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-013] source identity is a single resolved subsystem without legacy aliases`；`requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-013] WHAT package identity is separate from source subsystem` |
| ENF-014 | `requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-014] stale anchors and unclassified sensitive declarations fail closed`；`requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-014] explicit and legacy-mapped subsystems resolve through the real repository path` |
| ENF-017 | `requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-017] every authority contract declares its multiplicity` |
| ENF-018 | `requirements/capability-enforcement/tests/process-capability-lifecycle.test.mjs::WHAT[ENF-018] process capability consumes once and reports duplicate consumption without effect` |
| ENF-019 | `requirements/capability-enforcement/tests/process-capability-lifecycle.test.mjs::WHAT[ENF-019] provider-attempt composition requires fresh current-process admission without codec or event recovery` |
| ENF-020 | `requirements/capability-enforcement/tests/m6-fatal-boundary.test.mjs::WHAT[ENF-020] invalid configuration reaches one injected fatal adapter only through composition` |
| ENF-021 | `requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs::WHAT[ENF-021] repeat_terminal_idle_is_idempotent_no_duplicate_nudge`；`requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs::WHAT[ENF-021] idle_without_quiescence_permit_spends_no_budget`；`requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs::WHAT[ENF-021] next_terminal_sends_at_most_one_aabb_then_abandons`；`requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs::WHAT[ENF-021] transform_and_idle_interleave_resolves_to_single_owner`；`requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs::WHAT[ENF-021] transform_on_aabb_claimed_terminal_waits_without_double_spend`；`requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs::WHAT[ENF-021] repair_without_journal_abandons_without_physical_sends`；`requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs::WHAT[ENF-021] shutdown_rejects_new_repair_episode_before_drain` |
