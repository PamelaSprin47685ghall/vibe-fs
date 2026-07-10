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

集成在 `src/Shell/MessageTransformPipeline.fs` 的 `runMessageTransformPipeline` 中，紧接 `applyBacklogProjection` 之后、并行工具提示注入之前。

```
let afterBacklog =
    applyBacklogProjection plan.SessionID plan.Excluded backlogOps afterAmend

let afterBudget =
    applyContextBudget plan.SessionID plan.Excluded backlogOps afterBacklog

let afterPrompt =
    if plan.Excluded then
        afterBudget
    else
        tryInjectParallelToolPrompt plan.SessionID afterBudget
```

### 3.2 新增函数

`src/Shell/MessageTransformPipeline.fs` 新增：

```
let contextBudgetNudgeText =
    "Attention: the system context is about to be suspended. You must immediately force an emergency stop to all work and call the todowrite tool."

let buildContextBudgetNudgeMessage
    (sessionID: string)
    (time: obj)
    : Message<obj> =
    { info =
        { id = "context-budget-nudge-" + System.Guid.NewGuid().ToString()
          sessionID = sessionID
          role = User
          agent = "orchestrator"
          isError = false
          toolName = ""
          details = null
          time = time }
      parts = [ TextPart contextBudgetNudgeText ]
      source = Synthetic "context-budget-nudge"
      raw = null }

let applyContextBudget
    (sessionID: string)
    (excluded: bool)
    (backlogOps: BacklogSessionOps)
    (messages: Message<obj> list)
    : Message<obj> list =
    if excluded then
        messages
    else
        let state = ContextBudgetStore.get sessionID
        let projected = applyBacklogProjection sessionID false backlogOps messages

        let currentTokens =
            estimateTokenCount projected

        let backlogTokens =
            estimateBacklogTokenCount sessionID projected

        let phaseBase = state.phaseBaseTokens
        let backlogAtStart = state.backlogTokensAtPhaseStart

        if ContextBudget.F currentTokens plan.MaxInputTokens backlogAtStart phaseBase then
            messages @ [ buildContextBudgetNudgeMessage sessionID (null) ]
        else
            messages
```

### 3.3 ContextBudgetStore

`src/Shell/SessionProjectionStore.fs` 或新建 `src/Shell/ContextBudgetStore.fs`：

- 按 `sessionID` 保存 `ContextState`。
- 成功 `todowrite` 后调用 `afterSuccessfulTodo` 更新。
- 进程重启时由 `EventLogRuntime` 重放 NDJSON 重建。

### 3.4 Token 计数策略

`estimateTokenCount` 与 `estimateBacklogTokenCount` 必须精确：

- 使用 `encodeMessages` 输出后的数组，调用宿主暴露的 tokenizer 或近似函数。
- 如果宿主未提供 tokenizer，则使用比例估计公式 `(last token count / last text bytes) * (current text bytes)` 进行估算。如果没有任何历史测量比例，则该 turn 不进行 nudge 强制，交由 compact 作为最后 fallback 机制。
- `estimateBacklogTokenCount` 将 projected prompt 中的 backlog 投影移除后计数，差值即为 $c$。

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
