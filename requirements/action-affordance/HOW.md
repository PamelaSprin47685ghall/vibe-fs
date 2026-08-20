# action-affordance — HOW

## 架构与实现机制

1. **描述资源作为契约载体**：
   - 动作契约文本统一定义于 `resources/provider/tool/<name>/description/{en,zh-CN}.md`。
   - `ToolRegistry` 与 OpenCode `Tool.Def` 仅负责加载已本地化的描述文本，`ToolHostCodec` 负责布局与转义，不拥有散文语义。

2. **双语语义锚点与防退化门禁**：
   - 高风险动词的核心约束通过双语认知锚点进行机械化保护（如 `TOOL_DESCRIPTION_ANCHORS`）。
   - 静态检查门禁保证多语言描述中语义锚点严格成对、无遗漏。

3. **边界镜像机制**：
   - Canonical 权限后果由 `office-capability` 唯一定义；本包在调用边界（如 `fork`、`inspect` 描述）中镜像其负边界与职能分工，防止跨角色误用。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| ACTION-AFFORDANCE-001 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-002 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-003 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-004 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-005 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-006 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-007 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-008 | `requirements/action-affordance/tests/tool-referential-integrity.test.mjs` |
| ACTION-AFFORDANCE-009 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-010 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-011 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-012 | `requirements/action-affordance/tests/action-affordance.test.mjs` |
| ACTION-AFFORDANCE-013 | `requirements/action-affordance/tests/tool-description-anchors.test.mjs` |
