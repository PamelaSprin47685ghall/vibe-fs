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

## STRUCTURED-WORKFLOW-011: 跨 locality 依赖必须由 contract slice 授权

每个 production `.fs` 必须拥有恰一个 primary semantic owner，并且被恰一个 owner-locality fsproj 编译；每个 locality 只承载一个 primary semantic owner、恰对应一个 fsproj，并拥有稳定且全局唯一的 locality ID。owner 只拥有 vocabulary、invariant、failure algebra 与业务决策；locality/project 拥有编译身份；contract slice 拥有能力授权。任意 `consumer locality != provider locality` 的 dependency 都必须经过 provider slice grant，same-owner 不豁免。owner rename 或 merge 不得改变任何 locality edge、grant 或 authorization verdict。

ProjectReference 图必须是 DAG，并且始终是源码输入与归属边界的权威。compiler-resolved declaration use 必须映射为规范化的 cross-locality source edge；每条 actual source edge 的 provider locality 必须位于 consumer locality 的 ProjectReference transitive closure。release lane 必须 fresh 分析完整 production compile set；禁止以 aggregate 编译成功、人工 baseline、旧 snapshot、delta、mtime 或跨 run cache替代该证明。`Wanxiangshu.fsproj` 只是无语义的 flattened emitter：其 compile set 必须与 locality source 并集精确相等、不得引用 locality project、不得成为授权 provider。requirement dependency graph 与 source graph 只共享 owner identity，不要求边集合相等。

每个可被其他 locality 使用的 provider locality 必须形成一个 contract slice，并由 sibling `.fsi` 作为唯一 export inventory；manifest 不得重复保存或声称实施 per-symbol/per-owner ACL。同一 slice 的全部 `.fsi` exports 对其 effective audience 可见；若该能力集合不可共同授权，必须拆 slice。未来若重新要求 exact symbol isolation，必须使用保留 symbol identity 的 compiler-resolved analyzer，或改用提供真实 assembly isolation 的构建机制。

slice exposure 只有 `shared`、`bounded`、`effect`；locality kind 只有 `contract`、`runtime`、`adapter`、`composition`。`private` locality 不形成 slice，禁止其他 locality 引用。`shared` 只允许 immutable data、opaque identity、纯函数与无 authority 的 capability type；`bounded` 还必须满足 `actual effective consumers ⊆ allowed effective consumers`；两者只能由 `.fsi` 完整、直接与传递 closure 均为 Contract 的 contract locality 承载，且 closure 不超过 100 个 production `.fs`。Host import、IO、写入、网络、进程、provider、Git mutation、mutable registry、capability value 与 factory 属于 `effect`，只能位于 runtime/adapter；其全部实际反向可达 consumer 必须是 composition。composition 只承担 terminal wiring；若被引用，只允许 composition consumer 通过 exact composition-wiring relation 到达，禁止充当普通 provider。

manifest 只保存规范允许集合：每个 slice 的 provider locality、owner、exposure、`allowed_direct_consumers`、bounded slice 的 `allowed_effective_consumers`、`laws[]`、justification 与 semantic evidence。实际 direct consumers、effective consumers 与 source edges 必须由当前 ProjectReference DAG 和 compiler-resolved analyzer 同次推导，不得写入 manifest 形成第二事实源。每条 cross-locality direct ProjectReference 必须恰有一个 slice grant；actual direct consumers 必须与 allowed direct consumers 精确相等。physical port、adapter 与 composition wiring 必须保留 exact `consumer locality → provider slice` capability relation及两端 module，不得降级为 owner pair、裸路径或仅证明 ProjectReference 存在。

`semantic-evidence` 必须继续引用由 `requirement-trace` 建立的唯一 active、无 rejection 的 exact `{path,title,what_id,surface_module}` proof edge：WHAT package 等于 contract owner；`surface_module` 是 owner 注册、包含该 law 的 production Surface；exact test callback 的静态可达调用闭包消费该 Surface。裸路径、文件存在、源码字符串、comment-only、skip/todo、错误 title/WHAT、路径穿越、替代 proof、仅 import 未使用或仅由同文件其他 callback 使用均不授权。该闭包只证明 callback 触达 production Surface；行为 oracle 仍由 owner proof 负责。

门禁必须拒绝：unowned/duplicate owner 或 locality、重复编译归属、locality kind/exposure 非法、ProjectReference SCC、missing/stale/duplicate reference 或 grant、same-owner 未授权 edge、actual source edge 逃出声明 closure、private 被引用、contract closure 含非 Contract 或超过预算、shared 携带 effect authority、bounded effective audience 越界、effect 被非 composition 到达、composition 被普通 consumer 引用、emit/locality compile-set drift、`.fsi` 缺失或与 implementation 不兼容、伪造/悬空 semantic evidence，以及 physical port/adapter/composition relation 降级。真实 compiler canary 必须同时固定：aggregate 可编译但 missing closure edge 为 RED；合法 direct/transitive edge 为 GREEN；alias/open/generic/type/pattern declaration use 均可归属；external/package symbol 不产生 production locality edge；module-local private binding 与 `.fsi` 未导出 symbol 保持不可见。

## STRUCTURED-WORKFLOW-012: owner/impact compile 必须是精确输入并集的一次 flat Fable

owner compile 的输入是目标 owner locality 加其 transitive ProjectReference closure；impact compile 的输入按 changed contract 决定。修改带 sibling `.fsi` 的实现 `.fs` 且 `.fsi` 未变时，只选择该实现 locality 与其 forward contract closure，普通 reverse consumer 禁止进入 impact set。修改 `.fsi`、新增/删除公开 source，或同批同时修改 sibling `.fsi` 时，必须选择 owning locality 的全部 transitive reverse consumers，再对所有选中 root 求 forward closure。多个改动合并为集合并集，不得逐工程重复编译。fsproj、`Directory.Build.*`、package lock、Fable/tool manifest 或 aggregate emitter 变化保守选择 full。

所有选中 source 必须按 `Wanxiangshu.fsproj` 的 canonical order 生成一个零 ProjectReference flat fsproj，并由一次真实 Fable invocation 编译；input union 少一个、多一个、重复或乱序都失败。选中 production `.fs` 超过 aggregate 的 60% 时直接退化为 full flat build，不伪装 focused。全自动增量编译根据代码与产物的新旧状态精确计算 impact 闭包并复用产物缓存，无文件变更时秒级返回，禁止依赖 watch 守护进程。

full release build 始终以 aggregate emitter 的完整 source/config union 启动一次 Fable；多工程 topology 只负责 ownership 与 impact 计算，不得逐 owner 编译后再拼接。等价 clean 环境下，最终 full multi-project 路径的输入集合、Fable 配置、invocation 数与原始单工程路径相同；交付必须记录两者的实际 clean timing，禁止仅凭结构推断性能等价。
