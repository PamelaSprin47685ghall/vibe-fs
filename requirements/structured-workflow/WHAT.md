# structured-workflow — WHAT

本文件是 `structured-workflow` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## STRUCTURED-WORKFLOW-001: 业务流程由宿主语言结构直接表达

业务控制流必须直接由宿主语言的原生结构（当前为 F# 的 `task { }`、`let!`、`do!`、`use!`、`match` / `match!`、`return!`、具名纯函数与有界递归）表达。宿主语言的调用栈即为业务流程栈，严禁将「程序下一步走向」编码为可长期存储的字段或枚举。

## STRUCTURED-WORKFLOW-002: 禁止第二业务运行时

领域 DSL 必须是直接执行的 CE 与领域具名操作。严禁在业务层构造内部 AST 后再进行二次解释，严禁引入 Command/Reply 消息总线、Step continuation 状态树或调用序列回放器。外部协议的解码器与物理进程命令的强类型化不受此限。

## STRUCTURED-WORKFLOW-003: 存储描述现实而非执行位置

DU 与数据字段仅允许表示封闭的领域词汇、已发生事实的持久证据或单次函数返回结果。严禁把 `NextAction`、`NextStep`、`ResumeAt*`、`StepIndex`、`ContinueToken` 等执行位置存入 record/DU/mutable cell，或通过 exported/cross-module surface 暴露给调用者驱动下一步；改名、跨文件搬运与恢复专用命名均不改变其 PC 性质。外部协议、物理句柄等同名碰撞必须在声明处以 `DSL-class: ExternalSignal` / `PhysicalHandle` 正向分类，路径或类型名本身不形成豁免。合法分类集合还明确区分 `Witness`、`Capability` 与 `Receipt`，这些权威/证据值仍不得承载执行位置。严禁表示流程当前执行到第几步或多个状态的正交乘积。持久化事实与投影只描述发生过的事实与已证明的证据，严禁记录持久化程序计数器（Durable PC）。`Stage`、`Phase`、`Lease`、`Owner`、`Generation` 等词汇严禁作为流程控制的程序计数器或伪领域状态；除真实物理归属与底层物理资源世代标识外，上述词汇不得出现在业务状态机命名中。同一领域事实严禁在多处定义同构 DU；跨文件出现相同 case 集合的重复定义必须合并，或明确划分 bounded context 并提供单向转换。系统崩溃后的恢复必须通过 Journal fold 产生领域事实，随后直接调用普通业务 workflow 入口重新进入流程；严禁恢复 continuation 指针、程序计数器或临时中间状态；Reconcile 仅作为观测稳定边界，不承担业务调度操作系统职责。

SW-003 vs SW-009 消歧：若恢复时将 projection fold 成唯一「最新 case」，等价于恢复一个隐藏的 durable resume-address，此举为 SW-009 禁止；合法的模式必须是 semantic entry 从一组 durable facts 与当前物理现实重新证明 outstanding obligation，随后直接调用普通业务流程。Relay 等协议中的 Phase 只能是诊断视图与只读投影，严禁作为 next-action 或 effect-selection API；执行控制必须回到拥有该业务过程的 subsystem 原生 CE。

## STRUCTURED-WORKFLOW-004: 纯决策与物理效果显式分缝

代码按 subsystem 的知识边界组织。纯决策、领域词汇、端口与物理适配器都只是 subsystem 内部实现；不得为这些实现种类另建与 subsystem 平级的治理身份。composition root 必须宽而浅，只承担 construction、typed topology/mode selection、fixed order、routing、lifetime、drain 与 disposal；`PluginBoot`、`HostSignalBootstrap`、`PluginTransforms`、`ToolRegistry` 等 root 严禁实现 foreign-subsystem decision/recovery/classification、存储 PC 或动态 pipeline。例如 Relay/Manager 流程中的循环判定与自动评审/工作/收尾时序必须内聚于 Relay subsystem 的单一 CE；root 仅保留固定拓扑装配与生命周期挂接。领域操作必须通过具名 capability 调用副作用，严禁用泛化执行总线抹平强类型边界。控制分支内部严禁嵌套产生第二层及更深控制决策树；嵌套错误处理与短路逻辑必须通过标准 `Result` / `Option` 组合子扁平化，复杂领域决策必须提取为独立具名责任。

## STRUCTURED-WORKFLOW-005: 可变存储仅承载物理资源、投影缓存或算法草稿

可变存储（`let mutable` 与 `ref`）仅允许用于纯算法局部暂存、并发同步原语、投影缓存或底层物理句柄。严禁使用可变存储记录业务阶段、执行槽位或行为布尔值。物理可变存储必须显式进行声明式标注。

