# STATUS/evidence/host-context-recovery — 包 X0 Host 源码确认

绑定 Host 版本：`1.18.9`（`packages/opencode/package.json:3`）。
绑定本仓库 commit：`cd1f8f09`。
确认日期：2026-07-30。

方法：阅读 `/home/kunweiz/Desktop/vibe/opencode` 的 TypeScript 源码。未使用 `node_modules` 的 `.d.ts`，未做黑盒实验。每条结论附文件路径与行号。

结论摘要：第 1–5 项（transform 能力）全部可用，但有一条未公开的调用约定必须遵守。第 6–9 项（compaction 关闭）中三项可关闭、一项不可关闭，触发 HOST-006 的 SSOT 例外协议，见 `STATUS/blocker-HOST-006.md`。

---

## 第 1 项：transform 能否返回与物理 transcript 不同的消息集

结论：可以，但只能通过就地修改数组。 重新赋值 `output.messages` 被静默忽略。

`packages/opencode/src/session/prompt.ts:1255-1263`

```ts
yield* plugin.trigger("experimental.chat.messages.transform", {}, { messages: msgs })

const [skills, env, instructions, mcpInstructions, modelMsgs] = yield* Effect.all([
  ...,
  MessageV2.toModelMessagesEffect(msgs, model),
])
```

`packages/opencode/src/plugin/index.ts:284-293`

```ts
>(name: Name, input: Input, output: Output) {
  if (!name) return output
  const s = yield* InstanceState.get(state)
  for (const hook of s.hooks) {
    const fn = hook[name] as any
    if (!fn) continue
    yield* Effect.promise(async () => fn(input, output))
  }
  return output
})
```

`{ messages: msgs }` 是一次性对象字面量，第 1255 行丢弃 `trigger` 的返回值，第 1262 行读的是原 `msgs` 绑定。因此 `splice` / `push` / `length = 0` / 逐消息改字段都能到达 provider；`output.messages = newArray` 不能。

不存在与持久行的重新合并：`msgs` 在 `prompt.ts:1092` 由 `MessageV2.filterCompactedEffect` 从数据库读出，是每轮迭代重建的内存数组（`message-v2.ts:469-490`、`574-576`），修改后直接进入 `toModelMessagesEffect`。修改不落盘，下一轮重新读库。

影响：COMPANION-005 的 frame 投影可实现，但实现方式受限。 这是一条会静默失效的约定——写成 `output.messages = frames` 不报错、不抛异常，只是 provider 收到未修改的原始 transcript，而所有断言都会通过。它与 `tests-mjs/domain.mjs` 封死的三个陷阱同类。Host 仓库内无该 hook 的测试或文档（唯一提及在 `packages/core/src/plugin/skill/customize-opencode.md:355`），因此无法判断这是有意契约还是疏漏，不能依赖它在未来版本保持不变。

## 第 2 项：是否接受连续 user 消息

结论：接受。Host 侧无校验、无归一化、无角色交替强制。

`packages/opencode/src/session/message-v2.ts:195-241` 按顺序逐条 push，不回看前一条角色，唯一过滤是空消息（`if (userMessage.parts.length > 0) result.push(userMessage)`）。Host 自身就依赖这一点：`message-v2.ts:382-399` 注入携带 tool-result 媒体的合成 user 消息。

`message-v2.ts:406-414` 调用的 `convertToModelMessages`（`node_modules/ai/src/ui/convert-to-model-messages.ts:66-92`）是逐消息 switch，不合并。

合并发生在 provider adapter：`@ai-sdk/anthropic/src/convert-to-anthropic-messages-prompt.ts:1166-1219` 的 `groupIntoBlocks` 把连续 `user`/`tool` 折叠为一个 `UserBlock`。

未解决：只验证了 Anthropic adapter。OpenAI 及其他 adapter 是否合并未确认。 影响面有限——连续 user 消息在两种行为下都不会报错，最坏情况是模型看到多个 user turn 而非一个——但 canary 剧本的前缀匹配需要知道实际形状，登记为包 K 的待确认项。

## 第 3 项：合成 user 消息 id 如何影响 assistant 的 parentID

