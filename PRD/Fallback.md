# PRD-FB：Fallback + 会话恢复系统

## 1. 系统概述

子代理模型报错时，内循环拦截 → 完全平方数启发式选模型 → 实际发 continue 探测可用性 → 成功则续推（父代理无感知），耗尽才传播失败。外循环（Nudge 审查重试）与内循环（Fallback 连接恢复）正交，Fallback 优先拦截。

**零定时器**：全系统无 setTimeout/setInterval/Date.now，纯事件驱动。
**纯启发式**：不调用 methodology_* 工具，不依赖时间戳。
**实际探测**：模型可用性 = 实际发 continue prompt，不靠冷却计时。

## 2. 配置格式

AGENTS.md frontmatter YAML `models:` 段。存在时覆盖 opencode.json agent 模型配置。

```yaml
---
models:
  default:
    - anthropic/claude-sonnet-4
    - openai/gpt-5.5
    - zai-coding-plan/glm-5.1
  agents:
    bookkeeper:
      - id: openai/gpt-5.4:high
        temperature: 1.0
      - id: anthropic/claude-sonnet-4
    sisyphus:
      - opencode-go/deepseek-v4-pro
      - openai/gpt-5.5
---
```

### 字段语义

| 字段 | 类型 | 说明 |
|---|---|---|
| `models.default` | `ModelEntry[]` | 未单独配置的代理使用的 fallback 链。第一项 = 首选模型 |
| `models.agents.<name>` | `ModelEntry[]` | 覆盖该代理的 fallback 链。第一项 = 首选模型 |
| `ModelEntry` | `string \| object` | 简写 `"provider/model"` 或 `{ id: "provider/model:variant", temperature?: number, ... }` |

### model id 格式

`provider/model:variant` — `:variant` 可选，映射到 opencode 的 `variant` 字段。

### 配置优先级

```
AGENTS.md models: 存在
  → agents.<name> 链第一项 = 该代理首选模型（覆盖 opencode.json agent.<name>.model）
  → 链其余项 = fallback 顺序
  → default 链 = 未单独配置的代理的 fallback 顺序
AGENTS.md models: 不存在
  → 不启用 fallback，沿用现有行为
```

### opencode.json 覆盖机制

Opencode `config` hook 中，读 AGENTS.md `models:` 段：
- 存在 → 对每个 `agents.<name>`，将链第一项写入 `cfg.agent.<name>.model`（覆盖 opencode.json 值）
- `default` 链 → 对未在 `agents` 中列出的内置代理，用 default 第一项覆盖

Mux 已有 frontmatter 覆盖机制（`resolveDelegatedAgentAiSettings` 三层合并），扩展 `MuxAiSettingsCodec` 读 `models:` 段即可。

## 3. 完全平方数启发式算法

### 核心状态

每会话维护：
- `currentIndex`：当前模型在链中的位置（0-indexed）
- `failureCount`：连续失败次数 k
- `chain`：fallback 链（ModelEntry 数组）

### 算法

```
failure(sessionID):
  k = failureCount[sessionID] + 1
  failureCount[sessionID] = k
  N = currentIndex[sessionID]

  if isPerfectSquare(k):
    scanStart = 0
  else:
    scanStart = N

  // 扫描：从 scanStart 开始逐个发 continue 探测
  // 每次探测 = 发 continue prompt + 等待事件
  // session.error → 该模型不可用 → 尝试链中下一个
  // session.busy 或 session.idle（正常）→ 该模型可用 → N' = 该位置

  N' = scan(sessionID, chain, scanStart)

  if N' is None:  // 全部耗尽
    propagate(sessionID)  // 传播失败给父代理
    return

  currentIndex[sessionID] = N'

  if N' < N:
    failureCount[sessionID] = 0
  elif N' > N:
    failureCount[sessionID] = k + 1
  // N' = N → 不动作（k 保持已递增值）
```

### 完全平方数判断

```fsharp
let isPerfectSquare (n: int) : bool =
    if n <= 0 then false
    else
        let root = int (sqrt (float n))
        root * root = n
```

### 扫描机制（事件驱动）

扫描不是同步循环，而是跨事件周期的状态机：

