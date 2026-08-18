# structured-workflow — HOW（实现模型与约束）

> 本文件**非 normative**：只解释实现模型、静态门禁机制与历史考古。唯一 normative
> 合同是 WHAT.md。命名/行数/文件布局可整体重写，只要 WHAT 命题不变。

## 1. 实现种类（住在 owner 内部，不是目录根）

生产代码分成四种东西：

```text
Business CE          讲故事（owner workflow 入口与有界递归）
Semantic Vocabulary  给复杂时序一个领域名字与 law（DSL-013/014）
Port Decorator       给一次能力逐层增加 observation / normalization / physical policy（DSL-015）
Physical Adapter     真的碰 OpenCode / Git / process / timer（owner 的 OpenCode/Host 叶，或 OpenCode/Git/Persistence/Process/Resources 根）
```

原则：**CE 负责故事；Vocabulary 负责定理；Decorator 负责能力；Port 负责物理。**
四种是代码性质，不是 `Domain/` / `Application/` / `Session/` / `Infrastructure/` 根。
不发明第二套 workflow framework，不造 AST / interpreter / `ReliableFlowBuilder` 黑盒，
不把生产程序重新压成几十个 `Decision` case。

组合原则：**Fractal CE / composition closure**。任意 Business CE 缩小后可以成为一个
具名 Vocabulary operation；展开该 operation，仍应看到 CE bind/return、有界递归、
高阶组合与更小的 Semantic Vocabulary。递归只在纯 `Evidence → Decision` 或 physical
adapter 叶停止。parent 不读取 child 的 stage/phase/registry presence 来 drive child。

## 2. 模块地图（当前实现）

### 2.1 Kernel 类型（直接 CE 程序的领域事实）

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Execution/Agent/Errors.fs` + `src/Wanxiangshu/Context/Companion/Errors.fs` | `AgentError`（HostFailure / SessionDead / InvalidFork / ParentCancelled）、`CompanionError`、`AgentContext { SessionId; AgentName }`、`CompanionContext { SessionId }` —— rotation-2 后由各领域 owner 持有，**不是 Flow AST** |
| `src/Wanxiangshu/Foundation/Outcome.fs` | `AgentRunResult`（SessionId / AuthorityRootUserMessageId / ProviderRun / Role / Directory option / TerminalText / TurnFormalText + `IsValid`，EXEC-006）；`SendOutcome`（AdmittedWithReceipt / AdmittedWithPhysicalMessage / Retryable / AcceptanceUnknown / Fatal，PROMPT-005）；`SessionError`（NoProgress / SessionCancelled / AutoRecoveryExhausted / ReviewExhausted / PromptUncertain / ProjectionBroken / InboxFull / Protocol）；`CommitResult<'e>`（Committed / CommitUnknown，PERSIST-002） |

`Kernel/Parallel.fs` 是唯一业务并发原语 `mapBounded`（ARCH-009，见 WHAT 010）。

### 2.2 Application 直接 CE（故事层）

| 文件 | 导出 | 角色 |
|---|---|---|
| `src/Wanxiangshu/Application/Manager/ManagerWorkflow.fs` | `observe`、`observeIdle` | Manager 终态业务故事：handoff → background → idle labor，全部 CE 顺序表达 |
| `src/Wanxiangshu/Mission/Review/Barrier/Reverify.fs` | `ReviewBarrierWorkflow.reverify` | Finality dual-PERFECT 唯一 temporal owner：first judgement → physical challenge → second judgement 时序在 CE 栈表达 |
| `src/Wanxiangshu/Mission/Review/Judgement/Workflow.fs` | `observe` | Reviewer terminal observer 只报告物理 completion；Finality CE ownership 下不决定 challenge/confirmation 工作 |
| `src/Wanxiangshu/Composition/Turn/Workflow.fs` | `observe` | 极薄 router：按 bounded context 委派（Manager/Reviewer/Ordinary），不计算 pending/shouldContinue/phase |

Manager 词汇：`ManagerBackground.ensureSettled`、
`ManagerIdle.encourageLabor`、`ManagerJobHandoff.completeIfTransferred`、
`ManagerFinality.admitLabor` / `classifyEnding`。
Reviewer 词汇：`ReviewBarrierWorkflow.reverify`、`ReviewJudgementInbox.acquire/tryDeliver`（物理 rendezvous）、`ReviewerContinuation.ensureVerdictSubmitted`（仅 process review 缺失 repair）。
恢复词汇：`SessionRecoveryWorkflow.recoverFamilyDirect`（Application/Reconciliation）、
`ProviderRecoveryWorkflow.continueAfterConfirmedFailure` / `continueAfterLoopKill` /
`awaitRecoveryMaterial`（Application/Recovery）。

### 2.3 Domain 纯规则（Evidence → Decision）

`src/Wanxiangshu/Domain/ReconcileProgram.fs`：纯观测稳定边界——`decideStep`（有界因果
reread，≤3 次）、`publishDecision`（terminal/provisional consume maps）、
`isTerminalOutcome`、`SnapshotObservation` 承载已降级的 `TurnUnknown`（HOST-004：
`TurnUnknown` 不是 `TurnOutcome` case）。它不拥有任何业务 workflow。

### 2.4 主设计法 vs 可用形态

```text
首选：typed evidence / capability → semantic vocabulary → CE bind / 有界递归 / 高阶组合 → effect
可用：Evidence → Decision → 穷尽 match → typed port effect（局部封闭判定）
```

## 3. 静态门禁机制（可执行约束）

### 3.1 `scripts/checks/dsl-ownership.mjs`（positive 结构门，`--threshold=0`）

全量扫描 `src/Wanxiangshu/**/*.fs`，无目录级整体豁免。门清单（GATE_NAMES）：