## STRUCTURED-WORKFLOW-006: 业务 workflow 组合具有结构闭包与合法状态证明

当数据结构同时包含两类以上状态控制轴时，必须证明所有可达组合具备确切业务含义并完成结构化分类；无法证明的多轴组合必须拆成独立流程或显式 capability。父 workflow 只能观察子 workflow 的类型化输入、领域结果与能力证明，严禁接收、存储或探测子流程执行位置，也不得通过 `Advance/Tick/Step` 轮询驱动。模块接缝与 cross-callback registry 必须携带正向 physical/capability proof，否则严禁退化为状态机驱动总线。

Protocol-boundary exemption（外部协议边界豁免条件）：若存在外部交互协议必须通过 step/nextTool 与外部 caller 交互，必须满足：(1) kernel 唯一拥有 continuation/closure/停止权；(2) external caller 只提供 observation；(3) 豁免必须以书面 protocol-boundary exemption 形式记录于规范中。

## STRUCTURED-WORKFLOW-007: 语义压缩需 subsystem law 与行为证明

业务 CE 中复杂时序操作必须封装为具有明确领域承诺的 Semantic Vocabulary。被压缩的词汇必须属于一个明确 subsystem，并拥有自身时序或行为证明；隐藏机械步骤不得改变宿主 CE 直接调用的本质。业务流程正确性必须由可观察效果、领域事实、调用轨迹、端口交互与最终状态证明，严禁通过解释器内部 AST 节点证明正确性。

## STRUCTURED-WORKFLOW-008: 改 trace 的高阶组合必须命名与拥有

passed operation 恰好 once-through 且保持 business outcome、multiplicity 与 authority 的透明资源/诊断 scope 合法；重复调用或在 recovery/fallback/catch path 再调用会改变业务 trace，必须属于一个明确 subsystem、绑定 WHAT law、允许 trace relation、executable proof、有限 bound 及 failure/cancel/deadline policy，并在调用点具有明确名称。严禁 generic middleware/decorator interface、动态注册和匿名全局框架；不得以 central runtime 取代普通 CE re-entry。

## STRUCTURED-WORKFLOW-009: 取消是控制面，不是业务数据

取消与中断属于控制面事件，用于决定程序是否继续执行，严禁伪装为业务终态结果数据。取消事件不得直接当作业务结果写入数据流，防止恢复与降级逻辑误判业务状态。

## STRUCTURED-WORKFLOW-010: 有界循环与有界扇出

所有业务循环与并发扇出必须有界。业务并发扇出必须通过 `Parallel.mapBounded` 进行，明确指定正有限的并发上限、保持输入下标顺序、支持取消传递并在异常时立即拒绝与归还许可。严禁在业务层使用无界并发或无界重试作为默认机制。

## STRUCTURED-WORKFLOW-011: subsystem 是唯一架构治理粒度

每个 production `.fs` 必须恰属于一个 subsystem，并恰由一个 compile shard 编译。**Subsystem 是唯一需要人工命名、裁决、分工、重构/重写/删除和验收的架构单位。** 一个 subsystem 拥有其 vocabulary、invariant、failure algebra、decision、public contract 与行为证明；它必须能够在只依赖公开 contract 与 proof 的前提下被整体替换。

compile shard、`.fsproj`、`.fsi`、ProjectReference 与构建 closure 只是 subsystem 以下的编译实现。它们可以为增量编译、签名隔离和物理适配而自由拆分/合并，不取得独立 semantic owner、授权 audience、exposure class、slice adjudication 或 lifecycle。旧 `WanxiangshuSemanticOwner`、`WanxiangshuOwnerLocality`、locality kind、published-contract owner ACL 等字段在迁移期只允许作为兼容读取数据，禁止成为 release verdict 的权威输入。新 shard 应显式声明 `WanxiangshuSubsystem` 与 `WanxiangshuCompileShard`；未迁 shard 可暂由唯一 legacy-owner→subsystem 映射解析，不得因此恢复 owner 治理。

compile-shard ProjectReference 图必须是 DAG；每个 production source 唯一归属，且 `.fs` 与 sibling `.fsi` 同 shard。`Wanxiangshu.fsproj` 是无语义 flattened emitter，其 `.fs/.fsi` compile set 必须与全部 compile shard 的 source 并集精确相等且不得 ProjectReference shard。release gate 只检查这些可由构建事实直接兑现的约束，不建立 per-symbol/per-owner ACL，也不得自建 FCS 扫描器来制造第二套源码依赖真相。

