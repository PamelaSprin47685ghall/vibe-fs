# REF36: DCA——动态计算分配模式

## 1. 概念

DCA（Dynamic Compute Allocation）是一个轻量级的多策略探索系统，通过两道工序生成解决方案树：
- 第一道：生成 N 个正交策略方向（Pool Generator）
- 第二道：对每个方向根据优先级分配计算资源（Local Pool Agents）

## 2. 核心类型

```typescript
interface DCASolution {
    id: string
    title: string
    content: string
    priority?: number        // 2-5
    parentId?: string
    type: 'root' | 'orthogonal' | 'evolution'
}

interface DCAPipelineState {
    id: string
    problem: string
    status: 'idle' | 'processing' | 'completed' | 'error' | 'cancelled'
    error?: string
    solutions: DCASolution[]
    isStopRequested?: boolean
}
```

## 3. 流程

```
startDCAProcess(problem):
  1. Pool Generator: 生成 10 个正交解决方案
  2. 每个方案分配 priority (2-5)
  3. 所有 Local Pool Agents 并行执行
     - 每个 agent 收到目标 solution ID + priority + 完整池
     - 生成 priority 个进化子方案
  4. 所有进化子方案添加到全局树
  5. 状态完成
```

## 4. Priority 的含义

- 2-5 之间的整数
- 表示该方向应该被探索的深度
- 较高的 priority 意味着该方向会被 Local Pool Agent 探索得更加深入
- 设置高 priority 不是给最有信心的答案，而是给最有潜力且真正可能成立的方向

## 5. UI

DCA 使用 React 渲染（DCAView.tsx + DCAUI.tsx）：
- 根问题 → 正交方案列表 → 进化子方案列表
- 每个方案可点击查看详情
- 停止按钮可终止处理
