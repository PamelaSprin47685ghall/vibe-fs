# 一、先把理论表述校正到位

你的校正是对的，而且现有规范文档本身已经这样写了：EPI-010 定义的三种经典退化是 **Graph A***、**Bayes**、**Graph-MCTS**，并不是 Bellman、A*、MCTS。

当前实现之所以容易造成误解，是因为代码把：

```fsharp
type SolverMode =
    | Bellman
    | BestFirst
    | MonteCarlo
```

做成了互斥枚举，同时又分别维护 `Bayesian`、`Search`、`MonteCarlo` 三种投影。 

这实际上混淆了三件不同层次的东西：

| 层次      | 正确位置                 |
| ------- | -------------------- |
| Bellman | 递归一致性／固定点语义          |
| Bayes   | 概率因子消元的一个精确退化        |
| A*      | min-plus 固定点上的有界展开退化 |
| MCTS    | 随机期望固定点上的采样退化        |

不过还需要再严谨一步：

> **标准 Bellman 方程本身不足以让 Bayes 成为严格退化。**

标准 Bellman 方程主要描述动作选择、状态转移和未来价值；Bayes 描述的是因子乘积、隐变量消元和归一化。为了让 Bayes、A*、MCTS 真正成为同一个理论的三个严格退化，母体必须比普通 Bellman 方程再高一层：

> **带偏序证书和可替换消元算子的因子—动作超图固定点演算。**

Bellman 是这个母体在“决策节点”上的表现形式，而不是三个退化之一。

下面暂称这个理论为：

# Sphinx-GEC

## Sphinx Generalized Epistemic Calculus

## Sphinx 广义认识演算

---

# 二、最终架构：一套核心、两个宿主、全部认识论插件化

```text
                         ┌─────────────────────────────┐
                         │      Epistemic Plugins      │
                         │                             │
                         │ Bayes / A* / MCTS           │
                         │ Borda / BTL / PL / BTS      │
                         │ Probe Design / Reflection   │
                         │ Stop / Renderer / Ontology  │
                         └──────────────┬──────────────┘
                                        │
                              opaque schema + laws
                                        │
┌─────────────────┐          ┌──────────▼──────────┐          ┌──────────────────┐
│   V1 MCP Host   │◄────────►│    Sphinx Core     │◄────────►│ V2 OpenCode Host │
│                 │          │                    │          │                  │
│ yield / submit  │          │ Event Log          │          │ Session Forking  │
│ legacy wrappers │          │ Hypergraph         │          │ Subagents        │
│ optional tasks  │          │ Certificates       │          │ Full Scheduler   │
│ provider bridge │          │ Plugin Registry    │          │ Fan-out / Fan-in │
└─────────────────┘          │ Work Agenda        │          └──────────────────┘
                             └──────────┬──────────┘
                                        │
                                 WorkEnvelope
                                        │
                         ┌──────────────▼──────────────┐
                         │         LLM Witnesses       │
                         │                             │
                         │ propose / rank / predict    │
                         │ critique / reflect / render │
                         └─────────────────────────────┘
```

## 核心只硬编码“计算机制”

Sphinx Core 可以硬编码：

* 稳定 ID；
* 事件顺序；
* revision；
* 内容哈希；
* 图和超边；
* 分支谱系；
* 工作项依赖；
* lease、取消和重试；
* 资源账本；
* schema 与插件版本；
* 证书精化关系；
* 确定性回放。

这些不是认识论立场，而是运行时语义。

## 下列内容全部移出核心

* Finding、Evidence、Hypothesis 本体；
* Why、How、Polar 分类；
* 方法名称及权重；
* 什么算证据；
* 什么算独立；
* 如何计算价值；
* 如何更新信念；
* 什么叫充分调查；
* 如何停止；
* 如何综合答案；
* Borda、Bradley–Terry、BTS；
* 问卷变体；
* 反思协议；
* 对 prompt 诱导的模型；
* 对“真实想法”的操作性定义。

当前 `Methodology.fs` 直接写入方法名称、问题形式权重、facet 权重和基础成本；这整个模块都应成为一个默认插件，而不再属于 Kernel。

---

# 三、路线图总览

| 阶段 | 主要产物                     | 对外形态        |
| -- | ------------------------ | ----------- |
| R0 | 理论与兼容契约冻结                | 不改变行为       |
| R1 | 事件溯源核心和通用图               | 内部重构        |
| R2 | 插件 ABI，迁出全部认识论           | 内部重构        |
| R3 | 统一固定点与 ValueCertificate  | 新计算核心       |
| R4 | V1 通用 MCP 工作协议           | MCP         |
| R5 | 问卷、Borda 推广与 truthful 插件 | MCP 科研模式    |
| R6 | OpenCode 插件外壳            | OpenCode    |
| R7 | subagent 分叉与完整调度         | OpenCode V2 |
| R8 | 科研级评测、回放和实验报告            | V1/V2 共用    |

最重要的工程原则是：

> **不要在现有 `Policy.fs` 上继续堆 if/else；先把现有行为包进 Legacy 插件，再用绞杀式迁移替换。**

---

# 四、R0：冻结当前行为，建立不会丢失的基线

## 4.1 把当前示例变成黄金轨迹

当前示例具有很高价值：

1. 根问题经 `start` 进入 `assess`；
2. 语义评估后激活七种方法；
3. LLM 提交七个候选；
4. 当前标量价值函数选中 `ExperimentDesign`，其值为 `1.089`；
5. 系统持续运行到 revision 52；
6. revision 56 才进入综合；
7. revision 57 以 `stop-dominates` 结束。   

把它保存为：

```text
fixtures/
  legacy/
    programming-quality.full.jsonl
    programming-quality.expected-summary.json
    programming-quality.event-projection.json
```

以后每次重构都必须验证：

```text
旧 MCP 调用
→ Legacy Adapter
→ 新 Core
→ Legacy Renderer
```

在可观察行为上不破坏旧客户端。

## 4.2 新增四份 ADR

```text
docs/adr/
  001-generalized-epistemic-calculus.md
  002-epistemology-is-plugin-owned.md
  003-one-core-two-hosts.md
  004-llm-witness-and-protocol-relative-truth.md
```

其中 ADR-004 明确：

> 没有外部可信源时，Sphinx 的科研对象不是未经假设即可识别的外部世界真值，而是某模型在声明的探究协议下表现出的潜在判断、反思迁移、稳定性和问法效应。

这不是降低目标，而是避免混淆可识别对象。

## 4.3 增加四条新不变量

建议在 EPI-014 后新增：

### PROPOSED_EPI_015：认识论零硬编码

Core 不得导入方法论、证据本体、信念更新、排序、停止或综合插件。

### PROPOSED_EPI_016：单一证书空间

Bayes、A*、MCTS 不得拥有互斥 `SolverMode`；它们必须精化同一个认识图上的证书。

### PROPOSED_EPI_017：探究协议是可回放实验

每次措辞、选项顺序、候选标签、上下文分支和随机种子都必须可重放。

### PROPOSED_EPI_018：宿主等价

给定相同初始状态、插件锁文件与观测事件序列，MCP Host 和 OpenCode Host 必须产生相同 Core 状态。

---

# 五、R1：建立事件溯源核心

当前 `SessionStore` 直接用进程内 `Dictionary` 保存状态，因此进程重启后 handle 失效；现有测试也明确把“restart invalidates handles”作为当前特征。 

V1 科研级实现必须改成：

> 追加式事件日志为权威状态，所有 Map 和证书都是物化投影。

## 5.1 推荐目录

```text
src/Wanxiangshu/Sphinx/
  Core/
    Ids.fs
    JsonEnvelope.fs
    Events.fs
    Graph.fs
    Certificate.fs
    Work.fs
    Budget.fs
    Effects.fs
    Reducer.fs

  Runtime/
    EventStore.fs
    Projection.fs
    PluginRegistry.fs
    RefinementEngine.fs
    Agenda.fs
    Scheduler.fs
    Runtime.fs

  Plugins/
    Legacy/
    Bayes/
    AStar/
    Mcts/
    Ordinal/
    Questionnaire/
    Reflective/
    Render/

  Hosts/
    Mcp/
    OpenCodeInterop/

packages/
  sphinx-opencode/
    src/
      index.ts
      runtime.ts
      scheduler.ts
      worker-pool.ts
```

## 5.2 Core 数据类型

