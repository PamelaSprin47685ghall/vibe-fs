# 前四模块本地修复与上游差异分析

日期：2026-10-18，时区 Asia/Singapore。接续 [本地工作总结](LOCAL-FIRST-FOUR-MODULES-REPORT-2026-10-18.md)。本轮只读取、对照和运行临时反例，不合并 upstream，不修改生产实现、需求或测试。

## 结论

**不能用 upstream 的较高版本整体替换本地前四模块修复。** 上游已经覆盖个别局部修正，也替换了部分旧产品流程；但 Journal 释放、完整输出采集、物理监管、Trace 原件接管和 PTY 持久归属等关键修复仍未被覆盖。工具退役和规范删减不能作为这些保证已经实现的证据。

| 模块 | 已覆盖或已有相同实现 | 仍需保留 | 需要按上游新流程重新落位 |
|---|---|---|---|
| CUT-01 生命周期与日志 | Work、DurableWork 的共同基础仍在；上游另有连续未评审接力门禁 | Journal statement 释放、Unknown 后封存及显式 close 重试、竞争和原生释放测试 | `.Herdchestra` 持久路径与源码 submodule 接入方式 |
| CUT-02 进程执行 | 基础 ProcessSession 的旧留尾能力仍在，不能视作完整 Capture | Capture、SupervisedProcess、extent、Bun 背压、Spool 失败清理、PATH、纯契约及显式能力注入 | 旧 ExecutorTool 的预算与呈现修复须接到新 `js.run`，不能恢复已退役工具来凑接线 |
| CUT-03 原始轨迹 | WeakMap 缓存一次初始化已经修正；基础 capture 的重入 guard 相同 | 完整及晚到 Opening、原件 owner、身份与来源护栏、冷恢复、指导与物理原件隔离 | 旧 child WorkRecord、LWR_prev、Blogger squash 的部分消费职责已退出，相关旧组合代码不能照搬 |
| CUT-04 资源归属 | 库端 Ownership 核心相同，属于共同基础 | Host 层确切预留与结束、回执未知留账、共享端口隔离、收尾继续执行、持久轮次准入和重启接管 | 新 `js.run` 的 foreground/background 也需要相应执行归属；旧 PTY 控制工具不恢复 |

以上是按行为划分的去留结论，不是四个模块的完成率，也不表示保留每一行旧代码。将来若接入上游，应保留必要保证与反例，再选择适合新调用链的实现。

## 对照基线与证据范围

| 对象 | 固定版本 |
|---|---|
| 本地主仓已提交基线 | `d2db5a0c3`，生产变更截至 `ebfbe3d83` |
| 本地独立库 | `df14f489d0a503ce300da6df6ef8f71910b477eb`，实际 vendor 包 `0.6.8` |
| 上游主仓 | `705e1ea3b0946ea91edb3ce694e22f58228b5c19`；本轮 fetch 后未变化 |
| 上游主仓实际固定的库 | submodule `e4ba7ab6fd7e1028a68b26dcc5fda2a6e2e51f78`，包版本 `0.8.0` |
| 两条主仓线的共同祖先 | `7d8feff030adfdeae511bdc0d8bb77f3c00b2a1f` |

另外检查并保存了分析期间暂存区的 R6-B 总预算候选及其报告。快照 patch 的 SHA-256 为 `aa45e2ee7316d27b7c13487bf9e66663477212300d694fa93da4ecf104bed330`，不把它混入上述初始已提交基线。

分析收尾时，另一项已有施工已将 R6-B 以 `3c8f385c7` 提交并推送，本报告初稿也随该提交入库。预算修复因此由暂存候选变为本地已交付成果；其报告记载相关组 911 pass、0 普通 fail、15 skip、29 TODO，Node/Bun 精确包四文件各 46/46。该状态变化不影响上游差异结论：上游已删除旧 ExecutorTool，尚未接入同等完整 wire 预算。本轮没有重跑该施工的验收，详情以 [总预算交付报告](PROCESS-WIRE-BUDGET-2026-10-18.md) 为准。

本轮在临时目录按上游锁文件安装声明依赖，用 `node scripts/build.mjs` 编译了 eventstore、trace、process、lifecycle 四个库。针对本地精确 vendor 产物和上游固定源码的生成物，在 Node 26.6.0、Bun 1.3.14 上运行同一组公开接口探针。其余主仓判断来自固定版本源码与契约对照，没有把上游整个 Host 链或全量套件宣称为已验证。

