# relay-incumbency — WHAT

Work 与 tenure 的主合同在 requirements/work/。本文件保留 surface 仍引用的编号。

## [001] 唯一活跃任期

同一 open Work 至多一个 active tenure。精确事实重放只产生一个逻辑任期。身份或载荷冲突须拒绝。

## [005] 退休不可逆

已退休 tenure 不得因重放、恢复、rebase、冲突或 Host crash 复活。继续工作只能开启新的 incumbencyId。每次退休 cut 的 stale provider-run 身份跨后续 tenure 累积。迟到观测不得触发 ordinary repair 或 continuation。

## [006] 退休结果

每次退休提交退休身份、tenure、精确 provider-run/tool-call cut 与唯一结果 Relayed 或 Delivered。Relayed 保持 Work 开放并派发后继。Delivered 收束。cut 只识别 stale 请求。循环信号只从结果名派生。物理 SessionId 可复用。逻辑 tenure 与 provider context 必须重开。派发由 Manager owner 掌握，不得内联在 composition root。

## [008] 后继独立 review

后继一律独立 review。上一任已经记下的评审结果不阻断后继开任。

## [009] 权威推进

活跃 tenure 接纳追加需求时，须在同一 durable update 中绑定 expected、精确 tenure 与 next，原子追加 AdvanceCommitted。精确重放幂等。旧 expected、错误 tenure、冲突重放或退休目标均拒绝。普通 continuation 不构成推进。

## [010] 固定 DevOps 控制权

每条 open Work 只有一个逻辑 DevOps。交接时新任取得调用权，退休的旧任立即失去新派工与调用权。

## [011] 跨 tenure 执行连续性

固定 DevOps 的执行事实、终端状态、环境和未决后台进程跨 tenure 保留。assignment 与进程始终有明确 owner。完成记录不得丢失或误归新任务。故障可替换物理会话，但同一逻辑 DevOps 不得同时拥有两个可执行权威。

## [012] DevOps 绑定幂等

Work 首次绑定一个逻辑 DevOps 后持久化，之后只可 resume，不可 fork 第二个。初始化、晚到结果、接收未知和崩溃恢复均保持唯一性与幂等。不支持并发 Manager 共享 DevOps。
