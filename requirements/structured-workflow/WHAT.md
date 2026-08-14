# structured-workflow — WHAT（唯一 normative 合同）

> 本文是 `structured-workflow` 包的**唯一 normative 合同**。当前世界必须同时成立下列
> 全部命题。历史断言、迁移沉积、被拒方案不在此列（见 HOW.md「历史与弃权」）。
> 每条命题的测试落点见 PROOF.md §1 表对应行。

条款来源映射：历史五层 docs（dsl-structured-program/flow/architecture/loop/execution，2026-08-14 归档）
的 OWNED Clause（COVERAGE.md 归属）与本包 boundary card 的 OWNS。反向下述全部
structured-workflow OWNED Clause 均已落到下列命题或显式驳斥/移交（见各命题「边界」）。

---

## STRUCTURED-WORKFLOW-001：业务流程由宿主语言结构直接表达

**规范陈述**：业务控制流只使用宿主语言原生结构——当前 F# 为 `task { }`、`let!`、
`do!`、`use!`、`match` / `match!`、`return!`、具名纯函数、有界递归。宿主语言调用栈
就是流程栈。**禁止把「程序下一步去哪」编码为可长期存储的字段。**

**含义/动机**：`let!` 是等待、`match` 是分支、`return!` 是继续、`try/finally` 是资源
作用域——语言已经给了全部结构化边界。再造字段版流程栈 = 第二运行时，恢复/测试/非法
状态同时膨胀（WHY.md §1）。

**边界**：
- F# CE 语法本身不是命题；换一种宿主语言结构化表达（仍无第二业务 runtime）不违约
  （boundary card INDEPENDENT CHANGE）。
- 「有界递归」的界限（具体预算）由具体领域条款定义；本命题只要求调用链显式接收并
  使用有限预算，不以无限循环/重试为业务默认。

**证据**：PROOF.md §1 第 1 行。

---

## STRUCTURED-WORKFLOW-002：禁止第二业务运行时

**规范陈述**：领域 DSL 是 CE + 领域命名操作构成的源码表面，**直接执行**。禁止先构造
内部 AST 再解释。下列形态禁止引入或保留：

```text
Program<'instruction,'result> = Pure | Suspend
Command / Reply 总线
Step continuation AST
把普通调用序列编码后再回放的解释器
```

**含义/动机**：Reply DU + Trace 解释器把复杂度乘在每一业务步上；「规则 DSL」只判
「是否允许」，不管「程序下一步」（FLOW-001/002 直执裁决，见 WHY.md §3）。

**边界**：
- 合法例外：JSON/TOML/Host-wire 等**外部协议**边界上的解码（codec/parser，非业务
  Interpreter）——由 `isExternalProtocolPath` 豁免。
- Process 层物理命令形状豁免（`isProcessCommandPath`），因为那是物理请求类型化
  （EXEC-010），不是业务解释器。

**证据**：PROOF.md §1 第 2 行。

---

## STRUCTURED-WORKFLOW-003：状态标签只表示物理/领域真实事物

**规范陈述**：允许用 DU/字段表达：封闭领域词汇（`Role`、`TurnOutcome`、
`BloggerRequestKind`）、已发生事实的持久证据（`AgentFact` 子族、`PromptSubmitted`）、
单次函数返回结果（`ExitOrDeadline`、`KillResult`）。禁止用 DU/字段表达：当前执行到
第几步（`CurrentStage`、`NextAction`、`InFlight`、`Parked`、`Sealed`、`Armed`…）、
由多个 bool/option/DU 正交组合而成的程序状态乘积、跨多个 bounded context 的单一大
总和类型却无分治 fold。

**判断标准**：删除该字段后，能否通过普通函数调用、`match!`、`return!`、资源作用域或
有界递归表达同样顺序？若能，则该字段是程序计数器（DSL-002）。

**推论（无持久程序计数器，GLORY-009）**：Fact 与 Projection 只描述发生过的事实和
已证明的证据；禁止 `Stage`、`Phase`、`NextStep`、`ResumeAt`、`LifeStanding`、
`AwaitingSecondPerfect`、`TodoPlanningStage`、`ReviewStage` 及等价 durable PC。

