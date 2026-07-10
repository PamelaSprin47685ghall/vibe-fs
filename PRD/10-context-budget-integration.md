# 10 上下文预算钩子集成方案

## 1. 目标

在消息投影管线 `MessageTransformPipeline` 中集成 `PRD/09-context-budget-todowrite-trigger.md` 定义的 $F$ 判定。当 $F = true$ 时，在当前请求消息序列末尾追加一条英文 user prompt，强制 LLM 立刻强制紧急停止一切工作并调用 `todowrite`。

## 2. 注入文本

```text
Attention: the system context is about to be suspended. You must immediately force an emergency stop to all work and call the todowrite tool.
```

该消息作为 `TextPart` 包装为 `User` 角色的 `Message` 对象，source 标记为 `Synthetic "context-budget-nudge"`。

## 3. 集成点

### 3.1 入口位置

集成在 `src/Shell/MessageTransformPipeline.fs` 的 `runMessageTransformPipeline` 中，紧接 `applyBacklogProjection` 之后。由于需要进行 Token 异步计算，管线使用了 `Promise` 包装。

```fsharp
let afterBacklog =
    applyBacklogProjection plan.SessionID plan.Excluded backlogOps afterAmend

let! afterBudget, encodedBacklogOpt =
    if plan.Excluded then
        promise { return afterBacklog, None }
    else
        promise {
            let encodedBacklog = encodeMessages afterBacklog
            let! res = applyContextBudget plan backlogOps afterBacklog encodedBacklog encodeMessages
            return res, Some encodedBacklog
        }

let afterPrompt =
    if plan.Excluded then
        afterBudget
    else
        tryInjectParallelToolPrompt plan.SessionID afterBudget
```

### 3.2 管道预算判定函数

`src/Shell/MessageTransformPipeline.fs` 内部实现 `applyContextBudget`：

```fsharp
let applyContextBudget
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (messages: Message<obj> list)
    (encodedAll: obj array)
    (encodeMessages: Message<obj> list -> obj array)
    : JS.Promise<Message<obj> list> =
    promise {
        if plan.Excluded || messages.IsEmpty then
            return messages
        else
            let totalBytes = JS.JSON.stringify(encodedAll).Length
            let! tokenCountOpt = plan.GetContextUsage encodedAll
            let storeEntry = ContextBudgetStore.get plan.Scope plan.SessionID

            let currentTokens =
                match tokenCountOpt with
                | Some t -> t
                | None ->
                    match ContextBudget.estimateTokens totalBytes storeEntry.LastUsage with
                    | Some t -> t
                    | None -> 0

            if currentTokens <= 0 then
                return messages
            else
                ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                    { entry with LastUsage = Some {| tokenCount = currentTokens; textBytes = totalBytes |} })

                let backlog = backlogOps.GetOrRebuildBacklog plan.SessionID plan.Cleaned
                let currentStore = ContextBudgetStore.get plan.Scope plan.SessionID

                let! state =
                    promise {
                        if backlog <> currentStore.LastBacklog || currentStore.State.IsNone then
                            let stableMessages =
                                projectBacklogFor
                                    backlogOps.Host
                                    plan.Cleaned
                                    backlog
                                    true
                                    plan.SessionID
                            let stableEncoded = encodeMessages stableMessages
                            let stableBytes = JS.JSON.stringify(stableEncoded).Length
                            let! stableTokensOpt = plan.GetContextUsage stableEncoded

                            let stableTokens =
                                match stableTokensOpt with
                                | Some t -> int64 t
                                | None ->
                                    let currentLastUsage = (ContextBudgetStore.get plan.Scope plan.SessionID).LastUsage
                                    match ContextBudget.estimateTokens stableBytes currentLastUsage with
                                    | Some t -> int64 t
                                    | None -> int64 currentTokens

                            let backlogBytes = ContextBudgetUsageCodec.backlogBytesFromEncoded backlogOps.Host stableEncoded
                            let newState = ContextBudget.beginPhase stableTokens (int64 stableBytes) (int64 backlogBytes)

                            ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                                { entry with
                                    State = Some newState
                                    LastBacklog = backlog
                                    NudgeInjected = false })
                            return newState
                        else
                            return currentStore.State.Value
                    }

                let state_c = state.backlogTokensAtPhaseStart
                let state_s = state.phaseBaseTokens - state_c

                if ContextBudget.isCompactingRequired state.phaseBaseTokens (int64 plan.MaxInputTokens) then
                    return messages
                elif ContextBudget.F (int64 currentTokens) (int64 plan.MaxInputTokens) state_c state_s then
                    let updatedStore = ContextBudgetStore.get plan.Scope plan.SessionID
                    if updatedStore.NudgeInjected then
                        return messages
                    else
                        ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                            { entry with NudgeInjected = true })
                        return List.append messages [ buildContextBudgetNudgeMessage plan.SessionID ]
                else
                    return messages
    }
```

### 3.3 ContextBudgetStore

`src/Shell/ContextBudgetStore.fs`：

- 按 `sessionID` 保存 `ContextBudgetEntry`，结构如下：
  ```fsharp
  type ContextBudgetEntry =
      { State: ContextState option
        LastUsage: {| tokenCount: int; textBytes: int |} option
        LastBacklog: Wanxiangshu.Kernel.BacklogProjectionCore.BacklogEntry list
        NudgeInjected: bool }
  ```