```fsharp
type InquiryId = private InquiryId of string
type EventId = private EventId of string
type NodeId = private NodeId of string
type EdgeId = private EdgeId of string
type WorkId = private WorkId of string
type BranchId = private BranchId of string

type PluginRef =
    { Id: string
      Version: string
      AbiVersion: string }

type SchemaRef =
    { Id: string
      Version: string
      Hash: string }

type JsonEnvelope =
    { Schema: SchemaRef
      Payload: JsonValue }

type GraphNode =
    { Id: NodeId
      Kind: string
      Payload: JsonEnvelope
      Revision: int64 }

type HyperEdge =
    { Id: EdgeId
      Tails: Set<NodeId>
      Heads: Set<NodeId>
      Relation: string
      Payload: JsonEnvelope option }

type WorkStatus =
    | Planned
    | Ready
    | Leased
    | Running
    | InputRequired
    | Succeeded
    | Failed
    | Cancelled
    | Superseded

type WorkItem =
    { Id: WorkId
      InquiryId: InquiryId
      BranchId: BranchId
      Producer: PluginRef
      Capability: string
      Input: JsonEnvelope
      OutputSchema: SchemaRef
      Dependencies: Set<WorkId>
      BlindToken: string option
      RandomizationSeed: uint64
      Budget: ResourceBudget
      Status: WorkStatus }

type InquiryState =
    { Id: InquiryId
      Revision: int64
      EventHead: EventId option
      Graph: Map<NodeId, GraphNode>
      Edges: Map<EdgeId, HyperEdge>
      Certificates: Map<NodeId, ValueCertificate>
      Work: Map<WorkId, WorkItem>
      PluginLock: Map<string, PluginRef>
      Budget: ResourceBudget
      Status: InquiryStatus }
```

这里 `Kind`、`Relation` 和 `Payload` 都是插件定义的。Core 不知道 `"Evidence"`、`"Hypothesis"` 或 `"BordaBallot"` 是什么。

## 5.3 Core 事件只表达运行事实

```fsharp
type CoreEvent =
    | InquiryCreated of JsonEnvelope
    | PluginSetBound of PluginRef list
    | GraphPatched of GraphPatch
    | WorkPlanned of WorkItem list
    | WorkTransitioned of WorkId * WorkStatus
    | ObservationAccepted of WorkId * JsonEnvelope
    | CertificatePatched of NodeId * CertificatePatch
    | BudgetDebited of ResourceUsage
    | InquiryStatusChanged of InquiryStatus
```

插件自己的认识论事实统一放入：

```fsharp
GraphPatched
ObservationAccepted
CertificatePatched
```

的 opaque payload 中，而不是继续增加：

```fsharp
| EvidenceAdded
| HypothesisUpdated
| BayesianPosteriorChanged
```

这类 Kernel 枚举。

## 5.4 事件存储

V1 第一版可以用 SQLite：

```sql
inquiries(
  id TEXT PRIMARY KEY,
  revision INTEGER NOT NULL,
  status TEXT NOT NULL,
  head_event_id TEXT,
  created_at TEXT NOT NULL
);

events(
  sequence INTEGER PRIMARY KEY AUTOINCREMENT,
  event_id TEXT UNIQUE NOT NULL,
  inquiry_id TEXT NOT NULL,
  revision INTEGER NOT NULL,
  event_type TEXT NOT NULL,
  plugin_id TEXT,
  payload_json TEXT NOT NULL,
  previous_hash TEXT,
  event_hash TEXT NOT NULL,
  created_at TEXT NOT NULL
);

work_items(
  work_id TEXT PRIMARY KEY,
  inquiry_id TEXT NOT NULL,
  branch_id TEXT NOT NULL,
  status TEXT NOT NULL,
  lease_owner TEXT,
  lease_expires_at TEXT,
  attempt INTEGER NOT NULL,
  payload_json TEXT NOT NULL
);

snapshots(
  inquiry_id TEXT NOT NULL,
  revision INTEGER NOT NULL,
  projection_json TEXT NOT NULL,
  event_hash TEXT NOT NULL,
  PRIMARY KEY(inquiry_id, revision)
);
```

关键要求：

* 事件先落盘，响应后返回；
* `expectedRevision` 实现乐观并发；
* 重复提交相同 `workId + attempt` 必须幂等；
* snapshot 只是加速缓存；
* 删除 snapshot 后仍能从事件重放。

---

# 六、R2：插件 ABI——真正清空 Kernel 的认识论成分

## 6.1 插件清单

```fsharp
type PluginManifest =
    { Id: string
      Version: string
      AbiVersion: string
      Capabilities: Set<string>
      InputSchemas: SchemaRef list
      OutputSchemas: SchemaRef list
      Dependencies: PluginRef list
      Laws: PluginLawDeclaration list }
```

推荐能力名称：

```text
construct.model
candidate.generate
graph.normalize
transition.model
observation.model
value.algebra
refiner.exact
refiner.bound
refiner.sample
elicitation.ordinal
elicitation.truthful
probe.design
scheduler.priority
stop.certificate
answer.render
```

## 6.2 插件接口

```fsharp
type PluginContext =
    { InquiryId: InquiryId
      Revision: int64
      GraphView: GraphView
      CertificateView: CertificateView
      Budget: ResourceBudget
      RandomSeed: uint64 }

type PluginDelta =
    { GraphPatch: GraphPatch option
      CertificatePatches: CertificatePatch list
      WorkItems: WorkItem list
      Diagnostics: JsonEnvelope list }

type EpistemicPlugin =
    { Manifest: PluginManifest
      Initialize: PluginContext -> JsonEnvelope -> Result<PluginDelta, PluginError>
      Observe: PluginContext -> WorkItem -> JsonEnvelope -> Result<PluginDelta, PluginError>
      Close: PluginContext -> Result<PluginDelta, PluginError>
      ProposeRefinements: PluginContext -> RefinementProposal list
      StopCertificates: PluginContext -> StopCertificate list
      Render: (PluginContext -> JsonEnvelope option) option }
```

插件不是被 Kernel “审判”。这些接口只保证插件能够组合、重放和声明自身假设。

## 6.3 当前文件迁移表

| 当前文件                   | 新位置                                     |
| ---------------------- | --------------------------------------- |
| `Bayes.fs`             | `Plugins/Bayes/ExactBayes.fs`           |
| `Search.fs`            | `Plugins/AStar/AStarRefiner.fs`         |
| `MonteCarlo.fs`        | `Plugins/Mcts/MctsRefiner.fs`           |
| `Methodology.fs`       | `Plugins/Legacy/LegacyMethodology.fs`   |
| `Value.fs`             | `Plugins/Legacy/LegacyValue.fs`         |
| `Representation.fs`    | `Plugins/Pareto/ParetoValue.fs`         |
| `Types.fs` 中 Finding 等 | `Plugins/Legacy/LegacyTypes.fs`         |
| `Closure.fs`           | `Runtime/RefinementEngine.fs`           |
| `Policy.fs`            | `Runtime/Scheduler.fs` + Legacy Adapter |
| `Mcp*.fs`              | `Hosts/Mcp/`                            |

当前闭包写死了 `Bayes → Value → Representation → Search → MonteCarlo` 的调用顺序。新闭包应改成插件事件传播和依赖拓扑，而不是继续扩大这条固定流水线。

## 6.4 删除 `SolverMode`

删除：

```fsharp
SolverMode: SolverMode
Search: Map<...>
MonteCarlo: Map<...>
Bayesian: BayesianBelief option
```

替换成：

```fsharp
ActiveRefiners: PluginRef list
Certificates: Map<NodeId, ValueCertificate>
Agenda: RefinementAgenda
```

同一节点可以同时拥有：

* 精确 Bayes 消息；
* A* 下界；
* MCTS 样本；
* Borda 序数约束；
* 问法效应后验。

它们不是互斥模式。

---

# 七、R3：统一的 ValueCertificate

## 7.1 证书结构

```fsharp
type ValueCertificate =
    { NodeId: NodeId
      Semantics: PluginRef

      Exact: JsonEnvelope option

      LowerEnvelope: JsonEnvelope option
      UpperEnvelope: JsonEnvelope option

      SampleSummary: JsonEnvelope option
      OrdinalConstraints: JsonEnvelope list
      LatentPosterior: JsonEnvelope option

      Residual: JsonEnvelope option
      WitnessEvents: EventId list
      DerivationEvents: EventId list

      Revision: int64 }
```

