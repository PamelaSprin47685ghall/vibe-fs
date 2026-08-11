# Predict & Reduce Strength — Current-Repo Rebase Proposal

> 本文件位于 `changes/proposed/` 时只是已批准变更的 Proposal 原文，不是当前产品规范。
> 实施时，目标语义必须分别进入 `docs/{why,what,shape,how,proof}`；本文件不定义正式 Clause ID。
> 本次 rebase 以当前仓库已经落地的 Universal SessionOwnership（ExecutionClass × Ownership）、EventStore/`payload_refs`、PromptAuthority、Projection Algebra、XTrace、Companion、Fallback 与结构化程序边界为基线。
> **Storage / Ownership 收口（相对旧稿强制）**：`FrameBundleRef` / `PredictorSnapshotRef` / candidate material 一律改为 EventStore envelope 的 opaque `PayloadRef`（`payload_refs`）；删除 Journal NDJSON 文件与 `RuntimePath` blob/`blobs/<sha256>` 假定；禁止再向 `SatelliteKind` 塞 `Replica`——Strength Replica 归 Universal `InternalLeaf × Attached(..., StrengthReplica)`；全文不再以 Student/Teacher 为产品边界。

## 一、这次 rebase 的结论

旧版 Strength 的**问题判断仍然成立**，但大量实现机制已经过时。新版保留以下核心命题：

1. 昂贵主模型的工作流中，确实存在大量“下一次 provider 请求大概率只是机械性只读调查”的局部窗口。
2. 如果能让较便宜模型提前完成 1–2 个这样的请求，并把**真实工具调用与真实工具结果**交给主模型，就有机会减少昂贵 provider 请求。
3. 旁路投机最大的风险不是“多花了一次便宜模型调用”，而是错误调查结果改变主模型后续推理；所以深度必须有限、工具必须只读、失败必须可丢弃。
4. 投机结果在主模型真正消费之前不能成为活动历史；主模型消费之后又必须成为可恢复、可压缩、可重放的正式语义历史。
5. 主模型和便宜模型都不应知道 Strength 机制本身存在；控制、预算、来源和置信度属于 Host 内部事实。
6. 预测器追求的是长期稳定的正收益工作点，而不是宣称知道“没有 Strength 时主模型反事实上一定会做什么”。

但旧稿以下设计全部撤销或重写：

| 旧设计                                                 | 当前 rebase 裁决                                                                            |
| --------------------------------------------------- | --------------------------------------------------------------------------------------- |
| `SSOT/14` 与 `STRENGTH-*` 正式条款                       | 删除。Change 不定义正式 Clause；实施时拆入正式 docs                                                     |
| 新增 `fast-replica/deep-replica` Agent / Replica 特殊身份 | 删除。Replica 复用当前角色的 `fast-ROLE`，不新增 Agent、不新增 CanonicalRole                              |
| Replica 自己一套 Agent→Role 例外映射                        | 删除。所有 attempt 仍只走 `AttemptExecutionProfile` 单一构造链                                       |
| `ExecutionSurface = StrengthReadOnlySurface`        | 删除。工具面用当前 `ProviderRequestKind × CanonicalRole → ToolCapabilitySet` 表达                  |
| 另造 Satellite 生命周期/关联框架                              | 删除。不向 `SatelliteKind` 塞 `Replica`；归 Universal `SessionExecutionClass × SessionOwnership` / `AttachmentKind.StrengthReplica` |
| Replica 自有 Companion                                | 删除。Replica 是叶子 Satellite，不递归拥有 Companion                                                |
| 自造 `SemanticEventCursor` / 全局语义事件体系                 | 删除。跨 Session 语义使用 `ProviderSemanticProjection`；持久语义顺序复用 `XTraceCursor`                  |
| 先完成 Projection DSL 大迁移才能做 Strength                  | 删除。Projection Algebra 已经落地；Strength 只增自己需要的 intent                                      |
| `ParkedTransform` 长时间挂起等待下一次复用                      | 删除。每个 Strength 决策使用短生命周期 Replica，完成即 retire                                             |
| Replica 训练流按概率回灌 + `StrengthController` 负反馈         | 删除。投机是 intervention，不拿 intervention 当 counterfactual label；改用确定性 control/shadow holdout |
| `Status = Candidate/Promoted/...` 一类状态字段            | 删除。只记录已经发生的事实，由 projection 推导当前状态                                                       |

这次 rebase 的核心不是“把旧类名换成新类名”，而是让 Strength 成为当前架构上的一个薄功能：

```text
现有 Work Session / Universal Attached ownership
+ 现有 PromptAuthority / AttemptExecutionProfile
+ 现有 request-specific ToolCapabilitySet
+ 现有 Projection Algebra
+ 现有 XTrace / Companion durable semantic history
+ 统一 EventStore（Strength events + payload_refs）
+ 一个 Strength-specific pure policy + workflow
```

而不是再建立第二套 runtime。

---

# 二、背景：真正要优化的是什么

LLM coding / investigation workflow 中经常出现：

```text
grep
→ read
→ read
→ edit
```

或：

```text
glob
→ 并发 read 若干候选
```

或：

```text
read 索引 / manifest
→ read 其中明确引用的文件
```

重要的不是工具名叫 `read`，而是某个时刻满足：

> **从当前语义历史出发，下一次 provider request 很可能只需要选择并发起低风险、无副作用、结果可直接交给主模型继续使用的调查动作。**

今天这些动作仍由昂贵主模型生成。Strength 的收益来源是：

```text
ExpectedValue(K)
=
预期省去的 deep provider request 成本
- fast Replica provider request 成本
- Replica 工具执行与 Host 编排成本
- 新增主模型输入字节成本
- 主请求被阻塞的延迟成本
- 错误调查方向造成的 steering risk
```

这里 `K ∈ {0,1,2}`，且 K 的单位始终是 **provider request 数**，不是 tool call 数。

一个 provider request 可以并发产生多个 `read/glob/grep`；这些调用作为一个 batch 接收。这样预算衡量的是“替代了多少次模型决策”，而不是“模型在一次决策中并发点了几个工具”。

---

# 三、第一性原理

## 1. 投机必须具有“错误时只损失成本”的形状

如果旁路允许写文件、执行命令、修改 Git、创建 fork、提交 verdict 或访问网络，那么一次错误预测会产生真实世界副作用，无法通过“主模型忽略它”恢复。

因此第一版 Strength 的安全集合严格是：

```text
read
glob
grep
```

后续若扩大集合，必须逐个证明：

```text
副作用 = 0
权限交互 = 0
结果可稳定重放
错误方向的损失有界
```

“看起来只读”不是充分条件。

## 2. 投机结果不是历史；被消费以后才是历史

Strength 必须区分两个世界：

```text
Candidate
    Replica 已经做了调查
    但主 provider 尚未被证明消费

Promoted
    某个明确 ProviderRun 已经在输入中看到了 Candidate
    且随后产生了可归因于该 run 的 provider output
```

Candidate 可以消失，不能进入 Companion、LWR 或未来请求。

Promoted 不能消失。它已经影响主模型，若重启后不再出现，就是把真实因果历史从上下文里删除。

所以不是“先写进去，失败再 rollback”，而是：

```text
准备候选
→ 只对目标 attempt 可见
→ 观察消费证据
→ promotion
```

**没有 rollback。**

## 3. 身份、权限和投影必须各有唯一 owner

Strength 不得再拥有：

```text
ReplicaAgentRoleMapper
ExecutionSurface
StrengthPromptAuthority
StrengthProjectionDsl
StrengthSessionRegistry
StrengthFallback
```

这些概念当前 repo 已经分别有 owner。

Strength 只能向它们增加一个合法 case：

