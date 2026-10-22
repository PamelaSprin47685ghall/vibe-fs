# 2026-10-22 两仓合并施工记录

本记录随实际验收更新。固定输入、停止条件和包依赖见[计划](../../proposals/extraction-kanban/UPSTREAM-MERGE-2026-10-22.md)。自有计划提交 `540fe20e7` 合入上游 `939ada1eb`，主仓真实 merge 的 182 个冲突路径已全部消解；未提交或推送主仓候选。

原运行工作树保持 `8db33b3429a70d5cdbea30e393363666b3649573`，用户未跟踪文件 `万象术独特功能导论.md` 未动。构建设置 `XDG_CONFIG_HOME=/private/tmp/m22-build-config`，避免既有构建入口修改运行端配置。本批没有部署动作。

## 已取得的证据

| 范围 | 结果 | 实际边界 |
| --- | --- | --- |
| 构建规划/缓存/CLI | structured-workflow012：42 pass / 0 fail / 10 skip；真实 CLI 缺失 clean 变量先红后绿 | standalone 与现有监督入口均运行。跳过 freshness 仅用于不依赖产品产物的构建工具测试；10 skip 是尚未启用的真实 compiler/integration 条款，不计通过 |
| 独立库最终 Node22 全套 | 1,016 项，1,009 pass / 0 fail / 7 skip，退出 0 | 已包含 capture/publish、sandbox class/pause、最低额度跨层传递、全模块隔离构建与公开包测试；没有 TODO/cancel |
| 独立库最终 Bun 定向 | delivery/programming/sandbox：60 pass / 2 fail | 两红均为 VM 的 post-await 无限循环硬期限；固定 upstream 与旧主仓独立进程探针均复现。不是本次通过项，也不关闭 MF-01 |
| 新库包外消费 | Node22 / Bun 均退出 0 | 实际临时 tarball 安装；继承字段/getter/接收者、toJSON 不激活、global 逃逸拒绝、pause/resume、迟到结算拒绝及最低额度未捕获出口。包 SHA256 `84293b2270d873975ac344a7a325bd328606f134e49d9564ed1747926b3024dc` |
| 主仓 Fable 与 ESM | build15：generation 7，772 emitted modules linked；退出 0 | 本次影响闭包 1,322 条，编译器读取 1,360 个源；153 个公开语义面闭合。freshness 覆盖 1,677 个输入、828 个产物；后续测试与收据修正仍须统一重建 |
| 主仓物理与持久基线 | generation 3：14 文件 369 pass / 0 fail / 5 integration skip | 六个 process-execution 文件含真实 Node/Bun 进程；DE005 的原 10k segment 用例 3,863ms，通过原 5 秒监督预算；真实 Git stdout/stderr 与 Unicode、日志 write/fsync/unlink 切点通过。5 skip 不代证真实 compiler integration |
| 持久化结算失败 | generation 4：MSL028 全 6 pass / 0 fail，监督组已回收 | 原生 EACCES/未知写入保持失败，父 Source 与 join 得到 PERSIST；只结算等待，不伪造完成或重发效果 |
| 交付与效果分离 | generation 4：effect013 全 6 pass / 0 fail，监督组已回收 | 实际 Host 准入及 writer；仅交付时请求可继续，真实停止后重复 provider step 明确 EMR-010 拒绝，账本、见证和已提交材料保持 |
| 子工作真实终态 | generation 5：delegation024/025 全 46 pass / 0 fail / 1 既有 TODO，监督组已回收 | 真实工具交付与后续 Host 停止分别归属，父方读取封存材料；TODO 不计通过 |
| 物理与回调 | generation 5：MSL025 全 123 pass / 0 fail；effect002 全 5 pass / 0 fail | 含真实 Node/Bun 回调与物理停止；同轮 delegation024 的旧夹具失败已在上述独立重验消除，不将旧混合批次称为全绿 |
| 验证工具链 | generation 5：verification016 全 288 pass / 0 fail / 20 integration skip / 1 TODO | 单文件正式监督器完成并回收；不代证 Mac 全发行，多个全批次仍在 Node 工具归档复制/校验处触发原 5 秒预算 |
| 权限与恢复修正 | generation 7：capability025、provider-recovery003 无失败；整批未通过 | 014 两项身份比较失败、MSL022 一项 Bookkeeper 完成边界失败、capability026 producer 原 5 秒预算失败，另有两个误写的测试路径。D11 两个真实子监督器各保留原 5 秒预算，外层新增明确的 14 秒物理回收上限 |

库的首个两父 merge `c1aa9af` 与最终功能修正 `cf54390da393ea92e71eb1a37243b7c77ab94f96` 已普通推送自有 main，主仓 gitlink 已锁定；远端尚无该库的 Actions 运行，不以空状态声称 CI 通过。详细 Bun 基线与安全修复见[库追踪](../../wanxiangli/evidence/upstream-merge-20261022/sandbox-bun-followup.md)。主仓各保留 owner 和待验边界见[子批收据](library-host-receipt.md)。

## 合并时修正的真实问题

