# STATUS/blocker-CTX-006 — BlogSquash 生产链路缺失

状态：实现阻塞，不是 SSOT 例外。
触发日期：2026-07-31。
绑定范围：当前工作树 `refactor/ssot-shock-anneal`。

## 结论

`BlogSquashCommitted` 的事实、fold、纯函数投影、唯一 durable writer 与首版生产调用
路径已存在，但完整 CTX-007 结局链仍未验证：invalid terminal repair、attempt 计划的
持久绑定与失败后下一槽重试尚未闭合。

## 已验证的断点

| 环节 | 现状 | 位置 |
|------|------|------|
| 恢复槽判定 | 只有纯函数 `RecoverySlot.mayRecover` / `onSquashOutcome` | `next/Domain/RecoverySlot.fs:99-134` |
| squash projection | 只有纯函数 builder，未被生产调用 | `next/Domain/CompanionProjectionBuilder.fs:66-106` |
| Blogger 发送 | squash 已有局部 request-kind 标记，但仍经普通 `SendAgentOwnerRoot`，未进入 `AttemptExecutionProfile` | `next/Session/CompanionHostBlogger.fs:38-52`、`next/OpenCode/PromptDispatcherSend.fs:101-150` |
| Blogger transform | 已按父 Work Session 找到 CompanionHost，并在 `Squash` request kind 下替换消息；未编译验证 | `next/OpenCode/CompanionTransform.fs:98-121`、`next/Session/CompanionHost.fs:148-151` |
| durable writer | `AppendSquash` 已成为唯一 `BlogSquashCommitted` writer | `next/Session/CompanionTypes.fs:49-52`、`next/Session/CompanionJournalPort.fs:102-145` |
| terminal reconcile | squash 目前从真实 `TerminalOutcome.Completed` 直接调用 durable writer；仍缺 profile 绑定、显式 repair 与 attempt-plan projection | `next/Session/CompanionHostBlogger.fs`、`next/OpenCode/XWire.fs:194-242` |

## 必须同时落地的最小链路

```text
真实 Y 失败
  → HostSignalBootstrap.arm Y 槽
  → 下一次 Y 请求识别 armed + odd Offset + frame material
  → PromptDispatcher 以 ProviderRequestKind.BloggerSquash 发送
  → Y transform 注入 CompanionProjectionBuilder.Squash
  → 记录该 attempt 的 ProviderRunIdentity 与 frame descriptor
  → terminal 只接受 Completed + valid，invalid 进入一次 repair
  → Blob 先写入，再唯一 append BlogSquashCommitted
  → 同槽继续 BloggerMain；squash 失败则推进 cursor 且不发 main
```

其中任一半缺失都不能称为 CTX-006 接线完成。尤其不能把 `BlogProjection.applySquash`
的纯函数调用、mock 响应或测试 fixture 当成生产 writer。

## 处理决定

暂停继续扩展 CTX-006，直到 invalid terminal repair、attempt 绑定与失败后下一槽行为
有生产接线和判据。不得新增第二个 writer，不得新增测试专用生产出口。
