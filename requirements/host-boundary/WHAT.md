# host-boundary — WHAT

## HOST-BOUNDARY-001: 业务层不消费流式碎片事件

业务逻辑严禁直接消费流式碎片事件（如 `message.updated`、`part.delta` 等）。合法交互路径仅允许由最早边界过滤碎片，转化为粗粒度的唤醒信号，随后读取完整的 SDK 快照作为唯一的业务事实来源。

## HOST-BOUNDARY-002: 业务层信号闭集与精准分型

进入业务层的宿主信号严格限于类型化闭集（`SessionIdle`、`ProviderRetry`、`ProviderFailure`、`SessionDeleted` 与 `AttemptAborted`）。中止错误必须解码为专用的物理中断唤醒，绝不得与提供者失败混淆。

## HOST-BOUNDARY-003: 传输与领域分离且信号仅作唤醒

宿主信号仅作为单向唤醒触发器，不得作为业务事实载体。信号内携带的尝试次数仅供诊断参考，不得直接作为领域的重试或回退计数。coarse `AttemptAborted(SessionId)` 只能撤销当前物理 attempt 的 quiescence capability 并唤醒 reconciler；它没有 exact `PhysicalUserMessageId`，不得把该 session 的全部 current chat execution 投影为 `SessionAborted`，尤其不得终结已被更新 user message 接纳的新 execution。

## HOST-BOUNDARY-004: TurnUnknown 为对齐私有观测而非业务结局

快照中未决的中间状态归类为调和器私有的 `TurnUnknown` 观测，严禁跨越调和边界发布为公开的业务完成终态，防止产生虚假的完成或缺失报告。

## HOST-BOUNDARY-005: Reconciler 单飞快照观测与事件驱动收敛

调和器对每个会话严格保证单飞执行（single-flight），接收到粗粒度信号时执行单次完整快照读取。在无新信号或明确投影边缘时，严禁通过墙钟轮询重复读取快照。Provider failure 的 exact assistant `message.updated` 即使尚无 retry owner 的 terminal disposition，也必须用自身 exact physical identity 发出 typed failure wake；同一 physical 随后的 idle/retry wake 或无 identity 的 coarse failure 不得覆盖它，新 physical 或显式 abort 才能替换它。无 current physical binding 的 coarse failure 不得读取快照或发布 terminal turn，只能等待 exact projection evidence。Exact failure 不得伪装成 managed-chat terminal，也不得因 coarse `session.error` 缺席或 disposition 缺失被丢弃。

## HOST-BOUNDARY-006: Raw Part 与 ToolParts 状态投影一致性

工具调用的原始分段状态与业务分段状态必须保持严格一致：未完成状态映射为挂起调用，已完成或失败状态映射为调用结果，严禁出现状态分叉投影。

## HOST-BOUNDARY-007: Compaction 观测门禁之预防与收容

宿主压缩控制实行双层防护：启动前严格校验并关闭自动压缩配置，首轮调用若产生非预期压缩则直接拒绝启动；运行时若观测到压缩事实，必须立即触发原子上下文重锚定。

## HOST-BOUNDARY-008: Transform 到 ProviderRunIdentity 因果读与唯一性

从快照提取运行身份必须基于严格的因果规则（角色、完成时间、父节点匹配与最大序列）；当且仅当命中唯一候选时方可确认绑定，命中 0 个或多个候选时一律安全失败。`experimental.chat.messages.transform` 属于 provider inference 之前的物理边界，因此该 hook 不得把“当前 assistant run 已经存在”作为业务前置条件，也不得通过 bounded wait 把未来 run 伪装成 projection lag；需要在该 hook 冻结的 recovery 决策必须先以 exact `PhysicalUserMessageId` 保存为未绑定 attempt plan，同一 physical 的重复 transform 只能复用首次冻结值，不得重算后以冲突终止；待后续完整 Host 观测出现 exact `ProviderRunIdentity` 后再一次性绑定。

## HOST-BOUNDARY-009: Tool 身份双半边与缺失 Fail-Closed

工具执行上下文必须同时完整具备消息 ID 与调用 ID 两个半边身份；任一半边缺失直接安全失败，严禁跨上下文推测配对。

## HOST-BOUNDARY-010: 多实例边界与共享注册表访问纪律