- 上游构建 CLI 仍传已删除的 clean 变量；影响闭包、冷缓存、损坏产物和失败构建也补了实际反例。
- 交付材料先捕获，durable writer 成功后发布；writer 对 ChildWork 复用真实准入核对。交付本身不宣告完成，须由该工作的真实终态结合封存材料通知父方；迟到观察不得重开已关闭的权威。真实工具调用与最终停止可以是同一工作内不同的 provider step，分别核对各自因果身份。
- durable 存在而调用归属缺失时显式拒绝交付，普通读取仍可执行。后续 provider step 用 Host 已观察的 session/physical 映射核对，不能把固定的首个 startedEvidence 当作所有后续 step。
- Plan 的交付缺少 owning assistant 的物理 parent 时，在轨迹捕获及效果前拒绝。物理 stop 只结束步骤；仍活跃、仍可交付的工作可由同一输入下 Host 实际观察的后续步骤交付。取消、失败、拒绝或未启动的物理输入不能借此建立材料。
- provider 成功事实写入失败时，以 typed Result 传回 Host：Sync 按当前 root/physical 精确结算等待，Fork 收到带 authority 的真实失败并保留原持久错误；不以调度器日志代替父方结果。真实终态释放资源后，只保留该终态的 Host 步骤身份供下游结算，不恢复执行 lease。
- Fission 接管复用普通终态的同工作见证选择，保留 logical/root/physical 围栏及后续 provider 的真实 Host 观察；发布见证中的封存 View，原散文仍单独保留为 TurnFormalText。修正前该路径只留下见证布尔值，可能用后来的散文替换已交付材料；新增分步交付与停止的真实 Host 反例仍在验收。
- Bun 的 vm global 原型泄漏宿主构造器，已切断 global 原型并复验；暂停时已结算 Promise 的交付延至恢复，且保留终态/迟到结算围栏。Bun 未实现 afterEvaluate 微任务约束的既有硬期限问题单列，未伪造支持。
- 接收新 HostPorts 时修正了未对齐的 F# 签名、Task/Result/option、Fable 不支持的路径 API 和 Task.Delay 的 ESM 缺失出口；OMP 未运行真实 smoke。

## 尚待完成

主仓剩余失败路径、真实 compiler/integration、完整发行、主仓最终合并提交及普通推送、远端精确 SHA CI。generation 7 的 Fission014 两例把日志中类型化身份与字符串直接比较，修正后仍待重验；MSL022 和 capability026 的真实失败仍在修正；structured011/014 的本轮显式清单误写包路径，须用真实 structured-workflow 路径重新运行。Mac 多次完整发行未通过，保留原监督预算和失败日志；用户明确仅在本机验收，停止远端源码传输。所有未运行阶段保持未验，不以源码检查、局部成绩或旧证据替代。

## 后续 upstream 增量

用户再次要求检查并合入新更新。本机 fetch 确认主仓在原固定输入之后新增 29 提交，最新 `155309b880b3684255204bff429278814db4c308`，231 个文件、8,240 行新增、2,768 行删除；独立库上游 main 仍为 `663ba59b8282db5e3eeccda0c1100ea8cdca0dd0`，本地已验证的 `cf54390` 不变。新增范围包含真实权威终态来源、Host 中立接线、工具策略拆分和验证器因果状态观察；须在保留当前修复的候选上继续三方整合并重验，不能沿用旧候选成绩宣称新候选完整通过。

新增差异按 `939ada1eb` 到 `155309b88` 的 Git 三方补丁应用，出现 19 处冲突，逐块手工合成。原候选索引树 `d04504026c7fc4348fded968dfcada03c31a811e` 与完整补丁保存在本机临时证据中；没有提交未验收的主仓中间态。已退出的 `ToolRuntimeScopeSurface.fs` 不恢复，最新的 established-delivery 查询由现有 `ToolRuntimeScope` 承担。

新增静态检查首先发现 12 处控制层次违规及两处格式问题，均须在真实 owner 内修正，不增加门禁基线。明确保留下列边界：Wire 的 typed Result 与 exact authority root、成功写入后才消费 plan；DeliverTool 的真实 assistant/tool/user-parent 归属迁入新 DeliveryFlow 端口；上下文声明仍由 ToolWorkflow 在成功结算后提交，删除薄 adapter 中重复的提交路径；不恢复把 Manager 生命周期改写成 TerminalOutputCaptured 的伪 append 入口；无 journal 的 Host 交付显式拒绝，普通读取继续合法执行。

验证器接收上游真实 runner 状态转换，但仍由本地 NativeTestSupervision 持有唯一计时状态，核对真实 PID、计划、阶段与 runId；重复、错序、未知对象及任意字节均不续期。物理等待的绝对期限不因其他文件推进而改变。未接线且事实核验不足的 tool-lifecycle 辅助模块不作为通过证据。新权威闭包的 managed-source 选择与无 source 的 canonical witness 接受缺口，正在按实际持久事实修正，尚未验收。

