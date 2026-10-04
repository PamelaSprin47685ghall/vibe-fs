# verification-system 测试说明

Node/npm 回归按实际声明的 transitive 依赖选缺库叶，不将某个 npm 版本的包名硬编码为通用结构。完整工具归档正例固定0775成员，真实安装在 umask002 下执行并恢复原mask，完整产物模式与库存必须保持；不以 portable 归整模式后放宽摘要相等取绿。远端 Linux 原失败、本地明确 npm10/11 的证明层级与最终 CI 状态见[aaa记录](../../../proposals/archive/2026-10-04/Upstream增量-aaa123b12-2026-10-04.md)。

016 增加选定 SDK 的正式轻量用例与显式 integration 入口；完整归档及原 global.json 驱动实际版本、SDK/runtime 路径、库存与回收断言。integration 要求明确 `WXS_VERIFICATION_DOTNET_ROOT`，缺少该选择不默认跳过；默认层只跳过 integration，外借实际 SDK 反例另外显式披露工具条件。实际计数和证明范围见[SDK准备记录](../../../proposals/archive/2026-10-04/S03选定SDK准备-2026-10-04.md)。不证明 NuGet、实际 Fable、只读执行或实际 verify；原两个 TODO 保留。

[WHAT](../WHAT.md) 定义必要边界及必须统一的方法；本说明解释当前测试，不另立规则。2026-09-28用户确认008按命题要求判断断言完整性，并在成本核查后采用016固定隔离快照方向；016运行器改造与证明仍未完成。测试通过不表示全仓已满足每项证明义务。

## [006] 的测试用例与证明范围

| 目标与观察 | 用例证明 |
|---|---|
| 首次进展前没有活动 | 从监测开始计时，达到静默限值后诊断并结束 |
| 套件完成判定 | 当前运行器的 pass、fail、complete 续期；失败判定也是执行进展，不改变失败结论 |
| 挂起用例持续打印 | stdout、stderr、diagnostic 不续期；记录所有背景活动不是规范义务 |
| 同一目标的新观察 | pending 到 accepted 的样本续期，重复 accepted 不续期；不代表真实业务接线已全部核实 |
| 持续推进与窗口变更 | 总时长可超过静默窗口；调整窗口不能重置最近进展时间；停止后不复活 |
| 诊断不可得 | 缺少、损坏快照以及采集异常、卡住均披露原因；采集有界，诊断先于退出 |
| 单次终止 | 无收集器、采集成功、采集异常、采集挂起及诊断输出抛错均只终止一次；终止后晚到进展和窗口调整不能重启监测，虚拟时间推进覆盖显式配置的静默窗口 |

`006.test.mjs` 使用 `support/watchdog-harness.mjs` 注入真实共享虚拟时间端口。`support/verdict-feed.mjs` 分类运行器事件，`e2e/support/causal-observation.js` 去重观察。事件名、状态值及辅助函数是当前设施，不是唯一允许的形式；去重器不判断业务变化是否真的推进目标。

文件启动和结果流排空另报生命周期事实，用于区分尚未派发、正在运行、已有文件判决但流未排空；这些消息不续期。超时诊断列出活动文件及其最近判决，不把尚未派发的整个选集当作活动集合。watchdog 的 background 计数是本轮累计值，显示的 lane 只属于最后一条消息；Node 每个单文件运行结束都会产生汇总 diagnostic，不能由累计数推断该 lane 输出异常。

连续同步操作与已完成 Promise 可使 Node 子进程的原生 reporter 得不到事件循环调度：测试已完成，父进程却迟迟收不到判决。运行器通过专用 `verdict-transport.mjs` 在测试完成后的 `afterEach` 中让出一次调度，使原生 reporter 能发送真实结果；让步本身不发送进展，不改变静默窗口。006 的真实子进程反例覆盖文件总时长超过窗口但逐项完成、完成若干项后持续打印并挂起，以及 after hook 抛错。挂起用例不会进入完成后的让步，背景输出仍不能续期。

005 的插件夹具回归用子进程加载钩子观察并拒绝真实包入口：仅导入夹具或跳过 integration 测试不会初始化插件；实际创建时仍加载真实入口，导入异常不得被吞掉。夹具其它同步导出及生产模块保持原契约。

共用预算见 `e2e/support/time-budget.js`，这里不抄写数值。虚拟时间证明不能替代真实子进程的退出、泄漏和监测接线；这些 Adapter 用例在 `integration/harness/`。对全部构建和验证阶段的全程覆盖仍需补齐，特别是只设总时长上限的阶段。

