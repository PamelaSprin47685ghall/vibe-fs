# Enforcer — 所有权与边界

## 分层（禁止双写）

### 所有权轴（physical ownership）

0.5.4 之后，Blogger 生命周期不再由长期 `BloggerRuntimeState` / `BloggerRuntimeCell` 程序位置 DU 驱动，而由以下物理所有权轴承载：

| 轴 | 当前权威 |
|------|------|
| busy / current request | Host flight ownership：`HasFlight` + CurrentRequest |
| parked waiter | physical parked registry：`HasParked` |
| pending offer | 独立 PendingOffer 槽 |
| durable lifecycle seal | Journal projection |
| reactivation window | `DrainWindow = Closed | Open of DrainPermit` |
| protocol recovery | durable claim + provider-visible evidence → `BloggerToolRecovery` |

Material routing 是一个纯决策：

```
hasFlight            → Skip
!hasFlight + parked  → Offer
otherwise            → Start
```

### 组件职责

| 组件 | 职责 |
|------|------|
| BloggerCoordinator | 主会话 material 唯一入口 `onMainMaterial` |
| EnforcerHost | continuation / catch-up / repair |
| BloggerRuntimeHost | seal / blocks / reactivate 侧效 |
| BloggerRuntime | 纯 `decideMaterial`：从 `HasFlight` / `HasParked` / ctx 等物理事实派生决策；不是长期 State DU / cell 转移程序计数器 |

### 物理所有权说明

无生产 `BloggerRuntimeState` / `BloggerRuntimeCell` 程序位置 DU；`BloggerRuntimeState.fs` 文件名可保留，但所有权是物理槽（flight / parked / pending / seal / drain），不是 cell 状态机。

规则数据：Domain 校验，Infrastructure 加载；启动 fail fast；无代码内 fallback catalog。

## ENFORCER-041：身份

仅 `ToolContext.messageID` + `callID`；缺失不得进入领域合并（HOST-011）。

## ENFORCER-043：Cycle 有效性

Cycle 有效：可证明 ProviderRunIdentity、至少一个成功 blog、规范化 text 非空、tip→RuleId、ToolCallId 唯一。

## ENFORCER-044：提交边界

`blog.execute` 不直接拆多 BlogFrame 乱序提交。

## ENFORCER-045：BlogEntryCommitted 原子 cycle 事实

`BlogEntryCommitted` **原子**推进 frame 与 coverage——禁止「frame 有了 coverage 没动」或其反面。

## ENFORCER-050：唯一 Offer 决策

同一时刻对 Blogger 的 offer 决策唯一；禁止多入口同时 materialize 请求。

## ENFORCER-064：BloggerToolRecovery 证据投影

缺工具/无效调用的恢复证据投影归属 EnforcerHost：

```fsharp
type BloggerToolRecovery =
    | NoRecovery
    | InteractionNudgeIssued of ProviderRunIdentity
    | AabbRepairConsumed
```

`InteractionNudgeIssued` 携带触发 Nudge 的真实 terminal run。相同 run 重入只表示同一观察重放；不同 run 再次无效才证明 Nudge 语义失败。不得退化成计数器，Coordinator 与 Host 不得各维护一套。

## ENFORCER-160：每个 Companion 最多一个悬挂 transform

每个 Companion 最多一个悬挂 transform。

## ENFORCER-161：不同 Session 独立

不同 Session 的悬挂 transform 与 recovery 状态独立。

## ENFORCER-162：取消

悬挂 transform 取消语义明确；取消后不得当成功 cycle 提交。

## ENFORCER-163：有界处理

catch-up / repair 处理有界；禁止无限环。
