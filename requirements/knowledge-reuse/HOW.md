# knowledge-reuse — HOW

## 架构机制与核心模型

### 1. 实质访问采集与双基线模型

1. **实质访问采集（Substantive Access Capture）**：
   - 监听直接文件工具与 JS 编程沙箱执行：成功 `read`（含局部读取）记录规范路径并关联整文件；成功 `create`/`write`/`rewrite`/`edit` 记录目标路径；成功 `rm` 记录原路径；成功 `mv` 同时记录旧路径与新路径；
   - 过滤掉 `grep`、`glob`、`ls` 等搜索与枚举行为，过滤未提交或回滚的写操作，严禁从模型自然语言推断访问；
   - 实质访问路径集合在逻辑工作结束时去重固定为 `relatedPaths`。

2. **完整文件状态与双基线（Dual Baselines）**：
   - 逻辑工作结束边界处冻结本次关联文件的完整状态作为初始基线 `completionFileState`（B），至少区分 `Present(内容引用)` 与 `Missing(不存在)`；
   - 初始时 `maintenanceFileState` 同样指向 B；
   - 维护推进时，计算 `maintenanceFileState` 与当前目标状态 T 之间的真实 diff（B → T）；刷新成功后将 `maintenanceFileState` 原子推进为 T，`completionFileState` 永久保持 B。

### 2. 逻辑 Engineer 轨迹与 Fission 收敛

1. **轨迹范围与单次归档**：
   - 案例身份复用本次 invocation 因果身份，同一 Session 的多次 resume 产生多段独立的逻辑来源与案例；
   - 若发生 Fission，逻辑工作整合裂变前工作、各 lane 的 keyed 工作记录与实质访问（取路径并集），以及最终 takeover 收敛结果；
   - 仅在整项逻辑工作完全终结且最终接管完成时，冻结文件状态并触发恰好一次 `CaseFinalize`。

### 3. Bookkeeper 维护与事务机制

1. **请求契约划分**：
   - `CaseFinalize`：初次归档时由 Bookkeeper 根据 Engineer 轨迹生成规范 Q&A；
   - `CaseRefresh`：后续更新时仅输入旧案、关联文件真实 diff 及变更元数据，不提供仓库文件系统读写、glob/grep 搜索或外部调查权限。

2. **事务 Staging 与 SDK**：
   - `BookkeeperStaging` 提供 `beginTransaction`、`snapshot`、`apply` 与 `take` 操作；
   - `js-bookkeeper(program)` 执行传入的 JS 代码，在沙箱中提供 `setQuestion` 与 `setAnswer` 接口，支持单事务内的原子修改；
   - 刷新时正文与新维护基线在同一次事件边界发布；若 Bookkeeper 判定无须修改正文，依然推进维护基线以避免重复计算同一份 diff；若刷新失败，正文与基线均不推进。

3. **一次目标捕获，无 Replay 循环**：
   - `fetch` 执行时对当前工作区执行一次目标捕获 T，计算 B → T：无 diff 直接返回旧案；有 diff 且维护成功后原子更新；
   - 废除 `replay-before / replay-after` 稳定性重放。

### 4. 持久化与索引投影

1. **统一事件流（Store）**：
   - 归属统一 EventStore 的 `casebook` 流，支持 `EngineerCaseCaptured`（兼容 `InspectorCaseCaptured`）、`EngineerCaseRefreshed`、`EngineerCaseAccessed` 与 `EngineerCaseEvicted` 事件；
   - 大文本与文件状态通过 `PayloadRef` 存储在 blob 存储中，事件体仅保留引用与元数据。

2. **低信任索引（Index）**：
   - `CasebookIndex` 管理 `{ shelfmark, canonicalQuestion }` 快照，按 epoch 缓存冻结，不泄漏会话拓扑与内部状态。

### 5. 完整文件状态接通方案 (Physical File State Integration Design)

本方案定义 Casebook 双基线与底层持久化机制的最小接通路径，作为 P4 施工的确定输入：

1. **底层既有能力复用与反平行库原则**：
   - **底层持久化底座**：复用既有的 `EventStoreBlobWriter.WritePayload / ReadPayload`（`Persistence/EventStore/ProcessEventLog.fs`），提供基于 SHA-256 哈希的内容寻址不可变存储，生成与解析 `PayloadRef`；
   - **底层差异提取**：复用既有的 `GitSubject.diffHeadBinary`（`Foundation/GitSubject.fs`），提供关联文件与工作区最新状态间的真实二进制 diff 提取；
   - **零平行快照库**：严禁创建平行的文件快照目录、私有 git 分支或影子备份存储，所有文件状态基线直接由 `PayloadRef` 承载，持久化事实完全收敛于统一 EventStore。

2. **Case 数据模型双基线扩展**：
   - Case 数据结构显式维护两个完整文件状态基线：
     - `completionFileState: Map<string, FileEntryState>`：初次逻辑工作完成时的完整文件状态基线（B），生成后永久只读、绝对不可改写；
     - `maintenanceFileState: Map<string, FileEntryState>`：最近一次成功维护所对应的完整文件状态基线，随维护刷新单调演进（B → C → D）；
   - 文件状态条目分型：
     ```fsharp
     type FileEntryState =
         | Present of PayloadRef
         | Missing
     ```
   - 初始归档时，`completionFileState` 与 `maintenanceFileState` 指向完全相同的状态映射集合。

3. **Lifecycle 结束边界冻结管道**：
   - 在逻辑工作结束边界（Engineer 完成任务或 Fission 最终 takeover 收敛完成时），由 Lifecycle / Casebook 触发冻结管道：
     1. 从工具层实质访问收集去重后的关联路径集合 `relatedPaths`；
     2. 逐一读取磁盘真实文件：若文件存在，调用 `EventStoreBlobWriter.WritePayload` 写入 blob 存储并获得 `Present(PayloadRef)`；若文件不存在或已删除，记录为 `Missing`；若读取发生 IO/权限异常则安全中断；
     3. 构造初始基线 `completionFileState`（B）与 `maintenanceFileState`（B）；
     4. 伴随 `EngineerCaseCaptured` 事件将双基线引用与元数据原子提交至 EventStore。

4. **`fetch` 维护与基线演进管道**：
   - 当调用 `fetch(shelfmark)` 时：
     1. 从当前工作区捕获 `relatedPaths` 的目标文件状态 T（同样转化为 `Map<string, FileEntryState>` 或通过 `GitSubject.diffHeadBinary` 获取真实变更）；
     2. 计算 `maintenanceFileState` 与 T 之间的真实 diff（B → T）；
     3. **无 diff**：直接返回旧案正文，声明未检测到变化；
     4. **有 diff**：调用 `js-bookkeeper` 执行 `CaseRefresh`，传入旧案与真实 diff：
        - 刷新成功：将更新后的正文与推进后的新基线 `maintenanceFileState = T` 在同一 `EngineerCaseRefreshed` 事件中原子提交（`completionFileState` 保持 B 绝对不变）；
        - 刷新失败：放弃更新，保持旧正文与原维护基线不变。

## GAP

- `GAP-KR-001`（OPEN）：Case 数据模型双基线字段（`completionFileState`/`maintenanceFileState`）与 Lifecycle 结束边界自动冻结管道待在 P4 施工中落地接通（底层已具备 `EventStoreBlobWriter.WritePayload/ReadPayload` 与 `GitSubject.diffHeadBinary`）。
