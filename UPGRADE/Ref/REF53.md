# REF53: Contextual 模式——系统提示词架构

## 1. 四个代理的提示词

### MAIN_GENERATOR_SYSTEM_PROMPT
- 核心自我修正器
- 必须完全开放地接受批判
- 不能假设原答案"基本正确只需打磨"
- 必须愿意彻底改变框架、方法、结论

### ITERATIVE_AGENT_SYSTEM_PROMPT
- 攻击性的方案批判代理
- 只诊断不修复
- 生成 5 个关键问题 + 反例
- 检测局部最小值（所有分支陷入同一条死路）

### STRATEGIC_POOL_AGENT_SYSTEM_PROMPT
- 维护 12-15 个正交策略方向的池
- 每个方案必须有不同最终答案
- 根据批判持续调整置信度
- 检测退出条件：连续 3 次无缺陷 → `<<<Exit>>>`

### MEMORY_AGENT_SYSTEM_PROMPT
- 维护客观、演化的探索历史记录
- 不是逐条日志而是模式提炼
- 记录：已验证的不变量、死路、持续缺陷、有用技术、被驳斥的假设

## 2. 提示词的可定制性

所有提示词可通过 `ContextualPromptsManager` 在 UI 中编辑，编辑器使用 `PromptStylingEditor` 组件。

## 3. 配置接口

```typescript
interface CustomizablePromptsContextual {
    sys_contextual_mainGenerator: string
    sys_contextual_iterativeAgent: string
    sys_contextual_solutionPoolAgent: string
    sys_contextual_memoryAgent: string
    model_mainGenerator?: string | null
    model_iterativeAgent?: string | null
    model_solutionPoolAgent?: string | null
    model_memoryAgent?: string | null
}
```