## STRUCTURED-WORKFLOW-012: compile shard 只服务隔离与精确增量编译

实现 `.fs` 改动且 sibling `.fsi` 未变时，impact compile 只需选择 owning shard 与其编译所需 forward closure，不得因为 subsystem、旧 owner 或人工 audience 标签把普通 reverse consumer 纳入。`.fsi` 改动、新增/删除公开 source 或同批签名变化时，选择该 shard 的 transitive reverse consumers，再对所有 root 求 forward closure。工程、aggregate、toolchain、lockfile 或 Fable 配置变化可保守走 full。

所有选中 source 必须按 aggregate canonical order 合并成一次零 ProjectReference 的 flat Fable invocation；多个 shard 的影响集合先求并集，禁止逐 shard 重复启动 Fable。full release 仍只编译 aggregate 的完整 source/config union。compile shard 的数量、名称和内部边界属于可调整的构建优化参数，不得迫使 subsystem 数量随之变化，也不得为了减少项目数把不相关知识重新塞进 shared foundation。

## STRUCTURED-WORKFLOW-013: subsystem 边界必须遵守依赖倒置

跨 subsystem 依赖必须指向 provider 的窄 public contract、纯数据/decision 或 capability port；高层业务不得读取 foreign runtime registry、constructor、physical handle、内部 Stage/Phase/slot 或 adapter implementation。需要物理效果时，由 composition 注入窄 capability；业务 subsystem 依赖 port，adapter 依赖外界，禁止业务反向依赖具体 adapter。

可复用 `runtime-platform` shard 只能包含无领域决策的泛化原语，并且不得依赖任何 domain subsystem。若某个“通用”shard 需要 Session/Provider/Git/Relay/Blogger 等领域知识，它就不是平台原语，必须回到拥有该知识的 subsystem。相同语法形状、相同 primitive type 或“大家都用”均不是合并理由。compile shard 拆分必须减少真实依赖闭包或隔离不同 reason-to-change；只换文件名、标签、manifest 而保持同一宽依赖闭包不算完成。

subsystem dependency graph 的目标形状是单向、可解释、可替换。跨 subsystem SCC 是架构债，迁移期间必须被工具显式报告并持续消解；不得通过新增 facade、event bus、service locator 或共享 DTO 大包隐藏双向依赖。

## STRUCTURED-WORKFLOW-014: 物理能力留在边界，业务只消费窄 capability

文件、进程、网络、provider、Git mutation、时钟与其他外界 authority 必须有单一明确物理实现边界。业务代码不得因为复用 helper 而获得 factory、mutable registry、Node import 或具体 Host implementation。physical adapter 可有独立 compile shard，但它仍属于一个 subsystem 的内部实现，不获得第二治理身份。真实安全要求必须由 exact capability、identity、lifetime 与行为反例证明；project 名称、`internal`、manifest 标签或注释不能替代证明。

## STRUCTURED-WORKFLOW-015: 生成物与工具产物不形成第二架构

repository-generated JavaScript、codec、resource 与其他构建产物必须可追溯到产生它的 subsystem 与确定输入；构建可验证 digest、lineage 与确定性，但这些记录只是生成正确性的证据，不是新的 owner/slice/locality 授权体系。生成物若携带 filesystem/process/network 等 authority，必须按真实 capability 边界处理，不能因为 deterministic 或 generated 而视为 pure。禁止为了证明 subsystem 边界再建立一套与源码竞争的 canonical world、worksheet、adjudication snapshot 或 symbol ACL。

## STRUCTURED-WORKFLOW-016: 旧 M6 locality/slice 治理退役，禁止双重权威

M6 中 semantic-owner/locality/slice/exposure/canonical-adjudication 作为 release authorization 模型的路线终止。已有 `.fsi`、fsproj、ProjectReference、impact compile、compiler canary 与已完成的真实 contract/adapter 拆分继续作为普通编译资产保留；旧 manifest、owner ACL、locality classification、worksheet、formal snapshot 与相关 adjudication 只能保留为历史资料或迁移兼容输入，不得参与当前 release verdict。

切换后 `scripts/checks/subsystems.mjs` 是 source→subsystem→compile-shard 结构事实的唯一 release gate；旧 `semantic-owners.mjs`、`owner-contracts.mjs`、`owner-projects.mjs` 不得与其并行形成双重权威。迁移不得通过给新 shard 补旧 ACL、复制 old manifest claim 或新增 facade 来取得绿灯。任何旧 gate 与新 subsystem 模型冲突时，修正或退休旧 gate，而不是恢复已放弃的治理层级。
