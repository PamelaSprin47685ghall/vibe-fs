# 上游增量合并计划：view、交付与权限

2026-10-20。用户已授权根据本计划逐包合并；当前施工状态见 BOARD，验收记录追加在本文末尾。目标是接收最新上游中能够完整落地的功能，保持常用工作链可用；小的边缘问题记账后继续。

用户随后明确选择“完整对齐上游新模型，包含旧功能退役”。本批据此接收 Blogger、Chronicle、WorkRecord、Replica 与自动 requirement grounding 的退役及其依赖变更；下文原先暂缓这些删除的处置已被本次裁决替代。历史计划和验收证据继续留档，不自动删除运行状态。固定上游仍为 11052493b，后续 df7846ab8 不混入本批。

新增 UM-R：在 UM-2 的已验证候选上完整合并固定上游，先统一退役后的公共类型、库 gitlink、构建图和持久事实，再完成 UM-3/UM-4 的实际链路。使用真实 merge 保留双方历史；已接收的局部实现按新合同重新核对，不把旧合同测试绿色当作新模型验收。保留本地进程监管、资源收尾、Plan P-only 路径和逐次权限约束；旧 Trace/WorkRecord 的历史读取不再作为保留生产模块的理由。库升级到上游所需的可获取提交并核对包入口，自有 URL 另行保留。UM-R 完成后更新本文中被替代的逐项范围与证据。

## 1. 固定输入与调查结果

| 对象 | 本次核对结果 |
|---|---|
| 开发基线 | 自有 `origin/master@f04553e5afbe10ad7aa87b8907cb6bdaef58e447` |
| 上次接收参照 | `0932e0c08de04a77d56b81722e90b308d6ce9e90`；已接内容见[前批计划](UPSTREAM-INTAKE-2026-10-20.md)与[交付记录](../../evidence/upstream-intake-20261020/delivery.json) |
| 本次固定上游 | `11052493bedb4edbcbe0c341fa677afd44c486f1`，提交时间 2026-10-20 17:35:50 +08:00；已执行 `git fetch --multiple origin upstream` |
| 共同祖先 | `2f23502c60bac0abed11ebdd39d8555cc1d90457` |
| 库依赖 | 以自有 `05f6b5a` 真正合并上游 `a8b89a4`，形成 `fc75b85`，已普通推送自有 master；公开入口由16减至15，只删除自动 grounding，保留本地 supervision 和时间运行时 |
| 原运行工作区 | `master@8db33b3429a70d5cdbea30e393363666b3649573`，干净；落后自有远端 66 个提交，继续保留运行版本 |
| 计划工作区 | 从自有 `origin/master` 建立独立工作树与 `codex/upstream-plan-20261020` 分支；不切换原运行目录 |

相对上次参照，上游新增 **17 个提交、386 个变动路径**，其中新增 44、修改 276、删除 66。Git 历史显示双方独有提交为 83/28；28 不等于 28 个待做工作包，因为前批已经定点接收其中部分语义，未把混合提交整体标为合并。

使用临时对象目录，对上述固定两头执行 `git merge-tree --write-tree --name-only`，退出 1，得到 **162 个冲突路径**：源码 80、requirements 70、resources 8、scripts 2、AGENTS.md 1、库 gitlink 1。冲突消息为 content 131、modify/delete 28、rename/delete 1、add/add 2、submodule 1；消息与路径并非一一对应，不能相加当文件数。这是文本预演，不是编译或功能验证。

原计划采用按功能定点整合，UM-1/UM-2 已按该范围形成提交。用户扩大范围后改为完整合并固定目标；冲突按退役合同和本地有效保护逐项裁决，完成后以 merge commit 记录双方来源。

施工开始时重新 fetch；若上游又前移，把新提交登记为下一批，保持本批输入固定。若自有 master 前移，先检查是否涉及同一编辑面，保留对方提交，再更新本批开发基线和证据。

## 2. 接收范围

