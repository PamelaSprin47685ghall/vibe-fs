# RFC/strength — Predict & Reduce Strength

Status: proposed
Product contract: no
Runtime status: inactive
Target version: undecided

条款前缀：`STRENGTH-`。正文中「spec/13 — Projection Algebra」的引用已被 superseded 到 `spec/16 — Projection Algebra`，代数语义由本文档 STRENGTH-066…074 与 spec/16 共同承载。

---

# 一、执行摘要

Predict & Reduce Strength 用于识别主模型即将进行的机械性只读调查，并让便宜模型在独立旁路工作会话中提前执行最多两个 provider 请求。旁路产生的真实只读工具调用及结果，经确定性投影加入主工作会话；主模型继续完成后续推理、写入、执行和用户可见输出。

本功能不是同一会话内的模型降级，也不把主工作会话的一轮控制权直接交给便宜模型。它是：

> 有限深度、只读、无来源标记、可丢弃的旁路投机执行。

最终裁决如下：

1. 使用独立 Strength Replica 工作会话，不在主会话中切换模型。
2. 主工作会话和 Strength Replica 各自拥有独立 Companion。
3. 两个模型均不知道 Strength 功能的存在。
4. 主模型看不到任何来源标记。
5. 便宜模型不接收任何“只做机械步骤”之类的特殊合同。
6. Strength Replica 的 provider-visible 工具集在结构上只包含允许的只读工具。
7. 预算单位是 provider 请求数，不是工具调用数。
8. 一次 provider 请求内的并发只读调用全部作为一个请求批次接收。
9. 预测器输出 `K ∈ {0,1,2}`。
10. 第 `K+1` 次 Replica transform 默认挂起，不向 provider 发出请求。
11. 后续再次需要 Replica 时，以最新镜像恢复该挂起 transform；但限制在同一 Primary Authority Root 内。
12. Replica 在预算内自然 text-out 时，文本全部丢弃，只提交此前完成的工具批次。
13. 已提交的旁路工具批次在主模型投影中与普通历史工具调用字节一致。
14. 训练流按受控概率纳入旁路请求符号；该概率由预测倾向的负反馈控制。
15. 系统追求稳定、成本合理的闭环工作点，不声称识别反事实最优策略。
16. 投影功能必须通过 typed DSL/组合子编写（spec/16 Projection Algebra）；领域事实和生命周期仍由 Journal Fold 与普通程序控制。
17. 候选帧（Candidate Frame）采用「候选-消费-提升」两阶段语义：候选只对首次目标 attempt 可见，主 provider 确认消费后才 promote 为活动历史。
18. Replica 使用 fast-replica/deep-replica 内部 Agent，只提供模型绑定，不决定 CanonicalRole。
19. 训练状态只按 X 的 CanonicalRole 分桶，不按模型组合或版本细分。
20. 所有策略参数是集中式代码常量，不新增运行时配置面。

---

# 二、问题定义

## STRENGTH-001：观察

LLM 工作流中存在大量如下模式：

```text
grep
→ read
→ read
→ edit
```

其中 grep 之后的第一个 read 经常由搜索结果直接决定，机械性较强。使用昂贵主模型生成这一请求通常没有必要。

相似模式还包括：

```text
glob
→ 并发 read 若干候选
```

```text
read 某索引文件
→ read 索引中明确引用的目标文件
```

这些步骤的共同特征不是“工具名字恰好是 read”，而是：

> 下一次 provider 请求大概率只产生无副作用的调查操作，并且错误投机的损失有界。

## STRENGTH-002：收益来源

命中时，Strength Replica 代替主模型完成一个或两个 provider 请求。

主模型本来就需要接收相同或相近的工具结果，因此主要收益是：

```text
被省去的主模型请求成本
- Replica 请求成本
- 额外投影字节成本
- 阻塞延迟成本
- 错误调查方向的风险成本
```

本功能不承诺每次触发都节省成本，只要求长期运行形成稳定、合理、总体有利的工作点。

## STRENGTH-003：主要风险

风险分为三层：

```text
第一层：Replica 请求白跑。
第二层：无用工具结果增加主模型输入。
第三层：错误阅读路径改变主模型后续推理倾向。
```

第三层是主要质量风险。因此最大预测深度固定为两个 provider 请求，不开放无限只读连读。

---

# 三、目标与非目标

## STRENGTH-004：目标

本功能必须做到：

1. 在高概率机械只读场景中减少昂贵模型请求。
2. 不让便宜模型产生用户可见正文。
3. 不让便宜模型执行写入或其他高风险操作。
4. 不修改主会话已经存在的物理 transcript。
5. 不需要回滚已提交事实。
6. 不破坏主模型和 Replica 各自的稳定前缀。
7. 任意不确定情况均可退化为正常主模型请求。
8. Replica 上下文长度与主模型上下文长度解耦。
9. 重启后能由 Journal Fold 得到唯一、确定的投影视图。
10. 投影规则可组合、可审计、可做性质测试。

## STRENGTH-005：非目标

本功能不负责：

1. 证明主模型在没有旁路时本来会选择什么。
2. 寻找数学意义上的全局最优策略。
3. 保证旁路读取路径与主模型反事实路径完全一致。
4. 降低用户可见回答所使用的模型强度。
5. 允许 Replica 写文件、执行命令或提交 verdict。
6. 根据上下文窗口、剩余 token 或模型容量主动压缩。
7. 改写 Fallback cursor。
8. 改写主工作会话的 SelectedAgent 或 EffectiveAgent。
9. 改写现有 PrefixEpoch。
10. 让投影 DSL 承担业务生命周期状态机职责。

---

# 四、术语

## STRENGTH-006：会话记号

```text
X              Work Session
Y_X            Companion Satellite   of X
Z_X            Replica   Satellite   of X

⟨X ; Y_X , Z_X⟩    X 的 managed cluster
Sat(X)              X 的卫星集
S_X                 X 的任意卫星

P(X)     Prose       正式正文累积      由 X   产出
L(X)     Log         工作日志累积      由 Y_X 产出
F(X)     Frames      委派帧累积        由 Z_X 产出
```

`F(X) = F°(X) ⊎ F•(X)`，即候选帧与已提升帧的不交并。

每个 `X` 至多关联一个活跃 `Z_X`。

`Z_X` 是卫星会话，不进入任何模型可见的 Agent enum、fork schema、list 或 join 结果。`Z_X` 自身无 Companion（STRENGTH-009）。

### Session 关联持久化

「每个 X 至多一个活跃 Z_X」不能依赖启动时扫描或内存 Map。必须持久化：

```fsharp
type SatelliteLinked =
    { OwnerSessionId: SessionId
      SatelliteSessionId: SessionId
      Kind: SatelliteKind           // Companion | Replica
      Agent: string
      LinkEpoch: int64 }

type SatelliteRetired =
    { OwnerSessionId: SessionId
      SatelliteSessionId: SessionId
      Reason: SatelliteRetireReason }
```

创建流程必须遵守 Session 创建的 durable-effect 纪律：

```text
DurableEffectRequested
→ 创建 Z_X
→ DurableEffectAccepted
→ SatelliteLinked
```

Fold 必须证明：

```text
ExactlyOnePersistent 卫星    一个 X 恰好一个该种卫星
AtMostOneEphemeral   卫星    一个 X 至多一个未 retired 该种卫星
卫星 SessionId ≠ X
卫星自身无卫星
```

X 删除时级联 retire 全部 `Sat(X)`。

## STRENGTH-007：组件名称

```fsharp
type StrengthPredictor
```

统计预测器。根据请求级工具序列、当前结果结构及成本特征，输出 `K=0/1/2`。

```fsharp
type StrengthReplica
```

使用便宜模型执行只读请求的旁路工作会话。

```fsharp
type StrengthController
```

通过训练流纳入概率的负反馈，调节预测器长期倾向。

```fsharp
type StrengthFrameCandidate
```

一个 Replica provider 请求产生的完整工具调用批次（候选帧），待主模型消费后提升为活动历史。


## STRENGTH-007B：Replica Agent 设计

### 新增两个内部 Agent

系统必须新增：

```text
fast-replica
deep-replica
```

二者都必须绑定低成本模型。

它们不属于用户可选择的普通角色 Agent，不进入：

```text
公开 Authority Root agent 参数
fork-agent enum
fork-manager enum
inspector/coder 工具 enum
list 的 Agent catalog
任何模型可见 Agent 描述
```

启动时必须验证二者存在且 model 字符串非空、互异、可由 Host 通过 `config.agent[effectiveAgent].model` 解析。

必须存在的 Agent 总数由 20 增加为 22。

### Replica Agent 只提供模型绑定

普通角色 Agent 继续遵循：

```text
Agent 身份 → CanonicalRole → SystemPromptId → CanonicalRolePermissions
```

Replica Agent 是唯一例外：

```text
fast-replica / deep-replica
→ 只决定模型绑定
→ 不决定 CanonicalRole
→ 不决定 SystemPromptId
→ 不决定完整角色权限
```

Replica attempt 的不可变 profile：

```fsharp
type ReplicaAttemptProfile =
    { OwnerPrimarySessionId: SessionId
      EffectiveAgent: ReplicaAgent
      CanonicalRole: Role
      SystemPromptId: SystemPromptId
      ExecutionSurface: ExecutionSurface
      RequestKind: ProviderRequestKind
      ProviderRunIdentity: ProviderRunIdentity
      StrengthDecisionId: string }
```

其中 `CanonicalRole` = 所属 X 当前 `AttemptExecutionProfile.CanonicalRole`，
`SystemPromptId` = `systemPromptOf(CanonicalRole)`，
`ExecutionSurface` = `StrengthReadOnlySurface`。

Replica 因而使用与主会话当前角色相同的 system prompt，但模型由 `fast-replica` 或 `deep-replica` 提供。

### Replica Tier 映射

```fsharp
let replicaOfPrimaryTier = function
    | Fast -> FastReplica
    | Deep -> DeepReplica
```

即 fast-coder → fast-replica，deep-coder → deep-replica，以此类推。

### Host Agent Prompt 隔离

`fast-replica` 和 `deep-replica` 的 Host 静态 Agent 定义不得携带会与角色 prompt 冲突的实质性 system prompt。

必须有 Host canary 证明 `Agent = fast-replica/deep-replica` 时，最终 provider-visible system prompt 确实等于所属 X 的 CanonicalRole prompt。

---

# 五、会话拓扑

## STRENGTH-008：ManagedSessionKind 扩展

现有 Session 种类改为：

```fsharp
type SatelliteKind =
    | Companion
    | Replica

type ManagedSessionKind =
    | WorkSession
    | Satellite of kind: SatelliteKind * owner: SessionId
```

## STRENGTH-009：卫星不变量

以下不变量适用于所有 Session：

```text
每个 WorkSession 恰好拥有一个 Companion 卫星（Y_X）。
每个 WorkSession 至多拥有一个 Replica 卫星（Z_X）。
卫星自身没有卫星：Sat(S_X) = ∅。
卫星的 SessionId ≠ 其所属 X。
```

因此：

```text
∀X.  |Y_X| = 1
∀X.  |{active Z_X}| ≤ 1
∀S_X. Sat(S_X) = ∅
```

`Z_X` 是卫星而非 Work Session，不落入 COMPANION-001 的辖区（该辖区限定为 `ProviderRequestKind = WorkMain`；`Z_X` 使用 `ReplicaMain`）。COMPANION-001 无需修订。 这与 `Y_X`（使用 `BloggerMain`）自身无 Companion 是同一机制。

## STRENGTH-010：Z_X 继承 X 的前缀 epoch

`Z_X` 无自身 epoch 状态。`Z_X` 的投影按 X 的当前 epoch 渲染：

