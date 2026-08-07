# 15 — Blogger as Enforcer（Rebased to current baseline）

> Rebase 基线：仓库 `0.5.4` + 当前 `Unreleased` 文档治理与实现，快照日期 2026-08-07。  
> 本文件是对旧 `15.md` 的语义 rebase / 设计说明，不是当前产品规范，也不重新定义任何正式 `ENFORCER-*` Clause。  
> 当前正式语义只以 `docs/{why,what,shape,how,proof}` 为准；实现面以 `src/`、`resources/` 与 tests 为证据。  
> 旧 `15.md` 中的 Clause 编号、公式、状态机和规则全文只作为历史输入，不在本文继承其规范权威。

---

## 0. Rebase 结论

旧 `15` 的真正目标不是“维护一个 0–9 评分系统”，而是同时满足四件事：

1. Blogger 继续异步维护可恢复的稠密工作日志；
2. Blogger 对工程行为给出结构化、可审计的监督信号；
3. Blogger 的失败、busy、重启和 compaction 不阻塞 Main，也不破坏 coverage；
4. 所有语义从 durable facts 与可证明的物理身份恢复，而不是从 transcript 或内存状态猜测。

旧稿把第 2 点实现为：

```text
120 个 optional 0..9 字段
→ score vector
→ leaky-evidence throttle
→ NudgeAnchored / NudgeConsumed
→ Main Session fake-user enforcement overlay
```

最新基线已经明确收敛为另一套、更小的语义核：

```text
Main material
→ typed BloggerRequestContext durable materialization
→ Blogger provider run
→ exactly one blog(text, tip, evidence?)
→ deterministic Blogger Cycle
→ BloggerMain: one atomic BlogEntryCommitted
      = work-log frame + RecordCoverage + one TipRuleId
→ bounded RecentTips
→ future Blogger prompt as low-trust history
```

因此本次 rebase 的核心裁决是：**保留“异步工作日志 + 工程监督 + 原子恢复”这一产品意图，删除旧 score/throttle/Main-overlay 机制，不试图给它们找同名的新壳。**

最新语义里，`tip` 是一次 cycle 的单一工程判断；它是 Blogger 自身后续判断的低信任历史，不是 Main 的新 Authority，也不产生 engineering fake-user message。

---

## 1. 第一性原理

### 1.1 Work log 是主产物，Enforcer 信号是附属语义

Blogger 首先是 Companion work-log writer。每次有效 `BloggerMain` cycle 必须产生一个稠密事实 frame，并与 coverage 原子提交。

Enforcer 不应建立第二套“与日志平行的运行时”。最新设计因此把监督信息压到同一原子 cycle 上：

```text
BlogEntryCommitted
├── TextRef / TextDigest            # 工作日志事实
├── coverage fields                 # 覆盖推进
├── ProviderRun / ToolCallIds       # cycle 身份
├── TipRuleId / FieldNameAtCommit   # 单一工程 tip
└── EvidenceRef?                    # 可选证据
```

不存在独立 `EnforcementCycleCommitted`，也不存在独立 score report stream。

### 1.2 监督不应变成第二个控制循环

旧 score vector 必然引出：聚合、衰减、时间、阈值、reset、overlay 锚定、消费确认和恢复窗口。它把“给出一个工程意见”升级成第二个解释器。

最新设计把不可机械证明的事情留给模型：**在当前观测中选择一个最有价值、最可行动的 tip**。Host 只证明：

- tip 来自 catalog；
- cycle 身份成立；
- text 非空；
- commit 与 coverage 原子；
- replay / recovery 不重复产生业务事实。

Host 不重新解释严重度，不对工程判断做数值积分。

### 1.3 物理事实优于影子状态

0.5.4 之后，Blogger 生命周期不再由 `Idle | InFlight | Parked | Disposed` 这样的长期 `BloggerRuntimeState` 驱动。

当前可观察轴分别有真实所有者：

| 轴 | 当前权威 |
|---|---|
| busy / current request | Host flight ownership：`HasFlight` + CurrentRequest |
| parked waiter | physical parked registry：`HasParked` |
| pending offer | 独立 PendingOffer 槽 |
| durable lifecycle seal | Journal projection |
| reactivation window | `DrainWindow = Closed | Open of DrainPermit` |
| protocol recovery | durable claim + provider-visible transcript 派生的 `BloggerToolRecovery` |

