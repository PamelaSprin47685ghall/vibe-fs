# change-integration — WHAT

## CHGINT-001: 独立道路进共享 ref 走 publish lifecycle

独立 worktree 中的变更候选进入共享目标 ref 时，必须经历完整发布生命周期：Relay 在精确 snapshot/authority 上产出有效 `QualityCertificate` → Change 做确定性 artifact admission → 如需 rebase 则先失效旧证书、执行 rebase 并请求普通 successor → successor 在新 snapshot 上重新独立 assessment → 获取短门禁执行 CAS → ff-only 发布 → 记录 durable facts。严禁模型满分直接越过 Git/CAS。

## CHGINT-002: Clean Gate——工作区必须干净才受理

编排器处理用户请求前，目标工作区必须处于 clean 状态（无暂存、未暂存、未追踪或脏子模块变更）。严禁自动执行 git stash、自动提交或猜测清理，插件自身的运行时与日志目录必须置于工作树之外。

## CHGINT-003: candidate/rebase/publish claim/CAS 的原子边界

从候选产生、变基、发布认领（publish claim）到 CAS 推进，每一步骤均必须产生明确的不可变持久事实标记。状态迁移之间不存在未定义的中间态，保证恢复时动作具有确定性。

## CHGINT-004: 共享 ref mutation 是唯一短 critical section

Integration Gate 仅在推进共享 ref 物理指针的短暂 CAS 窗口内持有。严禁在 Relay assessment、Manager work、rebase、冲突处理或 successor 等待期间持有该门禁。

## CHGINT-005: conflict 是机器事实，处理者只能是普通 successor

检测到 unmerged entries 或 rebase conflict 时，Change 必须记录 typed `ConflictDetected` 与精确 `WorkspaceSnapshotId`，失效旧 certificate，并在门禁外请求普通 Relay successor。不得 `ResumeManager`、不得恢复 retired predecessor、不得另起 Reviewer；后续质量判断只能来自 successor 的普通 assessment。

## CHGINT-006: restart 后从 durable facts + 外部现实重证 outstanding obligation

系统崩溃重启后，完全根据持久化事实与当前外部目标分支现实重新计算未决义务并恢复执行。SW-003 vs SW-009 消歧：projection 不 fold 成唯一「最新 case」，不新增 ResumeAtXxx 补偿日志，严禁通过扫描文件系统残留或恢复隐藏程序计数器反推执行进度。

## CHGINT-007: PublishClaimed 三分支固定顺序、穷尽互斥

已认领发布状态在恢复判定时，必须按以下固定顺序穷尽判定：
1. 当前 head 已等于变基 commit：说明快进已完成，幂等补写 Published 事实；
2. 当前 head 等于预期 head：说明尚未快进，在短门禁内再次确认后执行快进发布；
3. 其余情况：说明认领已过期，作废旧验证并重新进入变基发布循环。

## CHGINT-008: target ref 安全——冻结 + ff-only CAS

目标分支在委托初始阶段即完成符号引用冻结。读取 head 失败时必须快速失败，发布推进必须同时满足当前分支与冻结目标一致、当前 head 与预期一致、且提交关系为严格快进。

## CHGINT-009: Road/worktree 稳定，authority 与 incumbency 可演化

同一 Road 沿用既有 `ManagerJobId`、worktree identity 与物理 Manager session 容器，不因追加 charge、冲突或 rebase 创建第二个 worktree。逻辑 `IncumbencyId` 可以并且在 retirement 后必须轮换；追加 charge 通过 durable `AuthorityRevision` 更新当前 active WorkOwned incumbent，而不是把“同一物理 session”误当成同一逻辑 Manager。

## CHGINT-010: 长 incumbent/rebase/conflict 工作不占全局门

Relay incumbent 工作、assessment、certificate invalidation、rebase、conflict resolution 与 successor audit 全部在全局门禁之外；只有 target 重读 + ff-only mutation 处于门内。

## CHGINT-011: 墙内机械不进 provider horizon

门禁状态、分支指针、内部 job_id、CAS 算法与 worktree 路径等底层机械对调用模型透明，穿透 horizon 的仅为自然语言结果与结构化工作记录。

## CHGINT-012: 恢复禁止扫盘反推、禁跳步

恢复流程严禁新建 worktree 替换既有 Road 状态、严禁以磁盘残留替代 durable facts、严禁从旧 stage/program-counter 猜下一步。恢复可以继续等待当前 active incumbent，或在 durable retirement 后创建 successor；绝不能为“身份连续”复活 retired incumbent。

## CHGINT-013: target 变化/CAS miss 后旧 certificate 作废

在 gate 前重读或 CAS 本身发现目标分支已推进时，旧 `QualityCertificate` 立即失效；Change 释放门禁、基于最新 head 重新 rebase/capture snapshot，并请求普通 successor。旧证书绝不能在新 target/base 上复用。

## CHGINT-014: stale certificate 与 Git machine facts 永远不能被模型满分覆盖

certificate snapshot 与当前 workspace 不一致、存在 unmerged entries、target/base 已变化或 ff-only CAS 条件不成立时，必须在进入共享 ref mutation 前 fail closed / invalidation + successor。`8×10` 只产生质量候选，不能把这些机器事实翻译成“仍然 perfect，所以继续发布”。
