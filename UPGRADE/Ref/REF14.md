# REF14: Deepthink 模式——核心类型详解

## 1. DeepthinkPipelineState (核心管道状态)

```typescript
interface DeepthinkPipelineState {
    id: string
    challenge: string              // 原始挑战
    challengeText: string          // 处理后的文本
    challengeImageBase64?: string  // 图片
    status: 'idle' | 'processing' | 'retrying' | 'completed' | 
            'error' | 'stopping' | 'stopped' | 'cancelled'
    error?: string
    activeTabId: string
    activeStrategyTab?: number
    isStopRequested?: boolean
    
    // 策略
    initialStrategies: DeepthinkMainStrategyData[]
    
    // 假设
    hypotheses: DeepthinkHypothesisData[]
    hypothesisHistory?: DeepthinkHypothesisData[][]
    hypothesisRounds?: HypothesisRoundSnapshot[]
    knowledgePacket?: string
    
    // 批判和合成
    solutionCritiques: DeepthinkSolutionCritiqueData[]
    dissectedObservationsSynthesis?: string
    
    // PQF
    postQualityFilterAgents: DeepthinkPostQualityFilterData[]
    memoryBankAgents?: DeepthinkMemoryBankAgentData[]
    
    // 解决方案池
    structuredSolutionPool?: string
    structuredSolutionPoolAgents: DeepthinkStructuredSolutionPoolAgentData[]
    
    // 最终裁判
    finalJudgedBestSolution?: string
    finalJudgingStatus?: AgentStatus
    
    // 实时事件
    liveEvents?: DeepthinkLiveEvent[]
}
```

## 2. 主要子类型

- **DeepthinkMainStrategyData**: 主策略，含策略文本、子策略列表、分支版本、替换历史
- **DeepthinkSubStrategyData**: 子策略，含执行/批判/修正状态和 EDFS 迭代
- **DeepthinkHypothesisData**: 假设，含文本、测试状态、目标策略路由
- **DeepthinkSolutionCritiqueData**: 批判，含回应、分支版本、迭代信息
- **DeepthinkPostQualityFilterData**: PQF 决策，含保留/淘汰的策略 ID
- **DeepthinkStructuredSolutionPoolAgentData**: 池代理，含解析后的候选方案
- **DeepthinkMemoryBankAgentData**: 记忆代理，含压缩历史
- **DeepthinkStrategyReplacementRecord**: 策略替换记录
- **DeepthinkLiveEvent**: 实时事件（用于 Live Tab）

## 3. 分支版本控制

- `branchVersion`: 每次 PQF 替换递增
- `branchIterationCount`: 当前分支的局部迭代数
- `globalIteration`: 全局编排周期
- `replacementHistory`: 保存所有历史分支记录
