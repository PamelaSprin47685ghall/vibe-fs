# managed-session-lifecycle — GAP

## GAP-MSL-001: 角色身份整合与旧活跃会话退役收束闭包（CLOSED）

- **状态**：CLOSED
- **现状与闭合证明**：历史 Coder/Inspector 会话构造路径已清除，旧活跃会话显式收束，DevOps 物理会话崩溃恢复维持单一执行权威、锁定模型与排空进程。落点 `tests/023.test.mjs` 与 `tests/024.test.mjs`。
- **目标合同**：MANAGED-SESSION-023 要求新任务仅接纳新身份且旧活跃会话显式收束；MANAGED-SESSION-024 要求固定 DevOps 崩溃恢复维持单一执行权威、锁定模型与排空进程。
- **影响范围**：`src/Wanxiangshu/Execution/Session/`、`src/Wanxiangshu/OpenCode/Host/ManagedAgent.fs`。
