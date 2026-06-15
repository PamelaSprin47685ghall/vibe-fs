# REF76: 各模式的退出条件

## 1. Agentic 模式退出条件

```
只有当:
  currentContent 已验证 (lastVerifiedContent === currentContent)
  并且 agent 认为无需进一步编辑
  才允许调用 Exit 工具
```

如果未验证就调 Exit，系统返回错误。

## 2. Contextual 模式退出条件

```
策略池代理检测到:
  迭代代理 (Solution Critique) 连续 3 次未发现缺陷
  → 输出 <<<Exit>>>
  → 标记 isComplete

主生成器判断:
  多轮迭代后仍然无法改进方案
  → 没有显式退出，由策略池代理控制
```

## 3. Deepthink 模式退出条件

```
正常结束:
  1. EDFS 达到配置深度（max 10）
  2. 所有分支完成修正 + 方案池 + 裁判
  
用户中断:
  1. 用户点击 Stop
  2. 所有 agent 检查 isStopRequested
  3. 抛 PipelineStopRequestedError
  4. status = 'stopped'

控制关键失败（策略生成/PQF/策略更新）:
  1. 重试 4 次全部失败
  2. status = 'error'
```

## 4. 超时终止

```typescript
// 15 分钟超时后:
tools response.timedOut = true
// Python 会话被销毁
// 上层根据 timedOut 决定是否继续
```

## 5. 关键 vs 非关键失败的区分

| 失败类型 | 影响 |
|----------|------|
| 策略生成失败 | 管道停止 |
| 子策略生成失败 | 该策略降级为 direct |
| 执行失败 | 该子策略标记为 error，其他继续 |
| 批判失败 | 该子策略无批判 |
| 修正失败 | 该子策略无修正 |
| 方案池失败 | 该分支无池 |
| PQF 失败 | 管道停止 |
| 策略更新失败 | 管道停止 |
| 裁判失败 | 管道无最终结果 |
