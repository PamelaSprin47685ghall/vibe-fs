# REF64: 假设路由选择策略

## 1. 三种注入模式的选择逻辑

### 选择依据
```
if (EDFS 模式) →
    强制 selective_injection
else if (hypothesisInjectionMode === 'parallel') →
    Blind Trust: 假设无策略上下文
else if (hypothesisInjectionMode === 'strategy_aware') →
    Strategy-Aware: 假设有策略上下文
else →
    Selective: 假设有策略上下文 + 策略映射
```

## 2. 选择性注入的映射构建

```typescript
// 构建 per-strategy 包
for (const strategy of activeStrategies) {
    const relevant = hypotheses.filter(h => 
        // 匹配目标策略
        h.targetStrategyIds?.includes(strategy.id) ||
        // 空数组 = 全局 (所有策略都接收)
        !h.targetStrategyIds || h.targetStrategyIds.length === 0
    )
    strategyPackets[strategy.id] = formatPacket(relevant)
}
```

## 3. Hypothesis Tester 的隔离

每个假设测试者只收到 Core Challenge + 一个假设文本：
- 看不到目标策略 ID
- 看不到其他假设
- 看不到分支历史
- 输出分类：VALIDATED / REFUTED / INCONCLUSIVE

## 4. 假设刷新时的状态更新

EDFS 心跳刷新时：
1. 旧假设存档到 `hypothesisHistory`
2. 新假设替换 `hypotheses`
3. PQF 替换的策略 → 清空该策略的 selective packet
4. 心跳同时为所有策略生成新映射

## 5. 假设的清理时机

| 事件 | 清理内容 |
|------|----------|
| PQF 替换策略 | 该策略的 selective packet → 占位符 |
| 假设心跳 | 所有假设 → 历史存档 |
| 新分支首次执行 | 旧策略的假设不传递 |
