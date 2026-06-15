# REF79: API 调用序列与编排策略

## 1. Deepthink EDFS 的 API 调用序列

### 首轮（全局迭代 1）
```
策略生成: 1 call (同步)
子策略生成: 0 calls (EDFS 跳过)
假设生成 + 测试: 1 + N_hyp calls (并行)
首次执行: N_strategy calls (并行)
首次批判: N_strategy calls (每个执行完成后立即开始)
方案池: N_strategy calls (并行)
```

### 后续轮（全局迭代 2-N）
```
修正: N_strategy calls (并行)
批判: N_strategy calls (每个修正完成后立即开始)
方案池: N_strategy calls (并行)
假设心跳(偶数轮): 1 + N_hyp calls (并行)
记忆(每 5 轮): N_due calls (并行)
PQF(每 5 轮): ceil(N/2) calls (并行)
策略更新(如需): 1 call
```

### 最后
```
最终裁判: 1 call
```

## 2. Contextual 模式

```
主生成器: 1 call
迭代代理: 1 call
策略池代理: 1 call
记忆代理(每 10 轮): 1 call
```

## 3. Agentic 模式

```
agent → tools → agent → tools → ... → agent → Exit
（最大 48 步 LangGraph 递归）
```

## 4. API 调用数估算（EDFS）

```typescript
// 全局迭代数 = depth
// 策略数 = N_strat
// 假设数 = N_hyp
// 心跳轮次 = floor(depth / 2)

总调用 ≈ 初始策略(1) + 初始假设(1 + N_hyp) 
  + (首次执行 + 批判 + 池) × N_strat
  + sum_{i=2}^{depth} (修正 + 批判 + 池) × N_strat
  + 假设心跳 × (1 + N_hyp)
  + 记忆 × N_due + PQF × ceil(N_strat/2) + 策略更新
  + 裁判(1)
```
