# structured-workflow — WHY（不可替代的存在理由）

## 1. 为什么这个包必须独立存在

F# 调用栈已经为业务流程提供了全部结构化边界：

```text
let!     = 等待一个事实或效果
do!      = 执行一个效果
match    = 分支
return!  = 继续（有界递归）
use! / try-finally = 资源作用域
```

把「程序下一步走到哪」重新编码为**可长期存储的字段**（`CurrentStage`、`NextAction`、
`InFlight`、`Parked`、`Sealed`、`Armed`…），等于在业务层再造一个**手写第二运行时**。
它的代价同时打在三个方向：

1. **恢复**：手写运行时必须恢复「执行位置」。而执行位置是调用栈，不可序列化——
   所谓「透明续跑」是假的，实际要么丢栈要么在错误基座上继续。
2. **测试**：测试被迫断言「枚举序数 / 内部 tag」，而不是可观察效果；门禁与 canary
   失去失败价值。
3. **非法状态**：多个 stage 型 bool/option/DU 正交组合时，可表示状态空间爆炸，
   大量组合无业务意义；类型系统不再拦截非法态，反而帮它们合法化。

本包是唯一拥有「控制流不是领域状态」这一保证的包；`crash-reconciliation`、
`effect-accounting`、`obligation-ledger` 等包在它们的 WHAT 里依赖这一保证（见
COVERAGE.md 的 TODO-012 / EXEC-020 / PERSIST-006 / CTX-007 / HOST-007 交叉行）。

## 2. 失败模式（RED 长什么样）

### 2.1 源码表面 Direct-CE，实际仍是状态机

`archive/changes/completed/ce-temporal-ownership.md` §0 记录了一次关键裁决：

> 项目把「Direct CE」做成了源码表面要求，却没有真正把业务时序所有权交给 CE 调用结构。

当时的「完成」是假的：生产代码仍大量采用

```text
Host event → Reconcile → 读取很多 State/Pending/bool
→ 计算「现在到底处于哪个阶段」→ 再决定下一个动作
```

这正是 `StateMachine.execute(state, event)`，只不过被拆成了散落的 if/match。
`TurnCompletionProgram.fs` 变成事实上的**第二运行时**：同时拥有 missing-final-report
repair、interaction repair、ProviderRetry suppression、coverage-before-retry、
fallback advance、loop-kill bridge、abort、terminal materialisation、join guard、
ordinary completion——一个什么都管的业务操作系统，且恢复语义仍由
`25ms polling + wall clock` 推动。

### 2.2 名字再漂亮的程序计数器还是程序计数器

`ManagerTurnDecision = Activate | WaitForChildren | WaitForFinality | Encourage | Complete`
或 `StudentStage = Learning | TeacherReturning | CompileDispatching | Compiling | Finalizing`：

```text
名字再漂亮，仍然等价于 pc = 0/1/2/3/4
```

大 DU 可以是领域词汇或持久事实，但不可以是「当前执行到第几步」。

### 2.3 mutable record 状态机逃逸静态门禁

`fsharp-dsl-governance.md` / `ce-temporal-ownership.md` §13-14 记录：旧的名称黑名单
门禁被 `StudentRunCell` 形态绕过——一个 record 携带 `mutable State`、`mutable Return`、
`mutable Handoff`、`mutable Final` 多个状态轴，字段名全不在黑名单里。教训：

- 名称黑名单只能防住已经想起来的坏名字；
- 必须解析**结构**（record 字段类型轴乘积、`mutable` 存储），字段改名不能改变判定；
- 门禁必须先被故意破坏并变红，才算存在（VERIFY-004 精神）。

### 2.4 双写与影子状态

`dsl-structured-program-gap.md` 记录：`BloggerRuntimeState.Idle | InFlight` 与物理
flight registry 双写，runtime cell 保存流程位置的影子状态。修复 = 删除双写，让物理
single-flight registry（`IParkedTransformHost.HasFlight` / `bloggerFlights`）成为 busy
与 current request 的唯一来源；busy 是**物理事实**，不是业务阶段。

### 2.5 词汇退化为伪 opcode

`rabbit.md`（G4R-CE）记录：复杂时序若没有准确语义名就被隐藏（`executeSafe`、
`process`、`handle`、`doRetry`、`runReliable`、`withPolicy`、`continue2`），调用点无法
回答「调用者在等待什么语义」，压缩变成黑箱，审查失去锚点。对策 = DSL-013/014/015：
任何聪明都必须有名字；任何名字都必须有 law。

## 3. 备选与被拒（考古）

| 备选 | 被拒理由 | 来源 |
|---|---|---|
| 封闭 AST + 唯一 Interpreter 表达流程 | 与 ARCH-001 冲突；Reply DU + Trace 解释器把复杂度乘在每一业务步上 | `archive/docs/why/flow.md` |
| 恢复协程指针 | 调用栈不可序列化；假装透明续跑实为不可恢复 | `archive/docs/why/flow.md` |
| 规则 DSL 兼管程序下一步 | 规则面长第二运行时；职责收窄到「是否允许」，控制流归语言 CE | `archive/docs/why/flow.md` |
| 继续只靠名称黑名单 | 可被等价改名绕过 | `fsharp-dsl-governance.md` Alternatives 1 |
| 对任何含多个 DU/option 的 record 一律判红 | 误伤合法领域模型，不可接受 | `fsharp-dsl-governance.md` Alternatives 2 |
| 只做报告不做门禁 | 不能长期替代可执行门禁 | `fsharp-dsl-governance.md` Alternatives 3 |
| 独立 Loop 恢复机制 | 第二状态机；破坏 FALLBACK-003 唯一写入口；桥接 FallbackController 复用统一预算 | `archive/docs/why/loop.md`（degeneration-guard 交叉） |
| 大 `Decision` DU 压扁整个 workflow | 仍是程序计数器；只允许小型真实领域判断（ReviewWitness / ReviewerOutcome / PromptAcceptance / FamilyRecovery） | `ce-temporal-ownership.md` §2 |
| `TurnCompletionProgram` 什么都管 | 第二运行时；拆成五个独立时序 owner + 薄 router | `ce-temporal-ownership.md` §3/§15 |

## 4. 什么情况下世界 RED

世界 RED 当且仅当下列任一成立：

1. 领域模型保存程序位置（可存储字段回答「下一步去哪」）；
2. 业务层解释 AST 或回放编码后的调用序列（第二运行时）；
3. 依赖可变 stage / bool 乘积才知道下一步做什么；
4. 恢复恢复执行位置而不是重入普通流程；
5. Semantic Vocabulary 名字不声明完整业务承诺，或被压缩的时序没有 proof；
6. 可变存储承载跨调用业务流程位置而不是物理资源。

每条对应 WHAT.md 的命题；可执行证据见 PROOF.md。
