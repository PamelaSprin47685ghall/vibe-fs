# host-boundary — HOW

## 架构机制

### 1. 信号适配与碎片过滤

宿主事件编解码器（`HostEventCodec`）在边界拦截所有流式碎片（如 `part.delta`、`message.updated`）。普通碎片不离开边界；粗粒度会话条件转化为 `HostSignal`，exact assistant lifecycle 字段转化为只供 provider-start/terminal accounting 使用的 typed physical observation，绝不成为业务 `HostSignal`：
- `SessionIdle` 与 `ProviderRetry`：触发单飞调和流程。
- `AttemptAborted`：撤销当前物理尝试的静止能力，不推进业务重试。
- `ProviderFailure`：结合快照中的确切助手消息共同确认失败终态。
- `signals.test.mjs` 直接穿过 `HostSignalSurface` 固定五 case 的完整 JSON 投影、无关输入拒绝与 wrapper wire 形态。abort proof 同场比较 owned/unowned `AttemptAborted` 与可跨 ownership 的 `ProviderFailure`；任何混型都会改变可见路由结果并使测试失败。

### 2. 快照投影与身份因果解析

- **SessionSnapshotPort**：把 SDK/HTTP wire 投影为 `SessionSnapshot` 契约词汇，不拥有定位规则。
- **严格定位证明**：`SessionSnapshotSurface.locateToolCall` 直接调用纯 `SessionSnapshot.locateToolCall`。`session-snapshot-locality.test.mjs` 同时固定唯一目标 + 非目标 decoy、目标缺失、目标多解三个世界；只允许唯一目标返回运行上下文，后两者分别返回 typed `Missing` / `Ambiguous`，禁止 first-match 猜测。
- **最大序列**：`SessionSnapshotPort` 只接受 finite Host `time.created`；latest assistant 按 `(time.created, id)` 排序，ID 仅作同时间的确定性 tie-break。任一 assistant 缺少合法 creation evidence 时绑定 fail closed。
- **因果绑定**：公开 `message.updated.properties.info` 同时携带 exact `sessionID`、assistant `id`、exact user `parentID`、`role=assistant` 与 `time.created` 时，边界才发布 exact provider-start observation；任一字段缺失或不匹配即安全失败。
- **Pre-run transform 边界**：公开顺序为 `chat.message → experimental.chat.messages.transform(user only) → chat.params(可重复) → provider`。Transform 只按 exact `(SessionId, PhysicalUserMessageId)` 冻结 pending attempt plan，绝不建立 `ProviderRunIdentity` 或写 `ProviderStarted`。首次 exact assistant observation 一次性绑定 plan 并先持久 `ProviderStarted`；同一事件若还携带 terminal evidence，只有 start 持久确认后才进入 terminal accounting。
- **两半边身份守门**：`ToolHostCodec` 必须在上下文内同时获取消息 ID 与调用 ID，否则拒绝执行。
- **Schema 表示归属**：`HostSchema` 保持私有构造，`ToolHostCodec.schemaValue` 在同一 adapter shard 内通过 typed pattern match 返回原生 SDK schema；`ToolHostSurface` 不读取 Fable 布局，也不把 schema 自身的 `.value` 当成额外包装。`tool-host-codec-full.test.mjs` 用公开 `@opencode-ai/plugin/tool` 的真实 validator 验证接受、拒绝与 optionality；原生 literal schema 的反例在旧实现报 `schema.parse is not a function`，typed 解包后保持正常验证。该证明属于 verification-system-008，不再用虚构 schema 形状证明 provider-projection-005。

### 3. 调和器调度与事件驱动收敛

`Scheduler` 维护会话的单飞调和生命周期：
- 单一会话至多并发一次调和，避免并发快照冲突。
- 依赖因果事件驱动推进，快照未决时保持挂起，杜绝全局墙钟轮询。
- 维护物理执行租约与完成状态，确保失败步骤与最终执行完成明确区分。