- 包含 `get`、`put`、`update` 函数，利用宿主 `RuntimeScope` 进行线程安全的按会话隔离缓存。
- 当 `backlog <> currentStore.LastBacklog` 时，自动在 `applyContextBudget` 内部调用 `ContextBudget.beginPhase` 并重置 `NudgeInjected = false`。
- 进程重启时由 `EventLogRuntime` 重放 NDJSON 重建待办事项，从而在首次会话进入管道时自动折叠生成新的 `ContextState`。

### 3.4 Token 计数与字节率估算策略

`ContextBudget.estimateTokens` 使用如下的比例估计：

- 如果宿主平台提供了 Token 计数 API，使用 `plan.GetContextUsage` 获取精确数值。
- 如果没有，则使用最近一次成功测量的比例进行估算：
  $$\text{estimated} = \frac{\text{last token count} \times \text{current text bytes}}{\text{last text bytes}}$$
- 使用 `ContextBudgetUsageCodec.backlogBytesFromEncoded` 通过模式匹配识别 `backlog` 消息，以精准排除/提取待办投影在总大小中的字节数。

## 4. 状态流转

```
[会话启动]
  │
  ▼
重放 NDJSON ──► fold backlog ──► projectBacklogFor
  │
  ▼
beginPhase ──► ContextState 写入 ContextBudgetStore
  │
  ▼
每轮消息转换
  │
  ▼
applyContextBudget
  │
  ├─ F = false ──► 继续正常管线
  │
  └─ F = true  ──► 追加 nudge user 消息
        │
        ▼
LLM 调用 todowrite
        │
        ▼
work_backlog_committed 事件落盘
        │
        ▼
afterSuccessfulTodo ──► 更新 ContextState
```

## 5. 防止重复注入

- 每次 `runMessageTransformPipeline` 调用单独生成消息 ID。
- 通过 `source = Synthetic "context-budget-nudge"` 标记。
- 下一次调用时若消息序列中已存在同 source 消息且未发生新的 `todowrite` 成功，不再追加第二条；改为替换最后一条内容。
- 若 `F` 已经连续多轮为 true 但 LLM 仍未调用 `todowrite`，由宿主 compact fallback 处理，不再追加消息。

## 6. 与现有 nudge 系统的交互

- `context-budget-nudge` 是一种系统级 `nudge`，优先级高于 `nudge-todo` 和 `nudge-loop`。
- 若 `F = true` 与现有 nudge 同时触发，只注入 `context-budget-nudge`；忽略其它 nudge。
- 注入位置在 `tryInjectParallelToolPrompt` 之前，保证上下文提示最后出现，对 LLM 的即时影响最大。

## 7. 宿主适配

### OpenCode

`src/Opencode/MessageTransform.fs` 调用 `runMessageTransformPipeline` 的参数不变，由 `MessageTransformPipeline` 内部追加消息。

### Mux

`src/Mux/MessageTransform.fs` 同样通过 `MessageTransformPipeline` 统一处理。

### OMP

`src/Omp/MessageTransform.fs` 通过同一管线；OMP 子 workspace 的 `ContextState` 与主 session 隔离。

## 8. 失败 fallback

1. 若追加 nudge 后 LLM 仍未调用 `todowrite`，下一轮的 `F` 仍为 true；
2. 若此时 `currentTokens` 已接近或超过 `maxInputTokens`，系统不再追加 nudge，直接触发 compact；
3. compact 由宿主负责，本方案不处理具体实现。

## 9. 测试清单

| 测试 | 目标 |
|------|------|
| `ContextBudgetHookTests.fs` | 验证 `F = true` 时追加 user 消息，文本完全匹配 |
| `ContextBudgetNoReinjectTests.fs` | 验证连续多轮 `F = true` 不会无限追加 |
| `ContextBudgetAfterTodoTests.fs` | 验证成功 `todowrite` 后调用 `afterSuccessfulTodo` 更新 |
| `ContextBudgetIntegrationTests.fs` | 验证 `MessageTransformPipeline` 端到端行为 |

## 10. 边界条件

| 条件 | 行为 |
|------|------|
| `messages` 为空 | 不追加 nudge |
| `excluded = true` | 跳过 |
| `currentTokens` 无法精确获取 | 使用近似计数，但保证 `F` 在阈值前触发 |
| 已存在同 source nudge | 替换不追加 |
| `F` 为 true 但 `currentTokens` 已接近 `maxInputTokens` | 停止追加，交给 compact |

## 11. 需要改动的文件

- `src/Shell/MessageTransformPipeline.fs`：注入 `applyContextBudget`。
- `src/Shell/ContextBudgetStore.fs`（新建）：按 session 保存 `ContextState`。
- `src/Shell/ContextBudget.fs`（新建）：实现 `F`、`beginPhase`、`afterSuccessfulTodo`。
- `src/Shell/WorkBacklogToolsCodec.fs` 或 `ToolExecute`：成功 `todowrite` 后调用 `afterSuccessfulTodo`。
- `tests/ContextBudget*`：新增测试文件。

## 12. 一句话总结

> 在 `MessageTransformPipeline` 中投影后调用 $F$；为 true 时追加一条英文 user 消息，强制 LLM 停止工作并调用 `todowrite`，由 `ContextBudgetStore` 在每次成功 `todowrite` 后更新阶段状态。
