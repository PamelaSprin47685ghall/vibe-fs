# REF26: Deepthink 模式——提示词系统 (DeepthinkPrompts.ts)

## 1. 设计架构

提示词系统包含两部分：
- **系统指令（System Instruction）**: 定义代理的持久角色和行为，可在 UI 中编辑
- **运行时用户提示（Runtime User Prompt）**: 包含精确的挑战、分配工件、分支身份和允许的仓库视图，由核心生成，不可编辑

## 2. 代理特定提示词

每个代理有独立的系统提示词，可在 UI 中自定义：

| 提示词键 | 代理角色 |
|----------|----------|
| `sys_deepthink_initialStrategy` | 初始策略生成 |
| `sys_deepthink_subStrategy` | 子策略生成 |
| `sys_deepthink_solutionAttempt` | 执行/方案尝试 |
| `sys_deepthink_solutionCritique` | 方案批判 |
| `sys_deepthink_dissectedSynthesis` | 剖析观察合成 |
| `sys_deepthink_selfImprovement` | 自我改进/修正 |
| `sys_deepthink_hypothesisGeneration` | 假设生成 |
| `sys_deepthink_hypothesisTester` | 假设测试 |
| `sys_deepthink_postQualityFilter` | PQF |
| `sys_deepthink_memoryBank` | 记忆银行 |
| `sys_deepthink_finalJudge` | 最终裁判 |
| `sys_deepthink_structuredSolutionPool` | 结构化方案池 |

## 3. 代理特定模型选择

每个代理可覆盖全局模型设置（在 `CustomizablePromptsDeepthink` 中）：

```
model_initialStrategy, model_subStrategy, model_solutionAttempt, ...
```

未设置时使用全局所选模型。

## 4. 共享上下文 (DeepthinkContext)

所有代理共享一个 `DeepthinkContext` 常量字符串，描述：
- Deepthink 系统的运作方式
- 每个代理的职责定位
- 工件类型说明
- 系统哲学

关键指令：不要在最终的向用户工件的输出中暴露系统内部机制。