Core 不解释 `LowerEnvelope` 里是浮点数、Pareto 集、概率分布还是偏好后验。解释权属于提供 `value.algebra` 的插件。

## 7.2 两个偏序

必须明确区分：

### 价值偏序

$$
v_1\preceq_V v_2
$$

表示某认识论／价值插件认为 \(v_2\) 不劣于 \(v_1\)。

### 证书精化偏序

$$
C_1\sqsubseteq_I C_2
$$

表示 \(C_2\) 比 \(C_1\) 更精确，但不一定“价值更高”。

例如：

```text
[0.2, 0.9] → [0.5, 0.7]
```

是认识精化，不是价值上升。

## 7.3 Refiner

```fsharp
type RefinementProposal =
    { Id: string
      Plugin: PluginRef
      Target: NodeId
      Capability: string
      Preconditions: Set<string>
      ConflictKeys: Set<string>
      ExpectedEffect: JsonEnvelope
      EstimatedUsage: ResourceBudget
      WorkTemplate: JsonEnvelope option }

type Refiner =
    { Plugin: PluginRef
      Applicable: PluginContext -> bool
      Propose: PluginContext -> RefinementProposal list
      ApplyExact: (PluginContext -> RefinementProposal -> PluginDelta) option
      ApplyObservation:
          PluginContext ->
          RefinementProposal ->
          JsonEnvelope ->
              PluginDelta }
```

Bayes、A*、MCTS、Borda 都只是 Refiner。

---

# 八、V1：MCP 保姆级实现方案

当前 MCP 架构已有一个值得保留的优点：MCP SDK 只存在于 `McpServer.fs`，所有 continuation 最终汇入 `SessionStore.ResumeObservation`，协议层不自行裁决阶段。

但当前接口仍是固定的四阶段：

```text
assess → propose → investigate → synthesize
```

并由 `nextTool` 做同型映射。

V1 应保持旧接口，同时新增通用工作协议。

## 8.1 对外工具

### 新通用工具

```text
sphinx_inquiry_start
sphinx_work_submit
sphinx_inquiry_status
sphinx_inquiry_export
sphinx_inquiry_cancel
```

可选：

```text
sphinx_work_next
```

但最好让 `submit` 的返回值直接携带下一批工作，减少往返。

### 旧工具继续保留

```text
start
assess
propose
investigate
synthesize
status
cancel
resume
```

它们全部变成 Legacy Adapter。

## 8.2 `sphinx_inquiry_start`

输入：

```json
{
  "question": "用户根问题",
  "profile": "research-default",
  "plugins": [
    "sphinx.probe.open@1",
    "sphinx.ordinal.btl@1",
    "sphinx.truthful.self-prediction@1",
    "sphinx.refiner.bayes@1",
    "sphinx.refiner.astar@1",
    "sphinx.refiner.mcts@1"
  ],
  "executionMode": "delegated",
  "budget": {
    "maxModelCalls": 40,
    "maxTokens": 200000,
    "maxBranches": 16
  }
}
```

输出：

```json
{
  "inquiryId": "iq_...",
  "revision": 0,
  "status": "input_required",
  "manifestHash": "sha256:...",
  "work": [
    {
      "workId": "work_...",
      "branchId": "branch_...",
      "plugin": {
        "id": "sphinx.probe.open",
        "version": "1.0.0"
      },
      "capability": "candidate.generate",
      "input": {
        "schema": "sphinx.probe.open/input@1",
        "payload": {}
      },
      "outputSchema": {
        "id": "sphinx.probe.open/output",
        "version": "1",
        "hash": "sha256:..."
      },
      "blindToken": "opaque_...",
      "randomizationSeed": "..."
    }
  ]
}
```

## 8.3 `sphinx_work_submit`

```json
{
  "inquiryId": "iq_...",
  "expectedRevision": 7,
  "results": [
    {
      "workId": "work_...",
      "attempt": 1,
      "model": {
        "provider": "openai",
        "model": "model-id",
        "temperature": 0.7,
        "seed": "..."
      },
      "payload": {
        "schema": "sphinx.ordinal.ballot@1",
        "payload": {}
      },
      "usage": {
        "inputTokens": 3200,
        "outputTokens": 900
      }
    }
  ]
}
```

返回：

```json
{
  "revision": 8,
  "status": "input_required",
  "accepted": ["work_..."],
  "work": [...],
  "decisionView": {
    "schema": "sphinx.research.status@1",
    "payload": {}
  }
}
```

或：

```json
{
  "revision": 28,
  "status": "completed",
  "answer": {...},
  "researchManifest": {...}
}
```

## 8.4 Legacy Adapter 映射

| 旧请求                         | 新 WorkItem                    |
| --------------------------- | ----------------------------- |
| `SemanticAssessmentRequest` | `legacy.semantic-assessment`  |
| `GenerateCandidatesRequest` | `legacy.candidate-generation` |
| `InvestigateRequest`        | `legacy.investigation`        |
| `SynthesizeRequest`         | `legacy.render`               |

旧客户端仍看到：

```text
nextTool = assess / propose / investigate / synthesize
```

新客户端只看到通用 `WorkEnvelope`。

MCP 本身允许工具采用不同交互方式，并不要求所有工具必须使用同一种阶段模型，因此通用工作协议不违背 MCP。([Model Context Protocol][1])

## 8.5 V1 的三种执行模式

### Delegated

MCP 调用方自己充当 LLM witness：

```text
Sphinx 返回 ProbeEnvelope
→ 当前 LLM 回答
→ 调用 submit
```

优点是兼容性最好。

限制是多个回答共享主上下文，不能声称是严格隔离样本。

### Direct Provider

Sphinx MCP Server 内部直接调用配置的 LLM provider：

```text
MCP Client
  → start
Sphinx Server
  → 独立调用多个 LLM 分支
  → 汇聚
MCP Client
  ← 状态或最终结果
```

科研级 split-ballot 和相同前缀分叉优先使用此模式。

不建议依赖 MCP Sampling。当前 MCP 2026-07-28 规范已将 Sampling 标记为 deprecated，并明确建议新实现直接集成 LLM provider API。([Model Context Protocol][2])

### Hybrid

* 主问题与最终反思由当前调用方完成；
* 隔离问卷、排序 panel 由 direct-provider 完成。

这是 V1 最实际的科研模式。

## 8.6 长任务的协议策略

基础实现继续使用 inquiry handle 和 `status`，保证旧客户端可用。

支持当前 MCP Tasks 扩展的客户端，可以将 direct-provider 长任务映射为：

```text
working
input_required
completed
failed
cancelled
```

并用 `tasks/get`、`tasks/update`、`tasks/cancel` 管理；但 Tasks 必须作为协商后启用的可选能力，不能成为 V1 的唯一入口。([Model Context Protocol][3])

## 8.7 MCP 版本兼容

现有 stdio 测试仍以 `2024-11-05` 初始化。

增加协议矩阵：

```text
2024-11-05：legacy structuredContent/yield
2025-*：兼容测试
2026-07-28：resultType、MRTR、可选 Tasks
```

生成以下测试：

```text
mcp-protocol-2024.test.mjs
mcp-protocol-2026.test.mjs
mcp-capability-negotiation.test.mjs
mcp-tasks-optional.test.mjs
```

---

# 九、V1 的调度循环

```fsharp
let rec advance runtime state =
    let closed = RefinementEngine.close runtime.Plugins state

    match Stop.selectCertificate runtime.Plugins closed with
    | Some stop ->
        let answer = Render.render runtime.Plugins closed stop
        closeInquiry answer closed

    | None ->
        let refinements =
            runtime.Plugins
            |> PluginRegistry.proposeRefinements closed

        let ready =
            Agenda.resolveDependencies closed refinements

        let selected =
            runtime.Scheduler.select closed.Budget ready

        match selected with
        | [] ->
            suspend "no-runnable-refinement" closed

        | batch ->
            let events, work = WorkPlanner.plan batch closed
            appendEvents events closed, PublishWork work
```

V1 每次遇到 `PublishWork` 就通过 MCP yield 给调用方。

V2 遇到同一个 `PublishWork`，则自动交给 OpenCode worker pool。

这就是“一套核心、两个宿主”。

---

# 十、R5：从 Borda 推广的科研级 elicitation 插件组

## 10.1 第一批插件

