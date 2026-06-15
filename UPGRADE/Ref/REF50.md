# REF50: 消息格式详解——Agentic 消息系统

## 1. AgenticMessage

```typescript
interface AgenticMessage {
    id: string
    role: 'agent' | 'system' | 'user'
    content: string
    timestamp: number
    status?: 'success' | 'error' | 'processing'
    segments?: ResponseSegment[]   // agent 消息
    blocks?: SystemBlock[]         // system 消息
}
```

## 2. ResponseSegment（Agent 消息段）

```typescript
type ResponseSegment =
    | { kind: 'text'; text: string }        // 可见推理文本
    | { kind: 'tool'; tool: ToolCall }      // 工具调用指示
```

## 3. SystemBlock（系统消息块）

```typescript
type SystemBlock =
    | { kind: 'error'; message: string }
    | { kind: 'tool_result'; tool: string; result: string; toolCall?: ToolCall }
```

## 4. ToolCall

```typescript
type ToolCall =
    | { type: 'read_current_content'; params?: number[] }
    | { type: 'verify_current_content' }
    | { type: 'searchacademia'; query: string }
    | { type: 'searchacademia_and'; terms: string[] }
    | { type: 'multi_edit'; operations: DiffCommand[] }
    | { type: 'Exit' }
```

## 5. ContentHistoryEntry

```typescript
interface ContentHistoryEntry {
    content: string
    title: string
    timestamp: number
}
```

### Evolution Viewer

内容历史被用于演化查看器（`DiffModal/EvolutionViewer`），以时间线形式展示内容的各版本变化。