```
scanStart → 发 continue 给 chain[scanStart]
  → session.error → scanStart++ → 发 continue 给 chain[scanStart]
  → session.busy → N' = scanStart → 扫描完成
  → scanStart ≥ chain.Length → N' = None → 耗尽
```

每次 session.error 在扫描期间不递增 failureCount，只推进 scanStart。扫描完成（成功或耗尽）时才更新 failureCount。

### k 重置时机

| 事件 | failureCount |
|---|---|
| 新用户消息 | → 0 |
| fallback 成功且 N' < N | → 0 |
| fallback 成功且 N' > N | → k + 1 |
| fallback 成功且 N' = N | 不变 |
| 模型失败 | → k + 1（触发扫描前） |

## 4. Kernel 设计

`src/Kernel/FallbackKernel/` — 纯函数，去掉 Node/宿主对象后仍成立。

### Types.fs

```fsharp
type ModelVariant = string  // "high" | "medium" | "low" | ...

type FallbackModel = {
    ProviderID: string
    ModelID: string
    Variant: ModelVariant option
    Temperature: float option
    TopP: float option
    MaxTokens: int option
    ReasoningEffort: string option
}

type FallbackChain = FallbackModel list

type ErrorClass =
    | Ignore              // MessageAborted, 用户 ESC
    | RetrySame           // 可重试错误，retryCount < maxRetries
    | ImmediateFallback   // 401/402/403, isRetryable=false
    | Exhausted           // retry 耗尽，进 fallback 链

type FallbackPhase =
    | Idle
    | Retrying of retryCount: int
    | Scanning of scanIndex: int * originalIndex: int
    | Exhausted
    | Propagated

type SessionFallbackState = {
    Phase: FallbackPhase
    CurrentIndex: int
    FailureCount: int
    Cancelled: bool
    TaskComplete: bool
    ContinueCount: int      // 幻觉循环检测：连续 continue 次数
    Todos: TodoItem list
}

type FallbackConfig = {
    DefaultChain: FallbackChain
    AgentChains: Map<string, FallbackChain>
    MaxRetries: int
}

type FallbackEvent =
    | SessionError of error: ErrorInput
    | SessionIdle
    | SessionBusy
    | NewUserMessage
    | TaskCompleteCalled
    | TodoUpdated of TodoItem list

type FallbackAction =
    | DoNothing
    | SendContinue of model: FallbackModel
    | AbortAndResume of model: FallbackModel
    | PropagateFailure
    | ScanToolCallAsText
    | CheckTodoState

and ErrorInput = {
    ErrorName: string
    Message: string
    StatusCode: int option
    IsRetryable: bool option
}
```

### Decision.fs

```fsharp
let classifyError (input: ErrorInput) (state: SessionFallbackState) (config: FallbackConfig) : ErrorClass =
    // 1. MessageAbortedError → Ignore
    // 2. state.Cancelled → Ignore
    // 3. state.TaskComplete → Ignore
    // 4. HTTP 401/402/403 → ImmediateFallback
    // 5. isRetryable = false → ImmediateFallback
    // 6. isRetryable = true + retryCount < maxRetries → RetrySame
    // 7. HTTP 429/5xx + retryCount < maxRetries → RetrySame
    // 8. retryCount ≥ maxRetries → Exhausted
    // 9. 默认 → RetrySame（安全网）
    // 上下文溢出、prefill not supported 等均按普通错误走上述分类
```

### Recovery.fs

```fsharp
let isPerfectSquare (n: int) : bool = ...

let scanStartIndex (failureCount: int) (currentIndex: int) : int =
    if isPerfectSquare failureCount then 0 else currentIndex

let selectModel (chain: FallbackChain) (index: int) : FallbackModel option =
    chain |> List.tryItem index

let updateFailureCount (n': int) (n: int) (k: int) : int =
    if n' < n then 0
    elif n' > n then k + 1
    else k
```

### StateMachine.fs

