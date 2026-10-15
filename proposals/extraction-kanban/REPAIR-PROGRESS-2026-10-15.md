# 四模块修复记录

依据：[源码问题报告与修复计划](SOURCE-AUDIT-2026-10-15.md)。状态和认领只在 [BOARD.md](BOARD.md#source-audit-repairs) 维护。本文件记录实际修改、验收证据和剩余边界；未执行的测试不写通过。

## 本批交付

主仓代码提交 `ffe028797`（包含前序独立修复），库交付分支 [codex/repair-four-modules-20261015](https://github.com/dyx13/wanxiangli/tree/codex/repair-four-modules-20261015) 已推送至 `3ad5b88`。主仓固定 `0.6.0` tarball，不更改独立库远端 `main`。这里只报告代码与验证，没有部署到运行中的宿主。

| 问题 | 本批结果 | 剩余边界 |
|---|---|---|
| P-01 | 完整 Capture → Spool；写盘失败显式失败并清理 | 默认基础 ProcessSession 仍按原契约留尾 |
| P-02 | 所选非 PTY 执行使用存活组长；目标先退出后的取消/期限反例转绿 | 脱组后代、宿主崩溃接管及任意全树清除不在此承诺内 |
| P-03 | 相对 PATH 按目标 cwd 解析，原生 spawn 对照通过 | 无新增隐式环境继承 |
| T-02 / T-03 | 完整 Opening、追加晚绑定、固定 cut 与恢复通过 | T-01 原件权威仍未迁移；旧外存 raw cut 的跨协议兼容不保证 |
| O-01 | 生产持久 Reserve、首次 Committed 才启动、统一 blocker、冷读取已接通 | 部分完成；跨重启物理接管、跨进程每轮代际准入尚未承接 |
| O-02 / O-03 / O-04 | 不提前 Ended；同步完成和丢回执精确重放；查询失败不吞空；Applied 不伪装 Committed | 未观察的旧物理实例保持阻塞，不猜测释放 |
| E-01 / E-02 | Proxy 实际重入反例；生产 bun-pty 真实 TERM→等待→KILL→exit 通过 | GAP-133 整体仍 PARTIAL；其他既有 TODO 未关闭 |

最终构建 generation 467；format:check、check、真实主项目安装包消费均 exit 0。相关主仓套件共 741 tests：699 pass / 0 fail / 6 skip / 36 TODO / 0 cancelled；TODO 和 skip 不计为已证明。详细命令、分组和原始失败记录在下文。

## 本轮基线与隔离

- 主仓起点 `b440f4f5282f703c0bc2b68da7b7622c0dc0d532`，`master` 与 `origin/master` 一致，工作区干净。
- 独立库起点 `46d9c072af50389d069709e847d5772af3ec160a / 0.5.2`，工作区干净。
- Process：`codex/repair-process-p1`，主仓 `/private/tmp/vibe-fs-repair-process`，独立库 `/private/tmp/wanxiangli-repair-process`。
- Trace：`codex/repair-trace-p1`，主仓 `/private/tmp/vibe-fs-repair-trace`，独立库 `/private/tmp/wanxiangli-repair-trace`。
- Ownership 物理边界：`codex/repair-ownership-p1`，主仓 `/private/tmp/vibe-fs-repair-ownership`。组合层由 root 在主工作区串行修改、集成。
- 各树自行安装依赖并拥有产物。包版本、tarball、锁、看板及主线集成由 root 串行处理；不同时构建同一输出目录。

## 工作包与验收范围

### Process：P-01 优先，P-02 不放宽旧 PGID 规则

保留基础留尾接口，增加显式、有界的无损输出消费能力，由主仓消费字节并写 Spool。先补大双路输出、慢消费者、EOF 与失败反例；主仓停止用轮询 tail 承接完整输出。相对 PATH 修复可独立验收。Draining 的监管能力必须单独证明可靠实例身份，不能因 P-01 修复而宣称 P-02 关闭。

### Trace：T-02 / T-03 的完整 Opening 与稳定 cut

保留原首条 Opening。大材料用确认消息部件分片，通过只追加的 Opening 绑定记录指认完整材料；晚绑定不重建同名轨迹，旧 through 不读取后来绑定。先补大材料异尾、UTF-8 边界、晚绑定后旧 cut 和恢复测试。T-01 的原件权威迁移仍独立保留，不把局部修复算成整卡完成。

### Ownership：O-02 / O-04 优先，同时修同步完成窗口

取消或解除名称绑定不再直接写 Ended；Spawn 前准备预留与请求关联，不提前声明 Started，真实同步 Ended 可以结算。查询错误显式失败。内存 Applied 与持久 Committed 分开；纯本进程 callback 仅取得内存许可。生产 PTY 持久预留与跨进程恢复须另附真实接线证据，不能只用库组合测试代替。

## 验收记录

以下保留中间候选的实际记录及当时待验范围。最终精确包与扩大复验另列，不把中间通过数拼成整卡已完成。

### Ownership 组合层的第一组反例

- `env TMPDIR=/private/tmp node --test --test-name-pattern='process-local reservation|unknown ownership scope|durable blocker queries' requirements/work/tests/009.test.mjs`：原实现 0 pass / 3 fail，exit 1。分别命中 Applied 被改为 Committed、缺 scope 被吞为空、缺 durable owner 被吞为空；日志 `/private/tmp/cut04-r1-root-red.log`。
- 首次修复后 `node scripts/build.mjs`：exit 0，generation 464，Fable focused impact 1530 items；日志 `/private/tmp/cut04-r1-root-build.log`。
- 该中间候选 `env TMPDIR=/private/tmp node --test requirements/work/tests/009.test.mjs`：6 tests / 6 pass / 0 fail / 0 TODO，exit 0；日志 `/private/tmp/cut04-r1-root-green.log`。随后新增的生产持久接线与 factory 接口尚待统一构建，不引用这次运行替它们验收。

### Trace 的库内协议与主仓候选

- 原库加 OpeningBound 反例：44 tests / 40 pass / 4 fail，日志 `/private/tmp/trace-opening-red.log`。
- 修复后 `node scripts/build.mjs trace` 与 `node --test src/trace/tests/*.test.mjs`：55 tests / 55 pass / 0 fail / 0 skip / 0 TODO，日志 `/private/tmp/trace-opening-green.log`。
- 库候选 `4e83d4307f7e9eb6fd75451e9479e7ffa92fb0e6`，主仓候选 `d1abbb51ffbda01598880da13ed4d3131c81f620`，已由 root 串行集入两仓。主仓使用新包的构建及 semantic-trace / 相邻取材回归尚待执行。
- 未发现生产持久保存新库 raw cut / Bridge records 的消费者；旧 journal 保存旧事实和 blob refs。小且首条 Opening 保留旧编码；不保证旧坏版本被外部私存的 raw cut 自动跨协议升级兼容。T-01 原件权威迁移仍未闭合。

### 原报告的独立套件验证缺口

- Journal 竞争测试在发送启动消息前登记 worker 退出等待，双方正常退出后才重开；新增真实构造锁竞争并核对被拒连接关闭。没有修改 `busy_timeout=0` 或 CAS 规则。
- Process 的 cwd 比较物理目录身份，真实子程序环境与同输入原生 spawn 对照；另检查传给 spawn 的精确环境及宿主哨兵变量不泄漏，没有忽略任意多余键或跳过 macOS。
- `node scripts/build.mjs eventstore process` 与 `node --test src/eventstore/tests/*.test.mjs src/process/tests/001.test.mjs`：37 tests / 37 pass / 0 fail / 0 skip / 0 TODO，exit 0；日志 `/private/tmp/validation-build.log`、`/private/tmp/validation-tests.log`；库提交 `6acb798`。完整组合验收仍待新包固定。

### Process 无损采集候选

- 库 `77acafa`（主线集成为 `490e1c0`）：默认 ProcessSession 保留原留尾契约；显式 Capture 构造提供单消费者 `takeOutput`，每块最多 64 KiB，原生 pipe 背压限制未消费积压；`discardOutput` 是显式放弃，不冒充采集完成。
- 库 process 002–006：35 / 35 通过。独立安装包在 Node/Bun 上逐通道核对 32 MiB 输出，TypeScript 正反声明检查通过。主仓桥接的双路各 64 MiB 输出摘要一致；消费者抛 ENOSPC 后记录 failure，再观察真实 close，日志 `/private/tmp/process-repair-main-bridge.log`。
- 主仓候选 `d4164b4a0`（主线集成为 `205ebbb61`）删除有损尾部轮询；落盘/采集失败进入 ExecutionFailed，清理部分 spool，discard 后迟到输出不能重新创建 spool。正式 process-execution 回归待新包统一构建。

### 独立库中间整合回归

- 输入为 Trace、Capture、验证缺口三项集成后的库 `490e1c0`，尚不包含新的监管器。
- `node scripts/build.mjs` 及源码报告列出的完整原有测试命令：286 tests / 285 pass / 0 fail / 1 skip / 0 TODO / 0 cancelled，exit 0。skip 是仅在 Linux 运行的 process 安装包拆除测试；日志 `/private/tmp/p1-library-interim-build.log`、`/private/tmp/p1-library-interim-tests.log`。
- 原库仓只有读取权限，已在用户账号建立公开 fork [dyx13/wanxiangli](https://github.com/dyx13/wanxiangli)。后续源码推送到该 fork；原仓保留为 upstream。当前仍是集成候选，不把本次中间运行替代最终 tarball 和主仓验收。

### Process 监管与真实反例

- 主仓修复前运行 `node --test --test-name-pattern='terminates the group after the target exits' requirements/process-execution/tests/{004,006}.test.mjs`：2 tests / 0 pass / 2 fail。目标已实际退出，取消和硬期限都未终止同组孙进程；测试自行清理后代，日志 `/private/tmp/p02-host-red.log`。取消沿用既有异常契约，测试不要求它返回 ProcessCancelled 值。
- 独立 `SupervisedProcess` 保留真实组长身份；以基础 ProcessSession Capture 运行交付的 F# bundle，再以同组目标的启动、退出及输出关闭事实回答。目标退出与监管器退出分开，未观察目标退出时保持 UNKNOWN_TARGET_EXIT；基础对象仍拒绝使用旧 PGID。
- 监管库候选 `086ec780541dec32d3b94e31360d48e403cb9029`（主线 `4ec9bdc`）：Node 12/12；Bun 逐 001–005 共 12/12；strict TypeScript 与独立 Fable 构建通过。日志 `/private/tmp/supervision-node-green.log`、`/private/tmp/supervision-bun-001.log` 至 `005.log`、`/private/tmp/supervision-build-final.log`。
- Bun 曾在清组后卡住 Capture EOF。原生 probe 证实 readable 缓冲已空且 ended，但尚未发布 end；补一次零字节 read 推进真实 EOF，不伪造 close。修复 `892f0e6`（主线 `d6b45b0`），监管原反例转绿。新增 Process 转发/KILL 用例在旧版也可通过，只算回归覆盖，不冒称红绿证明。
- 主仓只在本实例 KILL 返回 Requested 且缺目标 exit 时，保留取消/超时结果；其余控制、输出、清理错误仍进入 ExecutionFailed。目标信号按本平台原生编号映射为 `128 + signal`，不再把任意信号都报告成 137。
- 此范围不包含 PTY、脱离进程组的后代、宿主崩溃后的接管或任意进程树清除证明。监管器意外死亡后的 Closed 不能证明目标已死。

### 精确交付候选

- 库源码 `a736920`，版本 `0.6.0`；固定包 `wanxiangli-0.6.0.tgz`，SHA-256 `56c80e4bf63b999e47b021b22a6bcac3cc0d1054243d4b63a087efaf60e09b19`。主仓通过 vendor 与 package-lock 安装，未使用 npm link 或兄弟源码路径；旧 tarball 由 Git 历史保留。
- 同一库源码 `node scripts/build.mjs` 及完整源码/既有安装套件：299 tests / 298 pass / 0 fail / 1 skip / 0 TODO / 0 cancelled，exit 0；日志 `/private/tmp/p1-library-final-build.log`、`/private/tmp/p1-library-final-tests.log`。skip 仍为仅 Linux 的基础 process 安装包测试；新的监管独立安装验收另记，不用 skip 替代跨平台证明。
- 主仓旧生成缓存含已废弃 WorkspaceSnapshot 引用，会被源码扫描扫到；已将整个旧 `src/Wanxiangshu/.fable-build/output-compile` 移存 `/private/tmp/vibe-fs-stale-output-compile-20261015`，未修改或放宽门禁，未删除源码。主仓从当前输入重新构建后再报告门禁结果。
- 推送前 fetch 发现 fork 的 `origin/main` 已含 `81fef75`、`8422617` 两批新模块（0.7.0 / 0.8.0），与报告基线分叉。本轮不重写该 main，也不将这些未纳入本轮验收的模块合入 0.6.0；独立库交付分支为 `codex/repair-four-modules-20261015`，主仓仍正常快进推送 master。

### PTY 持久接线、退出及恢复

- 主仓候选 `ad3356b7bdb04f8a442c55dc825e9d9f802c96bd`（主线 `4b363f858`）统一生产 registry 为 `fork-pty:<session>`，根 scope 为该 session，运行实例使用唯一子 scope。默认真实后端缺 durable owner 时，在 Reserve 前拒绝；只有首次 Committed Reserved 允许调用 Fork。没有启动观察时保持 Reserved，不伪造 Started。
- 移除 Track/Untrack 及 Join 中的合成终结。同名绑定败方调用实际 ClosePty 并等退出；TERM 请求、空 Close 和未知 spawn/attach 异常均不结清。只有确定尚未调用 spawn 的后端加载失败，才可报告已知失败。
- lease 与 incarnation 在调用 Fork 前登记；同步 Complete 不丢失、不复活名称。真实 Ended 在提交回执前后丢失时保留原观察，以原请求重放；不补启动或重复完成通知。
- Applied 与 Committed 保持区别；查询缺 scope、坏历史和 durable owner 缺失显式失败。可选 durable 查询仅在完全没有记录时返回 None。冷父关系匹配 HandleId **及 ChildSessionId**，两父使用同名 engineer-1 不互相认领。
- 隔离树原反例 4 pass / 3 fail / 1 TODO（`/private/tmp/cut04-red-tests.log`）；修复后 managed-session-lifecycle 012/025、work 009/044 共 55/55、0 skip / TODO，`check.mjs` exit 0；日志 `/private/tmp/cut04-final-targeted-tests.log`、`/private/tmp/cut04-check.log`。这些是原锁包上的候选证据，主仓 0.6.0 的扩大复验单列。
- 边界：重启后只恢复持久阻塞，不接管没有本机精确 port 绑定的旧进程，不猜旧 PID、不重发启动。每轮 CloseOwned 的准入锁限于本进程；跨进程每轮代际 Drain/Reserve CAS、隐藏 Host-owned 后代的父级冷恢复，仍未证明。永久 detach 只 Drain 自己的实例 scope，不关闭继任实例的根范围。

### 精确包安装与主仓验收

- 库验收提交 `3ad5b88`：`WANXIANGLI_TEST_TARBALL=/private/tmp/wanxiangli-0.6.0.tgz TMPDIR=/private/tmp node --test scripts/tests/supervision-package.test.mjs`，5/5、0 skip / TODO，exit 0。strict TypeScript 正反用例、Node/Bun 各路 8 MiB 完整字节、目标先退出后的 TERM→KILL 均通过；删除无关模块及全部源码后仍可运行，删除 supervision 后基础 process 仍可运行。日志 `/private/tmp/p1-supervision-package-final.log`。此后只新增未打包测试文件，tarball 内容与 `a736920` 一致。
- 主仓 `node scripts/build.mjs`：最终 generation 467，1532 items、174 surfaces、836 modules，exit 0；日志 `/private/tmp/p1-main-final-build.log`。
- Trace 相邻回归：`TMPDIR=/private/tmp node --test requirements/semantic-trace/tests/*.test.mjs requirements/work-record/tests/{002,005,008,014}.test.mjs requirements/prefix-stability/tests/{009,012,014}.test.mjs`：102 tests / 93 pass / 0 fail / 1 skip / 8 TODO，exit 0；日志 `/private/tmp/p1-main-trace.log`。包括新 Opening、旧 cut、恢复和 Proxy 重入断言。原 GAP-108 等 TODO 不计为证明；直接 capture 保留指导后缀的已知差异未被本轮改写。
- 首轮扩大 Process 回归的真实取消、期限及信号状态反例已通过；其两处失败是旧后端文案、旧 ProcessSession 源码接线断言。Ownership 扩大回归暴露测试结束后的排空异常，静态门禁暴露 ProcessRunner 两处控制金字塔；该轮未全绿，后续修复结果见下一节。
- 首轮全仓格式检查另有 6 个原有文件失败，修复前与 `b440f4f52` 逐文件 diff 为空。已逐项手工调整 7 行缩进，独立提交 `fcba11928`；未批量重写源码。随后及最终异步清理补丁后的 `npm run format:check`、`npm run check` 均 exit 0（日志 `/private/tmp/p1-main-format.log`、`/private/tmp/p1-main-check.log`）。

### 最终扩大复验

- 测试清理异常根因是 ToolSurface 测试夹具同步 Dispose 丢弃 scope 的 detach Task，立即关闭/删除 journal；异步 Drain 因而遇到空历史。新 018 反例先红（返回 undefined），修复公共清理接口及全部调用方 await，生产严格 scope 查询未变。提交 `e4043fe`（主线 `37ec80033`）；隔离 17 文件 132 tests：122 pass / 0 fail / 3 skip / 7 TODO，日志 `/private/tmp/cut04-scope-regression-red.log`、`/private/tmp/cut04-scope-green.log`。
- 最终 `TMPDIR=/private/tmp node --test requirements/process-execution/tests/*.test.mjs`：192 tests / 187 pass / 0 fail / 0 skip / 5 TODO / 0 cancelled，exit 0；`/private/tmp/p1-main-process.log`。004/006 真正观察目标先退出、同组持 pipe 后代终止；004 区分目标 TERM/INT/KILL 状态；005/011 覆盖完整输出、失败和相对 PATH。原 GAP-092 总结果预算 TODO 仍会报出预期缺口，不算通过。
- 最终 `TMPDIR=/private/tmp node --test requirements/work/tests/*.test.mjs requirements/managed-session-lifecycle/tests/*.test.mjs requirements/session-ontology/tests/*.test.mjs requirements/crash-reconciliation/tests/020.test.mjs requirements/delegation/tests/{003,004,006,024,026,027,032}.test.mjs requirements/participant-horizon/tests/{009,010,011}.test.mjs`：447 tests / 419 pass / 0 fail / 5 skip / 23 TODO / 0 cancelled，exit 0；`/private/tmp/p1-main-ownership.log`。没有测试结束后的 unhandled rejection；025 包含实际 Bun PTY，009/044 包含 strict scope、持久预留及冷父关系。
- Trace 分组仍为上述 102 / 93 pass / 0 fail / 1 skip / 8 TODO，同一精确库；后续修改未改 Trace 行为。三个分组没有重叠文件，总计为本批交付栏所列数。
- 最终 `npm run format:check`、`npm run check` 均 exit 0；前者实际执行、后者实际执行，非缓存跳过。静态 shard-edge-audit 的既有 report-only 提示不作测试结果。`git diff --check` 通过。
- `npm_config_cache=/private/tmp/p1-npm-cache node scripts/verify-package.mjs`：exit 0，generation 467，2073 members，13,134,990 bytes；主项目 tarball SHA-256 `003f8fb5ce71e17c3779d88d8b87fc2311439da194925cf9af566343561f10af`。实际打包、解包并在不借仓内 node_modules 的外部消费者中通过；日志 `/private/tmp/p1-main-package.log`。这是安装包证明，不是生产宿主部署证明。

## T-01 / D-02：仍未闭合的 P1 与下一工作包

本轮没有把描述符桥改名后算作原件迁移，也没有为通过验收删掉旧语义。以下是下一工作包的约束，不表示每项都有新缺陷。

| 义务 | 当前事实及接线点 | 验收边界 |
|---|---|---|
| 推理、工具及省略材料 | Trace/Capture、Materialization；正文和推理都参与现有视图 | 库保存真实字节与语义种类；Y、prefix、WorkRecord 使用同一原件视图，不能改成最后一条 assistant text |
| typed retry 排除 | Capture 按 ProviderRetryAttempt 排除正文，保留 Host 行坐标 | 在原件提交前过滤；不进入 Trace/Y/WorkRecord/prefix digest，不按文本猜测 |
| 并发捕获 | 同 session 在分配游标前串行化 | 保留异步队列；库同步重入 guard 不能替代它；失败不推进已提交 head |
| 真实身份与 generation | ProviderWireCapture、Capture；合法数据可缺 ProviderRun，也无统一完成事实 | 保留物理身份、重锚 generation、stable insertion、已有覆盖游标；缺失就明确缺失，不能每 part 虚构 Completed run |
| Terminal 前沿 | Projection、TerminalReporter、WorkRecord | 按 ProviderRun 绑定既定前沿，可含推理；同材料幂等、冲突拒绝，不替换为库 statement 的不同语义 |
| Fission / Invocation / Resume | Fission Host、Fork/RunLifecycle StartCursor | 保留原半开区间及业务归属；WHAT[011]/[012] 的 GAP-101 TODO 不被存储迁移冒充关闭 |
| 历史与载体 | 正文已在事件 Payloads 内嵌；JournalIntegration 目前丢弃该上下文 | 相同事实形状/含义可以原样重放，仅换派生 owner；改变事实词汇或增加原历史没有的来源/执行断言，按 durable-events[026] 离线导入新 event_type |

下一包 **T01：原件接管及消费者切换**：先冻结库侧“已确认材料＋明确终结绑定”协议；在 JournalIntegration 的 envelope 接口保留 payload 上下文，由同步 fold 把已校验字节交给库。统一 EventStore 继续作为唯一持久载体，不新建旁挂数据库。然后保持已发布语义游标，切换 Materialization、WorkRecord 等实际消费者，删除 Model 中描述符桥及临时原文重捕获，最后用真实 journal 验收大正文、推理、工具、Terminal、并发、retry、重锚与旧保存范围。缺 payload、摘要失配、冲突必须拒绝且不推进 head。

旧生产 journal 未发现持久保存桥的 raw cut；外部另存的旧 raw cut 不保证在更换内部协议后继续使用同 ID/游标。这一边界须在协议中显式写明。此包尚未实现，T-01 保持未关闭。
