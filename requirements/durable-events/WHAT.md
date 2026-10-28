# durable-events — WHAT

本包定义本机持久化事件底座的可观察行为：事件如何追加、如何判定提交、如何重放、坏数据如何拒绝。状态收敛规则见 durable-convergence。

## [001] Event 是唯一 durable truth 且 append-only

动态业务状态只能由不可变事件表达。状态变更是追加新事件，状态撤销是追加 tombstone/retirement 事件。在 durable-convergence-011 的 writer retention 窗口内，已提交事件不得被修改、覆盖、删除或重新解释；超过窗口的 writer 允许作为完整物理流整体退出。投影只是衍生状态，可随时从事件重建；先改内存投影后补盘属于违规。

## [002] EventEnvelope 无版本与 additive vocabulary

事件信封是统一结构：`event_id`、`stream_id`、`event_type`、`parents`、`payload`、`payload_refs`、内嵌 `payloads`。信封与存储层不携带 format/schema version 字段。已提交的 `event_type` 载荷结构一经发布即冻结；新业务语义必须声明全新的 `event_type`。

## [003] canonical JSON 是 identity 协议

事件按规范化 JSON 序列化：UTF-8、无 BOM、以单个 LF 结尾；对象键按 Unicode 码点升序递归排序；`parents` 与 `payload_refs` 先去重再按字符序排序，内嵌 `payloads` 键按字符序排序。相同 `event_id` 产生不同字节流是标识碰撞，fail-closed；相同 `event_id` 且相同字节流则幂等去重。

## [004] local append 的提交原语为完整 NDJSON 行

本地事件持久化的提交动作是在当前 writer 的当前段文件末尾（见 [005]）写入完整的 canonical 行（`JSON + LF`）。成功返回前完成物理落盘并释放跨进程存储锁。运行时追加不创建 Git blob、tree、ref，也不做 Git CAS。半行、截断行、原地覆写均非法；滚动切换目标段不改写已封存段的任何字节。

## [005] Process 对应 single writer 与有界段序列

每个进程实例持有全局唯一的 `WriterId`，独占追加 `.Herdchestra/wanxiangshu/events/<WriterId>-<seq>.ndjson` 段文件序列（`seq` 为四位零填充十进制，自 `0000` 起连续；`WriterId` 不得以 `-` 加四位数字结尾）。一个 writer 的逻辑流是同 `WriterId` 全部段按 `seq` 递增拼接的字节序列；不可变、按序重放、只在末尾追加的语义对整条逻辑流成立。单段上限 64 MiB（67,108,864 字节）：追加前若当前段非空且加上本次编码字节后超限，滚动到下一 `seq`；单次追加自身超限时允许独占一段，其后必须滚动。滚动不是独立物理事务：向新段写入失败沿用 [006] 的阶段语义，已写入字节不变；不得为绕开失败向超限旧段继续追加。进程退出后其段序列整体封存，不被新进程接管；超过 retention TTL 后允许整条删除。新进程必须创建新的 `WriterId`；业务流、机器、角色不作为物理写者划分依据。

无段后缀的旧形态文件（`<WriterId>.ndjson`）按完整内容读作单条逻辑流。物化阶段（`RetentionSurface.reorganizeWithSegmentCap`）可对超上限旧形态文件做一次性重组：按行边界把事件行原样分配到新段序列，逻辑流与原文件逐字节等价；全部新段落盘成功后才删除旧文件。重组只在存储锁内的物化阶段执行，不产生新事件、不改写事件字节。旧文件与段序列并存时的收敛规则：互为字节前缀或相等时，较长一方权威，较短一方在替换残骸处置落盘后移除；旧文件由完整 canonical 行组成且事件 ID 与段序列无重叠（行级延续）时，把旧行字节不变地追加进段序列尾段（空间不足则滚动），落盘后删除旧文件；读取层按「段拼接在前、旧行在后」组装逻辑流。重叠事件 ID、半行、非 canonical 行不属于任何收敛判定，按 [007] fail-closed。

## [006] commit outcome 只由本地事实存在性判定

提交成功的判定标准只有一条：该 `event_id` 的 canonical 字节已完整存在于本地事实流。不得通过 Git ref、内存状态或退出码反推提交状态。进入物理追加之前的异常是 `AppendNotAttempted`；进入追加之后的写入、durability barrier、commit 或锁释放异常是 `CommitUnknown`；重复或空请求未产生新追加时，锁释放失败独立为 `NoNewWriteReleaseFailed`，不撤销已有事实。错误保留原请求、实际 preparation（含 cut-tail）、阶段、原异常与有序 cleanup 异常；cleanup 不覆盖主故障。Journal 懒写 `RuntimeStarted` 属于初始化事实：初始化失败时业务事实仍为未尝试；后续 poison 拒绝保留首次失败的事件身份和原因。

## [007] StorageInvalid 全局 fail-closed

