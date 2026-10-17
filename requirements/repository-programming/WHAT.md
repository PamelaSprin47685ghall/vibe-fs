# repository-programming — WHAT

## [001] Capability 投影面与单一权限源

每次请求的 `js-ROLE` 工具只从该 Attempt 的唯一权威能力集生成，不另算 role→JS 权限。没有文件系统 primitive capability 时不生成 `js-*` 工具。

## [002] 四层同构应用

每项文件系统 capability 在公开基类、工具描述、canonical examples 与运行时门禁中一致投影；缺失能力在四层均不可用，伪造调用也须拒绝。一项能力可生成固定顺序的同权成员族，不产生第二份授权。

## [003] 确定性生成

同一角色与能力集生成字节相同的工具名称、Schema、描述、基类及示例。执行档位不进入名称，也不改变同角色的编程面。

## [004] 生成工具名门禁

只接受当前 Attempt 实际拥有的生成工具名。其他角色或旧 Attempt 的名字须拒绝；名字合法不能代替当前能力授权。

## [005] 编程面与推荐诚实性

公开基类、描述和示例只包含当前可执行成员，不暴露宿主私有接口或注入键；说明钩子不得推荐当前 Provider 不可见的工具。

## [006] 沙箱隔离与无 Ambient OS 权限

用户程序只获得显式纯数据与受控原语，不直接获得文件系统、网络、进程或环境变量。执行有硬性超时、内存与输出上限，超时终止并清理资源。结果只由 `run()` 的结构化返回值决定，stdout/stderr 不作为编辑产物。

## [007] 不可变快照与 Anchor 代数

`file(path, matches)` 返回当前事务的不可变 UTF-8 快照，锚点按声明顺序匹配，`text(from, to)` 截取原文；后续修改不改变已取得视图。非法 UTF-8 拒绝为 `INVALID_UTF8`，不猜测或修复编码。

`name±N` 的偏移按解码后 JS 字符串的 UTF-16 代码单元计，不按行或 UTF-8 字节计。声明冲突、空模式或按序未命中返回具名 `ANCHOR_NOT_FOUND`。

## [008] 确定性路径枚举

`glob(pattern)` 确定性枚举 gitignore/wildmatch 路径：`*` 不跨目录，`**` 匹配零段或多段目录；遵守各级 `.gitignore` 与 exclude，不进入 `.git`，不跟随符号链接。内部不截断，超限由宿主结果留尾规则处理。

## [009] Grep Capability 投影

`grep(needle, pattern)` 只由 Grep capability 授权；Read+Glob 不代替它。needle 接受字符串或正则，必填 pattern 按 glob 规则选文件；返回行列位置与匹配文本，不可读或非 UTF-8 文件跳过。

## [010] Rewrite 与 Write 分离

`rewrite` 只替换已有文件，缺失时报 `FILE_NOT_FOUND`；`write` 只创建新文件，已存在时报 `FILE_ALREADY_EXISTS`。同一程序对同一路径只能声明一次修改意图，重复时报 `DUPLICATE_MUTATION_TARGET`。

## [011] 可表示的返回值与提交前校验

返回值限于 JSON 兼容子集：null、布尔值、有限数字、字符串、普通数组与对象；数组不得含 null 或异构类型。undefined、BigInt、NaN、Infinity、函数、Symbol、循环引用及其他非法值均在提交前以 `INVALID_RETURN_VALUE` 拒绝，终止事务。

## [012] 事务 As-Is 逐笔落盘与单一 EventStore 记账

一次调用只有一个事务。每笔 `edit`/`write` 在调用发生时立即经 MutationFs 原子写盘（单笔文件层原子，跨笔不回滚）；`run()` 成功结束前磁盘已经反映已发生的每笔写入。程序以任何方式终止或挂起（throw、超时、输出超限）时，那一刻之前已落盘的改动保持为事实分界线，之后的未发生。

持久化 Prepare 与 Commit 只进入统一 EventStore，不另建专有文件或存储。

## [013] 写前校验、提交计划与 durable 记账失败映射

