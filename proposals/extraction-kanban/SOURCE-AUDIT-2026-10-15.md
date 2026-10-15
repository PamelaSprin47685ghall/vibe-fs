# 四模块源码复核：问题报告与修复计划

日期：2026-10-15。状态和认领以 [BOARD.md](BOARD.md#source-audit-repairs) 为准。本报告记录复核结果、修复顺序和验收条件；本轮没有修改生产实现或测试，也没有关闭 GAP。

后续已开始实施，实际变更与验收见 [修复进度记录](REPAIR-PROGRESS-2026-10-15.md)。下文保留源码复核时的基线与结论，不用后续进度覆盖原始发现。

<a id="second-review-fixes"></a>
## 0. 修复后再次检查：合理性与新增问题

基线：主仓 `5dd9dec9c`，库 `3ad5b88 / 0.6.0`。本节来自后续复核与本轮实现，不属于下文首次调查的“未修改代码”范围。状态见看板 CUT-01-R2 / CUT-04-R2，修复及实际验收见进度记录的同名段落。

| 模块 | 判断 | 后续取舍 |
|---|---|---|
| Work / Journal / DurableWork | 领域、存储与提交顺序分开是合理边界；无需重写状态机 | 日志须自己管理原生 statement，不能把连接寿命留给 GC；本轮修复 J-01 |
| Process / Supervision | 基础有界进程对象与更强的监管组合各有明确承诺，完整字节由主仓写 Spool | 保持两种契约，不合并成万能进程框架；本轮复跑完整进程需求 |
| Trace | 独立库的追加记录、字节与稳定 cut 成立；主仓仍主要使用派生描述符桥 | T-01 仍为优先后续包：先冻结真实材料协议，再切捕获、恢复和消费者，最后删除桥；本轮不把再次捕获当作接管 |
| Ownership / Host | lease 与 blocker 由库维护，Host 保存物理绑定和待确认退出，这种分工合理 | Host 必须完成“本实例物理退出→同一事实持久确认”的合作；本轮修复 O-05/O-06/O-07，跨重启物理接管仍待实现 |

新增反例及修改点：

| ID / 优先级 | 已证实的反例 | 修复边界与验收条件 |
|---|---|---|
| J-01 / P1 | Bun 1.3.14 在 COMMIT 入口抛错后，Journal 返回 Unknown，但未 finalize 的 prepare statement 令旧连接继续持写锁；重开后的追加为 NotWritten，直到 GC | 每次 statement 操作在 finally 中释放；Node 由 connection.close 释放其 statement。仍返回 Unknown、封存实例，不重发外部效果。Node/Bun 源码和安装包都须在保留原生引用、不调用 GC 的条件下重开并立即写入 |
| O-05 / P1 | 真实 PTY 在 detach 内退出，Ended 提交前/后丢回执；旧 detach 只等待端口 CloseAll，返回成功时本地 lease 仍未确认，提交前丢失还保留 durable blocker | 单项关闭后核对已缓存的确切退出，以原 request/lease/incarnation 确认；仍 Unknown 时 detach 必须失败并保留义务；不重复发布进程退出 |
| O-06 / P2 | 两个运行时共享端口时，空运行时 detach 也会经端口 CloseAll 终止另一个实例的实际进程；父取消有同一路径 | 只按本实例 lease 发物理关闭，并核对其独立 scope；业务 CloseOwnedPtys 的根范围 blocker 检查保留。真实 Bun 同端口、同 durable root 的 detach/cancel 均须保全另一实例 |
| O-07 / P1 | Drain 在 COMMIT 前丢回执，detach 已失败，同一个运行时却仍可 fork 新 PTY | 永久 detach 开始时关闭本地准入，不能依赖 durable Drain 一定提交；失败后保持关闭，新运行时可按新 scope 正常启动；不扩大为跨进程每轮准入已证明 |
| B-01 / P2 | 只改 Host/Runtime.fs 的聚焦构建缺少 Mission/Lifecycle，原项目未声明 DurableWorkHost 所在引用；全量构建可通过 | 补直接 ProjectReference，不修改构建器或放宽门禁；全量与随后聚焦构建均须通过 |

驱动依据也作了核对：Node 22.13 的 [DatabaseSync 原生实现](https://github.com/nodejs/node/blob/v22.13.0/src/node_sqlite.cc) 在关闭时 finalize statement；Bun 的 [Statement.finalize](https://bun.com/reference/bun/sqlite/Statement/finalize) 释放原生资源。Bun 当前 close 文档不能替代 1.3.14 的实际行为；本轮使用明确的 statement.finalize，不依赖新版 close(true) 或补调 GC。

## 1. 结论与本次纠正

**独立库的基础边界大体成立，但 CUT-02、CUT-03、CUT-04 的主仓接线没有兑现全部原定职责。** 此前发现的大输出缺失、Draining 后取消失效、Opening 截断和固定 cut 失效、PTY 提前清账与同步完成丢失，均得到完整源码支持。不能据此笼统断言四个库都实现错误。

| 模块 | 完整源码带来的结论 | 当前不能宣称的结果 |
|---|---|---|
| Lifecycle：Work / Journal / DurableWork | 状态机、日志与组合分开，主仓已实际消费；此次未推翻原 CUT-01 的迁移边界 | 全部 Host / release 义务闭合；本次独立库并发测试还暴露了一次 Journal 初始化锁冲突，见 §4 |
| Process | 库明确提供有界留尾，且直接子进程退出后拒绝再用旧 PGID 发信号；主仓把它用于完整外存和强取消，能力不匹配 | CUT-02 的完整输出和进程组收束已经承接 |
| Trace | 库保留传入字节、拒绝超过 512 KiB 的字节部件及晚 Opening；主仓截断字节、重建同名轨迹，并主要传入描述符 | 原件与稳定物理身份已迁入新库；主仓合法追加及恢复不改变已发布 cut 指向的前缀 |
| Ownership | 基础库有意只管内存状态；真实效果与持久提交由组合层负责，库已支持 Ended 早于 Started | 主仓生产 PTY 已接通持久预留、重启对账和真实退出结算 |

对上轮措辞作以下修正：

- 撤回“Trace 有两个相互竞争的持久 owner”的强表述。现状是**旧 AgentJournal / Projection 仍为权威，新 Trace 是派生描述符索引**。抽取未完成，但不能把派生索引自动算成第二个持久写入者。
- 不把 Process 的有损尾部、Draining 拒绝信号、Ownership 的内存模型归为库内缺陷。它们都有公开规则和独立测试；缺陷在主仓没有补足更强的产品承诺。
- 同步 PTY 完成反例证明的是生产对象与端口契约之间的竞态，尚未证明真实 bun-pty 必然同步触发。blocker 查询吞错也不等于“所有 SQLite 错误都被吞”。
- Trace 的 getter 被拒绝不能证明无用户代码可达。Proxy 的 `getOwnPropertyDescriptor` trap 可触发重入；库现有测试已证明守卫有效，需要纠正主仓的验收解释。

## 2. 基线与证据范围

| 对象 | 精确基线 |
|---|---|
| 主仓 | `master@7d8feff030adfdeae511bdc0d8bb77f3c00b2a1f`；调查开始时无未提交改动 |
| 独立库 | `wanxiangli main@46d9c072af50389d069709e847d5772af3ec160a`，版本 `0.5.2`；调查开始时无未提交改动 |
| 主仓消费包 | `vendor/wanxiangli-0.5.2.tgz`；SHA-256 `53f21944e86a314035d82e40e395aa785730e95110a95fb74177791460422656` |
| 本机环境 | macOS / Darwin，Node `v26.6.0`、npm `11.18.0`、Bun `1.3.14`；没有把本机证据外推为 Linux 或真实 OpenCode Host 验收 |

独立库放在主仓的同级目录，不加入主仓源码依赖。本次用锁定依赖执行 `node scripts/build.mjs`，六个入口均构建成功。重建的六个 JS bundle 与 tarball **不是逐字节相同**；用 Acorn 解析、去掉位置与 literal raw 字段后，六个 AST 均相同。六份 `.d.ts` 及 `PROCESS.md`、`TRACE.md`、`OWNERSHIP.md`、`MIGRATION.md` 逐字节相同。这支持源码与当前包的实现对应关系，不等于可复现二进制构建证明。

上轮已经使用主仓真实锁定包和编译产物验证接线行为，因此并非只凭目录或接口名称猜测。本次新增的是库 F# 源码、独立规则、测试和组合证明；它们主要改变缺陷归属和修复方法。下列主仓文件链接对应上述基线，行号用于定位，后续修改可能移动。

## 3. 已确认的问题

P1 表示会丢材料、遗漏真实执行或破坏主要职责，应优先修；P2 表示较窄的功能或验收缺陷。优先级不是事故发生频率的测量。

### P-01 · P1 · 用有损尾部采集完整输出

**位置：** [NodeProcessHost.fs](../../src/Wanxiangshu/Process/NodeProcessHost.fs) 154、169–176、203 行。**依据：** [process-execution WHAT](../../requirements/process-execution/WHAT.md) [009][010][014] 与 [CUT-02](CARDS.md#cut-02)。

主仓设 16 MiB 尾部容量，每 20 ms 读取一次，却忽略返回的 `discarded`，然后推进游标。高速输出超过读取速度时，旧字节在进入 Spool 前已经被覆盖。上轮三次真实命令各输出 134,217,728 字节，桥接收集到 39,124,992、36,110,336、75,169,792 字节，命令退出码均为 0。数值依调度变化，但丢失原因确定。

库 [OutputTail.fs](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/46d9c072af50389d069709e847d5772af3ec160a/src/process/OutputTail.fs#L35) 正确执行有界留尾；`PROCESS.md` 明确要求调用方处理缺口。公开 API 没有另一个可供主仓接完整输出的流、背压或文件接口。修复须补真实无损输出通道；只扩大 ring、缩短轮询或显示缺口，均不能完成完整外存承诺。

### P-02 · P1 · Draining 阶段取消不再终止残留进程组

**位置：** [NodeProcessHost.fs](../../src/Wanxiangshu/Process/NodeProcessHost.fs) 219–220 行。**依据：** process-execution [003][004][006] 与 CUT-02 明列的“父退出但子仍占输出管道”反例。

父进程退出、同组子进程仍持 stdout 时，库进入 Draining。主仓调用 `signal('SIGKILL')` 并丢弃 `NotRunning`；上轮真实反例中，子进程继续存活，管道不关闭，显式清理后才收尾。等待方仍可有限返回 Timeout，不能写成“所有等待必然无限挂起”。

库 [004.test.mjs](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/46d9c072af50389d069709e847d5772af3ec160a/src/process/tests/004.test.mjs#L89) 明确验证 Draining 时返回 NotRunning；公开契约说明，这项限制用于避免直接子进程退出后继续使用可能过期的 PGID。正确修复需要真实监管能力，保持可安全终止的确切实例；不能简单放宽 phase 判断或在 C 中拿旧 PID 盲杀，也不能承诺能收回任意逃逸后代。

### P-03 · P2 · 相对 PATH 按宿主 cwd 查找

**位置：** [NodeProcessHost.fs](../../src/Wanxiangshu/Process/NodeProcessHost.fs) 125–129 行。

非空相对 PATH 项直接参与 `pathJoin`，存在性检查落在宿主 cwd；命令指定的 cwd 没有参与解析。上轮 `PATH='.'`、命令 cwd 指向 Node 所在目录，原生 spawn 成功，新 resolver 返回不存在的候选。库要求绝对 executable 是正确边界。主仓应按命令的绝对 cwd 解析相对项及空项，并保留 PATH 顺序。

### T-01 · P1 · 原件职责仍在旧链，新 Trace 主要承接描述符

**位置：** [Model.fs](../../src/Wanxiangshu/Context/Trace/Model.fs) 318–325、585 行；[Capture.fs](../../src/Wanxiangshu/Context/Trace/Capture.fs) 386 行附近；[Projection.fs](../../src/Wanxiangshu/Context/Trace/Projection.fs) 20–37、335 行；[Materialization.fs](../../src/Wanxiangshu/Context/Trace/Materialization.fs) 158–165 行。**依据：** [CUT-03](CARDS.md#cut-03) 与 semantic-trace [001][005][007][008]。

新库捕获的主要是 `role|provenance|kind|toolName|textRef|textDigest`，并按旧游标合成 `xrun` / `xmsg` 及完成状态。真正捕获、分配游标、写 blob、提交事实、恢复和正文解析仍依赖旧链。描述符作为字节输入本身合法，问题是它不能证明真实 message / part / run 身份和原件职责已由新库承接。临时 Trace 中再次装入正文，也不能替代生产原件权威迁移。

这属于完成范围不实，而非仅凭“有桥接代码”判错。若长期保留旧原件与新索引，应明确裁定另一职责范围和实际收益；在现有计划下，应先完成 D-02 对账，再接真实捕获和恢复，最后删除已迁移的旧判断。

### T-02 · P1 · Opening 超过 512 KiB 被静默截断

**位置：** [Model.fs](../../src/Wanxiangshu/Context/Trace/Model.fs) 139–149 行。**依据：** semantic-trace [010] 与 CUT-03 原件容量要求。

`boundedOpeningBytes` 主动丢弃超出 524,288 字节的尾部。上轮经主仓 SemanticTraceSurface 输入两个长 614,401 字符、仅尾字不同的 Opening：旧 `openingEvidence` 保留完整文本，新 Trace 的 Opening 均仅 524,288 字节、head 相同。不能据此说旧持久原件已经被删；已证实的是新 Trace 中材料不完整，不能承接替换。

库 [Wire.fs](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/46d9c072af50389d069709e847d5772af3ec160a/src/trace/Wire.fs#L110) 会拒绝超过 512 KiB 的字节部件，不会截断；编码记录另限 1 MiB。仅删除 `slice` 会把静默丢失变成捕获失败。修复须定义完整 Opening 材料的分片与稳定范围，分片全部确认后才能发布完整证据。

### T-03 · P1 · 晚 Opening 重建同名轨迹，使已发布 cut 失效

**位置：** [Model.fs](../../src/Wanxiangshu/Context/Trace/Model.fs) 230–244 行；[Projection.fs](../../src/Wanxiangshu/Context/Trace/Projection.fs) 275–297 行。**依据：** semantic-trace [001][006][009][010]。

已有 Part 时，主仓创建相同 traceId 的空 Trace，将 Opening 插入首条，再重放 Parts。上轮真实 Surface 中旧 head cursor 为 5，补 Opening 后为 6；恢复新 records，再用旧 cut 读取，得到 `Cut does not match this trace prefix`。库正确发现前缀已经不同。

库明确只允许首条 Opening，且有原子拒绝测试；主仓既有语义又允许晚补，历史 journal 也可能包含此顺序。修复必须同时处理两者：新轨迹在发布 cut 前安排首条 Opening；确需事后确认材料时，用只追加的范围绑定协议。不能无审计地拒绝全部旧输入，也不能在同一 traceId 下移动已有前缀。必要迁移使用新身份并保留原件。

### O-01 · P1 · 生产 PTY 没有接通同一份持久预留与 blocker 来源

**位置：** [Host/Runtime.fs](../../src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs) 45–46、526–532、1266–1277 行；[ToolRuntimeScope.fs](../../src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs) 116–120、1143–1154 行；[DurableWorkHost.fs](../../src/Wanxiangshu/Mission/Lifecycle/DurableWorkHost.fs) 555–558、691 行附近。**依据：** [CUT-04](CARDS.md#cut-04)。

实际 registry 以空 records 创建，预留走内存；`executeOwnershipDurable` 只有测试 Surface 调用。退休查询使用 sessionId registry，与 PTY 的 `fork-pty:<sessionId>` 不一致。ToolRuntimeScope 使用的通用内存 helper 还将 `Applied` 包装为 `Committed`；PTY 路径本身直接 execute，没有持久提交许可。上轮直接按包协议重建得到：活动 blocker 为 1，空账重建并 OpenScope 后为 0，按 records 恢复则仍为 1。

库 [OWNERSHIP.md](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/46d9c072af50389d069709e847d5772af3ec160a/OWNERSHIP.md#L90) 已将持久化交给 C，并明确首次 Committed 才允许启动。应接通所选外部 PTY 资源族的持久组合，统一 registry / scope / stream；纯本进程 callback 若只承诺本进程排空，可继续用内存账，不必全部改成持久资源。

### O-02 · P1 · Untrack 没有退出证据就写 Ended

**位置：** [Host/Runtime.fs](../../src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs) 1191 行；实际调用方 [PtyTool.fs](../../src/Wanxiangshu/OpenCode/Tools/PtyTool.fs) 151–155 行。

`UntrackPtyRun` 直接以 Cancelled 结算并删除 lease。名称绑定失败可走这条路径；两个同名请求可能先都查到空闲，再异步启动，后者绑定失败。它没有先终止并等待该实例。

本轮重跑生产 HostForkRuntime 加受控 PtyPort：Untrack 后 blocker 为空，port 仍判定实例活动；再调用 CloseOwnedPtys 也未发送关闭命令（只记录到 Spawn）。这是端口级反例，未启动 OS 进程。修复需走真实关闭与退出确认，不能把“取消/解除名称绑定”写成物理终态。

### O-03 · P2 · Spawn 内同步完成被丢弃

**位置：** [Host/Runtime.fs](../../src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs) 614–623、1251–1255 行。

先调用 Fork，后写入 lease 索引；完成监听器只认已登记实例。本轮受控 backend 在 Spawn 内 Complete：port 已无活动实例，但 blocker 残留、completion 数为 0。真实 bun-pty 的同步触发概率未测。

库 [Ownership.fs](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/46d9c072af50389d069709e847d5772af3ec160a/src/session/Ownership.fs#L151) 已支持 Reserved 直接接 Ended，晚到 Started 不复活它。主仓应在外部调用前准备关联，并避免返回后重新插入已结算实例；不需要为此重写基础状态机。

### O-04 · P1 · blocker 查询错误被解释为空

**位置：** [DurableWorkHost.fs](../../src/Wanxiangshu/Mission/Lifecycle/DurableWorkHost.fs) 576–580 行；[Host/Runtime.fs](../../src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs) 133–138 行。

`blockers(scope)` 的异常被 `catch → []` 吞掉；库对缺 scope 明确抛错。不存在 scope、registry 接错或查询失败不能证明没有资源。这与 O-01 的身份错接相互掩盖。SQLite 读取和 records 构造在这层 catch 外，本项不声称所有存储损坏都会变成空列表。应显式传播未知/错误，只有已知存在且无 blocker 的 scope 才返回空。

### E-01 / E-02 · P2 · 两处验收证据需要纠正

- **Trace 重入：** [005.test.mjs](../../requirements/semantic-trace/tests/005.test.mjs) 161–172 行只证明 accessor 被拒。[库 006.test.mjs](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/46d9c072af50389d069709e847d5772af3ec160a/src/trace/tests/006.test.mjs#L58) 已用 Proxy trap 证明重入读写会被拒，本轮运行也通过。保留 accessor 测试，另补可达重入反例，纠正“busy 时无用户代码可达”的解释；库守卫无需修复。
- **PTY 真实终止：** [CUT-04 历史记录](cards/CUT-04.md) 称超时升级全部通过，但 [025.test.mjs](../../requirements/managed-session-lifecycle/tests/025.test.mjs) 102 行仍将真实 TERM / KILL / exit 标为 GAP-133 TODO。受控端口和直接 Untrack 不是物理退出证据；库的独立进程组合测试也不能替主仓 PTY 接线作证。本轮在原卡追加更正，保留原日志。

## 4. 实际验证与尚未证明的范围

| 范围 | 命令/结果 | 边界 |
|---|---|---|
| 主仓构建（上轮同一基线） | `node scripts/build.mjs`，exit 0，generation 463 | 本轮只改文档，没有以旧 manifest 宣称新文档提交通过全量 verify |
| 主仓 work（上轮） | `env TMPDIR=/private/tmp node --test requirements/work/tests/*.test.mjs`；153 tests，152 pass，0 fail，1 TODO | 初次默认临时目录有两项 `/var` 与 `/private/var` 路径差异；使用规范临时路径后通过 |
| 主仓 process-execution（上轮） | `node --test requirements/process-execution/tests/*.test.mjs`；186 tests，181 pass，0 fail，5 TODO | 既有用例未覆盖上述大输出及 Draining 接线反例 |
| 主仓 Trace 与相邻接线（上轮） | semantic-trace 全部；work-record 002/005/008/014；prefix-stability 009/012/014；managed-session-lifecycle 009/025；118 tests，107 pass，0 fail，1 skip，10 TODO | TODO / skip 不计为证明通过，GAP-133 未关闭 |
| 主仓静态检查（上轮） | `npm run check`，exit 1，168 项 clean-break-symbols 命中 | 命中旧的、被忽略的 `.fable-build/output-compile` 生成 fsproj 中的 `WorkspaceSnapshot`；报告保留失败，不把它改写为全绿 |
| 独立库源码构建（本轮） | `node scripts/build.mjs`，exit 0 | 六模块均经 Fable 编译，未运行 `dotnet build` |
| 独立库完整原有测试（本轮） | 下方命令，exit 1；274 tests，270 pass，3 fail，1 skip，0 TODO / cancelled | 三个失败必须单独解释，不能用通过的包测试覆盖 |
| 主仓受控 PTY 反例（本轮） | `/private/tmp/cut04-audit.mjs` 重跑 | O-02、O-03 均复现；属于真实生产对象加受控端口，无 OS 进程 |

独立库实际测试命令：

```sh
node --test src/lifecycle/tests/*.test.mjs src/eventstore/tests/*.test.mjs src/assembly/tests/*.test.mjs src/process/tests/*.test.mjs src/trace/tests/*.test.mjs src/session/tests/*.test.mjs scripts/tests/*.test.mjs
```

三个失败经针对性调查后，归因如下；复跑不覆盖首次失败记录：

- **Journal 初始化竞争：** 两进程竞争的“恰好一个 Committed、另一个 NotWritten/Conflict”断言已通过，随后校验连接在 `SqliteJournal.fs:65` 的 PRAGMA 初始化阶段遇到 `database is locked`。worker 先发结果，在发送回调中才关闭连接；父进程未等 worker 退出便重开。定向复跑两次均通过。已确认失败阶段和缺少关闭同步，未直接观测 SQLite 内部哪一步持锁；不能写成 CAS 失效或确定的 checkpoint 故障。
- **Process 平台测试假设：** `/var` 与 `/private/var` 的差异、Node 子程序额外的 `__CF_USER_TEXT_ENCODING` 均可由原生 spawn 重现。拦截库的 spawn 参数确认 cwd/env 与请求一致；同样环境启动 `/usr/bin/env` 没有额外键，启动本机 Homebrew Node 才出现该键。规范临时目录下定向复跑仍为 0 pass / 2 fail：路径差异消失，两例都在 env 断言失败。此项是 macOS/Homebrew Node 下的测试可移植性缺口，不是新增的库参数转发缺陷。

跳过的是仅在 Linux 运行的 process 安装包拆除测试。Lifecycle、Trace、Ownership 的包安装与进程死亡组合测试在本机 Node / Bun 上通过；Ownership 恢复夹具使用可持久查询的退出见证，不能推导当前主仓 PTY 已拥有同样能力。

原始日志暂存于本机 `/private/tmp/`：`vibe-fs-review-build.log`、`vibe-fs-review-work-canonical-tests.log`、`vibe-fs-review-process-tests.log`、`vibe-fs-review-trace-ownership-tests.log`、`vibe-fs-review-check.log`、`wanxiangli-source-audit-build.log`、`wanxiangli-source-audit-tests.log`、`cut04-source-audit-results.log`。它们未提交，可能被临时目录清理；本报告保存关键结果，正式修复必须将一次性反例转为可复跑的编号测试，不能将临时脚本当作已交付门禁。

本轮文档经三路只读复审。链接检查还发现原 `cards/CUT-00.md` 缺失；看板保留历史登记并标明缺件，不补造验收记录。

## 5. 修复计划

四条关联修复卡在看板登记为 BACKLOG，尚未分配实现 owner。以下是施工顺序和验收条件，不代表接口已冻结或修复已完成。P1 行为修复先于下游继续宣称对应抽取完成；既有 CUT 的历史提交与证据保留。

<a id="repair-lifecycle"></a>
### CUT-01-R1：补齐 Journal 关闭同步及库平台证明

先修库 eventstore-001 的测试同步：在发送竞争启动消息前注册两个 worker 的退出等待，收齐回执并确认正常退出后再重开校验连接。保留双方回执及唯一提交断言；另测构造期锁竞争显式失败且连接关闭。库当前约定 `busy_timeout=0`，不以 sleep、隐式重试或提高 timeout 掩盖竞争；产品若需等待，应另行明确契约。

process-001 使用规范目录或比较物理目录身份；分别验证传给 spawn 的精确环境，以及真实子程序与同输入原生 spawn 的对照。保留宿主哨兵变量不得泄漏和特殊键语义，不无条件忽略额外键或跳过 macOS。此卡修验证缺口，不修改已通过的 CAS 规则或重做 Work；候选修改面主要是库 `src/eventstore/tests`、`src/process/tests`。验收覆盖对应编号套件、composition 和安装包，平台限制如实列出。

<a id="repair-process"></a>
### CUT-02-R1：补足输出与物理监管，再接回单次运行

1. **固定 D-03。** 明确尾部查询、完整输出、取消返回、真实退出四项不同承诺。选定无损通道和监管实例方案；列明平台与进程组边界。库继续独立，不注入 Work / Journal / Spool 服务。
2. **先补反例。** 双路输出超过留尾容量、慢消费者和落盘失败；父退出而同组子持管道后的期限/取消；目标 cwd 与宿主不同的相对 PATH。
3. **落实最小能力。** 可考虑有界单消费者字节通道及背压，由 C 写 Spool 并确认消费；保持原 tail 的公开语义。监管方案必须提供命令退出后仍可靠的物理身份，先证明可行再冻结 API。路径修复可独立小提交，仅改主仓。
4. **接线和清旧。** 删除“轮询 tail 冒充完整输出”；传播通道/落盘失败；取消结果与退出结算分开。若新监管能力暂不能完成，明确保留或恢复单一可用旧 owner，收回对应完成范围，不能双启动或继续记作已迁完。
5. **验收。** 逐通道核对完整字节数与摘要、内存积压上限、EOF 和失败；屏障确认父已退出后再取消，观察同组子实际退出和管道关闭；保留库拒绝旧 PGID 的正控。PATH 覆盖 `.`, `./node_modules/.bin`、空项、绝对项和顺序。不要断言 stdout/stderr 存在 OS 未保证的全局顺序。

候选修改面：库 `src/process`；主仓 `Process/NodeProcessHost`、`NodeProcessWait`、`ProcessRunner`、`Spool` 及相关接口/Surface。按 process-execution [003][004][006][009][010][014] 等原条款补测试；PATH 映射执行参数条款。库协议变更先完成，主仓消费其精确新包后再清旧路径。

<a id="repair-trace"></a>
### CUT-03-R1：确定原件与 Opening 协议，保持已发布范围

1. **补齐 D-02。** 对推理部件、retry 排除、完整 Opening、并发捕获、稳定身份和 Fission 逐项写承接/明确留旧/后续卡；丢失的旧卡前文不能靠猜补回。选定一条真实 capture 与恢复链，写明原件权威和旧数据处置。
2. **先补失败用例。** 大 Opening 等长异尾；保存 cut 后晚补 Opening、重启再读；真实身份冲突；Proxy trap 重入。大材料覆盖分片边界、UTF-8、多字节、BOM/NUL 和缺片，不能只用 ASCII 小串。
3. **先定公共协议，再改调用方。** 完整 Opening 由稳定分片/范围承接；如需晚确认，增加只追加绑定，保留 first-only Opening 语义。同身份历史前缀不重写。历史转换需新 trace identity、原件保留和可验证范围，不能临时换编号掩盖冲突。
4. **接真实材料和持久恢复。** C 提供真实 run/message/part 身份与已确认字节；明确 blob 是否仅为存储载体。新旧只读比较可用，生产只能有一个权威写入口。删除已经迁移的旧校验、描述符伪原件和临时原文重捕获；未迁范围明确保留。
5. **验收。** semantic-trace [001][005][006][007][008][009][010] 及 work-record、prefix-stability 相邻用例；用真实 journal 重启验证原字节、可信 cut 和下游取材。迁移前已发布范围仍有明确、无歧义的读取路径；不支持的请求原子拒绝且不改 head / records。

候选修改面：库 `src/trace`（仅必要协议扩展）；主仓 `Context/Trace`、真实 capture/fold、WorkRecord/Prefix 的相邻取材入口。原始日志格式和在途数据的处置先评审，再删除旧路径。

<a id="repair-ownership"></a>
### CUT-04-R1：先修真实终结，再接持久预留与恢复

1. **局部物理边界先闭环。** 名称绑定失败请求关闭并等真实退出，必要时 TERM→KILL；未观察结束不得移除 blocker。外部 Spawn 前保存已预留 lease 与本次启动请求的待确认关联，不预先声明 Started，允许同步真实 Ended；返回后的 Started 不能复活已结束实例。移除可任意清账的 Untrack 出口。
2. **冻结生产资源族的组合契约。** 统一 registry / scope / stream；OpenScope、Reserve 持久提交，只有首次 Committed 才启动；Reserve 与 Drain 共享 revision CAS。Ended 提交确认前保留结算义务，Unknown 不重跑。取消把 Applied 包装成 Committed 的语义混用，缺 scope 或查询错误显式失败。
3. **接真实恢复证据。** 现有 PtyId 和内存退出观察不足以支持跨进程恢复。物理 owner 须提供确切 incarnation 与可复核终态；没有证据时恢复为未结算 blocker，不能猜 NotStarted。已有实例停止新准入后按确切句柄排空或明确保留未决义务，不可用新空 registry 抹去旧账。
4. **最后统一退休查询并清旧。** Work 读取同一份已提交 blocker；删除所选资源族的旧独立计数/清空能力。纯 callback 的内存排空、Attachment / ReuseScope 留在声明范围，不顺手扩大迁移。
5. **验收。** 同名并发绑定；Spawn 内同步完成恰好一次结算；Reserve/Drain 两种顺序；COMMIT 前后丢回执；Reserve 后、Started 前、退出后但 Ended 提交前重启；错 registry/缺 scope/查询失败。增加主仓真实 PTY 的 TERM、等待、KILL、exit 证明，通过前保留 GAP-133。

候选修改面：主仓 `Host/Runtime`、`PtyTool`、`ToolRuntimeScope`、`DurableWorkHost` 与 PTY 身份/监管接口，相关 `.fsi` 同步修改。基础 Ownership 已有需要的 Ended-before-Started 协议，当前无证据要求重写；保留并复跑库 session 001–005、ownership composition 和 package 测试。

### 集成与交付顺序

先各自补反例和契约，再认领精确文件。Process、Trace、Ownership 可并行调查与局部实现；`DurableWorkHost`、公共 PTY 接口、包版本/锁/vendor 和主仓集成串行。CUT-01-R1 与 CUT-04-R1 若同时触及 Journal 打开策略，由同一集成者先定边界。

库有修改时，从精确提交构建、测试、打包，升级版本并记录 tarball 摘要；不得原地替换 `0.5.2`。主仓按编号 requirement 套件验证受影响链，最后集成者执行看板规定的最终门禁并记录实际 TODO / skip / fail。每卡分别报告代码提交、测试结果、主线推送和真实运行证据；没有真实 Host 或平台证据就保留边界，不把通过单元测试写成部署完成。

## 6. 本轮进度

- 已完成：拉取并核对独立源码；重建六入口并对照锁包；三路交叉复核；复跑库原有测试和 PTY 反例；形成问题报告及修复条件；更正两处历史验收解释并挂入唯一看板。
- 尚未完成：生产代码修复、正式回归用例、公共协议定稿、新库发布、主仓接线切换和真实 PTY / Host 验收。
- 下一步：按看板认领具体修复卡。可以先推进相对 PATH、PTY 提前清账/同步完成、Proxy 回归等局部闭环；同时完成无损输出、监管实例及 Opening 的协议设计，避免以局部修复宣称整卡关闭。
