# REF80: 分支保留/替换决策树

## 1. PQF 决策树

```
PQF 开始:
  └→ 检查策略 N 的最近 5 轮历史
     ├→ 策略理解错了问题根本?
     │   └→ UPDATE
     ├→ 完全无意义或离题?
     │   └→ UPDATE
     ├→ 不适配问题描述?
     │   └→ UPDATE
     ├→ 批判反复指出根本性问题?
     │   └→ UPDATE
     ├→ 策略有前景、方向正确?
     │   ├→ 有轻微可修复问题?
     │   │   └→ KEEP
     │   └→ 表现出色?
     │       └→ KEEP
     └→ 其他
         └→ KEEP (默认保留)
```

## 2. Aggressiveness 的影响

| 模式 | KEEP 倾向 | UPDATE 触发条件 |
|------|-----------|----------------|
| Balanced | 默认保留 | 明确证据表明战略失败 |
| Very Aggressive | 更严格 | 持续弱点、域不匹配、低价值 |

## 3. 策略更新生成器的输入

```typescript
buildStrategyUpdatePrompt({
    challenge,
    decisionVector,            // 完整 PQF 决策
    allCurrentStrategies,      // 当前所有策略文本
    previousStrategies,        // 之前所有替换过的策略
    updateRequests,            // 要更新策略的详细信息
        // 包含: oldStrategyText, pqfReasoning, latestSolution,
        //       latestCritique, memoryBank, latestSolutionDisplay
})
```

## 4. 替换后的验证

```typescript
// 替换后立即执行新分支的首次执行+批判
await executeSolutionAttempt(...)
await critiqueSolution(...)
// 方案池将在下次全局迭代中生成
```
