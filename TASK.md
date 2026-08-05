# Structured Program DSL 根治方案

## 一、裁决

当前实现不应做局部整理。应执行 clean break：

1. 删除“通用 Flow 包一层 Task 就算 DSL”的设计。
2. 为每个业务上下文建立封闭、强类型、可解释的 Program DSL。
3. 业务程序只能构造 Program，不能直接执行 Task、调用 Host port 或修改运行时状态。
4. 所有副作用只能由 Interpreter 执行。
5. 用编译边界与 CI 门禁封死绕过路径。
6. 迁移完成后删除旧 Flow、旧 coordinator、旧布尔状态与双实现。

核心定义：

```text
computation expression ≠ DSL

DSL =
    封闭领域指令
  + 纯 Program
  + 唯一 Interpreter
  + 强类型结果
  + 可检查轨迹
  + 无逃生口
```

当前仓库的 `Flow<'ctx,'error,'a>` 本质是 Reader + CancellationToken + Task<Result<...>>。`Flow.lift` 可以把任意 Task 塞进去，意味着任何业务代码都能绕开领域词汇、规则组合与架构审查。`agent {}`、`companion {}`、`orchestrator {}` 目前主要只是语法包装，不是封闭语言。

仓库静态扫描还显示：

* 生产代码仅约 5 个领域 CE 使用点；
* 各层约有 201 个原始 `task {}`；
* `guide-contract.test.mjs` 主要验证导出函数和 builder 存在，不验证业务流程是否真正经过 DSL；
* `agent-dsl` E2E 验证 fork/join 结果，不证明内部没有直接 Task、状态机或旁路；
* `EnforcerHost.fs`、`ReconcileSupervisor.fs` 等仍以多个 mutable flag 驱动后续分支。

因此现有门禁会把“有几个 DSL 样板函数”误判为“系统采用了 DSL”。

另有一处必须先处理的规范漂移：`docs/rfcs/strength.md` 把 `spec/13` 称为 Projection Algebra，并引用 `PROJ-` 条款；实际 `spec/13.md` 是 ARCH-010 Synthetic TOML 解释规范，`scripts/checks/spec.mjs` 也没有登记 `PROJ-`。未先修复这处 SSOT 冲突，任何 Projection DSL 实现都没有稳定合同。

---

## 二、不可误杀的边界

禁止的是程序计数器状态，不是所有状态。

### 必须删除

凡是回答“程序下一步去哪”的字段：

```text
Dirty
Running
RepairSpent
ReactivatedAfterSeal
injectRepair
commitUnknown
abandonThenCatchUp
forceConfirmedReviewer
isContinuation
publishToMailbox
openReviewBarrier
```

典型特征：

* 修改它只为改变稍后的控制分支；
* 两个以上 bool 可以形成非法组合；
* 重启后需要猜测如何恢复它；
* 它等价于调用栈、局部变量或 continuation；
* 名字含 stage、phase、next、running、pending、spent、already、should。

### 可以保留

真实物理事实或不可约领域事实：

```text
进程是否已经退出
PTY 是否已经关闭
文件是否存在
Git 工作区是否 dirty
消息是否被 Host 标为 completed
输出是否发生截断
某条持久化事实是否存在
```

但即使是真实事实，只要不同值携带不同数据，也应优先用 DU：

```fsharp
type ProcessOutput =
    | Inline of stdout: string * stderr: string
    | Spooled of path: SpoolPath
```

而不是：

```fsharp
{ Stdout: string
  Stderr: string
  Spooled: bool
  SpoolPath: SpoolPath option }
```

---

## 三、目标架构

### 3.1 四层模型

```text
Domain
  纯值、事实、决策、规则组合子

Program
  只构造领域 DSL；零 Task、零 I/O、零 mutable

Interpreter
  解释一条领域指令；不拥有业务决策

Infrastructure
  OpenCode、Git、进程、文件、时钟、Journal 适配
```

依赖方向：

```text
Infrastructure → Interpreter → Program → Domain
```

Program 不得引用 Interpreter、Infrastructure 或 Host port。

### 3.2 不造万能 DSL

禁止建立全局 `Operation` 大联合：