跨工作区实例间仅共享只读或受限的全局身份注册表，且注册表访问不得跨越异步等待点；各实例的持久化日志写入器与状态缓存完全隔离，严禁共享写入通道。

## HOST-BOUNDARY-011: 空 Content 预防与连续 User 消息插桩

在向底层提供者交付消息前，必须对空白内容进行安全补占位符处理，并在连续出现的两条用户消息之间插桩无语义的助手消息，防止底层协议报校验错误。

## HOST-BOUNDARY-012: SessionID 与 CallID 定位唯一性

依据 `SessionID + CallID` 在完整快照中解析原始调用分段时，必须证明其能唯一确定对应的运行上下文与追踪范围；若匹配出现歧义或无法定位，必须安全失败。

## HOST-BOUNDARY-013: Stream Sensor 专属识别与单 Run 触发限制

流式传感器（如 LoopSensor）仅识别对应分段中的专属标识与模式，普通正文与工具输出均不触发；每个运行周期内至多触发一次，且仅限中断当前子会话的物理尝试。

## HOST-BOUNDARY-014: 零 Host 源码修改与 Typed Hook Membrane

系统完全基于公开的宿主 Hook 与 SDK 集成，不修改宿主源码。所有挂载 Hook 必须经过同一个 typed membrane：边界用公开 evidence 将失败穷尽归一为 `execution-failure-policy` 的 closed algebra，再解释其完整 decision。Provider/LLM 工具参数未通过已声明 wire/schema 属于 `ProtocolRejection`，原 typed rejection 返回 Host 供 provider 修正，不触发 fatal。membrane 禁止 wildcard catch 后直接 retry/fatal，禁止按 exception/error text 路由；未分类物理形状必须 fail closed 并扩展代数。

## HOST-BOUNDARY-015: Tool 文本返回结果有界截断

自定义工具返回的文本结果在进入宿主传输层前必须执行确定性的尾部有界截断，确保超长输出不会导致传输层溢出或阻塞。

## HOST-BOUNDARY-016: HostEventPort Run 去重与 Sticky 重放

事件端口对同一运行周期的完成事件执行幂等去重，对迟到订阅者提供粘性重放，并在监听器释放后彻底停止投递。run-scoped `Failed/Aborted` 与 `Completed` 一样必须保留其 Authority Root causal identity；future-only subscriber 不得重放既有 sticky terminal，供新 work unit 使用时只能观察订阅后的新事件。

## HOST-BOUNDARY-017: Host 身份提取与 Managed Config 投影适配

宿主边界负责将原始事件解析为规范的会话与角色身份，并单向将托管配置投影到底层宿主，宿主适配逻辑不反向生成业务权威。

## HOST-BOUNDARY-018: Host 源码零 Fork

系统只通过受支持的公开 Hook 与 SDK 无侵入集成。Host 源码修改、补丁、私有模块 import、运行时 monkey patch 与 vendored fork 均不属于合法实现路径，严禁作为能力缺口的补偿方案。

## HOST-BOUNDARY-019: Host 物理能力缺口必有 Canary 与 Contract 证明

业务所依赖的全部宿主物理能力（包括时序、快照解析、模型路由等）必须同时具备契约测试与真实 canary：canary 必须启动受支持的真实 Host build，经公开 Hook/SDK 发起实际场景并观察公开结果；mock adapter、源码/类型形状检查、伪造 callback 与 UI 截图均不算 canary。缺少任一级证明即判定环境不支持。一次 provider transform 内若 XWire 选择未提交的 prefix probe，必须以 typed `PrefixPresentationHorizon.TentativeCold` 直接返回给同一静态组合根；组合根据此在当前调用中抑制会重放旧历史 horizon 的后置 auxiliary projectors。该事实不得通过跨 callback mutable registry/flag 传播。

## HOST-BOUNDARY-020: 观测不足或多解严格 Fail-Closed

宿主边界在面临任何观察证据不足、查询返回 typed failure、多重冲突或数据不一致的情形时，一律执行安全失败（fail closed），严禁妥协猜测；elapsed time 本身不构成业务结论。

## HOST-BOUNDARY-021: Plugin Load Phase 纯洁性与 Activation 分界

插件加载初始化阶段仅允许执行资源解析、静态校验与 Hook 注册，严禁调用宿主业务接口、执行崩溃恢复或追加业务持久化事实。

