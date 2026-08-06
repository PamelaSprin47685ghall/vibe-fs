# Proposal: F# 结构化流程 DSL 治理加固

未裁决候选。不是当前规范，也不是可直接实施的迁移计划。本文末尾保留的 ChatGPT 导出仅是研究输入；其中的路径、计数、结论与代码草图来自未入库的 `repomix-output(20260806-085150).xml`，使用前必须对当前 HEAD 重新取证。

## Problem

ARCH-001 与 FLOW-005 要求业务控制流由结构化程序表达，但现有 DSL 门禁主要依赖路径、名称与局部语法形态。它能阻断已知坏形状，不能单独证明任意 DU、可变 cell 或等待循环没有把执行位置编码成数据。

## Current baseline

- 正式合同位于 `docs/what/architecture.md`、`docs/what/flow.md` 与对应 `shape/how/proof` 文件；它们仍是唯一实现依据。
- `scripts/checks/dsl-ownership.mjs` 与 ratchet 是当前静态门禁，测试与人工评审补足语义判定。
- 原始研究列出的 `NodeProcessWait`、Blogger runtime、Companion recovery、重复类型与事实代数是待复核候选问题，不因进入本文而成为已确认 gap。

## Goal

- 建立可复现的语义审计方法，区分领域事实、物理资源状态与程序计数器。
- 将每个确认的问题拆成单一所有权、单一兼容性处置、可独立证明的 proposal。
- 只有在现行规范无法充分表达目标时才修改正式层；纯实现偏差进入 `status/`。

## Non-goals

- 不以 DU case 数、`mutable` 数量或名称黑名单直接判定架构违规。
- 不把本文的 CE 草图、PR 顺序或阈值当作已裁决设计。
- 不依据缺失附件直接修改生产代码。

## Impact map

- what: `docs/what/architecture.md`、`docs/what/flow.md`
- shape: `docs/shape/architecture.md`、`docs/shape/flow.md`
- how: `docs/how/architecture.md`、`docs/how/flow.md`
- proof: `docs/proof/architecture.md`、`docs/proof/flow.md`、`docs/proof/verify.md`
- code/resources: `src/Wanxiangshu/` 中经当前 HEAD 复核后确认的各独立所有权边界；`scripts/checks/dsl-ownership.mjs`

## Alternatives

- 只扩充禁用名称：成本低，但改名即可绕过。
- 以统一 case 数或布尔数量阈值失败：机械可判定，但会误伤合法词汇、证据与局部算法状态。
- 仅靠人工评审：能判断语义，但缺少可重现证据与回归保护。

## Migration / cutover

先为每个候选问题补当前源码证据与能判红的回归，再按所有权拆分 proposal。每个被接受的子 proposal 按 GOV-006 原子更新正式层；实现落后时另建 `status/`，禁止把本研究转录直接搬入 status 或代码。

## Compatibility disposition

`ExplicitMigration`，仅作为当前合并候选的最严格上界。拆分后的子 proposal 必须分别选择 `Compatible | ExplicitMigration | ExplicitReset | CleanBreak`；未拆分前不得裁决。

## Proof plan

- 对每个候选点给出当前 HEAD 的精确源码位置、可表示非法状态或第二运行时的最小证明。
- 先破坏目标性质，确认新增门禁或测试确实变红；再提交修复。
- 保持 `npm run lint`、单元、集成与相关 canary 绿，不用名称统计代替行为证明。

## Decision owner

未指定。裁决前必须由架构负责人认领，并确认拆分后的 Impact map、兼容性处置与 proof plan。

## Admission blockers

- 原始证据附件未入库，无法从本文复核快照来源。
- 当前文本捆绑进程等待、Companion、Blogger、持久事实、角色类型与门禁等不同所有权。
- 若干建议是启发式审查条件，不是可直接机械执行的正确性判据。

## Research appendix：原始 ChatGPT 导出

以下内容原样保留用于追溯思路；其中“最终裁决”“推荐 PR 顺序”等措辞只属于原对话，不代表 GOV-006 裁决。

