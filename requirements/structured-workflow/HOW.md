# structured-workflow — HOW（实现模型与约束）

> 本文件**非 normative**：只解释实现模型、静态门禁机制与历史考古。唯一 normative
> 合同是 WHAT.md。命名/行数/文件布局可整体重写，只要 WHAT 命题不变。

## 1. 四层实现模型（rabbit.md 目标架构）

生产代码分成四种东西：

```text
Business CE          讲故事（Application workflow 入口与有界递归）
Semantic Vocabulary  给复杂时序一个领域名字与 law（DSL-013/014）
Port Decorator       给一次能力逐层增加 observation / normalization / physical policy（DSL-015）
Physical Adapter     真的碰 OpenCode / Git / process / timer（Infrastructure / Process）
```

原则：**CE 负责故事；Vocabulary 负责定理；Decorator 负责能力；Port 负责物理。**
不发明第二套 workflow framework，不造 AST / interpreter / `ReliableFlowBuilder` 黑盒，
不把生产程序重新压成几十个 `Decision` case。

## 2. 模块地图（当前实现）

### 2.1 Kernel 类型（直接 CE 程序的领域事实）

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Kernel/DomainFlow.fs` | `AgentError`（HostFailure / SessionDead / InvalidFork / ParentCancelled）、`CompanionError`、`AgentContext { SessionId; AgentName }`、`CompanionContext { SessionId }` —— 领域错误/上下文词汇，**不是 Flow AST** |
| `src/Wanxiangshu/Kernel/Outcome.fs` | `AgentRunResult`（SessionId / AuthorityRootUserMessageId / ProviderRun / Role / Directory option / TerminalText / TurnFormalText + `IsValid`，EXEC-006）；`SendOutcome`（AdmittedWithReceipt / AdmittedWithPhysicalMessage / Retryable / AcceptanceUnknown / Fatal，PROMPT-005）；`SessionError`（NoProgress / SessionCancelled / AutoRecoveryExhausted / ReviewExhausted / PromptUncertain / ProjectionBroken / InboxFull / Protocol）；`CommitResult<'e>`（Committed / CommitUnknown，PERSIST-002） |

`Kernel/Parallel.fs` 是唯一业务并发原语 `mapBounded`（ARCH-009，见 WHAT 010）。

### 2.2 Application 直接 CE（故事层）

| 文件 | 导出 | 角色 |
|---|---|---|
| `src/Wanxiangshu/Application/Manager/ManagerWorkflow.fs` | `observe`、`observeIdle` | Manager 终态业务故事：handoff → background → activation → idle labor，全部 CE 顺序表达 |
| `src/Wanxiangshu/Application/Review/ReviewerWorkflow.fs` | `observe` | Reviewer turn 唯一 continuation writer：`ReviewerEvidence.classifyNeed` 分派 → 具名 Vocabulary 发送承诺，无存储 State/Stage 计数器 |
| `src/Wanxiangshu/Application/Reconciliation/TurnWorkflow.fs` | `observe` | 极薄 router：按 bounded context 委派（Manager/Reviewer/Ordinary），不计算 pending/shouldContinue/phase |

Manager 词汇：`ManagerBackground.ensureSettled`、`ManagerActivation.ensureAccepted`、
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

### 3.4 高阶 Vocabulary 证明义务（DSL-014）

每个改变 trace 的压缩 Vocabulary 必须有 temporal/behavioral proof。当前义务表
（源自 `archive/docs/proof/dsl-structured-program.md`）：

| Vocabulary | 必须证明 |
|---|---|
| `ManagerActivation.ensureAccepted` | exactly-once activation traces |
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
- `scripts/checks/kolmogorov-size.mjs` → verification-system MECHANISM（advisory，
  无 semantic owner）。
- `scripts/checks/test-boundary.mjs` → verification-system MECHANISM：`requirements/`
  scope 内测试禁止直接 import `dist/fable_modules/**`。
- `scripts/checks/g4r-freeze.mjs` → migration freeze ratchet，**不归本包**。