```fsharp
let transition (state: SessionFallbackState) (event: FallbackEvent) (config: FallbackConfig) (chain: FallbackChain)
    : SessionFallbackState * FallbackAction =
    match event with
    | NewUserMessage ->
        { state with Phase = Idle; FailureCount = 0; ContinueCount = 0 }, DoNothing

    | TaskCompleteCalled ->
        { state with Phase = Idle; TaskComplete = true }, DoNothing

    | SessionError error ->
        if state.Cancelled || state.TaskComplete then state, DoNothing
        else
            let errorClass = classifyError error state config
            match errorClass, state.Phase with
            | Ignore, _ -> state, DoNothing

            | RetrySame, Idle ->
                { state with Phase = Retrying 1 },
                SendContinue chain.[state.CurrentIndex]

            | RetrySame, Retrying count when count < config.MaxRetries ->
                { state with Phase = Retrying (count + 1) },
                SendContinue chain.[state.CurrentIndex]

            | Exhausted, _ | ImmediateFallback, _ | RetrySame, Retrying _ ->
                // 进入扫描
                let k = state.FailureCount + 1
                let start = scanStartIndex k state.CurrentIndex
                match selectModel chain start with
                | Some model ->
                    { state with
                        Phase = Scanning (start, state.CurrentIndex)
                        FailureCount = k },
                    SendContinue model
                | None ->
                    { state with Phase = Exhausted }, PropagateFailure

            // 扫描中收到 error → 推进 scanIndex
            | _, Scanning (scanIdx, origIdx) ->
                let nextIdx = scanIdx + 1
                match selectModel chain nextIdx with
                | Some model ->
                    { state with Phase = Scanning (nextIdx, origIdx) },
                    SendContinue model
                | None ->
                    { state with Phase = Exhausted }, PropagateFailure

    | SessionBusy ->
        match state.Phase with
        | Scanning (scanIdx, origIdx) ->
            // 扫描成功
            let n' = scanIdx
            let n = origIdx
            let k = updateFailureCount n' n state.FailureCount
            { state with
                Phase = Idle
                CurrentIndex = n'
                FailureCount = k }, DoNothing
        | Retrying _ ->
            { state with Phase = Idle }, DoNothing
        | _ -> state, DoNothing

    | SessionIdle ->
        match state.Phase with
        | Idle when not state.TaskComplete && not state.Cancelled ->
            // 检查工具文本 + todo 状态
            state, ScanToolCallAsText
        | _ -> state, DoNothing

    | TodoUpdated todos ->
        { state with Todos = todos }, DoNothing
```

## 5. Shell 设计

### FallbackConfigCodec.fs

解析 AGENTS.md frontmatter `models:` 段 → Kernel `FallbackConfig`。

```fsharp
// 从 frontmatter obj 提取 models: 段
let extractFallbackConfig (frontmatter: obj) : FallbackConfig option = ...

// ModelEntry 解析：string | object → FallbackModel
let parseModelEntry (entry: obj) : FallbackModel = ...

// model id 解析："provider/model:variant" → { ProviderID; ModelID; Variant }
let parseModelId (id: string) : FallbackModel = ...
```

参照 `WorkspaceFiles.fs` 的 `extractImportList` 模式：`Dyn.get frontmatter "models"` → 递归提取 `default` 和 `agents` 子段。

### FallbackRuntimeState.fs

每会话 mutable Maps，同 `context-state.ts` 模式。零时间戳。

```fsharp
// 模块级 Maps
let sessionStates = Map<string, SessionFallbackState>()
let sessionChains = Map<string, FallbackChain>()
let sessionAgents = Map<string, string>()  // sessionID → agent name

// API
let getOrCreateState (sessionID: string) : SessionFallbackState
let updateState (sessionID: string) (state: SessionFallbackState) : unit
let getChain (sessionID: string) : FallbackChain option
let setChain (sessionID: string) (chain: FallbackChain) : unit
let cleanupSession (sessionID: string) : unit
```

### FallbackEventBridge.fs

公共事件翻译层。定义宿主无关的 `FallbackEvent` 和 `FallbackAction`（已在 Kernel Types.fs 定义），各宿主实现翻译。

