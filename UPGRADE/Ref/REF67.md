# REF67: 假设生命周期管理

## 1. 假设的完整生命周期

```
生成阶段:
  Hypothesis Generator → 输出 hypotheses 数组
  (每假设含 text + 可选 target_strategies)

测试阶段:
  for each hypothesis:
    Tester 接收 { Core Challenge + 单条 hypothesis }
    Tester 输出 { 测试论证 + CLASSIFICATION }
    分类: VALIDATED | REFUTED | INCONCLUSIVE

注入阶段:
  Packet = 组装所有测试结果
  if (selective): 每个策略构建独立子包
  注入到: 执行代理（所有模式）
          修正器 + 方案池（EDFS 模式）

存档阶段:
  if (心跳触发):
    旧 hypotheses → push 到 hypothesisHistory
    生成新的 hypotheses
    旧 packet → push 到 hypothesisRounds
    生成新的 packets
```

## 2. 假设轮次跟踪

```typescript
interface HypothesisRoundSnapshot {
    roundNumber: number
    globalIteration: number
    packet: string           // 完整的 <Full Information Packet>
    strategyPackets: Record<string, string>  // per-strategy 包
}
```

## 3. 假设的 Cleanup 时机

| 时机 | 操作 |
|------|------|
| 假设心跳（每 2 轮） | 当前 hypotheses → history，当前 packet → rounds |
| PQF 策略替换 | 该策略的 selective packet → flushed 占位符 |
| 管道结束 | 保留所有轮次数据供 UI 查看 |

## 4. 假设与策略的依赖关系

假设生成器在 strategy_aware 和 selective 模式下依赖策略文本，但假设测试者独立于策略：
```
            依赖                     不依赖
Generator ──→ 策略文本         Tester ──→ 策略
Generator ──→ 分支历史(心跳)    Tester ──→ 其他假设
Generator ──→ 假设历史(心跳)    Tester ──→ 分支状态
```
