# structured-workflow — WHAT

本文件是 `structured-workflow` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## STRUCTURED-WORKFLOW-001: 业务流程由宿主语言结构直接表达

业务控制流必须直接由宿主语言的原生结构（当前为 F# 的 `task { }`、`let!`、`do!`、`use!`、`match` / `match!`、`return!`、具名纯函数与有界递归）表达。宿主语言的调用栈即为业务流程栈，严禁将「程序下一步走向」编码为可长期存储的字段或枚举。

## STRUCTURED-WORKFLOW-002: 禁止第二业务运行时

领域 DSL 必须是直接执行的 CE 与领域具名操作。严禁在业务层构造内部 AST 后再进行二次解释，严禁引入 Command/Reply 消息总线、Step continuation 状态树或调用序列回放器。外部协议的解码器与物理进程命令的强类型化不受此限。

## STRUCTURED-WORKFLOW-003: 存储描述现实而非执行位置

DU 与数据字段仅允许表示封闭的领域词汇（如角色、终态类别）、已发生事实的持久证据（如 AgentFact 子族）或单次函数返回结果。严禁用于表示流程当前执行到第几步（如 `CurrentStage`、`NextAction`、`InFlight`、`Parked`）或多个状态的正交乘积。持久化事实与投影只描述发生过的事实与已证明的证据，严禁记录持久化程序计数器（Durable PC）。`Stage`、`Phase`、`Lease`、`Owner`、`Generation` 等词汇严禁作为流程控制的程序计数器或伪领域状态；除真实物理归属与底层物理资源世代标识外，上述词汇不得出现在业务状态机命名中。同一领域事实严禁在多处定义同构的 DU 类型；跨文件出现相同 case 集合的重复定义必须合并或明确划分有界上下文（bounded context）并提供单向转换，消除双写与状态漂移。系统崩溃后的恢复必须通过 Journal fold 产生领域事实，随后直接调用普通业务 workflow 入口重新进入流程；严禁恢复底层的 continuation 指针、程序计数器或临时中间状态；Reconcile 仅作为观测稳定边界，不承担业务调度操作系统的职责。

SW-003 vs SW-009 消歧：若恢复时将 projection fold 成唯一「最新 case」，等价于恢复一个隐藏的 durable resume-address，此举为 SW-009 禁止；合法的模式必须是 semantic entry 从一组 durable facts 与当前物理现实重新证明 outstanding obligation，随后直接调用普通业务流程。

## STRUCTURED-WORKFLOW-004: 纯决策与物理效果显式分缝

代码目录必须按照拥有者（owner）成树组织，严禁设立全局分层的顶层根目录（如 `Domain/`、`Application/`、`Infrastructure/`）。纯决策计算、具名语义词汇、端口装饰器与物理适配器属于 owner 内部的实现种类。领域操作必须通过具名 capability 调用副作用，严禁使用泛化的执行总线抹平强类型边界。控制分支（如 `match`、`if`、`try`）内部严禁嵌套产生第二层及更深的控制决策树（lexical pyramid）。嵌套错误处理与短路逻辑必须通过标准的 `Result` / `Option` 组合子（如 `result { }`、`taskResult { }`、`traverse`）进行扁平化表达，复杂的领域决策必须提取为独立的具名决策责任。

## STRUCTURED-WORKFLOW-005: 可变存储仅承载物理资源、投影缓存或算法草稿

可变存储（`let mutable` 与 `ref`）仅允许用于纯算法局部暂存（algorithm scratch）、并发同步原语、投影缓存（projection cache）或底层物理句柄（如 Task、Dictionary、CancellationTokenRegistration、锁对象等物理资源）。严禁使用可变存储记录业务阶段、执行槽位或行为布尔值。物理可变存储必须显式进行声明式标注。

## STRUCTURED-WORKFLOW-006: 业务 workflow 组合具有结构闭包与合法状态证明

当数据结构同时包含两类以上的状态控制轴（如 `State`、`Pending/Offer`、`Recovery/Repair`、`Drain`）时，必须能够证明所有可达组合均具备确切的业务含义并完成结构化分类。无法证明合理性的多轴组合必须拆分为独立的业务流程或显式权限许可（permit）。当一个业务 workflow 组合另一个 workflow 时，组合结果必须继续由原生语言调用、CE bind/return 及具名词汇直接表达。父 workflow 只能观察子 workflow 的类型化输入、领域结果与能力证明，严禁探测子流程的执行位置（如 `Stage`、`ResumeAt`）或通过 `Advance/Tick/Step` 等轮询接口驱动子流程。模块接缝处严禁退化为状态机驱动总线。

Protocol-boundary exemption（外部协议边界豁免条件）：若存在外部交互协议必须通过 step/nextTool 与外部 caller 交互，必须满足：(1) kernel 唯一拥有 continuation/closure/停止权；(2) external caller 只提供 observation；(3) 豁免必须以书面 protocol-boundary exemption 形式记录于规范中。

## STRUCTURED-WORKFLOW-007: 语义压缩需 owner law 与行为证明

业务 CE 中调用的复杂时序操作必须封装为具有明确领域承诺的 Semantic Vocabulary（语义词汇）。词汇名称必须准确表达业务承诺（如 `reviewUntilPerfect`、`recoverDurably`），严禁使用无明确语义的伪操作码（如 `process`、`handle`、`doRetry`）。已被独立测试完整覆盖的机械时序允许通过 Semantic Vocabulary 进行压缩。被压缩的词汇必须拥有自身专属的时序或行为证明（temporal/behavioral proof），隐藏内部机械步骤不得改变宿主 CE 直接调用的本质。业务流程的正确性必须由可观察效果（产生的领域事实、调用轨迹、端口交互与最终状态）进行端到端证明，严禁通过断言解释器内部运行到的 AST 节点来判定正确性。

## STRUCTURED-WORKFLOW-008: 改 trace 的高阶组合必须命名与拥有

端口装饰器与高阶组合分为两类：不改变业务因果 trace 的透明装饰器（如诊断、指标收集）允许自由叠加；改变业务 trace 的语义装饰器与高阶策略（如重试、降级、恢复、超时策略）必须具备所属 owner 的正式规范语义并在调用点具有明确名称。严禁使用匿名的全局中间件框架（如 MiddlewarePipeline、DecoratorBase、IWorkflowDecorator、WorkflowBuilder）。

## STRUCTURED-WORKFLOW-009: 取消是控制面，不是业务数据

取消与中断属于控制面事件，用于决定程序是否继续执行，严禁伪装为业务终态结果数据。取消事件不得直接当作业务结果写入数据流，防止恢复与降级逻辑误判业务状态。

## STRUCTURED-WORKFLOW-010: 有界循环与有界扇出

所有业务循环与并发扇出必须有界。业务并发扇出必须通过 `Parallel.mapBounded` 进行，明确指定正有限的并发上限、保持输入下标顺序、支持取消传递并在异常时立即拒绝与归还许可。严禁在业务层使用无界并发或无界重试作为默认机制。