```text
AttachmentKind.StrengthReplica
  （InternalLeaf × Attached；不是 SatelliteKind case）
ProviderRequestKind.StrengthReplica
ProjectionIntent 的 Strength intent
Strength EventStore events / projection
  （大 material 仅经 envelope payload_refs）
```

所有公共不变量继续由原 owner 证明。

## 4. 低成本路径必须具有更低 authority，而不是更弱的文字提醒

不能通过 system prompt 告诉便宜模型：

```text
“请只读”
“不要写”
“最多做两步”
“你只是预读模型”
```

真正的约束来自：

```text
Provider-visible tool schema 只出现 read/glob/grep
+
execution gate 使用同一个 ToolCapabilitySet fail closed
+
Host 在第 K 个 provider request 后物理停止 Replica
```

模型不负责遵守 Strength 的预算；Host 负责。

## 5. 机制不可见，但事实必须可审计

主模型 provider-visible 历史中不得出现：

```text
strength
replica
prefetch
weak model
confidence
budget
prediction
source=sidecar
```

Replica 也不接收“你正在帮另一个模型预读”之类提示。

但 EventStore events / diagnostics 必须保留：

```text
DecisionId
ReplicaSessionId
TargetProviderRun
Candidate digest
Promoted evidence
K
predictor features / score
cost estimate
failure reason
```

**模型不可见 ≠ 系统不可审计。**

## 6. intervention 不能冒充 observation

这是本次 rebase 对旧算法最大的修正。

如果 Strength 预测“下一步会 read”，然后自己先 read，再把这次 Replica read 当作训练数据，预测器会把自己的行为当成世界的证据，形成自我强化闭环。

旧稿用“训练纳入概率 + 负反馈 controller”缓解这个问题；当前版本不再这么做。

新的原则是：

> **Replica 产生的数据是 intervention data，不是“主模型本来会怎么做”的 label。**

反事实训练样本只来自 shadow/control opportunity。

---

# 四、当前 repo 基线

Strength 的实现必须建立在以下已经存在的结构上。

## 1. Session ownership 已归 Universal（禁止再扩 SatelliteKind）

当前正式模型已经是正交二维（HOST-008 / Universal）：

```fsharp
type SessionExecutionClass =
    | Work
    | InternalLeaf

type SessionOwnership =
    | Root
    | Attached of ownerSessionId: SessionId * attachment: AttachmentKind

type AttachmentKind =
    | Companion
    | SyncInspector
    | SyncCoder
    | Bookkeeper of transactionId: string
    // Strength 只增加下一 case；不回退到 SatelliteKind.Replica
    | StrengthReplica
```

`SatelliteKind` 现仅保留 Companion leaf 投影兼容面；**不得**再塞 `Replica` / `Teacher` / SyncDelegate。Dedicated SyncInspector/SyncCoder 是 Work+Attached；Companion/Bookkeeper/StrengthReplica 是 InternalLeaf+Attached。

Strength Replica 的分类固定为：

```text
SessionExecutionClass.InternalLeaf
× SessionOwnership.Attached(ownerWorkSessionId, AttachmentKind.StrengthReplica)
```

物理 ensure / abort / retire 归现有 Attached/leaf runtime 路径（可有 `StrengthRuntime` 做 decision-local batch 收集），**不**创建 `StrengthSatelliteRuntime`，**不**扩展 `SatelliteRuntime` 的 kind switch 充当第二套 ownership。

第一版仍保持：

```text
每个 owner Work Session 至多一个 active StrengthReplica attachment
StrengthReplica 是 InternalLeaf：无 Companion、无 SyncDelegate 子会话、无嵌套 StrengthReplica
owner 删除/取消 → 级联停止 Replica
短生命周期：按 StrengthDecision 使用，完成后 retire；不跨 decision 复用 transcript
```

关联投影可以最小增加 `StrengthReplicaSessionId` / `tryStrengthReplicaOf`（按 owner × AttachmentKind 索引）。不要为了 Strength 把 Universal ownership 记录重写成泛型图；也不要复活 Student/Teacher 身份来解释 leaf。

## 2. 不新增 Replica Agent

当前 Agent 体系已经固定为每个 CanonicalRole 一对：

```text
fast-ROLE
deep-ROLE
```

Tier 只决定模型绑定；同 Role 的 fast/deep 共享 system prompt、工具语义和能力矩阵。

因此 Replica 的身份直接是：

```text
Primary = deep-coder      → Replica = fast-coder
Primary = deep-inspector  → Replica = fast-inspector
Primary = deep-devops     → Replica = fast-devops
Primary = deep-meditator  → Replica = fast-meditator
```

不增加：

```text
fast-replica
deep-replica
Role.Replica
Replica-specific system prompt
```

这有三个好处：

1. 不破坏“Agent identity → CanonicalRole”的单一映射。
2. 不需要 Replica 成为 PromptAuthority 的特殊例外。
3. 便宜模型天然拿到与主模型同角色的思考方式，而工具能力可以在 request kind 上收窄。

第一版只有满足以下条件才允许实际投机：

```text
Authority.SelectedTier = Deep
EffectiveAgent = SelectedAgent        // 不在 fallback B-side / recovery 中
fast peer 存在
fast/deep model binding 不同
Host 有足够证据认为 fast 请求具有成本优势
```

如果当前主 attempt 已经是 fast，或成本关系未知：

```text
K = 0
```

不要因为名字叫 Fast 就在经济模型里硬编码“它一定更便宜”。

## 3. 请求级权限已经有正式表达

当前 `AttemptExecutionProfile` 已原子冻结：

```text
Authority
PhysicalUserMessageId
ProviderRun
Origin
EffectiveAgent
SystemPromptId
ToolCapabilitySet
RequestKind
ProjectionChoice
```

而现有 request-specific 路径（例如 SyncInspector / Bookkeeper / StrengthReplica）已证明：同一个 CanonicalRole 可以因为真实请求语义不同而拥有不同的 provider-visible 工具集。Student/Teacher 产品面已由 Universal clean break 删除，不得再作为 Strength 权限样板。

Strength 应直接增加：

```fsharp
type ProviderRequestKind =
    | ...
    | StrengthReplica
```

并在现有 `PromptAuthority.toolCapabilitiesFor` 中定义：

```fsharp
eligible role × StrengthReplica
    → { Read; Glob; Grep }

其他 role × StrengthReplica
    → ∅
```

同时：

```text
mayCarryProbe(StrengthReplica) = false
clearsFailureCountOnSuccess(StrengthReplica) = false
```

StrengthReplica 的成功或失败都不属于 owner Work Session 的 fallback 证据。

## 4. Projection Algebra 已经落地

旧稿把 Projection Algebra 当作 Strength 的前置大迁移；当前 repo 已经有：

```text
Effectful Coordinator
→ Pure Projection Planner
→ Canonical Renderer
```

并规定业务模块只能声明 `ProjectionIntent`，不能直接改 `Message list`。

因此 Strength 只需要扩展代数，不再建设自己的 DSL。

## 5. 跨 Session 语义已经有 canonical 形式

当前 repo 已明确区分：

```text
ProviderSemanticProjection   // 去 ID、跨会话语义等价
ProviderWireProjection       // 有 ID、字节/seal/local timeline
```

所以 Replica 镜像不需要旧稿的自造 `SemanticEventCursor`。

注意：Replica 镜像只复用 **messages semantic history**，不复制 owner 的：

```text
provider id
model id
tool schema
system prompt binding
```

Replica 的 system/tools/model 仍来自它自己的 `AttemptExecutionProfile`。

## 6. XTrace 已经是持久语义顺序

当前 `XTraceCursor.Sequence` 已独立于 Host 临时 turn/part 编号，并且 compaction 不会重置。

Strength promotion 后的工具调用/结果必须最终进入这个 durable semantic timeline，而不是再建立 `StrengthSemanticLog`。

---

# 五、目标拓扑

记：

