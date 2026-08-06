# Enforcer → FallbackController 桥接缺口（ENFORCER-062/067/068）

目标：
- Nudge 后的新无效 terminal 或无需 Nudge 的协议失败，必须经 `FallbackController.recordConfirmedFailure` 唯一推进 A/A/B/B cursor，并服从 `AutoRecoveryBudget`。

当前：
- `Session/EnforcerHost.fs` 的 `aabbRepair` 只把进程内 `BloggerToolRecovery` 标为 `AabbRepairConsumed`，刷新当前 request 并注入 repair projection。
- 该路径没有调用 `FallbackController`，不写 `FallbackCursorAdvanced` / `FallbackExhausted`，也不改变下一物理 attempt 的 `EffectiveAgent`。
- 现有 `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs` 只断言 repair marker 与 runtime cell，未断言 cursor、预算或唯一写入口。

缺口：
- 当前名为 “AABB” 的修补不是 FALLBACK-002/003 定义的 A/A/B/B 恢复；ENFORCER-062/067/068 尚未落地。
- 实现应把确认失败交给 `FallbackController`，再按其 `MayContinue | Exhausted` 结果决定是否经 `PromptDispatcher` 发送下一 attempt；同一 `ProviderRunIdentity` 重放必须只推进一次。

阻塞：
- 无。需补 cursor/budget/EffectiveAgent 行为测试，且保持 `FallbackController` 为事实唯一 writer。
