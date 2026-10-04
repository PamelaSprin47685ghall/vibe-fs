# 019/018 变异红证设计（mutation red plan）

本卡为「019/018 变异红证合并补做」的施工图。七个变异点均已完成静态调查：真实函数签名、类型、调用形态全部核对过源码。DevOps 按图执行红绿闭环，不需要再做调查。

行号以 2026-10-04 仓库状态为准。实施时以「改前文本」逐字匹配定位，行号仅作辅助。

## 通用流程（每个变异点单独走一轮）

1. 按该点的「改前 → 改后」做变异（只动该点，不动其他任何文件）。
2. `node scripts/build.mjs` —— 全部七点都是 `.fs` 实现改动、`.fsi` 不变，build 默认走 focused-impact 编译（`build.mjs` 的 `focused-impact` 模式自动选 shard 及前向闭包）。严禁 `dotnet build`。若增量状态异常导致 build 失败，用 `node scripts/build.mjs --clean` 全量重试。
3. 跑对应测试文件（node --test 单文件）：
   - 019 各点：`node --test requirements/managed-session-lifecycle/tests/019.test.mjs`
   - 018 各点：`node --test requirements/managed-session-lifecycle/tests/018.test.mjs`
4. 确认该点「预期红断言」红（其余连锁红允许，见各点说明；测试进程退出非 0 即红）。
5. 按该点「还原 diff」逐字还原（改后文本整段替换回改前文本）。
6. 再次 `node scripts/build.mjs`，重跑同一测试文件，确认全绿。绿了才算该点闭环。
7. 七点全部闭环后，跑整个 managed-session-lifecycle 套件确认无残留。

## 019 变异点（PluginSessionScope.fs / SessionRecoveryHost.fs）

### 019-① SettleExecution 把 release 提前到 terminal await 之前

- 文件：`src/Wanxiangshu/OpenCode/Host/PluginSessionScope.fs`
- 函数：`member private _.SettleExecution(durable: AgentJournal, execution: ChatExecutionState) : Task`（约 106-137 行）
- 变异语义：把 `releasePhysicalExecution` 调用从 `let! settled`（terminal 提交 await）之后**移动**到它之前。不是删除 release——release 仍执行一次，只是不再等 terminal 提交确认。held 期间 lease 被提前归还。
- 改前（release 位于 await 之后，135-136 行）：

```fsharp
    member private _.SettleExecution(durable: AgentJournal, execution: ChatExecutionState) : Task =
        task {
            let! settled =
                match execution.startedEvidence with
```

（中段不变，结尾为）

```fsharp
            ModelRouting.releasePhysicalExecution execution.key.SessionId execution.key.PhysicalUserMessageId
            |> ignore
        }
```

- 改后（release 移到 `task {` 之后、`let! settled` 之前；原 135-136 行删除）：

```fsharp
    member private _.SettleExecution(durable: AgentJournal, execution: ChatExecutionState) : Task =
        task {
            ModelRouting.releasePhysicalExecution execution.key.SessionId execution.key.PhysicalUserMessageId
            |> ignore

            let! settled =
                match execution.startedEvidence with
```

（中段不变，结尾的 `ModelRouting.releasePhysicalExecution ... |> ignore` 两行删除，直接以 `Result.defaultWith` 块收尾到 `}`）

```fsharp
            settled
            |> Result.defaultWith (fun failure ->
                invalidOp (
                    sprintf
                        "session execution settlement failed (%s/%s): %s"
                        (SessionId.value execution.key.SessionId)
                        (PhysicalUserMessageId.value execution.key.PhysicalUserMessageId)
                        failure))
        }
```

- 还原 diff：把插入 `task {` 之后的 `ModelRouting.releasePhysicalExecution ... |> ignore` 两行删除，并在 `Result.defaultWith` 块之后、`}` 之前恢复原两行 `ModelRouting.releasePhysicalExecution execution.key.SessionId execution.key.PhysicalUserMessageId |> ignore`（逐字，含原缩进与空行结构）。
- 预期红：`019.test.mjs` 第二个 test（GAP-126 delete，130 行前那个「actual session delete waits…」），held 期间断言

  `assert.equal(executionCount('ses-delete', 'msg-ses-delete-started'), 1)`（105 行）

  —— 变异后 started key 的 lease 被提前归还，计数变 0，断言红。第三个 test（cancel）的 149 行同款断言也会红。held 期间的 `terminalLineCount === 0` 与 `phase === 'ProviderStarted'` 断言不受此变异影响（terminal append 仍被 barrier park）。
