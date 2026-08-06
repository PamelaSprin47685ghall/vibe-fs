# Prompt — 证明

行为：`what/prompt.md`。所有权：`shape/prompt.md`。程序：`how/prompt.md`。

## 写入口

| 证明 | 期望 | 条款 |
|------|------|------|
| 无第二 `prompt_async` | 生产路径全部经 Dispatcher | PROMPT-005 |
| 四阶段事实 | Claimed/Submitted/PhysicalAccepted/Abandoned 完备 | PROMPT-005 |
| `accepted-*` 非 Authority | 不得升级为 PhysicalAccepted | PROMPT-005 |

## Profile 与发送

| 证明 | 条款 |
|------|------|
| AttemptExecutionProfile 原子：禁止拼装 | PROMPT-008 |
| 发送 `Agent=EffectiveAgent`，`Model=None` | PROMPT-006 |
| Fire-and-forget 仍完整 claim | PROMPT-007 |

代表：`tests/unit/prompt/*`、`tests/unit/context/attempt-plan.test.mjs`。

## 恢复

| 证明 | 期望 | 条款 |
|------|------|------|
| PromptKey 匹配 tail window | 找到则补 PhysicalAccepted | PROMPT-011 |
| 未找到 | Pending，**不**自动重发 | PROMPT-011 |
| 预算耗尽 | Abandoned(UnresolvedAfterRecovery) | PROMPT-011 |

## 禁区

Continuation 不得改 SelectedAgent / 新 Run / 重置 Fallback（PROMPT-003、PROMPT-010）。  
UnknownOrigin fail-closed（PROMPT-004）。
