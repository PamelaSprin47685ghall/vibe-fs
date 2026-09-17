# execution-model-routing — GAP

## GAP-EMR-001: 新角色集合模型路由与 DevOps 模型锁定闭包（CLOSED）

- **状态**：CLOSED
- **现状与闭合证明**：已全面迁至 engineer/devops 并闭合 DevOps resume 模型不可变逻辑。落点 `tests/018.test.mjs` 与 `tests/019.test.mjs`（ModelRoutingSurface 驱动 fail-closed 与 boundDevopsTarget 不可变锁定）。
- **目标合同**：EMR-018 保证新角色集合路由与旧槽位解耦；EMR-019 保证 DevOps 模型绑定持久性与禁止借 resume 换模型。
- **影响范围**：`src/Wanxiangshu/OpenCode/Host/ModelRouting.fs`、`resources/wanxiangshu.mjs`。