```text
X      主 Work Session
Y_X    X 的 Companion Satellite
Z_D    某次 Strength Decision 的 StrengthReplica（InternalLeaf+Attached）
D      StrengthDecisionId
```

第一版 Z 是**短生命周期、按 decision 使用**：

```text
eligible primary attempt
→ Ensure StrengthReplica attachment
→ 运行最多 K 个 provider request
→ 得到 0..K 个完整只读 batch
→ candidate 已物化或决定 K0
→ retire Replica
```

不跨 Strength decision 复用 Replica transcript。

理由：

1. 避免旧决策本地 tool history 污染新决策。
2. 不需要 parked transform。
3. 不需要“同 Authority Root 才可恢复”的隐藏生命周期规则。
4. restart 的恢复对象是 durable Candidate/Promotion，而不是一段挂起的 provider continuation。

如果未来数据证明 Session 创建成本显著，可以另开变更研究 safe reuse；第一版不拿复杂性换未经证明的收益。

---

# 六、Strength Decision

Domain 只需要纯 Evidence → Decision。

建议形态：

```fsharp
type StrengthBudget = K0 | K1 | K2

type StrengthEligibility =
    | Ineligible of reason: string
    | Eligible of StrengthOpportunity

type StrengthDecision =
    | Skip of reason: string
    | ControlHoldout
    | Speculate of budget: StrengthBudget * estimate: StrengthValueEstimate
```

`StrengthOpportunity` 只包含已经冻结的事实，例如：

```text
OwnerSessionId
AuthorityRoot
TargetProviderRun
CanonicalRole
SelectedAgent / EffectiveAgent
semantic history suffix
current provider-visible byte estimate
role/model cost metadata
recovery/finality/satellite facts
```

不得包含：

```text
CurrentStage
NextStep
ReplicaPhase
WaitingForPromotion
```

Application workflow 直接：

```text
read evidence
→ decide
→ match
→ typed ports
```

---

# 七、第一版 Eligible 边界

只有同时满足以下条件才进入预测：

```text
session = Work + Root（普通主工作会话）
request kind = WorkMain
role ∈ { Coder, Inspector, DevOps, Meditator }
selected tier = Deep
current effective agent = selected deep agent
不是 fallback retry
不是 InteractionRepair
不是 PrefixProbe attempt
不是 Reviewer / finality verification
不是任何 Attached / InternalLeaf session（含 Companion、SyncDelegate、Bookkeeper、StrengthReplica）
没有 owner cancellation / abort
可以唯一绑定 TargetProviderRun
EventStore 可用且健康（唯一 dynamic durability；非 Journal NDJSON / RuntimePath blob）
Replica fast peer 可解析
成本模型可用
```

任何一项不确定：

```text
K0
```

Browser 第一版不纳入。它的高价值调查往往涉及网络工具，而网络不是第一版 Strength 的可投机副作用边界。

Manager / Orchestrator 没有这种普通只读工作面；Reviewer 的因果 seal 不允许被投机内容干扰。Student/Teacher 已删除，不再出现在 eligible 否定清单里——它们不是“排除项”，而是不存在的产品面。

---

# 八、两段式 Main Transform，而不是一个巨型 hook

Strength 在主会话需要两个不同时间点的职责。

## 1. StrengthReplay：早期重放已 Promoted 帧

目的：让过去已经影响过主模型的 Strength 工具交换重新成为当前语义历史的一部分，并让 XTrace/Companion 可以看到它们。

顺序：

```text
StrengthReplay
→ XTraceCapture
→ Companion
→ XWire
→ EnforcerHost
→ StrengthSpeculate
→ PairProgrammingThoughtTransform
→ ReviewSeal
```

`StrengthReplay` 只处理 durable Promoted frames；**绝不重放 Candidate**。

每组 frame 以稳定 anchor 重建在它原本因果位置：

```text
... prior history
→ promoted Strength tool batch(es)
→ TargetProviderRun assistant output
```

最自然的 anchor 是 `TargetProviderRun` 对应的 physical assistant message：Promoted frames 插在该 assistant 之前。

这样下一次 transform 的 `XTraceCapture` 首次看到：

```text
Strength tool call/result
→ target assistant output
```

顺序与主模型真实消费关系一致。

## 2. StrengthSpeculate：晚期为当前 TargetProviderRun 准备 Candidate

它必须在当前 Work projection 已经完成 Companion/XWire/Enforcer 处理之后运行，以便 Replica 看到主模型真正接近发送时的语义历史。

但它必须在 PairProgrammingThought 之前完成 Candidate 插入；否则新插入的 tool-result anchor 会绕过 HOST-013 marker 不变量。

因此冻结镜像点是：

```text
post-Enforcer
pre-Strength-candidate
pre-Pair-marker
```

Replica projection 和最终 main projection 都再经过现有 Pair intent。

`ReviewSeal` 仍然最后；Strength 不改变“seal 覆盖 provider 最终 bytes”的边界。

---

# 九、Replica provider view

Replica 有真实的 Host Session 与 AgentOwnerRoot，这用于：

```text
Prompt claim / submit / PhysicalAccepted
Agent identity
provider run identity
Host cancellation
recovery ownership
```

但 bootstrap transport text 不是 Replica 的业务上下文。

不要依赖当前尚未生产接线的 `SuppressTransportOnly` 去删它；Strength 应明确增加一个 Replica base projection intent，例如：

```fsharp
| UseStrengthMirror of StrengthMirror
```

其语义是：

```text
Replica 的 provider-visible message base
= owner 在冻结点的 SemanticMessage list
+ Replica 本 decision 已完成的本地 tool batches
```

而不是 Replica 的物理 bootstrap transcript。

这个 intent 只对：

```text
AttachmentKind.StrengthReplica
  （InternalLeaf × Attached）
+
ProviderRequestKind.StrengthReplica
```

合法；其它组合 fail closed。不得用旧 `SatelliteKind.Replica` 作为合法判别。

它与普通 Work Session 的：

```text
KeepPhysicalPrefix
ActivatePrefixEpoch
```

是互斥 base selection，不允许同时出现。

之后可复用现有：

```text
InsertPairProgrammingThought
```

让 Replica 与主模型得到同一角色语境中的 marker 行为。

Replica 不接收任何 Strength-specific instruction。

---

# 十、Replica 预算执行

预算永远由 Host 计数 provider request。

## K1

```text
request #1
→ 若产生 allowed tool calls：执行全部并等待结果
→ 下一次 Replica transform 到来
→ 收割完整 batch #1
→ 阻止 request #2
→ retire Z
```

## K2

```text
request #1
→ tools/results complete
→ request #2
→ tools/results complete
→ 下一次 transform
→ 收割 batch #2
→ 阻止 request #3
→ retire Z
```

## 自然 text-out

如果 Replica 在预算内直接输出正文而不再调用工具：

```text
正文丢弃
此前完整 tool batch 保留
停止 Replica
```

例如 K2：

```text
#1 = tool batch
#2 = prose completion
```

最终只返回 batch #1。

如果 #1 就 prose completion：

```text
0 frame
→ main 正常继续
```

## 并发调用

一个 provider request 可以：

```text
read A
read B
grep C
```

只要全部来自 allowed set，并且所有结果都有完整配对，它们共同构成一个 request batch，预算只记 1。

Canonical batch 顺序必须使用 Host/provider 已有稳定顺序规则，不按 tool 完成时间重排。

## 停止方式

不再使用旧稿的 `ParkedTransform`。

预算到达后，在 Replica 的下一 transform / reconcile 边界：

```text
标记本 decision 已取得最终可用 batch
→ Abort/Retire Replica physical session
→ 不允许 K+1 provider request 外发
```

Host canary 必须证明这种“transform 内停止 satellite request loop”的真实行为；证明不了就不启用 Strength。

---

# 十一、Candidate Frame 的 canonical 形状

Strength 不复制 Replica prose/reasoning，只物化真实工具交换。

