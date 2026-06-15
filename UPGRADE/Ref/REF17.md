# REF17: Deepthink 模式——假设系统 (Hypothesis System)

## 1. 假设生成

### 初始假设生成 (runHypothesisRound)
- 根据 `hypothesisCount` 决定是否启用
- 三种注入模式：
  - `parallel` (Blind Trust): 假设生成器看不到策略，完整 packet 注入所有执行代理
  - `strategy_aware`: 假设生成器看到所有策略，完整 packet 仍注入所有代理
  - `selective_injection`: 假设生成器看到策略，每个假设映射到特定策略

### 假设格式（Selective 模式）
```json
{
  "hypotheses": [
    {
      "text": "假设文本",
      "target_strategies": ["main1", "main3"]
    }
  ]
}
```

## 2. 假设测试

- 每个假设由独立测试代理并行测试
- 测试代理只收到 Core Challenge + 一个假设文本
- 不收到：其他假设、策略、目标 ID、分支历史
- 测试结果输出为 VALIDATED / REFUTED / INCONCLUSIVE

## 3. Information Packet 构建

所有测试结果组装成完整的 `<Full Information Packet>`：
- 每个假设包含：假设文本、目标策略、测试输出
- 选择性模式下，为每个策略构建独立的 `<Strategy-Specific Information Packet>`

## 4. 假设心跳 (Hypothesis Heartbeat)

EDFS 模式下：
- 每 2 个全局迭代刷新一次
- 接收：所有之前的假设轮次 + 最近 2 个修正-批判对
- 生成新的假设集（替换旧的 selective packets）
- PQF 替换策略时，该槽位的 selective packet 被刷新为显式占位