Material routing 只是一个纯决策：

```text
hasFlight        → Skip
!hasFlight + parked → Offer
otherwise        → Start
```

不能为了“写起来像状态机”重新制造一套镜像状态。

### 1.4 Provider contract 必须闭合，不从错误输入发明业务含义

旧稿的 fuzzy key normalization / Damerau–Levenshtein repair 会把未知字段强行解释成某条工程规则。

最新语义相反：

```text
text      required string
 tip       required exact catalog field enum
 evidence  optional string
```

缺 tip、空 tip、未知 tip 都失败；不存在“最接近的规则”。额外 property 不得偷偷变成 tip 或 text。

### 1.5 Main Authority 不因 Enforcer 改写

Main 的 Authority、Logical Run、Fallback、Projection 与 ProviderInputSeal 继续由现有正式域拥有。

当前 Enforcer **不向 Main 注入工程 fake-user message**。如果未来重新提出“将工程意见送给 Main”，那是一个新的产品变更，必须重新裁决消息类型、Authority 关系、投影稳定性、seal 和恢复；不能从旧 `15` 的 `NudgeAnchored` 机制暗渡回来。

---

## 2. 当前语义核

### 2.1 `blog` 是 Blogger 唯一工具

fast-blogger 与 deep-blogger 共用 authoritative system prompt，唯一工具为 `blog`。

当前 provider-facing 逻辑合同：

```text
blog(
    text: required string,
    tip: required enum<catalog.field>,
    evidence?: string
)
```

约束：

- 每个请求恰好调用一次 `blog`；
- `text` trim 后必须非空；
- `tip` 必须精确命中 `resources/enforcer/catalog.json` 的 `field`；
- `evidence` 可缺省，缺省不发明内容；
- `blog.execute` 只做边界校验并立即返回固定成功结果 `OK`；
- merge / commit 发生在 continuation boundary，不在 tool execute 内挂起。

### 2.2 tip 不是 score

`tip` 表达：“这次观测里最值得现在提醒的一个工程问题是什么？”

它不是：

- 严重度；
- 置信度数字；
- 多规则集合；
- throttle 输入；
- Main Session 的 instruction。

System prompt 要求 Blogger：

1. 每次只选一个 tip；
2. 优先当前最有价值、最可行动的问题；
3. 查看 `previous_enforcer_tip` 历史，避免无意义的密集重复；
4. 但严重、阻断或持续复发的问题可以重复；
5. `text` 与 tip 围绕同一个核心问题，不在正文里列一串 enforcement 清单。

这把旧 throttle 的“机械重复抑制”改为一个有界、可观察但不引入第二控制系统的模型策略。

---

## 3. Normal 与 Squash

### 3.1 Normal request

Blogger 的 provider-visible projection 由 durable state 重建：

```text
low-trust previous_enforcer_tip assistant messages
+ durable historic_frame assistant messages
+ one physical user delta message LAST
```

delta 是 instruction-first Synthetic TOML，后接 `[[new_work_to_record]]` 数据。

有效 normal cycle：

```text
canonical text
+ one canonical tip
+ optional evidence
→ one BlogEntryCommitted
```

此事实同时推进 frame 与 RecordCoverage，并把 tip 写入 EnforcementProjection。

### 3.2 Squash request

Squash 是 B 表示的重写，不是对新 Main 工作的再次观察：

```text
previous tips
+ selected historic frames
+ final squash instruction user message
```

当前 provider contract 仍要求 squash 调用 `blog` 时提供一个合法 tip；但 squash durable commit 只保留 rewritten frame，不推进 Main coverage，也不把该次 tip 追加为新的 RecentTip。

这是合理的因果边界：**重写历史表示不应伪造一次新的工程观察。**

已有 RecentTips 不因 squash 被清空。

---

## 4. Blogger Cycle 与确定性归并

### 4.1 身份

一个 cycle 绑定一个 provider assistant run。工具执行边界上的身份来自 `ToolContext.messageID` / `callID`；continuation 从完整 assistant snapshot 读取同一 provider step 的 completed `blog` parts。

身份不足时不能进入领域 commit。

