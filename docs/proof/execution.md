# 执行 — 证明

行为：`what/execution.md`。所有权：`shape/execution.md`。程序：`how/execution.md`。

## Handle / PTY

| 证明 | 条款 |
|------|------|
| 四态不可非法回退 | EXEC-009 |
| PTY completion 仅 onExit | EXEC-015 |
| ABORTED 非 agent 终态 | EXEC-020 |

## Join

| 证明 | 条款 |
|------|------|
| JoinGuard 优先 Review | EXEC-016 |
| 中断 = interrupted 非 error | EXEC-017 |
| EXEC-018 的批次上限、稳定排序、CAS | EXEC-018 |
| blob v2；LegacyFalseAbort 永不 completion | EXEC-021 |
| 假 completion 补偿路径 | EXEC-022 |

## Mailbox / 恢复

| 证明 | 条款 |
|------|------|
| agent Pulse vs PTY Publish 分通道 | EXEC-024 |
| ChildRecovery 分支穷尽与线性序 | EXEC-023 |

代表：`tests/unit/execution/*`（join-v2-wire、handle、fork）。
