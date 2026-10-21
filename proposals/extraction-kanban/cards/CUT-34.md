# CUT-34：OpenCode 适配边界

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-34) / [范围对账](../COVERAGE.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`DOING`（2026-10-21 由 `eng-cut17f-goose` 认领；先做前置已 DONE 的部分，CUT-11/12/25 相邻能力待其 DONE 后接续）
- **所属泳道 / 波次**：宿主 / W5
- **切换前置**：`CUT-11`（派发认领与外部效果记账）、`CUT-12`（会话关联与执行事实）、`CUT-17`（请求投影与信息边界）、`CUT-25`（受限 JS 编程面与执行组合）
- **关联决策护栏**：受 `D-07` 约束——OpenCode SDK 与 Hook 适配能力留在主仓明确适配层，不要求另发布一个独立 npm 包；内核与独立库绝不反向导入 OpenCode 适配层。

## 2. 主仓源码区域与编译分片（`.fsproj`）盘点

委托说明中提及的 `OpenCode/` 宿主胶水层（Plugin、Hooks、Transforms、ToolRegistry、ChatParams 等）在主仓当前基线中对应 `src/Wanxiangshu/OpenCode/{Host,Codec,Signals,Tools,Plugin}/` 与 `src/Wanxiangshu/Host/`，受 `host-boundary`（`requirements/host-boundary/WHAT.md` `[001]`—`[033]`）管辖：

| 职责子域 | 主仓真实路径（含 `.fsi`） | 所属编译分片（`.fsproj`） | 去向判定 |
|---|---|---|---|
| **SDK 传输、会话创建/查找与快照读取** | `OpenCode/Host/OpenCodePort.fs`、`OpenCodeContract.fs`、`Sessions.fs`、`SessionHostPort.fs`、`SessionContract.fs`、`SessionSnapshot.fs`、`SessionSnapshotPort.fs`、`PluginHost.fs`、`PluginHostInterop.fs` | `opencode-host-opencodeport.fsproj`、`opencode-host-opencodeport-adapter.fsproj`、`host-session-contract.fsproj`、`host-session-snapshot-contract.fsproj` | **保留在主仓宿主适配层（A）**：隔离 OpenCode 1.18.29 SDK / HTTP 调用、`encodeURIComponent` 目录头、family root 父链遍历与最多 2 条候选的 root 发现查询 |
| **原始事件解码与信号订阅** | `OpenCode/Codec/{HostEventEnvelope,HostEventCodec,HostMessageCodec,LoopEventCodec,OpencodeTypes}.fs`<br>`OpenCode/Signals/{EventContract,HostSignal,HostSignalAdapter,HostSignalSubscribe}.fs`<br>`OpenCode/Host/{Events,SharedTerminalBus}.fs` | `host-event-envelope.fsproj`、`host-message-codec.fsproj`、`loop-event-codec.fsproj`、`host-opencode-types.fsproj`、`host-signal-adapter.fsproj` | **保留在主仓宿主适配层（A）**：在最早边界过滤流式碎片，解码为 `SessionIdle | ProviderRetry | ProviderFailure | SessionDeleted | AttemptAborted` 闭集，严禁向业务层泄露流式碎片 |
| **工具协议编解码、参数保险箱与注册表** | `OpenCode/Codec/{ToolHostCodec,ToolHostSurface}.fs`<br>`OpenCode/Host/{ToolSchemaJson,ProtocolArgumentVault,ProtocolArgumentStash,ReadonlyDelegationContract}.fs`<br>`OpenCode/Tools/{ToolContext,ToolSurface,ToolRegistry,StaticTools}.fs` | `host-tool-adapter.fsproj`、`opencode-tools-statictools.fsproj`、`plugin-composition.fsproj` | **保留在主仓适配层（A）/ 与 CUT-08、25 组合（C）**：负责 Effect Schema → JSON Schema 转换、`contract` 与 `estimated_readonly_rounds`/`self_note` 的 `before` 剥离与 `after`/wire 同源恢复、工具结果尾部有界截断及 6 阶段固定顺序门禁 |
| **Hook 策略膜、ChatAdmission 与 ChatParams** | `OpenCode/Host/{HookPolicy,ChatParamsHook,HostCompactionGate,HostCompactionObserver,HostSessionDeletion,ProviderSystemTransform,ProviderFailurePresentation,ReliabilityDiagnostics}.fs`<br>`OpenCode/Host/ChatAdmission/{Intent,Transaction,ProviderLifecycle}.fs` | `host-diagnostics-runtime.fsproj`、`provider-system-transform.fsproj`、`opencode-host-providerfailurepresentation.fsproj`、`opencode-host-chatadmission-intent.fsproj`、`opencode-host-chatadmission-transaction.fsproj` | **适配膜（A）+ 组合根（C）**：每个 live Hook 对应 `HookPolicy` 闭集行，经 `policyAwareHook` 将异常归一为 `execution-failure-policy` 代数；`chat.params` 只读校验已提交 exact lease 并仅投影 `temperature = 1.0` |
| **插件入口、Hooks 与 Transforms 装配** | `OpenCode/Plugin/{Plugin,SpikePlugin,PluginBoot,PluginHostWiring,PluginSessionWiring,PluginRecoveryWiring,PluginHooks,PluginTransforms}.fs` | `Wanxiangshu.Owner.opencode-plugin.plugin-composition.fsproj` | **与 CUT-35 共享顶层组合边界（C）**：Load Phase 纯装配，Activation 后按固定顺序调用各独立领域个体 |

## 3. 目标

- **用户/维护者当前损失**：`PluginHooks.fs`、`PluginTransforms.fs` 与 `ToolRegistry.fs` 仍包含部分与业务领域交织的转换逻辑；当 CUT-08（能力门禁）、CUT-11（派发记账）、CUT-12（会话与执行）、CUT-17（请求投影）、CUT-25（JS 编程面）逐一独立后，需要将宿主适配层收窄为纯粹的“观察翻译 + 协议搬运 + 闭合 Hook 膜”。
- **这次完成后可少维护什么**：
  1. 宿主 SDK 对象（`input.client`、HTTP 响应、Effect tool schema、流式 `message.updated`/`part.delta`）只在 `OpenCode/{Host,Codec,Signals}` 内被触碰；
  2. 领域判断（谁有权调工具、哪条消息已承接、哪段历史该剥除、哪次失败该重试）全部交由各自独立个体回答，适配层只报告真实观察到的物理事实并执行确定的宿主副作用。
- **明确不做**：
  - 不修改或 fork OpenCode 宿主源码，不导入宿主私有模块或做 monkey patch（`host-boundary-014/018`）；
  - 不把传输层 `AdmittedWithReceipt` 当成物理消息已落地（`dispatch-protocol-003`）；
  - 不在 SDK 不支持的能力上靠标题模糊匹配、正文正则或超时猜测伪造成功（`host-boundary-020/023`）。

## 4. 依据

- **正式 WHAT / 条款**：
  - `requirements/host-boundary/WHAT.md` `[001]`—`[033]`（流式碎片过滤、信号闭集、Reconciler 单飞快照、Tool 双半边身份、Typed Hook Membrane、Fatal 前精确结算、HookPolicy 闭集、编译分界与单向依赖、原生 JS 类型谓词、`ProtocolArgumentVault` 同源恢复、读取端 exact 只读租约校验）。
  - `requirements/host-provider-failure-ownership/WHAT.md` `[001]`—`[007]`（`experimental.chatMaxRetries = 0`、单次呈现、OpenCode `1.18.29` 兼容基线）。
  - `requirements/obligation-ledger/WHAT.md` `[001]`—`[007]`（原生 `todowrite` 保持宿主原样，仅在 exact `completed` ToolPart 时追加压缩 checkpoint）。
- **对应测试与真实 Canary 套件**（注：委托说明提及的 `opencode-plugin` / `host-adapter` 在主仓正式需求包中统一归于 `requirements/host-boundary/` 与 `requirements/host-provider-failure-ownership/`）：
  - `requirements/host-boundary/tests/001.test.mjs` 至 `033.test.mjs`
  - 真实 Host canary 脚本：
    - `requirements/host-boundary/tests/support/run-opencode-chat-admission-canary.mjs`
    - `requirements/host-boundary/tests/support/run-manager-review-tools-canary.mjs`
    - `requirements/host-boundary/tests/support/run-readonly-delegation-schema-canary.mjs`
    - `requirements/host-boundary/tests/support/run-user-input-canary.mjs`
  - `requirements/host-provider-failure-ownership/tests/001.test.mjs` 至 `007.test.mjs`
  - 静态架构门禁：`scripts/checks/hook-policy.mjs`、`scripts/checks/architecture.mjs`、`scripts/checks/subsystems.mjs`

## 5. 自治个体（A / B / C）

- **A（OpenCode 物理适配与无状态 Codec：`OpenCodePortAdapter`、`InjectedSessionPort`、`SessionSnapshotPort`、`HostEventEnvelope`、`HostEventCodec`、`HostMessageCodec`、`LoopEventCodec`、`ToolHostCodec`、`HostSignalSubscribe`）**：
  - 负责：将动态 JS 宿主输入经严格运行时类型谓词（`typeof === 'boolean'`、`Array.isArray`、非空原始 `string`）解码为封闭 F# 值；向 OpenCode SDK 发起会话创建、父链查询、列表查询、提示发送、中止和快照读取。
  - 永远成立的不变量：零业务权威；遇到非普通对象、继承属性、满页列表（`limit = 2` 返回 2 条）或半边身份缺失立即返回 typed 拒绝（`host-boundary-009/020/030`）。
- **B（各独立业务个体：`Work`、`Ownership`、`Trace`、`Authority`、`Dispatch`、`ChatExecution`、`ModelRouting`、`Projection`、`Capability`）**：
  - 负责各自的领域状态与纯决策，对 OpenCode SDK 零依赖。
- **C（Hook 策略膜与插件接线：`HookPolicy`、`HostSignalBootstrap`、`ChatParamsHook`、`PluginHooks`、`PluginTransforms`）**：
  - 负责：按 `HookPolicy` 静态注册全部 live Hook（`config`、`chat.message`、`chat.params`、`experimental.chat.messages.transform`、`experimental.chat.system.transform`、`experimental.session.compacting`、`experimental.text.complete`、`tool.definition`、`tool.execute.before`、`tool.execute.after`、`event`）；在 `tool.execute.before` 暂存并剥离协议字段，在 `tool.execute.after` 及 provider wire 投影时从 `ProtocolArgumentVault` 同源恢复。

## 6. 公开消息与最小完整契约

- **输入字段 / 字节 / 范围 / 身份**：
  - 宿主原始 Hook 入参 `(input: obj, output: obj)`、SDK `client` 对象与事件流。
- **成功结果及其精确保证**：
  - `Load Phase`（`PluginBoot.create`）只做资源装载、模型调度器初始化与静态校验，零 Host 会话调用、零持久化写入（`host-boundary-021`）；
  - `chat.message` 完成 `resolve pre-provider identity → durable Accepted → acquire exact capacity → project into Host`；
  - `chat.params` 以 exact `(SessionId, PhysicalUserMessageId)` 只读核验已提交租约，仅设置 `output.temperature <- 1.0`，不调用 scheduler、不改写 model（`host-boundary-033`）；
  - `experimental.chat.system.transform` 原地修改 `output.system` 数组（保留数组引用身份）；
  - `tool.execute.before` / `after` 保证参与工具的 `estimated_readonly_rounds` / `self_note` 与评审 `contract` 在业务执行前剥离、执行后及历史 wire 投影中原样恢复（含键顺序与对象身份，`host-boundary-032`）。
- **拒绝结果及零副作用保证**：
  - 任何观察不足、列表满页（2 条）、属性冲突、缺少 exact 租约或参数校验失败均 fail-closed；`ProtocolRejection` 原样交回 Host 供模型修正，不触发进程熔断；`FatalAfterSettlement` 必须先完成 exact 容量与消息终态结算才调用注入的 `FatalProcess`（`host-boundary-014/022/029`）。

## 7. 必须能红的最小反例

1. **反例 1（列表满页伪称唯一）**：交付恢复发现查询请求 `limit = 2, roots = true`，当 Host 返回 2 条记录（即使本地按精确标题过滤后只剩 1 条），必须判定为“满页不唯一”并拒绝收养（对应 `work-048` / `host-boundary-020`）。
2. **反例 2（`chat.params` 退回 session-current 缓存或触发调度）**：向 `chat.params` 传入未取得已提交 exact lease 的 `(sessionID, messageID)`（哪怕同一 session 另一条旧消息有 lease），必须立即 fail-closed 拒绝且不修改 `output`，不得调用 scheduler 或回退读取会话级缓存（对应 `host-boundary-033`）。
3. **反例 3（`tool.execute.after` 未跑或异常退出时丢失原始协议字段）**：当工具执行抛异常或宿主跳过 `after` 时，下一次 `experimental.chat.messages.transform` 仍必须从 `ProtocolArgumentVault` 还原 `contract` / `estimated_readonly_rounds` / `self_note`，确保发给 provider 的历史调用参数不漂移（对应 `host-boundary-032`）。
4. **反例 4（粗粒度 `AttemptAborted(SessionId)` 误杀新执行）**：当同一 `SessionId` 已接纳新的 `PhysicalUserMessageId` 后，迟到的粗粒度 `AttemptAborted` 信号只唤醒 reconciler 核对快照，绝不能将新执行标为 `SessionAborted` 或释放新租约（对应 `host-boundary-003`）。

## 8. 精确写集合与删除清单

- **候选写集合**：
  - `src/Wanxiangshu/OpenCode/Host/*.fs(.fsi)`
  - `src/Wanxiangshu/OpenCode/Codec/*.fs(.fsi)`
  - `src/Wanxiangshu/OpenCode/Signals/*.fs(.fsi)`
  - `src/Wanxiangshu/OpenCode/Plugin/{PluginHooks,PluginTransforms,PluginHostWiring}.fs(.fsi)`
  - `src/Wanxiangshu/Host/*.fs(.fsi)`
- **清理目标**：
  - 当 `CUT-08`、`CUT-11`、`CUT-12`、`CUT-17`、`CUT-25` 完成后，删除 `PluginTransforms.fs`、`PluginHooks.fs`、`ToolRegistry.fs` 中内联的业务规则判断与重复投影分支，只保留对独立个体的调用与 OpenCode 协议转换。

## 9. 验证命令（交由 `devops` 执行）

```sh
node scripts/build.mjs
node --test requirements/host-boundary/tests/*.test.mjs requirements/host-provider-failure-ownership/tests/*.test.mjs
node scripts/checks/hook-policy.mjs
node scripts/check.mjs
```

## 10. 施工记录

### 子范围①：宿主 SDK 只在适配区被消费（静态门禁，eng-cut17f-goose）

- 现状核对（master `3e6f71c33`）：`@opencode-ai/*` 导入只在 `OpenCode/Codec/ToolHostCodec.fs`、`OpenCode/Host/PluginHostInterop.fs`；SDK `client` 句柄（`?client`、`client.session`/`client?…`）只在 `OpenCode/Host/{OpenCodePort,SessionSnapshotPort}.fs`、`OpenCode/Plugin/PluginHooks.fs`、`OpenCode/Signals/HostSignalSubscribe.fs`。退出条件前半句在代码上已经成立，但此前没有门禁：原 `architecture.mjs` ⑤ 只看 `Foundation/`。
- 改动：`scripts/checks/architecture.mjs` 新增 ⑦ `host-sdk-boundary`：适配区（`OpenCode/{Codec,Host,Signals,Plugin}/`、`Host/`）以外的生产 `.fs` 一旦导入 `@opencode-ai/` 或触碰 SDK `client` 句柄即 fail-closed。随 `scripts/check.mjs` 运行。
- 反例/负控（`evidence/CUT-34/sdk-boundary-negative.mjs`，`.txt`）：真实树 0 违规；在业务文件 `Mission/Work/Contract.fs` 上分别植入 SDK 导入、`input?client`、`client.session`、`client?session?list` 四种越界，各报 1 条违规；正控：门禁正则在真实适配文件 `OpenCodePort.fs`、`SessionSnapshotPort.fs` 上确实命中（排除适配区才不报）。
- 全量静态检查 `node scripts/check.mjs`：base（未改）与 candidate 输出逐行相同（两边都因既有的 test 层 `deep-dist-import`/`du-shape` 报告返回 1，与本刀无关）。
- 未覆盖（如实记录）：退出条件后半句“内核不导入本层”——`Composition/Turn`、`Interaction/Dispatch`、`Execution/{Delegation,Session}`、`Context/{Prefix,Trace,Companion}` 等仍 `open Wanxiangshu.OpenCode` 以使用 `ProviderWireDecode`/`ProviderWireCapture`/`HostMessageProjection` 等宿主 codec；其中大半属于 CUT-11/12/25 写集合，待这些卡 DONE 后逐一收窄，本刀不动。

### 子范围②：内核→适配层引用只减不增（棘轮门禁，eng-cut17f-goose）

- 改动：`architecture.mjs` 新增 ⑧ `adapter-import-ratchet`。“内核文件”＝适配区（`OpenCode/`、`Host/`）、组合根 `Composition/`（卡 §5 的 C）、业务域内的 `*/OpenCode/` 适配子目录与纯 JS 边界 `*Surface.fs` 以外的生产 `.fs`。内核文件引用 `Wanxiangshu.OpenCode`（`open` 或限定名，注释除外）必须在 `scripts/checks/opencode-import-baseline.json` 上；基线条目不再引用时也报错，要求同刀删掉条目，棘轮只能收紧。
- 基线（master 现状，31 个文件）：`Change/Host/Types`、`Context/{Companion/Host,Companion/Transform,Prefix/Wire,Trace/Capture,Trace/TerminalReporter}`、`Execution/Delegation/*`（17 个，含 `Fork/Host/*`）、`Execution/Session/Attachment/CompanionLeaseRuntime`、`Interaction/Dispatch/*`（5）、`Interaction/Repair/*`（2）、`Mission/Manager/Workflow`、`Participant/Provider/Attempt/Fallback/Workflow`。这是后续 CUT-11/12/25/34 收窄的待办清单，不是验收。
- 负控（同一 `sdk-boundary-negative.mjs`）：新文件 `open`（N5）、限定名引用（N6）各报 1 条；注释里的引用不报（N7）；基线文件 `Context/Prefix/Wire.fs` 去掉引用而基线未删 → 报 1 条（N8）。
- 全量 `node scripts/check.mjs` 输出与子范围①落地后逐行相同（既有 test 层报告，返回码 1 不变）。

### 子范围③：删除三处无用的适配层 `open`（棘轮 31 → 28，eng-cut17f-goose）

- `Execution/Delegation/Fork/Host/{Agent,Join}.fs`、`Execution/Delegation/Handle/CompletionCodec.fs` 的 `open Wanxiangshu.OpenCode` 未被使用（文件中没有该命名空间下 255 个类型/模块名的任何引用，也没有限定名引用）。删除后同刀从基线移除三条。
- 证据：全新构建（generation 40，`js-module-linkage` 725 个模块链接加载 OK）；三个文件的 Fable 产物与删除前（同一源码基线 generation 39）`cmp` 逐字节相同——行为不可能改变。`architecture.mjs` OK。
- 落地复核（如实记录）：`59c5da9fd` 推送前的变基带入了 CUT-10 的 `1601da81c`/`06d8e1267`（ModelCapacity/ModelRouting 等 9 个源文件，未触及本刀三个文件），推送前只重跑了 `architecture.mjs`，没有重新构建——违反了“变基后先复核再推”。推送后立即在已落地的树上全新构建（generation 41，OK）并跑 `files.txt` 集 248 用例：237 pass / 0 fail / 1 skipped / 10 todo。master 未受影响。

### 当前阻塞（DOING + BLOCKED）

- 剩余能力（会话创建/查找/绑定、chat.params 精确租约、tool before/after 同源恢复、AttemptAborted 精确化、基线 28 个内核文件的收窄）分别与 CUT-11（Dispatch 5 个文件）、CUT-12（Session/ChatExecution、`AttemptAborted`）、CUT-25（tool 协议/JS 编程面）、CUT-10（chat.params 租约）相邻，按卡 §1 须待这些卡 DONE 后相邻验收。解除条件：CUT-10/11/12/25 在看板上 DONE。下一检查人：接手本卡的 worker 或协调者。

### 子范围④：终端信号契约移出适配层（棘轮 28 → 26，eng-cut09i-goose）

- 现状：`TerminalStop`、`TerminalOutcome`、`TerminalCompletionListener`、`IEventObservationPort` 只用 Foundation 的身份类型和 `AgentRunResult`，不含任何 SDK 形状，却放在 `OpenCode/Signals/EventContract`。内核文件为了用这四个类型不得不 `open Wanxiangshu.OpenCode`。
- 改动：文件移到 `Foundation/TerminalSignal.fs(i)`，命名空间改为 `Wanxiangshu.Foundation.TerminalSignal`；shard（`host-signal-contract`，runtime-platform）和编译位置都不变。用到这些类型的文件在原 `open Wanxiangshu.OpenCode` 后加一行新 `open`，保持原来的名字解析次序；限定名引用同步改名。`SyncDelegate/Workflow`、`Trace/TerminalReporter` 因此不再引用适配层，同刀从基线删掉。
- 路径断言：host-boundary-026/027 里按文件路径写的 shard 归属断言和“不得获得终端词汇”的反向断言改为新路径。不改的话，反向断言会因为旧路径不存在而永远通过，失去作用。WHAT 107 行的名字同步更新。

### 子范围⑤：受管 agent 名册移出适配层（棘轮 26 → 24，eng-cut09i-goose）

- 现状：`ManagedAgent`、`AgentVisibility`、`ManagedAgentParseError`（Role → 名字/可见性、名字解析）是参与者名册，不是宿主协议，却放在 `OpenCode/Tools/ManagedAgent`。
- 改动：文件移到 `Participant/Persona/ManagedAgent.fs(i)`，命名空间 `Wanxiangshu.Participant.ManagedAgents`，shard（`opencode-tools-managedagent`）不变。`Delegation/PtyPort`、`Fork/Host/Pty` 离开基线。`Process/Surface`（JS 测试面，不受棘轮约束）原来只为 `ManagedAgent` 打开适配层，这次一并删掉这条 `open`，否则 shard-edge-audit 会多出 2 条 high。structured-workflow-014 的 shard 编译项断言和 participant-identity 的 `APPLIES-TO` 跟着改路径。

### 子范围④⑤ 证据

- 两刀都只是移动文件、改命名空间，没有改任何类型定义或函数体。
- `node scripts/checks/architecture.mjs` OK（棘轮 24 条）。`node scripts/check.mjs` 与 base 在归一行号后逐行相同：shard-edge-audit 258 条，control-pyramid 111 条。
- 子范围④在 b1b59d6f4 上全量构建通过（`js-module-linkage` 全部链接）。host-boundary、semantic-trace、advisor、intra-participant-parallelism 共 346 例：pass 317，fail 3。3 个失败如下：
  - host-boundary-023 并发下偶发失败，单独重跑通过。
  - 026 的“tool registration … terminal bus”子项在未改动的 master 上同样失败（bookkeepertool 的 shard 闭包获得了 HostEventCodec，静态检查，与本刀无关）。
  - 026 的路径断言已按上条修正。
- 两刀合并后的落地复验见落地循环 `logs-cut09i/loop34*.out`。
- 回退：`git revert` 即可，不涉及持久数据。

### 子范围⑥：会话侧中性契约与入口准入决策移出适配层（棘轮 24 → 22，eng-cut09i-goose）

- `OpenCode/Host/SessionContract`（`ProviderAttemptSource`、`ContinuationAcceptanceObserver`、`DegenerationKind`、`AbortCause`、`ILoopSensor`、`ISessionQuiescenceGate`）只用 Foundation 的身份和 quiescence 类型，不含 SDK 形状。它移到 `Foundation/HostSession.fs(i)`，命名空间 `Wanxiangshu.Foundation.HostSession`，shard 仍是 `host-session-contract`（provider）。
- `OpenCode/Host/ChatAdmission/Intent`（`ChatAdmissionIntent`）在 CUT-09-a 之后只是 `wanxiangli/authority.admit` 的类型化门面，依赖 Foundation、Interaction.Authority、ChatExecution，属于领域决策（卡 §5 的 B），不是适配。它移到 `Interaction/Admission/ChatAdmissionIntent.fs(i)`，命名空间 `Wanxiangshu.Interaction.Admission`，shard 不变。`Transaction`、`IntentSurface` 这两个适配和测试面仍留在 `OpenCode/Host/ChatAdmission`。
- 结果：`Interaction/Dispatch/{Dispatcher,Ingress}` 不再引用适配层，离开基线。host-boundary-026、delegation-028 的路径断言（包括 delegation-028“恢复闭包不得含 SessionContract”的反向断言）以及 dispatch-protocol、interaction-authority 的 `APPLIES-TO` 跟着改路径。
- 静态证据：`architecture.mjs` OK（棘轮 22 条）。`check.mjs` 与子范围⑤在归一行号和路径后相同；多出的一条 control-pyramid（`Fork/ChildRecovery.fs`）来自 master 的新提交，与本刀无关。
