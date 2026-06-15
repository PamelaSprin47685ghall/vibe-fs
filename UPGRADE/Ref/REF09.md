# REF09: Agentic 模式——工具图内部调度详解

## 1. Graph 节点的执行顺序

LangGraph 在 Agentic 模式中的调度逻辑：

```
START → agent node (AI 调用)
  ↓
shouldRunTools → 检查 lastMessage.tool_calls
  ├── 有 tool_calls → tools node
  └── 无 tool_calls → END
  ↓
afterTools → 检查 state.shouldExit
  ├── true → END
  └── false → agent node (继续循环)
```

## 2. executeToolsNode 的批量执行

对每个 tool_call 按顺序执行，但所有 tool_call 的 ToolMessage 统一返回：
```typescript
for (const toolInvocation of lastMessage.tool_calls) {
    result = await executeToolCall(workingState, name, args, options)
    workingState = { ...workingState, ...result.statePatch }
    toolMessages.push(new ToolMessage({...}))
}
return { messages: toolMessages, ...workingState }
```

## 3. statePatch 机制

某些工具（multi_edit, verify, Exit）返回 `statePatch` 来修改 graph state：
- `multi_edit`: 更新 currentContent, contentHistory, 重置 lastVerifiedContent
- `verify_current_content`: 更新 verifierReports, verificationCount, lastVerifiedContent
- `Exit`: 设置 shouldExit = true

## 4. Gemini 与非 Gemini 的差异

| 特性 | Gemini | OpenAI/Anthropic |
|------|--------|-----------------|
| 模型创建 | 不创建 | `createToolCallingAgentModel()` |
| 工具绑定 | functionDeclarations | `.bindTools()` |
| 调用 | `invokeGeminiToolAgentTurn()` | `model.invoke()` |
| 回应的 tool_calls 提取 | 从 `functionCall` 部分提取 | 标准 AIMessage.tool_calls |

## 5. 验证器调用

`verify_current_content` 使用独立的 verifier 模型，通过 `callAI()` 低温（0.2）调用，返回纯文本验证报告。
