# REF27: Deepthink 模式——重试与超时机制

## 1. 通用重试策略 (DeepthinkCore.ts)

```typescript
MAX_API_ATTEMPTS = 4
INITIAL_RETRY_DELAY_MS = 20000
BACKOFF_FACTOR = 2
// 延迟序列: 20秒, 40秒, 80秒
```

## 2. 超时机制

15 分钟超时应用于以下代理（跨所有重试的**总预算**）：
- 假设生成
- 假设测试
- 方案执行
- 方案批判
- 单通道修正
- EDFS 修正
- 结构化方案池生成
- 记忆银行生成
- 最终裁判

**无 15 分钟超时包装的代理**：
- 初始策略生成
- 子策略生成
- 剖析合成
- PQF
- 策略更新

## 3. 关键性与非关键性失败

### 控制关键（停止整个管道）
- 初始策略生成失败
- PQF 失败
- 策略更新失败

### 可容忍失败（记录失败，允许其他分支继续）
- 假设测试失败
- 方案执行失败
- 方案批判失败
- 修正失败
- 方案池生成失败
- 记忆银行失败
- 最终裁判失败

## 4. callAgent 内部流程

```
for attempt = 1 to MAX_API_ATTEMPTS:
  if (isStopRequested) throw PipelineStopRequestedError
  target.status = 'processing'
  render()
  记录 liveEvent (agent_start)
  try:
    调用模型（m可能带 Python 工具）
    记录 liveEvent (agent_complete)
    return result
  catch:
    记录 liveEvent (agent_error/agent_retry)
    if (attempt == MAX_API_ATTEMPTS) break
    target.status = 'retrying'
    wait(delay)
```