```fsharp
// 宿主事件 → FallbackEvent
type IEventTranslator =
    abstract TranslateError: errorObj: obj -> ErrorInput
    abstract ExtractSessionID: event: obj -> string option
    abstract IsSessionError: eventType: string -> bool
    abstract IsSessionIdle: eventType: string -> bool
    abstract IsSessionBusy: eventType: string -> bool
    abstract IsNewUserMessage: event: obj -> bool

// FallbackAction 执行
type IActionExecutor =
    abstract SendContinue: sessionID: string * model: FallbackModel -> JS.Promise<unit>
    abstract AbortSession: sessionID: string -> JS.Promise<unit>
    abstract FetchMessages: sessionID: string -> JS.Promise<MessageWithParts list>
    abstract PropagateFailure: sessionID: string -> JS.Promise<unit>
```

## 6. 宿主适配设计

### 公共流程

```
宿主事件
  → IEventTranslator 翻译为 FallbackEvent
  → FallbackRuntimeState 读取/更新状态
  → Kernel StateMachine.transition 决策
  → IActionExecutor 执行 FallbackAction
  → 返回 consumed 标志
```

### consumed 标志机制

```fsharp
type FallbackHookResult = { Consumed: bool }
```

- `Consumed = true` → 事件被 fallback 消费，不传播给 Nudge/Subagent
- `Consumed = false` → 事件正常传播

传播规则：

| Phase | SessionError | SessionBusy | SessionIdle |
|---|---|---|---|
| Idle | consumed=true（进入 retry/fallback） | consumed=false | consumed=false（检查工具文本） |
| Retrying | consumed=true | consumed=false（恢复 Idle） | consumed=false |
| Scanning | consumed=true（推进扫描） | consumed=false（扫描成功） | consumed=false |
| Exhausted | consumed=**false**（传播给 Nudge） | consumed=false | consumed=false |
| Propagated | consumed=false | consumed=false | consumed=false |

**关键**：`Exhausted` 时 `Consumed = false`，让 Nudge/Subagent 看到失败，正常传播给父代理。这是"内循环放弃才对接对话结束"的实现。

### Opencode FallbackHooks

注入点：`SessionLifecycleObserver.fs` 第 68-69 行之间，`decodeNudgeHostEvent` 后、`NudgeState.handleEvent` 前。

```fsharp
// SessionLifecycleObserver.handleEvent 内
let nudgeEvent = decodeNudgeHostEvent eventType props
// ← 插入 FallbackHooks
let fallbackResult = handleFallbackEvent sessionID eventType props nudgeEvent
if not fallbackResult.Consumed then
    let nextState, wantsNudge = NudgeState.handleEvent state sessionID nudgeEvent
    ...
```

模型切换：扩展 `OpencodeSessionEventCodec.createPromptBody` 支持可选 `model` 参数。

### Mux FallbackHooks

注入点：`Mux/EventHook.fs` 事件处理函数内，在现有处理前插入 fallback 拦截。

模型切换：`Mux/Delegate.fs` 的 `createInput` 已支持 `modelString`，fallback 时注入 model。

### Omp FallbackHooks

注入点：`Omp/PluginCore.fs` 的 `registerAbortHandler` 内，在 `deactivateReview + clearNudgeSession` 前插入 fallback 拦截。

模型切换：`Omp/ChildSession.fs` 的 `runSubagent` 内，prompt 时注入 model 参数。

**约束**：Omp FallbackHooks 仅依赖 Kernel + Shell，禁止引用 Opencode/Mux。

## 7. 内循环自闭环

### 机制

子代理 session.error 被 FallbackHooks 拦截（consumed=true）→ SubagentIo 不看到错误 → FallbackHooks 发 continue → 子代理继续工作。只有 Exhausted 时 consumed=false → SubagentIo 看到错误 → reject 父代理 promise。

### 流程

```
子代理 session.error (model A)
  → FallbackHooks 拦截 → consumed=true
  → Kernel: failureCount++, 确定 scanStart
  → 发 continue 给 chain[scanStart] (model B)
  → 子代理继续工作（SubagentIo promise 仍 pending）
  → 子代理正常完成 → session.idle → SubagentIo resolve（父代理收到结果）

  OR

  → model B 也 error → FallbackHooks 再次拦截 → consumed=true
  → 推进 scanIndex → 发 continue 给 chain[next] (model C)
  → ...直到成功或耗尽

  OR

  → 全部耗尽 → FallbackHooks → consumed=false
  → SubagentIo 看到 session.error → reject
  → 父代理收到失败通知
```