建议 Domain 形状：

```fsharp
type StrengthToolExchange =
    { ToolName: string
      CanonicalArguments: string
      CanonicalResult: string }

type StrengthRequestBatch =
    { RequestOrdinal: int
      Exchanges: NonEmptyList<StrengthToolExchange> }

type StrengthFrameBundle =
    { DecisionId: StrengthDecisionId
      Batches: NonEmptyList<StrengthRequestBatch>
      SemanticDigest: ContentDigest  // 内容摘要；不是 RuntimePath BlobDigest / blobs/<sha256>
      ByteLength: int64 }
```

要求：

```text
1. CanonicalArguments 使用现有 canonical JSON 规则。
2. tool result 必须来自真实 Host tool result，不允许模型自行摘要替代。
3. call/result 必须一一配对。
4. 只允许 read/glob/grep。
5. 完整保留 request batch 边界。
6. 总 bytes 有硬上限；超限则 K0 / 丢弃本次 speculation。
```

跨 Session 时不复制 Replica 的 tool call ID。

Semantic bundle 去 ID；注入 owner wire 时生成 deterministic synthetic ToolCallId：

```text
hash(owner session
     + decision id
     + request ordinal
     + exchange ordinal
     + canonical semantic digest)
```

禁止 GUID、随机数和时间戳。

---

# 十二、Candidate → Promotion

## 1. Candidate Prepared 是 durable fact

Replica 返回可用 bundle 后，主请求**在真正看到它之前**先持久化：

```fsharp
StrengthCandidatePrepared
    OwnerSessionId
    DecisionId
    TargetProviderRun
    StrengthReplicaSessionId
    Budget
    AnchorDigest
    FrameBundleDigest
    FrameByteLength
    // 大 material 不进 inline journal/blob path：
    // EventEnvelope.PayloadRefs : PayloadRef list
    //   - frame bundle payload（原 FrameBundleRef）
    //   - predictor snapshot payload（原 PredictorSnapshotRef；可选）
    // Domain 只见 opaque PayloadRef；Persist 映射到 Git OID / payloads/ tree
```

Event 表示：

> “这些候选 bytes 已经作为 EventStore payload 准备好，并被绑定给这个明确的 TargetProviderRun。”

它不表示 provider 已经消费。

**禁止**再引入：

```text
FrameBundleRef / PredictorSnapshotRef 作为独立 storage 类型
RuntimePath → .../runtimes/<RuntimeId>.ndjson
RuntimePath → .../blobs/<sha256>
feature-owned Strength blob directory
```

Candidate material 与 predictor snapshot 一律经统一 EventStore：`append StrengthCandidatePrepared` 时把大正文写入 raw payload，并在 envelope `payload_refs` 中引用；小字段可留在 canonical JSON payload 内（此时对应 refs 可为空集规则仍服从 Storage §7.1）。

Candidate 成功 commit 到 EventStore 后，`StrengthSpeculate` 才声明 `InsertStrengthFrames` intent。

如果普通 Replica 失败发生在此之前：

```text
fail open → 不插入 → main 正常请求
```

## 2. CommitUnknown 不能 fail-open

如果 Candidate append 的结果未知：

```text
不能猜“没写进去”然后继续 main
```

因为 EventStore 可能实际已 commit，而 provider input 没有 Candidate；之后若仅凭 TargetProviderRun output 自动 promotion，就会把模型从未见过的 frame 变成历史。

正确流程：

```text
append outcome unknown
→ 重读 EventStore Strength projection（按索引，不扫 NDJSON 文件）
→ candidate 明确存在：注入同一 bundle（同一 PayloadRef 集合 / digest）后继续
→ candidate 明确不存在：按 K0 继续
→ 仍无法证明：阻止当前 provider attempt 外发 / fail closed
```

## 3. Promotion 证据来自 ReconciledTurn

当前 repo 已经把一次 assistant message 绑定为一个 `ProviderRunIdentity`。

因此不需要再为 Strength 发明第二套 input seal。

在 reconcile 看到：

```text
turn.ProviderRun == Candidate.TargetProviderRun
+
该 run 存在真实 provider output
+
不是“请求尚未开始 / transport-only / 完全空失败”
```

即可证明这个 provider run 是 Candidate 的消费者。

然后写：

```fsharp
StrengthCandidatePromoted
    OwnerSessionId
    DecisionId
    ConsumingProviderRun
    FrameBundleDigest
    // 可再次列出同一 frame bundle PayloadRef；禁止另写 RuntimePath blob 副本
```

如果 run 在产生可用 provider output 前 Failed/Aborted：

```text
不 promotion
```

Candidate 只是孤立的准备事实，不进入活动历史。

## 4. promotion 必须先于下一 continuation

若 target run 已经消费 Candidate，系统在发出下一 WorkMain continuation 前必须完成 promotion。

否则会出现：

```text
模型上一请求看过 Strength frame
→ 下一请求重启/投影时 frame 消失
```

这违反 append-only semantic history。

Promotion append 若 CommitUnknown，必须在 reconciliation 内 resolve；无法证明时 fail closed，不得继续下一 provider request。

---

# 十三、Projection Algebra 扩展

建议只增加两个 intent：

```fsharp
| UseStrengthMirror of StrengthMirrorIntent
| InsertStrengthFrames of StrengthFramesIntent
```

## `UseStrengthMirror`

只用于 Replica request 的 base timeline 选择。

## `InsertStrengthFrames`

统一承担：

```text
Main 当前 Candidate 注入
Main 历史 Promoted replay
Replica 本 decision 的 prior completed batches
```

由载荷中的 visibility / anchor 明确三种语义，不靠 renderer 猜来源。

Canonical order 原则：

```text
base timeline / prefix selection
→ blog/context representation
→ repair
→ Strength frame insertion
→ review challenge（Strength 本身不在 Reviewer 启用）
→ pair-programming thought
→ reanchor lifecycle no-op
```

核心要求不是具体 rank 数字，而是：

1. Strength tool-result anchor 必须在 Pair marker 之前出现，让 Pair 仍能覆盖所有 anchor。
2. ReviewSeal 永远在所有 provider-visible mutation 之后。
3. 同 anchor 两个不同 Strength payload → `ProjectionConflict`，不能按注册顺序选一个。
4. 同 DecisionId + 同 digest 重放必须幂等。
5. Candidate 只能注入它绑定的 TargetProviderRun。
6. Promoted replay 不能注入到 Replica mirror 形成反射回路。

---

# 十四、Promoted Frame 如何进入 XTrace / Companion

这是 Strength 正确性的关键，不允许只解决“当前 request 看见了”。

当前 `XTraceCapture` 在其它 synthetic transform 之前捕获 X 的 durable semantic trajectory。Strength 因此采用：

```text
当前 request：Candidate 在 XTraceCapture 之后注入
             → Candidate 不进入 XTrace

消费成功后：Promotion durable

下一 request：StrengthReplay 在 XTraceCapture 之前重建 Promoted frame
             → XTraceCapture 看见它
             → Companion delta 可以消费它
```

这样天然实现：

```text
Candidate ∉ XTrace
Promoted ∈ XTrace
```

无需单独维护另一条语义日志。

## XTrace 的最小扩展

不要替换 `XTraceCursor`；它已经是正确的全生命周期单调坐标。

只需要让 capture 能把 synthetic Strength message 的稳定 identity 与当前 XTrace range 关联起来，例如增加一个真实观察事实：

```fsharp
StrengthFramesTraced
    OwnerSessionId
    DecisionId
    FirstXTraceSequence
    LastXTraceSequence
    HostGeneration
```

它表示：

> “这组已经 Promoted 的 frame 已实际进入 XTrace 的这段 cursor 范围。”

如果 crash 发生在 XTrace parts 已写、range fact 未写之间，下一次 capture 必须能从 deterministic Strength message identity + 当前 Host generation 重建这个 range，再幂等补 fact。