```text
Π(Z_X) = seal ∘ suppressBootstrap ∘ localParts(Z_X)
              ∘ prefix (L(X) ↾ cut(ε(X))) ∘ raw(X) ↾ cut(ε(X))
```

即 `Z_X` 的可见历史由两部分构成：X 的 cutoff 之后原始历史，与 `L(X)` 的冻结切片（`FrozenRecordPrefix`）。

```text
Z_X 不维护 ActivePrefixEpoch / CoverableRecordPrefix / Coverage / FrameEpoch。
禁止将 Y_X 的 FrozenRecordPrefix 直接用于 Z_X——Z_X 继承的是 ε(X) 下的冻结切面，不是 Y_X 的原始状态。
```

理由：`Z_X` 每次都 bootstrap 新 turn，自身 1–2 个请求的历史不产生有价值的前缀缓存；镜像 X 的投影使 `Z_X` 的前缀稳定性直接等于 X 的前缀稳定性。

## STRENGTH-011：Replica 不升级为主模型

`Z_X` 的恢复不使用 PeerAgent 切换到昂贵主模型。

`Z_X` 的恢复策略为 `NoRecovery`：

```text
ReplicaMain Failed / Aborted
→ 本 Strength 决策失败
→ 丢弃未提交候选
→ X 正常请求
```

`Z_X` 的失败不是主工作会话的 Fallback 失败。不推进 X 的 FallbackCursor。

---

# 六、执行权限

## STRENGTH-012：工具权限双层 fail-closed（替换原 STRENGTH-012/014）

两层均可由现有 Host hook 与 SDK 实现，无需修改 Host 本体（ARCH-003 不需要例外）。

### 第一层：provider-visible schema 过滤

Z_X 创建时经 `POST /session` 挂接 session 级 ruleset。该 ruleset 在 `LLMRequestPrep.prepare` 的 `resolveTools` 中与 agent ruleset 合并（session 级在最后，`findLast` 使后者胜），白名单外的工具不进入 provider-visible schema。

```text
供给通道  Session.create({ permission }) 经 SDK 或 HTTP
生效点    LLMRequestPrep.prepare → resolveTools → Permission.disabled
效果      白名单外的工具从 provider-visible schema 消失
```

### 第二层：execution gate

即使 Host schema 配置异常，第二层按 `AttemptExecutionProfile.CanonicalRole = Replica` 拒绝越权工具执行。

### 零交互要求

Z_X 的 ruleset 每条规则解析结果必须落在 `{ allow, deny }`。任何解析为 `ask` 的规则视为配置错误，本次决策立即 K0。

理由：`ask` 会从一个用户从未主动创建的卫星会话弹出权限提示，且在 X 等待期内消耗 `StrengthDecisionDeadline`，超时后该提示成为孤儿。

### fail-closed 形态

无法识别 SessionKind、CanonicalRole 或卫星契约时，Z_X 可见工具集为空。可观察形态为 ∅ 或 `{ _noop }`（copilot provider 下当工具集为空且历史存在 tool call 时会注入一个 noop 工具），canary 断言必须同时承认两种。

## STRENGTH-013：第一版允许工具

允许工具集表述为具体 ruleset：

```jsonc
{
  "*": "deny",
  "read":  { "*": "allow", "*.env": "deny", "*.env.*": "deny" },
  "glob":  "allow",
  "grep":  "allow",
  "external_directory": { "*": "deny" }
}
```

不允许：

```text
write, edit, apply_patch, executor, fork-pty, fork-agent, fork-manager
join, list, verdict, coder, inspector
任何网络工具
```

后续扩大只读集合必须单独审阅其副作用、结果稳定性和上下文成本。

## STRENGTH-014：Replica 的恢复策略

Z_X 的恢复策略为 `NoRecovery`：ReplicaMain Failed / Aborted 后丢弃未提交候选，X 正常请求，不推进任何 FallbackCursor。

---

# 七、双方无感知原则

## STRENGTH-015：主模型无感知

主模型 provider-visible projection 中不得出现：

```text
sidecar
predictor
strength
delegated
prefetch
weak model
来源标记
置信度
预算
控制器状态
```

已提交的候选帧（Candidate）或已提升帧（Promoted）在 provider-visible projection 中必须渲染成与普通 assistant tool-call/tool-result 历史完全相同的语义形状。

本规则只约束 provider-visible 内容。内部 Journal 必须保留来源身份。

## STRENGTH-016：弱模型无感知

Replica 不接收以下特殊指令：

```text
只做显而易见的下一步
不要分析
最多读两个回合
不确定就停止
你是预读模型
你是便宜模型
```

预算控制由 transform 完成，不通过语言合同完成（STRENGTH-103）。允许角色语义文本：「你是一个只读调查角色」是角色定义，不是自限合同，不属于此类禁令。Replica 使用与 `Replica` 角色一致的普通 system prompt。

## STRENGTH-017：Replica 正文不可见

`P(Z_X)` 不进入任何 X 可见路径。`L(Z_X)` 不存在（Z_X 无 Companion）。

允许投影的内容只有：

```text
工具调用
工具调用参数
真实工具结果
必要的工具错误结果
```

---

# 八、预算语义

## STRENGTH-018：预算单位

预算单位是 provider 请求，不是工具调用。

```fsharp
type StrengthBudget =
    | K0
    | K1
    | K2
```

含义：

```text
K0：不运行 Replica。
K1：最多接收 Replica 的一个工具型 provider 请求。
K2：最多接收 Replica 的两个工具型 provider 请求。
```

## STRENGTH-019：并发工具调用

Replica 可以在一个 provider 请求内并发发出多个只读调用。

例如：

```text
同一个 provider 请求：
    read A
    read B
    read C
```

该批次计为一个请求，不计为三个请求。

该批次要么整体按 canonical 顺序提交，要么整体不提交。禁止只挑选其中一部分调用投影。

## STRENGTH-020：请求级符号

预测模型的主要时间单位也必须是 provider 请求。

```fsharp
type RequestSymbol =
    | Eot
    | ReadBatch of ReadBatchSignature
    | WriteBatch
    | ExecuteBatch
    | ControlBatch
    | VerdictBatch
    | OtherBatch
```

`ReadBatchSignature` 至少包含：

```fsharp
type ReadBatchSignature =
    { Tools: Set<ReadToolKind>
      ParallelismBucket: ParallelismBucket
      ResultBucket: ResultBucket
      TargetConcentration: ConcentrationBucket }
```

工具调用的完整名字和参数可以作为附加特征，但不得把一次并发请求错误展开成多个预测步。

---

# 九、预测器

## STRENGTH-021：预测输出

StrengthPredictor 输出：

```fsharp
type StrengthPrediction =
    { ProbabilityRead1: float
      ProbabilityRead2: float

      ExpectedBytes1: int64
      ExpectedBytes2: int64

      ExpectedDelay1: float
      ExpectedDelay2: float

      Risk1: float
      Risk2: float

      Value0: float
      Value1: float
      Value2: float

      RawTendency1: float
      RawTendency2: float

      ChosenBudget: StrengthBudget
      PredictorVersion: string }
```

其中：

```text
ProbabilityRead1
= 下一次 provider 请求为纯只读批次的概率

ProbabilityRead2
= 接下来两个 provider 请求均为纯只读批次的概率
```

## STRENGTH-022：序列基线模型与训练分桶

第一版使用可变阶请求级 n-gram：

```text
插值 Kneser-Ney
最大 order = 3
```

### 训练状态分桶

预测模型和控制器状态只按主工作会话 X 的 CanonicalRole 分桶：

```fsharp
type StrengthRoleBucket =
    { CanonicalRole: Role }
```

允许的桶：Coder、Inspector、DevOps、Meditator。

禁止再按以下字段细分：

```text
Primary model ID
Replica model ID
Primary/Replica model pair
AgentTier
模型版本
provider
当前模型是否与历史模型一致
SessionId
仓库
用户
```

一个角色桶内统一接收：

```text
该角色所有 X Session 的原生请求级符号
按控制概率纳入的 Replica 请求级符号
不同时间使用的 fast/deep 主 Agent 数据
不同时间使用的 fast/deep Replica 数据
模型升级或替换前后的数据
```

即所有 Coder 会话的数据统一进入 `StrengthRoleBucket(Coder)`，
不区分 deep-coder + fast-replica 或 fast-coder + fast-replica 等组合。

### 模型切换的处理

模型切换不：

```text
清空状态
创建新桶
冻结旧桶
迁移桶
建立 model-pair identity
```

非平稳性只通过统一的计数衰减处理。

### 计数衰减触发源

衰减触发点由 Journal Fold 派生：

```text
衰减触发量 = 该角色桶内已纳入 EffectiveTrainingSequence 的符号累计计数
该计数每跨过 CountDecayInterval 的整数倍，对桶内全部计数乘以 CountDecayFactor
禁止触发源：wall-clock、进程启动次数、快照写入时间、Session 数量
```

### Session 信息的有限用途

当前 Session 的请求级后缀仍然作为预测上下文，但统计计数来自角色共享桶。

```text
Context = 当前 Session suffix
Model parameters = CanonicalRole 共享状态
```

不得维护第二套 session-local 训练模型。

## STRENGTH-023：附加结构特征

预测器至少可以使用：

```text
最近请求级符号后缀
最近一次 grep/glob 命中文件数
命中位置数量
结果是否为空
是否出现唯一明确路径
候选路径集中度
最近一次 read 是否成功、失败或截断
最近请求并发调用宽度
最近工具结果实际 UTF-8 字节数
当前 CanonicalRole
主模型/Replica 模型对
当前是否处于 Authority Root 后第一请求
当前是否存在 PrefixProbe
```

禁止读取：

```text
模型上下文窗口
剩余 token
上下文占比
预计距离溢出还有多少 token
```

## STRENGTH-024：价值函数（含完整成本项与决策规则）

决策按净价值选择。

```text
V0 = 0
```

```text
V1 =
    P(read request 1) × SavedPrimaryRequestCost1
  - ReplicaProviderCost1
  - ExpectedProjectedBytesCost1
  - BlockingDelayCost1
  - SteeringRisk1
```

```text
V2 =
    P(read request 1) × SavedPrimaryRequestCost1
  + P(read requests 1 and 2) × SavedPrimaryRequestCost2
  - ExpectedReplicaTotalProviderCost
  - ExpectedProjectedBytesCost1And2
  - BlockingDelayCost1And2
  - SteeringRisk1
  - SteeringRisk2
```

Z_X 无恢复成本项，恢复即直接丢弃决策（STRENGTH-014）。Z_X 无独立 Companion，故无相关 ingestion 成本。

### 决策规则

```text
候选集初始为 { K0 }

若 V1 ≥ MinimumPositiveDecisionValue       则 K1 ∈ 候选集
若 V2 ≥ MinimumPositiveDecisionValue
   且 V2 - V1 ≥ MinimumK2AdvantageOverK1   则 K2 ∈ 候选集

K = 候选集中 V 最大者
并列时取较小的 K
```

若最大值不大于 `MinimumPositiveDecisionValue`（而非零），则选择 `K0`。

`MinimumK2AdvantageOverK1` 确保 K2 相对于 K1 存在独立净优势时才被选择。

`Risk2` 必须高于同条件下的单步风险，因为第二步建立在 Replica 第一批结果及其调查方向之上。

如果无法取得任何可靠的模型价格配置：

```text
CostModelUnavailable
→ K0
```

不要在 SSOT 中假设“弱模型一定便宜”。

## STRENGTH-025：投影字节成本

预计投影字节成本必须直接进入 `V1/V2`。

该字节成本使用：

```text
canonical provider-visible UTF-8 字节数
```

它不与上下文窗口比较，不换算 token，不触发主动 compaction，因此不属于上下文容量预测。

