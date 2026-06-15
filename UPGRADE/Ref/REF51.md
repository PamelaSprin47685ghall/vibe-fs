# REF51: Contextual 消息格式

## 1. ContextualMessage

```typescript
interface ContextualMessage {
    id: string
    role: 'main_generator' | 'iterative_agent' | 'memory_agent' 
         | 'strategic_pool_agent' | 'system'
    content: string
    timestamp: number
    iterationNumber: number
    status?: 'success' | 'error' | 'processing'
    blocks?: ContextualSystemBlock[]
    codeExecution?: CodeExecutionPart[]
}
```

## 2. ContextualSystemBlock

```typescript
type ContextualSystemBlock =
    | { kind: 'error'; message: string }
    | { kind: 'info'; message: string }
```

## 3. CodeExecutionPart

```typescript
interface CodeExecutionPart {
    code: string
    language: string
    output?: string
}
```

## 4. ContentHistoryEntry

与 Agentic 模式相同的类型：
```typescript
interface ContentHistoryEntry {
    content: string
    title: string
    timestamp: number
}
```

## 5. HistoryMessage

```typescript
interface HistoryMessage {
    role: 'system' | 'assistant' | 'user'
    content: string
    rawParts?: any[]
    loopMessages?: any[]
}
```

## 6. MemorySnapshot

```typescript
interface MemorySnapshot {
    memory: string           // 记忆文档
    finalGeneration: string  // 最终生成
    condensePoint: number    // 压缩时的迭代数
}
```