### 4.2 多调用是违约，但必须防御性收敛

正常协议仍是 exactly one `blog`。Provider 偶发给出多个有效调用时，不因为协议违约就丢掉整轮 coverage。

当前生产实现按 provider-visible `PartOrdinal` 排序：

```text
MergedText
  = 非空 text 按 PartOrdinal
  = "\n\n" 拼接

CanonicalTip
  = PartOrdinal 最早的已 decode tip

MergedEvidence
  = 非空 evidence 按 PartOrdinal
  = 完全相同文本去重
  = "; " 拼接
```

`MultiCall = true` 只用于记录 protocol violation，不引入新的业务分支。

当前实现还有明确边界：

- defensive merged tool-call cap：32；
- merged text UTF-8 上限：512 KiB；
- evidence UTF-8 上限：128 KiB。

这些是实现安全界，不应重新演化成业务 score / severity 参数。

### 4.3 当前 baseline 有一个必须显式承认的文档漂移

当前 `docs/how/enforcer.md` 仍写着“多调用时按 catalog ordinal 选 tip”；但：

- `src/Wanxiangshu/Domain/EnforcerCycle.fs` 明确实现“PartOrdinal 最早 tip”；
- `tests/unit/enforcer/cycle-nudge.test.mjs` 固定该行为；
- `tests/unit/enforcer/tip-v2-contract.test.mjs` 也固定该行为。

`docs/what/enforcer.md` 只要求选择确定，不规定具体优先级。

因此本 rebase 采用**当前可执行且有 proof 的 PartOrdinal-first 行为**作为最新事实，同时把 `docs/how/enforcer.md` 视为需要单独修正的 baseline gap。不能把这个矛盾藏在 rebase 文案里。

---

## 5. 原子持久化与恢复

### 5.1 请求在发送前 materialize

Main material 要变成 Blogger provider request 前，先冻结 typed `BloggerRequestContext` 并写入 `BloggerRequestMaterialized`。

它携带请求身份、Main/Blogger Session、RequestKind、context blob/digest、observed prefix epoch、coverage baseline、frame epoch、selected frame digests 等因果信息。

物理 send 不能成为“上下文是什么”的唯一证据。

### 5.2 `BlogEntryCommitted` 是 normal cycle 的唯一业务提交

当前 fact 的关键字段包括：

```text
SessionId
BloggerSessionId
RequestId
FrameEpochId
Previous/NextIngestedThroughSequence
Previous/NextCoverableTurnCutoffExclusive
NextCoveredPrefixDigest
TextRef / TextDigest
ProviderRun
ToolCallIds
TipRuleId
FieldNameAtCommit
EvidenceRef?
ObservedPrefixEpochId
```

其不变量是：

```text
frame append
+ RecordCoverage advance
+ prefix coverage proof
+ provider/tool identity
+ tip identity
```

必须属于同一个原子事实。

没有 `ScoreVectorRef`。

### 5.3 Tip 的 durable identity

`TipRuleId` 是稳定业务身份；`FieldNameAtCommit` 保留提交时的 field 名快照，便于审计与低信任呈现。

`EnforcementProjection` 由 `BlogEntryCommitted` 派生：

```text
ByProviderRun: ProviderRun → EnforcementCycleRecord
RecentTips: bounded oldest → newest list
```

当前 `RecentTipLimit = 8`。

重复 ProviderRun 不得产生第二个 enforcement record；cycle receipt 与 BlogEntry 同样用于 exactly-once / recovery 判定。

### 5.4 CommitUnknown

任何 durable append 结果为 `CommitUnknown` 时都 fail closed 并进入 reconcile。不能“为了保险”再问模型一次，因为那会把未知写入变成重复外部效果。

恢复优先使用：

```text
Journal fold
+ materialized request context
+ Host assistant/provider-step snapshot
+ ProviderRunIdentity / ToolCallId
+ cycle receipts / blob digest
```

物理 Y transcript 不是有效 work-log 历史的 SSOT。

---

## 6. 运行时协调：从 State DU 映射到物理所有权

旧 `15` 里的：

```text
Idle
InFlight of BloggerRequestContext
Parked
Disposed
```

不应继续保留。0.5.4 已经把这种程序位置镜像删除。

### 6.1 唯一 material 入口

