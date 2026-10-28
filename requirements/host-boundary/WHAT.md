# host-boundary — WHAT

## [001] 业务层不消费流式碎片事件

万象通（宿主适配层）在最早边界过滤宿主流式碎片事件（如 `message.updated`、`part.delta` 等），将其收敛为粗粒度唤醒信号；核侧业务逻辑严禁直接消费流式碎片，统一读取完整快照作为业务事实来源。

## [002] 业务层信号闭集与精准分型

万象通将宿主物理事件解码为中立的类型化信号闭集（`SessionIdle`、`ProviderRetry`、`ProviderFailure`、`SessionDeleted` 与 `AttemptAborted`）后投递给业务层。中止操作必须解码为专用物理中断唤醒，绝不得与提供者失败混淆。

## [003] 传输与领域分离且信号仅作唤醒

宿主信号仅作为单向唤醒触发器，不得作为业务事实载体。信号内携带的尝试次数仅供诊断参考，不得直接作为领域的重试或回退计数。粗粒度 `AttemptAborted(SessionId)` 仅唤醒调和器（reconciler）；撤销静止门禁、取消 Strength 所有者与推进 attempt stop fence 必须依据快照或公开 exact 取消回执，校验本次 `PhysicalUserMessageId` 仍拥有对应执行。无 exact `PhysicalUserMessageId` 的粗粒度信号不得把该会话的全部执行投影为 `SessionAborted`，也不得撤销已被更新用户消息接纳的新执行。当前操作者中止仅撤销本次执行能力，旧执行迟到的中止不影响新执行。

## [004] TurnUnknown 为对齐私有观测而非业务结局

快照中未决的中间状态归类为调和器私有的 `TurnUnknown` 观测，严禁跨越调和边界发布为公开的业务完成终态，防止产生虚假的完成或缺失报告。

## [005] Reconciler 单飞快照观测与事件驱动收敛

调和器对每个会话严格保证单飞执行（single-flight），接收到粗粒度信号时执行单次完整快照读取。在无新信号或明确投影边缘时，严禁通过墙钟轮询重复读取快照。提供者失败的 exact assistant `message.updated` 即使尚无 retry 所有者的终态决策，也必须用自身精确物理身份发出类型化失败唤醒；`TurnFailed` 只有在该精确物理身份匹配时方可发布，先到达的 idle/retry 只能等待，不得将裸错误终态化；同一物理执行随后的 idle/retry 唤醒或无身份的粗粒度失败不得覆盖它，新物理执行或显式 abort 才能替换它。无当前物理绑定的粗粒度失败不得读取快照或发布 terminal turn，只能等待精确投影证据。精确失败不得伪装成受管会话终态，也不得因粗粒度 `session.error` 缺席被丢弃。

## [006] Raw Part 与 ToolParts 状态投影一致性

万象通在将宿主原始工具调用分段映射到中立 ToolParts 时必须保持状态严格一致：未完成状态映射为挂起调用，已完成或失败状态映射为调用结果，严禁出现状态分叉投影。

## [007] Compaction 观测门禁之预防与收容

宿主压缩控制实行双层防护：启动前校验并关闭宿主自动压缩配置；启动阶段只在首回合窗口内（会话开始至首个 completed assistant 消息含自身）拒绝非预期压缩；首回合窗口之后的压缩（含用户手动 `/compact` 与后续回合压缩）属于运行时收容范围。运行时观测到压缩事实时，必须立即触发原子上下文重锚定。

## [008] Transform 到 ProviderRunIdentity 因果读与唯一性

