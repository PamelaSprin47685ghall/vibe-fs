# REF08: Agentic 模式——LangGraph 工具图

## 1. 图结构

```
START → agent → shouldRunTools({tool_calls}? → tools : END)
                tools → afterTools({shouldExit}? → END : agent)
```

`agent` 节点：调用 LLM，可能返回工具调用
`tools` 节点：顺序执行所有工具调用

## 2. 关键组件

### GraphState 注解
```typescript
const AgenticGraphAnnotation = Annotation.Root({
    messages: Annotation<BaseMessage[]>({ reducer: messagesStateReducer }),
    currentContent: Annotation<string>(),
    contentHistory: Annotation<ContentHistoryEntry[]>(),
    verifierReports: Annotation<string[]>(),
    verificationCount: Annotation<number>(),
    lastVerifiedContent: Annotation<string | null>(),
    shouldExit: Annotation<boolean>()
})
```

### createAgenticGraph(options)
- 解析提供商（Gemini vs 其他）
- 绑定工具定义到模型
- 如果是 Gemini，使用 `invokeGeminiToolAgentTurn`
- 其他提供商使用标准 `createToolCallingAgentModel().bindTools()`

## 3. Verifier 验证机制

- 使用独立的 verifier 系统提示
- 低温（0.2）调用，严格审查
- 返回结构化验证报告
- Exit 前必须验证通过

## 4. 工具执行顺序

对 agent 返回的每个 tool_call：
1. 更新 `workingState`（累积状态变更）
2. 执行工具，捕获结果
3. 处理错误（返回 `[TOOL_ERROR: ...]` 消息）
4. 如果是 `multi_edit`，每步操作独立计数 OK/FAIL
5. 如果是 `Exit`，检查 `lastVerifiedContent === currentContent`
