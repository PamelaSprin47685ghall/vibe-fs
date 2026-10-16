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

本轮为作者施工与自检，尚未进行非作者独立复审；看板已进入 REVIEW，不能写成非作者复审通过。主仓本提交按门禁正常提交并推送 master，不强推；未部署线上 Host。