| 来源 | 目标处置 | 边界 |
|---|---|---|
| 前批 call/Callable、sanity 数组、rg、动态输出预算、candidateDigest | 复用已交付实现与测试，作为回归保护 | 不重做，不拿上游较弱的进程实现覆盖本地监管 |
| `959f7ce64`、`16eb2f5b5` | 接统一区间 view、`thenMap`、Plan 共用编程基础设施、view-only 交付 | view 改造与交付消费者一起完成；Plan 仍只能访问自己的 P，不获得仓库或执行权限 |
| `60f0802fd`、`9c87a15c5` | 接稳定内联锚点及所有终态工具结果的 Pair Hint | 不改写原始 Host 消息；错误、取消结果也验证，重复 transform 不重复插入 |
| `11052493b` | 接 Pair Programming 语言提示的精简及对应合同/测试 | 随 UM-1 检查双语一致；不改变会话语言绑定和协议标识 |
| `45bf5b74a`、`83f23e9ab` | 删除 Manager 评审保证书参数；程序入口常开，文件读取经 `reviewOnly` 逐次授权 | 保留身份、取消和存储边界；保存的方法、park 恢复及插件重开不能继承过期授权 |
| `60812f575` | 接带精确调用身份的交付事实，以及子工作、join、Fission、恢复的完成准入 | 自然 stop、进度散文及旧交付不能冒充完成；未交付继续保持工作义务 |
| `073fa0766`、`3e7a47223` | 接与实际 view 规则一致的日常整理和深度整理提示 | 双语同步；先实现规则，再更新示例，不扩成提示词重写 |
| `4aa7f06d5` 的《任务书A》 | 作为未完成迁移的调查资料 | 暂不接统一 `reportMapping` 委派协议，不提前删除现有 fork/resume/commission/devops Host 注册 |
| 早前大规模退役、库入口和历史身份删除 | 按用户新裁决完整接收 | 删除 Blogger、Chronicle、WorkRecord、Replica、Predictor 及自动 grounding 的生产路径和过时测试；保留自有 submodule URL、本地有效进程保护与历史证据，不迁移或清空用户运行状态 |
| 上游 AGENTS 改动 | 仅参考产品背景 | 本轮用户给出的手工精准修改、Fable 构建等纪律继续有效 |

### 必须先统一的接口

上游当前源码与说明存在半迁移，不能只挑名称照搬：

1. `ToolHost.fs` 已接受顶层 `prefixUntil`、`thenMap` 并拒绝 `view` 包装；`repository-programming-053` 与 `context-compression-031` 仍描述工具顶层 `view` 对象。本批拟采用源码现状：工具参数扁平，内部共享声明及 `this.deliver(view)`、`advisor(intent, view)` 仍使用 `{ prefixUntil, thenMap }`。先统一正式合同、真实 schema、双语示例与测试，再改调用方；不保留双套公开参数。
2. 《任务书A》要求 `Delegate(name, payload, reportMapping)`，但固定上游的 `DelegationBridge` 和 `ToolRegistry` 仍使用 `Fork(payload)`，独立委派工具仍注册。能力目录已有 Resume/DevOps，`ToolsBindings.createApiCore` 却没有对应成员分支。本批只投影已真实接通的成员；不能把缺失方法当作可用功能。统一委派协议留待独立闭环。
3. 上游 `repository-programming/tests/060.test.mjs` 用 060 锚点测试交付结算失败，但 WHAT[060] 是机器词汇保留；061、062 也没有对应正式条款。导入有效反例时并入本地真正拥有行为的编号文件，例如交付记录 059、park 030；先核对条款，不覆盖旧测试、不新增悬空锚点。

以上是固定源码的静态核对结果。本轮没有构建 upstream，不能据交接文档断言它当前必然编译失败，也不能引用文档里的旧绿色结果证明最新头可用。

## 3. 工作包与顺序