除软成本外，还必须存在固定输入合同：

```fsharp
MaxDelegatedBatchBytes: int64  // 第一版：65536 (64 KiB)
MaxDelegatedDecisionBytes: int64  // 第一版：98304 (96 KiB)
```

任何单批 canonical tool-call/result 字节超过上限时，该批次不得提交。

> 说明：现有系统最低动态输入合同为 200 KiB。
> 64 KiB 单批上限显著低于该合同，允许常见源文件和搜索结果进入投影，
> 但限制并发读取造成的大规模上下文污染。
> K2 总量为 96 KiB，不简单放宽到 128 KiB，
> 避免两个接近上限的错误批次同时进入主模型。
> 这仍然不是上下文窗口预测，只是固定输入安全合同。
> 超出上限时整批丢弃，不截断工具结果。

---

# 十、训练流与控制论闭环

## STRENGTH-026：两类序列

系统区分：

```text
PhysicalNativeSequence：
    主模型和普通工作会话真实产生的请求级符号

EffectiveTrainingSequence：
    PhysicalNativeSequence
    + 按概率选中的旁路请求级符号
```

StrengthPredictor 使用 `EffectiveTrainingSequence` 在线更新。

Replica 请求不是无条件真值，也不是永远排除。

## STRENGTH-027：训练纳入概率与确定性抽样

对第一个和第二个旁路请求分别维护：

```fsharp
type StrengthControllerState =
    { InclusionProbability1: float
      InclusionProbability2: float

      SmoothedTendency1: float
      SmoothedTendency2: float

      ControllerVersion: string }
```

每个成功提交的旁路请求独立决定是否进入训练流。

### 确定性抽样（替代随机数）

不使用进程级随机数。使用冻结的纳入概率和确定性哈希：

```fsharp
let u =
    hashToUnitInterval(
        decisionId,
        requestOrdinal,
        "strength-training-inclusion-v1")

let included = u < frozenInclusionProbability
```

```text
IncludedInTraining1 = u1 < ρ1
IncludedInTraining2 = u2 < ρ2
```

要求：

1. `frozenInclusionProbability` 必须随 StrengthDecision 一起持久化。
2. 重启后不得重新抽样——`decisionId` + `requestOrdinal` 决定相同结果。
3. 不得使用进程级随机数或系统时间作为抽样依据。
4. 确定性保证幂等性：重放 Journal 时训练标签不变。

### 纳入结果持久化

```fsharp
type StrengthTrainingInclusionCommitted =
    { DecisionId: string
      RequestOrdinal: int
      FrozenProbability: float
      HashDigest: string
      Included: bool }
```

## STRENGTH-028：负反馈方向

定义：

```text
z1 = 预测器对 K≥1 的平滑倾向
z2 = 在已倾向 K≥1 时进一步选择 K=2 的平滑倾向
```

控制器必须满足：

```text
z 上升 → ρ 下降
z 下降 → ρ 上升
```

推荐的无目标参数形式是：

```text
ρ1,target = 1 - z1
ρ2,target = 1 - z2
```

也可以使用等价的单调递减函数：

```text
ρtarget = g(z)
g'(z) < 0
```

## STRENGTH-029：控制论解释

当 `ρ=0`：

```text
Replica 已经替主模型读取
→ 旁路 read 不进入训练流
→ 主模型后续更容易直接 write
→ 训练序列偏 write
→ 预测倾向下降
```

当 `ρ=1`：

```text
所有旁路 read 都进入训练流
→ read 序列自强化
→ 预测倾向上升
```

通过令 `ρ` 随预测倾向反向变化，系统形成负反馈：

```text
预测过多
→ 降低旁路序列纳入率
→ read 训练信号减少
→ 预测倾向回落

预测过少
→ 提高旁路序列纳入率
→ read 训练信号增加
→ 预测倾向回升
```

本功能追求的是内部稳定工作点，不声称该工作点等于反事实全局最优值。

### 最终简化原则（适用于整个 spec/14）

```text
模型身份只影响本次请求路由，不影响训练分桶。

CanonicalRole 决定共享训练状态、system prompt 和角色语义。

ExecutionSurface 决定 Replica 的只读权限。

所有历史数据按角色融合，使用衰减适应模型变化。

所有策略参数都是集中式代码常量（PolicyConstants）。

所有 provider-visible 历史都由同一个 Projection DSL（spec/16）家族产生。

不为统计纯洁性增加模型对、版本、仓库或 Session 分桶。

不为运行时调参增加配置文件。

不为 Strength 保留旧的手写投影路径。
```

## STRENGTH-030：稳定性约束

控制器必须使用慢更新：

```text
预测计数模型：快速更新
训练纳入概率：慢速更新
```

推荐形式：

```fsharp
let desired = 1.0 - smoothedTendency

let filtered =
    (1.0 - alpha) * previousProbability
    + alpha * desired

let next =
    filtered
    |> clamp minimumProbability maximumProbability
    |> rateLimit previousProbability maximumStep
```

第一版建议：

```text
0 < alpha << 1
每累计一批样本再更新控制概率
单次概率变化设固定上限
不使用积分控制
使用 EWMA 平滑
```

推荐配置起点，不构成永久规范：

```text
minimumProbability = 0.05
maximumProbability = 0.95
每 128 个 eligible 决策更新一次
单次最大变化 = 0.01
EWMA 半衰期 = 512 个 eligible 决策
```

## STRENGTH-031：两个独立反馈环

`K1` 和 `K2` 必须使用不同控制状态。

禁止只控制总旁路率，因为以下两种状态风险不同：

```text
大量 K1、极少 K2
少量 K1、其中大部分 K2
```

第二环应采用：

```text
更低的上限
更慢的更新
更高的风险惩罚
更高的投影字节惩罚
```

## STRENGTH-032：不要求随机动作对照组

系统不要求随机强制选择 `K0` 的独立对照组。

该裁决意味着系统不声称：

```text
无偏估计主模型的反事实下一动作
证明当前策略是全局最优
提供严格因果节省量
```

这是主动接受的工程取舍。

系统仍必须记录自身状态，使审计者可以观察：

```text
RawTendency
ChosenBudget
InclusionProbability
IncludedInTraining
Value0/1/2
实际提交字节
后续重读/重搜代理指标
```

---

# 十一、触发条件

## STRENGTH-033：Eligible 条件

只有同时满足以下条件时，预测器才可以输出非零 K：

1. 当前 Session 是 `PrimaryWork`。
2. 当前 SelectedAgent 是昂贵层级。
3. 存在对应便宜模型。
4. CanonicalRole 在允许名单内。
5. 当前请求是普通 WorkMain。
6. 当前没有 attempt-local PrefixProbe。
7. 当前不是 InteractionRepair。
8. 当前不是 ReviewConfirmation。
9. 当前不是 Blogger 请求。
10. 当前不是 compaction pseudo-run。
11. 当前不是 Authority Root 后第一请求。
12. 上一轮未发生用户打断。
13. 当前 Primary 没有另一个 Strength 批次在执行。
14. Replica 会话和 Companion 关联可被唯一证明。
15. Host canary 全部通过。

任一条件不满足，强制 `K0`。

## STRENGTH-034：第一版角色范围

第一版建议启用：

```text
Coder
Inspector
DevOps
Meditator
```

第一版关闭：

```text
Orchestrator
Manager
Reviewer
Blogger
Executor
Browser
```

Reviewer 的 verdict 因果合同、Browser 的外部读取稳定性需要单独评审后再开放。

---

# 十二、主会话执行流程

## STRENGTH-035：Xm transform 主流程（两阶段）

```text
Xm 的 messages.transform 开始
→ 绑定本次 Primary ProviderRunIdentity
→ Fold 得到不可变 ProjectionSnapshot
→ 检查是否已有该 ProviderRunIdentity 的已提交 StrengthDecision
    ├─ 有：确定性重渲染
    └─ 无：计算 Eligibility 与 K
→ K=0：正常编译 Xm projection 并返回
→ K>0：
    → 获取该 Xm 的 single-flight StrengthReplica
    → 用 Xm 最新语义快照启动或恢复 Xs
    → 等待 Xs 收割 0..K 个工具请求批次
    → 验证并提交 StrengthFrameCandidateCommitted
      （候选只允许渲染给本次 ProviderRunIdentity）
    → 将 Candidate 帧叠加到 Xm projection
    → 生成 seal
    → 返回最终消息

★ 主模型请求成功完成后（非 Failed/Aborted）：
    再次提交 StrengthFramesPromoted
    → 帧变为活动历史，后续 attempt 继续渲染

★ 主模型请求 Failed / Aborted / 无法证明发出：
    不提交 Promotion
    → 候选帧不进入后续投影
    → blob 成为可清理未引用资源
```

Xm transform 在等待期间不得持有阻塞其他 Session 的全局锁。

## STRENGTH-036：失败开放边界

以下情况发生且尚未产生 CommitUnknown 时，Xm 必须直接退化为正常主模型请求：

```text
预测器异常
Replica 创建失败
Replica provider 失败
Replica text-out 且没有工具批次
Replica 工具批次超出字节上限
Replica transform 绑定不唯一
Replica 投影编译冲突
等待超时
用户打断
锚点失效
```

不得把这些情况计入主会话 Fallback cursor。

---

# 十三、Replica 执行流程

## STRENGTH-037：Bootstrap

当 Xs 不存在、已经 text-out 或已经被维护性丢弃时，使用 transport-only prompt 启动一个新 turn。

该 prompt：

1. 只负责让 Host 创建 provider 请求。
2. bootstrap 的物理 user 消息可以是 transport-only，但驱动 Replica 工作的领域事件必须有 Authority Root。
3. 在 Xs transform 中从 provider-visible projection 删除。
4. 不得被弱模型看到。
5. 不进入有效语义事件流。

### Replica 的 Authority Root

```fsharp
type AuthorityRoot =
    | HumanRoot of ...
    | AgentOwnerRoot of ...
    | StrengthReplicaRoot of
        primarySessionId: SessionId *
        replicaSessionId: SessionId *
        replicaEpoch: int64
```

### Prompt Origin

```fsharp
type PromptOrigin =
    | ...
    | StrengthReplicaBootstrap of StrengthReplicaRoot
```

### 请求种类

```fsharp
type ProviderRequestKind =
    | WorkMain
    | BloggerMain
    | BloggerSquash
    | InteractionRepair
    | StrengthReplicaMain
```

### AttemptExecutionProfile 扩展

```fsharp
type ExecutionSurface =
    | FullRoleSurface
    | StrengthReadOnlySurface

type AttemptExecutionProfile =
    { ...
      RequestKind: ProviderRequestKind
      ExecutionSurface: ExecutionSurface
      StrengthDecisionId: string option }
```

物理 bootstrap user message 可在 wire projection 中删除，但不能说它背后「没有 Authority Root」。
PromptDispatcher 的 claim、submit、physical accept 和 PROMPT-011 恢复仍须完整执行。

### Replica 最终看到的投影

```text
Xs 自己的固定 system prompt
+ 从共享语义事件构造的最新镜像
+ Xs 当前批次已经产生的本地工具 parts
```

## STRENGTH-038：预算执行点

预算计数只发生在 `Xs.messages.transform`。

每次 transform 恰好代表下一次 provider 请求即将被发送。

```text
预算尚未耗尽：
    返回投影，允许请求发送。

已经收割 K 个工具型请求：
    不返回投影。
    将第 K+1 次 transform 挂起。
```

禁止在 `tool.execute.after` 中维护预算计数。

## STRENGTH-039：工具型请求收割

一个 Replica provider 请求完成后：

```text
有一个或多个工具调用
→ 等待该请求全部工具结果完成
→ 形成一个候选帧（Candidate frame）
```

同一请求中的并发调用按 canonical call order 渲染。