```fsharp
type Operation =
    | ForkAgent
    | AppendJournal
    | ReadGit
    | RenderProjection
    | StartProcess
    | ...
```

这会迅速变成新的 God Module。

应建立多个小型语言：

```text
AgentProgram
CompanionProgram
ReconcileProgram
BloggerProgram
ReviewProgram
OrchestratorProgram
ProjectionProgram
```

共享的只有最小 Program 机制、取消模型、资源作用域和测试工具。领域指令不共享。

---

## 四、DSL 内核

### 4.1 Program 必须是可检查的数据

以 Agent 为例：

```fsharp
type AgentProgram<'result> =
    | Return of 'result
    | Fork of ChildSpec * (ChildHandle -> AgentProgram<'result>)
    | Join of ChildHandle * (ChildCompletion -> AgentProgram<'result>)
    | JoinAny of NonEmpty<ChildHandle> * (ChildCompletion -> AgentProgram<'result>)
    | SendContinuation of Continuation * (PromptAcceptance -> AgentProgram<'result>)
    | ReadTranscript of SessionId * (Transcript -> AgentProgram<'result>)
    | Fail of AgentError
```

CE builder 只负责组合这些构造：

```fsharp
agent {
    let! coder = fork coderSpec
    let! completion = join coder
    do! requireSuccessful completion
    return completion
}
```

Program 本身：

* 不执行；
* 不持有 Runtime；
* 不读取时钟；
* 不追加 Journal；
* 不捕获 Host 对象；
* 不序列化；
* 不作为恢复中的暂停协程。

崩溃恢复仍遵守 ARCH-005：

```text
Journal facts
    → Fold
    → 构造新的 Program
    → 从事实决定合法入口
```

绝不持久化 Program 节点、continuation 或“执行到第几步”。

### 4.2 删除逃生口

以下 API 必须删除或降为 Interpreter 内部私有：

```text
Flow.lift
Flow.create
fromTask
通用 Runtime -> Task<Result<...>>
```

只要 Program 层还能调用 `lift`，DSL 就不存在强制力。

### 4.3 不提供通用 While

DSL builder 不应暴露任意 `While`、无限 `For` 或裸 `TryWith`。

改为命名组合子：

```text
repeatUntil
retryAtMost
reviewUntilConfirmed
forEachBounded
withOwnedResource
onCancellation
```

每个组合子必须固定：

* 最大次数或退出证据；
* 取消传播；
* 错误类型；
* 资源释放；
* 可观察轨迹。

---

## 五、规则 DSL 与流程 DSL 分离

很多“布尔地狱”实际是规则组合错误，不能全塞进工作流语言。

建立纯规则组合子：

```fsharp
type Rule<'input, 'error> = 'input -> Result<unit, 'error>

andThen:
    Rule<'a,'e> list -> Rule<'a,'e>

validateAll:
    Rule<'a,'e> list -> 'a -> Result<unit, NonEmpty<'e>>
```

语义：

```text
有依赖的规则 → andThen，首错短路
相互独立的规则 → validateAll，一次收全
```

禁止：

```fsharp
let mutable valid = true
let mutable reason = None

if ... then valid <- false
if valid && ... then ...
```

应写成规则原文：

```fsharp
reviewConfirmationRules =
    andThen [
        requireCurrentBarrier
        requireSameGitTree
        requireDistinctProviderRun
        requireChallengeInInputSeal
    ]
```

流程 DSL 决定“做什么”；规则 DSL 决定“是否允许”。

---

## 六、各子系统目标语言

### 6.1 Agent DSL

领域词汇：

```text
fork
join
joinAny
sendContinuation
awaitTerminal
cancelChild
requireSuccessful
```

`ChildRunProgram` 不得再通过 `Flow.lift` 反向调用另一个 AgentFlow 来验证 identity。Identity 应在构造 `ChildHandle` 时成立，非法 handle 根本造不出来。

### 6.2 Orchestrator DSL

领域词汇：

```text
awaitManager
reviewCurrentTree
registerCandidate
acquirePublishGate
readTargetHead
rebase
resumeConflict
publishFastForward
terminateChildren
releaseWorktree
appendFact
```

