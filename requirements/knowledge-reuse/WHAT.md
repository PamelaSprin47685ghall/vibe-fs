# knowledge-reuse — WHAT

## [001] Casebook 是尽力而为的语义缓存而非真理系统

Casebook 属于 best-effort 语义缓存：每个 Case 保存针对特定工程问题的 Q&A 与对应支撑该答案的实质访问路径及完整文件状态基线。后续调用可通过 `fetch` 读取并根据文件差异进行维护。Casebook 不作为代码库真理系统，不保证历史答案等价于当前规范，严禁使用时间戳裁决有效性，且执行过程严禁对工作区产生意外破坏。对外仅描述「关联文件未检测到变化」或「已根据所提供差异维护」，严禁宣称「案例已验证正确」。

## [002] Case 最小数据模型与双基线引用

Case 结构由以下核心事实构成：
1. `identity`：本次逻辑工程工作的稳定案例身份（复用 invocation 因果身份，不以物理 SessionId 为唯一标识）；
2. `sourceTrace`：不可变轨迹引用及其工作范围；
3. `question / answer`：可维护的逐字初始任务描述与规范结果正文；
4. `relatedPaths`：实质访问路径的去重集合；
5. `completionFileState`：轨迹结束时的完整文件状态引用（B），永不改写；
6. `maintenanceFileState`：最近一次成功维护所对应的完整文件状态引用（维护推进 B→C→D）；
7. `accessOrder`：由访问事件派生的单调访问序。
初始时两个文件状态引用指向同一份存储引用，不复制完整文件。

## [003] 实质访问采集合同

关联文件路径必须且仅能从工具层面的**实质访问**中采集：
- **成功读取**：成功读取文件正文（含局部读取）记录规范路径，关联整个文件，不维护行级依赖；
- **成功创建**：记录新路径；
- **成功修改**：记录目标路径；
- **成功删除**：记录原路径，结束状态允许为 `Missing`；
- **成功移动／重命名**：同时关联旧路径与新路径；
- **排除项**：`grep`、`glob`、目录列表（`ls`）、只展示符号位置的导航、模型在回答/附件/命令字符串中提及的路径，均**不构成实质访问**，严禁计入关联；
- **执行失败**：工具执行失败或未提交的修改意图不记录修改，但此前已发生的真实读取仍可记录。
采集必须在实际工具执行层完成，严禁由模型主动上报，不设行数阈值，不构造静态依赖闭包。

## [004] 结束边界冻结基线与外部变化

逻辑工程工作结束时，立即固定 `sourceTrace` 与 `relatedPaths`，并冻结本次结束时的完整文件状态基线（`completionFileState` = B）。
- 完整状态精确区分 `Present(内容引用)` 与 `Missing(当时不存在)`，读取失败/权限错误不得作为 Missing 处理；
- 「外部变化」指本段轨迹之外的一切修改（包括其他工作、DevOps 修复、或同一 Engineer 的下一次 resume）；
- DevOps 的修复按 B→C 更新相关案例，但不成为 Engineer 来源，亦不单独产生伪造的 Engineer 案例。

## [005] `fetch` 语义由公开 Shelfmark 与真实 Diff 驱动

`fetch(shelfmark)` 仅接收公开的 Shelfmark 标识符，不暴露内部持久化会话标识。执行时：
1. 读取旧案及其维护基线 B；
2. 捕获一次关联文件在当前工作区的目标状态 T，并计算真实 diff（B → T）；
3. **无差异**：直接返回旧案正文，并声明关联文件未检测到变化；
4. **有差异**：触发 Bookkeeper 传入旧案与真实 diff 执行 `CaseRefresh`；
   - 刷新成功：将更新后的案例正文与新基线 `maintenanceFileState = T` 在同一事件边界原子提交，并返回新正文；
   - 刷新失败：安全退避，保留旧案与原维护基线 B，返回旧正文并明确标注维护未完成；
5. 一次目标捕获，不做前后 replay 循环或稳定性重试，容忍过时是系统预期的正常产品语义。

## [006] Bookkeeper 契约与单程序原子维护边界