**边界**：
- 真实资源世代名（如 `CancellationEpoch`）合法——它命名物理世代，不命名执行步骤。
- 领域证据 DUs / 纯查询以 `Pending|Spent|Phase` 结尾、物理算法名（
  `EstimatedRunningSeconds`、`RecoveryStageProbe`）、fold 拒绝 token（`Already*`）是
  真实事物或拒绝事实，不是行为 bool（dsl-ownership allowlist 语义）。

**证据**：PROOF.md §1 第 3 行。

---

## STRUCTURED-WORKFLOW-004：ARCH-008 禁止词不作程序计数器

**规范陈述**：`Stage`、`Phase`、`Lease`、`Owner`、`Generation` 不得作程序计数器或伪
领域状态（真实资源世代名如 `CancellationEpoch` 除外）。

**含义/动机**：这些词天然诱惑「下一步去哪」命名；名字黑名单只能防已经想起来的坏
名字，因此本命题由结构门兜底（`behaviour-bool` 后缀表、`mutable-record-field`
business token 表），改名不改变判定。

**边界**：本命题只钉「不作程序计数器」；`Owner`/`Lease` 作为 session-ontology /
managed-session-lifecycle 的**真实物理归属**词汇时合法——物理资源归属不是程序位置。

**证据**：PROOF.md §1 第 4 行。

---

## STRUCTURED-WORKFLOW-005：组合状态必须可证明合法

**规范陈述**：记录或 DU 同时包含 `State` + `Pending/Offer` + `Recovery/Repair` +
`Drain` 两类以上字段时，必须计算可表示组合总数，并证明每种组合都有真实业务意义；
否则拆分为独立流程或 capability/permit。`state-product` 门禁在**字段名无关**的结构
层面识别 ≥2 个独立状态轴（本地 DU/option/bool、`mutable`/`ref` 存储），并要求
`/// DSL-state-combination: domain|physical` 分类。

**含义/动机**：状态乘积让非法态被类型系统合法化；结构解析 + 显式分类把「未分类即红」
变成构建期失败，同时不误伤合法领域/物理组合（fixtures `state-axes-{illegal,domain,physical}.fs`）。

**边界**：门禁只守卫「未分类即红」，不替代 DSL-002/005 的人工语义判断（正交组合
人工证明见 HOW.md §3.4.1）。

**证据**：PROOF.md §1 第 5 行。

---

## STRUCTURED-WORKFLOW-006：单一真理源

**规范陈述**：同一领域事实不得在多处定义同构 DU。发现 case 集完全相同的两个类型，
必须合并或明确区分其 bounded context，并给出单向转换理由。跨文件重复 case 集判红
（`dup-cases` 门）；显式登记的同构豁免合法（`DUP_CASES_EXEMPT`）。

**含义/动机**：同构 DU 分居是双写/漂移的前奏（DSL-006）；单一真理源压缩不一致面。

**边界**：同一文件内两个 DU 相同 case 集**不**触发（门目标是跨 bounded context 重复）；
bounded context 内单一定义 + 显式转换理由 = 合法。

**证据**：PROOF.md §1 第 6 行。

---

## STRUCTURED-WORKFLOW-007：纯决策与效果按 owner 成树，不按分层成根

**规范陈述**：

生产目录按 bounded owner 成树（Context / Interaction / Enforcer / Execution /
Mission / Change / …），**禁止** `Domain/`、`Application/`、`Session/`、
`Infrastructure/` 作为顶层根。`Kernel/` 只允许 universal primitives，正式名
`Foundation/`（Identity / Roles / Outcome / Temporal / Parallel / AsyncSupport）。

CE、Semantic Vocabulary、Port Decorator、Physical Adapter 是 owner **内部**的
实现种类，不是目录根。

代码性质约束（与目录根正交）：

```text
纯规则 / Evidence / Decision / Projection / Fact cases
    无 Host I/O；不得引用 OpenCode、Process 或 Fable.Core.JsInterop

Semantic Vocabulary
    住在其 bounded owner；名字描述完整业务承诺

Physical Adapter
    只适配外部协议，不解释业务命令
    能力专属 adapter 住在该 owner 的 OpenCode/ 或 Host/ 叶
    删掉任意单个 capability 仍成立的 Host 协议面住在 OpenCode/、Git/、
    Persistence/、Process/、Resources/
```

