# 执行 — 理由

Handle 四态与 tombstone 不可回退，防止「弃置会话复活」造成双写 completion。

PTY completion 只信 backend onExit，避免 stdout 启发式假完成——假完成会污染 join 与父状态机。

Join 有界批次 + 稳定排序，把并发完成收敛成确定性 wire。ABORTED 不是 agent 终态：取消是控制面，不是业务结果；把 abort 洗成终态会让恢复与 fallback 走错分支。

## 备选与被拒

**完成后光：Handle 四态 + tombstone vs 可回退复活。** 拒回退：弃置会话复活会双写 completion（EXEC-009 retired tombstone 不可回退）。

**PTY 完成：仅 backend onExit vs stdout 启发式。** 拒启发式：stdout 会假完成，污染 join 与父状态机；onExit 是物理完成信号（EXEC-015）。agent 路径禁止 aborted（EXEC-020），假 abort blob 由 EXEC-021/022 驳回——历史 aborted blob 永不 `RunCompletion`。

**Join 输出：有界批次 + 稳定排序 vs 无序并发。** 拒无序：并发完成按 EXEC-018 收敛成确定性 wire，否则父流程无法稳定判断。

**取消：ABORTED 当终态 vs 仅控制面。** 拒终态化：取消不是业务结果；洗成终态让恢复/fallback 走错分支（EXEC-020）。

**Join 中断：session future latch vs JoinAttempt。** 拒 latch：零 waiter 时收到的用户消息没有可归属的 wait；保留它会把过去的 ingress 错接给未来 join。attempt 在工具入口先建立，用户消息只唤醒当时活动的 attempt；无 attempt 时只保留正常 Host 消息，不产生 join wake，也不取消 sub-session。Esc 是用户对当前父 attempt 的取消：当前 join 返回 operator_abort，父 TurnAborted cleanup 同时取消全部仍在运行的 sub-session。两种 ingress 不得混同（EXEC-017）。

**Student–Teacher：生命周期 cell vs 独立物理 scope。** 拒 `RunState`、handoff 与 pending slot 合并：它们把调用栈位置藏进可变字段，terminal handler 必须猜下一步。Teacher call、return completion、Student final completion 与 skill mutation 各自只拥有一个物理 lifetime；业务顺序由 prompt facts 与 CE 调用结构表达（EXEC-026/027）。
