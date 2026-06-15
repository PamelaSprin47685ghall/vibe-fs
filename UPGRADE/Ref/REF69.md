# REF69: PQF 决策融合算法

## 1. PQF 组的划分

```typescript
const grouped = grouped(strategies, PQF_GROUP_SIZE)
// 默认 PQF_GROUP_SIZE = 2
// 有 N 个策略 → ceil(N/2) 个 PQF 代理
// 例如 5 个策略 → 3 个 PQF 代理（2+2+1）
```

## 2. PQF 代理输入

```typescript
buildPqfPrompt({
    challenge,
    groupIndex,           // 组序号
    groupCount,           // 总组数
    strategiesInGroup,    // 本组评估的策略（最多 2 个）
    allActiveStrategies,  // 所有活跃策略（仅文本，情境感知用）
    historyByStrategy,    // 本组策略的完整近期历史
    aggressiveness,       // balanced | very_aggressive
}) → prompt
```

## 3. 输出解析

```typescript
// 每个 PQF 代理返回:
{
    analysis_summary: string,
    strategies: [
        { strategy_id: "main1", decision: "keep", reasoning: "..." },
        { strategy_id: "main2", decision: "update", reasoning: "..." }
    ]
}

// 决策融合:
const decisions: PqfDecision[] = []
for (const item of parsed.strategies) {
    const validIds = new Set(group.map(s => s.id))
    if (!validIds.has(item.strategy_id)) continue  // 跳过不在此组的
    
    decisions.push({
        strategyId: item.strategy_id,
        decision: item.decision === 'update' ? 'update' : 'keep',
        reasoning: item.reasoning
    })
}
```

## 4. 决策融合后的处理

```typescript
const updateDecisions = decisions.filter(d => d.decision === 'update')
if (updateDecisions.length === 0) return []  // 无更新

// 所有 UPDATE 决策发到统一的策略更新生成器
const updatedIds = await updateStrategiesFromPqf({
    process,
    parts,
    challengeText,
    runtimes,
    decisions,     // 完整决策向量
    globalIteration,
})
```

## 5. 攻击性（Aggressiveness）的影响

| 模式 | 行为 |
|------|------|
| balanced | 默认。仅当证据表明根本性战略失败时标记 update |
| very_aggressive | 更激进：策略有持续概念弱点就标记 update |