写前校验作为实现单元保持精确形状并短路：重复的修改意图最先报 `DUPLICATE_MUTATION_TARGET`，缺失目标按快照校验报 `FILE_CHANGED`，全部通过才继续。提交计划（commitPlan）输出精确的执行序列（rewrite 携带 expectedCurrent，create 携带文本），成功时把计划内全部目标写入；计划内某笔写失败时以 `TRANSACTION_COMMIT_FAILED` 失败，并按计划内还原序把已写目标还原——该还原是实现单元自身的语义，工作流主路径仍按 [012] 的 as-is 逐笔落盘。

durable 记账追加失败时（Prepare 或 Commit 阶段的各失败形态），程序结果为 Failed，给稳定码 `TRANSACTION_PREPARE_*`/`TRANSACTION_COMMIT_*`，不报告成功；按 [012]/[029] 的 as-is 语义，已落盘的文件效果如实上报且不回滚。失败不重试追加、不换新事务身份，并携带原事件 id、事务 id 与原始 cause；Prepared 失败后 pending 投影为空（记账未完成），Committed 失败后原 Prepared 仍留在 pending。

EventStore 端口的坏输入防御先结算物理事实（append、fsync、close、release 的确切计数与落盘字节），再经唯一的语义熔断（`js-transaction-semantic-cut`）以 SIGKILL 终止；对 Release 阶段的坏输入，最终报告保留原请求事件身份。

## [014] 冲突检测与无隐式重试

每次写前各自做冲突校验（校验点从统一 preflight 移到每次写前）：若目标路径在 readSnapshots 中，读取当前盘字节与快照比对，不一致时以 `FILE_CHANGED` 失败并停止；已发生的改动保留在盘上。不自动重读、不重定位锚点、不重跑程序。

## [015] 中断证据与重开不重放

Prepare 已入账而无 Committed 是中断审计证据：pending 投影保留该记录，跨 EventStore 重开仍可见。重开只观察证据，不自动重放、回滚或补交事务，磁盘保持中断时已落盘的状态。

物理写失败以 `TRANSACTION_COMMIT_FAILED` 失败，失败笔之前的 as-is 已落盘改动保留；程序失败先于 durable 记账完成时（写失败、返回值非法），记账不发生，不留 durable pending。每笔写入经 MutationFs 单笔原子落盘，重开不盲目重试模糊的半完成提交。

CAS 还原（rollbackPlan：restore 与 removeCreated，逆序）保留为实现单元，还原时按 expectedCurrent 比对、不覆盖第三方已改内容；TransactionStore 不提供手动历史读取接口。

## [016] Synthetic TOML 结果面

成功以 `# ok` 开头，返回值放在 `[data]`、`data = ...` 或 `[[data]]`；有实际写盘时，文末 `[fs]` 声明 rewritten/created 路径。失败以 `# failed` 开头，根级给稳定 code 与可读 reason，并附本次调用已真实落盘的改动清单（`[fs]` 的 rewritten/created）；无已落盘改动时失败不附 fs 节。

成败由顶级结构区分，不另用 `status = "ok"` 等歧义字段；编码与组合遵守 provider-projection。

## [017] 并行调用安全与确定性串行提交

同一助理消息的多个工具调用由宿主按确定顺序逐个执行，各为独立事务，后者读取前者提交后的状态。同文件修改顺序叠加，不丢更新；不同文件仍各自原子提交。

## [018] 稳定失败代数

预期失败使用稳定具名错误码，不笼统压成 `PROGRAM_FAILED`。原因只给受控摘要，不泄露沙箱内部代码、宿主路径或敏感环境；具体原语的错误码按对应条款。

## [019] 失败不产生 durable Commit，已落盘改动保留

程序以任何方式失败（throw、返回值非法、目标缺失）时结果为 Failed，不产生 durable Commit；失败前已按 as-is 落盘的改动保留在盘上——非法返回值不撤销已应用的重写。新旧内容完全相同时成功且不做无意义写盘。

## [020] 文件变换的 POSIX 语义

`mv` 移动或重命名文件及目录，支持覆盖，源缺失报错；`rm` 删除文件或空目录，拒绝非空目录。参数错误与操作系统失败给稳定可读结果。

## [021] 禁止手写 per-role 工具变体

角色编程工具统一按角色及其已授予能力生成，不手写各角色变体；合法工具名登记中的枚举不承担额外授权。

## [022] 工具选择与失败经验引导

描述先给可执行形态与原语选择，再给显著风险和失败经验：

