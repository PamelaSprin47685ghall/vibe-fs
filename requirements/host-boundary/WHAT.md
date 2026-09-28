# host-boundary — WHAT

## [001] 业务层不消费流式碎片事件

业务逻辑严禁直接消费流式碎片事件（如 `message.updated`、`part.delta` 等）。合法交互路径仅允许由最早边界过滤碎片，转化为粗粒度的唤醒信号，随后读取完整的 SDK 快照作为唯一的业务事实来源。

## [002] 业务层信号闭集与精准分型

进入业务层的宿主信号严格限于类型化闭集（`SessionIdle`、`ProviderRetry`、`ProviderFailure`、`SessionDeleted` 与 `AttemptAborted`）。中止错误必须解码为专用的物理中断唤醒，绝不得与提供者失败混淆。

## [003] 传输与领域分离且信号仅作唤醒

宿主信号仅作为单向唤醒触发器，不得作为业务事实载体。信号内携带的尝试次数仅供诊断参考，不得直接作为领域的重试或回退计数。coarse `AttemptAborted(SessionId)` 只能撤销当前物理 attempt 的 quiescence capability 并唤醒 reconciler；它没有 exact `PhysicalUserMessageId`，不得把该 session 的全部 current chat execution 投影为 `SessionAborted`，尤其不得终结已被更新 user message 接纳的新 execution。

## [004] TurnUnknown 为对齐私有观测而非业务结局

快照中未决的中间状态归类为调和器私有的 `TurnUnknown` 观测，严禁跨越调和边界发布为公开的业务完成终态，防止产生虚假的完成或缺失报告。

## [005] Reconciler 单飞快照观测与事件驱动收敛

每个会话的快照观测必须单飞。信号触发一次完整读取，后续读取需要新信号或明确投影变化，不得墙钟轮询。

带精确物理身份的 assistant failure 必须产生类型化唤醒，即使尚无 retry owner 的终局裁决或 coarse `session.error`。只有身份匹配的 failure witness 才能发布 `TurnFailed`，不得把它直接当成 managed-chat terminal；先到的 idle/retry 等待证据，不能终结裸错误。同一 physical 后续的 idle/retry 唤醒和无身份的粗失败信号均不得覆盖精确 witness，新 physical 或显式 abort 才能替换。没有当前 physical binding 的 coarse failure 不读取快照、不发布终态。

## [006] Raw Part 与 ToolParts 状态投影一致性

工具调用的原始分段状态与业务分段状态必须保持严格一致：未完成状态映射为挂起调用，已完成或失败状态映射为调用结果，严禁出现状态分叉投影。

## [007] Compaction 观测门禁之预防与收容

宿主压缩控制实行双层防护：启动前严格校验并关闭自动压缩配置，首轮调用若产生非预期压缩则直接拒绝启动；运行时若观测到压缩事实，必须立即触发原子上下文重锚定。

## [008] Transform 到 ProviderRunIdentity 因果读与唯一性

运行身份按角色、完成时间、父节点和最大序列从完整快照中唯一定位；零个或多个候选均安全失败。

`experimental.chat.messages.transform` 发生在 provider inference 之前，不得要求当前 assistant run 已存在，或通过等待把未来 run 当作投影延迟。此时的 recovery 决策按精确 `PhysicalUserMessageId` 冻结为未绑定计划；重复 transform 复用首次值，不重算。后续完整观察出现匹配的 `ProviderRunIdentity` 时再绑定一次。

## [009] Tool 身份双半边与缺失 Fail-Closed

工具执行上下文必须同时完整具备消息 ID 与调用 ID 两个半边身份；任一半边缺失直接安全失败，严禁跨上下文推测配对。

## [010] 多实例边界与共享注册表访问纪律

跨工作区实例间仅共享只读或受限的全局身份注册表，且注册表访问不得跨越异步等待点；各实例的持久化日志写入器与状态缓存完全隔离，严禁共享写入通道。

## [011] 空 Content 预防与连续 User 消息插桩

在向底层提供者交付消息前，必须对空白内容进行安全补占位符处理，并在连续出现的两条用户消息之间插桩无语义的助手消息，防止底层协议报校验错误。

## [012] SessionID 与 CallID 定位唯一性

依据 `SessionID + CallID` 在完整快照中解析原始调用分段时，必须证明其能唯一确定对应的运行上下文与追踪范围；若匹配出现歧义或无法定位，必须安全失败。

## [013] Stream Sensor 专属识别与单 Run 触发限制

流式传感器（如 LoopSensor）仅识别对应分段中的专属标识与模式，普通正文与工具输出均不触发；每个运行周期内至多触发一次，且仅限中断当前子会话的物理尝试。

## [014] Typed Hook Membrane