禁止根据工具完成先后顺序改变帧顺序。

## STRENGTH-040：自然 text-out

Replica 在已收割 `j < K` 个工具请求后 text-out：

```text
丢弃本次 Replica 的全部正文与 reasoning
提交此前已经完成的 j 个候选帧
解除 Xm 等待
将 Xs 标记为需要下次 bootstrap
```

`j=0` 时等价于本次预测白跑，Xm 正常执行。

## STRENGTH-041：预算耗尽后的挂起

Replica 已收割 K 个工具请求后，下一次 transform 默认挂起。

挂起状态是 runtime continuation，不是持久领域状态。

```text
本批次结束
→ 提交前 K 个工具请求帧
→ Xm 继续
→ Xs 的第 K+1 次 transform 保持 pending
```

下一次 Strength 触发时：

```text
取 Xm 最新共享语义快照
→ 恢复 pending transform
→ 用最新投影返回该请求
→ 开始新的 K 预算
```

挂起本身提供 per-Xm single-flight。

### 复用限制

Xs pending transform 只能由同一个组合恢复：

```text
PrimarySessionId
PrimaryLogicalRunId
PrimaryAuthorityRootUserMessageId
```

发生以下任一事件时必须取消 pending transform：

```text
Xm 接受新的 HumanRoot
Xm 接受新的 AgentOwnerRoot
Xm 被删除
Xm 发生 Host compaction reanchor
Xs 的 ProviderRunIdentity 无法唯一绑定
pending 超时
插件 dispose
```

下一次触发重新 bootstrap。

这保证 Xs pending assistant 的物理 parent user message 不会因镜像新的用户消息而失效。

### 跨 Authority Root 仍保留缓存

如果希望跨用户 turn 保留 Xs 的缓存，可以保留已经完成的 Xs 历史和 Ys，但：

```text
保留 Session 和 PrefixEpoch
取消 pending assistant
新 PromptKey bootstrap 新 turn
```

不能复用旧的 pending provider run。

## STRENGTH-042：挂起安全阀

必须配置固定上限（见 PolicyConstants）：

```fsharp
// PolicyConstants.Strength.ParkedTransformLifetime
TimeSpan.FromMinutes 10.0
```

超时后：

```text
取消或 abort Xs 当前物理 run
丢弃所有未提交 parts
保留已经提交的候选帧（CandidateOnly）
下次重新 bootstrap
```

该 abort 是 Replica 维护动作，不是主工作会话失败。

## STRENGTH-042B：Replica 的恢复策略（非标准 Fallback）

Z_X 不进入标准 Fallback cursor。

```fsharp
type RecoveryPolicy =
    | StandardFallback
    | CompanionProbeOnly     // Y_X
    | NoRecovery             // Z_X
```

Z_X 使用 `NoRecovery`：

```text
StrengthReplicaMain Failed / Aborted
→ 本 Strength 决策失败
→ 丢弃未提交候选
→ X 正常请求
```

Replica 不允许使用 PeerAgent 切换到昂贵主模型。Z_X 无 Companion，故无前缀 probe。Z_X 失败不推进 X 的 FallbackCursor。

Strength 预测错误、text-out、无工具结果或帧丢弃不推进任何 fallback cursor。

## STRENGTH-043：宿主不支持长挂起

默认实现要求长 pending transform canary 通过。

若 Host 不支持，功能必须：

```text
默认关闭
```

受控 abort 截断模式可以作为后续独立实现，不得在没有专门条款和 canary 的情况下静默启用。

---

# 十四、共享语义事件

## STRENGTH-044：统一事件身份与游标

系统内部不得通过复制消息文本维持 Xm/Xs 镜像。

所有可共享内容先转化为稳定语义事件。

由于 Strength 引入不属于物理 transcript 的 DelegatedFrame，原来的 `TurnIndex + PartIndex` 不足以描述全部有效语义历史。全局改为统一事件游标：

```fsharp
type SemanticEventCursor =
    { SessionId: SessionId
      EventOrdinal: int64
      EventDigest: string }
```

该游标追溯适用于：

```text
Companion IngestCursor
Coverable cutoff
PrefixSnapshot coverage
BloggerDelta 起止位置
Candidate/Promoted 帧 coverage
Host reanchor 后的新 timeline 定位
```

物理消息仍保留自己的 MessageId/PartId；SemanticEventCursor 是投影和 coverage 的统一坐标，不替代 Host 物理身份。

必须同步修改的关联条款：

```text
CTX IngestCursor → SemanticEventCursor
BlogEntryCommitted：记录 Previous/NextSemanticCursor
PrefixSnapshot：记录 CoveredSemanticCursor
PrefixRebaseCommitted：重新验证 CoveredSemanticDigest
BloggerDeltaProjection：按统一事件流取 delta
```

核心语义事件：

```fsharp
type SemanticEvent =
    { EventId: SemanticEventId
      EventOrdinal: int64
      OriginTimeline: TimelineId
      AnchorEventId: string option
      Kind: SemanticEventKind
      CanonicalPayloadDigest: string
      CanonicalPayload: SemanticPayload }
```

## STRENGTH-045：Origin 只在内部存在

`OriginTimeline` 用于：

```text
防重复
防反射
审计
Fold
帧归属
```

它不得进入 provider-visible projection。

## STRENGTH-046：No Reflection（两条保证）

`Z(X, Y)` 使 `L(X)` 进入 Z_X 的投影，出现原文案没有的回路：Z_X 工具结果 → F•(X) → Π(X) → Y_X 消化 → L(X) → Z_X 下一次投影。该路径上 EventId 已经消失（内容被 Blogger 摘写成散文），故按 EventId 去重抓不到。裁决如下：

### 保证一：字节层去重

同一 EventId 在同一投影中最多渲染一次。按 EventId 去重，不按文本内容去重。

### 保证二：提升门控

```text
F°(X) ∩ L(X) = ∅
```

候选帧不得进入工作日志，因而不得经 Y_X 回流到 Z_X。已提升帧走该回路是正确行为——promote 时刻内容归属已从 Z_X 转移给 X（STRENGTH-048），Y_X 消化 X 的历史正是其职责。故 F•(X) ⊆ L(X) 允许。

以下循环必须被结构性阻止：

```text
Z_X 事件
→ X Candidate/Promoted 帧
→ X 镜像到 Z_X
→ 再次成为 Z_X 新事件
```

## STRENGTH-047：时间线视图

X 与 Z_X 从同一语义事件图构造不同视图：

```text
X 可见：
    主物理 transcript 的有效语义事件
    + 已提交并适用的 F•(X)（提升帧）

Z_X 可见：
    X 可共享的有效语义事件
    + Z_X 自己尚未提交的本地工具 parts
    + FrozenRecordPrefix 即 L(X) ↾ cut(ε(X))
    - transport-only prompt
    - 已按相同 EventId 出现的重复帧
```

---

# 十五、DelegatedRequestFrame

## STRENGTH-048：两阶段提交流程

DelegatedFrame 不再一次性成为永久活动帧。采用「候选→消费→提升」两阶段语义：

### 阶段一：Candidate

```fsharp
type StrengthFrameCandidateCommitted =
    { DecisionId: string
      PrimarySessionId: SessionId
      FirstVisibleProviderRunId: ProviderRunIdentity
      PrimaryInputSealDigest: string
      FrameRefs: BlobRef list
      FrameDigests: string list
      Anchor: SemanticAnchor }
```

该候选只允许渲染给 `FirstVisibleProviderRunId`，不能自动渲染给后续 attempt。

### 阶段二：Promotion

主模型请求确实消费候选后，提交提升事实：

```fsharp
type StrengthFramesPromoted =
    { DecisionId: string
      ConsumingProviderRunId: ProviderRunIdentity
      ConsumingInputSealDigest: string
      FrameDigests: string list
      PromotionEvidence: StrengthConsumptionEvidence }
```

### 状态转换

```text
候选请求成功消费
→ FramesPromoted
→ 后续请求继续渲染

候选请求 Failed / Aborted / 无法证明发出
→ 不 promote
→ 后续请求不渲染
→ 候选 blob 成为可清理未引用 blob
```

### 消费证明

“消费成功”不能只看 prompt receipt。至少必须证明：

1. ProviderInputSeal 包含这些 frame digest；
2. seal 已绑定到目标 ProviderRunIdentity；
3. provider run 实际产生了可证明的 provider 输出或完整 Host outcome。

这相当于 Strength 版 PrefixProbe：不是回滚，而是失败的候选从未进入活动投影。

## STRENGTH-049：提交前验证

### Candidate 阶段验证

候选帧提交必须同时满足：

```text
所有工具属于 StrengthReadOnlySurface
所有工具调用的 SyntheticToolCallId 唯一
所有结果能绑定到相同 SyntheticToolCallId
请求已经 terminal 或其全部工具 parts 已完整收敛
canonical renderer 成功（BlobRef digest 验证通过）
字节数不超过 MaxDelegatedBatchBytes
Replica ProviderRunIdentity 唯一
Primary anchor digest 仍然有效
Ordinal ≤ Requested K
```

任一条件不满足，整帧不提交。

### Promotion 阶段验证

提升验证必须证明：

```text
存在对应的 StrengthFrameCandidateCommitted
主模型的 ProviderInputSeal 包含所有候选帧 digest
seal 已绑定到与 FirstVisibleProviderRunId 一致的 ProviderRunIdentity
provider run 产生了可证明的 provider 输出或完整 Host outcome
同一 DecisionId 尚未被 promote
```

## STRENGTH-050：Canonical Renderer（两阶段一致性）

Replica 本地历史中的工具调用/结果与 Xm 中 StrengthFrameCandidate 的渲染必须调用同一个 renderer。

要求：

```text
相同 SemanticPayload
→ 相同 role
→ 相同 part 类型
→ 相同参数序列化
→ 相同结果序列化
→ 相同 UTF-8 LF 字节
```

禁止分别维护“Replica renderer”和“主会话 frame renderer”。

### 跨 Session 合成 ID（用于候选帧渲染）

不能假设 Xs 的物理 messageID/toolCallID/partID 在 Xm 中仍然唯一或合法。

必须按确定性映射生成合成 ID：

```text
SyntheticAssistantMessageId =
    hash(PrimarySessionId, DecisionId, FrameOrdinal, "assistant")

SyntheticToolCallId =
    hash(PrimarySessionId, DecisionId, FrameOrdinal, CallOrdinal, "call")

SyntheticToolResultMessageId =
    hash(PrimarySessionId, DecisionId, FrameOrdinal, "result")
```

调用和结果在 Xm 中使用同一个 synthetic call ID。

因此“与 Xs 物理字节完全一致”应改为：

> 去除 transport identity 后，工具名、规范化参数和结果 payload 完全一致；
> 目标时间线的 message/part/call identity 由确定性映射生成。

Promotion 阶段使用同样的 synthetic ID（基于同一 DecisionId 和 FrameOrdinal），确保候选和提升后的帧在 ID 空间上一致。

## STRENGTH-051：只追加（针对已提升帧）

已提交且提升的 Candidate 帧（即 `StrengthFramesPromoted` 中的帧）是 append-only 事实。

禁止：

```text
提升后编辑 frame
提升后删除其中一部分调用
提升后清理被认为无用的读取
通过 transform 掩码回滚 frame
```

CandidateOnly 状态的帧不在此限制内——它们可以被自然丢弃（主模型 Failed/Aborted 后不再渲染）。
后续 prefix rebase 可以由 Companion 工作日志覆盖对应前缀，但不得修改已提升历史事实。

---

# 十六、持久化

## STRENGTH-052：唯一核心事实

每次 eligible 决策最终提交一到两个事实：