### 子代理 sessionID 关联

通过 `ChildAgentRegistry`：
- `registry.LookupChildAgent(sessionID)` → 判断是否子代理
- `registry.ResolveSubsessionParentID(sessionID)` → 找父代理
- 子代理 session.error → 查 registry → 是子代理 → 走内循环自闭环

## 8. 事件驱动能力映射

从 auto-resume 借鉴，全部去时间依赖：

| 能力 | 触发事件 | 动作 | 零时间实现 |
|---|---|---|---|
| 工具文本检测 | session.idle | fetch messages → 正则扫描 XML 工具调用 → 发恢复 prompt | 立即执行，无延迟 |
| 幻觉循环检测 | 每次 send continue | continueCount++ → ≥ 阈值 → abort+resume | 纯计数，无时间窗 |
| 本 session busy/idle 跟踪 | session.status (busy→idle) | busyCount 归零，仅本 session 本地状态 | 事件驱动，无等待 |
| task_complete | tool 调用 | 标记 TaskComplete=true → 停止恢复 | 经 ToolCatalog 注册 |
| todo 感知续推 | session.idle | 检查 todos → 全 completed → 跳过 continue | 事件驱动 |
| ESC 取消 | session.error (MessageAborted) | 标记 Cancelled=true → 停止恢复 | 同 |
| 工具执行暂停 | session.idle + 有 pending tool parts | 不触发恢复 | 事件状态检查，非时间检查 |

### 不实现的能力

| 能力 | 原因 |
|---|---|
| stall detection (48s 超时) | 需要定时器，且会杀长工具连接（auto-resume bug） |
| session discovery (定时 list) | 需要定时器，改用 session.created/updated 事件 |
| backoff 延时 | 需要定时器，改为立即重试 + 完全平方数启发式 |

## 9. Nudge 交互

### 优先级

```
session.error
  → FallbackHooks 先拦截
      模型错误 → Fallback 处理 → consumed=true
      Fallback 耗尽 → consumed=false → 传播给 Nudge
  → Nudge 看到 error（仅 Fallback 耗尽时）
      → 审查重试逻辑
```

### 错误路由

| 错误类型 | Fallback 动作 | Nudge 动作 |
|---|---|---|
| 模型错误（401/429/5xx/isRetryable） | 拦截 + fallback | 不感知 |
| MessageAborted | 标记取消 | 不感知 |
| Fallback 耗尽 | consumed=false | 看到 error → 审查重试 |
| 审查相关错误 | 不拦截 | 正常处理 |

### 两循环正交

- 内循环（Fallback）：连接层面恢复，模型切换，零时间
- 外循环（Nudge）：审查层面重试，提交→审查→修改→再提交
- 内循环优先：Fallback 未放弃前，Nudge 不感知错误

## 10. 架构约束

### 零定时器强制

```
架构测试：src/Kernel/FallbackKernel/、src/Shell/Fallback*、src/Opencode/Fallback*、
         src/Mux/Fallback*、src/Omp/Fallback*
         无 setTimeout | setInterval | Date.now
```

### Kernel 纯度

```
架构测试：src/Kernel/FallbackKernel/ 无 Dyn 引用、无 Shell 引用、无 Node 引用
```

### Omp 隔离

```
架构测试：src/Omp/Fallback* 无 Opencode/Mux 引用
```

### 配置 SSOT

```
AGENTS.md models: 存在 → 覆盖 opencode.json agent model
架构测试：FallbackConfigCodec 是 models: 段的唯一解析器
```

## 11. 测试计划

### 纯函数测试

| 测试名 | 验证点 |
|---|---|
| `isPerfectSquare` 边界 | 0→false, 1→true, 2→false, 4→true, 9→true, 15→false, 16→true |
| `scanStartIndex` 完全平方数 | k=1→0, k=4→0, k=9→0 |
| `scanStartIndex` 非完全平方数 | k=2→N, k=3→N, k=5→N |
| `updateFailureCount` 三分支 | N'<N→0, N'=N→k, N'>N→k+1 |
| `classifyError` 优先级 | MessageAborted→Ignore, 401→ImmediateFallback, isRetryable=true→RetrySame, ... |
| `transition` 状态转换 | Idle+error→Retrying, Retrying+busy→Idle, Scanning+error→推进, Scanning+busy→更新k, Exhausted→Propagate |

