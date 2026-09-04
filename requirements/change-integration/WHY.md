# change-integration — WHY

## 领域价值与核心矛盾

在多任务并发执行中，多条 Road 在各自 worktree 中独立成熟；一条 Road 内部可以经过多任 Relay incumbent，最终仍必须把一个经过质量与机器准入的成果合入同一个共享目标分支（如 `master`）。

核心矛盾在于：**如何既保证并发分支进入共享目标时的原子性与因果一致性，又不通过粗暴的全局长事务锁破坏多任务的并行演进**。若全局加锁，Manager 工作、Relay assessment、rebase 与冲突修复都会迫使其他 Road 串行；若缺乏原子发布门禁，并发提交又会相互覆盖。

## 核心不变量

1. **质量与机器准入分权**：Relay `QualityCertificate` 只证明某任在精确 snapshot/authority 上给出独立满分；Git 冲突、rebase、target head 与 ff-only CAS 继续由 Change 机器事实裁决，模型满分不能越过机器准入。
2. **发布生命周期完整性**：任何 rebase、target move、CAS miss、workspace mutation 都会改变证书绑定域，必须显式失效旧 certificate，并经过普通 successor 的新独立 assessment 后才可再次发布。
3. **唯一短临界区（Integration Gate）**：全局门禁只覆盖共享 ref 的最终重读与 ff-only CAS，严格禁止在 incumbent 工作、assessment、rebase 或冲突修复期间持有。
4. **干净工作区准入（Clean Gate）**：编排器受理请求前工作区必须处于 clean 状态，严禁隐式 stash 或猜测用户意图。
5. **Road/worktree 连续，incumbent 可轮换**：冲突或 binding change 保留同一 `ManagerJobId` 与 worktree，但旧 incumbent 绝不被 Resume；修复责任通过普通 Relay successor 接棒。
6. **基于事实的重放恢复**：崩溃恢复仅依赖不可变持久事实、Relay projection 与目标分支当前现实，严禁文件系统猜测和隐藏程序计数器。

## 破坏后果

- **并发提交覆盖**：多个任务并发推送共享分支导致提交丢失或快进历史断裂。
- **并发性能雪崩**：长时间审查与冲突排查持有全局排他锁，导致所有独立道路被动串行化。
- **不可判定与幽灵状态**：恢复时根据磁盘未追踪文件猜测状态，导致未经验证的代码意外合入主干。
