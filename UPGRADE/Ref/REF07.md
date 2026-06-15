# REF07: Agentic 模式——核心引擎 AgenticEngine

## 1. 状态管理 (Core/AgenticCore.ts)

```typescript
interface AgenticState {
    id: string
    originalContent: string     // 初始内容
    currentContent: string      // 当前内容
    messages: AgenticMessage[]  // 消息历史
    isProcessing: boolean
    isComplete: boolean
    error?: string
    contentHistory: ContentHistoryEntry[]  // 内容变更历史
}
```

## 2. 消息类型

| 角色 | 来源 | 内容 |
|------|------|------|
| `user` | 系统创建 | 初始用户消息 |
| `agent` | AI | 文本段 + 工具调用段 |
| `system` | 工具结果/错误 | 结构化结果块 |

### ResponseSegment 结构
Agent 消息包含多个 segment：
- `{ kind: 'text', text: string }` — 可见推理
- `{ kind: 'tool', tool: ToolCall }` — 工具调用指示器

### SystemBlock 结构
系统消息包含多个 block：
- `{ kind: 'error', message }` — 错误信息
- `{ kind: 'tool_result', tool, result, toolCall }` — 工具执行结果

## 3. 工具集 (AgenticToolGraph.ts)

| 工具 | 描述 |
|------|------|
| `read_current_content` | 读取当前草稿（支持行范围） |
| `multi_edit` | 批量文本编辑（最多 20 个操作） |
| `searchacademia` | 单查询 arXiv 搜索 |
| `searchacademia_and` | 多术语 AND arXiv 搜索 |
| `verify_current_content` | 独立验证当前草稿 |
| `Exit` | 完成精炼过程 |

## 4. 编辑命令 (AgenticEdits.ts)

| 类型 | 参数 | 行为 |
|------|------|------|
| `search_and_replace` | [find, replace] | 查找替换 |
| `delete` | [toDelete] | 删除 |
| `insert_before` | [marker, text] | 在标记前插入 |
| `insert_after` | [marker, text] | 在标记后插入 |