`open Wanxiangshu.OpenCode|Process` 仅允许：`OpenCode/`、`Process/`、`Git/`、
`Persistence/`、`Resources/`、`Host/`，或路径含 `/OpenCode/` 或 `/Host/` 的
adapter 叶。**禁止 basename 白名单。**

Session 是 `Execution/Session` 的领域概念，不是「凡长生命周期对象都扔这里」的
技术层。event 来源不是 ownership（Journal 不是 projection owner；Host fact 不是
OpenCode Contract）。

`Evidence → Decision` → 穷尽 `match` → effect 是一种可用形态，但不是唯一理想
形态，不得压过具名 Vocabulary 组合（FLOW-004 首选形态）。

**含义/动机**：双根（ownership 树 + layer 树）让同一能力拆成两半；分层标签回答
不了「这个文件消失，哪个概念会不完整」。owner 树让依赖长成非均匀平衡树；
代码性质仍可静态守（`infrastructure-leak` 门）。

**边界**：
- 领域操作必须通过**具名 capability** 调用副作用，每操作一种结果类型，禁止泛化
  `execute Command` 与大 Reply DU 吞掉不可能分支（FLOW-003）。
- 测试对 Fable 产物形状的适配只属于 `requirements/verification-system/tests/support/domain.mjs`（DSL-011）；
  不为测试便利新增生产 export。
- Vocabulary 不得下沉 OpenCode tool adapter、不得上提为与 owner 无关的纯规则层。

**证据**：PROOF.md §1 第 7 行。

---

## STRUCTURED-WORKFLOW-008：mutable/ref 只承载物理资源或局部纯实现

**规范陈述**：`let mutable` 与 `ref` 都是可变存储，只允许：纯算法 scratch（局部函数
内）、并发原语（`Kernel/Parallel.fs`）、物理 Task / Dictionary / TaskCompletionSource /
CancellationTokenRegistration / 锁对象。禁止用 `mutable`/`ref` 表达业务阶段、
`slotArmed`、行为 bool 等控制流状态；record 的 `Foo: T ref` 与 `mutable Foo: T` 同受
state-product 与 physical-owner proof 约束。纯语义路径（无 `/OpenCode/`、`/Host/`、`/Runtime/`、`/Wait/`、`Process/`、
`Git/`、`Persistence/`、`Resources/`）的 mutable 字段直接红；物理运行时路径只有
真正物理状态允许，且必须显式 `/// DSL-state-combination: physical`。

**含义/动机**：可变存储是第二运行时的地基；把豁免变成**声明式**（`// DSL-MUTABLE:
<category>` + physical annotation）让编译器站岗，让「字段改名逃逸」失效。

**边界**：豁免按**具体类型**用结构化 annotation 表达，禁止目录级/文件级整体豁免；
目录级豁免逃逸 → RED。

**证据**：PROOF.md §1 第 8 行。

---

## STRUCTURED-WORKFLOW-009：恢复重入普通流程

**规范陈述**：崩溃后由 Journal fold 产出领域事实（`Boot Fold`），再调用**普通 workflow
入口**。禁止恢复 continuation、program counter、`SlotArmed`、`InFlight` 等执行位置。
Reconcile 是**观测稳定边界**，不是业务操作系统：`ReconcileDecision` 只解决 snapshot
是否稳定、是否需要因果 reread、可否 publish、是否存在 idle repair capability；不拥有
Reviewer/Student/Manager/Join 生命周期。

**含义/动机**：「恢复暂停的协程」是假的——调用栈不可序列化；从事实重入普通 CE 才能
让恢复、重放、审计共享同一程序（ARCH-005 不是「恢复协程」）。

**边界**：
- 恢复协议本身（permit 门、recovery budget、crash 后事实重放的执行语义）→
  `crash-reconciliation`；本命题只钉「重入普通流程、无执行位置恢复」。
- 恢复 evidence 不足时 fail closed，不猜测旧流程执行到哪一步。

**证据**：PROOF.md §1 第 9 行。

---

## STRUCTURED-WORKFLOW-010：有界循环与有界扇出

