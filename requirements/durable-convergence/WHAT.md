# durable-convergence — WHAT

## DURABLE-CONVERGENCE-001: 活跃 writer 集合内 merge 等于 set union

在同一 writer-retention 截止时刻下，副本之间仍处于活跃窗口内的 writer 必须严格按 append-only 集合并集与 `event_id` 幂等去重合并。retention 只允许整体移除最后活动时间早于固定 TTL 的 writer；禁止在一个被保留的 writer 内按时间戳、版本号或到达顺序裁掉单个事实。

## DURABLE-CONVERGENCE-002: k-way merge 是统一 primitive

多流合并原语 `KWayMerge(writerStreams[])` 必须满足结合律、交换律、幂等性与确定性。同一组有序写者流无论以何种枚举顺序输入、由哪个进程或在哪台机器上执行，都必须产生完全相同的规范事件序列。

## DURABLE-CONVERGENCE-003: 生产 writer-stream k-way merge 等价于 retained-union oracle

生产环境先对本地与远端完整 writer 流做统一 retention 过滤，再与保留 writer 集合的全量集合并集理论规范等价。本地每个写者文件与远端每个写者 blob 仍作为不可分段的完整有序输入流，通过 k-way merge 进行流式归并；禁止 writer 内部分段、按事件 TTL 或复杂的增量对象协议。

## DURABLE-CONVERGENCE-004: 合法并发 fork 表达为 DomainConflict 而非 StorageInvalid

同一业务流基于相同父事件并发追加所产生的合法分叉，属于物理层并发的正常现象。底层必须完整保留全部竞争分支的头部（Heads），并在业务投影中显式表达为确定的 `DomainConflict` 冲突状态，严禁将其升级判定为底层的 `StorageInvalid` 致命损坏。

## DURABLE-CONVERGENCE-005: resolution event 以全部 heads 为 parents 才收敛

业务解决冲突的裁决事件必须显式将所有竞争的 Heads 全部声明为其父事件（`parents`）。只有当裁决事件及其包含的全部竞争父事件均已完成折叠时，业务投影方可离开 `DomainConflict` 状态并收敛为唯一的权威状态。

## DURABLE-CONVERGENCE-006: 禁止用 wall-clock 或 revision 对保留事实做 LWW

合并层严禁使用物理时钟、自增版本号或写者到达先后顺序在一个被保留的 writer 内挑选“赢家”或丢弃事件。物理时间只允许用于 DURABLE-CONVERGENCE-011 定义的整条 writer retention；retention 之外不得改变保留 writer 的事件集合。

## DURABLE-CONVERGENCE-007: 相同 retained merged history 导出同一个 Integrator Current

收敛公式严格定义为 `Current(now) = CanonicalIntegrator(KWayMerge(Retain(now, writerStreams)))`，严禁在投影层直接进行状态对象的模糊合并。相同截止时刻与相同 writer 集合输入，经由唯一规范 retention 与 Integrator 后必须得到完全相同的 `Current`。

## DURABLE-CONVERGENCE-008: durability activation ensure hooks 且用户 Git 进程独立触发双向 sync

万象术不提供后台常驻同步器或自动上传服务。插件加载期不修改 Git 配置；仅在首次激活持久化能力时确保安装 `reference-transaction` 与 `pre-push` Hook。后续同步完全由用户自身的 Git 操作拉起独立 Hook 子进程执行，通过双向读取本地与远端写者流完成全量 k-way merge，直接原子替换本地写者集合并发布远端快照。

## DURABLE-CONVERGENCE-009: dumb remote 无 domain 逻辑

远程 Git 仓库严格作为通用、无感知的对象存储，仅提供标准的对象读写、引用推进与 CAS 门禁能力。严禁在服务端引入任何领域事件解释、服务端合并或自定义的万象术专有后端逻辑。

## DURABLE-CONVERGENCE-010: hook 热路径成本只随变化量增长

同步机制允许使用无权威属性的物理状态指纹缓存。当物理文件指纹与远端头部均未发生变动时，Hook 直接复用既有快照，避免重复全量读取与解压 blob；增量变化时仅针对变动文件进行读写与验证，保证同步开销与实际数据增量成比例。

## DURABLE-CONVERGENCE-011: writer 以最后输出活动时间整体过期且不可被旧快照复活

每个 writer 对应一次进程输出流。协议使用固定 24 小时 TTL，并定义 `Retain(now, W) = { w ∈ W | lastActivity(w) >= now - TTL }`。`lastActivity` 属于 writer blob 的物理派生元数据：origin append 更新本地文件活动时间；Git snapshot manifest 将 `(writer blob OID, lastActivity)` 原子固化；远端导入继承 manifest 时间而不得使用 fetch 时间刷新活动性。完全缺少 `writer-manifest` 的旧远端 snapshot 不具备可证明的 activity，因此其 writer tree 必须直接忽略，禁止以 fetch 时间猜测或续命；一旦 snapshot 声明 manifest，则 manifest 与 `writers/` 必须逐项一一绑定且 OID 完全相等，任何缺项、多项、重复、格式错误或 OID 不匹配均 fail-closed。同步必须在统一截止时刻应用 `Retain(A ∪ B) = Retain(A) ∪ Retain(B)`，过期 writer 从本地文件集合和新发布的远端 snapshot 同时消失。retained writer 中指向已退出 retention window 的 parent 被视为窗口外已满足因果边界；仅 retained 集合内部的缺失/成环依赖继续 fail-closed。snapshot/cache 命中不得跨越下一 writer expiry 时刻。