现有 `Program.fs` 中辅助函数内部的大量 `task {}` 必须迁入 Interpreter。Program 只能描述：

```fsharp
orchestrator {
    do! awaitManager job
    do! reviewCurrentTree job
    do! registerCandidate job
    return! rebaseReviewPublish job
}
```

重试次数不能用递归参数 `attempt` 裸传，改为有界类型：

```fsharp
type RebaseBudget = private RebaseBudget of byte
```

### 6.3 Reconcile DSL

`ReconcileSupervisor.fs` 的 `Dirty + Running + cont + releaseOnExit + terminalFound` 应整体退出业务层。

DSL：

```text
readAuthoritativeSnapshot
classifyCompletedTurn
awaitCausalProgress
commitCompletion
sendContinuation
abortRun
```

Single-flight/coalescing 是基础设施并发原语，不是业务状态：

```text
Signal → CoalescingQueue → 每 Session 串行解释 ReconcileProgram
```

队列中有信号即代表 dirty；解释器正在运行即代表 running。不再镜像成两个 bool。

### 6.4 Blogger/Enforcer DSL

这是最高优先级重写区。

`BloggerRuntimeState` 中：

```text
Idle
InFlight
Parked
Sealed
Disposed
RepairSpent
ReactivatedAfterSeal
```

大部分是在重述调用栈。

目标程序：

```fsharp
blogger {
    let! request = awaitMaterial mainSession
    let! outcome = runCycle request

    match outcome with
    | Valid cycle ->
        do! commitCycle cycle
        return! awaitNextMaterial ()
    | EmptyTerminal ->
        let! repaired = repairOnce request
        return! resolveRepair repaired
    | ProviderFailure failure ->
        return! recoverFrom failure
}
```

对应消除：

```text
InFlight              → 正在 await runCycle 的调用栈
Parked                → 正在 awaitMaterial
Disposed              → CancellationToken
RepairSpent           → repairOnce 组合子的局部结构
ReactivatedAfterSeal  → 新 AuthorityRoot 启动新的 Program
```

`EnforcerHost.fs` 中的多个 mutable flag 改为一个穷尽结果：

```fsharp
type CycleResolution =
    | CommitMain of MainCommit
    | CommitSquashThenContinue of SquashCommit
    | InjectSingleRepair of BloggerRequestContext
    | AbandonStaleAndCatchUp of reason: string
    | StopPhysicalRun of reason: string
    | FailClosed of reason: string
```

决策函数纯化：

```fsharp
resolveCycle:
    CycleEvidence
    -> Result<CycleResolution, CycleProtocolError>
```

Host 只解释 `CycleResolution`，不再自己拼规则。

### 6.5 Crash Recovery

`BloggerCrashRecovery` 的三个 bool：

```text
hasPhysicalAccepted
hasCompletedBlogTool
hasCycleReceipt
```

改为从事实构造的封闭证据：

```fsharp
type RecoveryEvidence =
    | NeverAccepted
    | AcceptedWithoutTerminal
    | TerminalWithoutReceipt of CompletedBlog
    | ReceiptCommitted of CycleReceipt
    | Contradictory of RecoveryContradiction
```

恢复决策对该类型穷尽匹配，非法组合在边界立即变成 `Contradictory`。

### 6.6 Projection DSL

先修复 SSOT，再实现。

目标链：

```text
ProjectionSnapshot
    → ProjectionIntent list
    → conflict detection
    → ProviderSemanticProjection
    → ProviderWireProjection
    → ProviderInputSeal
```

功能模块只能声明 intent：

```text
keepPhysicalPrefix
activatePrefixEpoch
insertBlogFrames
insertRepair
suppressTransportOnly
appendReviewChallenge
reanchorAfterCompaction
```

禁止任何业务功能直接接收和修改 `Message list`。

不同 intent 修改同一锚点时必须：

* 有明确定义的合并律；或
* 返回 `ProjectionConflict`；
* 不允许依赖注册顺序。

---

## 七、逐文件处理清单

### 删除或根改

