# 结构化程序 DSL 纠偏（Structured Program DSL 方向撤回）

> ⚠ **SUPERSEDED 声明**
>
> 上一版把 DSL 定义为「封闭指令 AST + 纯 Program + 唯一 Interpreter + Trace Interpreter」，
> 并声称 `computation expression ≠ DSL`。这正是本轮复杂度膨胀的根源：各业务调用被拆成
> Command 定义、Reply 定义、构造器、Interpreter 分支、测试 facade 五份；大 Reply DU 让每步
> 都要处理十几个理论上不可能出现的回复；为测「执行顺序」又造 Trace Interpreter。它实际是
> 用业务代码重新实现了一遍动态调用协议——而 ARCH-001 早已给出答案：语言运行时
> 已经提供 continuation、调用栈与取消，业务层直接用 computation expression 写流程。
>
> 本文件据此重写。**目标不是做出一个更好的解释器，而是让这类解释器根本不再需要。**

---

# 一、裁决的一句话

> **本项目中的 DSL，是直接执行的 F# computation expression，加上领域命名的强类型操作和少量组合子；它不是待解释的业务 AST。**

`docs/what/flow.md` 声明冲突时以 ARCH-001/ARCH-002/ARCH-003 为准，因此本纠偏直接依据
SSOT 落地，不依赖对「哪一种解释更高级」的进一步论证。

---

# 二、正确心智模型

## 1. DSL 应当是什么

```text
DSL
  = F# computation expression
  + let! / do! / return! / match
  + 领域命名的强类型函数
  + 少量有明确语义的组合子
```

例如：

```fsharp
orchestrator {
    let! managerResult = awaitManager ops job ct
    let! candidate = reviewCandidate ops job managerResult ct
    let! published = rebaseAndPublish ops job candidate ct
    return published
}
```

* F# 调用栈就是流程栈；
* `let!` 就是顺序控制；
* `match` 就是业务分支；
* `return!` 就是尾调用；
* `CancellationToken` 就是取消协议；
* `Task<Result<_,_>>` 就是异步和错误通道；
* 普通递归就是循环；
* 类型系统直接约束每个操作的输入与返回值。

不需要额外的 `Command`、`Reply`、`Step`、`Suspend` 或 Interpreter。

## 2. DSL 不应当是什么

以下形态全部属于「第二套运行时」：

```fsharp
type Command = | ReadHead ... | Rebase ... | Publish ...
type Reply = | UnitOk ... | Head of CommitHash | RebaseOk | Failed of string
type Program = | Return of Result<...> | Step of Command * (Reply -> Program)
```

即使改成 `Program<'instruction,'result> = Pure | Suspend`，只要业务流程靠构造 AST +
Interpreter 回放推进，就仍属于第二运行时。它会必然产生：

* 每条正常调用被拆成 Command、Reply、构造器、Interpreter 分支、测试 facade 五份；
* 一个操作只能返回自己那一种结果，但大 Reply DU 允许所有回复，因此每步都要处理十几个不可能出现的 Reply；
* 为测试执行顺序又造 Trace Interpreter；
* 为复用又造通用 Program Kernel；
* 最后 CE 只是 AST 构造器的表面语法。

当前通用 Trace Interpreter 通过给 continuation 传入 `null` 遍历程序，已是抽象失真的强信号。

---

# 三、目标架构

只保留四层，不再存在 `Program AST → Interpreter` 这一中间层。

```text
Domain
    事实、证据、值对象、业务结果 DU、纯决策函数

Application Workflow
    直接执行的 computation expression（let! / match / return!）

Ports / Capabilities
    业务所需的强类型操作接口

Infrastructure / Runtime
    Host、Git、Journal、锁、队列、时钟、网络、进程
```

## Domain 层保留

描述真实业务概念的 DU 与纯函数，例如：

```fsharp
type ReconcileEvidence =
    | SnapshotError of string
    | NoTurn
    | Provisional of ObservedTurn
    | Unknown of ObservedTurn option
    | Terminal of ObservedTurn
    | BudgetExhausted of hasCandidate: bool
    | SessionCleared

type ReconcileDecision =
    | RereadWithBackoff of clearCandidate: bool
    | Publish
    | StopPass

val decideStep : ReconcileEvidence -> ReconcileDecision
```

