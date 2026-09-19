# knowledge-reuse — WHY

## 领域动力与核心张力

历史沉淀的工程经验与知识（Casebook）能够作为极高价值的语义缓存与效率加速器，显著减少针对代码库重复工程问题的探索与调查开销。然而，**历史工程案例绝不能直接等同于对当前代码库真实状态的证明**。若将历史缓存直接作为当前真理，代码库演进后的过时答案将成为系统判断的非法真源；若完全禁止复用历史记录，系统又将在每一次相似任务上面临重复的全量调查与推演成本。

`knowledge-reuse` 的核心存在理由是确立基于「双基线与真实 Diff 维护」的语义缓存机制：
1. **实质访问与粗粒度关联**：关联文件仅从真实读写删移操作中采集，作为可能相关的粗粒度线索，不求行级最小依赖，不伪造严格证明；
2. **结束基线与 Diff 维护**：以逻辑工程工作结束时的完整文件状态为初始基线（B），后续外部变化通过真实 diff 驱动私有 Bookkeeper 刷新维护（B→C→D），原始结束基线与轨迹永不改写；
3. **单次目标捕获与非 Replay 语义**：采用一次目标捕获与真实 diff 维护，不引入脆弱的观察集合完全匹配与前后重放循环，容忍过时是系统预期的正常产品语义；
4. **统一持久化权威**：Case 事实完全由统一的 EventStore 承载，淘汰通过追加事件表达，并发冲突由领域冲突（DomainConflict）显式建模，杜绝本地时间戳竞争或私有存储分叉。

## 核心不变量

1. **缓存定位与 Diff 维护机制**：Casebook 是尽力而为的语义缓存，`fetch` 调用通过计算维护基线与当前状态的真实 diff 决定直接返回或触发刷新。
2. **实质访问采集**：关联文件严格从工具执行层的成功读（整文件关联）、写、删（允许 Missing）、移操作中捕获，严禁将 grep/glob/目录列表或模型提及计入访问。
3. **逻辑轨迹与一次归档**：以一次逻辑 Engineer 工作（含 Fission 收敛合并）为唯一来源，全生命周期仅触发恰好一次归档，多次 resume 形成多段独立案例。
4. **单程序原子维护（Bookkeeper）**：维护操作由私有的 `js-bookkeeper` 在单个事务内原子完成，Bookkeeper 严禁获得仓库文件读写与调查权限，仅接收旧案与真实 diff。
5. **双基线生命周期**：`completionFileState` 记录结束时初始状态（永不改写）；`maintenanceFileState` 随成功维护单调推进；正文与维护基线同边界原子提交。
6. **低信任公开索引与诚实表述**：面向外部仅暴露包含 Shelfmark 与规范化问题的低信任索引快照；对外仅表述「未检测到变化」或「已根据差异维护」，严禁宣称「已验证正确」。

## 边界与失效模式

- **不负责当前仓库事实确立**：当前观察的采集法则与只读约束归 `repository-investigation`。
- **不负责文件系统读写与事务**：底层文件工具与 JS 编程沙箱归 `repository-programming`。
- **不负责事件持久化底层**：底层事件追加与 CAS 存储归 `durable-events`。

**失效表现（RED）**：
- 将历史 Case 宣称为当前代码库的正确性证明；
- 从 grep、glob 或模型文本中推断文件关联；
- 同一逻辑 Engineer 工作或 Fission 单 lane 触发多次归档；
- Bookkeeper 获得仓库读取/调查权限，或在刷新时重新全仓扫描；
- 维护失败时覆盖或推进维护基线，或正文与基线未原子提交；
- 恢复旧的 `replay-before / replay-after` 稳定性死循环。

## DEPENDS ON

`knowledge-reuse → repository-investigation, repository-programming, durable-events, durable-convergence`

## Physical fatal boundary

Casebook semantic conflict与cut-tail属于knowledge-reuse durable truth；process termination不属于store。直接fatal可在补偿事实未settle时结束进程，使重启无法判断本次Case更新是否已隔离。