ProcessHost 的 health 与项目 `/path` 各自在原阶段 deadline 内观察；同阶段的重试共用该 deadline，不续期。合法在途请求使用剩余预算，不再被 100ms 切片反复取消；只有明确尚未就绪才轮询。HTTP 非成功、非法 JSON/字段、错误目录与同一个 child 提前退出均拒绝，诊断不续期。目录比较采用 canonical workspace，避免 `/var` 与 `/private/var` 别名误拒绝。新增正式物理 harness 分别证明慢响应可成功、拒绝路径和原 deadline 仍会终止；本批完整 harness 285/285。该局部证据不宣称上轮全量项目健康失败的唯一原因已被重现，完整输入结果见[本批记录](../../../proposals/archive/2026-10-03/Host就绪与Guard替代修复-2026-10-03.md)。

`integration/harness/timeout-cases.mjs` 的waitFact用例读取当前EventStore，证明无事实与无关背景记录不续期、声明的进展可续期，并完整核对5个CandidateReady、2个Published及总计7个事实。该夹具分别限制启动和实际等待，启动通知不作为套件进展。七项协议反例覆盖未就绪退出、错误或重复通知、启动静默、监测后无判决、外部终止及后代持管道；预期拒绝之外的检查、回收或排空错误不能算通过，同组后代须实际确认清空。

原 006 的报告计数与结果流完整性用例归 021；因果前沿先于事件尾部的输出义务归 causal-wait-007。去掉装饰性标题、固定辅助入口和事件类清单的锁定，不取消诊断信息本身。原事故样本字段检查不证明挂死判据，已从 006 移除。

## 条款与证据

| 条款 | 当前证据及边界 |
|---|---|
| 001、003、019 | 层级、逐级证明与行为测试原则需结合人工审阅；不靠读取条文或统计断言函数证明 |
| 002、014 | 注册、场景编译及 release 控制的真实 Long Stroke；普通套件跳过真实宿主用例，不能据此宣称发布通过 |
| 004、005 | 受控门禁反例、非零失败、停止后续步骤；不是全仓每一门禁已逐一闭合 |
| 006 | 见上表；全程接线和每种业务进展的有效性仍有审阅工作 |
| 007 | 明确虚拟时间、取消与相同 trace 重放；保留既有业务时序样本。固定排列数是特定 fixture 的有限域，不是通用覆盖要求；持久化样本包含物理边界，不因此成为纯 Temporal 证明 |
| 008 | 按命题要求检查完整结果与副作用；规范要求精确结构或文本时完整比较。没有可忠实判定任意测试断言充分性的机械检查器，仍需人工审阅；不为此添加措辞扫描或恒真占位用例 |
| 009 | 真实目标路径、发现集和执行集，排除路径不存在造成的空跑 |
| 010 | 事件预算和不重试调度的局部证据；不能证明全部冻结判据从未被放宽 |
| 011 | 完整生产分母、未载入模块计零、损坏或陈旧覆盖率报告被拒绝 |
| 012、018 | 已知机械行数/FCS 扫描模式；受控正反例调用与仓库扫描相同的检查逻辑，仍不是任意动态构造的完备检测 |
| 015 | UTF-8/行边界、正文诱饵、损坏、冲突、截断、身份替换、多写者、通知和关闭；不替日志写入方证明原子追加 |
| 016 | 输入集合和步骤边界变更检测；真实 Git 夹具证明 loop-detector 的 tracked corpus 文件（含 proposals）内容和跟踪集合均进入验证身份，Git inventory 失败由生成输入与验证输入收集入口传出，验证不启动阶段。新增输入根、普通输入和corpus父目录符号链接拒绝；真实外部目标反例证明未知映射不被漏收或跟随，输出根链接和同名普通文件保留原边界。这是输入闭包的前置修复，不等价于不可变快照。“阶段内改后恢复”仍是尚未通过的可执行反例；固定隔离快照方案已选，实际隔离、准备时一致性、各阶段同源及结论绑定待证。需区别原工作区继续编辑与快照输入被改写，不能把复制耗时测量当隔离证明。55撤掉的旧Change绑定伪证明仍待真实证据替代 |
| 017 | 日常/发布顺序、范围、失败停止；调度替身通过不代表真实发布执行 |
| 020 | fast-check 固定 seed 与 run budget、失败收缩路径可重放的设施反例；不证明全仓生成器均正确配置，也不代替 oracle 独立性审阅 |
| 021 | 相同结果在不同报告模式下保持一致，跳过/TODO/取消及原因可见，同名不同用例不混淆，矛盾终局不能覆盖失败，真实文件完成和结果流排空。全仓报告链对证据范围的传播仍需核查 |