## Domain 层删除

```fsharp
ReconcileCommand
ReconcileReply
ReconcileProgram
ProtocolMismatch
materializePass
TraceInterpreter
```

它们描述的不是领域，而是「程序执行到这里以后，下一个函数调用是什么」——这正是
ARCH-001 要交给语言运行时的东西。

### 实际替换语义：禁止外包层

纠偏不是给旧实现换一层皮。凡是「外面包一层、舍不得删」的形态都属于未完成：

* CE builder 内部仍然构造 `Program` AST、再交给隐藏 Interpreter 回放——`taskResult.Bind`
  必须直接 `let!` 到真实 Task 调用，不得在 Bind 里回头造节点；
* 保留 `Kernel/Program.fs` + `Kernel/TraceInterpreter.fs` 作为「通用内核」，只在顶部加 CE——
  这两个文件按 PR 2 真实删除，不许改名挪回 `Kernel/Flow.fs` 之类的位置继续当内核；
* 旧 coordinator / `RuntimeState` 仍在暗处跑，只是新增一个直接 CE 入口——同一 PR 必须
  断开旧入口并删除旧代码，不双跑；
* 把五个 bool 换成一个 `Dirty2` 或 flags enum——行为选择仍要用穷尽匹配的 Decision DU 替代；
* facade 包旧 Flow 并称为「Program」。

判据：删除旧执行面之后，新 CE 路径仍是完整、可运行、可测试的——不是「留着旧的兜底，
新的只是演示」。跑通旧路径不能让迁移算完成；删得掉旧路径才算。

### 融合优点：旧设计值得保留的，在直接 CE 中如何承载

旧 FLOW 文档立过几个真实价值。纠偏不是丢弃它们，而是换掉实现载体：

| 旧设计的优点 | 在直接 CE 中的载体 |
|--------------|-------------------|
| 可检查的轨迹（trace 可断言执行顺序） | fake capabilities 记录调用事件（如 `ResizeArray<ReconcileEvent>`），测试断言事件序列；不再要求 AST 树 |
| 纯决策可从 Host 抽出 | `Evidence -> Decision` 纯函数留在 Domain，CE 只按 Decision 分支执行效果 |
| 有界循环 / 命名组合子 | 保留 `repeatUntil` / `retryAtMost` / `forEachBounded` / `withOwnedResource` / `onCancellation`（FLOW-007），只是不靠 AST 表达 |
| 规则 DSL（`andThen` / `validateAll`） | 保留为 Domain 纯组合子（FLOW-004），流程与规则仍分离 |
| 资源作用域 + 取消传播 | 由 CE / 命名组合子 + `CancellationToken` 直接保证（ARCH-009） |
| 无逃生口 / 强制力 | 靠编译边界与门禁禁止 `Command/Reply/Step`、`obj/unbox`、内部业务 Interpreter（FLOW-006） |
| 崩溃恢复从 Journal 重入 | 保留：facts → Fold → 纯恢复决策 → 调用普通 workflow 合法入口（FLOW-005） |

一句话：**能直接 `let!` 表达的优点，全部用直接 `let!` 表达；只把「先建 AST 再解释」这一层真正删掉。**

---

# 四、最小 CE 实现

优先使用内置 `task {}`。只在 `Result` 短路样板明显过多时，保留一个极小的 `TaskResultBuilder`：

```fsharp
type TaskResult<'value, 'error> = Task<Result<'value, 'error>>

type TaskResultBuilder() =
    member _.Return(value: 'value) : TaskResult<'value, 'error> =
        Task.FromResult(Ok value)
    member _.ReturnFrom(op: TaskResult<'value, 'error>) : TaskResult<'value, 'error> = op
    member _.Bind(op: TaskResult<'value, 'error>, next: 'value -> TaskResult<'next, 'error>) =
        task {
            match! op with
            | Ok value -> return! next value
            | Error error -> return Error error
        }
    member _.Zero() : TaskResult<unit, 'error> = Task.FromResult(Ok())
    member _.Delay(factory: unit -> TaskResult<'value, 'error>) =
        task { return! factory () }

let taskResult = TaskResultBuilder()
```

约束：