不要用 mutable map 作为唯一证据。

## 何时可以停止 raw replay

Promoted frame 在以下事实成立前必须继续能进入主投影：

```text
要么它仍作为 raw promoted Strength frame 重放；
要么现有 Companion / prefix representation 已经证明覆盖了它。
```

为了避免拿“物理 Host cutoff”冒充“语义 XTrace coverage”，Strength 应显式使用 `XTraceCursor` 证明覆盖。

最小实现可以在 StrengthProjection 中维护：

```text
DecisionId → traced XTrace range
```

并使用现有 `RecordCoverage.IngestedThroughSequence` / prefix 证明决定是否仍需 raw replay。

若现有 PrefixSnapshot 无法证明某次冻结 memory 覆盖到哪个 XTrace sequence，则为 PrefixSnapshot 增加一个**语义 coverage 证明字段**（例如 `CoveredXTraceThrough`），而不是用 sentinel turn index 猜测。

物理 cutoff 与语义 coverage 是两个不同问题：

```text
CutoffExclusive       // Host/provider message prefix 的物理边界
CoveredXTraceThrough  // Companion 已表达的 durable semantic history 边界
```

不要把二者压成一个 int。

---

# 十五、No Reflection

必须证明两条独立性质。

## 1. Replica 不读自己的旧 Strength 产物

每个 Decision 使用 fresh/retired Replica session；`UseStrengthMirror` 只采用 owner frozen mirror + 当前 decision 本地 batch。

因此不存在跨 decision 的 Replica transcript reflection。

## 2. Owner mirror 不把当前 Candidate 再喂回当前 Replica

冻结顺序固定：

```text
freeze owner mirror
→ run Replica
→ persist Candidate
→ insert Candidate into owner target request
```

Replica 输入在 Candidate 出现之前已经冻结。

历史 Promoted frame 可以作为 owner 正常语义历史出现在新的 decision mirror 中；这是**过去真实影响主模型的事实**，不是 reflection bug。

---

# 十六、Predictor：从旧闭环改成可识别的 control 数据

## 1. Shadow 阶段

上线前先只计算，不投机：

```text
所有 eligible opportunity
→ predictor 输出 K/score
→ 实际仍由 deep primary 正常执行
→ 记录主模型后续真实 request label
```

这给出无 intervention 的基线。

## 2. 启用后保留 deterministic control holdout

启用 Strength 后，固定一小部分 eligible opportunity 强制：

```text
K0
```

选择必须 restart-stable，例如：

```text
hash(AuthorityRoot + TargetProviderRun + policyVersion)
→ control bucket
```

不是运行时 RNG。

Control opportunity 上观察：

```text
R1 = 下一次 primary provider request 是否为非空、纯 allowed-readonly batch
R2 = 若 R1=true，再下一次 primary provider request 是否仍为纯 allowed-readonly batch
```

这些才是训练 `P1/P2` 的 label。

Replica 产生的请求永不直接进入 predictor target sequence。

## 3. 第一版模型保持简单

建议继续保留旧稿的 request-level n-gram 思路，但把它从“规范必须 Kneser-Ney”降为实现选择。

第一版足够：

```text
Role bucket
+ 最近 1..3 个 primary request symbol
+ 最近 tool-result 结构特征
+ 当前可见字节规模
→ P(next readonly)
→ P(second readonly | first readonly)
```

request symbol 示例：

```text
R = 纯 read
G = grep/glob 搜索
M = mutate
E = execute
T = text-only / terminal
O = other
```

训练状态按 CanonicalRole 分桶即可；不要按具体模型版本无限切碎数据。

## 4. K 的选择

```text
V0 = 0
V1 = P1 * SavedDeep1 - Fast1 - Byte1 - Delay1 - Risk1
V2 = P1 * SavedDeep1
   + P1*P2 * SavedDeep2
   - Fast1 - P1*Fast2
   - Byte2 - Delay2 - Risk2
```

选择：

```text
argmax(V0,V1,V2)
```

并要求：

```text
K1 必须超过正安全 margin
K2 必须比 K1 再多超过一个更高 margin
```

第二层 steering risk 高于第一层，所以 K2 阈值必须更保守。

## 5. 不声称反事实最优

即使有 control，也只能估计 aggregate treatment effect 与 request-level 命中率。

不应写：

```text
“这次 Strength 精确省了 1.73 次 deep request”
```

应写：

```text
“在 comparable control bucket 中，treatment 的 deep request / latency / review outcome 分布如何变化”
```

---

# 十七、成本模型

旧稿把很多常量集中进全局 `PolicyConstants.fs`；当前 repo 没有这个 owner，不应为了 Strength 发明一个全局策略垃圾桶。

Strength-specific 默认值放在：

```text
Domain/StrengthPolicy.fs
```

或同 bounded context 内的单一模块。

第一版至少需要：

```text
MaxProviderRequests = 2
MaxCandidateBytes
DecisionDeadline
EligibleRoles
AllowedTools
ControlHoldoutRate
K1Margin
K2Margin
PolicyVersion
```

模型成本不能凭 `Fast/Deep` 名字猜。

允许来源：

```text
已有 provider usage / price metadata
或显式的 Host-internal cost class
```

如果当前 repo 没有可靠成本数据：

```text
CostModelUnavailable → K0
```

Shadow 阶段仍可运行 predictor 观测，不实际投机。

---

# 十八、Fallback / Recovery 边界

Replica 有自己的物理 provider request，但**没有 Strength Fallback**。

```text
StrengthReplica Failed / Aborted / unusable
→ 本 decision 结束
→ 已完成且验证过的 batch 可按规则使用
→ 未完成 batch 丢弃
→ owner 的 FallbackCursor 不动
```

Replica 不从 fast 自动 fallback 到 deep；那会把本来为节省 deep request 的机制变成额外 deep request。

Owner 若处于：

```text
ProviderRetry
InteractionRepair
PrefixProbe
```

第一版直接 K0。

这样 Strength 不参与恢复因果链，也不改变 `ConsecutiveFailureCount`。

---

# 十九、Review / Finality 边界

第一版 Reviewer 不 eligible，Strength 也不得改变：

```text
review challenge
ProviderInputSeal
ReviewBarrier
ConfirmedReviewWitness
Finality cohort
```

`ReviewSeal` 继续最后执行。

Strength 在普通 Coder/Inspector/DevOps/Meditator 的工具结果里即使恰好包含 `PERFECT` / `REVISE` 字样，也只是普通 read result 数据，不具有 review authority。

不要从 Strength 文本推断任何 control state。

---

# 二十、Universal ownership / Companion 边界

## Student / Teacher

**不存在。** Universal clean break 已删除 Student/Teacher 产品面与 request kind。Strength 不得：

```text
以 Student/Teacher 作为 eligible 排除样板
复用 Teacher-style SatelliteKind 分类解释 Replica
引入任何教育控制流 / QA / SKILL 依赖
```

## Replica 自身

Strength Replica 是 Universal InternalLeaf attachment：

```text
ExecutionClass = InternalLeaf
Ownership = Attached(owner, StrengthReplica)
无 Companion
无 SyncDelegate 子会话
无嵌套 StrengthReplica
无 fork/list/join
无 fallback deep peer
```

## Owner Companion

只有 Promoted frame 才能进入 XTrace，进而被 owner Companion 消化。

Candidate 永远不可进入：

```text
XTrace
Blogger delta
BlogFrame
LifecycleWorkRecord
PrefixSnapshot
```

---

# 二十一、Enforcer / Pair marker / Synthetic content

Strength 不改变 Enforcer 的控制权。

Replica 使用与 owner 同 CanonicalRole 的 system prompt，但工具 schema 被 request kind 收窄；如果 Enforcer 对普通角色的行为规则仍会运行，它看到的是正常角色请求，而不是“Replica”语言身份。

