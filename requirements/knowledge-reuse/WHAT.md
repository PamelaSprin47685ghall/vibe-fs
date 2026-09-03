# knowledge-reuse — WHAT

## KNOWLEDGE-REUSE-001: Casebook 是尽力而为的语义缓存而非真理系统

Inspector Casebook 属于 best-effort 语义缓存：每个 Case 保存针对特定问题的 Q&A 与对应支撑该答案的可重放仓库观察。后续 Inspector 可通过 `fetch` 读取并在当前工作区重放观察。Casebook 不作为代码库真理系统，不维护全局提交历史，不保证历史答案等价于当前规范，严禁使用时间戳裁决有效性，且执行过程严禁修改目标工作区。

## KNOWLEDGE-REUSE-002: Case 结构由逐字问答与可重放观察构成

Case 的问题字段（Q）逐字等于原始 Inspector 的初始任务描述（不经过任何摘要）；答案字段（A）逐字等于实际 Inspector 工具执行返回的规范正文；Observations 包含该答案所依据的全部类型化仓库观察。Bookkeeper 可在维护时调整 Q 或 A，最终 A 仍须满足工具结果大小边界。

## KNOWLEDGE-REUSE-003: 观察捕获基于工具类型化结果而非文本推断

Observation 必须且仅能从只读工具的类型化执行结果中捕获（读取文件对应 `FileRead`、文件检索对应 `GlobResult`、文本搜索对应 `GrepResult`），严禁从大模型输出文本中反向推断。若部分命令无法被类型化识别，不阻止当前 Case 的归档，仅代表未来重放时少一次差异比对机会。

## KNOWLEDGE-REUSE-004: `fetch` 语义由公开 Shelfmark 与前置重放驱动

`fetch(shelfmark)` 仅接收公开的 Shelfmark 标识符，不暴露内部持久化会话标识。执行时首先针对当前工作区只读重放全部已记录的 observations：
- 若未检测到任何环境差异，直接返回精确的规范答案 A，并注明未发现证据变更（作为新鲜度提示）；
- 若检测到环境差异，触发 Bookkeeper 尝试刷新 Case；刷新成功则返回更新后的规范答案，刷新失败则安全退避并返回旧答案 A，同时明确标注其为陈旧记录。

## KNOWLEDGE-REUSE-005: 新鲜度指示不构成正确性证明且允许返回陈旧记录

任何重放无差异的结果仅作为新鲜度提示，不构成当前代码库状态的必然证明。维护流程失败不导致 `fetch` 抛错：Bookkeeper 刷新失败时保留旧 Case 并返回旧答案，容忍过时是系统预期的正常产品语义。

## KNOWLEDGE-REUSE-006: Bookkeeper 契约与单程序原子维护边界

私有 Bookkeeper 代理提供 `CaseRefresh` 与 `CaseFinalize` 两类请求契约。其操作工具严格限定为唯一的 `js-bookkeeper(program)`：单个 JavaScript 程序代表一次原子 staged 变换，`setQuestion` 与 `setAnswer` 在单次程序中至多调用一次，零修改属于合法操作。Bookkeeper 严禁获得文件系统读写权限。

## KNOWLEDGE-REUSE-007: Casebook 持久权威归于统一 EventStore

Casebook 的持久化权威唯一归属于统一的 `EventStore`：由 `InspectorCaseCaptured`、`InspectorCaseRefreshed`、`InspectorCaseAccessed` 与 `InspectorCaseEvicted` 事件以及对应的 `CasebookProjection` fold 构成，大文本通过 PayloadRef 引用存储。严禁设立独立的 Git 分支、文件数据库或私有日志作为第二真源。

## KNOWLEDGE-REUSE-008: LRU 淘汰以事件表达且访问序单调派生

Casebook 维护容量有界的 LRU 缓存：条目淘汰通过追加 `InspectorCaseEvicted` 事件显式表达，被淘汰项退出当前活跃投影。条目的最后访问顺序由 `InspectorCaseAccessed` 事件单调递增派生，严禁依赖系统墙钟时间戳进行淘汰裁决。

## KNOWLEDGE-REUSE-009: 特性启用受 Marker 目录与执行双门禁保护

当仓库缺少 Casebook marker 目录时，系统在提示词层面不注入 `fetch` 工具描述，在执行层面直接拒绝 `fetch` 执行，不构建 Casebook 索引，不追加任何 Casebook 相关事件，保证未启用特性的仓库行为完全中立。

## KNOWLEDGE-REUSE-010: Inspector 生命周期保证作用域内仅 Finalize 一次

对于非复用 Inspector 作用域，会在会话结束时归档捕获结果；对于复用 Inspector 作用域，在整个调用期间仅暂存草稿，直到 ReuseScope 关闭时触发恰好一次 `CaseFinalize`。严禁在每个回合、空闲时刻或定时器触发时重复执行 finalize。异常删除的会话仅执行清理，不追加持久化事件。

## KNOWLEDGE-REUSE-011: 并发分叉显式表达为 DomainConflict 且禁止 LWW

针对同一 Case 的合法并发分支，在合流投影中显式建模为 `DomainConflict`，由后续的刷新或淘汰事件进行收敛。分布式副本合流遵循事件集合并原则，严禁使用基于版本号或墙钟时间戳的 LWW 规则。相同工作区内的并发 fetch 通过 single-flight 进行串行化。

## KNOWLEDGE-REUSE-012: 公开索引仅暴露低信任 Shelfmark 与规范问题

面向外部模型的 `CasebookIndexSnapshot` 属于低信任数据：模型仅可见 `{ shelfmark, canonical question }` 元组。Shelfmark 作为稳定的公开寻址标识，在内部解析为持久化 Case 身份，严禁将内部会话状态、新鲜度标记或机器私有字段泄漏至索引。

## KNOWLEDGE-REUSE-013: Casebook fatal先settle补偿事实再经注入fuse执行

Casebook semantic conflict必须先写入对应durable failure/cut-tail并取得committed或unknown settlement evidence，再构造typed incident。Store/runtime只接受composition注入的mandatory fatal capability；不得直接引用physical adapter、optional/default/global fallback。同一incident只允许一次report与kill，fatal不得修改Case projection、epoch或freshness。
