# change-integration — WHY

## 领域价值与核心矛盾

在多任务并发执行中，多条 Road 在各自 worktree 中独立成熟；一条 Road 内部可以经过多任 Relay incumbent，最终仍必须把一个经过质量与机器准入的成果合入同一个共享目标分支（如 `master`）。

核心矛盾在于：**如何既保证并发分支进入共享目标时的原子性与因果一致性，又不通过粗暴的全局长事务锁破坏多任务的并行演进**。若全局加锁，Manager 工作、Relay assessment、rebase 与冲突修复都会迫使其他 Road 串行；若缺乏原子发布门禁，并发提交又会相互覆盖。

## 核心不变量

1. **质量与机器准入分权**：Relay `QualityCertificate` 只证明某任在精确 snapshot/authority 上给出独立满分；Git 冲突、rebase、target head 与 ff-only CAS 继续由 Change 机器事实裁决，模型满分不能越过机器准入。
2. **发布生命周期完整性**：任何 rebase、target move、CAS miss、workspace mutation 都会改变证书绑定域，必须显式失效旧 certificate，并以无参数 `ContinueLoop` 在同一 `ManagerJob`/worktree 上开启另一轮普通独立 assessment 后才可再次发布。失效原因只记录在 durable invalidation 事实中，不进入 `ContinueLoop` 参数。
3. **唯一短临界区（Integration Gate）**：全局门禁只覆盖共享 ref 的最终重读与 ff-only CAS，严格禁止在 incumbent 工作、assessment、rebase 或冲突修复期间持有。
4. **干净工作区准入（Clean Gate）**：编排器受理请求前工作区必须处于 clean 状态，严禁隐式 stash 或猜测用户意图。
5. **Road/worktree 连续，incumbent 可轮换**：冲突或 binding change 保留同一 `ManagerJobId` 与 worktree，但任何 retired iteration 绝不被 Resume；修复责任由同一 `ManagerJob`/worktree 上的下一轮普通独立 loop incumbency 承担。
6. **基于事实的重放恢复**：崩溃恢复仅依赖不可变持久事实、Relay projection 与目标分支当前现实，严禁文件系统猜测和隐藏程序计数器。
7. **工作树修复后必须重新验证**：DevOps 自修或 Engineer 变更导致工作树快照演化后，旧快照上的评估与证书立即失效，必须在新快照上重新执行验证与独立评审，严禁跨快照冒充验证。
8. **并行验证对象协调**：并行 Engineer 与 DevOps 不得无协调修改同一验证对象；必须通过任务边界或固定快照隔离，严禁用前后文件哈希相同冒充运行期未变，禁止引入自动架构分类器。
9. **多道路汇聚后验证**：Orchestrator 区分互补道路集成和竞争道路取舍；多条道路在各自 worktree 独立通过不等于汇聚结果通过，汇聚变基后必须保留证书失效与重新独立验证合同。

## 破坏后果

- **并发提交覆盖**：多个任务并发推送共享分支导致提交丢失或快进历史断裂。
- **并发性能雪崩**：长时间审查与冲突排查持有全局排他锁，导致所有独立道路被动串行化。
- **不可判定与幽灵状态**：恢复时根据磁盘未追踪文件猜测状态，导致未经验证的代码意外合入主干。
- **旧证书偷渡**：将 DevOps 自修前的旧证书搬到新改动上，导致未经独立评估的代码合入主干。
- **无协调重叠写入**：并行 Engineer 与 DevOps 在同一验证窗口内相互覆盖破坏，或用文件哈希欺骗验证机制。
