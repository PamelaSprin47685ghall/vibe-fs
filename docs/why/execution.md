# 执行 — 理由

Handle 四态与 tombstone 不可回退，防止「弃置会话复活」造成双写 completion。

PTY completion 只信 backend onExit，避免 stdout 启发式假完成——假完成会污染 join 与父状态机。

Join 有界批次 + 稳定排序，把并发完成收敛成确定性 wire。ABORTED 不是 agent 终态：取消是控制面，不是业务结果；把 abort 洗成终态会让恢复与 fallback 走错分支。