私有 Bookkeeper 代理提供 `CaseFinalize`（初次按轨迹整理）与 `CaseRefresh`（按旧案+真实 diff 更新）两类请求契约：
- **无仓库调查权**：Bookkeeper 没有仓库 `read`/`glob`/`grep`、无完整新旧文件、无 Inspector 调查权限，只能通过 `js-bookkeeper` 访问并编辑正在维护的案例自身；
- **单程序原子操作**：单个 JavaScript 程序代表一次原子 staged 变换，`setQuestion` 与 `setAnswer` 在单次程序中至多调用一次，零修改属于合法操作；
- **正文与基线原子提交**：Bookkeeper 可以判定 diff 与结论无关而保持正文不变，成功后仍推进维护基线；失败则正文与基线均不推进。

## [007] Casebook 持久权威归于统一 EventStore

Casebook 的持久化权威唯一归属于统一的 `EventStore`：由 `InspectorCaseCaptured`（或 `EngineerCaseCaptured`）、`InspectorCaseRefreshed`（或 `EngineerCaseRefreshed`）、`InspectorCaseAccessed` 与 `InspectorCaseEvicted` 事件以及对应的 `CasebookProjection` fold 构成，大文本与文件状态通过 PayloadRef 引用存储。严禁设立独立的分支、文件数据库或私有日志作为第二真源。

## [008] LRU 淘汰以事件表达且访问序单调派生

Casebook 维护容量有界的 LRU 缓存：条目淘汰通过追加 `InspectorCaseEvicted` 事件显式表达，被淘汰项退出当前活跃投影。条目的最后访问顺序由 `InspectorCaseAccessed` 事件单调递增派生，严禁依赖系统墙钟时间戳进行淘汰裁决。

## [009] 特性启用受 Marker 目录与执行双门禁保护

当仓库缺少 Casebook marker 目录时，系统在提示词层面不注入 `fetch` 工具描述，在执行层面直接拒绝 `fetch` 执行，不构建 Casebook 索引，不追加任何 Casebook 相关事件，保证未启用特性的仓库行为完全中立。

## [010] 逻辑 Engineer 轨迹生命周期与 Fission 收敛归档

案例归档以一次逻辑 Engineer 工作轨迹为唯一来源：
1. **单次归档**：正常完成（实现、调查或交回边界）触发恰好一次 `CaseFinalize`；同一 Session 多次 resume 产生多段独立来源与案例，不互相覆盖；
2. **Fission 合并**：若发生 Fission，逻辑工作包含裂变前工作、各 lane 的 keyed 记录与实质访问，以及确定性收敛与最终 takeover；访问路径取并集，全部收敛后冻结一次文件状态，只归档一次；
3. **忽略重复与晚到事件**：单 lane 完成、晚到 terminal、重复事件或中间压缩事件严禁触发单独归档；异常取消的会话执行清理，不追加持久化事件。

## [011] 并发分叉显式表达为 DomainConflict 且禁止 LWW

针对同一 Case 的合法并发分支，在合流投影中显式建模为 `DomainConflict`，由后续的刷新或淘汰事件进行收敛。分布式副本合流遵循事件集合并原则，严禁使用基于版本号或墙钟时间戳的 LWW 规则。相同工作区内的并发 fetch 通过 single-flight 进行串行化。

## [012] 公开索引仅暴露低信任 Shelfmark 与规范问题

面向外部模型的 `CasebookIndexSnapshot` 属于低信任数据：模型仅可见 `{ shelfmark, canonical question }` 元组。Shelfmark 作为稳定的公开寻址标识，在内部解析为持久化 Case 身份，严禁将内部会话状态、会话拓扑、新鲜度标记或机器私有字段泄漏至索引。

## [013] Casebook fatal先settle补偿事实再经注入fuse执行

Casebook semantic conflict必须先写入对应durable failure/cut-tail并取得committed或unknown settlement evidence，再构造typed incident。Store/runtime只接受composition注入的mandatory fatal capability；不得直接引用physical adapter、optional/default/global fallback。同一incident只允许一次report与kill，fatal不得修改Case projection、epoch或freshness。

## [014] 预算与截断诚实性

大轨迹与大 diff 必须遵守预算限制，执行截头取尾处理，并保留明确的截断声明与变更路径信息。Bookkeeper 严禁将仅见尾部声称已审阅全部变更；无法做出有效更新时保留旧案与旧基线，严禁通过反复重试、模型蒸馏或读取全仓文件绕过预算。

## [015] 废止严格 Replay 与稳定性校验循环

系统不采用基于 `FileRead/GlobResult/GrepResult` 集合相等性判定的严格 replay 机制，不执行刷新前后的 `replay-before / replay-after` 稳定性循环。案例的新鲜度维护完全基于真实文件 diff 进行单次迁移。