| Gate | 守住的 WHAT 命题 | 机制 |
|---|---|---|
| `second-runtime-protocol` | 002 | `*Command/*Reply`（含泛型）、`*Program<T>`、`Step/Suspend of` 节点、`ProtocolMismatch` 补偿 token |
| `business-interpreter` | 002 | 内部 `module *Interpreter =`（外部协议路径豁免） |
| `flow-lift` | 002/001 | `Flow.lift` / `Flow.create` 旧 monad 面 |
| `program-counter` | 003 | 词表 + `/// DSL-class: ControlState` 硬红；**legacy symbol blacklist 部分 = migration，cutover DELETE** |
| `behaviour-bool` | 003/004 | `*Stage/*Phase/*Next/*Running/*Pending/*Spent/*Already/*Should` 后缀（allowlist 除外）+ 精确名块；**名称正则部分 = migration，cutover DELETE** |
| `state-product` | 005 | 结构解析 record 字段轴乘积（本地 DU/option/bool + `mutable`/`ref` 存储），字段名无关；要求 `DSL-state-combination` 分类 |
| `mutable` | 008 | `let mutable` 必须带 `// DSL-MUTABLE: <category>` 声明（全路径声明制） |
| `mutable-record-field` | 008 | `mutable Foo:` / `Foo: T ref` 业务 token（State/Phase/Stage/Mode/RunState/Handoff/…）直接红；Session/Process 需 `DSL-state-combination: physical` |
| `bool-loop` | 003/008 | 文件级：两个 mutable false + while 循环 |
| `dup-cases` | 006 | 跨文件同 case 集（`DUP_CASES_EXEMPT` 显式豁免） |
| `registry-joint-branch` | 003/005 | 两个 declared registry 的 direct/try probe 联合选择 effect branch |
| `infrastructure-leak` | 007 | `open Wanxiangshu.Infrastructure|OpenCode|Process` 或 FQN 引用；仅允许登记的 Host owner exact paths（DSL-010） |
| 大 DU 分类 + `DSL-control-state-reason` | 003/005 | ≥10 case 必须 `DSL-class`；ControlState 必须机器可校验理由（ce-equivalent=none + blockers 覆盖 function-call/match!/return!/resource-scope/waiter/bounded-recursion） |

fixtures：`tests/fixtures/{state-axes-illegal,state-axes-domain,state-axes-physical,state-axes-multiline,mutable-record-program-counter,ref-record-program-counter,registry-joint-branch}.fs`。

### 3.2 `scripts/checks/g4r-ce-vocabulary.mjs`（CE vocabulary absence ratchet）

- `OBSOLETE_CONTROLLER_PATHS`：`TurnCompletionProgram.fs`、`FinalityController.fs`、
  `ReviewController.fs`、`ManagerLifecycleGate.fs`、`ReviewerGuardState.fs`、
  `HostReviewProgram.fs` 必须 absent（S14 hard phase，当前生产树已全删）。
- `RAW_TIME_TOKENS` 扫描 Domain/Application/Session 的 `DateTimeOffset.UtcNow` /
  `Date.now` / `setTimeout` / `timerTask` 等——**raw-time 禁止语义归
  `time-capability`**，本门只是共享 scanner（MECHANISM 交叉）。

### 3.2.1 `scripts/checks/fsharp-control-pyramid.mjs`（lexical decision ratchet）

STRUCTURED-WORKFLOW-016 的 shape gate。scanner 以 F# offside/缩进结构识别
`match` / `match!` / `function` / 多行 `if` / `try` / `while` / `for`；同级顺序 decision
不算 nesting，`if/elif/else` 算同一层。扫描前会屏蔽行注释、可嵌套 block comment、普通
字符串与 triple-quoted string，避免教程/示例文本制造命中。

当前生产树 bootstrap：301 个文件、2166 个 depth≥2 decision。直接 hard-zero 会把任何
小改动不得被历史噪音掩盖，因此 `scripts/check.mjs` 以 `--root=src/Wanxiangshu`
执行绝对零门：任一 nested decision 立即 RED。`--show-all` 显示全部债务；
不存在 migration baseline，也不存在 grandfathered control-flow debt。

失败输出契约：先打印每个命中的 `file:line + depth + chain + source`，随后只打印一次
`CONTROL_PYRAMID_GUIDE`。教程由测试硬钉最低 512 行 / 9302 字符，禁止以后以“精简”名义
删掉修复路径。

门禁先修已经落地：

- `FsToolkit.ErrorHandling 5.2.0`：只使用其 **Fable source surface** 提供的 `result` /
  `option` 与纯 Result collection vocabulary；Sphinx 不再私造 `ResultBuilder`。禁止引用该包
  被 `FABLE_COMPILER` 排除的 `Task.map` / `TaskResult.*` / `List.traverseTaskResultM`。
- `Wanxiangshu.Foundation.TaskResultBuilder` + `TaskValue` / `TaskResult` / `TaskResultList`：本仓
  Fable-only async Result vocabulary。`Task<Result<_,_>>` / `Result<_,_>` 直接 bind，普通
  `Task<'T>` 显式 `TaskResultCE.ofTask`；错误映射用 `TaskResult.mapError`，异步短路遍历用
  `TaskResultList.traverseM`，普通异步映射用 `TaskValue.map`。这些组合子只压平 plumbing，
  不拥有业务 decision。
- `WriterStreamSync.readRemote` / `syncWriterStreams` 是 `taskResult` 参考调用点；
  `importRemote` 是 `result` 参考调用点。

工程师命令：

```text
node scripts/checks/fsharp-control-pyramid.mjs --explain
node scripts/checks/fsharp-control-pyramid.mjs --root=src/Wanxiangshu --show-all
node scripts/checks/fsharp-control-pyramid.mjs --root=src/Wanxiangshu --snapshot
node --test requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs
node --test requirements/structured-workflow/tests/error-handling-vocabulary.test.mjs
node scripts/check.mjs
```

`--show-all` 是清债视图；正式 CI 直接报告全部当前命中并在任一命中时失败。
没有 suppression / allowlist；若某命中不是机械 bind，也必须人工重审并命名
control-flow boundary。

