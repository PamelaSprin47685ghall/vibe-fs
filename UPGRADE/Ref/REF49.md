# REF49: 假设注入模式详解

## 1. 三种模式对比

### Blind Trust (`parallel`)
- 假设生成器无策略上下文
- 完整 packet 注入所有执行代理
- 无每策略过滤
- 注入时机：策略和子策略生成后

### Strategy-Aware (`strategy_aware`)
- 假设生成器看到所有策略和子策略
- 完整 packet 仍注入所有执行代理
- 策略感知影响假设选择，而非投递

### Selective (`selective_injection`)
- 假设生成器看到策略，假设映射到特定策略 ID
- 每个策略收到独立的 per-strategy packet
- 测试者永远看不到目标 ID
- EDFS 模式强制使用

## 2. 选择性注入映射规则

```typescript
// 对于每个策略，构建独立 packet
策略 specificPackets[id] = 
  hypotheses.filter(h => 
    // 匹配目标 ID
    h.targetStrategyIds?.includes(id) ||
    // 空数组 = 全局有用
    !h.targetStrategyIds || h.targetStrategyIds.length === 0
  )
```

## 3. 注入到哪些代理

非 EDFS 模式：
- 执行代理（Execution Agents）

EDFS 模式：
- 初始执行代理（Initial Execution Agents）
- EDFS 修正代理（Corrector Agents）
- 结构化方案池代理（Solution Pool Agents）

## 4. 假设刷新时的映射更新

每个偶数全局迭代的心跳刷新中：
- 策略感知假设生成器看到最新分支状态
- 新假设可以有不同向目标策略映射
- PQF 替换策略时，旧映射被清除