## 生命周期与 Journal

| 本地工作 | 上游对应实现 | 判定 |
|---|---|---|
| J-01 statement 确切释放 | 上游仍直接 prepare/get/all/run，没有本地的 withStatement/finalize | **保留**。Bun 实际反例中，读操作抛错后保留的原生 statement 仍可执行；本地已 finalize |
| J-02 业务封存与物理 close 分开 | 上游 close 正常路径在原生 close 成功后才置 closed；Unknown 路径却先置 closed，随后 close 直接跳过 | **保留**。两种运行时均复现：首次普通 close 失败后上游仍允许业务读取；Unknown 内 close 首次失败后，显式 close 不再尝试释放 |
| E-03 真实追加竞争及初始化持锁验证 | 上游竞争测试缺少本地逐个初始化就绪、worker 退出确认及构造持锁反例 | **保留测试修正**。不能用一次竞争结果代替连接及进程寿命验证 |
| Host 构造失败时清理连接 | 上游 useJournal/useEphemeralWork 已使用 finally，构造成功和失败分别关闭 Journal 或原连接 | **部分覆盖**。有清理框架，但不能替代上述 Journal 释放与封存；关闭重试、原错误保留仍须按真实故障矩阵核对 |
| Work 与 DurableWork 的基础划分 | 上游仍是 Work 领域规则、Journal 存储、DurableWork 组合；库 src/assembly 与本地逐字相同 | **共同基础**。无需再搬一份相同实现，也不能据此推断 Journal 修复已经进入上游 |

上游反而新增了一项本地没有的规则：第二次连续未评审 Plan 接力返回 ReviewRequired，不写新记录。同一公开 Work 探针在两种运行时均得到：本地三次 Relayed，各写一条；上游第三次 ReviewRequired，零写入。以后组合库源码时须保留这项上游进展，不能用本地旧 Work 文件整体覆盖。

