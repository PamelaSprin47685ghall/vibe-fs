# REF52: Contextual 模式——记忆代理算法

## 1. 调用时机

每 10 轮迭代触发一次记忆压缩：
```typescript
if (turnsSinceLastCondense >= 10) {
    // 构建记忆代理上下文
    const memoryPrompt = [...]
    const memoryResult = await callContextualAgent('Memory Agent', ...)
    // 更新记忆快照
    activeContextualState.memorySnapshots.push(...)
    // 压缩消息历史
    condenseMessages()
}
```

## 2. 记忆代理输入

```typescript
const memoryPrompt = [
    `Initial User Request:\n${initialUserRequest}`,
    ...memorySnapshots.map((snap, idx) => 
        `Memory V${idx + 1}:\n${snap.memory}\nFinal Generation:\n${snap.finalGeneration}`
    ),
    'Recent Iterations to Analyze:',
    ...completeIterations.map(m => 
        `[Iteration ${m.iterationNumber}] ${m.role}: ${m.content}`
    ),
    'Task: 总结有效和无效的方法'
].join('\n')
```

## 3. 消息历史压缩

记忆代理完成后，各代理的消息历史被压缩：
```typescript
mainGeneratorMessages = [
    initialReqMessage,           // 原始需求
    memoryCondenseMessage,       // 记忆摘要
    new HumanMessage('Latest Context:\n'),
    ...mainLoopMessages,         // 最近的循环消息
    new HumanMessage(combinedCritique)  // 当前批判
]
```

## 4. 记忆快照结构

```typescript
interface MemorySnapshot {
    memory: string            // 记忆文档
    finalGeneration: string   // 压缩时的最佳生成
    condensePoint: number     // 在哪个迭代压缩的
}
```

## 5. Contextual 与 Deepthink 记忆的对比

| 特性 | Contextual | Deepthink EDFS |
|------|-----------|----------------|
| 触发条件 | 每 10 轮 | 每 5 个全局迭代 |
| 输入 | 所有迭代摘要 | 最近 5 个分支历史 |
| 递归 | 手动拼接 | 自动传入前次记忆 |
| 输出 | 自然语言摘要 | 结构化章节 |
