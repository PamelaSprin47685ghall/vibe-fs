# REF32: LangGraph 工具运行时 (LangGraphToolRuntime.ts)

## 1. 目的

为不支持原生 tool calling 的提供商提供统一工具调用接口，同时支持标准 LangChain 工具调用。

## 2. Provider 解析

```typescript
resolveProviderForModel(modelName: string): ResolvedProvider
// 返回: { providerName: 'gemini' | 'openai' | 'openrouter' | 'local' | 'anthropic',
//          providerConfig: ProviderConfig }
```

## 3. 模型创建

| 提供商 | 模型类 | 特殊配置 |
|--------|--------|----------|
| OpenAI | ChatOpenAI | `dangerouslyAllowBrowser: true` |
| OpenRouter | ChatOpenAI | baseURL + HTTP-Referer |
| Local | ChatOpenAI | 自动补全 `/v1` |
| Anthropic | ChatAnthropic | maxTokens=4096 |
| Gemini | 特殊处理 | 通过 callAI + functionDeclarations |

## 4. Gemini 工具调用

对于 Gemini 提供商，不使用 LangChain，而是通过 `invokeGeminiToolAgentTurn()`：
1. 将 BaseMessage[] 转换为 Gemini 内容格式
2. 构建 functionDeclarations
3. 通过 callAI 调用
4. 将 Gemini 响应转换回 AIMessage

### 关键转换
- HumanMessage → 'user' role
- AIMessage → 'model' role + functionCall
- ToolMessage → functionResponse
- 如果是 Gemini 原生格式（含 inlineData、functionCall），则直接传递

## 5. 消息内容提取

```typescript
messageContentToText(content): string
```

处理多种内容格式：
- 纯字符串 → 直接返回
- 复杂数组 → 提取 text 部分
- 排除 thinking 部分
