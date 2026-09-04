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
   - `scripts/checks/authority-contracts.json` 是正向 exact-symbol manifest。每行同时记录 class、owner、WHAT、scope、freshness、multiplicity、consume、durability 以及声明/发行 source anchor；它不是按名字放行的 allowlist。
   - `authority-boundary.mjs` 导出可注入 fixture 的 scanner，拒绝 stale anchor、未分类敏感声明、foreign issuance、bool 一次性消费、Capability codec/JSON 持久化，以及未经过 current subject/version/digest admission 的 witness-direct-effect。
   - `Evidence / Decision / Witness / Capability / Receipt / PhysicalHandle` 使用同一六类 DSL；`Vocabulary` 是显式正向分类，确保 `JsCapability` 这类非权威名词不会被名称启发式误判。

5. **Quiescence typed owner gate**：
   - `SessionQuiescenceGate.ObserveIdle` 在 current physical attempt 的 idle edge 上发行 opaque `QuiescencePermit`。
   - `TryConsume` / `TryRelease` 返回 `Result<unit, QuiescencePermitFailure>`；owner mismatch、重复、attempt supersede、revoke、无 fresh idle 各自保持稳定 typed 分支并且 Error 零效果。
   - JS `QuiescenceSurface` 只暴露 typed result view。重启恢复 durable facts 后仍由普通 attempt composition 重新 `ObserveIdle`，不编码或复活旧 permit。

6. **复用既有离任与集成证明**：
   - 离任准入与资源闭包继续由 `RETIRE-001` ~ `RETIRE-008` 的 IncumbencyId、WorkspaceSnapshotId 与 recursive live resources closure 合同建立。
   - 确定性发布与集成门禁由 `CHGINT-001` ~ `CHGINT-006` 对有效 quality candidate 的 typed admission 发行；durable `PublicationCommitted` 是结果，不另造第二套审查权威。

## 验证与测试落点

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
| ENF-013 | `requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-013] all six authority classes require exact positive contracts while JsCapability remains vocabulary` |
| ENF-014 | `requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-014] stale anchors and unclassified sensitive declarations fail closed` |
| ENF-015 | `requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-015] witness cannot drive an effect without current subject/version/digest admission` |
| ENF-016 | `requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-016] stale witness needs a fresh current admission before an effect` |
| ENF-017 | `requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-017] every authority contract declares its multiplicity` |
| ENF-018 | `requirements/capability-enforcement/tests/process-capability-lifecycle.test.mjs::WHAT[ENF-018] process capability consumes once and reports duplicate consumption without effect` |
| ENF-019 | `requirements/capability-enforcement/tests/process-capability-lifecycle.test.mjs::WHAT[ENF-019] provider-attempt composition requires fresh current-process admission without codec or event recovery` |
| ENF-020 | `requirements/capability-enforcement/tests/m6-fatal-boundary.test.mjs::WHAT[ENF-020] invalid configuration reaches one injected fatal adapter only through composition` |
