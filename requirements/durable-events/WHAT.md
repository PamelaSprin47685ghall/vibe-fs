# durable-events — WHAT

## [001] Event 是 retained history 的唯一事实

动态业务状态只能由不可变事件表达；变更、撤销与退休均追加新事件，投影仅为衍生状态。在 durable-convergence-011 的 retention 窗口内，不得修改、单独删除或重新解释已提交事件；过期 writer 只能整流淘汰。不得先改投影后补盘。

## [002] 无版本信封与追加词汇

统一信封包含 `event_id`、`stream_id`、`event_type`、`parents`、`payload`、`payload_refs`。信封与存储层不得携带格式或 schema 版本。已发布事件类型的载荷结构永久冻结，新语义使用新事件类型。

## [003] Canonical identity

事件使用 UTF-8、无 BOM、单个结尾 LF 的 canonical JSON；对象键递归按 Unicode 代码点升序排列，parents 与 payload_refs 去重排序。同一 event_id 与相同字节幂等去重，与不同字节构成 Identity Collision，必须 fail-closed。

## [004] 完整行提交

本地提交以当前 writer 末尾完整的 canonical NDJSON 行为见证；成功返回前完成物理落盘并释放存储门禁锁。禁止半行、截断行、原地覆写，以及运行时追加中的 Git object/tree/ref/CAS 操作。

## [005] 进程独占 writer

每个进程实例使用全局唯一 WriterId，独占追加 `.git/wanxiang/events/<WriterId>.ndjson`，不得分段。进程退出后封存，新进程不得接管；过期后仅可整文件删除。业务流、机器与角色不得划分物理 writer。

## [006] 提交结果来自本地事实

提交成功以本地存在该 event_id 的完全一致 canonical 字节为准，不得从 Git ref、内存或退出码推断。物理追加发生后的异常为 CommitUnknown；进入追加门禁前因 writer 关闭或损坏而拒绝，必须明确为未尝试。

## [007] StorageInvalid

格式损坏、非 canonical 字节、标识碰撞、retained 集合内部缺失 parent、因果环、payload 缺失或哈希失配、未知权威事件类型，均须拒绝构建投影与启动运行时。不得跳过损坏继续折叠；按 durable-convergence-011 整体淘汰 writer 的 parent 属于 retention 边界，不算内部缺失。

## [008] DomainConflict

合法并发分支须保留全部竞争事实，不得将领域冲突升级为 StorageInvalid。

## [009] 旧布局不读取

存储层不维护迁移代次；旧物理格式与私有存储留存但不读取，不得运行时双读、双写或自动迁移。

## [010] 唯一持久化底座

所有动态业务事件及其大对象仅存于 `.git/wanxiang/events/*.ndjson` 与 `.git/wanxiang/payloads/*`，不得另设私有 journal 或数据库。

## [011] Git blob 仅用于远程同步

仅用户 Git 远程操作触发的 Hook 可将一个完整 writer 文件编码为恰好一个 Git blob。运行时主进程不得主动同步或在事件追加中操作 Git blob。

## [012] Payload closure

大对象按内容哈希生成不透明 PayloadRef，存于 `.git/wanxiang/payloads/<PayloadRef>`。引用它的事件提交前，所有 payload 必须已落盘且哈希匹配；缺失或失配为 StorageInvalid。

## [013] Commit 后发布 Current

业务查询只读唯一 Integrator 的 Current，不得自行扫描或折叠历史。可预计算待提交状态，但仅在完整本地提交后原子发布；追加失败或提交闭包未执行时，事件、结构 head 与全部业务 Current 保持提交前状态。

## [014] 确定性积分

保留 writer 内追加顺序，先按统一截止时刻淘汰过期整流，再按确定的 k-way merge 顺序积分。同一截止时刻与同一 writer 集合必须得到相同 Current。

## [015] 领域不变量

业务 fold 必须校验所属领域的事件不变量；违反时拒绝该事实的语义效果并触发持久化重置，不得污染投影。

## [016] 物理类型隔离

Git 对象、ref 与存储快照等物理类型不得进入业务领域；领域持久化交互只使用事件信封与 PayloadRef 等稳定契约。

## [017] 追加成本有界

本地追加成本仅随本次事件及 payload 字节量增长，不得随历史事件数或文件大小增长；Git 对象、树与引用操作数恒为零。

## [018] 远程同步与 retention

远程同步仅由用户 Git 操作触发的独立 Hook 执行。同步对本地 writer 与远端 blob 应用统一 retention 和 k-way merge 校验，删除过期本地整流，并全量替换不含过期 writer 的远端快照。主进程不得运行同步定时器或后台上传器。

## [019] 唯一 Integrator

业务仅向唯一 Integrator 注册逐信封的纯 fold，不得自行读取历史或重建重放循环。Structural、Journal、Strength、Casebook、JsTransaction 各注册须有实际 Current 行为证明：移除该注册后，合法 live fact 不再产生预期 Current；名称、token 与调用次数不足为证。

## [020] 延迟业务激活

插件加载只验证物理可读性，不得重放业务、修复崩溃或写入新事实。首次实际消费持久化能力才触发业务积分与水印追加；激活只读取 retention 窗口内的 writer，不读取或解码过期整流。

## [021] Semantic cut

合法信封被业务 fold 语义拒绝时，须保留坏事实并紧随持久化 ProjectionCutTail，重置故障作用域。产生坏事实的进程随后立即 fatal 退出；下一代进程从重置后的 Current 接续。

## [022] 单向且有界的编译闭包

稳定事件模型、读写契约与事件词汇须独立于物理存储、Integrator、业务运行时、Host 与测试实现，后者只能单向依赖契约。业务消费者不得传递引入 Git、文件编解码、Integrator、Strength 预测/副本运行时或 Host 实现。Git 同步的物理 port 独立，不得从业务读写契约泄漏。

契约的传递生产 `.fs` 数不超过 100，focused EventStore runtime 不超过 185。局部编译须将 ProjectReference 闭包展开为零 ProjectReference 的单工程，由一次 Fable 调用完成。

## [023] 协议、领域与物理分层

Canonical 编码、解码、UTF-8 解码、身份校验与合并共同遵守 [003]，作为本模块拥有的完整纯契约供各消费者显式依赖，不得为取得 codec 反向引入存储运行时。领域仅产生自己的事实、意图、fold 状态与封闭拒绝结果；外层事实汇总、跨投影组合及日志追加由持久化组合层拥有。文件、锁和 store 生命周期不得进入纯契约。

## [024] 先结算再 fatal

Semantic cut 须先取得坏事实与 ProjectionCutTail 的 committed 或 unknown 结算证据，再构造 typed fatal incident。Fatal 能力必须在构造时注入，不得直接引用物理实现、使用可选默认、全局绑定或服务定位器。同一 incident 仅一个报告者与终止者；拒绝未结算先 fatal 及重复执行。

## [025] 持久化拒绝与 fatal 所有权

持久化操作返回 typed 结果，由组合层唯一决定 fatal；不得保留可选的全局 fatal hook。Prepared cut 前不得发生事务文件副作用；Committed cut 后按 committed 或 unknown 证据恢复，unknown 不得当作未写入。
