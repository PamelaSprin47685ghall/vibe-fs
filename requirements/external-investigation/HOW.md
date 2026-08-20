# external-investigation — HOW

## 架构与实现机制

1. **角色隔离与权限受控**：
   - 外部网络访问能力（如 `stealth-browser-mcp`）仅对 Browser 角色开放，其他所有角色均受限拦截。
   - 权限矩阵由 `capability-enforcement` 与 `AgentProgram.fs` 实施硬件级隔离。

2. **Role Law 散文契约与语义锚点**：
   - 外部调查的全部证据法则完整固化在 `resources/provider/role/browser/{en,zh-CN}.md`。
   - 通过 `ROLE_SEMANTIC_ANCHORS.browser` 的 8 个核心溯源锚点及 `BROWSER_OBLIGATION_BOUNDARY_ANCHORS`，机械化锁定溯源区分与“观察非义务”负边界。

3. **Canary 契约验证**：
   - 通过 `browser-provenance-canary.test.mjs` 与 `facts-not-obligations.test.mjs`，确保在无真实浏览器运行的单元测试套件中，双语契约与实质性语义区分始终有效且不退化。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| EXTERNAL-INVESTIGATION-001 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-002 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-003 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-004 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-005 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-006 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-007 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-008 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-009 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-010 | `requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs` |
| EXTERNAL-INVESTIGATION-011 | `requirements/external-investigation/tests/facts-not-obligations.test.mjs` |
