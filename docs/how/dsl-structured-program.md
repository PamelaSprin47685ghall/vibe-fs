# DSL 结构化程序规则 — 实现姿态

行为见 `what/dsl-structured-program.md`（`DSL-001..007`）；边界见 `shape/dsl-structured-program.md`；证明见 `proof/dsl-structured-program.md`。本文是 PR 1 的教学样板与迁移完成记录。

## CE 风格指南（七个符号）

业务控制流只用 F# 原生结构，调用栈就是流程栈：

| 符号 | 用途 | 反例 |
|------|------|------|
| `task { }` | 一次业务流程的边界 | 业务 AST + Interpreter |
| `let!` | 等待一步结果并继续 | 把结果先存进字段再 match |
| `do!` | 等待副作用完成 | 丢弃 Task 后靠另一个回调推进 |
| `use!` | 资源作用域（进程、租约、端口） | 手动 dispose + `isDisposed` 布尔 |
| `match` / `match!` | 按领域事实分派 | 按 `CurrentStage` 分派 |
| `return!` | 尾调用进入下一步 | `next <- NextStage.X; loop ()` |
| 具名纯函数 / 有界递归 | 可读的分层 | 把判断写成脑内单步调试的控制流 |

判断标准（DSL-002）：删除某个字段后，能否通过普通函数调用、`match!`、`return!`、资源作用域或有界递归表达同样顺序？若能，该字段是程序计数器。

## 最小正例

```fsharp
// 失败恢复：证据 → 决策 → 直接执行（DSL-003 / DSL-004）
let chooseNext (claim: RepairClaim) (coverage: Coverage) : NextDecision =
    match claim with
    | ClaimNone when coverage.HasGap -> NextDecision.Resume gapStart
    | ClaimSome run when run = coverage.LastCommitted -> NextDecision.Normal
    | _ -> NextDecision.Stall

let runRecovery (ports: Ports) (claim: RepairClaim) (coverage: Coverage) : Task<RecoveryOutcome> =
    task {
        match chooseNext claim coverage with
        | NextDecision.Resume offset ->
            let! ctx = ports.Rebuild offset
            return! ports.Send ctx
        | NextDecision.Normal -> return! ports.SendNormal ()
        | NextDecision.Stall -> return RecoveryOutcome.Stalled
    }
```

没有 `Stage` 字段、没有 `pending/offered` 布尔；「下一步做什么」由 `match` 和调用栈承载。

## 负面示例（门禁负例）

- `CurrentStage` / `NextAction` / `InFlight` / `Parked` / `Sealed` / `Armed` 作字段——`scripts/checks/dsl-ownership.mjs` 的 `program-counter` 门与后缀模式（`Stage|Phase|Next|Running|Pending|Spent|Already|Should`）红；测试 `tests/unit/verify/dsl-ownership.test.mjs`。
- 「DU 改名后仍然是程序计数器」——门禁盯后缀与名单，不盯历史名字：`CurrentStage`/`CurrentMode`/`RuntimeCondition`/`LifecyclePosition`/`InFlightFlag`/`ParkedMarker` 均列入名单（改名 canary）。
- 多布尔循环（`let mutable a = false` ×2 + `while`）——`bool-loop` 门。
- 重复 DU case 集——`dup-cases` 门；豁免须登记 `DUP_CASES_EXEMPT` 并附理由。

## 迁移完成记录

proposal 登记的 9 项偏离已全部消除：