016的2026-10-04准备增量使用真实Git tree/blob/index证明指定源码身份：工作区/index后改不污染原tree，特殊路径/二进制/执行位保留，SHA1/SHA256均可重构；attributes与replace refs不改原blob，继承Git环境不重定向读取或写回，缺对象/不支持的entry/promisor仓/无法忠实物化的tree都拒绝并回收自有root。该API尚未接实际verify；源码receipt不证明运行期不可改、依赖封闭或全阶段同源，两个TODO仍保留。

原依赖归档准备回归校验明确SHA-256归档、gzip/tar完整性和独立物化目录；真实Node import与完整字节/mode/隐藏文件/内部.bin链接证明所选依赖可独立读取。外部、悬空、循环、重复、特殊entry、链接祖先及受限语法外的路径拒绝，失败不发布root。该API只绑定所选归档与lock字节，不单独证明安装来源符合lock。

2026-10-04接续[真实npm与Node工具准备](../../../proposals/archive/2026-10-04/S03真实npm与Node工具准备-2026-10-04.md)，由正式`016.test.mjs`调用实际owner与支持夹具。下列数字是Node22定向用例报告计数（含父组），不是assert调用数，也不是本批官方选集或全仓验收数量。

| 定向范围 | 已取得的证据 | 仍不证明 |
|---|---|---|
| 真实npm安装：45 passed / 0 failed | 显式Node与真实npm CLI，两个锁定registry包及独立候选内真实import；失败的lifecycle scripts被禁用，恶意HOME/NODE_OPTIONS/NODE_PATH/npm配置不进入执行；真实EINTEGRITY与缺transitive lock失败；registry origin/path、link/workspace/非法依赖及递归override预拒绝，裸本地目录或tar路径也在启动前拒绝；工具SHA/版本/packageManager不符拒绝；挂起实际tarball请求后取消、原Error/null原因保留、HTTP关闭及安装root回收 | receipt的`bootstrap-admission`只绑定启动Node和npm CLI入口准入，不冻结整个npm工具包。fixture实际npm11.18.0，不证明仓库声明11.12.1依赖已实际安装 |
| 完整selected Node/npm bundle：15 passed / 0 failed | 明确摘要归档包含完整选定Node/npm包；从独立root运行实际版本/platform/arch探针；摘要错、入口越界/链接/无执行位、缺真实npm内部模块均拒绝；ambient配置隔离；npm声明的必需生产依赖图闭合于所选npm包，direct `graceful-fs`/transitive `@gar/promise-retry`缺失时不能借父目录实际补包；实际copied CLI新增文件后，即使真实版本正确也不能发布旧成员身份；实际挂起npm探针取消保留Error/null原原因，POSIX进程组与后代退出、pipe排空及ownedroot回收，已取消调用先于缺失归档读取 | `selected-node-npm-bundle`绑定完整所选成员及实际探针；optional缺失允许，存在则递归。仍不证明任意loaded module、绝对文件读取、官方分发来源、OS动态加载库或全部外部工具闭包；Windows子树回收未证 |
| 完整工具归档安装：10 passed / 0 failed | `installVerificationDependenciesFromToolArchive`自己准备并回收完整工具，直接使用原Node/npm角色路径，保留实际Node布局；真实安装两锁定包并独立import；在ci前、依赖物化后及公开发布段完整复核工具，固定toolDigest纳入installation/dependencyDigest；held真实tarball后改非CLI库/新增成员，npm正常结束后拒绝发布并回收；actual Node/npm版本不符拒绝，Error/null取消保留原原因、PID退出及所有ownedroot回收，启动前取消先于缺失输入读取 | 新入口不升级旧raw bootstrap的45项范围；API缺失10失败是入口红，不冒称旧实现已执行工具变更的行为红。边界复核与digest绑定不证明阶段内改后恢复、不可写输入或actualverify同候选 |
| 实际application安装：4 passed / 0 failed / 0 skipped / 0 TODO（1父3叶） | 选定174c2a2533的完整Git tree/sourceDigest，完整Node22.23.3/npm11.12.1归档真实安装仓库236个锁定包；package/lock原字节、toolDigest及11693完整安装成员绑定receipt；darwin-arm64的7项optional存在、16项缺失符合平台取舍；独立候选内实际Fable List/Acorn/Tar消费者通过，全部ownedroot回收 | 本行是原生Node定向证据，不是当前合并树或全套验收；lifecycle scripts禁用，不证明native Host/lifecycle/SDK、实际Fable编译、RO、actualverify或外部Git/NuGet/OS加载闭包 |

归档实现共用`scripts/lib/verification-archive.mjs`，完整核对路径、类型、mode、字节、目录成员与内部链接。工具探针后按独立预期清单重新验证实际物理目录；去掉该复核的隔离变异使新增文件用例失败，这是oracle敏感性证据，不冒充原生产基线缺陷。复核只是步骤边界检查，输入及receipt仍可写，不证明阶段内write→restore不可发生。