```fsharp
type StrengthDecisionStatus =
    | CandidateOnly  // 候选已提交，等待 promotion
    | FullyPromoted  // 候选已被消费提升
    | CandidateDiscarded  // 候选未提升，已废弃

type StrengthDecisionCommitted =
    { DecisionId: string

      PrimarySessionId: SessionId
      PrimaryProviderRunIdentity: ProviderRunIdentity
      PrimaryAnchorDigest: string

      ReplicaSessionId: SessionId option
      ReplicaProviderRunIdentities: ProviderRunIdentity list

      RequestedBudget: StrengthBudget
      HarvestedRequestCount: int

      ProbabilityRead1: float
      ProbabilityRead2: float
      Value0: float
      Value1: float
      Value2: float

      RawTendency1: float
      RawTendency2: float

      InclusionProbability1: float
      InclusionProbability2: float

      CandidateFrameRefs: BlobRef list
      CandidateFrameDigests: string list
      CandidateByteLength: int64

      PromotedFrameDigests: string list option
      Status: StrengthDecisionStatus

      PredictorVersion: string
      ControllerVersion: string }
```

## STRENGTH-053：两阶段提交时点

### 阶段一：Candidate 提交

`StrengthFrameCandidateCommitted` 必须在 Xm transform 返回包含这些帧的最终 projection 之前写入 Journal。

```text
Replica 完成
→ canonicalize
→ validate
→ Journal commit StrengthFrameCandidateCommitted
→ Xm projection render（含候选帧）
→ Xm transform 返回
```

禁止先把帧发给主 provider，再补写事实。

### 阶段二：Promotion 提交

主模型请求成功完成后（非 Failed/Aborted），在同一个因果事务中提交：

```text
主 provider 返回成功
→ 验证 ProviderInputSeal 包含候选帧 digest
→ 证明 provider run 产生了完整 Host outcome
→ Journal commit StrengthFramesPromoted
→ 候选帧变为活动历史
```

如果主模型请求中途 Failed / Aborted / 无法证明发出：

```text
不提交 StrengthFramesPromoted
StrengthDecisionCommitted.Status = CandidateDiscarded
候选降级为可清理未引用 blob
```

## STRENGTH-054：CommitUnknown

Journal append 返回 `CommitUnknown` 时必须 fail closed。

禁止：

```text
重新运行 Replica 以确保有结果
假设事实没有写入
把未确认帧返回给 Xm
```

必须先 reconcile Journal，证明事实是否存在。

## STRENGTH-055：Retry 幂等

同一个 `PrimaryProviderRunIdentity` 的 transform 重入时：

```text
找到已有 StrengthDecisionCommitted
→ 检查 Status：
   ├─ FullyPromoted：使用已提交 canonical bytes 重渲染（标准重放）
   ├─ CandidateOnly：该候选从未被证明消费过，不渲染候选帧
   └─ CandidateDiscarded：同上，不渲染
→ 验证 anchor
→ 不重新运行 Replica
→ 不重新抽样 IncludedInTraining
```

只有 `FullyPromoted` 状态下的候选帧才能在重入时继续渲染。
`CandidateOnly` 帧在重入时不可见——如果新的 attempt 需要 Strength，必须重新运行 Replica。

## STRENGTH-056：Fold 规则与积分状态

Fold 必须以 O(1) 积分状态回答：

```text
某 Primary 是否有关联 Replica
当前是否有适用的 Candidate/Promoted 帧
当前 ControllerState
当前 n-gram 计数快照引用
最近 eligible 决策
```

正常 projection 查询不得扫描完整 Journal。

预测计数表可以：

```text
由 Journal Fold 重建
或使用可丢弃的可再生快照加速
```

可再生快照不是事实源。

### 各事实的 Fold 验证规则

```text
StrengthReplicaLinked：
    Primary 当前没有活跃 Replica
    Replica/Companion Session 均唯一
    model/role 配对合法

StrengthFrameCandidateCommitted：
    DecisionId 未出现
    FirstVisibleProviderRunId 唯一
    所有 blob digest 重验通过
    frame count ≤ requested K
    每批字节 ≤ 上限
    工具全部属于 StrengthReadOnlySurface

StrengthFramesPromoted：
    存在对应 candidate
    ProviderInputSeal 包含全部 frame digest
    ConsumingProviderRunId 与 candidate 一致
    同一 candidate 只 promote 一次

StrengthTrainingInclusionCommitted：
    对应 frame 已存在
    probability 与 decision 中冻结值一致
    抽样结果只接受一次
```

---

# 十七、崩溃与恢复

## STRENGTH-057：未提交工作

以下内容不是持久事实：

```text
挂起的 continuation
Replica 未完成 assistant parts
未完成工具结果
尚未 commit 的候选帧
内存中的等待者
```

进程崩溃后全部丢弃。

## STRENGTH-058：启动恢复（区分两阶段状态）

Boot Fold 后：

```text
已提交 StrengthDecision（FullyPromoted）：
    按 anchor 和适用规则恢复投影；
    已提升的帧继续渲染。

已提交 StrengthDecision（CandidateOnly / CandidateDiscarded）：
    候选帧不渲染；
    候选 blob 成为可清理未引用资源。

存在物理 busy Replica，但没有对应已提交批次：
    abort 或删除该 Replica run。
    从 Xm 最新语义投影重建。

挂起 transform：
    不恢复协程。
    下次需要时重新 bootstrap。
```

这符合“恢复事实，不恢复暂停协程”的原则。

崩溃恢复时的核心原则：

```text
只有被证明已消费（FullyPromoted）的帧才进入活动投影。
CandidateOnly 状态在崩溃后视同从未存在。
```

## STRENGTH-059：Anchor 失效

渲染候选帧或已提升帧时，如果无法证明其 anchor 仍属于当前有效语义时间线：

```text
Candidate 帧：不渲染，丢弃候选
Promoted 帧：不渲染，保留 Journal 事实供审计
记录 StrengthFrameInapplicable 诊断
```

不存在回滚事实。

## STRENGTH-060：用户打断

Xm 等待 Replica 时发生用户打断：

```text
取消当前等待
取消 Replica 未提交工作
不提交候选帧
Xm 按 Host 的正常打断语义处理
```

已经提交的帧按普通历史事实处理，不因后续打断回滚。

---

# 十八、与现有机制的边界

## STRENGTH-061：Fallback

Strength 不得读写：

```text
FallbackOffset
ConsecutiveFailureCount
AutoRecoveryBudget
SelectedAgent
EffectiveAgent
```

Replica 失败也不得推进主会话 Fallback cursor。

## STRENGTH-062：PrefixProbe

当前 Xm attempt 使用 PrefixProbe 时强制 `K0`。

原因是：

```text
PrefixProbe 的成功或失败承担上下文恢复因果含义。
Strength 注入会改变该 attempt 的输入和归因。
```

Xs 可以独立使用 Ys 的 prefix probe，但不得把其结果提升为 Xm 的 PrefixEpoch。

## STRENGTH-063：Review

以下请求强制 `K0`：

```text
ReviewConfirmation
包含 skeptical challenge 的确认请求
verdict 相关请求
Reviewer 正在建立双 PERFECT 因果链的请求
```

Strength 不得改变 ProviderInputSeal 对 challenge 证据的证明。

## STRENGTH-064：Companion ingestion（两阶段协调）

Xm 的 ProviderSemanticProjection 必须包含适用的候选帧（Candidate）或已提升帧（Promoted）。

Ym 的 delta ingestion 需要区分：

```text
Candidate 阶段：
    Xm projection 包含候选帧，Ym 记录这些帧的 SemanticEventCursor
    但这些帧尚不是活动历史（还未 promote）
    Ym 不应将这些帧写入 FrozenRecordPrefix，因为它们可能被丢弃

Promoted 阶段：
    帧变为活动历史
    Ym 的 delta ingestion 必须看到这些帧
    后续 prefix rebase 不会丢失旁路读取事实
```

同样的规则适用于 Xs/Ys 按 Xs 自己的有效语义投影工作。

## STRENGTH-065：Context Recovery

Strength 不测量上下文窗口，不预测是否接近上限。

`ExpectedBytes` 和 `MaxDelegatedBatchBytes` 只用于：

```text
成本函数
固定输入安全合同
```

不得用于：

```text
切换 PrefixEpoch
主动 squash
推导剩余上下文
按模型窗口选择 K
```

---

# 十九、投影 DSL（全局架构，不属于 Strength 子模块）

> 根据最终审阅裁决，Projection DSL 提升为全局基础设施 spec/16 — Projection Algebra（条款前缀 `PROJ-`）。
> 以下条款是 STRENGTH 对 DSL 的使用规范。完整 DSL 定义见 spec/16。

## STRENGTH-066：采用 DSL/组合子

所有 provider-visible projection 的唯一生产路径是 Projection DSL。

禁止各功能直接接收并任意修改 `Message list`。

目标是让审阅者从程序结构直接看到：

```text
当前 projection 使用了哪些事实
在哪些锚点插入了哪些帧
哪些运输消息被隐藏
哪个 Epoch 生效
最终 seal 覆盖了什么
```

### 统一中间表示

DSL 不直接在 Message list 上工作。核心输入为：

```fsharp
type ProjectionSnapshot =
    { Attempt: AttemptExecutionProfile
      PhysicalTimeline: PhysicalTimeline
      SemanticEvents: SemanticEventTree
      ActivePrefixEpoch: ActivePrefixEpoch
      CandidatePrefixProbe: PrefixProbe option
      BlogFrames: BlogFrame list
      DelegatedFrames: DelegatedFrame list
      HostReanchor: ContextReanchorSnapshot option
      LocalPendingParts: LocalPendingParts
      TransportMessages: Set<MessageId> }
```

核心输出依次为：SemanticEventTree → ProviderSemanticProjection → ProviderWireProjection → ProviderInputSeal。

## STRENGTH-067：DSL 与领域状态边界

DSL 只负责：

```text
不可变 ProjectionSnapshot
→ 确定性 provider-visible projection
```

DSL 不负责：

```text
启动 Replica
等待 provider
执行工具
写 Journal
恢复 Prompt
管理 ProviderRunIdentity
推进生命周期状态
控制器在线更新
```

这些职责属于 effectful coordinator、PromptDispatcher、Reconciler 和 Journal Fold。

## STRENGTH-068：三层结构

实现必须分为：

```text
Effectful Coordinator
    读取 Host、等待 Replica、生成不可变快照

Pure Projection Planner
    汇总各功能 ProjectionIntent、排序、冲突检查

Canonical Renderer
    渲染最终 provider wire bytes、生成 digest/seal
```

## STRENGTH-069：ProjectionIntent

建议最小代数：

```fsharp
type ProjectionIntent =
    | SelectBaseTimeline of TimelineId

    | ApplyHostReanchor of ContextReanchorSnapshot

    | ReplacePrefix of
        epoch: ActivePrefixEpoch *
        cutoff: SemanticAnchor *
        replacement: ProjectionFrame

    | InsertAfter of
        anchor: SemanticAnchor *
        frames: ProjectionFrame list

    | OverlaySharedEvents of
        events: SemanticEvent list

    | IncludeLocalPendingParts of
        providerRunId: ProviderRunIdentity

    | SuppressTransport of
        physicalMessageId: MessageId

    | RequireInvariant of
        ProjectionInvariant
```

## STRENGTH-070：固定阶段（按 Writeback 分组）

投影阶段顺序必须显式：

```text
1. 读取物理 base timeline
2. 应用 Host reanchor
3. 应用 ActivePrefixEpoch
4. 叠加共享语义事件
5. 所有 Writeback = ReplacePrefix 的卫星贡献（按 Precedence 全序）
6. 所有 Writeback = InsertAfter 的卫星贡献（按 Precedence 全序）
7. 加入当前 Session 的本地 pending parts
8. 删除 transport-only 消息
9. canonicalize
10. 生成 ProviderInputSeal
```

