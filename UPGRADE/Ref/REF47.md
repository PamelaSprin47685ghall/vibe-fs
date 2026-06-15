# REF47: PipelineStopRequestedError 与中断处理

## 1. 错误类型

自定义错误类：
```typescript
class PipelineStopRequestedError extends Error {
    constructor(message: string) {
        super(message)
        this.name = "PipelineStopRequestedError"
    }
}
```

## 2. 中断链

```
用户点击 Stop 按钮
  → globalState.isAgenticRunning = false (或 isGenerating = false)
  → abortController.abort()
  → 各代理检查 isStopRequested / signal.aborted
  → throw PipelineStopRequestedError
  → 最终 finally 块清理状态
  → pipeline.status = 'stopped'
```

## 3. 各模式的中断入口

| 模式 | 中断函数 |
|------|----------|
| Agentic | `agenticEngine.stop()` |
| Contextual | `stopContextualProcess()` |
| Deepthink | `process.isStopRequested = true` |
| DCA | `stopDCAProcess()` |
| Adaptive Deepthink | `cleanupAdaptiveDeepthinkMode()` |

## 4. Deepthink 的中断检查点

在以下位置检查 `isStopRequested`：
- 每个 API 调用前
- 循环迭代开始前
- 每个主要阶段入口

## 5. 状态恢复

中断后：
1. `isGenerating = false`
2. `isAgenticRunning/isContextualRunning/... = false`
3. `updateControlsState()` 更新 UI 状态
4. 管道状态标记为 `'stopped'`（而非 `'error'`）
5. 保留已完成的中间结果供用户查看