### 4. Typed Hook Membrane、结算与加载纯洁性

- **一次 typed normalization**：所有交付 Host 的 Hook 先从公开 Hook/SDK evidence 归一为 `ExecutionFailure`；wire/schema rejection 直接形成 `ProtocolRejection`。diagnostic text 不进入决策输入，未识别结构 fail closed，不存在 catch-all retry/fatal。
- **单一 decision interpreter**：membrane 只解释 `execution-failure-policy` 返回的六维 decision。`FatalAfterSettlement` 路径严格执行 exact capacity fence settlement → managed-chat typed disposition durable submission → settlement committed/unknown evidence → `FatalProcess`，任一步不得委托 Host/UI 私有 cleanup。
- **closed policy score**：`HookPolicy.metadata` 以 `HookKey` 穷尽匹配承载每个 live Hook 的 criticality、context/effects、retry、capacity、failure、identity 与 admission 权限；`PluginHooks.create` 只按显式固定顺序调用 `registeredHook`，由 score 唯一生成 Host key 与 `policyAwareHook` diagnostic operation。
- **optional observation boundary**：Casebook observation 只能经 `HookPolicy.observeOptional` 执行。Plugin composition 注入 `Diagnostic.emit` physical port；boundary 捕获失败、调用该 port 并返回 typed outcome。critical tool-after checkpoint 与 result mutation 已先完成，optional outcome 不参与其返回值或 failure decision。
- **阶段划分**：严格分离 Load Phase 与 Activation Phase。加载期严禁调用宿主会话 API 或触碰持久化日志，确保插件初始化的无副作用。
- **订阅资源分型**：`HostSignalSubscribe`把无资源的公开Hook路径与持有opaque disposer的legacy listener路径收敛为封闭mode DU；顶层与events数据验证plain record，OpenCode SDK client按ordinary object capability验证，class instance无legacy events时直接选择公开Hook。listener能力与返回disposer都在JavaScript边界按函数类型验证，任何异常变为封闭error DU。`HostSignalSubscribeSurface`只通过F# typed pattern match投影plain JS verdict，不读取Fable的`.tag/.fields`。`HostSignalBootstrap`取得typed failure后才执行唯一`signal-subscribe-failed` fatal；adapter工程因此不再引用diagnostics或temporal locality。

### 5. 公开 contract 与真实 canary

- contract adapter 只 import 受支持公开 Hook/SDK surface；architecture proof 拒绝 Host fork、private module、monkey patch 与 UI/DOM 依赖。
- canary 启动声明支持的真实 Host build，通过公开入口执行 Hook ordering、snapshot identity、routing projection、terminal observation 与 fatal settlement 场景，并从公开输出断言结果。mock/fixture tests 仍可做低层 contract proof，但不能标记为 canary。
- canary failure 或能力不可观察时环境 fail closed；不得以版本猜测、wall-clock wait 或 UI 文案推定支持。

### 6. Contract/Runtime 编译架构分界与单向依赖