## HOST-BOUNDARY-022: Fatal 前必须完成 exact settlement

Typed hook membrane 收到 `FatalAfterSettlement` 后，必须先按同一个 policy decision 完成 exact opaque capacity fence settlement，并把 typed message disposition 交给 `managed-chat-execution` durable 提交；提交未知必须写成显式 unknown。只有所有已持有 ownership 均取得 committed/unknown settlement evidence 后才可调用 `FatalProcess`。严禁先退出再依赖 `finally`、Host cleanup、session deletion、UI 提示或 best-effort count decrement 收尾。

## HOST-BOUNDARY-023: 业务承诺只建立在公开 Host contract

Requirement、领域状态与恢复策略只能依赖受支持的公开 Hook/SDK 输入输出及真实 canary 已证明的物理行为。private Host field/module、未公开 callback ordering、内部 retry counter、DOM/UI text、toast、spinner 或渲染时机均不得成为 identity、acceptance、provider start、terminal、capacity 或 fatal settlement 的证明；无法由公开 contract 观察的能力视为不存在并 fail closed。

## HOST-BOUNDARY-024: Hook Policy 闭集与可选观测隔离

每个 live Host Hook 必须且只能对应一行 closed metadata，声明 `Security | Workflow | Invariant | Degradable | AuditOnly` criticality、允许的 context/effect、retry permission、capacity owner 与 failure disposition。composition root 按该 closed score 显式静态注册，固定次序不得由 list iteration、动态 middleware 或 service locator 隐藏。Hook identity 权限至多只读，admission 权限至多进入唯一 owned gate；不存在 identity mutation 或 admission bypass。`Security`、`Workflow`、`Invariant` 失败始终经 `policyAwareHook` fail closed，不能降级为 best effort。已证明可选的 Casebook/audit observation 必须置于 typed best-effort boundary；其失败只发送现有 diagnostic，不能改变已完成的 critical Hook result。

## HOST-BOUNDARY-025: 单一因果诊断与显式脱敏

Host 只通过 `ReliabilityDiagnostics.CausalDiagnosticRecord` 发布结构化因果记录。可用事实携带 exact logical run、session、physical user message、provider run、agent、role、request kind、state transition、typed failure/retry/fallback、capacity、recovery 与 persistence commitment；不可用事实必须为 `None/null`，严禁猜测。schema 不接收 prompt、content、token、credential、cookie 或 path 字段；adapter 对允许的自由文本显式脱敏并压成单行。known typed failure 只输出一行 JSON 且无 stack。diagnostic emit/counter/query 失败不得改变 Hook result、admission、retry、recovery、capacity settlement 或 durable fact。

## HOST-BOUNDARY-026: Host Contract/Runtime 编译分界与单向依赖

Host 边界严格切分为 Contract、Runtime 与 Adapter 架构 locality：
- `Host.Session.Contract`（`host-session-contract`）：仅包含会话静止 capability 与纯 `SessionSnapshot` 词汇、端口、定位 decision，不依赖 SDK/HTTP 投影、具体 Host 运行时、进程控制、诊断或 Sphinx MCP。
- `Host.Signal.Contract`（`host-signal-contract`）：包含规范的宿主唤醒信号与编解码器词汇表，无状态且零副作用。
- `Host.Diagnostics.Runtime`（`host-diagnostics-runtime`）：封闭 Hook 策略元数据与因果诊断单向消费 Contract，不得反向侵入应用契约闭包。
- `Host.Signal.Adapter`（`host-signal-adapter`）：宿主信号路由、物理订阅与事件总线适配器，仅作为 Adapter 实现契约。
- `Host.Session.Runtime`（`host-session-runtime`）：SDK/HTTP 快照投影、进程级静止门禁状态机（`SessionQuiescenceGate`、`QuiescenceSurface`）、消息就地变更与宿主上下文投影，禁止被普通业务契约直接引用。
- `Sphinx.Host.Adapter`（`sphinx-host-adapter`）：外部 Sphinx MCP 启动配置与环境适配器，隔离于核心契约之外。

普通业务契约仅允许引用 `Host.Session.Contract` 或 `Host.Signal.Contract`，严禁传递编译 Sphinx、诊断、消息就地修改或具体 Host 运行时。
