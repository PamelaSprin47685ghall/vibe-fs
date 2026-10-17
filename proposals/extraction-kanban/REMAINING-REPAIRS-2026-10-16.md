# 四模块剩余问题与下一轮修复计划

立项日期：2026-10-16。原主仓输入 `c1ed871be`，库输入 `eef4825 / 0.6.2`。依据[双版独立复审](REGRESSION-REVIEW-2026-10-16.md)、当前源码及 requirement；施工状态只在 [BOARD.md](BOARD.md#source-audit-repairs) 更新。难度按契约变化、历史数据和验收成本判断，不按改动行数或 TODO 数量判断。

<a id="post-t01-plan"></a>
## 2026-10-17：T-01 复审后的剩余工作与排期

本次盘点输入为干净主仓 `c3689035c`、库 `10bbb32 / 0.6.4`。**后续顺序以本节为准；下文 2026-10-16 的问题快照和施工顺序保留为历史依据。** 本节只规划四模块整改的后续，不把全仓其他抽取卡并入本轮，也不开始新的生产修改。

同日执行：已从 `53cba28de` 完成 CUT-06-R1 的范围隔离、原件补足和真实同步/异步消费者修正，见[反例、非作者复验与交付证据](WORK-RECORD-RANGES-2026-10-17.md)。GAP-111 保持 PARTIAL：后续 squash 后的历史表示字节冻结仍是独立边界；R12-U1 继续暂缓。下文保留当时盘点及计划，当前状态以看板为准。

同日独立复审：CUT-06-R3修正e7新引入的Terminal重复回退，并修复前已有的formal-only重复，原跨界与正文保留继续成立。真实完成→squash的对照已将CUT-06-R2从“待补冻结证据”收窄为[已确认的历史重物化契约缺口](#gap111-history)；Fork完成blob、Join及冷持久消费稳定，不等于所有历史查询都已冻结。最新计数、归因和边界见[复审证据](WORK-RECORD-RANGES-2026-10-17.md#independent-review)，R12-U1继续暂缓。

原四项 P1 工作包 R6-A/B/C 与 T-01 已在声明范围内验收；最近 T-01 复审确认的问题均已修正。父卡 CUT-03-R1、CUT-04-R1 仍有产品链和监管范围义务。上一轮 2270 项中的 100 TODO、51 skip 不是 151 个已确认错误；缺实现、缺证据、运行前提与已确认失败须分别处理。

### 当前剩余项

| 工作与证据性质 | 优先级、难度 | 当前结论与下一步 |
|---|---|---|
| **CUT-06-R1 / GAP-111：跨 invocation 的 Chronicle frame 越界**；已有生产物化反例 | **P1，中高** | 本次 work-record-004 再现：只取后一次工作，结果仍含 `EARLIER_WORK`。先修范围与 coverage 的共同语义，再验证冷恢复和实际消费；不能只滤掉 frame 却继续沿用其 coverage，导致当前工作正文被漏掉 |
| **CUT-17-R1/R2 / GAP-108：指导入口核查与独立复审** | **四项R1回退已修并独立验收** | 真实Host隔离结论保留；四项R1回退均有旧包正控，600删捕获前还原、保留呈现owner冷重放。新增13项反例、1363项相关回归0普通失败、实际Host和2e0f精确包非作者隔离Node/Bun各12组暖/新OS冷读通过；O1撤回为review误判，旧错历史不迁移，完整CUT-17不销项。下一包R6-B/GAP-092。[归因与证据](GUIDANCE-INGRESS-2026-10-17.md#r2-review) |
| **CUT-02-R6-B / GAP-092：完整结果超预算**；已确认行为缺口 | **P2，中** | 本次 256 字节预算返回 549 字节。先定最小可接纳预算与执行前拒绝通道，再限制真实编码后的完整结果，保留退出码、信号和合法 UTF-8 |
| **CUT-02-R6-C：Bun Process 四项失败**；能力差异与测试假设混合 | **P2，中** | 最近独立实测为 0.6.2 的 65 pass / 4 fail；本轮未重跑库 Process，不能记成 0.6.4 新测结果。特殊 env 键有实际差异，spawn 拦截与两项 stdin 断言须按原生 API 分别处理，不把四项统称四个产品错误 |
| **CUT-02-R6-A / GAP-091：纯契约仍带物理编译依赖**；已确认结构缺口 | **P2，中** | 本次 process-execution-012 仍显示 Request 与 NodeProcessHost 同编译单元。先拆实际契约闭包，再改真实消费者注入；须独立正反编译，不能只移目录 |
| **GAP-101/109：Trace、Opening、Resume、Fission 的完整产品链**；主要为未完成能力及证明 | **高，逐场景定级；难度高** | 原件、局部 Terminal 与冷恢复已证明；完整 Opening 区间、实际 inspect/fork/join/Casebook 同范围、重复 Resume、Fission 合流仍须逐条验收。Opening 的旧 BlindPlan/T1 条款与当前 checkpoint 语义先对账，不凭空补造旧业务事实 |
| **O-01 / GAP-133：完整启动恢复、主动脱组、容量与平台边界**；超出已验收子范围 | **高；Bootstrap 中高，脱组很高** | 现有控制终端会话内监管、持久准入和确切实例接管保持有效。完整 Bootstrap 强杀矩阵仍缺证；主动 setsid/脱离控制终端不在已声明监管范围。先各做一个真实小闭环，不把物理口测试当作完整 Host 验收 |
| **R12-U1：原自然 Unknown**；未唯一归因 | **暂缓** | 按用户决定保留 BACKLOG；有新现场或用户重启此项时再处理，本计划不继续调查 |

旧历史中已经错写的 retry 正文、Blog coverage，以及有歧义或缺失的物理身份，不在 T-01 中自动改写。若发现实际受影响数据，另开遵守 durable-events-026 的明确迁移包；不能根据缺失证据猜原值。未测平台、断电和规模/RSS 也分别保留，不能用增加普通测试次数替代。

<a id="gap111-next"></a>
### 第一个工作包：CUT-06-R1，修复跨 invocation 取材

1. 对照 work-record-002/003/004/005/014/016 与 context-compression-029，确定跨界 frame 可用范围和有效 coverage 的共同规则。调用方给出真实 invocation 区间；Trace 保管原件，WorkRecord 决定有证据的 bounded 视图，不能复制第二份正文或压缩状态。
2. 以现有 004 失败为起点，补 frame 完全在内、跨起点、跨终点、同时跨两端及多 frame 的实际 journal 用例。必须同时断言“旧工作不出现”和“当前工作不丢失”，并覆盖仅 terminal、工具材料排除和重复物化。
3. 跨界摘要没有可验证的分段证据时，不按字符串裁摘要。优先研究从同一 Trace 重物化该区间并匹配实际采用材料的 coverage；若证据不足则明确拒绝该表示。不能删除摘要后仍把对应原文算作已展示，也不改写历史 BlogFrame。
4. 接入真实 inspect 与 fork/join 的 bounded 消费，验证重复 Resume、后续追加和冷重开不会改变已完成范围。旧 caller charge、Opening 显示方向及当前 Terminal 选择保持原义。
5. 定向运行 work-record、semantic-trace、context-compression、prefix 及实际受影响的 delegation/horizon 用例；由非作者复审最终候选。这里只关闭 GAP-111 对应反例与已验证路径，不顺带宣布 GAP-101/109 或 CUT-06 全部完成。

候选写集合从 `Mission/WorkRecord/Materialize.fs(.fsi)`、必要的 Trace/Blogger 范围查询及相应编号测试开始；先核对实际调用点再预约公共文件。当前只完成盘点，尚未认领生产编辑面。

<a id="gap111-history"></a>
### CUT-06-R2：已完成范围的历史重物化

2026-10-17执行：已按下列计划完成有确切Terminal事实的WorkRecord表示派生、真实Sync/Fork终点及暖冷历史查询，核心69pass/1TODO、相关1239项0普通失败；非作者源/行为复验、旧精确包升级blob消费、精确新包隔离默认入口及同包非作者Node/Bun行为复验均通过，随本原子提交交付。[实现、消费者盘点、历史边界和日志](WORK-RECORD-HISTORY-2026-10-17.md)。以下保留施工依据；GAP-111/109不整项关闭，R12-U1继续暂缓。

同日R4独立复审纠正上述验收的外延：并发晚摘要/原件可在冷排序中提前，原R2未固定其因果取材。现按Terminal显式父边、Trace材料见证与WorkRecord摘要替换关系修正，多head写者保留全部父边；同身份/同终点有不同完成表示时缺证，不按回放次序猜胜者。旧丢边历史不补造关联，旧v2 blob仍保留原字节。1311个不同用例0普通失败，checkpoint/消费者/独立冷读、三名非作者及229980精确包隔离Node/Bun消费者均已通过；撤回的五个越界Kernel白盒用例不计正式验收。本包限定范围完成，完整GAP-111/109及R12-U1边界保留。[问题分类与R4证据](WORK-RECORD-HISTORY-2026-10-17.md#r4-review)。

难度中高。已确认后续squash会改变已完成range经当前投影重物化的表示，WHAT-004尚未满足；不是已发布完成blob被改写，也不是本轮Terminal修正新引入。Fork的v2完成blob、同进程Join和冷持久consume已有正控，新进程Join按既有归属契约排除旧handle。不能用这一条Fork链替其他消费者关闭缺口。

1. 逐一盘点SyncDelegate、process review、Finality、parent bounded record等实际调用者：谁需要当前范围视图，谁需要已经发布的canonical完成记录，已有哪一笔完成事实和blob能承接。核对完成身份、ProviderRun、range及读取时机，不先添加通用range缓存。
2. 在现有work/完成记录owner决定冻结与读取契约，优先消费已有确切完成表示；缺少持久关联时先定完成时的原子事实，再调整真实生产调用者。Trace只保管原件，Blogger只保管摘要和coverage；不向Trace塞Blogger快照，不回写历史coverage，不通过弱化WHAT-004来迁就当前实现。
3. 用实际完成→后续entry/squash/terminal/reanchor的正反对照验证，同一完成身份/range的canonical读取暖冷保持确切表示，新invocation不借旧记录；同时保留当前range取材完整与工具材料排除。没有独立consumer和新进程证据，不关闭整项GAP-111/109。先按消费者分包，旧错历史不自动迁移。

<a id="guidance-followup"></a>
### 第二个工作包：CUT-17-R1，证明或修正真实指导回流

以 prefix-stability-014 的现有反例为风险线索，驱动实际 Plugin 请求变换与后续捕获，分别观察原始 Host part、呈现 wire 和 durable 原件。覆盖工具完成、快照补捕获、重复消息及普通用户引用相同指导文本，并冷重开核对原文。若真实回流成立，在拥有来源证据的入口修正；若真实路径隔离成立，保留边界负控并把 TODO 换成真实 Host 证明。验收需同时证明指导不会污染原件、正常正文不会被误删，不通过修改原件层的保真契约来满足夹具。

2026-10-17执行完成：正常持久Host入口未见原件回流；修正输入已有的引用误删。包内014及实际OpenCode1.18.29网络、持久消息和独立冷读已通过；三个不重叠相关组1350项0普通失败，精确f047包隔离Node/Bun消费通过。原直接捕获误判、本轮接缝错误及旧夹具问题分开记录；此处不关闭完整CUT-17或旧数据迁移。[完整报告](GUIDANCE-INGRESS-2026-10-17.md)。R12-U1继续暂缓，接着处理下列R6-B完整结果预算。

R2四项R1回退已闭环：最终600删捕获前还原，只让呈现owner重放已确认原件。同代变化明确拒绝，新代合法字面原文完整捕获；1363项相关回归0普通失败、真实Host及2e0f精确包非作者Node/Bun各12组暖/新OS冷读均通过，接着处理R6-B。人工加工wire冒充原件的O1产品归因已撤回，不改写成正常Host必然回流。

### 随后的顺序与依赖

- **第一段：上述两个 P1 范围。** CUT-06-R1 先完成，CUT-17-R1 的只读调用链调查可并行；共享捕获、coverage 和消费者修改串行。若后者证明正常路径无缺陷，按证据关闭核查，不为凑修复制造改动。
- **第二段：三个 Process P2，按 B → C → A 各自闭环。** 先完整结果预算，再运行时行为与验收差异，最后纯契约编译边界。B 只先定所需预算契约，不以整个 A 重构作为前置；有实证依赖时再调整顺序。每包一次明确提交，库产品变更须新版本与精确包，保留原始红例。
- **第三段：补完整链路，逐个场景交付。** Trace 依次核对 Opening 契约和真实来源区间、重复 Resume 的各消费者、真实 compaction/reanchor、Fission 合流与终结。Ownership 单独覆盖实际 Bootstrap 的加载/Drain/接管/持久终结，切在 Reserve 后、Started 前后、Ended 提交前后；命令只启动一次，证据不足留账。两条调查独立，公共接线串行。现场若发现新的 P1，优先插回修复队列。
- **主动脱组另做原型。** 先明确目标平台和监管承诺，再用真实脱组后代、同用户无关进程隔离、Host 强杀验证可持续的实例证据。只有原型成立才安排生产接线；不能扩大旧 PID/PGID 扫描和发信号范围来宣称全树监管。此项可以先只读研究，不阻塞上述可交付闭环。

每包完成都更新看板、确切版本、反例/正控/冷恢复证据和未覆盖范围。通过范围内验收后转下一包，不循环开展没有新增风险依据的全量复审。已验证、已提交推送、已部署分别记录；本次未部署 Host。

### 本次盘点证据

本次未改生产代码、测试或库包。对主仓 `c3689035c` 的现有构建运行：

```sh
TMPDIR=/private/tmp node --test requirements/work-record/tests/004.test.mjs requirements/prefix-stability/tests/014.test.mjs requirements/process-execution/tests/012.test.mjs requirements/process-execution/tests/015.test.mjs
```

结果 **17 项 / 12 pass / 0 普通 fail / 0 skip / 5 TODO**，日志 `/private/tmp/post-t01-plan-baseline-20261017.log`。其中四个 TODO 执行后仍失败：GAP-111 越界、GAP-108 直接回流、GAP-091 编译边界、GAP-092 完整结果；另一个 TODO 只有待补的消费者能力声明。命令 exit 0 不代表这五项已通过，也不重复计算为上轮 2270 项之外的新全面验收。

## 历史执行补充

2026-10-17 执行补充：R6-A/B/C 已按各自声明范围完成独立复审，原 R12-U1 按用户决定单列 BACKLOG。本次继续完成 T-01 的原件接管、消费者切换及旧路径退出，见 [T-01 逐项进度和证据](TRACE-ORIGINALS-2026-10-17.md)。下文立项快照及分期计划保留；整体 O-01、GAP-101 等独立产品证明和 Process P2 不由此关闭。

同日独立复审补充：CUT-03-R6 因真实反例重开，现已修正并按本轮范围验收。原 T-01 新回退、复审候选引入的问题、既有捕获缺口与验收误判分别记录；库0.6.4、主仓2270项0普通失败、独立消费者、非作者Node/Bun及精确包强杀恢复均通过。R12-U1 继续 BACKLOG，Trace/Fission/Invocation 整体 GAP 不借此关闭，详见上方 T-01 证据。

2026-10-16 执行补充：下表保留立项时的问题快照。R6-A 已完成原型、F# 生产接线及非作者独立复审；新引入的 owner 无响应、直接 Ctrl-Z 停监管者、退出信号错名已修正并复验，见 [PTY 监管报告](PTY-SUPERVISION-2026-10-16.md)。R6-B 也已完成非作者复审，修正停止失败漏清理、构造失败连接泄漏和既有 Load void 准入遗漏，见[每轮准入报告](PTY-RUN-ADMISSION-2026-10-16.md)。R6-C 已按原型→生产接线→真实强杀/精确包/平台验收施工，自验通过，待非作者复审，见[跨重启接管报告](PTY-RESTART-RECOVERY-2026-10-16.md)。脱离控制终端的守护进程与 Trace 仍未完成；不把局部闭环当成整项 O-01 完成。

## 立项问题快照

| 工作包 | 剩余问题 / 证明范围 | 优先级、难度 | 为什么不能当小补丁 |
|---|---|---|---|
| CUT-04-R6-A | O-01：真实 PTY leader 退出后，同组后代仍活着，但 lease/blocker 已空 | P1，高 | 需在 leader 退出前建立可持续监管的物理实例；事后拿旧 PID/PGID 发信号可能误伤 |
| CUT-04-R6-B | O-01：DevOps 每轮收尾的准入只在本进程关闭，另一进程尚无同一轮持久准入边界 | P1，中高 | 要把本轮工作身份、Reserve 和停止准入放到同一持久仲裁中，不能多加一个内存 bool |
| CUT-04-R6-C | O-01：重启后能保留 blocker，却不能接管原物理实例并证明退出 | P1，很高 | 缺少跨进程可验证的实例身份、连接和退出见证；数据库中的旧 PID 不是操作许可 |
| CUT-03-R6 | T-01：Trace 仍主要接派生描述符，原件捕获、持久恢复与部分下游仍由旧链承担 | P1，很高 | 涉及真实 run/message/part 身份、原字节、旧 cut 和多个消费者，不能直接删除桥 |
| CUT-02-R6-A | GAP-091：Process 纯契约仍与物理实现同编译单元，部分消费者缺独立能力边界证明 | P2，中 | 要同时调整项目依赖和调用方；改目录或造一个转发接口不能证明拆开 |
| CUT-02-R6-B | GAP-092：正文符合预算，完整返回值加包装和程序事实后仍可能超预算 | P2，中 | 先明确最小可接纳预算及拒绝通道，不能为了塞进一字节预算删掉退出事实 |
| CUT-02-R6-C | Bun 的四项 Process 测试失败：特殊 env 键、参数拦截、两项 stdin 断言 | P2，中 | 已确认原生 API 差异，需分别修能力或验收假设，不能统一放宽断言 |

立项时的四项 P1 中，R6-A 声明的会话范围及 R6-B 每轮准入现已独立复审通过；R6-C 完成生产施工与自验，仍需非作者复审。主要剩余施工是 Trace，脱组守护进程仍属于 O-01 的未完成边界。已有的预留、Unknown 保留、准入关闭和冷 blocker 防错继续有效；它们不代替上表其余缺失的能力。

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

已按本节施工，具体实现、真实两进程反例、试制修正和最终验收见[每轮 PTY 跨进程准入报告](PTY-RUN-ADMISSION-2026-10-16.md)；当前状态只在看板维护。以下保留原计划作为验收依据，不据此关闭 R6-C 或 O-01 整体。

先确定 canonical admitted work 与 PTY run scope 的精确绑定，再实现 Reserve / 停止准入的同 revision 仲裁。每轮 scope 唯一，上一轮关闭不影响下一轮或另一 owner；不把整个 Session 永久关掉，也不把 scope 名称猜成业务身份。

用两个独立进程和屏障验证两种先后：Reserve 先确认时，排空必须包含该实例；停止准入先确认时，第二进程不得 Spawn。COMMIT 前后丢回执、重复请求、旧轮晚到消息都保持原确切身份与 Unknown 义务。只有首次持久确认才给物理启动许可。

候选面为 Host/Runtime、RunLifecycle、ToolRuntimeScope 和 DurableWorkHost 的合作协议；仅在现有 Ownership 公开协议确实不足时扩展库，不先复制状态机。与 A 的实例/结束语义对齐后施工，不以跨重启接管完成为前提。

### 3. CUT-04-R6-C：按真实实例恢复，不重发命令

已按本节完成原型、生产接线与限定范围的作者自验，见[实际实现、失败归因、验收和剩余边界](PTY-RESTART-RECOVERY-2026-10-16.md)；当前状态只在看板维护。以下保留原计划，不将未测的完整 Bootstrap 或 owner/回执同时丢失写成可恢复。

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

## 原施工顺序与持续纪律

立项时先做 CUT-04-R6-A 的真实 PTY 监管原型，再做持久每轮准入与崩溃接管，随后完成 T-01。上述四包现已按声明范围验收；新的执行顺序见本文开头的[当前排期](#post-t01-plan)。协议和共享消费面有依赖时仍串行修改，不能以容易补的证明代替 P1 进度。

每包先固定反例与公开承诺，再改实现和调用者。库生产协议变化须新版本、精确包和安装消费者，再回主仓；不覆盖旧版本。每包完成更新看板、进度、确切版本和剩余边界，保留失败记录；只跑受影响 requirement，源代码构建仍用 `node scripts/build.mjs`，不执行 dotnet build。

立项轮只完成小修复和较难部分的计划；后续四个 P1 包的实现与复审见历史执行补充及各自报告。T-01 已按限定范围完成，O-01 整体、GAP-091/092/133 及本次列出的产品链缺口仍保留，没有部署运行中的 Host。
