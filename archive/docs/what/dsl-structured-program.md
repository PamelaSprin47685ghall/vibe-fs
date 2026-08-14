# DSL 结构化程序规则 — 行为合同

条款前缀：`DSL-`。与 `FLOW-`、`ARCH-001` 同向；冲突时以 `ARCH-001/002/003` 为准。  
边界见 `shape/dsl-structured-program.md`；实现姿态见 `how/dsl-structured-program.md`；证明见 `proof/dsl-structured-program.md`。

## DSL-001：业务流程必须用语言结构表达

业务控制流只使用 F# 原生结构：`task { }`、`let!`、`do!`、`use!`、`match` / `match!`、`return!`、具名纯函数、有界递归。

F# 调用栈就是流程栈。禁止把「程序下一步去哪」编码为可长期存储的字段。

## DSL-002：状态标签必须对应物理世界事物

允许用 DU 表达：

- 封闭领域词汇（如 `Role`、`TurnOutcome`、`BloggerRequestKind`）；
- 已发生事实的持久证据（如 `AgentFact` 子族、`PromptSubmitted`）；
- 单次函数返回结果（如 `ExitOrDeadline`、`KillResult`）。

禁止用 DU / 字段表达：

- 当前执行到第几步（`CurrentStage`、`NextAction`、`InFlight`、`Parked`、`Sealed`、`Armed` 等）；
- 由多个 bool / option / DU 正交组合而成的程序状态乘积；
- 跨多个 bounded context 的单一大总和类型，却无分治 fold。

判断标准：删除该字段后，能否通过普通函数调用、`match!`、`return!`、资源作用域或有界递归表达同样顺序？若能，则该字段是程序计数器。

## DSL-003：纯决策与效果分层

- `Domain`：纯函数 `Evidence → Decision`；不写盘、不发网、不读时钟。
- `Application` / `Session`：直接执行 CE，按 `Decision` 调用端口。
- `Infrastructure`：把能力端口暴露为 `Task<'T>`，不解释业务命令。

禁止在业务层构造 AST 再解释执行。

## DSL-004：恢复重入普通流程

崩溃后由 Journal fold 产出领域事实，再调用普通 workflow 入口。  
禁止恢复 continuation、program counter、`SlotArmed`、`InFlight` 等执行位置。

## DSL-005：组合状态必须可证明合法

记录或 DU 同时包含 `State` + `Pending/Offer` + `Recovery/Repair` + `Drain` 两类以上字段时，必须计算其可表示组合总数，并证明每种组合都有真实业务意义。否则拆分为独立流程或 capability/permit。

## DSL-006：单一真理源

同一领域事实不得在多处定义同构 DU。发现 case 集完全相同的两个类型，必须合并或明确区分其 bounded context，并给出单向转换理由。

## DSL-007：mutable 仅用于物理资源

`let mutable` 与 `ref` 都是可变存储，只允许：

- 纯算法 scratch（局部函数内）；
- `Kernel/Parallel.fs` 等并发原语；
- 物理 Task / Dictionary / TaskCompletionSource / CancellationTokenRegistration / 锁对象。

禁止用 `mutable` 或 `ref` 表达业务阶段、`slotArmed`、行为 bool 等控制流状态；record 的 `Foo: T ref` 与 `mutable Foo: T` 同受 state-product 与 physical-owner proof 约束。


## DSL-012：业务异步等待必须具有非权威因果观测

任何跨业务 owner、跨 Host turn、跨 provider attempt 或跨 physical capability 的业务等待，都必须能够生成一个 process-local diagnostic wait observation。

该 observation：

- 可以描述当前 wait、owner、producer、causal identity、cancellation/deadline；
- **不得**成为决策权威、Journal fact、或 Prompt/decision 输入；
- Application 不得持有 `IWaitSnapshotReader`；Domain 不得引用 CausalWait 实现。

落点：`CausalWait` / `CausalWaitRegistry` / `CausalAwait`（Session），E2E watchdog 的 `CAUSAL FRONTIER` 一屏展示。

## DSL-013：Semantic Vocabulary（语义词汇）

业务 CE 可以调用内部包含复杂时序的具名 Vocabulary。Vocabulary 的名字必须描述完整业务承诺，而不是实现动作。

允许（示例）：

```text
reviewUntilPerfect
publishEventually
recoverDurably
awaitChildrenSettled
finalizeWhenSafe
fallbackAcross
```

拒绝（示例）：

```text
executeSafe
process
handle
doRetry
runReliable
withPolicy
continue2
```

判据：只看调用点名字 + 参数 + 返回类型，reviewer 是否能够合理知道调用者在等待什么语义？若不能，则该名字不合格。

原则：任何聪明都必须有名字；任何名字都必须有 law。所有权与落点见 `shape/dsl-structured-program.md`；命名 review 与 proof 义务见 `proof/dsl-structured-program.md`。

## DSL-014：Semantic Compression（语义压缩）

已被独立 proof 完整覆盖的机械时序允许被 Vocabulary 压缩。

调用点可以隐藏内部机械步骤（例如 read head → rebase → review → CAS → target moved → 再 rebase/review…），但被压缩的 Vocabulary 必须拥有自己的 temporal / behavioral proof。无对应 proof 不得压缩。

压缩不改变 DSL-001：调用栈仍是 F# CE；隐藏的是已证明的机械时序，不是程序计数器。

## DSL-015：Decorator Boundary（装饰器边界）

Port Decorator 分两类。

### Transparent Decorator

不改变业务 trace 集，例如：

```text
diagnostics
metrics
causal observation
protocol normalization
exception normalization
```

可自由叠加。

### Semantic Decorator

改变业务 trace 集，例如：

```text
retry
fallback
recovery
dedupe
claim
deadline policy
```

必须满足以下之一：

1. 自身已经是有正式 law 的 Semantic Vocabulary（DSL-013）；
2. 在业务 CE 调用点拥有明确语义名字。

禁止匿名 middleware 魔法，以及全局 `DecoratorBase` / `MiddlewarePipeline` / `IWorkflowDecorator` 一类框架。局部 module decorator 叠加允许，边界见 `shape/dsl-structured-program.md`。

## 相关条款定义位置

以下条款按 GOV-011 定义于 shape，本表仅为导航，不重复定义。

| 条款 | 定义位置 |
|---|---|
| DSL-001 | 本文件 |
| DSL-002 | 本文件 |
| DSL-003 | 本文件 |
| DSL-004 | 本文件 |
| DSL-005 | 本文件 |
| DSL-006 | 本文件 |
| DSL-007 | 本文件 |
| DSL-012 | 本文件 |
| DSL-013 | 本文件 |
| DSL-014 | 本文件 |
| DSL-015 | 本文件 |
| DSL-008 | [`shape/dsl-structured-program.md`](../shape/dsl-structured-program.md)（DSL-008：分层所有权） |
| DSL-009 | [`shape/dsl-structured-program.md`](../shape/dsl-structured-program.md)（DSL-009：模块与职责） |
| DSL-010 | [`shape/dsl-structured-program.md`](../shape/dsl-structured-program.md)（DSL-010：Host 边界白名单） |
| DSL-011 | [`shape/dsl-structured-program.md`](../shape/dsl-structured-program.md)（DSL-011：测试可见面） |
