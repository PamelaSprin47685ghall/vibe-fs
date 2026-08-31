# structured-workflow — WHAT

本文件是 `structured-workflow` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## STRUCTURED-WORKFLOW-001: 业务流程由宿主语言结构直接表达

业务控制流必须直接由宿主语言的原生结构（当前为 F# 的 `task { }`、`let!`、`do!`、`use!`、`match` / `match!`、`return!`、具名纯函数与有界递归）表达。宿主语言的调用栈即为业务流程栈，严禁将「程序下一步走向」编码为可长期存储的字段或枚举。

## STRUCTURED-WORKFLOW-002: 禁止第二业务运行时

领域 DSL 必须是直接执行的 CE 与领域具名操作。严禁在业务层构造内部 AST 后再进行二次解释，严禁引入 Command/Reply 消息总线、Step continuation 状态树或调用序列回放器。外部协议的解码器与物理进程命令的强类型化不受此限。

## STRUCTURED-WORKFLOW-003: 存储描述现实而非执行位置

DU 与数据字段仅允许表示封闭的领域词汇（如角色、终态类别）、已发生事实的持久证据（如 AgentFact 子族）或单次函数返回结果。严禁把 `NextAction`、`NextStep`、`ResumeAt*`、`StepIndex`、`ContinueToken` 等执行位置存入 record/DU/mutable cell，或通过 exported/cross-module surface 暴露给调用者驱动下一步；改名、跨文件搬运与恢复专用命名均不改变其 PC 性质。外部协议、物理句柄等同名碰撞必须在声明处以 `DSL-class: ExternalSignal` / `PhysicalHandle` 正向分类，路径或类型名本身不形成豁免。合法分类集合还明确区分 `Witness`、`Capability` 与 `Receipt`，这些权威/证据值仍不得承载执行位置。严禁表示流程当前执行到第几步（如 `CurrentStage`、`InFlight`、`Parked`）或多个状态的正交乘积。持久化事实与投影只描述发生过的事实与已证明的证据，严禁记录持久化程序计数器（Durable PC）。`Stage`、`Phase`、`Lease`、`Owner`、`Generation` 等词汇严禁作为流程控制的程序计数器或伪领域状态；除真实物理归属与底层物理资源世代标识外，上述词汇不得出现在业务状态机命名中。同一领域事实严禁在多处定义同构的 DU 类型；跨文件出现相同 case 集合的重复定义必须合并或明确划分有界上下文（bounded context）并提供单向转换，消除双写与状态漂移。系统崩溃后的恢复必须通过 Journal fold 产生领域事实，随后直接调用普通业务 workflow 入口重新进入流程；严禁恢复底层的 continuation 指针、程序计数器或临时中间状态；Reconcile 仅作为观测稳定边界，不承担业务调度操作系统的职责。

SW-003 vs SW-009 消歧：若恢复时将 projection fold 成唯一「最新 case」，等价于恢复一个隐藏的 durable resume-address，此举为 SW-009 禁止；合法的模式必须是 semantic entry 从一组 durable facts 与当前物理现实重新证明 outstanding obligation，随后直接调用普通业务流程。

## STRUCTURED-WORKFLOW-004: 纯决策与物理效果显式分缝

代码目录必须按照拥有者（owner）成树组织，严禁设立全局分层的顶层根目录（如 `Domain/`、`Application/`、`Infrastructure/`）。纯决策计算、具名语义词汇、端口装饰器与物理适配器属于 owner 内部的实现种类。composition root 必须宽而浅，只能承担 construction、typed topology/mode selection、fixed order、routing、lifetime、drain 与 disposal；`PluginBoot`、`HostSignalBootstrap`、`PluginTransforms`、`ToolRegistry` 等 root 严禁实现 owner-specific decision/recovery/classification、存储 PC 或动态 pipeline。此约束不得退化为 LOC/import-count 规则。领域操作必须通过具名 capability 调用副作用，严禁使用泛化的执行总线抹平强类型边界。控制分支（如 `match`、`if`、`try`）内部严禁嵌套产生第二层及更深的控制决策树（lexical pyramid）。嵌套错误处理与短路逻辑必须通过标准的 `Result` / `Option` 组合子（如 `result { }`、`taskResult { }`、`traverse`）进行扁平化表达，复杂的领域决策必须提取为独立的具名决策责任。

## STRUCTURED-WORKFLOW-005: 可变存储仅承载物理资源、投影缓存或算法草稿

可变存储（`let mutable` 与 `ref`）仅允许用于纯算法局部暂存（algorithm scratch）、并发同步原语、投影缓存（projection cache）或底层物理句柄（如 Task、Dictionary、CancellationTokenRegistration、锁对象等物理资源）。严禁使用可变存储记录业务阶段、执行槽位或行为布尔值。物理可变存储必须显式进行声明式标注。

## STRUCTURED-WORKFLOW-006: 业务 workflow 组合具有结构闭包与合法状态证明

