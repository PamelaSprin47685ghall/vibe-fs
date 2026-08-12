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
6. StrengthReplica attempt 的成功或失败不进入 owner Logical Run 的 FallbackController，不推进 FallbackCursor，也不清零 ConsecutiveFailureCount（STRENGTH-004/019）。

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

## FALLBACK-013：Host abort / cleanup 残留不计入推进

Host 因 abort 清理而把在途工具调用标记为失败（`status=error` 且 `metadata.interrupted=true`）不是已确认的 provider attempt 失败，不得推进任何 cursor，也不得消耗自动恢复预算。

判据只看 Host 标记，不看错误散文（CTX-005）：

| 证据 | 是否推进 |
|------|---------|
| `status=error` ∧ `interrupted=true`（abort 清理残留） | 否 |
| `status=error` ∧ 无 `interrupted`（工具本身失败） | 是 |

原因：一次 owner attempt 失败会同时被两个观察者看到——它自己的 provider 失败路径，以及被同一次 abort 清理打断的 Companion cycle。两者的 `ProviderRunIdentity` 来自不同 Session，FALLBACK-003 的去重无法折叠，结果是同一次失败被记两次，并让 FALLBACK-002 的 provider 可见 A/A/B/B 顺序取决于两次 append 的竞争。

与 LOOP-006 一致：用户中止与清理中止不得自动 AABB。Companion 侧的具体分派见 `how/enforcer.md` ENFORCER-065/068。

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

## FALLBACK-011：槽内维护子请求

一次自动恢复槽最多两个物理 provider request：

1. 维护子请求：`BloggerSquash`  
2. 业务主请求：`WorkMain` / `BloggerMain`

| 路径 | 结果 |
|------|------|
| 维护失败 | 槽失败，不发主请求 |
| 维护成功 | 不清零 ConsecutiveFailureCount，继续主请求 |
| 主失败 | 槽失败 |
| 主成功 | 清零 ConsecutiveFailureCount |

每个失败槽恰好一次 `FallbackCursorAdvanced`，`ProviderRunIdentity` 指向使该槽终止失败的物理 attempt。维护成功单独不算 Logical Run 业务完成。

## FALLBACK-012：armed 合取

恢复槽允许 X prefix probe 或 Y squash，当且仅当：

```text
1. armedByFailure：本槽由本次自动恢复内紧邻的真实失败推进而来
2. primed：Offset 为奇数（A′ / B′）
```

禁止仅根据持久 Offset 奇偶 arm（成功后 Offset 可停在奇数）。  
`armedByFailure` 是执行局部变量，崩溃后丢失（安全侧）。  
新 Logical Run 第一槽永不 armed。  
不变量：任意两次 squash 之间至少隔一次真实失败。
