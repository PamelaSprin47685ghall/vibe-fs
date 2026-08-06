# Fallback — 可观察行为

条款前缀：`FALLBACK-`。  
所有权（写入口）见 `shape/fallback.md`。  
Cursor 算术、槽位、持久事实与恢复序列见 `how/fallback.md`。

## FALLBACK-001：Fallback 属于 Logical Run

Fallback 不是 Session 永久状态，也不是「模型槽位」。

它是一次 Logical Run 上的恢复策略：SelectedAgent 与 PeerAgent 构成 A/B 两侧；新的 Authority Root 开启新的 Fallback 生命周期（Offset 归零，A 侧 = SelectedAgent）。

跨 Run 不得继承上一次的连续失败计数或侧边状态。

## FALLBACK-004：推进不变量

任意一次已确认失败的 provider attempt，对当前 Logical Run 恰好产生下列效果之一组：

| 结局 | Offset | ConsecutiveFailureCount | Authority 身份 | EffectiveAgent |
|------|--------|---------------------------|----------------|----------------|
| 失败 | 前进一格（见 how） | +1 | 不变 | 可因 Offset 侧变化而变 |
| 成功 | 不变 | 归零 | 不变 | 不变 |

补充不变量：

1. SelectedAgent、PeerAgent、CanonicalRole 永不因 Fallback 改变。  
2. Fallback 只允许改写 `AttemptExecutionProfile.EffectiveAgent`。  
3. Host 仍在自动重试时，插件不得额外发送 continuation。  
4. 仅当 Host 已停止自动重试时，才允许发送同一 Logical Run 的 continuation。  
5. continuation 本身不得触发第二次 cursor 推进。

## FALLBACK-005：有限自动恢复预算

必须区分两件独立的事：

| 概念 | 是否有界 | 含义 |
|------|---------|------|
| A/A/B/B 侧循环 | 无界 | 失败永远可以换侧；循环本身不判死 |
| 自动恢复预算 | 有界 | 连续失败达到预算后停止自动物理请求 |

默认 `AutoRecoveryBudget = 12`（可配置为其它有限正整数）。

达到预算后写入 `FallbackExhausted`，不再自动发出新物理请求。恢复路径只有：

1. 新的 Authority Root（PROMPT-002），或  
2. 用户显式恢复动作。

两者都必须创建新的 cursor（Offset 与连续失败计数归零）。

本条款不定义 wall-clock deadline：预算只数连续失败，不数时间。

## FALLBACK-008：空 / XML-only terminal

空 terminal 或 XML-only terminal 不计入 A/B 失败推进。  
至多允许一次 Interaction Repair continuation；不得因此推进 Fallback cursor。

## FALLBACK-010：Host Attempt 不是领域计数

`HostSignal.ProviderRetry.Attempt` 是 Host 自己的重试序号，语义由 OpenCode 决定，可重置、可重复。

`FallbackCursor.ConsecutiveFailureCount` 是万象术领域计数，只在确认失败的 `ProviderRunIdentity` 上由唯一写入口推进。

禁止：

```text
把 Host Attempt 写入 ConsecutiveFailureCount
用 Attempt 判断是否耗尽预算
用 Attempt 推导 Offset
用 Attempt 决定是否发送 continuation
```

`Attempt` 仅可用于诊断日志与唤醒。Host 是否仍会自动继续，只能由 reconcile 后的完整 snapshot 判断。
