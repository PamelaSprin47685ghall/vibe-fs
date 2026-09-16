# managed-session-lifecycle — GAP

## GAP-MSL-001: 角色身份整合与旧活跃会话退役收束闭包

- **现状**：生产代码尚存历史 Coder/Inspector 会话构造路径；DevOps 物理会话崩溃恢复时模型绑定与进程收束需在 ManagedAgent / SessionExecutionBinding 层面完整闭合。
- **目标合同**：MANAGED-SESSION-023 要求新任务仅接纳新身份且旧活跃会话显式收束；MANAGED-SESSION-024 要求固定 DevOps 崩溃恢复维持单一执行权威、锁定模型与排空进程。
- **影响范围**：`src/Wanxiangshu/Execution/Session/`、`src/Wanxiangshu/OpenCode/Host/ManagedAgent.fs`。
