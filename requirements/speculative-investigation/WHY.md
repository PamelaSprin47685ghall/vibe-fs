# speculative-investigation — WHY

> 本页回答：这个包为什么必须独立存在？它防止哪一类世界破坏？哪些被拒方案曾经存在？
> 规范见 `WHAT.md`；实现见 `HOW.md`。本页不新增 normative 命题。

## 1. 不可替代的存在理由

### 1.1 投机必须是「错误时只损失成本」的形状

Strength 要替代的是昂贵 primary 在局部窗口里重复做出的**机械只读调查**，不是替代写入、
执行、权限或最终判断。`read/glob/grep` 的错误方向只增加有界成本与上下文字节；写文件、
执行命令、网络访问或权限交互会把一次错误预测变成**真实世界副作用**，无法靠 primary
忽略结果恢复。

「看起来只读」不是充分条件。扩大安全集合必须逐个证明：副作用 = 0、权限交互 = 0、
结果可稳定重放、错误方向的损失有界（archive/changes/completed/strength.md §三.1）。

### 1.2 投机结果不是历史；被消费以后才是历史

Replica 行为是**干预（intervention）**。primary 尚未消费时，它既不是用户行为，也不是
primary 已发生的因果历史。提前写入 XTrace/Companion 会让未发生的世界污染未来请求；
反过来，primary 已消费后若重启时丢失，又会删除真实因果历史。因此必须用
durable Candidate → consumption proof → Promotion 分开「准备好了」与「已经影响了 primary」。

关键：**没有 rollback。** 不是「先写进去，失败再回滚」，而是 Candidate 只对目标 run 可见、
观察消费证据、再 promotion（archive/changes/completed/strength.md §三.2）。

### 1.3 低成本路径必须具有更低 authority，而不是更弱的文字提醒

不能通过 system prompt 告诉便宜模型「请只读」「不要写」「你只是预读模型」。真正的约束
来自三个结构事实：provider-visible tool schema 只出现 `read/glob/grep`；execution gate
读取同一个 ToolCapabilitySet fail closed；Host 在第 K 个 provider request 后**物理**停止
Replica。**模型不负责遵守预算；Host 负责。**

### 1.4 机制不可见，但事实必须可审计

主模型 provider-visible 历史中不得出现 `strength/replica/prefetch/weak model/confidence/
budget/prediction/source=sidecar` 等机制 provenance；Replica 也不接收「你正在帮另一个模型
预读」之类提示。但 EventStore events / diagnostics 必须保留 DecisionId、ReplicaSessionId、
TargetProviderRun、K、digest、predictor features/score、cost estimate、failure reason。
**模型不可见 ≠ 系统不可审计。**

### 1.5 干预不能冒充观测（对旧算法最大的修正）

如果 Strength 预测「下一步会 read」，然后自己先 read，再把这次 Replica read 当训练数据，
预测器就把自己的行为当成世界证据，形成自我强化闭环。旧稿曾用「训练纳入概率 + 负反馈
controller」缓解；当前裁决是：**Replica 产生的数据是 intervention data，不是「主模型本来
会怎么做」的 label。** 反事实训练样本只来自 shadow/control opportunity。

### 1.6 为什么默认 K0

Strength 是优化，不是正确性前提。成本关系、目标 ProviderRun、Host canary、durability 或
eligibility 任一无法证明时，最安全的选择是 K0。但**已经 Promoted 的历史属于真实因果事实**，
即使新 speculation 熔断也必须继续恢复与 replay。

### 1.7 为什么复用现有 owner，不建第二套 runtime

Session 身份、Prompt authority、Projection、XTrace、Fallback 与 durable storage 已各有唯一
owner。Strength 只在这些代数中增加合法 case：`AttachmentKind.StrengthReplica`（`InternalLeaf
× Attached`，不是 `SatelliteKind` case）、`ProviderRequestKind.StrengthReplica`、
ProjectionIntent 的 Strength intent、EventStore 事件族。重新建立 Replica role、Satellite
kind、私有 journal/blob、fallback 或 projection DSL 会制造同一事实的第二表示，并让恢复与
权限产生分叉（archive/docs/shape/strength.md STRENGTH-013..019 逐条分配 owner）。