- **现有编译分片**：以下旧 kind 值只用于说明现存工程元数据，不作为架构治理或 capability 授权；实际边界按源码知识、公开合同与物理效果区分。
  - `Host.Session.Contract`（`host-session-contract`，kind: `contract`）：仅包含 `SessionContract` capability 与 `SessionSnapshot` 纯词汇、端口、定位 decision；
  - `Host.Signal.Contract`（`host-signal-contract`，kind: `contract`）：沿用 `host-digest` 工程路径，仅编译 `EventContract.fs/.fsi`，只引用 identity 与 outcome。终端合同不再传递 SDK record、MessagePart 或摘要实现；它不是兼容 umbrella。`SessionSnapshot` 显式引用唯一 message contract，SDK model 继续经其实际需要的 OpenCode prompt port 合同进入。
  - `Host.Message.Contract`（`host-message-contract`）：零 ProjectReference，显式归属 `host` subsystem，仅编译既有 `Message.fs/.fsi`；SessionSnapshot 与 message codec 消费唯一消息词汇。
  - `Host.SDK.Types`（`host-opencode-types`）：零 ProjectReference，显式归属 `host` subsystem，仅编译既有 `OpencodeTypes.fs/.fsi`。`OpenCodeContract` 与 `ModelRouting` 只需要其中的 `OpencodeModel`，直接消费此分片，不再编入终端事件与 `MessagePart`。源码、公开签名、模型字段与 aggregate 顺序不变。
  - `runtime-platform/digest`：零 ProjectReference，仅编译既有 `Host/Digest.fs/.fsi`，保留 `HostDigest` 公开名称但不属于 Host subsystem；名称不构成对 Host 实现的知识依赖。
  - `Host.Event.Envelope`（`host-event-envelope`，kind: `contract`）：raw envelope unwrap、event type、session/message-session identity 的唯一无状态公式；
  - `Host.Message.Codec`（`host-message-codec`，kind: `contract`）：raw message part decode，bounded 到三个 fresh production consumer；直接引用 `host-message-contract` 中既有 `Message.fs/.fsi`，后者显式归属 `host` subsystem、零 ProjectReference。SessionSnapshot 与 message codec 消费同一消息定义，没有复制定义或运行时别名；SDK DTO、终端事件与摘要实现不再进入 message codec 闭包。
  - `Host.Loop.Event.Codec`（`loop-event-codec`，kind: `contract`）：loop text-delta decode/query，只依赖 `Host.Event.Envelope`；
  - `Host.Fatal.Effect`（`host-fatal-effect`）：`Foundation/FatalProcess` 进程 fuse 的窄物理效果边界；旧 `kind: contract` 标签不使进程控制变成纯合同，物理执行与 capability 注入由 host-boundary-029 的既有证明承接。
  - `Host.Diagnostics.Runtime`（`host-diagnostics-runtime`，kind: `runtime`）：包含 `HookPolicy` 元数据表与 `ReliabilityDiagnostics` 因果记录收集；
  - `Host.Signal.Adapter`（`host-signal-adapter`，kind: `adapter`）：包含 `HostSignal` 词汇、完整 provider failure/terminal `HostEventCodec`、`HostSignalAdapter` 路由器、`HostSignalSubscribe` 订阅器与 `Events` 终端总线；不再拥有 message/loop codec 或工具注册实现。
  - `Host.Tool.Adapter`（`host-tool-adapter`）：显式归属 `host` subsystem，独立编译既有 `ToolHostCodec.fs/.fsi` 与 `ToolHostSurface.fs/.fsi`，只引用 identity、roles 和 ToolResultBound provider。HostIngressCodec 随唯一实现保留其中；schema factory、随机 handle、abort listener 与工具注册仍是物理适配能力，不把此分片声明为纯合同。bootstrap 同时消费工具与信号分片，SessionExecutionBinding 的 ExactProviderStartObservation 仍由信号侧提供。此拆分按 structured-workflow-011..014 调整编译资产，不新增 subsystem、旧 owner ACL 或平行实现。
  - `Host.Session.Runtime`（`host-session-runtime`，kind: `runtime`）：包含 `SessionSnapshotPort`/`SessionSnapshotSurface` wire 投影、`SessionQuiescenceGate`、`QuiescenceSurface`、`HostMessageProjection` 就地修改与 `HostSessionContext`；
  - `Sphinx.Host.Adapter`（`sphinx-host-adapter`，kind: `adapter`）：包含 `SphinxMcpConfig` 启动配置与环境适配。
- **单向依赖与闭包纯洁性**：应用与领域契约按真实知识消费窄合同、纯数据/decision 或 capability port，严禁传递包含 Runtime 与 Adapter 实现。compile shard 可以调整，不因旧 locality 标签或源码数量取得架构权威；结构治理以 structured-workflow-011 至 016 为准。