* 直接执行，不构造 AST；
* 不含 `Command`、`Reply`、`Step`、`Suspend`；
* 不含 `obj`、`unbox`、反射；
* 不实现通用 Interpreter；
* 不持久化 continuation；
* 不提供通用 `While`、`For` 和复杂异常 DSL；
* 总体保持几十行，不能逐步长成另一个框架。

`agent`、`companion`、`orchestrator` 可以只是该 builder 的语义别名，甚至全部直接使用
`taskResult`。`AgentProgram` 与 `CompanionProgram` 当前明确使用「functions, not a Flow AST」，
并直接以 `task`、`let!`、异常映射执行——它们是本方向的参考实现。

---

# 五、Orchestrator 如何改

## 1. 强类型 capability 替代 Command/Reply

```fsharp
type OrchestratorOps = {
    AwaitManager : ManagerJobId -> CancellationToken -> Task<Result<ManagerResult, OrchestratorError>>
    ReadTargetHead : TargetRef -> CancellationToken -> Task<Result<CommitHash, OrchestratorError>>
    RebaseOnto : WorktreePath -> TargetRef -> CancellationToken -> Task<Result<RebaseResult, OrchestratorError>>
    Review : ReviewRequest -> CancellationToken -> Task<Result<ReviewResult, OrchestratorError>>
    Publish : PublishRequest -> CancellationToken -> Task<Result<PublishResult, OrchestratorError>>
    ReleaseWorktree : WorktreePath -> CancellationToken -> Task<Result<unit, OrchestratorError>>
}
```

每个操作返回自己的结果：

```fsharp
type RebaseResult =
    | Rebased of CommitHash
    | Conflicted of files: string list * worktreeHead: CommitHash

type PublishResult =
    | Landed of CommitHash
    | TargetMoved
```

`ReadTargetHead` 不会再理论上收到 `ReviewOk` 或 `PublishFailed`。

## 2. 直接写流程

```fsharp
let rec rebaseReviewPublish
    (ops: OrchestratorOps) (job: ManagerJob) (round: int) (ct: CancellationToken)
    : Task<Result<CommitHash, OrchestratorError>> =
    taskResult {
        let! targetHead = ops.ReadTargetHead job.TargetRef ct
        let! rebaseResult = ops.RebaseOnto job.Worktree.Path job.TargetRef ct

        match rebaseResult with
        | Conflicted(files, worktreeHead) ->
            let! resumed = resumeConflict ops job files worktreeHead ct
            return! rebaseReviewPublish ops resumed (round + 1) ct
        | Rebased candidate ->
            let! review = ops.Review { ... } ct
            match review with
            | RevisionRequired feedback ->
                let! resumed = resumeAfterReview ops job feedback ct
                return! rebaseReviewPublish ops resumed (round + 1) ct
            | ConfirmedPerfect ->
                let! publish = ops.Publish { ... } ct
                match publish with
                | Landed commit ->
                    do! ops.ReleaseWorktree job.Worktree.Path ct
                    return commit
                | TargetMoved ->
                    return! rebaseReviewPublish ops job (round + 1) ct
    }
```

审阅者看到的是业务流程，而不是一棵要在脑内执行的 AST。

---

# 六、Reconcile 如何改

把两种东西拆开：

1. **运行时调度机制**（物理运行时状态，可保留）：队列、single-flight、generation、清理、并发锁；
2. **一次 reconcile pass 的业务流程**：直接用 CE。

```fsharp
type ReconcileOps = {
    ReadActiveBinding : SessionId -> CancellationToken -> Task<Result<ActiveRunBinding option, ReconcileError>>
    ReadSnapshot : SessionId -> CancellationToken -> Task<Result<SessionMessage list, ReconcileError>>
    Delay : TimeSpan -> CancellationToken -> Task<Result<unit, ReconcileError>>
    PublishTurn : ReconciledTurn -> CancellationToken -> Task<Result<unit, ReconcileError>>
    ObserveSnapshot : SessionId -> SessionMessage list -> CancellationToken -> Task<Result<unit, ReconcileError>>
}
```

