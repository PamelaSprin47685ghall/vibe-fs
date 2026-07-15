# 问题 6：model routing 与人类轮次路由

## 一、Flow 管线位置

本问题映射到管线上的 **HumanTurnRoute 从 `chat.message` Input 派生**，进入 `HumanTurnProjection`（PRD-09）。普通 todo/review nudge 只使用此 projection 中的 route，不读取 session 级 stale injected model。

## 二、当前根因

当前模型解析存在三种来源混用：
* 最近 assistant 的 model；
* 最近 user message 的 model；
* fallback runtime 中最近 injected model。

其中 injected model 是 session 级缓存，而且真人新消息时没有在所有路径稳定清除。

典型错误：
1. 用户用模型 A 发起任务；
2. fallback 切到模型 B；
3. 用户随后用模型 C 发新消息；
4. todo/review nudge 仍读取旧 injected model B。

OMP 某些路径直接从最后 assistant 选择模型。compaction/title 或旧 assistant 都可能改变结果。

`Opencode/NudgeEffect.collectSnapshot` 当前从 assistant info 中读取 model 时只接受 `providerID`/`modelID` 组成的对象。如果 model 是 `"openai/gpt-4o"` 字符串，局部解析会返回 `None`。仓库中已存在 `FallbackMessageCodec.decodeModelFromObj`，能够同时处理字符串和对象。

## 三、OpenCode v1.17.13 模型字段形态

官方字段形态不同：

```text
UserMessage:
  model.providerID
  model.modelID
  model.variant

AssistantMessage:
  providerID
  modelID
  variant

Session:
  model.id
  model.providerID
  model.variant
```

不能用一个只支持对象或只支持字符串的局部解析器处理所有来源。

## 四、模型事实拆分

### 4.1 HumanTurnRoute

来自真实 user message，在 `chat.message` Hook 中建立：providerID、modelID、variant、agent、messageID、humanTurnID。

普通 todo/review nudge 使用这个事实。它不属于 session 全局，只对当前 human turn 生效。

### 4.2 HostObservedRoute

来自 `chat.message`、`chat.params`、Session model 查询。其中 `chat.params` 最接近实际 LLM 请求边界，但也可能属于 compaction、title 或其他 agent，必须结合当前 owner 分类。

### 4.3 FallbackAttemptRoute

只属于某个 fallback continuation lease。只在 fallback 自己发送 continuation 时使用。

### 4.4 ObservedSessionRoute

表示 Host 当前报告的 session active model。有价值观测来源，但必须带 observation event ID、generation、observedAt、source、confidence。它可以帮助填补缺失信息，但不能无条件覆盖更明确的 HumanTurnRoute。

## 五、模型选择优先级

### 5.1 普通 todo/review nudge

1. 当前 HumanTurnRoute；
2. 与当前 humanTurnID 和 generation 匹配的 HostObservedRoute；
3. Session 当前模型；
4. Agent 默认模型；
5. 最后非 synthetic user model，兼容回退。

明确禁止：旧 injected fallback model、compaction assistant model、title model、无 generation 的 runtime cached model、最后 assistant model 无条件覆盖。

### 5.2 fallback continuation

1. 当前 FallbackAttemptRoute；
2. 当前 HumanTurnRoute；
3. fallback chain 默认。

Fallback 模型不能跨 human turn 生效。

### 5.3 不存在 current human turn 的恢复场景

从 EventLog 恢复 HumanTurnRoutingProjection；无法恢复时使用明确默认；不使用无作用域的 stale injected model。

## 六、当前 `resolveNudgeModel` 的问题

当前对普通 user message 的处理顺序大致是：
1. `fallbackRuntime.GetInjectedModel`
2. 当前 user message model
3. `fallbackRuntime.GetModel`
4. assistant/default model

这意味着 session 中只要残留 injected model，它就可能覆盖后一条真人消息显式选择的模型。短期应改为：
1. 最近一条确认是真人的、非 nudge、非 fallback synthetic message 的显式模型；
2. 当前 human turn 保存的 routing context；
3. session/agent 默认；
4. 最后非 synthetic assistant model，仅作为兼容回退。

## 七、`FallbackRuntimeState.GetModel` 不是 SSOT

当前 `FallbackEventBridge` 在识别 `NewUserMessage` 时会清空 fallback chain、调用 `runtime.ClearModel sessionID`、重置 fallback state。这说明 runtime model 至少在当前实现中是 fallback 运行期缓存，不是稳定的当前用户轮次路由事实。