build16 的冻结索引树为 `2a55aa60c539581272aae7c1779061bcdcf032b1`，Fable 读取 1,472 个源后在 Bookkeeper 的 TaskResult builder 引用处失败；改用本仓 Fable builder 并明确提升普通 Task，仍须下一次统一构建确认。同期静态门拒绝新测试的 7 处私有 dist 导入/DU 形状读取；将通过真实公共 Surface 观察终态资源，保留强引用身份与最后一次释放反例，不放宽门禁。共享终态接线返回 wrapper 而非 acquire 得到的真实 source，导致引用回收失配的问题已修，待该反例正式验证。

build17 冻结树 `b7b8983186c1fc2856deffeb353369f2a5337038` 构建通过（generation 8，783 ESM，1,701 输入/839 产物），格式及全部静态门通过。随后 18 文件关键批次在首个 verification006 文件结束时失败，只有 1/18 文件完成，无权威通过总数；监督组 `49876` 回收 accepted=true。根因是新进程身份校验与真实 `runner:file-drained` 事件缺字段不一致，不能降低校验求绿；补齐真实 producer 的 pid/parentPid/runId，并以既有真实两文件进程用例核对始末身份。其余 17 文件未获得该批证明，继续保持未验。

build18 刷新为 generation 9，冻结树 `0dd465a3995eb75bcf15cdef5fa9db1a0f1f2cd0`。同 18 文件正式批次完整完成，216 pass / 66 fail，监督组 `52135` 回收 accepted=true；整个批次不计通过。真实始末进程身份协议已通过，006 剩余两项新挂起夹具因没有活句柄而被 Node 取消，不是真正静默等待，须保留实际资源后按原预算验证。主要产品红点是 AgentOwnerRoot 的新 acceptance 错携 HumanRootManagedRun closure kind，导致真实派发 fail-closed；新增闭包夹具还需正确获取 shared journal 能力，错误 source 的语义拒绝遵守既有持久化隔离规则，不能把会写审计事实与 CutTail 的接口误断言为零磁盘写入。其余行为待消除根因后重新验收。

再次 fetch 发现新增 `13c00cb8437b4332939ed38f839bca39b1f252cf`（1 提交、23 文件，643 行新增/104 行删除），自有远端 master 仍为 `540fe20e7`。已将 `155309b88` 至该提交按三方补丁合入，产生 5 处冲突；此前索引树为 `277a71cf7c96aca8bf95e4613fa7ee04fd177aa5`。本地运行工作树不动。此次增量的同物理输入内前序散文归属纳入验收；两项行为冲突按用户“保住本地已验证修复”合成：不把交付事实直接发布成 Completed，仍等真实 Host 终态并保留原 sealed View；不把未知 AgentOwner life 默认为 Child，也不把非法 recorded closure kind 静默改写成合法值。新 delegation033 与 WHAT 同步保留这些强边界，不能以放宽旧反例接收上游实现。

build19 冻结树 `ced8066f417ad8e798de41ae65b70bada705cf79` 的格式检查通过，Fable 在 DeliverPolicy 的 4 处 ToolResultText 调用失败；此前较大的影响闭包偶然带入该模块，较小闭包暴露真实缺失的直接分片依赖。为交付策略分片补上实际 `execution-tool-product` 引用，不能依赖无关模块顺带进入编译。该轮未运行产品测试，须重建后才进入正式验收。

build20 冻结树 `5341f20962934609f05806be165e43f4c0afdf71` 通过，generation 10、783 ESM、153 公开面；格式通过。静态门仅拒绝 PluginSessionWiring 新增的归属领域引用（12→13），整轮仍不计通过。实际 Sync ownership 查询应收回已有 AttachedSessionRuntime，从唯一租约表读取，而不是在三处宿主接线重复枚举角色与推导归属；不提高 adapter-thinness 基线。该轮未运行产品测试。

build21 冻结树 `12b78eca717bbd3658ef4d33fd79e1cb2811e2c5` 通过，generation 11、783 ESM、153 公开面；格式和全部静态门通过。随后 19 文件正式批次在 Fission014 的真实 takeover 处触发原 5 秒监督，只有 5/19 文件完成，无权威通过总数；组 `62267` 的残留进程已回收，验收 accepted=false，整批不计通过。已完成文件中 verification006、IA005、MSL020、PAR003 的 worker 退出 0；IA018 原持久 source/closure 反例完成，新性质例在 `AgentOwnerRoot,false,Child,null` 失败。Fission 的真实失败是通道继承 owner 权威、没有独立恢复预算，却被新成功结算入口报 `NoActiveRun` 并阻断终态；必须修正实际预算归属，同时保留真实持久化失败和旧 root 拒绝，不放宽测试或延长监督预算。其余 13 文件尚无本批结果。

该性质例是公开入口迁移时误用字符串前缀判断，合法 `AgentOwnerChildWork` 并不以 `AgentOwnerRoot` 开头；恢复调用真实分类 owner 判断类别家族，400 次性质检查及全部拒绝反例保留。并行通道预算则由唯一 projection query 按 durable membership 查实际 owner，核对其 active run/root 与既有预算；运行端和 replay 共用查询，持久事实中的真实 lane SessionId/ProviderRun 及去重身份不改，只将预算变化写回被证实的 owner。不补造根、预算或成功，也不忽略真实写入失败。两项修正仍待下一冻结候选正式重验。