### 3.3 高阶 Vocabulary 证明义务（DSL-014）

每个改变 trace 的压缩 Vocabulary 必须有 temporal/behavioral proof。当前义务表：

| Vocabulary | 必须证明 |
|---|---|
| `ManagerBackground.ensureSettled` | completion / join / wake permutations |
| `ManagerIdle.encourageLabor` | independent idle occasions / stale permit |
| `ReviewBarrierWorkflow.reverify` | first PERFECT / challenge PhysicalAccepted / second PERFECT / REVISE / terminal ordering |
| `ReviewerContinuation.ensurePerfectConfirmed` | closed continuation no-op / missing verdict nudge |
| `FallbackLedger.recordConfirmedFailure` | dedupe / AABB / exhaustion |
| `ProviderRecoveryWorkflow.continueAfterConfirmedFailure` | failure → material → continuation |
| `FinalityCohort.reviewUntilFirstRevisionOrAllConfirmed` | cohort interleavings |
| `SessionRecoveryWorkflow.recoverFamilyDirect` | closure orders / missing evidence |
| `Orchestrator.publishEventually` | target movement recursion |

新增高阶 Vocabulary 必须追加本表一行并挂可观察效果测试。

### 3.3.1 正交组合证明（DSL-005，人工）

> 本节约 DSL-005 的人工证明（2026-08-14 cutover 自旧 proof 归档吸收）。
> 自动化下限现已含结构化 `state-product` 门禁：`scripts/checks/dsl-ownership.mjs` 解析
> record 的字段类型结构（本地 DU/`option`/`bool`），识别 ≥2 个独立状态轴并要求显式
> `/// DSL-state-combination: domain|physical` 分类；判定与字段名无关。`ControlState`
> 分类要求机器可校验的 `/// DSL-control-state-reason:` 理由。下表仍是架构级语义枚举，
> 门禁只守卫「未分类即红」，不替代 DSL-002/DSL-005 的人工语义判断。

**正交轴与物理归属（当前生产）**

| 轴 | 物理归属 / 类型 | 说明 |
|---|---|---|
| busy / current request | `IParkedTransformHost` flight registry（`HasFlight` / `bloggerFlights`） | 唯一 writer 与读取来源；不再用 `BloggerRuntimeState` DU |
| parked waiter | physical parked registry / `HasParked` | 与 flight 分离 |
| pending offer | pending-offer 物理槽（与 current request 分离） | 见收敛测试 C0 断言 |
| drain | `DrainWindow`（`Closed \| Open of DrainPermit`） | 单轴；permit 不可伪造 |
| tool recovery | `BloggerToolRecovery`（由 durable evidence 派生） | 非长期 cell 程序计数 |
| material 路由 | 纯函数 `BloggerRuntime.decideMaterial` | 由物理事实 + 请求上下文派生，不持久化流程位置 |

**可表示组合与业务意义**

当前 Blogger 运行时**不**将 State + Pending/Offer + Recovery/Repair + Drain 编码进同一长期
record/DU。可观察「组合」由**独立物理槽位的存在性**构成，而非组合状态机 case：

1. 无 flight / 无 parked / drain Closed：可接受新 material（空闲路径）。
2. 有 flight：busy；新 material 由 `decideMaterial`/`blocksNewRequest` 跳过或排队策略处理，不另写 Idle|InFlight 镜像。
3. 有 parked（无或有关联 offer 槽）：parked 等待；与 flight 正交，不合并为单一程序计数 DU。
4. drain Open：仅 reactivation 路径可 mint；与 busy 由物理槽位分别表示，不合成 `InFlightAndDraining` 一类 case。
5. recovery 需要：由 journal/durable evidence 派生 `BloggerToolRecovery`，不写入 runtime cell 位置字段。

因此 DSL-005 要求的「组合总数」在当前架构下为**槽位笛卡尔积的可观测子集**，每种可达
组合均对应上表真实物理语义；不可达组合（例如「用 cell.State 表示下一步」）已通过删除
`BloggerRuntimeState`/`BloggerRuntimeCell` 与 C0 永久测试禁止。

**自动化下限**（防止重新引入程序计数字段与影子状态）：

- `scripts/checks/dsl-ownership.mjs --threshold=0`（program-counter / large-DU / ControlState 理由 /
  `state-product` 组合轴等结构门；全量扫描全部生产 `src/Wanxiangshu/**/*.fs`，无目录级豁免）
- `requirements/context-compression/tests/blogger-convergence-gaps.test.mjs`（`HasFlight` 唯一 busy、
  无 shadow state API）
- `requirements/structured-workflow/tests/dsl-ownership.test.mjs`（含 `state-axes-{illegal,domain,physical}.fs`
  与 `ControlState` reason fixtures）

`state-product` 门禁在字段名无关的结构层面识别 record 状态轴乘积；它不替代上表人工枚举
的架构级语义，只把「未分类组合」变成构建期失败。`registry-joint-branch` 只拒绝两个
declared registry 的 direct/try probe 联合选择 effect branch 这一语法反例；其它多 registry
联合 presence 的分散探测不在该自动门禁范围内，须由人工 proof 判断是否驱动阶段推进。

**SyncDelegate registries — CE collapse（原 StudentTeacher 证明已迁此）**

`StudentTeacherRuntime` / `StudentQaStore` / Learn·Compile·SKILL **G3 已删除（absent）**，不得再作
当前架构证明面。原 Teacher Returned→Completion CE 价值由
`src/Wanxiangshu/Session/SyncDelegateRuntime.fs` 承接（EXEC-026/028）。

参照上表 Blogger 正交物理槽位风格：SyncDelegate registries **各拥有单一物理 lifetime /
投递地址**，且批次边界来自 Host 已知事实，不从 registry presence 或调度时机推断：

