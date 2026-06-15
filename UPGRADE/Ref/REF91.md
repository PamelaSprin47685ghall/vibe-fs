# REF91: Agentic 模式的消息 ID 管理

## 1. ID 生成

```typescript
function newMsgId(prefix = 'msg'): string {
    return `${prefix}-${nanoid(8)}`
}

// 使用示例:
const userMsg = { id: newMsgId('user'), ... }      // user-xxxxxxxx
const agentMsg = { id: newMsgId('agent'), ... }     // agent-xxxxxxxx
const systemMsg = { id: newMsgId('system'), ... }   // system-xxxxxxxx
```

## 2. 初始状态构建

```typescript
function createInitialState(initialContent: string): AgenticState {
    return {
        id: `agentic-${nanoid(10)}`,
        originalContent: initialContent,
        currentContent: initialContent,
        messages: [createInitialUserMessage()],
        isProcessing: false,
        isComplete: false,
        contentHistory: [createInitialHistoryEntry(initialContent)]
    }
}
```

## 3. Deepthink Pipeline ID

```typescript
const process.id = `deepthink-${nanoid(12)}`
```

## 4. Contextual State ID

```typescript
const state.id = `contextual-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
```

## 5. Contextual 消息 ID

```typescript
function newMessageId(prefix: string): string {
    return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

const mainMsg = { id: newMessageId('main'), ... }
const iterMsg = { id: newMessageId('iter'), ... }
const stratMsg = { id: newMessageId('strat'), ... }
const memMsg = { id: newMessageId('mem'), ... }
const systemMsg = { id: newMessageId('system'), ... }
```

## 6. Deepthink Critique ID

```typescript
const critiqueId = `critique-${strategy.id}-${globalIteration || 'initial'}-${nanoid(4)}`
```
