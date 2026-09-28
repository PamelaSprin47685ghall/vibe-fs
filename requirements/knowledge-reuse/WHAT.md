# knowledge-reuse — WHAT

## [001] Best-effort cache

Casebook 保存工程问题的 Q&A、实质访问路径和完整文件状态基线，供后续读取及按差异维护。历史答案不证明当前规范或代码正确；不得用时间戳裁决有效性或意外破坏工作区。对外只说明关联文件未检测到变化、已按所提供差异维护或维护未完成，不宣称案例已验证正确。

## [002] Case 与双基线

每个 Case 包含：基于逻辑工作 invocation 的稳定 identity、不可变 sourceTrace 引用及工作范围、可维护的 question/answer（逐字初始任务描述与规范结果正文）、去重 relatedPaths、不可变 completionFileState、最近成功维护的 maintenanceFileState，以及访问事件派生的单调 accessOrder。物理 SessionId 不能单独决定案例身份；初始双基线共用同一存储引用。

## [003] 实质访问

关联路径只从实际工具执行层采集：成功读取（含局部读取）关联整个文件；成功创建、修改、删除关联相应路径，移动关联旧、新路径。失败或未提交的修改意图不计入，但此前真实读取仍可计入。

grep、glob、目录列表、仅展示符号位置的导航及文本中提及的路径不构成实质访问。不由模型上报，不设行数阈值，不建立静态依赖闭包。

## [004] 完成边界

逻辑工程工作结束时固定 sourceTrace、relatedPaths 和完整 completionFileState。状态区分 Present 与 Missing，读取故障不能冒充不存在。轨迹外的修改均属外部变化，包括其他工作、DevOps 修复和同一 Engineer 下次 resume；DevOps 修复可推进原案维护基线，但不改写其 Engineer 来源或伪造新 Engineer 案例。

## [005] Fetch

`fetch(shelfmark)` 只接受公开 Shelfmark，不暴露内部持久化会话标识。读取旧案及维护基线 B，一次捕获关联文件目标状态 T，计算真实 diff（B→T）：

- 无差异：返回旧正文，说明关联文件未检测到变化。
- 有差异：以旧案和 diff 请求 CaseRefresh；成功时在同一事件边界原子提交正文与维护基线 T，返回新正文；失败时保留旧案和基线 B，返回旧正文并说明维护未完成。

不以稳定性重试追求最新状态；允许捕获后的外部变化使结果过时。

## [006] Bookkeeper

私有 Bookkeeper 只提供 CaseFinalize（按轨迹整理）和 CaseRefresh（按旧案与 diff 维护），通过 `js-bookkeeper` 访问当前案例；无仓库调查或读写权，不接收完整新旧文件。

一次 JavaScript 程序是一笔原子 staged 变换，setQuestion、setAnswer 各至多调用一次，零修改合法。成功即推进维护基线，包括判断 diff 无关而保持正文的情况；失败则正文与基线均不推进。

## [007] 持久权威

捕获、刷新、访问、淘汰事实及其派生 Case 投影统一归 EventStore；大文本和文件状态通过 PayloadRef 保存。不得设立独立分支、文件数据库或私有日志作为第二权威。

## [008] LRU

缓存容量有界。淘汰以追加事件表达，使条目退出活跃投影；最近访问次序由访问事件单调派生，不依赖墙钟时间戳。

## [009] 未启用时保持中立

仓库没有 Casebook marker 目录时，不注入 fetch 描述，不构建索引，不追加 Casebook 事件，并在执行入口拒绝 fetch。

## [010] 逻辑工作归档

一次逻辑 Engineer 工作正常完成（包括实现、调查或交回边界）只触发一次 CaseFinalize。同一 Session 的每次 resume 是独立来源和案例，不能互相覆盖。

Fission 来源包含裂变前工作、各 lane 的 keyed 记录和实质访问、确定性收敛与最终 takeover；访问路径取并集，全部收敛后只冻结、归档一次。单 lane、重复或晚到 terminal、中间压缩不能单独触发归档；异常取消只清理，不追加持久化事件。

## [011] 并发

同 Case 的合法并发分支在合流时显式形成 DomainConflict，由后续刷新或淘汰收敛。跨副本采用事件集合并，不按版本或墙钟 LWW；同工作区并发 fetch 使用 single-flight 串行化。

## [012] 公开索引

索引是低信任数据，只向模型暴露 `{shelfmark, canonical question}`。Shelfmark 稳定寻址内部 Case，不泄漏内部会话状态、拓扑、新鲜度标记或机器私有字段。

## [013] Fatal settlement

Casebook semantic conflict 须先写 durable failure/cut-tail 并取得 committed 或 unknown settlement evidence，再构造 typed incident。fatal 只能通过装配时必需注入的 capability 执行，无 physical adapter 直连、optional/default/global fallback。同一 incident 只 report、kill 一次；fatal 不修改 Case 投影、epoch 或 freshness。

## [014] 预算与截断

大轨迹和 diff 须在预算内截头取尾，保留截断声明和变更路径。不能把只见尾部说成审阅全部；无法有效更新时保留旧案与基线，不用反复重试、模型蒸馏或全仓读取绕过预算。

## [015] 单次差异维护

新鲜度维护仅基于真实文件 diff 的单次迁移，不以 FileRead/GlobResult/GrepResult 集合相等判定有效性，不运行 replay-before/replay-after 稳定性循环。

## [016] 完整文件状态

Present 绑定完整内容的 SHA-256 和持久 payloadRef；同内容复用同引用，blob 不可覆写，相同 digest 对应不同字节须 fail closed。Missing 只表示捕获时物理文件不存在；Missing→Present 和 Present→Missing 分别产生新增、删除差异。

打开、读取或哈希存储发生 I/O 故障时，立即停止冻结并返回失败，归档结算为 notCommitted；不得将故障伪装为 Missing 或 Present。
