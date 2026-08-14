# structured-workflow — 业务流程由宿主语言结构直接表达

> 业务流程应该由宿主语言的控制结构直接表达（`task { }` / `let!` / `match!` / `return!` /
> 有界递归），**不能在领域层再造第二程序计数器或第二 runtime**。状态标签只表示物理/领域
> 真实事物；「程序下一步走到哪」永远由调用栈回答，不由可存储字段回答。

## 一句话 WHY

**业务控制流若被编码成 Stage/Phase/Program AST，会在宿主语言之外再造第二 runtime，
使恢复、测试与非法状态同时膨胀。**

F# 调用栈已经是流程栈：`let!` 是等待，`match` 是分支，`return!` 是继续，`try/finally`
是资源作用域。把「下一步去哪」重新编码为字段，等于在业务层重建一个手写运行时——恢复
变成「恢复协程指针」（不可序列化、假装透明续跑）、测试变成「断言枚举序数」、类型系统
不再拦截非法态反而帮它们合法化。

## WHAT 概览（15 条命题，见 WHAT.md）

| 组 | 命题 | 一句话 |
|---|---|---|
| 直接表达 | STRUCTURED-WORKFLOW-001/002 | 流程只用宿主语言结构；禁止 AST+Interpreter / Command-Reply 总线 / Step AST 第二运行时 |
| 状态真相 | STRUCTURED-WORKFLOW-003/004/005/006 | 状态标签只表示物理/领域事实；禁程序计数器与 ARCH-008 禁止词；组合状态须可证明合法；同构 DU 单一定义 |
| 分层 | STRUCTURED-WORKFLOW-007/008 | 纯决策与效果壳分层；mutable/ref 只承载物理资源或局部纯实现 |
| 恢复 | STRUCTURED-WORKFLOW-009 | 恢复 = Journal fold → 事实 → 重入普通 workflow，不恢复执行位置 |
| 有界 | STRUCTURED-WORKFLOW-010 | 循环与扇出必须有界（`mapBounded` 唯一并发原语） |
| 词汇 | STRUCTURED-WORKFLOW-011/012/013 | Semantic Vocabulary 是领域事实词汇；压缩必须有 proof；decorator 必须具名 |
| 验证 | STRUCTURED-WORKFLOW-014/015 | 流程正确性由可观察效果证明；取消是控制面，不是业务数据 |

## HOW 概览（见 HOW.md）

```text
Business CE          讲故事（Application workflow 入口与有界递归）
Semantic Vocabulary  给复杂时序一个领域名字与 law（DSL-013/014）
Port Decorator       给一次能力逐层增加 observation / normalization / physical policy（DSL-015）
Physical Adapter     真的碰 OpenCode / Git / process / timer（Infrastructure / Process）
```

- 类型：`src/Wanxiangshu/Kernel/DomainFlow.fs`（AgentError/CompanionError/Context）、
  `src/Wanxiangshu/Kernel/Outcome.fs`（AgentRunResult / SendOutcome / SessionError）
- 直接 CE：`src/Wanxiangshu/Application/Manager/ManagerWorkflow.fs`（`observe` / `observeIdle`）、
  `src/Wanxiangshu/Application/Review/ReviewerWorkflow.fs`（`observe`）、
  `src/Wanxiangshu/Application/Reconciliation/TurnWorkflow.fs`（薄 router，按 bounded context 委派）
- 纯领域：`src/Wanxiangshu/Domain/ReconcileProgram.fs`（观测稳定边界：`decideStep` /
  `publishDecision` / `isTerminalOutcome`）
- 静态门禁：`scripts/checks/dsl-ownership.mjs`（positive 结构门，`--threshold=0`）、
  `scripts/checks/g4r-ce-vocabulary.mjs`（CE vocabulary absence + raw-time，`--phase=hard`）
- 迁移 ratchet（cutover 后删除）：`scripts/checks/dsl-ownership-ratchet.mjs`、
  `g4r-ce-vocabulary.mjs` 的 obsolete-controller 部分

## proof 概览（见 PROOF.md）

- MOVE（3 文件，已单跑绿）：`tests/unit/verify/direct-ce-contract.test.mjs`、
  `tests/unit/kernel/parallel.test.mjs`、`tests/unit/domain/reconcile-program.test.mjs`
  → `requirements/structured-workflow/tests/`
- NEW（3 文件，已单跑绿）：`workflow-surface.test.mjs`、`recovery-reentry.test.mjs`、
  `semantic-vocabulary.test.mjs`
- REUSE：`tests/unit/verify/{dsl-ownership,dsl-ownership-ratchet,g4r-ce-vocabulary}.test.mjs`、
  `tests/unit/guide-contract.test.mjs`、`tests/unit/execution/join-aborted-not-terminal.test.mjs`
  （effect-accounting 交叉）、`tests/unit/temporal/**`（time-capability/causal-wait 交叉）

单跑：`node --test requirements/structured-workflow/tests/<file>`。

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在、历史上 RED 过什么（rabbit / ce-temporal-ownership 考古）
2. `WHAT.md` —— 唯一 normative 合同：15 条编号命题 + 每条边界
3. `HOW.md` —— 实现模型：四层划分、模块地图、门禁机制、历史与弃权
4. `PROOF.md` —— 每条命题的测试落点表、cutover 待办、anchor id

## DEPENDS ON

无产品语义依赖（`requirements-design/INDEX.md` 依赖骨架为唯一来源）。历史上曾有一条
`structured-workflow → causal-wait` hard edge，Phase E 已审计删除：CE builder 是
implementation coupling，不是定义前提；event-driven wake 与 deadline escape 都是消费关系。

## 边界（DOES NOT OWN）

- 时间怎样进入系统（clock/timer capability、deadline 注入）→ `time-capability`
- 一个等待怎样被因果诊断（wait observation 非权威）→ `causal-wait`（含 DSL-012）
- 某个具体 workflow 的业务规则（Manager/Reviewer/Finality 的领域判断）→ 各业务 owner
- outcome 分型代数（Completed/Failed/Abandoned、ABORTED 非终态的执行语义）→ `effect-accounting`
- 恢复协议本身（permit 门、recovery budget、crash 后事实重放）→ `crash-reconciliation`
- 退化循环检测与 LoopKill 桥接 → `degeneration-guard`（LOOP-*）
- Host 观测语义（事件是信号、snapshot 真相源）→ `host-boundary`
- F# computation expression 语法本身：F# 是当前 HOW，WHAT 是「宿主语言直接控制结构、
  无第二业务 runtime」
- 当前 dsl-ownership allowlist / 旧 symbol absence ratchet（migration proof，cutover 删除）

## RED 判定

世界 RED 当且仅当：**领域模型开始保存程序位置、解释 AST、或依赖可变 stage 才知道下一步
做什么。** 对应 WHAT 命题的失败模式见 WHY.md 与 PROOF.md 各落点。
