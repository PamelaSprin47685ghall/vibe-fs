# REF30: 多分支策略管理器 —— BranchRuntime 详解

## 1. 生命周期

```
runtime = createRuntime(strategy)
  → 首次执行 + 批判（初始迭代）
  → 方案池生成
  → for each 修正迭代:
      → 构建修正仓库（含历史）
      → 调用修正代理
      → 调用批判代理
      → 更新 history
      → 方案池生成
      → 可选假设心跳
      → 可选 PQF + 记忆
```

## 2. 状态属性

```typescript
interface BranchRuntime {
    strategyId: string          // 例如 "main1"
    branchVersion: number       // PQF 递增
    branchIterationCount: number
    globalIteration: number
    history: BranchHistoryEntry[]       // [{solution, critique, label}]
    poolHistory: PoolHistoryEntry[]     // [{poolResponse}]
    memoryBank?: string
    lastMemoryHistoryCount: number
    lastHypothesisFlushGlobalIteration?: number
}
```

## 3. 快照构建 (runtimeSnapshot)

每次构建修正或方案池提示前，先生成 `StrategySnapshot`，确保：
- 不同分支看到的其他分支状态是**同步的快照**（不是部分变化的共享状态）
- 修正开始时，所有修正器看到其他分支的上次同步迭代结束状态

## 4. 迭代编号规则

| 编号 | 说明 |
|------|------|
| 策略 ID | 稳定槽位，如 `main1` |
| 分支版本 | PQF 替换时递增 |
| 分支本地迭代 | 当前分支版本产生的条目数 |
| 全局迭代 | 所有活跃槽位的共享编排周期 |

初始执行和第一次批判 = 分支本地迭代 1 + 全局迭代 1
