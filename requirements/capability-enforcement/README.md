# capability-enforcement

**一句话 WHY**：provider 看见的 capability 与 runtime 真能执行的 capability 必须同源且不扩大 office
entitlement——否则会出现「看得见但不能做」或更危险的「看不见却能执行」的分叉。

## WHAT 概览

本包保证：每次 provider attempt 有一个 canonical ToolCapabilitySet，由 CanonicalRole × RequestKind
唯一决定；provider-visible schema 与 runtime execution gate 读同一 capability truth；projection 可
按 office + request contract 收窄但不得扩大 entitlement；tier 不改变同 office authority；
StrengthReplica 只 {Read;Glob;Grep}；internal participant 不进无资格工具面；Host-native/MCP/插件等
不同技术来源服从同一 semantic capability policy；js-* 编程面四层同构；工具名引用完整性；双层
fail-closed；external_directory 唯一写点。全部命题见 `WHAT.md`（`ENF-001..012`）。

## HOW 概览

- 域事实：`src/Wanxiangshu/Kernel/Roles.fs`（`ToolPermission`、`Roles.permissions`）、
  `Domain/PromptAuthority.fs`（`toolCapabilitiesFor`、`buildAttemptExecutionProfile`、
  `AttemptExecutionProfile.ToolCapabilitySet`）、`Domain/AttemptPlanner.fs`（唯一构建点）、
  `Domain/JsCapability.fs`（js 投影）。
- schema 层：`Infrastructure/OpenCode/Host/ManagedAgentConfig.fs`（Host-final permission）、
  `Tools/StaticTools.fs`（`permissionObj`）。
- gate 层：`Infrastructure/OpenCode/Tools/ToolRegistry.fs`（`rolePredicate` + `gateExecute`）。
- 详见 `HOW.md`；非 normative。

## proof 概览

- `tests/capability-isomorphism-gate.test.mjs`（自 `tests/unit/verify/` 移入）：四层同构静态 ratchet。
- `tests/managed-agent-config.test.mjs`（自 `tests/unit/host/` 移入）：Host-final 配置门、owned 字段、
  binding 校验。
- `tests/agent-permission-gate.test.mjs`（自 `tests/unit/plugin/` 移入）：AGENT-006/007/010/019
  矩阵投影 + 双层 fail-closed。
- REUSE：`requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs`、`requirements/capability-enforcement/tests/inquiry-permissions.test.mjs`、
  `requirements/speculative-investigation/tests/runtime.test.mjs`（replica 收窄）、`requirements/capability-enforcement/tests/tool-referential-integrity.test.mjs`。
- 落点表见 `PROOF.md`。

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在、RED 长什么样。
2. `WHAT.md` —— 唯一 normative 合同。
3. `HOW.md` —— 实现模型 + 历史与弃权。
4. `PROOF.md` —— 每条命题的测试落点与跑法。

## 边界（不归我）

- office 有资格产生什么 consequence → `office-capability`（DEPENDS ON）。
- 谁在行动（Role/Persona/Binding）→ `participant-identity`（DEPENDS ON）。
- action 描述的五问认知合同 → `action-affordance`。
- 什么信息有资格被看见 → `participant-horizon`。
- 编程面的 SDK 语义（transaction/sandbox/anchors）→ `repository-programming`（本包只拥有「四层同构」
  之律，不重复拥有其应用）。
