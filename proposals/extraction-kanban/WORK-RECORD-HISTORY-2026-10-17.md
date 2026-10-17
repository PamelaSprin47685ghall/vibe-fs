# CUT-06-R2：已完成范围的历史表示

输入为干净主仓 `2ec1a5400`、库 `10bbb3247853653543d26eb269ac49008e1b5623 / 0.6.4`。本包按[历史重物化计划](REMAINING-REPAIRS-2026-10-16.md#gap111-history)施工，接续 R1/R3；R12-U1 继续暂缓。当前验收进度见本文后半和[看板](BOARD.md#source-audit-repairs)。

2026-10-17 独立复审从干净 `49b64879c` 继续，确认前轮只证明了线性历史，没有闭合并发父边下的冷恢复。当前修正与验收见[下方 R4](#r4-review)；原命令、包身份及线性历史证据保留，不能再据原交付结论推断所有并发历史已稳定。

## 问题与归因

| 编号 | 性质 | 复现与修正 |
|---|---|---|
| R2-01 / P1 | 输入已有的 WHAT[work-record-004] 缺口 | 完成范围先显示 `CURRENT_REASONING_SUMMARY`，晚到 entry 会增加 `LATE_SUMMARY`，跨 invocation squash 会将其改为原文。修前八个真实 Terminal/journal 对照全部失败，与是否提前读取、是否带 ProviderRun 无关 |
| R2-02 / P1 | 输入已有的完成消费时序缺口 | Sync 使用 capture receipt 的当前 head；Fork 等 drain 后读取当前 head。旧精确包实际 Fork 在 Terminal 后追加语义 part，再发完成通知，发布 blob 混入 `FUTURE_DETAIL`，见 `/private/tmp/g111h-old-frontier-red.log`。现在两个生产消费者都用该 run 已验证 Terminal 的确切终点 |
| R2-E1 | 前轮验收表述过宽 | 旧 Fork `settle` / `prepareTerminalDelivery` 测试接缝只捕获原件便发完成通知，未写生产 TerminalReporter 必先捕获的 Terminal。前轮完成 blob/Join 的稳定证明成立，但这两个用例不能单独证明真实 Terminal 已捕获。本轮补齐该接缝，直接 Terminal 反例和生产 TerminalReporter 接线分别核对 |
| R2-E2 | 本轮测试夹具错误，已修 | 首次 reanchor 测试把通用 JournalHandle 传给使用另一 opaque wrapper 的 Grounding surface，报 disposed 访问错误。改为现有 JournalSurface.appendAgent 解码真实 ContextReanchored；没有解包私有句柄或改 reanchor 业务规则 |
| R2-E3 | 编译规模门禁的必要契约更新 | 首轮相邻回归只有 delegation-028 失败。与 HEAD 的 ProjectReference 传递集合精确比较为391→392，新增仅纯 `Mission/WorkRecord/CompletionProjection.fs`、删除为空；按 WHAT-028 的增长规则记录原因并更新 ratchet。60%硬上限、合同及恢复隔离未放宽，日志 `/private/tmp/g111h-closure-delta.log` |

当前候选的限定验收未发现新的行为回退。首候选的 FSI 缩进编译失败和构建期间补测试导致的 mid-build mutation 都已保留日志；失败构建不算交付证据。R1/R3 的来源隔离、coverage 与 Terminal 去重继续回归。

## 完成点与职责

`CompletedWorkRecordProjection` 属于 WorkRecord，在已验证 `TerminalOutputCaptured` 成功折叠的同一次 aggregate 更新中，保存 ProviderRun、其不可变 Terminal frontier、当时的 Blogger frame 引用。它不写新事实或旁挂数据库，不按首次读取建立 range 缓存；冷恢复按原有事实顺序派生相同结果。

保存的是已有 newest-first 不可变 list 引用，后续 entry 可共享链尾；没有复制正文、BlogCoverage、PrefixCoverage 或整个 Blogger 状态。Trace 继续拥有并校验原件/Terminal，Blogger 拥有当前 frame 和 coverage，Composition 只组装两个领域的输出。拒绝 Terminal 在派生完成表示前返回，重放同 run 不覆盖已捕获引用。冻结时没有 frame 的 `Some []` 也是证据，不能退回当前摘要。

带 ProviderRun 的 bounded 读取只在终点匹配该 run 时取冻结引用。不带 run 时取该终点第一个完成事实的摘要引用，仍不借 Terminal 正文；同一终点后续 run 的完成表示可不同，但不能改掉前一 run 的表示。两条路径继续按 R1 的连续完整前缀、实际采用的 coverage 和原件补足规则物化。全 session 记忆与未完成范围保留当前视图；没有回写历史 coverage。

## 消费者盘点

| 消费者 | 完成身份与读取契约 | 本包验证 |
|---|---|---|
| SyncDelegate / Engineer、DevOps 等 | invocation 首个 StartCursor + 当前 turn.ProviderRun；Terminal 成功后以该 run frontier 关闭范围，共用 materializer | 实际复用子会话两轮，上一轮不混入、本轮正文保留；完成后追加原件/squash/另一 Terminal 再处理完成 turn，返回字节与完成时一致；新进程冷读一致 |
| Fork / resume / Join | PendingHostRun.StartCursor + 完成结果 ProviderRun；以确切 Terminal frontier 物化后写原有 v2 completion blob | 实际两轮和 Join、完成后 squash 的 Join/冷持久 consume；Terminal 与通知之间插入新原件/squash/另一 Terminal，发布 blob 不扩大范围 |
| parent bounded handoff | 已发送 frontier→当前 head；它是发送时的工作增量，未完成的父工作不是 child 完成事实 | 保留准备时的范围；若终点有 Terminal，公共 bounded 读取使用当时引用。初次 parent 全记录仍为当前记忆视图，不伪造父工作完成 |
| process review / Finality | 当前树没有原 ProcessReview / FinalityController 产品消费者；保留在需求文字及 WorkRecord-016 的范围夹具中 | 不以016夹具冒充真实工具验收，也不为已退出路径增加 facade；实际接线在 PluginSessionWiring/PluginHooks 指向上列共享消费者 |

Sync 的 Terminal 写入 NotCommitted/Unknown 分支继续按 delegation-031 交付已产生的 turn，不重执行；本包的冻结证明限于有已验证持久完成事实的路径，没有把它写成物理 Unknown 已归因。

## 历史兼容边界

原 v2 完成 blob 含 work_record、ProviderRun、child/root，但没有可证明的 Start/End 关联。原 Join、解码和 cold consume 继续消费这个已保存的确切表示，不根据当前摘要重写它。一般 range 或子范围查询不能只凭 ProviderRun 取整份旧 blob。

旧版本可能在 Terminal 后又入账摘要，再物化并发布旧完成 blob。本轮对原事件回放固定的是 Terminal 当时的引用；这种旧 blob 与派生历史查询可能原本就来自不同的摘要选择点。二者不自动迁移、不宣称升级后统一成同一份字节；该数据关联缺失仍属于显式历史迁移/完整 GAP-109 产品链的边界。没有 Terminal 的旧日志不补造完成事实。

已用修前精确 `eeaa8b…` 包生成真实 Terminal→late entry→完成 blob，退出旧进程后由新候选冷读并 durable consume。原 blob 摘要 `491189275a48a5f614f96effd8cb740c7a9ae2e59a5b84188df0561356f0cbfe`，包含 `EARLY_SUMMARY` 和 `LATE_SUMMARY`，升级后确切保留；新范围查询只使用 Terminal 当时的 `EARLY_SUMMARY`。脚本 `/private/tmp/g111h-upgrade-compat.mjs`，日志 `/private/tmp/g111h-upgrade-produce2.log`、`/private/tmp/g111h-upgrade-consume.log`。此升级对照借用主仓 runtime 依赖，单列为历史语义证据；精确新包的隔离安装由后面的独立包验收证明。首次旧包解包后的依赖缺失日志 `/private/tmp/g111h-upgrade-produce.log` 不算产品失败或成功安装证据。

## 验收进度

| 工作包 | 状态 | 证据 |
|---|---|---|
| 反例与契约 | 完成 | `/private/tmp/g111h-red.log`：原实现新增10项全部失败，八项有真实 Terminal、两项是旧 Fork 通知接缝；WHAT-004 明确完成点和历史边界 |
| 完成表示与生产消费 | 完成 | Fable 574，174 surfaces / 839 modules；`/private/tmp/g111h-build-seam.log`；基础构建573含核心生产修改，574补既有 ContextReanchored 的 JS 解码 |
| 核心 Node/Bun | 完成 | 004/010/011 各70项 / 69pass / 0fail / 1TODO，日志 `/private/tmp/g111h-core-node2.log`、`/private/tmp/g111h-core-bun.log`；唯一 TODO 为实际 inspect/Fork 同事实完整链 GAP-109 |
| 不重叠相关回归 | 完成 | 三组共1239项 / 1137pass / 0普通fail / 34skip / 68TODO；Trace/WorkRecord/prefix 341pass / 0fail / 1skip / 16TODO，durable/obligation 247pass / 0fail / 27skip / 9TODO，consumer最终549pass / 0fail / 6skip / 43TODO。首轮consumer的ratchet失败保留 |
| 非作者源与行为复验 | 完成 | `g111r_contracts` 在574固定产物独立 Node/Bun 验证真实Fork完成→squash→历史查询/Join/独立冷读/consume；空frame且无提前读取、同run重放、同终点多run、错误身份/终点及冲突Terminal被拒后的冷前缀均通过 |
| 独立编译、checkpoint、Host | 完成 | 新CompletionProjection owner仅声明闭包的94个源文件独立Fable编译通过；原Trace opaque接口正反编译1/1；真实非空Trace/Terminal checkpoint1/1；host-boundary-021为20/20（接线/前缀smoke，不代替真实消费者） |
| 旧包升级、精确新包、门禁 | 完成 | 上列升级对照通过；575精确新包2076members / 13,336,942bytes，SHA-256 `df0ccd0a0be4913df8a4c241719b0ebda1827ffcf9e36db54b0b91663751e81a`；真实pack、外部隔离安装与默认入口通过。非作者同包独立离线安装、全文件字节核对及Node/Bun实际消费者四项probe通过；format/check/diff通过 |

默认组命令使用 `WXS_ACCEPT_TODO=1 TESTS_MJS_FILES="<对应目录内编号测试的逗号列表>" node requirements/verification-system/tests/run.mjs`；三个不重叠目录组分别为 work-record/semantic-trace/prefix-stability、context-compression/delegation/participant-horizon、durable-events/obligation-ledger。TODO/skip 不记为通过。日志 `/private/tmp/g111h-{trace,consumers-final,durable}.log`；修订前consumer日志 `/private/tmp/g111h-consumers.log`。

integration 独立启用 `WXS_TIER_INTEGRATION=1`，按 `independently compile` / `completed-terminal checkpoint` 名称运行 semantic-trace-005、obligation-ledger-004；日志 `/private/tmp/g111h-integration-{trace,checkpoint}.log`。新增owner编译为仓库 `compileOwnerProject`，日志 `/private/tmp/g111h-completion-owner-compile.log`，未运行 dotnet build。格式、静态和包日志 `/private/tmp/g111h-{format-final,check-final,package}.log`；生产574、文档/测试元数据575，174 surfaces / 839 modules。

非作者探针日志 `/private/tmp/gap111r2-contracts-freeze-{node,bun}-574.log`、`/private/tmp/g111h-contracts-terminal-boundary-{node,bun}-verified-574.log`。冲突Terminal触发既有semantic-cut/SIGKILL，原未隔离的exit137日志保留；一次独立probe漏计既有coverage边界的重读reasoning，修正oracle后通过，不改产品。

同一精确包的独立安装日志 `/private/tmp/g111h-contracts-package-{ci,integrity}.log`，行为日志 `/private/tmp/g111h-contracts-package-{freeze-node,freeze-bun,boundary-node,boundary-bun}.log`。离线安装117项依赖，移除顶层wanxiangli，实际使用包内0.6.4，所有依赖及符号链接限定在隔离安装中，不借主仓node_modules；2076个安装文件与archive逐字相同。暖/冷Fork冻结和Terminal边界四项probe均exit0。产物在文档收尾后再次与完整archive核对，见 `/private/tmp/g111h-final-package-closure.log`。

ESM解析定位与默认入口在Node/Bun均通过，日志 `/private/tmp/g111h-contracts-package-isolation-verified-{node,bun}.log`；首次辅助定位误用CJS解析import-only exports、把默认导出对象当函数的两项oracle错误均保留原日志，修正为实际ESM/入口契约后通过，不算产品错误。隔离安装和全部探针Git/journal目录已清理，见 `/private/tmp/g111h-contracts-package-cleanup.log`；旧包升级探针目录也已清理，脚本与原始日志保留。

## 交付结论

CUT-06-R2 所列完成表示、确切终点、真实消费者与暖冷历史查询已通过验收，随本原子提交正常推送master，不强推。旧历史的range关联缺失按上述边界保留。库0.6.4未修改；本包没有部署运行中的用户Host。

首次核心 reanchor 夹具失败日志 `/private/tmp/g111h-core-node.log`，编译/并发输入失败分别为 `/private/tmp/g111h-build-projection.log`、`/private/tmp/g111h-build-consumers.log`。这些不覆盖后面的成功证据，也不归因给原拆分。

本包只处理有确切完成事实的历史表示与已列消费者。GAP-111、GAP-101/109、Opening/Resume/Fission 完整产品链、CUT-06 父卡和 R12-U1 不据此整体销项。提交推送与运行中用户 Host 部署分别记录。

<a id="r4-review"></a>
## CUT-06-R4：R2 非作者复审

三名未参与 R2 实现的复审者分别审查冻结、真实消费者、回放/持久边界。先保存干净基线：核心 Node 为 70 项、69 pass、0 fail、1 TODO，包 `df0ccd…` 另存 `/private/tmp/r2r-before-49b64879c.tgz`；库 0.6.4 未改。

| 编号 | 归因 | 反例与处理 |
|---|---|---|
| R4-01 / P1 | 原完成冻结缺口未闭合；R2 复审漏检 | A 完成后，旧视图 B 才追加同父边摘要；冷进程合法地先折叠 B 的摘要，再折叠 A Terminal，Chronicle 因而改变。真实 A/B/C 进程证据 `/private/tmp/r2r-replay-order-process-node.log`；改用 Terminal 的持久父边闭包选材 |
| R4-02 / P1 | 输入已有的 Trace 完成终点缺口 | 同样的晚原件使冷 Terminal frontier 从 3→4 或 4→5；旧范围丢正式陈述，新范围混入后续原件。`/private/tmp/r2r-replay-frontier-node.log`、`/private/tmp/r2r-freeze-causal-frontier-appended-node.log`；由 Trace 按因果可见连续原件前缀确定终点 |
| R4-03 / P1 | 原多写者契约缺口；R2 未覆盖 | 多 head 时 `TryHead=None` 导致 Journal 新事实丢父边；同 run 并发同文 Terminal 的不同终点、不同 run 同终点的不同 Chronicle 又会按规范首笔任意选胜者。`/private/tmp/r2r-freeze-causal-{duplicate,terminals-ordered}-node.log`；writer 改取全部 heads，完成歧义显式缺证 |
| R4-N1 | 本轮候选新引入，交付前修正 | 因果筛选去掉 E1 后，E2 的 coverage 可能跳过未表示原件；bounded 前缀现在显式验证从起点连续接续，不完整时保留原件。新增正式反例 |
| R4-N2 | 本轮候选新引入，交付前修正 | 初稿把任一未证 Terminal 当作整个 session 的永久阻断。现在只约束其已保存候选边界，后续未完成范围继续读取当前视图。新增正式反例 |
| R4-N3 | 本轮候选新引入，交付前修正 | 独立 Trace 的无 Opening Terminal，因后来并发 Opening 的规范排序而变无效。现在 raw 终点证明与完整 WorkRecord 的 Opening 证明分开；已有材料缺证不冒充完整记录，合法 standalone Trace 也不被撤销 |
| R4-N4 | 本轮候选新引入，交付前修正 | 把相同 run 的并发候选数量当作终点冲突，使相同证据的重复完成阻断错误终点的当前视图；并且有序旧未证终态重放一度用 aggregate head 扩大未知范围。现在分别比较实际终点、使用 Terminal 保存的候选边界 |
| R4-N5 | 本轮候选新引入，交付前移除 | 链时钟在重复分叉/合流时扫描不断增宽的 clock，即便业务不查成员也付费。6145 事件旧候选约507ms、新候选约71ms；删除 eager clock，改为父边索引、指定目标早停和已验证材料的可选正向见证。时间与 CPU 证据单列，不把 GC 噪声说成内存上界 |
| R4-E1 | 前轮验收范围误判 | 原 checkpoint 观察口只转发十四个旧 slice，没有新完成投影。补真实完成物化前后逐字比较和独立冷读，不再以十四个 slice 的相等代替该证明 |
| R4-E2 | 本轮排除的 review 疑点 | 空 Completed 是接缝直接构造；真实分类器会续写修复空输出。ReconcilePass 的 onSnapshot 只做 compaction 观察，不是原件捕获。均未证明生产回退，不据此改生产规则 |
| R4-E3 | 本轮测试接缝越界，已撤回 | 将五组 Kernel 白盒探针直接加入 requirements 后，JS 边界门禁报13处违规：深导入内部产物、依赖 Fable 集合及读取 DU 布局。已撤回该 helper 和五个用例，不放行门禁、不新增生产 facade；临时探针只作独立调查证据，不计正式用例。原失败日志 `/private/tmp/r2r-check-delivery.log` 保留 |

“输入已有”指这些问题已存在于本轮输入 `49b64879c`，并不推断全由最初拆分引入。N1–N5 是本轮未提交候选的回退，未混记成 R2 发布版新增问题。未在本轮已检验范围发现 R2 发布修改另引入的回退；R2 对原并发缺口的修复及验收不完整，已单列纠正。

职责保持分开：EventStore 仅把既有显式父边加入不可变索引；单信封 oracle 只能询问确切 EventId，不能枚举事件、读取正文或取得历史 reader。成员查询按目标早停，同一次调用内只缓存布尔结果。未查询成员的追加不展开祖先；因果索引随候选 Current 一起提交，语义拒绝和 cut-tail 仍传递结构父边，重复 EventId 不改变原事实的含义。没有修改事件词汇、持久格式、k-way 排序或库包。

Trace 保存实际 part 前缀见证及各 part 当时的不可变见证；新 part 只有已证明旧前缀全部可见时才将见证收敛到自身。Terminal 可以提供当前前缀的可选正向捷径，未命中仍查实际材料见证，不强制并发合法分支知道另一个 Terminal。同材料 Opening 的不同成功捕获见证均保留。raw 终点缺证不向库写入猜出的绑定；缺 Opening 见证的完整 WorkRecord 返回缺证，独立 Trace 的 raw 证据保持独立。

WorkRecord 保存摘要引用及替换关系：不可见的晚 squash 展开其旧节点，可见且 replacement 证据齐全的 squash 才采用新节点。完成事实折叠时一次选择；常态共享 Blogger 当前不可变 list，稀有并发分支才展开原节点。完成状态只保存选择结果，不保留因果上下文、成员闭包、正文或 coverage。Frame 的正向捷径与实际见证同样分开，变更只清除自己的捷径。完成且空、尚未完成、完成缺证分别处理；并发候选相同可读，不同则缺证，不借当前摘要。

物理 writer 的自然顺序及 RuntimeId 不用于补造父边：同 EventId 可以出现在另一 writer 的副本中，用副本的前驱反推原始已见状态会改写同一事实。旧历史若已丢父边，不能猜回原材料关系；已保存 v2 完成 blob 的原字节消费不受影响。未证完成只影响其旧观察范围，后续尚未完成范围仍可读取。GAP-111/109、完整 Opening/Resume/Fission 链及 R12-U1 均不整项关闭。

成员查询对未知目标最坏仍遍历该信封的祖先元数据闭包，不声称任意 DAG 的所有 CPU 开销为常数。没有把 durable-events-017 的零 Git/历史 I/O 当作完整 CPU 复杂度证明。此次普通追加的 clock 退化已移除，正常完成通过已证明见证避免反复查起点；异常/分叉下实际所需的成员证明按其材料边界计算。

| R4 验收 | 状态与证据 |
|---|---|
| 先红反例 | 首12项3 pass/9 fail：`/private/tmp/r2r-formal-matrix-red.log`；580的N3/N4/未证重放三项全红：`/private/tmp/r2r-formal-n3-n4-580-red.log`。正式 WorkRecord 新增22项，经注册 Surface 构造事实并由独立进程冷读；五组 Kernel 白盒探针另列 |
| Fable 与核心 | 生产generation582，174 surfaces/839 modules，`/private/tmp/r2r-build-final2.log`。Bun核心92项/91 pass/0 fail/1 TODO；Node同核心全部包含于下面Trace组。初候选579的Node/Bun 88 pass证据另留，不代替最终候选 |
| 三组不重叠默认回归 | Trace/WorkRecord/prefix：380项/363 pass/0 fail/1 skip/16 TODO；consumer：598/549/0/6/43；durable/obligation/新增durable-convergence：333/283/0/33/17，日志 `/private/tmp/r2r-{trace,consumers,durable}-final.log`。共1311个不同用例 / 1195 pass / 0普通fail / 40 skip / 76 TODO；TODO 与 skip 不算通过，撤回的五个白盒用例不计入 |
| 真实 checkpoint 与接口 | 完成前后实际 bounded record逐字相同，独立冷进程再读一致：`/private/tmp/r2r-checkpoint-final.log`，1/1。Trace consumer正反Fable编译：`/private/tmp/r2r-opaque-compile-final.log`，1/1。十四个旧slice比较仍保留，但不再冒充新完成投影观察 |
| 非作者冻结复验 | 582 Node/Bun各40范围、三类squash各10范围、Opening/shortcut、同文重复/并发歧义及未证重放均通过；`/private/tmp/r2r-freeze-{matrix,candidate,proof,ambiguity}-{node,bun}-582.log`。原真实三进程晚Blog/part各24轮通过，只记抽样，不当全部拓扑穷尽 |
| Kernel 边界与成本 | 临时白盒探针直连 Fable 产物，五组验证分叉/merge/alias、非Journal和失败/cut relay、prepared discard/失败/stale commit及retention；`/private/tmp/r2r-replay-kernel-582.log`。它们是独立调查，不冒充注册 JS 消费者或正式 requirements 门禁。真正并发shortcut正控的规范顺序已核对：`/private/tmp/r2r-replay-shortcut-582.log`。旧/新成本 `/private/tmp/r2r-replay-kernel-cost-{580,582}.log` |
| 真实消费者与历史 | Sync/Fork两轮暖交付=固定范围=新进程冷读，`/private/tmp/r2r-consumer-final-582.log`。旧无range v2格式fixture的blob与冷consume保持旧字节，`/private/tmp/r2r-consumer-legacy-final-582.log`。另用真正旧df0ccd包的Fork producer发布Terminal→晚摘要后的完成blob，退出后582新进程读/consume/再读均保留原字节，派生也不借晚摘要；原blob SHA256 `2b892d70c6d0c590dc5f87ed8bfb20f93ff1770c49c900b474e67ebb71eb4c66`，`/private/tmp/r2r-old-binary-upgrade-582.log`。旧二进制升级借主仓runtime依赖，单列语义证明，不冒称隔离安装；临时包/store已清理 |
| 独立owner与门禁 | Completion owner声明闭包94源独立Fable编译成功，`/private/tmp/r2r-completion-owner-final.log`。撤回越界测试后，完整 check、format:check 均 exit 0：`/private/tmp/r2r-check-after-test-boundary.log`、`/private/tmp/r2r-format-after-test-boundary.log`；JS 边界0违规，未增加豁免。正式013重验11/11，监管收尾 accepted=true，`/private/tmp/r2r-durable-013-withdrawal-verified.log`，不与默认组重复相加。早先16/16包含已撤回五项，只留历史日志，不当正式通过数 |
| 精确包与隔离消费 | 584实际 pack、隔离安装和默认入口通过：2076文件 / 13,366,102字节，SHA-256 `22998049cad15b0b4cf580c87def35f95b0656b55835fc775d4e3607f7f4176c`，`/private/tmp/r2r-package-final.log`。非作者对同一包独立离线 npm ci，移除顶层 wanxiangli，实际使用包内0.6.4；Node/Bun各做真实 Sync/Fork 暖交付和新进程冷读，2076个安装文件前后均与 archive 相同，包摘要未变，隔离目录已清理；`/private/tmp/r2r-isolated-package-584.log`。文档收尾后的完整 archive 闭包零差异，`/private/tmp/r2r-final-package-closure-verified.log` |

首轮 F# 类型推导失败、第二轮编译输入变化、初始控制嵌套/格式失败及包验证的过期输入失败日志保留，不计交付证据。收尾辅助核对首稿把 `assertBuildFresh` 返回的摘要记录误当成带 `ok` 的结果，日志 `/private/tmp/r2r-final-package-closure.log`；另一次013的11项断言通过，但沙箱禁止 `ps` 导致监管无法收尾，日志 `/private/tmp/r2r-durable-013-withdrawal-final.log`。修正核对脚本、在可执行原监管的环境重验后均通过，未改产品或放宽监管。收尾只刷新文档/测试元数据，生产仍为582；交付包按完整文件闭包再次核对，不以旧 manifest 冒充当前产物。

CUT-06-R4 所列并发冻结缺口已修正，三名非作者在最终582产物完成复验；所验范围未留已确认的新回退。正常后续 entry/squash、实际消费者和新进程冷恢复保持完成时表示；无法证明唯一完成材料时明确缺证。按 AGENTS.md 与本原子提交正常推送 master，不强推。库0.6.4未改，旧丢边历史不补造关联，GAP-111/109及父卡不整体销项，R12-U1继续暂缓；未部署用户 Host。