| registry | 物理归属 | 消费方式 |
|---|---|---|
| `pendingBatches` | `(ReuseScope, role, ProviderRun)` 尚未收齐的 semantic batch；expected `ToolCallId` 顺序由 Host assistant message 固定 | 每个 tool invocation 只填自己的已知 slot；收齐 expected members 后一次性转 active，禁止 timer/microtask drain |
| `activeBatches` | `(ReuseScope, role)` 当前唯一已 admission batch reservation | batch completion / cancel / dispose 释放；另一 ProviderRun overlap fail closed，不排队 |
| `callsByOwnerScope` / `callsByDelegate` | semantic batch 对 dedicated Session 的一次真实 Send→ordinary Completion | `HandleTurn` 只按 delegate Session 找当前 active call；完成后只写一份 bounded WorkRecord |

`Invoke` 为单一 CE 栈：Admit current `ToolCallId` against ProviderRun expected members → batch
complete 后 GetOrCreate → ordered prepare/concat → one Send → await ordinary Completion →
materialize one bounded WorkRecord。provider 顺序第一项是 canonical invocation；siblings 只得到
`MergedInto canonicalCall`。不存在 `Returned`、`pendingCompletionTexts`、
`SyncDelegateReturnCompletion` 或 `return` 工具。

结构性证明目标：batch membership 只由 `(ProviderRunIdentity, ToolCallId order)` 决定；`HandleTurn`
只消费一个 active call。无 joint-registry PC、无到达时序分支、无 queue-on-session，因此
「批次尚未收齐」与「callee 正在执行」是两个互斥物理 lifetime，而不是隐式业务阶段。

举一反三（同仓物理 presence 对照）：`BloggerRuntime.decideMaterial`、
`PluginRuntimeScope` parked∩pendingOffer、`Reconciler` active∩queued、`BloggerCrashRecovery`
多 presence、`ReviewJudgementInbox` owners∩waiters 均属**物理资源路由 / 投递握手 /
调度 latch**，不是用 mailbox presence 推导 lifecycle stage 的 PC；保持 ACCEPT-as-physical，
不套用 CE collapse。

### 3.5 Vocabulary 命名 review（DSL-013 五问）

每个新增 Application public function（尤其拟作 Semantic Vocabulary 暴露的入口）必须
在 review / Change proof 中回答：

1. 它的名字声明了什么业务承诺？
2. 它隐藏了哪些时序？
3. 哪个 temporal / behavioral proof 证明这些时序？
4. 它改变 trace 集，还是 transparent decorator？
5. crash 后从什么 durable evidence 重入？

回答不出则 REVISE。

### 3.6 Cross-module CE seam review（STRUCTURED-WORKFLOW-017）

每次 workflow 重构除文件内 census 外，再沿调用链检查 seam：

1. 返回值是否包含 `Stage/Phase/NextAction/ResumeAt/ContinueToken` 或等价执行位置？
2. caller 是否 `match` 子模块控制 token 后决定下一业务 effect？
3. parent 是否读取 child registry / mutable cell presence 推导 lifecycle stage？
4. 是否存在 `Advance/Tick/Resume/Step` API family 由 caller 反复 drive？
5. normal path 是否是 CE，但 recovery 会跳入 child 内部 stage/continuation？
6. Semantic Vocabulary 展开后是否仍满足 CE + Vocabulary + bounded composition，直到纯
   decision / physical adapter？

命中 1–5 默认 REVISE。合法 physical presence 必须停留在 adapter/physical owner，向上
收敛成 typed capability/outcome/evidence；合法 domain result 可以由 parent 穷尽 `match`。

### 3.7 其它机制（非本包 owner）

- `scripts/checks/architecture.mjs`（ARCH-001 分层、fsproj、资源读取位置）→
  verification-system MECHANISM。
- `scripts/checks/test-boundary.mjs` → verification-system MECHANISM：`requirements/`
  scope 内测试禁止直接 import `dist/fable_modules/**`。
- `scripts/checks/g4r-freeze.mjs` → migration freeze ratchet，**不归本包**。

## 4. 依赖

无产品语义依赖（`requirements/INDEX.md` 骨架）。历史上 `structured-workflow →
causal-wait` hard edge 已删（Phase E）：CE builder 是实现耦合；event-driven wake /
deadline escape 都是消费关系，非定义前提。

## 5. 历史与弃权

### 5.1 EVIDENCE（WHY 考古，信息已收入 WHY.md/WHAT.md）

| 源 | 吸收位置 |
|---|---|
| 历史 change（rabbit，G4R-CE Vocabulary） | WHY.md §2.5/§3；WHAT 011/012/013；HOW §1/§3.2 |
| 历史 change（ce-temporal-ownership，时序所有权清算） | WHY.md §2.1/§2.2/§3；WHAT 009；HOW §2.2/§2.3 |
| 历史 change（fsharp-dsl-governance，mutable record 状态乘积） | WHY.md §2.3/§3；WHAT 005/008；HOW §3.1 |
| 历史 change（dsl-structured-program-gap，DSL 结构化程序缺口闭环） | WHY.md §2.4；WHAT 005；HOW §3.1（flight registry 单一物理来源） |
| 历史五层 docs（dsl-structured-program/flow/architecture/loop/execution） | WHAT.md 反向覆盖清单 + 各命题 |
| 历史 COVERAGE（flow/dsl/arch/execution/loop 小节） | WHAT.md 反向覆盖清单 |
| 历史 EVIDENCE §2 行 | README.md HOW 概览 |
| 历史 PROOF-MAP（dsl-ownership SPLIT、g4r-ce-vocabulary KEEP、g4r-freeze DELETE、domain/kernel/temporal/verify family） | HOW.md §4/§6 |

### 5.2 GARBAGE（弃权记录）