顺序不得由插件注册顺序隐式决定。同类 Writeback 内的卫星全序由 `SatelliteContract.Precedence` 显式声明。

## STRENGTH-071：Typed Stage

建议使用 phantom type 限制顺序：

```fsharp
type Raw
type Reanchored
type Prefixed
type Shared
type Overlaid
type Cleaned
type Sealed

type Projection<'stage>
```

示意接口：

```fsharp
val fromRaw:
    ProjectionContext ->
    Projection<Raw>

val applyHostReanchor:
    Projection<Raw> ->
    Result<Projection<Reanchored>, ProjectionError>

val applyPrefixEpoch:
    Projection<Reanchored> ->
    Result<Projection<Prefixed>, ProjectionError>

val overlaySharedEvents:
    Projection<Prefixed> ->
    Result<Projection<Shared>, ProjectionError>

val overlayDelegatedFrames:
    Projection<Shared> ->
    Result<Projection<Overlaid>, ProjectionError>

val suppressTransport:
    Projection<Overlaid> ->
    Result<Projection<Cleaned>, ProjectionError>

val seal:
    Projection<Cleaned> ->
    Result<Projection<Sealed>, ProjectionError>
```

## STRENGTH-072：主/旁路投影程序

```fsharp
let primaryProjection =
    fromRaw
    >> applyHostReanchor
    >> applyPrefixEpoch
    >> overlaySharedEvents
    >> overlaySatelliteContributions     // 按 Writeback 分组，非硬编码
    >> includePrimaryLocalParts
    >> suppressInternalTransport
    >> seal

let replicaProjection =
    fromRaw
    >> applyHostReanchor
    >> applyPrefixEpoch
    >> overlaySharedEvents
    >> includeReplicaLocalParts
    >> deduplicateSemanticEvents
    >> suppressBootstrapPrompt
    >> seal
```

## STRENGTH-073：冲突规则

Planner 发现以下情况必须 fail closed：

```text
两个 ReplacePrefix 修改相同区域
两个不同 payload 使用相同 EventId
InsertAfter 的 anchor 不存在或不唯一
Candidate/Promoted 帧与本地 pending part 重复
同一 SyntheticToolCallId 对应不同结果
transport suppression 目标不是 transport-only prompt
任何操作试图修改 sealed prefix 内字节
```

禁止通过“后注册规则覆盖先注册规则”解决冲突。

## STRENGTH-074：投影定律

必须通过 property tests 证明：

```text
Determinism
同一快照产生完全相同字节。

Idempotence
重复应用同一帧不产生重复内容。

No Reflection
Xs 来源事件不会经 Xm 再反射回 Xs。

Frame Uniqueness
同一 EventId 最多渲染一次。

Anchor Stability
同一 Epoch 内锚点解释稳定。

Seal Stability
同一 Epoch 的 sealed prefix 字节不变。

Explicit Loss
Wire → Semantic → BloggerDelta 的有损转换显式命名。

Retry Stability
相同 ProviderRunIdentity 重入不重新执行 Replica。

Conflict Rejection
重叠修改 fail closed。
```

---

# 二十、Prompt 与身份

## STRENGTH-075：统一 PromptDispatcher

以下物理 prompt 全部必须经过统一 PromptDispatcher：

```text
Xm 普通请求
Xs bootstrap 请求
Xs 恢复请求
Ym/Ys Companion 请求
```

Strength 不得绕开 Prompt Claim/Submit/Accept 身份链。

## STRENGTH-076：Transform 身份绑定

Xm 和 Xs 的 transform 都必须按 Host 的唯一因果判据绑定 `ProviderRunIdentity`。

命中零个或多个候选时：

```text
不运行 Strength
不提交帧
不生成 Strength seal
```

不得通过 Session 尾部、时间接近或 same-root 猜测身份。

## STRENGTH-077：Input Seal

ProviderInputSeal 建议扩展：

```fsharp
type ProviderInputSeal =
    { ...
      IncludedCandidateFrameDigests: Set<string> }
```

这样可以证明某个主 provider 请求确实看到了哪些候选帧。

该证据用于审计和指标，不用于 Review PERFECT 判定，除非 Review SSOT 另行修改。

---

# 二十一、Host 门禁

## STRENGTH-077B：模型数据边界门禁

主动把仓库上下文和文件内容发送给 Peer 模型，可能改变：

- provider；
- 数据驻留区域；
- retention 政策；
- 企业许可边界。

即使普通 Fallback 可以使用 Peer，也不自动意味着允许主动投机请求。

```fsharp
type StrengthTrustPolicy =
    { PrimaryModel
      ReplicaModel
      AutomaticDelegationAllowed: bool
      DataBoundaryId: string }
```

只有同时满足以下条件才能启用 Strength：

```text
AutomaticDelegationAllowed = true
且
DataBoundaryId 相容（主模型与 Replica 属于同一数据边界）
```

该门禁在 Host canary 之前检查。

## STRENGTH-078：启用前必过 canary

以下 canary 必须全部通过。状态与判据按调研实读结论分类（注：`plugin.trigger` 无超时、Effect.promise 不响应 fiber 中断，超时与取消责任全在插件侧）。

### Transform 顺序与身份

```text
C-01  每个 provider 请求恰好触发一次 messages.transform
      （prompt.ts step 循环内，已验证）

C-02  agentic tool loop 的后续请求也触发 transform（同上）

C-03  transform 可以异步等待（plugin.trigger 对每个 hook await Promise，已验证）

C-04  某 Session 的驻留 transform 不阻塞其他 Session
      ——硬门槛，不通过则功能默认关闭

C-05  驻留 transform 可持续到 ParkedTransformLifetime
      ——硬门槛，不通过则下调常量至实测可达值

C-06  恢复驻留 transform 后 provider 请求仍可正常发出

C-07  transform 返回时 ProviderRunIdentity 绑定仍然唯一
      （HOST-010 判据；命中 0 或 ≥2 时 fail closed）

C-08  transform 内发起另一个 Session 请求不形成锁反转

C-09  删除 Session 或卸载插件会取消驻留 transform
      判据：dispose 后 Z_X 不得再产生任何 journal 写入
      注：不可依赖 Effect 中断语义，必须显式 resolve 驻留 promise

C-10  transform retry 不导致 Z_X 重跑
```

### 投影与渲染

```text
C-11  bootstrap transport prompt 可从 provider-visible projection 安全删除

C-12  同一工具 part 经本地历史与候选帧两条路径渲染时字节一致
      （STRENGTH-050 单一 renderer）

C-13  并发工具调用的 canonical 顺序稳定（STRENGTH-039）

C-14  F°(X) ∩ L(X) = ∅
      候选帧不得进入工作日志，不得经 Y_X 回流到 Z_X

C-15  未提升候选帧在同一 ε(X) 内消失不触发 seal barrier 违规
      （COMPANION-009 attempt-local 豁免）
```

### 权限与角色

```text
C-16  session 级 deny ruleset 使非只读工具从 provider-visible schema 消失
      判据：write/edit/apply_patch/executor/fork-*/join/list/verdict 及网络工具
      均不出现在 Z_X 的 schema 中

C-17  Z_X 的 ruleset 不产生任何 ask
      判据：读 .env、读 worktree 外路径均直接 deny，无权限事件发布

C-18  fail-closed 形态为 ∅ 或 {_noop}

C-19  system.transform 注入的 Replica prompt 生效
      判据：Agent = fast-replica/deep-replica 时，最终 provider-visible system prompt
      等于 systemPromptOf(Replica)，且不含预算/深度/停止条件/成本身份语言

C-20  Replica 角色不出现于任何模型可见面
```

### 升级

```text
C-21  Host upgrade 后以上 canary 先于生产发布运行重新验证
```

```text
任一 canary 失败 → Strength 默认关闭。
C-04 或 C-05 未通过 → 不进入阶段 D 及之后。

---

# 二十二、配置

## STRENGTH-079：统一代码常量（PolicyConstants）

不新增 Strength 配置文件、TOML 配置节、环境变量或用户设置。

所有策略常量集中在一个生产代码文件 `src/PolicyConstants.fs` 中。Strength 不得在多个实现文件中散落数字字面量。

Replica Agent 的 model 字符串仍由现有 Host Agent 配置提供，除此以外不增加 Strength 运行时配置面。

### 第一版 best-guess 常量

```fsharp
module PolicyConstants.Strength

// Execution
[<Literal>]
let MaxDelegatedProviderRequests = 2

[<Literal>]
let MaxConcurrentReplicaDecisionsGlobal = 8

let EligibleRoles =
    set [ Role.Coder; Role.Inspector; Role.DevOps; Role.Meditator ]

let AllowedTools =
    set [ "read"; "glob"; "grep" ]

// Projection size
[<Literal>]
let MaxDelegatedBatchBytes = 64L * 1024L       // 64 KiB

[<Literal>]
let MaxDelegatedDecisionBytes = 96L * 1024L     // 96 KiB

// Timing
let ReplicaProviderRequestDeadline =
    TimeSpan.FromSeconds 45.0

let StrengthDecisionDeadline =
    TimeSpan.FromSeconds 75.0

let ParkedTransformLifetime =
    TimeSpan.FromMinutes 10.0

// Predictor
[<Literal>]
let NGramMaximumOrder = 3

[<Literal>]
let KneserNeyAbsoluteDiscount = 0.75

[<Literal>]
let MinimumRoleObservationsForK1 = 64L

[<Literal>]
let MinimumRoleObservationsForK2 = 256L

[<Literal>]
let CountDecayInterval = 4096L

[<Literal>]
let CountDecayFactor = 0.5

// Controller
[<Literal>]
let ControllerUpdateInterval = 128L

[<Literal>]
let ControllerEwmaHalfLife = 512.0

[<Literal>]
let ControllerMaximumProbabilityStep = 0.01

[<Literal>]
let InitialInclusionProbabilityK1 = 0.50

[<Literal>]
let InitialInclusionProbabilityK2 = 0.35

[<Literal>]
let MinimumInclusionProbabilityK1 = 0.05

[<Literal>]
let MaximumInclusionProbabilityK1 = 0.95

[<Literal>]
let MinimumInclusionProbabilityK2 = 0.05

[<Literal>]
let MaximumInclusionProbabilityK2 = 0.75

// Normalized utility
[<Literal>]
let PrimaryFastRequestValue = 1.00

[<Literal>]
let PrimaryDeepRequestValue = 3.00

[<Literal>]
let FastReplicaRequestCost = 0.15

[<Literal>]
let DeepReplicaRequestCost = 0.30

[<Literal>]
let ProjectedByteCostPerKiB = 0.003

[<Literal>]
let BlockingDelayCostPerSecond = 0.005

[<Literal>]
let IncorrectPathLossK1 = 0.35

[<Literal>]
let IncorrectPathLossK2 = 1.00

[<Literal>]
let MinimumPositiveDecisionValue = 0.05