所有挂载 Hook 必须经过同一个 typed membrane：边界用公开 evidence 将失败穷尽归一为 `execution-failure-policy` 的 closed algebra，再解释其完整 decision。Provider/LLM 工具参数未通过已声明 wire/schema 属于 `ProtocolRejection`，原 typed rejection 返回 Host 供 provider 修正，不触发 fatal。membrane 禁止 wildcard catch 后直接 retry/fatal，禁止按 exception/error text 路由；未分类物理形状必须 fail closed 并扩展代数。

## [015] Tool 文本返回结果有界截断

自定义工具文本在进入 Host 传输层前按宿主限制确定性截断：添加固定截断标记，优先保留最新的完整尾部行，最终结果不超过宿主限制且不再被二次截断。

## [016] HostEventPort Run 去重与 Sticky 重放

事件端口对同一运行周期的完成事件执行幂等去重，对迟到订阅者提供粘性重放，并在监听器释放后彻底停止投递。run-scoped `Failed/Aborted` 与 `Completed` 一样必须保留其 Authority Root causal identity；future-only subscriber 不得重放既有 sticky terminal，供新 work unit 使用时只能观察订阅后的新事件。

## [017] Host 身份提取与 Managed Config 投影适配

宿主边界负责将原始事件解析为规范的会话与角色身份，并单向将托管配置投影到底层宿主，宿主适配逻辑不反向生成业务权威。

## [018] Host 源码零 Fork

系统只通过受支持的公开 Hook 与 SDK 无侵入集成。Host 源码修改、补丁、私有模块 import、运行时 monkey patch 与 vendored fork 均不属于合法实现路径，严禁作为能力缺口的补偿方案。

## [019] Host 物理能力缺口必有 Canary 与 Contract 证明

每项依赖的 Host 物理能力同时具备契约测试与真实 canary：启动受支持的 Host build，经公开 Hook/SDK 运行场景并观察公开结果。模拟适配器、源码/类型检查、伪造 callback 和 UI 截图均不替代 canary；缺任一级证明即视为环境不支持。

单次 provider transform 选择未提交的 prefix probe 时，必须直接向同一静态组合根返回类型化 `TentativeCold`，在本次调用内抑制会重放旧历史 horizon 的后置辅助投影；不通过跨 callback 可变注册表或标志传播。

## [020] 观测不足或多解严格 Fail-Closed

宿主边界在面临任何观察证据不足、查询返回 typed failure、多重冲突或数据不一致的情形时，一律执行安全失败（fail closed），严禁妥协猜测；elapsed time 本身不构成业务结论。

## [021] Plugin Load Phase 纯洁性与 Activation 分界

插件加载初始化阶段仅允许执行资源解析、静态校验与 Hook 注册，严禁调用宿主业务接口、执行崩溃恢复或追加业务持久化事实。

## [022] Fatal 前必须完成 exact settlement

Typed hook membrane 收到 `FatalAfterSettlement` 后，必须先按同一个 policy decision 完成 exact opaque capacity fence settlement，并把 typed message disposition 交给 `managed-chat-execution` durable 提交；提交未知必须写成显式 unknown。只有所有已持有 ownership 均取得 committed/unknown settlement evidence 后才可调用 `FatalProcess`。严禁先退出再依赖 `finally`、Host cleanup、session deletion、UI 提示或 best-effort count decrement 收尾。

## [023] 业务承诺只建立在公开 Host contract

Requirement、领域状态与恢复策略只能依赖受支持的公开 Hook/SDK 输入输出及真实 canary 已证明的物理行为。private Host field/module、未公开 callback ordering、内部 retry counter、DOM/UI text、toast、spinner 或渲染时机均不得成为 identity、acceptance、provider start、terminal、capacity 或 fatal settlement 的证明；无法由公开 contract 观察的能力视为不存在并 fail closed。

## [024] Hook Policy 闭集与可选观测隔离

每个活动 Hook 恰对应一行封闭策略，声明 `Security | Workflow | Invariant | Degradable | AuditOnly` 类别、允许的 context/effect、重试权、capacity owner 和失败处置。composition 显式静态注册，固定顺序不藏在列表迭代、动态 middleware 或 service locator 中。

Hook 对身份至多只读，准入只能进入唯一所有者的 gate，不得修改身份或绕过准入。前三类失败统一通过类型化边界安全失败，不得降级为 best effort。已证明可选的观察使用独立的类型化 best-effort 边界，其失败只报告诊断，不改变已完成的关键 Hook 结果。

## [025] 单一因果诊断与显式脱敏

Host 因果诊断采用唯一结构：记录可用的精确 logical run、session、physical user message、provider run、participant、role、request kind、状态转换、类型化 failure/retry/fallback、capacity、recovery 与 persistence commitment；不可用值为 `None/null`，不得猜测。

schema 不接收 prompt、content、token、credential、cookie 或 path；允许的自由文本显式脱敏并压成单行。已知类型化失败只输出一行 JSON、不附 stack。诊断输出、计数或查询失败均不改变 Hook 结果、准入、重试、恢复、capacity 结算或持久事实。