```text
sphinx.ordinal.borda
sphinx.ordinal.robust-borda
sphinx.ordinal.bradley-terry
sphinx.ordinal.plackett-luce
sphinx.ordinal.mallows-mixture

sphinx.questionnaire.split-ballot
sphinx.questionnaire.balanced-block
sphinx.questionnaire.anchor-carryover
sphinx.questionnaire.reverse-wording

sphinx.truthful.proper-self-prediction
sphinx.truthful.peer-prediction
sphinx.truthful.bayesian-truth-serum
sphinx.truthful.surprisingly-popular

sphinx.reflect.commit-reveal-revise
sphinx.reflect.counterargument
sphinx.reflect.future-self
```

Borda 是基线，而不是最终唯一聚合器。

## 10.2 标准探究协议

### 第一阶段：开放生成

从同一上下文产生多个隔离分支：

```text
禁止展示方法清单
禁止展示其他分支答案
要求自由提出候选
```

目标是采集初始认知，而不是验证预定义本体。

### 第二阶段：候选冻结

由独立分支完成：

* 候选释义；
* 语义近邻判断；
* 是否是同一潜在方案；
* 是否只是不同措辞；
* 是否存在包含关系。

等价关系也保留为后验，不立即硬合并。

### 第三阶段：盲化序数施测

* 随机候选标签；
* 随机左右顺序；
* 平衡不完全区组；
* pairwise；
* best–worst；
* top-\(k\)；
* 允许并列；
* 允许弃权；
* 不显示候选提出者。

### 第四阶段：元预测

每个 witness 同时报告：

```text
自己的选择
对其他隔离分支选择分布的预测
对未来反思后自己的预测
决定选择的最小理由
什么信息会使自己改变选择
```

### 第五阶段：commit–reveal–revise

```text
锁定初始选择
→ 展示匿名化双方最强理由
→ 重新回答
→ 提交变化原因
→ 回答共同锚题
```

### 第六阶段：估计问法效应

分别估计：

* 位置效应；
* 正反措辞效应；
* 开放题／封闭题效应；
* 候选集合效应；
* 展示理由后的反思迁移；
* 对后续锚题的 carryover。

## 10.3 Truthful 不等于惩罚

这里的 scoring 不应成为“处罚 LLM 的权力”，而应成为：

* 明确回答目标；
* 促使模型表达完整概率；
* 采集自我预测；
* 检验自我模型；
* 区分稳定判断与措辞服从；
* 校准哪些施测协议更能显影其判断。

严格 proper scoring rule 的理论性质是：在相应期望效用假设下，真实报告预测分布会最大化预期得分。([Taylor & Francis Online][4])

BTS、Peer Prediction 和 Surprisingly Popular 专门处理缺少外部可验证真值的主观信息问题，但它们各自依赖相关信号、共同认知结构或理性响应等条件。([PubMed][5])

因此对 LLM 的正确做法是：

> 把 truthful 机制当作可比较、可消融、可校准的探究协议，而不是声称装上 BTS 后便自动得到真理。

---

# 十一、V1 Definition of Done

V1 完成必须同时满足：

| 项目            | 验收条件                                       |
| ------------- | ------------------------------------------ |
| 旧接口           | 现有八个工具测试全部通过                               |
| 黄金轨迹          | 当前 `main.jsonl` 可经 Legacy Adapter 重放       |
| 持久性           | 进程重启后 inquiry 可恢复                          |
| 插件隔离          | Core 中不存在 Methodology、Evidence、Bayes 等领域类型 |
| 单一证书          | 删除 `SolverMode`                            |
| 三种退化          | Bayes、A*、MCTS 各有严格 conformance test        |
| Elicitation   | 至少支持 split-ballot、Borda、BTL、自我预测           |
| 可复现性          | 保存模型、版本、温度、seed、题项排列和插件锁                   |
| MCP 兼容        | 旧协议可运行，2026 协议有能力协商                        |
| 无 Sampling 依赖 | 科研分支走 direct provider 或外部 submit           |
| 导出            | 可导出 event log、manifest、certificate、结果      |

---

# 十二、V2：OpenCode 插件总体设计

截至 2026 年 9 月 3 日，OpenCode 支持：

* 项目级 `.opencode/plugins/`；
* 全局插件目录；
* npm 插件；
* TypeScript 插件；
* custom tools；
* session、message、tool 等事件钩子。([OpenCode][6])

OpenCode 还区分 primary agent 与 subagent；General subagent 的官方用途之一就是并行执行多个工作单元。([OpenCode][7])

服务器 API 支持：

* 创建带 `parentID` 的子 session；
* 查询 children；
* 在指定 message 处分叉 session；
* 异步 prompt；
* abort；
* SSE 事件流。([OpenCode][8])

这恰好足以承载 Sphinx V2。

---

# 十三、V2 OpenCode 包结构

```text
packages/sphinx-opencode/
  package.json
  src/
    index.ts
    config.ts
    runtime.ts

    host/
      opencode-client.ts
      session-fork.ts
      event-router.ts
      output-parser.ts

    scheduling/
      scheduler.ts
      agenda.ts
      conflict-graph.ts
      worker-pool.ts
      lease-manager.ts
      retry-policy.ts
      budget-manager.ts

    persistence/
      sqlite-store.ts
      migrations.ts

    tools/
      start.ts
      status.ts
      cancel.ts
      explain.ts
      export.ts

    rendering/
      parent-session.ts
      diagnostics.ts
```

F# Core 通过 Fable 编译为共享 JS 包：

```text
packages/sphinx-core-js/
```

MCP Host 和 OpenCode Host 都依赖同一个版本：

```json
{
  "@wanxiangshu/sphinx-core": "2.x"
}
```

---

# 十四、V2 的 agent 不是认识论插件

Agent profile 只决定：

* 使用哪个模型；
* 上下文如何隔离；
* 可用工具；
* temperature；
* 输出 schema；
* 是否可修改工作区。

它不决定什么是真。

建议预置以下执行 profile：

```text
sphinx-witness
sphinx-generator
sphinx-comparator
sphinx-forecaster
sphinx-reflector
sphinx-critic
sphinx-renderer
```

这些 profile 的具体 prompt 由当前启用的探究插件提供，而不是写死在 agent 文件里。

例如同一个 `sphinx-witness` 可以执行：

* Borda ballot；
* causal probe；
* ontology proposal；
* future-self prediction；
* Bayes likelihood elicitation。

---

# 十五、OpenCode 配置

```json
{
  "$schema": "https://opencode.ai/config.json",
  "plugin": [
    "@wanxiangshu/sphinx-opencode"
  ],
  "subagent_depth": 1,
  "agent": {
    "sphinx-orchestrator": {
      "mode": "primary",
      "description": "Sphinx inquiry orchestration surface",
      "permission": {
        "task": {
          "*": "deny",
          "sphinx-*": "allow"
        }
      }
    },
    "sphinx-witness": {
      "mode": "subagent",
      "hidden": true,
      "permission": {
        "edit": "deny",
        "bash": "deny",
        "task": "deny"
      }
    },
    "sphinx-reflector": {
      "mode": "subagent",
      "hidden": true,
      "permission": {
        "edit": "deny",
        "bash": "deny",
        "task": "deny"
      }
    },
    "sphinx-renderer": {
      "mode": "subagent",
      "hidden": true,
      "permission": {
        "edit": "deny",
        "bash": "deny",
        "task": "deny"
      }
    }
  }
}
```

OpenCode 当前默认 `subagent_depth` 为 1；设为 2 才允许 subagent 再启动一层 subagent。V2 初期建议保持 1，由插件调度器直接拥有整个 worker tree，避免出现无法纳入 event log 的递归分叉。([OpenCode][9])

`hidden: true` 和 `permission.task` 可以把这些 agent 作为程序化内部 worker，同时限制 primary agent 可调用的 subagent 集合。([OpenCode][7])

这里的权限不是为了“压制模型”，而是保证：

* 不同问卷处理组不相互污染；
* witness 不会无意读取其他分支；
* 单次实验的上下文条件可复现。

---

# 十六、OpenCode 插件外壳

