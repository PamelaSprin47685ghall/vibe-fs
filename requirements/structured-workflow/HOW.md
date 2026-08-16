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
| `src/Wanxiangshu/Application/Review/ReviewerWorkflow.fs` | `observe` | Reviewer turn 唯一 continuation writer：`ReviewerEvidence.classifyNeed` 分派 → 具名 Vocabulary 发送承诺，无存储 State/Stage 计数器 |
| `src/Wanxiangshu/Composition/Turn/Workflow.fs` | `observe` | 极薄 router：按 bounded context 委派（Manager/Reviewer/Ordinary），不计算 pending/shouldContinue/phase |

Manager 词汇：`ManagerBackground.ensureSettled`、
`ManagerIdle.encourageLabor`、`ManagerJobHandoff.completeIfTransferred`、
`ManagerFinality.admitLabor` / `classifyEnding`。
Reviewer 词汇：`ReviewerContinuation.ensurePerfectConfirmed` / `ensureVerdictSubmitted`、
`ReviewerEvidence.classifyNeed`。
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
| `infrastructure-leak` | 007 | `open Wanxiangshu.Infrastructure|OpenCode|Process` 或 FQN 引用；Host 边界 basename 白名单豁免（DSL-010） |
| 大 DU 分类 + `DSL-control-state-reason` | 003/005 | ≥10 case 必须 `DSL-class`；ControlState 必须机器可校验理由（ce-equivalent=none + blockers 覆盖 function-call/match!/return!/resource-scope/waiter/bounded-recursion） |

fixtures：`tests/unit/verify/fixtures/{state-axes-illegal,state-axes-domain,state-axes-physical,mutable-record-program-counter,ref-record-program-counter,registry-joint-branch}.fs`。

### 3.2 `scripts/checks/dsl-ownership-ratchet.mjs`（migration）

per-file/per-gate 违规计数基线（`dsl-ownership-ratchet-baseline.json`）。**这是
legacy symbol blacklist 的迁移 ratchet**（PROOF-MAP：dsl-ownership SPLIT 的 DELETE
半边），cutover 后随 legacy blacklist 一起删除。

### 3.3 `scripts/checks/g4r-ce-vocabulary.mjs`（CE vocabulary absence ratchet）

- `OBSOLETE_CONTROLLER_PATHS`：`TurnCompletionProgram.fs`、`FinalityController.fs`、
  `ReviewController.fs`、`ManagerLifecycleGate.fs`、`ReviewerGuardState.fs`、
  `HostReviewProgram.fs` 必须 absent（S14 hard phase，当前生产树已全删）。
- `RAW_TIME_TOKENS` 扫描 Domain/Application/Session 的 `DateTimeOffset.UtcNow` /
  `Date.now` / `setTimeout` / `timerTask` 等——**raw-time 禁止语义归
  `time-capability`**，本门只是共享 scanner（MECHANISM 交叉）。

### 3.3.1 `scripts/checks/fsharp-control-pyramid.mjs`（lexical decision ratchet）

STRUCTURED-WORKFLOW-016 的 shape gate。scanner 以 F# offside/缩进结构识别
`match` / `match!` / `function` / 多行 `if` / `try` / `while` / `for`；同级顺序 decision
不算 nesting，`if/elif/else` 算同一层。扫描前会屏蔽行注释、可嵌套 block comment、普通
字符串与 triple-quoted string，避免教程/示例文本制造命中。

当前生产树 bootstrap：301 个文件、2166 个 depth≥2 decision。直接 hard-zero 会把任何
小改动淹没在历史噪音里，因此 `scripts/check.mjs` 使用
`fsharp-control-pyramid-baseline.json` 做 **per-file ratchet**：历史计数只冻结，新文件从
0 开始；任一文件新增一个 nested decision 立即 RED。`--show-all` 显示全部债务；
`--snapshot` 只打印当前 JSON，不写 baseline；baseline review 只允许数字下降。

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

`--show-all` 是清债视图；正式 CI 只报告相对 baseline 的新增文件/新增计数，避免 2166 个
历史 todo 每次刷屏。没有 suppression / allowlist；若某命中不是机械 bind，也必须人工
重审并命名 control-flow boundary。

### 3.4 高阶 Vocabulary 证明义务（DSL-014）

每个改变 trace 的压缩 Vocabulary 必须有 temporal/behavioral proof。当前义务表：

| Vocabulary | 必须证明 |
|---|---|
| `ManagerBackground.ensureSettled` | completion / join / wake permutations |
| `ManagerIdle.encourageLabor` | independent idle occasions / stale permit |
| `ReviewerContinuation.ensurePerfectConfirmed` | first PERFECT / challenge / second PERFECT |
| `ReviewBarrierWorkflow.reverify` | verdict absence / revision / confirmation |
| `FallbackLedger.recordConfirmedFailure` | dedupe / AABB / exhaustion |
| `ProviderRecoveryWorkflow.continueAfterConfirmedFailure` | failure → material → continuation |
| `FinalityCohort.reviewUntilFirstRevisionOrAllConfirmed` | cohort interleavings |
| `SessionRecoveryWorkflow.recoverFamilyDirect` | closure orders / missing evidence |
| `Orchestrator.publishEventually` | target movement recursion |

新增高阶 Vocabulary 必须追加本表一行并挂可观察效果测试。

### 3.4.1 正交组合证明（DSL-005，人工）

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
- `scripts/checks/dsl-ownership-ratchet.mjs`（基线防回归）
- `requirements/context-compression/tests/blogger-convergence-gaps.test.mjs`（`HasFlight` 唯一 busy、
  无 shadow state API）
