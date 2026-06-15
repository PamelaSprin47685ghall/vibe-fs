# REF77: 知识包格式详解

## 1. Full Information Packet 格式

```xml
<Full Information Packet>
<Hypothesis 1>
Hypothesis: 假设文本
Target Strategies: All 或 main1, main2
Hypothesis Testing: 测试输出全文
</Hypothesis 1>
<Hypothesis 2>
...
</Hypothesis 2>
</Full Information Packet>
```

## 2. 策略特定知识包格式

选择性模式下为每个策略构建独立包：

```xml
<Strategy-Specific Information Packet for Strategy main1>
<Hypothesis hyp1-1>
Hypothesis: 假设文本
Hypothesis Testing: 测试输出
</Hypothesis hyp1-1>
<Hypothesis hyp3-2>
...
</Hypothesis hyp3-2>
</Strategy-Specific Information Packet for Strategy main1>
```

## 3. 知识包注入位置

```typescript
// Contextual 模式:
combinedCritique = suggestions + '---' + '## Strategic Pool' + strategicPool

// Deepthink 非 EDFS:
buildSolutionAttemptPrompt({
    ...
    knowledgePacket: process.knowledgePacket,
    ...
})

// Deepthink EDFS:
buildSolutionPoolPrompt({
    ...
    hypothesisPacket: strategySpecificPackets[strategy.id],
    ...
})
```

## 4. 假设测试结果的三种分类

| 分类 | 含义 | 下游使用 |
|------|------|----------|
| VALIDATED | 假设被证实 | 作为有效证据使用 |
| REFUTED | 假设被证伪 | 避免相关方向 |
| INCONCLUSIVE | 无法确定 | 不要当作证据 |

## 5. 知识包的作用域

```typescript
// Contextual:
scope = 每 10 轮压缩后保留

// Deepthink 非 EDFS:
scope = 单次管道

// Deepthink EDFS:
scope = 每 2 轮心跳刷新
```
