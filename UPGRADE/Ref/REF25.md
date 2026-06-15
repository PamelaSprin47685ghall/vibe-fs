# REF25: Deepthink 模式——Iterative History 仓库构建

## 1. 文件定位 (DeepthinkIterativeHistory.ts)

该文件包含确定性的 EDFS prompt 和仓库构建工具。它故意避免使用外部对话历史管理器。

## 2. 核心类型

```typescript
interface StrategySnapshot {
    id: string
    strategyText: string
    slotIndex: number
    branchVersion: number
    branchIterationCount: number
    globalIteration: number
    latestSolution?: string
    latestCorrection?: string
    latestCritique?: string
    latestPool?: string
    memoryBank?: string
    hypothesisPacket?: string
    replacedAtGlobalIteration?: number
    replacementReason?: string
}

interface BranchHistoryEntry {
    globalIteration: number
    branchIteration: number
    branchVersion?: number
    label: string
    solution: string
    critique: string
}

interface PqfDecision {
    strategyId: string
    decision: 'keep' | 'update'
    reasoning: string
}
```

## 3. 关键函数

### 仓库构建函数

| 函数 | 用途 |
|------|------|
| `buildCritiquePrompt()` | 构建批判提示（含最近历史窗口） |
| `buildCorrectionRepository()` | 构建修正代理的上下文仓库 |
| `buildCorrectionPrompt()` | 构建修正提示 |
| `buildSolutionPoolRepository()` | 构建方案池代理的上下文仓库 |
| `buildSolutionPoolPrompt()` | 构建方案池提示 |
| `buildMemoryBankPrompt()` | 构建记忆银行提示 |
| `buildPqfPrompt()` | 构建 PQF 提示 |
| `buildStrategyUpdatePrompt()` | 构建策略更新提示 |
| `buildHypothesisRefreshPrompt()` | 构建假设刷新提示 |

## 4. 仓库分割模式

使用 `REPOSITORY_CURRENT_CONTEXT_MARKER` 分割仓库为两部分：
- `otherContext`: 其他策略的情境感知信息
- `currentContext`: 当前策略的完整上下文（记忆、历史、池、批判）