- 普通替换、插入、删除及全匹配修改默认用 edit，同文件局部修改合并为一个 changes 数组。
- 有 Read 时，结构切片、重排和计算式变换可用快照、锚点、text 与 rewrite；无 Read 时只教授已有的 rewrite。具备高级原语时，不默认退回手写字符串边界或盲目大替换；grep 只找候选。
- 示例提供 replace/insert/delete/all 的最小可复制形态，并保留一个展示角色职责与多文件事务的 Ultra Example。
- 在同一快照上形成完整目标后单次 staging，不留错误给后续程序清理；返回前检查关键不变量与数据规模，异常时抛错取消提交。

## [023] Edit capability 的渐进式成员族

Edit 按固定顺序生成同权的 edit、rewrite；rewrite 保持完整文件替换能力及原事务语义，不新增尺寸限制。

`edit(path, changes)` 接受单个 plain object 或非空数组，canonical 字段为 `{ find, put, all? }`：find 是非空字符串或非零宽正则，put 是完整目标文本，all 默认 false。允许无歧义的 oldText/newText、search/replace 别名，但文档只教授 canonical 形态；未知字段或非法参数在读文件前以 `INVALID_EDIT` 拒绝，不污染 ReadSet。

全部 change 在同一原始快照定位：默认恰好一处，all 至少一处并替换全部非重叠命中。全部成功且相互不重叠后才暂存一个 Rewrite；任一失败零暂存。字符串匹配可将一致 CRLF 与调用方 LF 对应，结果保留原换行风格；除此之外只有精确匹配获得写权限。

成功返回冻结的 `{ path, changed, operations, replacements }`；最终内容不变时 changed 为 false、零暂存。内部读取参与快照冲突检测，提交前外部变化仍报 `FILE_CHANGED`；它不要求公开 Read，也不使 file 出现在 Edit-only 编程面。

## [024] 编辑失败恢复协议与保守近似

edit 的预期失败至少区分 `INVALID_EDIT`、`EDIT_NOT_FOUND`、`EDIT_AMBIGUOUS`、`EDIT_OVERLAP`，不压成 `PROGRAM_FAILED`；均在 staging 前失败。reason 给受控的路径、change 序号、尝试的 find、失败种类与本调用零修改后果。

- 未命中的字符串给有限带行号近似窗口；仅当候选唯一、达到保守置信阈值且完整建议在预算内时，给只修正 find、保留 put 的可复制 change。建议 find 必须是实际精确子串，不用近似整行代替原 span。
- 多义匹配给有限候选，说明扩充唯一上下文，或仅在全部命中都应修改时用 all。重叠要求合并为声明最终文本的一个 change，不按数组顺序猜优先级。
- 近似只用于诊断，不自动落盘；所有窗口、字段名、候选数及建议独立于文件/put 大小有界，预算不足省略建议，不制造资源故障。
- 控制语完整本地化；code、API 字段、路径等协议 token 可不变，其余修复说明不混入另一语言。

## [025] 事务致命失败先结算再熔断

事务不变量失败先完成 durable semantic cut-tail 结算（保留已落盘的 as-is 文件效果与第三方变化），取得 committed/unknown 结算证据后才构造 typed incident，并调用由 composition 注入的 mandatory fatal capability。不得直接取得物理适配器或用 optional/default/global 兜底。

同一 incident 只报告、终止一次；旧快照、第三方变化及普通编辑拒绝仍为 typed nonfatal，致命处理不得覆盖工作树。

## [026] 事务快照与案例实质访问分离

ReadSnapshots 只用于事务隔离与冲突检测，不直接作为案例关联文件。案例只记录显式 read/file 的规范路径及已提交的修改、创建、删除、移动路径；grep 内部扫描不算实质访问。提交前 effectPaths 是意图，未提交、校验失败或回滚均不追加修改访问。

## [027] Engineer 与 DevOps 的统一文件面

两者按已授予能力获得直接 Read/Write/Edit/Glob/Grep/Move/Remove 工具及生成的 js-engineer/js-devops。直接与可编程文件操作共享路径边界、UTF-8 校验、符号链接防护和 As-Is 逐笔落盘写语义（每笔写即原子落盘、跨笔不回滚、失败保留已落盘改动）。

Manager 的 `js-manager` 仅提供 Read/Glob/Grep，能力过滤同样到达真实 API；评审及当前事实准入遵循 capability-enforcement-025，不因生成工具而取得修改或执行权。

