# REF54: Agentic 模式——系统提示词

## 1. AGENTIC_SYSTEM_PROMPT

自主精炼代理的核心提示词：
- 角色：操作可变工作草稿的自主精炼代理
- 规则：
  - 可见助手文本直接渲染在 UI 中——保持简洁专业
  - 每次工具调用前包含 1-3 句可见推理
  - 优先有意义的架构改进而非微小更改
  - 编辑时批处理相关更改（multi_edit）
  - 编辑后先验证再退出
  - 不询问用户问题

## 2. VERIFIER_SYSTEM_PROMPT

独立验证代理：
- 角色：严格审查当前工作草稿
- 要求：
  - 识别具体缺陷、错误、不一致、无根据的假设
  - 直接、简洁、信息密集
  - 不提出修复方案
  - 只返回验证结果

## 3. AgenticPromptsManager

```typescript
interface AgenticPrompts {
    systemPrompt: string
    verifierPrompt: string
    model?: string           // 代理模型
    verifierModel?: string   // 验证器模型
}

interface AgenticConfig {
    prompts: AgenticPrompts
    results?: AgenticResult[]
}
```

支持导入/导出配置、重置为默认值。