```ts
import { type Plugin, tool } from "@opencode-ai/plugin"
import { SphinxRuntime } from "./runtime"

export const SphinxPlugin: Plugin = async ({
  client,
  directory,
  worktree,
}) => {
  const runtime = await SphinxRuntime.open({
    client,
    directory,
    worktree,
    databasePath: `${directory}/.opencode/sphinx/sphinx.db`,
  })

  return {
    tool: {
      sphinx_start: tool({
        description: "Start a Sphinx research inquiry",
        args: {
          question: tool.schema.string(),
          profile: tool.schema.string().optional(),
        },
        async execute(args, context) {
          return runtime.start({
            parentSessionId: context.sessionID,
            question: args.question,
            profile: args.profile ?? "research-default",
          })
        },
      }),

      sphinx_status: tool({
        description: "Read Sphinx inquiry status",
        args: {
          inquiryId: tool.schema.string(),
        },
        async execute(args) {
          return runtime.status(args.inquiryId)
        },
      }),

      sphinx_cancel: tool({
        description: "Cancel a Sphinx inquiry and its active workers",
        args: {
          inquiryId: tool.schema.string(),
        },
        async execute(args) {
          return runtime.cancel(args.inquiryId)
        },
      }),

      sphinx_explain: tool({
        description: "Explain current certificates and unresolved forks",
        args: {
          inquiryId: tool.schema.string(),
        },
        async execute(args) {
          return runtime.explain(args.inquiryId)
        },
      }),

      sphinx_export: tool({
        description: "Export the reproducible inquiry manifest",
        args: {
          inquiryId: tool.schema.string(),
        },
        async execute(args) {
          return runtime.export(args.inquiryId)
        },
      }),
    },

    event: async ({ event }) => {
      await runtime.onOpenCodeEvent(event)
    },

    "experimental.session.compacting": async (input, output) => {
      const summary = await runtime.compactionContext(input.sessionID)

      if (summary) {
        output.context.push(summary)
      }
    },
  }
}
```

OpenCode 插件接收 `client`，可以添加 custom tools，并监听 `session.idle`、`session.error`、`session.status`、`tool.execute.*` 等事件。([OpenCode][6])

对长 session，只应把：

* 当前根问题；
* 插件锁；
* 证书摘要；
* 未决分支；
* 当前预算；
* event head hash；

注入 compaction，而不是把所有 sibling 回答灌回主上下文。OpenCode 提供专门的 compaction hook。([OpenCode][6])

---

# 十七、V2 完整调度器

## 17.1 调度对象不是“下一个认识论阶段”

调度对象应是：

```text
RefinementProposal
=
(target node,
 refiner plugin,
 required LLM treatment,
 expected certificate change,
 resource request,
 dependencies,
 conflict keys)
```

可能的工作项包括：

```text
生成三个新候选
比较 A 与 B
预测其他分支会选什么
对候选 C 做反向措辞复测
估计某个结果分支
反思前后回答共同锚题
将当前证书渲染为答案
```

## 17.2 Worker 生命周期

```text
Planned
  ↓
Ready
  ↓
Leased
  ↓
Running
  ├──→ InputRequired
  ├──→ Succeeded
  ├──→ Failed → Ready(retry)
  ├──→ Cancelled
  └──→ Superseded
```

每个 lease 包括：

```text
leaseOwner
leaseExpiresAt
heartbeatAt
attempt
childSessionId
promptMessageId
```

## 17.3 精确上下文分叉

```text
Parent Session
    │
    ├── Fork at message M₀ → Branch A → wording A
    ├── Fork at message M₀ → Branch B → wording B
    ├── Fork at message M₀ → Branch C → open response
    └── Fork at message M₀ → Branch D → reversed order
```

OpenCode 当前提供在指定 message 处分叉 session 的 API，因而可以让各处理组拥有相同前缀。([OpenCode][8])

每个 branch 只收到：

```text
公共根上下文
该分支的 ProbeEnvelope
该分支的随机化参数
必要的插件说明
```

不收到：

* sibling 回答；
* 当前候选排名；
* 其他 branch 的得分；
* 最终聚合倾向。

## 17.4 并发派发

```ts
async function dispatchBatch(batch: WorkItem[]) {
  const launchable = batch.filter((item) =>
    dependencyGraph.ready(item) &&
    leaseManager.available(item) &&
    budgetManager.canReserve(item)
  )

  for (const item of launchable) {
    const child = await forkAtRootSnapshot(item)
    await leaseManager.claim(item, child.id)
    await sendStructuredPromptAsync(child.id, item)
  }
}
```

OpenCode 服务器支持同步 message、异步 `prompt_async`、子 session 查询和 abort，因此可以实现真正的 fan-out/fan-in。([OpenCode][8])

## 17.5 汇聚

```text
session.idle / message.updated
        ↓
读取结构化结果
        ↓
校验 workId、branchId、schemaHash
        ↓
追加 ObservationAccepted
        ↓
调用对应 refiner
        ↓
CertificatePatched
        ↓
运行全局 fixed-point closure
        ↓
调度下一批 refinements
```

## 17.6 重试

重试必须遵循：

* 同一 `workId`，新的 `attempt`；
* 新 child session；
* 从原始分叉点重新 fork；
* 不把失败输出带入新 branch；
* 原 attempt 标记为 Failed；
* 多个成功 attempt 不重复计入，除非插件明确把它们当作重复测量。

## 17.7 推测执行

对高方差、长响应任务，可以并行启动两个 attempt：

```text
attempt-1
attempt-2
```

第一个满足 schema 的结果进入主精化；第二个可以：

* 取消；
* 作为复测样本保留；
* 交给插件判断是否可视为独立重复测量。

Core 不决定其认识论含义。

## 17.8 取消

取消 inquiry 时：

1. 追加 `InquiryStatusChanged(Cancelling)`；
2. 找到所有 Running/Leased work；
3. 调用 OpenCode `session.abort`；
4. 将工作项标记 Cancelled；
5. 释放预算预留；
6. 追加 `InquiryStatusChanged(Cancelled)`。

## 17.9 宕机恢复

插件重启：

1. 打开 SQLite；
2. 重放到最新 revision；
3. 查询 OpenCode child session 状态；
4. 已完成但未吸收的结果重新摄取；
5. 已失效 lease 回到 Ready；
6. 正在运行的 session 重新绑定；
7. 保证 `workId + attempt` 幂等。

---

# 十八、V2 的调度算法

不能再用：

```text
最大 expectedRootGain
+ 0.65 × gateway
- cost
```

当前代码确实使用了这种标量近似。

新调度器选择的是“下一次计算”，而不是直接给认识论方案打总分。

## 18.1 Refinement frontier

每个插件提交：

```text
预计减少哪一类歧义
可能产生哪些观测
需要多少资源
与哪些工作冲突
能否并发
```

Scheduler 维护不可比较的前沿：

```text
更能区分认知模式
更能估计措辞效应
更可能改变最终选择
成本更低
对后续问题污染更小
能解锁更多分支
```

不预先写死权重。

## 18.2 批调度

Scheduler 选择一个相容工作集：

$$
B\subseteq\mathcal R
$$

满足：

$$
\sum_{\rho\in B}c(\rho)\leq C_t
$$

并且：

```text
依赖已满足
不违反 branch 隔离
不争用同一独占资源
并发数不超限
```

认知上的收益函数由插件提供，运行上的约束由 Runtime 执行。

---

# 十九、V2 分阶段实施

## V2.0：插件壳

完成：

* npm/local plugin 可加载；
* `sphinx_start/status/cancel/export`；
* 同一 Core；
* 暂时仍串行执行。

退出条件：

```text
MCP 与 OpenCode 对同一事件序列产生相同状态哈希
```

## V2.1：一个 WorkItem 对应一个 child session

完成：

* create/fork；
* structured output；
* session event 回收；
* abort；
* 重试。

## V2.2：并行 fan-out/fan-in

完成：

* worker pool；
* concurrency limit；
* lease；
* 依赖 DAG；
* 批量吸收；
* sibling 隔离。

## V2.3：随机问卷引擎

完成：

* permutation；
* blind labels；
* split-ballot；
* open-before-closed；
* anchor；
* carryover；
* commit–reveal–revise。

## V2.4：序数和 truthful 引擎

完成：

* Borda baseline；
* Robust Borda；
* Bradley–Terry；
* mixture model；
* self-prediction；
* peer prediction；
* BTS／SP 插件。

## V2.5：混合精化调度

完成：

* exact refiner；
* bound refiner；
* sample refiner；
* ordinal refiner；
* 同一证书上的并行更新；
* value-of-computation 调度。

## V2.6：科研输出

完成：

* protocol manifest；
* model manifest；
* branch tree；
* framing matrix；
* ranking posterior；
* calibration；
* initial/reflective disposition；
* replay bundle。

---

