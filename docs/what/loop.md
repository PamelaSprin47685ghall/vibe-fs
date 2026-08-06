# 循环检测 — 可观察行为

条款前缀：`LOOP-`。传感器边界见 `shape/loop.md`；算法与强杀见 `how/loop.md`。

## LOOP-001：问题与非目标

### 问题

LLM 流式输出偶发退化：单字符循环、短短语循环，或整句 20～30 字符反复。继续让该 run 跑完只会浪费时间、污染 transcript 尾部，并延迟进入有效恢复槽。

### 非目标

```text
不估算 token / 上下文窗口             // CTX-001
不按错误文字分类 provider 失败         // CTX-005
不发明独立的自动恢复预算               // 复用 FALLBACK-005
不把 part.delta 内容拼装成领域事实     // ARCH-002
不修改 OpenCode 本体                   // ARCH-003
不按角色/模型/自然语言 vs 代码动态改阈值  // 统一按代码（LOOP-004）
```

本条款覆盖低多样性循环与短句级周期重复；不声称语义理解式「同一意思换说法」检测。

---

## LOOP-006：LOOP 强杀与 AABB 接入

### 动作序列

```text
is_loop = true
→ 若该 session 当前 attempt 尚未武装 LoopKill：
     1. 记录 LoopKillArmed
     2. AbortSession(sessionId)
→ 若已武装：忽略后续 delta（幂等）
```

禁止在 abort 返回前发送 continuation。 禁止根据 delta 内容裁剪 transcript 或改写已发出的客户端可见文本。

### 与 Host abort 语义的桥接

Host 对插件 abort 通常落成 `MessageAbortedError` / `finish=aborted`。现有路径会把其 reconcile 为 `TurnAborted`，而 `TurnAborted` 不推进 Fallback（用户中止与清理中止不得自动 AABB）。

本条款要求：

```text
reconcile 得到 TurnAborted
且 LoopKillArmed 命中该 session
→ 清除 LoopKillArmed
→ 按 TurnFailed 走 continueAfterProviderFailure 等价路径：
     FallbackController.recordConfirmedFailure（FALLBACK-003 唯一写入口）
     若 mayContinue：
       发送 ProviderRetryAttempt continuation（PROMPT-003/005）
     否则：
       FallbackExhausted 终局（FALLBACK-005）
```

`LoopKillArmed` 是进程内局部事实，不写 Journal，崩溃后自然丢失（安全侧）。

### Continuation 文本

固定 instruction-only Synthetic TOML（ARCH-010），经 PromptDispatcher 发送：

```text
Continue from the interruption without repeating already produced content.
```

ContinuationKind = `ProviderRetryAttempt`。不新增 Origin 种类。

### 预算

```text
不设独立 MAX_RESTARTS
连续 LOOP 强杀与连续 provider failure 共享 ConsecutiveFailureCount
达到 AutoRecoveryBudget → FallbackExhausted
```

---

## LOOP-007：作用域与豁免

必须检测：

```text
插件 Owned 的 managed WorkSession / CompanionSession / BloggerSession
正在进行中的 assistant 文本流（field=text）
```

必须忽略：

```text
非 Owned session
reasoning 字段 delta（避免思考循环误杀正式正文）
compaction pseudo-run（HOST-006）
title / 非 managed 的 Host 内部 run
已 LoopKillArmed 的同一 attempt 的后续 delta
```

受限字母表任务若未来引入，须新增显式豁免条款；在此之前默认开启。

---

## LOOP-008：与既有恢复协议的关系

| 问题 | 归属 |
|------|------|
| cursor 如何推进 | FALLBACK-002…004，唯一写入口 FallbackController |
| 自动恢复是否继续 | FALLBACK-005 预算 |
| 恢复槽是否 armed / primed | FALLBACK-012 + CTX-006 |
| X probe / Y squash | CTX-006…012 |
| abort 后的 terminal 通知 | HOST-004 + TurnCompletionProgram |
| 发送 continuation | PROMPT-003/005/006 |

LOOP 只负责：更早地把退化 attempt 变成一次可恢复的失败。不绕过 FallbackController，不直接改 Offset，不直接发 prefix probe 或 squash。

---