- build：focused。

#### 019-① 偏差根因与修订（2026-10-04 调查卡追加，原文保留）

**偏差事实**：第十三批 DevOps 按上图实施变异，`--clean` gen 306 确认 `dist/OpenCode/Host/PluginSessionScope.js:167` 的 release 调用已在提前位置，但 019.test.mjs 4/0 全绿，105 行预期红断言不敏感。

**根因结论**（第十三批候选方向一「drain 链路时序」成立，方向二「routing 观察口径」不成立）：三个事实叠加，变异点在 held 期间从未在唯一可观察的 key 上执行。

1. **串行 drain 按字典序取第一个 key**：`SettleSessionExecutions` 的 `unfinished` 来自 `ChatExecutionProjection.nonTerminal`，而 `current = ByKey |> Map.toList |> List.map snd`（Projection.fs 97-98）。F# `Map.toList` 按键字典序排序，`ChatExecutionKey` 先比 SessionId 再比 PhysicalUserMessageId；fixture 的 `msg-ses-delete-before-provider`（'b' < 's'）排在 `msg-ses-delete-started` 之前——**for 循环第一个 settle 的是 before-provider key**。
2. **held barrier park 在第一个 key 的 terminal append 上**：第一个 `SettleExecution` 进入后，`PreProviderSettlement.settle` 的 Terminal append 到达 `ControlledTerminalWriter`（SessionRecoveryHostSurface.fs 369-389），arrival 先触发（`awaitTerminalBarrier` 由此返回），gate 未开则 append 挂起——整个串行 for 循环停在第一个 key 上。**started key（唯一持有 live lease 的 key）的 `SettleExecution` 在断言观察时从未进入，变异的 release 前移在该 key 上从未执行**，`executionCount(started)` 保持 1，105 行断言自然绿。
3. **变异在第一个 key 上是 no-op**：变异后 `releasePhysicalExecution(ses-delete, before-provider)` 确实提前执行了，但该 key 的 lease 已被同 session supersession 原子移除（routing 006；测试 104 行断言其计数本就为 0），`retirePhysicalExecution` 的 exact guard（`lease.PhysicalUserMessageId = Some physical`，ModelRouting.fs 782-786）与 active 的 started lease 不匹配——release 落空，无任何可观察差异。

**方向二排除依据**：`sharedCapacitySnapshot().executions` 直接投影 `activeBySession`（ModelRouting.fs 1363-1374）；release 路径 `ReleasePhysicalExecution → releasePhysicalExecutionLocked → Applied → retirePhysicalExecution` 原子执行 `activeBySession.Remove` + `capacity.ReleasePhysical`（ModelRouting.fs 1345-1350、780-787）。若 release(started) 在 held 期间真的被调用，executionCount 确实变 0——观察口径没有盲区；盲区在断言观察时点早于变异代码的执行时点。

**附带发现（原文预期红的一处错误）**：原文称「第三个 test（cancel）的 149 行同款断言也会红」——不成立。cancel 场景走 `signalSessionCancelled → SessionRecoveryHost.SignalSession → Signal → persistTerminal`（SessionRecoveryHost.fs 200-216），其 release（170-176 行）本就在 terminal await 之后，且整条路径**不经过** `PluginSessionScope.SettleExecution`。019-① 的变异点只覆盖 delete 路径（`ClearSession → SettleSessionExecutions → SettleExecution`）；cancel 侧的 release 时序归 SessionRecoveryHost 拥有，若需 cancel 侧红证须另设变异点（`persistTerminal` 内 `do! release key` 前移到 `let! result` 之前），是否补入由 Manager 裁决。

**019-① 修订（变异点移动，替代上图原变异位置）**：把变异从「`SettleExecution` 内部前移」改为「`SettleSessionExecutions` 在 for 循环之前批量预释放」。

- 改前（PluginSessionScope.fs 153-156 行，与 019-③ 改前共用此段，注意两变异不同轮实施）：

```fsharp
        task {
            for durable, execution in unfinished do
                do! this.SettleExecution(durable, execution)
        }
```

- 改后（task 之前插入预释放循环；`SettleExecution` 内部原 release 行不动）：