`BloggerCoordinator.onMainMaterial` 是 Main material → Blogger 生命周期的唯一生产入口。

处理顺序从事实出发：

```text
durable lifecycle seal / drain gate
→ HasFlight?
→ recovery squash opportunity?
→ compute next typed Main context from durable coverage + XTrace
→ BloggerRuntime.decideMaterial(HasParked, HasFlight, ctx)
```

### 6.2 Busy

busy 的唯一含义是 Host 当前拥有 flight：

```text
HasFlight = true
```

此时新 material：

- 不打断当前 request；
- 不替换 CurrentRequest；
- 不推进 RecordCoverage；
- 不建立字符串 delta queue；
- material 保留在 XTrace，后续从新的 coverage baseline 自然 catch up。

### 6.3 Park / Offer

有 parked waiter 且无 flight 时，新的 typed context 进入独立 PendingOffer 槽并唤醒 parked transform。

Parked waiter、CurrentRequest、PendingOffer 是不同的物理资源，不用一个 `State` 字段把它们压成互斥 case。

### 6.4 Seal / Drain

Main 的 durable lifecycle handle 被 seal 后，新的 Blogger work 默认被阻止；新 Authority Root 可通过不可伪造的 `DrainPermit` 打开一次 drain window，使必要的后续 material 能继续收敛。

这也是为什么 `Disposed` 不再是业务状态：生命周期结束体现在 durable seal、registry ownership 与资源 teardown 上。

---

## 7. Continuation、Repair 与 Fallback

### 7.1 成功路径

有效 cycle commit 后：

```text
commit known
→ 清理本次 physical flight ownership
→ rebuild from durable frames + typed context
→ 若有 catch-up material 则继续
→ 否则 continuation 可以 park 等未来 offer
```

Main Session 不以“等待 Blogger 输出”作为自己的前置条件。

### 7.2 `InteractionNudge` 的名字必须重新解释

旧 `15` 的 “Enforcement Nudge” 是投影给 Main 的工程警告。

**当前仓库里的 `InteractionNudge` 完全不是这件事。**

它是 Blogger 违反 `blog` 协议后的真正 `InteractionRepair` continuation，例如模型完成了一轮却只输出 prose。它：

- 发送给 Blogger 自己；
- 不创建新的 Authority Root；
- 每个逻辑请求最多一次；
- 用触发它的真实 terminal `ProviderRunIdentity` 作为 durable recovery evidence；
- 同一 terminal 重放幂等；
- 新 terminal 仍失败才证明 repair 语义失败，随后进入统一 Fallback。

`BloggerToolRecovery` 当前只有：

```text
NoRecovery
InteractionNudgeIssued of ProviderRunIdentity
AabbRepairConsumed
```

它是从 durable claim + provider-visible evidence 派生的恢复视图，不是长期 cell 上的 repair counter / stage。

### 7.3 不为 Blogger 另造 Fallback 预算

协议 repair 失败、tool execution error 或其它 confirmed failure 进入统一 `FallbackController`。Enforcer 不建立独立 retry cursor、cooldown 或 wall-clock budget。

---

## 8. Projection 与 `previous_enforcer_tip`

### 8.1 RecentTips 只作为低信任 Blogger history

每个 normal `BlogEntryCommitted` 派生一个 RecentTip：

```text
RuleId
FieldName
CycleId = ProviderRunIdentity
```

重建 Blogger provider view 时，RecentTips oldest → newest 渲染为：

```toml
[[do_not_exec]]
kind = "previous_enforcer_tip"
tip = "primitive-obsession"
cycle = "..."
```

消息 role 是 `assistant`，且明确是 low-trust history，不是 parent instruction。

### 8.2 当前不存在 Main enforcement overlay

旧稿的：

```text
role=user fake enforcement message
NudgeAnchored
NudgeConsumed
PrefixEpoch-local append-only overlay
ProviderInputSeal 包含 enforcement digest
```

在当前产品中没有对应运行时路径。

当前 `previous_enforcer_tip` 的消费方是 Blogger 的 projection builder。Main 的 Companion memory 仍由 durable work-log frames / 正式 Context Projection 规则构造；tip history 不应被解释为 Main 指令。

### 8.3 Compaction 不再承担“清空 enforcement overlay”

