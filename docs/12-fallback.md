# 12 — 模型降级 (Fallback)

## 动机

上游 401/402/403、429、5xx 或断连时，**内环** Fallback 拦截错误、按链切换模型并 `continue` 探测；链耗尽才向父 agent 传播（`Consumed = false`）。与 **外环** nudge/review 正交；Fallback 优先消费错误。

## 公理

1. **零定时器**：禁 `setTimeout`/`setInterval`/`Date.now`（架构测试）
2. **真实 continue 探测**：不靠 sleep  backoff
3. **配置**：`AGENTS.md` YAML `models:` 覆盖宿主默认

```yaml
models:
  default: [ "provider/model", ... ]
  agents:
    sisyphus: [ ... ]
```

## 完美平方启发式

- $k$ = `failureCount`；$N$ = 当前链索引
- $k \in \{1,4,9,\ldots\}$（完全平方）→ 扫描从索引 **0** 重试
- 否则从当前 $N$ 继续
- 新用户消息 → $k=0$

## ErrorClass（摘要）

`MessageAborted` / 已取消 / task complete → Ignore  
401–403 或不可重试 → ImmediateFallback  
429/5xx 且未超 retry → RetrySame  
否则 Exhausted

## Consumed 路由（摘要）

`Idle`+`SessionError` → 常 `Consumed=true`（内环自愈）  
`Exhausted` → `Consumed=false`（外环可见）

## 空输出 Idle

最后 assistant 无 tool、text 为空 → `EmptyOutputError` → Fallback continue，**阻止 nudge**（见 [10](./10-message-transform.md)）。

## 模块

| 层 | 路径 |
| :--- | :--- |
| FSM | `Kernel/FallbackKernel/` |
| 运行时 | `Shell/FallbackRuntimeState`、`FallbackMessageCodec`、`FallbackEventBridge` |
| 宿主 hook | 各 `*/Fallback*` |

## 测试

`FallbackKernelTests`、`FallbackConfigCodecTests`、`FallbackIntegrationTests`、`ArchitectureTestsFallback`。

## 相关

- [04-shell.md](./04-shell.md)
- [10-message-transform.md](./10-message-transform.md)