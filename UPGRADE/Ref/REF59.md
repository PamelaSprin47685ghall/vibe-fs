# REF59: Deepthink 配置面板——Token 估算系统

## 1. 设计目的

在 UI 中实时显示 token 消耗估算，帮助用户了解配置变更的成本影响。

## 2. 核心函数

```typescript
buildEvolvingDfsTokenTrend(config): EvolvingDfsTokenEstimate[]
calculateEvolvingDfsTokenEstimate(config): EvolvingDfsTokenEstimate
```

## 3. 估算因素

| 因素 | 影响 |
|------|------|
| 策略数 | 分支数直接影响调用次数 |
| 假设数 | 每个假设需要测试和注入 |
| 深度 | 迭代轮数倍增所有消耗 |

## 4. UI 展示

饼图四格显示：
- Input（紫色）: 输入 tokens 范围
- Output（绿色）: 输出 tokens 范围
- Total: 总 tokens 范围
- API Calls: API 调用次数范围

## 5. 图表特性

- 支持线性/对数两种缩放
- 鼠标悬停显示深度对应值
- 可选聚焦输入/输出/总计
- 图例显示系列说明

## 6. 使用场景

用户调整 EDFS 深度时，实时看到 token 估算变化，帮助在深度和成本之间做权衡。