| 源 | 弃权理由 | 记录位置 |
|---|---|---|
| 历史 transcript（ChatGPT-时序控制流修复提案，4310 行 raw chat export） | **GARBAGE（transcript）**：ChatGPT 对话原始导出，非规范源。其中 2N Finality cohort、REVISE 立即短路、Blessed 后 rest-in-peace、Reviewer HostOwnedHidden、Join 中断仅 OperatorAbort\|DeadlineExpired 等决策的**规范结果**已落旧五层 docs（GLORY/EXEC-017/EXEC-020 等）并由对应 owner 拥有；transcript 本身不携带任何独立 normative 内容，不迁移为命题 | HOW.md §5.2；CHANGES-AUDIT.md 行 56 |
| 历史 transcript（refactor，1821 行 raw chat export） | **GARBAGE（transcript）**：按知识主权重新装箱的施工对话导出。其工程结果（god-module 拆分、domain.mjs family 化）已是当前仓库事实并分别归属各 semantic owner；kolmogorov-size.mjs advisory 于 2026-08-15 删除（行数不做机械检查）；transcript 不产生本包新命题 | HOW.md §5.2；CHANGES-AUDIT.md 行 57 |
| 历史 LOOP-001..008 | **不归本包**：degeneration-guard 单 owner；本包只提供 LOOP-006 桥接依赖的「无第二状态机 / 进程内局部事实」保证 | WHAT.md 反向覆盖清单 |
| 历史 EXEC-001..032 主体 | **不归本包**：delegation / process-execution / effect-accounting / work-record / managed-session-lifecycle / participant-horizon / time-capability 等各自 owner；本包只吸收 EXEC-020 控制面/数据面（WHAT 015） | WHAT.md 反向覆盖清单 |
| 历史 ARCH-002/003/004/006/007/010-017 | **不归本包**：host-boundary / prefix-stability / action-affordance / provider-projection / office-capability 等各自 owner | WHAT.md 反向覆盖清单 |
| `scripts/checks/dsl-ownership.mjs` 的 `program-counter` 词表 + `behaviour-bool` 名称正则、`g4r-ce-vocabulary` obsolete-controller absence、`g4r-freeze` | **migration ratchet（DELETE@cutover）**：旧 symbol absence 黑名单与计数基线已删除；新世界以 positive 结构门（state-product / mutable-record-field / second-runtime-protocol / registry-joint-branch）+ 本包 NEW 测试为正式证明面 | HOW.md §4；PROOF-MAP DELETE 清单 |

### 5.3 已实施的 clean break（不再回退）

- `TurnCompletionProgram.fs` / `FinalityController.fs` / `ReviewController.fs` /
  `ManagerLifecycleGate.fs` / `ReviewerGuardState.fs` / `HostReviewProgram.fs` 已删除；
  `g4r-ce-vocabulary --phase=hard` 证明生产树干净。
- `StudentRunCell` / `BloggerRuntimeState.Idle\|InFlight` 双写与影子状态已删除；
  物理 flight registry（`HasFlight` / `bloggerFlights`）是 busy 唯一来源。
- `Program.fs` / `TraceInterpreter.fs` / Command-Reply 总线 / Step AST 不得回到生产
  路径（FLOW 禁止回归清单）。

## 6. 遗留风险

- `state-product` 类型级自动组合计数仍是「结构门 + 人工证明」组合（dsl-structured-
  program-gap.md blocker 保留），不降低 WHAT 005。
- dsl-ownership 的永久结构门与本包测试现在承担 003/004/005/008 的机器证明；
  旧名称黑名单与 ratchet 计数基线已在 cutover 删除。
- `tests/unit/temporal/**`、`guide-contract`、`verify/**` 的 SPLIT@cutover 未执行
  （HOW.md §4），当前为共享套件。

## DEPENDS ON

无产品语义依赖（`requirements/INDEX.md` 依赖骨架为唯一来源）。历史上曾有一条
`structured-workflow → causal-wait` hard edge，Phase E 已审计删除：CE builder 是
implementation coupling，不是定义前提；event-driven wake 与 deadline escape 都是消费关系。

## 验证与测试落点