从快照提取运行身份必须基于因果规则（角色、完成时间、父节点匹配与最大序列）；命中唯一候选方可确认绑定，命中 0 个或多个候选一律安全失败。万象通挂载的 `experimental.chat.messages.transform` 属于模型推理前的物理边界，该 Hook 不得把“当前 assistant run 已经存在”作为前置条件，亦不得通过有界等待将未来 run 伪装成投影延迟；在该 Hook 冻结的恢复决策必须先以精确 `PhysicalUserMessageId` 保存为未绑定 attempt plan，同一物理消息的重复 transform 只能复用首次冻结值；待后续完整观测出现精确 `ProviderRunIdentity` 后一次性绑定。各 Hook（`chat.params`、transform、tool context、provider-start）通过物理证据（物理消息 ID、assistant parentID、`ProviderRunIdentity`）关联到精确执行并保持只读，不允许使用 session-current 回退替代精确证据。

## [009] Tool 身份双半边与缺失 Fail-Closed

工具执行上下文必须同时具备消息 ID 与调用 ID 两个半边身份；任一半边缺失直接安全失败，严禁跨上下文推测配对。

## [010] 多实例边界与共享注册表访问纪律

跨工作区实例间仅共享只读或受限的全局身份注册表，且注册表访问不得跨越异步等待点；各实例的持久化日志写入器与状态缓存完全隔离，严禁共享写入通道。

## [011] 空 Content 预防与连续 User 消息插桩

在向底层提供者交付消息前，万象通必须对空白内容进行安全补占位符处理，并在连续出现的两条用户消息之间插桩无语义的助手消息，防止底层协议报校验错误。

**专席 Advisor 的独立出站投影例外：**专席的持久原文包含只有 reasoning、没有普通正文的助手回合，或相邻的两条用户载体时，不得给模型捏造 `assistant "."`（避免模型将兼容占位错认为真实历史答复）。仅在专席的提供者请求副本中省略没有正文也没有工具效果的纯 reasoning 助手消息，并合并相邻用户消息及其原始文字；原始会话、reasoning 的持久事实与已经产生的真实助手正文不被修改。普通角色仍受上述非空与交替规则约束；判断专席身份依据持久的 CanonicalRole，而非用户消息文字。

## [012] SessionID 与 CallID 定位唯一性

依据 `SessionID + CallID` 在完整快照中解析原始调用分段时，必须能够唯一确定对应的运行上下文与追踪范围；若匹配出现歧义或无法唯一定位，必须安全失败。

## [013] Stream Sensor 专属识别与单 Run 触发限制

流式传感器仅识别对应分段中的专属标识与模式，普通正文与工具输出均不触发；每个运行周期内至多触发一次，且仅限中断当前子会话的物理尝试。

## [014] 零 Host 源码修改与 Typed Hook Membrane

系统完全基于公开的宿主 Hook 与 SDK 集成，不修改宿主源码。所有挂载 Hook 必须经过万象通统一的 typed membrane：边界将底层异常收敛为中立的类型化代数（execution-failure-policy-001），再解释其完整决策。提供者/模型工具参数未通过已声明 wire/schema 属于 `ProtocolRejection`，原 typed rejection 返回宿主供模型修正，不触发进程 fatal。membrane 禁止通配 catch 后直接重试或退出，禁止按异常文本做字符串匹配路由；未分类物理异常必须 fail closed 并扩展类型代数。

## [015] Tool 文本返回结果有界截断

自定义工具返回的文本结果在进入宿主传输层前必须执行确定性的尾部有界截断，确保超长输出不会导致传输层溢出或阻塞。

## [016] HostEventPort Run 去重与 Sticky 重放

事件端口对同一运行周期的完成事件执行幂等去重，对迟到订阅者提供粘性重放，并在监听器释放后彻底停止投递。run 作用域的 `Failed/Aborted` 与 `Completed` 一样必须保留其 Authority Root 因果身份；新工作单元的订阅者不得重放既有粘性终态，只能观察订阅后的新事件。

## [017] Host 身份提取与 Managed Config 投影适配

万象通负责将宿主原始事件解析为规范的会话与角色身份，并单向将托管配置投影到底层宿主；核侧只认中立身份与配置，宿主适配逻辑不反向生成业务权威。

## [018] Host 源码零 Fork