```text
Kernel/Flow.fs
Kernel/DomainFlow.fs
Agent/AgentProgram.fs
Session/CompanionProgram.fs
Application/Orchestration/Program.fs
Application/Reconciliation/ReconcileSupervisor.fs
Session/BloggerRuntimeState.fs
Session/EnforcerHost.fs
```

### 定向清理

```text
BloggerCrashRecovery.fs
    bool 证据 → RecoveryEvidence

TurnCompletionProgram.fs
    forceConfirmedReviewer → 独立 typed path

RecoverySlot.fs
    CommitMain of bool
    → CommitMainAndClearFailures
    | CommitMainPreservingFailures

HostForkRuntime.fs / ForkRuntime.fs
    optional bool 参数 → policy DU 或不同构造函数

ForkTypes.fs
    HasPendingCompletion + LastCompletionStatus
    → 单一 completion 形态

CompanionHost.fs
    bloggerTask + bloggerId + bloggerFailed
    → 一个真实资源句柄，不允许三字段漂移
```

### 保留但收边界

以下 mutable 可在 Infrastructure 内保留，但不能泄露成业务协议：

```text
Promise/TCS 的单赋值实现
Semaphore permit 计数
物理进程 handle
PTY buffer
Host SDK 动态对象
Journal writer 的串行队列
```

---

## 八、迁移顺序

### 阶段 0：冻结与补合同

1. 暂停新增 workflow 功能。
2. 修复 `spec/13` / Projection Algebra 冲突。
3. 新增 active spec，例如 `spec/14.md — Structured Program DSL`。
4. 注册正式前缀，例如 `FLOW-`。
5. 明确 Program、Interpreter、Rule、Projection 四类边界。
6. 所有现有行为变化先写 spec 条款。

出口：spec 检查能发现悬空 DSL 条款和错误 owner。

### 阶段 1：建立失败门禁

先让以下坏代码在 CI 中失败，再写新 DSL：

```text
Program 模块出现 task {
Program 模块出现 let mutable
Program 模块调用 Flow.lift
Program 模块引用 Infrastructure/OpenCode
workflow API 使用行为型 bool 参数
workflow record 保存程序计数器 bool
```

门禁必须有负例测试，证明每条规则真的会红。

### 阶段 2：新 Program 内核

1. 建立封闭 AST。
2. 建立 CE builder。
3. 建立生产 Interpreter。
4. 建立纯模拟 Interpreter。
5. 建立 Trace Interpreter。
6. 完成取消、资源作用域与错误传播性质测试。
7. 不提供任意 Task 注入。

### 阶段 3：Orchestrator 试点

选择 Orchestrator，因为：

* 边界清楚；
* 已有相对完整的顺序流程；
* Journal 与 Git 事实明确；
* 容易验证重启和发布 CAS。

同一 PR 内：

```text
新 Program 上线
旧入口断开
旧 helper 删除
行为测试保持
```

不得长期并存。

### 阶段 4：Agent/Fork

迁移 fork、join、child completion、cancel、busy nudge。

完成后删除通用 `AgentProgram.forkAgent` 样板和任何 identity 自检绕路。

### 阶段 5：Reconcile

将 single-flight、退避与快照读取分离：

```text
基础设施负责调度
Program 负责因果决策
Domain 负责分类
```

### 阶段 6：Blogger/Enforcer

一次性迁走 Cycle 决策、repair、catch-up、commit、park、seal。该阶段不得保留旧 `BloggerRuntimeState` 作为 facade。

### 阶段 7：Projection

按正式 PROJ 迁移顺序逐条迁移。Legacy 与 DSL 的双跑只允许在测试中比较 canonical digest；生产只能选择一个固定实现。

### 阶段 8：全库布尔清扫

逐项分类：

```text
查询结果 bool       保留
物理事实 bool       可保留，优先 DU
行为选择 bool       禁止
生命周期组合 bool   禁止
模式参数 bool       禁止
```

所有豁免必须说明对应物理事实。迁移完成时豁免列表归零或只剩明确 Host DTO。

### 阶段 9：断根

删除：

```text
旧 Flow
Flow.lift
旧 coordinator
旧 RuntimeState
LegacyProjection
临时 parity adapter
迁移 feature flag
兼容入口
```