**规范陈述**：循环与扇出必须有界（与 ARCH-009 一致）。业务层扇出唯一原语是
`Parallel.mapBounded`：`maxConcurrency` 正有限（禁止 0=无界或 0=1）、结果按输入下标
不按完成序、空输入空结果、取消观察 token 并传给每个 action、任一 action 抛出立即
拒绝且许可必须归还。禁止业务层无界 `Promise.all` / `Task.WhenAll` 盖全集；禁止无界
重试环作为业务默认。

**含义/动机**：无界扇出让 canary 因机器负载而非逻辑失败；有界原语把并发预算变成显式
参数，空/取消/拒绝路径可测。

**边界**：适配器内部实现有界原语除外；有界递归的具体预算由领域条款定义。

**证据**：PROOF.md §1 第 10 行。

---

## STRUCTURED-WORKFLOW-011：Semantic Vocabulary 是领域事实词汇

**规范陈述**：业务 CE 可以调用内部包含复杂时序的具名 Vocabulary。Vocabulary 的名字
必须描述**完整业务承诺**，而不是实现动作。判据：只看调用点名字 + 参数 + 返回类型，
reviewer 是否能够合理知道调用者在等待什么语义？

允许示例：`reviewUntilPerfect`、`publishEventually`、`recoverDurably`、
`awaitChildrenSettled`、`finalizeWhenSafe`、`fallbackAcross`。
拒绝示例：`executeSafe`、`process`、`handle`、`doRetry`、`runReliable`、
`withPolicy`、`continue2`。

**含义/动机**：任何聪明都必须有名字；任何名字都必须有 law。词汇名是调用点的第一份
文档——伪 opcode 名让审查失去锚点（rabbit.md，WHY.md §2.5）。

**边界**：
- Vocabulary 住在其 bounded owner 的具名 module；纯规则层仍只拥有
  `Evidence → Decision` / Projection，不因「它是纯的」而回到已删除的 `Domain/` 根。
- 命名 review 义务（DSL-013 五问：名字声明什么承诺 / 隐藏哪些时序 / 哪个 proof 证明 /
  是否改变 trace / crash 后从什么 durable evidence 重入）见 HOW.md §3.5。

**证据**：PROOF.md §1 第 11 行。

---

## STRUCTURED-WORKFLOW-012：Semantic Compression 必须有 proof

**规范陈述**：已被独立 proof 完整覆盖的机械时序允许被 Vocabulary 压缩。调用点可以
隐藏内部机械步骤（read head → rebase → review → CAS → target moved → 再
rebase/review…），但被压缩的 Vocabulary **必须拥有自己的 temporal/behavioral proof**；
无对应 proof 不得压缩。压缩不改变 STRUCTURED-WORKFLOW-001：调用栈仍是宿主 CE；
隐藏的是已证明的机械时序，不是程序计数器。

**含义/动机**：压缩是词汇的私有实现细节；proof 挂在该 Vocabulary 上，调用点只见承诺
名字（rabbit.md DSL-014 目标条款）。

**边界**：每个高阶 Vocabulary 的 proof 义务表见 HOW.md §3.4（源自
历史 DSL proof 条款高阶 Vocabulary 证明义务表）；新增高阶
Vocabulary 必须追加该表一行并挂可观察效果测试。

**证据**：PROOF.md §1 第 12 行。

---

## STRUCTURED-WORKFLOW-013：Decorator 边界

**规范陈述**：Port Decorator 分两类。**Transparent Decorator**（不改变业务 trace 集：
diagnostics、metrics、causal observation、protocol/exception normalization）可自由
叠加。**Semantic Decorator**（改变业务 trace 集：retry、fallback、recovery、dedupe、
claim、deadline policy）必须满足以下之一：(a) 自身已经是有正式 law 的 Semantic
Vocabulary；(b) 在业务 CE 调用点拥有明确语义名字。禁止匿名 middleware 魔法，以及
全局 `DecoratorBase` / `MiddlewarePipeline` / `IWorkflowDecorator` 一类框架；局部
module decorator 叠加允许（composition root 或明确 port-wiring module）。