系统只通过受支持的公开 Hook 与 SDK 无侵入集成。宿主源码修改、补丁、私有模块 import、运行时 monkey patch 与 vendored fork 均不属于合法实现路径，严禁作为能力缺口的补偿方案。

## [019] 宿主物理能力边界与适配层隔离

万象通（宿主适配层）向核侧提供经适配校验的物理交互能力（时序、快照解析与模型请求路由）；核侧业务仅依赖万象通暴露的中立能力合同，不假设未经适配层确认的底层宿主隐式行为。在单次 provider transform 期间，不依赖未提交的试探状态，也不通过跨回调的可变标志传递截断决定。
【程序待改】清退 canary 夹具与环境证明装置（如 `fixtures/*canary*.json`、`tests/support/*canary*.mjs`），将真实宿主能力检验迁入宿主适配层的集成测试，核侧不再保留 canary 宗教叙事。

## [020] 观测不足或多解严格 Fail-Closed

宿主边界在面临任何观察证据不足、查询返回类型化失败、多重冲突或数据不一致的情形时，一律执行安全失败（fail closed），严禁妥协猜测；流逝时间本身不构成业务结论。

## [021] Plugin Load Phase 纯洁性与 Activation 分界

插件加载初始化阶段仅允许执行资源解析、静态校验与 Hook 注册，严禁调用宿主业务接口、执行崩溃恢复或追加业务持久化事实。

## [022] Fatal 前必须完成 exact settlement

万象通 typed membrane 收到 `FatalAfterSettlement` 后，必须先按同一策略决策完成精确的不透明容量围栏结算（capacity fence settlement），并将类型化消息处置交给 `managed-chat-execution` 完成持久化提交；提交结果未知必须显式记录为 unknown。只有所有已持有的所有权均取得已提交或明确未知的结算证据后，方可调用 `FatalProcess`。严禁先退出再依赖 `finally`、宿主清理、会话删除或尽力而为的计数递减收尾。

## [023] 业务承诺只建立在公开 Host contract

万象通与业务系统的交互只能基于受支持的公开 Hook 与 SDK 接口。私有宿主字段、未公开的回调顺序、内部重试计数器、界面文本或渲染时机均不得作为身份认定、准入、开始、终态、容量结算或退出决策的输入；凡无法通过公开接口可靠获取的状态一律视为不存在并安全失败（fail closed）。
【程序待改】废止依赖私有字段或特定宿主版本（如 OpenCode 1.18.29）私有属性的适配逻辑，统一由万象通向核侧暴露标准化语义。

## [024] Hook 策略分级与可选观测隔离

挂载到宿主的各 Hook 按照关键等级（安全性、业务流、不变式、可降级、仅审计）进行静态注册与错误分流。涉及安全性、业务工作流与核心不变式的 Hook 发生失败时必须执行安全失败（fail closed），不得降级为尽力而为（best-effort）；审计与诊断等可选观测失败时仅记录中立诊断日志，不得改变或破坏核心 Hook 的执行结果与既有决策。

## [025] 单一因果诊断与显式脱敏

系统通过 `ReliabilityDiagnostics.CausalDiagnosticRecord` 发布结构化因果记录。可用事实携带精确 logical run、session、physical user message、provider run、participant、role、request kind、state transition、typed failure/retry/fallback、capacity、recovery 与 persistence commitment；不可用事实记录为 `None/null`，严禁猜测。诊断结构不接收 prompt、content、token、credential、cookie 或 path 字段；适配层对允许的自由文本显式脱敏并压成单行。已知类型化失败仅输出紧凑 JSON 且不带调用栈。诊断输出失败不得改变 Hook 结果、准入、重试、恢复、容量结算或持久化事实。

## [026] Host Contract/Runtime 编译分界与单向依赖

