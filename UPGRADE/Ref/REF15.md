# REF15: Deepthink 模式——EDFS 核心编排逻辑

## 1. 初始阶段

```
startDeepthinkAnalysisProcess(challengeText, imageBase64?, mimeType?)
  → 创建 Pipeline
  → generateStrategies() → 生成主策略（1-5 个，EDFS 模式最多 5 个）
  → generateSubStrategies() → EDFS 模式跳过直接分支
  → 如果 EDFS:
      → runEvolvingDepthFirstSearch()
    否则:
      → runHypothesisRound() → runNonIterativeRefinement()
  → 非停止状态 → finalJudge()
  → 设置 status = completed / stopped / error
```

## 2. EDFS 主循环

```
runEvolvingDepthFirstSearch():
  1. 初始化 runtimes (每个策略一个 BranchRuntime)
  2. runHypothesisRound(round=1) → 假设侦察
  3. runInitialExecutionsAndCritiques() → 首次执行+批判
  4. runSolutionPools(globalIter=1) → 首次解决方案池
  
  5. for globalIteration = 2 to depth:
     a. runCorrectionIteration() → 修正+批判
     b. runSolutionPools() → 池扩张 (与 c 并行)
     c. runHypothesisHeartbeatIfDue() → 假设心跳 (与 b 并行)
     d. runPostFiveIterationMaintenance() → 每5轮维护
```

## 3. BranchRuntime

```typescript
interface BranchRuntime {
    strategyId: string
    branchVersion: number
    branchIterationCount: number
    globalIteration: number
    history: BranchHistoryEntry[]       // 修正-批判历史
    poolHistory: PoolHistoryEntry[]     // 池历史
    memoryBank?: string                  // 递归压缩的记忆
    lastMemoryHistoryCount: number
    lastHypothesisFlushGlobalIteration?: number
}
```

## 4. 关键调度参数

```typescript
MAX_API_ATTEMPTS = 4               // 最大重试次数
INITIAL_RETRY_DELAY_MS = 20000     // 初始重试延迟
BACKOFF_FACTOR = 2                 // 退避因子
AGENT_TIMEOUT_MS = 15 * 60 * 1000 // 15分钟超时
POOL_HISTORY_WINDOW = 5            // 池历史窗口
CORRECTION_HISTORY_WINDOW = 5      // 修正历史窗口
MEMORY_INTERVAL = 5                // 记忆间隔（每5轮）
PQF_GROUP_SIZE = 2                 // PQF 每组评估策略数
HYPOTHESIS_HEARTBEAT_INTERVAL = 2  // 假设心跳间隔（每2轮）
```
