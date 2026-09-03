# durable-convergence — WHY

## 1. 领域动机与核心矛盾

在分布式协作或多进程并发场景下，多个副本（Replicas）可能各自合法地追加事件（例如两个进程基于同一父事件各自产生了互斥的业务操作）。如果同步机制依靠物理时钟（Wall-Clock）、版本号或 Last-Write-Wins (LWW) 简单粗暴地挑选“赢家”，会导致灾难性的后果：
1. **静默丢失合法事实**：稍晚到达或时钟略慢的合法分支被直接覆盖或删除，用户已依赖的事实凭空消失。
2. **多副本世界分叉**：相同的事件集合在不同副本上因到达顺序不同而折叠出互相矛盾的业务现实。
3. **将并发冲突误杀为存储损坏**：将合法的领域并发分支误判为底层的存储格式损坏，导致数据库整体不可恢复。

`durable-convergence` 确立了确定性事实收敛模型：
- **活跃 writer 内永不丢事实**：固定 retention 窗口内的 writer 合并等价于事件集合的有穷并集（Set Union）与标识去重；
- **并发分支显式表达**：合法并发分叉在投影层明确表达为 `DomainConflict`，通过显式的裁决事件（Resolution Event）完成收敛；
- **确定性 k-way merge**：无论输入流枚举顺序或在何处执行，相同输入必定产生全局一致的规范序列。
- **有界历史**：进程 writer 超过固定 24 小时无输出后整条退出 canonical writer set，使物理历史规模由“项目总寿命”变为“最近活动窗口”。

## 2. 核心不变量与破坏后果

- **Retained Merge = Set Union**：同一 retention 截止时刻仍活跃的 writer 内，两个不同 EventId 必须全部进入合并后的历史；只能整体淘汰过期 writer，不能按事件时间挑赢家。
- **Resolution 必须覆盖全部 Heads**：解决冲突的裁决事件必须显式将所有竞争分支的 Head 作为其父事件；若破坏，重放历史无法证明冲突已真正被解决。
- **Dumb Remote 原则**：远端仓库仅作为哑对象存储，不包含任何领域逻辑；所有收敛与验证完全在客户端完成。
- **Activity 不等于 fetch 时间**：远端 snapshot 必须携带 writer blob OID 绑定的 activity manifest；否则一次下载就会错误延长 writer 寿命，导致历史无法按窗口收缩。
- **Payload identity 已经是内容证明**：payload 文件名/缓存 OID 与远端 payload tree OID 同属 content-addressed identity；本地 stat identity 未变且缓存 OID 等于远端 OID 时，再读取同一 remote payload blob 不增加任何事实，只会把同步成本放大到历史 payload 总量，并让持有 store gate 的 Git Hook 长时间阻塞在线 append。
- **收敛比较必须覆盖业务 Current**：相同 retained history 只得到相同事件顺序仍不充分；Structural、Journal、Strength、Casebook 与 JsTransaction 的 production Current 观察也必须逐项相同，否则重复 reducer 或遗漏注册仍可隐藏在绿色结构测试后。

## DEPENDS ON

- `durable-events`