- `requirements/structured-workflow/tests/dsl-ownership.test.mjs`（含 `state-axes-{illegal,domain,physical}.fs`
  与 `ControlState` reason fixtures）与 `dsl-ownership-ratchet.test.mjs`

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

举一反三（同仓联合 presence / park→bind 对照）：`BloggerRuntime.decideMaterial`、
`PluginRuntimeScope` parked∩pendingOffer、`Reconciler` active∩queued、`BloggerCrashRecovery`
多 presence、`ReviewSeal` PendingReviewSeals park→bind 均属**物理资源路由 / 投递握手 /
调度 latch**，不是用 mailbox presence 推导 lifecycle stage 的 PC；保持 ACCEPT-as-physical，
不套用 CE collapse。ReviewSeal 消费面是 VerdictTool fail-closed resolve，不是 HandleTurn 上的
nudge-vs-complete 分支。

### 3.5 Vocabulary 命名 review（DSL-013 五问）

每个新增 Application public function（尤其拟作 Semantic Vocabulary 暴露的入口）必须
在 review / Change proof 中回答：

1. 它的名字声明了什么业务承诺？
2. 它隐藏了哪些时序？
3. 哪个 temporal / behavioral proof 证明这些时序？
4. 它改变 trace 集，还是 transparent decorator？
5. crash 后从什么 durable evidence 重入？

回答不出则 REVISE。

### 3.6 其它机制（非本包 owner）

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
| 历史 change（rabbit，G4R-CE Vocabulary） | WHY.md §2.5/§3；WHAT 011/012/013；HOW §1/§3.3 |
| 历史 change（ce-temporal-ownership，时序所有权清算） | WHY.md §2.1/§2.2/§3；WHAT 009；HOW §2.2/§2.3 |
| 历史 change（fsharp-dsl-governance，mutable record 状态乘积） | WHY.md §2.3/§3；WHAT 005/008；HOW §3.1 |
| 历史 change（dsl-structured-program-gap，DSL 结构化程序缺口闭环） | WHY.md §2.4；WHAT 005；HOW §3.1（flight registry 单一物理来源） |
| 历史五层 docs（dsl-structured-program/flow/architecture/loop/execution） | WHAT.md 反向覆盖清单 + 各命题 |
| 历史 COVERAGE（flow/dsl/arch/execution/loop 小节） | WHAT.md 反向覆盖清单 |
| 历史 EVIDENCE §2 行 | README.md HOW 概览 |
| 历史 PROOF-MAP（dsl-ownership SPLIT、g4r-ce-vocabulary KEEP、g4r-freeze DELETE、domain/kernel/temporal/verify family） | PROOF.md §4/§6 |

### 5.2 GARBAGE（弃权记录）

| 源 | 弃权理由 | 记录位置 |
|---|---|---|
| 历史 transcript（ChatGPT-时序控制流修复提案，4310 行 raw chat export） | **GARBAGE（transcript）**：ChatGPT 对话原始导出，非规范源。其中 2N Finality cohort、REVISE 立即短路、Blessed 后 rest-in-peace、Reviewer HostOwnedHidden、Join 中断仅 OperatorAbort\|DeadlineExpired 等决策的**规范结果**已落旧五层 docs（GLORY/EXEC-017/EXEC-020 等）并由对应 owner 拥有；transcript 本身不携带任何独立 normative 内容，不迁移为命题 | HOW.md §5.2；CHANGES-AUDIT.md 行 56 |
| 历史 transcript（refactor，1821 行 raw chat export） | **GARBAGE（transcript）**：按知识主权重新装箱的施工对话导出。其工程结果（god-module 拆分、domain.mjs family 化）已是当前仓库事实并分别归属各 semantic owner；kolmogorov-size.mjs advisory 于 2026-08-15 删除（行数不做机械检查）；transcript 不产生本包新命题 | HOW.md §5.2；CHANGES-AUDIT.md 行 57 |
| 历史 LOOP-001..008 | **不归本包**：degeneration-guard 单 owner；本包只提供 LOOP-006 桥接依赖的「无第二状态机 / 进程内局部事实」保证 | WHAT.md 反向覆盖清单 |
| 历史 EXEC-001..032 主体 | **不归本包**：delegation / process-execution / effect-accounting / work-record / managed-session-lifecycle / participant-horizon / time-capability 等各自 owner；本包只吸收 EXEC-020 控制面/数据面（WHAT 015） | WHAT.md 反向覆盖清单 |
| 历史 ARCH-002/003/004/006/007/010-017 | **不归本包**：host-boundary / prefix-stability / action-affordance / provider-projection / office-capability 等各自 owner | WHAT.md 反向覆盖清单 |
| `scripts/checks/dsl-ownership.mjs` 的 `program-counter` 词表 + `behaviour-bool` 名称正则、`dsl-ownership-ratchet` 基线、`g4r-ce-vocabulary` obsolete-controller absence、`g4r-freeze` | **migration ratchet（DELETE@cutover）**：旧 symbol absence 黑名单只能防已经想起来的坏名字；新世界以 positive 结构门（state-product / mutable-record-field / second-runtime-protocol / registry-joint-branch）+ 本包 NEW 测试为正式证明面 | PROOF.md §4/§6；PROOF-MAP DELETE 清单 |

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
- dsl-ownership 的 legacy 名称门与 ratchet 在 cutover 前仍承担部分 003/004 证明；
  删除前需确保 positive 结构门 + 本包 NEW 测试覆盖等价面（见 PROOF.md §6 待办）。
- `tests/unit/temporal/**`、`guide-contract`、`verify/**` 的 SPLIT@cutover 未执行
  （PROOF.md §4），当前为共享套件。
