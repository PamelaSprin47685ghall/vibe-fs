# 执行 — 所有权与边界

## EXEC-009：Handle 生命周期

Handle 四态：Active / CompletedAwaitingJoin / Abandoned / Retired。  
tombstone 与 abandon **均不可回退**。  
持久化身份 `HandleId`；消费路径唯一，禁止第二处「假装完成」。

## EXEC-014：Executor 私有 Runtime

Executor 映射子会话是私有 runtime，不暴露为公开 fork 目标（配合 AGENT-008）。

## JoinAttempt 中断所有权

`JoinAttemptRegistry` 只持有当前 active attempt 的物理 lease；零 active attempt 时没有可写的 future wake。external-user ingress 只 resolve lease，不拥有 child runtime。Esc 同时拥有两层效果：lease 产生当前 join 的 `operator_abort` wire；父 provider 的 `TurnAborted` cleanup 调用 `AbortChildren`，取消全部仍在运行的 sub-session。session delete、parent teardown 与 runtime dispose 保持同一 child cancellation owner；用户消息路径不得借用它。

## EXEC-023：恢复所有权与线性序

Session/Child 恢复：端口全强制；结果分支穷尽（RecoveredActive / Terminal / Abandoned / RecoveryIncomplete / RecoveryBlocked）。  
线性序：permit → join；禁止跳步恢复。  
Executor 定向等待（AwaitAgentWithPermit）同样受 permit 门：每次定向 await 前重新 requirePermit，校验通过才可读目标 agent 的 Journal 权威 completion；TCS/Pulse 仅作唤醒，不构成第二份 RunCompletion 真理源。

## EXEC-024：Mailbox 双通道

```text
agent 路径：仅 Pulse（结果读 Journal）
PTY 路径：PublishPty
```

禁止把 agent completion 塞进 PTY 通道或反之。

## EXEC-026：SatelliteRuntime 与 StudentRun 所有权

`SatelliteRuntime` 统一拥有 Companion/Teacher 的 child create、Host children reconcile、Session kind
登记、abort、retire 与 owner 级联；不得复制 child Session map。

`StudentRun` 的 durable truth 只包括：PromptAuthority profile、QA 字节流、Student↔Teacher 关联。
不存在一个同时容纳 request kind / 单飞 latch / pending return / pending final 的业务阶段轴的
StudentRun cell —— 那是一个业务程序计数器。

物理 owner（投递地址，不得联合 presence 推导 lifecycle stage）：

- `runs`：活跃 Student run lifetime
- `teacherCalls`：进行中的一次 teacher 调用（`Returned` + `Completion` 两个 CE await 点；兼 EXEC-027 单飞）
- `pendingCompletionTexts`：仅武装 `experimental.text.complete` 改写正文（TextComplete 查询；HandleTurn 分支不得靠其 presence 选 effect）
- `skillMutations`：观测到的 skill 写/改证据

已删除的伪 stage 槽位：`teacherOwners`（改读 durable `SessionAssociationProjection`）、
`teacherCompletions` / `CompletionRun option`（回答进入 `TeacherCall.Returned`，固定 completion 由 turn
payload 比对 resolve `Completion`）、以 `studentFinalCompletions` presence 充当 Compile 完成 PC
（改读 QA.md 存在性）。

Teacher `InvokeTeacher` 是单一 CE 调用栈：`sendTeacherPrompt` → await `Returned` → await `Completion`。
Host 边界退化为 resolve/查询：`return` 落盘后武装 pending text 并 resolve `Returned`；
`TextComplete` 命中 pending 则改写；`HandleTurn` 对 Teacher 只比较 turn payload 是否为固定
`TeacherReturnCompletion`（normalize 后），匹配则 resolve `Completion`，否则有界 nudge
（`AgentPairCursor.DefaultAutoRecoveryBudget`）。无 flight、重复 return、错误 owner、固定正文不匹配
或预算耗尽均 fail closed；成功路径不 abort。

Student 最终 `return` 的 pending message 只存到同一 Host loop 完成；不是知识 Journal，也不能跨任务复用。
完成信号必须核对 StudentCompile attempt 和 QA 已不存在，之后才 retire run。Compile idle nudge
同样受 `DefaultAutoRecoveryBudget` 约束。

## 单一写入口（完成）

PTY completion 写入口 = backend onExit（EXEC-015）。  
Agent completion 经 Journal 事实 + join 消费，不由碎片事件拼。
