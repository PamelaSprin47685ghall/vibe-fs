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

万象术不提供后台常驻同步器或自动上传服务。插件加载期不修改 Git 配置；仅在首次激活持久化能力时确保安装 `reference-transaction` 与 `pre-push` Hook。后续同步完全由用户自身的 Git 操作拉起独立 Hook 子进程执行。若本地物理 fingerprint、retention expiry 与上次成功 materialization 均未变化，且本地 tracking ref 仍等于该 cached snapshot，则本次 `pre-push` 没有新的 Wanxiang truth 需要发布，必须零网络直接复用该 snapshot。任一 writer/payload 变化、TTL 跨界或已观察 tracking ref 变化时，才通过双向读取本地与远端写者流完成全量 k-way merge，原子替换本地写者集合并 CAS 发布远端快照。未被本机观察到的远端推进不会被 clean no-op `pre-push` 主动拉取，但也绝不会被覆盖；下一次本地 truth 变化或 tracking 更新时必须进入完整 convergence。

## DURABLE-CONVERGENCE-009: dumb remote 无 domain 逻辑

远程 Git 仓库严格作为通用、无感知的对象存储，仅提供标准的对象读写、引用推进与 CAS 门禁能力。严禁在服务端引入任何领域事件解释、服务端合并或自定义的万象术专有后端逻辑。

## DURABLE-CONVERGENCE-010: hook 热路径成本只随变化量增长

同步机制允许使用无权威属性的物理状态指纹缓存。当物理文件 fingerprint 未变、cache 未跨 retention expiry 且 tracking ref 等于 cached root 时，`pre-push` 必须在启动任何 Wanxiang Git transport 前直接复用既有快照；不得为 no-op 额外执行 `ls-remote`、`fetch` 或内部 `git push`。Hook 自动安装器还必须仅在当前仓库内为未自定义 SSH multiplex 的 `core.sshCommand` 保留原命令/identity 参数并追加短生命周期 `ControlMaster=auto` 复用，使用户 push 已建立的 SSH transport 可被 Wanxiang 的内部 CAS push 复用；若用户已显式配置 `ControlMaster` 或 `ControlPath`，安装器不得覆盖。安装器写入的永久 `core.sshCommand` 不得直接依赖一次安装时创建的易失 `/tmp` 子目录：Wanxiang 自有 SSH wrapper 必须在每次 SSH invocation 前重建并收紧 repo-scoped multiplex 目录，再执行保留的原 SSH 命令；安装器必须识别并迁移历史上由 Wanxiang 写入的长 repo-local socket path 与易失 tmp-directory socket path。增量变化时仅针对变动文件进行读写与验证，并通过现有 CAS convergence 处理远端竞争，保证同步开销与实际数据增量成比例。writer 的 remote-read 判定仍必须比较 `writer-manifest` activity；payload 没有 writer activity 语义，因此当 cache 中该 payload 的 stat identity 等于当前本地 stat identity、cached OID 等于 remote payload tree OID 且 entry 为 blob 时，必须判定为无需读取 remote blob。禁止把 writer manifest 的存在条件复用于 payload，否则一次无关 writer 变化会把全部历史 payload 重新读取，并在全局 store gate 内形成 O(total payload history) 临界区。

## DURABLE-CONVERGENCE-011: writer 以最后输出活动时间整体过期且不可被旧快照复活

每个 writer 对应一次进程输出流。协议使用固定 24 小时 TTL，并定义 `Retain(now, W) = { w ∈ W | lastActivity(w) >= now - TTL }`。`lastActivity` 优先由 writer durable bytes 自身推出：尾部 `JournalEnvelope.payload.ObservedAt` 是精确 producer activity；尾部若只有连续 `ProjectionCutTail`，反向越过这些 integrator metadata 后读取最近 Journal `ObservedAt`。只有尾部不是 Journal 事实时，才使用 producer-side file activity 作为物理 fallback。Git snapshot `writer-manifest v2` 将 `(writer blob OID, lastActivity)` 原子固化并跨副本传播；远端导入不得使用 fetch 时间或新文件 mtime 刷新活动性。完全缺少 manifest 或使用旧 mtime 语义 `v1` 的远端 snapshot 不具备 v2 可证明 activity，因此其 writer tree 必须直接忽略；一旦 snapshot 声明 v2 manifest，则 manifest 与 `writers/` 必须逐项一一绑定且 OID 完全相等，任何缺项、多项、重复、格式错误或 OID 不匹配均 fail-closed。同步必须在统一截止时刻应用 `Retain(A ∪ B) = Retain(A) ∪ Retain(B)`，过期 writer 从本地文件集合和新发布的远端 snapshot 同时消失。retained writer 中指向已退出 retention window 的 parent 被视为窗口外已满足因果边界；仅 retained 集合内部的缺失/成环依赖继续 fail-closed。snapshot/cache 命中不得跨越下一 writer expiry 时刻。
