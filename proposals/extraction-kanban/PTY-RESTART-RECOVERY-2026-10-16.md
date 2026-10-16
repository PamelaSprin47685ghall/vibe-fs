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
| owner 寿命 | 原 Host 被 SIGKILL 不关闭 master。终态发布后 custodian 关闭 server/socket，并在下一个事件循环退出，使原 PtyScope 的清理回调先完成；不留下常驻的已结束 owner。控制等待有宿主侧期限，失联保持 Unknown。 |

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
