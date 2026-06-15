# REF83: Pipeline 状态迁移图

## 1. Deepthink Pipeline 状态

```
idle → processing → (完成) → completed
                 → (错误) → error
                 → (停止) → stopping → stopped
                 → (取消) → cancelled

子状态:
  processing → retrying (重试中)
  retrying → processing (重试成功)
  retrying → error (重试用尽)
```

## 2. Agent 状态

```typescript
type AgentStatus = 'pending' | 'processing' | 'retrying' | 'completed' | 'error' | 'cancelled'
```

状态迁移:
```
pending → processing → (成功) → completed
                     → (失败) → retrying → processing
                                          → error
                                          → cancelled
```

## 3. Contextual Pipeline 状态

```
isRunning = true → isProcessing = true → (迭代中)
               → (用户停止) → isRunning = false, isProcessing = false
               → (退出条件) → stopContextualProcess()
```

## 4. Agentic Pipeline 状态

```
isProcessing = false, isComplete = false
  → start() → isProcessing = true
  → graph.stream (按消息逐步更新)
  → 完成 → isProcessing = false, isComplete = true
  → stop() → isProcessing = false, isComplete = true
```

## 5. 全局 isGenerating 状态

```typescript
globalState.isGenerating = true  // 任意模式在生成时
  // → UI 控制面板禁用导出/导入/模式切换
globalState.isGenerating = false // 生成完成或取消
  // → UI 恢复
```
