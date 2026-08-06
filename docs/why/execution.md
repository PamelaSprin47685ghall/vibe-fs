# 执行 — 理由

Handle 四态与 tombstone 不可回退，防止「弃置会话复活」造成双写 completion。

PTY completion 只信 backend onExit，避免 stdout 启发式假完成——假完成会污染 join 与父状态机。

Join 有界批次 + 稳定排序，把并发完成收敛成确定性 wire。ABORTED 不是 agent 终态：取消是控制面，不是业务结果；把 abort 洗成终态会让恢复与 fallback 走错分支。

## 备选与被拒

**完成后光：Handle 四态 + tombstone vs 可回退复活。** 拒回退：弃置会话复活会双写 completion（EXEC-009 retired tombstone 不可回退）。

**PTY 完成：仅 backend onExit vs stdout 启发式。** 拒启发式：stdout 会假完成，污染 join 与父状态机；onExit 是物理完成信号（EXEC-015）。agent 路径禁止 aborted（EXEC-020），假 abort blob 由 EXEC-021/022 驳回——历史 aborted blob 永不 `RunCompletion`。

**Join 输出：有界批次 + 稳定排序 vs 无序并发。** 拒无序：并发完成收敛成确定性 wire（EXEC-018 MaxJoinBatch=32），否则父状态机不可判定。

**取消：ABORTED 当终态 vs 仅控制面。** 拒终态化：取消不是业务结果；洗成终态让恢复/fallback 走错分支（EXEC-020）。