## [028] programPromise 暴露与 deadline 可重置

沙箱启动与结果等待解耦：`runSurface` 返回一个持有 `ProgramPromise`（async IIFE 的未 settle promise）与可变 `DeadlineRef` 的句柄，调用方可跨调用持有该 promise 而不在启动处 await 到底；vm context 由 promise 未 settle 时的闭包链自然持有，不显式提升。异步 deadline 从生成代码内联常量改为 api 闭包捕获的可变引用，`resetDeadline` 可在不重包、不重跑程序的前提下改变后续 api 调用的超时窗口。哨兵识别、错误分类与结果反序列化保持不变。

## [029] As-Is 逐笔落盘的失败与成功形态

每次 `edit`/`write` 调用即原子落盘，跨笔不回滚。程序后续失败、终止或挂起（throw、超时、输出超限、返回值非法）时，已落盘的改动保持为事实分界线并如实上报：

- 成功返回 `# ok`，`[fs]` 声明 rewritten/created。
- 失败返回 `# failed`，根级给 code/reason，并附本次调用已真实落盘的改动清单（`[fs]` 的 rewritten/created）；无已落盘改动时失败不附 fs 节。
- 写前冲突校验（目标当前内容与写者所见不一致）以 `FILE_CHANGED` 失败，该笔不落盘，此前已落盘的改动保留。

## [030] park 与 Parked 结果

程序调用 `await this.park(payload)` 后执行结果变为 Parked：顶级 parked 载荷携带非空 handle 与程序传入的 payload；渲染文档以 `# parked` 开头并包含 handle。未调用 park 的程序正常完成（Succeeded，无 handle）。park 是 async 成员，在 await 点挂起程序并让本次调用返回该 parked 载荷；首个 park 赢得竞争，第二次 park 要等第一次恢复后才到达。

## [031] 泄漏兜底三件套的调用接线

交付硬门禁接在 deliverSpec 执行链（plan/manager 分派之前）：存在未处理 parked handle 时拒绝交付，拒绝内容列出全部 handle，资源路径 `tool/js-program/parked-deliver-blocked`。每回合提醒经由 PairHint 的 composeMarkerDocument 并入：查询全部未表态 handle，非空时渲染 `tool/js-program/parked-hint` 提醒并追加为 marker 文档指令；无载体回合不走到 composeMarkerDocument，不凭空注入。会话终结在 ClearSession 清理链中强制 reap 该 session 名下全部 parked 现场，durable 记 Reaped。两个资源均为 zh-CN.md/en.md 成对双语文件、占位符集合一致且含 `{{handles}}`。三个调用点只调用 JsParkedSites，不定义现场表类型或 reap 接口本身。

## [032] unpark 恢复

以 `{ unpark: { handle, value } }` 调用恢复挂起程序：恢复后的程序继续运行到完成，返回继续结果（Succeeded，无 handle）；挂起点的 await 表达式求值为注入值。未知 handle 报 `HANDLE_UNKNOWN`。恢复后的程序再次 park 时返回新的 parked 载荷与新 handle。

## [033] handle 恰好消费一次

一个 handle 只被消费一次：第一次 unpark 恢复程序，第二次对同一 handle 的 unpark 报 `HANDLE_UNKNOWN`。abandon 同样消费 handle：abandon 之后对同一 handle 的 unpark 报 `HANDLE_UNKNOWN`。对未知 handle 的 abandon 也报 `HANDLE_UNKNOWN`。

## [034] abandon 与程序侧 finalizer

abandon 以拒绝 parked await 的方式终结程序：原生 try/finally 作为 finalizer 照常运行，finalizer 内的 rm/write 按 as-is 落盘。abandon 清理窗口内 api 仍可照常使用。程序捕获放弃并继续执行时，其返回值被丢弃，结果仍为 Abandoned（Failed，无值）。

## [035] abandon 清理窗口内禁止再 park

清理窗口内的 park 被拒绝，程序侧得到 `{ ok: false, code: 'CANNOT_PARK_WHILE_ABANDONING' }` 结果；程序可通过 as-is 写盘记录该拒绝。

## [036] parked-site 表并发上限

parked-site 表有进程级并发上限（默认 8）：上限内 park 成功；超出后下一个 park 以 Failed 与 `CONCURRENCY_LIMIT` 拒绝，无排队。一个现场被 abandon 释放后，新 park 可入。