源码依据：[上游 Journal](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/e4ba7ab6fd7e1028a68b26dcc5fda2a6e2e51f78/src/eventstore/SqliteJournal.fs#L57)、[本地 Journal](https://github.com/dyx13/wanxiangli/blob/df14f489d0a503ce300da6df6ef8f71910b477eb/src/eventstore/SqliteJournal.fs#L64)、[上游接力门禁](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/e4ba7ab6fd7e1028a68b26dcc5fda2a6e2e51f78/src/lifecycle/Work.fs#L96)。

## Process 与新 js.run

上游存在两条需要分别判断的路径。旧 NodeProcessHost 仍给内部调用者使用 ProcessSession 的留尾轮询；新的模型执行面则由 ToolWorkflow 直接调用 JsRunLoop。它们都没有采用本地的 Capture/SupervisedProcess 组合。

| 本地工作 | 上游现状 | 判定 |
|---|---|---|
| P-01 完整采集与故障传播 | 上游库没有 Capture 重载、takeOutput 或 discardOutput；旧主仓桥每 20ms 读取最多 16MiB 留尾，吞掉 handler/read 异常 | **保留**。不能把已丢字节的 tail 当完整 Spool；公开接口探针确认本地 Capture 有两方法，上游没有 |
| P-02 Draining 阶段监管 | 上游包没有 supervision 入口，基础对象在直接子进程退出后拒绝旧 PGID 信号 | **保留监管保证**。基础安全限制不能删除，需要外侧维持真实可控制实例 |
| 相对 PATH 从目标 cwd 解析 | 上游仍直接对非空 PATH 目录执行 pathJoin(dir, fileName)，没有将相对目录先绑定目标 cwd | **保留**。新 js.run 使用 shell 是另一个调用合同，不覆盖旧内部 runner 的错误 |
| P-04 首次 Spool 写失败清理 | 上游 startStreamingSpool 直接 writeFile 后返回，没有本地失败时删除已创建路径的处理 | **保留**。本地原反例仍是验收依据，本轮没有重新注入该主仓故障 |
| R6-A 纯契约与显式能力 | 上游 process-processrequest 工程仍编译 NodeProcessHost、ProcessRunner；012 仍是 GAP-091 TODO；HostForkRuntime 仍隐式创建 PtyBackend | **保留**。接口与实际调用者需要一起迁移，不能只保留文件拆分 |
| R6-C 环境、argv、stdin | 上游库仍只走 node:child_process；主仓环境用普通对象赋值，stdin 非 Written 只退出写循环，写异常也被吞掉 | **保留必要语义并适配新入口**。本地 Bun 适配、特殊键和真实回执修正没有被较高版本自动吸收 |
| R7-A 逐路 extent | 上游没有本地 Supervision Worker 的最终 extent 与外侧 observed 一致屏障 | **保留或重做等价屏障**。目标 exit、close 或写回调不能单独证明最终消费者收齐 |
| R7-B Bun 实际背压 | 上游没有本地 native-process/BunProcess 的 FIFO 读写及 Worker 出口交接；新 JsRunLoop 用 data 事件接字符串 | **保留背压与字节边界要求**。公开队列大小、截断字符串和写者受到背压不是同一证明 |
| R6-B 总 wire 预算及预取消修复 | 本地于分析期间完成交付；上游已删除 ExecutorTool，JsRunLoop 的 head/tail 按字符串长度截取，外层返回结构另行渲染 | **重新落位**。保留完整编码预算、最小预算拒绝、Host 窗口、BOM、预取消事实的反例，不照搬旧 run 工具注册或资源文件 |

JsRunLoop 确实有独立 maxDuration、超时竞速和子进程清理循环，不能把它说成没有执行监管。但其 onChildExit 监听 exit 而非输出 close，回调未携带 signal，killManaged 会 destroy stdout/stderr，SliceBuffer 在 check 前持续拼接字符串。这些实现没有构成本地“逐路字节交付、确切退出、背压和持久资源归属”的等价证明，需要按新执行面单独验证，不能以旧工具退役判为覆盖。

R6-D 的退出码与信号区分尚不是本地已完成修复：本地桥会归一为 128+signal，上游旧桥将任意 signal 归为 137，新 JsRunLoop 的 signal-only exit 也没有独立信号字段。应继续保留该待办，不把它列成本地现成补丁。

R6-B 最终交付还登记了 R6-E：旧 ExecutorTool 的取消监听在实际等待前已被 finally 移除，排队取消后仍可能启动命令。它是修前缺口，预算包只关闭预取消路径。上游已退役这个工具，不能把旧 finally 补丁原样搬到 JsRunLoop，也不能据工具删除宣称新的排队、执行中取消与收尾已经验证。

源码依据：[上游旧物理桥](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Process/NodeProcessHost.fs#L151)、[上游纯契约测试仍为 TODO](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/requirements/process-execution/tests/012.test.mjs#L9)、[新 JsRunLoop 的启动和退出观察](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Process/JsRunLoop.fs#L207)、[新执行面的实际接线](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Repository/Programming/Js/OpenCode/ToolWorkflow.fs#L423)。

## Trace 原件与旧消费者退出

| 本地工作 | 上游现状 | 判定 |
|---|---|---|
| T01-03 WeakMap 缓存一次初始化 | Model.fs 已改成模块级 emitJsExpr 值，取读与追加复用该缓存 | **已覆盖源码修正**。不用重复搬本地补丁；保留实际产物验收用例 |
| 基础 capture 重入保护 | 上游和本地均先进入 guard，再 decode 和去重；相关 Wire 基础代码相同 | **已有同等基础**。不能据此声称新增原件协议也相同 |
| 完整 Opening、晚到绑定、旧 cut 不被重写 | 上游桥仍 boundedOpeningBytes 截到 524288 字节，晚到 Opening 走重建 bridge；库不支持 OpeningBound | **保留**。上游没有覆盖本地大 Opening 和稳定旧边界修复 |
| T-01 真实原件接管及 Terminal 绑定 | 上游库没有 MaterialConfirmed、TerminalBound、material；桥仍追加描述符，物化时从 Blob 读正文再经临时 Trace 投影 | **保留**。同一材料探针在本地可原字恢复，上游返回 Unknown trace observation |
| 身份、来源及冲突护栏 | 上游捕获仍使用旧 provenance/Host 下标路径；没有本地 tuple、双 facet 共享身份检查及统一 typed-origin 裁决的完整实现 | **保留**。与 N3–N9、T01-04/05/06 对应的反例一起带入新链，不能只换 Trace 构造器 |
| canonical 坐标、因果完成前沿 | 上游没有本地完成前沿及对应证据投影；仍保留其他 Trace/WorkRecord 读取接口 | **保留仍存消费者所需的保证**，按当前事实协议调整，不整体恢复旧 child 输出流程 |
| 指导引用保真、跨代与物理编辑 | 上游 user 分支仍直接 stripCursorSuffixes；PluginTransforms 仍在物理捕获前调用 canonicalCaptureMessages | **保留**。本地删除捕获前还原、用来源与当前代证据处理呈现的修复未覆盖 |
| CUT-06 完成表示与 bounded Chronicle 组合 | 上游删除 CompletionProjection，真实插件 childRecordForRun 固定返回 None；Sync/Fork 改读 js.deliver 值或 child 正式输出 | **产品流程已替换**。旧 child LWR 组合器不原样移植；若继续提供历史范围查询、Casebook 或 inspect，仍需范围与完成不变性的独立验收 |

上游已明确退役 LWR_prev 注入、父子 DelegationHandoff 和 Blogger squash，使用 ANCHOR 视图与显式 charge 承接上下文。故本地针对这些旧生产调用链的接线、格式和测试预期，应在将来的产品取舍中退出或改写。这属于需求变化，不等于旧缺陷已由同等实现修好。残留的 WorkRecord API 仍会选择与范围相交的当前 frames，不能把“真实 child 已不调用”扩大为整个范围查询已经安全。

源码依据：[缓存已覆盖](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Context/Trace/Model.fs#L32)、[Opening 截断与描述符桥](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Context/Trace/Model.fs#L155)、[指导 user 分支](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/OpenCode/Host/PairProgrammingThoughtTransform.fs#L716)、[物理捕获前还原](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs#L620)、[child 读取接线退出](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs#L256)。

## Ownership 与 Host 收尾

库 src/session 与本地逐字相同，说明基础 scope、lease、blocker 规则并没有分叉。本地后续修复的主要部分在 Host 与物理监管层，上游这些层没有取得同等实现。

| 本地工作 | 上游现状 | 判定 |
|---|---|---|
| 首次预留才准许启动、Started/Ended 精确持久确认 | HostPtyOwnership 直接创建进程内 Ownership；Reserve 取 reply.result.lease，没有本地 durable receipt 的启动许可判断 | **保留**。不能因库核心相同而丢掉实际持久组合 |
| 早到终态、Unknown 重试、Cancel 失败仍清理 | 上游 settlePtyLease 先删本地 lease 再调用 ended；缺少本地原始退出事实缓存和持久确认后的清账顺序 | **保留**，包括提交前后丢回执的真实反例 |
| 共享端口隔离、监听释放、永久 detach 防止再启动 | 上游仍有无可释放订阅的 AddExitListener、默认端口和端口级 CloseAll；缺本地租约限定收尾与相关准入边界 | **保留**。订阅、关闭、取消与 canonical DevOps 合法复用需分别验证 |
| O-13/O-14 批量继续收尾、活 ID 不覆盖 | 上游 CloseAll 首个异常会中断循环，Fork PTY/Port 仍是旧注册方式 | **保留**，不能以单个实例的正常关闭代替批量失败矩阵 |
| R6-A 会话内后代监管 | 上游没有 PtyScope，直接 bun-pty onExit 即完成，信号走 PGID/PID | **保留真实监管能力**。直接 leader 退出不是范围结束 |
| R6-B 每轮跨进程准入 | 上游没有持久 per-LogicalRunId OpenRun/Drain 协议 | **保留**。终态之后另一进程不得再预留，需要同一日志修订裁决 |
| R6-C 认证重启接管及发布失败恢复 | 上游没有 PtyRecovery、独立 custodian、认证回执和 pending 发布重试 | **保留并适配状态路径**。Host 消失不能清账，原命令和输入不能重发 |

旧 PTY 工具已退出新工具面，可以让它们的注册与说明退出。上游新 js.run 仍支持 foreground/background 和 stdin/signal 交互，这些实际进程的归属与结束义务没有随工具名消失。是否继续承诺跨 Host 重启接管，需要另作明确产品决定；本轮没有授权删除已持久化资源或降低现有恢复保证。

上游新增 managed-session-lifecycle-028：child Terminal 已证实时，辅助 WorkRecord 读取失败不得搁浅 completion；结算故障必须成为可 join 的显式失败。RunLifecycle 中已有对应 catch/settlement。它解决 child 结算可观察性，值得保留，但不覆盖物理 PTY 的认证、退出和留账，也不能以空记录回退替代原件完整性要求。

源码依据：[进程内归属与旧清账顺序](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs#L506)、[端口批量清理](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Process/Pty.fs#L336)、[child 终态结算新处理](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs#L640)。

## 本轮实际反例结果

下面是固定库公开接口的对照观察，不是主仓全套回归。故障注入只替换测试自己创建的真实 SQLite 连接方法；原生连接、statement、工作状态机和 Trace 都实际运行，保留引用，不调用 GC。

| 探针 | 本地 0.6.8 | 上游固定 0.8.0 | 范围 |
|---|---|---|---|
| 读操作抛错后，保留的 statement 是否 finalize | 已 finalize | 未 finalize，原生 get 仍可调用 | Bun；Node 不提供同一 finalize 接口，不用该行给 Node 判失败 |
| 第一次普通 close 抛错，之后能否继续业务 read | 拒绝，业务已封存 | 仍允许读取 | Node、Bun |
| COMMIT Unknown，内部首次 close 抛错，再显式 close | close 尝试共 2 次，连接真实关闭 | close 只有 1 次，连接仍可执行 SQL | Node、Bun |
| 缺 ProviderRun 的 MaterialConfirmed 经 records 冷恢复 | 保留 BOM、CRLF、NUL 原文 | Unknown trace observation | Node、Bun；证明协议不等价，不把拒绝未知协议单独称为上游自身合同错误 |
| 显式 Capture 构造 | takeOutput/discardOutput 均为函数 | 两者均不存在 | Node、Bun；测试进程最终均 Closed |
| 连续未评审 Plan 接力 | 第三次仍 Relayed，写 1 条 | 第三次 ReviewRequired，写 0 条 | Node、Bun；上游新增规则，本地尚无 |

临时证据目录为 `/private/tmp/vibe-fs-four-module-analysis.0EMCLD/`，`results/{local,upstream}-{node,bun}/summary.json` 保存四次最终输出。探针脚本 `probes.mjs` 的 SHA-256 为 `24cc165dac8eafe14ce0c3177b8927476d4d50c3e6bc84fb91ee289aacff09f1`。首版探针未传 Capture 构造参数，不能据其两边方法均缺失判断能力；修正为显式 Capture 后才形成上表结果，普通模式不被要求暴露 Capture 方法。

## 将来处理差异时的边界

1. Journal 释放和封存修复可作为独立小包移入上游库，但保留上游 Work 新门禁及资源、身份、时间等新增模块；不能拿本地整个 0.6.8 包覆盖上游 0.8.0。
2. Process、Trace、Ownership 应分别迁移真实保证、调用者和反例。上游规范删除了不少本地补充条款，012 甚至仍是 TODO；规范变化必须先作语义裁决，不能靠选一侧文件消冲突。
3. ExecutorTool、旧 PTY 工具、LWR_prev、父子 handoff、Blogger squash 的旧接线不作为保留目标。若采用上游产品流程，相关保证要在 js.run、显式交付、ANCHOR 或仍存的查询入口重新成立。
4. 上游持久路径改为 `.Herdchestra`，禁止读取旧 `.git/wanxiang*`，依赖改为源码 submodule。它与本地 vendor 固定包及既有恢复路径是两项独立集成决定，不顺带迁移用户历史，不遗忘活动 lease 或未知提交。
5. R12-U1、旧精确包 PTY 首轮失败、完整 Bootstrap、脱组监管、平台容量和正式监督缺口继续按原台账保留。既未被本轮修复，也不因源码差异或后来测试通过而关闭。

本轮交付仅为差异报告；没有 merge、cherry-pick、rebase、主仓依赖升级或运行中 Host 部署。R6-B 由另一项已有施工完成提交，本报告只补记其状态与边界，不将该生产修改记为本轮分析实施。
