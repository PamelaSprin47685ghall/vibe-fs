# PTY 跨重启物理接管

工作包 CUT-04-R6-C，输入主仓 `81cb278a5`、独立库 `b6984d9 / 0.6.2`，两仓干净。施工状态只在 [BOARD.md](BOARD.md#source-audit-repairs) 维护。

## 目标与依据

R6-A 已能证明一个活 owner 监管的控制终端会话结束，R6-B 已能持久停止确切工作轮次的新准入；目前 PTY master 及实例连接仍随 Host 消失。仅从日志恢复 lease、旧 PID 或空本地表，不能证明原命令退出。

本包先做真实重启原型，再接入现有 Host/Process/Ownership 组合。物理 owner 独立于 Host 持有 PTY，通过本地私有端点及随机凭据识别同一实例；终态沿用 R6-A 的原生退出与监管范围结束证据。恢复只重连或读取匹配终态，不重新启动或补写原命令。Ownership 不查询 OS、不持有连接。

## 施工与验收

1. 验证独立 owner 能在 Host 被 SIGKILL 后继续持有同一真实 PTY；Node/Bun 恢复客户端可以鉴别实例、请求 TERM/KILL、读取确切结束回执，原命令执行次数保持一次。
2. 固定物理身份、准备记录及结束记录的写入边界；验证重复 owner 启动、错误凭据、旧 incarnation 和 owner 失联均不造成重新启动或虚假 Ended。没有端点且没有确切终态的预留继续保留 blocker。
3. 原型通过后再定窄生产契约，接入持久 Reserve → 物理 owner → 确切 Ended。冷恢复先停止准入，再处理该轮资源，最后允许工作作废或结算；不得靠全局 PID 扫描接管。
4. 真实强杀切在 Reserve 后/Start 前、物理启动后/Started 确认前、退出后/Ended 确认前；保留 Unknown、COMMIT 丢回执及跨轮隔离正反例。
5. Fable、对应 requirement、精确包隔离消费及当前平台验收通过后更新状态；未测的操作系统、完整 Bootstrap 强杀矩阵及主动脱组分别登记，不将局部证据写为 O-01/GAP-133 全部闭合。

## 原型结果

`/private/tmp/r6c-prototype.mjs` 使用当前已编译的真实 PtyScope、独立 Bun owner 和 Unix socket；未复制监管算法。首轮 Node/Bun 的运行中与已退出四场景通过，`r6c-prototype.log`。补齐连接独占及未启动封存后，Node/Bun 六场景通过，`r6c-prototype-control.log`：旧 Host 被 SIGKILL 后同实例可控；旧连接尚在时拒绝接管；错误凭据、旧 incarnation 拒绝；原命令各执行一次。creator 未连接时 owner 不启动命令，明确封存为 NotStarted，执行次数为零。

这证明本地控制和生存期方案可行，不是生产接线、完整 Bootstrap 或任意故障恢复的证明。接线沿用相同原则：creator 连接是初次启动许可的一部分，恢复连接只能查询/停止；只有旧连接断开后才允许接管。没有物理 owner 或确切记录的预留保持 Unknown，不根据进程不存在清账。

首次扩展原型执行的自动审批超时，按工具指示重试一次成功；没有更改权限规则。原型通过后才开始以下生产接线。

## 生产实现

`Process/PtyRecovery` 承接持久准备、本地连接和独立 custodian；custodian 持有现有 `PtyScope`，复用 R6-A 的真实 bun-pty、原生目标退出与会话监管，不复制其成员观测或信号算法。`Ownership` 仍只裁决 lease、scope 和持久确认，不查 OS、不持有 socket。本包未修改独立库、vendor、默认包入口或门禁名单。

| 边界 | 实现与不变量 |
|---|---|
| 物理身份 | 以 registry/resource/token 的 SHA-256 定位 `<git-common-dir>/wanxiangshu/pty/<key>`；不可覆盖的 `request` 记录该 lease 的随机 incarnation、连接凭据、命令及原调用方环境。custodian 用独占 `claim` 拒绝同一准备记录被第二次启动。PID 仅供测试观测，不授予接管或清账许可。 |
| 首次启动 | 只有持久 Reserve 的首次许可进入 prepare/start；custodian 还须接受唯一 creator 连接才运行命令。重复准备直接拒绝，不重新 launch。creator 的控制帧只发送一次，连接失败后不自动重连或补发。 |
| 恢复接管 | 校验 key/incarnation/随机凭据；旧控制连接还活着时拒绝接管。recover 连接只允许 inspect、SIGTERM、SIGKILL，不能 write、resize、发送普通信号或再次启动。被拒绝的连接立即失去控制权，同批后续帧也不能操作。 |
| 确切终态 | 只在原生目标退出与 R6-A 监管范围结束均确认后写入 Ended；回执绑定原 lease 的 key/incarnation，并用私有凭据校验 HMAC。文件先 fsync，再原子发布；收到通知仍回读并核对持久回执。socket EOF、超时、空成员表或 PID 不存在都不形成 Ended。 |
| 明确未启动 | creator 尚未接纳时，owner 可以封存 NotStarted 并退出，此后不再接纳启动；启动 owner 本身明确失败也可记录 NotStarted。这不是把等待超时当退出，更不是从缺少文件推断没有进程。 |
| 生产收尾 | Host 的 exact run 先持久 Drain，仍尝试所有本地实例的确切回收；确认失败保留原错误。成功后尝试该轮未绑定资源的物理接管或终态回读，逐项持久确认，再检查 blocker。ToolRuntimeScope、Load 和 Plugin 冷恢复均等待该过程后才允许 work terminal/void。 |
| owner 寿命 | 原 Host 被 SIGKILL 不关闭 master。复审修正后，终态发布后 custodian 等待有序 socket 的 end 发送回调/close，最多 5s，再在下一事件循环退出，使原 PtyScope 的清理回调先完成；无控制连接则直接安排退出。控制等待有宿主侧期限，失联保持 Unknown。 |

Host 内有本地 lease/incarnation 时传入确切 binding；冷恢复从相同持久 lease 得到 key，读取其原准备记录。没有补写虚假的 Started，也没有用旧 PID/PGID 清账。普通 creator 的输入、尺寸、输出及原 BUN 环境继续通过同一物理 PTY；内部 `BUN_BE_BUN` 启动标记不冒充调用方环境。

## 反例、试制错误与归因

修复前先把两项真实 Bun Host 重启用例运行在原 R6-B 产物上，结果 **0/2**，`/private/tmp/r6c-baseline.log`。失败是原命令仍有义务但新 Host 没有物理 binding；不是因测试找不到新 export。原问题仍记 O-01 的未承接能力，不归给本轮补丁。

| 项目 | 归因与处理 |
|---|---|
| R11-01：运行中接管的结束等待不返回 | 本轮未交付候选使用了 Fable 运行时不存在的 TCS.TrySetResult/TrySetException；确切 terminal 已落盘，回调却不能完成等待。generation 526 的四项重启 **2 pass / 2 fail**，`r6c-restart-first.log`。改用目标运行时已有的 SetResult/SetException，527 的四项全通过。 |
| R11-02：恢复连接可发送普通信号 | 本轮未交付协议只限制 write/resize，遗漏了 signal 的权限收窄。正式负控在 527 上失败，`r6c-control-boundary-red.log`；收紧为 TERM/KILL，并使拒绝连接及其缓存帧失效。528 起反例转绿，最终 helper 同时证明原输入与 USR1 均未到达目标。 |
| 编译与契约试制错误 | 补齐 compile-order/fsproj、明确签名类型、修正 computation expression 与 Queue 枚举。Task.WhenAny 会生成不存在的 JS named export，改用现有 NodeTiming.raceExit。初次 build/check 的失败保留在 `r6c-build-{first,2,3}.log` 与 `r6c-check-*.log`，未放宽格式、控制嵌套或模块链接门禁。 |
| 测试与工具问题 | 强杀 worker 的夹具原来只看 exitCode，会等一个已经发出的 signal exit；补看 signalCode。收尾扫描另发现两次 owner-lost 负控未清理新 custodian；原 Unknown 断言成立，但旧夹具只回收原范围内的进程。已记录该测试创建的确切 custodian，在负控断言后回收并等待退出；两次遗留也按精确命令行核对后清除，不更改生产 Unknown 语义。Linux 首次传输胶囊被 HTTP 413 拒绝；最终包摘要复核首轮被沙箱拒写 npm 缓存，按原操作所需权限重跑通过。均保留失败，不算产品失败或一次成功。 |

上述两项候选实现缺陷在本轮交付前修正，不追记为原拆分错误，也不隐去失败日志。本轮没有非作者复审，未确认新的 review 误判。

## 正式验收

`managed-session-lifecycle/025` 由 **90 增至 103 项，103/103 通过**。新增十三项均走真实 Host、持久 SQLite、生产后端及新 OS 进程：

- Node/Bun 各覆盖运行中、旧 Host 死后自然退出、冷 Load 三条接管路径；原命令计数保持一次，真实目标 PID 被回收，最终 blocker 为空。
- Node/Bun 各在真实 Ended 事务 COMMIT 前、COMMIT 后 SIGKILL，核对旧义务分别保留或已提交；新进程只回读同一终态，不再次执行命令。
- Reserve 已 COMMIT、尚未物理准备时 SIGKILL：重启不 launch，仍有一个 blocker，不能完成 work。
- Node/Bun 各同时失去 creator/custodian：即使原 PID 已消失，没有原 owner 的确切终态也保留 blocker，不重发命令。

`process-execution/003` 新增一项正式测试，内部七场景覆盖 Node/Bun 的运行中、自然退出、creator 交互及 Node 的未接纳启动封存。实际检查重复 prepare、错误 key/incarnation/secret、活控制连接、拒绝后的缓存帧、write/USR1 越权、被破坏的 terminal proof；只有正确身份与证据才被接纳。creator 输入、输出、resize、环境保持正确；七个 custodian 均确切退出。先行结果 `r6c-{025-expanded,creator-regression,control-boundary-green}.log` 不替代最终干净构建。

最终生产候选 **clean generation 529，174 surfaces / 838 modules**，`/private/tmp/r6c-build-clean.log`，exit 0。冻结全部仓库输入期间完成以下不重叠回归：

| 套件 | total / pass / 普通 fail / skip / TODO | 日志（均在 `/private/tmp/`） |
|---|---|---|
| Work、managed-session、session-ontology、crash 018/020、相关 delegation/horizon | **543 / 514 / 0 / 5 / 24** | `r6c-ownership-clean.log` |
| managed-chat、execution-model-routing、capability-enforcement | **430 / 401 / 0 / 11 / 18** | `r6c-provider-clean.log` |
| Process | **216 / 211 / 0 / 0 / 5** | `r6c-process-clean.log` |
| 合计 | **1189 / 1126 / 0 / 16 / 47** | skip、TODO 及其内部失败断言未计通过 |

format/check、另一次不经缓存的 `node scripts/check.mjs` 和 diff whitespace 检查均通过；`r6c-format-clean.log`、`r6c-check-expanded.log`、`r6c-check-direct.log`。未使用 dotnet build，未修改门禁规则。

收尾只补 owner-lost 夹具的确切 custodian 清理，未改生产源码；正式 025 另重跑，见 `r6c-025-cleanup-final.log`。上述干净构建及包消费仍对应完全相同的生产字节；不把测试清理改动冒称又一轮全量行为验证。

复验命令：

```sh
node scripts/build.mjs --clean
TMPDIR=/private/tmp node --test --test-concurrency=4 requirements/work/tests/*.test.mjs requirements/managed-session-lifecycle/tests/*.test.mjs requirements/session-ontology/tests/*.test.mjs requirements/crash-reconciliation/tests/{018,020}.test.mjs requirements/delegation/tests/{003,004,006,024,026,027,032}.test.mjs requirements/participant-horizon/tests/{009,010,011}.test.mjs
TMPDIR=/private/tmp node --test --test-concurrency=4 requirements/managed-chat-execution/tests/*.test.mjs requirements/execution-model-routing/tests/*.test.mjs requirements/capability-enforcement/tests/*.test.mjs
TMPDIR=/private/tmp node --test --test-concurrency=4 requirements/process-execution/tests/*.test.mjs
npm run format:check
node scripts/check.mjs
TMPDIR=/private/tmp node /private/tmp/r6c-package-proof.mjs
```

### 包与平台

精确包 `.fable-build/verify-logs/r6c-package/wanxiangshu-0.9.0.tgz`：**2075 members / 13,264,745 bytes**，SHA-256 **`e0764eb324842a545c0a9292b1f3b88a0153892d2fc46b30446eafb0e23b1b14`**。真实 pack/解包、隔离 npm ci、默认入口消费通过。随后从同一 tarball 再隔离安装：旧物理口在普通 Bun 与已安装 OpenCode 可执行文件下通过 TTY/输入/resize/原生退出/后代排空；新物理口七场景分别由 Node/Bun creator 运行，另三场景由真正的 OpenCode 可执行文件充当 creator 并启动其 custodian。均不借用主仓运行依赖，日志 `r6c-package.log`，脚本 `r6c-package-proof.mjs`。OpenCode 可执行文件验收不是完整 UI/Bootstrap 强杀矩阵。

本机为 macOS arm64 / Node **26.6.0** / Bun **1.3.14**。Linux x64/glibc / kernel **7.0.14** / Node **22.22.1** / Bun **1.4.2** / bun-pty **0.4.10** 另运行相同正式 helper 的七场景，全部通过，命令计数 **1,1,1,0,1,1,1**，七个 custodian 均退出。生产公开 Surface 验证胶囊 SHA-256 **`fc53d7cb7d914c0515d6c11c9bde5efd6cbeed183f47f6b8d67dde04766bb3fa`**，正式 helper SHA-256 **`e71abc2dca5752d074f2db16c33d34d3309f59eab6d39f0165a97f8c2c1b5e0e`**，两端字节一致。原始工具回执为 E5-2699A session **32**；本地 `r6c-linux-summary.json` 明确只作回执摘要。扫描无遗留 worker，两份临时验证源文件已删除，沿用的依赖缓存未改。这是 Linux 物理接管证据，不是 Linux 完整 Host/SQLite 验收。

## 剩余边界与交付

1. R6-C 只接管本版本已建立不可变准备记录的 PTY。旧版本遗留资源、Reserve 后尚无物理准备、owner 与确切回执同时丢失，都只能显式保留 Unknown；不提供基于旧 PID 的补救清账。
2. 冷恢复只排空原资源，不恢复交互或补发输入；旧 Host 断开后的输出不缓存回放。每个 PTY 有一个独立 Bun custodian，并保留少量物理记录；大规模容量、记录回收和极端并发 RSS 未验收。
3. 仍依赖现有 POSIX/Bun 监管范围。主动脱离控制终端的守护进程、Windows/musl、完整 Bootstrap 强杀矩阵、断电及恶意同用户文件篡改不在已验证范围。
4. 本轮是作者施工与自验，R6-C 进入 REVIEW，待非作者复审；O-01/GAP-133 整体、Trace 和其余 P2 不销项。仅在相关回归范围内未见未修正的新回退。

本轮按 AGENTS.md 正常提交并推送 master，不强推；验证、提交与部署分别报告。尚未部署到用户运行中的 Host。文档收尾后只刷新构建元数据并复核精确包字节，不把它写成新一轮行为验收；`r6c-build-docs.log`、`r6c-package-final-identity-green.log` 已确认 generation 530 的包摘要未变，后续测试清理与提交后仍按同一方式核对。

## 独立复审与修正（2026-10-16）

复审输入为干净的 `9a89db96a`，同时对照修复前 `81cb278a5`。本段新增结论不覆盖上面的作者施工历史。用户明确授权后，`r6c_protocol_review`、`r6c_ownership_review`、`r6c_observation_review` 分别审查协议认证、生产持久边界和观察传输；根代理负责共享文件修改。非作者结论与最终验收在下文补齐，不提前写 DONE。

| 编号 | 确认问题、归因与修正 |
|---|---|
| R12-01 / P1 | **R6-C 新增回退**：活 creator 轮询到磁盘 terminal 后立即销毁连接，或在 EOF 时直接回读 terminal 宣布完成，都会越过未读输出；custodian 又在下一事件循环退出，未等 socket 发送完成。真实连接停读 500ms 时，16 字节和 1MiB+16 字节输出都曾静默变为空成功；原有 PtyScope 对照完整收到 1,048,592 字节。现 creator 只能从有序控制流确认完成，提前断流报告观察失败；custodian 在物理终态后等待 end 发送回调/close，并有 5s 退出上限。冷恢复仍可独立接纳真实物理回执，未把输出完整性混成物理退出证据。 |
| R12-02 / P2 | **R6-C 新增竞态缺陷**：有效 NotStarted 若在 drain 入口检查之后、连接阶段才读到，会被通用客户端当启动失败，而入口处已有的同一回执却能结清。现 recover 把已校验的 NotStarted 作为确切终态；creator 用独立 no-start 事实报告未启动，不伪造原生退出。完成通知只交付一次。Ended 的同类竞态原本通过，未确认未处理 Promise 拒绝，不将这两个假设列为缺陷。 |
| R12-03 / P2 | **R6-C 新增接线遗漏**：Node Host 的真实 `spawn bun ENOENT` 已形成匹配 NotStarted，原 Host 两次 stop 和 dispose 仍失败，只有 fresh Host 能清账；creator 启动观察先失败、owner 迟到封存的情况也一样。现 `PtyExitEvent.NotStarted` 经独立回调清理 Supervisor session、pending/read、Port active/exit-task/fault；Host 缓存原始类型化事实，以同一 incarnation 的 NotStarted 请求重试归属。显式 close 前只重读匹配 NotStarted，不能用 Ended 文件修补输出观察失败。对外仍是带 `PTY_NOT_STARTED` 和原原因的失败结果。 |
| R12-04 / P2 | **R6-C 新增排空遗漏**：同轮有一个本地 owner 丢失时，原 ClosePtyRun 提前抛本地失败，未尝试另一可恢复的旧实例。现确认 Drain 后仍尝试同轮所有无本地绑定的 blocker，最后保留 stop/local/recovery 的原错误优先级；仍有本地 lease 的实例不进入冷恢复，避免吞掉 creator 的输出故障。Drain Unknown 时只清确切本地实例，不接管旧实例。 |
| R12-05 / P2 | **R6-C 新增资源遗漏**：真实 custodian spawn 失败后，只写 NotStarted，遗留本次私有短 socket 空目录。现仅在 child spawn error 路径 finally 清理该端点；重复 claim 失败不清另一 owner 的端点。 |

五项均归 R6-C 新实现，未追记给原拆分或 R6-A/B；没有发现它们使原命令再次执行。真实回执、认证或实例缺失仍保留 Unknown，不能为了输出修复而放宽清账。另移除 Runtime 中“未接跨进程恢复”的失效注释，澄清 WHAT-025 的停止准入不代替接管凭据，与新条款保持一致。独立库与 vendor 保持 `b6984d9 / 0.6.2`，未改默认包入口或门禁。

### 复审取证与候选修正

`/private/tmp/r6c-review-output-{red,large-red,native-control}.log` 保留小输出、1MiB 输出反例及原物理口对照。`r6c-review-terminal-NotStarted-{race,control}.log` 则用真实 owner 封存的同一类型回执，对比入口已存在和首次检查后才可见的结果，不伪造 NotStarted。

003 增加八项正式用例：Node/Bun 各验证停读小输出、大输出与提前断流；Node 另验证真实 NotStarted/Ended 的两次读取竞态。首轮夹具误以为 Bun 会同步更新 builtin namespace，五个 Bun 用例没有进入产品断言；改用实际 Socket.prototype.on 控制真实连接，文件读取竞态只用已验证的 Node 原生钩子，不增加 skip。原交付正确夹具为 **8 / 1 pass / 7 fail**（`r6c-review-observation-red-2.log`）；候选 533 为 **8/8**（`r6c-review-observation-green-first.log`）。不把最初的夹具失败算作产品回退或宣称 Bun 假注入有效。

025 另补八项真实 Host/SQLite/PTY 边界：Node/Bun 各在冷接管 Ended 的 COMMIT 前后丢回执，当前收尾仍失败，下一进程凭同一物理回执完成，命令计数一次；Node/Bun 各把另一个真实旧实例的 terminal 置于当前仍活资源，分别验证错 proof、有效签名但错实例。错误资源保持 blocker 且未被发信号，另一正确资源仍排空，恢复正确证据后才结清。候选 534 **8/8**，`r6c-review-terminal-boundaries.log`。

R12-03/04 正式反例和输出失败负控共七项，535 为 **2 pass / 5 fail**；另两项 stalled TERM 原行为负控通过，合计 **9 / 4 pass / 5 fail**，`r6c-review-local-red-2.log`。536 聚焦 25 项为 **24 pass / 1 fail**，`r6c-review-focus-536.log`；唯一失败是下面单列的旧回执前置夹具，不将这次运行写成全绿。即时/迟到 NotStarted、混合恢复和断流负控均转绿。

### 非作者结论

- `r6c_protocol_review`：536 九项独立入口场景通过。重复 custodian 的 claim 拒绝、活连接独占、双 recovery 仲裁、有效 HMAC 但外来 incarnation 均符合边界。即时/迟到 NotStarted 可由原 Host 收束；新读盘入口的坏 proof/错 incarnation 保留 blocker，恢复正确回执后才清账。真实 SQLite 提交失败仍保留并重放 NotStarted 和原原因，持久零 Ended、命令零次；输出断流仍保留本地失败与 blocker。证据 `r6c-independent-{instant,late,proof,instance,stream}-536.log`、`r6c-independent-ns-commit-{before,after}-536.log`、`r6c-independent-stalled-term-536.log`、`r6c-independent-protocol-536.log`。
- `r6c_ownership_review`：536 十二例真实 Node/Bun 验证通过。混合资源首轮 stop 已排空 old，仅 local Unknown 留账；Drain COMMIT 前/后 Unknown 只清本地，确认 fence 后才接管；冷 NotStarted Unknown 不完成工作；本地输出故障不被物理 Ended 掩盖。证据 `r6c-independent-mixed-drain-green-536.log`、`r6c-independent-mixed-drain-unknown-536.log`、`r6c-independent-local-observation-536.log`、`r6c-independent-notstarted-536.log`。
- `r6c_observation_review`：536 的真实 ENOENT 端点清理和 Node/Bun 先 started 后停读 6s 三例通过。慢连接各只收到 73,728/1,048,576 字节，显式连接失败、零成功退出；owner 约 5.15s 退出，fresh recovery 读取同一 Ended，命令一次。证据 `r6c-review-no-bun-536-green.log`、`r6c-review-six-pause-{node,bun}-536.log`。其首轮仍等旧 onFault 而超时的私有探针按新 onNotStarted 契约修正，保留失败日志，不算产品回退。

三名非作者未参与 R6-C 实现，也未编辑共享源码、编译或改 Git；声明所审范围内未发现仍未修正的已确认 P1/P2。这不证明任意平台、任意负载或所有启动故障均可自动恢复。

### 复审候选、夹具与未定因现象分别记录

1. **本次复审候选的设计风险**：为接纳迟到 NotStarted，草稿曾准备无界等待 TERM handler；非作者在构建前指出永不返回的异步 handler 会阻断 KILL。最终以同一个 `performance.now()` grace deadline 竞速 dispatch/已确认 exit，再用剩余宽限等待，未另加一轮期限。003 的 stalled TERM→KILL、TERM 未回但已真实完成两项正式负控通过。这个草稿风险不归给原 R6-C 交付，也不冒充已交付回退。
2. **R12-06，既有验收夹具缺陷**：两 admission worker 只传 `error.stack`，Fable Exception 没有 stack，父端把 `{error: undefined}` 当成功。该代码在 `81cb278a5` 已存在。Node/Bun 的真实未知 close 新断言修前 **0/2**（`r6c-review-worker-errors-red.log`），现 worker 回传 stack/message/String，父端按 error 字段存在即拒绝；混合与认证六项 **6/6**（`r6c-review-fixtures-green.log`），非作者 537 **2/2**（`r6c-independent-error-carrier-green-537.log`）。没有改产品失败语义。
3. **旧回执前置假设过强**：非作者实际捕获 Ended 已到达、custodian 已退出，但 SQLite 构造 PRAGMA 报 database locked，写账尚未开始；另一进程只读 blocker 不会重放本地缓存。原 Host `close(exactId)` 可重放同一观察并持久确认。夹具现先等真实 terminal，再明确 close/确认，不扩大 5s 等待，不把锁错误改成成功。原失败、单项重跑和诊断保留在 `r6c-review-focus-536.log`、`r6c-review-foreign-retry-536.log`、`r6c-independent-prior-node-a.jsonl`。
4. **R12-U1，未定因观察，OPEN**：132 轮仅作诊断，其中 3 次 producer 初始化 PRAGMA Busy、7 次 reader Busy 有原始记录；另 1 次 5.135s 时只见 attached，命令一次，owner 存活但没有 terminal/fault，blocker 正确保留。原夹具随即移除了 workspace，未保留内层记录或延后观察；不能归 SQL、不能算通过，也没有足够证据认定新回退。原始记录 `r6c-independent-prior-node-a.jsonl:441`。537 定向 Node/Bun 各 20 次均确切 Ended(code23)，最长 572ms，40 次都采到内层 started/target-exit/scope-ended，无 SQLite 或 close 失败；`r6c-independent-inner-observation-537.log`、`r6c-inner-observation-nMJtVN/events.jsonl`。未复现意味着故障目录保全和延后 10s 观察分支未实际执行，不能据此关闭原现象。NodeTiming 相对 81cb 未变，PtyScope 监管/计时/退出逻辑也未变；9a 至今未改 PtyScope，但这不能证明新层没有交互回退。
5. **施工与工具记录**：check 首轮两处、扩展修正后四处嵌套分别由有明确职责的 helper 展平，`r6c-review-check-{first,2,3,4}.log`，未放宽门禁。第一版混合红例的 finally 过早断言回收，留下两个确切测试 owner；已等实际终态后再检查，并按核对过的 PID/进程组/Unix 端点清理旧遗留，不改 Unknown。Linux 临时传输最初多一个末尾换行，两端 SHA 不同；修正并核对完全一致后才执行测试。

### 最终构建、回归与交付证据

生产源码最终为 clean **generation 537**，1536 items / 1574 source references，174 surfaces / 838 modules；`r6c-review-build-clean-537.log` exit 0。只修夹具后 Ownership 完整相关链已复验通过。Process/provider 已在相同生产字节的 536 通过，clean 537 的精确包摘要相同，不把重新编译冒称另一轮行为验收。

| 不重叠套件 | total / pass / 普通 fail / skip / TODO | 证据（`/private/tmp/`） |
|---|---|---|
| Work、managed-session、session-ontology、crash 018/020、相关 delegation/horizon（clean 537，夹具修正后） | 558 / 529 / 0 / 5 / 24 | `r6c-review-ownership-clean-537.log`；此前 `r6c-review-ownership-536.log` 同计数通过 |
| managed-chat、execution-model-routing、capability-enforcement（536） | 430 / 401 / 0 / 11 / 18 | `r6c-review-provider-536.log` |
| Process（536） | 226 / 221 / 0 / 0 / 5 | `r6c-review-process-536.log` |
| 合计 | **1214 / 1151 / 0 / 16 / 47** | TODO/skip 未算通过 |

精确包 **2075 members / 13,272,635 bytes**，摘要 **`63de43ac854bf7958627cb1cbde66f51a3584e2ac9cd5c5989e82aa5c0c290b3`**，536 隔离 npm ci、默认入口、包外 Bun/OpenCode 原 TTY/resize/后代监管、Node/Bun 七接管场景及 OpenCode 三场景通过；新有序输出/断流/NotStarted 竞态 Node 五、Bun 三、OpenCode 三场景也通过，`r6c-review-package-536.log`。clean 537 实际重包同摘要，`r6c-review-package-clean-537.log`；测试未借用源码仓 devDependencies。

Linux 同前述 x64/glibc 环境运行当前相同正式 helper，原七接管场景和八观察场景全部通过，MCP session **37**；本地 `r6c-review-linux-summary.json` 明确只是结构化工具回执摘要。胶囊 SHA-256 **`abd7cff1b571a7f2df74c946291ce786070e2f3958e62416c82da12a47f4b9a8`**，probe **`e71abc2dca5752d074f2db16c33d34d3309f59eab6d39f0165a97f8c2c1b5e0e`**，观察 helper **`d86a02bbde87d9bd36bbb7235273b7e1a4701b82b161e7655e7482777ac4cfc3`**，两端字节一致。自有进程扫描为空，三个临时验证文件已删除，依赖缓存未动。这仍不是 Linux 完整 Host/SQLite 或 Bootstrap 验收。

format/check 已通过：`r6c-review-format-final.log`、`r6c-review-check-final.log`；无 dotnet build、无门禁修改。剩余范围仍为历史无准备实例、owner/确切回执同失、主动脱组、容量/记录回收、未测平台和完整 Bootstrap 矩阵；O-01/GAP-133 整体与 Trace/其他 P2 不销项。提交、推送与部署分别报告，尚未部署用户 Host。

### R12-U1 的后续计划与验收口径

本次五项已确认缺陷已闭环；因 R12-U1 证据不足，CUT-04-R6-C 仍为 REVIEW，不能给出无条件“未引入任何回退”的结论。不以盲目补丁或扩大所有超时替代定因。

下次定向捕获必须在删除工作区前保存脱敏的外层帧、request 身份摘要、内层 started/target-exit/scope-ended/fault 和确切进程关系，并延后观察一次有界窗口。内层已结束但外层缺 terminal 时定位 Recovery 交付边界；内层缺结束时对比直接 PtyScope 控制，区分原监管 Unknown 与新层交互。只有取得稳定反例才调整实现，随后加入正式 003/025、跑对应回归并由非作者复验；无论哪条路径，缺失退出证据继续留 blocker，不用 PID 或计数推断结束。禁止把“40 次未复现”写成该现象已修复。
