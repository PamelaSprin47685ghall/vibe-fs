# Host 行为证据 — `experimental.chat.messages.transform` 与 ProviderRunIdentity 的绑定

采集日期：2026-07-29
Host 源码：`../opencode` @ `7565e035`，`packages/opencode/package.json` version 1.18.9
Host 二进制：`~/.cache/.bun/install/global/node_modules/opencode-ai/bin/opencode.exe`，`opencode --version` = 1.18.9
Plugin SDK：`@opencode-ai/plugin` 1.17.4、`@opencode-ai/sdk` 1.17.4（本仓库 `node_modules/`）
证据方法：直接读 Host TypeScript 源码；并用二进制内 bundled JS 交叉确认已安装版本与源码一致

源码与二进制两侧的 transform 触发点、assistant message 构造顺序完全一致，因此下述结论对实际运行的 Host 成立，不只是对源码树成立。

## 为什么需要这份证据

`SSOT/05.md` REVIEW-010 要求：

> `messages.transform` 返回最终消息视图 → 生成 `ProviderInputSeal` → 下一 assistant/provider run 出现时绑定 `ProviderRunIdentity`
>
> 如果 Host 无法把一次 transform 输出可靠绑定到 ProviderRunIdentity，必须 fail closed。不能退回 same-root 或 physical-message 猜测。

若 Host 确实无法建立该绑定，就必须触发 SSOT 例外协议。结论是可以建立，不需要例外。

## 一、SDK 类型层面看起来不可能

`../opencode/packages/plugin/src/index.ts:282-291`（发布产物为 `node_modules/@opencode-ai/plugin/dist/index.d.ts:259`）

```ts
"experimental.chat.messages.transform"?: (
  input: {},
  output: {
    messages: {
      info: Message
      parts: Part[]
    }[]
  },
) => Promise<void>
```

`input` 是 空对象 `{}`。没有 sessionID、messageID、requestID、runID。

`node_modules/@opencode-ai/sdk/dist/gen/types.gen.d.ts:98-127` 的 `AssistantMessage` 也没有任何 provider request 标识：

```ts
export type AssistantMessage = {
    id: string;              // 唯一的 per-run 身份
    sessionID: string;
    role: "assistant";
    time: { created: number; completed?: number; };   // ← 关键
    parentID: string;        // 父 user message id
    modelID: string;         // 配置标签，非 run 身份
    providerID: string;      // 配置标签，非 run 身份
    ...
    finish?: string;
};
```

`modelID` / `providerID` 是配置标签，同一 Session 每次 run 都相同，不能当 run 身份。
唯一的 per-run 身份是 `id`。

工具执行侧（`../opencode/packages/plugin/src/tool.ts`，发布产物 `node_modules/@opencode-ai/plugin/dist/tool.d.ts:2-24`）：

```ts
export type ToolContext = {
    sessionID: string;
    messageID: string;      // ← 这就是 assistant message id
    agent: string;
    directory: string;
    worktree: string;
    abort: AbortSignal;
    ...
};
```

所以插件当前的 `ProviderRunId` 就是 `ToolContext.messageID`
（`next/OpenCode/ToolHostCodec.fs:150`）——assistant message id。

到此为止，静态类型的结论是："transform 时没有任何东西能连到 assistant message"。

这个结论是错的。 类型定义漏掉了执行顺序这一维度信息。

## 二、Host 源码的执行顺序证明绑定可行

`../opencode/packages/opencode/src/session/prompt.ts`，`SessionPrompt.run` 主循环内
（每个 provider step 一次）：