1. `Process/NodeProcessWait.fs`：两段等待 + 顶层 `task`（`waitForProcess`），阶段即当前函数；仅剩计时器资源所有权 mutable（bounded scratch 豁免）。
2. `Session/Companion.fs`：恢复槽为一次性物理信号（`ArmRecoverySlot`/`IsRecoveryArmed`/`DisarmRecoverySlot`），无 `TaskCompletionSource` 死尾巴；`TryConsumeRecoverySlot` 已删（零调用）。
3. `Session/BloggerRuntimeState.fs`：`Recovery` 由 `BloggerRecoveryProbe` 从 durable claim + transcript 推导（ENFORCER-153）；`PendingOffer` 交 host dictionary；`Sealed` 为 durable projection 查询；`Parked` 已删——cell 只剩 `InFlight`/`Idle`，`onMaterial` 以显式 `hasParkedWaiter` 读取 host waiter 事实。
4. `Session/BloggerRuntimeState.fs`：`BloggerToolRecovery` 不再入 cell；恢复阶段由 `BloggerCrashRecovery.liveRecovery` 推导。
5. `Domain/SessionRecovery.fs`：trace 解释器删除；`FamilyRecoveryPermit` 私有构造器保证恢复后业务入口持证。
6. `TurnOutcome`：`Domain/ReconcileProgram.TurnOutcome` 唯一。
7. `Role`：`Kernel.Role` 唯一（`AgentRole` 已删，`AgentRoleIdentity` 为转换模块）。
8. `Journal/AgentFact`：54-case 拆为 7 个 bounded-context family（`*FactCases`），wire 逐字节兼容。
9. `scripts/checks/dsl-ownership.mjs`：9 门 + 1 报告——`mutable`（目录级豁免 + fail-closed）、`flow-lift`、`second-runtime-protocol`、`business-interpreter`、`infrastructure-leak`（Host 边界白名单）、`program-counter`（名单 + 后缀 + 改名 canary）、`behaviour-bool`、`bool-loop`（文件级）、`dup-cases`（文件级 + 豁免登记）、`scanLargeDus` 报告（≥10 case 须 `/// DSL-class:` 标注）。`Process/` 为物理路径豁免（OS 进程等待是物理世界，非业务状态机）。

## 迁移顺序（历史）

```text
PR 2 NodeProcessWait      → 已落地（物理豁免 + 两段等待）
PR 3 TurnOutcome + Role   → 已落地
PR 4 RecoveryTrace        → 已落地（permit 替代解释器）
PR 5 BloggerToolRecovery  → 已落地（推导化）
PR 6 Companion 恢复槽     → 已落地（物理信号）
PR 7 BloggerRuntimeCell   → 已落地（二态 + 显式 waiter 参数）
PR 8 AgentFact            → 已落地（family 拆分）
PR 9 dsl-ownership        → 已落地（9 门 + 1 报告）
```

## 迁移验收表（提交前逐项回答）

### 业务所有权

- 这段 workflow 的唯一入口是什么？
- 哪个函数拥有完整资源生命周期？
- 哪个模块拥有纯 Decision？
- 哪个模块执行副作用？

### 状态检查

- 是否存在表示「下一步执行什么」的字段？
- 是否存在由多个 bool/option/DU 正交组合出的状态空间？
- 能否把其中某个字段变成当前函数的局部变量？
- 能否把某个状态变成正在等待的 Task？
- 能否把某个 flag 变成不可伪造的 capability/permit？

### CE 检查

- 异步效果是否通过 `let!`/`do!` 明确出现？
- 分支是否通过 `match`/`match!` 明确出现？
- 下一轮是否通过 `return!` 明确出现？
- 资源是否在同一作用域释放？
- 是否新增了 AST、Interpreter、Command/Reply 或 Step 节点？

### 测试检查

- 测试是否走公共入口？
- 是否断言真实 Journal 事实或端口调用？
- 是否覆盖失败、取消、超时和恢复？
- 是否还在断言内部 state tag？
- 删除旧状态后，测试是否反而更接近可观察行为？

## 最终判断口诀

看到一段复杂代码时，按这个顺序问：

```text
一、这是事实，还是「下一步」？
二、如果是事实，能否由纯函数得到 Decision？
三、如果是下一步，能否直接调用下一函数？
四、如果要等待，能否 let! 等一个真实信号？
五、如果要重试，能否 return! 进入下一轮？
六、如果要清理，能否让资源留在同一作用域？
七、如果仍需 mutable，它管理的是物理资源，还是业务进度？
```

只有最后一个问题的答案是「它只管理锁、Task、TCS、Dictionary、buffer、subscription 或 cancellation lifetime」时，这个 mutable 才应进入豁免（dsl-ownership 的 `mutable` 门 fail-closed 之外的登记路径）。

团队不需要「学会一套神秘的 F# DSL」——普通函数命名业务动作，纯函数做决定，`task` CE 排列顺序，`let!` 等待，`match` 分支，`return!` 继续，`try/finally` 管资源生命周期。除此之外不再创造第二套运行时。