# 二十、数学附录 A：母体对象

设 Sphinx 的计算空间是一个有向类型化超图：

$$
\mathcal G=(V,E)
$$

每个节点 \(v\in V\) 保存一个待求值对象，每条超边 \(e\in E\) 表示一个局部组合、转移、消元或选择关系。

一个插件族 \(\Phi\) 声明：

$$
\Phi=
\left(
\mathcal X,
\preceq,
\oplus,
\otimes,
\mathfrak M,
K,
\psi,
\mathcal N
\right)
$$

其中：

* \(\mathcal X\)：值域，可以是实数、分布、Pareto antichain 或后验；
* \(\preceq\)：值的偏序；
* \(\oplus\)：备选分支之间的组合；
* \(\otimes\)：局部值与未来值的组合；
* \(\mathfrak M\)：结果变量的消元／聚合；
* \(K\)：转移或观测核；
* \(\psi\)：局部势函数；
* \(\mathcal N\)：必要的规范化算子。

定义广义固定点算子：

$$
(\mathfrak F_\Phi X)(v)
=
\bigoplus_{e\in\operatorname{Out}(v)}^{\Phi}
\left[
\psi_e
\otimes_\Phi
\mathfrak M_{\omega\sim K_e}^{\Phi}
X\!\left(\tau(v,e,\omega)\right)
\right]
\tag{1}
$$

最终对象是：

$$
X^*=\operatorname{Fix}(\mathfrak F_\Phi)
\tag{2}
$$

在动作选择节点上，式（1）表现为 Bellman 方程。

在概率因子节点上，式（1）表现为 sum-product 消息传播。

在最短路节点上，表现为 min-plus 递推。

在只能采样的随机节点上，\(\mathfrak M\) 由经验估计逼近。

广义分配律正是把 sum-product、min-sum 等多类算法放入共同消息传递框架的重要理论基础。([authors.library.caltech.edu][10])

---

# 二十一、数学附录 B：Bayes 退化

令：

* 没有可控动作；
* 潜在假设为 \(H\)；
* 先验为 \(p_0(h)\)；
* 观测为 \(y_1,\ldots,y_n\)；
* 每个观测对应似然因子 \(L_j(h)=p(y_j\mid h)\)；
* \(\otimes=\times\)；
* 隐变量消元 \(\oplus=\sum\)。

则未归一化消息为：

$$
\widetilde p(h)
=
p_0(h)\prod_{j=1}^{n}L_j(h)
\tag{3}
$$

归一化：

$$
p(h\mid y_{1:n})
=
\frac{\widetilde p(h)}
{\sum_{h'}\widetilde p(h')}
\tag{4}
$$

这就是 Bayes。

对于动态潜在状态：