宿主子系统的公开 Contract、Runtime 与物理 Adapter 必须保持编译隔离。以下名称标识合同和编译边界，不赋予 compile shard 独立的架构治理身份或 consumer 授权：
- `Host.Session.Contract`（`host-session-contract`）：仅包含会话静止 capability 与纯 `SessionSnapshot` 词汇、端口、定位 decision，不依赖 SDK/HTTP 投影、具体 Host 运行时、进程控制、诊断或 Sphinx MCP。
- `Host.Signal.Contract`（`host-signal-contract`）：仅发布共享终端词汇 `Foundation.TerminalSignal`，无状态且零副作用；不得因终端词汇而传递摘要实现、消息词汇、SDK 类型、工具注册或物理启动配置。
- `Host.Message.Contract`（`host-message-contract`）：独立发布 `Message`/`MessagePart` 消息词汇，不携带 SDK 类型、终端事件或摘要实现。
- `Host.SDK.Types`（`host-opencode-types`）：独立发布 `OpencodeTypes` SDK 数据类型与 `HostModel` 宿主中立模型词汇，不携带消息词汇、终端事件或物理 Host 能力。
- `Host.Event.Envelope`（`host-event-envelope`）：只发布原始宿主 envelope unwrap、event type、session identity 与 message-session identity 的无状态提取逻辑；不得修改输入对象。
- `Host.Message.Codec`（`host-message-codec`）：只发布原始宿主 message part 到 `MessagePart` 的无状态解码；其边界仅服务消息消费者。
- `Host.Fatal.Effect`（`host-fatal-effect`）：进程级退出熔断（`Foundation/FatalProcess`）的窄物理效果边界；其能力注入与结算顺序遵守 [029]。
- `Host.Diagnostics.Runtime`（`host-diagnostics-runtime`）：Hook 策略与因果诊断单向消费 Contract，不得反向侵入应用契约闭包。
- `Host.Signal.Adapter`（`host-signal-adapter`）：宿主信号词汇 `HostSignal`、完整提供者 failure/terminal `HostEventCodec`、信号路由（`HostSignalAdapter`）、物理订阅与事件总线适配器（`TerminalWiring`/`Events`）；按实际需要消费窄 codec contract，不向外部输出完整闭包。
- `Host.Tool.Adapter`（`host-tool-adapter`）：独立拥有 `ToolHostCodec` 与 `ToolHostSurface` 的参数解码、上下文身份配对、SDK schema、工具注册、abort 监听与有界输出接线；工具注册闭包不得获取信号路由或终端总线实现。
- `Host.Session.Runtime`（`host-session-runtime`）：SDK/HTTP 快照投影、进程级静止门禁状态机（`SessionQuiescenceGate`、`QuiescenceSurface`）、消息就地变更与宿主上下文投影，禁止被普通业务契约直接引用。
- `Sphinx.Host.Adapter`（`sphinx-host-adapter`）：OpenCode 的 dispatch 借道端口（`OpenCodeHostPort`，能力仅 `dispatch`），隔离于核心契约之外；不注册原生工具，不注入 Sphinx MCP。

`HostDigest` 属于 `runtime-platform/digest` 的无领域摘要原语，不属于 `Host.Signal.Contract`；摘要计算不使调用方获得宿主消息、SDK、终端或物理适配能力。普通业务契约仅按实际需要依赖窄契约、纯数据或 capability port；严禁传递编译 Sphinx、诊断、消息修改、工具注册、信号订阅或具体宿主运行时。

## [027] Host codec 按语义与 consumer cohort 切片

无状态 `HostEventEnvelope` 是原始宿主 envelope unwrap、event type 与 session/message-session identity 读取的唯一实现；`HostEventCodec` 消费该 contract，禁止复制动态字段解析逻辑。`HostMessageCodec` 形成独立 bounded contract，只服务消息消费方。`Host.Session.Runtime` 不得为 message codec 引用完整 signal adapter；signal adapter 不得反向引用 diagnostics runtime。

## [028] signal subscription 与 optional diagnostic 必须分型