### Shell 测试

| 测试名 | 验证点 |
|---|---|
| `FallbackConfigCodec` 解析 | frontmatter obj → FallbackConfig，default + agents 链 |
| `parseModelId` 格式 | `"provider/model"` → {ProviderID; ModelID}，`"provider/model:variant"` → 含 Variant |
| `parseModelEntry` 简写 vs 完整 | string → FallbackModel，object → 含 temperature 等 |
| `FallbackRuntimeState` 生命周期 | getOrCreate/update/getChain/cleanup |

### 集成测试

| 测试名 | 验证点 |
|---|---|
| 内循环自闭环 | 子代理 session.error → fallback 成功 → 父代理不收到结束事件 |
| 内循环放弃 | 全模型耗尽 → consumed=false → 父代理收到失败 |
| 完全平方数扫描 | k=1 从 0 扫，k=2 从 N 扫，k=4 从 0 扫 |
| 工具文本检测 | session.idle → 扫描消息 → XML 工具调用 → 发恢复 prompt |
| 幻觉循环 | 连续 N 次 continue → abort+resume |
| 本 session busy/idle 跟踪 | busyCount 归零，仅本 session 本地状态 |
| task_complete | tool 调用 → 停止恢复 |
| todo 感知续推 | 全 completed → 跳过 continue |
| ESC 取消 | MessageAborted → 停止恢复 |
| 配置覆盖 | AGENTS.md models: 存在 → opencode.json agent model 被覆盖 |
| 三宿主适配 | Opencode/Mux/Omp 各有 FallbackHooks 且 consumed 标志正确 |

### 架构测试

| 测试名 | 验证点 |
|---|---|
| 零定时器 | Fallback 相关文件无 setTimeout/setInterval/Date.now |
| Kernel 纯度 | FallbackKernel 无 Dyn/Shell/Node 引用 |
| Omp 隔离 | Omp/Fallback* 无 Opencode/Mux 引用 |

## 12. 实现切片

| 切片 | 内容 | 依赖 |
|---|---|---|
| Slice 1 | Kernel: FallbackKernel（Types+Decision+Recovery+StateMachine） | 无 |
| Slice 2 | Shell: FallbackConfigCodec + FallbackRuntimeState | Slice 1 |
| Slice 3 | Shell: FallbackEventBridge 公共接口 | Slice 1 |
| Slice 4 | Opencode: FallbackHooks + consumed 机制 + createPromptBody 扩展 | Slice 2+3 |
| Slice 5 | 内循环自闭环（SessionLifecycleObserver 注入 + Subagent 拦截） | Slice 4 |
| Slice 6 | auto-resume 能力事件驱动版（工具文本/幻觉循环/task_complete/todo/ESC） | Slice 4+5 |
| Slice 7 | Mux: FallbackHooks | Slice 2+3 |
| Slice 8 | Omp: FallbackHooks | Slice 2+3 |
| Slice 9 | 配置覆盖（AGENTS.md models: → opencode.json 覆盖） | Slice 4+7 |
| Slice 10 | 测试 + 架构约束 | 全部 |

Slice 1-3 可顺序执行。Slice 4 后，5+6/7/8 可并行。Slice 9 依赖 4+7。Slice 10 最后。

## 13. Phase 划分

### Phase 1（本次实现）

- 完全平方数 fallback 链 + 实际 continue 探测
- 内循环自闭环（consumed 标志）
- 错误分类（isRetryable + HTTP 状态码）
- 工具文本检测（事件驱动）
- 幻觉循环检测（纯计数）
- 本 session busy/idle 跟踪（事件驱动）
- task_complete 工具
- todo 感知续推
- ESC 取消
- 配置覆盖（AGENTS.md models: → opencode.json）
- 三宿主适配

### Phase 2（未来）

无。上下文溢出依靠 opencode 自身压缩机制，本系统不干预、不破坏。
