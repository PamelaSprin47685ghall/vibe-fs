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

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| ENF-001 | `requirements/capability-enforcement/tests/attempt-plan-authority.test.mjs` |
| ENF-002 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-003 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-004 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-005 | `requirements/capability-enforcement/tests/strength-replica-tool-map.test.mjs` |
| ENF-006 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-007 | `requirements/capability-enforcement/tests/stealth-browser-mcp-wildcard.test.mjs` |
| ENF-008 | `requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs` |
| ENF-009 | `requirements/capability-enforcement/tests/tool-referential-integrity.test.mjs` |
| ENF-010 | `requirements/capability-enforcement/tests/agent-permission-gate.test.mjs` |
| ENF-011 | `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` |
| ENF-012 | `requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs` |
