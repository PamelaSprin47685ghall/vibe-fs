# REF55: 状态序列化处理器 (StateSerializer/handlers/)

## 1. 模式注册

```typescript
// handlers/index.ts — 自动注册
registerModeHandler(deepthinkStateHandler)
registerModeHandler(agenticStateHandler)
registerModeHandler(contextualStateHandler)
registerModeHandler(adaptiveDeepthinkStateHandler)
registerModeHandler(dynamicComputeStateHandler)
```

## 2. 各处理器职责

| Handler | 模式 | 状态类型 |
|---------|------|----------|
| DeepthinkStateHandler | deepthink | `DeepthinkExportState` (含 pipeline + pool + tab) |
| AgenticStateHandler | agentic | `AgenticState` |
| ContextualStateHandler | contextual | `ContextualState` |
| AdaptiveDeepthinkStateHandler | adaptive-deepthink | `AdaptiveDeepthinkStoreState` |
| DynamicComputeStateHandler | dynamic-compute | `DCAPipelineState` |

## 3. DeepthinkStateHandler 详解

```typescript
interface DeepthinkExportState {
    pipeline: DeepthinkPipelineState | null
    solutionPoolVersions: Array<{ content, title, timestamp }> | null
    activeTabId: string
}
```

导出时，额外收集 `SolutionPool` 的版本历史。

## 4. ModeStateHandler 接口

```typescript
interface ModeStateHandler<TState = unknown> {
    readonly modeName: ApplicationMode
    
    getFullState(): TState | null                    // 获取当前状态
    restoreState(state: TState | null): void         // 恢复状态
    renderAfterImport(): void                        // 导入后渲染
    
    getEmbeddedState?(): unknown | null               // 获取嵌入状态
    restoreEmbeddedState?(state: unknown | null): void // 恢复嵌入状态
}
```
