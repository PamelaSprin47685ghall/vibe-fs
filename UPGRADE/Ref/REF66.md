# REF66: 分支槽位管理系统

## 1. 槽位概念

分支槽位（Branch Slot）是 EDFS 中的核心抽象：
- 每个槽位有一个稳定 ID（main1, main2, ... mainN）
- 槽位的内容（策略文本）可以随 PQF 更新
- 槽位的存在是永久的（不会因 PQF 删除）

## 2. 槽位生命周期

```
槽位创建（初始策略生成）:
  策略 ID = main{index}
  分支版本 = 1
  分支文本 = 初始策略文本
  子策略 = [direct]
  替换历史 = []

每轮迭代:
  执行/修正 → 批判 → 方案池
  （每 5 轮）PQF 评估:
    keep: 继续
    update: 替换槽位内容

替换:
  旧内容 → 替换历史存档
  策略 ID → 不变
  分支版本 +1
  策略文本 → 替换文本
  分支历史 → 清空
```

## 3. 替换记录

```typescript
interface DeepthinkStrategyReplacementRecord {
    strategyId: string
    previousStrategyText: string
    replacementStrategyText: string
    replacedAtGlobalIteration: number
    previousBranchVersion: number
    newBranchVersion: number
    pqfReasoning: string
    memoryBank?: string           // 替换前的记忆
    latestSolution?: string       // 替换前的最新方案
    latestCritique?: string       // 替换前的最新批判
    branchHistory?: BranchHistoryEntry[]    // 存档全历史
    poolHistory?: PoolHistoryEntry[]        // 存档池历史
}
```

## 4. 替换后的状态清理

| 状态 | 操作 |
|------|------|
| 分支历史 | 存档到替换记录，清空当前 |
| 方案池历史 | 存档到替换记录，清空当前 |
| 记忆银行 | 存档到替换记录，设置为 undefined |
| Selective 假设包 | → 占位符文本 |
| Python 会话 | 旧版本回话失效（新版本号） |

## 5. 槽位可见性

替换后：
- 旧分支在 UI 中可通过版本切换查看
- 序列化的方案池仓库中包含替换记录
- 活跃代理（修正器/池/PQF/裁判）不看到旧分支