```fsharp
        for _, execution in unfinished do
            ModelRouting.releasePhysicalExecution execution.key.SessionId execution.key.PhysicalUserMessageId
            |> ignore

        task {
            for durable, execution in unfinished do
                do! this.SettleExecution(durable, execution)
        }
```

- 还原 diff：删除插入的预释放 for 循环三行。
- 变异语义不变：仍是「exact capacity release 先于 durable terminal commit」，破坏 managed-session-lifecycle-019 的同一不变量（delete 须等每个 key durable terminal 且 capacity 归还，release 不得先于 terminal 提交确认）。
- 预期红：delete test held 期间 105 行 `executionCount('ses-delete', 'msg-ses-delete-started') === 1`——预释放循环同步跑完（含 started key），第一个 append park 时 started lease 已归还，计数变 0，断言红。其余断言仍绿：104 行本就为 0；101 行 `completed === false` 不受影响（ClearSession 的 await 结构未动，019-② 的红证锚不冲突）；`terminalLineCount === 0` 与 `phase === 'ProviderStarted'` 不受影响（append 仍被 park；`SettleExecution` 内的 release 变成幂等重放，`AlreadyApplied | StaleFence` 被 `|> ignore` 吞掉，不抛错）。barrier 释放后的最终断言全绿。cancel test 不受此变异影响（见附带发现）。
- 红证锚与 019-②（101 行 completed）、019-③（124/125 行 decoy）互不重叠，维持原文「三组红证互不重叠」格局。
- build：focused。

**测试断言观察盲区（归下批，本卡只记录）**：串行 drain + 单一 held gate 的组合下，held 期间结构性只能观察 drain 字典序第一个 key 的行为；started key（唯一 live lease）的逐 key release 时序在当前 fixture 命名下不可观察。若下批选择测试侧修订而非上述变异点移动：把 fixture 的 started key 命名改为字典序第一（如 `msg-a-started` 配 `msg-z-before-provider`），使 started key 成为 drain 第一个被 settle 的 key——此时原 019-① 变异位置（`SettleExecution` 内前移）即可使 105 行红。两个方案二选一，不必都做。

### 019-② ClearSession 不 await SettleSessionExecutions

- 文件：`src/Wanxiangshu/OpenCode/Host/PluginSessionScope.fs`
- 函数：`member this.ClearSession(sessionId: string) : Task`（约 166-188 行）
- 变异语义：把 `do!`（await）改成同步丢弃——结算仍在后台跑，但 delete 完成信号不再等它。
- 改前（168 行）：

```fsharp
    member this.ClearSession(sessionId: string) : Task =
        task {
            do! this.SettleSessionExecutions sessionId
```

- 改后：

```fsharp
    member this.ClearSession(sessionId: string) : Task =
        task {
            this.SettleSessionExecutions sessionId |> ignore
```

（`task { ... }` CE 内同步表达式后接 `|> ignore`，编译无警告；等价形态 `let _ = this.SettleSessionExecutions sessionId` 亦可。）
- 还原 diff：把 `this.SettleSessionExecutions sessionId |> ignore` 整行替换回 `do! this.SettleSessionExecutions sessionId`（逐字）。
- 预期红：`019.test.mjs` delete test，断言

  `assert.equal(completed, false, 'the delete completion promise must not publish while a terminal commit is held')`（101 行）

  —— 变异后 `drained` 立即 resolve，completed 变 true，断言红。后续 `await drained` 后的 durable 断言可能连锁红（结算尚未完成时 terminal/disposition 未落盘），属预期波及。
- build：focused。

### 019-③ SettleSessionExecutions 去掉 session 过滤

- 文件：`src/Wanxiangshu/OpenCode/Host/PluginSessionScope.fs`
- 函数：`member private this.SettleSessionExecutions(sessionId: string) : Task`（约 139-156 行）
- 变异语义：删除 `List.filter` 的 session 过滤行，让 unfinished 收进**所有会话**的非终态执行——邻近 decoy 会话（ses-preserved）的执行也被结算并释放。
- 改前（143-151 行）：

```fsharp
        let unfinished =
            journal
            |> Option.map (fun durable ->
                AgentJournal.snapshot durable
                |> fun projection -> projection.AgentProjections.ChatExecutions
                |> ChatExecutionProjection.nonTerminal
                |> List.filter (fun execution -> execution.key.SessionId = sid)
                |> List.map (fun execution -> durable, execution))
            |> Option.defaultValue []
```