```ts
// ① 创建 assistant message，分配单调 id，无 time.completed
// prompt.ts:1186-1200
const msg: SessionV1.Assistant = {
  id: MessageID.ascending(),
  parentID: lastUser.id,
  role: "assistant",
  mode: agent.name,
  agent: agent.name,
  variant: lastUser.model.variant,
  path: { cwd: ctx.directory, root: ctx.worktree },
  cost: 0,
  tokens: { input: 0, output: 0, reasoning: 0, cache: { read: 0, write: 0 } },
  modelID: model.id,
  providerID: model.providerID,
  time: { created: Date.now() },        // ← 没有 completed
  sessionID,
}

// ② 立即持久化
// prompt.ts:1201
yield* sessions.updateMessage(msg)

// ③ 中断兜底：只有这里和 process 结束才写 time.completed
// prompt.ts:1203-1211
const finalizeInterruptedAssistant = Effect.gen(function* () {
  if (msg.time.completed) return
  msg.error ??= MessageV2.fromError(new DOMException("Aborted", "AbortError"), {...})
  msg.time.completed = Date.now()
  yield* sessions.updateMessage(msg)
})

// ④ 建立 processor handle
// prompt.ts:1213-1219
const handle = yield* processor.create({ assistantMessage: msg, sessionID, model })
  .pipe(Effect.onInterrupt(() => finalizeInterruptedAssistant))

//   ...（SessionTools.resolve、structured output、summarize fork）...

// ⑤ 触发 transform —— 此时 msg 已存在且已持久化
// prompt.ts:1255
yield* plugin.trigger("experimental.chat.messages.transform", {}, { messages: msgs })

// ⑥ transform 之后的 msgs 变成 provider wire 消息
// prompt.ts:1257-1263
const [skills, env, instructions, mcpInstructions, modelMsgs] = yield* Effect.all([
  ..., MessageV2.toModelMessagesEffect(msgs, model),
])

// ⑦ 真正发出 provider request，结果写入 msg
// prompt.ts:1272+
const result = yield* handle.process({ user: lastUser, agent, ... })
```

### 决定性事实

`sessions.updateMessage(msg)`（`prompt.ts:1201`）严格早于 transform 触发（`prompt.ts:1255`）。

也就是说，transform 执行时：

1. 目标 assistant message 已经存在于 Session transcript；
2. `msg.id` 已经确定，并且它就是后续该 run 内所有 tool call 收到的
   `ToolContext.messageID`（`processor.create({ assistantMessage: msg })`，`prompt.ts:1214`）；
3. `msg.time.completed` 未设置（只有 `finalizeInterruptedAssistant` 或
   `handle.process` 结束才设置）；
4. `msg.parentID = lastUser.id`，`lastUser` 是 transform 输出 `msgs` 中的最后一条
   user message；
5. Session prompt 循环由 `ensureRunning(sessionID, ...)` 按 Session 串行化，
   因此同一时刻同一 Session 只有一个未完成的 assistant message。

### 由此得到的绑定判据

transform 触发时，目标 `ProviderRunIdentity` 是该 Session 中唯一满足以下全部条件的消息：

```text
role = "assistant"
time.completed 未设置
parentID = transform 输出中最后一条 user message 的 id
id 为该 Session 中最大（MessageID.ascending 单调）
```

这不是猜测，而是对已持久化 Host 事实的一次因果读取。读取方式是
`ARCH-003` 明确允许的 SDK API：`client.session.messages`。

### Host 会等待 hook 内的异步读取

`../opencode/packages/opencode/src/plugin/index.ts:280-292`：

```ts
const trigger = Effect.fn("Plugin.trigger")(function* <...>(name, input, output) {
  if (!name) return output
  const s = yield* InstanceState.get(state)
  for (const hook of s.hooks) {
    const fn = hook[name] as any
    if (!fn) continue
    yield* Effect.promise(async () => fn(input, output))   // ← await 每个 hook
  }
  return output
})
```

每个 hook 都被 `await`，且多个 plugin 的同名 hook 串行执行。因此：

1. 在 transform 内 `await` 一次 SDK 读取是合法的，Host 会阻塞等待；
2. transform 返回后 Host 才继续 `toModelMessagesEffect` 与 `handle.process`，
   所以 seal 计算发生在 provider request 真正发出之前；
3. seal 覆盖的消息视图就是 Host 随后实际发送的视图（同一个 `msgs` 数组对象，
   transform 通过原地 mutation 修改它）。

第 3 点是 `ProviderInputSeal` 有意义的前提：seal 的对象与发出的对象是同一个。

## 三、必须 fail closed 的两种情形

### 1. Compaction 路径的顺序是相反的

`../opencode/packages/opencode/src/session/compaction.ts:349-360`：

```ts
const msgs = structuredClone(selected.head)
yield* plugin.trigger("experimental.chat.messages.transform", {}, { messages: msgs })   // ← 先 transform
const modelMessages = yield* MessageV2.toModelMessagesEffect(msgs, model, {
  stripMedia: true,
  toolOutputMaxChars: TOOL_OUTPUT_MAX_CHARS,
})
const ctx = yield* InstanceState.context
const msg: SessionV1.Assistant = {                    // ← 后创建
  id: MessageID.ascending(),
  role: "assistant",
  parentID: input.parentID,
  sessionID: input.sessionID,
  mode: "compaction",
  agent: "compaction",
  summary: true,
  ...
}
```

