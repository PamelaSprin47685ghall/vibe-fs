# REF90: 多提供商故障转移策略

## 1. 当前设计

系统目前使用"先配置优先"的模型→提供商映射，没有自动故障转移。

```typescript
// 单个管道内的所有请求使用同一模型
// 模型由用户选择

function resolveProviderForModel(modelName: string): ResolvedProvider {
    const providerConfig = providerManager.getProviderConfigForModel(modelName)
    if (!providerConfig) throw new Error(`No configured provider for model: ${modelName}`)
    // ...
}
```

## 2. 提供商配置状态

```typescript
interface ProviderConfig {
    name: string          // 'google' | 'openai' | 'anthropic' | 'openrouter'
    apiKey?: string
    isConfigured: boolean  // 是否已配置（有有效 apiKey）
    // ...
}

function hasValidApiKey(): boolean {
    // 检查是否至少有一个提供商已配置并可用
}
```

## 3. 图片兼容性检查

```typescript
// 在 App.handleGenerate 中预检查:
if (provider === 'openrouter') {
    alert("OpenRouter models do not support file uploads.")
    return
}
if (provider === 'openai' || provider === 'anthropic') {
    // 检查图片类型是否在支持列表中
    const supportedImageTypes = ['image/png', 'image/jpeg', 'image/gif', 'image/webp']
    // ...
}
```

## 4. 代理特定模型覆盖

不同提供商的能力不同，代理特定模型选择允许：
- 策略生成用更强大的模型
- 假设测试用更快的模型
- 批判用不同温度的模型

## 5. 扩展性设计

添加新提供商的步骤：
1. 在 Routing 中添加 provider config 类型
2. 在 LangGraphToolRuntime 中添加 model 创建逻辑
3. 在 App.handleGenerate 中添加文件兼容性检查
4. 注册到 ProviderManager

当前支持的提供商适配模式：
- OpenAI 兼容（OpenAI, OpenRouter, Local）: 共享 `createOpenAICompatibleAgentModel`
- Anthropic: `ChatAnthropic` 原生
- Gemini: 通过 `callAI` + functionDeclarations
