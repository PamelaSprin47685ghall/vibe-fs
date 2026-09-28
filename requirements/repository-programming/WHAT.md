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

## [012] 事务 Staging 与单一 EventStore 提交

一次调用只有一个事务。全部修改先暂存于内存，`run()` 成功结束前不改变磁盘；持久化 Prepare 与 Commit 只进入统一 EventStore，不另建专有文件或存储。

## [013] 多文件 All-or-Nothing 提交

全部文件修改作为一个事务提交，顺序为：全部预检 → 持久化 Prepare → 按规范路径顺序写入 → 持久化 Commit → 返回成功。任一写入失败时全量回滚，第三方已改内容按 [015] 保留。

## [014] 冲突检测与无隐式重试

预检比较读取时的快照指纹。读取过的文件或写入目标被外部改变时，以 `FILE_CHANGED` 失败，不自动重读、重定位锚点或重跑程序。

## [015] 正常回滚与崩溃后不自动补提交

进程内正常失败按 CAS 还原已写入的临时修改，不覆盖第三方变化。Prepare 后、Commit 前崩溃，重启不得自动重放、回滚或补交事务；未完成 Prepare 只作为中断审计证据，保持失败。

## [016] Synthetic TOML 结果面

成功以 `# ok` 开头，返回值放在 `[data]`、`data = ...` 或 `[[data]]`；有实际写盘时，文末 `[fs]` 声明 rewritten/created 路径。失败以 `# failed` 开头，根级给稳定 code 与可读 reason，不含 data/fs 结果节。

成败由顶级结构区分，不另用 `status = "ok"` 等歧义字段；编码与组合遵守 provider-projection。

## [017] 并行调用安全与确定性串行提交

同一助理消息的多个工具调用由宿主按确定顺序逐个执行，各为独立事务，后者读取前者提交后的状态。同文件修改顺序叠加，不丢更新；不同文件仍各自原子提交。

## [018] 稳定失败代数

预期失败使用稳定具名错误码，不笼统压成 `PROGRAM_FAILED`。原因只给受控摘要，不泄露沙箱内部代码、宿主路径或敏感环境；具体原语的错误码按对应条款。

## [019] 返回值与 Commit 耦合

有修改时，返回值校验与 Commit 均成功后才暴露业务结果；提交失败不返回该结果。纯查询校验后直接返回，无须提交；新旧内容完全相同时成功且不做无意义写盘。

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

事务不变量失败先完成保留第三方变化的 CAS 回滚或 durable semantic cut-tail，取得 committed/unknown 结算证据后才构造 typed incident，并调用由 composition 注入的 mandatory fatal capability。不得直接取得物理适配器或用 optional/default/global 兜底。

同一 incident 只报告、终止一次；旧快照、第三方变化及普通编辑拒绝仍为 typed nonfatal，致命处理不得覆盖工作树。

## [026] 事务快照与案例实质访问分离

ReadSnapshots 只用于事务隔离与冲突检测，不直接作为案例关联文件。案例只记录显式 read/file 的规范路径及已提交的修改、创建、删除、移动路径；grep 内部扫描不算实质访问。提交前 effectPaths 是意图，未提交、校验失败或回滚均不追加修改访问。

## [027] Engineer 与 DevOps 的统一文件面

两者按已授予能力获得直接 Read/Write/Edit/Glob/Grep/Move/Remove 工具及生成的 js-engineer/js-devops。直接与可编程文件操作共享路径边界、UTF-8 校验、符号链接防护和 All-or-Nothing 事务语义。

Manager 的 `js-manager` 仅提供 Read/Glob/Grep，能力过滤同样到达真实 API；评审及当前事实准入遵循 capability-enforcement-025，不因生成工具而取得修改或执行权。
