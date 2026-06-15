# REF74: 内容历史追踪与演化查看

## 1. 内容历史机制

Agentic 和 Contextual 模式都追踪内容的历史版本：

```typescript
interface ContentHistoryEntry {
    content: string
    title: string
    timestamp: number
}
```

## 2. Agentic 的内容历史

```typescript
contentHistory: [{
    content: initialContent,
    title: 'Initial Content',
    timestamp: Date.now()
}]

// multi_edit 成功后追加:
{
    content: currentContent,
    title: `After ${okCount} successful edits`,
    timestamp: Date.now()
}
```

## 3. Contextual 的内容历史

```typescript
// 每轮主生成后追加:
{
    content: mainGeneration,
    title: `Iteration ${iterationCount} - Main Generation`,
    timestamp: Date.now()
}
```

## 4. EDFS 的内容历史（Deepthink）

通过 `DeepthinkStrategyReplacementRecord.branchHistory` 追踪：
```typescript
interface BranchHistoryEntry {
    globalIteration: number
    branchIteration: number
    branchVersion?: number
    label: string
    solution: string
    critique: string
}
```

## 5. 方案池版本历史

```typescript
const solutionPoolVersions = new Map<string, SolutionPoolVersion[]>()
// 每次方案池生成后添加新版本
```

## 6. 演化查看器的使用

- Agentic: 按钮 `onClick → openEvolutionViewerFromHistory(state.contentHistory, state.id)`
- Contextual: 按钮 `onClick → openEvolutionViewerFromHistory(state.contentHistory, state.id)`
- Solution Pool: 按钮 `onClick → openSolutionPoolEvolution(pipelineId)`