### 1. 命题 → 测试

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| STRUCTURED-WORKFLOW-001（业务流程由宿主语言结构直接表达） | `tests/direct-ce-contract.test.mjs`：`FLOW_001_direct_task_workflow_is_allowed`；`tests/workflow-surface.test.mjs`：`SW_001_workflow_entrypoints_are_the_exported_surface`；`tests/orchestrator-program.test.mjs`：`ORCHESTRATOR_PROGRAM_001`；REUSE `requirements/verification-system/tests/guide-contract.test.mjs`：`VERIFY_005_AgentProgram_publishes_its_flow_entrypoints`、`VERIFY_005_CompanionProgram_publishes_its_flow_entrypoints`、`VERIFY_005_OrchestratorProgram_publishes_exactly_one_entrypoint` | MOVE + NEW + REUSE | `node --test requirements/structured-workflow/tests/direct-ce-contract.test.mjs requirements/structured-workflow/tests/workflow-surface.test.mjs` |
| STRUCTURED-WORKFLOW-002（禁止第二业务运行时） | `tests/direct-ce-contract.test.mjs`：`FLOW_006_second_runtime_patterns_are_rejected`；`tests/reconcile-program.test.mjs`：`RECONCILE_PROGRAM_006`（Domain surface 无 Command/Reply/Trace AST 导出）；`tests/orchestrator-program.test.mjs`：`ORCHESTRATOR_PROGRAM_002/003/004`；REUSE `tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_negative_second-runtime-protocol_goes_red`、`DSL_OWNERSHIP_negative_business-interpreter_goes_red`、`DSL_OWNERSHIP_negative_flow-lift_goes_red`；REUSE `tests/g4r-ce-vocabulary.test.mjs`（CE vocabulary absence ratchet 机制，HOW §3.3）：`G4R_CE_S0_*`、`G4R_CE_S14_production_has_no_obsolete_controllers` | MOVE + REUSE | `node --test requirements/structured-workflow/tests/direct-ce-contract.test.mjs requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| STRUCTURED-WORKFLOW-003（状态标签只表示物理/领域真实事物；无持久程序计数器） | `tests/workflow-surface.test.mjs`：`SW_002_workflow_modules_export_no_program_counter_shaped_names`、`SW_003_domain_flow_and_outcome_types_are_domain_facts`；REUSE `tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_negative_program-counter_goes_red`、`DSL_OWNERSHIP_negative_program-counter-current-stage_goes_red`、`DSL_OWNERSHIP_negative_behaviour-bool_goes_red`、`DSL_OWNERSHIP_negative_bool-loop-agent_goes_red`、`DSL_OWNERSHIP_control_state_class_is_a_program_counter`、`DSL_OWNERSHIP_control_state_requires_structured_reason`、`DSL_OWNERSHIP_large_du_*`（大 DU 分类）、`DSL_OWNERSHIP_domain_pending_evidence_is_not_behaviour_bool`、`DSL_OWNERSHIP_verb_named_function_ending_Pending_is_not_behaviour_bool`、`DSL_OWNERSHIP_physical_pending_latch_and_estimate_fields_are_not_behaviour_bool`、`DSL_OWNERSHIP_field_named_HasPendingCompletion_still_fires_behaviour_bool`、`DSL_OWNERSHIP_comment_only_line_is_ignored`、`DSL_OWNERSHIP_scanFiles_aggregates_entries`、`DSL_OWNERSHIP_clean_source_stays_green` | NEW + REUSE | `node --test requirements/structured-workflow/tests/workflow-surface.test.mjs` |
| STRUCTURED-WORKFLOW-004（ARCH-008 禁止词不作程序计数器） | REUSE `tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_business_stage_bool_suffix_still_fires_behaviour_bool`（`*Stage/*Phase/*Running/*Spent` 后缀判红）、`DSL_OWNERSHIP_session_mutable_requires_physical_annotation`（business token 表含 State/Phase/Stage/Mode/RunState/Handoff）、`DSL_OWNERSHIP_pascal_member_Pending_still_fires_behaviour_bool` | REUSE | `node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs` |
| STRUCTURED-WORKFLOW-005（组合状态必须可证明合法） | REUSE `tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_renamed_record_state_axes_are_reported`、`DSL_OWNERSHIP_mutable_record_program_counter_fires_state_product`、`DSL_OWNERSHIP_ref_record_program_counter_fires_mutable_record_field`、`DSL_OWNERSHIP_joint_registry_match_with_effect_fires_registry_joint_branch`、`DSL_OWNERSHIP_physical_state_record_mutable_fields_are_allowed`、`DSL_OWNERSHIP_domain_state_combination_is_explicitly_allowed`、`DSL_OWNERSHIP_physical_state_combination_is_explicitly_allowed`（fixtures `state-axes-{illegal,domain,physical,multiline}.fs`、`mutable-record-program-counter.fs`、`ref-record-program-counter.fs`、`registry-joint-branch.fs`） | REUSE | `node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs` |
| STRUCTURED-WORKFLOW-006（单一真理源：同构 DU 单一定义） | REUSE `tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_negative_dup-cases_goes_red`、`DSL_OWNERSHIP_cross_file_duplicate_case_set_is_violation`、`DSL_OWNERSHIP_single_file_duplicate_case_set_is_not_cross_file`、`DSL_OWNERSHIP_cross_file_duplicate_case_set_exemption_stays_clean` | REUSE | `node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs` |
| STRUCTURED-WORKFLOW-007（纯决策与效果分层） | `tests/reconcile-program.test.mjs`：`RECONCILE_PROGRAM_001/003/004`（Domain 纯决策面：isTerminalOutcome / decideStep / publishDecision）；REUSE `tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_negative_infrastructure-leak_goes_red`、`DSL_OWNERSHIP_qualified_infrastructure_reference_is_leak_outside_infra`、`DSL_OWNERSHIP_qualified_process_reference_is_leak_outside_infra`、`DSL_OWNERSHIP_qualified_process_reference_is_clean_inside_infra`、`DSL_OWNERSHIP_namespace_OpenCode_declaration_is_not_infrastructure_leak`、`DSL_OWNERSHIP_namespace_Process_declaration_is_not_infrastructure_leak`、`DSL_OWNERSHIP_host_boundary_open_is_not_gate_red`（Host 边界白名单机制）；REUSE `requirements/verification-system/tests/guide-contract.test.mjs`：`VERIFY_005_Domain_ReconcileProgram_publishes_pure_decisions` | MOVE + REUSE | `node --test requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| STRUCTURED-WORKFLOW-008（mutable/ref 只承载物理资源或局部纯实现） | REUSE `tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_negative_mutable_goes_red`、`DSL_OWNERSHIP_negative_bool-loop-process_goes_red`、`DSL_OWNERSHIP_mutable_requires_dsl_mutable_declaration`、`DSL_OWNERSHIP_unknown_mutable_category_is_rejected`、`DSL_OWNERSHIP_mutable_record_program_counter_fires_mutable_record_field`、`DSL_OWNERSHIP_physical_state_record_mutable_fields_are_allowed`、`DSL_OWNERSHIP_infrastructure_declared_mutable_is_accepted`、`DSL_OWNERSHIP_journal_declared_mutable_is_accepted`、`DSL_OWNERSHIP_infrastructure_bare_mutable_still_fires`、`DSL_OWNERSHIP_journal_bare_mutable_still_fires` | REUSE | `node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs` |
| STRUCTURED-WORKFLOW-009（恢复重入普通流程） | `tests/recovery-reentry.test.mjs`：`SW_009_reconcile_domain_is_observation_stabilization_not_a_program`、`SW_009_recovery_surface_drives_ordinary_workflow_entrypoints`；`tests/reconcile-program.test.mjs`：`RECONCILE_PROGRAM_005/007`（TurnUnknown 结构性降级，业务边界稳定）；REUSE `requirements/crash-reconciliation/tests/session-recovery-combine.test.mjs`（crash-reconciliation 交叉：恢复组合） | NEW + MOVE + REUSE | `node --test requirements/structured-workflow/tests/recovery-reentry.test.mjs requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| STRUCTURED-WORKFLOW-010（有界循环与有界扇出） | `tests/parallel.test.mjs`：12 个 `ARCH_009_*` 锚点（结果序按输入下标 / 并发上限 / 空输入短路 / 取消 / 拒绝 / 非正 max 拒绝）；REUSE `requirements/verification-system/tests/guide-contract.test.mjs`：`VERIFY_005_the_Parallel_kernel_publishes_only_bounded_parallelism`（无 unbounded `Parallel.map*` 旁路） | MOVE + REUSE | `node --test requirements/structured-workflow/tests/parallel.test.mjs` |
| STRUCTURED-WORKFLOW-011（Semantic Vocabulary 是领域事实词汇） | `tests/semantic-vocabulary.test.mjs`：`SW_011_named_vocabulary_surface_exists_in_Application`、`SW_011_vocabulary_names_declare_business_promises_not_implementation_actions` | NEW | `node --test requirements/structured-workflow/tests/semantic-vocabulary.test.mjs` |
| STRUCTURED-WORKFLOW-012（Semantic Compression 必须有 proof） | `tests/semantic-vocabulary.test.mjs`：`SW_011_named_vocabulary_surface_exists_in_Application`（被压缩 Vocabulary 的存在面）、`WHAT[STRUCTURED-WORKFLOW-012] every obligation-table vocabulary is a real production definition`（HOW §3.3 义务表登记 ↔ 生产定义一一对应）；proof 义务表见 HOW.md §3.3（每个高阶 Vocabulary 必须有 temporal/behavioral proof；正交组合人工证明见 §3.3.1） | NEW + HOW | `node --test requirements/structured-workflow/tests/semantic-vocabulary.test.mjs` |
| STRUCTURED-WORKFLOW-013（Decorator 边界：transparent vs semantic） | `tests/semantic-vocabulary.test.mjs`：`SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary` | NEW | `node --test requirements/structured-workflow/tests/semantic-vocabulary.test.mjs` |
| STRUCTURED-WORKFLOW-014（流程正确性由可观察效果证明） | REUSE `requirements/verification-system/tests/guide-contract.test.mjs`：`VERIFY_008_every_emitted_module_actually_loads`（导出面即契约）；REUSE `tests/unit/temporal/**`（finality-cohort-law / fallback-aabb-confluence / manager-unhappy-exactly-once / join-guard-wakeup / orchestrator-conflict-confluence / until-signal-or-deadline：可观察效果轨迹证明，无解释器节点指针）；REUSE `tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_threshold_freeze_semantics` | REUSE | `node --test requirements/verification-system/tests/guide-contract.test.mjs` |
| STRUCTURED-WORKFLOW-015（取消是控制面，不是业务数据） | `tests/reconcile-program.test.mjs`：`WHAT[STRUCTURED-WORKFLOW-015] operator abort is a control-plane wake, never a business outcome`（AbortWake ∈ ReconcileWake 控制面、∉ TurnOutcome 业务面）；REUSE `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs`：`P0_RECOVERY_JOIN_001_aborted_alone_is_not_terminal`、`P0_RECOVERY_JOIN_001_joinable_completion_has_no_fromAborted_export`（effect-accounting 拥有 outcome 代数；本命题钉控制面/数据面分离） | NEW + REUSE | `node --test requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| STRUCTURED-WORKFLOW-016（控制决策不得形成 lexical pyramid） | `tests/fsharp-control-pyramid.test.mjs`：nested match RED、match→if→try RED、flat/tuple/if-elif GREEN、comment/string lexical shielding、absolute-zero gate、production zero exact、单次 repair manual + 教程篇幅下限；`tests/error-handling-vocabulary.test.mjs`：FsToolkit Fable Result vocabulary、项目自有 TaskResult CE + TaskValue/TaskResult/TaskResultList，且生产树禁止引用 FsToolkit 的 .NET-only `Task.map` / `List.traverseTaskResultM`；WriterStreamSync 代表性糖化 | NEW | `node --test requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs requirements/structured-workflow/tests/error-handling-vocabulary.test.mjs` |
| STRUCTURED-WORKFLOW-017（业务 workflow 组合具有结构闭包） | `tests/direct-ce-contract.test.mjs`：`FLOW_017_composition_keeps_domain_results_and_rejects_child_program_counters`（domain outcome seam GREEN / child CurrentStage seam RED）；REUSE `tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_joint_registry_match_with_effect_fires_registry_joint_branch`（presence 不得成为 control seam）；REUSE `tests/recovery-reentry.test.mjs`（recovery 从普通 semantic entry 重入）；HOW.md §3.6 cross-module CE seam review（Advance/Tick/Resume/Step、control token、registry presence 人工 census） | NEW + REUSE + HOW | `node --test requirements/structured-workflow/tests/direct-ce-contract.test.mjs requirements/structured-workflow/tests/dsl-ownership.test.mjs requirements/structured-workflow/tests/recovery-reentry.test.mjs` |

### 2. 本包拥有的测试文件（全部单跑绿）

| 文件 | 来源 | 状态 |
|---|---|---|
| `tests/direct-ce-contract.test.mjs` | MOVE `requirements/structured-workflow/tests/direct-ce-contract.test.mjs` | 3 pass（含 STRUCTURED-WORKFLOW-017 composition-closure contract） |
| `tests/parallel.test.mjs` | MOVE `requirements/structured-workflow/tests/parallel.test.mjs` | 已跑绿（12 pass） |
| `tests/reconcile-program.test.mjs` | MOVE `requirements/structured-workflow/tests/reconcile-program.test.mjs` | 已跑绿（7 pass；含 STRUCTURED-WORKFLOW-015 contract test） |
| `tests/workflow-surface.test.mjs` | NEW | 已跑绿（3 pass） |
| `tests/recovery-reentry.test.mjs` | NEW | 已跑绿（2 pass） |
| `tests/semantic-vocabulary.test.mjs` | NEW | 已跑绿（4 pass；含 STRUCTURED-WORKFLOW-012 contract test） |
| `tests/fsharp-control-pyramid.test.mjs` | NEW | 已跑绿；production nested decisions=0，永久门禁无 baseline |
| `tests/error-handling-vocabulary.test.mjs` | NEW | 已跑绿（4 pass；Fable build 同步通过） |
| `tests/g4r-ce-vocabulary.test.mjs` | MOVE（CE vocabulary absence ratchet 机制，HOW §3.3） | 已跑绿（11 pass；S14 已拆为 obsolete/raw-time 两 test，raw-time 生产事实归 TIME-004） |
| `tests/dsl-ownership.test.mjs` | MOVE（positive 结构门） | 已跑绿（54 pass；NEGATIVES 循环已按命题拆分为静态 test） |
| `tests/orchestrator-program.test.mjs` | MOVE（cutover Wave 2a） | 已跑绿（4 pass） |

### 3. 单跑命令

```text
node --test requirements/structured-workflow/tests/direct-ce-contract.test.mjs
node --test requirements/structured-workflow/tests/parallel.test.mjs
node --test requirements/structured-workflow/tests/reconcile-program.test.mjs
node --test requirements/structured-workflow/tests/workflow-surface.test.mjs
node --test requirements/structured-workflow/tests/recovery-reentry.test.mjs
node --test requirements/structured-workflow/tests/semantic-vocabulary.test.mjs
node --test requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs
node --test requirements/structured-workflow/tests/error-handling-vocabulary.test.mjs
node --test requirements/structured-workflow/tests/g4r-ce-vocabulary.test.mjs
node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs
node --test requirements/structured-workflow/tests/orchestrator-program.test.mjs
```

### 4. REUSE 落点（留在原处，SPLIT@cutover）

| 现有测试 | 本包锚点 | cutover 计划 |
|---|---|---|
| `requirements/structured-workflow/tests/dsl-ownership.test.mjs`（726 行） | 全部 `DSL_OWNERSHIP_*` 锚点（第二运行时、program-counter、behaviour-bool、mutable、state-product、dup-cases、infrastructure-leak、bool-loop、registry-joint-branch、ControlState reason、大 DU 分类、Host 边界白名单） | SPLIT@cutover：positive 结构门锚点归本包；`program-counter` 词表与 `behaviour-bool` 名称正则的 **legacy symbol blacklist 部分 DELETE**（migration ratchet，见 PROOF-MAP dsl-ownership SPLIT 行） |
| `requirements/structured-workflow/tests/g4r-ce-vocabulary.test.mjs`（161 行） | `G4R_CE_S0_documents_obsolete_controller_paths`、`G4R_CE_S14_production_is_clean_in_hard_phase`（CE vocabulary absence ratchet = 本包）；`G4R_CE_S0_raw_time_*`（raw-time 扫描 = time-capability 交叉） | SPLIT@cutover：obsolete-controller absence ratchet 基线稳定后弱化/删除；raw-time 部分移交 time-capability |
| `requirements/verification-system/tests/guide-contract.test.mjs`（顶层） | `VERIFY_005_*`（直接 CE 入口导出面）、`VERIFY_005_the_Parallel_kernel_publishes_only_bounded_parallelism`、`VERIFY_005_Domain_ReconcileProgram_publishes_pure_decisions`、`VERIFY_005_the_outcome_kernel_publishes_the_two_commit_results`、`VERIFY_008_*` | SPLIT@cutover：verification-system 拥有 harness；各语义断言按导出面归属各包 |
| `requirements/crash-reconciliation/tests/session-recovery-combine.test.mjs` | 恢复组合（permit → 普通流程） | SPLIT@cutover：crash-reconciliation 拥有恢复协议；「无执行位置恢复」半边由本包 `tests/recovery-reentry.test.mjs` 承担 |
| `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs` | `P0_RECOVERY_JOIN_001_*`（aborted 非终态） | SPLIT@cutover：effect-accounting 拥有 outcome 代数；本命题只钉控制面/数据面分离 |
| `tests/unit/temporal/**`（6 文件） | finality-cohort-law / fallback-aabb-confluence / manager-unhappy-exactly-once / join-guard-wakeup / orchestrator-conflict-confluence / until-signal-or-deadline（可观察效果轨迹证明姿态） | SPLIT@cutover：time-capability（fake clock/virtual timer）+ causal-wait（until-signal-or-deadline）各取所属断言；本包保留「以可观察效果证明流程」的证明姿态 |
| `tests/unit/enforcer/blogger-convergence-gaps.test.mjs` | C0 断言：`HasFlight` 唯一 busy、无 shadow state API（DSL-005/009 人工 proof 的机器下限） | SPLIT@cutover：behavior-diagnosis 保留 enforcer 面；single-flight 物理事实断言归 structured-workflow（或由本包 NEW 测试接替） |

### 5. semantic anchor id

`scripts/checks/semantic-anchors.mjs` 中**没有**直接归本包的 semantic ID：
锚点目录只声明 `cognitive-environment` / `office-capability` / `action-affordance` /
`epistemic-reasoning` / `review-judgement` 五类 owner，本包语义由 F# 类型 + 上述
positive 结构测试承担。若 cutover 后需要散文 canary，建议新增 `STRUCTURED_WORKFLOW_*`
锚点并声明 owner 为本包。

### 6. cutover 待办

- [ ] 删除 `dsl-ownership` 的 legacy symbol blacklist 部分（`program-counter` 词表、
  `behaviour-bool` 名称正则）与其 ratchet 基线（PROOF-MAP DELETE 清单）；positive
  结构门保留为 `--threshold=0`。
- [ ] `g4r-ce-vocabulary` obsolete-controller absence ratchet 基线稳定后弱化；raw-time
  扫描移交 `time-capability`。
- [ ] `g4r-freeze`（migration freeze ratchet）不归本包，由 lead 按 PROOF-MAP DELETE 处理。
- [ ] `tests/unit/verify/*`、`guide-contract`、`temporal/**` 的 SPLIT@cutover 按 §4 表执行。
