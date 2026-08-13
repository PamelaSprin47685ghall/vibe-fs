# 执行 — 理由

Handle 四态与 tombstone 不可回退，防止「弃置会话复活」造成双写 completion。

PTY completion 只信 backend onExit，避免 stdout 启发式假完成——假完成会污染 join 与父状态机。

Join 有界批次 + 稳定排序，把并发完成收敛成确定性 wire。ABORTED 不是 agent 终态：取消是控制面，不是业务结果；把 abort 洗成终态会让恢复与 fallback 走错分支。

Provider 面只投影后果与 WorkRecord，不投影 `job_id`/`worktree`/`agent_id`/`status`/`code` 等机器 DTO。朝向远方用 `horizon`，委托道路用 `commission`/`fork`（不同 contract 不同名），同步取证用 `inspect`——动词命名一个语义 act，不为机器拓扑留第二张脸。

## SyncDelegate：无 return 通道（GrandRewrite）

同步委派结束于 specialist 的 ordinary completion → 物化该次 invocation 的 bounded WorkRecord。不再有 `return(message)` 第二出口，也不再有 `Returned → Completion` 双 await 与 `completion_text` magic。ReuseScope / dedicated session / tier 绑定仍守机器精度；穿过 horizon 的只有 WorkRecord 投影。

## 备选与被拒

**完成后光：Handle 四态 + tombstone vs 可回退复活。** 拒回退：弃置会话复活会双写 completion（EXEC-009 retired tombstone 不可回退）。

**PTY 完成：仅 backend onExit vs stdout 启发式。** 拒启发式：stdout 会假完成，污染 join 与父状态机；onExit 是物理完成信号（EXEC-015）。agent 路径禁止 aborted（EXEC-020），假 abort blob 由 EXEC-021/022 驳回——历史 aborted blob 永不 `RunCompletion`。

**Join 输出：有界批次 + 稳定排序 vs 无序并发。** 拒无序：并发完成按 EXEC-018 收敛成确定性 wire，否则父流程无法稳定判断。稳定排序 ≠ 因果序；ordinal/count 不得进 horizon。

**取消：ABORTED 当终态 vs 仅控制面。** 拒终态化：取消不是业务结果；洗成终态让恢复/fallback 走错分支（EXEC-020）。

**Join 中断：session future latch vs JoinAttempt。** 拒 latch：零 waiter 时收到的用户消息没有可归属的 wait；保留它会把过去的 ingress 错接给未来 join。attempt 在工具入口先建立，用户消息只唤醒当时活动的 attempt；无 attempt 时只保留正常 Host 消息，不产生 join wake，也不取消 sub-session。Esc 是用户对当前父 attempt 的取消：当前 join 返回 operator_abort，父 TurnAborted cleanup 同时取消全部仍在运行的 sub-session。两种 ingress 不得混同（EXEC-017）。

**Student–Teacher：生命周期 cell vs 独立物理 scope。** 拒 `RunState`、handoff 与 pending slot 合并：它们把调用栈位置藏进可变字段，terminal handler 必须猜下一步。Teacher call、return completion、Student final completion 与 skill mutation 各自只拥有一个物理 lifetime；业务顺序由 prompt facts 与 CE 调用结构表达（EXEC-026/027）。**G3 已删 Student/Teacher；本条保留为历史拒因。** GrandRewrite 后同步委派不再依赖 leaf `return` 工具。

**同步收口：`return` 双 await vs ordinary completion → WorkRecord。** 旧路径（superseded by GrandRewrite）：Dedicated Inspector/Coder 走 SyncDelegate，`return` resolve `Returned`，再等 `TurnCompleted`，防下一调用与上一 turn 尾部重叠。新路径拒第二出口：`return` 把「结束协议」伪装成工具能力，污染 self-model，并逼调用方解码双通道。选 ordinary completion 物化 bounded WorkRecord；ReuseScope 与 tier 绑定仍在墙内。OneShot dispose-after 与 dedicated reuse 仍互斥。

**Sync batch：ProviderRun 语义批次 vs microtask/排队竞态。** 拒“先 drain 一次、创建 child 后再 drain、prepare 后再 drain”式时间窗口拼接，也拒把同一 assistant message 的并发 sync calls 当成多轮队列：批次成员应由 Host 已完成的 assistant message 直接给出。选 `(immediate caller ReuseScope, SyncDelegateRole, ProviderRunIdentity)` 作为语义批次；同一 ProviderRun、同一 dedicated role 的全部 sync calls 按 provider tool-call 顺序拼成一次 callee prompt。一个 canonical call 返回 bounded WorkRecord，其余 sibling call 只引用该 canonical result，不复制正文。不同 ProviderRun 若前一 sync call 尚未完成则 fail closed，不向同一 dedicated Session 叠发第二轮。嵌套 `DevOps→Coder→Inspector` 仍合法，因为 ownership key 留在 immediate caller ReuseScope（EXEC-026/031）。

**Delegate tier：owner 确定性绑定 vs 每轮自选 Agent。** 拒模型每轮选 fast/deep：否则 `(OwnerReuseScopeId, role)` 无法对应唯一 dedicated Session，prefix/context 复用崩溃。`fast→fast`、`deep→deep`（EXEC-026）。Binding 属机器；Persona 不随 binding 变。

**朝向工具：list DTO / 后台监视 vs horizon。** 拒 `agent_id/session_id/status/...` 列表：那是调试面冒充世界。也拒 timer、轮询、订阅式后台监视：朝向是调用者需要时主动看的 measurement，不应制造隐藏观察流。选一次性自然语言 snapshot：「谁还在远方」+ 每个可见 subagent 最新一条 durable 工作记录；内部来源是最新 `BlogFrame`。最新记录不可读时不得退回旧记录冒充最新。

**同步取证工具名：inspector vs inspect。** 拒名词工具：People=nouns，Tools=verbs；且与 office 名撞车会诱导「工具=角色」。

**Provider 迁移：alias / 渐进双轨 / 机器 DTO 留 horizon vs clean break。** 拒前三者：过渡期永久化旧解码器心智；机器精度留墙内，经验只见后果。
