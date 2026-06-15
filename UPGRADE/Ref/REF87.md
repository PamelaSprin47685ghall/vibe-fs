# REF87: 性能优化策略

## 1. 并行执行

| 模式 | 并行度 |
|------|--------|
| Agentic | 无（串行 LangGraph） |
| Contextual | 四代理串行，但每代理内部模型调用串行 |
| Deepthink 初始 | N_strat 子策略生成 + N_hyp 假设测试 |
| Deepthink EDFS | N_strat 执行/修正/方案池 + N_hyp 假设测试 |
| PQF | ceil(N/2) 并行 |
| 记忆 | N_due 并行 |

## 2. 懒加载

```typescript
// 模式模块动态 import，非初始化时加载
// 减少启动时间
let deepthinkModulePromise: Promise<DeepthinkModule> | null = null
async function loadDeepthinkModule(): Promise<DeepthinkModule> {
    if (!deepthinkModulePromise) {
        deepthinkModulePromise = import('../Deepthink/Deepthink').then(mod => {
            deepthinkModule = mod
            return mod
        })
    }
    return deepthinkModulePromise
}
```

## 3. 上下文优化

- 方案池代理只收到：其他策略的最新池（不是全历史）
- 修正代理只收到：其他策略的最新修正+批判（不是全历史）
- 历史窗口限制：最多 5 条历史
- 池历史窗口限制：最多 5 条
- 记忆银行压缩：5 条以上 → 记忆摘要

## 4. 无阻塞的立即批判

```typescript
// 批判在对应执行/修正完成后立即触发
// 不需要等待所有分支的执行完成才开始批判
```

## 5. React 渲染优化

- `flushSync`：确保 React 更新同步完成
- `requestAnimationFrame` 双缓冲：防止滚动抖动
- Root 重用：避免不必要的 unmount/remount