- 改后（删除 filter 行；`sid` 仍被上方 `cancelUnacquiredExecution` 使用，不会产生未用变量）：

```fsharp
        let unfinished =
            journal
            |> Option.map (fun durable ->
                AgentJournal.snapshot durable
                |> fun projection -> projection.AgentProjections.ChatExecutions
                |> ChatExecutionProjection.nonTerminal
                |> List.map (fun execution -> durable, execution))
            |> Option.defaultValue []
```

- 还原 diff：在 `ChatExecutionProjection.nonTerminal` 行之后恢复 `|> List.filter (fun execution -> execution.key.SessionId = sid)`（逐字）。
- 预期红：`019.test.mjs` delete test，barrier 释放后断言

  `assert.equal(executionCount('ses-preserved', 'msg-preserved'), 1)`（124 行）
  `assert.equal(recoveryHost.executionStatus(host, 'ses-preserved', 'msg-preserved').phase, 'ProviderStarted')`（125 行）

  —— 变异后 decoy 也被结算 Cancelled 并归还 lease：124 行计数变 0 红，125 行 phase 变 `Terminal` 红。held 期间（108 行）不红：decoy 的结算同样被 barrier park，lease 未释放。
- build：focused。

### 019-④ SignalSession 不逐 key await

- 文件：`src/Wanxiangshu/OpenCode/Host/SessionRecoveryHost.fs`
- 函数：`member this.SignalSession (sessionId: SessionId, eventOf: ChatExecutionKey -> ChatExecutionRecoveryLifecycleEvent) : Task`（约 423-439 行）
- 变异语义：`for key in keys do do! this.Signal(...)` 改为 fire-and-forget——Signal 仍在后台执行（其内部 release 仍在 terminal await 之后），但 SignalSession 不等逐 key 结算完成就返回。
- 改前（436-437 行）：

```fsharp
            for key in keys do
                do! this.Signal(eventOf key)
```

- 改后：

```fsharp
            for key in keys do
                this.Signal(eventOf key) |> ignore
```

- 还原 diff：把 `this.Signal(eventOf key) |> ignore` 替换回 `do! this.Signal(eventOf key)`（逐字）。
- 预期红：`019.test.mjs` cancel test（「actual logical cancel waits…」），断言

  `assert.equal(completed, false, 'the logical cancel must not complete while a terminal commit is held')`（146 行）

  —— 变异后 `cancelled` 立即 resolve，completed 变 true，断言红。held 期间的 lease 断言（149 行）不红：后台 Signal 的 release 仍被 barrier park 在 terminal await 之后。barrier 释放后的 durable 断言（157-163 行）可能连锁红（后台结算与断言竞速），属预期波及，以 146 行为该点红证锚。
- build：focused。

## 018 变异点（RunLifecycle.fs / ExecutionFactFold.fs）

### 018-① complete 的 Aborted 分支改走 deliverFailedCompletion（上批失败点，本次已核对签名）

- 文件：`src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs`
- 函数：`let complete (gate: obj) (pendingRuns: Dictionary<string, PendingHostRun>) (journal: AgentJournal option) (parentId: SessionId) (sessions: ISessionHostPort) (handoffPort: ReusableHandoffPort option) (run: PendingHostRun) (outcome: TerminalOutcome) (workRecord: string option) : Task`（约 493-571 行）
- **deliverFailedCompletion 真实签名**（466-473 行，`let private`）：

```fsharp
    let private deliverFailedCompletion
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (run: PendingHostRun)
        (error: string)
        : Task =
```

六个参数，顺序固定：`gate → pendingRuns → journal → parentId → run → error`。`error` 是最后一个参数、类型 `string`。仓内现成调用范例在同文件 571 行：`deliverFailedCompletion gate pendingRuns journal parentId run stop.Reason`。上批变异编译失败的根因就是没核对这个形态（参数个数/顺序/末位 string 与 `deliverProvenCompletion` 的 evidence 参数混同）。
- 变异语义：Aborted 观察分支不再观察，而是把 run 当作 proven failure 结算（ERROR code，因 error 字符串 ≠ "cancelled"）。run 的 durable lifecycle 从 Active 变为完成态。
- 改前（515-517 行）：

```fsharp
        match outcome with
        | Aborted _ ->
            // Observation only. Keep pending run Active for a later proven terminal.
            Task.FromResult(())
```

