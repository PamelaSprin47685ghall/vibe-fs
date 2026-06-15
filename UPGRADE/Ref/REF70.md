# REF70: Final Judge 候选构建协议

## 1. 候选来源

裁判的候选来自所有活跃策略的最终方案：

```typescript
const allSolutions = activeStrategies(process).flatMap(strategy =>
    strategy.subStrategies.map(sub => ({
        id: sub.id,
        solution: sub.refinedSolutionFinal         // 修正最终版
               || sub.solutionAttemptFinal          // 或执行最终版
               || sub.refinedSolution                // 或修正版
               || sub.solutionAttempt,               // 或执行版
        mainStrategyId: strategy.id,
        subStrategyText: sub.subStrategyText,
    }))
).filter(item => item.solution.trim())
```

## 2. 裁判隔离原则

裁判只收到：
- Core Challenge（原始需求）
- 原始图片（如果有）
- 候选方案列表（每个含 ID、策略名、子策略描述、方案文本）

裁判不收到：
- 各代理的中间过程
- 批判内容
- 置信度评分
- PQF 决策
- 分支历史

## 3. 裁判输出解析

```typescript
parseJson(response, 'Final Judge')
// 预期格式:
{
    best_solution_id: string,
    final_reasoning: string
}

// 合法性检查:
if (!parsed.best_solution_id || !parsed.final_reasoning) {
    throw new Error('Missing required fields')
}

// 查找对应方案:
const winningSolution = allSolutions.find(s => s.id === parsed.best_solution_id)
```

## 4. 错误处理

- 无候选方案：`finalJudgingStatus = 'error'`，设置错误信息
- 解析失败：不重试（非控制关键）
- 超时：重新尝试或用已有结果

## 5. 最终结果结构

```
Solution ID: 方案号
Origin: 策略名 + 子策略文本
Final Reasoning: 裁判比较结论
--- 
Definitive Solution: 选中的方案全文
```