主 Work Session 的 synthetic content 顺序要保持：

```text
... existing projection
→ Strength frames
→ PairProgrammingThought
→ ReviewSeal
```

Strength 自己不得向模型显示 provenance 标签。

如果 Host adapter 需要 `info.source` 做幂等过滤，它只能是 Host-only metadata；canary 必须证明不会进入 ProviderSemanticProjection 的模型字节。

---

# 二十二、持久事实：只记录发生过的事（EventStore only）

建议新增独立 Strength **EventStore event family**，而不是塞进 Companion / Fallback，也不是私有 Journal/Blob store。

### Storage 收口（相对旧稿）

| 旧假定 | 裁决 |
|---|---|
| `FrameBundleRef` | 删除类型名；改为 `EventEnvelope.PayloadRefs` 中的 opaque `PayloadRef` |
| `PredictorSnapshotRef` | 同上（可选 snapshot payload） |
| Journal NDJSON (`RuntimePath` `*.ndjson`) | 删除；Strength 不写、不读该 substrate |
| `RuntimePath` `blobs/<sha256>` | 删除；大 material → EventStore `payloads/` via `payload_refs` |
| feature-owned Strength store / ref | 禁止 |

核心 events：

```fsharp
StrengthDecisionObserved
    // 可选；仅当需要 durable predictor/control audit 时

StrengthCandidatePrepared
    // Replica bundle 已 commit 到 EventStore，绑定 target run
    // payload_refs ⊇ { frameBundlePayload, predictorSnapshotPayload? }

StrengthCandidatePromoted
    // target run 已产生消费证据
    // 可引用同一 frame bundle PayloadRef；禁止复制第二份 RuntimePath blob

StrengthFramesTraced
    // promoted bundle 已被 XTrace 捕获到明确 cursor range
```

可选诊断事件：

```fsharp
StrengthCandidateAbandoned
```

只在“target run 已明确终止且未消费”这种真实事件发生时记录；不要为了推进流程写：

```text
StrengthPhaseChanged
AwaitingReplica
ReadyToPromote
PromotionPending
```

当前状态由 projection 纯推导：

```fsharp
type StrengthCandidateView =
    | Prepared of ...
    | Promoted of ...
```

甚至这个 DU 也只是 Projection 视图，不是持久 stage。

Projection 至少索引：

```text
OwnerSessionId → current/open candidate by TargetProviderRun
DecisionId → prepared metadata + PayloadRef set + digests
DecisionId → promotion evidence
DecisionId → XTrace range
TargetProviderRun → DecisionId
```

业务热路径不得每次扫描 event 全集或任何遗留 NDJSON 文件；只读 Strength projection / 索引。Committed `payload_refs` 必须落在 EventStore root `payloads/` closure 内（dangling → StorageInvalid）。

---

# 二十三、崩溃矩阵

## A. Replica 创建前 crash

无 durable Strength EventStore effect。

```text
restart → 正常 K0 / 重新决策
```

## B. Replica 已运行、Candidate 尚未 durable

Replica side effect 只有只读操作。

```text
restart → 丢弃
```

## C. Candidate append 明确失败

```text
main 正常 K0
```

## D. Candidate append CommitUnknown

```text
必须重读证明
无法证明 → 不发送 target provider request
```

## E. Candidate durable，main transform 在外发前 crash

重启后若 TargetProviderRun 仍是同一 bindable run：

```text
重放同 DecisionId / 同 digest Candidate
```

若该 run 已不可能消费：

```text
不 promotion
```

## F. provider 已消费并产生输出，promotion 前 crash

Reconciler 重建同 TargetProviderRun：

```text
补 StrengthCandidatePromoted
→ 再允许 continuation
```

## G. promotion append CommitUnknown

```text
重读 resolve
无法证明 → continuation fail closed
```

## H. Promoted 后、XTrace 尚未捕获 crash

下一 transform 的 StrengthReplay 重建 frames：

```text
XTraceCapture 再捕获
```

## I. XTrace 已捕获、StrengthFramesTraced 未写 crash

根据 deterministic frame identity + current generation 恢复 range，幂等补事实。

## J. owner 被用户取消/删除

```text
取消当前 Replica attempt
retire Replica
Candidate 若未消费不 promotion
owner 自己按现有 cancellation 语义处理
```

Strength 不伪造“用户消息中断”一类原因。

---

# 二十四、Host canary：没有这些就不启用

由于 Strength 把旁路 provider request 放在主 request 的 transform 临界区，Host 行为必须实测，不靠猜。

必须有 canary 证明：

1. **Nested session safety**：一个 Work transform 等待 StrengthReplica（InternalLeaf+Attached）session 的 provider/tool loop 不死锁 Host。
2. **Budget stop**：Replica 达 K 后可以在下一 transform/reconcile 物理阻止 K+1 请求。
3. **Target binding**：当前 transform 可以唯一绑定将要消费输入的 `ProviderRunIdentity`；无唯一答案时 K0。
4. **Tool schema**：StrengthReplica provider-visible schema 恰好是 `{read,glob,grep}`（provider 要求 noop 的合法例外单独登记）。
5. **Execution gate**：伪造 `write/edit/executor/fork/join/network` 调用仍被拒绝，而不是只从 schema 隐藏。
6. **No permission ask**：Replica 不产生任何用户权限弹窗。
7. **Same-role prompt**：`deep-ROLE` owner 与 `fast-ROLE` Replica 的 role system prompt 语义一致，没有 Replica 专用身份提示。
8. **Semantic mirror**：owner history 跨 session 去 ID 后等价；Replica 自己的 model/tools 不被 owner projection 覆盖。
9. **Deterministic wire IDs**：同 Decision 重放得到相同 synthetic call IDs / bytes。
10. **Candidate invisibility**：未 promotion 的 Candidate 永不进入 XTrace/Companion/LWR。
11. **Promotion durability**：消费后 crash/restart，下一 request 仍看到等价 Promoted history。
12. **Pair invariant**：Strength tool-result anchors 之后仍存在 PairProgrammingThought marker。
13. **Review invariant**：ReviewSeal 仍覆盖最终 provider bytes；Reviewer 路径 Strength 永远 K0。
14. **InternalLeaf attachment**：StrengthReplica 不创建 Companion / SyncDelegate / 嵌套 StrengthReplica，也不进入普通 fork/list/join surface；分类不得回退为 `SatelliteKind.Replica`。
15. **Upgrade canary**：Host / OpenCode 版本变化后，上述行为重新跑；任一关键项失败 → Strength 自动禁用。

---

# 二十五、测试与 proof 义务

当前 Repomix 未包含 tests 内容，因此这里定义目标测试矩阵，不假定现有具体测试文件名。

## Domain property tests

```text
Decision pure / deterministic
K ∈ {0,1,2}
K2 margin > K1 margin
ineligible → K0
unknown cost → K0
non-deep / fallback side → K0
StrengthReplica tool set = exact readonly set
invalid role × StrengthReplica → empty
Candidate cannot be rendered for wrong TargetProviderRun
Promoted replay is idempotent
same Decision + different digest → conflict
wire ID derivation deterministic
```

## Projection tests

```text
UseStrengthMirror conflicts with ordinary Work base selection
Strength insertion canonical order independent of registration order
Strength before Pair marker
Candidate not in early replay
Promoted inserted before target assistant
semantic digest stable across replica/owner call IDs
```

## EventStore / Fold tests

```text
Prepared idempotent same digest + same payload_refs
Prepared same Decision different digest rejected
Prepared material only via payload_refs（无 RuntimePath blob / NDJSON side write）
Promoted without Prepared rejected
Promoted wrong TargetProviderRun rejected
Promoted twice idempotent
Traced before Promoted rejected
XTrace range monotonic
restart snapshot equals live EventStore projection
payload_refs ⊆ committed payloads/ closure
```