```fsharp
let rec reconcileActive (ops: ReconcileOps) (policy: ReconcilePolicy) (state: ReconcilePassState) (ct: CancellationToken) =
    taskResult {
        if state.BudgetRemaining <= TimeSpan.Zero then
            return! publishCandidateIfNeeded ops state ct
        else
            let! messages = ops.ReadSnapshot state.SessionId ct
            let evidence = classifySnapshot state.Binding messages
            match decideStep evidence with
            | StopPass -> do! ops.ObserveSnapshot state.SessionId messages ct
            | Publish ->
                do! publishEvidenceIfNeeded ops state evidence ct
                do! ops.ObserveSnapshot state.SessionId messages ct
            | RereadWithBackoff clearCandidate ->
                let delay = policy.NextDelay state.BackoffIndex state.BudgetRemaining
                do! ops.Delay delay ct
                let next =
                    state |> ReconcilePassState.afterObservation evidence clearCandidate
                          |> ReconcilePassState.consumeBudget delay
                return! reconcileActive ops policy next ct
    }
```

改造后：不再有 Reply 协议、因而没有协议错配、每个 port 返回类型编译期确定、调度器只负责
「何时跑」不负责「业务下一步做什么」。

---

# 七、逐 PR 纠偏顺序

## PR 0：紧急停止继续跑偏

目标：先阻止新增 AST，不动生产行为。

1. 本文件加醒目 `SUPERSEDED` 声明（已完成）。
2. 修订 FLOW 规范，明确它不得改变 `ARCH-001`。
3. 新增架构决议：**本项目 DSL 为直接执行的 computation expression；禁止把普通业务调用序列编码成 Command/Reply/Step AST。**
4. 暂停新增以下类型：

   ```text
   *Command
   *Reply
   *Program = Return | Step
   Pure | Suspend
   ProtocolMismatch
   *Interpreter 用于解释内部业务调用
   ```

## PR 1：纠正测试与门禁

当前门禁扫描整个 Agent、Application、Domain、Kernel、Session 并把原始 `task {}` 视为违规；
同时对 `Interpreter.fs` 结尾文件豁免 raw-task。这会制度化地诱导工程师把正常代码搬进
Interpreter，应调整。

* 删除或改写 `tests/unit/verify/program-kernel-contract.test.mjs`、`program-kernel.test.mjs`、
  要求 `programKernel` 导出的 facade、要求 `Pure/Suspend`/Trace Interpreter 存在的静态测试、
  要求 Orchestrator 只能经 Interpreter 运行的 shape test——它们已从防回归测试变成错误架构的护城河。

新门禁禁止 / 允许：

```text
禁止：业务层 CurrentStage / NextAction / Running 等程序计数器
      Command + Reply + Step/Suspend 内部执行协议
      持久化 continuation 或 Program 节点
      Domain 引用 Host/Infrastructure
      obj/unbox 驱动的通用业务程序内核
      仅用于重放普通调用序列的 Interpreter
允许：Application 中直接 task/taskResult
      有界递归
      物理并发状态的 mutable
      锁、队列、取消源、completion cell
      外部协议的 codec/parser/interpreter
      纯领域决策 DU
```

尤其删除 `raw-task` 违规项——`task {}` 正是正确方案的一部分，不是逃生口。

## PR 2：删除错误的通用基础设施

删除 `src/Wanxiangshu/Kernel/Program.fs` 与 `Kernel/TraceInterpreter.fs`；同步删除 `.fsproj`
编译项、`domain.mjs` 的 `programKernel` facade、对应 contract/behavior tests、所有只为内核存在的导出。

`Kernel/Flow.fs` / `Kernel/DomainFlow.fs`：首选直接用 `task {}`；次选保留最小 `TaskResultBuilder`；
需要保留的并行能力（如 `parallelMapBounded`）单独放进 `Kernel/Parallel.fs`，不因保住一个并行
函数而保留整个 Flow 框架。

## PR 3：先改 Orchestrator（首个垂直切片）

迁移步骤：

1. 为现有行为补 characterization tests；
2. 定义 `OrchestratorOps`；
3. 把 `executeCommand` 每个分支变成一个强类型 capability；
4. 把 `OrchestratorPrograms` 嵌套 `Step` 改成直接 CE；
5. 测试 fresh run / conflict resume / target moved 重试 / review revision / publish landed；
6. 同一 PR 删除旧 AST 和 Interpreter。

不长期双跑；合并必须 clean break。

## PR 4：再改 Reconcile