因为 overlay 已不存在，PrefixEpoch 切换也不再有旧稿中的 throttle/nudge reset 语义。

当前 RecentTips 是独立的 bounded EnforcementProjection：

- normal commit 追加；
- squash 不清空；
- restart / recovery 重建可继续读取；
- compaction rebuild 继续通过同一个 builder 呈现；
- 超过上限只保留最近 8 条。

不要把旧“epoch 内永久，epoch 切换清空”的规则迁移到 tip v2。

---

## 9. Rule Catalog：资源是 SSOT，文档不复制 120 条

当前 catalog 唯一运行时资源：

```text
resources/enforcer/catalog.json
```

Domain 类型：

```text
EnforcerRule
  RuleId
  FieldName
  Family
  ScoreWhen
  Nudge
  CatalogOrdinal
```

这里的 `ScoreWhen` / `Nudge` 是 catalog 描述数据的历史命名，**不代表 runtime 仍有 score-vector 或 Main nudge pipeline**。

启动校验要求：

- `schemaVersion` 支持；
- catalog 非空；
- RuleId 唯一；
- FieldName 唯一；
- `catalogOrdinal` 连续 `1..N`；
- 必需文本非空；
- 资源缺失、JSON 非法、Domain 校验失败时启动 fail fast；
- 不存在代码内 fallback catalog。

当前打包资源恰好有 120 条规则，当前 tests 也固定 120；但 rebase 文档不再复制整份 120 条正文，也不把“120”重新做成协议形状。**协议只依赖 catalog field enum；规则内容只在资源中维护一次。**

这样避免旧 `15` 中“规范规则表 + schema + 生成器 + runtime”多份真相重新漂移。

---

## 10. 明确删除的旧语义

以下内容不是“待迁移”，而是已经被最新架构否定或 clean break 的历史机制：

| 旧 `15` 机制 | 最新映射 |
|---|---|
| 每规则 optional `0..9` 字段 | 删除；改为 required single `tip` |
| 缺失 score = 0 | 删除；无 score 语义 |
| 数字字符串 / clamp / score parser | 删除 |
| `enf_*` 字段命名空间 | 删除 |
| Damerau–Levenshtein typo repair | 删除；tip 精确 enum |
| 同规则 score 取 max | 删除 |
| `EnforcementReport` score vector | 删除；cycle enforcement half 只存一个 TipRuleId |
| `EnforcementObservationOrdinal` | 删除 |
| leaky integrator / `tau` / pressure threshold | 删除 |
| `EnforcerThrottle` | compiled tombstone，零生产调用 |
| score-batch `EnforcerNudge` renderer | compiled tombstone，零生产调用 |
| `NudgeAnchored` / `NudgeConsumed` | 删除 |
| Main fake-user engineering nudge | 删除，无当前等价物 |
| PrefixEpoch enforcement overlay | 删除 |
| epoch 切换清 throttle / nudge | 删除 |
| `BloggerRuntimeState = Idle/InFlight/Parked/Disposed` | 删除；物理 ownership 轴替代 |
| `BloggerRuntimeCell` | 删除 |
| 文档内复制 120 条 catalog | 删除；`catalog.json` 单源 |
| `SSOT/15` 自己定义全部 `ENFORCER-*` | 删除；正式 Clause 只在 `docs/` 分层定义 |

这张表是 rebase 的关键：它防止“把删掉的机制换个名字又带回来”。

---

## 11. 旧概念 → 最新概念映射

| 旧概念 | 最新概念 | 语义变化 |
|---|---|---|
| BloggerMain scoring report | `BlogEntryCommitted` enforcement half | 从 N 维 score 变为一个 TipRuleId |
| `EnforcementReport` | `EnforcementCycleRecord` | 由 BlogEntry fold 派生，不独立写 fact |
| score history | `RecentTips` | 有界 8 条，只记录 tip identity |
| throttle anti-spam | prompt anti-repeat policy | 从 Host 数学控制转为模型选择约束 |
| engineering Nudge → Main | **无等价物** | 当前不向 Main 交付 tip |
| Nudge recovery facts | protocol-repair durable claim | 只用于 Blogger InteractionRepair，不是工程反馈 |
| Runtime State DU | physical slots + pure `decideMaterial` | 程序位置变成物理事实组合 |
| synthetic delta append | durable frames + typed context rebuild | 不复用脏物理 Y transcript |
| generated catalog/schema | packaged JSON + Domain validation | 数据单源、启动 fail fast |
| rule-specific top-level fields | `tip` enum | wire contract 从 120 维降到 1 维 |
| old giant SSOT | five-layer formal docs + resource + proof | Change / history 与当前规范分离 |