## 4. 依赖

无产品语义依赖（`archive/requirements-design/INDEX.md`）。历史上 `structured-workflow →
causal-wait` hard edge 已删（Phase E）：CE builder 是实现耦合；event-driven wake /
deadline escape 都是消费关系，非定义前提。

## 5. 历史与弃权

### 5.1 EVIDENCE（WHY 考古，信息已收入 WHY.md/WHAT.md）

| 源 | 吸收位置 |
|---|---|
| `archive/changes/completed/rabbit.md`（G4R-CE Vocabulary） | WHY.md §2.5/§3；WHAT 011/012/013；HOW §1/§3.3 |
| `archive/changes/completed/ce-temporal-ownership.md`（时序所有权清算） | WHY.md §2.1/§2.2/§3；WHAT 009；HOW §2.2/§2.3 |
| `archive/changes/completed/fsharp-dsl-governance.md`（mutable record 状态乘积） | WHY.md §2.3/§3；WHAT 005/008；HOW §3.1 |
| `archive/changes/completed/dsl-structured-program-gap.md`（DSL 结构化程序缺口闭环） | WHY.md §2.4；WHAT 005；HOW §3.1（flight registry 单一物理来源） |
| `archive/docs/{why,what,shape,how,proof}/{dsl-structured-program,flow,architecture,loop,execution}.md` | WHAT.md 反向覆盖清单 + 各命题 |
| `archive/requirements-design/COVERAGE.md`（flow/dsl/arch/execution/loop 小节） | WHAT.md 反向覆盖清单 |
| `archive/requirements-design/EVIDENCE.md` §2 行 | README.md HOW 概览 |
| `archive/requirements-design/PROOF-MAP.md`（dsl-ownership SPLIT、g4r-ce-vocabulary KEEP、g4r-freeze DELETE、domain/kernel/temporal/verify family） | PROOF.md §4/§6 |

### 5.2 GARBAGE（弃权记录）

| 源 | 弃权理由 | 记录位置 |
|---|---|---|
| `archive/changes/completed/ChatGPT-时序控制流修复提案.md`（4310 行 raw chat export） | **GARBAGE（transcript）**：ChatGPT 对话原始导出，非规范源。其中 2N Finality cohort、REVISE 立即短路、Blessed 后 rest-in-peace、Reviewer HostOwnedHidden、Join 中断仅 OperatorAbort\|DeadlineExpired 等决策的**规范结果**已落 `archive/docs/`（GLORY/EXEC-017/EXEC-020 等）并由对应 owner 拥有；transcript 本身不携带任何独立 normative 内容，不迁移为命题 | HOW.md §5.2；CHANGES-AUDIT.md 行 56 |
| `archive/changes/completed/refactor.md`（1821 行 raw chat export） | **GARBAGE（transcript）**：按知识主权重新装箱的施工对话导出。其工程结果（kolmogorov-size.mjs ratchet、god-module 拆分、domain.mjs family 化）已是当前仓库事实并分别归属 verification-system MECHANISM / 各 semantic owner；transcript 不产生本包新命题 | HOW.md §5.2；CHANGES-AUDIT.md 行 57 |
| `archive/docs/what/loop.md` LOOP-001..008 | **不归本包**：degeneration-guard 单 owner；本包只提供 LOOP-006 桥接依赖的「无第二状态机 / 进程内局部事实」保证 | WHAT.md 反向覆盖清单 |
| `archive/docs/what/execution.md` EXEC-001..032 主体 | **不归本包**：delegation / process-execution / effect-accounting / work-record / managed-session-lifecycle / participant-horizon / time-capability 等各自 owner；本包只吸收 EXEC-020 控制面/数据面（WHAT 015） | WHAT.md 反向覆盖清单 |
| `archive/docs/what/architecture.md` ARCH-002/003/004/006/007/010-017 | **不归本包**：host-boundary / prefix-stability / action-affordance / provider-projection / office-capability 等各自 owner | WHAT.md 反向覆盖清单 |
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