结论：完全无影响。`parentID` 在 hook 运行前已固定，且无 id 存在性校验。

`packages/opencode/src/session/prompt.ts:1096`、`1186-1201`

```ts
const { user: lastUser, assistant: lastAssistant, finished: lastFinished, tasks } = MessageV2.latest(msgs)
...
const msg: SessionV1.Assistant = {
  id: MessageID.ascending(),
  parentID: lastUser.id,
  role: "assistant",
  ...
}
yield* sessions.updateMessage(msg)
```

assistant 消息在 1186-1201 构造并持久化，hook 在 1255 触发。`lastUser` 在 1096 由 max-id 扫描得出（`message-v2.ts:585-601`），扫的是 transform 前的数组。全仓未发现任何把 transform 输出的消息 id 与持久行交叉校验的代码。

影响：COMPANION-013 的确定性 synthetic id 公式安全——合成 id 不会污染持久 parent/child 图，也不能用来重新指定父子关系。

## 第 4 项：输出末条 user 消息是否必须等于物理末条

结论：不必须。

`prompt.ts:1272-1273` 传给 processor 的 `user` 与 1188 的 parent 同源，都是 1096 算出的 `lastUser`。1255 之后唯一读该数组的是 1262 的 `toModelMessagesEffect`。（`prompt.ts:1222` 确实重扫 `msgs` 找末条 user，但在 1255 之前，且只喂 `bypassAgentCheck` 标志。）

影响：COMPANION-005 的「delta 必须最后」不是 Host 强制，而是本方案的选择。 原设计文档 §10.2 的理由是「更容易保持零例外绑定」，这一判断成立但不是必需——HOST-010 的绑定用的是 1096 算出的 `lastUser`，与 transform 输出顺序无关。该顺序仍应保留：它让物理 delta 消息同时是 provider 看到的最后一条，避免 Host 与 provider 对「本轮新内容是什么」产生两种答案。

## 第 5 项：transform 输入能否读到 prompt metadata 或 request kind

结论：输入不能，metadata 只能从输出消息的 parts 读到。

输入在两个调用点都是空对象字面量——`prompt.ts:1255` 与 `compaction.ts:350`。已发布 `.d.ts` 中的 `{}` 类型是准确的，不是类型擦除（`packages/plugin/src/index.ts:282-290`）。对比 `experimental.chat.system.transform`（`index.ts:291-296`）确实收 `{ sessionID?: string; model: Model }`。

metadata 可从输出读到：`output.messages[].info` 是完整 `User`/`Assistant` 记录，含 `sessionID`、`agent`、`model`（`packages/schema/src/v1/session.ts:327-355`、`453-485`）；`output.messages[].parts[]` 的 text part 有 `metadata: Schema.optional(Schema.Record(Schema.String, Schema.Any))`（`session.ts:102-116`），且插件写入的 part metadata 能存活持久化往返。

`PromptInput`（`prompt.ts:1499-1520`）无 `metadata` 字段，因此 prompt 级 metadata 只能夹带在 part 的 metadata 里——这正是 `PromptMetadataCodec` 现在的做法。

影响：hook 内无法区分「普通轮」与「compaction 轮」，也无法知道自己被哪个调用点触发。 `RequestKind`（PROMPT-008）必须由插件自己的 claim 状态回答，不能期望 Host 告知。这与 PROMPT-009 的来源解析优先级一致：身份由 PromptKey 锚定，不由 Host 输入推断。

---

## 第 6 项：automatic compaction 的真实关闭位置

结论：可关闭，唯一开关是全局用户配置 `compaction.auto`。触发决策上没有插件 hook。

两个触发点：

`packages/opencode/src/session/prompt.ts:1161`

```ts
if (
  lastFinished &&
  lastFinished.summary !== true &&
  (yield* compaction.isOverflow({ tokens: lastFinished.tokens, model }))
) {
  yield* compaction.create({ sessionID, agent: lastUser.agent, model: lastUser.model, auto: true })
  continue
}
```

`packages/opencode/src/session/processor.ts:477`