## Integration tests

```text
K1 read → primary consumes → promote → continuation still sees frame
K2 parallel read batches
Replica text-out after first batch
Replica denied write
Replica provider failure → main normal
Candidate CommitUnknown resolution
promotion crash recovery
owner cancellation while waiting Replica
Host compaction / reanchor with old Promoted frames
Companion ingestion only after promotion
```

## Statistical / simulation tests

```text
control assignment restart-stable
control assignment independent of predictor score
Replica events never become training labels
shadow labels reconstruct actual primary requests
cost function monotonic in fast cost / byte cost / delay / risk
K2 does not activate before minimum evidence floor
```

---

# 二十六、灰度顺序

不要从“代码写完”直接跳 K2。

## Phase 0 — Architecture splice

只落：

```text
AttachmentKind.StrengthReplica（InternalLeaf × Attached）
StrengthReplica request kind + permissions
Strength EventStore events/projection + payload_refs
Projection intents
replay/candidate wiring skeleton
all feature decisions forced K0
```

目标：Strength disabled 时普通系统字节与行为完全不变。

## Phase 1 — Host canary

跑前述 15 项。

关键 canary 不通过，不继续。

## Phase 2 — Shadow predictor

```text
100% K0
只记录 opportunity / prediction / actual next primary request labels
```

先证明“这个模式今天还真实存在”，不要拿旧版本统计当事实。

## Phase 3 — Replica dry run

真实创建 Replica、真实执行 read-only batch，但**不注入主模型**。

比较：

```text
Replica 预测路径
vs
control primary 实际路径
```

验证工具权限、延迟、bytes、稳定性。

## Phase 4 — K1 treatment + control holdout

只启用 K1。

观察：

```text
deep request / task
fast request / task
provider cost proxy
wall-clock
input bytes
fallback / repair rate
review/finality outcome
user-visible failure rate
```

## Phase 5 — K2

只有 K1 的：

```text
经济收益为正
质量无显著退化
promotion/recovery 零不一致
```

持续一段稳定窗口后，才允许 K2。

---

# 二十七、自动熔断

任一以下条件触发 Host-internal Strength disable：

```text
Candidate / Promotion durable inconsistency
ProjectionConflict involving Strength
wrong-target render
permission schema mismatch
execution gate mismatch
nested session deadlock/timeout rate异常
promotion recovery failure
control bucket 显示质量指标显著恶化
成本模型不可用或收益长期为负
Host canary 版本漂移
```

熔断后：

```text
新 decision 一律 K0
已有 Promoted history 仍正常 replay / recover
已有 Candidate 仍按其 target run 完成或自然失效
```

禁止“关开关”导致已经影响模型的 Promoted frame 从历史消失。

---

# 二十八、建议模块落点

按当前 repo 分层，建议：

```text
Domain/
  StrengthPolicy.fs
      eligibility / predictor / value / control assignment（纯）
  StrengthFrame.fs
      semantic batch / digest / deterministic wire identity
  StrengthProjection.fs
      durable view types / pure decisions
  StrengthEvents.fs
      StrengthCandidatePrepared/Promoted/... vocabulary；PayloadRef fields only
  ProjectionAlgebra.fs
      + UseStrengthMirror / InsertStrengthFrames
  PrefixCandidate.fs
      + ProviderRequestKind.StrengthReplica semantics
  PromptAuthority.fs
      + request-specific readonly capability mapping
  SessionOwnership / AttachmentKind
      + StrengthReplica（Universal owner；非 SatelliteKind）

Infrastructure/Persist/（或 EventStore adapter 面）
  Strength event codec / fold / indexed projection
  payload_refs ↔ raw payloads mapping
  禁止 Journal NDJSON writer、禁止 RuntimePath blob path

Application/
  Strength/
    StrengthWorkflow.fs
    StrengthPromotion.fs
  Reconciliation/
    StrengthReplay.fs
    StrengthSpeculate.fs

Session/
  StrengthRuntime.fs
      decision-local physical ownership / batch collection only
  Attached/leaf runtime
      复用 Universal ownership；不扩 SatelliteKind.Replica

Infrastructure/OpenCode/Host/
  Strength host adapter / canary glue
  SpikePlugin.fs
      插入两处 hook 顺序
```

模块职责必须保持：

```text
Domain      不访问 Host、不写 EventStore/Git OID
Application 结构化 workflow + typed ports
Session     single-flight / physical StrengthReplica resource
Persist     EventStore events / payload_refs / fold / indexed projection
Infrastructure 只做 Host adapter / codec
```

不要创建一个 1000 行 `StrengthTransform.fs` 同时做预测、session、权限、投影、EventStore、恢复。

---

# 二十九、实现顺序

## 1. 先接身份与空行为

```text
AttachmentKind.StrengthReplica
Universal ownership association / accessors
ProviderRequestKind.StrengthReplica
ToolCapabilitySet
AttemptExecutionProfile integration
```

所有 decision 强制 K0，证明普通行为零变化。

## 2. Projection intent

加入：

```text
UseStrengthMirror
InsertStrengthFrames
```

先做纯 Domain/property tests。

## 3. Replica runtime + dry run

复用 Universal Attached/leaf runtime（StrengthRuntime），跑真实 fast same-role request，但不注入 main。

## 4. Candidate durability

实现：

```text
Prepared
CommitUnknown resolution
wrong-run rejection
```

仍不 promotion。

## 5. K1 candidate injection + promotion

先只 K1，打通：

```text
candidate
→ target provider
→ reconcile evidence
→ promotion
→ replay before XTrace
```

## 6. XTrace / Companion closure

证明：

```text
candidate excluded
promoted included
compaction/restart 不丢失
```

没有这个闭环，不允许长期启用。

## 7. Shadow/control predictor

最后才让 predictor 决定 K。

先有正确执行语义，再优化触发率；不要反过来。

## 8. K2

K1 稳定后单独开。

---

# 三十、明确拒绝的方向

## 同一 Work Session 临时切 fast model

拒绝。

它会污染：

```text
Authority / fallback identity
stable prefix
provider run attribution
model-visible continuity
```

独立 Satellite 才是正确隔离边界。

## 新增 Replica CanonicalRole

拒绝。

Strength 需要的是：

```text
同 role 语义
+
fast model binding
+
request-specific readonly tools
```

当前 Agent/Tier/PromptAuthority 已经能直接表达，没有理由制造例外身份。

## 新增 fast-replica/deep-replica

拒绝。

它会迫使 `Agent → Role` 变成非函数或引入特殊 mapper，违背当前原子 profile 的目的。

## 只靠 prompt 说“不要写”

拒绝。

权限必须结构化。

## 无限只读 prefetch

拒绝。

只读不等于无害；每多一层都增加 steering risk 与 input bytes。

## 按 tool call 数预算

拒绝。

成本单元是 provider 决策。

## 让主模型看到来源标签

拒绝。

会改变推理策略，并把内部机制升级成模型协议。

## Candidate 直接进 XTrace

拒绝。

未消费的投机不是历史。

## promotion 后只靠内存记住

拒绝。

重启会删除真实因果历史。

## Replica request 写入主 FallbackCursor

拒绝。

这是不同资源、不同目的的 attempt。

## 继续旧版 training-inclusion controller

第一版拒绝。

先用可解释的 deterministic control holdout 得到干净 label；未来若 control 成本确实过高，再拿数据论证是否需要更复杂的 off-policy estimator，而不是先上 controller。

---

# 三十一、最终不变量

实现完成后必须能机械证明：