遇到格式损坏、非规范化 JSON、标识碰撞、retained writer 集合内部缺失父事件、成环依赖、载荷缺失/哈希失配或未知权威 `event_type` 时，拒绝以此快照构建投影或启动运行时，进入 fail-closed。durable-convergence-011 已整体淘汰的 writer 中的 parent 属于 retention 边界，不算缺失父事件。不跳过 retained writer 内的损坏事件继续折叠。

## [008] 并发 fork 不升级为全局 corruption

并发产生的合法分支（`DomainConflict`）是正常物理现象：保留全部竞争事实，不判为 `StorageInvalid`。

## [009] 无 schema/store generation，旧物理布局不读取

存储层不维护数据迁移代次。历史遗留的旧物理格式、私有文件与早期临时存储结构一律不读取（leave-unread）：不双读、不双写、不自动迁移。需要接纳旧事实时，唯一路径是离线一次性导入：经 EventStore 所有者的读取、payload 与 append 边界，在备份或副本上追加仅承载历史材料的导入事实（全新 `event_type`，不与旧类型同名）。导入事实只重建旧事实的因果位置、digest、payload 引用与覆盖范围，不得推导出可运行的授权、绑定或候选。未迁移的旧存储由入口检查拒绝并被新运行时消费，给出明确迁移指引；迁移不原地改写 append-only 历史、不清库、不覆盖用户配置，envelope 父边、payload refs 与因果锚点保留或正确重建。

## [010] 本机状态统一位于 .Herdchestra

主工作树的 `.Herdchestra/` 是唯一项目状态根；linked worktree 共用主工作树这份根；非 Git 工作区使用给定工作区下同名目录（`RuntimePath.stateRoot`）。通用事件写入 `.Herdchestra/wanxiangshu/events/`，Work 与 Ownership 等业务自有存储由各自 owner 管理 `.Herdchestra/` 之下的数据，诊断写入 `.Herdchestra/diagnostics/`。同一事实不能有两份权威。旧 `.git/wanxiang/` 与 `.git/wanxiangshu/` 完全不读、不写、不迁移，也不作为恢复来源。

## [011] 不提供运行数据的 Git 对象与远端同步能力

产品只承诺同机持久化。运行数据不写入 Git objects、refs 或远端分支；`fetch`、`push`、源码提交不是状态保存或恢复入口。本机分段、完整行追加与恢复验证继续保留。

## [012] PayloadRef 与本地 payload closure

大体积正文按内容哈希生成不透明 `PayloadRef`，内容内嵌于同一事件行的 `payloads` 字段，事件行自包含（ndjson 是唯一载体，不产生旁挂文件）。追加成功前，引用的所有 payload 必须已内嵌且哈希匹配；引用缺失或哈希失配构成 `StorageInvalid`。单行体积无固定上限，实际边界由存储性能与 GC 条款联合约束。

## [013] 查询只读规范 Integrator 的 Current，先 commit 后 integrate

业务查询不手动全量扫描、过滤或折叠事件历史，只读规范 Integrator 维护的 `Current` 积分状态。修改状态可以先计算待提交状态，但只有本地事实完整追加成功后才原子推进 `Current`；追加失败或提交闭包未执行时，事件、结构 head 与所有业务 Current 保持提交前状态。

## [014] k-way 输入顺序与确定性积分

每个 writer 的逻辑流（同 `WriterId` 段序列按 `seq` 拼接，见 [005]）内部按自然追加顺序排列；同 `WriterId` 的段必须先聚合为单条逻辑流才可作为 k-way merge 输入，不得把段当作独立 writer 流参与合并。先按 durable-convergence-011 的统一截止时刻过滤整条过期 writer，再用确定的 k-way merge 产生全局一致的规范序列。Integrator 按此顺序消费事件：相同截止时刻与相同 writer 集合在任何环境计算出完全一致的 `Current`。

## [015] business fold 不变量与失败拒绝

业务折叠规则严格校验关键事件不变量（不可重复捕获、游标单调递增、摘要与哈希匹配等）。任一不变量不满足时，当前事实被语义拒绝，并触发持久化重置，防止脏状态污染投影。

## [016] 存储物理概念不外泄至领域层

`GitTypes`/`StoreRef` 等物理存储概念仅限基础设施内部使用。业务领域层不直接引用或感知这些底层类型，只与 `EventEnvelope` 和 `PayloadRef` 交互。

## [017] local append 复杂度不依赖 history 与 Git

向本地追加事件时，Git 对象、树和引用操作数为 0。追加开销只与当前事件及关联 payload 的字节大小相关，不随历史事件总数或历史文件大小增长。

## [018] 启动和诊断不修改 Git 配置、不安装 hooks

插件加载、首次激活、事件追加和诊断都不改 `.git/config`、`.git/info/exclude`、`.git/hooks`，不安装 SSH wrapper、refspec 或后台同步。`.Herdchestra/.gitignore` 由应用在自身目录内创建并排除全部运行数据。源码 Git 操作与 submodule 管理由 Git 自身处理，不属于应用持久化。

