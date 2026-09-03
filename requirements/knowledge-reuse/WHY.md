# knowledge-reuse — WHY

## 领域动力与核心张力

历史沉淀的仓库知识（Casebook）能够作为极高价值的语义缓存与新鲜度提示（freshness hint），显著减少针对代码库的重复调查开销。然而，**历史问答与观察记录绝不能直接等同于对当前代码库真实状态的证明**。若将历史缓存直接作为当前真理，代码库演进后的过时答案将成为系统判断的非法真源；若完全禁止复用历史记录，系统又将在每一次相同查询上面临重复的全量调查成本。

`knowledge-reuse` 的核心存在理由是确立基于「先重放、后刷新」的语义缓存机制：
1. **重放优先与提示语义**：通过 `fetch(shelfmark)` 对当前工作区重放已捕获的类型化观察；观察未发生变化仅表明当前答案具备高新鲜度，属于提示而非绝对证明；
2. **容忍过时与失败退避**：当观察发生变化时由私有 Bookkeeper 尝试刷新 Case；若刷新失败，返回旧答案并明确告知其属于陈旧记录，容忍过时是预期的产品语义；
3. **统一持久化权威**：Case 事实完全由统一的 EventStore 承载，淘汰通过追加事件表达，并发冲突由领域冲突（DomainConflict）显式建模，杜绝本地时间戳竞争或私有存储分叉。

## 核心不变量

1. **缓存定位与先重放机制**：Casebook 是尽力而为的语义缓存，`fetch` 调用必须先对当前工作区执行只读观察重放。
2. **类型化捕获与逐字记录**：观察记录严格从类型化工具执行结果中提取，不从文本推断；问题与答案保持逐字原样记录，不经过第二作者摘要。
3. **单程序原子维护（Bookkeeper）**：维护操作由私有的 `js-bookkeeper` 在单个事务内原子完成，不支持文件修改权限。
4. **统一事件溯源与显式冲突**：Case 状态流转通过 `InspectorCaseCaptured`、`InspectorCaseRefreshed`、`InspectorCaseAccessed` 与 `InspectorCaseEvicted` 表达；并发副本合流遵循事件集合并，冲突显式表达为 DomainConflict，严禁使用 LWW（最后写入者胜出）。
5. **低信任公开索引**：面向外部仅暴露包含 Shelfmark 与规范化问题的低信任索引快照，内部机器字段与状态不泄漏至提示词。

## 边界与失效模式

- **不负责当前仓库事实确立**：当前观察的采集法则与只读约束归 `repository-investigation`。
- **不负责事件持久化底层**：底层事件追加与 CAS 存储归 `durable-events`。
- **不负责分布式收敛物理律**：事件集合合并与并发收敛机制归 `durable-convergence`。

**失效表现（RED）**：
- 历史 Case 未经当前工作区重放即直接断言为当前真相；
- 从模型文本中猜测提取观察内容而非从类型化工具返回捕获；
- 引入私有数据库或使用时间戳/版本号执行 LWW 冲突裁决；
- Inspector 会话在单次调用或每个回合重复触发 finalize。

## DEPENDS ON

`knowledge-reuse → repository-investigation, durable-events, durable-convergence`

## Physical fatal boundary

Casebook semantic conflict与cut-tail属于knowledge-reuse durable truth；process termination不属于store。直接fatal可在补偿事实未settle时结束进程，使重启无法判断本次Case更新是否已隔离。
