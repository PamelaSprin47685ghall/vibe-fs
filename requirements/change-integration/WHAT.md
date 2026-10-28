# change-integration — WHAT

## [001] 发布生命周期

独立 worktree 的候选进入共享目标 ref 前，须经 Change 确定性准入、短门内 ff-only CAS 与发布事实持久化。需要 rebase 时，先记下冲突，再 rebase，再按最新 head 重新验证。发布对象只含操作范围、target、candidate、expected head 与当次真实 Git 检查结果；用户明确要求的审查步骤作为该任务的输出处理，不是所有发布的内建前置条件。语义评分不能跳过 Git/CAS。

## [002] 干净工作区门

Orchestrator 受理用户请求前，目标工作区须无暂存、未暂存、未追踪及脏子模块变更。拒绝时指出阻挡的具体路径（`git status --porcelain` 的非空条目）。不得自动 stash、提交或猜测清理；插件运行时与日志放在工作树外。

## [003] 可恢复的原子边界

候选、rebase、publish claim 与 CAS 各边界须有明确的不可变持久事实（CandidateReady、RebasedCandidateReady、PublishClaimed、Published），使任一中断点的未决义务可确定，不留下无法判定的中间态。

## [004] Integration Gate 的范围

Integration Gate 是跨进程的发布序列化锁（`src/Wanxiangshu/Git/IntegrationGate.fs`，按 repo 与 branch 定位锁文件），只覆盖 target 最终重读与共享 ref 的 ff-only CAS。Incumbent 工作、旧绑定失效、rebase、冲突处理、loop continuation 等待与 audit 都在锁外。

【程序待改】当前 `Change/Program.fs` 的 `publishUnderGate` 把 durable claim 与 Published 事实写入也放在锁内，与「锁只覆盖 ref 重读与 CAS」的口径不一致，需要收窄锁内动作或修订口径。

## [005] 冲突归属

Unmerged entries 或 rebase conflict 须记录 ConflictDetected。在锁外交给同一 Work 的下一 tenure 重新处理与验证；不得复活已退休 tenure，也不得另起评审者。失效原因只记录在 durable invalidation 中。

## [006] 从事实恢复

恢复只依据持久事实与当前目标分支现实重证未决义务，不以磁盘残留、隐藏程序计数器、唯一 latest case 或补偿日志猜测进度。保留既有道路与 worktree（按 worktree identity 定位，路径只作诊断）；可继续等待 active incumbent，或在 durable retirement 后 ContinueLoop 到下一 incumbency，不得复活 retired incumbent。

## [007] PublishClaimed 恢复

依次、互斥地判定当前 target head：等于 rebased commit 时幂等补记 Published；等于 expected head 时在短门内复核并快进；其余情况视为 claim 过期，失效旧验证后重入 rebase/验证循环。

## [008] 冻结目标与 ff-only CAS

委托开始时冻结目标符号引用，读取 head 失败立即拒绝。推进共享 ref 须同时满足当前分支匹配冻结目标、head 匹配 expected head、提交关系为严格快进。

## [009] 道路连续与任次轮换

同一条道路（一个 Work）保留 ManagerJobId、worktree identity 与物理 Manager session，不因追加 charge、冲突或 rebase 另建 worktree。Retirement 后须轮换 IncumbencyId；追加 charge 以 durable AuthorityRevision 更新 active WorkOwned incumbent。物理 session 相同不代表逻辑 Manager 相同。

## [011] Provider horizon

模型只接收自然语言结果与结构化工作记录，不暴露锁状态、分支指针、内部 job_id、CAS 机制或 worktree 路径。

## [013] 目标变化使旧绑定失效

Gate 前重读或 CAS 发现 target 已推进时，旧发布绑定不再成立。释放锁后，基于最新 head rebase 并重新验证。旧绑定不得跨 target/base 复用。

## [014] 机器事实不可被评分覆盖

存在 unmerged entries、target/base 已变化或 ff-only CAS 条件不成立时，不得修改共享 ref，须拒绝后继续循环。语义判断或高分结果都不能覆盖这些机器事实，也不构成发布必须满足的附加条件。

## [015] 工作区变化后重新验证

DevOps 修复或 Engineer 变更改变工作区后，旧测试结果不再支持发布；须在当前工作区重新验证并重跑受影响的检查，不得搬用旧结果，也不得用重新评分代替测试。该失效只作用于发布绑定，不阻断 Manager 的评审与收口。

## [017] 汇聚后验证

Orchestrator 须区分互补道路的集成与竞争道路的取舍。各道路独立通过不代表汇聚通过；合流/rebase 后须在最终工作树验证，并保留冲突失效与重新验证的要求，不能直接发布。
