# PTY 监管原型与生产接线

工作包 CUT-04-R6-A；状态以 [BOARD.md](BOARD.md#source-audit-repairs) 为准。输入为主仓 `ec37ab8c2`、库 `b6984d9 / 0.6.2`，两仓起点干净。原修复交付于 `fbd90f4d5`，本次独立复审接续该提交。本包只修改主仓物理 PTY 适配器及证明，不改 Ownership 库协议或 vendor。

## 原缺陷与物理协议

原 O-01 反例在本轮开始时重新成立：`node --test --test-name-pattern='real bun-pty leader-exit' requirements/managed-session-lifecycle/tests/025.test.mjs` 的实际断言为 `descendantAlive=true`，期望 false。测试标 TODO，不能计为 pass。旧适配器把直属 shell 退出直接翻译为整个 PTY 的 Complete，Host 因此过早提交 Ended。

新物理 owner 在创建 PTY 时建立 POSIX 会话，再创建目标及组监管者。目标通过 exec 保持真实 TTY 和 PID，原生 exit 给出目标退出码/signal。目标退出后，owner 继续观察本次控制终端上的进程并核对 SID。关闭时先为每个作业组建立同 SID 的组监管者，经专用输入管道请求它向**自己的当前组**发信号。宿主不缓存目标 PID/PGID 再发组信号；加入失败则无信号许可，先等临时监管者退出，再重新观察。

协议把 started、target-exit、scope-ended 分开。目标已退出、会话中除组监管者外没有活进程、组监管者实际退出、再次观察为空，owner 才写 scope-ended 并退出。外侧还须收到 owner 原生正常退出，以及匹配 incarnation/owner/target 的回执，才能向 PtyPort 发布完成。Host 继续通过原来的确切物理退出与持久 Ended 确认收尾；Ownership 不查询 OS，不认识 Host。

控制命令按序号分别保存，快速 TERM/KILL 不覆盖。signal-forwarded 只证明交给组监管者，不是退出事实。启动、组加入、控制确认及内部退出等待超时只报告 Unknown。owner 失联、回执缺失或观察失败会拒绝已等待的 drain，保留 port、lease、blocker，不重放原命令。完成订阅者取得独立副本，一个订阅者改值或抛错不改写事实或阻止其余订阅者。

## 原型及试制问题归因

隔离原型先验证四组真实行为：TTY/输入/resize/退出码；leader 先退、后代忽略 TERM 后升级 KILL；后代自然退出；Ctrl-Z、fg、Ctrl-C 及另一个后台作业组。macOS/Bun 1.3.14 全通过，目标退出码分别为 37、23、23、29。日志 `/private/tmp/r7-prototype-*-v4.log`；归档 `r7-prototype-v4.mjs` SHA-256 为 `a3e42e6b1ad44f305362cbd08fb74323b7e5529d8fdfcd65e112b533d2e2b011`。生产接线后删除仓内重复原型，正式测试只经注册的 Process Surface 消费 F# 产物。

以下属于本轮试制候选，不能算成原拆分问题或已发布回退：前台组操作最初把捕获 SIGTTOU 当成忽略；TTY 枚举把用于观察的 ps 自身算入目标组；首个 F# 接线有语法/回调 arity 错误。均在候选阶段修正。第一轮构建因已知语法错误主动中断，第二轮编译失败，不计为通过。初版失败留下的 7 个确切测试 PID 已逐个核对 SID 后清理，原生 waitpid 探针的子进程也已回收。

安全自查把外侧 PGID 信号改为组内自身信号，去掉编号复用窗口；作业组在加入前消失只返回 Unavailable，不发信号、不清账。自动审批曾拒绝仅凭 TTY 枚举信号的原型；补 SID 核验、同会话入组及组内信号后，按更窄路径完成验证。原 O-01 的 review 结论没有被推翻；`fbd90f4d5` 原型交付时尚无新增非作者独立复审，本次复审另记后文。

## 验收与资源证据

| 正式落点 | 证明 |
|---|---|
| process-execution 003 | 目标先退不完成；TERM/KILL、自然退出和 sibling 隔离；见证丢失拒绝已等待的 drain |
| process-execution 006 | 真实 Ctrl-Z/fg/Ctrl-C；不同后台作业组；快退作业和长作业并存；确切 PID 收尾 |
| process-execution 010 | 三个 fd 均为 TTY、输入、resize、真实目标退出码；完成副本和异常隔离 |
| managed-session-lifecycle 025 | 原 leader-exit TODO 转正；自然结束；owner 被 SIGKILL 后保留 Unknown；既有共享端口和 COMMIT 前后故障矩阵 |

generation 496 的 025 为 **64/64 pass**。generation 498 完整 Process 为 **202 tests / 197 pass / 0 fail / 5 TODO**，归属链为 **494 / 466 / 0 / 5 skip / 23 TODO**，format/check 均 exit 0。日志 `r7-process-final.log`、`r7-ownership-final.log`、`r7-format-final.log`、`r7-check-final.log` 均在 `/private/tmp/`。这些属于该候选；资源调整后的最终版本须重跑。初次小组选集唯一普通失败为 `/bin/ps` 沙箱 EPERM，沙箱外原组 110/110，不记为产品失败。

双 Bun 候选的空闲 owner/组监管者 RSS 为 **61,280 / 59,824 KiB**，目标 shell 2,000 KiB。组监管者因此改为 exec 到仅从专用管道读信号的小型 shell；仍只向自己的当前组发信号。generation 499 六项定向测试再绿，组监管者 RSS 降为 **1,920 KiB**，owner 61,024 KiB。RSS 不是独占内存，这次采样不代表多 PTY 压力基准。试用 `--smol` 未见可归因收益，已移除。证据 `r7-pty-footprint{,-shell}.log`。

Linux x64 / Linux 7.0.14 / Bun 1.4.2 / bun-pty 0.4.10 已验证 generation 498 物理口的 TTY/输入/resize、leader TERM→KILL/自然结束、sibling 隔离和作业控制，四项均 exit 0。公开 Surface 生成的临时验证胶囊 SHA-256 为 `00c9337cdfe839cf8df7374f8db1cea145160759489d96d283babeff454983c9`。它是物理适配器的平台证据，不是 Linux 完整 Host/SQLite 验收。

上述是中间候选的证据；最终 shell 监管者及观测修正的结果另记如下，不挪用旧成绩。

generation 500 的并发完整复验保留一次普通失败：Process **196 pass / 1 fail / 5 TODO**，归属链仍 466 pass / 0 fail。leader-natural 的物理回执为 `undefined is not an object (evaluating 'row.state.startsWith')`；这是本轮新适配器的观测解析缺陷，结果保留 Unknown，没有发布完成，但自然结束被阻塞。未保存该次 ps 原文，不能断定是空输出还是残行，更不能据此直接归咎 Bun。失败日志 `r7-process-shell-final.log` 和 `wanxiangshu-pty-XRGsaO/fault` 保留。

修正把实际生产解析器抽成同一可测试步骤：成员表必须有活 owner 和完整、无冲突的行。空表、缺 owner、残行和重复 PID 返回未确认；最终观察未确认时继续等下一次有效观察，不转成空会话或永久故障。003 增加六个负控和一个完整表正控。该修正不重放命令或恢复旧 PID 信号许可；完成前仍需真实 target exit、全部组监管者退出及本次会话的有效空表。失败测试唯一残留的 owner PID 80462 在确认该会话已无目标或后代后手工结束；这只是测试清理，没有伪造产品 Ended。

观测修正候选 **generation 501 / 174 surfaces / 837 modules**。完整 Process **209 tests / 204 pass / 0 fail / 0 skip / 5 TODO**；归属链 **494 / 466 / 0 / 5 skip / 23 TODO**；不重叠合计 **703 / 670 / 0 / 5 / 28**。025 为 **64/64**，原 leader-exit 已转正。日志 `r7-{process,ownership,format,check}-observation-final.log`；四个命令均 exit 0。针对曾间歇失败的 leader-natural 再做六个并发真实实例，全部通过且确切 PID 退出，见 `r7-natural-concurrent.log`。这不是多 PTY 性能基准。

观测修正候选的 Linux 物理口四项再次 exit 0；胶囊与本地摘要一致，为 `464e6852036af9112a21e6ea8714561c0306ec9eb40e41c13550eaf0f194c422`，主机复核没有遗留的 PTY worker。Bun 1.4.2 / Linux x64/glibc 的证明范围仍只限该物理口，未扩成完整 Linux Host 验收。

编译型宿主补验前的 clean 候选为 **generation 502 / 174 surfaces / 837 modules**，`r7-build-delivery-clean.log`，exit 0。实际 verifyPackage 打包/解包、隔离 npm ci、默认入口消费全部通过：**2074 members / 13,189,411 bytes**，tarball SHA-256 **`5d154a651a50adfbd5e319ed917f85ae31e6c73c64c65d84882fe09b6ee3c786`**。随后再次从同一精确 tarball 隔离安装，在仓库外通过已打包 Surface 真正启动其 worker：TTY/输入/resize、目标退出码 37，以及目标先退 23、后代仍活时不完成、KILL 后确切后代退出，全部通过。没有借用仓库运行依赖。脚本 `/private/tmp/r7-package-proof.mjs`、`r7-package-pty-consumer.mjs`，日志 `r7-package-delivery.log`。该摘要保留为中间候选，最终包另记。

R6-A 在 `fbd90f4d5` 交付时进入 REVIEW。该提交收尾刷新至 generation 506，包摘要保持一致，正常推送 master；其非作者复审与后续修正见下文。Linux 的临时源文件已删除；远端工具要求明确确认才允许递归删除其验证依赖缓存，因此该缓存暂留，不存在遗留 worker。没有部署运行中的 Host。

## 编译型宿主补验

交付复核补足了本轮新监管方案的宿主适配边界：普通 Bun 运行通过，不能替代编译型 OpenCode 的自启动证明。按 [Bun 的独立可执行文件约定](https://bun.sh/docs/bundler/executables#act-as-the-bun-cli)，内部 worker 的环境显式设 `BUN_BE_BUN=1`，用当前宿主可执行文件解释已打包 worker，不依赖另找一个 Bun。目标命令 exec 前通过原生 setenv/unsetenv 恢复调用方该键；内部启动标记不能泄漏，宿主环境也不改写。新 worker 使用 JS 环境快照，避免只继承旧原生环境而遗漏调用方修改。

001 新增真实 OpenCode 1.18.29 / Bun 1.3.14 的两项：原键不存在、原键为 original-value，均核对真实 TTY、退出码、独立环境哨兵及宿主键未变。最早两项红为 MODE:1、哨兵为空；当时夹具只改 JS 键，未同步原生环境，不能把该结果说成“原生无标记时启动失败”。正式夹具已同步 C 和 JS 环境，两项 **2/2 pass**。该补验和试制覆盖缺口归本轮，未归因给原拆分，也未声称完整线上 Host 已部署。

最终源码 **generation 504**：Process **211 tests / 206 pass / 0 fail / 0 skip / 5 TODO**，归属链 **494 / 466 / 0 / 5 skip / 23 TODO**，合计 **705 / 672 / 0 / 5 / 28**；format/check exit 0。日志 `r7-{process,ownership,format,check}-compiled-final.log`，真实宿主先红/转绿记录 `r7-compiled-host-{red,green}.log`。Linux 四项用该版本再验通过，胶囊 SHA-256 `53cb5af1e488128e13d17b7b1e1fb446cdcf6121fce1315ae8175195de5df918`；其中作业控制夹具一次把 text 字段当方法，尚未到 Ctrl-Z 断言便失败，修正后该项通过，未记作产品失败或第一次成功。

最终交付 clean 为 **generation 505 / 174 surfaces / 837 modules**，`r7-build-compiled-clean.log`，exit 0；新增 PROC-001 Surface 映射后的 `r7-check-delivery-final.log` 亦 exit 0。精确包 **2074 members / 13,190,294 bytes**，SHA-256 **`2e9d79e61516fdd699a0f5d78bd4eabfed43eaac93b87c2f354cdbf9dd76657d`**。verifyPackage 的真实打包/解包、隔离 npm ci 和默认入口消费通过；随后从同一 tarball 再隔离安装，分别用普通 Bun 和已安装的 OpenCode 可执行文件运行包外消费者。两者均证实包内 worker 的 TTY/输入/resize、目标退出码 37、内部启动标记不泄漏，以及目标先退 23、后代仍活不完成、KILL 后确切后代及 owner 退出。包内模块及依赖均来自隔离安装，OpenCode 可执行文件取自主仓已安装版本。日志 `r7-package-compiled-final.log`，脚本 `r7-package-proof.mjs`、`r7-package-pty-consumer.mjs` 均在 `/private/tmp/`；没有以中间候选摘要代替最终包。

本地最终需求命令：

```sh
TMPDIR=/private/tmp node --test requirements/process-execution/tests/*.test.mjs
TMPDIR=/private/tmp node --test requirements/work/tests/*.test.mjs requirements/managed-session-lifecycle/tests/*.test.mjs requirements/session-ontology/tests/*.test.mjs requirements/crash-reconciliation/tests/020.test.mjs requirements/delegation/tests/{003,004,006,024,026,027,032}.test.mjs requirements/participant-horizon/tests/{009,010,011}.test.mjs
dotnet fantomas --check src/Wanxiangshu
node scripts/check.mjs
```

## 独立复审与修正（2026-10-16）

三名非作者分别核查物理会话/信号、生产接线/归属、宿主/验收：`pty_scope_review_r8`、`pty_integration_review_r8`、`pty_host_evidence_review_r8`。对照 `ec37ab8c2 → fbd90f4d5`，确认以下均属本轮新监管方案的问题，不挪给原拆分：

| 问题 | 真实反例与归因 | 修正 |
|---|---|---|
| R8-01 / P1：owner 不响应时排空无界等 | 暂停本次 owner；TERM/KILL 已排队，6,200 ms 后仍 Running、无 fault、drain pending。5 秒期限只在被监督者内执行 | 宿主侧使用单调时钟，约束启动、最早未转发请求及首次 KILL 后的范围结束等待；转发回执或重复 KILL 均不能冲掉结束期限。超时保留 Unknown/归属，不伪造退出 |
| R8-02 / P1：直接前台作业 Ctrl-Z 连同组监管者一起暂停 | 真实 `exec sleep 60` 的 target、anchor 均 T+；KILL 已转发但 6,200 ms 后仍活。原直接组信号不依赖可运行的 anchor，属新回退 | owner 忽略 TSTP；anchor 入组前原生忽略 TSTP，exec 后 shell 保持该处置；目标交接前台后恢复 TSTP 默认行为 |
| R8-03 / P2：macOS 目标退出信号错名 | SIGUSR1/USR2 回执成 SIGPWR/SIGSYS。Node 原生对照正确，Bun child_process 和 Bun.spawn 均错误，不是发错信号 | 仅目标创建改为原生 posix_spawnp，由 owner 对直属目标 waitpid(WNOHANG) 回收真实状态；正常高退出码与信号分开，本机信号常量命名，不造固定逆映射 |

R8-03 的运行时原因与 [Bun 上游问题](https://github.com/oven-sh/bun/issues/35296) 一致。原生实现按 [POSIX waitpid 契约](https://pubs.opengroup.org/onlinepubs/9699919799/functions/wait.html) 只等待本 owner 创建的确切孩子，不根据目标 PID 消失推断退出；等待错误继续 Unknown。macOS 的 WNOHANG/退出位布局同时核对本机 SDK `sys/wait.h`。未引入 C 构建、外部 Bun 回退或库层 OS 查询。

独立反例在 `/private/tmp/r8-pty-{owner-stop,stopped-group}-probe.log`、`r8-independent-{signal-env-all,native-status}.log`。003 新增 owner/anchor 暂停后有限 Unknown 两项；006 补直接目标 Ctrl-Z；010 补七种实际信号与 0/130/158/159/255 五种正常退出码。四项在交付产物上 **0 pass / 4 fail**，`r8-review-regressions-red.log`，不计为成功。原生 posix_spawnp/waitpid 11 项隔离原型先通过，`r8-posix-target-status.log`，不以原型成绩代替生产证明。

接线分支初轮独立验证：Supervisor **17/17**、真实 Host 五项 **5/5**、故障先于/发生于 drain 和 parked read/queued write 的三组反例均通过。原有旧工具退出码呈现缺口未被这次修改引入；原 leader-exit 修复方向成立，未观察到本轮新增假 Ended。沙箱 ps 受限的四项失败在相同沙箱外重跑通过；该轮六个残留 owner 已逐个核对确切 request、命令及 SID 后清理，`r8-integration-cleanup.log`。夹具的非法 agent、漏接信号 handler 不算产品问题；没有 review 结论被实证推翻。

generation 508 的三个修复均经发现者复验：正式暂停/直接 Ctrl-Z **3/3**；独立启动暂停约 **5,019 ms** 进入 Unknown，重复 TERM 仍约 **5,022 ms** 拒绝 drain，已转发 12 次且重复 KILL 的 anchor 暂停仍约 **5,106 ms** 报告范围结束超时。端口/归属保持、零完成事件，恢复本探针持有的确切进程后确认全部退出。信号正式矩阵、原 USR1/USR2 反例、编译型 OpenCode **2/2**、四个并发 leader-natural 实例全部通过，未观察到 Bun 抢收原生目标。证据 `r8-scope-independent-{formal-green,startup,owner,anchor,direct}.log`、`r8-independent-{native-status,compiled-host,signal-counterexample}-green.log`、`r8-independent-natural-{1,2,3,4}.log`。

最终静态门禁曾在 `r8-check-final.log` 拒绝新增的两层 if；已局部展平退出观测，未压低门禁或加豁免。之后 clean **generation 509 / 174 surfaces / 837 modules**、format/check 均 exit 0，日志 `r8-build-delivery-clean.log`、`r8-{format,check}-delivery-final.log`。509 首次完整 Process 为 **209 pass / 1 fail / 5 TODO**，失败是旧 `draining.mjs` 对最终 JSON 直接写入、观察方仅凭文件存在即解析，读到空内容。该文件先前最后修改于 `ffe028797`，不在本轮 PTY 改动内；单列为 **R8-04：既有测试夹具竞争**，不归为产品回退或 review 误判。伴随的测试后 TypeError 未获独立定因，不据此写死因果。

两行夹具现改为同目录 `.pending` 写完再 rename 发布；不改变目标退出、deadline 或 cancel 的判定。非作者复审两行改动，并在同生产字节的 metadata **generation 510** 重跑完整 Process：**215 tests / 210 pass / 0 fail / 0 skip / 5 TODO**；与其并发的 004/006 deadline/cancel 对照 **2/2**。没有再次出现 JSON 解析或异步 TypeError。归属链在 509 为 **494 / 466 / 0 / 5 skip / 23 TODO**；不重叠总计 **709 / 676 / 0 / 5 / 28**。日志 `r8-independent-process-fixture-final.log`、`r8-independent-draining-control-final.log`、`r8-independent-ownership-delivery.log`；509 首次失败 `r8-independent-process-delivery.log` 原样保留。508 的旧包与测试成绩不冒作本次最终身份。

最终精确 tarball：**2074 members / 13,193,634 bytes**，SHA-256 **`cba299600d29c24133acf111a14368924f7fd70154f82a72a72331fc0bd1a16b`**。真实打包/解包、隔离 npm ci、默认入口消费及包外 Bun/OpenCode 两种宿主的真实 PTY/环境隔离/后代排空全部通过，`r8-package-delivery-final.log`。验证脚本沿用 `/private/tmp/r7-package-proof.mjs`，被测产物为 510 的当前精确包。

同一 509 生产字节在 Linux x64 / Bun **1.4.2** / bun-pty **0.4.10** 的物理口九项并发复验全 exit 0：TTY/输入/resize、两种 leader 后代、交互作业、快退作业、直接 Ctrl-Z、两类暂停 Unknown、七信号/五正常退出码。正式 helper 原字节与本地一致，SHA-256 `12cbabbfa2780aba697ee755a46c9a7ea0e6201a3d306a00a87d9197e03e95b0`；公开 Surface 的验证胶囊 SHA-256 `80cfe247fb1cadd24dc752779e13dd7c7391bae912c0d816a668c6fcb716d6c1`，两端一致。原始结果见 E5-2699A session 28 回执，本地摘要 `/private/tmp/r8-linux-final-summary.json` 明确只作回执汇总。pgrep 复核零 worker；两份临时源文件已删除，沿用的依赖缓存未改。没有把物理口验证扩成 Linux 完整 Host 验收。

三名非作者在已测范围内未发现其余新回退。R6-A 的声明范围按本提交进入 DONE；原 O-01 和 GAP-133 整体仍为未完成。AGENTS.md 所要求的局部手工改动、Fable 构建、编号测试及真实消费证明均保留；未引入 .NET 编译构建、强推或线上部署。文档收尾只刷新元数据并复核同一包摘要。

## 剩余边界

本包可证明的物理范围是仍连接本次控制终端的 POSIX 会话，包含 shell 建立的其他作业组，以及关闭标准 fd 但仍在会话的后代。主动 setsid、TIOCNOTTY、隐藏/脱组守护进程不在已证范围。跨进程每轮持久准入仍属 R6-B，重启接管属 R6-C；owner 丢失只保留 Unknown。O-01 与 GAP-133 整体不关闭。

运行依赖 Bun、POSIX 和可读取的 `/bin/ps`。平台证据限 macOS arm64、Linux x64/glibc；Windows、musl、多 PTY 容量和断电恢复未验收。未做极端 fork/退出压力下快照原子性的证明，也没有可复现反例把它另记成产品缺陷。物理口保留目标退出状态，本包未迁移所有旧工具完成文本的呈现协议。没有部署运行中的 Host。