它还可能存在：尚未捕获新模型、session.updated 事件缺失、旧事件晚到、fallback attempt model 覆盖用户选择、重启后内存丢失、多个 child session 混淆、model 存在但 variant/reasoning effort 等信息不完整。

Runtime map 可以加速访问，但不能成为跨重启、跨竞态的权威事实。

## 八、`session.updated` 和 `session.busy` 捕获模型

在 Host event 中及时调用 `SetModel` 有价值，但需升级为版本化 observation。不能只做 `sessionID -> model`，而应记录：session ID、human turn ID、event sequence、session generation、source event、route、observed timestamp。

旧 generation 的 session.updated 晚到时必须丢弃。

## 九、统一 model decoder

当前 `Opencode/NudgeEffect.collectSnapshot` 从 assistant info 中读取 model 时只接受 `providerID`/`modelID` 组成的对象。如果 model 是 `"openai/gpt-4o"` 字符串，局部解析会返回 `None`。

仓库中已存在 `FallbackMessageCodec.decodeModelFromObj`，能够同时处理字符串和对象，因此不应再编写第二套不完整解析器。

应当统一以下全部调用同一 decoder 或同一 model identity codec：
* `collectSnapshot`
* last user model 提取
* last assistant model 提取
* fallback route 恢复
* provider limit 模型提取

## 十、chat.message 是真人轮次的重要入口

普通用户提交 prompt 时会触发 `chat.message`，包含：sessionID、messageID、agent、model、variant、parts。

应在此建立 HumanTurnRoute。但 compaction synthetic continuation 不经过该 Hook，所以不能把"没有 chat.message"解释成事件缺失；它可能是官方 synthetic message。

## 十一、chat.params 用于校验实际模型

`chat.params` 可以观察到即将发送给 Provider 的真实模型。建议记录：

```text
RouteObservation
- owner
- sessionID
- humanTurnID
- model
- variant
- agent
- source = ChatParams
```

如果 owner 是 Human：更新当前 human route observation。如果是 Fallback：更新 fallback attempt route。如果是 Compaction：只更新 compaction observation。如果是 Title：不得污染 human route。

## 十二、Nudge 必须显式传递模型

调用 OpenCode `session.prompt` 时应显式设置 `model.providerID`、`model.modelID`、`model.variant`、`agent`。不能只发送 text 和 custom type，然后依赖 Session 默认。

OMP 当前 `sendMessage` 主要传递 customType、content、display、triggerTurn、deliverAs，模型并没有从 snapshot 显式传递给主机。应完成两件事：snapshot 模型来源改为当前 human turn；按 OMP 主机真实支持的 API 字段显式传递 route。

## 十三、reasoning effort 等无法完全恢复

官方消息和 Session 不持久保存 temperature、topP、reasoning effort、thinking、provider-specific options。这些由 agent、model 和当前配置在请求时重新计算。

HumanTurnRoute 至少应可靠保存 provider、model、variant、agent。其他参数若来自万象术自己的 fallback config，可在 FallbackAttemptRoute 中保存；若完全由 OpenCode 内部计算，普通 nudge 应服从当前 Host 配置，而不是伪造历史参数。

## 十四、Fallback model 的作用域

fallback 选中的 model 应保存为：humanTurnId、continuationId、attempt、selected route。它只对这次 fallback attempt 生效。

以下情况必须清除：真人新消息、Esc、fallback episode settle、task complete、session cleanup、session generation 改变。

## 十五、事件日志调整

折叠为三个独立部分：
* LastHumanTurnRouting
* CurrentFallbackAttemptRouting
* LastAssistantRouting

nudge 只使用 LastHumanTurnRouting。fallback 自己执行时使用 CurrentFallbackAttemptRouting。诊断和 UI 才使用 LastAssistantRouting。

## 十六、必测场景

* Human A → nudge：A。
* Human A → fallback B → fallback continuation：B。
* Human A → fallback B → Human C → nudge：C。
* Human C → compaction model D → nudge：仍为 C。
* Human C → title model E → nudge：仍为 C。
* Human C 无 model → 使用 agent/session default。
* 进程重启后从事件日志恢复：仍为 C。
* model 相同但 variant 不同：variant 必须保留。
* stale injected event 晚到：不能覆盖新 human turn。
* 真人 user model 成为 HumanTurnRoute；
* `chat.params` 按 owner 分类；
* compaction/title 不污染 human route；
* 旧 injected model 不影响新轮次；
* nudge 请求显式携带 model/variant/agent；
* fallback attempt route 不跨 turn；
| 字符串、User object、Assistant flat fields 均可解析。
