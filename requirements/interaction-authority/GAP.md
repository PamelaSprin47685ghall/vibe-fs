# interaction-authority — GAP

## GAP-IA-001: 历史事件不可变隔离与 DevOps 模型锁定闭包（CLOSED）

- **状态**：CLOSED
- **现状与闭合证明**：历史反序列化与活跃 participant-identity 解析已彻底隔离，DevOps resume 路径已严格校验不可变模型绑定。落点 `tests/021.test.mjs` 与 `tests/022.test.mjs`（历史身份隔离与 DevOps 模型锁定）。
- **目标合同**：INTERACTION-AUTHORITY-021 保证历史不重写且旧身份不升权；INTERACTION-AUTHORITY-022 保证 DevOps 恢复与续行保持固定模型与单一执行权威。
- **影响范围**：`src/Wanxiangshu/Interaction/Authority/`、`src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs`。
