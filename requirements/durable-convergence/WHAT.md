# durable-convergence — WHAT

本包定义本机事实流如何收敛为业务状态：合并规则、冲突形态、retention 窗口。事件追加与存储形态见 durable-events。

## [001] 活跃 writer 的合并是集合并集

同一 retention 截止时刻，活跃 writer 的合并是 append-only 事实的集合并集，按 `event_id` 幂等去重。writer 是同 `WriterId` 的全部段文件（见 durable-events-005）；retention 只按 UTC 日期分组整体移除过期 writer（连同全部段），不按时间、版本或到达顺序裁掉保留 writer 内的事实，也不按段部分移除。

## [002] 统一 KWayMerge

所有多流合并使用同一 `EventKWayMerge(writerStreams[])`，满足结合律、交换律、幂等性与确定性。同一组有序 writer 流不因枚举顺序、进程或机器而改变规范事件序列。因果顺序优先于 `EventId` 字典序，parent 先于 child。

## [003] 完整 writer 流与 retained union 等价

本地完整 writer 逻辑流按同一截止时刻过滤，再以 k-way merge 流式归并；结果等价于保留 writer 的全量集合并集。每个本地 writer 段文件是物理存储单元；同 `WriterId` 的段先按 `seq` 聚合为单条有序逻辑流才可作为 k-way 输入，不得把段当作独立 writer 流参与合并。归并以完整逻辑流为单位，不按事件 TTL 或额外增量协议处理；无段后缀的旧形态文件按其 `WriterId` 聚合为单条逻辑流。本机 `.Herdchestra` 是唯一状态根。

## [004] 合法并发是 DomainConflict

同一业务流从相同父事件合法分叉时，保留全部竞争 Heads，业务投影呈现确定的 `DomainConflict`，不判为 `StorageInvalid`。

## [005] 裁决覆盖全部 Heads

解决冲突的事件以全部竞争 Heads 为 `parents`；只有裁决事件及这些父事件均已折叠，业务投影才离开 `DomainConflict` 并收敛为唯一权威状态。部分覆盖时多个 Heads 继续并存。

## [006] 收敛只认事实因果，不认时间戳

保留 writer 内不按墙钟、revision 或到达顺序挑选赢家或丢弃事件——竞争事实一律保留，胜负由业务显式裁决事件表达。物理时间只用于 [011] 的整条 writer retention 分组。

## [007] 同一历史导出同一 Current

`Current(now) = CanonicalIntegrator(KWayMerge(Retain(now, localWriterStreams)))`，writer 流来自本机 `.Herdchestra` 的段序列。相同截止时刻与 writer 集合导出相同的 Structural frontier 和每个已注册业务的 Current；不直接合并投影状态。

## [009] 收敛只依赖本机文件与锁

本地收敛不依赖任何远端，不解释领域事件之外的内容；不提供 Git 对象库、远端快照、引用推进或 CAS 的运行时读写。跨进程文件锁只覆盖 writer 字节边界：读取本地 writer 流、过期 writer 删除、超限旧形态文件的段重组（durable-events-005）与物化缓存写入在同一互斥窗口内完成，锁外不读取本地 writer 文件字节。

## [010] 物化成本随变化量增长

未发生变化时，收敛在任何文件写之前短路返回；发生变化时只读取、验证变动的本地段文件。物理 fingerprint 缓存只节约工作，不具权威。

## [011] Writer 整体过期且不被旧状态复活

每个 writer 是一次进程输出流，物理上是同 `WriterId` 的段序列（见 durable-events-005）。保留单位是最近活动时间的 **UTC 日期分组**：今天与昨天保留，前天及更早整体过期（连同全部段文件）。`Retain(now, W) = {w ∈ W | utcDay(lastActivity(w)) >= utcDay(now) - 1}`。日期只作 GC 分组，不携带历史语义，允许 writer 因活动推进而移动到更晚的日期；同次收敛按统一截止时刻满足 `Retain(A ∪ B) = Retain(A) ∪ Retain(B)`；过期 writer 退出本地集合，缓存不跨下一次 expiry 命中，收敛不以拷贝时间或新 mtime 刷新活动性。

活动时间优先取 writer 逻辑流尾部（最高 `seq` 段末尾）Journal 的 `payload.ObservedAt`；连续 `ProjectionCutTail` 向前越过后再判定，可跨段回溯；非 Journal 尾部才回退到文件活动时间。保留 writer 指向窗口外 parent 时，视为已满足的因果边界；保留集合内部的依赖缺失或成环仍拒绝。