2026-09-10，用户在本次规范冲突裁决中批准同步 host-boundary-026：纠正已拆出的摘要、消息、SDK 类型与工具适配器仍被写入旧宽合同的描述，解除仅允许两个旧合同名称的限制，但保留物理能力隔离、唯一实现、失败与结算语义。源码、签名、工程和测试未改；既有 host-boundary-026/027 的终端、SDK、消息、工具闭包反例及 host-boundary-029 的 fatal 注入证明继续承接对应性质，没有用文档一致性冒充新增编译或行为证明。

**旧验证迁移已落地**：在 `a0a710fb2` 上，`host-session-contract-closure.test.mjs` 改用既有 `readCompileShardInventory` 与 `buildSubsystemInventory`，删除自建 XML/legacy locality/kind 解析、100/185 数量预算及 legacy owner 文件集合比较。全仓唯一生产来源、sibling 签名与 aggregate union 仍由原库存检查承接；Host 代表性源码归属显式覆盖消息、SDK 与工具新分片，摘要要求归属 runtime-platform。已有真实 provider、闭包排除和 composition 引用保护保留，并明确排除会话合同取得工具注册、物理订阅和事件总线。`SessionSnapshot.Model` 需要的 `OpencodeModel` 是数据合同，不误禁为物理 SDK 投影。

临时真实 fsproj 对照证明：会话分片改为显式 subsystem/shard 且移除旧 kind 后通过；误引工具适配器、会话源码移出 Host、显式工具分片移出 Host 均退出 1，并命中对应闭包／归属断言。每次对照后恢复原字节，生产工程未变。这些静态证明不代替独立 Fable 编译或行为证明，也不关闭 GAP-033 的其余知识隔离缺口。

以 `26bfed9d0` 为基准，本批把 terminal contract 收到 identity/outcome，移除 diagnostics 与 message visibility 的无实际用途引用。声明递归闭包（项目／`.fs/.fsi` 输入）分别为：terminal 7／26 → 4／20，diagnostics 14／62 → 10／54，visibility 11／36 → 5／12，SyncDelegate runtime 17／66 → 14／60，session contract 9／34 → 8／32。

signal adapter 首次独立编译暴露既有 `ExecutionFailure`、`ChatExecutionTerminalDisposition`、`RuntimePath` 缺失；显式补齐各自 provider 引用，删除 adapter 与 SessionSnapshot sibling 中无用途的 ingress namespace 引入，不用动态加载或扩大 shared contract 掩盖错误。adapter 声明闭包由漏报的 12／60 修正为 35／198；RuntimePath 合法保留摘要依赖，这一增长不是隔离收益。terminal、diagnostics、visibility、SyncDelegate、session、adapter 的最终独立 Fable 编译分别通过 58、92、50、98、70、236 parsed sources；四组签名的实际反向消费者并集通过 1426 parsed sources／1388 items（`0e6946de9286`）。

既有 `host-session-contract-closure.test.mjs` 的 host-boundary-026 证明同时拒绝终端合同重新引入 SDK／Message／digest，以及 adapter 再次丢失真实 provider。四个独立引用反例均退出 1；窄合同下相关结构检查通过。新隔离产物的 `HookPolicySurface` smoke 保持 critical policy fail-closed 与 optional effect 失败不改变 critical result；`MessageVisibilitySurface` 用显式 deadline 证明 foreign signal 不唤醒、同 session signal 取消 deadline、期限触发后清空 waiter。新消费者产物的 `EventsSurface` smoke 证明 sticky replay、future-only 不重放、同 run completion 去重、Failed/Aborted 的 exact authority 保留及 dispose 后零投递。`notify` 的布尔值表示 listener presence，不表示 sticky 是否保存。上述 smoke 不替代真实 Host canary；既有 `events-port.test.mjs`、`message-visibility.test.mjs` 与 `signals.test.mjs` 继续作为正式行为证明。