build22 冻结树 `66ef6c855924b83fb06d17d24d3703b977700d5d` 构建通过（generation 12，783 ESM、153 公开面），格式通过；静态门拒绝新增查询与单 case 变化处理的两处嵌套 match。将查询改为明确的 option 过滤与映射，在参数处解构单 case 变化，保留相同 owner 校验与 typed 拒绝；不提高控制层次基线。该轮仍未运行产品测试。

build23 冻结树 `1826463ba31d5b30bd49b99b78e2540082fd715d` 构建通过（generation 13，783 ESM、153 公开面），静态门通过，控制整理的两文件格式通过。19 文件正式批次完整结束：287 pass / 3 fail，组 `66652` 回收 accepted=true；整批不计通过。剩余红点为 Fission014 的重复 FissionLaneMaterialized、MSL022 的同 provider 终态原文与 sealed View 混写触发 PERSIST010、delegation033 的新正例把两条 assistant 都留在运行态而被真实准入报 AmbiguousRun。033 改为先执行一次真实普通工具并由 Host 报 tool-calls 完结，再提供同物理输入的下一条 deliver；不放宽多在途候选的拒绝。

build24 冻结树 `3adba6d333449a178b38b94981aa63f034fdc54d` 构建、格式和静态门均通过（generation 14，783 ESM、153 公开面，1,701 输入/839 产物）。19 文件关键批次完整通过：290 pass / 0 fail / 0 skip / 3 既有 TODO，组 `72179` 回收 accepted=true；TODO 不计通过。真实 Fission 重复事件不再追加材料，真实终态原文与 sealed 返回值职责分离，MSL022 原 no-CutTail、日志前缀、实际 Bookkeeper 完成强断言均通过，033 拆分散文正例及三个拒绝反例通过。

同一 generation 的本地保留修复 16 文件批次完整结束：496 pass / 1 fail，组 `74994` 回收 accepted=true。真实进程/PTY、Int64 调度状态、Git stdout/stderr 分路、日志故障切点及 Node/Bun 回调均未出现运行行为失败；唯一红点为 EMR010 的既有源码结构断言仍要求 public/private admission 直接 `spec.Execute`，而上游已抽出 `executeToolBody`。更新两处断言为经过公共正文 stage，并继续核对该 stage 调用原 `spec.Execute`；provider→tool 先后、精确物理绑定和无时间启发式断言全部保留。此轮不宣称完整发行通过。

build25 为 generation 15，产品产物字节未变。EMR010 单文件正式复验为 17 pass / 1 fail，组 `79720` 回收 accepted=true；剩余源码断言还引用已退出的 `attachmentAdmission ctx`，实际 private admission 来自 `ToolAdmissionTable.privateAttachmentAdmitsForSession spec.Name ctx.SessionId`。按真实入口更新检查，保留 attachment 准入先于原正文的要求，须重建并正式重验，不将该轮记为通过。

build26 冻结树 `71d29b8fe374113b359b7eb7b9528a63f56fce01` 刷新为 generation 16，783 ESM、153 公开面；EMR010 单文件正式重验 18 pass / 0 fail，组 `80811` 回收 accepted=true。此前 16 文件的唯一源码断言红点已按实际准入入口消除，产品字节未变。

完整发行第三轮使用独立 Node22、隔离 XDG、`NODE_TEST_CONCURRENCY=4`、`UNIT_VERDICT_SILENCE_MS=5000`、`WXS_VERIFICATION_WORKER_DIAGNOSTICS=0`、`WXS_ACCEPT_TODO=1`，未调整任何原预算。候选输入 digest `85da10f295c66ff0`，格式、静态与构建通过；unit 在 158.1 秒触发原 5 秒静默，112/766 文件完成，无权威通过总数，组 `81308` 回收 accepted=true。integration/e2e/package 均未运行，本轮不计通过。完整日志 `/private/tmp/m22-main-release-3.log`。

该轮暴露了尚未迁移的正控夹具：真实派发的 Child 归属未写入，旧 schema-v2 预期遗漏新增闭包字段，以及交付夹具未报告真实 assistant/tool/provider 观察。必须补齐真实输入，不能默认 Child 或降低准入门禁。另有真实产品问题：SDK error 缺少 `data` 时读取嵌套 message 抛错；park-race 直接等待 ProgramPromise 绕开既有 typed 超时分类；读取交付值的编译依赖混入 Sandbox/Capability/MutationFs；预算 projection/ledger 重复实现独立库应持有的规则。这些修正均须重新冻结、构建和正式验证，不能以旧批成绩替代。

只读远端复核确认 upstream/master 仍为 `13c00cb8437b4332939ed38f839bca39b1f252cf`，origin/master 仍为 `540fe20e7f0c6e0cd53245fa08111e8fadd3dadc`；运行树 master 仍为 `8db33b3`，仅原有用户未跟踪文档。本机验收边界不变，不传输源码、不部署运行版本。