**含义/动机**：匿名管道把 retry/fallback/recovery 等语义装饰变成不可追踪的黑箱；
具名让每个改变 trace 的装饰有名字、有 law、可审查。

**边界**：transparent decorator 仍须标明「不改变 trace」；语义 decorator / 压缩
Vocabulary 必须能指出对应 proof（DSL-014 联动）。

**证据**：PROOF.md §1 第 13 行。

---

## STRUCTURED-WORKFLOW-014：流程正确性由可观察效果证明

**规范陈述**：流程正确性由可观察效果（事实、调用轨迹、端口交互、终态）证明，不由
「解释器走到了哪个 AST 节点」证明。fake ports 记录调用轨迹与事实；Vocabulary 调用点
以语义名 + 契约证明，不以内部机械步数为权威。

**含义/动机**：测内部 tag = 测实现；测可观察效果 = 测行为。导出面即契约
（`guide-contract`：入口可调用 + 元数钉死），轨迹即因果。

**边界**：proof ladder（static → pure → temporal → adapter → Long Stroke）的层级选择
与晋级规则 → `verification-system`；本命题只钉「证明什么」的语义姿态。

**证据**：PROOF.md §1 第 14 行。

---

## STRUCTURED-WORKFLOW-015：取消是控制面，不是业务数据

**规范陈述**：取消/中断等控制面事件**不是业务阶段**，不得伪装成业务结果数据
（EXEC-020 控制面/数据面分离）。`ABORTED` 不是 agent 终态——取消是控制面事件，把
abort 洗成终态会让恢复与 fallback 走错分支。任何表示控制面事件的字段必须对应真实
物理/领域事物（STRUCTURED-WORKFLOW-003 推论）。

**含义/动机**：控制面（取消、中断、deadline 到达）改变的是「程序接下来还跑不跑」，
不是「世界发生了什么」；两者混同 = 状态标签承载程序位置（TODO-012 交叉：恢复只从
durable facts，禁止 Stage/布尔/时间猜）。

**边界**：
- outcome 分型代数（`Completed | Failed | Abandoned`、completion blob finality）→
  `effect-accounting`；本命题只钉控制面/数据面分离原则。
- join 中断后果（`JoinWaitOutcome.Interrupted`、Esc 语义）→ `delegation` /
  `managed-session-lifecycle`。

**证据**：PROOF.md §1 第 15 行。

---

## 反向覆盖清单（COVERAGE.md 归属核对）

| 源 Clause | 落点 |
|---|---|
| DSL-001/002/003/004/005/006/007/013/014/015 | STRUCTURED-WORKFLOW-001/003/007/009/005/006/008/011/012/013 |
| DSL-008/009/010/011（shape） | STRUCTURED-WORKFLOW-007（分层 + Host 边界白名单 + 测试可见面机制） |
| DSL-012（因果等待观测） | **显式驳斥/移交** → `causal-wait`（本包边界：等待的因果诊断不归 structured-workflow） |
| FLOW-001/002/004/005/008（what）+ FLOW-003/006/007（shape） | STRUCTURED-WORKFLOW-001/002/007/009/014 + 002/010 |
| ARCH-001 / 005 / 008 / 009 | STRUCTURED-WORKFLOW-001+003+008 / 009 / 004 / 010 |
| GLORY-009（无持久程序计数器） | STRUCTURED-WORKFLOW-003 推论 |
| EXEC-020 控制面/数据面 | STRUCTURED-WORKFLOW-015（outcome 分型本体 → `effect-accounting`） |
| LOOP-001..008 | **不归本包** → `degeneration-guard`（LOOP-* 全部）；本包只提供其依赖的「无第二状态机 / 进程内局部事实」保证（LOOP-006 桥接 `continueAfterLoopKill` = structured recovery 词汇） |
| ARCH-002/003/004/006/007/010/011/012/013/014/015/016/017、EXEC-001..032 其余、HOST-*、PERSIST-*、CTX-*、TODO-* | 各自身 owner（host-boundary / prefix-stability / action-affordance / capability-enforcement / provider-projection / participant-horizon / work-record / office-capability / delegation / process-execution / managed-session-lifecycle / effect-accounting / crash-reconciliation / obligation-ledger / review-* / semantic-trace / context-compression 等） |