早期45项默认夹具实际npm为11.18.0；完整npm11.12.1工具包另取得15项定向通过，这两组本身不证明实际仓库依赖安装，后续独立4项证明范围如上。追加Homebrew Node26的58项定向只有45通过、13失败，缺`libnode`导致真实启动拒绝，原失败保留。后续平台证据统一见[本批记录](../../../proposals/archive/2026-10-04/S03真实npm与Node工具准备-2026-10-04.md)，不从下载完成推导通过。

实际application安装支持夹具为`support/repository-npm-install-tests.mjs`，输入明确来自提交174c2a2533的tree `66fcb921999e598a4cae5904ad5f6f945723c4fb`，sourceDigest `e85e25d5…`、完整工具toolDigest `1e25872a…`；完整身份和此次结果见[590同步记录](../../../proposals/archive/2026-10-04/Upstream增量-590a3f69e-2026-10-04.md)。Fable List操作通过只是实际安装的JavaScript库消费，不是Fable编译成功；原生Node定向4项结果不冒充尚未执行的正式integration选集。

第二批安装入口与输出边界详见[工具归档安装与输出根](../../../proposals/archive/2026-10-04/S03工具归档安装与输出根-2026-10-04.md)。Mac真实只读输入内的可写输出挂载证明reset保留root、只清子项、不改输入和owned挂载回收；`compileIncremental`使用受控spawn替身，只证明输出发布清理，不能算actual Fable只读执行。upstream `590a3f69e` copy/chmod实际审核中，check读取父目录替换后的新字节，再恢复原父目录与文件inode/ctime，verify仍报告PASS、exitCode0。该反例不是TODO通过，也不由归档安装回归闭合；真正只读输入阻止替换或实际替换使运行失效才满足命题。

T418/T419与GAP-055 PARTIAL保留。工具prepare/runProbe取消、完整工具归档安装及选定仓库源码的实际依赖安装已具备有限证据，不重做已完成接线。下一步闭合SDK、Git、dotnet/Fable/NuGet、实际只读输入/可写输出、actualverify统一候选及结论绑定。准备owner的定向绿色不能替代这两条TODO。

009 的覆盖检查实际调用父集成入口和 distribution 子入口的 `--dry-run`，将二者公布的文件计划与独立发现的声明集核对，拒绝漏项、过时项和重复归属。正式入口共用 `support/discover-suite-tests.mjs`：只选择实际 `integrationTest` 声明，不把注释、示例字符串或单独导入当作集成用例；必需目录缺失、非目录以及源码解析错误向上报告。计划核对证明可达性，不代表这些集成用例已执行或通过。

009 的仓库封闭反例先证明合规输入通过，再分别验证未归属生产源与不合规打包白名单被拒绝。白名单直接运行 distribution-004 的同一测试，核对实际断言失败及用例计数；加载失败或未匹配到测试不能冒充违约被识别。该用例不证明真实 tarball 或独立消费者验收。

2026-09-26 既有归属迁移保持有效：原 008 的构建产物用例归 distribution-005、structured-workflow-011/012、js-semantic-surface-006；原 013 的接口用例归 js-semantic-surface-002/003，013 永久空缺。迁移范围见 [本批记录](../../../proposals/archive/2026-10-03/35模块PR施工记录-2026-09-28.md)。

[GAP](../../GAP.md) 区分实现缺陷、证明不足与人工审阅责任。[本批记录](../../../proposals/archive/2026-10-03/35模块PR施工记录-2026-09-28.md) 保存新上游基线的实际验证结果，不沿用旧版本的通过数字。

## 运行

在仓库根目录先执行 `node scripts/build.mjs`，再运行 00—02 条款测试：

```sh
TESTS_MJS_FILES="$(rg --files requirements/feature-ablation/tests requirements/requirement-system/tests requirements/verification-system/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```

运行器检查产物新鲜度并标明 scoped 范围。TODO 是待完成证据，skip 是未执行，均不是通过；TODO 使监督器返回非零退出码，阻止上层整体验收通过。本轮 016 反例尚未闭合，因此上述命令会报告已通过项后以失败退出。此命令不包含完整 integration 或真实 Long Stroke。

验证设施的物理协议回归：`node requirements/verification-system/tests/integration/harness/run.mjs`。它运行隔离的辅助进程和协议替身，不是多个真实 E2E 世界。

日常入口 `npm run format-build-test`，发布入口 `npm run verify:release`。Long Stroke 的设施见 [e2e/README](e2e/README.md)。
