# Fallback — 所有权与边界

行为不变量见 `what/fallback.md`。算法与事实形状见 `how/fallback.md`。

## FALLBACK-003：统一 FallbackController

Host 的 idle / retry 只负责唤醒，不携带业务裁决。

唯一允许提交下列事实的写入口是 `FallbackController`：

```text
FallbackCursorAdvanced
FallbackExhausted
```

规范路径：

```text
idle 或 retry 信号
→ single-flight reconcile
→ 从完整 Host snapshot 识别失败的 provider attempt
→ FallbackAttemptIdentity 去重
→ FallbackController 原子推进
→ 仅当 Host 不再自动继续时，才发送 continuation
```

```fsharp
type FallbackAttemptIdentity =
    { SessionId
      LogicalRunId
      AuthorityRootUserMessageId
      ProviderRunIdentity }
```

同一 failed attempt 最多推进一次。  
StrengthReplica attempt outcome 永不进入 FallbackController；不是 owner Logical Run 的 failed attempt（STRENGTH-004/019）。

禁止的第二写入口：

- `ProviderFailureContinuation` 直接写 cursor  
- raw `retry` 事件处理器直接写 cursor  
- Guard / repair / 其它模块旁路推进  

`armedByFailure` 等执行局部标志不属于 SessionAssociation、cursor 字段或 Journal（见 FALLBACK-012）。

## Persona / language / system prompt 所有权边界（FALLBACK-014）

`FallbackController` **只**可改写 `AttemptExecutionProfile.EffectiveAgent`（及对应模型绑定）。

下列**不**属于 Fallback 写入口（AGENT-028/029、HOST-026、ARCH-016 Gate D）：

```text
SessionPersona
SessionProviderLanguage
system prompt 字节（office + Role Law + Common Law）
CanonicalRole / SelectedAgent / PeerAgent / Authority 身份
```

| 概念 | Owner | Fallback 可否改 |
|------|-------|-----------------|
| ExecutionBinding / EffectiveAgent | FallbackCursor 推进 | 是（仅此） |
| SessionPersona | session 创建绑定 | 否 |
| system prompt 身份字节 | Prompt composition | 否 |
| ProviderLanguage | Host session bind | 否 |
| cursor / SideA·B / Offset / ConsecutiveFailureCount | 墙内 Fallback 代数 | 不得暴露给 provider horizon |

换 Peer = 换执行者 ≠ 换人。T1 / review / reanchor / Strength 同守此边界。