新增预算库冻结树 `915e99a40bbbd172dbbb608d5374af51b866d872` 构建通过；Node 全套 1,022 项，1,015 pass / 0 fail / 7 skip / 0 TODO，Bun 原四例与新六例共 10 pass / 0 fail。真实临时包安装后的 Node22/Bun 消费均退出 0，验证 60 次重置、全 run 身份、旧成功拒绝与旧公开 API；包 SHA256 `24953f9b4cb16dc15fda1c74f6a03ae166d6f59b4fc2b8f5e99e05e54aa438d4`。库源码尚未再次提交，主仓预算接线仍待正式验证；既有 Bun 两项 VM 硬期限失败不因本轮纯预算验证被覆盖。

build27 冻结主仓树 `527511ee2cfc0b13714560470c414f950a055fa1`，格式通过；Fable 的 1,456 源闭包在 Projection.observeFailure 的 reason 被动态调用推成泛型、与公开 string 签名不符处失败。静态门同时拒绝该结果解码的两层嵌套。补明确参数类型，并把同一 closed vocabulary 的 Result/Record/Replay 解码改为平面匹配，不改预算规则、不提高检查基线。未运行该轮产品测试，须重建后正式验收。

build28 冻结树 `1f4a6974461528a97abc8d8a8d140a055e54bb55` 通过（generation 17，783 ESM、153 公开面），静态门通过。27 文件正式回归在 19/27 文件完成时被 advisor004 的 5 秒静默阻断，无权威总数；组 `20639` 中残留 worker `21192` 已回收，accepted=false，整批不计通过。process004、RP009、预算 owner 与 delegation028 的 worker 均退出 0；RP031 各例完成、worker 退出 0，但因整批未完成不记独立全绿。

剩余可见红点包括 ST013 三项把完整 sealed `[[returned]]` View 误认成裸字符串的预期、JNGD 两项裸写根但缺少实际物理 landing 的正控，以及 advisor004 四项真实 managed acceptance 缺少可证明生命周期。前两类按真实输入/完整封存材料修正；advisor 的资源未退出是实际阻断，必须查清 seat 所持 owner 证据如何进入统一准入，不能按角色猜 life 或仅放宽测试。其余 8 文件未获得本批完整证明，继续保持未验。

已通过独立库验收的预算增量与收据以 `003995a0e668b26ee97780c474188d5421a1a240` 提交并普通推送自有 main（`cf54390..003995a`），工作区干净；不代表主仓已交付。源码相对前述已测树仅补 AGENTS/收据记录。主仓须更新精确 gitlink 并继续验收，运行树仍不变。

主要原始日志位于 `/private/tmp/m22-*`；最终收据将记录实际执行命令、候选提交和结果摘要。历史 upstream 证据中的原始空白与失败结果原样保留，不改写日志求 diff 干净。

用户再次要求检查新增upstream。实际fetch发现 `13c00cb84..720ef576d` 共5提交、51文件（853增/1164删）；origin仍540fe20。保存索引树 `59ee7585ddc62558c26fba703b3dcf22acb8a1d8` 后按真实三方增量合入，12路径冲突人工收口；原始补丁 `/private/tmp/m22-upstream-followup-720.patch` SHA256 `aa8d4e250b33d069f7f89426b1849f54bb16bcc8956ccc84b9a91f3f93e9e69a`。wanxiangli独立处理，普通两父merge `63286b4` 保003995a与b1ba965祖先；除AGENTS外blob/mode完全相同，已普通推送自有main（003995a..63286b4）。

本轮合成保留已验Callable工程循环，新增string call直接使用同一实际ProcessHost资源核；不接上游rawspawn丢失真实Closed与完整采集的回退，不用0/-1伪造signal/失败终结。自动yield经原park机制等待unpark，同ParkState只允许一个异步权限/输出drain flight；signal/send绑定实际令其park的ticket。name可选，空/重复显式name在程序前拒绝，不引入定时删现场代替取消与物理回收。相关新强反例与同一冻结构建仍待验收。

Advisor proof现来自同一实际Journal内已成功创建的Seat child，经真实PluginHostWiring组合后注入统一准入；pending、失败、错Journal、owner/lifecycle冲突不形成证明。Sync的View与实际TurnFormalText分为独立字段，只有真实Host终态及非空实际散文归属校验通过才返回sealed View。测试已迁真实Host输入，新增同label不同Journal及pending→Ok负例；尚未正式运行，不记通过。empty mapping新例改用真实插件/事实/终态，因canonical capture仍含真实prose，不宣称该例证明字面空sealed View。

build29 冻结树 `ef474ecab52d9837bb09025d0435ae8eaff47d5b`，全仓静态门通过；上游OpenCodePort一处格式已整理并复验格式通过。Fable在新TakeDelta提前return与后续Task unit不统一、waitCommand的ParkState缺明确类型两处失败（1,464源闭包），未运行产品测试。精确补类型后须重新构建。命名park最终保每次唯一handle，name只作可读标签，旧unpark不能因同名新现场复活；030新增实际重放拒绝反例，未验不记通过。

