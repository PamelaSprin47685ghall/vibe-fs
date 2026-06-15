# REF38: AI 提供商适配层

## 1. 提供商统一接口

所有 AI 调用通过 `callAI()` 统一入口，各提供商在内部适配。

## 2. 提供商管理

```typescript
class ProviderManager {
    // 管理多个 AI 提供商
    addProvider(config: ProviderConfig): void
    removeProvider(name: string): void
    getProvider(name: string): ProviderConfig | undefined
    getProviderConfigForModel(modelName: string): ProviderConfig | undefined
    // 根据模型名查找对应提供商
}
```

## 3. 模型到提供商的映射

通过 `resolveProviderForModel()` 解析：
- 检查所有已配置的提供商
- 返回匹配模型名的一个
- OpenAI 兼容的提供商（OpenAI, OpenRouter, Local）共用同一模型创建逻辑

## 4. OpenAI 兼容适配

```typescript
createOpenAICompatibleAgentModel(options, apiKey, configuration): ChatOpenAI
```

用于 OpenAI, OpenRouter, Local 三种提供商：
- OpenAI: 标准 OpenAI API
- OpenRouter: baseURL + custom headers
- Local: baseURL + 'not-needed' apiKey
- Local 端点自动补全 `/v1`

## 5. 提供商特定能力

| 能力 | Gemini | OpenAI | Anthropic | OpenRouter |
|------|--------|--------|-----------|------------|
| 文件上传 | 全支持 | 图片限 PNG/JPEG/GIF/WEBP | 同 OpenAI | 不支持 |
| Tool Calling | functionDeclarations | 原生 | 原生 | 原生（OpenAI 兼容） |
| Thinking | 支持 | 不支持 | 不支持 | 视模型 |
| JSON 模式 | isJson 参数 | response_format | 不支持 | response_format |