## [019] 唯一 canonical Integrator，业务只注册 fold 规则

系统只有一个规范 Integrator 负责历史事实的解释与积分（boot 与 live 使用同一积分程序）。各业务模块只向 Integrator 注册单个信封的纯计算折叠规则，自身不拥有读取底层历史或重写重放循环的权限。删除任一注册（Structural、Journal、Casebook、JsTransaction）后，该领域的合法 live fact 不再产生对应的 Current 结果，其他领域不受影响。

## [020] plugin load 只验证物理可读性，业务激活延迟

插件加载阶段只验证存储文件物理可读性，不执行业务重放、崩溃修复或新事实写入。业务积分与水印追加延迟到首次实际消费持久化能力时触发；激活重放只枚举当前 retention 窗口内的 writer，过期文件不读取不解码。

## [021] semantic failure 写 durable cut-tail，当前进程退出

注册的业务折叠规则对合法信封返回语义错误时，系统持久化保留该错误事实并紧随写入 `ProjectionCutTail` 重置事件，隔离故障作用域。产生坏事实的当前进程立即退出，防止内存状态污染；重启后的下一代进程从重置后的 `Current` 接续。

## [022] EventStore contract 与 runtime 的编译闭包单向有界

EventStore 的契约项目（model、append-result、event-vocabulary、git-contract、storetypes、canonical codec 等 `Wanxiangshu.Owner.durable-events.*` 契约 fsproj）只拥有 `EventEnvelope`、`EventStreamId`、`PayloadRef`、append request/result/error 与 `IEventStore` capability 等稳定领域模型；物理实现项目（process log、store、journal writer、integrator engine 等）依赖这些契约，反向依赖禁止。业务 consumer 的传递编译闭包不包含 Git object/ref、process/file codec 或 Canonical Integrator。每个 locality 的编译闭包由声明的 ProjectReference 生成，闭包内 production `.fs` 数量有界。

## [023] canonical codec、领域 fold 与物理 store 分层

`CanonicalEventCodec` 的公开操作共同定义同一 canonical identity 协议，是 durable-events 拥有的 bounded pure contract；merge、integrator、surface 及 core runtime 按 locality 显式引用该 slice，merge runtime 不得为取得 codec 反向依赖 runtime。领域 constructor 只能产生 owner-owned fact case、intent、fold state 与 closed rejection；`AgentFact` outer union、跨 projection 组合及 journal append 只由 durable composition routing 拥有。`ProcessEventLog`、Store factory、file/lock lifecycle 是 effect，不进入 codec 或领域 fold contract。

## [024] semantic cut 先 settle 再经 capability 执行 fatal

semantic fold 失败先 durable 写入 bad fact 与 `ProjectionCutTail`，取得 committed 或 unknown settlement 结果，再构造 typed fatal incident。caller 只持有构造时必填的 fatal capability，不直接引用 `FatalProcess` 物理实现、optional/default fallback 或 service locator。同一 incident 只有一个 report owner 与一个 kill owner；重复调用与 fatal-before-settlement 一律拒绝。

## [025] persistence cut 的 fatal 决定在 composition 侧

semantic-cut 分支把 settlement 结果交给 composition 侧唯一 owner 决定；store 内部不设 optional fatal fallback。`JsToolsTransactionStore.appendPrepared/appendCommitted` 的 `FatalProcess.trip "js-transaction-semantic-cut"` 是该边界的 process-fatal 实现：cut 已由 Integrator 先行 durable settle（bad fact + `ProjectionCutTail` 同一次 append 落盘），trip 之前 caller 已持有 committed 或 unknown settlement 结果。

## [027] 退役生命周期词汇 fail-closed 拒绝

已退出权威词汇表的 9 类生命周期事件类型（`WorkOpened`、`DevOpsBound`、`IncumbencyOpened`、`ConfirmationCommitted`、`IncumbencyRetired`、`Reviewed`、`Reopened`、`AdvanceCommitted`、`RetirementBlocked`）不再可读。保留历史中残留任一此类 `event_type` 的 canonical 行在 boot/reload 校验时整体拒绝装载，报 `unknown durable event type during replay: <event_type>`，不跳过、不静默装载、不改写、不删除该行。移除该行后其余合法历史正常装载。这些类型不得重新注册回权威词汇表。

## [028] 状态根独立于 Git 元数据与旧数据

目录解析只查询 Git 的公开 worktree 拓扑，不打开旧运行数据。主工作树、其子目录及 linked worktree 定位同一 `.Herdchestra`；独立仓库、submodule 及非 Git 工作区保持隔离。给定 `.git` 内路径、bare 仓库或伪装的 `.Herdchestra` 必须拒绝。新根内的日志与业务存储正常恢复；旧 `.git` 数据即使存在或损坏，也不影响新根的读取。清理只涉及已确认的应用专属目录与配置，不清空源码 objects，不重写代码历史。