build30 冻结树 `e5c60eb9bf99a24a92e7e7c98e2b1c3769a015fe` 通过（generation18，783模块/153公开面，1,702输入/839产物）。18文件正式批次完整结束：211 pass/10 fail/2 skip/5既有TODO，22.66秒wall，组32266回收accepted=true；整批不计通过。Advisor实际归属与新增跨Journal/pending证明反例均完成，Sync/Sphinx双字段、强交付/终态边界通过。新增string call在Fable对Task.IsCompleted缺运行支持处失败，导致真实并行第二命令被原失败清理；同时015漏公开execute导入。033的旧raw getter与sealed TOML预期不符、crash020仍断言已更名的ChildRunVoided。按真实状态和公开接口修复，不调高预算、不抹去原反例；须重建复验。

验收前复查又发现 `32ec1423745602ce1ea433bade781b8f3d941bb5`（1提交/12文件），origin仍540fe20。本批最终目标固定于此；库增量c2afb1af另行三方合并，主仓11个规范/资源/测试路径clean合入。增量前索引树为 `b541c5c7360c756b26b68016a8cd39df4eb3fb93`；主仓补丁 `/private/tmp/m22-upstream-followup-32ec-main.patch` SHA256 `c4e1a433bb91138826b4e5c350ed2abfe91b6a7d1ae7e20c2ec66cf29b93daac`。新法则未列锚点不传、null显式保留；库与真实主仓消费者须同候选验收，原sealed capture/实际散文/提交后发布/终态证据边界仍保。

库c2合并的冻结树 `1c00afb4d1b828abcfd2a289d2654e8f966131bf` 构建通过；Node22全套1025项，1018 pass/0 fail/7 skip/0TODO，74863ms。Bun compression001/002/003与failure-budget001/002共29 pass/0fail；真实临时包离线安装后的Node22/Bun消费均退出0，SHA256 `d3e326e046e0bc75405dfc6e89454d5256a761ad5ace700c2e4c4a25d553257c`。只补收据后普通两父提交 `0ef8ad1608615118b178e148263b56660d04be4a`，父63286b4+c2afb1af，普通推送自有main。新drop/保护前缀/null/后来输入及strict undefined反例通过；原Bun两项VM硬期限失败仍不计通过。主仓新gitlink和全部实际消费者待下轮构建/验收，不能用库通过代替宿主通过。

build31 的树 `dc5fc238f84e3c958a50de67ab6368689222e6a5` 构建通过（generation19），但英文资源缺少新增的 `Record<string, null|string>`，静态语言对齐门拒绝，未运行产品测试。按同一真实规则补齐英文声明，不增加标识符或检查基线。build32 冻结树 `2d1d41e26d60c054f04779ee86b339c6c25693bb` 构建通过（generation20，783模块/153公开面，1,702输入/839产物），全仓静态门通过，F# 输入较已通过的格式检查不变。

generation20 的25文件正式批次全部完成：267 pass/7 fail/2 skip/6既有TODO，23.88秒wall，组39969回收accepted=true；整批仍未通过。新视图031/032、编程053/054和真实封存交付033通过。七项失败分布为直接命令015三项、crash020两项及Advisor002/004各一项：正在核查命令成功发布与实际关闭/期限归还的先后，以及park恢复、嵌套身份和显式保留锚点的测试输入。所有预算保持原值，skip/TODO不计通过；下一候选须统一重建后复验。

七项失败后的修正已冻结待重建：直接命令只有在真实关闭、采集退休及BorrowDeadline归还后才发布已关闭的结果；未知关闭仍及时失败并由实际owner保留到真实关闭，清理失败保原cause。015启动真实unpark后再等输入，显式park按实际raw返回值消费，原期限与SIGTERM/Closed强断言保留。crash020改完整wire身份精确断言，仍验legacy活跃投影清理/Abandoned/horizon及只作废一次，不据此宣称GAP-123 typed source producer闭合。Advisor004显式null保留实际caller锚点；002分别验证prefix0无增量但有header与非零保护前缀的精确材料，未以假新材料驱动header。

build33冻结树 `12414313c4e6ea38c81492b6d915ed96d03a1ea0` 的格式和静态门通过；Fable在 `SetException` 的exn/seq重载推断失败，未运行产品测试。补明确exn参数类型后重新冻结重建，不改变失败处理或期限。

build34冻结树 `99b29d7c1115b4eccb644e604fa6fa878e2c47ae` 通过（generation21，783模块/153公开面）。同25文件正式批次273 pass/2 fail/2 skip/6既有TODO，24.55秒wall，组43997回收accepted=true。直接命令的并发输入归属、显式park及原期限耗尽反例均通过，Advisor全部通过。剩余crash020两红显示actual activeRun已清空，但公开childView仍绕过规范查询、直接读取旧binding的Active；实际scoped work已为Abandoned。须改用现有HandleProjection当前work摘要，保原强断言后重建复验，不新增第二份状态。

build35冻结树 `7b7693a4876afc5b87aa34c0fbf68279b44861a3` 通过（generation22，783模块/153公开面）。LoadRecoverySurface只改用现有tryFindByChildSession，稳定binding与实际work的职责不变。同25文件正式批次275 pass/0 fail/2 skip/6既有TODO，25.24秒wall，组46251回收accepted=true；原5秒监督预算不变。skip为未启用的真实durable integration，TODO仍单列，不算通过。接下来固定同一产品输入运行完整verify:release，不能用该关键批次替代完整发行。

