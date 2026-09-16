# crash-reconciliation — GAP

## GAP-CR-001: 固定 DevOps 崩溃恢复单一权威与命令去重闭包

- **现状**：需确保在 Host 进程崩溃后，DevOps 的物理执行不会在未收到显式指令前自动重放，且恢复时单一执行权威与模型绑定得到确定性验证。
- **目标合同**：CRASH-020 保证 DevOps 崩溃恢复维持单一权威、命令不自动重放、模型锁定与进程收束。
- **影响范围**：`src/Wanxiangshu/OpenCode/Host/ExplicitResume.fs`、`src/Wanxiangshu/Execution/Session/`。