**User:** Anonymous  
**Created:** 8/6/2026 16:52:11  
**Updated:** 8/6/2026 17:47:38  
**Exported:** 8/6/2026 17:58:43  
**Link:** [https://chatgpt.com/c/6a744b38-bda4-83eb-aa7d-226a87fa7d7a](https://chatgpt.com/c/6a744b38-bda4-83eb-aa7d-226a87fa7d7a)  

## Prompt:
8/6/2026, 4:52:08 PM

目前写代码的工程师对使用 F# DSL 而不是状态机和布尔地狱的规定还是阳奉阴违，十分草率。例如，随意引入动辄十几二十种的代数数据类型却不分析是否可以用结构化程序表达，等等。你发现了吗？

[Attachment: repomix-output(20260806-085150).xml]

## Response:
8/6/2026, 4:52:10 PM

我会把“规定本身”“实际代码形态”和“自动检查是否真正约束住”分开核对，重点找大枚举式 DU、程序计数器状态，以及本可由顺序/组合子表达却被状态化的流程。

第一处制度性问题已经很明确：规范要求“F# 调用栈就是流程栈”，但现有门禁的基线文件只按文件统计 `mutable`，它最多防止新增可变点，不能判断一个大型 DU 究竟是领域事实还是伪装的程序计数器。

扫描结果里确实有一个比“十几二十种”更直接的例子：`AgentFact` 单个 DU 有 **41 个 case**，同时横跨 Prompt、Authority、Fallback、Review、Execution、Orchestrator、Companion 等域。大 DU 本身不一定错，但这种跨域总和类型至少应提交“为什么不能分层/分域”的证明；仓库里目前看到的是注释性辩护，不是结构选择的比较。

> **检索F# DSL结构化编程相关资料**
>
> 
>
> **枚举 F# 联合类型及其案例**
>
>
> **枚举F#联合类型并检查文件格式十六进制内容**
>
> 
>
> **解析 XML 提取 F# 联合类型**
>
>
> **解析并检查F#状态类型、DSL归属和恢复流程一致性**
>
> 
>
> **列出 Fact 类型全部案例**
>
>
> **列出并分析Fact案例及博客运行状态**
>
> 
>
> **搜索并定位关键代码片段**
>
> 
>
> **统计可变状态与联合类型**
>
>
> **核查状态约束**
>
> 我正在核对实现与文档是否一致：路径豁免似乎过于宽泛，程序计数器检查依赖变量名，可能漏掉语义等价字段；此外，多枚独立状态字段还会形成至少六十种可表示组合，其中不少组合或许并不合法。
>
> **核查状态约束**
>
> 
>
> **完成状态统计、DSL状态机与程序检查并执行所有权棘轮**
>
> 
>
> **评估状态设计**
>
> 我正在区分真实问题与合理枚举：核心缺陷包括词法式检查易被改名绕过、多个独立字段形成无效组合，以及流程状态混入普通可变布尔量。重复类型则提示领域边界和职责可能过度分散，但大型联合类型本身不一定错误。
>
> **评估了状态设计**
>
> 
>
> **分析可变状态门控漏洞并确认双门禁类型定义**
>
> 
>
> 思考了 5m 31s

**发现了，而且不是零星的风格问题，而是“规范很强、门禁很弱、代码靠改名绕过”的系统性偏差。**

我实际执行了当前仓库的：

```text
dsl-ownership.mjs --threshold=0
dsl-ownership-ratchet.mjs
```

两者都是绿色。但这个绿色不能证明“结构化程序 DSL 已经主导”，只能证明没有命中那几条正则表达式。

## 1. 门禁基本是名称黑名单，无法识别语义上的状态机

规范明确要求业务流程直接使用 `task`、`let!`、`match`、`return!`，并规定 F# 调用栈就是流程栈，禁止把执行位置重新编码为状态。`repomix-output(20260806-085150).xml`

实际门禁却有两个根本漏洞：

- `Domain`、`Session`、`Application` 中所有 `let mutable` 都按**路径整体豁免**，并没有判断它究竟是算法 scratch、物理资源 cell，还是业务程序计数器。
- program-counter 检查只识别 `Dirty`、`Running`、`RepairSpent` 等几个具体名字；改叫 `InFlight`、`Parked`、`Sealed`、`Drain` 就不会命中。
- `Process` 和 `Infrastructure` 根本不在 DSL 检查目录中。

这些都直接写在扫描器里。`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

所以现在的门禁实际约束是：

> 不要使用我们已经想起来的几个坏名字。

而不是：

> 不要把控制流重新编码为数据。

这就是最主要的“阳奉阴违”通道。

## 2. `BloggerRuntimeCell` 已经是典型的状态机乘积

这里不仅有一个五态 DU：

```fsharp
Idle | InFlight | Parked | Sealed | Disposed
```

旁边还平行放了：

- `PendingOffer: option`
- 三态 `Recovery`
- 两态 `Drain`

并配套七种 `TransitionError` 和一组 `onMaterial`、`beginRequest`、`onCycleCommitted` 等状态转移函数。`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

仅按顶层标签计算，它至少允许：

```text
5 × 2 × 3 × 2 = 60
```

种组合，尚未计算 `InFlight` 的负载内容。这正是规范自己所说的“状态标签加正交旗标形成组合行为”。

这里不是说运行期协调永远不能保存状态，而是代码没有回答关键问题：

> 为什么生命周期不能由一个拥有资源作用域的结构化异步流程表达，而必须由可长期任意调用的 transition API 表达？

当前注释只是逐个解释 case 的含义，没有比较过结构化程序方案。

## 3. 有变量甚至公开承认自己是“control-flow state”

`Companion` 中的 `slotArmed` 注释原文就写着：

> Control-flow state only

随后提供 `ArmRecoverySlot`、`DisarmRecoverySlot` 和 `IsRecoveryArmed`，由锁保护地反复修改。`repomix-output(20260806-085150).xml`

这不是“可能像程序计数器”，而是实现自己明确承认它就是控制流状态。

它更自然的表达通常应是：

```fsharp
runNormalSlot (...)
runSlotAfterFailure (...)
```

或将 `SlotArming` 作为当前结构化递归调用的参数，由失败分支直接调用下一步，而不是存入跨调用对象，等待别的入口以后读取、消费和清除。

现在虽然把 `bool` 包装成了 `SlotArming` DU，但真正的运行时所有权仍是可变布尔。类型名变强了，控制结构没有变。

## 4. `NodeProcessWait` 是非常直接的布尔地狱

一个循环同时维护：

```fsharp
timedOut
cancelled
killSent
killAckExpired
```

然后在 `while` 条件、循环内部和循环结束后重新推导自己处于哪个阶段。`repomix-output(20260806-085150).xml`

这个流程本来就天然是线性的：

```text
等待正常退出或 deadline
→ deadline 到达则 kill
→ 等待 kill acknowledgement
→ 返回真实退出 / kill 未确认 / cancellation
```

可以直接拆成三个 `task` 函数并用返回值连接，无需四个可变布尔共同承担程序计数器。

尤其值得注意的是：这个文件位于 `Process`，完全不在 DSL 门禁扫描范围内。即便把四个字段扩展成八个，当前门禁仍会保持绿色。

## 5. ADT 数量确实出现了“建模即枚举”的倾向

我对生产 F# 源码作了结构扫描：

- 143 个 DU；
- 48 个有至少 5 个 case；
- 9 个有至少 8 个 case；
- 5 个有至少 10 个 case；
- 最大的是 `AgentFact`，有 **41 个 case**。

`AgentFact` 同时容纳 Prompt、Authority、Fallback、Review、Execution、Orchestrator、Companion、上下文恢复等事实族。文件自己也通过分段标题显示了这些不同辖区。`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

这不属于“应改成顺序程序”的直接例子，因为它是持久事实集合；但它是另一个问题：

> 一个跨多个 bounded context 的 41-case 总和类型，为何仍由同一个模块、codec 和 fold 顶层所有？

比较稳妥的形状应当类似：

```fsharp
type PromptFact = ...
type ReviewFact = ...
type ExecutionFact = ...
type OrchestratorFact = ...

type AgentFact =
    | Prompt of PromptFact
    | Review of ReviewFact
    | Execution of ExecutionFact
    | Orchestrator of OrchestratorFact
```

这样至少能让每个子代数拥有自己的不变量、codec、fold 和演化策略。现在的形状会推动所有调用者依赖一个不断膨胀的全局事件目录。

## 6. 还存在完全重复的 ADT

仓库里有两个 case 集完全相同的十态角色类型：

- `Kernel.Role`
- `Session.AgentRole`

`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

也有两份完全相同的六态 `TurnOutcome`：

- `Application/Reconciliation/ReconciledTurn.fs`
- `Domain/ReconcileProgram.fs`

`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

这不是强类型建模，而是 duplicated truth。新角色或新 outcome 加入时，编译器只会分别要求两边穷尽，却不能保证两份定义继续同构。最终还得写转换函数，把自己制造出来的类型差异重新抹平。

## 7. 一个尤其讽刺的小例子

`SessionRecovery` 定义了十态 `RecoveryTrace`，随后又定义所谓的 “Trace interpreter”，用递归游走 trace 来证明业务操作之前出现过 `FamilyReadyIssued`。`repomix-output(20260806-085150).xml`

它目前是纯测试/证明数据，不一定是生产第二运行时；但它展示了当前团队的惯性：

> 先把执行顺序枚举成事件，再写一个解释器检查顺序。

对于“必须先 ready，才能执行 business operation”这种性质，更强的结构是让 `BusinessOperation` 接受不可伪造的 `FamilyRecoveryPermit`。仓库其实已经创建了这个 permit，却又额外建立一套 trace 语言来验证顺序。类型能力和事件模拟同时存在，后者很容易重新长成第二运行时。

## 判断

所以我的结论是：

**是的，已经出现明显偏离。**

但不应简化成“DU 多就是错”。真正的问题是下面三种东西没有被区分：

1. **封闭领域词汇**：例如权限、错误类别，较大的 DU 可以合理。
2. **持久事实代数**：可以较大，但应按 bounded context 分解。
3. **下一步执行什么**：应优先用 CE、调用栈、资源作用域和局部递归表达；不能因为换成 DU，就认为已经符合函数式设计。

当前代码在第 2 类上有总和类型膨胀，在第 3 类上则确实存在状态机、可变控制位和布尔乘积；现有门禁又主要检查拼写，因此无法阻止这种偏移。

最优先的修复不是再增加几个禁用名称，而是把门禁改成以下审查条件：

- 任意 DU 达到 8 个 case，必须说明它是词汇、事实还是控制状态。
- 发现完全相同的 case 集直接失败。
- 含 `State + option/flag/recovery/drain/pending` 的记录必须计算可表示组合，并证明每种组合合法。
- `mutable` 豁免必须精确到声明或函数，不能按整个目录放行。
- 门禁加入改名型自测：`InFlight`、`Parked`、`Sealed`、`Armed` 等必须照样能触发。
- 每个 workflow PR 必须给出“结构化 CE 方案为何不适用”的简短反证，而不是只解释每个 case 代表什么。

## Prompt:
8/6/2026, 5:40:39 PM

工程师问：那具体 F# DSL (computation expression) 我们没见过，你得逐一给我们讲清楚每一处如何写。请你给保姆级方案。不过代码可以不由你写。

## Response:
8/6/2026, 5:40:40 PM

我会按“先讲 CE 最小语法，再逐个问题点给出目标形态、迁移步骤、验收标准”的方式写；重点不是让他们背语法，而是让每一处都能照着改。

仓库其实已经有可照抄的正例：`OrchestratorProgram` 用 `task { match! ...; return! ... }` 把“重基→复审→短锁发布”直接写成程序；`SessionRecoveryWorkflow` 用局部递归处理节点。方案会以这些现成写法为模板，而不是另造一个 DSL 框架。

> **检索工作流DSL并梳理编排恢复模式**
>
> 
>
> **设计实现路线**
>
> 我会先按“可编译、可测试、低风险”拆分：先实现领域类型与端口，再实现任务 CE、Enforcer 流程、UI 接入，最后处理安全与回归测试。
>
> **设计了实现路线**
>
> 
>
> **梳理恢复语义**
>
> 我会把“失败后才允许下一次压缩”建模为长期运行 CE 的局部递归状态，而不是全局可变布尔值：失败开启机会，后续材料消费机会，只有真正压缩成功才关闭。
>
> **梳理恢复语义**
>
> 
>
> **梳理并重构 BloggerRuntime 状态恢复及材质流程**
>
> 
>
> 思考了 3m 29s

下面这份可以直接作为工程团队的迁移说明。核心原则是：**先使用 F# 自带的 `task` computation expression，不先发明新的 `workflow {}` 框架。所谓 DSL，是 `task {}` 加上领域命名函数与能力端口，而不是另一套语法树。**

**document: **

# F# 结构化流程 DSL 保姆级迁移方案

## 一、先统一概念：这里的 DSL 到底是什么

本项目所说的 F# DSL，不是要求团队先开发一个复杂的自定义 computation-expression builder。

第一阶段只使用 F# 自带的：

```fsharp
task {
    let x = pureCalculation input
    let! y = asyncCapability x
    do! writeFact y

    match! anotherCapability y with
    | Ok value ->
        return value
    | Error error ->
        return! handleFailure error
}
```

这里的“领域 DSL”来自函数名称：

```fsharp
readTargetHead
rebaseCandidate
requestDualPerfectReview
publishUnderGate
recoverPromptClaims
restoreLinkedHandles
```

这些名称连起来，就能直接读出业务过程。

仓库规范本身要求编排、恢复与 Join 使用 `task`、`let!`、`match`、`return!` 直接执行；纯决策放在 Domain，Application 根据决策调用具名 capability。`repomix-output(20260806-085150).xml`

### 1. 必须掌握的七个符号

#### `task { ... }`

表示这是一段异步流程。

```fsharp
let run () : Task<int> =
    task {
        return 42
    }
```

它既是代码块，也是资源和异常的结构化边界。

---

#### `let`

取得立即可用的普通值，不等待异步操作。

```fsharp
let nextRound = round + 1
let decision = decide evidence
```

纯函数调用通常使用 `let`。

---

#### `let!`

执行并等待一个异步操作，把完成值取出来。

```fsharp
let! snapshot = ports.ReadSnapshot sessionId
```

假设：

```fsharp
ports.ReadSnapshot : SessionId -> Task<Result<Snapshot, SnapshotError>>
```

那么 `snapshot` 的类型是：

```fsharp
Result<Snapshot, SnapshotError>
```

不是 `Task<Result<...>>`。

---

#### `do!`

执行一个异步操作，但不关心它返回的普通值。

```fsharp
do! ports.ReleaseGate gate
do! ports.TerminateChildren jobId
```

通常用于返回 `Task<unit>` 的副作用。

---

#### `match!`

“等待，然后立即匹配”。

```fsharp
match! ports.ReadTargetHead targetRef with
| Ok head ->
    return head
| Error error ->
    return raiseFailure error
```

它相当于：

```fsharp
let! result = ports.ReadTargetHead targetRef

match result with
| Ok head -> ...
| Error error -> ...
```

项目现有 `OrchestratorProgram` 已经大量使用这一写法，例如读取目标 HEAD、重基、复审和发布。`repomix-output(20260806-085150).xml`

---

#### `return`

结束当前流程，返回一个普通值。

```fsharp
return DecisionEffect.Sealed
```

---

#### `return!`

把当前流程剩余的控制权直接交给另一段异步流程。

```fsharp
return! rebaseReviewPublish deps job (round + 1)
```

这就是结构化流程中的“下一步”。

它不需要：

```fsharp
NextStage <- RebaseAgain
```

也不需要：

```fsharp
return ContinueWith RebaseAgain
```

现有 Orchestrator 在目标分支移动后，直接 `return!` 重新进入重基-复审-发布流程。`repomix-output(20260806-085150).xml`

---

#### `try/finally`、`use` 和 `use!`

资源的获得与释放必须出现在同一个词法作用域内。

```fsharp
task {
    let! gate = acquireGate ()

    try
        return! publish ()
    finally
        do! releaseGate gate
}
```

实际代码可能因 F# `finally` 对异步释放的限制而使用嵌套 `task`，但原则不变：

> 获得资源的流程，必须看得见资源最终如何释放。

现有 `publishUnderGate` 已经接近这一结构：取得 Integration Gate，在内部执行发布，随后释放。`repomix-output(20260806-085150).xml`

---

## 二、团队写 DSL 时必须遵循的固定分层

每个业务流程都拆成三层。

### 第一层：Evidence

Evidence 是已经观察到的事实。

例如：

```fsharp
type PublishEvidence =
    { ExpectedHead: CommitHash
      CurrentHead: CommitHash
      Candidate: CommitHash }
```

Evidence 不表示“下一步执行到哪里”，只表示现在知道了什么。

---

### 第二层：纯 Decision

```fsharp
type PublishDecision =
    | PublishNow
    | TargetMoved
    | AlreadyPublished
    | FailClosed of reason: string
```

纯函数：

```fsharp
decidePublish : PublishEvidence -> PublishDecision
```

这里不能：

- 访问 Git；
- 写 Journal；
- 读取时间；
- 修改状态；
- 启动 Task。

---

### 第三层：直接执行的 Workflow

```fsharp
let executePublish ports evidence =
    task {
        match decidePublish evidence with
        | PublishNow ->
            return! publishUnderGate ports evidence

        | TargetMoved ->
            return! rebaseReviewPublish ports evidence.Candidate

        | AlreadyPublished ->
            do! ports.BackfillPublished evidence.Candidate
            return Published evidence.Candidate

        | FailClosed reason ->
            return Failed reason
    }
```

Decision 说“做什么”。

Workflow 说“按什么顺序调用能力”。

---

## 三、什么 DU 是合理的，什么 DU 是伪装状态机

不能简单规定“DU case 多就是坏”。

### 合理 DU

它描述封闭的真实结果：

```fsharp
type ProcessWaitResult =
    | Exited of exitCode: int
    | TimedOutWithoutExitConfirmation
```

它回答：

> 操作已经产生了什么结果？

---

### 可疑 DU

它描述：

> 代码下一次应该从哪里继续？

```fsharp
type ProcessStage =
    | Waiting
    | Killing
    | WaitingForKillAck
    | Finished
```

如果它随后被存进字段，再由另一个函数读取并执行下一步，它就是程序计数器。

判断方法不是看名字，而是问：

> 删除这个字段后，能否通过普通函数调用、`match!`、`return!` 和资源作用域表达同样顺序？

能，就应当优先使用结构化程序。

---

# 四、逐处迁移方案

## 第一处：`NodeProcessWait.fs` 的四个可变布尔

### 当前问题

当前流程同时维护：

```fsharp
timedOut
cancelled
killSent
killAckExpired
```

循环条件和循环后的分支共同推导当前执行阶段。`repomix-output(20260806-085150).xml`

这是最适合首先练手的一处，因为业务顺序非常清楚：

```text
等待自然退出
    ↓ deadline 到达
发送 kill
    ↓
等待 kill acknowledgement
    ↓
确认退出 / 未确认退出
```

而且 `Process` 不在当前 DSL 门禁的 `PROGRAM_DIRS` 中，所以现有检查无法发现这里增加的控制位。`repomix-output(20260806-085150).xml`

### 目标形态

拆成三个有明确输入输出的函数：

```fsharp
awaitExitOrDeadline
killAndAwaitAcknowledgement
waitForProcess
```

建议结果类型：

```fsharp
type ExitOrDeadline =
    | Exited of exitCode: int
    | DeadlineReached

type KillResult =
    | ExitConfirmed of exitCode: int
    | ExitNotConfirmed
```

这些 DU 是操作结果，不会被存入长期对象，所以不是程序计数器。

### 顶层流程应读成这样

```fsharp
let waitForProcess ports child deadline ct =
    task {
        match! awaitExitOrDeadline ports child deadline ct with
        | Exited exitCode ->
            return
                { ExitCode = exitCode
                  TimedOut = false }

        | DeadlineReached ->
            child.Kill()

            match! awaitKillAcknowledgement ports child ct with
            | ExitConfirmed exitCode ->
                return
                    { ExitCode = exitCode
                      TimedOut = true }

            | ExitNotConfirmed ->
                return
                    { ExitCode = -1
                      TimedOut = true }
    }
```

取消不应与 deadline 混在一个布尔乘积中。

`awaitExitOrDeadline` 收到 cancellation 时，直接抛出或返回明确的取消结果。外层负责 kill，然后传播 `OperationCanceledException`。

### 实施步骤

1. 先锁定现有五类行为：
   - deadline 前自然退出；
   - deadline 到达，kill 后正常退出；
   - kill 后 acknowledgement 超时；
   - 外部取消；
   - 退出与 deadline 几乎同时发生。

2. 为每一类写公共入口测试。

3. 提取现有的单段等待逻辑为：

   ```fsharp
   awaitExitOrTimer : Child -> int -> CancellationToken -> Task<ExitOrTimer>
   ```

4. 删除顶层 `while`。

5. 删除四个可变布尔。

6. 将事件订阅、timer 和 cancellation registration 放入单段等待函数的同一个 `try/finally` 中。

### 验收条件

源码中不再出现：

```fsharp
let mutable timedOut
let mutable cancelled
let mutable killSent
let mutable killAckExpired
```

公共行为保持不变。

任一结束路径都能证明：

- timer 已释放；
- `OnExited` handler 已移除；
- cancellation registration 已释放；
- kill 最多调用一次。

---

## 第二处：`Companion.slotArmed`

### 当前问题

当前实现明确将它描述为：

> Control-flow state only

然后提供：

```fsharp
ArmRecoverySlot
DisarmRecoverySlot
IsRecoveryArmed
```

供不同调用点在不同时间设置、查询和清除。`repomix-output(20260806-085150).xml`

失败处理先调用 `ArmRecoverySlot`，以后主会话材料到达时，`tryStartSquash` 查询 `IsRecoveryArmed`，成功开始 squash 后再清除。`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

### 不要做的“假修复”

不要改成：

```fsharp
type RecoveryArming =
    | Armed
    | NotArmed
```

然后继续把它存在可变字段里。

那只是把布尔换了名字。

### 目标形态：一次失败拥有一次结构化恢复机会

失败发生时，启动一段有明确生命周期的流程：

```fsharp
runRecoveryOpportunity
```

概念结构：

```fsharp
let runRecoveryOpportunity ports failure =
    task {
        let! nextMaterial =
            ports.AwaitNextMainMaterial failure.MainSessionId

        let evidence =
            buildRecoveryEvidence failure nextMaterial

        match decideRecovery evidence with
        | NoRecovery ->
            return ()

        | StartSquash squashContext ->
            do! ports.RunBloggerRequest squashContext
    }
```

这段流程结束后，“armed”自然不存在。

不需要另一个函数去 disarm。

### 如何等待未来材料

需要一个**物理同步对象**，不是业务状态字段。

例如由 Companion owner 创建的一次性：

```fsharp
TaskCompletionSource<ProviderSemanticProjection>
```

它的职责只有：

> 让正在等待下一份材料的 Task 被唤醒。

它不回答：

- 当前是哪个业务阶段；
- 下一步该运行什么；
- 是否应该 squash。

这些仍由 workflow 和纯决策负责。

### 防止丢失唤醒

失败路径必须按这个顺序执行：

```text
1. 在 Companion owner 的锁内注册一次性 waiter
2. 将 waiter 放入唯一物理槽
3. 退出锁
4. 启动/继续等待 workflow
```

材料路径：

```text
1. 在同一把锁内取出 waiter
2. 从物理槽移除
3. TrySetResult(material)
```

这样材料不会落在“已经 arm、但 waiter 尚未建立”的窗口里。

### 重启语义

当前语义规定重启后 `slotArmed = false`，最坏只损失一次压缩机会。`repomix-output(20260806-085150).xml`

新结构天然保持这一点：

- 进程重启；
- 未完成 Task 和一次性 waiter 消失；
- 不从 Journal 恢复 continuation；
- 以后按普通材料流程运行。

这正符合 FLOW-005：恢复普通业务事实，不恢复协程指针或执行位置。`repomix-output(20260806-085150).xml`

### 应删除的接口

最终删除：

```fsharp
ArmRecoverySlot
DisarmRecoverySlot
IsRecoveryArmed
slotArmed
```

替换成类似：

```fsharp
StartRecoveryOpportunity
OfferMainMaterial
CancelRecoveryOpportunity
```

其中后两项只能操作物理 waiter，不能返回“下一业务阶段”。

---

## 第三处：`BloggerToolRecovery`

### 当前问题

当前 cell 又携带一份三态恢复状态：

```fsharp
NoRecovery
InteractionNudgeIssued
AabbRepairConsumed
```

并通过：

```fsharp
markInteractionNudgeIssued
markAabbRepairConsumed
onFail
```

改变它。`repomix-output(20260806-085150).xml`

它与 `Idle/InFlight/Parked/Sealed/Disposed` 正交组合，使 runtime cell 同时承担：

- 物理运行状态；
- 请求内容；
- repair 是否已经尝试；
- seal/drain 权限。

### 最优先方案：从可观察 transcript 推导 repair 是否已发出

当前 repair message 已经有：

- 稳定内容；
- 稳定合成 ID；
- `source = "interaction-repair"`；
- 被追加到 provider-visible `rawMessages`。

`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

因此不要再维护一份隐藏的 `BloggerToolRecovery`。

写一个纯函数：

```fsharp
repairEvidence :
    requestKey:string ->
    rawMessages:obj list ->
    RepairEvidence
```

结果只需要表达已观察到的事实：

```fsharp
type RepairEvidence =
    | RepairNotObserved
    | RepairObserved of messageId: string
```

然后：

```fsharp
let decideEmptyCycle evidence =
    match evidence with
    | RepairNotObserved ->
        InjectRepair

    | RepairObserved _ ->
        RepairExhausted
```

### 周围的 CE

```fsharp
task {
    match validateCycle rawMessages with
    | Ok validCycle ->
        return! commitValidCycle ports validCycle

    | Error EmptyCycle ->
        match repairEvidence requestKey rawMessages with
        | RepairNotObserved ->
            return projectWithRepair rawMessages requestKey

        | RepairObserved _ ->
            do! ports.AbandonRequest requestKey
            return stopPhysicalRun rawMessages "protocol-repair-exhausted"

    | Error other ->
        return! handleInvalidCycle ports other
}
```

### 必须先做的 canary

删除 runtime recovery state 之前，必须用真实 Host canary 证明：

> 插入到 transform 输出中的 repair synthetic message，在下一次 transform 输入的完整 snapshot 中仍然存在。

如果 Host 会丢弃它，就不能凭空假设 transcript 可作为证据。

这时应选择一个真实可观察事实，例如 repair prompt 的 durable claim，而不是重新加隐藏状态。

### 验收条件

删除：

```fsharp
BloggerToolRecovery
Recovery: BloggerToolRecovery
markInteractionNudgeIssued
markAabbRepairConsumed
```

测试不再通过读取 runtime tag 判断是否已经 repair。

测试必须提供真实的两份 snapshot：

1. 第一份不含 repair message；
2. 第二份含同一个 request 对应的 repair message。

---

## 第四处：`BloggerRuntimeCell` 状态乘积

### 当前问题

现有结构包含：

```fsharp
State: BloggerRuntimeState
PendingOffer: BloggerRequestContext option
Recovery: BloggerToolRecovery
Drain: DrainWindow
```

其中 State 又有：

```fsharp
Idle
InFlight of BloggerRequestContext
Parked
Sealed
Disposed
```

`repomix-output(20260806-085150).xml`

随后 `onMaterial`、`beginRequest`、`onCycleCommitted`、`onFail` 等函数构成传统 transition API。`repomix-output(20260806-085150).xml`

### 目标：每一个旧状态映射到一种真实结构

| 旧状态/字段 | 新的表示 |
|---|---|
| `Idle` | 当前没有 single-flight Blogger Task |
| `InFlight ctx` | 正在运行的 Task，其局部变量持有 `ctx` |
| `Parked` | Task 正在 `let!` 等待下一份材料 |
| `Sealed` | 从 durable projection 查询到 main 已 sealed |
| `Disposed` | owner lifetime cancellation token 已取消，registry 已移除 |
| `PendingOffer` | parked waiter 的一次性输入 |
| `Recovery` | 从 transcript / durable evidence 推导 |
| `DrainWindow.Open` | 私有的 `DrainPermit` 能力值，只传给一次 drain workflow |

关键区别是：

> “正在等待什么”由当前正在执行的函数表示，而不是由字段表示。

### 目标流程分成五个普通函数

#### 1. `chooseNextRequest`

纯函数或只读查询。

输入：

- durable Blog projection；
- XTrace projection；
- 当前 main projection；
- 是否持有 recovery opportunity；
- 是否持有 drain permit。

输出：

```fsharp
type NextBloggerRequest =
    | NoRequest
    | MainRequest of BloggerRequestContext
    | SquashRequest of BloggerRequestContext
```

它不返回 `Start`、`Offer`、`Skip`。

这些词是在描述 runtime 怎么做，而不是业务需要什么请求。

---

#### 2. `materializeAndStart`

现有 `startFrozen` 已经接近正确 CE：

```text
materialize durable
→ 设置 current request
→ 启动 Host send
→ 将 PromptKey 回写 durable materialization
```

当前源码明确规定了这一顺序。`repomix-output(20260806-085150).xml`

迁移后删除 `cell` 参数：

```fsharp
materializeAndStart :
    Ports ->
    BloggerRequestContext ->
    Task<DecisionEffect>
```

概念结构：

```fsharp
task {
    match! ports.CheckMayStart ctx with
    | Blocked ->
        return Sealed

    | Allowed ->
        match ports.MaterializeRequest ctx None with
        | Error error ->
            return MaterializeFailed error

        | Ok () ->
            use! ownership = ports.RegisterCurrentRequest ctx

            match! ports.StartBlogger ctx with
            | Error error ->
                do! ports.AbandonRequest ctx error
                return StartFailed error

            | Ok promptKey ->
                match ports.BindPromptKey ctx promptKey with
                | Ok () ->
                    return Started

                | Error error ->
                    do! ports.AbandonRequest ctx error
                    return StartFailed error
}
```

`RegisterCurrentRequest` 返回一个 scoped ownership handle。

释放 handle 时自动清除 current request。

不能再要求每个错误分支记住：

```fsharp
scope.ClearCurrentRequest key
```

---

#### 3. `runOneBloggerCycle`

它拥有从请求开始到 commit/fail 的完整生命周期：

```fsharp
runOneBloggerCycle :
    Ports ->
    BloggerRequestContext ->
    Task<CycleResult>
```

内部顺序：

```text
materialize
→ send
→ 等待 provider-visible完成 snapshot
→ validate
→ 必要时 repair
→ commit
→ 返回是否还有剩余材料
```

如果 Host 生命周期暂时不允许一段 Task 横跨全部 callback，就先保持 callback adapter，但必须做到：

- callback 只获取当前 request ownership；
- callback 调用纯分类函数；
- callback 直接执行结果；
- 不更新一个独立的业务 State DU。

---

#### 4. `parkUntilMaterialOrSeal`

当前 `ParkTransform` 已经通过 Task 等待恢复信号，而不是固定 sleep；这是可以保留的物理机制。`repomix-output(20260806-085150).xml`

目标函数：

```fsharp
parkUntilMaterialOrSeal :
    Ports ->
    BloggerSessionId ->
    Task<ParkResult>
```

结果可以是：

```fsharp
type ParkResult =
    | MaterialArrived of BloggerRequestContext
    | MainSealed
    | LifetimeCancelled
```

然后直接匹配：

```fsharp
match! parkUntilMaterialOrSeal ports bloggerId with
| MaterialArrived ctx ->
    return! runOneBloggerCycle ports ctx

| MainSealed ->
    return Stopped

| LifetimeCancelled ->
    return Disposed
```

不保存 `Parked`。

Task 正在等待，就是 parked。

---

#### 5. `drainAfterReactivation`

不要保存：

```fsharp
DrainWindow.Open
```

定义一个不可在模块外构造的能力值：

```fsharp
type DrainPermit = private DrainPermit of AuthorityRootUserMessageId
```

只有“新 Authority Root 确实重开 drain”这一条路径能得到它。

```fsharp
let drainAfterReactivation ports permit =
    task {
        match! ports.ReadNextCatchUpRequest permit with
        | None ->
            return DrainComplete

        | Some ctx ->
            let! result = runOneBloggerCycle ports ctx

            match result with
            | Committed ->
                return! drainAfterReactivation ports permit

            | Failed error ->
                return DrainFailed error
    }
```

`DrainPermit` 是权限证据，不是阶段枚举：

- 不可在模块外构造；
- 不可改成 Open/Closed；
- 不存入通用 runtime cell；
- workflow 结束后自然失效。

### 新的总入口

当前 `onMainMaterial` 对 `Sealed`、`Idle`、`Parked` 分别重复加载 projection、构造 context、调用 `onMaterial`。`repomix-output(20260806-085150).xml`

迁移后应收束为：

```fsharp
let onMainMaterial ports material =
    task {
        match! ports.CheckLifecycle material.MainSessionId with
        | Disposed ->
            return DecisionEffect.Disposed

        | Sealed ->
            return DecisionEffect.Sealed

        | Active ->
            match! ports.TryOfferToParked material with
            | Offered resumed ->
                return DecisionEffect.OfferedParked resumed

            | NoParkedWaiter ->
                match chooseNextRequest material with
                | NoRequest ->
                    return DecisionEffect.NoMaterial

                | MainRequest ctx
                | SquashRequest ctx ->
                    return! ports.EnsureSingleFlight ctx
    }
```

### `EnsureSingleFlight` 的职责

这一层可以有锁和 Dictionary，因为它管理的是物理 Task ownership。

它只回答：

```fsharp
type SingleFlightResult =
    | Started of Task<DecisionEffect>
    | AlreadyRunning
```

它不得保存：

- repair 阶段；
- drain 阶段；
- pending business decision；
- next action；
- sealed 的第二份镜像。

### 迁移顺序

不要一次删掉整个 `BloggerRuntimeCell`。

#### 第一步

把 `Recovery` 从 cell 中移除。

#### 第二步

把 `Drain` 替换为私有 `DrainPermit`。

#### 第三步

把 `PendingOffer` 完全交给 parked waiter/TCS。

#### 第四步

把 `Sealed` 改成每次从 durable projection 判断。

#### 第五步

用 single-flight Task ownership 替换 `InFlight`。

#### 第六步

删除 `Idle/Parked/InFlight/Sealed/Disposed` 及 transition module。

每一步都必须保持公共入口测试为绿，不能在一个提交里同时重写所有生命周期。

---

## 第五处：`RecoveryTrace` 与 trace interpreter

### 当前问题

`SessionRecovery` 定义了一组步骤型 trace：

```fsharp
DiscoverClosure
RecoverPromptClaims
RecoverBloggerWindow
RestoreLinkedHandles
RecoverManagerJob
ValidateClosure
FamilyReadyIssued
FamilyWaiting
FamilyBlocked
BusinessOperation
```

随后递归解释这些 trace，以检查业务操作前是否出现过 `FamilyReadyIssued`。`repomix-output(20260806-085150).xml`

但仓库已经有更强的结构：

```fsharp
FamilyReady of FamilyRecoveryPermit
```

恢复流程只有全部条件满足时才发出 permit。相关测试也已验证 ready 分支会产生私有 permit。`repomix-output(20260806-085150).xml`

### 目标

所有需要在 family recovery 后执行的业务入口，必须接受 permit：

```fsharp
runBusinessOperation :
    FamilyRecoveryPermit ->
    BusinessInput ->
    Task<BusinessResult>
```

没有 permit，就根本不能调用。

调用方：

```fsharp
task {
    match! recoverFamily ports root with
    | FamilyReady permit ->
        return! runBusinessOperation permit input

    | FamilyWaiting waits ->
        return Waiting waits

    | FamilyBlocked blocks ->
        return Blocked blocks
}
```

这已经由 F# 函数签名证明了顺序。

不需要再构造：

```fsharp
[ DiscoverClosure
  RecoverPromptClaims
  FamilyReadyIssued
  BusinessOperation ]
```

然后解释一次。

### 测试方式

fake business port：

```fsharp
type FakeBusinessPort =
    { mutable Calls: BusinessInput list }
```

测试断言：

- `FamilyBlocked`：Calls 为空；
- `FamilyWaiting`：Calls 为空；
- `FamilyReady`：Calls 恰有一次；
- 调用收到的 permit root 与恢复 root 相同。

`RecoveryTrace` 如果只用于诊断，可以保留为测试侧普通字符串日志；不能作为生产正确性的第二证明系统。

---

## 第六处：两份 `TurnOutcome`

### 当前问题

Application 中定义了一份：

```fsharp
TurnInProgress
TurnNeedsContinuation
TurnCompleted
TurnAborted
TurnFailed
TurnUnknown
```

Domain 的 `ReconcileProgram` 中又定义了完全相同的一份。`repomix-output(20260806-085150).xml`

`Reconciler` 因此需要 `domainOutcome` 手工逐 case 转换。`repomix-output(20260806-085150).xml`

这不是 CE 问题，而是单一事实来源问题。

### 目标

选定：

```fsharp
ReconcileProgram.TurnOutcome
```

作为 canonical domain type。

然后：

```fsharp
type ReconciledTurn =
    { ...
      Outcome: ReconcileProgram.TurnOutcome }
```

删除：

```fsharp
Application.Reconciliation.TurnOutcome
domainOutcome
```

### 边界例外

只有 wire/Host 原始状态可以定义自己的边界类型，例如：

```fsharp
type HostTurnStatus =
    | Running
    | Completed
    | Aborted
    | Failed
```

该类型必须只存在于 Codec/Adapter。

转换方向固定为：

```text
HostTurnStatus → ReconcileProgram.TurnOutcome
```

不能让 Application 再定义一份“几乎相同”的领域结果。

---

## 第七处：`Role` 与 `AgentRole`

### 当前问题

仓库同时有十态 `Kernel.Role` 和十态 `Session.AgentRole`，case 集相同。`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

### 目标

选择 `Kernel.Role` 为唯一类型。

迁移期可以使用类型缩写：

```fsharp
type AgentRole = Role
```

但缩写只能作为短期兼容面。

随后：

1. 所有函数签名改用 `Role`；
2. 删除 Role ↔ AgentRole 转换；
3. 删除重复 DU；
4. Codec 只解码为 `Role`；
5. 权限表只以 `Role` 为 key。

这项修改不需要 CE。

---

## 第八处：大型 `AgentFact`

### 当前问题

`AgentFact` 是一个扁平总和类型，同时承载：

- Prompt dispatch；
- Authority；
- Fallback；
- Review；
- Execution handles；
- Orchestrator；
- Companion；
- Context/Blog/Enforcer 等事实。

源码自身已经用分段标题暴露了这些不同辖区。`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

### 这里不能用 CE 解决

事实代数不是控制流。

不能把 40 多种事实改成：

```fsharp
task {
    do! promptFact ...
    do! reviewFact ...
}
```

正确修复是按 bounded context 分解。

### 目标内部模型

```fsharp
type PromptFact =
    | PromptClaimed of ...
    | PromptSubmitted of ...
    | PromptPhysicalAccepted of ...
    | PromptAbandoned of ...

type ReviewFact =
    | BarrierStarted of ...
    | VerdictRecorded of ...
    | ChallengeIssued of ...
    | InputSealed of ...
    | WitnessConfirmed of ...

type ExecutionFact =
    | HandleLinked of ...
    | HandleCompleted of ...
    | HandleRetired of ...
    | HandleAbandoned of ...

type OrchestratorFact =
    | JobCreated of ...
    | CandidateReady of ...
    | ConflictDetected of ...
    | RebasedReady of ...
    | PublishClaimed of ...
    | Published of ...
    | Failed of ...
    | Abandoned of ...

type AgentFact =
    | Prompt of PromptFact
    | Review of ReviewFact
    | Execution of ExecutionFact
    | Orchestration of OrchestratorFact
    | Companion of CompanionFact
```

### 必须注意 wire 兼容

不能直接改变持久化 JSON case 形状并假装无协议变化。

建议分两阶段。

#### 阶段 A：不改 wire

保留现有扁平 `AgentFact` 作为 durable wire type。

新增内部 family view：

```fsharp
PromptFact.tryOfAgentFact
ReviewFact.tryOfAgentFact
ExecutionFact.tryOfAgentFact
```

各 projection/fold 只消费自己的 family。

此阶段消除跨域 fold，但不改变已有 Journal 字节。

#### 阶段 B：版本化迁移

只有在明确增加 schema version 后，才允许写入嵌套 family fact。

Codec 必须能够：

```text
旧扁平事实 → canonical family fact
新嵌套事实 → canonical family fact
```

Domain fold 永远只看 canonical family fact。

### 不允许的实现

不要创建：

```fsharp
AgentFactInterpreter
FactCommand
FactReply
FactProgram
```

顶层只进行一次数据分派：

```fsharp
match fact with
| Prompt prompt -> PromptProjection.apply prompt
| Review review -> ReviewProjection.apply review
| Execution execution -> ExecutionProjection.apply execution
...
```

这是数据折叠，不是业务流程解释器。

---

# 五、团队可直接照抄的三种 CE 模板

## 模板 A：线性流程

```fsharp
let run ports input =
    task {
        let evidence = buildEvidence input

        match decide evidence with
        | Reject reason ->
            return Rejected reason

        | Proceed command ->
            let! result = ports.Execute command
            return Completed result
    }
```

适用：

- 一次发送；
- 一次读取；
- 一次提交；
- 一次资源操作。

---

## 模板 B：失败即结束的多步流程

```fsharp
let run ports input =
    task {
        match! ports.First input with
        | Error error ->
            return Failed error

        | Ok first ->
            match! ports.Second first with
            | Error error ->
                return Failed error

            | Ok second ->
                match! ports.Third second with
                | Error error ->
                    return Failed error

                | Ok final ->
                    return Completed final
    }
```

项目现有 Orchestrator 大量使用这种显式 `match!`，虽然缩进较深，但每一个失败出口和副作用顺序都清楚。`repomix-output(20260806-085150).xml`

第一阶段不要为了减少缩进马上发明新 builder。

---

## 模板 C：重试或继续循环

```fsharp
let rec runRound ports round input =
    task {
        match! ports.TryOnce round input with
        | Completed result ->
            return result

        | Retry nextInput ->
            return! runRound ports (round + 1) nextInput

        | Failed error ->
            return raiseFailure error
    }
```

递归参数必须是：

- 实际下一轮输入；
- 有界预算；
- round identity；
- 剩余集合。

不能是：

```fsharp
currentStage
nextAction
isRunning
```

如果是可能无限等待的 runtime 生命周期，则等待必须发生在 `let! awaitSignal` 上，而不是高速递归或 sleep 轮询。

---

# 六、什么时候才允许自定义 computation-expression builder

本轮迁移不应先做自定义 builder。

只有满足以下条件，才进入评审：

1. 至少三个独立 workflow 出现完全相同的 `Task<Result<_,_>>` plumbing；
2. builder 不构造 AST；
3. builder 不储存 continuation；
4. builder 不隐藏副作用顺序；
5. builder 展开后仍等价于普通 `let!`、`match`、`return!`；
6. stack trace 和调试体验不显著恶化；
7. 不允许出现 `Step`、`Suspend`、`Command`、`Reply`、`Interpreter`。

即使最终定义：

```fsharp
resultTask {
    let! x = operationA ()
    let! y = operationB x
    return y
}
```

它也只能负责短路 `Result`。

它不能决定业务的下一阶段。

---

# 七、推荐的 PR 顺序

## PR 1：建立教学样板和负面测试

只增加：

- CE 风格指南；
- 一个最小正例；
- 一个 `CurrentStage` 负例；
- 一个“DU 改名后仍然是程序计数器”的负例；
- 不改生产行为。

---

## PR 2：重构 `NodeProcessWait`

目标：

- 删除四个可变布尔；
- 改成两段等待和一个顶层 `task`；
- 保持所有进程退出语义。

这是最低风险的训练题。

---

## PR 3：统一重复类型

处理：

- `TurnOutcome`；
- `Role` / `AgentRole`。

这一步降低后续迁移中的转换噪声。

---

## PR 4：删除 `RecoveryTrace` 正确性解释器

让 `FamilyRecoveryPermit` 成为业务入口的必需参数。

现有 `recoverFamilyDirect` 已经是直接 CE 和局部递归的良好模板。`repomix-output(20260806-085150).xml`

---

## PR 5：删除 `BloggerToolRecovery` 隐藏状态

前置：

- Host canary 证明 repair message 在下一次 snapshot 可见。

然后：

- 通过 raw transcript 推导 repair evidence；
- 删除 runtime recovery 字段。

---

## PR 6：将 `slotArmed` 改为一次性恢复 workflow

实现：

- 一次性 material waiter；
- structured recovery opportunity；
- 完成或取消后自动清理。

---

## PR 7：拆除 `BloggerRuntimeCell` 状态乘积

依次移除：

1. Recovery；
2. Drain；
3. PendingOffer；
4. Sealed 镜像；
5. InFlight tag；
6. 整个 transition module。

不要一次性重写。

---

## PR 8：分解 `AgentFact`

先分 fold ownership，不改 wire。

之后另开版本化迁移 PR。

---

## PR 9：升级 DSL 门禁

当前检查主要是路径豁免和名称正则。例如 Domain、Session、Application 中的 `let mutable` 被整体豁免，program-counter 也只识别有限名称。`repomix-output(20260806-085150).xml` `repomix-output(20260806-085150).xml`

新的门禁至少应加入：

### 1. 扫描范围

将：

```text
src/Wanxiangshu/Process/
```

纳入控制流检查。

Infrastructure 不宜整体按业务规则扫描，但负责业务生命周期的 Host/Plugin 文件必须纳入。

### 2. mutable 精确豁免

禁止按整个目录放行。

允许的 mutable 必须属于下列明确类别：

- bounded algorithm scratch；
- Dictionary/Set 等物理 registry；
- TaskCompletionSource；
- cancellation/resource ownership；
- byte buffer/output collector。

每一个豁免声明应带固定注解或登记项。

### 3. 组合状态检测

记录同时包含下列两类以上字段时要求人工证明：

```text
State
Pending/Offer
Recovery/Repair
Drain/Reactivated
Stage/Phase
Next
```

不能只查具体拼写。

### 4. 多布尔循环检测

发现：

```fsharp
let mutable a = false
let mutable b = false
while ...
```

应直接要求审查。

### 5. 大 DU 审查

达到约定 case 数时，不直接失败，但要求分类：

```text
Vocabulary
DurableFact
Evidence
Decision
ExternalSignal
ControlState
```

标为 `ControlState` 的类型必须附带：

> 为什么普通 CE、调用栈、局部递归或 scoped wait 无法表达？

### 6. 重复 case 集检测

两个 DU 拥有完全相同 case 名集合时，门禁失败，除非其中一个明确位于 Codec/Wire 边界，并存在单向转换理由。

### 7. 改名 canary

以下形式都必须能让门禁变红：

```fsharp
CurrentStage
CurrentMode
RuntimeCondition
LifecyclePosition
InFlightFlag
ParkedMarker
RecoveryArming
```

不能只防历史上已经出现过的名字。

---

# 八、每个迁移 PR 的统一验收表

工程师提交前逐项回答。

## 业务所有权

- 这段 workflow 的唯一入口是什么？
- 哪个函数拥有完整资源生命周期？
- 哪个模块拥有纯 Decision？
- 哪个模块执行副作用？

## 状态检查

- 是否存在表示“下一步执行什么”的字段？
- 是否存在由多个 bool/option/DU 正交组合出的状态空间？
- 能否把其中某个字段变成当前函数的局部变量？
- 能否把某个状态变成正在等待的 Task？
- 能否把某个 flag 变成不可伪造的 capability/permit？

## CE 检查

- 异步效果是否通过 `let!`/`do!` 明确出现？
- 分支是否通过 `match`/`match!` 明确出现？
- 下一轮是否通过 `return!` 明确出现？
- 资源是否在同一作用域释放？
- 是否新增了 AST、Interpreter、Command/Reply 或 Step 节点？

## 测试检查

- 测试是否走公共入口？
- 是否断言真实 Journal 事实或端口调用？
- 是否覆盖失败、取消、超时和恢复？
- 是否还在断言内部 state tag？
- 删除旧状态后，测试是否反而更接近可观察行为？

---

# 九、给工程师的最终判断口诀

看到一段复杂代码时，按这个顺序问：

```text
一、这是事实，还是“下一步”？
二、如果是事实，能否由纯函数得到 Decision？
三、如果是下一步，能否直接调用下一函数？
四、如果要等待，能否 let! 等一个真实信号？
五、如果要重试，能否 return! 进入下一轮？
六、如果要清理，能否让资源留在同一作用域？
七、如果仍需 mutable，它管理的是物理资源，还是业务进度？
```

只有最后一个问题的答案是：

> 它只管理锁、Task、TCS、Dictionary、buffer、subscription 或 cancellation lifetime。

这个 mutable 才应进入豁免。

团队不需要“学会一套神秘的 F# DSL”。

他们真正需要学会的是：

```text
普通函数负责命名业务动作
纯函数负责做决定
task CE 负责排列动作顺序
let! 负责等待
match 负责分支
return! 负责继续
try/finally 负责资源生命周期
```

除此之外，不再创造第二套运行时。

下一步最适合把前四个 PR 进一步拆成“具体文件、删除符号、增加测试、验收命令”四栏施工表。



---
Powered by [ChatGPT Exporter](https://www.chatgptexporter.com)
