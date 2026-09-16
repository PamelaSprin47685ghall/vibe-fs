# execution-model-routing — GAP

## GAP-EMR-001: 新角色集合模型路由与 DevOps 模型锁定闭包

- **现状**：推荐模板与测试套件中仍有部分引用 coder/inspector/distiller 槽位；需全面迁至 engineer/devops 并闭合 DevOps resume 模型不可变逻辑。
- **目标合同**：EMR-018 保证新角色集合路由与旧槽位解耦；EMR-019 保证 DevOps 模型绑定持久性与禁止借 resume 换模型。
- **影响范围**：`src/Wanxiangshu/OpenCode/Host/ModelRouting.fs`、`resources/wanxiangshu.mjs`。