[<Literal>]
let MinimumK2AdvantageOverK1 = 0.20
```

### 常量选择理由

**MaxDelegatedProviderRequests = 2**：一个请求代表一个新的调查判断点。第二步已经可能基于 Replica 自己形成的调查倾向，第三步风险不再属于机械预读。

**64 KiB / 96 KiB**：单批允许常见源文件和搜索结果进入投影，但限制并发读取造成的大规模上下文污染。现有系统的最低动态输入合同为 200 KiB；Strength 上限必须显著低于该合同。

**order = 3**：足以捕获 grep→read、glob→read、read→read 以及基本任务阶段变化；更高阶在统一角色桶中更容易形成稀疏计数。

**64 / 256 冷启动样本**：K1 的错误损失较低，可以较早开放。K2 需要更多角色级观测。

**每 4096 个符号衰减 0.5**：由于不按模型组合分桶，衰减承担模型升级、prompt 调整和任务分布变化的统一适应机制。

**控制器半衰期 512**：足以过滤单个仓库或短任务的偶然 read/write 波动，同时仍能在数百次 eligible 判断后发生明显迁移。

**ρ 不到达 0 或 1**：不是为了因果对照，而是避免控制器永久卡在不可恢复的饱和边界。

**MinimumK2AdvantageOverK1 = 0.20**：只有当第二步相对于 K1 存在明确净价值时才选择 K2。同时 `MinimumPositiveDecisionValue = 0.05` 确保净价值在该门槛下不启动——替代原 `argmax` 的「大于零即启用」规则，使决策规则与常量的定义位置对齐。

---

# 二十三、日志与指标

## STRENGTH-080：诊断日志

日志可以记录：

```text
session_id
decision_id
primary_provider_run_id
replica_provider_run_ids
requested_k
harvested_k
q1
q2
v0/v1/v2
raw_tendency_1/2
inclusion_probability_1/2
included_in_training_1/2
frame_bytes
duration
outcome
discard_reason
```

日志不是恢复协议。

## STRENGTH-081：核心运行指标

至少监控：

```text
Eligible 决策数
K0/K1/K2 比例
实际收割 0/1/2 比例
Replica 自然 text-out 比例
Replica 超时比例
Replica provider 失败比例
平均/分位投影字节
主模型重读相同路径比例
主模型立即重新 grep/glob 比例
K2 后转向不相交文件集合比例
估算模型成本变化
端到端延迟变化
Controller ρ1/ρ2
SmoothedTendency z1/z2
```

## STRENGTH-082：质量解释

由于不保留随机动作对照组，指标用于：

```text
发现明显回归
调节成本权重
调节风险权重
验证闭环是否稳定
```

不得把观察到的前后差异宣称为严格因果效果。

---

# 二十四、实现模块

## STRENGTH-083：建议模块划分

```text
SatelliteTypes.fs             卫星种类、契约与Descriptor（STRENGTH-083）
SatelliteRuntime.fs                               结构层共享运行时

StrengthTypes.fs             类型与判别联合
StrengthFacts.fs                                    Journal facts 与 envelope schema
StrengthFold.fs                                     O(1) 积分状态
StrengthPredictor.fs                                KN n-gram 与结构特征
StrengthController.fs                               ρ1/ρ2 负反馈控制器
StrengthValue.fs                                    V0/V1/V2 成本函数
ReplicaProgram.fs                                   Z_X 策略程序（受 SatelliteRuntime 托管）
StrengthCoordinator.fs                              X 等待、single-flight、提交编排

ProjectionDsl.fs                                    typed projection algebra
ProjectionPlanner.fs                                intent 合并与冲突检测
ProjectionRenderer.fs                               canonical wire renderer

PrimaryProjectionProgram.fs                         X 投影程序
ReplicaProjectionProgram.fs                         Z_X 投影程序

StrengthCanary.mjs                                  Host 门禁
StrengthPropertyTests.fs                            DSL 与 Fold 性质测试
```

## STRENGTH-084：禁止的代码形态

禁止：

```text
一个数百行 messages.transform 函数
用 mutable stage 字符串推进生命周期
在多个 hook 中分别修改同一消息数组
依赖插件注册顺序决定投影
通过字符串匹配识别 Strength 帧
使用全局可变 Map 作为唯一事实源
将 Replica continuation 持久化后尝试恢复协程
```

---

# 二十五、开发顺序

## STRENGTH-085：阶段 0——记号改革与卫星结构层

先于 Projection DSL 迁移。记号与结构不先稳定，后续工作会在旧记号上定型后再改一次。

```text
spec/99 规范符号表落地
[机械] 类改动批量执行，与 [语义] 类分离提交
ssot-lint 扩展退役符号检测
SatelliteTypes / SatelliteRuntime / SatelliteFold
既有 Companion 迁移到 SatelliteRuntime 托管
```

判据：退役符号残留数为零；Companion 行为无回归。

## STRENGTH-086：阶段 A——spec/16 Projection DSL 迁移

先完成 spec/16（Projection Algebra）的 DSL 实现与迁移：

```text
SemanticEvent
ProjectionIntent
typed stages
canonical renderer
conflict detection
property tests