---

## 12. 当前仓库中与本 rebase 直接相关的 baseline gaps

本 rebase 不是“把当前仓库当成无矛盾真理”。至少有两处最新源码已经超过正式文档：

### Gap A — ENFORCER-025 多调用 tip 选择

- `docs/how/enforcer.md`：catalog ordinal 优先；
- `EnforcerCycle.fs` + 两组单测：PartOrdinal 最早优先。

应修正式 `how`，或重新裁决后改实现与 proof；不能长期双解释。

### Gap B — BloggerRuntime 所有权描述

- `docs/shape/enforcer.md` 组件表仍写 `BloggerRuntime | 纯 cell 转移`；
- 0.5.4 生产代码和 convergence tests 已删除 `BloggerRuntimeState` / `BloggerRuntimeCell`，改成 `HasFlight`、`HasParked`、PendingOffer、DrainWindow 等物理轴。

应把 shape 更新成物理所有权，而不是为了迁就旧文档把 cell 重新引入。

这两项是**当前基线文档债**，不是旧 `15` 想恢复的产品功能。

---

## 13. Rebased verification contract

不再验证旧 score/throttle 数学性质。当前应该证明的是：

### 13.1 Catalog / codec

- packaged catalog 能加载；缺失/非法 fail fast；
- 当前 120 个 field 与 tool enum 一致；
- RuleId / FieldName 唯一；ordinal 连续；
- missing tip 失败；
- unknown tip 失败；
- exact field → exact RuleId；
- extra numeric properties 不复活 score path；
- `Scores` / `parseScore` surface 不存在。

### 13.2 Cycle

- text 按 PartOrdinal 稳定合并；
- canonical tip 的选择只有一个确定规则；
- evidence exact-dedup；
- multi-call 仍提交单 cycle 并留下 protocol violation diagnostic；
- duplicate ToolCallId / identity 不成立时拒绝；
- size / count bounds fail closed。

### 13.3 Atomic commit / recovery

- 一个 normal cycle 恰好追加一个 frame；
- frame 与 coverage 同事实推进；
- 每个成功 normal cycle 恰好记录一个 tip；
- duplicate ProviderRun 不产生第二条；
- crash window 能从 Materialized context + Host snapshot reconcile；
- CommitUnknown 不盲重试模型。

### 13.4 RecentTips / projection

- RecentTips oldest → newest；
- 上限 8；
- squash 不清空；
- restart / recovery / compaction rebuild 保持同一投影规则；
- tip message role = assistant；
- `previous_enforcer_tip` 是 `[[do_not_exec]]` 低信任历史；
- final user delta / squash instruction 仍保持 HOST binding 所需的最后位置。

### 13.5 Runtime ownership

- `HasFlight` 是唯一 busy 定义；
- busy skip 不推进 coverage；
- parked + no flight → Offer；
- idle physical facts → Start；
- CurrentRequest 与 PendingOffer 是独立槽；
- 无 `BloggerRuntimeState` / `BloggerRuntimeCell` 生产引用；
- session teardown / seal 通过物理 registry 与 drain 清理，不写 `Disposed` 程序状态。

### 13.6 Repair / fallback

- pure prose / invalid protocol 只有一次 InteractionRepair opportunity；
- same terminal replay 不重复 nudge；
- new invalid terminal 才证明 repair 失败；
- tool execution failure / repair exhaustion 进入统一 Fallback；
- recovery marker 不退化成整数 counter。

---

## 14. 建议的正式落点

如果要把本 rebase 真正合回当前仓库，不应重新创建一个巨型 `SSOT/15`。正确落点是按当前治理拆分：