### 1.8 为什么 same-role fast leaf

Strength 需要与 primary 相同的 CanonicalRole 语境、不同的较低成本模型、更窄的
request-specific 工具集合。现有 `fast-ROLE/deep-ROLE` 与 `AttemptExecutionProfile` 已能
表达三者；新增 Replica role 或 Agent 会破坏 Agent→Role 的函数关系。

### 1.9 为什么 Strength 不消费 Semble

历史曾用 Semble 命中伪造 Inspector/Reviewer `read`——把未发生的调查写成 primary 可见工具
交换，破坏「只保留真实 Host 工具」。Semble 能力归 `knowledge-reuse` 的 AGENT-027；Strength
不调用。

## 2. 失败模式（世界 RED 长什么样）

| 失败 | 破坏 | 对应命题 |
|---|---|---|
| 未消费的 speculative 干预进入 XTrace/Companion/LWR/PrefixSnapshot | 未发生的世界污染未来请求 | SPEC-INV-006 |
| Replica 能写/执行/联网 | 错误预测产生真实副作用 | SPEC-INV-004/005 |
| Promoted 历史重启后消失 | 删除真实因果历史 | SPEC-INV-007/008 |
| 把 Replica 干预数据当 primary counterfactual label | predictor 自我强化闭环 | SPEC-INV-010 |
| 机制 provenance 进模型字节 | 改变推理策略、机制升级成模型协议 | SPEC-INV-012 |
| canary/cost/evidence 不足仍启用 treatment | 优化成为正确性依赖 | SPEC-INV-002/011 |
| durable 歧义时 fail-open | 状态与世界事实脱节 | SPEC-INV-006/007/011 |

历史失败模式第一手考古：`archive/changes/completed/strength.md` §二十二（存储收口）、§二十三
（崩溃矩阵）、§三十（明确拒绝的方向）、§三十一（最终不变量）。

## 3. 明确拒绝的方向（考古，不构成 WHAT）

以下方向在 archive/changes/completed/strength.md §三十逐条拒绝，理由已吸收进对应命题：

- **同一 Work Session 临时切 fast model**：污染 authority/fallback identity、stable prefix、
  provider run attribution、model-visible continuity；独立 leaf 才是正确隔离边界。
- **新增 Replica CanonicalRole / fast-replica / deep-replica**：破坏 Agent→Role 函数关系。
- **只靠 prompt 说「不要写」**：权限必须结构化（schema + execution gate 同源）。
- **无限只读 prefetch**：只读不等于无害，每层都增加 steering risk 与 input bytes。
- **按 tool call 数预算**：成本单元是 provider request，不是 tool call（见 SPEC-INV-003）。
- **让主模型看到来源标签**：改变推理策略（见 SPEC-INV-012）。
- **Candidate 直接进 XTrace**：未消费的投机不是历史（见 SPEC-INV-006）。
- **promotion 后只靠内存记住**：重启删除真实因果历史（见 SPEC-INV-007/008）。
- **Replica request 写入主 FallbackCursor**：不同资源、不同目的的 attempt（见 SPEC-INV-004）。
- **继续旧版 training-inclusion controller**：先用可解释的 deterministic control holdout 拿
  干净 label，不上 off-policy estimator（见 SPEC-INV-010）。

## 4. 边界：什么**不归**本包

- repository fact acquisition contract → `repository-investigation`。
- participant identity canonical semantics（persona/language 继承的语义定义）→
  `participant-identity`。
- provider projection generic law（UseStrengthMirror/InsertStrengthFrames 的代数性质）→
  `provider-projection`。
- fallback/retry policy → `provider-attempt-recovery`。
- `unpromoted ≠ history` 的另一半（canonical trace 侧）→ `semantic-trace`（HANDOFF §18.6）。
- 当前 Strength 名字、same-role-fast 模型选择、具体 budget/predictor algorithm → HOW，不进 WHAT。
- 大 material 存储 substrate（EventStore envelope / payload_refs 语义）→ `durable-events`。

边界卡片：`archive/archive/archive/requirements-design/18-optimization-epistemics.md`。