追溯迁移所有旧投影（按 PROJ-008 顺序）
```

在 spec/16 迁移完成前，不实现生产 Strength transform。

### PROJ-008 迁移顺序

所有旧投影必须按以下顺序逐步迁移到 DSL：

```text
第一步：普通 X + ActivePrefixEpoch projection
第二步：attempt-local PrefixProbe projection
第三步：Companion BloggerMain / BloggerSquash / BloggerDelta projection
第四步：InteractionRepair projection
第五步：ReviewConfirmation + skeptical challenge Seal projection
第六步：Host compaction reanchor 后 projection
第七步：Strength Primary/Replica projection（含 transport-only suppression）
```

### 迁移纪律

1. 迁移期间测试环境可同时运行 LegacyProjection 和 DslProjection 并比较 canonical digest。
2. 生产环境不得按请求随机混用两套实现。
3. 切换条件：所有历史 canary 轨迹 LegacyDigest = DslDigest；允许有意变化的差异须有明确的新 SSOT 条款。
4. 切换后删除 LegacyProjection，不长期维护双实现。
5. **Strength 生产开关只能在前六步全部完成后启用。**

### 迁移纪律

本次迁移涉及对旧 SSOT 条款的处理。所有历史 SSOT 条目必须映射为以下之一：

```text
contract：当前仍有效的规范
migration：仅用于旧字段/旧流程/旧格式转换
deprecated：已废弃但仍需识别的旧定义
rejected：与现行规范冲突、禁止继续使用的定义
```

同一语义存在多个旧定义时，优先级为：
现行 DSL > 已批准迁移映射 > 最新 SSOT > 旧版说明。

## STRENGTH-087：阶段 B——Host canary

验证：

```text
transform 逐请求触发
跨 Session 不死锁
长 pending
恢复 pending
identity binding
canonical part rendering
dispose cancellation
```

失败则停止功能开发或选择经单独评审的替代方案。

## STRENGTH-088：阶段 C——Shadow Predictor

只计算：

```text
请求级符号
q1/q2
ExpectedBytes
V1/V2
建议 K
```

不启动 Z_X，不修改 projection。

验证：

```text
grep/glob 后的请求预测
EOT 预测
请求级而非工具级计数
字节预测误差
K2 比 K1 更保守
```

## STRENGTH-089：阶段 D——Replica Dry Run

启动 Z_X，但不投影到 X。

记录：

```text
Z_X 工具批次
与主模型随后工具批次的路径重合
Replica text-out
Replica 字节
Replica 延迟
```

该阶段是上线前验证，不要求长期保留随机对照。

## STRENGTH-090：阶段 E——K1 灰度

只开放：

```text
K0
K1
```

要求：

```text
无非只读工具执行
无 Replica 文本泄漏
无重复帧
无 PrefixEpoch/Fallback 污染
Controller 收敛而不振荡
成本代理总体非负
重读/重搜率在可接受范围
```

## STRENGTH-091：阶段 F——K2 灰度

K2 单独开关。

只有 K1 稳定后才开放。

K2 必须使用：

```text
独立控制器
更低概率上限
更高风险成本
更严格字节成本
独立监控
```

---

# 二十六、测试计划

## STRENGTH-092：预测器单元测试

覆盖：

```text
grep → read
glob → 并发 read
read → write
read → EOT
稀疏上下文回退
新 Session 冷启动
计数衰减
请求级批次 canonicalization
```

## STRENGTH-093：控制器仿真

至少使用以下人工 plant：

```text
单调线性 plant
S 形单调 plant
高增益 plant
带观测延迟 plant
模型分布突变
主/弱模型切换
```

断言：

```text
ρ 不持续卡死在 0 或 1
z 与 ρ 不无限振荡
更新步长受限
K1/K2 环相互独立
分布变化后重新进入稳定区间
```

## STRENGTH-094：Projection 性质测试

随机生成：

```text
物理 transcript
PrefixEpoch
DelegatedFrames
共享事件
本地 pending parts
transport prompts
```

验证 STRENGTH-074 的所有定律。

## STRENGTH-095：故障注入

覆盖以下崩溃点：

```text
Replica provider 返回前
工具批次部分完成
全部工具完成但未 canonicalize
canonicalize 后未 commit
Journal commit 期间
commit 后 Xm transform 返回前
Xm provider 请求发送后
pending transform 挂起期间
恢复 pending transform 时
```

每个点必须得到唯一恢复结果。

## STRENGTH-096：安全测试

必须证明：

```text
Replica schema 不含 write/edit
execution gate 再次拒绝 write/edit
伪造 SessionKind 时工具集为空
Replica 正文永不进入 Xm
来源 metadata 永不进入 provider projection
ReviewConfirmation 永远 K0
PrefixProbe attempt 永远 K0
```

---

# 二十七、验收标准

## STRENGTH-097：正确性门禁

生产启用前必须满足：

1. 所有 Host canary 通过。
2. 所有 Projection property tests 通过。
3. Replica 非只读执行次数为零。
4. Replica 用户可见文本泄漏次数为零。
5. 重复候选帧（Candidate frame）次数为零。
6. 相同 EventId 多次渲染次数为零。
7. Strength 导致 Fallback cursor 变化次数为零。
8. Strength 导致 Xm PrefixEpoch 非法变化次数为零。
9. CommitUnknown 后继续发送含候选帧请求次数为零。
10. crash replay 后 projection digest 不一致次数为零。

## STRENGTH-098：运行门禁

灰度扩大前必须满足：

```text
Controller 进入有界稳态
K1/K2 比例无持续发散
投影字节分位数受控
Replica timeout 低于配置门限
重读/重搜代理指标无明显恶化
总体模型成本代理为正收益或接近中性
端到端延迟没有不可接受回归
```

本条使用工程阈值，不要求严格因果显著性。

## STRENGTH-099：自动熔断

出现以下任一条件时自动关闭 Strength，主模型继续正常工作：

```text
Host canary 在升级后失败
Projection conflict 连续出现
CommitUnknown 无法 reconcile
Replica 非只读 gate 被触发
Frame digest 不一致
No Reflection 性质被破坏
pending transform 导致跨 Session 阻塞
Controller 概率持续振荡超出门限
```

关闭 Strength 不得影响普通 Work Session、Companion 或 Fallback。

---

# 二十八、明确拒绝的替代方案

## STRENGTH-100：拒绝同会话降强度

拒绝：

```text
在 Xm 同一物理 transcript 中临时切换到弱模型
→ 拦截写请求
→ 后续用 projection 清理被拒绝内容
```

原因：

```text
已提交事实需要回滚
中段历史发生可变分叉
缓存边界不再由明确 Epoch 事实控制
主模型会看到弱模型正文或拒绝残骸
恢复协议显著复杂化
```

## STRENGTH-101：拒绝无限只读连读

即使 Replica 结构上不能写，也不得无限运行。

原因：

```text
读取路径本身会形成调查假设
steering 风险随推理深度累积
上下文污染随帧数增长
弱模型会逐渐从机械执行进入问题分析
```

最大深度固定为两个 provider 请求。

## STRENGTH-102：拒绝工具调用计数预算

禁止按 read 调用次数限制。

原因：

```text
一次 provider 请求可以并发读取多个目标
工具数量代表执行宽度
provider 请求数才代表判断深度
```

## STRENGTH-103：拒绝语言自限合同

不通过 prompt 告诉 Replica：

```text
你只能走一步
你不确定就停止
你是便宜模型
```

预算必须由 transform 结构性实施。

## STRENGTH-104：拒绝来源标记

主模型 projection 不显示：

```text
以下内容由弱模型预读
以下文件可能不可靠
```

来源只保留在内部事件身份和 Journal 中。

## STRENGTH-105：拒绝追求反事实最优

第一版不实现：

```text
随机 K0 动作探索
逆概率因果估计
严格 contextual bandit
全局最优策略证明
```

本功能采用控制论 best-effort：

```text
有限风险
慢速负反馈
内部稳定工作点
显式成本
可观察熔断
```

---

# 二十九、最终设计不变量

审阅时只需抓住以下核心不变量：

```text
1. X 永远由主模型发出自己的 provider 请求。
2. Z_X 使用 Replica 角色的 fast-replica/deep-replica 两个 tier agent，只产生只读工具调用及结果。
3. P(Z_X) 不进入任何 X 可见路径；L(Z_X) 不存在（Z_X 无 Companion）。
4. K 只取 0、1、2，按 provider 请求计数。
5. 第 K+1 次 Replica transform 默认挂起（不跨 Primary Authority Root 复用）。超时由插件侧实施（STRENGTH-078），10 分钟取值待 C-05 确认。
6. DelegatedFrame 采用候选-消费-提升两阶段语义；失败的候选不进入活动投影。
7. X 拥有两颗卫星 Y_X 与 Z_X，二者皆为叶子。Z_X 无 Companion（COMPANION-001 辖区 = {WorkMain}）。
8. 两边 projection 从共享语义事件构造，按统一 SemanticEventCursor 定位。防反射为两条保证：EventId 字节层去重 + F°(X) ∩ L(X) = ∅ 的提升门控。
9. 跨 Session 的 identity 由确定性合成 ID 映射生成。
10. 训练状态只按 X 的 CanonicalRole 分桶（Z_X 自身的角色不是分桶键），旁路标签按负反馈概率纳入。
11. 所有策略参数是集中式代码常量。
12. 投影 DSL（spec/16）是所有 provider-visible projection 的唯一生产路径。
13. 目标是稳定合理，不是反事实最优。
14. Z_X 无自身 epoch，按 ε(X) 渲染（STRENGTH-010）。
15. Z_X 无恢复路径，失败即丢弃决策（STRENGTH-014）。
```

---

# 三十、审阅结论建议

建议结论：

```text
APPROVE WITH GATES
```

批准以下架构方向：

```text
独立 Strength Replica
独立 Companion
K≤2
请求级计数
双方无感知
transform 挂起
负反馈训练投影
typed projection DSL
```

### 最终审阅状态

```text
ARCHITECTURE: APPROVED
CONTROL STRATEGY: APPROVED
PROJECTION DSL MIGRATION: REQUIRED
IMPLEMENTATION: APPROVED AFTER HOST CANARIES
```

### 批准的核心架构裁决

```text
三会话拓扑 X / Y_X / Z_X（Z_X 为叶子，无 Companion）
只读工具集（双层 fail-closed，由 session ruleset + execution gate 实现）
Replica 内部 CanonicalRole（与 Blogger 同构，两个 tier agent）
K ∈ {0,1,2}，按 provider 请求计数
双方无感知（主模型看不到来源标记，弱模型不接收预算语义合同）
候选-消费-提升两阶段语义（候选只对首次 attempt 可见）
统一 SemanticEventCursor（替代 TurnIndex+PartIndex）
控制论负反馈（只按 X 的 CanonicalRole 分桶，两个独立控制环）
typed projection DSL（spec/16，全局架构，所有投影的唯一生产路径）
所有策略参数集中在 PolicyConstants 中
卫星结构层泛化（SatelliteContract + SatelliteRuntime，策略层不泛化）
全局记号统一（规范位置 spec/99）
不追求反事实最优
```

### 批准理由

1. **Replica 作为独立内部 CanonicalRole**，使低成本模型选择与只读能力在同一个既有概念内表达，无需新增正交权限维度，且消除了 prompt 与可用工具集的内在矛盾。
2. **只按 X 的 CanonicalRole 分桶**（Replica 自身的角色不是分桶键）显著降低状态数量、迁移逻辑和冷启动问题。
3. **统一衰减** 足以为 best-effort 系统处理模型切换产生的非平稳性。
4. **代码常量** 避免形成不必要、难验证的运行时参数空间。
5. **Projection DSL 追溯应用于既有 SSOT**，解决的是系统已经存在的横切复杂度，而不只是为 Strength 增加一层抽象。
6. **`K≤2`、候选后 promote、字节上限和自动 fail-open** 约束了最坏损失。
7. **控制器目标明确限定** 为稳定合理工作点，不承担无法可靠定义的全局最优责任。
8. **ARCH-003 不需要例外。** 两层权限均可由现有 Host hook 与 SDK 实现（resolveTools + session create），实读结论确认过滤发生在下游 `LLMRequestPrep.prepare` 而非 `ToolRegistry.tools()`。
9. **卫星结构层泛化** 消除三处既有重述（STRENGTH-009/061/075），并暴露出投影阶段缺少显式全序的既有缺陷。

### 生产启用前置条件

```text
记号改革与卫星结构层完成（阶段 0）
Host canary C-01 至 C-21 全部通过
Projection DSL 与性质测试完成（阶段 A）
旧投影完成追溯迁移（PROJ-008 六步完成）
K1 灰度稳定
K2 独立门禁通过
```

### 风险控制总结

本方案的价值不依赖预测器达到高精度。

由于 Replica 结构性只读、深度有界（K≤2）、任何疑点均可退化为主模型正常执行，错误预测的主要代价是有限的弱模型调用、投影字节和调查方向偏移。

通过 `K≤2`、候选-消费-提升语义、字节成本入账、独立 K2 控制环、慢速负反馈、自动熔断和数据边界门禁，这些风险被约束在工程可接受范围内。超时与取消责任显式归属插件侧（STRENGTH-078），Host 不提供保障。

### 最终状态

```text
APPROVED AS FINAL ARCHITECTURE
IMPLEMENT ONLY AFTER PROJECTION DSL MIGRATION AND HOST CANARIES
```

### 特别提醒

`Replica` 是一个正式的内部 CanonicalRole，与 `Blogger` 同构。

`fast-replica / deep-replica` 是它的两个 tier agent，仅在模型绑定上不同。用户配置只提供 model 字符串；prompt 由插件经 system.transform 注入，permission 由 session 级 ruleset 供给。

**不得让 Z_X 继承 X 的 CanonicalRole。** 继承会强制引入一个正交的权限收窄维度（原 ExecutionSurface），而那需要修订 AGENT-001、AGENT-007、AGENT-010，并新增一套交集代数；同时会让 Z_X 拿到 X 的角色 prompt 却只有只读工具，造成 prompt 与可用工具集互相矛盾。Replica 使用自己的 prompt 和角色。


---

# 三十一、对其他 SSOT 文件的修订清单

本节列出对 spec/00–12、99 的全部改动。[语义] 类改变规范内容；[机械] 类仅记号替换。

| 文件 | 性质 | 改动 |
|------|------|------|
| spec/00 | 语义 | Agent 角色一览新增 Replica 行；术语速查表指向 spec/99 |
| spec/02 | 语义 | AGENT-001 Role 新增 Replica；AGENT-002 20→22；AGENT-006 矩阵加 Replica 行；AGENT-008 不可见清单加 Replica |
| spec/04 | 语义 | FALLBACK-002 side 构造子 Selected \| Peer（原 SideA/SideB），Offset 原名不变 |
| spec/05 | 语义 | REVIEW-010 ProviderInputSeal 新增 IncludedCandidateFrameDigests |
| spec/07 | 语义 | HOST-005 ProseRecord（原 ARecord）；HOST-008 引入 SatelliteKind；HOST-010 补充卫星 transform 绑定判据 |
| spec/08 | 语义 | COMPANION-003 整条重写（P(X)/L(X)/切点记号）；COMPANION-005 BlogFrame→LogSegment（事实名不改）；COMPANION-009 加 attempt-local 候选帧豁免；COMPANION-010 包裹文本保持逐字节不变 |
| spec/09 | 机械 | EXEC-004 join 字段重命名；EXEC-006/008 父背景记号更新 |
| spec/11 | 语义 | PERSIST-010 新增 SatelliteLinked/Retired fold 规则；单调性条件改用切点 |
| spec/12 | 机械 | CTX-011 probe 规则改为序关系；CTX-003 记号更新 |
| spec/99 | 语义 | 新增规范符号表一节 |
| spec/16 | 语义 | 阶段顺序按 Writeback 类分组；冲突规则泛化 |

## 纪律

1. [机械] 类可批量执行，但与 [语义] 类分开提交。
2. 记号改革不驱动 journal schema 变更。唯一例外是 SemanticEventCursor。
3. ssot-lint 更新退役符号检测，设映射表与迁移历史为豁免区。
4. 条款变更合入时，合规状态表在同一提交内回写，不得延后。

# 三十二、调研结论

```text
spec/14 成立。
```

三条支撑：

1. **基础设施可对位。** Host 已有事件溯源日志与原子投影提交、前缀 epoch 同构物、两阶段准入先例。Journal Fold、PrefixEpoch、候选-消费-提升三者都是在既有结构上扩展与改名，非从零发明。

2. **ARCH-003 不需要例外。** 双层权限、Replica prompt 注入、卫星权限供给全部可由现有 hook 与 SDK 实现。工具 schema 过滤不在 `ToolRegistry.tools()` 而在下游 `LLMRequestPrep.prepare`（`session/llm/request.ts:208-214`）。session 级 permission 经 `POST /session` 供给，在 merge 中位于最后。

3. **修订后比原稿简单。** 删除 ExecutionSurface 及其交集代数、Y(Z) 及其恢复机制、Z_X 的 epoch 状态；成本函数由六项减至四项；撤回对 AGENT-001/007/010 及 COMPANION-001/002 的修订要求。净结果是条款更少、Host 依赖更少、用户配置负担不变（22 个 agent，各一个 model 字符串）。

## 真实验证门槛

```text
C-04  驻留 transform 不阻塞其他 Session      硬门槛，不通过则功能默认关闭
C-05  驻留时长可达 ParkedTransformLifetime    硬门槛，不通过则下调常量
```

`plugin.trigger` 在 Host 侧无超时（`index.ts:280-293`），超时与取消责任 100% 在插件侧实现。`Effect.promise` 下外层 fiber 中断不停止底层 Promise，dispose 必须显式 resolve 而非依赖 Effect 中断语义。

## 本次调研修正的早期误判（留档以免重走）

```text
错判一  STRENGTH-014 第一层需要 ARCH-003 例外。
        成因：只看到 ToolRegistry.tools() 忽略 permission，未追到下游 resolveTools。

错判二  Z_X 的权限询问会导致主会话死锁。
        实际：Host 会把提示送达 UI，用户可回复。问题降级为 UX 泄漏与时限浪费。

错判三  应保留 Y(Z) 以符合 COMPANION-001。
        成因：未注意 COMPANION-001 辖区限定词「普通」= WorkMain。
```

## 最终状态

```text
ARCHITECTURE            APPROVED（含本次修订）
NOTATION REFORM         PHASE 0（先于其余全部工作）
SATELLITE GENERALIZATION REQUIRED
ARCH-003 EXCEPTION      NOT REQUIRED
PROJECTION DSL MIGRATION REQUIRED（PHASE A）
IMPLEMENTATION          APPROVED AFTER C-04 & C-05
```

```text
生产启用前置条件
    阶段 0（记号改革 + 卫星结构层）完成，退役符号残留数为零
    阶段 A（spec/16 投影 DSL 迁移）完成，PROJ-008 六步全过
    C-01 至 C-21 全部通过
    K1 灰度稳定
    K2 独立门禁通过
```