完整发行第四轮冻结树 `4d3bc9852f45c55e3f4d57f729758e8dcf6b4471`，4,258个验证输入摘要 `fe2f0501f27315dde645536a1f789c14c3902842eaee2923d6ff8a3467ae5750`。格式、静态、构建通过；unit在8/766文件完成时触发原5秒静默，unit45.1秒、全轮62.6秒，无权威总数；integration/e2e/package未运行。活跃为verification006/016、MSL025、DE005，structured012此前已在2.292秒完成。完整发行仍未通过。

该轮中间回收因真实ps查询超时失败，最终accepted=true仅核主组48243，不能代证全部后代。只读现场确认006的detached fixture组53038/子53040仍存活（synchronous-verdicts、明确属于本轮）；已精确TERM此组并用ps确认两PID均退出，没有操作运行服务。源码核到监督器只杀主组，却只等待已捕获的不同PGID后代，须修真实终止与全组核验。016另在真实probe输出回调内同步复制135,447,552字节工具树、chmod及全hash，拟先备好同字节不同inode的真实replacement，边界只执行两次rename，保原拒绝、仅初始版本探测一次及无registry请求的强断言；不提高预算、不缩减真实工具或10k段反例。

监督器最小修正沿用同一回收owner：保存快照中实际后代的PID/PGID见证，终止前按新真实快照复核仍有该见证且不是caller组，实际终止已证组后在原绝对1,000ms内核验；捕获或清理失败保原cause，accepted不能仅由主组为空判真。此范围只证明已捕获的实际cohort，不宣称整个可继续fork的未观测闭包。016复用原先真实构造并验证的完整selected树作为replacement，临界点只改根权限并两次rename；拒绝后仍全树核对原件和替换件的字节/权限、不同inode与无额外版本/registry执行。待统一构建及006/016/MSL025/DE005四文件正式复验。

build36冻结树 `40a9eb48eefd537223e668a2b1772b685a81aedf` 通过（generation24）。四重文件N4批次在15.402秒触发原5秒静默，0/4完成、无权威总数；组57027及已证后代回收accepted=true，44.703ms，未再出现回收错误。随后同一候选N1单文件DE005完成33 pass/0 fail，11.52秒wall，10k段一项4,209ms，组58276回收accepted=true。再按N1运行006/016/MSL025，006于46.083秒file-drained，无可见断言红；016在真实Node身份探测触发5秒静默，MSL025未开始，整批无权威总数，组58582回收accepted=true。这些不算完整发行通过。

016的真实Node子进程spawn到exit0为4,070ms，前后还有解档和完整输入核验；没有证据表明stream drain死锁，npm版本探测尚未开始。独立本机诊断把当前Node复制为全新inode并核同字节，首次启动3,230.6ms、再次50.7ms；只证明启动耗时差异，不推断具体系统机制。现有工具链只有AbortSignal，没有执行绝对期限，monitor的1,000ms只是退出后的收尾期限；不能把它登记成执行预算，也不能给nested调用偷加物理许可。

下一步按既有006有限物理合同收窄真实探测边界：工具owner/monitor/probe贯穿可选的同一绝对deadline，启动前到期零派发、到期真实回收、迟到成功拒绝；新增强反例。实际Node身份与npm版本组成一次真实物理段，独立顶层调用在执行前获确认，至多5,000ms且不续期；解档、核验和断言仍受普通门禁，包装者不能替换实际结果或脱离真实owner。普通5秒静默、原物理收尾预算和所有负例保持；这不是把整个测试改成较长超时，修改后仍须统一重建及正式复验。

该闭环已落盘待复验。绝对deadline只由显式option传入，ambient私有字段不成为第二来源，也不传给真实工具；monitor及owner关闭观察保留到期失败，不用迟到exit0覆盖。probeScope是窄执行policy，实际结果和唯一promise仍归内部producer；scope结束后封存run，提前结束则取消并等真实关闭。新增await后首次派发前重核实际owner和完整清单；无scope旧路径不增加这次额外hash。七项scope边界负例、五类真实工具deadline负例及五个独立顶层真实Node/npm物理case已补，静态语法/diff检查通过，尚未记行为通过。准备阶段解档/初始清单及结果断言不借物理许可，执行中必要身份复核保留在同一段。最终只去除monitor已报告的同deadline重复错误，独立cleanup错误仍保留。

build37冻结树 `b35299b548e64dea976dec2c562189fd2ee5fef3` 构建通过（generation25，783模块/153公开面）。006/016/MSL025的N1正式批次触发原300,000ms总回收门禁：006于50.219秒drained，016于264.169秒累计drained，MSL025已开始但未完成；没有权威总数，整批不计通过。主组65514及本次已证后代均已回收，残余检测使accepted=false，不冒充成功。

完整发行第五轮仍为同树，4,259验证输入摘要 `3ff02b580fd9e13ce82d3e39a3ba005ba2ff20c0c4877c2f9ce0f69c19d138b7`。N2下格式6.2秒、静态5.4秒、构建1.4秒通过；unit在136.5秒触发原5,000ms静默，全轮152.0秒，4/766文件drained、无权威总数；integration/e2e/package未运行。MSL025于135.093秒累计drained，016正在嵌套的真实npm安装null取消场景；已捕获进程回收，不能把文件drained写成权威单文件通过。原日志 `/private/tmp/m22-main-release-5.log` 与 `.fable-build/verify-logs/2026-10-22T15-46-12-622Z/unit.log`。

