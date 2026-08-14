# Meta / Programming / Causality

## `requirement-system`

**WHY**  
当前接受的产品真理必须有唯一 semantic owner；否则 docs、gate、test、change 会形成互相覆盖的平行法域。

**OWNS**
- Requirement Package 的语义身份、package-local normative authority 与显式 dependency。
- accepted repository state 中全部 packages 必须同时为真；dependency 不表示优先级、冻结或 override。
- 每个 normative proposition 恰有一个 owner。
- 每个新世界 executable proof 恰有一个 package owner。
- no naked normative authority：跨包治理规则本身也必须被 package 拥有。
- package verifier 的存在与 dependency-closure acceptance 语义。

**DOES NOT OWN**
- 什么证据技术足够证明某个产品事实。
- 任一产品领域事实。
- Git/PR 历史沉积、Proposal 生命周期本身；未来 main 只表达当前接受真理。
- 当前 Clause ID、why/what/shape/how/proof 文件层级。

**DEPENDS ON**  
无产品语义依赖。

**PROVIDES**
- semantic ownership、dependency、acceptance 与 proof ownership 的元合同。

**FAILURE MEANING**  
RED = 仓库中存在无 owner、双 owner、互相矛盾或无法独立验收的 normative authority。

**INDEPENDENT CHANGE**  
把 manifest 从 TOML 改为其它机器格式，或改变 package 物理 layout，而不改变任何产品 package WHAT。

**CURRENT EVIDENCE**  
`AGENTS.md`、`docs/what/document-governance.md`、`changes/README.md`、现有 GOV 条款，以及本次重构目标。旧 changes lifecycle 只作反例与迁移证据。

---

## `verification-system`

**WHY**  
Requirement acceptance 需要可失败、可重放、按风险升级的证据体系；否则“绿”只表示某个测试命令碰巧通过。

**OWNS**
- proof ladder / evidence qualification：static → pure law → deterministic temporal → physical adapter → Long Stroke → release 等层级的选择原则。
- semantic branch 不应无理由直接升级为昂贵 E2E。
- verifier 必须真正可红；canary/gate 必须先证明失败价值。
- dependency closure 的验证执行规则。
- 一个 physical world 可为多个 package-local semantic oracle 提供证据；物理 E2E ownership 与 semantic oracle ownership 分离。
- deterministic、无真实 wall-clock 偶然性的 proof 原则。

**DOES NOT OWN**
- “artifact 必须含 resources”等 distribution 产品事实。
- “prompt 不得泄漏 SessionId”等具体产品事实；这些由对应 package 拥有，verification 只规定如何证明。
- 当前 `tests/unit|integration|e2e` 顶级目录分类。
- 当前 One Long Stroke 的 OpenCode 具体脚本名。

**DEPENDS ON**
- `requirement-system`。

**PROVIDES**
- 什么样的 proof 有资格支持 `Satisfied(P)`。

**FAILURE MEANING**  
RED = repository 无法可信地区分“requirement 已满足”与“测试/门禁没有覆盖或没有失败能力”。

**INDEPENDENT CHANGE**  
把真实 Host canary 从当前 harness 换成另一物理 adapter，package-local product contracts 不变。

**CURRENT EVIDENCE**  
`docs/proof/verify.md`、`scripts/checks/**`、CI workflow、One Long Stroke、各类 temporal/pure/adapter proof。

---

## `structured-workflow`

**WHY**  
业务控制流若被编码成 Stage/Phase/Program AST，会在宿主语言之外再造第二 runtime，使恢复、测试与非法状态同时膨胀。

**OWNS**
- 业务流程直接由宿主语言结构表达：structured bind/branch/return/resource scope/bounded recursion。
- 状态标签只允许表示物理/领域真实事物，不允许表示“程序走到第几步”。
- pure decision 与 effect shell 分层。
- 恢复应重入普通流程，而不是恢复手写 program counter。
- mutable/ref 只允许承载真实物理资源或局部纯实现，不承载跨调用业务流程位置。
- Semantic Vocabulary 应是领域事实词汇，不是伪 runtime opcode。