- 改后（注释一并删除；分支类型仍为 `Task`，match 类型不变）：

```fsharp
        match outcome with
        | Aborted _ ->
            deliverFailedCompletion gate pendingRuns journal parentId run "aborted"
```

- 还原 diff：把 `deliverFailedCompletion gate pendingRuns journal parentId run "aborted"` 整行替换回原两行（注释行 + `Task.FromResult(())`，逐字含注释）。
- 预期红：`018.test.mjs` 第二个 test（GAP-133 四场景），turn-aborted 场景断言

  `assert.equal(stopped.lifecycle, scenario.expected, ...)`（78-79 行，expected 为 `'Active'`；失败消息 `turn-aborted: the stop settles per its real semantics, never as an unauthorized abandon`）

  —— 变异后 lifecycle 变 `CompletedAwaitingJoin`，断言红。后续 coldWorkSnapshot / re-enlist 断言（90、115 行）连锁红属预期波及。注意 `assert.notEqual(stopped.lifecycle, 'Abandoned')`（80 行）**不红**（变异产物是完成态不是 Abandoned）——该行的红证属于 018④。
- build：focused。

### 018-② 删 MISSING_FINAL_REPORT 观察分支

- 文件：`src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs`
- 函数：同上 `complete`
- 变异语义：删除 MISSING_FINAL_REPORT 的观察分支，让该 reason 的 Failed stop 直落到兜底分支 `| Failed stop -> deliverFailedCompletion gate pendingRuns journal parentId run stop.Reason`（571 行）——provider-retry 观察被错误结算为 ERROR 终态。
- 改前（559-570 行，整段删除）：

```fsharp
        | Failed stop when not (stopBelongsToRun run stop) -> Task.FromResult(())
        | Failed stop when
            stop.Reason = "MISSING_FINAL_REPORT"
            || stop.Reason.Contains("MISSING_FINAL_REPORT")
            ->
            // provider-attempt-recovery-008 / P0-RECOVERY-JOIN-001: a missing final report is not a
            // proven terminal failure. The subagent auto-retries and continues (its
            // reconcile loop keeps repairing the empty terminal); delivering a
            // proven MISSING_FINAL_REPORT failure here concludes the run before the
            // last effort (the same reason the `Aborted` branch observes only).
            // Observation only — keep pending run Active for a later proven terminal.
            Task.FromResult()
        | Failed stop -> deliverFailedCompletion gate pendingRuns journal parentId run stop.Reason
```

- 改后（保留 belongsToRun guard 分支与兜底分支，仅删中段）：

```fsharp
        | Failed stop when not (stopBelongsToRun run stop) -> Task.FromResult(())
        | Failed stop -> deliverFailedCompletion gate pendingRuns journal parentId run stop.Reason
```

（match 穷尽性不受影响：`Failed stop` 兜底分支仍在。）
- 还原 diff：在 `stopBelongsToRun` guard 分支之后、兜底分支之前，整段恢复原 559-570 行文本（含 guard、注释与 `Task.FromResult(())`，逐字）。
- 预期红：`018.test.mjs` 四场景 test，provider-retry 场景断言

  `assert.equal(stopped.lifecycle, scenario.expected, ...)`（78-79 行，expected `'Active'`；失败消息 `provider-retry: the stop settles per its real semantics, never as an unauthorized abandon`）

  —— 变异后 MISSING_FINAL_REPORT 被 deliverFailedCompletion 结算，lifecycle 变完成态，断言红。turn-aborted 场景在本变异下不受影响（走 Aborted 分支）。
- build：focused。

### 018-④ fold 把 SendFailure 结算改写 HandleAbandoned

- 文件：`src/Wanxiangshu/Execution/Delegation/ExecutionFactFold.fs`
- 函数：`ExecutionFactFold.fold` 的 `HandleWorkCompleted` 分支（约 88-99 行）
- 结算链核对：fission 场景 `emitStopWithReason(runtime, owner, '', 'Failed', 'fission external abort')` → root 为空 → `TerminalStop.session` → `TerminalOutcome.Failed` → `complete` 兜底分支 → `deliverFailedCompletion` → `recordWorkCompletion` → `appendWorkCompletion`（Handle/Controller.fs 154 行）→ **`ExecutionFactCases.HandleWorkCompleted`**（work 级 fact，Kind = `SendFailure`）→ 本 fold 分支 `HandleProjection.completeWork` → 投影 `CompletedAwaitingJoin`。所以变异落点是 **HandleWorkCompleted 分支**（不是 handle 级的 `HandleCompleted` 分支）。
- 变异语义：Kind 为 SendFailure 的工作完成结算被改写成 abandon——投影落 `Abandoned(ParentCancelled)` 而非 `CompletedAwaitingJoin`。
- 改前（88-99 行；注意 foldWork 第 5 参为 `true`）：