compaction 的 transform 发生在 assistant message 创建之前，因此上述判据找不到目标。

这不构成问题：`HOST-006` 要求 managed session 关闭官方 compaction。但 seal 逻辑
必须在找不到唯一未完成 assistant message 时 fail closed，不得退化成猜测。

识别特征：compaction assistant message 的 `agent = "compaction"`、`mode = "compaction"`、
`summary = true`。

### 2. 找到 0 条或 ≥2 条

0 条 → 不是 managed 主循环路径（可能是 compaction），或读取时序异常 → fail closed。
≥2 条 → Host 行为与本证据不符（版本变化）→ fail closed。

两种情形都不写 seal；REVIEW-003 的第二次 PERFECT 因此无法确认，返回
`PendingIdentity` 或 `Rejected`，绝不 `Confirmed`。

## 四、Tool 执行身份：`callID` 在 Host 源码中是真实字段

`next/OpenCode/ToolHostCodec.fs:149-151` 读取三个字段。对照 Host 源码：

| 插件读取 | Host `Tool.Context` | SDK 发布的 `ToolContext` | 结论 |
|---------|--------------------|------------------------|------|
| `messageID` / `messageId` | ✅ `messageID: MessageID` | ✅ `messageID: string` | 可用，等于 assistant message id |
| `toolCallId` / `callID` | ✅ `callID?: string` | ❌ 未声明 | 可用，Host 确实传入（见下） |
| `userMessageID` / `userMessageId` | ❌ 不存在 | ❌ 不存在 | 死代码，生产中恒为 `None` |

### `callID` 的来源链

`../opencode/packages/opencode/src/tool/tool.ts:35-46` 定义 Host 内部 context：

```ts
export type Context<M extends Metadata = Metadata> = {
  sessionID: SessionID
  messageID: MessageID
  agent: string
  abort: AbortSignal
  callID?: string          // ← 声明存在
  extra?: { [key: string]: unknown }
  messages: SessionV1.WithParts[]
  metadata(...): Effect.Effect<void>
  ask(...): Effect.Effect<void>
}
```

`../opencode/packages/opencode/src/session/tools.ts:59-63` 填充它：

```ts
const context = (args, options: ToolExecutionOptions): Tool.Context => ({
  sessionID: input.session.id,
  abort: options.abortSignal!,
  messageID: input.processor.message.id,     // ← assistant message id
  callID: options.toolCallId,                // ← AI SDK 提供的 tool call id
  ...
})
```

`../opencode/packages/opencode/src/tool/registry.ts:143-148` 把它透传给插件工具：

```ts
const pluginCtx: PluginToolContext = {
  ...toolCtx,                                // ← 含 callID
  ask: (req) => bridge.promise(toolCtx.ask(req)),
  directory: ctx.directory,
  worktree: ctx.worktree,
}
const result = yield* Effect.promise(() => def.execute(args as any, pluginCtx))
```

`...toolCtx` 展开时 `callID` 一并传入。`PluginToolContext` 类型（即
`@opencode-ai/plugin` 的 `ToolContext`）没有声明该字段，但对象上实际存在——
这是发布类型不完整，不是插件依赖幻觉字段。

`messageID` 同理确认：`input.processor.message.id` 就是 `prompt.ts:1186` 创建的
那条 assistant message 的 id，与第二节的绑定目标是同一个值。这直接闭合了因果链：

```text
transform 时读到的唯一未完成 assistant message.id
  ==  processor.message.id
  ==  同一 run 内 verdict 工具的 ToolContext.messageID
  ==  ProviderRunIdentity
```

### 另一半：`tool.execute.before` / `after`

`../opencode/packages/plugin/src/index.ts:266-281`：

```ts
"tool.execute.before"?: (
  input: { tool: string; sessionID: string; callID: string },
  output: { args: any },
) => Promise<void>
"tool.execute.after"?: (
  input: { tool: string; sessionID: string; callID: string; args: any },
  output: { title: string; output: string; metadata: any },
) => Promise<void>
```