$$
b_{t+1}(z')
=
\frac{
O(y_{t+1}\mid z',a_t)
\sum_z
P(z'\mid z,a_t)b_t(z)
}{
\sum_{\bar z'}
O(y_{t+1}\mid \bar z',a_t)
\sum_z
P(\bar z'\mid z,a_t)b_t(z)
}
\tag{5}
$$

当前实现的 Bayes 代码正是在固定假设空间中按证据似然乘积并归一化。

因此严格退化条件是：

```text
无动作选择
完整概率因子
sum-product 值代数
精确消元
正的归一化常数
```

Bayes 插件应输出一个 singleton certificate：

$$
\gamma(C)=\{p(H\mid Y)\}
$$

---

# 二十二、数学附录 C：A* 退化

令：

* 状态完全可观测；
* 转移是确定性的；
* 结果变量只有一个点；
* 成本非负；
* \(\otimes=+\)；
* \(\oplus=\min\)；
* 终点 \(g\) 的代价为 0。

式（1）退化为：

$$
J(s)
=
\min_{a\in A(s)}
\left[
c(s,a)+J(T(s,a))
\right]
\tag{6}
$$

这是最短路 Bellman 方程。

A* 并不是式（6）之外的另一种价值语义，而是它的一个有界求解日程。

对路径前缀节点 \(n\)：

$$
L(n)=g(n)+h(n)
\tag{7}
$$

其中：

$$
h(n)\leq J^*(n)
\tag{8}
$$

A* 每次展开最小 \(L(n)\) 的节点，并维护当前可行解上界 \(U\)。

当找到更优的 \(g\) 时重新开放节点。当前 A* 实现也显式支持这一点，并拒绝负成本图。

严格退化条件：

```text
确定性转移
min-plus 值代数
非负边成本
固定目标
可采纳启发式
按最小下界展开
发现更优 g 时重开
```

A* 的证书是：

$$
C_n=
\left(
L_n=g_n+h_n,\;
U_n,\;
\text{parent}_n,\;
\text{closed}_n
\right)
\tag{9}
$$

---

# 二十三、数学附录 D：MCTS 退化

令：

* 动作可控；
* 转移与观测是随机的；
* \(\oplus=\max\)；
* \(\otimes=+\)；
* \(\mathfrak M=\mathbb E\)。

则：

$$
Q(b,a)
=
r(b,a)
+
\gamma
\mathbb E_{y\sim p(\cdot\mid b,a)}
V\!\left(\tau(b,a,y)\right)
\tag{10}
$$

$$
V(b)=\max_a Q(b,a)
\tag{11}
$$

当 \(K\) 无法枚举、只能调用生成式模拟器时，用：

$$
\widehat Q_n(b,a)
=
\frac{1}{N_n(b,a)}
\sum_{k=1}^{N_n(b,a)}R_k
\tag{12}
$$

逼近期望。

UCT 的典型采样日程为：

$$
a_n
=
\arg\max_a
\left[
\widehat Q_n(b,a)
+
c
\sqrt{
\frac{\log N_n(b)}
{N_n(b,a)}
}
\right]
\tag{13}
$$

UCT 论文在有限时域或折扣 MDP 条件下给出了相合性和有限样本分析。([施普林格自然链接][11])

在部分可观测情形，POMCP 同时使用 Monte Carlo 信念更新和 Monte Carlo tree search，只要求黑箱模拟器。([NIPS 会议论文集][12])

严格退化条件：

```text
随机 Bellman 语义
只能访问生成式转移核
经验均值近似期望
UCT/PUCT 分配采样
visit/value 统计参与证书，不充当外部真值
```

---

# 二十四、数学附录 E：三种退化的统一表

| 母体成分 | Bayes     | A*                  | MCTS             |
| ---- | --------- | ------------------- | ---------------- |
| 隐状态  | 假设 \(H\)  | 无或完全可见              | 状态／belief        |
| 控制动作 | 无         | 有                   | 有                |
| 分支聚合 | 求和        | 最小值                 | 最大值              |
| 时间组合 | 乘法        | 加法                  | 奖励加期望            |
| 结果消元 | 精确求和      | 恒等                  | 期望               |
| 求解方式 | 精确因子消息    | 下界驱动展开              | 样本逼近期望           |
| 证书   | posterior | \(g+h\) 与 incumbent | \(N,\hat Q,\) 区间 |
| 日程   | 拓扑／消息传递   | best-first          | UCT/PUCT         |

因此最终理论的正确表述是：

> **Sphinx-GEC 是母体；Bellman 是其决策固定点面；Bayes、A*、MCTS 是在不同值代数、可观测性与计算访问条件下的三个可验证退化。**

---

# 二十五、数学附录 F：混合证书

定义证书：

$$
C_{v,a}
=
\left(
\mathcal L,
\mathcal U,
\widehat\mu,
N,
S^2,
\Omega,
\Theta,
W
\right)
\tag{14}
$$

其中：

* \(\mathcal L\)：价值下包络；
* \(\mathcal U\)：价值上包络；
* \(\widehat\mu\)：采样均值或后验；
* \(N\)：样本数；
* \(S^2\)：样本方差；
* \(\Omega\)：序数约束；
* \(\Theta\)：潜变量后验；
* \(W\)：所有观测和推导 witness。

设 \(\gamma(C)\) 是该证书允许的精确值集合。

一个精化 \(\rho\) 是 sound 的，当：

$$
\gamma(\rho(C))
\subseteq
\gamma(C)
\tag{15}
$$

于是：

* Bayes 精确消元可把 \(\gamma(C)\) 收缩为单点；
* A* 展开收紧上下界；
* MCTS 样本收紧经验后验；
* Borda／BTL 增加序数约束；
* split-ballot 更新 framing 参数。

同一节点允许这些操作交错进行。

---

# 二十六、数学附录 G：调度器选择“计算价值”

令 \(\mathcal D(C)\) 为当前决策歧义。其定义由插件提供，例如：

* Bayes：后验熵或决策风险；
* A*：上下界 gap；
* MCTS：best-action error posterior；
* 排名：首位候选后验不确定性；
* 问卷：措辞效应未识别程度；
* 多模态模型：mode assignment entropy。

一次 refinement \(\rho\) 的计算价值：

$$
\operatorname{VOC}(\rho\mid C)
=
\mathbb E
\left[
\mathcal D(C)
-
\mathcal D(C\oplus\Delta_\rho)
\right]
-
\lambda\,c(\rho)
\tag{16}
$$

其中：

* \(\Delta_\rho\)：可能产生的证书变化；
* \(c(\rho)\)：token、调用次数、延迟等资源；
* \(\lambda\)：不是 Kernel 常量，而是当前资源／偏好插件的一部分。

并行批次：

$$
B^*
=
\arg\max_{\substack{
B\subseteq\mathcal R\\
B\text{ compatible}\\
c(B)\leq B_t
}}
\mathbb E
\left[
\mathcal D(C)
-
\mathcal D
\left(
C\oplus
\sum_{\rho\in B}\Delta_\rho
\right)
\right]
\tag{17}
$$

精确求解太贵时，可由 scheduler 插件提供：

* greedy；
* Pareto；
* Thompson scheduling；
* index policy；
* MCTS over computations；
* LLM 序数调度。

Core 只负责执行返回的相容工作集。

---

# 二十七、数学附录 H：LLM 潜在认知与问题干预

设 LLM 的潜在认知状态为：

$$
z_t
$$

一次探究问题定义为：

$$
q_t=(\kappa_t,f_t,\pi_t)
$$

其中：

* \(\kappa_t\)：要探测的构念；
* \(f_t\)：措辞、标签、顺序、响应格式；
* \(\pi_t\)：施测协议。

问题可能先改变认知状态：

$$
z_t^+
\sim
P(z^+\mid z_t,q_t)
\tag{18}
$$

随后产生回答：

$$
y_t
\sim
O(y\mid z_t^+,q_t)
\tag{19}
$$

再形成后续状态：

$$
z_{t+1}
\sim
C(z'\mid z_t^+,q_t,y_t)
\tag{20}
$$

因此必须分别估计：

### 初始倾向

$$
\theta^{(0)}
$$

### 协议下的反思倾向

$$
\theta^{(\Pi)}
$$

### 探究造成的迁移

$$
\Delta_\Pi
=
d\!\left(
\theta^{(0)},
\theta^{(\Pi)}
\right)
\tag{21}
$$

变化不是自动等于偏差。它可能是：

* 发现矛盾；
* 新理由整合；
* 概念重构；
* 从直觉进入反思；
* 从一个潜在模式切换到另一个模式。

---

# 二十八、数学附录 I：从 Borda 到潜变量排名

## 28.1 Borda 基线

对候选 \(i\)：

$$
B_i
=
\sum_r
\sum_{j\neq i}
\mathbf 1[i\succ_r j]
\tag{22}
$$

它等价于累计候选在各 ballot 中战胜多少其他候选。

## 28.2 带问法效应的 Bradley–Terry

设 branch \(r\) 比较 \(i,j\)：

$$
\Pr(i\succ j\mid r,f,o,\pi)
=
\sigma
\left[
\theta_i-\theta_j
+
\alpha_{f,i}-\alpha_{f,j}
+
\beta_o
+
\gamma_\pi
+
u_r
\right]
\tag{23}
$$

其中：

* \(\theta_i\)：稳定潜在偏好；
* \(\alpha_{f,i}\)：措辞对候选 \(i\) 的影响；
* \(\beta_o\)：左右／顺序效应；
* \(\gamma_\pi\)：协议效应；
* \(u_r\)：branch 随机效应。

Bradley–Terry 模型本来就是为不完全区组中的成对比较设计的。([JSTOR][13])

## 28.3 鲁棒污染模型

$$
\Pr(y_{r,ij}=1)
=
(1-\epsilon_r)
\sigma(\cdots)
+
\epsilon_r\frac12
\tag{24}
$$

\(\epsilon_r\) 不表示“这个 LLM 不可信”，而表示这次测量可能受：

* 含混；
* 注意漂移；
* 格式失败；
* 多种模式竞争；
* 位置服从；

影响。

## 28.4 多认知模式

$$
p(y)
=
\sum_{k=1}^{K}
\pi_k
p(y\mid\theta^{(k)})
\tag{25}
$$

最终不必强行平均成一个排序，而可报告：

```text
模式 A：理论完整性优先
模式 B：可执行性优先
模式 C：最小风险优先
```

及各自后验质量。

普通 Borda 是式（23）—（25）在以下条件下的低阶退化：

```text
所有 branch 等权
无措辞效应
无顺序效应
无协议效应
无多模态
无相关结构
```

---

# 二十九、数学附录 J：proper self-prediction

让 branch \(r\) 预测未来反思后的自己：

$$
q_r^{\mathrm{future}}(i)
=
\Pr(
X_r^{\mathrm{future}}=i
)
$$

实际反思后结果为 \(x_r^{\mathrm{future}}\)。

对数分数：

$$
S_{\log}
=
\log
q_r^{\mathrm{future}}
\left(
x_r^{\mathrm{future}}
\right)
\tag{26}
$$

Brier 的最大化写法：

$$
S_{\mathrm{Brier}}
=
2q_y-\sum_i q_i^2
\tag{27}
$$

可以分别计算：

* 对自己未来选择的校准；
* 对其他 branch 的校准；
* 对排名稳定性的校准；
* 对“我会不会被某反例改变”的校准。

评分不直接替代答案。它更新的是：

```text
这个 branch 的自我预测能力
这个探究协议的显影能力
该模型在此类问题上的反思稳定性
```

---

# 三十、数学附录 K：随机问卷的可识别量

同一上下文快照随机分配到问法 A、B：

$$
q_A,\quad q_B
$$

即时问法效应：

$$
\Delta_{\mathrm{surface}}
=
d
\left(
Y(q_A),
Y(q_B)
\right)
\tag{28}
$$

所有 branch 随后回答共同锚题 \(q_0\)。

持续携带效应：

$$
\Delta_{\mathrm{carry}}
=
d
\left(
Y(q_0\mid q_A),
Y(q_0\mid q_B)
\right)
\tag{29}
$$

反思效应：

$$
\Delta_{\mathrm{reflect}}
=
d
\left(
Y_{\mathrm{before}},
Y_{\mathrm{after\ reasons}}
\right)
\tag{30}
$$

随机化 manifest 必须包含：

```text
root snapshot hash
branch IDs
treatment assignment
candidate permutation
label permutation
question version
anchor version
seed
model and sampling parameters
```

---

# 三十一、数学附录 L：停止证书

停止不是 Kernel 写死的阈值，而是插件产生证书。

例如：

$$
\Pr(
a^*(\theta)=\hat a
\mid C
)
\geq 1-\delta
\tag{31}
$$

并且：

$$
\Pr(
a^* \text{ 在另一问法族下反转}
\mid C
)
\leq \epsilon
\tag{32}
$$

以及：

$$
\max_{\rho\in\mathcal R}
\operatorname{VOC}(\rho\mid C)
\leq 0
\tag{33}
$$

多模态时还应要求：

* 主要模式都已得到表达；
* 最终答案不能消灭少数但稳定的模式；
* 未决分叉被明确报告。

一个科研 profile 可以要求：

```text
ranking-stable
framing-estimated
future-self-calibrated
major-modes-covered
budget-respected
```

另一个 profile 可以只要求其中两项。

Core 不认识这些名称。

---

# 三十二、数学附录 M：待证明定理

## 定理目标 1：Bayes 退化一致性

在固定假设、正先验、完整有限似然和正归一化常数下，GEC 的 sum-product 插件输出式（4）的唯一 posterior。

## 定理目标 2：A* 退化一致性

在有限确定图、非负成本、可采纳启发式和正确重开策略下，GEC 的 min-plus bound refiner 返回最小成本路径。

## 定理目标 3：MCTS 退化一致性

在有限动作、有限时域或折扣回报、生成式转移核和公平 UCT 采样条件下，sample certificate 的动作价值估计依概率或几乎处处收敛。

## 定理目标 4：异步混合精化

若：

1. 每个 refiner 都满足式（15）；
2. 所有必要依赖最终都会被调度；
3. 样本估计强相合；
4. 独立 GraphDelta 可交换；
5. stop certificate 对 \(\gamma(C)\) 中所有可行精确值都稳健；

则异步 Exact、Bound、Sample、Ordinal 精化收敛到同一个决策等价类。

这条目前应标为**待完成证明**，不要在文档里先宣布已证。

## 定理目标 5：宿主等价

若 MCP 与 OpenCode Host 提交相同的：

```text
CoreEvent 序列
插件版本
随机种子
资源账本
```

则物化状态、证书和答案 hash 相同。

## 定理目标 6：问法效应可识别

在共同根快照、随机处理分配、无 branch 泄漏和共同锚题条件下，式（28）和式（29）可以作为对应协议中的平均处理效应估计。

---

# 三十三、不可识别性边界

必须正式写入理论：

设两个外部世界 \(W_1,W_2\) 在所有允许的探究协议 \(q\) 下，都诱导完全相同的 LLM 回答分布：

$$
P(Y\mid q,W_1)
=
P(Y\mid q,W_2)
\quad
\forall q
\tag{34}
$$

则任何只观察这些 LLM 回答的 Sphinx，都不可能区分 \(W_1,W_2\)。

因此在没有其他可信源时，Sphinx 可以科研级识别：

* LLM 的初始判断；
* 反思判断；
* 稳定核心；
* 潜在认知模式；
* 自我预测能力；
* 问法、顺序和协议效应；
* 进一步探究是否可能改变选择。

但它不能在无附加假设的情况下，把“模型稳定相信 \(X\)”直接证明成“外部世界中 \(X\) 为真”。

正确输出应区分：

```text
model-belief
reflective-model-belief
cross-branch consensus
protocol-stable judgment
externally-grounded claim
```

最后一种在当前假设下可以为空。

---

# 三十四、测试矩阵

## Core 测试

```text
event replay determinism
snapshot deletion replay
revision monotonicity
duplicate submit idempotence
independent delta commutativity
certificate refinement monotonicity
plugin version lock
budget conservation
lease expiry recovery
```

## 插件测试

```text
Bayes exact conformance
A* shortest-path conformance
A* reopen conformance
MCTS generative-model convergence
Borda permutation invariance
BTL synthetic parameter recovery
mixture-mode recovery
proper-score calibration
split-ballot treatment recovery
anchor carryover recovery
```

## Host 等价测试

```text
same event trace → same MCP projection
same event trace → same OpenCode projection
same answer hash
same certificate hash
```

## 问卷测试

```text
candidate label permutation
left/right reversal
question polarity reversal
open-before-closed versus closed-before-open
candidate clone insertion
candidate removal stability
commit/reveal branch isolation
future-self held-out calibration
```

## 调度测试

```text
dependency DAG
fan-out limit
fan-in completeness
retry from clean branch
speculative duplicate
cancel propagation
worker crash
plugin crash
process restart
budget exhaustion
```

## 黄金轨迹测试

当前示例要同时跑两种模式：

```text
legacy-exact:
  重现 revision 0 → 57 的旧行为

research-modern:
  将原串行探索改为分支批处理，
  最终输出初始倾向、反思倾向、排名后验和 framing diagnostics
```

---

# 三十五、建议的逐提交实施顺序

## Commit 1

新增 ADR、PROPOSED_EPI_015 至 PROPOSED_EPI_018，不改代码。

验证：

```text
docs lint
```

## Commit 2

把当前 `main.jsonl` 加入 fixture，并实现 transcript replay test。

验证：

```text
legacy fixture 可解析
```

## Commit 3

增加 `Core/Ids.fs`、`JsonEnvelope.fs`、`Events.fs`。

验证：

```text
稳定序列化
内容哈希
ID round-trip
```

## Commit 4

实现纯 `Reducer.fs`：

```text
state × event → state
```

验证：

```text
fold deterministic
```

## Commit 5

实现 SQLite EventStore 和 snapshots。

验证：

```text
重启恢复
删除 snapshot 后重放
```

## Commit 6

增加 PluginManifest、PluginRegistry、PluginLock。

验证：

```text
版本冲突
缺少依赖
schema hash 不符
```

## Commit 7

把现有全部类型和行为包进 `LegacyPlugin`。

验证：

```text
旧测试全部绿
```

## Commit 8

将 `Bayes.fs` 移入 Bayes plugin。

验证：

```text
现有 bayes tests 不变
新增 generalized-certificate test
```

## Commit 9

将 `Search.fs` 移入 A* plugin。

验证：

```text
最短路、重开、负成本测试
```

## Commit 10

将 `MonteCarlo.fs` 移入 MCTS plugin。

验证：

```text
UCT、共享节点、采样回溯
```

## Commit 11

实现 `ValueCertificate` 和 `RefinementEngine`。

验证：

```text
三插件可同时激活
删除 SolverMode
```

## Commit 12

实现 generic WorkEnvelope 和 MCP `work_submit`。

验证：

```text
单 work
批 work
expectedRevision
重复提交
```

## Commit 13

把旧四阶段工具改为 Adapter。

验证：

```text
旧 stdio/MCP contract 测试全部通过
```

## Commit 14

实现 split-ballot、Borda、BTL。

验证：

```text
随机排列不改变潜在排序估计
位置效应可恢复
```

## Commit 15

实现 self-prediction 和 proper scoring。

验证：

```text
held-out future-self calibration
```

## Commit 16

实现 OpenCode 插件壳和串行 child session。

验证：

```text
本地插件自动加载
工具可调用
child 结果回收
```

## Commit 17

实现 fork、worker pool、lease、retry、abort。

验证：

```text
8 个并发隔离分支
失败恢复
取消传播
```

## Commit 18

实现 mixed-refinement scheduler。

验证：

```text
Exact、Bound、Sample、Ordinal 同时在一个 inquiry 中工作
```

## Commit 19

实现 research manifest 和 replay export。

验证：

```text
全新进程从 bundle 得到相同结果 hash
```

---

# 三十六、最终发布边界

## Sphinx V1 / MCP

应发布为：

```text
@wanxiangshu/sphinx-core
@wanxiangshu/sphinx-mcp
@wanxiangshu/sphinx-plugins-default
```

具备：

* 旧八工具兼容；
* 新通用 work 协议；
* 持久事件；
* 插件 ABI；
* 三种经典退化；
* 基础问卷／排序；
* delegated、direct-provider、hybrid 三种模式。

## Sphinx V2 / OpenCode

应发布为：

```text
@wanxiangshu/sphinx-opencode
```

它只增加：

* OpenCode custom tools；
* child session 创建与 fork；
* subagent worker profiles；
* 异步 fan-out/fan-in；
* lease、重试、取消；
* 完整资源调度；
* 主 session 结果呈现。

它不复制：

* 图；
  -证书；
* 插件；
* Bayes；
* A*；
* MCTS；
* Borda；
* 停止逻辑。

---

# 最终结论

最正确的路线不是：

```text
V1 = 一个 MCP Agent
V2 = 另写一个 OpenCode 多 Agent
```

而是：

```text
Sphinx-GEC Core
  ├── V1 MCP Host
  └── V2 OpenCode Host
```

理论上的统一也不是：

```text
Bellman / A* / MCTS 三选一
```

而是：

```text
广义因子—动作固定点
  ├── Bayes：sum-product 精确退化
  ├── A*：min-plus 有界展开退化
  └── MCTS：sample-expectimax 退化
```

Borda、Bradley–Terry、BTS、proper scoring 和问卷随机化，则不是第四个求解器，而是向这个统一认识图提供：

> **偏好、元预测、问法效应、认知迁移和价值函数的可观测约束。**

这样做以后，Sphinx 的 Kernel 不负责制服 LLM，也不负责宣判哪一种认识论正确。它负责建造一个稳定的实验空间，使 LLM 能够：

* 在相同上下文上分叉；
* 在盲化条件下表达；
* 用排序而非伪精确评分作比较；
* 预测其他分支和未来自己；
* 接受最强反例后修正；
* 显示多个稳定认知模式；
* 让 Bayes、A*、MCTS 在同一证书空间中互补；
* 最终给出可回放、可解释、协议条件明确的科研结果。

这才是从当前 Sphinx 原型通往科研级 V1 MCP 与 V2 OpenCode 智能体框架的连续路线。