宿主信号订阅必须返回严格类型的 `HostSignalSubscriptionError` 与 `LocalEventHook | EventsListen of HostSignalSubscription` 模式，不得以 `option + string` 表达非法组合。顶层 input 与显式非 null `events` 载体必须是普通记录；`client` 是不透明 SDK capability，缺少 `events` 成员时回退为 `LocalEventHook`。输入格式错误一律收敛为 `InvalidInput`；`events.listen` 异常收敛为 `EventsListenFailed`，Surface promise 必须 resolve 类型化错误而非 reject。`EventsListen` 必须携带唯一 disposable 所有者；监听器注册失败即刻报错，禁止延迟到清理时爆发。万象通适配器只报告类型化错误，`HostSignalBootstrap` 是唯一退出决策者。循环诊断经必填窄 capability 注入，其失败不得改变主流程执行。

## [029] fatal process 是唯一注入的 physical adapter

`FatalProcessPort` 契约只包含不可变事件词汇与 capability 类型，不包含物理工厂或 Node 导入。唯一 fatal adapter 拥有该路径的 `console.error`、`process.kill` 与 `process.exit`；所有调用方由 composition 注入 capability，普通契约与适配器不得直接引用物理实现。退出前必须由持有所有权的模块完成类型化结算证据；同一事件仅允许一次上报与一次终止。

发生未恢复致命异常时，系统触发子进程确定性硬退出（通过 signal 或 `process.exit(1)`），且此前提交的持久化事实必须安全留存。退出熔断机制保证同一故障事件仅上报并终止一次，控制台输出异常或标准流关闭不得绕过终止逻辑。非致命拒绝与正常耗尽路径正常退出（code 0）。

## [030] raw Host membrane 只接受精确 JavaScript 类型

布尔标记只接受原生 `true | false`；字符串、数字、对象均不得通过隐式真假值（truthiness）转化为 compaction、synthetic 或 abort 标记。parts 只接受真实 Array，其他值安全投影为空且 Hook 不得抛出未捕获异常。事件与响应中的 SessionId、parentID、agent 只接受非空原始字符串。所有动态读取逻辑在 Fable/JS 边界执行显式类型判定，正确性不得依赖运行时异常捕获或偶发拒绝。

## [031] Root workspace first-bind effect 隔离

Root workspace 是进程局部的宿主资源定位结果，非公开可变状态。私有宿主运行时仅在当前值为 `None` 且候选为非空路径时完成首次绑定；空值与纯空白不占用绑定，首次绑定后任意后续候选均不得改写结果。Host composition 是绑定的唯一生产消费方，其余生产路径只能消费显式注入的只读 capability。不得从 Git 隐式推导工作区根路径，亦不得由消费方自行重算或读取全局状态。

## [032] 不注入评审保证书参数

`todowrite-for-manager` 的工具定义不得追加 `contract` 参数、固定 token 或必填签署字段。评审接纳前后的定义保持一致；是否允许执行只由当前 Manager 能力事实决定，不依赖模型签署保证。

工具入参中的 `contract` 仅作为对模型的提示字段；万象通在工具调用执行前（`before` 阶段）对参数进行私有暂存，并在执行完成后（`after` 阶段）原样恢复原始键序与对象属性，使模型上下文与 wire 层历史准确保留原始入参，异常退出路径同样保证同源恢复。参数暂存机制仅按字段所有权处理提示字段 `contract`，不为工具定义追加估计轮次或短记字段。
【程序待改】清理对 OpenCode 1.18.29 特定 canary 夹具与时序的硬编码依赖，由万象通统一提供参数暂存与恢复保证。

## [033] 读取端 hook 的 exact 只读租约校验

