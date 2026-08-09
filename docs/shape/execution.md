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
StudentRun cell —— 那是一个业务程序计数器。物理 owner 相互独立：`TeacherCallScope`（进行中的一次
teacher 调用）、`TeacherCompletionScope`（一次 teacher return 的单赋值 completion）、
`StudentFinalCompletionScope`（一次 Student 最终 return 的单赋值 completion）、`SkillMutationEvidence`。

pending Teacher return 与 pending final message 都是 **call-scoped / provider-run-scoped 的物理单赋值
completion capability**，只能表达「这个 pending 物理调用在下一次固定 Assistant completion 时的正文」，
不得合并为 Student 生命周期 stage。

Teacher `return` 在回答落盘后武装单赋值 pending return，其中同时保存调用 `return` 的 provider run、
回答和随后固定 Assistant completion 的 provider run。只有匹配的 `TurnCompleted` 才 resolve 该
单赋值 capability 并唤醒恰好一个父 `teacher` 调用；成功路径不调用 abort。无 flight、重复 return、
错误 owner、过期 provider run、completion 身份或固定正文不匹配均 fail closed。

Student 最终 `return` 的 pending message 只存到同一 Host loop 完成；不是知识 Journal，也不能跨任务复用。
完成信号必须核对 StudentCompile attempt 和 QA 已不存在，之后才 retire run。

## 单一写入口（完成）

PTY completion 写入口 = backend onExit（EXEC-015）。  
Agent completion 经 Journal 事实 + join 消费，不由碎片事件拼。
