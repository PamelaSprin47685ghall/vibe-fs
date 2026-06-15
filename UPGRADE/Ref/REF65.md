# REF65: 演化查看器——Diff 与 Evolution

## 1. 用途

EvolutionViewer（`Styles/Components/DiffModal/EvolutionViewer`）用于可视化内容的演化历史。

## 2. 触发途径

### Agentic 模式
- CurrentTextPanel 中的"movie"图标按钮
- 参数：`contentHistory`, `state.id`

### Contextual 模式
- CurrentBestGenerationPanel 中的"Evolutions"按钮
- 参数：`contentHistory`, `state.id`

### Solution Pool
- `openSolutionPoolEvolution()` 函数
- 使用方案池版本历史

## 3. 实时更新

Agentic 和 Contextual UI 在每次内容变化时调用：
```typescript
const { updateEvolutionViewerIfOpen } = await import(...)
updateEvolutionViewerIfOpen(state.id, state.contentHistory)
```

## 4. 内容历史条目

```typescript
interface EvolutionEntry {
    content: string
    title: string
    timestamp: number
}
```

演化查看器以时间线形式列出所有历史版本，支持查看任意两个版本的 diff。
