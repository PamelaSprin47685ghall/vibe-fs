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

## EXEC-026：SatelliteRuntime 与 SyncDelegate 所有权

`SatelliteRuntime` 统一拥有 Companion 的 child create、Host children reconcile、Session kind
登记、abort、retire 与 owner 级联；不得复制 child Session map。`SatelliteKind` **仅** `Companion`。

### Student / Teacher — G3 已删除（absent）

`StudentRun` / `teacherCalls` / `StudentQaStore` / Learn·Compile / `StudentCompile` idle nudge /
`InvokeTeacher` / SKILL mutation evidence：**G3 已删除（absent）**（EXEC-027 / AGENT-020…022 空缺）。
不得再列 `runs` / `teacherCalls` / `skillMutations` 为现行物理 owner，也不得用 registry presence
充当业务阶段 PC。后继双 await 与 idle 见下节 SyncDelegate（及 `SyncDelegateIdleNudge`）。

### SyncDelegate 所有权

通用 `SyncDelegate` 所有权（Dedicated Inspector / Dedicated Coder；原 Teacher CE 代数已迁此）。
`SyncDelegateRuntime` 拥有 dedicated synchronous callee 的 create/reuse、Host children reconcile、abort、
retire 与 OwnerReuseScope 级联；不得复制 child Session map，也不得把 SyncDelegate 伪装成 fork/handle/join。

物理 owner：

- `syncDelegateGate`：immediate caller ReuseScope 级单飞 lease（serialization key = **immediate caller
  ReuseScope**，**不是** family root / repository / worktree）
- `attachedSessions`：`(OwnerReuseScopeId, SyncDelegateRole)` → at most one live dedicated Session
- `delegateCalls`：进行中的一次 sync delegate 调用（`Returned` + `Completion` 两个 CE await 点）
- `pendingCompletionTexts`：仅武装 `experimental.text.complete` 改写正文（TextComplete 查询；HandleTurn
  分支不得靠其 presence 选 effect）

单一 CE 调用栈（业务 caller 不可见）：

```text
Acquire(immediate caller ReuseScope)
→ GetOrCreate(OwnerReuseScopeId, role)
→ Send
→ await Returned
→ await Completion
```

Host 边界退化为 resolve/查询：`return` 武装 pending text 并 resolve `Returned`；`TextComplete` 命中
pending 则改写；`HandleTurn` 只比较 turn payload 是否为固定 `SyncDelegateReturnCompletion`
（normalize 后），匹配则 resolve `Completion`，否则有界 `SyncDelegateIdleNudge`
（`AgentPairCursor.DefaultAutoRecoveryBudget`）。无 flight、重复 return、错误 owner、固定正文不匹配
或预算耗尽均 fail closed；成功路径不 abort。

不变量：

1. **Serialization**：同一 immediate caller ReuseScope 同时最多一个 active sync delegate call。
   嵌套合法且不得死锁：`DevOps → Coder → Inspector`（各层 gate 绑定各自 immediate caller ReuseScope）。
   禁止按 family root 串行（父持 family gate 等子、子再要同一 family gate → deadlock）。
2. **Reuse key**：`(OwnerReuseScopeId, SyncDelegateRole)`；同 scope 兼容续问复用同一 Session，
   `return` 后不 retire / 不 dispose。
3. **Tier**：owner effective tier → deterministic delegate tier（`fast→fast`，`deep→deep`）；
   模型不可每轮选择 target Agent。
4. **Dual await**：callee `return` 只 resolve `Returned`；reconciler 证明同 Host loop 的
   `TurnCompleted` 后才 resolve `Completion`；caller 阻塞到两者都完成（详见 EXEC-028 SyncDelegate 路径）。
5. **Lifetime**：Dedicated Session lifetime = OwnerReuseScope lifetime；graceful ReuseScope close 才
   retire/release（Casebook synthesis 若启用见 Casebook 合同，不属本条所有权）。

Dedicated Inspector/Coder = Work + Attached（可有 Companion），**不是**历史 Teacher-style InternalLeaf /
no-Companion Satellite。Student/Teacher 路径已删除；通用 SyncDelegate 不继承该拓扑。

## 单一写入口（完成）

PTY completion 写入口 = backend onExit（EXEC-015）。  
Agent completion 经 Journal 事实 + join 消费，不由碎片事件拼。
