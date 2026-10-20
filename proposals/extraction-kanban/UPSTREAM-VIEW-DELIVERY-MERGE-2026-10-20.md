# 上游增量合并计划：view、交付与权限

2026-10-20。本轮只制定计划，尚未合并产品代码、运行产品测试或切换运行。目标是接收最新上游中能够完整落地的功能，保持常用工作链可用；小的边缘问题记账后继续。

计划沿用前批范围：Blogger、Chronicle、WorkRecord、Replica 与自动 requirement grounding 的整体退役另排一批。新的 view 与交付协议可以替换其对应公开接口，但不借此删除仍在使用的历史读取职责。产品合同以本仓对应 WHAT 为准；上游交接文档中的待施工目标不直接成为本仓承诺。

## 1. 固定输入与调查结果

| 对象 | 本次核对结果 |
|---|---|
| 开发基线 | 自有 `origin/master@f04553e5afbe10ad7aa87b8907cb6bdaef58e447` |
| 上次接收参照 | `0932e0c08de04a77d56b81722e90b308d6ce9e90`；已接内容见[前批计划](UPSTREAM-INTAKE-2026-10-20.md)与[交付记录](../../evidence/upstream-intake-20261020/delivery.json) |
| 本次固定上游 | `11052493bedb4edbcbe0c341fa677afd44c486f1`，提交时间 2026-10-20 17:35:50 +08:00；已执行 `git fetch --multiple origin upstream` |
| 共同祖先 | `2f23502c60bac0abed11ebdd39d8555cc1d90457` |
| 库依赖 | 本地保持 `wanxiangli@05f6b5acf63a29d758e08e3162bba4c547dfda8d`；目标主仓仍固定上游库 `a8b89a45607572a62e1ca085c907fb02c878e6c0`，本次 17 个新增提交没有更新 gitlink |
| 原运行工作区 | `master@8db33b3429a70d5cdbea30e393363666b3649573`，干净；落后自有远端 66 个提交，继续保留运行版本 |
| 计划工作区 | 从自有 `origin/master` 建立独立工作树与 `codex/upstream-plan-20261020` 分支；不切换原运行目录 |

相对上次参照，上游新增 **17 个提交、386 个变动路径**，其中新增 44、修改 276、删除 66。Git 历史显示双方独有提交为 83/28；28 不等于 28 个待做工作包，因为前批已经定点接收其中部分语义，未把混合提交整体标为合并。

使用临时对象目录，对上述固定两头执行 `git merge-tree --write-tree --name-only`，退出 1，得到 **162 个冲突路径**：源码 80、requirements 70、resources 8、scripts 2、AGENTS.md 1、库 gitlink 1。冲突消息为 content 131、modify/delete 28、rename/delete 1、add/add 2、submodule 1；消息与路径并非一一对应，不能相加当文件数。这是文本预演，不是编译或功能验证。

本批采用按功能定点整合。先用 `0932e0c08..11052493b` 找新变化，再核对它对前批暂缓内容的实际依赖；不整包解决上述 162 个冲突，也不建立声称完整接收上游历史的 merge commit。每个实现提交记录上游来源 SHA、实际接收范围和未接部分。

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
| 早前大规模退役、库入口和历史身份删除 | 维持前批暂缓处置 | 库继续固定 05f6b5a、16 个公开入口；保留自有 submodule URL、原件、只读墓碑和历史证据 |
| 上游 AGENTS 改动 | 仅参考产品背景 | 本轮用户给出的手工精准修改、Fable 构建等纪律继续有效 |

### 必须先统一的接口

上游当前源码与说明存在半迁移，不能只挑名称照搬：

1. `ToolHost.fs` 已接受顶层 `prefixUntil`、`thenMap` 并拒绝 `view` 包装；`repository-programming-053` 与 `context-compression-031` 仍描述工具顶层 `view` 对象。本批拟采用源码现状：工具参数扁平，内部共享声明及 `this.deliver(view)`、`advisor(intent, view)` 仍使用 `{ prefixUntil, thenMap }`。先统一正式合同、真实 schema、双语示例与测试，再改调用方；不保留双套公开参数。
2. 《任务书A》要求 `Delegate(name, payload, reportMapping)`，但固定上游的 `DelegationBridge` 和 `ToolRegistry` 仍使用 `Fork(payload)`，独立委派工具仍注册。能力目录已有 Resume/DevOps，`ToolsBindings.createApiCore` 却没有对应成员分支。本批只投影已真实接通的成员；不能把缺失方法当作可用功能。统一委派协议留待独立闭环。
3. 上游 `repository-programming/tests/060.test.mjs` 用 060 锚点测试交付结算失败，但 WHAT[060] 是机器词汇保留；061、062 也没有对应正式条款。导入有效反例时并入本地真正拥有行为的编号文件，例如交付记录 059、park 030；先核对条款，不覆盖旧测试、不新增悬空锚点。

以上是固定源码的静态核对结果。本轮没有构建 upstream，不能据交接文档断言它当前必然编译失败，也不能引用文档里的旧绿色结果证明最新头可用。

## 3. 工作包与顺序

状态只在 [BOARD](BOARD.md#upstream-view-delivery-20261020) 维护。下表给出工作边界；产品包均尚未实施。

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

库本批不变；确有必须修改的公共契约时，先在库验证、普通推送可获取 SHA，再更新宿主 gitlink、锁和调用方，不引用未推送库提交或邻仓源码兜底。

运行切换单独执行：先确认活动 work、park、PTY 已按旧协议收束，保存旧包与状态恢复点，再安装已验证候选并做 Host 冒烟。新持久事实写入后须验证旧版本的读取能力才能回退程序；不能假定换回旧包即可安全回滚，更不能清空 `.Herdchestra`。运行切换不由源码推送自动触发。

每包完成后更新 BOARD 状态，并在本文追加候选、验收证据与剩余边界。本次计划提交只关闭 UM-0，不改变 UM-1—UM-5、N1—N6 或父 CUT/GAP 的状态。
