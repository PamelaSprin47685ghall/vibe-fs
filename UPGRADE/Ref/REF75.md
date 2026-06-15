# REF75: AbortController 的使用模式

## 1. 统一取消模式

所有模式都使用 `AbortController` 实现用户取消：

```typescript
let abortController: AbortController | null = null

// 开始
abortController = new AbortController()

// 取消
abortController?.abort()
abortController = null
```

## 2. Agentic 模式

```typescript
stop(): void {
    globalState.isAgenticRunning = false
    globalState.isGenerating = false
    this.abortController?.abort()
    updateControlsState()
}

// 在 runGraph 中使用:
const stream = await graph.stream(initialGraphInput, {
    signal: this.abortController?.signal  // 传给 LangGraph
})
```

## 3. Contextual 模式

```typescript
stopContextualProcess(): void {
    if (abortController) {
        abortController.abort()
    }
    globalState.isContextualRunning = false
    // 状态清理...
}

// 在等待延迟时监听中止:
await new Promise((resolve, reject) => {
    const timeout = setTimeout(resolve, 1000)
    abortController?.signal.addEventListener('abort', () => {
        clearTimeout(timeout)
        reject(new Error('Process stopped by user'))
    })
}).catch(() => { return })  // 静默处理中止
```

## 4. Deepthink 模式

```typescript
// 在 callAgent 中:
if (process.isStopRequested) throw new PipelineStopRequestedError('...')

// 在主循环中:
for (let globalIteration = 2; globalIteration <= depth; globalIteration++) {
    if (process.isStopRequested) throw new PipelineStopRequestedError('Stopped by user.')
    // ...
}
```

## 5. 取消后的状态

| 模式 | 取消状态 |
|------|----------|
| Agentic | isComplete: true, isProcessing: false |
| Contextual | isRunning: false, isProcessing: false |
| Deepthink | status: 'stopped' |
