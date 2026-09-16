# crash-reconciliation — HOW

## 架构与核心机制

### 纯恢复代数与决策

- **Family Recovery**：通过 `validateClosurePure` 验证闭包无环，结合各节点观察聚合恢复状态（Blocked > Waiting > Recovered），生成私有的 `FamilyRecoveryPermit`。
- **Child 恢复决策**：基于 durable 记录与 Host 快照，按照严格优先级依次判定为 RecoveredAbandoned、RecoveredTerminal、RecoveryIncomplete 或 RecoveryBlocked。
- **Completion 单一拥有者**：HandleController 负责原子记录完成态与 Pulse 唤醒，通过 retire 标记确保幂等性。

### 显式续传机制（`/continue`）

1. **Command 注册与捕获**：注册 `/continue` 命令，在 `command.execute.before` 阶段查询 durable 关联与 physical snapshot，生成 disclosure briefing。
2. **Materialize 与 Binding**：在真实的 `chat.message` 中将 briefing 注入为带有专用 marker 的 visible user part，并绑定到精确的 `(SessionId, PhysicalUserMessageId)`。
3. **Disclosure-Only 抑制**：命中显式续传 binding 的请求跳过普通的 business routing、Persona 强制与自动 continuation，仅执行基础的 wire sanitization，确保业务效果完全由 LLM 后续显式 tool call 驱动。

## 依赖关系

DEPENDS ON:
- `durable-events`
- `effect-accounting`
- `structured-workflow`
- `host-boundary`
