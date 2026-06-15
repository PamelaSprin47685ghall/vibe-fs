# REF41: Deepthink 代理系统——独立 Agent 调用 (DeepthinkAgents.ts)

## 1. 设计目的

提供可被其他模式重用的独立代理调用函数，不依赖对话历史。

## 2. Agent 列表

| 代理 | 函数 | 说明 |
|------|------|------|
| 策略生成 | `generateStrategiesAgent()` | 生成 N 个高级策略 |
| 假设生成 | `generateHypothesesAgent()` | 生成 N 个测试假设 |
| 假设测试 | `testHypothesesAgent()` | 并行测试多个假设 |
| 策略执行 | `executeStrategiesAgent()` | 并行执行多个策略 |
| 方案批判 | `solutionCritiqueAgent()` | 并行批判多个方案 |
| 方案修正 | `correctedSolutionsAgent()` | 基于批判生成修正方案 |
| 最佳选择 | `selectBestSolutionAgent()` | 评估并选择最佳方案 |

## 3. 共享工具

```typescript
interface AgentExecutionContext {
    callAI: (parts, temperature, model, systemInstruction?, isJson?, topP?) 
    cleanOutputByType: (raw, type?) → string
    parseJsonSafe: (raw, context) → any
    getSelectedTemperature: () → number
    getSelectedModel: () → string
    getSelectedTopY: () → number
}
```

## 4. 提示构建

每个代理类型有独立的提示构建函数：
- `buildStrategyPrompt`: 策略生成
- `buildHypothesisGenerationPrompt`: 假设生成
- `buildHypothesisTestingPrompt`: 假设测试
- `buildExecutionPrompt`: 策略执行
- `buildCritiquePrompt`: 方案批判
- `buildCorrectionPrompt`: 方案修正
- `buildFinalJudgePrompt`: 最终裁判

## 5. 错误处理

使用 `wrapAgent()` 统一包装，确保所有错误被捕获为 `AgentResponse`。

## 6. Information Packet 构建

```typescript
buildInformationPacket(hypothesisIds, results): string
// 构建 XML 格式的信息包
// <Full Information Packet>
//   <Hypothesis 1>...</Hypothesis 1>
// </Full Information Packet>
```
