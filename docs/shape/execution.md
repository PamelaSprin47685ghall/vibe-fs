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

## EXEC-024：Mailbox 双通道

```text
agent 路径：仅 Pulse（结果读 Journal）
PTY 路径：PublishPty
```

禁止把 agent completion 塞进 PTY 通道或反之。

## 单一写入口（完成）

PTY completion 写入口 = backend onExit（EXEC-015）。  
Agent completion 经 Journal 事实 + join 消费，不由碎片事件拼。