```ts
if (
  !ctx.assistantMessage.summary &&
  isOverflow({ cfg: yield* config.get(), tokens: usage.tokens, model: ctx.model })
) {
  ctx.needsCompaction = true
}
```

实际条件在 `packages/opencode/src/session/overflow.ts:22-33`：

```ts
export function isOverflow(input: {...}) {
  if (input.cfg.compaction?.auto === false) return false
  if (input.model.limit.context === 0) return false
  const count = input.tokens.total || ...
  return count >= usable(input)
}
```

配置 schema：`packages/core/src/v1/config/config.ts:149-153`。作用域是 per-instance（`packages/opencode/src/config/config.ts:606-608`），无 per-session 或 per-request 覆盖。

插件写入路径：`packages/opencode/src/plugin/index.ts:241-249` 把 `config.get()` 返回的活引用交给 `config` hook（`config/config.ts:607` 直接返回 `s.config`，无克隆无冻结），且 `plugin.init()` 先于其它服务运行（`packages/opencode/src/project/bootstrap.ts:36-38`，注释写明 "Plugin can mutate config so it has to be initialized before anything else"）。因此插件可以设 `cfg.compaction.auto = false`。

影响：可关闭，但是实例级而非会话级。 由于万象术管理整个实例，这个粒度可接受，甚至更安全——不会出现「某些会话关了、某些没关」的混合态。

## 第 7 项：overflow compaction 是否经过可拒绝 Hook

结论：无 hook，不可拒绝。但 `compaction.auto = false` 会把溢出转为终局错误，而那正是本方案需要的信号。

错误分类：`packages/opencode/src/session/message-v2.ts:676-689` 把解析为 `context_overflow` 的 `APICallError` 映射成 `ContextOverflowError`；流式变体在 `706-716`。`packages/opencode/src/session/retry.ts:70` 明确标为不可重试。

处理路径 `packages/opencode/src/session/processor.ts:607-618`：

```ts
if (SessionV1.ContextOverflowError.isInstance(error)) {
  if ((yield* config.get()).compaction?.auto === false && !ctx.assistantMessage.summary) {
    ctx.assistantMessage.error = error
    ctx.assistantMessage.finish = "error"
    yield* events.publish(Session.Event.Error, { sessionID: ctx.sessionID, error })
    yield* status.set(ctx.sessionID, { type: "idle" })
    return
  }
  ctx.needsCompaction = true
  ...
}
```

compaction 内唯一的 hook `experimental.session.compacting`（`compaction.ts:342-348`）输出类型是 `{ context: string[]; prompt?: string }`，无 `enabled`/`cancel` 字段（`packages/plugin/src/index.ts:298-308`），无法否决。

影响：这一项与 SSOT/12 对齐，不是障碍。 `auto = false` 时溢出产生 `finish = "error"` 的终局 assistant 消息与 `status = idle`。插件的 reconcile 从完整 snapshot 读到的就是 `Failed`——CTX-002 要求的「真实失败是唯一恢复触发信号」由 Host 免费提供。CTX-005 的「失败不分类」在此处尤其重要：插件不得读 `ContextOverflowError` 这个名字来推断根因。

## 第 8 项：manual compaction 能否被全局阻断

结论：不能。无 hook、无配置键。这是唯一无法关闭的路径。

HTTP 端点 `POST /session/:sessionID/summarize`（`packages/opencode/src/server/routes/instance/httpapi/groups/session.ts:303-315`），处理器 `handlers/session.ts:273-293`：

```ts
yield* compactSvc.create({
  sessionID: ctx.params.sessionID,
  agent: currentAgent,
  model: { providerID: ctx.payload.providerID, modelID: ctx.payload.modelID },
  auto: ctx.payload.auto ?? false,
})
yield* promptSvc.loop({ sessionID: ctx.params.sessionID })
```

TUI 绑定：`packages/tui/src/config/keybind.ts:99`（`<leader>c`）、`packages/tui/src/routes/session/index.tsx:121,556`、`handlers/tui.ts:15`。

任务由循环执行，`prompt.ts:1149-1159`，不查配置、无可否决 hook。