以 `26b439645` 为基准，完整审查 signal adapter 的十五个直接消费者：十三个只直接使用工具侧，改引 `host-tool-adapter`；bootstrap 使用两侧，分别显式引用；SessionExecutionBinding 只使用 signal 的 ExactProviderStartObservation，保留原引用。既有四个工具源码／签名输入原样转移，aggregate 输入与顺序不变。工具 audience 的声明递归闭包从原 signal adapter 的 35 项目／198 输入降为 5／26，signal adapter 自身降为 33／186。七个消费者的实际闭包变化及未缩小路径见 structured-workflow/HOW 第 3.2 节，不以直接引用替换冒称所有消费者已隔离。

独立 Fable 编译通过 tool adapter 64 parsed sources（`0857758197f1`）、signal adapter 224（`4ff036bcc89f`）、Attention consumer 370（`bbaafcd801bc`）、repository-programming runtime 644（`4c772196e3df`）；ToolHostCodec 与 HostEventCodec 的签名反向消费者按并集合并成一次 flat compile，通过 1416 parsed sources／1378 items（`bafa41b8e0a7`）。新增 host-boundary-026 编译边界回归在旧实现因包含 HostEventCodec 而失败；工具分片、Attention consumer、信号分片分别重新引入对侧引用的三个独立 mutant 均退出 1，恢复后通过。它证明声明闭包，不冒充业务语义证明。

新 tool adapter 产物通过公开 ToolHostSurface smoke：实际已安装 SDK schema 的接受／拒绝／optionality、callID/messageID 配对失败时清空、真实 AbortController 至多一次触发和 detach 后不触发、注册后执行保留 Unicode／CRLF、超长输出保留精确 Unicode 尾部且不超过 51200 bytes、执行异常原样 reject。既有 `tool-host-codec.test.mjs`、`tool-host-abort.test.mjs`、`tool-result-bound.test.mjs` 及 provider-projection 的 `tool-host-codec-full.test.mjs` 保留正式行为证明；此 smoke 不替代真实 Host canary。

### 7. Root workspace effect隔离

- `Host.RootWorkspace.Contract`只发布`IRootWorkspaceBinder`与`IRootWorkspaceReader`。binder只表达`TryBind(Some non-blank path)`的first-bind；reader只表达当前process-local结果。selector同时拒绝空白live candidate与空白后续候选。
- `Host.RootWorkspace.Runtime`私有持有cell。`None`、空串与纯空白不占位；首次非空白`Some`返回true，后续候选返回false且reader保持首值。
- `PluginHostWiring`是process runtime的唯一production acquisition point。它先绑定boot workspace，再把reader注入Causal diagnostic、provider transform与Host workflow；普通consumer不引用runtime project。
- `SharedStateSurface`只投影同一production runtime的bind/read行为，用“None→first→second”固定反例；不再公开set/clear或测试镜像cell。

### 8. Shared-state 分片的真实编译依赖

在 `e5794c8f4` 上删除无生产或测试调用方的 `OpenCode/Host/GitTree.fs/.fsi` 与 `GitTreePort`，同步去掉 aggregate、分片、APPLIES-TO 及 session-contract 排除表中的退役入口；EventStore 同名模块和 Relay snapshot 不变。shared-state 分片不再直接引用仅该适配器使用的 GitSubject 与宽 Host digest。历史 impact corpus／release-closure 快照不是当前 source authority，未重写其历史输入。

首次独立 Fable 编译暴露 `PluginSessionScope` 的 `IJoinAttemptRegistry`／构造器漏报依赖及失效 namespace 引入。现直接引用窄 `delegation/join-attempt-registry`，删除失效引入，不通过整个 recovery runtime 补齐类型。修复后独立编译通过 578 parsed sources（`cf8198d664c9`）；新隔离 `SharedStateSurface` smoke 覆盖父子查询、空白候选拒绝、first-bind 和 continuation directory fallback。该 smoke 不证明 join 全部时序；消费者编译并集及未缩小的闭包口径见 structured-workflow/HOW 的 GAP-033 记录。