```fsharp
        | ExecutionFactCases.HandleWorkCompleted p ->
            foldWork
                sessionState
                p.ParentSessionId
                p.Work
                "HandleWorkCompleted"
                true
                (HandleProjection.completeWork
                    p.Work
                    { Kind = p.Kind
                      CompletionRef = p.CompletionRef
                      CompletionDigest = p.CompletionDigest })
```

- 改后（新增 SendFailure guard 分支，形态与既有 `HandleWorkAbandoned` 分支（113-120 行）同构（foldWork 第 5 参同为 `true`）；`HandleAbandonReason.ParentCancelled` 为 Facts.fs 20-23 行 DU 的合法构造子；DU 的 `=` structural comparison 合法）：

```fsharp
        | ExecutionFactCases.HandleWorkCompleted p when p.Kind = HandleCompletionKind.SendFailure ->
            foldWork
                sessionState
                p.ParentSessionId
                p.Work
                "HandleWorkAbandoned"
                true
                (HandleProjection.abandonWork p.Work HandleAbandonReason.ParentCancelled)
        | ExecutionFactCases.HandleWorkCompleted p ->
            foldWork
                sessionState
                p.ParentSessionId
                p.Work
                "HandleWorkCompleted"
                true
                (HandleProjection.completeWork
                    p.Work
                    { Kind = p.Kind
                      CompletionRef = p.CompletionRef
                      CompletionDigest = p.CompletionDigest })
```

- 还原 diff：删除新增的整个 `| ExecutionFactCases.HandleWorkCompleted p when p.Kind = HandleCompletionKind.SendFailure ->` 分支（8 行），保留原分支原样。
- 预期红：`018.test.mjs` 四场景 test，fission-external-abort 场景（expected `'CompletedAwaitingJoin'`）：

  `assert.equal(stopped.lifecycle, scenario.expected, ...)`（78-79 行，失败消息 `fission-external-abort: the stop settles per its real semantics, never as an unauthorized abandon`）
  `assert.notEqual(stopped.lifecycle, 'Abandoned')`（80 行，「永不 Abandoned」断言）

  —— 变异后 lifecycle 为 `'Abandoned'`，两条断言同时红。同 test 内 turn-aborted / provider-retry / unknown-stop 场景不受影响（三者均不产生 HandleWorkCompleted fact：Aborted 观察、MISSING_FINAL_REPORT 观察、belongsToRun 拒绝）。coldWorkSnapshot 断言（90 行）大概率**仍绿**（冷重放用同一个变异 fold，两边同为 Abandoned，deepEqual 相等）——不要用它当该点红证锚。
- 波及提示：SendFailure 结算是 delegation 域通用路径。变异期间跑其他包（delegation、crash-reconciliation 等）依赖 SendFailure 完成结算的套件可能同时红。本点红证只以 018 四场景为准；还原后跑全量确认无残留绿转红即可。
- build：focused。

## 附：调查结论汇总

- 七点全部可行，无需替代变异位置。
- `deliverFailedCompletion` 签名见 018-① 小节：六参、末位 `error: string`、返回 `Task`；调用范例 `deliverFailedCompletion gate pendingRuns journal parentId run stop.Reason`（RunLifecycle.fs 571 行）。上批失败根因即未核对此签名。
- 019 三真测试的 held barrier（`awaitTerminalBarrier` / `releaseTerminalBarrier`）park 的是 terminal append 写入本身；因此「release 提前」（019-①）红在 lease 计数断言，「不 await」（019-②④）红在 completed 布尔断言，「去掉过滤」（019-③）红在 decoy 的 barrier 释放后断言——三组红证互不重叠，可各自定位。
- 018③（fold SendFailure 改写）即本卡 018-④，按分册编号为④；分册所列③已做，跳过。
- 所有变异均为 `.fs` 实现改动，`.fsi` 不动，`node scripts/build.mjs` 默认 focused-impact 路径；无需全量。
