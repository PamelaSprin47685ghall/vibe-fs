# change-integration — WHAT

## CHGINT-001: 独立道路进共享 ref 走 publish lifecycle

独立 worktree 中的变更候选（candidate）进入共享目标 ref 时，必须经历完整的发布生命周期：产出 candidate → rebase 到目标 head → 变基后 review 确认 → 获取短门禁执行 CAS → 仅快进（ff-only）发布 → 记录持久事实。严禁跳过任何环节向目标 ref 提交未经验证的变更。

## CHGINT-002: Clean Gate——工作区必须干净才受理

编排器处理用户请求前，目标工作区必须处于 clean 状态（无暂存、未暂存、未追踪或脏子模块变更）。严禁自动执行 git stash、自动提交或猜测清理，插件自身的运行时与日志目录必须置于工作树之外。

## CHGINT-003: candidate/rebase/publish claim/CAS 的原子边界

从候选产生、变基、发布认领（publish claim）到 CAS 推进，每一步骤均必须产生明确的不可变持久事实标记。状态迁移之间不存在未定义的中间态，保证恢复时动作具有确定性。

## CHGINT-004: 共享 ref mutation 是唯一短 critical section

Integration Gate 仅在推进共享 ref 物理指针的短暂 CAS 窗口内持有。严禁在代码审查（LLM Review）或冲突修复期间持有该门禁，确保多个任务可并发进行变基与审查。

## CHGINT-005: conflict 在门外 repair/review，再重新 claim

检测到变基冲突时，必须在释放门禁的状态下，在原 worktree 中由原 Manager 解决冲突并重新进行 review 确认；解决完成后重新认领发布，保持上下文连续。

## CHGINT-006: restart 后从 durable facts + 外部现实重证 outstanding obligation

系统崩溃重启后，完全根据持久化事实与当前外部目标分支现实重新计算未决义务并恢复执行。SW-003 vs SW-009 消歧：projection 不 fold 成唯一「最新 case」，不新增 ResumeAtXxx 补偿日志，严禁通过扫描文件系统残留或恢复隐藏程序计数器反推执行进度。

## CHGINT-007: PublishClaimed 三分支固定顺序、穷尽互斥

已认领发布状态在恢复判定时，必须按以下固定顺序穷尽判定：
1. 当前 head 已等于变基 commit：说明快进已完成，幂等补写 Published 事实；
2. 当前 head 等于预期 head：说明尚未快进，在短门禁内再次确认后执行快进发布；
3. 其余情况：说明认领已过期，作废旧验证并重新进入变基发布循环。

## CHGINT-008: target ref 安全——冻结 + ff-only CAS

目标分支在委托初始阶段即完成符号引用冻结。读取 head 失败时必须快速失败，发布推进必须同时满足当前分支与冻结目标一致、当前 head 与预期一致、且提交关系为严格快进。

## CHGINT-009: same-road continuation 与独立 road 的 integration identity

续做既有道路时，沿用既有的 job 标识、worktree 与 Manager 实例，不创建新 worktree 亦不重置执行绑定。独立道路的集成身份由稳定的标识保证，物理路径仅作诊断。

## CHGINT-010: 长 review/repair 不占全局门

变基后的验证审查与冲突修复均在全局门禁之外进行，其他任务在审查期间可自由进行变基、审查与原子发布，避免不必要的串行阻塞。

## CHGINT-011: 墙内机械不进 provider horizon

门禁状态、分支指针、内部 job_id、CAS 算法与 worktree 路径等底层机械对调用模型透明，穿透 horizon 的仅为自然语言结果与结构化工作记录。

## CHGINT-012: 恢复禁止扫盘反推、禁跳步

恢复流程严禁新建 worktree 替换既有状态、严禁更换执行者、严禁跳过变基后的审查确认，且严禁以磁盘状态替代事实证据。

## CHGINT-013: target 变化后旧 post-rebase witness 作废

CAS 重读 head 发现目标分支已被其他任务推进时，旧有的变基后审查证据立即失效，必须重新基于最新 head 执行变基并重新获取双重确认。
