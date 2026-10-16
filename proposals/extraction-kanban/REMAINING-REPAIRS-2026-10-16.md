# 四模块剩余问题与下一轮修复计划

日期：2026-10-16。主仓输入 `c1ed871be`，库输入 `eef4825 / 0.6.2`。依据[双版独立复审](REGRESSION-REVIEW-2026-10-16.md)、当前源码及 requirement；施工状态只在 [BOARD.md](BOARD.md#source-audit-repairs) 更新。难度按契约变化、历史数据和验收成本判断，不按改动行数或 TODO 数量判断。

2026-10-16 执行补充：下表保留立项时的问题快照。R6-A 已完成原型、F# 生产接线及非作者独立复审；本轮新引入的 owner 无响应、直接 Ctrl-Z 停监管者、退出信号错名已修正并复验。详见 [PTY 监管报告](PTY-SUPERVISION-2026-10-16.md)。脱离控制终端的守护进程、R6-B/C 与 Trace 仍在原边界内；不把这次会话范围的修复当成整项 O-01 完成。

## 目前还剩什么

| 工作包 | 剩余问题 / 证明范围 | 优先级、难度 | 为什么不能当小补丁 |
|---|---|---|---|
| CUT-04-R6-A | O-01：真实 PTY leader 退出后，同组后代仍活着，但 lease/blocker 已空 | P1，高 | 需在 leader 退出前建立可持续监管的物理实例；事后拿旧 PID/PGID 发信号可能误伤 |
| CUT-04-R6-B | O-01：DevOps 每轮收尾的准入只在本进程关闭，另一进程尚无同一轮持久准入边界 | P1，中高 | 要把本轮工作身份、Reserve 和停止准入放到同一持久仲裁中，不能多加一个内存 bool |
| CUT-04-R6-C | O-01：重启后能保留 blocker，却不能接管原物理实例并证明退出 | P1，很高 | 缺少跨进程可验证的实例身份、连接和退出见证；数据库中的旧 PID 不是操作许可 |
| CUT-03-R6 | T-01：Trace 仍主要接派生描述符，原件捕获、持久恢复与部分下游仍由旧链承担 | P1，很高 | 涉及真实 run/message/part 身份、原字节、旧 cut 和多个消费者，不能直接删除桥 |
| CUT-02-R6-A | GAP-091：Process 纯契约仍与物理实现同编译单元，部分消费者缺独立能力边界证明 | P2，中 | 要同时调整项目依赖和调用方；改目录或造一个转发接口不能证明拆开 |
| CUT-02-R6-B | GAP-092：正文符合预算，完整返回值加包装和程序事实后仍可能超预算 | P2，中 | 先明确最小可接纳预算及拒绝通道，不能为了塞进一字节预算删掉退出事实 |
| CUT-02-R6-C | Bun 的四项 Process 测试失败：特殊 env 键、参数拦截、两项 stdin 断言 | P2，中 | 已确认原生 API 差异，需分别修能力或验收假设，不能统一放宽断言 |

立项时的四项 P1 中，R6-A 声明的会话范围现已独立复审通过；当前主要剩余工作是 R6-B/C 和 Trace，脱组守护进程仍属于 O-01 的未完成边界。已有的预留、Unknown 保留、局部准入关闭和冷 blocker 防错继续有效；它们不代替上表其余缺失的能力。

还有验收缺口。上一轮主仓的 37 项 TODO 中，四条有实际失败输出：process-execution 012、015，prefix-stability 014，以及 PTY leader-exit；其余也不能计为通过。6 项 skip 是未开启 integration tier，不是 6 个已证实产品错误。Trace 的独立消费者 integration 曾单独通过，不据此宣布另外五项通过。没有把 37 项 TODO 说成 37 个独立缺陷。

## 本轮已直接修复的小项

CUT-02-R6-V：库 Capture 错误注入夹具。旧测试在 Bun 下不能通过 syncBuiltinESMExports 更新已加载的 named spawn，child 未赋值，尚未触达产品错误处理。现在从真实 stdout 读取定位原生 Readable，再向它注入 error 事件，检查生产监听器、EIO、stdout 通道、待决读取、无假退出及错误副本。

库提交 `b6984d9`，只改编号测试与证明记录，生产源码、类型和 npm 包内容未变，不重打或覆盖 0.6.2，主仓 vendor 也未更换。Node Process/Supervision **69/69**，Bun Capture/Supervision **19/19**。这只关闭该夹具问题，不等于 Bun 全部进程套件通过。

扩大运行 Bun Process/Supervision 是 **65 pass / 4 fail**。进一步对照未使用本库的原生 API：

| 场景 | Node 26.6.0 | Bun 1.3.14 | 目前结论 |
|---|---|---|---|
| async spawn / spawnSync 的 `__proto__` 环境键 | 两者均保留 | async 丢失，sync 保留 | 既有对照混用了不同原生路径；Bun 下的特殊键转发能力仍有实际缺口 |
| syncBuiltinESMExports 后的 named spawn | 原夹具可观察 | named 未更新，default 对象已更新 | 参数拦截是夹具失败，不能靠 observed 未赋值判断产品参数错误 |
| 暂停输入后结束子进程、已关闭输入管道 | 原生写回调 EPIPE | 原生写回调成功、无 EPIPE | 本库 Written 对应原生回调，不表示子程序消费；原断言强制 Unknown 的前提不成立，但还需稳定的错误回调负控 |
| stdout.destroy(EIO) | 原测试可触达错误处理 | 原生 stdout destroyed，但未观察到 error | 本轮改为明确的 error 事件注入；不冒称真实设备 I/O 故障实测 |

原始日志在 `/private/tmp/r6-*`，完整结果和边界已记入库 `施工顺序.md`。原生对照中只结束探针自己持有的子进程；最初使用进程组信号的探针失败，不把它写成产品失败或成功证据。

## 修复顺序与验收

### 1. CUT-04-R6-A：先证明 PTY 监管方案，再接线

1. 固定已有 leader-exit 反例，再补 sibling 隔离、TERM 忽略、KILL 升级和自然退出。开始时必须观察真实进程就绪，结束时逐一确认确切测试 PID 已退出。
2. 在隔离原型中验证创建 PTY 时就保留活监管者的方案。监管者要在交互 shell 退出后继续持有本次实例和被监管范围；不能等 shell 已退出才缓存 PID、扫描进程并推断身份。现有 pipe SupervisedProcess 只作设计参照，不能直接当 PTY 使用。
3. 同时验证真实 TTY、输入、resize、Ctrl-C、前台作业控制和目标退出码。只证明一个进程组，不能宣称覆盖了 shell 建立的其他作业组或脱组守护进程；若原型不能兑现所声明范围，停在原型阶段修订方案，不把未知写成 Ended。
4. 方案成立后再定窄物理协议：启动实例、目标退出、监管范围结束分别答复。PTY 物理 owner 提供证据；C 安排 Ended 持久确认；Ownership 不查询 OS、不持有 Host。
5. 接回 PtyBackend / PtySupervisor / Host，删除对应“leader exit 即全范围完成”路径。025 的 leader-exit 只有在真实后代已死、物理 owner 已确认结束后才转正。

完成条件：025 真实矩阵通过，早到退出/Unknown/共享端口/实际终结回调的既有反例继续通过；不能破坏交互 PTY 语义。先做原型，不在本轮预写一个未证实的 supervisor API。

### 2. CUT-04-R6-B：持久关闭同一轮的准入

先确定 canonical admitted work 与 PTY run scope 的精确绑定，再实现 Reserve / 停止准入的同 revision 仲裁。每轮 scope 唯一，上一轮关闭不影响下一轮或另一 owner；不把整个 Session 永久关掉，也不把 scope 名称猜成业务身份。

用两个独立进程和屏障验证两种先后：Reserve 先确认时，排空必须包含该实例；停止准入先确认时，第二进程不得 Spawn。COMMIT 前后丢回执、重复请求、旧轮晚到消息都保持原确切身份与 Unknown 义务。只有首次持久确认才给物理启动许可。

候选面为 Host/Runtime、RunLifecycle、ToolRuntimeScope 和 DurableWorkHost 的合作协议；仅在现有 Ownership 公开协议确实不足时扩展库，不先复制状态机。与 A 的实例/结束语义对齐后施工，不以跨重启接管完成为前提。

### 3. CUT-04-R6-C：按真实实例恢复，不重发命令

依赖 A 的可持续物理 owner、B 的持久准入。先确定接管连接和可验证 incarnation，不以 PID、PTY 名称、计时或进程不存在推断结果。owner 不可达或证据不匹配继续保留 Unknown/blocker。

真实强杀旧 Host，分别切在 Reserve 后/Start 前、真实启动后/Started 确认前、退出后/Ended 确认前。新 Host 只能连接同一实例或接受确切终态；不得重放原命令。覆盖旧回执到达新实例、owner 同时失联、持久确认失败，并保留无物理绑定的拒绝正控。若需常驻监管进程，其启动、退出、认证和持久身份本身也须独立验收。

完成条件：真实 Node/Bun 与目标平台的崩溃恢复矩阵，不是新建空 registry 后看到零 blocker。

### 4. CUT-03-R6：分四个闭环迁移原件

1. **盘点及冻结契约。** 依 D-02 对每类材料明确真实物理身份、typed origin、原字节、重放/冲突、游标、Terminal 与 Opening 绑定。盘点旧 AgentJournal facts、blob、派生 raw records、已发布 cut 的读取路径。历史缺失不能靠重新编码字符串或合成 xrun/xmsg 补成原件。
2. **一条真实捕获链。** 从实际 Host 的已确认材料，经 C 提交到唯一新 owner，再从冷打开的存储读回。覆盖 prompt/助手/推理/工具、typed retry 正文排除、并发捕获前串行、同身份异字节拒绝；纯描述符不算完成。此范围切换后只有一个可写入口。
3. **消费者逐个切换。** WorkRecord → prefix/Y → Casebook → invocation/Fission。每次带精确 cut/range 和旧路径删除清单，验证原字节、不跨 resume、确定性 lane 汇聚和请求呈现不入持久前缀。不让消费者继续重建原文或读内部投影猜范围。
4. **历史处理与清旧。** 对可验证原件生成明确版本/新 trace identity，保留已发布旧范围的合法读取或明确拒绝原因。坏版本已截短的 raw 字节、已改写的旧 cut 不保证能恢复；不得原地重写同 traceId 前缀或静默换算 cut。只读比较用于验收，不能留下两份长期写入权威。

完成条件：semantic-trace 及 WorkRecord/Prefix 的真实 Host + cold journal 证据，T-01 范围内旧 owner 退出。大 Opening/晚 Opening 已修的独立协议不重写；库仍不认识主仓或 Host。

### 5. 三个 P2 包各自闭环

- **CUT-02-R6-A**：先拆纯请求/结果/错误/PTY 词汇的实际编译依赖，再调整组合者与消费者的注入边界，最后清旧入口。用实际正反消费者编译验证没有物理实现仍能消费契约、不能反向访问 adapter；转正 012 前不靠移动文件或自造模型通过。
- **CUT-02-R6-B**：先在 WHAT 中明确最小可接纳预算、预执行拒绝通道及完整 wire 的计算范围；用实际编码结果分配正文尾部预算，保留真实 exit/signal/错误与合法 UTF-8。预算不足必须在 Spawn 前处理，不执行完后丢事实。含转义、双语、零正文、非零退出和极小预算，再转正 015。
- **CUT-02-R6-C**：原生对照已完成，下一步分别解决特殊 env 键的能力边界、跨运行时 spawn 参数证明，以及可控的真实 stdin 错误回调负控。先定公开承诺再改测试/实现；保留无自动重试、一次有界写入、输入副本和宿主 env 哨兵。Node 全部既有用例保持，Bun 四项逐条转绿，不加 skip 或宽泛的 Written/Unknown 二选一断言。

### 6. 相关 TODO 和平台证据按归属补，不凑数量

| 原选集待证明项 | 后续归属与边界 |
|---|---|
| Process 5 TODO：008、012 两项、013、015 | 012/015 由上面 P2 包承接；完成通道与零模型会话补真实 plugin 调用，不能只查函数名 |
| Trace/取材 8 TODO：semantic 007/008/009/011/012、prefix 009/014、work-record 008 | T-01 与 CUT-06/15/16/28 分项承接。GAP-108 先查真实 Host admission，不教原件捕获层按正文猜并删除指导后缀 |
| 归属相邻链 24 TODO：lifecycle 14，crash 2，delegation 3，horizon 2，session 2，work 1 | 四模块相关部分由 O-01 包补真实恢复/终止；exact prior-run closure、Inspector、旧角色处置等保留各自 GAP-123/124/133/149/153，未因库接线自动完成 |
| 其中旧 Join 挂起记录（participant-horizon 011 / GAP-080） | 先在当前版本独立复现，确定 waiter/持久结算/Host 收束归属，再立修复卡；旧记录不直接当成本轮已证新缺陷 |
| 6 个 integration skip、Linux/RSS/断电/脱组等范围 | 分别登记前提和运行目标；只证明已运行的具体场景，不把本地 Node/Bun 定向绿扩大为这些范围通过 |

## 施工纪律和下一步

先做 CUT-04-R6-A 的真实 PTY 监管原型，再做持久每轮准入与崩溃接管；Trace 可独立调查，协议和共享消费面有依赖时串行修改。P2 按表独立提交，不能以容易补的证明代替 P1 进度。

每包先固定反例与公开承诺，再改实现和调用者。库生产协议变化须新版本、精确包和安装消费者，再回主仓；不覆盖旧版本。每包完成更新看板、进度、确切版本和剩余边界，保留失败记录；只跑受影响 requirement，源代码构建仍用 `node scripts/build.mjs`，不执行 dotnet build。

立项轮只完成小修复和较难部分的计划；后续 R6-A 生产实现及独立复审见本文件开头的执行补充。不宣称 T-01、O-01、GAP-091/092/133 已关闭，也没有部署运行中的 Host。
