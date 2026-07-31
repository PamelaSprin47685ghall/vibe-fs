# STATUS/blocker-CTX-006 — BlogSquash 生产链路缺失

状态：已解除（2026-07-31，`63e9d5d6` 收口）。
触发日期：2026-07-31。
绑定范围：当前工作树 `refactor/ssot-shock-anneal`。

## 解除证据

原列三个断点的处置：

| 断点 | 处置 | 证据 |
|------|------|------|
| invalid terminal repair | 不 repair：squash 压缩不值得 repair（plan P2 裁决，CTX-007）。`Completed + invalid` 走 `TerminalValidity.check` → `failSquash`，等价 squash 失败 | `CompanionHostBlogger.fs` squash `Completed` 分支 |
| attempt 计划的持久绑定 | `scope.RecordAttemptPlan` 在 transform 时写入，`reconcileAttempt` 按 `(SessionId, ProviderRun)` 取回；promote 有 epoch 守卫 | `XWire.fs` `reconcileAttempt` |
| 失败后下一槽行为 | squash `Failed`/`Aborted` 返回 `Error`，reconcile 推进父 Work Session fallback cursor，不发 main（CTX-007 表第二行） | `3f1d707e`、`63e9d5d6`；`XWire.fs` companion 分支 |

P6 出口：`shock-audit` 标记 0、`dotnet build` 绿（0 warning 0 error）、`npm run test:mjs` 433/433 三时区全绿。

## 原阻断结论（归档）

`BlogSquashCommitted` 的事实、fold、纯函数投影、唯一 durable writer 与首版生产调用
路径曾存在，但完整 CTX-007 结局链未验证。已由 P1–P5 接线闭合：

```text
真实 Y 失败 → HostSignalBootstrap.arm / XWire armed 槽传播（P2）
→ handleCompanionTransform 读 arming → SquashIfArmedAsync（CompanionTransform.fs:151）
→ BloggerSquash kind 发送（CompanionHostBlogger.fs squash）
→ TerminalValidity.check → blob 先写 → AppendSquash 唯一 append（P3）
→ squash 失败 → reconcile 推进父 cursor（P4/P5）
```

X probe 侧：`AttemptPlanner.plan` → profile 携带 probe（CTX-010）→ 成功 reconcile
从同一 `ProviderRunIdentity` 提升 `PrefixRebaseCommitted`（CTX-012）。