影响：HOST-006 的「manual 也必须关闭」不可实现。 触发 SSOT 例外协议，见 `STATUS/blocker-HOST-006.md`。条款改为预防层 + 收容层：manual `/compact` 成为官方支持用法，任何出现的 compaction 触发一次重锚。

## 第 9 项：autocontinue 的真实调用路径

结论：hook 可否决合成 continue 轮，但 overflow replay 分支不经过 hook。在 `auto = false` 下该分支不可达。

replay 分支无 hook（`packages/opencode/src/session/compaction.ts:422-449`）；hooked 分支在 `451-472`：

```ts
if (!replay) {
  ...
  if (
    (yield* plugin.trigger("experimental.compaction.autocontinue", {...}, { enabled: true })).enabled
  ) {
    const continueMsg = yield* session.updateMessage({ id: MessageID.ascending(), role: "user", ... })
```

hook 契约：`packages/plugin/src/index.ts:309-326`，`enabled` 默认 `true`。

`replay` 仅在 `input.overflow` 为真时设置（`compaction.ts:310-326`）。而 `overflow: true` 只从 `prompt.ts:1319-1328` 的 `result === "compact"` 分支传入，该分支要求 `processor.ts:608` 未提前返回，即要求 `compaction.auto !== false`。

影响：`auto = false` 同时关闭了 replay 分支。 第 9 项在本方案的配置下不是漏洞。仍应显式设 `enabled: false`：它是唯一有 hook 的合成轮注入点，留着等于依赖上游默认值不变。

## 第 10 项：被 transform 省略的历史是否仍影响 Host 内部行为

结论：对 compaction 触发阈值无影响；对 compaction 内部与 prune 有影响。

token 计数来自 provider 响应，不来自 transcript：`packages/opencode/src/session/processor.ts:438-445` 用 `Session.getUsage`，后者只读 `input.usage.*` 与 provider metadata（`session.ts:338-361`）。两处溢出检查消费的正是这些数字：`processor.ts:479`（`tokens: usage.tokens`）与 `prompt.ts:1164`（`tokens: lastFinished.tokens`）。

读持久 transcript 的地方：
- `SessionCompaction.select` / `estimate` 在传入 `process` 的 `history` 上工作（`compaction.ts:333-341`、`180-186`）。
- `prune` 直接读存储，忽略 transform（`compaction.ts:248-250`），由 `compaction.prune` 门禁，默认 false（`packages/core/src/v1/config/config.ts:154-156`）。

影响：CTX-009 的「X 不发压缩请求、只做本地 projection 替换」在计量上是正确的。 transform 缩短请求后，provider 报告的 usage 相应变小，阈值不会误触。这也意味着 probe 成功后 Host 不会因为持久 transcript 仍然很长而自行压缩——前提是第 6 项的 `auto = false` 已生效。

`compaction.prune` 必须保持 false：它绕过 transform 直接删持久行，而 COMPANION-009 要求原始 Host transcript 永不物理删除。登记为 X7 的启动断言项。

---

## 未解决项

### 第二个 runner 的可达性

`packages/core/src/session/runner/llm.ts:215` 调用 `compaction.compactIfNeeded`，该实现从外发请求估算（`packages/core/src/session/compaction.ts:225-236`），且完全没有插件 hook（配置来自 config 文档，`compaction.ts:114-126`）。它接入了 `packages/core/src/location-services.ts:78`，但在 `packages/opencode/src/server` 中未找到驱动它的 HTTP 路由。

无法确认它在 1.18.9 是否可达，或是休眠的 v2 脚手架。

处置：X7 的启动能力门禁必须包含一次运行时探测，而不只是静态读源码结论。 若该 runner 在某个 Host 版本被启用，`compaction.auto` 对它无效，插件会在毫不知情的情况下运行两套压缩系统——这正是 HOST-006 禁止的状态。探测方式：启动后读一次 compaction 相关配置的实际生效值，并在首个 managed session 的第一轮请求后核对 `IsCompaction` 消息数为 0。

### 非 Anthropic adapter 的连续 user 合并行为

见第 2 项。登记为包 K 待确认项。
