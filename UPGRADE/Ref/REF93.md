# REF93: UI 与核心的通信机制

## 1. 基于事件的通信

| 事件 | 触发时机 | 监听者 |
|------|----------|--------|
| `appModeChanged` | 模式切换 | UI 组件 |
| `beforeRenderActiveMode` | 渲染前 | MainContent |
| `updateDeepthinkTabUI` | Tab 切换 | Tab 样式更新 |
| `selectedModelChanged` | 模型切换 | 配置面板 |

## 2. 观察者模式（提示词管理器）

```typescript
class AgenticPromptsManager {
    private observers: Set<AgenticPromptsObserver> = new Set()
    
    subscribe(observer): () => void {
        this.observers.add(observer)
        observer(this.currentPrompts)  // 立即通知
        return () => this.observers.delete(observer)
    }
    
    private notifyObservers(): void {
        for (const observer of this.observers) {
            observer(this.currentPrompts)
        }
    }
}
```

## 3. DeepthinkConfigController 事件

```typescript
// 配置变更时触发 'configchange' 事件
controller.addEventListener('configchange', () => renderPanel(controller))
```

## 4. 回调机制 (Contextual)

```typescript
let onStateUpdated: ((state: ContextualState) => void) | null = null
let onContentUpdated: ((content: string) => void) | null = null

// ContextualCore 更新 → 触发回调 → Contextual.tsx 更新 React
setContextualStateUpdateCallback((state) => {
    if (contextualUIRoot) {
        updateContextualUI(contextualUIRoot, state, stopContextualProcess)
    }
})
```

## 5. 状态拉取

```typescript
// Agentic 通过回调 + 状态对象传递
// Contextual 通过回调 + updateContextualUI
// Deepthink 通过 pipeline 引用 + render()
```
