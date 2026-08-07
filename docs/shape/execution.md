# 执行 — 所有权与边界

## EXEC-009：Handle 生命周期

Handle 四态：Active / CompletedAwaitingJoin / Abandoned / Retired。  
tombstone 与 abandon **均不可回退**。  
持久化身份 `HandleId`；消费路径唯一，禁止第二处「假装完成」。

## EXEC-014：Executor 私有 Runtime

Executor 映射子会话是私有 runtime，不暴露为公开 fork 目标（配合 AGENT-008）。

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
登记、abort、retire 与 owner 级联。`StudentRun` 只拥有本学习任务的 request kind、single-flight latch、
QA writer、pending Teacher return 与 pending final message；不得复制 child Session map。

Teacher `return` 通过单赋值 completion cell 唤醒恰好一个父 `teacher` 调用。父调用在 QA 回答落盘后才
abort Teacher 当前 provider turn并返回结果。无 flight、重复 return、错误 owner 或过期 provider run
均 fail closed。

Student 最终 `return` 的 pending message 只存到同一 Host loop 完成；不是知识 Journal，也不能跨任务复用。
完成信号必须核对 StudentCompile attempt 和 QA 已不存在，之后才 retire run。

## 单一写入口（完成）

PTY completion 写入口 = backend onExit（EXEC-015）。  
Agent completion 经 Journal 事实 + join 消费，不由碎片事件拼。