`callID` 已声明，但没有 `messageID`（`tools.ts:108`、`:123` 只传
`tool` / `sessionID` / `callID` / `args`）。

所以两个边界各持一半：

```text
ToolContext              : messageID ✅   callID ✅（未声明但真实存在）
tool.execute.before/after: messageID ❌   callID ✅（已声明）
```

`REVIEW-004` 的 `ReviewAttemptIdentity` 需要两者，只能从 `ToolContext` 同时取。

迁移决定：

1. `ProviderRunIdentity := ToolContext.messageID`，缺失 fail closed；
2. `ToolCallId := ToolContext.callID`，缺失 fail closed（现状已如此：`VerdictTool.fs:79-80`）；
3. 不注册 `tool.execute.after` 补身份——它没有 `messageID`，无法可靠配对；
4. 删除 `userMessageID` / `userMessageId` 读取（Host 源码中不存在该字段），
   物理用户消息身份只从插件自己的 `chat.message` 绑定表取
   （`scope.CurrentPhysicalUserMessage`）。

登记为 `HOST-011`（见 `SSOT/07.md`）。

## 五、结论

| 问题 | 结论 |
|------|------|
| REVIEW-010 的 seal → ProviderRunIdentity 绑定是否可实现？ | 可以 |
| 是否需要触发 SSOT 例外协议？ | 不需要 |
| 机制 | transform 内读 Session messages，取唯一"未完成 assistant message"，其 `id` 即 `ProviderRunIdentity` |
| 依赖的 Host 保证 | ① `sessions.updateMessage(msg)`（`prompt.ts:1201`）早于 transform 触发（`prompt.ts:1255`）；② prompt 循环按 Session 串行；③ `Plugin.trigger` await 每个 hook |
| seal 与实际发送内容是否同一对象 | 是。transform 原地 mutate `msgs`，Host 随后用同一数组做 `toModelMessagesEffect` |
| 不满足时的行为 | fail closed，不写 seal，不确认 PERFECT |
| 需要修改 OpenCode 本体吗？ | 不需要（`ARCH-003` 满足） |

同时确认的次要结论：

| 项 | 结论 |
|----|------|
| `ToolContext.callID` | Host 真实传入（`tools.ts:63` → `registry.ts:143` 展开），SDK 发布类型漏声明 |
| `ToolContext.userMessageID` | Host 源码中不存在，插件读取为死代码，休克期删除 |
| `ReviewAttemptIdentity` 两个 ID 的来源 | 只能同时从 `ToolContext` 取；`tool.execute.*` 无 `messageID`，不可替代 |

## 六、脆弱性登记

本绑定依赖 Host 内部执行顺序，而非公开类型合同。`@opencode-ai/plugin` 的
`input: {}` 不承诺"transform 之前 assistant message 已创建"。

因此：

1. 迁移必须实现一条 canary，直接断言该顺序仍然成立：transform 时能读到
   唯一未完成 assistant message，且其 id 等于随后同一 run 内 verdict 工具收到的
   `ToolContext.messageID`。
2. 该 canary 是 Host 版本升级门禁。顺序改变时它必须先红。
3. 顺序改变且无替代路径时，才触发 SSOT 例外协议。

登记为 `HOST-010`（见 `SSOT/07.md`）。

## 七、复核方法

本文件的结论可以按以下步骤独立复核，不需要运行插件：

```bash
# 1. transform 触发点，确认它在 updateMessage 之后
grep -n "experimental.chat.messages.transform" \
  ../opencode/packages/opencode/src/session/prompt.ts
sed -n '1186,1256p' ../opencode/packages/opencode/src/session/prompt.ts

# 2. compaction 的相反顺序
sed -n '345,365p' ../opencode/packages/opencode/src/session/compaction.ts

# 3. trigger 会 await 每个 hook
sed -n '280,292p' ../opencode/packages/opencode/src/plugin/index.ts

# 4. callID 真实存在并透传给插件
grep -n "callID" ../opencode/packages/opencode/src/tool/tool.ts
sed -n '59,64p'   ../opencode/packages/opencode/src/session/tools.ts
sed -n '143,148p' ../opencode/packages/opencode/src/tool/registry.ts

# 5. 已安装二进制与源码一致
opencode --version
grep -o 'version.*' ../opencode/packages/opencode/package.json | head -1
```