**DOES NOT OWN**
- 时间怎样进入系统。
- 一个等待怎样被因果诊断。
- 某个具体 workflow 的业务规则。
- F# computation expression 语法本身；F# 是当前 HOW，WHAT 是“宿主语言直接控制结构、无第二业务 runtime”。
- 当前 dsl-ownership allowlist/旧 symbol absence ratchet。

**DEPENDS ON**  
无必须产品依赖。

**PROVIDES**
- 所有 workflow packages 可依赖的“控制流不是领域状态”保证。

**FAILURE MEANING**  
RED = 领域模型开始保存程序位置、解释 AST 或依赖可变 stage 才知道下一步做什么。

**INDEPENDENT CHANGE**  
从 F# CE 改成另一种宿主语言结构化 effect 表达，只要仍无第二业务 runtime。

**CURRENT EVIDENCE**  
`docs/{why,what}/dsl-structured-program.md`、`docs/what/flow.md`、`scripts/checks/dsl-ownership*`、相关 completed DSL changes。

---

## `time-capability`

**WHY**  
时间若从 ambient wall clock/timer 偷渡业务代码，同一事实在不同运行时刻会产生不同判断，proof 与 replay 都失去确定性。

**OWNS**
- clock/timer 作为显式 capability 注入。
- deadlines / elapsed-time observation 的 typed 表达。
- virtualizable time；测试可替换物理时钟。
- Domain/Application/Session 不直接读 ambient `UtcNow`、全局 timer。
- 时间值本身不是 authority；只有消费它的领域规则决定意义。

**DOES NOT OWN**
- 等待的业务因果关系。
- 某个超时预算的产品数值，除非相邻 package 明确把该数值写成 WHAT。
- process deadline、join deadline、recovery budget 的具体业务意义。
- 当前 `IClockPort` / `ITimerPort` 名字。

**DEPENDS ON**  
无。

**PROVIDES**
- 可测试、可重放的时间/定时 capability。

**FAILURE MEANING**  
RED = 业务结果可能仅因 wall-clock 环境、测试运行速度或隐藏 timer 不同而改变。

**INDEPENDENT CHANGE**  
从当前 port 形态改为显式 `Instant/Deadline` token + scheduler capability，不改变 causal-wait 或 process semantics。

**CURRENT EVIDENCE**  
`IClockPort` / `ITimerPort`、ambient-time static gates、temporal ownership changes。

---

## `causal-wait`

**WHY**  
业务等待需要知道“正在等什么/为什么还没发生”，但诊断这一等待不能反过来成为 durable business fact、prompt authority 或决策真相源——观察可以看程序，程序绝不可以看观察（同一不变量的两侧，不是两个独立 WHY）。

**OWNS**
- wait observation 的非权威性。
- 跨 owner、Host turn、provider attempt、physical capability 的等待可形成 process-local causal diagnostic observation。
- causal observation 只描述依赖/进展，不可进入 Journal 作为世界事实，不可成为 Prompt input authority。
- event-driven wake 优先于 polling；等待应由实际依赖解除，而非 wall-clock luck。
- 取消/完成后 observation 生命周期终止，不能复活业务机会。

**DOES NOT OWN**
- 时间 capability。
- structured workflow 语法。
- 某个具体 reviewer/process/session 的等待条件。
- crash recovery；process-local observation 可在重启后安全消失。
- Host snapshot 的业务事实定义。

**DEPENDS ON**
- `structured-workflow`
- `time-capability`（当等待需要 deadline 时）

**PROVIDES**
- 可诊断但无 authority 的 wait abstraction。

**FAILURE MEANING**  
RED = 系统要么只能靠盲轮询/睡眠理解等待，要么把诊断状态升级成可改变业务结果的事实。

**INDEPENDENT CHANGE**  
把 process-local waiter 从当前实现换成 subscription/future/actor mailbox，而所有业务 package WHAT 不变。

**CURRENT EVIDENCE**  
`causal-ce-observability.md`、`waitfact-causal-renewal.md`、`ce-temporal-ownership.md`、Reviewer/Host 中 event-driven wait 纪律。
