# durable-convergence — WHAT

## DURABLE-CONVERGENCE-001: merge 等于 set union 且永不丢事实

副本之间的事件合并必须严格等价于 append-only 的集合并集与基于 `event_id` 的幂等去重。两个不同 `event_id` 的事件永远都必须完整进入合并后的历史（即便它们在业务领域上存在互斥关系）。存储层绝对禁止使用物理时间戳或版本号裁决所谓“赢家”而导致任何持久化事实丢失。

## DURABLE-CONVERGENCE-002: k-way merge 是统一 primitive

多流合并原语 `KWayMerge(writerStreams[])` 必须满足结合律、交换律、幂等性与确定性。同一组有序写者流无论以何种枚举顺序输入、由哪个进程或在哪台机器上执行，都必须产生完全相同的规范事件序列。

## DURABLE-CONVERGENCE-003: 生产 writer-stream k-way merge 等价于 union oracle

生产环境的合并算法必须与全量集合并集的理论规范等价。本地每个写者文件与远端每个写者 blob 均作为完整的有序输入流，通过 k-way merge 进行流式归并，禁止引入分段切片、临时索引树或复杂的增量对象协议。

## DURABLE-CONVERGENCE-004: 合法并发 fork 表达为 DomainConflict 而非 StorageInvalid

同一业务流基于相同父事件并发追加所产生的合法分叉，属于物理层并发的正常现象。底层必须完整保留全部竞争分支的头部（Heads），并在业务投影中显式表达为确定的 `DomainConflict` 冲突状态，严禁将其升级判定为底层的 `StorageInvalid` 致命损坏。

## DURABLE-CONVERGENCE-005: resolution event 以全部 heads 为 parents 才收敛

业务解决冲突的裁决事件必须显式将所有竞争的 Heads 全部声明为其父事件（`parents`）。只有当裁决事件及其包含的全部竞争父事件均已完成折叠时，业务投影方可离开 `DomainConflict` 状态并收敛为唯一的权威状态。

## DURABLE-CONVERGENCE-006: 禁止基于 wall-clock 或 revision 的 LWW

合并层严禁使用物理时钟、自增版本号或写者到达先后顺序来丢弃事件。时钟或固定决胜规则仅允许作为业务投影层生成当前特定只读视图的纯展示逻辑，绝不允许改变或删除底层的持久化事件。

## DURABLE-CONVERGENCE-007: 相同 merged history 导出同一个 Integrator Current

收敛公式严格定义为 `Current = CanonicalIntegrator(KWayMerge(writerStreams))`，严禁在投影层直接进行状态对象的模糊合并。相同的事件历史输入，经由唯一的规范 Integrator 计算后必须得到完全相同的 `Current`。

## DURABLE-CONVERGENCE-008: durability activation ensure hooks 且用户 Git 进程独立触发双向 sync

万象术不提供后台常驻同步器或自动上传服务。插件加载期不修改 Git 配置；仅在首次激活持久化能力时确保安装 `reference-transaction` 与 `pre-push` Hook。后续同步完全由用户自身的 Git 操作拉起独立 Hook 子进程执行，通过双向读取本地与远端写者流完成全量 k-way merge，直接原子替换本地写者集合并发布远端快照。

## DURABLE-CONVERGENCE-009: dumb remote 无 domain 逻辑

远程 Git 仓库严格作为通用、无感知的对象存储，仅提供标准的对象读写、引用推进与 CAS 门禁能力。严禁在服务端引入任何领域事件解释、服务端合并或自定义的万象术专有后端逻辑。

## DURABLE-CONVERGENCE-010: hook 热路径成本只随变化量增长

同步机制允许使用无权威属性的物理状态指纹缓存。当物理文件指纹与远端头部均未发生变动时，Hook 直接复用既有快照，避免重复全量读取与解压 blob；增量变化时仅针对变动文件进行读写与验证，保证同步开销与实际数据增量成比例。
