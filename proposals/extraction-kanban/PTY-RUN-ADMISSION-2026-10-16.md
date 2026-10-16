# 每轮 PTY 的跨进程准入

工作包 CUT-04-R6-B，输入主仓 `c659f9b33`、库 `b6984d9 / 0.6.2`，两仓起点干净。状态以 [BOARD.md](BOARD.md#source-audit-repairs) 为准。

## 契约和边界

现有库的 Ownership 已能通过单一日志修订仲裁 Reserve/Drain；本包修主仓接线，不复制状态机，不预设扩库。当前随机 runtime scope 只能关闭本实例，新进程仍可为同一 DevOps 工作启动 PTY。

每轮范围来自 canonical admitted work 的确切 LogicalRunId。工具调用绑定自己的已接纳 execution，不从 Session 当前值或 scope 字符串猜工作身份；重复工具调用使用稳定资源身份，只有首次已确认的 Reserve 可启动。每个 runtime 的资源仍有独立子范围，detach 不关闭别人或下一轮的准入。

真实终态回调先持久 Drain 该轮，再处理本地资源并查询整个轮次的 blocker；无本地绑定时只保留未完成义务，不假装接管。跨重启物理接管仍属 R6-C；脱组进程、Trace 和原 O-01/GAP-133 整体不销项。

## 验收计划

- 用两个真实进程和明确屏障验证：停止准入先确认时零 Spawn；Reserve 先确认时排空看到该实例并等待真实 Ended。
- 同 revision 的并发候选只能有一个首次提交；冲突、重复请求和 COMMIT 前后丢回执均不能变成新启动许可。
- 旧轮晚到请求、重放关闭不触及下一轮；另一个 owner 和每个 runtime 的 detach 保持隔离。
- 复验真实终态回调、冷无绑定、共享端口和 PTY 监管，保留失败记录；最终补 Fable clean、format/check 及精确包隔离消费。

## 问题与归因

| 编号 | 问题、证据与处理 |
|---|---|
| R6B-01 / P1，原 O-01 接线缺口 | 修复前的两个真实 Node 进程反例均失败：终态后另一进程仍能预留；先前实例 Ended 后旧轮仍能再开。原随机 runtime scope 不能表达整轮停止准入。现在以 canonical admitted work 的 LogicalRunId 建立轮次范围，终态先持久 Drain，再关闭本地实例并核对整轮 blocker。 |
| R6B-02 / P2，既有 Host/Journal 清理错误 | 双 Reserve 试验触及 SQLite 初始化锁竞争：库已关闭失败连接，主仓再次 close，把原始 locked 错误盖成 database is not open。三个 Host 组合入口统一交由 SqliteJournal 管理连接；正式反例核对一次关闭、原错误保留和零 Spawn。库及 vendor 未修改。 |
| R6B-03 / P1，前轮修复未封闭的组合前提 | `c1ed871be6` 增加可选 drain 回调并修好实际 ToolRuntimeScope 路径，但 None 仍沿用旧的 no-op，裸 HostForkRuntime 因而可跳过停止准入。现在明确拒绝；用真实 InstallRun、终态订阅与未完成 Source 验证，没有替缺失能力补一个空实现。 |

归因另核对了历史：R6B-02 的主仓重复 close 在原审计基线 `8f7f9be77` 已存在（源自 `9e81ebfb07 / eec8564a7c`），库 `46d9c07 / 0.5.2` 的构造失败也已关闭连接，故不是后续升级改变契约造成。R6B-03 是前轮修复漏封的前提，不能冒称原修复已覆盖所有构造方式；没有证据表明它让修复前已有的保证发生回退。两者均不归给本轮新增逻辑，也不据此推定独立 Ownership 库有错。

## 实现与旧路径退出

- 轮次范围之下再分 runtime 子范围；lease 同时保存确切 run 和本次 incarnation。DevOps 终态携带 AdmittedWork，不再只传 child SessionId。冷进程即使没有任何已知 PTY，也要持久停止该轮准入；没有物理绑定的预留继续阻止完成。
- 模型工具沿现有 Host 的 provider-run → exact physical execution 映射，再查询 canonical Accepted/Started；捕获确切 LogicalRunId。复用现有 managed-chat 契约，不增加第二份 ProviderStarted，不读取 Session 当前 execution 来补身份。当前 authority 只用来否决旧轮新开，不用来给旧调用换轮次。
- 资源身份由 owner、run、provider run 和 tool call 的完整编码摘要生成。重复调用换进程也不能换一个随机 PTY id 获得第二次启动。只有首次确认的 Reserve 可调用物理 Fork；Conflict、Unknown 和已提交请求的重放不授予启动许可。
- 每轮终态不再调用会话级 CloseOwnedPtys；只关闭该轮。detach 只关闭本 runtime 的子范围，旧 capability 失效；ToolRuntimeScope 可在同轮合法创建新 runtime，另一进程与真正接纳的下一轮继续可用。旧 capability 不能查询或写入下一轮的 PTY。
- 保留底层无 journal 的物理/受控端口探针；模型工具没有无 canonical execution 的准入路径。未新建状态机、服务层、重试调度器或库 API。

## 验收证据

正式锚点为 `WHAT[managed-session-lifecycle-025]`，文件 `requirements/managed-session-lifecycle/tests/025.test.mjs`；三个新增 worker/support 文件实际创建两个 OS 进程，共用同一个 Git commonDir 下的 work.sqlite。

| 范围 | 证据及限制 |
|---|---|
| 修复前反例 | `/private/tmp/r6b-baseline.log`，2 项普通失败；都实际观察到旧轮再次开 PTY，而非缺失导出或编译失败。 |
| 同 revision 仲裁 | 两进程各自生成候选，在 SQLite BEGIN IMMEDIATE 前暂停；核对所读 revision 相等后分别让 Reserve/Drain 先提交。败方不 Spawn。两个不同 Reserve 也只有一个首次许可；先 Reserve 时，后续已确认 Drain 保留外国实例，直到生产者 Ended 后新的进程才完成结算。 |
| 不确定与重复 | Reserve、Drain 各覆盖 COMMIT 前/后丢回执；Unknown 不能假报终态。已提交未知预留保持 blocker，重放不再启动。重复 tool call 在 Ended 前后都不重复 Spawn。 |
| 隔离与真实接线 | 同轮的 runtime detach、未知 provider、后续 assistant run、真正下一轮的旧 callback/capability、固定 DevOps 保留复用、冷旧资源、冷空轮次及缺失 drain 能力都有正反例。Node worker 使用真实 ToolRuntimeScope/终态/持久组合，物理命令端口受控。 |
| 真实 PTY | 两个 Bun 进程通过同一个 per-run Host 实现和生产 bun-pty 后端验证停止先行与预留先行。持有真实 PID 的目标仍在时，另一进程拒绝完成；原生产者关闭并证明 Ended、确切 PID 回收后才能完成。Bun 探针直接接 typed run capability，provider 身份查询另由 Node 真实工具组合验证；不将两者写成完整 OpenCode Host 的跨进程验收。 |

generation 516 的先行回归：归属链 **509 / 481 pass / 0 fail / 5 skip / 23 TODO**；provider/managed-chat/能力链 **430 / 401 / 0 / 11 / 18**；Process **215 / 210 / 0 / 0 / 5**。之后补齐缺失 drain 拒绝和冷空轮次两项；generation 517 的这两项 **2/2**。

最终生产候选 clean **518**，174 surfaces / 837 modules，`r6b-build-clean.log`，exit 0。冻结输入期间完成以下不重叠回归：

| 套件 | total / pass / 普通 fail / skip / TODO | 日志 |
|---|---|---|
| Work、managed-session、session-ontology、相关 delegation/recovery/horizon | **511 / 483 / 0 / 5 / 23** | `/private/tmp/r6b-ownership-clean.log` |
| managed-chat、execution-model-routing、capability-enforcement | **430 / 401 / 0 / 11 / 18** | `/private/tmp/r6b-provider-clean.log` |
| Process | **215 / 210 / 0 / 0 / 5** | `/private/tmp/r6b-process-clean.log` |
| 不重叠合计 | **1156 / 1094 / 0 / 16 / 46** | 不将 TODO 的失败断言或 skip 算作通过 |

其中正式 025 覆盖全部新增仲裁、冷路径、隔离和物理口；既有真实监管、未知 Ended、共享端口及终态回调回归继续通过。format/check、`git diff --check` 均 exit 0；`r6b-format-final.log`、`r6b-check-final.log`。没有使用 dotnet build。

精确包 **2074 members / 13,222,190 bytes**，SHA-256 **`308c1d002404260e66a5ebbcc8040b7852eebf5b922aa280ceb611f34107144c`**。verifyPackage 的真实打包/解包、隔离 npm ci、默认入口消费通过；同一 tarball 再隔离安装，包外普通 Bun 和已安装的 OpenCode 可执行文件均通过 TTY/输入/resize、原生退出状态与后代排空证明。日志 `/private/tmp/r6b-package.log`，脚本 `/private/tmp/r6b-package-proof.mjs`。这项包内物理证明沿用原公开 Surface，不冒充 per-run OpenCode 全链验收。

复验命令：

```sh
node scripts/build.mjs --clean
TMPDIR=/private/tmp node --test --test-concurrency=4 requirements/work/tests/*.test.mjs requirements/managed-session-lifecycle/tests/*.test.mjs requirements/session-ontology/tests/*.test.mjs requirements/crash-reconciliation/tests/020.test.mjs requirements/delegation/tests/{003,004,006,024,026,027,032}.test.mjs requirements/participant-horizon/tests/{009,010,011}.test.mjs
TMPDIR=/private/tmp node --test --test-concurrency=4 requirements/managed-chat-execution/tests/*.test.mjs requirements/execution-model-routing/tests/*.test.mjs requirements/capability-enforcement/tests/*.test.mjs
TMPDIR=/private/tmp node --test --test-concurrency=4 requirements/process-execution/tests/*.test.mjs
npm run format:check
npm run check
TMPDIR=/private/tmp node /private/tmp/r6b-package-proof.mjs
```

真实进程/PTY 套件在允许观察本次测试进程的环境执行；无修改主仓或库的运行中 Host。两仓库基线及库工作区重新核对；独立库仍为 `b6984d9 / 0.6.2`，无改动。

## 试制错误、验收误判与失败记录

- 本轮候选曾误把每个 assistant ProviderRun 都当作独立 ProviderStarted。对照 managed-chat [003] 后已撤销该索引及事实限制，补后续 assistant 的正式正控；没有发布或改变原事实结构。
- 新增的角色比对曾误拒绝底层 Manager owner 的受控 PTY 探针；已移除不属于 ownership 的角色判断，真实工具角色门禁照旧。Task.FromException 的 Fable 发射缺失、类型标注和新分支门禁也在交付前修正；失败日志 `r6b-build*.log`、`r6b-check*.log` 保留。
- 双 Reserve 首版屏障让 SQLite 初始化互相竞争，未保证候选抵达 CAS；原始失败与诊断保留在 `r6b-two-reserve*.log`。正式版本先让一个候选暂停，再让第二个读取同一 head，两项操作在提交前均未完成；不宣称两个并发命令一定都成功，不自动重试任何启动许可。
- detach 后的新工具请求可以取得新 runtime，首版把它误当作旧 capability 复活；正式测试分别验证两者。冷空轮次的 Drain 状态为 Draining，阻止新准入，不是自动 CloseScope；已改为验证实际协议及后到 Reserve 被拒绝。这两项是测试判断修正，不是产品回退。
- 真实退出后 PID 可短暂处于 zombie。正式物理验收只接受已消失或 Z 状态，再等待该确切 PID 回收，不把存活进程当作通过。没有用 PID 消失替代 Ownership 的确切 Ended。
- 沙箱首轮 provider 链的 capability-019 因 `spawnSync ps EPERM` 失败；开启本次测试所需进程观察权限后正式完整链通过。`r6b-provider-chain.log` 保留失败，不覆盖为通过。已有 TODO 内的断言失败仍是未闭合缺口。

## 剩余边界

R6-C 的跨重启物理接管尚未实现；新进程不能凭 PID、名称、空快照或历史回执接管实例。没有绑定的已提交未知预留仍阻止完成，不能把它从账上抹掉。主动脱离控制终端的守护进程、Trace、原 O-01/GAP-133 整体、Process GAP-091/092 及其他旧 TODO 均未销项。

以上为 `5a9e51e44` 的作者施工与自检记录，当时看板进入 REVIEW。后续独立复审见下节；未部署线上 Host。

## 独立复审与修正（2026-10-16）

从干净的 `5a9e51e44` 出发，三名非作者分别复审准入与 CAS（`r6b_admission_review`）、Unknown 与生命周期（`r6b_unknown_review`）、AGENTS.md、组合和证据（`r6b_requirements_review`），对照修复前 `c659f9b33`，独立库仍为干净的 `b6984d9 / 0.6.2`。

| 编号与归因 | 反例、修正与验收边界 |
|---|---|
| R10-01 / P1，本轮 R6-B 新增回退 | 本地已有 PTY 时，新增的 openRun/Drain 先抛 Unknown 或 Conflict，跳过 TERM/等待退出。独立真实 Bun 反例观察到终态后的确切 PID 仍为 `S+ sleep`；同 revision 的真实双进程竞争也留下本地和外国两个 blocker。现在分别完成停止准入与本地清理两项义务，保留第一个停止错误，继续关闭确切本轮本地实例，仍拒绝完成；外国及无绑定 blocker 不被删除。 |
| R10-02 / P2，本轮 R6-B 新增回退 | 初始化失败且首次 native close 抛错时，删除 Host 外层回收使连接遗留；Node/Bun 的 Work、Ephemeral、Ownership 三入口均重现，`c659f9b33` 原算法可再次关闭。现在只有构造失败时再尝试回收原连接，并保留构造抛出的错误；构造成功后仍由 Journal 关闭。验证原生连接确已关闭和成功关闭一次，不把调用次数当资源安全证明。永久 native close 失败仍不能保证释放，也没有声称修复库构造时原有的错误取舍。 |
| R10-03 / P1，既有恢复出口遗漏 | 启动恢复直接写 ChildWorkVoided，未停止旧轮准入。两个真实进程中，恢复方 canonical active 已为 null，旧进程仍由过期快照新开 PTY。现在恢复算法要求显式的 admitted-work drain 能力，Load 组合用相同 commonDir 的冷排空入口；确认停止且无 blocker 才可作废。不能仅刷新快照，刷新与 Reserve 之间仍会竞争。没有因此添加物理接管。 |

R10-01/02 由本轮修复引入，不能归给原拆分；R10-03 是既有生产恢复出口未被本轮覆盖。复审未把以下候选判为新缺陷：合法 DevOps 不能 Fission；生产 journal 与 workspace 同源；既有 ownedWorkFailure 保留会继续暴露旧错，但重复终态仍可完成真实 run；后者不等于终态永不可恢复。新增测试初稿误把 Bun 对已闭连接的 close 正常返回算作第二次实际关闭，非作者在候选复查中指出，已改用原生 SELECT 1 观测开放到关闭的转变；这是测试判断修正，不是产品回退。

修复前正式反例：本地 Unknown/Conflict **0/3**，`/private/tmp/r6b-review-drain-red.log`；真实 Bun 本地 PTY **0/2**，`r6b-review-real-drain-red.log`；Node/Bun 构造清理 **0/2**（各在首个 close-before 反例处失败，三入口另有独立反例），`r6b-review-journal-red.log`；恢复出口 **0/4**，`r6b-review-void-red.log`。独立探针另有 `r6b-review-local-drain-real.mjs`、`r6b-review-drain-conflict.mjs`、`r10-sqlite-init-cleanup*.mjs` 和 `r6b-review-voided-admission.mjs`。原候选完整 025 **81/81** 仍通过，`r6b-review-baseline-025.log`，说明原测试遗漏了上述切点，不能用既有全绿反驳反例。

候选 generation 522 已编译，三位非作者分别确认：准入/CAS/恢复 **10/10**（`r6b-review-admission-fixed.log`），原 void 探针恢复后旧进程 **零 Spawn**（`r6b-review-voided-admission-fixed.log`）；真实 Bun 的 Drain Unknown 前后、日志初始化失败及双进程 Conflict 均完成确切本地回收并保留失败/外国 blocker（`r6b-review-independent-{real,init,conflict}-clean.log`）；Node/Bun 三构造入口原生连接均关闭（`r10-sqlite-init-cleanup{-bun}-fixed.log`）。永久 close 失败的六场景实证仍保留原错误并拒绝命令，但连接未释放，临时注入解除后由测试清理；`r10-sqlite-permanent-close-{node,bun}.log`。

首轮 check 指出新增分支嵌套与测试 deep-dist import，已展平并改用注册的 Lifecycle Surface，没有放宽门禁。generation 522 正式 focused 为 **117 / 110 pass / 2 fail / 2 skip / 3 TODO**；两个普通失败来自新构造测试误要求 Surface.encodeOpening 抛错，该公开入口实际把失败返回为精确 `REFUSE`。已按原契约核对返回的拒绝与原错误，其他入口仍检查同一错误对象；失败日志 `r6b-review-focused.log` 与 `r6b-review-check.log` 保留，不计产品回退或改产品迁就测试。

最终 clean **523**，174 surfaces / 837 modules；`/private/tmp/r6b-review-build-clean.log`，exit 0。生产源码与非作者复验的 522 相同。修正测试 oracle 后，非作者在 523 另复验 Work-033 **2/2**、Node/Bun 三入口三故障共 **18 场景**，`r10-033-independent-final.log`。最终冻结输入期间的完整相关回归：

| 套件 | total / pass / 普通 fail / skip / TODO | 日志 |
|---|---|---|
| Work、managed-session、session-ontology、crash 018/020 与相关 delegation/horizon | **530 / 501 / 0 / 5 / 24** | `/private/tmp/r6b-review-ownership-clean.log` |
| managed-chat、execution-model-routing、capability-enforcement | **430 / 401 / 0 / 11 / 18** | `/private/tmp/r6b-review-provider-clean.log` |
| Process | **215 / 210 / 0 / 0 / 5** | `/private/tmp/r6b-review-process-clean.log` |
| 不重叠合计 | **1175 / 1112 / 0 / 16 / 47** | skip、TODO 及 TODO 内失败断言未算通过 |

正式 025 现在 **90/90**，覆盖新增恢复出口、本地 Unknown/Conflict 和两个真实 Bun 故障切点。format/check exit 0（`r6b-review-format-check.log`、`r6b-review-check-2.log`），`git diff --check` 通过；没有使用 dotnet build、修改独立库、vendor 或放宽门禁。

精确包为 `.fable-build/verify-logs/r6b-review-package/wanxiangshu-0.9.0.tgz`，**2074 members / 13,224,524 bytes**，SHA-256 **`edc4c842c8c5fd8c73f925f3fd6ea3c56d6db5d0e5a780ba381f3f319c975bbd`**。真实 pack/解包、隔离 npm ci、默认入口，以及同一包在包外普通 Bun 和已安装 OpenCode 可执行文件中的 PTY/输入/resize、目标原生退出及后代排空均通过；`/private/tmp/r6b-review-package.log`，脚本 `r6b-review-package-proof.mjs`。包物理证明仍沿用公开 Surface，不扩大为完整 OpenCode 双进程 R6-B 全链。

复验沿用上文命令，但归属链的 crash 文件增加 `requirements/crash-reconciliation/tests/{018,020}.test.mjs`；包命令为 `TMPDIR=/private/tmp node /private/tmp/r6b-review-package-proof.mjs`。所有正式新增测试沿用编号文件和 WHAT 锚点，构造探针只使用已注册 Lifecycle Surface。非作者最终未发现声明范围内仍未修正的新回退；R6-B 进入 DONE。

恢复只接入当前生产 Load 出口；冷入口不取得本地物理绑定，任何 blocker 都拒绝 void，不能据此断言整个激活阶段尚无 ToolRuntimeScope。Historical 无 AdmittedWork，保留历史隔离边界；尚未接回生产的 HostForkRestart/ChildRecoveryWorkflow 在接回前同样须提供停止准入能力。R6-C、完整 Bootstrap 排序和强杀接管、主动脱组、Trace 和 O-01/GAP-133 整体仍未闭合。主仓本提交正常交付 master，不强推；验证通过不等于部署，未改运行中的线上 Host。
