# REF40: 提示词编辑 UI (PromptsContent)

## 1. AgenticPromptsContent

两面板布局：
- **主代理配置**: systemPrompt 编辑器 + 模型选择器
- **验证代理配置**: verifierPrompt 编辑器 + 模型选择器

## 2. ContextualPromptsContent

四面板布局，每个代理一个：
- 主生成器
- 迭代代理/解决方案批判
- 方案池代理
- 记忆代理

每个面板含 system prompt 编辑器 + 模型选择器。

## 3. DeepthinkPromptsContent

十二面板布局（使用 `PromptPane` + `PromptCard` 组件）：
- 初始策略生成 → 子策略生成 → 方案尝试 → 方案批判
- 剖析合成 → 自我改进 → 假设生成 → 假设测试
- PQF → 记忆银行 → 最终裁判 → 结构化方案池

## 4. PromptPane 和 PromptCard

```typescript
PromptPane: { promptKey, title, children }
// 面板容器

PromptCard: { title, textareaId, agentName, value, onChange, 
              modelValue, onModelChange, availableModels }
// 卡片容器含编辑器和模型选择器
```

## 5. 模型选择器

每个代理可独立选择模型：
- `""`: 使用全局模型
- 特定模型名：该代理使用所选模型

模型列表来自应用路由器的动态配置。
