# REF11: Contextual 模式——核心循环详解 (ContextualCore.ts)

## 1. 状态类型

```typescript
interface ContextualState {
    id: string
    initialUserRequest: string
    initialMainGeneration: string
    currentBestGeneration: string
    currentBestSuggestions: string
    allIterativeSuggestions: string[]
    mainGeneratorHistory: HistoryMessage[]
    iterativeAgentHistory: HistoryMessage[]
    memoryAgentHistory: HistoryMessage[]
    strategicPoolAgentHistory: HistoryMessage[]
    currentMemory: string
    memorySnapshots: MemorySnapshot[]
    currentStrategicPool: string
    allStrategicPools: string[]
    iterationCount: number
    isProcessing: boolean
    isRunning: boolean
    messages: ContextualMessage[]
    contentHistory: ContentHistoryEntry[]
}
```

## 2. 每轮迭代详细流程

### 步骤 1: 主生成器
- 调用 `callContextualAgent('Main Generator', mainGeneratorMessages, sys_prompt)`
- 首轮输出作为 `initialMainGeneration`
- 将输出推入 `mainGeneratorMessages` 和 `iterativeAgentMessages`

### 步骤 2: 迭代代理（批判）
- 接收主生成器的输出，加上人类消息 "Please critique..."
- 生成 5 个关键问题 + 反例
- 输出不包含修复方案，只找缺陷

### 步骤 3: 策略池代理
- 观察主生成 + 批判
- 更新 N 个策略方向
- 检测退出条件（连续 3 次无缺陷 → `<<<Exit>>>`）

### 步骤 4: 记忆代理（每 10 轮）
- 压缩迭代历史
- 生成结构化记忆文档
- 重置各代理的消息历史以控制上下文长度

## 3. 重试机制

`callContextualAgent()` 使用指数退避：
- 最多重试 2 次
- 初始延迟 2000ms，退避因子 1.5
- 支持 AbortController 中止