`chat.params`、提供者步骤门禁与 attempt 生命周期一律以精确 `(SessionId, PhysicalUserMessageId)` 读取容量所有者已提交的租约完成校验：查询不得调用 scheduler、不得发放 fence、不得建立或回填第二份绑定状态。同一物理消息的重复观察幂等，拒绝原因保持一致；交错消息各自校验自身物理 ID，互不串读。受管输入缺少已提交租约时 fail-closed 拒绝且宿主输出不被改写，不得回退到 session-current 绑定副本，亦不得将观察升级为受管租约；未绑定辅助会话保持豁免。attempt plan 冻结与 `ProviderStarted` 持久化按精确键查验已提交的 Accepted 执行，缺失即明确拒绝。

活跃 run 的用户消息在宿主保存前不得撤销旧租约。provider transform 选择可见新输入时，先经 managed-chat-execution-003 的独立准入操作验证精确 Accepted、同 run 与旧租约，交接原容量并提交新租约，随后执行只读门禁。不得将任意缺失租约视为追加材料或从当前会话猜测身份。

## [034] 宿主适配器薄度与中立端口分层

万象通适配器文件（`src/Wanxiangshu` 下的 `OpenCode/`、`Omp/` 适配面）不得自行承担产品业务决策，不持有第二业务账本，亦不控制核心外部生命周期。纯粹的命名空间引用数量不作为硬性计数棘轮。

核侧的中立端口（`src/Wanxiangshu/Host/Ports/`）与领域模型（`src/Wanxiangshu/Host/Domain/`）保持宿主中立：中立端口签名只声明纯抽象接口与数据类型，严禁直接绑定特定宿主 SDK（如 OpenCode、omp）的私有客户端或句柄。物理接入统一由适配器装配注入。
【程序待改】废止端口签名中的 `HNI P-xx` 仪式性标记与硬编码门禁，简化架构分层检查。

## [035] omp 入口无产品决策接线

omp 入口模块位于 `src/Wanxiangshu/Composition/Omp/`，负责将产品接入 omp。宿主翻译的目标归属是万象通；现有代码尚未迁入，程序待改。业务状态及规则应由其实际主人持有，不靠扫描关键词或控制流判断职责。

## [036] 宿主功能性角色与自动 compaction 隔离契约

宿主内置的功能性代理（title、compaction、summary、explore、generate）服务于宿主自身的生命周期与元数据维护，不隶属于万象术受托治理的核心角色（manager、orchestrator、engineer、devops、plan、advisor）：

1. **请求级系统所有权与视图隔离**：宿主底层可能存在不携带当前代理标识的 Hook 调用；万象通负责将非受托的功能性请求与受托角色请求隔离开来，仅以受托角色提示词前缀开头的请求才进入核心 system 改写与出站快照。功能性请求原样保留，禁止通过宿主提示词片段或会话历史猜测当前角色；
2. **历史会话压缩服从 as-is 语义**：压缩钩子授权仅在当次上下文转换中旁路处理，宿主存活上下文传入什么内容即原样提供给压缩模型，不回溯已被裁切的早期历史，亦不注入预算后缀；
3. **宿主自动压缩策略保留**：万象术不物理禁用宿主原生自动压缩，允许宿主自发触发安全压缩；该压缩不推进 Scope epoch、不扣减计费估计、不更改在途委托；
4. **准入与执行旁路**：功能性角色在消息处理中不受受管租约约束，工具不进行强制屏蔽，允许其直接执行；
5. **参数隔离**：非受管请求不结算 Scope 进度、不触发 Advisor、不覆盖模型快照亦不记录受管模型容量；
6. **共享历史中的作者身份**：系统在请求副本追加的提示在出站时必须标记为合成消息（`synthetic=true`），避免宿主将系统提醒误判为真实用户输入而静默取消后台自动标题。

真机事实：在开启插件的会话中，未标记为合成消息的系统提示会污染用户消息计数导致宿主后台跳过自动标题；标记为合成消息后宿主正常发出标题请求并持久化更新。
【程序待改】万象通适配层应进一步抽象宿主原生功能性请求的过滤机制，避免在核侧保留针对具体 OpenCode 版本的特定提示词开头嗅探逻辑。