## [026] Host Contract/Runtime 编译分界与单向依赖

公开合同、运行时和物理适配器必须编译隔离。会话快照与静止能力、终端事件、消息词汇、SDK 数据类型分别形成窄合同。终端、消息与 SDK 数据合同互不夹带彼此词汇；会话合同不依赖 SDK/HTTP 投影。合同不夹带摘要实现、可变状态或物理能力；业务合同只能依赖所需纯数据、decision 和 capability 声明，不得传递引入 Host 运行时、诊断、进程控制、消息修改、工具注册、信号订阅或 Sphinx。

事件外壳与消息解码无状态且不修改输入；解码边界遵守 [027]。信号适配器拥有订阅和路由，工具适配器拥有参数解码、身份配对、schema、注册、中止监听和输出限界；两侧不得相互夹带实现，同时使用时由 composition 显式装配。诊断单向依赖合同；fatal 效果及其注入遵守 [029]。

摘要原语不授予消息、SDK、终端或物理适配能力。启动配置只属于对应适配器；原生 `/sphinx question` 配置与核心合同隔离，不启动或注入 Sphinx MCP。编译分片或合同引用本身不构成能力授权，物理效果只能通过 composition 注入的窄 capability 取得。

## [027] Host codec 按语义与consumer cohort切片

事件外壳解包、事件类型及会话身份读取共用唯一无状态解析，不得各自复制。消息分段解码与循环文本增量解码各有独立合同，只向实际消费者提供所需语义。

会话运行时不得为消息解码引入完整信号适配器；循环运行时不得取得完整 failure/terminal 解码或诊断实现；信号适配器不得反向依赖诊断运行时。

## [028] signal subscription 与optional diagnostic 必须分型

订阅以封闭错误类型和 `LocalEventHook | EventsListen` 互斥结果返回，不用 option 与 string 拼接状态。顶层输入及显式非 null 的 events 必须是纯对象；client 是不透明 SDK 对象，可为纯对象或类实例。没有 legacy events 时返回 `LocalEventHook`，不制造 disposer。

primitive、数组、boxed scalar、Date 和损坏的 events 均为 `InvalidInput`；坏 direct events 不能借合法 client 绕过。顶层或 client 的 getter/Proxy 抛错归 `InvalidInput`，读取或调用 listen 抛错归 `EventsListenFailed`；Promise 返回类型化错误，不直接拒绝。listen 和 disposer 缺失或非函数时立即返回相应错误。

`EventsListen` 恰有一个不透明 disposable owner，合法 disposer 自身异常原样交给该 owner。JS 解码错误在入口收敛，不污染领域类型。订阅适配器只报告类型化失败，不获得诊断或时间实现；Host composition 中的唯一订阅所有者将订阅失败解释为 fatal。Loop 诊断经必填窄 capability 注入，失败只产生非权威诊断，不改变 arm、interrupt、consume 或 continuation。

## [029] fatal process 是唯一注入的physical adapter

fatal 合同只含不可变 incident 词汇与 capability 类型，不含物理值、工厂或 Node 导入。唯一适配器拥有报告和进程 kill/exit；调用者从 composition 获得必填 capability，普通合同、运行时和适配器不直接引用物理实现。调用者提供前置结算的类型化证据，同一 incident 至多报告和终止一次。

退出由真实子进程经实际终止和致命诊断入口证明：父进程观察硬退出，按平台验证 signal 或 code 1 回退，并重开已提交的 store 验证事实幸存。报告器抛错、stdio 关闭和重复 incident 不得绕过终止或重复报告；正常拒绝、stale callback 和修复耗尽路径 exit 0。测试开关不替代物理证据，不得为运行 finally 弱化硬退出。

## [030] raw Host membrane 只接受精确 JavaScript 类型

布尔 marker 只接受 primitive true/false，不把 truthy 值当作 compaction、synthetic 或 abort。parts 只接受真实 Array，其他值投影为空且不使 Hook 抛错。session event 与 `session.get` 的 SessionId、parentID、agent 只接受非空白 primitive string，不经字符串强制转换制造身份。

动态值在 Fable 边界显式校验 JavaScript 类型，不依赖 unbox、异常捕获或下游函数偶然拒绝。

## [031] Root workspace first-bind effect隔离

Root workspace 是进程内 Host 资源定位结果：尚未绑定时只接受非空白路径，空值不占用绑定，绑定后不再改写。绑定能力只交给 Host composition，其余生产调用者使用显式注入的只读 capability；不得从 Git 推导 family root、各自重算或直接读取全局可变值。

## [032] Contract 提示字段解耦与参数清理安全

工具入参 `contract` 只作 provider 提示，缺失或错误不构成本地再次校验的拒绝理由。参数清理使用私有暂存，并在 `after` 及异常退出路径同源恢复；历史调用与上下文不得因此改写。恢复后的参数值、对象身份及原始键顺序须由真实 Host canary 证明。
