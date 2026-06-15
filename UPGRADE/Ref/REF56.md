# REF56: LangChain 消息转换详解

## 1. 核心消息类型

```typescript
import { AIMessage, BaseMessage, HumanMessage, SystemMessage, ToolMessage } from '@langchain/core/messages'
```

## 2. BaseMessage 到 AgenticMessage 的转换

```typescript
toAgenticMessage(message: BaseMessage): AgenticMessage | null
  HumanMessage → null (跳过)
  AIMessage → buildAgentMessage()
    → 提取 text 段 + tool_calls → ResponseSegment[]
  ToolMessage → buildSystemMessage()
    → 提取 status/artifact → SystemBlock[]
```

## 3. BaseMessage 到 Contextual 消息的转换

Contextual 模式不使用 LangChain 消息传递（改用 `HistoryMessage`）但 Python 工具运行时内部使用 LangChain 消息。

## 4. Gemini Content 格式转换

`buildGeminiContents(messages: BaseMessage[]): GeminiContent[]`

转换规则：
- HumanMessage → `{ role: 'user', parts: [...] }`
- AIMessage → `{ role: 'model', parts: [...] }`
  - 如果有 tool_calls → 添加 `functionCall` parts
- ToolMessage → 合并到最近同 role，添加 `functionResponse` parts

## 5. Gemini AIMessage 重建

```typescript
createGeminiAiMessage(response): AIMessage
  → 提取 response.candidates[0].content.parts
  → 找到 functionCall parts → 提取为 tool_calls
  → 返回新 AIMessage
```

## 6. 消息内容提取

```typescript
messageContentToText(content: BaseMessage['content']): string
  // 处理 string | 复杂数组 两种格式
  // 跳过 thought 部分
  // 只提取 text 字段
```