状态只在 [BOARD](BOARD.md#upstream-view-delivery-20261020) 维护。下表给出工作边界，实施证据追加在本文末尾。

| 包 | 内容 | 前置 | 最小完成点 |
|---|---|---|---|
| UM-0 | 固定两头、预演、范围和接口差异 | — | 本计划已有可复核版本、冲突规模和处置依据 |
| UM-1 | Pair Hint 终态覆盖、移除评审保证书参数 | UM-0 | 真实工具结果投影与 schema 一致；不改变完成准入 |
| UM-2 | 统一 view/Anchor 与 Plan 编程面 | UM-0 | 区间规则、顶层参数、Plan 的 P 路径限制、真实用户原文和历史原件保持正确 |
| UM-3 | Manager 程序常开与 `reviewOnly` | UM-2 | 程序可做计算/生命周期操作；文件访问逐次查当前权限，拒绝时零读取、零 rg 启动 |
| UM-4 | view-only 交付、持久见证与工作收束 | UM-2、UM-3 | 一条真实委派链从调用、交付、落盘到 join 完成；自然 stop 不误关，旧回执不误收 |
| UM-5 | 固定候选验收、源码集成与交付记录 | UM-1—UM-4 | 构建、相关回归、真实 Host 与独立包消费有同候选证据，完整发行结果单列 |

推荐执行顺序：UM-1 → UM-2 → UM-3 → UM-4 → UM-5。UM-1 可独立验收和提交；UM-2—UM-4 可以在整合分支形成小提交，但共同完成后才作为一组进入 master，避免暂时出现新交付协议配旧完成逻辑。

独立读取、差异调查和不争用资源的验证并行。Anchor 公共类型先定；随后修改消费方，再删除被替换的路径。`ToolRegistry`、`ToolWorkflow`、`ToolHost`、`ToolsBindings`、`Description`、构建图及合同编号由同一施工者串行处理。真实启动、期限、取消实验串行，避免负载互相干扰。

### UM-1：先完成小而明确的改进

接收终态结果的 Pair Hint 覆盖及最新双语语言提示精简，保持既有前缀冻结和原始调用参数恢复；移除 Manager 专属保证书 schema 和注入分支。错误结果、取消结果、重复结果转换都必须验证。此包不随意删除通用参数恢复机制，不改变实际读取授权。

最小验收：`prefix-stability-010`、`cognitive-environment-013`、`host-boundary-032`，以及实际触碰的前缀/参数恢复条款。若需要先引入新 Anchor 类型，将相关改动移至 UM-2，不额外造临时适配层。

### UM-2：让 view 和 Plan 真正可用

落实 `thenMap` 的 null 保留、整区间摘要、同轮相邻替换、历史累计替换、隐含锚点 0、prefixUntil 保护，以及晚到消息不被旧末尾替换吞掉。prefixUntil 可以升降，变化不恢复已压缩内容。内联标记只进入 provider 投影，原始 U/Trace 字节和既有冻结范围不改写。

Plan 复用普通生成编程面和文件原语，通过 `PLAN_PATH` 限定 P；不恢复旧 file/edit/rewrite 别名，也不授予真实仓库读写或 Exec。`this.ask` 继续使用已有 durable 问题 owner，保留 S1 限制、结果先落地再中断、真实用户回答与迟到终态的反例。

同步 WHAT、schema、`.fsi`、资源和实际消费者。历史 view 编号与摘要仍按真实进程寿命说明，不把 durable checkpoint 写成整个压缩现场的跨进程恢复证明。旧程序不自动重放。

最小验收：`context-compression-031`、`repository-programming-001/002/053`、`work-029/030/031/049/050` 及实际受影响的 prefix-stability、provider-language、office-capability 条款。缺少编号文件时按正式条款补齐，不借别的编号承载。

### UM-3：Manager 能继续工作，读取仍受约束

接收 `this.reviewOnly.cat/stat/ls/rg`，不保留 Manager 的顶层文件方法别名；`where/whereAll` 继续处理已获得文本。每次真实访问前检查活跃任期、评审接纳、冻结及清理事实。末任无读取豁免；跨角色工具绕路仍拒绝。

复用本地 rg 的根内路径、配置隔离、输出上限、同一期限、取消和进程 owner。只移动授权入口，不换底层搜索/进程实现。保存对象、绑定方法、原始 API 引用、park/unpark 和插件重开均重新检查；已经合法启动的查询与之后的新查询分别判断。

最小验收：`capability-enforcement-025/026`、`repository-programming-001/002/041/056`、`host-boundary-032` 的实际插件/Host 路径，以及本地 rg 相关正反例。

### UM-4：交付必须属于这次工作

先定义 TaskDelivery 的 session、logical run、authority、physical input、provider run、tool call、task 身份及持久 owner，再调整交付格、终态消费者和恢复入口。view-only 交付必须包含实际保留材料和末条正式散文，按材料身份去重；peek 不推进游标。落盘未确认、结算失败或未知时不得留下可被当作成功消费的交付格。

普通 return、空 stop、进度文本不能完成子工作或关闭 join。旧 provider run 的终态不能取走新调用的交付；Fission lane 的局部交付不能代替 owner 的最终交付。Advisor/Bookkeeper 仍使用自身专用提交事实，不强加外层交付义务。

恢复只认有效持久见证，不从快照里的 stop 或散文补造成功，不重发未知效果。Plan post-S3 就绪、用户请求执行、创建/绑定执行会话、Accepted 分别核对，不能把就绪写成已启动。park 只阻塞所属工作；外会话和旧句柄不能恢复别人的程序。

最小验收：`repository-programming-030/054/059`、`work-026/027/039/048/049/050`、delegation 的创建/回收/join 路径、`intra-participant-parallelism-014/015`、Advisor 本次调用与旧结果隔离、crash-reconciliation 相关条款。借用上游 060—062 的有效反例时先修正归属。UM-4 只完成这些实际链路，不顺带宣称整个长程审计的 30 项或历史 GAP 已关闭。

## 4. 验证与效率

每包先写或调整能区分新旧行为的测试，再实现；相关套件通过后停止重复跑同一证据。只有修改、失败或新风险才扩大验证。普通源码构建只用 `node scripts/build.mjs`；综合入口沿用 `npm run format-build-test`，禁止 `dotnet build`。

| 时点 | 验证范围 |
|---|---|
| 开工基线 | 记录基线的受影响条款结果及已有失败；不先跑一次全仓作为形式 |
| 每包 | Fable 新鲜构建、相关编号测试；先原生定位，再经正式监督入口取得该包结论 |
| UM-5 固定候选 | format/check/build；合并去重后的相关 requirement 选集；运行真实插件接线、持久恢复与受影响进程路径 |
| 用户工作链 | Plan 写 P/ask/review/交付；Manager 委派、评审读、接纳后拒绝读；Engineer 搜索/编辑/交付；DevOps call/取消/真实收尾；父方 join 与重启后读取 |
| 包消费 | 真实打包、仓外安装、公开入口加载与资源闭包；库 gitlink 和自有 URL 不漂移 |
| 完整发行 | 最终候选只运行一次 `npm run verify:release`；修了明确阻断或候选改变后才重跑 |

正式定向入口使用 `TESTS_MJS_FILES` 传入明确文件清单，再调用 `node requirements/verification-system/tests/run.mjs`；集成条款按现有 tier 入口执行。日志保留源 SHA、candidateDigest、运行时、命令、pass/fail/skip/TODO、权威汇总及实际收尾事实，重叠套件不相加。

最低 Node 22.13 验主要合同；受影响的进程、SQLite 和包加载分别核对当前 Node 与 Bun。使用完整可用运行时，不重演系统 Node26 单独复制后缺 libnode 的无效夹具。Linux 只有取得实际证据才记通过。

前批完整 release 在 distribution-007 的 5000ms verdict-silence 失败，后续阶段未进入。这由[模块改进计划](MODULE-IMPROVEMENT-2026-10-20.md) N1 承接，本批复用其修复和证据，不再开一条平行调查。它阻断完整发行结论；不阻止独立功能包调查与整合。若最终仍是同一既有失败，保存新候选结果和未执行阶段，停止循环全量重跑。

新功能及受影响的关键用户链必须通过。含既有 TODO 的正式套件仍如实记录不通过；不放宽静默门、不接受 TODO、不删仍适用的反例换绿色。限定源码集成、完整发行验收、运行切换分别报告。

## 5. 问题分流与停止条件

影响常用功能、权限、数据、真实资源收尾或完成判定的问题，修复后才能接收对应包；无法在本批解决就保留该包未完成或撤回对应接收。新合同不一致和缺失运行时成员属于这一类。

低频文案/显示问题、已有清晰绕行办法的非关键参数组合、与本批无关的旧 TODO，可登记后继续。一轮最小复现仍无法定位的非阻断问题，记录事实与范围，不扩大为无边界追查。每条记录写明固定候选、复现步骤、预期/实际、影响、绕行办法、责任包及重新处理条件。

| 记录 | 当前事实 | 本批处置 |
|---|---|---|
| V01 顶层 view 协议不一致 | 上游 ToolHost 扁平参数与 WHAT 的对象参数不同 | UM-2 接收前统一，不能作为文案小问题留到上线后 |
| V02 委派协议未完成 | 源码仍为 Fork 通道，reportMapping 只部分准备；Resume/DevOps 投影与绑定不齐 | 暂缓新协议；不发布未接线成员，保留现行真实入口；后续独立包接手 |
| V03 测试锚点不对应条款 | 上游 060 行为错位，061/062 缺正式 WHAT | UM-4 按实际 owner 接收反例，不照搬编号 |
| E03/E04 既有发行与运行时夹具问题 | 证据在[前批处置表](UPSTREAM-INTAKE-DISPOSITION-2026-10-20.md) | 由 N1 统一处理；不以旧失败证明本次改动无回归 |
| E06/E07 搜索参数范围与启动延迟 | 常用路径已验；少量选项明确拒绝，启动成本原因未定 | 继续留档；本次若实际导致期限/取消失效则升级为阻断 |
| 其他旧证明边界 | B2、完整 Long Stroke、通用恶意 JS 隔离、历史冷恢复/并发 GAP | 不销项，不作为本批全量清账目标；被本批直接触碰的保证仍要验证 |

## 6. 提交、切换与回退

产品施工在独立 `codex/` 分支进行，保留双方历史。小包按实际语义提交；UM-2—UM-4 同候选验收后整合。普通推送自有 master，禁止 force push。若远端前移先整合并复核受影响范围，不覆盖他人工作。

库先独立合并和验证、普通推送可获取 SHA，再更新宿主 gitlink及调用方；本批已完成此顺序，不引用未推送库提交或邻仓源码兜底。

运行切换单独执行：先确认活动 work、park、PTY 已按旧协议收束，保存旧包与状态恢复点，再安装已验证候选并做 Host 冒烟。新持久事实写入后须验证旧版本的读取能力才能回退程序；不能假定换回旧包即可安全回滚，更不能清空 `.Herdchestra`。运行切换不由源码推送自动触发。

每包完成后更新 BOARD 状态，并在本文追加候选、验收证据与剩余边界。本次计划提交只关闭 UM-0，不改变 UM-1—UM-5、N1—N6 或父 CUT/GAP 的状态。

## 实施记录

### 开工与 UM-1

开发基线为计划提交 `83875650a`，工作树原本干净。开工 fetch 见上游已前移到 `df7846ab8`；按固定输入规则登记为下一批，本批仍只接 `11052493b`。正式子模块初始化到 05f6b5a；复用同基线的依赖和生成物缓存，后续结论均须由本工作树正式新鲜构建确认。

行为反例在旧编译产物上为 0pass/3fail，分别证明保证书仍被注入和多终态只处理末项；语言资源为 1pass/1fail。原始日志见 `evidence/upstream-view-delivery-20261020/um1-behavior-red.log` 和 `um1-language-red.log`。已移除 schema 装饰，保留实际历史参数的隐藏/恢复；Pair Hint 处理每个终态，grounding 仍只使用自己的末项落点。当前构建与回归未结束，尚无完成、推送或部署结论。

UM-1 最终 gen43 构建、格式和静态检查通过。正式七文件 175pass/0fail/5skip/2旧TODO，7/7 排空、回收 accepted=true；同候选实际 Host/序列化两文件 109pass/0fail/1旧TODO，2/2 排空、回收 accepted=true。两组有重叠，不相加；TODO 继续使正式入口退出 1。多终态原件用真实 Engineer 当前任期装配，重放与独立进程冷读取均验证。

Host canary 的旧 schema 观察断言同步更新；其真实物理执行使用已有 begin/end ACK、最多 30000ms 授权，默认 5000ms 静默门和原子进程超时未改。原始静默、旧断言、夹具及构建期语料变化失败均保留，详见[证据索引](../../evidence/upstream-view-delivery-20261020/README.md)。当前仅满足 UM-1 限定源码接收；未执行完整发行、包消费或部署，旧 GAP/TODO 不关闭。

最终把本计划、看板和证据索引冻结暂存，再构建复验；最终候选结果只写入 [um1-delivery.json](../../evidence/upstream-view-delivery-20261020/um1-delivery.json)，不在验证期间继续修改作为构建输入的 Markdown。

### UM-2 开始

UM-1 已以24462522f普通推送master；提交后构建新鲜度仍为gen44。UM-2从该干净候选接续。先定义thenMap、有序区间、Plan用户原文保护及调用边界，再迁移实际消费者。上游Plan通用化目前只切工作目录并提供PLAN_PATH常量，未落实本地P-only文件约束；这是接收前必须用真实路径/原始API调用反例验证的边界，不按显示问题留到部署后。

### UM-2 接线与中间证据

已接入有序固定区间、null 保留、隐含 0、保护前缀、历史摘要和材料游标；Host 在真实用户与终态工具结果原位投影锚点。顶层 prefixUntil/thenMap、nullable map schema、持久 checkpoint 监听及相关夹具共同迁移。Plan 复用普通文件原语、效果记账和 park 流程，实际 API 限定 P；每次写入重新检查本程序原任期身份与评审，不能借用继任者评审。旧 file/edit/rewrite 算法和独立提交路径已手工移除。

gen45 构建通过；初选 28 项通过，真实 Plan Host 21/22 通过后修正 checkpoint 入口。gen46 正式扩选为 144 pass、28 fail、14 skip、1 旧 TODO，26/26 排空且回收 accepted=true。失败分为旧断言/夹具迁移与 provider-projection-011 的既有两项缺失 ProviderStarted 证据；后者在旧工作树同名测试原样复现为 6 pass、2 fail，日志为 um2-provider011-baseline.log，先记录，不修旧夹具。前缀断言保留原件冲突、冷恢复和原文字节反例，仅将新增内联标记与指导正文分开观察。Advisor 装配的完整模型尝试夹具会触发既有专席第二 prompt 的 authority 冲突，保留失败日志；其成员返回隔离测试使用共享 Anchor 边界准备本轮视图。

上述均为中间结果，不关闭 UM-2。最终源码冻结后同候选重新构建，正式选集及 integration checkpoint 单列；之后结果写入 evidence/upstream-view-delivery-20261020/um2-delivery.json。UM-3、UM-4 尚未实现，本组未进入 master，未部署。

gen47 构建与静态检查通过。核心 21 文件 156 pass、1 新断言失败、1 skip、1 旧 TODO；checkpoint integration 5 文件 22 pass、1 失败，两组均完整排空、回收 accepted=true。已定位并局部修正：新锚点测试遗漏已有指导后缀；provider-projection-011 与 obligation-ledger-004 的夹具读取旧 .git 状态目录。前者改用现有状态根后，与锚点反例原生合跑 24 pass、0 fail；不扩大修其他历史债务。Plan 文件观察器保持启用，改用 P 所在的真实目录，防止把底稿 plan.md 误记成仓库根的同名文件。自动审批曾拒绝移除该观察器的方案；未执行该删除，接收的是保留观察且修正路径归属的实现。

### UM-3 开始

UM-2 限定源码保存在整合分支39b5dd04d，gen48正式核心165 pass / 0 fail / 1 skip / 1旧TODO，checkpoint integration 23 pass / 0 fail，两组排空及回收均通过。完整验收仍为false，详见um2-delivery.json。本组未推master。UM-3先落实Manager程序入口与文件成员授权的不同边界，先补反例，再调整生成器、实际API和Host门禁；不移动rg进程实现，不减少Plan与普通读取的观察。

### UM-R / UM-3 / UM-4 实施记录

已完成固定 11052493b 的真实 merge 冲突裁决，构建图、持久投影、公共类型与旧模块退役一起接收。库合并 fc75b85 已推送；库原生选集 62 pass / 0 fail / 4 skip，包括15入口与包外消费。主仓 Fable gen49、gen50、gen51 构建通过；这些是中间候选，不代替最终验收。

Manager 的程序入口与逐次 reviewOnly 访问已接通，Plan 只展示和绑定自己的 P/Ask；Orchestrator 使用实际 commission 绑定。未接通的 Resume/DevOps SDK 成员撤下投影，已有 Host 入口保留，V02 新协议继续延期。view checkpoint 的终态识别已同步平铺参数，交付在账本结算成功后才入格；旧 provider run 不能消费新交付，普通 stop 与冷资源回收都不能补造任务完成。

Advisor 两条物理输入改为等待准入后串行发送，第二条继承当前任务，不再冲突开根。答复围栏通过 intent 的物理输入与持久 ProviderStarted 关联实际 provider run，读取该终态的正式散文，不回读旧快照。资源测试夹具显式提供交付，同时保留丢失确认、并发 Drain、SIGKILL、Node/Bun 真实进程及收尾反例。

上游原060中的两项账本失败反例归入059，061/062的重复行为由059/030保留；060改验实际 SDK 词汇。移除退役辅助文件，Trace 的晚结果和64位 cut 改由真实 journal 重开验证。中间失败不删日志，最终收据另记。

非阻断边界：本机没有 wget，052的两个真实下载用例明确 skip，其他 call 期限/取消/收尾照常验；SemanticTraceSurface 的便捷数字 cursor 投影对超过 Int32 的值仍截断，64位 durable cut 不受影响，本批只记录；原有发行静默门禁、旧 TODO/GAP 与 V02 均不销项。尚未切换原运行目录或部署。

静态门禁第一次报告37处控制流嵌套和29处JS边界问题。JS边界中的退役Cycle残留、RuntimePath深导入、内部Fable类型构造均已按实际owner迁移，原反例保留。控制流风格问题按用户的效率与功能优先原则留档，不改基线、不关闭门禁、不报告静态全绿；最终数量及完整发行实际阻断点以收据为准。源码功能验收与完整发行状态分别报告。

最终定向阶段找到前批留下的真实 GNU wget 1.25.0 临时构建，验收PATH加入其目录，052实际下载不再以缺工具跳过；它不进入运行依赖或发行包。补正同步交付中 reasoning 混入正式散文的问题，并保留 Advisor/Bookkeeper 专用终态的外层 TaskDelivery 豁免；Advisor 正例改走真实 Host 事件订阅和 TerminalReporter，而非直接发送合成 Completed。

最后核对恢复消费者：已具备有效持久交付见证的冷恢复使用见证中封存的 view，不再把快照中的散文替代为交付正文。其余缺少见证、身份不匹配或未知效果仍保持未完成；这项局部修正不关闭 GAP-149 的完整重启链证明。

完整 clean build gen57 通过。核心正式选集398 pass / 0 fail / 3 integration skip / 6旧TODO，53/53排空；真实进程与资源选集278 pass / 0 fail，13/13排空；checkpoint integration 11 pass / 0 fail，4/4排空，三组回收均 accepted=true。实际包1366成员、仓外安装与公开入口消费通过。静态检查为34处控制流嵌套问题，JS边界0；完整发行入口格式通过后停于该静态门，后续阶段不记通过。

真实Host首次合跑45 pass / 1 fail，失败是旧夹具仍假定正文位于呈现字符串开头，未计入新增锚点；原始物理工具结果与Trace一致。夹具只剥离已定义锚点再核对正文，用户引文、实际provider请求、持久原件和独立进程冷读断言保留。两项真实Host操作使用已有有限物理等待begin/end确认，最多30000ms，不修改默认5000ms静默门。最终冻结候选、正式Host复验、提交和普通推送以umr-delivery.json为准；旧TODO和完整发行欠账不因源码合并而关闭。

gen58正式组合选集420 pass / 1 fail，Manager实际Host通过；原文canary第二条prompt超时，旧夹具只等数据库stop，尚未等本轮idle。改为同时核对本轮正式终态和Host实际idle，再发后续输入。原生真实Host复验已通过：OpenCode1.18.29、4次provider请求、完整用户引文与持久工具原件保真、独立进程冷读取均确认。该修正只改测试时序，生产F#输入与发行资源摘要保持gen57相同。最终构建、正式组合复验与包摘要另记收据；原失败日志保留。

gen59组合仍为420/1：只有stop的旧mock没有实际交付，新模型按合同继续推进，偶然的idle不能充当工作收束。受控provider现以正式散文加真实deliver(view)完成前两轮，再观察第三条真实输入的provider请求；两次交付均断言实际tool成功。原生链路已走完6次请求并通过原文与冷读取。生产代码未改，不以豁免交付义务或放宽静默门换绿色。最终正式结果仍以收据为准。
