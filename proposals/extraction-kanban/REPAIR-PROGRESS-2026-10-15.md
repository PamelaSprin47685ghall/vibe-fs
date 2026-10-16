# 四模块修复记录

依据：[源码问题报告与修复计划](SOURCE-AUDIT-2026-10-15.md)。状态和认领只在 [BOARD.md](BOARD.md#source-audit-repairs) 维护。本文件记录实际修改、验收证据和剩余边界；未执行的测试不写通过。

## 第六批：剩余问题分级与小项直接修复（2026-10-16）

输入主仓 `c1ed871be`、库 `eef4825`，两仓起点干净。用户要求简单的直接修，较难的先定计划。[剩余问题和施工计划](REMAINING-REPAIRS-2026-10-16.md) 把四项 P1（后代监管、持久每轮准入、重启接管、Trace 原件迁移）分开，另登记 Process 编译边界、完整 wire 预算和 Bun 平台验收三个 P2 包；没有开始这些复杂包的生产实现，也不把 37 项 TODO 当成 37 个已确认缺陷。

已直接修 CUT-02-R6-V：库 Capture 006 错误注入夹具，提交 `b6984d9`。修复前 Bun 6 pass / 1 fail，停在 child.stdout；修复后从实际 stdout 的 read 取得原生流，再注入 error，验证生产监听器、失败结果、无假退出和无别名。Node Process/Supervision **69/69**、Bun Capture/Supervision **19/19**，日志 `/private/tmp/r6-node-process-green.log`、`r6-bun-capture-supervision-green.log`。仅改库测试和证明，不更换主仓锁/vendor，不重打 0.6.2。

第一次候选沿用 destroy(EIO)，Node 7/7，但 Bun 超时；原生 Bun 对照证实 stdout 已 destroyed 却无 error，故改为明确的原生 error 事件注入。它不是实际设备 I/O 故障。原测试 child 未捕获的失败和第一次候选的失败均保留，不写成产品 bug 或通过。

扩大 Bun Process/Supervision 为 **65 pass / 4 fail**，`r6-bun-process-green.log`，文件名不是成功标记。两条 env 失败和两条 stdin 断言单列 CUT-02-R6-C。原生 Node/Bun 对照分别证明 async/sync 的特殊 env 键不同、named spawn 拦截不成立、原生写回调 EPIPE/success 不同；见 `r6-native-{node,bun}.json`。未改这四条断言、未转 TODO/skip，也未宣称 Bun 全套通过。原生探针初版进程组清理失败；最终仅结束自己持有的子进程后得到上述结果，失败运行不计证据。

主仓本轮只改计划、看板与证据，上一轮 790 项结果仍归原提交；没有借文档更新宣称重跑主仓行为套件。`node scripts/build.mjs` 文档候选构建 exit 0，174 surfaces / 836 modules 链接通过，`/private/tmp/r6-main-doc-build.log`；diff whitespace 核对通过。T-01/O-01/GAP-091/092/133 保持未闭合，真实 leader-exit TODO 保留。库修改正常推送既有交付分支，主仓文档正常推送 master；未部署运行中的 Host。

<a id="regression-review-r5"></a>
## 第五批：全部修改的双版独立复审（2026-10-16）

起点为干净主仓 `86c0c9b8c`、库 `eef4825 / 0.6.2`。修复前为主仓 `7d8feff03`、库 `46d9c07 / 0.5.2`；覆盖全部 14 / 10 个后续提交。三名非作者只读审查：`regression_process_r5`、`regression_ownership_r5`、`regression_trace_journal_r5`。历史主仓使用原依赖独立构建成功，不能以当前产物冒充历史行为。详细归因和双版结果保存在[独立回归报告](REGRESSION-REVIEW-2026-10-16.md)。

| 工作包 | 本轮实现 | 验收和剩余边界 |
|---|---|---|
| CUT-02-R5 / P-04 | Spool 创建者在首次写失败时删除路径并重抛，不等 collector 得到句柄 | 新 writeFileSync 先实写再抛错用例先红：2 项 1 pass / 1 fail，append 对照通过；独立复验残留 true→false。未改变输出预算或摘要语义 |
| CUT-04-R5 / O-13、O-14 | CloseAll 逐项尝试后报首错；active ID 在 Spawn 前拒绝覆盖，见证退出后允许复用 | 非作者真实 fd 反例两路通过，未知 ID 保留、无假完成；不同 ID 健康 shell 已死；同 ID 第二次 Spawn=0。旧覆盖测试已纠正，WHAT003 明确未收尾物理实例不能覆盖 |
| CUT-04-R5 / O-12 | 真正 installRun 订阅和首 fork/reuse 均使用同一 preparation；有效性/exact token 在前，drain/物化在后，失败可重投；冷无绑定时读持久 blocker，拒绝假完成 | 非作者 held/retry/cancel/replacement/foreign Failed 及真实 Bun 通过；generation 490 冷 guard 四条独立对照通过。不是完整后代监管或重启接管 |

本轮施工失败如实保留：`r5-main-build.log` 首候选签名/返回类型错误；`r5-main-check.log` 嵌套控制门禁 4 项；`r5-main-ownership-tests.log` 新增矩阵因错误静态导出失败 6 项。没有把这些运行写成通过。fixture 还经非作者指出 DrainOwnedWork 会永久封准入，已改 AwaitObservedWork，7 项真实回调矩阵随后全部通过（`r5-terminal-matrix.log`）。

首个可运行候选 generation **489**，focused **1532 项 / 174 surfaces / 836 modules**。Process **196 tests / 191 pass / 0 fail / 5 TODO**；日志 `r5-main-process-tests.log`，含真实进程组观察，沙箱外运行避免 `/bin/ps` EPERM。`r5-main-build-fixed.log`。此后只做控制流展开、冷无绑定防错及文档收尾，最终候选须重新构建和验证，不能沿用此处旧候选身份。

当前库非作者定向复验为 Trace/Journal 85/85、Ownership 57/57、Process/Capture/Supervision 69/69、安装 Supervision/拆除增强 5/5；主 Trace integration 消费者 1/1。双版比较来自另行隔离安装的 exact 包探针，不是两版都运行上述新增套件。0.6.2 vendor 及库源码未修改，精确包 SHA-256 仍为 `d8909bebb92b01e0cd0cfa28cfdc14d0d1d89f467b7afe5e94a99efd96b0038e`。

最终源码构建 **generation 490**，focused **1532 项 / 174 surfaces / 836 modules**，`r5-final-build-fixed.log`。此前冷 fixture 的 Task 类型错误在 `r5-final-build.log` 留痕；拍快照改在 Dispose 前，completion 由真实 task continuation 观察，没有使用 Promise 的同步完成属性。`r5-delivery-format.log`、`r5-delivery-check.log` 均 exit 0，未放宽任何门禁。

| 完整相关需求分组 | tests / pass / fail / skip / TODO | 日志（均在 `/private/tmp/`，exit 0） |
|---|---|---|
| Work / lifecycle / session-ontology 及相邻归属链 | 492 / 463 / 0 / 5 / 24 | `r5-final-ownership-tests.log` |
| process-execution 全部 | 196 / 191 / 0 / 0 / 5 | `r5-final-process-tests.log` |
| Trace / WorkRecord / Prefix 相邻链 | 102 / 93 / 0 / 1 / 8 | `r5-final-trace-tests.log` |
| 不重叠文件合计 | **790 / 747 / 0 / 6 / 37** | 新增十项全通过；既有 TODO / skip 不算通过 |

复跑命令与前四批相同，仅测试数量增加：

```sh
TMPDIR=/private/tmp node --test requirements/work/tests/*.test.mjs requirements/managed-session-lifecycle/tests/*.test.mjs requirements/session-ontology/tests/*.test.mjs requirements/crash-reconciliation/tests/020.test.mjs requirements/delegation/tests/{003,004,006,024,026,027,032}.test.mjs requirements/participant-horizon/tests/{009,010,011}.test.mjs
TMPDIR=/private/tmp node --test requirements/process-execution/tests/*.test.mjs
TMPDIR=/private/tmp node --test requirements/semantic-trace/tests/*.test.mjs requirements/work-record/tests/{002,005,008,014}.test.mjs requirements/prefix-stability/tests/{009,012,014}.test.mjs
```

非作者冷恢复反例在 generation 489：实际 Scope 结算成功，却留 `pty:unbound-cold-resource`。generation 490 同探针拒绝，Finished=false / InFlight=false / delivered=0，订阅和原 blocker 保留；无遗留 stream 的正控正常完成。仓内 coldDevOpsReturn 入口也分别核对一致，四条全部 exit 0。日志 `ownership-r5-o12-cold-{before,after,empty-after,surface-after,surface-empty}.log`；没有 OS 进程，不把它算物理接管。

文档收尾后构建 **generation 491**，174 surfaces / 836 modules 链接通过；`r5-delivery-build.log`。实际 `node scripts/verify-package.mjs` 打包、解包、隔离 npm ci 和消费者全部 exit 0：**2073 members / 13,153,599 bytes**，tarball SHA-256 `4463c91e1c308880c9a4e1f13b63e2bfbf29fa9039cf7db56a74def7459fe987`；`r5-delivery-package.log`。本轮全部审查者的最终结论及措辞修订已纳入报告，没有把当前 85 项当成两版都通过，也没有把旧坏 raw cut 的自动升级写成已证。

CUT-02-R5 / CUT-04-R5 按上述限定范围 DONE，随本提交正常快进推送 master。提交后重新核对 freshness、主包摘要和远端身份；不使用 force push。历史临时 checkout 在确认无源文件改动后移除，运行日志仍在 `/private/tmp/`；没有把临时探针当成交付门禁。T-01、O-01 及 GAP-133 未闭合，没有部署到运行中的 Host。

<a id="terminal-cancellation-review"></a>
## 第四批：完全取消后的 PTY 准入

基线 `7d12e3462`；库及精确 vendor 包仍为已独立验证的 `eef4825 / 0.6.2`，字节未变。O-11 是第三批收尾时追加确认的问题，见[报告](SOURCE-AUDIT-2026-10-15.md#terminal-cancellation-review)。不覆盖第三批原始运行证据。

`independent_pty_r2` 依据核心 mailbox 的永久 IsCancelled、Fork/Join 和 ToolRuntimeScope 重建行为，独立确认旧实例应拒绝新 PTY。其公开端口反例在无/有初始 PTY 两路均观察到取消后的实际新 Spawn 和 completion=0；失败取消后的晚到结束也留下订阅。日志 `/private/tmp/independent-pty-r4-cancel-contract-red.log`。

新增四项先红：**4 tests / 1 pass / 3 fail**。三个拒绝/收尾反例失败；真实 Journal 固定 DevOps 保留分支的复用与 completion=2 正控通过，`r2-review-terminal-contract-red-final.log`。修复在 Reserve 前检查实际 `runtime.IsCancelled`，并在终结且 lease 空时释放订阅；CancelParentEpisode 捕获失败后的收束也覆盖空 lease 的早退。未把一次失败的取消尝试强行变成终态。

原 listener 用例的可复用操作改为 closeAll，恢复 completion=2 断言；真正可复用的取消另用 canonical DevOps 的持久 binding 验证。两个正控都通过，明确纠正第三批对“所有 cancel 都可复用”的误判。新 fixture 通过真实 Journal 协议建立 binding，调用实际 Runtime 的收养消息，不改内部集合或伪造状态。

`dotnet tool run fantomas --check src/Wanxiangshu` 与 `node scripts/check.mjs` 均 exit 0；`r2-review-terminal-format.log`、`r2-review-terminal-check.log`。实际 `node scripts/build.mjs` 为 focused **778 项 / generation 486**；018/025/delegation 003 合计 **78 tests / 76 pass / 0 fail / 2 TODO**，exit 0；`r2-review-terminal-build.log`、`r2-review-terminal-green.log`。025 新增四项后为 55 项；真实 Bun 故障矩阵还检查终结旧实例不能再次 fork。

最终完整归属链使用第三批同一文件分组，**485 tests / 456 pass / 0 fail / 5 skip / 24 TODO**，exit 0；`r2-review-terminal-ownership-green-final.log`。新增四项均通过；025 为 **55 tests / 54 pass / 1 TODO**。未改的 process-execution、Trace 分别沿用第三批 **193 / 188 pass / 5 TODO** 与 **102 / 93 pass / 1 skip / 8 TODO**，三个互不重叠分组共 **780 tests / 737 pass / 0 fail / 6 skip / 37 TODO**；这不是重新跑过未改的两组。

该归属链第一次执行有 1 个测试夹具失败：PID marker 已创建而内容尚未写入，`Number('')=0` 使 shared-detach 未进入行为断言，`r2-review-terminal-ownership-final.log`。将三个 PID marker 改为先写同目录临时文件、rename 后发布，保留 PID 有效性断言；没有重试业务或把失败转 skip。上述完整复验才是最终通过结果。

非作者在 generation 486 独立运行 025 **54 pass / 1 TODO**、delegation 003 **13/13 pass**。原先独立反例原样重跑：无/有初始 PTY 两路 next.ok=false、newSpawns=0、listenerAfterCancel=0；失败取消的晚到退出使订阅从 1 降至 0。另建 actual Journal 的 canonical DevOps 正控，连续两次取消后仍可复用，完成计数 **1→2→3**，最终 detach 后订阅为 0。证据 `independent-pty-r4-gen486-025-final.log`、`independent-pty-r4-gen486-delegation003-final.log`、`independent-pty-r4-cancel-contract-green.log`、`independent-pty-r4-canonical-companion-green.log`。O-11 按这两个实际状态分支的限定范围复审通过；空 lease 的 abandon 提前失败支路仅静态核对，未单独注入该故障。

PID 就绪修复也经非作者复审：三个写点均先写完整内容再发布 marker，原 PID 校验保留。独立并发运行 shared-detach **6/6**，helper 返回后另查 **12 个确切 PID 均已退出、12 个临时文件均不存在**；`/private/tmp/independent-pty-r4-marker-check.log`，脚本 `independent-pty-r4-marker-check.mjs`。没有重建或修改共享源码。

文档收尾后的实际构建 generation **487**、174 surfaces / 836 modules 链接通过；`r2-review-terminal-build-delivery.log`。真实 `verify-package.mjs` 打包、解包、隔离 npm ci 和消费均 exit 0：**2073 members / 13,142,101 bytes**，tarball SHA-256 `040e22e71cc7f8d607bb417af263ea32b6341de2d2b64af9ae05657880e3c7d3`；`r2-review-terminal-package.log`。它替代第三批主包作为当前交付候选，库 0.6.2 的固定包摘要未变。

R4 据独立复验和主包消费证据 DONE，主仓以本提交正常快进推送 master。提交后再核对 freshness、精确主包和远端一致性，最终身份在交付回复中报告。没有部署到运行中的 Host。T-01、O-01 和 GAP-133 状态不变，不将本地准入修复扩大为完整抽取完成。

<a id="independent-review-fixes"></a>
## 第三批：独立复审与 0.6.2 修复

复审基线为主仓 `bb9a811cf`、库 `f6cd8c3 / 0.6.1`，两仓起点干净。审查者不编辑共享源码：`independent_journal_r2` 复核 Journal，`independent_pty_r2` 复核 PTY，`independent_boundaries_r2` 复核跨包边界与实际构建。发现、优先级和后续计划见[报告独立复审段](SOURCE-AUDIT-2026-10-15.md#independent-review-fixes)。

O-09、O-08、J-02、O-10、E-03、E-04 的局部实现已完成。库 `eef482506f4ea65cad0a88d3a8c5769a8ca9adbb / 0.6.2` 已正常推送原交付分支；主仓固定 `vendor/wanxiangli-0.6.2.tgz`，SHA-256 `d8909bebb92b01e0cd0cfa28cfdc14d0d1d89f467b7afe5e94a99efd96b0038e`。0.6.1 留在 Git 历史，没有覆盖同版本内容。

### 反例与局部复验

- Cancel/Drain 十项新用例首次 **10 tests / 0 pass / 10 fail**；`/private/tmp/r2-review-cleanup-red.log`。真实 Bun 旧 Cancel 反例为 closeAll/detach/cancel × 提交前后六组合：无 Signal、两个 PID 均存活、两个 blocker。修复逐项保留 Cancel 错误、继续真实清理；没有返回假成功。
- Journal 关闭失败矩阵首次 **17 tests / 13 pass / 4 fail**，修复后 Journal **30/30**；`r2-review-journal-close-red.log`、`r2-review-journal-green.log`。独立关闭失败探针在 Node/Bun × COMMIT 前后四组合也通过。
- Listener 三项先红，**3 tests / 0 pass / 3 fail**；`r2-review-listener-red.log`。原生订阅可释放后，025 验证永久 detach、可复用 cancel、失败后的晚到退出与持续 Unknown 后显式确认；Process 003 验证同一公开 JS sender 多注册、重复 Dispose 只移除自己的注册。Surface 每次生成不同的底层闭包，此用例不直接证明底层函数引用相同的情况。第一次候选错误地用已 cancel mailbox 的 PendingCompletionCount 作 oracle，2 项不通过；改为实际订阅计数与 lease/blocker，未改生产语义。当时 018/025/Process 003：**83 tests / 82 pass / 0 fail / 1 既有 TODO**；`r2-review-listener-green-final.log`。其中 025 为 50 项，含受控与真实 Bun 各八项 Cancel 故障矩阵。
- 非作者先独立复验 Host 清理：真实 `node scripts/build.mjs`，focused 778、generation 477、174 surfaces / 836 modules，exit 0；随后 plan no-op。018/025 当时 **50 tests / 49 pass / 0 fail / 1 既有 TODO**；真实 Bun COMMIT 前后丢 Cancel 回执均保留 Unknown，同时 PID 退出、port/lease 清除、blocker 为空。日志 `r2-independent-host-build.log`、`r2-independent-host-tests.log`、`r2-independent-host-real-cancel.log`。此后订阅契约变更的真实聚焦构建为 generation 479、1532 项，exit 0；`r2-review-listener-build.log`。
- 库完整构建和七入口全部源码、真实组合、隔离编译及安装包套件：**320 tests / 319 pass / 0 fail / 1 skip / 0 TODO**；`r2-review-library-build.log`、`r2-review-library-tests.log`。skip 仍是 Linux 限定的基础 Process 安装用例。
- 非作者 Journal 复验 **30/30**；当前双写者用例连续 **25/25**。临时去 finalize 突变在 Bun 七种模式全部被拒绝，Node 七种仍通过；原 close/initialize/commit-after 假阳性已被保存的原生 get 断言捕获。严格 TypeScript、独立回滚/重入和显式 close 重试均通过。原始材料 `/private/tmp/journal-independent-r2.BRvunK/recheck/`；没有共享源码突变或 GC。
- 非作者对确切 0.6.2 tarball 另建隔离消费者：清除 NODE_PATH/NODE_OPTIONS 后，Node **7/7**、Bun **7/7** 和 strict TypeScript 均通过；22 个包文件逐字节等于库交付目录与实际安装内容。证据 `/private/tmp/r2-independent-exact-062-J4mBvW/`，含 `node-matrix.json`、`bun-matrix.json`；未重建共享 dist 或借用主仓依赖。

### 最终集成门禁与剩余范围

环境 macOS、Node 26.6.0、Bun 1.3.14。实际构建 generation 480 为 full impact 1532 项，接口与 836 模块链接通过。以下三个分组无重叠文件，均 exit 0；完整命令沿用第二批相同分组，没有跑无关全仓套件。

| 分组 | tests / pass / fail / skip / TODO | 日志（`/private/tmp/`） |
|---|---|---|
| Work、managed-session-lifecycle、session-ontology 及原相邻归属链 | 481 / 452 / 0 / 5 / 24 | `r2-review-main-ownership-final.log` |
| process-execution 全包 | 193 / 188 / 0 / 0 / 5 | `r2-review-main-process-final.log` |
| Trace 与原 WorkRecord/Prefix 相邻链 | 102 / 93 / 0 / 1 / 8 | `r2-review-main-trace-final.log` |
| 合计 | **776 / 733 / 0 / 6 / 37** | 零 cancelled；TODO/skip 不计通过 |

37 个 TODO 包含原 36 个及新增 O-01 的真实 leader-exit 反例。该项实际运行并因后代仍存活失败，绝不是新能力已通过。025 当前为 **51 tests / 50 pass / 1 TODO**。helper 在 finally 中只按测试持有的确切子 PID 清理，并等待真实退出；非作者另行读取两个 marker，确认两个 PID 均已不存在。`independent-pty-r3-025.log`、`independent-pty-r3-process003.log`、`independent-pty-r3-helper-check.log` 留存独立运行结果。外层夹具在 helper 成功返回后即记录已清理，避免预期断言失败后再次使用旧 PGID。

最终静态门禁首次发现 ClosePty 的 try→match 嵌套违反 fsharp-control-pyramid，未计通过。提取同一同步 Cancel 请求为私有成员后，`dotnet tool run fantomas --check src/Wanxiangshu`、`node scripts/check.mjs` 均 exit 0；日志 `r2-review-main-format-final2.log`、`r2-review-main-check-final2.log`。没有放宽门禁或改业务顺序。真实 Host 聚焦重编 generation **481 / 778 项** 后，018/025 再验 **61 tests / 59 pass / 0 fail / 2 TODO**；`r2-review-main-build-focused-final.log`、`r2-review-main-host-focused-final.log`。其它两组对应未改的实现，未把该定向重验冒作重新跑过全部 776 项。

`independent_pty_r2` 对最终 generation 481 独立运行 018/025，得到同样的 **61 / 59 pass / 0 fail / 2 TODO**，确认 O-08/O-09/O-10 及原 O-05/O-06/O-07 的限定范围成立，未发现新增问题；`/private/tmp/independent-pty-r3-gen481-host-final.log`。再次运行 helper 并读取 marker，两个确切 PID 均已退出；`independent-pty-r3-gen481-helper-check.log`。本批与 R2 的局部卡据此 DONE，原 R1 不被代替。

文档收尾后的 `node scripts/build.mjs` generation **482**，exit 0；`r2-review-main-build-delivery.log`。`npm_config_cache=/private/tmp/module-improvement-npm-cache node scripts/verify-package.mjs`，exit 0，已真实打包、解包、隔离 npm ci 并消费主包：2073 members、13,141,126 bytes，SHA-256 `856615c21f7798151c96b0b9f2b72ffd7778009d5193eb81cbda4c5e4dbce78e`；`r2-review-main-package.log`。该门禁检查主包内实际 bundled wanxiangli 版本 0.6.2，不借顶层依赖掩盖缺包。

两仓均采用正常提交与正常推送：库交付分支已到 `eef4825`；主仓本提交在 master，不强推、不改写历史。提交后再执行 freshness 与主包消费门禁，结果与包身份核对后记录在交付回复。没有部署到运行中的 Host。

T-01 / O-01 仍 DOING，GAP-133 仍 PARTIAL。本次独立真实反例确认：PTY leader 自然退出后，同组后代仍活着，而本地与持久 blocker 已空；这是完整物理监管的既有未完成能力，不能借 Cancel 回执修复关闭。下一包须保持可安全监管的确切实例，覆盖 leader 先退出与忽略 TERM 的后代；不拿缓存旧 PGID 盲杀。报告已写明验收条件。

<a id="second-review-fixes"></a>
## 第二批：0.6.1 收尾与准入改进

基线为主仓 `5dd9dec9c`、库 `3ad5b88 / 0.6.0`。本批问题及合理性分析见[报告第 0 节](SOURCE-AUDIT-2026-10-15.md#second-review-fixes)。独立库代码提交 `93a717316b376f94db7a7f4b8916d5e58d73eb8e` 已正常推送到原交付分支；主仓升级固定包 `vendor/wanxiangli-0.6.1.tgz`，SHA-256 `c998f59aa1934fcfe915e24fec750c7da6276825c9ac8c91b558a5f8925277ed`。旧包留在 Git 历史，不重写同版本字节。

| 问题 | 实现与本批验收 | 保留边界 |
|---|---|---|
| J-01 / P1 | Journal 对每个原生 statement 用 try/finally 释放；Node 仍由 connection.close 负责最终释放。新增 SqliteStatement 声明和自定义连接义务 | Unknown、零等待 CAS、历史格式不变；未证明断电、任意自定义驱动或原生 close 自身失败后的恢复 |
| O-05 / P1 | detach/cancel 改走当前实例逐项关闭；ClosePty 在物理等待后确认缓存的同一退出事实；仍提交未知就明确失败并保留 lease/blocker | 只重放同一 Ended 事实，不再次启动或假造 NotStarted；跨重启缺失的物理终态仍不能猜测 |
| O-06 / P2 | 物理清理按本实例 lease，detach/cancel 核对独立 scope；同端口、同 durable root 的其它实例继续存活 | CloseOwnedPtys 仍作根范围 blocker 检查，不能把本实例 detach 当作整个逻辑 owner 已可退休 |
| O-07 / P1 | 永久 detach 开始时锁住本地准入，Drain 丢回执也不重新允许 fork；新运行时的独立 scope 仍可使用 | 没有据此宣称跨进程、每业务轮次的代际准入已经实现 |
| B-01 / P2 | Host 项目补充 DurableWorkHost 所在直接引用；随后只改 Host 文件的真实聚焦编译通过 | 没有改构建器、隐式借用全仓源码或放宽检查 |

本段保留 0.6.1 交付时的证据：当时局部实现和自验完成，两张 R2 卡进入 REVIEW。后续非作者复审、关联缺陷及修复见上方第三批，不把原 316/754 项追写为新增证明。CUT-03-R1 的 T-01、CUT-04-R1 的 O-01 仍未闭合。本批没有发布到正在运行的 Host。

### 先红与纠正

- Journal 新矩阵首次 13 tests / 11 pass / 2 fail：Bun 的读取失败后 statement 未释放、COMMIT 入口未知后重开写入被锁；`/private/tmp/module-improvement-journal-red.log`。其余新增场景是回归，未冒称也曾失败。
- Host 的两个 Ended 丢回执、两个共享端口 detach 和两个共享端口 cancel 场景首次 6/6 失败；`/private/tmp/module-improvement-host-red.log`。
- Drain 未提交后仍允许 fork 的新增反例先红；COMMIT 后丢回执的正控原本通过，2 项合计 1 pass / 1 fail；`/private/tmp/module-improvement-drain-red.log`。
- 中间候选仍消费 0.6.0 时，真实 Bun 的 Ended-before 场景明确报告 `database is locked`；换入 0.6.1 后同一入口通过。detach 时 mailbox 已取消，PendingCompletionCount 不能作为 Ended 落盘 oracle；这里用实际 PID、lease、durable blocker 和重开读取，原有正常退出恰好一次测试继续保留。
- 一次聚焦构建暴露缺引用；另一次 Fable 编译虽完成，但我在编译期间更新了报告，改变生成输入，主构建以 `Mid-build mutation detected` 拒绝。两次均不计通过。补引用并冻结输入后，全量构建 generation 472 和聚焦构建 generation 473 均 exit 0；聚焦从原来缺依赖的 624 项扩为含所需依赖的 778 项。日志：`/private/tmp/module-improvement-main-build-final.log`、`module-improvement-main-build-dependency.log`、`module-improvement-main-build-stable.log`、`module-improvement-main-build-focused.log`。

### 验收结果

环境 macOS、Node 26.6.0、Bun 1.3.14。以下命令以相同 0.6.1 代码和已声明依赖运行；TODO、skip 仍不算证明。

- 独立库：`node scripts/build.mjs`；全部七入口源码、组合、隔离编译及安装包套件 **316 tests / 315 pass / 0 fail / 1 skip / 0 TODO**，日志 `/private/tmp/module-improvement-library-build.log`、`module-improvement-library-tests.log`。Journal 单独为 **26/26**，包含 Node/Bun 各五种真实资源收尾场景；`module-improvement-journal-green.log`。
- 精确 vendor 包另装入 `/private/tmp/module-improvement-exact/consumer`；清除 NODE_PATH/NODE_OPTIONS 后，在 Node/Bun 各跑正常关闭、初始化失败、读取失败、COMMIT 前/后丢回执，十次均成功，并通过 strict TypeScript。helper 来自已提交的 `src/eventstore/tests/recovery-consumer.mjs`；没有借主仓 node_modules，未调用 GC。SHA-256 与上述固定包相同。
- 主仓归属及相邻链：`TMPDIR=/private/tmp node --test requirements/work/tests/*.test.mjs requirements/managed-session-lifecycle/tests/*.test.mjs requirements/session-ontology/tests/*.test.mjs requirements/crash-reconciliation/tests/020.test.mjs requirements/delegation/tests/{003,004,006,024,026,027,032}.test.mjs requirements/participant-horizon/tests/{009,010,011}.test.mjs`：**460 tests / 432 pass / 0 fail / 5 skip / 23 TODO**，exit 0；`/private/tmp/module-improvement-main-ownership-final.log`。025 共 32 项，含四个新增真实 Bun Ended-before/after、shared-detach/cancel 场景，以及原真实 TERM→等待→KILL。
- 主仓进程：`TMPDIR=/private/tmp node --test requirements/process-execution/tests/*.test.mjs`：**192 tests / 187 pass / 0 fail / 0 skip / 5 TODO**，exit 0；`/private/tmp/module-improvement-main-process-final.log`。最初两项因沙箱禁止 ps 观察而未取得结论，获得对应权限后原套件复跑通过，未改 oracle 或转 skip。
- Trace 及相邻取材：`TMPDIR=/private/tmp node --test requirements/semantic-trace/tests/*.test.mjs requirements/work-record/tests/{002,005,008,014}.test.mjs requirements/prefix-stability/tests/{009,012,014}.test.mjs`：**102 tests / 93 pass / 0 fail / 1 skip / 8 TODO**，exit 0；`/private/tmp/module-improvement-main-trace.log`。后续只改 Host 准入标记、参数名和项目引用，没有更改 Trace 实现。
- 三个主仓分组没有重叠文件：合计 **754 tests / 712 pass / 0 fail / 6 skip / 36 TODO / 0 cancelled**。`dotnet tool run fantomas --check src/Wanxiangshu` 和 `node scripts/check.mjs` 均 exit 0，日志 `/private/tmp/module-improvement-format-final2.log`、`module-improvement-check-final2.log`。shard-edge-audit 的既有 report-only 提示没有改成通过证明。
- 文档收尾后的构建与主项目安装包验证单独记录在交付门禁段；不以这次源码回归代替包消费或部署证明。

### 交付门禁

- `node scripts/build.mjs`：generation 474，exit 0；`/private/tmp/module-improvement-main-build-delivery.log`。此前全量及只改 Host 文件后的聚焦编译分别证明直接引用与所选闭包成立。
- `npm_config_cache=/private/tmp/module-improvement-npm-cache node scripts/verify-package.mjs`：exit 0，真实打包、解包及隔离 npm ci 后消费，不借主仓 node_modules。2073 members、13,137,717 bytes，主项目 tarball SHA-256 `cfe28196f51118aa3038b3d0ab00f1e5a5846854c8b2f3cc5dda3b8b6809d610`；`/private/tmp/module-improvement-main-package.log`。最初沙箱网络导致 ENOTFOUND，未计为通过；允许网络后同一门禁通过，原记录在 `module-improvement-main-package-sandbox.log`。
- 主仓按正常提交、正常 push 交付 master；独立库留在既有交付分支，代码提交 `93a7173`，后续规则证明更新 `f6cd8c3`，没有改库 main 或强推。该次交付时 R2 的非作者复审尚待补，当前结果见上方第三批；这里只交付已验证代码，没有部署到实际 Host。

## 第一批交付：0.6.0（历史证据）

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
