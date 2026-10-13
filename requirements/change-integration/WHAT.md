# change-integration — WHAT

## [001] 发布生命周期

独立 worktree 的候选进入共享目标 ref 前，须经 Change 确定性准入、短门禁内 ff-only CAS 与发布事实持久化。需要 rebase 时，先记下冲突，再 rebase；沿同一 Work 进入下一 tenure，重新 review 后才可发布。模型满分不能跳过 Git/CAS。

## [002] Clean Gate

Orchestrator 受理用户请求前，目标工作区须无暂存、未暂存、未追踪及脏子模块变更。不得自动 stash、提交或猜测清理；插件运行时与日志放在工作树外。

## [003] 可恢复的原子边界

候选、rebase、publish claim 与 CAS 各边界须有明确的不可变持久事实，使任一中断点的未决义务可确定，不留下无法判定的中间态。

## [004] Integration Gate

门禁只覆盖 target 最终重读与共享 ref 的 ff-only CAS。Incumbent 工作、assessment、证书失效、rebase、冲突处理、loop continuation 等待与 audit 都在门禁外。

## [005] 冲突归属

Unmerged entries 或 rebase conflict 须记录 ConflictDetected。在门禁外交给同一 Work 的下一 tenure 重新 review；不得复活已退休 tenure，也不得另起评审者。失效原因只记录在 durable invalidation 中。

## [006] 从事实恢复

恢复只依据持久事实与当前目标分支现实重证未决义务，不以磁盘残留、隐藏程序计数器、唯一 latest case 或 ResumeAtXxx 补偿日志猜测进度。保留既有 Road/worktree；可继续等待 active incumbent，或在 durable retirement 后 ContinueLoop 到下一 incumbency，不得复活 retired incumbent。

## [007] PublishClaimed 恢复

依次、互斥地判定当前 target head：等于 rebased commit 时幂等补记 Published；等于 expected head 时在短门禁内复核并快进；其余情况视为 claim 过期，失效旧验证后重入 rebase/assessment 循环。

## [008] 冻结目标与 ff-only CAS

委托开始时冻结目标符号引用，读取 head 失败立即拒绝。推进共享 ref 须同时满足当前分支匹配冻结目标、head 匹配 expected head、提交关系为严格快进。

## [009] Road 连续与任次轮换

同一 Road 保留 ManagerJobId、worktree identity 与物理 Manager session，不因追加 charge、冲突或 rebase 另建 worktree。Retirement 后须轮换 IncumbencyId；追加 charge 以 durable AuthorityRevision 更新 active WorkOwned incumbent，物理 session 相同不代表逻辑 Manager 相同。

## [011] Provider horizon

模型只接收自然语言结果与结构化工作记录，不暴露门禁状态、分支指针、内部 job_id、CAS 机制或 worktree 路径。

## [013] 目标变化使证书失效

Gate 前重读或 CAS 发现 target 已推进时，旧发布资格不再成立。释放门禁后，基于最新 head rebase，请求下一 tenure 重新 review。旧资格不得跨 target/base 复用。

## [014] 机器事实不可被满分覆盖

存在 unmerged entries、target/base 已变化或 ff-only CAS 条件不成立时，不得修改共享 ref，须拒绝后继续循环。`8×10` 只能产生质量候选，不能覆盖机器事实。

## [015] 工作区变化后重新验证

DevOps 修复或 Engineer 变更改变工作区后，旧测试结果不再支持发布资格；须在当前工作区重新验证并重新 review，不得搬用旧结果。该失效只作用于发布资格，不阻断 Manager 的评审与收口。

## [017] 汇聚后验证

Orchestrator 须区分互补道路的集成与竞争道路的取舍。各道路独立通过不代表汇聚通过；合流/rebase 后须在最终工作树验证，并保留冲突失效与重新独立 assessment 的要求，不能直接发布。