```text
docs/what/enforcer.md
  可观察合同：single tip、cycle、RecentTips、无 score path

docs/shape/enforcer.md
  physical ownership：flight / parked / pending / drain / recovery evidence

docs/how/enforcer.md
  exact decode、PartOrdinal merge、repair/fallback、projection rebuild

docs/why/enforcer.md
  为什么 tip 取代 score-vector，为什么不向 Main 建第二控制环

docs/proof/enforcer.md
  catalog / codec / cycle / runtime ownership / crash recovery 证明

resources/enforcer/catalog.json
  规则数据唯一来源

resources/prompts/blogger-system.md
  Blogger 行为 prompt 唯一来源
```

若这次工作要以 Change 形式保存，则 Change 文件只记录：Current baseline、Proposed delta / rebase scope、impact、compatibility、proof plan；不要在 Change 中重新定义正式 `ENFORCER-*` Clause。

---

## 15. Compatibility / cutover

相对于旧 `15`，这是 **Clean Break**，不是兼容升级：

- 不读旧 ScoreVector；
- 不迁移旧 throttle accumulator；
- 不迁移 NudgeAnchored / NudgeConsumed；
- 不提供 fuzzy score-field alias；
- 不提供旧 Main overlay renderer；
- 不保留旧 runtime state DU 兼容层。

相对于当前 0.5.4 runtime，本 rebase 本身主要是**语义对齐与文档重构**，不应借机改变已存在的 wire/journal 行为。真正需要修改当前生产语义的部分，必须单独列成新 delta，而不是伪装成“rebase 修文档”。

---

## 16. 最终语义

> Blogger 是一个与 Main 解耦的 Companion work-log process。它从 durable coverage 与 typed context 观察 Main 的新工作，每个有效 normal provider cycle 通过 `blog` 提交一条稠密工作日志，并从 catalog 中选择恰好一个最有价值的工程 tip。Host 对 cycle 做确定性归并，把 frame、coverage、provider/tool identity 与 TipRuleId 原子写入 `BlogEntryCommitted`。最近 tip 以有界、低信任历史回投给未来 Blogger，帮助减少无价值重复，但不建立数值 throttle，也不向 Main 注入 engineering fake-user message。运行时协调只从真实 flight、parked waiter、pending offer、durable seal 与 drain 等物理事实派生；崩溃恢复只依赖 durable facts 与可证明的 Host snapshot，不从内存状态或物理 Blogger transcript 猜测业务事实。

这才是旧 `15` 的产品意图在当前架构中的最小、可恢复、可证明映射。

---

## 17. Rebase evidence index

本稿映射时重点读取的当前路径：

- `docs/what/enforcer.md`
- `docs/shape/enforcer.md`
- `docs/how/enforcer.md`
- `docs/why/enforcer.md`
- `docs/proof/enforcer.md`
- `docs/what/document-governance.md`
- `changes/README.md`
- `resources/enforcer/catalog.json`
- `resources/prompts/blogger-system.md`
- `src/Wanxiangshu/Domain/EnforcerCatalog.fs`
- `src/Wanxiangshu/Domain/EnforcerCodec.fs`
- `src/Wanxiangshu/Domain/EnforcerCycle.fs`
- `src/Wanxiangshu/Domain/EnforcerThrottle.fs`
- `src/Wanxiangshu/Domain/EnforcerNudge.fs`
- `src/Wanxiangshu/Domain/CompanionProjectionBuilder.fs`
- `src/Wanxiangshu/Journal/EnforcementProjection.fs`
- `src/Wanxiangshu/Session/BloggerRuntimeState.fs`（文件名保留，但 State DU 已删除）
- `src/Wanxiangshu/Session/BloggerRuntimeHost.fs`
- `src/Wanxiangshu/Session/BloggerCoordinator.fs`
- `src/Wanxiangshu/Session/EnforcerHost.fs`
- `src/Wanxiangshu/Infrastructure/OpenCode/Tools/BlogTool.fs`
- `tests/unit/enforcer/*`
- `tests/unit/verify/dsl-ownership*.test.mjs`
- `CHANGELOG.md`

---

## Active work

**Started**: 2026-08-07  
**Approved scope**: Per §0–§16 of this frozen Proposal. Semantic alignment + documentation reconstruction on baseline 0.5.4 + Unreleased. Do not redefine formal `ENFORCER-*` clauses inside this Change file. Do not revive score / throttle / Main overlay / Runtime State DU. Do not change wire/journal production semantics under the guise of documentation rebase.