```text
1. Strength disabled → 普通 Work Session provider-visible bytes 与控制流无变化。

2. StrengthReplica = InternalLeaf × Attached(StrengthReplica)；无 Companion / SyncDelegate / 嵌套 StrengthReplica；禁止 SatelliteKind.Replica。

3. 没有 Replica Role，没有 fast-replica/deep-replica Agent。

4. Replica 使用 fast-<owner-role>，system prompt 仍由 owner CanonicalRole 决定。

5. StrengthReplica tools 恰好是允许的 readonly set；schema 与 execution gate 同源。

6. K 的单位是 provider request；K ∈ {0,1,2}。

7. Candidate 在消费前不进入 XTrace / Companion / LWR / future durable history。

8. Candidate 只对一个明确 TargetProviderRun 可见。

9. 只有该 TargetProviderRun 的真实 provider output 才能触发 Promotion。

10. Promoted frame 在任何重启/continuation 后都不会从语义历史消失。

11. Promoted frame 最终进入 owner XTrace，并可被现有 Companion/context machinery 消化。

12. 跨 Session 相等性只比较 Semantic projection；wire identity deterministic localize。

13. Replica 失败不推进 owner FallbackCursor，也不触发 owner InteractionRepair。

14. Review/Finality/Attached/InternalLeaf 路径第一版永远 K0（含 Companion、SyncDelegate、Bookkeeper、StrengthReplica 自身）。Student/Teacher 已删除，不在词汇表中。

15. PairProgrammingThought 仍覆盖 Strength 新增的 tool-result anchor；ReviewSeal 仍最后。

16. 普通 pre-commit failure 可以 fail-open；任何 durable ambiguity / consumed-history ambiguity 必须 fail-closed。

17. Predictor 不把 Replica intervention request 当 primary counterfactual label。

18. control assignment restart-stable，且不由 predictor score 选择。

19. 所有 lifecycle 状态从 EventStore 事实与 projection 推导；没有 Stage/Phase/NextAction 程序计数器。

20. Host 版本 canary 任一关键边界失败 → 新 Strength decision 自动 K0；旧 Promoted history 仍可恢复。

21. Strength 大 material 只经 EventStore payload_refs；无 Journal NDJSON、无 RuntimePath blob、无 FrameBundleRef/PredictorSnapshotRef 独立存储类型。
```

---

# 三十二、Completion criteria

这个 Proposal 可以进入完成态的条件不是“Replica 能跑”，而是下面五层同时闭环：

## 语义正确

```text
Candidate → consumption proof → Promotion
```

在 crash/restart、fallback、compaction、user cancellation 下都没有“模型见过但历史丢失”或“模型没见过却被 promotion”。

## 权限正确

便宜路径从 provider schema 到 execution gate 都只有 `read/glob/grep`，无 permission ask，无隐藏写路径。

## 架构正确

Strength 只扩当前 Universal ownership（AttachmentKind.StrengthReplica）/ PromptAuthority / Projection / XTrace / EventStore owner，不产生第二套身份、权限、projection、fallback、lifecycle runtime 或 feature-owned storage。

## 经济正确

Shadow/control 数据表明：

```text
K1 至少在一个稳定 eligible cohort 中有正净收益
```

否则功能保持关闭；“架构实现完成”不自动意味着应该启用。

## 质量正确

Treatment 相对 control 在至少以下指标上无不可接受退化：

```text
任务成功率
review/finality 结果
fallback/repair rate
用户可见错误
wall-clock tail latency
provider input bytes
```

K2 需要独立通过，不继承 K1 的结论。

---

# 三十三、实施时需要更新的正式知识面

本 Proposal 启动后，正式语义不要整篇复制到某一个 docs 文件，而是按当前治理拆分：

```text
docs/why/
  Strength 为什么存在、为何独立旁路、为何只读、为何 control holdout

docs/what/
  可观察行为、不变量、eligible / K / candidate-promotion 语义

docs/shape/
  Satellite ownership、PromptAuthority owner、facts writer、projection intent owner

docs/how/
  两段 transform、Replica workflow、promotion reconcile、predictor/value 算法

docs/proof/
  Host canary、property tests、crash matrix、rollout / kill criteria
```

相关既有主题应引用而不是复制：

```text
agent
architecture
dsl-structured-program
host
prompt
projection
companion
context
fallback
review
persist
execution
```

最终代码必须以这些正式 docs 为目标，而不是以本 Proposal 的伪代码逐字实现。

---

# 三十四、一句话裁决

Strength 仍然值得做，但今天正确的形状已经不是“给旧系统外挂一个 Replica 子系统”，而是：

> **在当前 Work Session 的 provider request 临界点，按 Universal ownership 启一个 `InternalLeaf × Attached(StrengthReplica)` 同角色 fast leaf session，通过现有 AttemptExecutionProfile 把它约束成只读；它最多提前执行两个真实 provider request。其输出先作为 EventStore `StrengthCandidatePrepared`（frame/predictor material 仅经 `payload_refs`）绑定当前 TargetProviderRun；只有主 run 产生消费证据后才 Promotion，并在下一次早期 projection 中进入 XTrace/Companion 的 durable semantic history。预测器只用 shadow/control primary 行为训练，普通失败 K0，durable 因果不确定则 fail closed。不使用 Journal NDJSON / RuntimePath blob，不使用 `SatelliteKind.Replica`，不依赖 Student/Teacher。**

这保留了旧提案真正有价值的第一性原理，同时删除了所有已经被当前 repo 基础设施取代、或会重新制造第二套 owner 的实现包袱。

---

# Active work

> 本文件是变更工作记录，不是当前产品规范；当前产品语义仅以 `docs/` 正式层为准。

## Specification impact

- 在正式 `why/what/shape/how/proof` 中定义 Strength 的只读投机价值、eligible/K、Candidate→Promotion→XTrace 语义、Universal ownership、PromptAuthority、Projection Algebra、EventStore `payload_refs`、recovery/control/canary 边界。
- 既有 agent/host/prompt/projection/companion/context/fallback/review/persist/execution 条款保持 owner；Strength 只增加合法 case 与交叉引用，不建立第二套 runtime/storage/fallback/projection authority。

## Remaining work

1. 对齐正式 docs 与 glossary/navigation，消除旧 Phase-0/Student-Teacher/feature-owned storage 语义漂移。
2. 完成纯 Domain：eligibility/control/value policy、semantic frame/digest/wire identity、Strength events/projection、Projection intents 与冲突规则。
3. 完成 EventStore codec/fold/index 与 payload material durability；实现 Prepared/Promoted/Traced 幂等、冲突与 wrong-target 约束。
4. 完成 decision-local StrengthReplica runtime、same-role fast profile、readonly schema+execution gate、K1/K2 request budget 与停止语义。
5. 完成 StrengthSpeculate/Promotion/Replay/XTrace/Companion wiring、CommitUnknown resolve、cancellation/recovery/fuse。
6. 建立 proposal 要求的 domain/projection/persist/integration/statistical/Host canary 永久 proof；通过仓库标准 build/test/spec/lint 门禁。
7. 清理 Phase-0 stub、旧 Strength 残留与无价值临时脚手架；关闭 Change 并记录 Final outcome。

## Completion criteria

- Proposal §31 的 21 条最终不变量均有正式 Clause 与机械 proof。
- Candidate 只有绑定 TargetProviderRun 消费后才能 Promotion；Promoted history 在 crash/restart/compaction/continuation 后可恢复并最终进入 XTrace/Companion。
- StrengthReplica 为 `InternalLeaf × Attached(StrengthReplica)`，same-role fast peer，provider schema 与 execution gate 恰为 `read/glob/grep`；不影响 owner fallback/review/finality。
- 大 material 只经 EventStore `payload_refs`；不存在 Strength Journal NDJSON、RuntimePath blob 或独立 storage ref 类型。
- 启用策略只基于 shadow/control 可识别数据与显式成本；证据/成本/canary 不足时新 decision 必为 K0，已 Promoted 历史继续 replay。
- 标准仓库检查全绿，无未解决 blocker。

## Blockers

- 无。
