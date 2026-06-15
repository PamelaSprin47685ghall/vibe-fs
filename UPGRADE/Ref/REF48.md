# REF48: 分支替换与策略演化

## 1. PQF 驱动的分支替换

### 触发条件
- 每 5 个全局迭代（EDFS 模式）
- 由 PQF 评估后决定

### 替换流程

```
runPqfAgents() → 评估每个分支 → 返回 PqfDecision[]
updateStrategiesFromPqf() → 生成替换策略
  → 存档旧策略（含全历史）
  → 递增 branchVersion
  → 替换策略文本
  → 清除历史/记忆/假设包
  → 执行新分支的首次执行+批判
```

## 2. DeepthinkStrategyReplacementRecord

```typescript
interface DeepthinkStrategyReplacementRecord {
    strategyId: string
    previousStrategyText: string
    replacementStrategyText: string
    replacedAtGlobalIteration: number
    previousBranchVersion: number
    newBranchVersion: number
    pqfReasoning: string
    memoryBank?: string
    latestSolution?: string
    latestCritique?: string
    branchHistory?: BranchHistoryEntry[]
    poolHistory?: PoolHistoryEntry[]
}
```

## 3. 替换后的系统状态

替换后：
- 稳定策略 ID 保持不变（如 `main1`）
- 分支版本 +1
- 策略文本更新
- 活跃修正历史清空
- 活跃方案池历史清空
- 活跃记忆清空
- 选择性假设包刷新
- 新的首次执行和批判开始

## 4. 存档分支的可视性

替换的分支：
- 仍然在 UI 中可见（分支版本切换）
- 保留在序列化的方案池仓库中
- 不包含在活跃的修正器/池/假设/裁判上下文中
