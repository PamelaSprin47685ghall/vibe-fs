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
| Main tip Full/Identity | `TipDeliveryProjection`（fold `TipGuidanceDelivered`） |
| Observation tip 侧 | `EnforcementProjection.RecentTips`（fold `BlogEntryCommitted` + squash co-truncate） |
| Observation frame 侧 | Blog frames（fold `BlogEntryCommitted` / `BlogSquashCommitted`） |

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
| EnforcerHost | continuation / catch-up / repair；`resolveTipGuidance` Full/Identity；cycle validate/commit |
| BloggerRuntimeHost | seal / blocks / reactivate 侧效 |
| BloggerRuntime | 纯 `decideMaterial`：从 `HasFlight` / `HasParked` / ctx 等物理事实派生决策；不是长期 State DU / cell 转移程序计数器 |
| EnforcerCatalog（Domain） | folder 规则校验；TipName→Rule；`pairTipsAndFrames` / Observation 纯函数 |
| EnforcerCatalogResource（Infrastructure） | 扫描 `resources/enforcer/*/`，读 enforcer.md + main.md；合成 Blogger system 片段 |
| EnforcementProjection / TipDeliveryProjection | Journal fold 派生；非平行文件账本 |
| CompanionProjectionBuilder | 消息层 tip+frame Observation 配对渲染（消费 RecentTips + frames） |

### 物理所有权说明

无生产 `BloggerRuntimeState` / `BloggerRuntimeCell` 程序位置 DU；`BloggerRuntimeState.fs` 文件名可保留，但所有权是物理槽（flight / parked / pending / seal / drain），不是 cell 状态机。

规则数据：Domain 校验，Infrastructure 加载；启动 fail fast；无代码内 fallback catalog；无 `catalog.json` SSOT。

### 双消费者边界（禁止交叉污染）

```text
enforcer.md  → 仅 Blogger effective system（检测/边界）
main.md      → 仅 Main TipGuidance（Full 首次 / Identity 重复）
previous_enforcer_tip → 仅 Y 低信任 history（不得进 Main Authority）
TipGuidanceDelivered  → 仅 Main session stream（不得当 Blog frame）
BlogEntryCommitted    → Blog frame + Enforcement tip 同原子；不写 TipDelivery
```

禁止：把 main.md 拼进 Blogger system；把 enforcer.md 当 Main overlay；用进程内存代替 TipDelivery/Enforcement 投影；AgentJournal 与 IEventStore 双写同一逻辑事实。

## ENFORCER-041：身份

仅 `ToolContext.messageID` + `callID`；缺失不得进入领域合并（HOST-011）。

## ENFORCER-043：Cycle 有效性

Cycle 有效：可证明 ProviderRunIdentity、至少一个成功 blog、规范化 text 非空、tip→RuleId（= TipName）、ToolCallId 唯一。

## ENFORCER-044：提交边界

`blog.execute` 不直接拆多 BlogFrame 乱序提交。

## ENFORCER-045：BlogEntryCommitted 原子 cycle 事实

`BlogEntryCommitted` **原子**推进 frame 与 coverage——禁止「frame 有了 coverage 没动」或其反面。  
同一事实派生 Enforcement 半边（RecentTips append）；禁止另造 `EnforcementCycleCommitted`。

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

## Observation 所有权（ENFORCER-070）

| 半边 | Writer / fold | 非所有者 |
|------|---------------|----------|
| tip（RecentTips） | EnforcementProjection via BlogEntryCommitted；squash co-truncate via BlogSquashCommitted | Companion 不得私造 tip 账；物理 Y transcript 不是 tip 源 |
| frame | BlogProjection | Enforcement 不得写 frame |
| 配对视图 | 纯函数 `pairTipsAndFrames` / Companion zip（只读两侧投影） | 不得持久化第二套 Observation 事件流（除非未来显式 EventStore 词汇表变更） |
| Main Full 集合 | TipDeliveryProjection via TipGuidanceDelivered；reanchor 清空 | 不得用文件 ledger 或 process Set |

`ObservationUnit` 是 domain 视图名，不是平行 EventStore 事件爆炸要求；物理事实仍是 BlogEntry / BlogSquash /（Main）TipGuidanceDelivered。

## ENFORCER-160：每个 Companion 最多一个悬挂 transform

每个 Companion 最多一个悬挂 transform。

## ENFORCER-161：不同 Session 独立

不同 Session 的悬挂 transform 与 recovery 状态独立。  
TipDelivery Full 集合按 **Main** session 隔离；Blogger satellite 经 association 解析到 owner Main。

## ENFORCER-162：取消

悬挂 transform 取消语义明确；取消后不得当成功 cycle 提交。

## ENFORCER-163：有界处理

catch-up / repair 处理有界；禁止无限环。

## 规则包边界（ENFORCER-170）

| 层 | 拥有 | 禁止 |
|----|------|------|
| `resources/enforcer/<TipName>/` | 唯一规则正文 SSOT（enforcer.md + main.md） | catalog.json；第三文件当身份；dist 旁路副本 |
| Domain EnforcerRule | 校验后的不可变规则值（Name=RuleId=FieldName；EnforcerText；MainText；lexical order） | 运行期改写目录；fuzzy tip |
| Infrastructure loader | 扫描、读文件、拼 Blogger system 片段 | 业务恢复决策；写 Journal |
| Host / Session | cycle 提交、TipGuidance append、repair | 直接改 resources；私造 catalog |
