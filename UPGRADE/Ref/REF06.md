# REF06: Agentic 模式——架构概览

## 1. 定位

Agentic 模式是一个**自主精炼代理**，通过 LangGraph 有向图工具调用，对工作草稿进行迭代改进。用户提供初始内容，代理自主决定如何改进、验证，直到满意后退出。

## 2. 文件结构

| 文件 | 职责 |
|------|------|
| Agentic.tsx | React 组件入口，创建 AgenticEngine |
| AgenticCore.ts | 核心引擎，状态管理，Graph 运行 |
| AgenticToolGraph.ts | LangGraph 图定义、工具定义、工具执行 |
| AgenticEdits.ts | 文本编辑命令（搜索替换、插入删除） |
| AgenticTypes.ts | 类型定义 |
| AgenticUI.tsx | UI 组件（消息卡片、文本面板、活动面板） |
| AgenticUI_Bridge.tsx | 旧版桥接，提供 imperative API |
| AgenticModePrompt.ts | 系统提示词 |
| AgenticPromptsManager.ts | 提示词管理器 |
| AgenticPromptsContent.tsx | 提示词编辑 UI |
| AgenticUI.css | 样式 |

## 3. 核心工作流

```
start() → runGraph() → 创建 AgenticGraph
  → agent 节点（生成回应 + 工具调用）
  → tools 节点（执行工具）
  → agent 节点（继续）
  → ... → Exit → 完成
```

## 4. 关键设计

- 使用 LangGraph 的 `StateGraph`，包含 `agent` 和 `tools` 两个节点
- 支持 Gemini（通过 `invokeGeminiToolAgentTurn`）和 OpenAI/Anthropic（标准 tool calling）
- 最多 48 步递归限制
- 支持 AbortController 取消
- 所有工具调用前需有一段可见推理文本