## [037] Parked 载荷渲染

parked 结果渲染为顶级 parked 载荷文档：handle 字段与程序 payload 的字段平铺进文档顶层，可按 Synthetic TOML 解析。载荷就是程序传入的数据，原样呈现，不是宿主裁决。

## [038] park 的编程面投影

park 是生命周期能力：任何 js-* 生成面无条件携带。生成基类含 `async park(payload)`；只读面（如 Manager）同样携带。描述与示例教授 park/unpark/abandon 三态契约。park 不是文件系统权限投影，不进入 members 列表，保持既有角色的成员序列字节同一（file、glob、grep、edit、rewrite、write、rm、mv）。

## [039] 现场表归属与进程内存活

park 现场表是独立的进程级静态表模块 JsParkedSites（ParkedSites.fs，并发上限 8），唯一归属、不散落：工具编排层 ToolWorkflow 与 session 回收层 PluginSessionScope 共享引用；调用方（ToolRegistry、PairProgrammingThoughtTransform、PluginSessionScope）不重定义表模块。parked 现场携带 session owner，会话终结可据此 reap。parked 程序进程内存活：同进程内后续调用仍见其挂起，handle 可解析；每次 park 得到不同 handle。

## [040] rm/mv：Move/Remove 编程面与 as-is 落盘

`rm(path)` 与 `mv(from, to)` 由 Remove/Move capability 投影（`JsCapability.ofToolPermission` 是唯一映射），与 edit/write 同属 as-is 逐笔落盘路径。四层同构：生成基类含 `rm`/`mv` 方法、工具描述与 canonical example 含对应成员、运行时门禁按 capability 挂载 rm/mv 执行器。Manager 的 `js-manager` 仍只读，不含 Move/Remove。

- `rm` 删除文件或空目录；非空目录报 `DIRECTORY_NOT_EMPTY`，缺失目标报 `FILE_NOT_FOUND`。成功返回 `{ ok: true, removed: path }`。
- `mv` 按 POSIX 语义移动或重命名文件及目录，支持覆盖；源缺失报 `FILE_NOT_FOUND`。成功返回 `{ ok: true, moved: from, destination: to }`。
- 越界拒绝：rm 的目标、mv 的源与目标都在能力边界内校验，越界报 `PATH_DENIED` 且源存活。继承 [014] 的写前冲突校验：file() 取得的快照仍新鲜时，目标可照常被 rm 删除落盘。
- 每次 rm/mv 各记一笔 durable 条目，Prepared/Committed 成对排空 pending 投影；渲染结果报 `removed = [...]` 与 `moved = ["from -> to"]`。
- 稳定失败码 `DIRECTORY_NOT_EMPTY`、`CROSS_DEVICE_MOVE`、`INVALID_ARGUMENT` 登记在冻结失败目录；EXDEV 依靠 renameSync 原子语义保证源与目标都不残留中间态。

## [041] Lifecycle 审计的生产接线与会话收束

Parked/Unparked/Abandoned 的 lifecycle 审计沿生产组装路径接线：PluginHostInterop 从 workspace 的统一 WorkspaceEventStore 同源构造 `IJsLifecyclePersistence`（与 jsTransactionPersistence 共用同一 store），经 ToolRegistry.create 贯通到每个生成的 JsToolSpec；JsToolSpec.execute 调用点传入该 persistence，不留硬编码 None。JsToolWorkflow 在执行链上记录 Parked、Unparked、Abandoned 三类事件。宿主侧 ToolHostSurface 与 WorkflowSurface 同样从各自的 store 构造 lifecycle persistence 并传入调用。

四种 lifecycle 事件类型（Parked、Unparked、Abandoned、Reaped）同住 TransactionStore 的单一审计流（createLifecyclePersistence 与 appendLifecycleEvent）。会话关闭链沿 PluginBoot → PluginRuntimeScope → PluginSessionScope 贯通 jsLifecyclePersistence；ClearSession 先以 JsParkedSites.handlesForSession 枚举现场，reapSession 之后逐 handle 记录 Reaped 审计事件（durable.AppendLifecycleEvent）。ParkedSites 现场表保持进程内存活、不触碰 durable：Reaped 审计归调用方，现场表自身不含 appendLifecycleEvent。