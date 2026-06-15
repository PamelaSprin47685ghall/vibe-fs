# REF92: Agentic 模式的初始消息与历史

## 1. 初始消息

```typescript
function createInitialUserMessage(): AgenticMessage {
    return {
        id: newMsgId('user'),
        role: 'user',
        content: 'Started agentic refinement run.',
        timestamp: Date.now()
    }
}
```

## 2. 初始内容历史

```typescript
function createInitialHistoryEntry(initialContent: string): ContentHistoryEntry {
    return {
        content: initialContent,
        title: 'Initial Content',
        timestamp: Date.now()
    }
}
```

## 3. 初始 Graph 输入

```typescript
const initialGraphInput = {
    messages: [
        new HumanMessage('Refine the current working draft. Read the draft when needed, then make targeted improvements until it is verified and complete.')
    ],
    currentContent: this.state.currentContent,
    contentHistory: this.state.contentHistory,
    verifierReports: [],
    verificationCount: 0,
    lastVerifiedContent: null,
    shouldExit: false
}
```

## 4. 消息同步

```typescript
private async syncGraphState(graphState, processedMessages):
    // 只处理新消息（从 processedMessages 开始）
    for (const message of graphState.messages.slice(processedMessages)) {
        const mapped = toAgenticMessage(message)
        if (mapped) nextMessages.push(mapped)
    }
    
    this.updateState({
        messages: nextMessages,
        currentContent: graphState.currentContent,
        contentHistory: graphState.contentHistory
    })
```

## 5. 状态监听

```typescript
// AgenticEngine 使用回调通知外部:
interface AgenticEngineCallbacks {
    onStateChange: (state: AgenticState) => void
    onContentUpdated?: (content: string, isComplete?: boolean) => void
    onForceRender?: () => Promise<void>
}
```
