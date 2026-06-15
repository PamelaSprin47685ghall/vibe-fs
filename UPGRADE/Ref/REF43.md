# REF43: 大模型 AI Service 调用

## 1. 调用接口

```typescript
callAI(
    messages: StructuredMessage[], 
    temperature: number,
    modelToUse: string,
    systemInstruction?: string,
    isJson?: boolean,
    topP?: number,
    extraOptions?: object
): Promise<AIResponse>
```

## 2. AIResponse 结构

```typescript
interface AIResponse {
    text: string
    candidates?: GenerateContentResponse[]
    // ... 其他元数据
}
```

## 3. Gemni 特殊处理

对于 Gemini 提供商，`callAI` 内部：
- 使用 `@google/genai` SDK
- 构建 `GenerateContentRequest`
- 支持 `functionDeclarations` 工具
- 支持 `ThinkingConfig`

## 4. Provider 配置

```typescript
interface ProviderConfig {
    name: string         // 'google' | 'openai' | 'anthropic' | 'openrouter'
    apiKey?: string
    baseUrl?: string
    isConfigured: boolean
    // ...
}
```

通过 `ProviderManager` 管理多个提供商的 API key 和配置。

## 5. 调用路径

```
React 组件/代理
  → callAI (统一入口)
  → Provider-specific adapter
  → 底层 API（Google AI / OpenAI / Anthropic）
  → 返回统一 AIResponse
```
