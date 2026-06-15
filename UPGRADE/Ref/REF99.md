# REF99: Deepthink 非 EDFS 模式——单通道精炼

## 1. 执行路径

非 EDFS 模式（单通道精炼）的执行路径：

```
generateStrategies → 主策略
  ↓
generateSubStrategies → 子策略（除非跳过）
  ↓
runHypothesisRound → 假设生成 + 测试 → 信息包
  ↓
runInitialExecutionsAndCritiques:
  → 所有策略×子策略执行（并行）
  → 所有执行后批判（立即触发）
  ↓
(如果精炼启用)
  → 可选 Dissected Synthesis（全局诊断合成）
  → 可选 Full Solution Context（全部候选到修正器）
  → 所有子策略自我改进/修正（并行）
  ↓
finalJudge → 最佳方案选择
```

## 2. 配置选项

| 选项 | 影响 |
|------|------|
| Refinement = off | 无批判、无修正，执行结果直接作为候选 |
| Dissected Observations | 全局诊断合成加入每个修正器的上下文 |
| Full Solution Context | 所有原始方案和批判加入每个修正器的上下文 |
| Sub-strategies | 主策略展开为多个子策略 |

## 3. 执行计数

```typescript
// 总执行数 = 主策略数 × 子策略数
// 对于 3 策略 × 3 子策略:
// 执行: 9 调
// 批判: 9 调
// 修正: 9 调
// 总计: ~27 调
```

## 4. Dissected Synthesis

```typescript
if (deps.getDissectedObservationsEnabled()) {
    // 收集所有方案+批判
    // 调用一个专门的合成代理
    // 输出全局诊断文档
    dissectedSynthesis = synthesisResponse.contextText
    // 附加到每个修正器的上下文
}
```

## 5. Full Solution Context

```typescript
if (deps.getProvideAllSolutionsToCorrectors()) {
    // 收集所有原始候选（主+子策略）
    // 每个候选含: 策略名、子策略、方案、批判
    // 标记当前修正器的身份
    // 注入到修正 prompt
}
```
