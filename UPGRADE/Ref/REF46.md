# REF46: 错误处理与恢复模式

## 1. 错误类型分类

| 类型 | 处理方式 | 示例 |
|------|----------|------|
| AbortError | 静默中止，标记 stopped | 用户取消 |
| API 调用失败 | 重试（最多 4 次指数退避） | 网络错误、速率限制 |
| 空响应 | 重试 | 模型返回空 |
| JSON 解析失败 | 清理文本后重试 | 格式不标准 |
| 超时（15min） | 跳过大中断 | 单代理超时 |
| 控制关键失败 | 停止整个管道 | 策略生成、PQF、策略更新 |

## 2. 重试指数退避

```typescript
const delay = INITIAL_RETRY_DELAY_MS * Math.pow(BACKOFF_FACTOR, attempt - 1)
// 尝试序列: 0ms → 20000ms → 40000ms → 80000ms
```

总超时预算（15分钟）包括所有重试的等待时间。

## 3. Deepthink 的 callAgent 重试

```mermaid
attempt 1: normal call
  ↓ 失败
target.status = 'retrying', render()
wait(20s)
attempt 2:
  ↓ 失败
wait(40s)
attempt 3:
  ↓ 失败
wait(80s)
attempt 4:
  ↓ 失败
→ 如果是控制关键: throw Error (停止管道)
→ 否则: throw Error (标记为失败，继续)
```

## 4. PipelineStopRequestedError 的处理

在 `startDeepthinkAnalysisProcess` 的最外层 catch：
```typescript
catch (error) {
    if (error instanceof PipelineStopRequestedError) {
        process.status = 'stopped'  // 干净的中止
    } else {
        process.status = 'error'
        process.error = error.message
    }
}
```

## 5. 非关键失败的容忍

在 `Promise.allSettled` 中运行非关键并行任务：
```typescript
await Promise.allSettled(strategies.map(async strategy => {
    try {
        // 执行任务
    } catch (error) {
        strategy.status = 'error'
        strategy.error = error.message
    }
}))
```

这种方式确保一条分支的失败不会阻止其他分支继续。
