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
| StrengthReplica：same-role profile，schema/execution gate 恰为 Read/Glob/Grep，mayCarryProbe=false，成功不清 owner failure count | PROMPT-008、STRENGTH-004/015 |
| 发送 `Agent=EffectiveAgent`，`Model=None` | PROMPT-006 |
| Fire-and-forget 仍完整 claim | PROMPT-007 |

代表：`tests/unit/prompt/*`、`tests/unit/context/attempt-plan.test.mjs`、`tests/unit/strength/authority-policy.test.mjs`。

## 恢复

| 证明 | 期望 | 条款 |
|------|------|------|
| PromptKey 匹配 tail window | 找到则补 PhysicalAccepted | PROMPT-011 |
| 未找到 | Pending，**不**自动重发 | PROMPT-011 |
| 预算耗尽 | Abandoned(UnresolvedAfterRecovery) | PROMPT-011 |

## 禁区

Continuation 不得改 SelectedAgent / 新 Run / 重置 Fallback（PROMPT-003、PROMPT-010）。  
UnknownOrigin fail-closed（PROMPT-004）。

## Student / Teacher — G3 已删除；证明迁 SyncDelegate

`StudentLearn` / `StudentCompile` / `StudentCompileNudge` / `TeacherIdleNudge` / QA bootstrap /
SKILL 制品提示 **absent**（G3 clean-break；PROMPT-012 / AGENT-020…022 空缺）。不得再作现行证明行。
后继：SyncDelegate 经 Dispatcher 的首发与 idle nudge（PROMPT-005/003；`SyncDelegateIdleNudge`）；
见 `proof/execution.md` SyncDelegate 行与 HOST-008。

| 证明 | 期望 | 条款 |
|------|------|------|
| 任一插件发送失败或 unknown | 无旁路重发；按 PROMPT-011 保持或关闭 | PROMPT-005、PROMPT-011 |