当数据结构同时包含两类以上的状态控制轴（如 `State`、`Pending/Offer`、`Recovery/Repair`、`Drain`）时，必须能够证明所有可达组合均具备确切的业务含义并完成结构化分类。无法证明合理性的多轴组合必须拆分为独立的业务流程或显式权限许可（permit）。当一个业务 workflow 组合另一个 workflow 时，组合结果必须继续由原生语言调用、CE bind/return 及具名词汇直接表达。父 workflow 只能观察子 workflow 的类型化输入、领域结果与能力证明，严禁接收、存储或探测子流程的执行位置（包括 `Stage`、`NextAction`、`NextStep`、`ResumeAt*`、`StepIndex`、`ContinueToken`），也不得通过 `Advance/Tick/Step` 等轮询接口驱动子流程。模块接缝与 cross-callback registry 必须携带正向 physical/capability proof，否则严禁退化为状态机驱动总线；不存在 debt baseline。

Protocol-boundary exemption（外部协议边界豁免条件）：若存在外部交互协议必须通过 step/nextTool 与外部 caller 交互，必须满足：(1) kernel 唯一拥有 continuation/closure/停止权；(2) external caller 只提供 observation；(3) 豁免必须以书面 protocol-boundary exemption 形式记录于规范中。

## STRUCTURED-WORKFLOW-007: 语义压缩需 owner law 与行为证明

业务 CE 中调用的复杂时序操作必须封装为具有明确领域承诺的 Semantic Vocabulary（语义词汇）。词汇名称必须准确表达业务承诺（如 `reviewUntilPerfect`、`recoverDurably`），严禁使用无明确语义的伪操作码（如 `process`、`handle`、`doRetry`）。已被独立测试完整覆盖的机械时序允许通过 Semantic Vocabulary 进行压缩。被压缩的词汇必须拥有自身专属的时序或行为证明（temporal/behavioral proof），隐藏内部机械步骤不得改变宿主 CE 直接调用的本质。业务流程的正确性必须由可观察效果（产生的领域事实、调用轨迹、端口交互与最终状态）进行端到端证明，严禁通过断言解释器内部运行到的 AST 节点来判定正确性。

## STRUCTURED-WORKFLOW-008: 改 trace 的高阶组合必须命名与拥有

端口装饰器与高阶组合分为两类：passed operation 恰好 once-through，且保持 business outcome、multiplicity 与 authority 的透明资源/诊断 scope 合法；重复调用或在 recovery/fallback/catch path 再调用会改变业务 trace，必须在声明处绑定 owner、WHAT law、允许的 trace relation、executable proof、有限 bound，以及 failure/cancel/deadline policy，并在调用点具有明确名称。缺少任一项即为匿名策略。严禁 generic middleware/decorator interface、动态注册和匿名全局框架（如 MiddlewarePipeline、DecoratorBase、IWorkflowDecorator、ITransformMiddleware、WorkflowBuilder）；不得以 central runtime 取代普通 CE re-entry。

## STRUCTURED-WORKFLOW-009: 取消是控制面，不是业务数据

取消与中断属于控制面事件，用于决定程序是否继续执行，严禁伪装为业务终态结果数据。取消事件不得直接当作业务结果写入数据流，防止恢复与降级逻辑误判业务状态。

## STRUCTURED-WORKFLOW-010: 有界循环与有界扇出

所有业务循环与并发扇出必须有界。业务并发扇出必须通过 `Parallel.mapBounded` 进行，明确指定正有限的并发上限、保持输入下标顺序、支持取消传递并在异常时立即拒绝与归还许可。严禁在业务层使用无界并发或无界重试作为默认机制。

## STRUCTURED-WORKFLOW-011: 跨 owner 依赖必须携带显式架构授权

每个 production `.fs` 必须拥有恰一个 primary owner。production source dependency graph 必须由 `.fsproj` 编译集合上的 FCS checked symbol-use 证据独立投影，覆盖 alias、fully-qualified reference、经 `open` 解析的具体符号、pattern 与 type-only use；严禁用生成 JS/TS import、源码词法命中或文件命名猜测代替编译器语义。project symbol 无 declaration location、存在多个 production declaration location、FCS production set 与 `.fsproj` 不一致时必须 fail closed。requirement dependency graph 独立记录命题前提，严禁要求两图一一相等。

跨 owner source edge 只能终止于 provider owner 在 `scripts/checks/published-contracts.json` 中声明的 exact path + exact symbol contract / physical port，或由 physical adapter / composition root 的 exact consumer path + exact target path + exact target symbol wiring 建立。contract 必须列出被授权的 foreign consumer owner；一个文件被登记不授权 sibling symbol，一个 symbol 被登记不授权 sibling consumer。provider contract 必须绑定 provider path 所在的真实 DONE migration node、该 node 已发布 vocabulary 与 executable proof；physical adapter / composition root 必须绑定其 consumer path 所在的真实 DONE node。未 cutover provider 只形成可见 pending edge，不能提前获得完成信用。目录、namespace 前缀、文件名、F# 可见性和 `Surface.fs` 命名均不构成授权。每个声明与 requirement edge 必须携带非空架构理由；禁止 wildcard、baseline、allowlist 与目录 public heuristic。

门禁必须拒绝：unowned/duplicate ownership、missing/ambiguous declaration、foreign private/internal import、未登记 contract、unauthorized symbol/consumer、stale contract/target/cycle declaration、foreign Stage/Step/cursor/registry consumption、composition root 对未发布 foreign DU case 的 policy match，以及没有精确书面理由的 live owner SCC。cycle 只统计 semantic contract；composition root 只允许 construct/wire/order，physical adapter 只允许通过声明的 port edge 收敛物理现实。