该场景的初始真实Node启动2,146ms，从首monitor派发到ci monitor真实关闭为4,898ms，初始准备、完整身份重验及后续清理仍有额外耗时；没有archive-install-settled，不能猜测它已经完成。源码显示archive安装入口没有透传已实现的probeScope，测试还在执行期间启动nested invocation。下一步只透传初始真实probes的scope，并把这些实际archive场景改成独立顶层、保留全部材料/身份/取消/回收断言；不把整个安装借入物理许可，不提高预算，结果交正式复验。每个顶层仍只能用一次有限许可，end不续期，该绝对期限持续到顶层实际完成。

build38冻结树 `cfa1e8c5fa90bc436d22735d315d83d38b4d0bd7` 通过（generation26）。正式016单文件N1完整结束：289 pass/2 fail/20 skip/1既有TODO，195.54秒wall，组87126回收accepted=true；没有静默超时，整批因两红仍未通过。非CLI mutation明确在有限许可入口收到 `No unique ordinary top-level native invocation`；replacement未启动monitor，但原拒绝原因被必须观察实际probe的断言遮住，尚不能断言两者同因。下一候选将capture helper里的子测试层也内联成普通准备与断言，结构上消除一处可见重叠来源；监督器仍要求实际唯一顶层，不放宽规则或加等待。replacement强断言保留且补原拒绝cause。20个integration tier skip和1个TODO不计通过，真实tier验收仍待运行。

完整发行第六轮冻结树 `2734ead7b09a63fe0c0794a753e5699f4e06dd48`，4,259输入摘要 `201502daaf00698f91d807e560945318ca36ffe2caa7f4316ee8dc8e9d3729e3`。格式8.8秒、静态6.5秒、构建5.7秒通过（generation27）；N2 unit于原300,000ms物理总门禁截断，300.7秒，全轮324.4秒，58/766文件drained、无权威总数。此前两红未再出现，016于267.795秒累计drained（自身214.244秒）；MSL025、DE005及EMR010也已drained，但这些不升格为完整权威通过。主组95856回收accepted=true，23.320ms。integration/e2e/package未运行。完整发行仍未通过；下一轮在相同产品输入上采用N4，把当前53.5秒才启动的长016提前调度，普通5秒及总300秒门禁不变，不修改产品或失败基线来换取机器性能。

第七、八轮都保持同一产品输入，冻结树 `f038e1581ac833f64309fed8f1ada06950d3a525`。N4第七轮unit47.5秒、全轮64.2秒，8/766文件drained后在DE005真实10001文件继续legacy场景触发原5秒静默；组31925回收accepted=true，22.182ms。N3第八轮unit74.7秒、全轮89.0秒，8/766文件drained后在016真实new tool member反例触发原5秒绝对物理期限，初始probe已结束，安装/身份核验及拒绝清理尚未完成；组37954回收accepted=true，24.746ms。两轮均无权威总数，后续integration/e2e/package未运行，不算通过。改变并发未解决本机的两端约束，不能继续按“无新assert红”宣称完整验收通过。

只读性能核查发现RetentionSurface整理入口计算了未使用的LastActivityMs，本例会额外对10001文件取mtime；但该eager扫描同时形成全writer的I/O前置，直接改成ID枚举可能让早一writer先移动、后一writer才报错，也可能跳过无legacy writer原有的错误。因此本批不据“未使用值”删除这一现存行为，不夹带未经证明的性能改造。

已向用户提出本次本机完整unit仅采用仓库CI现有900,000ms整轮总限时的明确例外，保留普通5,000ms、每个有限物理期限、全部断言与真实回收，且不改源码默认预算、失败基线或运行版本。回答尚未到达前不执行此例外；先独立运行既有integration入口补实际证据，不能把它拼成原完整verify:release已通过。

用户随后明确答复“允许本次用15分钟总时限”，本批可在本机采用900,000ms整轮回收门禁，普通5,000ms与已有有限物理期限不变，不修改源码默认预算或失败基线。先前独立integration运行86.8秒，suites在15/40文件后于delegation027真实GUIDANCE_STOP物理期限失败；主组45685回收accepted=true23.250ms，package与harness子组结束。但该轮启动后发现generated-input-stale，整轮只作诊断，不计正式验收；须先重建，再在当前候选重跑。其失败也不因后来授权总限时而被改记通过。

构建的repository corpus包含已跟踪Markdown，补施工记录也会改变generated输入。为避免最终写验收结果后又使构建过期，后续状态、实际计数、源码交付与CI证据统一记入[最终结果](final-result.json)，本文件保留历史过程；看板和计划均指向该结果。JSON证据不属于现有repository corpus，也不改其筛选规则。正式验收前重建全部当前输入；最终只更新结果JSON时仍须比对验证、生成、编译和产物摘要，不能仅凭扩展名宣称等价。