**Remaining close conditions**:
1. [x] DONE — Fix Gap A — `docs/how/enforcer.md` ENFORCER-025 multi-call tip selection: document PartOrdinal-first only (match `EnforcerCycle.fs` + unit tests); remove catalog-ordinal secondary sort.
2. [x] DONE — Fix Gap B — `docs/shape/enforcer.md`: replace `BloggerRuntime | 纯 cell 转移` with physical ownership axes (`HasFlight` / `HasParked` / `PendingOffer` / `DrainWindow` / recovery evidence) and keep component responsibilities accurate.
3. [x] DONE — Strengthen `docs/proof/enforcer.md` so §13 verification contract is explicit (catalog/codec, cycle PartOrdinal-first, atomic commit/recovery, RecentTips bound 8, runtime ownership, repair/fallback, no score path).
4. [x] DONE — Confirm `docs/what` / `docs/why` need no residual score/throttle/Main-overlay fixes (already clean if still true).
5. [x] DONE — `npm run lint` passes after doc changes; run relevant `tests/unit/enforcer/*` if proof references require it.
6. [x] DONE — Append `Final outcome` and move this file to `changes/completed/`.

**Blockers**: None. Gaps are documentation; implementation already PartOrdinal-first and physical-ownership.

**Out of scope**: Restoring score-vector, throttle math, NudgeAnchored/NudgeConsumed, Main fake-user engineering overlay, or BloggerRuntimeState DU.

---

## Final outcome

**Completed**: 2026-08-07

**Outcome**: Fully closed. Rebase intent from this Proposal is reflected in the formal five-layer docs and verified against the existing 0.5.4 implementation. No wire/journal production semantics were changed under this Change. Old score/throttle/Main-overlay/Runtime State DU mechanisms were not restored.

**Docs delivered**:
- `docs/how/enforcer.md` — ENFORCER-025 multi-call tip selection is PartOrdinal-first only (Gap A closed).
- `docs/shape/enforcer.md` — physical ownership axes (`HasFlight` / `HasParked` / `PendingOffer` / `DrainWindow` / recovery evidence); no pure cell-transfer program counter (Gap B closed).
- `docs/proof/enforcer.md` — rebased §13 verification contract with real test/module evidence (catalog/codec, cycle, atomic commit/recovery, RecentTips=8, runtime ownership, repair/fallback, tombstones).
- `docs/what/enforcer.md` / `docs/why/enforcer.md` — confirmed already aligned; no residual score/throttle/Main-overlay product semantics requiring edit.

**Validation**:
- `npm run lint` — pass (exit 0)
- `npm run build` — pass (exit 0; required for unit tests via dist)
- `node --test tests/unit/enforcer/*.test.mjs` — 143/143 pass (exit 0), including PartOrdinal-first multi-call tip, RecentTips cap 8, physical HasFlight busy, throttle/nudge tombstones, catalog 120 rules, and `bounds.test.mjs` 4 pass locking the §13.2 size/count fail-closed bounds (`MaxMergedToolCalls=32` / text 512KiB / evidence 128KiB).

**Commits**:
- `dec23fcb` — docs close `how` / `shape` / `proof` (Gap A PartOrdinal-first, Gap B physical ownership axes, §13 proof inventory).
- `fa9c8f07` — `CHANGELOG.md` Unreleased entry documenting the Enforcer rebase docs close.
- `646140ac` — `bounds.test.mjs` permanent regression + `docs/proof/enforcer.md` §13.2 bounds rows + CHANGELOG/lifecycle test-count sync to 143/143.

**Tests**:
- lint — pass
- build — pass
- enforcer unit suite — 143/143 pass
- bounds — 4/4 pass

**Limitations**:
- Full monorepo integration/e2e suites were not re-run under this Change (out of scope for documentation rebase).
- Implementation was already tip-v2 / physical-ownership; this Change closed documentation drift and proof inventory, not a new runtime feature.

**Close conditions**: All Active work close conditions 1–6 satisfied.

**Docs**: Added `CHANGELOG.md` Unreleased entry documenting the Enforcer rebase docs close (tip-v2 baseline alignment, physical ownership axes, §13 proof inventory, lifecycle record in `changes/completed/enforcer.md`).