切分边界：

* 保留：`ReconcileEvidence`、`ReconcileDecision`、`decideStep`、`pickDelay`、`publishDecision`、`PublishMaps`、snapshot classification。
* 删除：`ReconcileCommand`、`ReconcileReply`、`ReconcileProgram`、`materializePass`、`protocolMismatch`、Trace Interpreter、`stepName`/`replyName` 测试辅助面。
* 重构：`ReconcileInterpreter.fs` → `Reconciler.fs`，队列/generation/single-flight/clear session 保留，删 `Interpret(program)`，改成直接调用 `runPass ops ...`；fake port 记录调用事件（`ResizeArray<ReconcileEvent>` → `ReadBinding/ReadSnapshot/Delay/Publish/Observe`）即可检查执行顺序，不要求生产代码先变成树。

## PR 5：清理其余小型解释器

逐个审查 `Domain/JoinProgram.fs`、`Domain/SessionRecovery.fs`、`Domain/ChildRecovery.fs`、
`Application/Reconciliation/JoinInterpreter.fs`、`SessionRecoveryInterpreter.fs`、`ChildRecoveryInterpreter.fs`。

审查标准只有一个：**这个 DU 描述领域事实/决策，还是只是在描述接下来调用哪个函数？** 前者保留，后者改成直接 CE。

`JoinProgram` 一方面宣称是 Program 数据，另一方面又把 `Task<unit>` 作为 `interrupt` 放进节点，
本身已说明「纯数据 AST」边界不成立。

## PR 6：清理文档和命名

全仓删除或更名 `Program AST`、`Program is data`、`unique production interpreter`、
`Trace Interpreter`、`Command/Reply protocol`、`materialize program`、`executeCommand`、
`ProtocolMismatch`。

不要机械删除所有 `Interpreter`：外部 JSON/TOML/Host 协议解释、codec、parser 可以保留；
仅把内部 `ReadX` Command 转回 `port.ReadX()` 的解释器、把普通函数调用序列编码后再回放的
解释器必须删除。

---

# 八、Code Review 三问法

对任何新的 DSL 抽象，Reviewer 只问三件事：

1. **这段流程能不能直接用 `let! / match / return!` 写？** 能，就不允许造 AST。
2. **这个 DU 表示真实领域状态，还是「程序下一步去哪」？** 后者删除。
3. **这个 Interpreter 在解释外部协议，还是在把内部 Command 重新变回函数调用？** 后者删除。

---

# 九、最终落地顺序

```text
先改 spec
→ 再改门禁
→ 删除通用 Program Kernel
→ Orchestrator 垂直切片
→ Reconcile 垂直切片
→ Join / Recovery 小型 AST 清理
→ 文档和 facade 收尾
```

不要从「优化 Interpreter」开始，也不要先设计更强的泛型 Program。

---

# 十、完成定义（每个子系统的纠偏标准）

1. 业务主流程可以从上到下直接阅读。
2. 不存在该子系统的 `Command + Reply + Step/Suspend`。
3. 不存在解释该内部 AST 的生产 Interpreter。
4. 不存在由大 Reply DU 导致的「不可能回复」分支。
5. Domain 中只剩事实、证据、决策和值对象。
6. 异步、取消和错误直接使用语言运行时。
7. 恢复从 Journal facts 和 projection 重新进入正常 workflow。
8. 测试通过 fake ports 验证调用顺序和外部结果。
9. 行为级 unit、integration、e2e 全部保持。
10. 删除旧实现，不留下长期双路径。

量化门槛：

```text
目标子系统 ProtocolMismatch 数量           = 0
目标子系统内部 Program Interpreter 数量    = 0
目标子系统 AST trace-only 测试数量         = 0
```

---

# 十一、禁止的伪修复

只在旧架构上做局部包装，一律拒绝：

```text
只把 task { } 外面套 agent { }
保留 Flow.lift 方便特殊情况
新增更多 RuntimeState DU
把五个 bool 换成一个 flags enum
用 facade 包住旧 coordinator
新旧实现长期双跑
仅新增文档，不加编译门禁
仅 grep Stage/Phase 字符串
测试只检查导出函数存在
把 Program AST 持久化以恢复执行位置
建立一个覆盖全系统的万能 Operation union
```