版本控制保存历史，生产树不保存尸体。

---

## 九、验证体系

### 9.1 编译边界

最强门禁不是 grep，而是不可引用。

建议拆分为：

```text
Wanxiangshu.Core
    Domain + Program AST + Rule DSL

Wanxiangshu.Runtime
    Interpreter + ports

Wanxiangshu.Plugin
    Infrastructure + composition root
```

Core 不引用 Runtime。Program 构造器保持受控可见性。

### 9.2 AST/源码门禁

源码检查只补充编译边界：

```text
Program 文件禁止 task
Program 文件禁止 mutable
Program 文件禁止 Flow.lift
Program 文件禁止 System.IO / OpenCode / GitOperations
行为型 bool 参数禁止
通配分支吞掉 DSL case 禁止
```

现有 `architecture.mjs` 只检查目录、依赖方向、资源读取和遗留词汇，不足以证明 ARCH-001。必须新增真正的 DSL ownership gate。

### 9.3 单元测试

每条 DSL 指令测试：

```text
构造是否合法
解释器调用哪个 port
错误如何映射
取消是否传播
资源是否释放
```

### 9.4 Program Trace 测试

Trace Interpreter 输出：

```text
Fork(coder)
Join(coder)
ReadSnapshot(session)
AppendFact(ChildCompleted)
Return(success)
```

测试断言领域操作顺序，不断言私有 helper 调用次数。

### 9.5 属性测试

至少覆盖：

```text
同一输入产生同一 Program trace
Program 不修改输入事实
任何失败路径都不泄漏资源
取消后不启动新的 owned effect
每个成功 append 最多出现一次
独立规则顺序不改变错误集合
Projection intent 合并具有确定性
```

### 9.6 崩溃矩阵

对每个 durable effect 边界注入崩溃：

```text
effect 前
effect 成功但返回前
fact append 前
fact append 成功后
内存投影更新前
资源释放前
```

重启后只从 Fold 构造新 Program。不得恢复 continuation。

### 9.7 E2E

现有 `agent-dsl` 只能保留为行为 canary。新增断言：

```text
操作轨迹来自 AgentProgram
不存在 direct Host bypass
取消传播到 child
join 只消费一次 completion
重启不恢复旧调用栈
```

---

## 十、完成定义

以下条件必须同时满足：

```text
[ ] active spec 中不存在 spec/13 / PROJ 冲突
[ ] Program 层原始 task { 数量 = 0
[ ] Program 层 let mutable 数量 = 0
[ ] Flow.lift 生产调用数量 = 0
[ ] workflow 行为型 bool 参数数量 = 0
[ ] workflow 程序计数器字段数量 = 0
[ ] 每个顶层 workflow 返回领域 Program
[ ] 每个 Program 有生产、模拟、Trace Interpreter
[ ] Interpreter 不拥有业务分支
[ ] 所有重启边界通过崩溃矩阵
[ ] Projection 只有一个生产 owner
[ ] LegacyProjection 已删除
[ ] 旧 Flow 与旧 RuntimeState 已删除
[ ] CI 负例证明绕过 DSL 必然失败
[ ] npm run check 全绿
```

任何一项未满足，都不能宣称“DSL 重构完成”。

---

## 十一、禁止的伪修复

以下方案一律拒绝：

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

它们只改变外观，不改变控制流所有权。

---

## 十二、最终形态

重构后的源码应让审阅者直接读出业务：

```fsharp
orchestrator {
    do! awaitManager job
    do! reviewCurrentTree job
    do! registerCandidate job
    do! rebaseAgainstFrozenTarget job
    do! reviewCurrentTree job
    return! publishFastForward job
}
```

```fsharp
blogger {
    let! material = awaitMaterial session
    let! cycle = produceCycle material
    let! resolution = validateCycle cycle
    do! commitResolution resolution
    return! continueFrom resolution
}
```

调用栈表示正在做什么；局部变量表示本次流程已知什么；Journal 表示已经发生什么；类型表示哪些世界合法。

不再用字段记录“下一步去哪”，不再用 bool 拼出隐式阶段，不再允许任意 Task 穿透领域语言。

