# REF39: 提示词管理器系统

## 1. 设计模式

每种模式有自己的提示词管理器，使用**观察者模式**，支持 subscribe/notify。

## 2. 管理器列表

| 管理器 | 模式 | 管理内容 |
|--------|------|----------|
| AgenticPromptsManager | Agentic | systemPrompt, verifierPrompt, model |
| ContextualPromptsManager | Contextual | 4 个代理的 system prompt + 4 个模型选择 |
| DeepthinkPromptsManager | Deepthink | 12 个代理的 system prompt + 12 个模型选择 |
| AdaptiveDeepthinkPromptsManager | Adaptive Deepthink | 模式专属提示词 |
| DCAPromptsManager | DCA | pool_generator, local_pool_agent |

## 3. 订阅机制

```typescript
// 原始 ref 模式（Contextual/Deepthink）
manager.subscribe(callback) → unsubscribe() 函数
// 或
manager.subscribe(callback) // Agentic，立即调用一次
```

## 4. 初始化

在 `App.initializeCustomPromptTextareas()` 中初始化：
```typescript
routingManager.initializePromptsManager(
    { current: 全局状态的 customPrompts 引用 },
    ...
)
```

使用 `{ current: ... }` 引用模式而非值模式，确保所有组件共享同一状态。

## 5. 默认值

每种模式的提示词管理器在初始化时使用各自的 `createDefaultCustomPrompts*()` 函数填充默认值。如果检测到空值则自动填充。
