# 万象术 Manager 与 Plan 统一重组

## 0. 任务缘起与用户意图

用户原话（U，2026-10-11）：

> 目前 Manager 和 Plan 模式有着极为相似的结构，Plan 是后加入的，更加简单也更符合逻辑。Manager 模式的隔离和用词还有实现都偏旧。与其 backport，不如直接重组——用 Plan 模式的升维后同时服务 Plan 和 Manager。Plan 本身就有 3 Phases，Manager 其实可以认为是特殊的第四 Phase，只是独立 session 而且高频使用。或者你有什么更好的主意？

- 用户已否决：backport（把 Plan 语义搬回 Manager）。
- 用户倾向：Plan 升维统一，Manager 视为特殊第四 Phase（独立 session、高频）。
- 用户开放：征求更好的主意。

用户第二段 prompt（2026-10-11，对 R1-R8 的裁决）：

> 不，Manager 完整保留物理历史是错误的历史决策，要和 Plan 模式对齐。Manager 就是 S4，是恰好只有一个 Stage 的退化情况。全部重命名成 Plan 的词汇风格。持久化迁移到 DRY 以 Plan 为底稿重新设计。证书也不要了，向 Plan 看齐。R6 真的各自保留，因为 Manager 是自动工作，Plan 是交互工作。R7 使用 KISS & DRY 原则重新设计。R8 允许重新设计。最终目标：仿佛是从一开始就设计好了 DRY 版本的基础设施，Manager 和 Plan 都是基础设施的实例化或者受益者，要看不出发展历程和痕迹。

（逐条裁决入档见第 4 节「用户裁决」。）

用户第三段 prompt（2026-10-12，在 S1 覆核期间）：

> 最后要保留 manager 的两段式 deliver 语义，plan 仍为一段式。这是两者不同风险偏好的体现。

这条裁决入档为 R9。它关掉了 G2 里「两段式退休确认的去留交给 S2」那一半。WorkspaceSnapshot 的去留已在第 9 节 P1 裁定：不留字段、不留类型名、不留独立模块。不再交给后续阶段。

用户第四段纠正（2026-10-12）：

> P 是空的，plan.md 是手写的。应该用 P，plan.md 只是材料。

因此：本规划任务的活底稿只是 js-plan 读写的 P。仓库根 `plan.md` 是材料，不是活底稿，也不是第二份规范。执行时不要改它来「同步规划」，也不要把它当成现行合同。

本底稿事实来源分三层，不要并成一层。

材料层：2026-10-11 经 ask 委派固定 DevOps 的只读调查，文字落在仓库根 `plan.md`（提交 `2d9f81549418708050028eb04570bb1b02049217`，2026-10-12 09:06:33 +0800，212 行、23141 字节）。仓库根 `plan.md` 是材料，不是活底稿。

HEAD 层：现行 HEAD 是 `650850eabe78fa6b1f0ddabd4bf5cac8725e2dd8`（2026-10-12 11:37:37 +0800，`fix: reuse the shared JS anchor tutorial for js-plan`）。修复本棒的这次 ask：`git rev-parse HEAD` 是这个哈希；`4b9ef101dbaa7e9d1068850f08e3d356bf2c1ff1` 是祖先，不是现行 HEAD；该祖先之后只有这一笔。上一任把生命周期坐标锚在 `4b9ef101d`，并把 js-plan 教程改动写成未提交工作区。那笔改动已经进了 `650850ea`。这次 ask 对六处坐标文件的 `git diff HEAD` 为空，`git status --short` 无输出。没有重跑 `Mission/Planning`、`Relay`、`Manager` 相对 `4b9ef101d` 的目录 diff，不把「描述提交没碰生命周期」写成新的目录级证据。

工作区层：现行工作区干净。上一任两次 ask 读到的脏树（js-plan / js-program 资源、`ToolRegistry`、`Description`、`ToolHost`、未跟踪 `ultra-plan/`，以及后一次 diff 里的 `requirements/planning/WHAT.md` 与 `011.test.mjs`）不再是未提交事实。那些观察留在 2.13，当作已被 `650850ea` 收进去的前态，不删，也不再当成现行脏树。

下文标〔F〕的坐标：这次 ask 重读过的行号，以 `650850ea` 为准。上一任读过、这次没有重读的行号，不自动升级成这次新核。材料里被推翻的旧坐标继续标失效。

本任接手时，js-plan 读到的活槽对任意子串（包括 `#`）都无法锚到。用户确认活槽本来就是空的。材料经本任 ask 核对标题、事实来源句和 `assembleAskContinuation` 两处行号后，迁入本活底稿，并做下面的订正。

## 1. 目标状态（可判定）

重组完成后，仓库应处于以下状态：

- G1 单一生命周期基础设施：唯一活跃任期、退休不可逆、每任恰好一次 findings-pairs 评审（禁自证）、接力不推进阶段、固定 DevOps 绑定、LWR 交接——六项核心机制只有一套实现。统一模型为一条生命周期：S1 事实 → S2 理解 → S3 流程 → S4 执行；Plan work 从 S1 起走，Manager work 是恰好只有一个 Stage（S4）的退化情况。结束动作同名 deliver。确认次数不是第二套生命周期，也不是可以和序列打架的自由字段。序列写在开立事件上。序列为 S4 则两段，序列为 S1、S2、S3 则一段（R9）。
- G2 Manager 特有能力裁决：fork/resume/join 委派、Orchestrator commission、Continue 自动派发（自动工作语义）保留；QualityCertificate 退役；完整历史保留退役，对齐 Plan 的任期隔离（剥除前任消息、注入 LWR_prev）。结束确认按 R9 分叉：Manager/S4 保留两段式 deliver（第一次调用不结束，只落下 durable 确认；第二次以不同调用才结束）；Plan 的 deliver 保持一段式。名称用 deliver，不恢复证书。两段式保留的是现有 Manager 确认语义，不是另发明一套两段。第二次的结果族在证书退役后的载荷形态由第 9 节定，确认次数不许并成一段。WorkspaceSnapshot 不留字段、不留类型名、不留独立模块，与 P1 同一裁定，不再交给后续阶段。保留项各有新锚点验收。
- G3 Plan 特有能力保留：S1/S2/S3 stage 递进、js-plan 编程面（写前 review 硬门、禁路径参数）、任期隔离+前缀重锚、自然停止（交互工作语义）、底稿 P、deliver 一段式双路径+spawn——全部保留，各有测试锚点。
- G4 高频路径重设计许可：用户不经 Plan 直接创建 manager 会话的路径允许随统一重新设计（不要求行为不变）；「自动工作」语义（Continue 循环、无需用户逐步驱动）保留，新行为有新锚点验收。
- G5 术语归一为 Plan 风格：同一概念在全仓（代码+需求+资源）只用一套词，以 Plan 词汇为唯一风格；relay 旧词（Road、iteration、assessment、suicide、certificate 等）无残迹。两段确认不靠保留 suicide 这个名字来保留。
- G6 持久化 DRY 统一：以 Plan 的 EventStore+底稿设计为底稿重新设计持久化，AgentJournal durable facts 双轨消除；WorkspaceSnapshot 不留字段、不留类型名、不留独立模块，与第 9 节 P1 同一裁定。Manager 的第一次确认必须是这个统一模型里的 durable 事实，不能只活在内存里。
- G7 测试资产 KISS & DRY 重设计：两套平行测试（planning 测试与 relay 约 30 文件）按统一后的契约重新设计，消除同构重复；无悬空锚点、无两套词指同一行为。确认次数分叉要有各自的锚点，不能只用一套 deliver 测试覆盖两种语义。
- G8 无考古痕迹：最终形态读起来像从一开始就设计好的 DRY 基础设施——Manager 与 Plan 都是它的实例化或受益者；目录、命名、需求条款不携带「先 Manager 后 Plan 再统一」的发展痕迹。不引入兼容层，不留过渡命名。风险偏好差异（一段 / 两段）是设计，不是痕迹。

## 2. 事实

标注：〔F〕已核实（附出处）；〔I〕推断（附依据）；〔U〕不可核实/未穷尽（附原因）。

### 2.1 文件与组织〔F〕

Plan：`src/Wanxiangshu/Mission/Planning/` 14 个实现模块（各有 `.fsi`，14 不含 `.fsi`）。2026-10-12 覆核 `wc -l`：Surface(999)、Fold(503)、PlanTools(508)、TenureIsolation(122)、Facts(167)、EventCodec(207)、PlanWorkOpen(142)、ActionGate(111)、ReviewPlanBackend(107)、Recovery(98)、Store(88)、PlanPath(120)、IntegrationRules(23)、EventVocabulary(21)。材料里的 TenureIsolation(390) 已失效：`4b9ef101d` 把装配重写后缩到 122 行。现行装配函数是 `TenureIsolation.assemble`；`Surface.fsi` 仍导出 `assembleTenureMessages`，实现转给 `assemble`。模块内已没有名为 `assembleTenureMessages` 的函数。需求 `requirements/planning/WHAT.md` 这次 ask 数到 20 条标题，不是材料里的 22。编号：001、002、003、004、005、007、008、009、010、011、013、014、015、016、017、018、020、021、022、023。没有 006、012、019。标题行号：3、11、19、35、43、57、69、80、88、96、106、114、131、139、147、160、174、186、194、202。资源路径 `resources/provider/{role/plan, planning/s1|s2|s3, tool/{js-plan,ask,review,deliver}}` 沿用材料，这次未重列目录。

Manager：`Mission/Manager/`（Workflow 477、WorkflowSurface 47）+ `Mission/Relay/`（Fold 890、OpenCode/SuicideTool 466、OpenCode/ReviewBackendManager 460、Surface 285、OpenCode/NarrativeTransform 180、OpenCode/ReviewShell 154、OpenCode/WorkspaceSnapshot 52、Retirement/Surface 66、Assessment/{Model 76,Surface 23}、Contract 116、Facts 99、ProjectionSurface 42）。这些行数在 2026-10-12 覆核中与 `wc -l` 一致。`src/Wanxiangshu/Mission/Manager` 自 `9fcc15824` 之后零提交。需求 `requirements/relay-{incumbency,retirement,assessment,context-projection}/`，资源 `role/manager` 与 `tool/{suicide,review,resume,fork,…}`。

已共享的代码（两模式已经在合用的部分）：`Mission/WorkRecord/`（LWR 物化，两边共用）；`Relay/OpenCode/ReviewShell.fs:13-19,144-154`（review 共享壳，按 role 分派 Plan/Manager 双后端；2026-10-12 覆核 13–18 与 144–148 行仍是这个分派，文件 154 行）；`OpenCode/Plugin/PluginTransforms.fs`（同一 transform 管线。材料坐标 1147-1180「按 owner role 互斥分支」这次重读对不上：1147-1151 是 `begin-physical-provider-attempt`；1164-1174 与 1179-1186 是顺序两个 stage，不是 if/else。角色判定在处理函数里：`planSessionOf` 658-661 只认 `Role.Plan`；`applyTenureIsolation` 837-847 不是 Plan 就返回；`ApplyRelayProjection` 871-874 才调用 `ensureManagerRoadOpened`）；`OpenCode/Host/ProviderSystemTransform.fs`（同一系统提示装配器，Plan 与 Manager 同表注册）；`OpenCode/Tools/{ToolRegistry,StaticTools,ToolRuntimeScope}.fs`（同一注册表）。上一任在当时工作区读到的注册点现已随 `650850ea` 入库。这次 ask 没有重读 `ToolRegistry.fs` 行号，不把那些行号升级成这次新核。材料坐标 ToolRegistry.fs:601-602、714-735、737-759 已漂移，不再当现行行号。

### 2.2 五维结构对比〔F〕

| 维度 | Plan | Manager |
|---|---|---|
| prompt | stage 方法行的字节幂等替换这次重读仍在 ProviderSystemTransform.fs:58-83。`PlanSystemPrompt` 不在这段，在第 94 行 `Role.Plan` 分支。材料把这两处并成一个坐标，第 94 行是这次补上的 | manager-assess/work/finish 三段路径这次重读仍在 Workflow.fs:22-29。76-90 的注释仍写从不可变事实推导，不从程序计数器来。分支：有任期无评审走 assess；有绑定证书走 finish；有评审无绑定证书走 work；无任期返回 None |
| session | 用户显式创建 plan 会话沿用先前读法，这次未重读创建入口。`workId = sessionId` 这次读到在 PluginTransforms.fs:663-674 的 `tryReadActivePlanState`。材料坐标 773-784 作废：现行 773 行是 `Task.FromResult()`，775-784 是 `tenureMessagesOf` 填 `TenureMessage`，不是入口 | 三来源（用户直建、Orchestrator commission、Plan spawn）是先前契约读法，不因一处坏行号撤回；但 Workflow.fs:277-302 不是这三来源的位置。现行 277-302 是 `decideInitialOpening`：没有 road view 且 `CanonicalRole = Role.Manager` 时做 `IncumbencyOpening.initial`，这段没有读到 commission 或 Plan spawn。无 parent 执行会话仍在 ToolRuntimeScope.fs:747-785；agent 是参数，不是写死的 `manager`。`spawn "manager"` 在 PlanTools.fs:150。Continue 的材料坐标 Workflow.fs:329-369 对不上整条后继路径，见本表下的重读注 |
| 工具面 | 工作区 `planningSpecs()`（ToolRegistry.fs:836-856）注册 js-plan、ask、deliver。review 不在这个列表里，在 baseSpecs 的 ReviewShell.spec（约 882-887 行），与 Manager 共用同一 ToolSpec。材料坐标 601-602、714-735 已漂移 | 工作区静态准入（约 321-333 行）仍有 fork、resume、join、horizon、suicide、review。baseSpecs 仍 yield 这些规格，再 `yield! generatedJsSpecs ()` 与 `yield! planningSpecs ()`。材料坐标 737-759 已漂移。`ToolRegistry.fs` 没有字面量 js-manager；`generatedJsSpecs` 本任未展开，不能写成未注册，也不能把材料里的「评审期只读 js-manager」升为本任新核。本任核对过的这些名字相对 HEAD 没有增删。PTY 不在这次贴出的准入片段里，材料里的 PTY 未重核，不写成已删除 |
| 生命周期 | 7 事件名沿用材料，这次未重读事件定义。deliver 无权接力、持权交付：这次重读双路径在 PlanTools.fs:60-64 与 304-316；`spawn "manager"` 在 144-150。322-341 不是双路径，作废 | 现行两段式仍以 2.12 已读的 SuicideTool.fs:352-393 为准；表内旧坐标 80-126 这次未重读，不再当现行位置。Continue 不用 Workflow.fs:329-369 冒充整段自动派发。目标态见 R9：名称改为 deliver，两段确认保留，证书退役 |
| 持久化 | EventStore ndjson（.git/wanxiangshu/events/<WriterId>.ndjson，canonical JSON 键 Unicode 升序）+ 底稿（plan/<work-key>/plan.md，排他临时文件原子替换） | AgentJournal durable facts（RelayTransaction 原子提交）+ WorkspaceSnapshot（git 状态快照）+ QualityCertificate |

本表目标态句与现行实现句要分开读。工具面行的行号是上一任在当时工作区读到的，现已随 `650850ea` 入库；这次 ask 没有重读 `ToolRegistry.fs`，那些行号不是这次新核。现行工作区干净，所以「相对现行 HEAD 的未提交 diff」为空；这不证明三个 Mission 目录相对 `4b9ef101d` 零差异，那次目录 diff 这次没重跑。

这次重读推翻或钉住的坐标：

- PlanTools.fs:322-341 已漂移。现行 318-337 是 `activeOfDecision` 与 `runCheckedDeliver` 的游标检查，339 行起是 `executeDeliver` 签名。双路径仍在 60-64 与 304-316。144-150 仍是 `spawn "manager" planContent`。
- Workflow.fs:329-369 是 `deliverLoopPrompt`，发送的资源路径是 `runtime/manager-assess`（341 行），经 `HostSessionNudge.trySendGateContinuation`，`ContinuationKind.ManagerGuard`。后继开任在 371-406 的 `deliverLoopContext`。`continueAfterRetiredAttempt` 在 422-441。`RetirementOutcome.Continue` 出现在第 184 行和第 264 行；第 264 行是 `Continue -> None`。第 228 行 `decideLoopContext` 的函数体这次没有展开，不把「Continue 一定自动派发」写成这次读到的句子。R6 的目标语义不因此重开。
- PluginTransforms.fs:773-784 不是 plan 入口。入口侧这次只钉住 663-674 的 `workId = sessionId`。

### 2.3 骨架对照〔F〕（九项里八项责任同构已核实；第 6 项已读到不同构）

1. 唯一活跃任期：relay-incumbency-001 ≈ planning-003。
2. 评审门：relay-assessment-001/006 ≈ planning-022（findings pairs、每任一次、禁自证、非空即修复义务）。
3. 接力不推进阶段：relay-incumbency-004 ≈ planning-007。
4. 固定 DevOps 绑定：relay-incumbency-010/011/012 ≈ planning-010。
5. stale 请求拦截：两边都有「过期尝试不得改后继状态」这一族责任，但机制已不对齐。Manager 侧 NarrativeTransform 的 cut 仍只决定请求身份，不过滤 provider 消息集（覆核仍成立，注释仍在 135–138 行）。Plan 侧现行 [017] 不再以 Host 消息上的 cursor / openingCursor 归属任期——那个字段在 Host 消息上并不存在。`4b9ef101d` 改成稳定 XTrace 身份，退休游标是 consumed-through 的物理交互边界。材料把二者写成 openingCursor 同构，该坐标已失效。同构的是责任，不是那个已废弃的字段。
6. 崩溃恢复不猜测：同构已读到不成立。Planning/Recovery.fs 全文 98 行。公开入口是 planRecoveryPosition（67）与 evaluateRecoveryEffects（94）。输入是已经折好的 PlanWorkView，外加一个「底稿文件在不在」的函数，不是一次还没落盘的工具调用，也不直接读事件流。Delivered 与活跃任期同时在，返回 Conflict（68-69）。只有交付且摘要、路径都在，返回 Delivered；缺摘要或路径也是 Conflict（20-31）。只有活跃任期返回 Active，授权缺省当 false（64）；底稿不存在仍是 Active，不改口成已交付（58-59）。两者都没有返回 Nothing（75）。evaluateRecoveryEffects 只把位置变成效果：交付是 DeliveredIdempotent，活跃是 ActiveRebound，冲突是 Conflict，Nothing 不产生效果（77-84）。文件里没有「不猜测」这三个字。对应句子在 requirements/planning/WHAT.md [018]（160-172）：恢复只依据持久化视图与底稿存在性，绝对不按底稿正文猜测阶段；既无活跃任期亦未交付时不猜测；底稿未创建绝对不得假定已交付；冲突必须 fail-closed。Relay 与 Manager 没有同构函数。Relay 里读到的是折叠重放：Fold.tryReplayAssessment（168）、replayedRetirement（466）、ReviewBackendManager 的 ReplayCandidate（392）。Manager 只在 Workflow.fs:471 有一条 provider 恢复注释。planRecoveryPosition 不读确认事件。Recovery.fs 对 Confirmation、RetirementConfirmation、WorkspaceSnapshot、QualityCertificate 零命中。relay 四包 WHAT 没有「不猜测」这句。relay-retirement [007] 第 25 行写崩溃后不得永久处于已退休但缺 outcome/cut。relay-incumbency [012] 第 47 行写崩溃恢复保持 DevOps 绑定唯一与幂等，接收未知不盲目重发。上一笔「同构未证实」到此改成已读到的不同构。选定位置表在第 9 节，不在这里重写。
7. LWR 交接：同一 LifecycleWorkRecord.materialize 两边共用。
8. 双语资源成对 fail-fast：provider-language-006 一体适用。
9. 原子事务写、幂等重放、异载荷拒绝：事件写入纪律同构。

### 2.4 独有能力〔F〕

Manager 现行独有：委派面（fork 仅 Engineer、resume 固定 DevOps、join、horizon）；Orchestrator commission 上游（delegation-004：commission 只委任 Manager）；两段式退休确认（目标态保留为 deliver 的两段，R9，不再叫 suicide）；QualityCertificate（目标态退役，R5）；WorkspaceSnapshot（目标态不留字段、不留类型名、不留独立模块，见第 9 节 P1）；AuthorityRevision；Continue 自动派发（manager-assess loop prompt + gate nudge）；完整物理历史保留（目标态退役，R2）；评审期只读 js-manager 与评审接纳前禁派工（capability-enforcement-025/026）。「js-manager」这一句沿用材料。本任在工作区 `ToolRegistry.fs` 没有读到这个字面量，也没有展开 `generatedJsSpecs`，不把它改写成未注册，也不升为本任新核过的注册事实。后续核对见第 9 节 P4：字面量不在 `ToolRegistry.generatedJsSpecs` 的函数体里；生产点在 `ManagerReviewTools` 与 `StaticTools`。两道门的义务进主路径，不停留在这句材料。

Plan 独有：3 Stage 递进（review 晋级是唯一推进器）；js-plan（写前 review 硬门、禁路径参数）；ask 同步委派（单未决）；任期隔离+前缀重锚（planning-017、TenureReanchored 事件）；工作底稿（唯一可写产物）；自然停止（planning-009 空停豁免，Plan 是唯一允许空停等用户 prompt 的 role）；reprompt 重开（PlanReopened 沿用原任期）；明令禁止质量证书（planning-008）；deliver 一段式（一次调用即接力或交付，R9）。

planning-017 现行机制（`requirements/planning/WHAT.md` 第 152–155 行是先前读到的位置，这次没有重读；材料写的 145–155 被已经入库的 [011] 条文挤过，不再当现行行号，也不是未提交增量）：上一任期及更早任期的全部 assistant 消息与 tool 消息必须全部剥除；非首任由 `LifecycleWorkRecord.materialize` 且 `includeOpening = false` 渲染 `LWR_prev`，在首条本任期物理消息之前插入一次。同一条款已被 `4b9ef101d` 改成按稳定 XTrace 身份归属，禁止依赖 Host 消息上并不存在的 `cursor` 字段。剥除与注入 `LWR_prev` 仍成立；材料里的 message-cursor 归属已失效。本任读的是 HEAD 条文，没有对调查日做逐字节 diff，不能声称 [017] 其余句子与调查日逐字相同。

### 2.5 语义分歧〔F〕（契约级，非实现惰性）

- 历史语义相反：Manager 完整保留物理历史给后继（relay-context-projection-001；NarrativeTransform.fs:135-138 注释「it never filters the provider message set」）；Plan 剥除前任 assistant/tool 消息、注入 LWR_prev（planning-017；现行装配是 `TenureIsolation.assemble`）。这是现行代码。R2 已裁决目标态全局剥除。
- 收尾语义：截至 HEAD，代码仍是 Manager 两段式 suicide + 证书收尾，Plan deliver 一段、双路径。目标态（R5+R9）改成：两边都叫 deliver；证书退役；确认次数保持分叉——Manager/S4 两段，Plan 一段。分叉是风险偏好，不是还没统一完的残留。
- 等待语义：Manager 无空停豁免；Plan 自然停止。R6 裁决各自保留。
- 派发语义：Manager Continue 自动派发后继 prompt；Plan 接力不派发，靠下一次请求自然装配。R6 裁决各自保留。
- 质量凭证：Manager 有证书；Plan 明令禁止。R5 裁决证书退役。

### 2.6 git 史〔F〕

Manager/Relay：2026-08-14 起 → 2026-09-04 relay 重构三部曲（382d162b0 wip → e175b3fc6 clean break → a19309ff long-stroke）→ 09-05/06 suicide lifecycle、pure loop → 09-07/08 架构切分 → 09-13 收尾 → findings pairs（30f53445e）→ 两段式 suicide（694750f00）。

Plan：2026-10-10 20:28 `9fcc15824` 一次性全新引入整个 Mission/Planning（非复制，此前仓库无此目录）→ f0bfd802f ask 两段式挂起（后废弃，019 条款已删）→ 24babfac8 重接（四工具真实现；handoff 并入 deliver、resume 从 Plan 退役）→ ad50ddc15 js-plan 重构+写前 review 硬门 → 4f34d54e1 等修复 → 3d327213d 提示词重写。

2026-10-11 之后、本任覆核前，碰到 Planning 或 relay-assessment / ReviewShell 的提交：`24babfac8`、`8af4fc11c`、`ad50ddc15`、`4b9ef101d`。`4b9ef101d` 只动 Planning 与 `requirements/planning`，没动 Relay、Manager、`requirements/relay-*`。`ad50ddc15` 改了 `ReviewShell.fs`，没有拆掉双后端。`requirements/relay-incumbency`、`relay-retirement`、`relay-context-projection` 自 `9fcc15824` 后没有被路径过滤到的提交。

统一重构先例：无。仅 8af4fc11c 合并重复条款（006→017、012→018）；无任何「统一两模式」的 commit 或 TODO。2026-10-12 覆核：`src/Wanxiangshu/Mission/{Planning,Relay,Manager}` 搜 TODO / FIXME / 统一 / WIP 没有命中。仓库根 `plan.md` 是手写材料，不是源码上的统一改动，也不是本任务的活底稿。

### 2.7 术语对照〔F〕

| 概念 | Manager 侧（2026-09 relay 词汇；目标态见右注） | Plan 侧（2026-10 新词） |
|---|---|---|
| 任务载体 | Road | Work |
| 任期 | Incumbency / iteration（序号 ordinal） | Incumbency / tenure（任期、一棒） |
| 评审 | assessment（工具名已统一 review，后端仍叫 AssessmentCommitted） | review（PlanReviewed） |
| 评审结果 | PERFECT/REVISE（已退役）→ findings 空/非空 | Promoted / Stayed |
| 结束动作 | 现行代码仍叫 suicide（两段式）。目标态改名为 deliver，两段确认保留（R9） | deliver（一段式，R9 明确保留） |
| 退休结果 | Continue / Accepted certificateId | Relayed / Delivered |
| 质量凭证 | QualityCertificate（目标态退役，R5） | 无（planning-008 禁止） |
| 接力材料 | 完整物理历史 + 快照（目标态改为剥除 + LWR_prev，R2） | LWR_prev + U + 当前任交互（剥除前任） |
| 阶段 | assess → work → finish（事实推导循环） | S1 → S2 → S3（显式 stage，review 推进） |
| 交接工具 | fork / resume / commission | 无（deliver 双路径内嵌） |

同一个「每任一次评审门」在需求里一处叫 relay-assessment-002，一处叫 planning-022——两套词汇指同一族概念。

### 2.8 调用关系与频率〔F+I〕

〔F〕Manager session 三来源：用户直建；Orchestrator commission（delegation-004）；Plan 持权 deliver spawn 无 parent manager 会话（planning-023，PlanTools.fs spawn "manager"，覆核位置 144–150 行）。Plan session 唯一来源：用户显式创建（agent=plan）。Plan 的交付产物必须交给 Manager 会话执行。

〔F〕用户在 U 中断言 Manager「高频使用」，并在 R8 允许这条路径重新设计。这是用户裁决，不是仓库测量。

〔I〕从调用拓扑可以支持这个断言的方向：Manager 是执行主干、Plan 无任何自动入口、Orchestrator 只委任 Manager。仓库无明文「默认走 Manager」。README 无 Plan 使用说明，CHANGELOG 把 Plan 列在 Unreleased。仓库测不到「高频」这两个字。规划不再把这一点留成待用户确认。

### 2.9 测试覆盖〔F〕

这次 ask 在现行 HEAD `650850ea`、干净工作区里重数。不是沿用材料。

planning 需求：20 条，见 2.1。缺 006、012、019。

planning 测试：`requirements/planning/tests/` 有 24 个 `*.test.mjs`：001、002、003、004、005、007、008、009、010、011、013、014、015、016、017、018、020、021、022、023、024、025、026、027。没有 006、012、019，也没有 `host-canary-plan-relay.test.mjs`。材料里的「25 个 + host-canary」作废。024 的标题锚点是 `WHAT[planning-018]`，不是 024。025、026、027 的锚点是 `WHAT[planning-017]`。`011.test.mjs:250` 仍是 `WHAT[planning-011] js-plan tool description reuses the shared anchor tutorial with pathless signatures`。各文件 `WHAT[planning-` 命中行这次有清单，不在这里逐行再抄；下一任要复查时从 `git grep -n 'WHAT\[planning-' -- requirements/planning/tests/*.test.mjs` 重跑即可，这次不是零命中。

support 三文件路径与无锚点沿用上一任已读结论，这次没有重开那三份文件。

relay 四包，这次逐个读了 WHAT 标题行和测试文件名：

- `requirements/relay-incumbency/WHAT.md` 12 条：3 [001]、7 [002]、11 [003]、15 [004]、19 [005]、23 [006]、29 [008]、33 [009]、37 [010]、41 [011]、45 [012]、49 [013]。缺 [007]。测试 001-006、008-013，同样缺 007。
- `requirements/relay-retirement/WHAT.md` 8 条：3 [001]、7 [002]、11 [003]、15 [004]、19 [005]、23 [007]、31 [008]、37 [009]。缺 [006]。测试 001-005、007-009，同样缺 006。
- `requirements/relay-assessment/WHAT.md` 11 条，001-011 连续，标题行 3、7、11、15、19、23、27、31、35、39、43。测试文件只有 001-010。WHAT 有 [011]，`011.test.mjs` 不存在。这一条材料没写，是这次新读到的缺口。
- `requirements/relay-context-projection/WHAT.md` 9 条，001-009 连续，标题行 3、7、11、15、19、23、27、31、35。测试 001-009，编号齐。

GAP-192、GAP-193、GAP-197 不是零命中，也不只是材料里的三句概括。源码目录没有这些字符串。登记行：`requirements/GAP.md:251` GAP-192 relay-incumbency PARTIAL；`252` GAP-193 relay-assessment PARTIAL；`256` GAP-197 relay-retirement PARTIAL。`253` 的 GAP-194 句子里也出现 GAP-193。测试 todo 仍在：incumbency `001.test.mjs:20`、`003.test.mjs:17`、`005.test.mjs:40`、`010.test.mjs:19`、`011.test.mjs:23`、`012.test.mjs:18`、`013.test.mjs:77`，以及该包 `tests/README.md:9`；assessment `003.test.mjs:22`、`005.test.mjs:28`、`008.test.mjs:114`、`010.test.mjs:58`，以及该包 `tests/README.md:8`；retirement `001.test.mjs:3`、`002.test.mjs:24`、`003.test.mjs:19`、`004.test.mjs:22`、`007.test.mjs:109`，以及该包 `tests/README.md:12`。材料里的三句概括仍然对得上这些句子的方向，但「本任未重开」那句作废。

### 2.10 `assembleAskContinuation` 已退役〔F〕

材料写它仍由 `Surface.fsi:62` 导出、`TenureIsolation.fs:378-390` 实现、源码无调用方。该坐标已失效，不是待清理残留。

2026-10-12 覆核：`src/` 下 `git grep` 零命中。`TenureIsolation.fs` 只有 122 行，没有这个函数。`Surface.fsi` 第 61 行是 `executeDeliver` 的 `retirementCursor` 参数，第 62 行是返回类型 `Task<obj>`，不是那个导出。`4b9ef101d` 的 CHANGELOG 第 12 行写明退役 `TenureIsolation.assembleAskContinuation`。现存提及只在 CHANGELOG、仓库根材料 `plan.md`，以及 `proposals/archive/feat-PLANNER/001.md`。统一重组的模块清单不得再把它列成活代码清理项。

### 2.11 不可核实/未穷尽〔U〕

- `proposals/` 归档目录未逐个展开搜索统一重构提案（本次仅确认 manager-review canary 相关日志存在，以及归档里还有 `assembleAskContinuation` 的提及）。
- 「Manager 高频」无仓库明文。用户已在 U 中断言，见 2.8。仓库仍测不到频率数字。
- support 三文件的路径与锚点状态已核实，见 2.9，不再是〔U〕。

### 2.12 2026-10-12 覆核仍成立的负载〔F〕

这些结论被本任 ask 直接读到，不是沿用材料坐标：

- 历史语义仍然相反。`requirements/relay-context-projection/WHAT.md` 第 3–5 行标题仍是「[001] 完整物理历史」，要求物理 transcript、durable audit 与后继 provider projection 均保留前任全部物理消息。
- QualityCertificate 仍是 Relay 活类型。`Contract.fs` 91–104 行定义记录，`RetirementOutcome.Accepted` 携带 `QualityCertificateId`。需求用中文「证书」，不常出现英文标识。planning-008 仍写「禁止引入质量证书（QualityCertificate）或事后复核机制。」现行 `requirements/planning/WHAT.md` 第 77 行就是这一句，已在 `650850ea` 提交正文里。本任评审前的 ask：`git diff HEAD -- requirements/planning/WHAT.md` 为空，相对 HEAD 无差异，不是工作区多出来的增量。`[011]` 标题在第 96 行，第 101 行共享教程句也在同一份已提交正文里。证书禁令这一句不在 `4b9ef101d` CHANGELOG 点名的改写清单里，没有把它和调查日原文做逐字节对照。
- Suicide 仍是两段式。`requirements/relay-retirement/WHAT.md` 第 21–23 行 [005]：第一次调用不退休，追加 durable `RetirementConfirmationCommitted`；第二次以不同 `toolCall` 调用才进入正常退休流程。`SuicideTool.fs` 352–393 行实现这一点，文件 466 行，自 `9fcc15824` 后无提交。
- Plan deliver 仍是无权接力、持权交付并 spawn manager。见 2.2 的覆核行号。退休游标不再按旧的 `openingCursor + 10` 伪造。
- ReviewShell 仍是共享壳，按 role 分派。需求锚点 `requirements/relay-assessment/WHAT.md` 第 45 行 [011]。

### 2.13 工作区前态已经进 HEAD〔F〕

现行事实：HEAD 是 `650850eabe78fa6b1f0ddabd4bf5cac8725e2dd8`（2026-10-12 11:37:37 +0800，`fix: reuse the shared JS anchor tutorial for js-plan`）。本任评审前的 ask：`git status --short` 无输出；`git diff HEAD -- requirements/planning/WHAT.md` 无输出，WHAT.md 相对 HEAD 无差异。`4b9ef101dbaa7e9d1068850f08e3d356bf2c1ff1` 是该 HEAD 的祖先。`[011]` 已在提交正文：标题第 96 行，共享教程句第 101 行。`[008]` 证书禁令在第 77 行，也在提交正文。下面列出的文件是更早两次 ask 看见的脏树前态，不是现行未提交改动。那些改动已经进了 `650850ea`。统一重组不得把这笔记当成还摊在工作区里的另一笔活去「避开覆盖」，也不得把它当成自己的迁移去改写或回退。生命周期事件没有新证据说被这笔记改掉；目录级 diff 相对 `4b9ef101d` 没重跑，不补写一条没读到的「源码零差异」。

上一任看见的清单留在这里，作为那笔记提交收进去的前态，不删：

第一次 `git status --short` 的已修改文件：`resources/provider/tool/js-plan/en.md`、`zh-CN.md`；`resources/provider/tool/js-program/` 下 contract-edit-recommendation、edit-prelude、edit-structural-guidance、rules-edit、rules-read 的 en/zh-CN；`src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs`、`ToolRegistrySurface.fs`、`ToolRegistrySurface.fsi`；`src/Wanxiangshu/Repository/Programming/Js/Description.fs`、`Description.fsi`、`OpenCode/ToolHost.fs`。未跟踪：`resources/provider/tool/js-program/ultra-plan/`。

第二次 diff 额外见到、第一次 status 没有列出的：`requirements/planning/WHAT.md`（+2）、`requirements/planning/tests/011.test.mjs`（+51）。`git diff HEAD --` Mission/Planning、Relay、Manager 源码目录为空。四个 relay 需求包没有出现在这次非空 diff 里。

工具名未改。`planningSpecs()` 仍是 js-plan、ask、deliver。review 仍是 ReviewShell.spec。manager 侧 fork、resume、join、horizon、suicide 仍在。`ToolRegistry.fs` 的 diff 只把 js-plan 的 Description 从单独的 `ProviderProse.render` 换成 `JsCanonicalDescription.renderPlan` 包住同一段 prose。没有新工具名、新角色名、新阶段名、新事件名。

`js-manager` 字面量没有出现在 `ToolRegistry.fs`。js 工具经 `yield! generatedJsSpecs ()` 进入规格表。本任没有展开这个生成函数，不能把「文件里没有这个字符串」写成「工作区没有注册 js-manager」。

上一任当时读到、现已随 `650850ea` 入库的 [011] 条文要点（不是现行未提交增量；标题第 96 行，共享教程句第 101 行）：交给模型的 `js-plan` 描述复用其它 `js-*` 的同一套教程面（有序锚点、`text(from, to)`、精确 `edit`、结构重组、按底稿职责成形的样例）；教程按无路径签名填写，`file(matches = [])`、`edit(changes)`、`rewrite(newText)`；`P` 的单一作用域、禁路径和写前评审只作为作用域约束放在教程前面。禁止另写精简教程，禁止在教程或样例里教路径参数，禁止把运行时内部对象暴露给模型。新增测试标题：`WHAT[planning-011] js-plan tool description reuses the shared anchor tutorial with pathless signatures`。新表面函数名：`jsPlanDescription`。

`js-plan` 的 en/zh-CN 正文被收成作用域前言，原先整段教程删掉，改由共享教程面填写。共享 `js-program` 的 md 把写死的 `file(path, ...)` / `edit(path, ...)` / `rewrite(path, ...)` 换成 `{{fileCall}}`、`{{editCall}}`、`{{rewriteCall}}` 一类占位符。这是描述装配，不是生命周期。

`ultra-plan/` 两份文件各 17 行，是 `class Js extends JsProgram` 的样例，调用已有的 `file`、`text`、`rewrite`。英文锚点 `## Facts`、`## Target`，中文锚点 `## 事实`、`## 目标`。不是新工具，不是新角色，不是新阶段。已跟踪代码里新增的是路径字符串 `tool/js-program/ultra-plan` 和 `UltraPlan` 字段；`ultraExample` 分支用的是已有角色名 `plan`。

执行约束（按现行 HEAD 改写）：上述文件不再是未提交文件。它们在 `650850ea` 里。统一重组不回退这笔记的 js-plan 描述装配，也不把它收成生命周期迁移。上一任写「不改生命周期事件」时，依据是当时的 diff 没改事件名；这次没有重开那份 diff，不把那句升级成对 `650850ea` 全文的新审计。

## 3. 根因

本节记录裁决前追出来的根因。R2、R5、R6、R9 已经把对应分岔裁完。不要把本节里「必须裁决」的句子再读成未决问题。

Q1 为什么两套结构并存？
时序根因：Manager/Relay 于 2026-09 relay 重构先成型；Plan 于 2026-10-10 由 9fcc15824 全新引入在后。引入时没有复制 Manager 代码（无共同文件祖先），但语义上大量借用 relay 词汇与合同（Incumbency/Retirement/Relayed/findings pairs/固定 DevOps）——概念同源、实现分叉。

Q2 平行重复从哪里来？
Plan 全新成域时，把已核实同构的骨架机制（状态机、评审门、接力、DevOps 绑定）各自重新实现了一遍；LWR 例外，直接共用。重复不是复制粘贴，是同一语义的两份独立实现。恢复不在这份已核实清单里：Plan 有 Recovery.fs 的判定，Relay/Manager 这次搜索没有同构判定函数。不能把「两边都重写了恢复」写成根因。

Q3 哪些已自然合流、哪些没有、为什么？
已合流：review 工具壳（relay-assessment-011，共享壳+按 role 分派双后端——这是本仓库已有的统一先例）、LWR、PluginTransforms 管线（同一管线里顺序两个 stage，角色判定在处理函数里，不是 if/else 双分支）、PromptCatalog、ToolRegistry。未合流：状态机（Relay/Fold 890 行 vs Planning/Fold 503 行）、持久化（AgentJournal vs EventStore+底稿）、术语、退休动作的名字（suicide vs deliver）、历史语义。
为什么：合流都发生在「外围工具与装配层」——改动局部、风险低；未合流的都在「状态与语义核心」。历史语义已由 R2 裁决。确认次数由 R9 裁决为保留分叉，不是合流对象。

Q4 「隔离与用词偏旧」的根因？
用词偏旧：Manager 侧词汇冻结在 2026-09 relay 重构期；Plan 引入时换了新词，两边各说各话。这是词汇演化时差，属惰性重复，可归一。R3 已裁。
隔离偏旧：Manager 的「隔离」是四件真实契约（评审信息时域隔离 relay-assessment-008、stale cut 拦截、评审期只读+禁派工、固定 DevOps 控制权）加一条与 Plan 相反的历史语义（完整保留 vs 剥除）。前四件是真实责任差异，统一时应保留；历史语义已由 R2 裁为剥除。

结论：重复分两类。惰性重复（状态机双实现、术语分岔、退休动作的名字）可以统一。恢复不在这份惰性重复里：Plan 有 Recovery.fs 判定，Relay/Manager 没有同构判定函数，已读到不同构，见 2.3 第 6 项。曾经被当成「待裁决语义分歧」的四条，现在都有了裁决：历史保留 vs 剥除 → 剥除（R2）；证书 vs 无证书 → 无证书（R5）；自动派发 vs 自然停止 → 按工作性质各自保留（R6）；一段确认 vs 两段确认 → 按风险偏好各自保留（R9）。R9 不是惰性重复。把它并成一次调用，会抹掉用户明确留下的风险差异。

## 4. 取舍

### 选项 A：Plan 升维为统一生命周期，Manager 作为执行段的宿主（用户倾向方向）

形态：把「任务生命周期」统一为一条阶段流水——S1 事实 → S2 理解 → S3 流程 → S4 执行。S1-S3 同 session、任期隔离、剥除历史、review 晋级；S4 即现在的 Manager：独立 session、委派循环。入口两个：S1 入口（显式规划）与 S4 直入口（高频执行，即现在的直接开 manager 会话）。

本段最初在材料里写成「S4 完整历史」。那是选项评估当时的描述，已被 R2 否决。S4 目标态是剥除历史、注入 LWR_prev，同时保留两段式 deliver（R9）。

与事实的相容点：
- Plan S3 持权交付已 spawn 无 parent manager 会话（planning-023）——「S3 毕业 → S4 入学」的接缝已经存在，A 只是把它升格为显式模型。
- ReviewShell 已是「共享契约 + 按 role 分派后端」的先例——统一生命周期可沿用同一模式。
- 骨架对照里八项责任同构提供语义对齐表。第 5 项的字段级同构已失效，责任级同构还在。第 6 项崩溃恢复已读到不同构，见 2.3。

冲突点与代价：
- Manager 无 stage 递进语义（assess/work/finish 是事实推导的循环，非 stage）。用户 R1 的修正比这里更彻底：不做「执行段+宿主」的区分建模。Manager 就是 S4，是恰好只有一个 Stage 的退化情况。
- 历史语义已由 R2 裁决为全局剥除。
- 两套持久化要统一重设计，不是桥接（R4）。
- relay 约 30 个测试锚点按 KISS & DRY 重设计（R7）。
- 高频路径允许重新设计（R8）。
- 确认次数不许被统一压平（R9）。

### 选项 B：backport（把 Plan 的隔离/deliver/新词搬回 Manager）

用户已否决。代价备档：当时 Manager 完整历史保留契约与剥除冲突；relay 测试资产大改；Plan 的 stage 模型对 Manager 无意义；方向是让新的向旧的看齐。R2 之后，历史语义代价已经不构成否决理由，因为保留契约本身被判定为错误决策。否决理由剩下的是：方向是让新的向旧的看齐，且不产生单一基础设施。

### 选项 C：抽共享核心层，两模式保留各自外壳

形态：已核实同构的骨架抽成 Mission 核心层（任期状态机、评审门、接力、DevOps 绑定），Manager/Plan 各留 session 拓扑、工具面、术语。恢复不进这份清单：Plan 有 Recovery.fs 判定，Relay/Manager 没有同构判定函数，已读到不同构，见 2.3。
代价：只修实现重复，不修术语与语义分岔；两套需求包继续并存；核心层若两边 invariant 必须一起改，形式分层就是假边界。为什么不是它：用户要的是重组，不是分层。R9 的确认次数分叉不是这条选项里的「各自外壳」；它由同一 deliver 机制按开立事件上的序列导出。用外壳分叉来承载它，会把风险偏好写成两套结束动作。

### 选项 D：维持两套，继续点状合流（现状惯性）

代价：术语分岔扩大、双份状态机维护、每次改骨架机制要改两处。收益：零迁移风险。为什么不是它：用户已明确要重组；惰性重复会继续累积。

### 对用户提议「Manager 作为特殊第四 Phase」的评估

与事实相符：Plan 的终点已是 Manager 的起点（spawn 接缝存在）；Manager 独立 session、高频直入，与前三段的同 session 模式确实构成「特殊」。
材料里曾写「更准确的建模是执行段+宿主角色」。R1 否决了这个区分。以用户裁决为准：Manager 就是 S4，恰好只有一个 Stage。

### 用户裁决（2026-10-11 第二段 prompt，八条全答；2026-10-12 补第九条）

- R1 统一深度：采纳 A 并更彻底——Manager 就是 S4，是恰好只有一个 Stage 的退化情况；不做「执行段+宿主」的区分建模。统一对象是单一生命周期模型本身，Manager 与 Plan 都是它的实例。
- R2 历史语义：全局统一为剥除。Manager 完整保留物理历史被判定为「错误的历史决策」，与 Plan 对齐（剥除前任消息、注入 LWR_prev）；relay-context-projection-001 契约推翻。Plan 侧现行剥除机制以 HEAD 的稳定 XTrace 身份为准，不以材料里的 message-cursor 模型为准。
- R3 术语：全部重命名成 Plan 的词汇风格。
- R4 持久化：迁移到 DRY，以 Plan 为底稿重新设计（统一重设计，非桥接）。
- R5 证书：退役，向 Plan 看齐（planning-008 禁令全局化）。
- R6 等待/派发语义：各自保留——Manager 是自动工作（Continue 自动派发），Plan 是交互工作（自然停止）。按工作性质保留，非按历史保留。
- R7 测试迁移：KISS & DRY 原则重新设计。
- R8 高频路径：允许重新设计。
- R9 结束确认次数（2026-10-12）：Manager 保留两段式 deliver 语义，Plan 仍为一段式。用户原话：「这是两者不同风险偏好的体现。」解读边界：保留的是现有 Manager 两段确认（第一次调用不结束，落 durable 确认事实；第二次以不同调用才结束）。名称改为 deliver。不恢复 QualityCertificate。不把两段并成 Plan 的一段，也不把 Plan 拉成两段。确认次数由开立事件上的序列导出，不是单独字段。第二次在无证书之后产出什么结果族载荷，S2 再设计；确认次数本身不再是 S2 的开放项。

总目标（用户原话）：「仿佛从一开始就设计好了 DRY 版本的基础设施，Manager 和 Plan 都是基础设施的实例化或者受益者，要看不出发展历程和痕迹。」

R9 与这个总目标不冲突。DRY 统一的是生命周期基础设施，不是把两种风险偏好压成同一次确认。从一开始就设计好的版本里，S4 本来就是两段确认，Plan 本来就是一段。

规划者推荐与用户裁决的差异（记录在案）：材料形成时，当时的规划者推荐过保守方案（按段裁决历史语义、持久化先桥接、relay 锚点改名不重写、高频路径行为不变）；用户裁决全部取彻底方向（全局剥除、统一重设计、测试重设计、路径可重设计）。以用户裁决为准。

2026-10-12 本任覆核曾把「两段式退休确认的去留」留到 S2。用户当轮否决了这个推迟：两段式留下，作为 Manager/S4 的 deliver，不是待裁的开放项。

## 5. 本棒记录与移交

本节不是验收声明。2026-10-12 覆核表明，材料把三处已失效坐标写成了〔F〕：`assembleAskContinuation` 的存续、`TenureIsolation` 的行数与函数名、planning-017 的 message-cursor 归属。这些已在上文订正。R9 已入档。仓库根 `plan.md` 与本活底稿不是同一个槽。下一任自行判断 S1 离 S2 还有没有差距。本任不自证。

写出 2.13 的那一任只订正了两处：事实基线拆成 HEAD 与工作区；support 三文件的路径和锚点状态。那一任不自证 S1 已满。

评审后的这一笔只补三处事实标注，外加 HEAD 从 `4b9ef101d` 挪到 `650850ea`、脏树已入库。条款与测试清单改成本次 ask 的出处。七处材料行号按重读改写或作废。2.3 第 6 项不再把没读到的 Relay 恢复判定写成同构〔F〕。这一笔同样不自证 S1 已满。再后一笔只改互相否定：根因不再把恢复写成两边各实现一遍，不再把 PluginTransforms 写成双分支；2.3 标题改为八项已核实、第 6 项同构未证实；2.12 与 2.13 对 WHAT.md 只留一个现行答案，相对 `650850ea` 无差异，[011] 已在提交正文。这一笔同样不自证 S1 已满。

下列五问的选定答案在第 9 节。这里只留索引，不重开：

- Q-S2-1 S4 单 Stage 退化模型的形态：统一生命周期的 Stage 语义（review 晋进）对 S4 意味着什么；Manager 的 assess/work/finish 事实推导循环在单 Stage 模型里如何表达；Plan work（S1→S3，交付 spawn）与 Manager work（直接 S4）在统一事件词表下如何同构；「Manager 恰好只有一个 Stage」与「S4 是四段流水的第四段」两种读法如何统一（候选：Stage 是 work 的属性序列，Plan work = S1,S2,S3，Manager work = S4；Plan 交付 spawn 的 Manager work 从 S4 开始）。确认次数已由 R9 裁死，不在这个形态问题里重开。
- Q-S2-2 剥除语义对 Manager 侧的推翻清单：relay-context-projection-001 及其 9 个测试锚点重写；NarrativeTransform 的 stale cut（「never filters the provider message set」前提消失，cut 只剩请求身份判定）；恢复逻辑；Continue 自动派发与剥除装配的相互作用——后继不再看到前任消息、靠 LWR_prev 接力，自动循环在新语义下如何保持连贯。Plan 侧现行剥除以 `4b9ef101d` / planning-017 HEAD 为底：稳定 XTrace 身份，consumed-through 退休游标，禁止依赖 Host 上并不存在的 cursor。不得拿材料里的 message-cursor 模型当现行契约。
- Q-S2-3 持久化统一设计：以 Plan EventStore+底稿为底稿吸收 Manager 侧事实；WorkspaceSnapshot、AuthorityRevision 的去留与载荷形态；证书退役后事件族裁剪；统一事件词表（Plan 7 事件 + Relay 事件族 → 一套）。Manager 第一次确认必须落进这套词表，不能成内存态。
- Q-S2-4 术语映射落地：Road→Work、iteration→tenure、assessment→review、suicide→deliver、Continue/Accepted→Relayed/Delivered。Accepted 去掉 certificateId 之后叫什么，并入 Q-S2-3 的结果族载荷，不在这里先起名。需求包收敛形态（relay-* 四包 + planning 一包 → 统一包）；资源目录归一。suicide 这个词不保留为「两段式」的别名。
- Q-S2-5 测试重设计锚点方案：统一契约的锚点编号空间；两套平行测试合并去重清单；GAP-192/193/197 处置。一段 deliver 与两段 deliver 各自要有锚点。planning 与四包 relay 的条款数、测试文件名、GAP-192/193/197 的登记行，已由修复本棒的 ask 重数，见 2.9，不再是未重数缺口。support 三文件这次没有重开，仍用上一任已读结论：路径在 `requirements/planning/tests/support/`，没有 `WHAT[planning-` 锚点。
- Q-S2-6 模块级迁移路径：合并/重写/退役模块清单；迁移顺序（先核心后外围）；每步验收门。`assembleAskContinuation` 已在 `4b9ef101d` 退役，不是清单上的活代码清理项。文档残留只在 CHANGELOG 已记录的退役句、归档提案，以及仓库根材料 `plan.md`。那份材料不是第二份规范。迁移不得把 `650850ea` 里的 js-plan 描述装配当成未提交脏文件去覆盖或回退。详见 2.13。那笔记不是本规划的迁移载荷。

S2 继任者注意：用户已把「无考古痕迹」定为最终判据——不引入兼容层、不留过渡命名、不为旧测试保留旧语义入口。

已关掉、不许再当成开放价值问题的：历史全局剥除（R2）；术语用 Plan 风格（R3）；证书退役（R5）；Manager 自动派发、Plan 自然停止（R6）；高频路径允许重设计（R8）；Manager/S4 deliver 两段、Plan deliver 一段（R9）；活底稿是 P，仓库根 `plan.md` 只是材料。工作区快照字段不留，不再留给下一次核对，见第 9 节 P1。权威推进改名保留为 AdvanceCommitted，不并进 epoch；这是规划者推荐，不是未裁的价值问题，见第 9 节 P1。工作区在 `650850ea` 上是干净的；上一任看见的脏树已入库，见 2.13。support 三文件路径与无锚点沿用上一任已读，这次未重开。Relay 与 Plan 的恢复不同构，已读到，见 2.3 第 6 项。统一位置表已选定，见第 9 节「恢复位置」。不在本节重写。

## 6. S2 第一段：Stage 是 work 的序列属性

本段只钉一个读法。不写路径，不动 Q-S2-2 到 Q-S2-6。

Stage 不是全局枚举里的一格，是 work 的一个属性，取值是一段序列。Plan work 的序列是 S1、S2、S3。Manager work 的序列是 S4。S4 标记的是「执行」这个性质，不是「前面排着三段」。没有「四段流水的第四段」这个对象。Manager work 的序列长度是 1，这就是用户原话里的「恰好只有一个 Stage 的退化情况」。

依据是 R1。用户否决了「执行段加宿主角色」的区分建模，裁的是「Manager 就是 S4」。第 5 节 Q-S2-1 列出的两种读法，按这条裁决收成上面这一种，不再并列。

Plan 持权 deliver spawn 出来的会话，序列也是 S4，不从 S1 再走一遍。接缝在现行代码里已经有：planning-023，`PlanTools.fs:144-150` 的 `spawn "manager"`（2.2 这次重读钉住，2.8 复核同一行号）。统一之后这条接缝还在，变的是它有了名字。

评审追加事实。投影是否换成下一个元素，只看序列里还有没有下一个。S4 的 review 仍是每任一次、禁自证、非空即修复义务（2.3 第 2 项：relay-assessment-001/006 与 planning-022）。序列长度是 1，没有下一个元素，投影留在 S4。S4 里没有「晋级」这个对象。deliver 不按这个投影跳转。

assess、work、finish 留在 S4 里面，是任期事实推出来的分支，不升成 Stage。依据是 `Workflow.fs:76-90`，注释写从不可变事实推导，不从程序计数器来（2.2 这次重读）。四个分支里，「有绑定证书走 finish」在证书退役后失去事实来源。换什么来源见第 9 节 P4 的五支表。证书不再是事实来源。

确认次数不在本段。R9 已裁：S4 两段，Plan 一段。

本段未写的五项，选定答案在第 9 节。候选、主路径与反事实在第 8–10 节。读法本身由 R1 钉住，后文不重开。

## 7. 理解

要改的是两套已经在跑的生命周期实现。一套是 Plan：同会话换任期，序列 S1、S2、S3，事件进 EventStore，后继看不到前任消息。一套是 Manager：独立会话，序列长度是 1、取值 S4，事实进另一套 Relay 事件，后继仍带着全部物理消息。结束动作都要叫 deliver。Plan 调用一次就结束。S4 第一次只落下确认，第二次另一次调用才结束。

做完之后，六项骨架只有一套实现：唯一活跃任期、退休不可逆、每任一次评审且禁自证、接力不改序列、固定 DevOps、LWR 交接。Plan work 与 Manager work 都是这条生命周期的实例。差别只剩工作性质：Plan 等用户，S4 自己派发后继；以及确认次数：一段或两段。

已裁死、不许重开的：全局剥除、术语用 Plan 风格、证书退役、自动与交互各自保留、高频路径可以重设计、两段与一段、活底稿是 P、仓库根 plan.md 只是材料、Stage 是 work 的序列属性。工作区快照比较这次已经读过，不再开着：没有「不等就专门拒绝」，快照字段不留，见第 9 节 P1。权威推进不并进前缀 epoch：Relay/Fold.fs:393-402 与 Prefix/Epoch.fs:151-160 不是同一函数，见第 9 节 P1。这是规划者推荐，不是未裁的价值问题。

难点不在换目录名。难在把两套事实收成一套，同时不把「一段 / 两段」和「自动 / 交互」抹平。剥除不会拆掉现行自动循环：后继派发不读物理消息，开后继也不拷贝消息。要钉死的是这条依赖，不是另造一条消息通道。

## 8. 候选路径

### 选定：一套事件内核，副作用由序列导出

核心思路：以 Plan 的事件存储和折叠为唯一事实面。S4 不另设状态机。序列写在 WorkOpened。确认次数、是否自动派发、持权 Delivered 是否 spawn，都从这条序列导出，不是可以互相打架的字段，也不是第二套事件名。

主要风险：同一个结果名会带出不同副作用。Plan 的 Relayed 不派发；S4 的 Relayed 派发后继。Plan 持权的 Delivered 才 spawn；S4 的 Delivered 不 spawn。读代码的人会把同名当成同副作用。

事实：提示函数 deliverLoopPrompt（Workflow.fs:329）不读消息，不读 LWR，参数是序号、退休 id、ProjectionCut.ProviderRunId。开后继是另一个函数 deliverLoopContext（Workflow.fs:371）：退休之后另一次事务，383 行 captureSnapshot，载荷含 snapshotId，不拷贝消息。统一后提示函数仍不读消息、不读 LWR；开后继不再抓快照，不把 snapshotId 写进开任载荷。评审在终元素上只把授权置真，不改写阶段字段（Fold.fs:236-239、254-262）。当前阶段是折叠投影。「结果不换序列」已经有先例。

### 淘汰：两套事件族，上面加翻译层

否掉。R4 要的是以 Plan 为底稿重设计，不是桥接。翻译层是兼容层，撞 G8。

### 淘汰：剥除之后给自动循环补一条消息摘要

否掉。提示函数 deliverLoopPrompt（Workflow.fs:329）不读物理消息，不读 LWR。开后继在 Workflow.fs:398-403 写 IncumbencyOpened，不拷贝消息。补摘要是在修一个不存在的断裂。开后继现行会抓工作区快照，那不构成消息通道，统一后删掉这次捕获，见 P1。context-projection-006 还禁止合成消息，这条义务剥除后仍留。

### 淘汰：S4 也走评审晋级，空 findings 当结束

否掉。序列长度是 1，没有下一元素（第 6 节，R1）。结束靠第二次 deliver（R9），不靠评审。

### 淘汰：保留 suicide 作为两段式的别名

否掉。R3 与 G5 要一套词。两段是确认次数，不是另一个工具名。

### 权威计数不并进前缀重锚

已读到权威比较与前缀重锚不是同一函数。不并。
若实现时发现二者是同一判定且同一调用方，则停止并退回规划。
不授权实现删 AdvanceCommitted。不授权实现改词表。
这不是交给执行者的取舍。
## 9. 主路径

副作用由开立时写下的序列导出，不由结果名决定。Relayed 与 Delivered 两个名字两边共用。派发、spawn、确认次数不从名字里读，也不另存字段。

### P1 事件内核

依赖：无。必须先于 P2–P7。串行。

触碰：Plan 的事件名、事实、折叠、存储。Manager 的 Relay 事件停写，不留第二套词表。

不触碰：工具的名字与注册规格、资源正文、XTrace 捕获、前缀 epoch 的折叠实现。工具文件里追加事件的那一处要改。P1 的「不触碰工具注册表」不得被读成「文件里的追加点也不许动」。

词表按这次读到的两份判别联合逐条处置，不用材料短名。Plan 在 Planning/Facts.fs:101-120，七个 case 都带 Plan 前缀。Relay 在 Relay/Facts.fs:4-24，九个 case。留下的名字去掉 Plan 前缀和 Road 前缀：两套实例共用一套词，前缀会把词表写成某一侧的私货。

留下的 case：

- WorkOpened。PlanWorkOpened 改名并入。RoadOpened 并入后删除。载荷是 workId、root、sequence、权威初值。sequence 是这条 work 的阶段序列，开立时写死，后继不改写。Plan work 写 S1、S2、S3。Manager work 与持权交付 spawn 出来的执行会话都写 S4。RoadOpened 上的 PhysicalUserMessageId 不进开立载荷，随 R2 作废。过期请求的身份由退休上的 cut 承担，不由开立时的消息 id 承担。权威初值是 WorkOpened 的必含字段。构造只在 Algorithm A 的 A3：固定标记 authority-initial-v1 加 workId 的确定性摘要。不依赖物理用户消息标识。不是 0。不是空串。这半条不再与推荐同进退。确认次数、是否自动派发、持权 Delivered 是否 spawn，不进载荷。它们从 sequence 导出：序列为 S4，则确认次数为 2、自动派发、Delivered 不 spawn；序列为 S1、S2、S3，则确认次数为 1、自然停止、仅持权 Delivered 才 spawn。不另留字段。序列长度不是 1 也不是 3，拒绝开立。
- DevOpsBound。PlanDevOpsBound 与 RoadDevOpsBound 并入这一个。载荷是 workId、devopsId、target。不留 Road 前缀。
- IncumbencyOpened。PlanIncumbencyOpened 与 Relay 的 IncumbencyOpened 并入这一个。载荷是 workId、incumbencyId、起始投影、openingCursor。阶段序列不在这里，只在 WorkOpened。首任的起始投影是序列的第一个元素。接力开出的下一任，起始投影等于退休当时的折叠投影，开任不另算下一步。S4 只有一个元素，起始投影写 S4。不含 snapshotId。无权接力同一笔里，序列为 S1、S2、S3 时，下一任 openingCursor 等于这笔退休载荷里名为 retirementCursor 的字段。序列为 S4 时，下一任 openingCursor 来自本次调用已观察到的 XTrace 头序号，不从退休载荷读取名为 retirementCursor 的字段。两种序列的值都是本次 deliver 的 XTrace CurrentHead.sequence，不加一、不加十。剥除比较是 sequence > OpeningCursor。出处：PlanTools.fs:256-274 把同一个 retirementCursor 写进退休和下一任开任；ToolRegistry.resolveDeliverRetirementCursor:755-771 取的是 receipt.CurrentHead 的序号；TenureIsolation.keepsInteraction:44-46。Planning 源码已无 openingCursor + 10。后继的新 id 仍由上一笔退休 id 派生，不拷贝消息。现行 Relay 开后继在 Workflow.fs:398-403，事件是 IncumbencyOpened(incumbencyId, snapshotId)，并且是退休之后的另一次事务。统一后 snapshotId 不进载荷，开任与这笔退休同一笔追加，见下面的交付形状。
- ConfirmationCommitted。RetirementConfirmationCommitted 改名并入。只允许确认次数为 2 的实例写入。载荷是 incumbencyId、providerRunId、toolCallId。现行是 SuicideTool.fs:377-386 的 confirmFirst，身份门在 361-368。Plan 不写这一笔。
- IncumbencyRetired。PlanIncumbencyRetired 改名并入。RetirementCommitted 并入后删除。结果只有 Relayed 与 Delivered。Continue 收成 Relayed：派发后继，不改序列。Accepted 收成 Delivered：收束，不 spawn。证书退役后，这两条不再由证书选出。确认次数为 2 时，第二次 deliver 读折叠上的评审 outcome：Promoted 写 Delivered，Stayed 写 Relayed。见 P4。序列为 S1、S2、S3 时，载荷是退休 id、incumbencyId、outcome、ProviderRunId、ToolCallId、名为 retirementCursor 的字段，外加它所声明的期望推进值，不带 ProjectionCut。序列为 S4 时，载荷是退休 id、incumbencyId、outcome、ProjectionCut，外加它所声明的期望推进值，不带名为 retirementCursor 的字段。身份摘要的字段次序两种序列相同：固定标记、workId、ProviderRunId、ToolCallId、outcome，外加入口读到的 work 计数。确认次数为 1 的标记是 oneshot-retirement-v1。确认次数为 2 的标记是 retirement-v1。两个标记不合并。retirementCursor 不进身份。ProjectionCut 进 S4 载荷，不重复进身份。不从活跃任期算这个 id。不把每次重捕的 XTrace 头序号算进身份。期望推进值从 work 上的同一计数读出，写进这笔退休，只是声明，不是第二个 writer。计数不挂在活跃任期上。与折叠上的当前值不符则拒绝。开立留下权威初值而退休载荷缺期望推进值，同样拒绝。不写 IncumbencyRetired，也不写 RetirementBlocked。四格的后继只在 Algorithm D 的 D3 与 Algorithm E 的 E6。AdvanceCommitted 是这一计数的唯一推进事件；退休事件不改计数，推进事件也不另带一套 expected 来和这笔声明竞赛。AuthorityRevision 这个词不留在统一事件里。RetirementSummary 在 Contract.fs:110-116，字段是 Id、IncumbencyId、SnapshotId、AuthorityRevision、ProjectionCut、Outcome。SnapshotId 默认不留。ProjectionCut 只留在序列为 S4 的退休载荷上：context-projection-002 的义务是 cut 只识别退休尝试，这条没被 R2 推翻，所以 cut 写在 S4 这笔退休上，不另设事件。序列为 S1、S2、S3 的退休载荷不带 ProjectionCut。幂等用退休 id，不靠证书。id 不从活跃任期算，也不把 XTrace 头序号算进去。
- Reviewed。PlanReviewed 改名并入。AssessmentCommitted 并入后删除。结果是 Promoted 或 Stayed。载荷是 incumbencyId、stage、outcome，不含 findings 对。Plan 现行就是这样：PlanReviewed（Planning/Facts.fs:119）只有这三项；对列表在返回值 PlanReviewResult.Gaps（ReviewPlanBackend.fs:9-11、82-106），不追加成事件；折叠只存 outcome（Planning/Fold.fs:55）。空与非空由 outcome 表达：Promoted 是空，Stayed 是非空。Relay 的 AssessmentFindings 不带进统一事件。Promoted 不改写阶段字段，也不推动计数器。它只追加一条 Reviewed 事实。当前阶段是折叠从 WorkOpened.sequence 和已落盘的评审事实投影出来的，不是存在字段里的下一步。序列里还有下一个元素，且 outcome 为 Promoted 时，投影换成下一个元素，对象仍是这一任。现行 promoteTenure（Planning/Fold.fs:232-239）做的就是这件事：S1 的投影变成 S2，S2 的投影变成 S3，同一 active。那是事实投影，不是程序计数器。序列已到最后一个元素时，投影留在该元素。交付授权只在这里置真，而且只在确认次数为 1：投影已是序列末元素，且 outcome 为 Promoted。S3 现行只把 Authorized 置真，投影留在 S3；确认次数为 1，对得上。确认次数为 2 时，终元素上的 Promoted 不置交付授权，投影也不换元素，只记下这一任已评。S4 序列长度是 1，走这一句。空 outcome 不结束，也不生证书。Stayed 不换投影，也不置交付授权。

时序判定的允许形与禁止形只写在这里。失败点七只引用这一处。

允许形，条款原文，本棒 ask，HEAD 650850eabe78fa6b1f0ddabd4bf5cac8725e2dd8，工作区干净：

[001] 第 5 行：「业务流程直接使用宿主语言的顺序、分支、调用、资源作用域与有界递归。调用栈承载执行位置，不得把“下一步走向”保存为长期字段或枚举。」

[002] 第 9 行：「领域 DSL 采用直接执行的 CE 与具名领域操作，不得构造业务 AST 再解释执行，或用 Command/Reply 总线、continuation 状态树及调用序列回放器调度业务。外部协议解码与物理命令的强类型表示不受此限。」

生命周期投影是对已落盘事件的折叠。现行折叠是对已落盘事实的穷尽 match，走到一支就停，返回一次结果。穷尽 match 是现行折叠写法，不是条款命令。阶段序列只出现在 WorkOpened 载荷里，那是这条 work 的序列属性，不是「下一步走向」。deliver 匹配任期、评审、确认、阻塞、授权，不按阶段跳转。

先前调查读过、本棒未重读，不写成这一棒的〔F〕：requirements/planning/WHAT.md 第 23 行、第 186 行标题「工作状态机」。若那两处仍在，指的是事件投影，不是第二套机器。本棒没有重读那两行。

禁止形，仍是上面的条款，不另造句子：

不得把“下一步走向”保存为长期字段或枚举（[001] 第 5 行后半）。不得构造业务 AST 再解释执行，或用 Command/Reply 总线、continuation 状态树及调用序列回放器调度业务（[002] 第 9 行）。这句上不添这次检索没有的词。

[003] 本棒读到的原文。第 13 行：「DU 与字段只表达封闭的领域词汇、已发生事实的持久证据或单次返回结果。不得在 record、DU、可变存储或跨模块接口中保存、发布执行位置、下一步指令或多个状态的正交乘积；改名、搬移和恢复用途不构成例外。」第 15 行：「Stage、Phase、Lease、Owner、Generation 不得作为流程程序计数器或伪领域状态；除真实物理归属和资源世代外，也不得用于业务状态机命名。」第 17 行：「恢复从 Journal fold 的事实和当前物理观察重新证明未完成义务，再进入普通业务流程；不得恢复 continuation、临时中间状态或把“最新 case”当作恢复地址。Reconcile 只稳定观察，不接管业务调度；协议 Phase 只能作为只读诊断投影。」

本棒读到的另两条禁止句。crash-reconciliation WHAT 第 17 行：「恢复过程遵循 Journal facts → Fold → 纯恢复决策 → 普通 workflow 合法入口。严禁恢复 Program 节点、continuation 或执行步数，严禁引入 RecoveryStage 等第二状态机。」delegation WHAT 第 95 行：「复用 participant 的每个新工作是独立 direct-CE invocation，不建立 durable Stage/Phase/ActiveWorkUnit 或第二状态机。」

先前调查读过、本棒未重读，不写成这一棒的〔F〕：relay-incumbency WHAT 第 9 行「另设状态机」；planning WHAT 第 32 行「旁路状态机、内存标记或外部计数器」，第 85 行「两段式 ask 挂起状态机」。

未找到的整话：用户原句「本仓库不允许“状态机”，只允许 F# DSL 来处理时序逻辑」在这次检索里没有。不升成条款。「计算表达式」四个字这次检索没有。条款写的是直接执行的 CE，不是那四个字。「转移表」这次检索没有，不升成条款。Program.fs:7-9 是文件头注释，写控制流是 plain let!/do!/match/task，不是状态机。它是注释，不是条款。

能证伪，只在这一处：实现把下一步走向存成长期字段或枚举；或构造业务 AST 再解释执行；或用 Command/Reply 总线、continuation 状态树、调用序列回放器调度；或把 Stage、Phase、Lease、Owner、Generation 当流程程序计数器或业务状态机的名字。观察到任一条，允许形作废。

一次 deliver 的过程顺序只写在 Algorithm D 与 Algorithm E。这里不另留一张活调用表。P3、P4 引用这两处，不另起一套门。

交付授权不是确认次数为 2 的锁。确认次数从 sequence 导出，不另存字段。

确认次数为 1 只走 Algorithm D。确认次数为 2 只走 Algorithm E。入口先按本次 Host 上下文的 ProviderRunId 与 ToolCallId 查已落盘退休，再谈无任期。现行 ToolRegistry.fs:819-820 与 PlanTools.fs:34-42 在更早一层返回 no active incumbency 失败字符串，这一层作废。围栏还在，不得先于查找返回。

一段式不写 ConfirmationCommitted。无评审放行。投影复制、阻塞写事件、持权先启动后写，都只按 Algorithm D。两段式的确认、同一调用冲突、第二次入口，都只按 Algorithm E。现行 SuicideTool.runFrozen（370–393）的无评审拒绝不套到一段式。用户没裁要给一段式加评审门。

能证伪的破坏：确认次数为 2 且已评 Stayed，第一次 deliver 因缺交付授权被拒；确认次数为 2 的 Promoted 仍置交付授权，并用该位放行或拒绝第一次确认；无评审却写了 ConfirmationCommitted。一段式：无评审的 deliver 被拒；S1 Promoted 之后的 deliver 被拒或写成 Delivered；遇阻塞只返回字符串、不写 RetirementBlocked。Relayed 不换投影。下一任开立载荷上的起始投影等于退休当时的折叠投影。现行 buildRelayTransition（PlanTools.fs:272）复制的是这次 deliver 看见的投影；需求 [004] 写接力不推进、下一任沿用退休时的投影。评审之后解锁写是许可（[005]），不是必须先写下一段才能接力的代码不变量。提示词方法行读这个投影，那是诊断，不是下一跳调度。评审事实落在这一任，所以下一次装配读到的是新投影。这次没有重读装配器行号，不补坐标。AssessmentBinding 类型不进统一事件。Contract.fs:74-82 的八个字段这次没有打开哪些是不等就拒绝，列进载荷就会把没读到的比较写成事实。Reviewed 载荷只留 incumbencyId、stage、outcome。钉住所裁那一次尝试的义务，由 Plan 现行这三项承担，不由那八个字段承担。AssessmentCommitted 元组里的 WorkspaceSnapshotId 不带上。findings 对仍不进事件。
- Reopened。PlanReopened 改名留下。Relay 没有对等 case。载荷是 incumbencyId。S4 不写这一笔。生产路径这次没有读到写入点：PlanEventStore.append 的生产调用不含 reopened；测试是直接 applyWorkEvent。条款写系统追加，调用方未读到。不发明函数名。折叠义务不抄现行硬编码。Fold.applyReopened（372-381）进 restoreDeliveredTenure（347-370）时，现行把 Authorized 写成 true，和条款「沿用原授权」对不上。目标是：同一任期 id，不生成新 id；阶段从已交付记录复制；评审整份留下；授权从那笔记得读，读不到就拒绝，不许补 true。没有 Delivered 退休、任期 id 不符、或授权读不到，都拒绝，不猜。
- AdvanceCommitted。AuthorityRevisionAdvanced 改名留下，不留 AuthorityRevision 这个词。载荷是 incumbencyId、expected、next。PhysicalUserMessageId 不带，随 R2。WorkspaceSnapshotId 默认不带。生产写入只有一条：已有 Manager job 再 commission。continueManagerJobCore（Change/Host/Host.fs:547-571）调用 advanceAuthorityRevision（507-544）。证书有效挡住推进（requireActiveWorkOwnedIncumbent:496-505，Valid = true 就拒绝）。证书失效不推这个计数；QualityCertificateInvalidated 的折叠不改 revision。证书退役后，这道有效证书门作废。缺评审仍拒绝：现行还要求 AcceptedAssessmentTransport 非空。没有这次再 commission，就不写 AdvanceCommitted，不另造推进动作。checkRetirementAuthority（Relay/Fold.fs:393-402）只比较，不推进。findAuthorityAdvanceViolation（302-318）管的是 expected/next 是否过期、是否没变、消息 id 是否用过。PhysicalUserMessageId 与 WorkspaceSnapshotId 不进载荷。证书失效不随这条留下。保留第二计数器、不并进 epoch，仍是规划者推荐，不是用户裁决。已读到它和前缀 epoch 不是同一函数：Relay/Fold.fs:393-402 与 826 行钉的是推进，Prefix/Epoch.fs:151-160 的 applyTenureReanchor 钉的是前缀。不并。若实现时发现二者是同一判定且同一调用方，则停止并退回规划。不并进 epoch。不删第二计数器。不改词表。
- RetirementBlocked。RetirementCleanupBlocked 改名留下。不并进 IncumbencyRetired，因为退休没有发生。载荷是 incumbencyId、blockerDigest。定义在 Relay/Facts.fs:22。写入在 SuicideTool.fs:128-129 与 Relay/Fold.fs:875-876。义务是 relay-retirement 的 [003]：唯一业务 blocker 是当前任期直接或递归拥有的 live child、background job、PTY/process、active tool、side-effect/execution lease、同步 descendant provider work，以及尚未观察到 terminal 的 cancel/join。清理之后可以再调用 deliver 走关闭。[007] 那句「CleanupBlocked 的通过任期可在清理后重试 Accepted」收成重试 Delivered，不留 Accepted 这个名。这不是用户裁决。没有哪条裁决把阻塞义务作废。

删除、义务另有着落的：

- PlanDelivered 删除。它不再既是结果名又是事件。结果名是 Delivered。execSessionId、digest、path 写进 IncumbencyRetired，仅当结果是 Delivered 且该实例的 spawn 参数为真。S4 的 Delivered 不填这三项，不 spawn。持权才 spawn，门在 runDeliverDecision（PlanTools.fs:313-316）的 AuthorizedDelivery。workId 已经在工作上，不重复写。

一段式交付的现行形状不是同一事务。appendRetireAndOpen（PlanTools.fs:90-100）先追加退休，成功后再追加开任。第一笔成功、第二笔失败，不回滚。appendRetireAndDeliver（113-123）同样，先退休再追加 PlanDelivered。持权路径是 spawnAndCommitDelivery（195-220）：先启动执行会话，失败则零 Plan 事件，成功后才写。

统一裁定：无权交付的退休与后继开任放进同一次 Append 的列表。这次调用不是整批原子屏障。AppendDurable 才算两笔都落地。AppendNotAttempted 才是两笔都未提交。CommitUnknown 不报告已落地，也不报告未落地。序列为 S1、S2、S3 时，下一任 openingCursor 等于这笔退休载荷里名为 retirementCursor 的字段。序列为 S4 时，下一任 openingCursor 来自本次调用已观察到的 XTrace 头序号，不从退休载荷读取名为 retirementCursor 的字段。两种序列的值都是本次 deliver 的 XTrace CurrentHead.sequence，不加一、不加十。持权仍先启动会话，失败零写；成功后只写退休，不再单独写 PlanDelivered。不把现行两笔 append 说成已经是同一事务。S4 的 Relayed 同样：开后继不再是退休之后的另一次事务，也不再 captureSnapshot。
- QualityCertificateInvalidated 删除。证书类型在 Contract.fs:91-99，字段是 Id、AssessmentId、IncumbencyId、SnapshotId、AuthorityRevision、Binding、Valid、InvalidationReason。随 R5 整个作废，没有留下的事实承接失效。

不并进这套的：TenureReanchored 仍在前缀（Companion/Facts.fs:105-110，写入 PluginTransforms.fs:707-712）。

工作区快照 id：这次只读已经打开比较点。相等检查嵌在精确重放谓词里，不是单独的「哈希不等就拒绝」分支。isExactAssessment（Relay/Fold.fs:147-159）只返回布尔。tryReplayAssessment（168-182）合取不成立，包括快照 id 不同，返回 AssessmentReplayConflict，错误名不写快照不符。decideStoredAssessment（185-199）精确相同（含快照 id）返回原状态；id 或 tool call 相同但快照不同，同样是 AssessmentReplayConflict。isExactAuthorityReplay（290-300）含快照相等；调用方 authorityReplay（276-280）精确重放失败返回 AuthorityRevisionReplayConflict，不是快照不符。decideIncumbencyOpen（608-613）同一 id 且同一快照返回原状态；同一 id、不同快照掉进 ActiveIncumbencyAlreadyExists，不是单独的快照拒绝。validateFreshAssessment（201-220）参数里有 snapshotId，函数体不比它。findAuthorityAdvanceViolation（302-318）比 revision 和 message id，不比快照。checkRetirementAuthority（393-404）只比 AuthorityRevision。Manager 目录没有把两个 snapshot id 拿来比的函数。搜过 SnapshotId 相等与不等、SnapshotMismatch、workspace hash、content hash，后一组零命中，没有读到 SnapshotId <> 这种直接不等比较。默认不留这个字段，不留 WorkspaceSnapshot 这个类型名，不留独立模块，是因为这次读到没有「不等就专门拒绝」，不是因为还没搜。上面凡是写「默认不带」的 snapshot id，都服从这一句。不把字段加回来，不新造 SnapshotMismatch。这不是用户裁决。

这一步答 Q-S2-3。证书退役后的结果族就是 Relayed 与 Delivered，不再有 Accepted certificateId。第一次确认是 ConfirmationCommitted，不是内存态。PlanDelivered 不再是独立事件。

写入点只列这次读到的生产构造。折叠里的 match 不算写入。停写之后这些函数不再持有第二套事实。统一事件只有一条追加口。没有读到的跳转不补函数名。

- RoadOpened 只在 IncumbencyOpening.initial（Relay/Facts.fs:68-69）与 IncumbencyOpened 同一笔事务。调用方是 Workflow.decideInitialOpening（Manager/Workflow.fs:295），条件是该 session 还没有 road view，且角色是 Manager，且没有 Algorithm A 的 spawn 标记。有这一标记的开立不走 Manager 直接开立。入口判别只在 Algorithm A。提交在 ensureManagerRoadOpened（同文件 316 行起）。Change/Host/Host.fs 不写 RoadOpened。统一后这一笔改成 WorkOpened（sequence 为 S4）加 IncumbencyOpened，不带 snapshotId。不开第二笔 work。
- RoadDevOpsBound 的事件字面量只出现在 Relay/Fold.fs 的 bindRoadDevOps（836、838、842）。生产侧 resume（Tool.fs:688）与 horizon（HorizonTool.fs:168）调用 scope.EnsureRoadDevOpsBound；函数体在 ToolRuntimeScope.fs:693，只转到 obtainRoadDevOpsTask。这一跳有没有落到 bindRoadDevOps，这次没有读完，不写函数名。S4 的绑定触发仍是 resume 与 horizon，不搬到开任。事件名改成 DevOpsBound。不发明中间函数。
- IncumbencyOpened 有三处「下一任」生产构造，不能用一处盖住另外两处。IncumbencyOpening.next（Relay/Facts.fs:88）只含这一条事件。deliverLoopContext（Workflow.fs:399）沿用上面已写的裁定：退休与后继开任同一笔追加，不抓快照，不写证书失效。过程不在本节。tryBuildAcceptedOpening（Workflow.fs:260）与 activateNextIfAbsent（Change/Host/Host.fs:270）只转 Algorithm Q。无活跃任期时零写、拒绝。不写 IncumbencyOpened。不并进一笔并不存在的退休。首任只来自 Algorithm A。后任只来自 Algorithm D 的 D8，或 Algorithm E 的 E11。deliverLoopContext 与这两处都停写 QualityCertificateInvalidated。
- AssessmentCommitted 的构造在 ReviewBackendManager.fs:210-228 的 assessmentTransaction。这个窗口没有 append，调用方这一轮没追到，所以还不是已证实的生产写入。Decision.assess（Fold.fs:851）这次没读到生产直接调用。统一后改写 Reviewed。不把 Surface 包装、也没打开的 append 调用方，当成已证实的生产写入点。
- AuthorityRevisionAdvanced 的唯一生产构造是 advanceAuthorityRevision（Change/Host/Host.fs:533）。调用链是 ContinueManagerJob，再是 continueExistingCommission（Fork/OpenCode/Tool.fs:797）。这是 commission 续做已有 Manager job，不是 fork，不新开 work。统一后仍只写 AdvanceCommitted。
- QualityCertificateInvalidated 随 R5 停写，事件删除。读到的构造：tryBuildAcceptedOpening（Workflow.fs:256）、deliverLoopContext（Workflow.fs:395），原因字符串都是 ContinuousSessionAdvancesRoad，仅当证书仍 Valid；SuicideTool.fs:123，原因 RetirementBindingChanged，与退休同一事务。Change/Host/Host.fs:218 另有一处构造，调用方这次没再往上追，不发明调用方。停写，不留事实。
- RetirementCleanupBlocked 的生产构造在 SuicideTool.fs:129。Decision 第 876 行也有构造，生产调用方这次没读到，不把它写成第二生产源。统一后改写 RetirementBlocked。
- RetirementConfirmationCommitted 的生产构造在 SuicideTool.fs:381。统一后改写 ConfirmationCommitted。
- RetirementCommitted 的生产构造在 SuicideTool.fs:126。统一后改写 IncumbencyRetired。
- fork 工具本体不写这九个事件。executeManagerAfterGuards 与 executeManagerNewCalling 这次搜索没有 RelayEvent。不改 fork 的写入。ForkHarness 上的 injectAcceptedAssessment（ToolSurface.fs:1429）与 injectAuditPendingIncumbency（1526）会写 RoadOpened、IncumbencyOpened、AssessmentCommitted，那是注入，不是生产路径。随 P6、P7 停写旧事件，不保留为第二生产源。join 这次搜索没有 RelayEvent，不改。

### 恢复位置

依赖：P1 的折叠视图。不新造模块，不新造事件。Relay 与 Manager 没有同构恢复函数，不发明函数名。现行 Planning/Recovery.fs 的 planRecoveryPosition（67）与 evaluateRecoveryEffects（94）改成读 Algorithm F。折叠重放（Fold.tryReplayAssessment 168、replayedRetirement 466）不是这张表，不拿来当恢复入口。

输入只两样：已经折好的统一视图，以及底稿文件在不在。不读还没落盘的工具调用，不读底稿正文，不读确认事件去发明退休。ConfirmationCommitted 只决定视图里有没有活跃任期，不单独变成一个恢复位置。

格子不在本节。恢复位置的唯一正文是 Algorithm F。输出还有 PairIncomplete 与拒绝，以及 F2、F3、F4、F5 已经命名的那些。F4.1 不可达，不是输出格。本节不另写一套格子。第 10 节只列破坏观察，也不另写格子。
不要把「只有 Delivered」读成不含历史 Relayed。不要把「看不到交付、也看不到活跃任期」读成 Relayed-only 的 Nothing。

### P2 历史投影

依赖：P1 的退休事实已经是后继唯一的交接事实。与 P4 并行，不与 P3 抢同一处写入。

触碰：Manager 后继的 provider 投影。后继装配时新接 LWR，复用 Plan 已有的 previousTenureRecord（PluginTransforms.fs:719-736），不在 Manager 再写一份 materialize。

不触碰：XTrace 捕获、前缀折叠、Plan 自己的剥除比较。

剥除依据与 Plan 相同：稳定消息身份的首次观察序，晚于本任 OpeningCursor 才留。比较单位是 XTrace 观察序（TenureIsolation.fs:46，Fold.fs 存的是 XTraceCursor）。不用退休工具调用的下标。无稳定身份则拒绝装配，不猜测。NarrativeTransform.fs:135-138 那句「never filters the provider message set」作废。projectActive 原样写回作废。

Manager 侧现行没有 LWR materialize 调用。统一后的注入是新接线，不是改一个已有参数。

context-projection 九条，按义务本身拆，不按「改名就能留」拆：

- 001、003、004、005、007、008、009 的义务是保留完整物理历史。剥除后改写义务，不是改名。
- 002 仍成立：cut 只判定请求身份，不当成新任的活跃请求。
- 006 仍成立：不生成合成消息，不概括前任。

这一步答 Q-S2-2。恢复不在 P2。位置表在上面「恢复位置」，不在这里新造模块。relay-assessment [008] 不在这一步。剥除历史覆盖不了提示时域，见 P4。

### P3 结束动作

依赖：P1 的确认事件与退休事件。串行于 P1。工具改名可与 P2、P4 并行，因为不写投影、不写派发。

触碰：结束工具的名字与资源目录 tool/suicide。

不触碰：确认次数的判定本身。次数从 WorkOpened.sequence 导出，不在工具里分叉成两个工具，也不另存字段。

名字只留 deliver。一次调用的过程顺序只在 Algorithm D 与 Algorithm E，这里不另写一套。确认次数为 2 的身份门只跳到 Algorithm E，不在这里另写谓词。无评审转 E3。无确认转 E4。同一调用转 E5。第二次入口转 E7。E7 的谓词是本次 toolCallId 与已确认的不同，且 providerRunId 在，且 toolCallId 在，且本次已有 XTrace 头序号。缺头序号按 E7 拒绝。零写。不进入 E8。确认次数为 1 不套这步，顺序只引 Algorithm D。现行 runFrozen（370–393）先看有没有评审，再进 runConfirmed（361–368）。把 E7 收成拒绝，两段式就死掉。

折叠侧管再写一笔确认事件，现行在 Relay/Fold.fs:674-695 的 confirmRetirement。两支：精确同一对返回原状态；已有确认但这对不完全相同，一律 RetirementConfirmationReplayConflict。这道宽拒绝不套到 deliver 调用上。第二次 deliver 不是再写确认事件，走身份门的第四支。

身份门与折叠重放门仍是两道比较，不并成一道。无任期、无评审那两步在它们前面，顺序以 Algorithm E 为准。围栏查找先于无任期失败字符串。Plan 一次调用直接退休，不写确认事件。不留 suicide 别名。

### P4 派发

依赖：P1 的 Relayed。与 P2 并行。

触碰：S4 后继 prompt 的装配。

不触碰：Plan 的自然停止；提示函数的资源正文可以重写，但提示函数的参数仍只来自序号、退休 id、ProjectionCut.ProviderRunId。开后继不再抓工作区快照。

S4 的 Relayed 之后自动派发。提示函数是 deliverLoopPrompt（Workflow.fs:329），文本来自 runtime 资源，不读物理消息，不读 LWR，不把 LWR 当控制输入。LWR 只进后继的交接材料（P2）。「不读消息」只描述这个提示函数，不描述开后继。开后继现行会抓快照，统一后删掉，见 P1。Plan 的 Relayed 不派发，等下一次请求。空停只属于 Plan。

这一步把 R6 落成序列导出：sequence 为 S4 则自动派发，否则自然停止。不是事件名差异，也不是单独字段。

确认次数为 2 的活任，提示词选择是一个互斥且穷尽的 match。读折叠投影，不读工具返回值，不读证书。读三样：有没有任期、评审 outcome（Promoted 或 Stayed）、确认位。确认位是折叠上的 RetirementConfirmation（Fold.fs:12-23 的 ActiveIncumbency.Confirmation，投影在 785-789）。现行 resourceForCurrentAction（Workflow.fs:86-90）不读这个位，只读 ActiveIncumbency、AcceptedAssessmentTransport、Certificate。finish 只由 88 行那一支选出，调用方 isBoundCertificate 在 80–88。换成五支是新接线，不是把现有分支改个名。选择器正文只在 Algorithm H。这里列出的格子与 H3 到 H7、H4.1 是同一套，不是第二份过程。H7.1 只留这两支没有命名的残格。拒绝格不在这里用通配补上。relay-assessment [008]（requirements/relay-assessment/WHAT.md:31-33）要的是提示与指令的时域，不被剥除历史代替，也不被「findings 不进事件」代替。评审前只给当前独立只读评估要求，不泄露低分接责、满分退场或后继循环。评审后供给者每次只看见一条指令。R9 下，Stayed 不是立刻结束，Promoted 也不是第一次 deliver 就退场：评审后的那一条指令按确认位分开，第一次只写确认，第二次才收束。自动循环机制不进这些正文。三份资源正文这一轮没打开，不写成现行正文已经满足或不满足。资源路径是现行的 runtime/manager-assess、runtime/manager-work、runtime/manager-finish，常量在 Workflow.fs:23、26、29。名字可以随 P5 改，事实来源不能改回证书，也不能改成工具进程内存。工具放行仍只引 Algorithm D 与 Algorithm E，不在这里另写一把锁。

- 无任期：不选资源。现行是 None。不允许 deliver。
- 有任期、无评审、无确认：选出 assess。不允许 deliver。这一格的正文只在 Algorithm H 的 H4。
- 有任期、无评审、已有确认：拒绝。不选 assess。零写。这一格的正文只在 Algorithm H 的 H4.1。
- Stayed 且尚无确认：选出 work。这是修复义务，不是工具拒绝。允许第一次 deliver：写 ConfirmationCommitted，返回，不收束。已有确认之后不再命中这一支，改走已确认那一支，不落回 work。Promoted 不走这一句。不读 findings 对，对不在事件里。
- Promoted 且尚无确认：选出 finish，参数是尚无确认。允许第一次 deliver：写 ConfirmationCommitted，返回，不结束。不要把现行「证书已绑定、可以结束」的正文发给这一支，否则模型会被教成一次调用就结束。这一支不读交付授权，放行不是因为授权解锁了它。
- 已有确认、未退休：选出 finish，参数是已有确认。Stayed 且已确认只命中这一支，不再选 work。第一次确认落地之后不能落回 work。这一支允许第二次 deliver。收束不写在提示词选择里。

收束选择器与这五支分开。只在已确认之后的另一次调用上读折叠上的评审 outcome 与确认位：Promoted 写 Delivered，不派发、不 spawn，工作收束；Stayed 写 Relayed，派发后继。不读证书，不读工具返回值，不读交付授权。S4 走到 Delivered 的路是已确认且 Promoted，不是提示词选了 finish。

确认次数为 1 的实例不走这五支。过程顺序只引 Algorithm D，这里不另写一把锁。无任期不放行。无评审放行，走无权 Relayed。终元素 Promoted 才置交付授权，解锁一次持权 deliver，持权才 spawn。Stayed 与非终元素 Promoted 不置持权，同任 deliver 是无权接力，不是拒绝。有活跃 blocker 在授权分支之前写 RetirementBlocked，不放行。

后继提示单独成段，不与这五支共用入口。它只在 Relayed 之后发出，不读这五支的正文当控制输入。参数仍只来自序号、退休 id、cut 的 provider run。

Stayed 这一支的出处：Manager 同任再评被 requirements/relay-assessment/WHAT.md [002]、[006] 拒绝，Relay/Fold.fs decideStoredAssessment 194–199 行两支都是错误，不是只拒绝精确重放。非空 findings 之后，SuicideTool.runFrozen 370–393 行的「没评审」门不挡，因为已有一次已提交的 assessment；无证书时 retirementTransaction 80–86 行结果是 Continue，不是拒绝。Plan 的 Stayed 不置 Authorized，PlanTools.fs decisionOfAuthorization 60–64 行走 RelayToSuccessor；requirements/planning/WHAT.md [022] 禁止的是第二评审和自证，不是同任 deliver。所以「选出 work」只是提示词路径，不是工具拒绝。

后继开任的写入点不止 deliverLoopContext。tryBuildAcceptedOpening 与 activateNextIfAbsent 的命运写在 P1，这里不另裁。

评审期两道门挂在 P4 下面，不新开事件，也不并进 P1 的 deliver 顺序。两道门都是对已落盘事实的穷尽 match，走到一支就停。这是现行折叠写法，不是条款命令。不是第三套生命周期，也不是解释执行的下一跳调度。

capability-enforcement [025] 是工具执行门，条款在 requirements/capability-enforcement/WHAT.md:111-113。禁的是工具执行，不是派工。`js-manager` 在 Schema 里固定可见，名字留下。它已经是 js- 工具名，不是 Road、suicide、certificate 那一类旧词。P5 不改这个名字，不删这个工具。运行时按统一事实收口，不读证书，也不读事件里没有的 findings 对。执行门正文只在 Algorithm I。已评、Stayed、尚无确认，放行修复派工，不是拒绝执行。清理阻塞指 I1 已落盘阻塞摘要那一格。退任冻结不单列，不保留。终元素 Promoted 的末任保留只读收尾：可读、清理、收口，不开新工作。原生 read、grep、glob，以及对 Manager 的跨角色 js-engineer、js-devops，仍然拒绝。现行落点是 PluginHooks.assertReviewPermitted（PluginHooks.fs:351-359）和 OfficeCapability.permissionsForManagerFacts（OfficeCapability.fs:133-143）。事实现在来自 AcceptedAssessmentTransport 与 AcceptedAssessmentFindings（ToolRuntimeScope.fs:466-477），不是证书。证书退役后改读：有没有评审、Promoted 或 Stayed、确认位、清理阻塞。ToolRegistry.generatedJsSpecs（ToolRegistry.fs:561-572）的函数体没有 `js-manager` 这个字面量。JsToolGenerator.generate 这一轮没打开，不写成已注册，也不写成未注册。字面量读到的生产点是 ManagerReviewTools.fs:11、16，StaticTools.fs:151、187，InvestigationEstimateContract.fs:34。退任冻结不保留。不补函数名。与 Algorithm I 同一句。

capability-enforcement [026] 是派工门，条款在同文件 115-117。没写 `js-manager`。与 deliver 不是同一把锁，不写进 P1 的调用顺序。评审接纳前禁止向绑定的固定 DevOps 派任何任务。拦截在任何持久化副作用之前。已有只读 Engineer 的合法 resume 与 fork 只读调查不受误伤。现行派工拒绝读到的是 executeManagerResumeAfterGuards（Tool.fs:677-708）：续做名字是 devops 且 HasAssessment 为假时返回 AssessmentPendingForDevOps。读的是评审传输是否存在，不是证书。证书退役后仍读「有没有评审」，不改去读证书。fork 路径这一轮没打开对应拒绝函数。[026] 的只读例外只按条款放，不写成已经在代码里核过。

能证伪：S3 还得自己决定 `js-manager` 去留；把禁派工写进 deliver 顺序；让这两道门改读证书。

### P5 术语与包

依赖：P1 的词表已定，否则包名会跟着旧词走。可与 P2–P4 的实现并行起草，但不能先于 P1 合并进仓库。

触碰：需求五包、资源里的 suicide 工具目录、代码与需求里的 Road、iteration、assessment、suicide、certificate、Continue、Accepted。

不触碰：role/plan 与 role/manager 两个角色目录。R6 的工作性质不同，方法行不同。留两个 role 是实例差异，不是考古。

映射：Road 改 Work，iteration 改 tenure，assessment 改 review，suicide 改 deliver。Continue 与 Accepted 不另起结果名，读成 Relayed 与 Delivered 的派发含义。certificate 删除，不改名保留。五包收成一包，条款按统一契约重编号，不留 relay- 前缀。tool/suicide 并入 deliver 的资源，两段写在实例说明里。

这一步答 Q-S2-4。Accepted 去掉 certificateId 之后不另起名，就是 Delivered。

### P6 测试

依赖：P5 的契约条文，否则新锚点会把旧义务抄进去。串行于 P5。

触碰：planning 与四包 relay 的测试。统一一个编号空间。一段 deliver 与两段 deliver 各有锚点，不能用一条 deliver 测试覆盖两种语义。

不触碰：把旧锚点改名后继续断言旧义务。

GAP-192、193、197 的登记行已核，测试正文这次未读。默认不继承旧锚点语义。写测试之前若要引用其中一条的现行断言，先重读那条测试。不在这里宣布它们作废或保留。

support 三文件这次仍未重开，沿用上一任已读：路径在 requirements/planning/tests/support/，没有 WHAT[planning- 锚点。不把它们写成已重读。

这一步答 Q-S2-5。

### P7 删除旧轨

依赖：P1–P6 的验收门都过了。最后做。串行。写入点的命运只引用 P1 那一段，这里不另列。

触碰：Relay 事件类型、AgentJournal 上的生命周期事实、证书类型、suicide 名字、仓库里把完整物理历史写成现行义务的句子。

不触碰：650850ea 里已经入库的 js-plan 描述装配，不把它当脏文件回退。仓库根 plan.md 不改。assembleAskContinuation 已退役，不是活清理项。

既有 AgentJournal 与 Relay 事件文件不读取、不转换、不写兼容层。新 work 只写统一事件轨。仓库根 plan.md 不改。不留兼容层，不留过渡名。

### 顺序

P1 先做完。验收门：两个实例都能开任、评审、退休；确认次数为 2 的实例，确认事件可重放，重放不第二次结束。

然后 P2 与 P4 并行，P3 的工具改名也可并行。三处写入点不同：投影、派发、工具名。

P5 在 P1 词表定了之后起草，合并晚于 P2–P4，早于 P6。P6 晚于 P5。P7 最后。

没有真依赖的不串成一队。P2 不等 P3，P4 不等 P2。

这一步答 Q-S2-6。

## 10. 检验

这条路径要解决的是 G1–G8，不是把目录和用词换掉。判据：六项骨架只剩一套实现；确认次数是参数；历史只剩剥除；术语只剩一套；持久化只剩一条事件轨。若实现做完仍有两套折叠、或 suicide 还在当别名，路径没有走完，不加适配器收场。

失败点一：第一次确认若只活在内存，G6 失败。调用在确认事件落地之前不得返回已确认。写盘失败则调用方看到未确认，可以再发，不得当成结束。

失败点二：原先担心剥除会打断自动循环。提示函数不支持这个担心。deliverLoopPrompt（Workflow.fs:329）不读消息，不读 LWR。所以不补消息通道。若实现时有人把提示函数改成拼接前任消息，P4 作废，回到只读退休事实。这是禁令，不是待补设计。开后继现行会抓工作区快照，那是另一件事，见失败点三。

失败点三：开后继现行在退休之后另一次事务里 captureSnapshot（Workflow.fs:371、383）。统一后删掉这次捕获，不把 snapshotId 写进开任载荷。若实现仍抓快照，就和默认不留工作区快照打架，P1 的默认不留作废。这次已经读到：没有「不等就专门拒绝」，相等只嵌在精确重放谓词里。不留。

失败点四：现行一段式交付是两笔 append。第一笔成功、第二笔失败，重放会看到已退休、无后继，G6 失败。无权交付必须把退休与开任放进同一次 Append 的列表。这次调用不是整批原子屏障。AppendDurable 才算两笔都落地。AppendNotAttempted 才是两笔都未提交。CommitUnknown 不报告已落地，也不报告未落地。持权路径若先写事件再启动会话，会话失败就留下没有执行会话的 Delivered。先启动，失败零写。

被证伪则停止并退回规划：若实现时发现前缀 epoch 与权威推进是同一判定、同一调用方，不授权实现并进 epoch，也不授权实现删第二计数器。工作区快照字段不在这一句里：这次读到没有不等就专门拒绝，默认不留，不新造模块，也不再留给下一次核对加回来。

失败点五：若实现去读取或转换既有 AgentJournal 与 Relay 事件文件，G8 失败。旧账不是新轨的输入。新 work 只写统一事件轨。

失败点六：若第二次 deliver 的结果仍从证书或工具返回值读，P4 作废。选择器只读折叠上的评审 outcome 与确认位。Promoted 已确认写 Delivered，Stayed 已确认写 Relayed。读不到这两样就拒绝写入，不猜测，不恢复证书。

失败点七：若实现走了第 9 节时序判定禁止形，或把那里标成未找到的整话升成条款，这条路径作废。允许形与禁止形只在第 9 节，这里不另写。

失败点八：若评审期工具门或禁派工改读证书，或把禁派工写进 P1 的 deliver 顺序，或删掉 `js-manager`，P4 下面那两道门作废。[025] 按统一事实拒绝执行，[026] 在副作用之前拒绝派工。两道门不是同一把锁。

失败点九：若把 relay-assessment [008] 当成 P2 已经答完，或把五支正文一起发给模型，或把「空 findings 即关闭退场」写回第一次 Promoted 调用，P4 作废。五支只选出一条可见指令。第一次确认不是退场。

失败点十：若 S4 成功收束因缺 digest 或 path 被判成 Conflict，或已确认未退休被判成已退休或 Delivered，或底稿不存在被改口成已交付，或崩溃后新造一个 DevOps 绑定，或读不到授权位时补 true，Algorithm F 作废。格子是 PairIncomplete、拒绝，以及 F2、F3、F4、F5。F4.1 不可达，不是输出格。第 9 节不另写格子。这里不另写。不新造 Recovery 模块，不新造事件。

这条路径不假装已经打开三份 manager 资源正文。不把它们写成已经满足 [008]。

## 11. 流程

执行只按本节。第 0–10 节是裁决记录，不是第二套过程。两处说法冲突时，以本节算法为准。

步骤标号是给实现者读的次序，不是运行时要保存的程序计数器。实现用宿主语言的顺序、分支与调用。不得把步骤标号存成枚举，不得构造转移表再解释执行，不得另开一套事件词表。

生命周期事件只有这一套：WorkOpened、DevOpsBound、IncumbencyOpened、ConfirmationCommitted、IncumbencyRetired、Reviewed、Reopened、AdvanceCommitted、RetirementBlocked。TenureReanchored 不是这一套里的事件，也不属于第二套生命周期账。不删除它。不把它并进这一套。不改前缀 epoch 的折叠。结果名只有 Relayed 与 Delivered。评审结果只有 Promoted 与 Stayed。追加口只有一个。一次调用可以交出按步骤次序排好的多笔信封。调用方把这些信封放进同一次 Append 的列表。列表次序就是步骤次序。宿主把这一次调用拼成连续多行，一次写出：一行一个信封，一行写完整才算落地。不得把同一笔拆成两次追加口调用。不得循环调用现行只收一笔的 append。不得把两笔信封并进一个 JSON 对象。不另造第二种编码。不借用 Sphinx 的 events 数组。不新造提交标记。这次写入没有整批全可见或全不可见的保证。一次 Append 调用只是落地形状，不是整批原子屏障。统一追加口返回三支，不把这三支压成 string。AppendDurable：列出的信封已落地。AppendNotAttempted：已知未提交，可以再发。CommitUnknown：既不报告已落地，也不报告未落地，不补另一笔，不截断，不另开一任。下一次打开才分类。下一次打开若文件不以换行结尾，读者整份返回 StorageInvalid。不进入 Algorithm F。不裁残行。不折被拒绝的前缀。下一次打开若只折出必成对两笔中的一笔，按 Algorithm F 返回 PairIncomplete。必成对只有两处：WorkOpened 与首任 IncumbencyOpened；Relayed 的 IncumbencyRetired 与后任 IncumbencyOpened。Delivered 单笔不套这格。不另开第二本生命周期账。
▷ 一行仍是一笔信封。理由：现行根对象的键是 event_id、stream_id、event_type、parents、payload、payload_refs，有内嵌载荷时再加 payloads。判别在 event_type。Planning 没有多事件一行编码。把多笔塞进一个对象，就是第二套编码。

没有 Algorithm P。无活跃任期时不另开一任，见 Algorithm Q。

权威计数挂在 work 上，不挂在活跃任期上。WorkOpened 写下权威初值，之后只有 AdvanceCommitted 改它。Delivered 取消任期、Relayed 换后任、后任 IncumbencyOpened、J2 重开，都不把它打回初值，也不另起一份。期望推进值是本次算法入口从 work 折叠读出的当前计数。不是开立初值的第二份拷贝。不是从活跃任期读出的字段。读不到这一计数，重放路径与写入路径各自拒绝、零写、终止。不回退到活跃任期。

### Algorithm A 开立一件 work

输入：workId、root，以及这次是不是用户创建的 Plan 会话、是不是 Manager 直接开立、是不是持权交付 spawn 出来的执行会话。
输出：AppendDurable 时两笔都落地；AppendNotAttempted 时返回未开立；CommitUnknown 时既不报告已开立也不报告未开立。写前拒绝则零写。

A1. [三个入口]
进入本步之前先走 A1.1。A1.1 拒绝则本算法不再继续。A1.1 通过才执行下面各句。
spawn 标记只由 Algorithm D 的 D6 在启动执行会话时置上。
载体只有一种：子会话的初始 prompt 首行，逐字节是固定标记 spawn-from-authorized-delivery-v1。
标记不进 WorkOpened 载荷。不进 Metadata。不从物理父标识推断。
开立这一刻只读这一行。读不到，则持权交付 spawn 入口不成立。不得降级成 Manager 直接开立。零写。算法终止。
若有这一标记，则 Manager 直接开立不成立，commission 新开不成立。
若这次同时命中两个入口，则拒绝。零写。算法终止。
有标记又被当成 Manager 直接开立或 commission 新开，是双重命中。按上一句拒绝。零写。算法终止。
若这次是已有 work 的再 commission，则不走本算法。它只走 Algorithm K。不新开 work。现行链是 continueExistingCommission 到 ContinueManagerJob。算法终止。
若这次是用户创建的 Plan 会话，则 sequence ← S1、S2、S3。转 A2。
commission 新开与 Manager 直接开立是同一入口，不是第四个标志。现行链是 commissionNewCalling 到 ForkManagerJob。不另起第四种序列。不写未读到的 RoadOpened 写入点。
若这次是 Manager 直接开立，或是 commission 新开，则 sequence ← S4。转 A2。
若这次是持权交付 spawn 出来的执行会话，则 sequence ← S4。转 A2。
若三者都不是，则拒绝。零写。不采用调用方另给的 sequence。不得从 S1 再走一遍。算法终止。
▷ 三个入口各自写死序列，命中后转入 A2。理由：赋值后若继续落到「三者都不是」，合法入口会被拒绝。
▷ S4 标记执行，不是前面排着三段。理由：序列长度是 1，这就是恰好只有一个 Stage。

A1.1. [已开立]
若折叠里该 workId 已有 WorkOpened，则拒绝。零写。不写第二笔 WorkOpened。不返回已开立。不转 Algorithm K。不转 Algorithm Q。算法终止。
若没有 WorkOpened，则回到 A1 的入口判定。
▷ 重复开立不是再 commission，也不是无任期补任。理由：再 commission 只走 K。无任期不另开一任只走 Q。本算法只开还没有的 work。
# 能证伪：同一 workId 第二次进入本算法仍写出第二笔 WorkOpened。

A2. [再查长度]
若 sequence 是 S4，则确认次数是 2，后继由系统派发，Delivered 不 spawn。
若 sequence 是 S1、S2、S3，则确认次数是 1，后继不派发，只有交付授权已置真时 Delivered 才 spawn。
若 sequence 长度不是 1，也不是 3，则拒绝开立。零写。算法终止。
若 sequence 被写成 S1、S2、S3、S4，则拒绝开立。零写。算法终止。
▷ 确认次数、派发与 spawn 不写入载荷。理由：三者由 sequence 导出，另存字段会和序列打架。
# 能证伪：开立载荷里出现确认次数、自动派发或 spawn 字段。

A3. [组装载荷]
权威初值 ← 对固定标记 authority-initial-v1 与 workId 做确定性摘要。
不把物理用户消息标识、消息 id、root、sequence 或空串放进这一摘要。
不是 0。不是空串。同一 workId 再算，必须得到同一个初值。
WorkOpened 载荷 ← workId、root、sequence、权威初值。
不写入消息 id。不写入 snapshotId。不写入证书。
不并进前缀重锚。
若实现时发现权威比较与前缀重锚是同一判定且同一调用方，则停止。不删本字段。不改词表。退回规划。
这一初值不是工具参数。开立之后不再有「没有初值」的活分支。
期望推进值只在后来的 deliver 开始时，从这同一个计数读出。
▷ 初值有唯一构造。理由：执行者不另选 0、空串或物理用户消息标识。

A4. [首任]
incumbencyId ← 新标识。
起始投影 ← sequence 的第一个元素。
若开立这一刻没有已观察到的 XTrace 头序号，则拒绝。零写。不追加 WorkOpened。不追加 IncumbencyOpened。不写空游标。不加一。不加十。算法终止。
openingCursor ← 开立这一刻已经观察到的 XTrace 头序号。不加一。不加十。
IncumbencyOpened 载荷 ← workId、incumbencyId、起始投影、openingCursor。
不写入 snapshotId。
▷ 缺头序号与 E7 同一口径。理由：游标是后任剥除的边界。没有观察到的序号，就没有可写的边界。Algorithm Q 不写游标。

A5. [一次追加]
同一笔追加 WorkOpened 与 IncumbencyOpened。
落地形状在第 11 节开头：同一次追加口调用，按步骤次序交出多笔信封，宿主落成连续多行。本步不另写一套。
若返回 AppendDurable，则两笔都已落地。返回已开立。不在这一笔里绑定 DevOps。不在这一笔里写 Reviewed。算法终止。
若返回 AppendNotAttempted，则两笔都未提交。返回未开立。可以再发。算法终止。
若返回 CommitUnknown，则既不报告已开立，也不报告未开立。不补另一笔。不截断。不另开一任。下一次打开才分类。算法终止。
否则拒绝。不把未分类的返回收成已开立。算法终止。
▷ 绑定不搬到开任。理由：S4 的绑定触发仍是 resume 与 horizon。

### Algorithm B 绑定 DevOps

输入：已开立的 workId、devopsId、target，以及触发动作。
输出：AppendDurable 时已绑定；AppendNotAttempted 时返回未绑定；CommitUnknown 时既不报告已绑定也不报告未绑定。写前拒绝则零写。

B1. [门]
若 work 尚未 WorkOpened，则拒绝。零写。算法终止。
不从开任调用本算法。
若 sequence 是 S4：
若触发不是 resume，也不是 horizon，则拒绝。零写。算法终止。
ask 不是本支的触发。
若 sequence 是 S1、S2、S3：
若触发不是 ask 绑定，则拒绝。零写。算法终止。
resume 与 horizon 不是本支的触发。
若 sequence 既不是 S4，也不是 S1、S2、S3，则拒绝。零写。算法终止。
▷ 两段不共用同一个触发名。理由：S4 的绑定仍由 resume 与 horizon 留下。一段式的绑定由 ask 留下。开任都不写绑定。

B2. [追加]
若该 work 已有 DevOpsBound，则返回原状态。不写第二笔。不在本算法里更换 target。算法终止。
否则载荷 ← workId、devopsId、target。
追加一笔 DevOpsBound。
若返回 AppendDurable，则已绑定。算法终止。
若返回 AppendNotAttempted，则返回未绑定。可以再发。算法终止。
若返回 CommitUnknown，则既不报告已绑定，也不报告未绑定。不补另一笔。不截断。下一次打开才分类。算法终止。
否则拒绝。不把未分类的返回收成已绑定。算法终止。
# 崩溃后不得新造另一个绑定。

### Algorithm C 追加评审并投影

输入：incumbencyId、outcome、当前折叠视图。outcome 只许是 Promoted 或 Stayed。
输出：AppendDurable 时给出新投影；AppendNotAttempted 时返回未评审；CommitUnknown 时既不报告已评审也不报告未评审。写前拒绝则零写。

C1. [不变量的唯一正文]
下一步走向不得写入长期字段、枚举、record 或跨模块接口。
不得构造业务 AST 再解释执行。
不得用 Command/Reply 总线、continuation 状态树或调用序列回放器调度本流程。
Stage、Phase、Lease、Owner、Generation 不得当流程程序计数器或业务状态机的名字。
▷ 阶段只是 WorkOpened.sequence 加上已落盘 Reviewed 折出来的投影。理由：这一次调用走到哪，由调用栈承载。投影不是下一步指令。
# 能证伪：下一阶段被存成可重放字段，或 deliver 按阶段枚举跳转。观察到任一条，本节作废。
其他算法不另写这份禁令。

C2. [任期门]
若视图里没有这个 incumbencyId 的活跃任期，则拒绝。零写。算法终止。
若这一任已经有 Reviewed，则拒绝。零写。算法终止。
▷ 每任恰好一次。理由：第二评审是自证，不是修复。

C3. [载荷]
若 outcome 既不是 Promoted，也不是 Stayed，则拒绝。零写。不追加 Reviewed。算法终止。
stage ← 视图里这一任的当前投影。
追加 Reviewed。载荷只含 incumbencyId、stage、outcome。
不写入 findings 对。不写入 snapshotId。不写入证书。
Promoted 表示 findings 为空。Stayed 表示 findings 非空。
对列表只留在本次调用的返回值里，不追加成事件。
若返回 AppendDurable，则转 C4。
若返回 AppendNotAttempted，则投影不变，授权位不变。返回未评审。可以再发。算法终止。
若返回 CommitUnknown，则既不报告已评审，也不报告未评审。不补另一笔。不截断。下一次打开才分类。算法终止。
否则拒绝。零写。不把未分类的返回收成已评。算法终止。

C4. [非终元素]
若不是从 C3 的 AppendDurable 转入，则拒绝。零写。不改投影。算法终止。
设 i 为 stage 在 sequence 里的位置。
若 outcome 是 Stayed，则投影留在 stage。交付授权保持假。返回已评。算法终止。
若 outcome 是 Promoted，且 i 后面还有元素，则投影 ← sequence 的下一个元素。对象仍是这一任。交付授权保持假。返回已评。算法终止。
若 outcome 是 Promoted，且 i 已是 sequence 最后一元，则转 C5。
▷ 这是折叠投影，不是程序计数器。理由：换的是已落盘事实的读法，不是保存的下一步。

C5. [终元素]
若 outcome 是 Promoted，且 i 已是 sequence 最后一元：
若 sequence 是 S1、S2、S3，则投影留在该元素。交付授权 ← 真。返回已评。算法终止。
若 sequence 是 S4，则投影留在 S4。交付授权保持假。只记下这一任已评。返回已评。算法终止。
否则拒绝。不进入 Algorithm D。算法终止。
▷ S4 不靠评审置授权。理由：序列长度是 1，结束靠第二次 deliver。
# 能证伪：S4 的 Promoted 把交付授权置真。
# 能证伪：S1、S2 或 S3 的终元素 Promoted 未把交付授权置真；C4 在转入 C5 之前拒绝终元素 Promoted。

### Algorithm D 确认次数为 1 的 deliver

输入：workId、本次 Host 上下文的 ProviderRunId、本次 Host 上下文的 ToolCallId、本次 deliver 的 XTrace 头序号（可能没有）、当前折叠视图、活跃 blocker 集合。
workId 与一段式写入用同一来源，现行是 ctx.SessionId。
头序号缺失不在入口发明。拒绝在 D7 与 D8，写游标之前。
ProviderRunId 与 ToolCallId 不是工具参数。活跃任期消失之后，这对围栏仍在本次上下文里。
输出：拒绝、RetirementBlocked、持权 Delivered，或无权 Relayed。AppendDurable 才算写出的信封已落地。CommitUnknown 不报告已落地，也不报告未落地。本算法不写 ConfirmationCommitted。

D1. [入口]
调用方只在 sequence 是 S1、S2、S3 时进入本算法。
若 sequence 不是这三段，则拒绝。零写。不调度另一套过程。算法终止。
更早一层不得因没有活跃任期拦掉本算法。重放判定在无任期拒绝之前。
入口先读 work 计数。读不到，则拒绝。零写。不进入 D1.1。不进入 D2。不写退休。算法终止。
读到了，这一读数记在本次调用里。D1.1、D3、D7、D8 只用这一读数。不从活跃任期再读一份。

D1.1. [重放]
更早一层不得先返回 no active incumbency 失败字符串。
fence ← 本次 Host 上下文的 ProviderRunId 与 ToolCallId。
workId 与写入侧用同一来源。不从活跃任期读取。
若 fence 缺一，则重放不命中。不另造 id。转入 D2。
在折叠里按 workId 加 fence 查找已落盘退休。载荷上的 ProviderRunId 与 ToolCallId 必须与 fence 相同。
若没查到，则重放不命中。不另造 id。不计算 retirementId。不比较闭集。转入 D2。
若查到，则 outcome ← 那笔已写下的结果名。
retirementId ← 对固定标记 oneshot-retirement-v1 与本次写入字段做确定性摘要。
字段次序固定为 workId、ProviderRunId、ToolCallId、outcome。
摘要再加入入口读出的期望推进值。开立必有初值，这一项不省略。
不把 retirementCursor、incumbencyId、digest、path、execSessionId、snapshotId、证书或本次 XTrace 头序号放进摘要输入。
同一组字段必须得到同一个 retirementId。这与 D7、D8 是同一个公式。
若算出的 id 与那笔已写下的 id 不同，则拒绝。零写。返回 RetirementReplayConflict。算法终止。
比较字段是闭集。不比闭集以外的字段。
outcome 从已落盘退休读。必须等于这次重算 id 时用的结果名。
期望推进值从已落盘退休读。必须等于这次入口读到的 work 计数。不从活跃任期读。
ProviderRunId 与 ToolCallId 从本次 Host 上下文读。必须与已落盘退休相同。
若那笔结果是 Delivered：path 由 workId 导出，必须与已落盘载荷的 path 相同。execSessionId 按 workId 问宿主。问到一台，且标识与已落盘载荷的 execSessionId 相同，才算相同。问不到，问到零台，问到两台及以上，或标识不同，都算不同。
若那笔结果是 Relayed：不比 path。不比 execSessionId。无权载荷没有这两项。
不比 retirementCursor。不比本次 XTrace 头序号。不比 incumbencyId。不比 digest。不读底稿字节。
闭集每一项都相同，则返回原状态。零写。不启动第二台。算法终止。
闭集有一项不同，则拒绝。零写。返回 RetirementReplayConflict。算法终止。
否则拒绝。零写。不转入 D2。算法终止。查到之后不得再走没查到。
▷ 重放先于无任期拒绝。理由：持权 Delivered 成功之后，活跃任期已经没了。现行入口会先返回失败字符串。围栏还在，任期不在。
# 能证伪：无活跃任期时，同一围栏重入返回 no active incumbency；身份摘要含有 XTrace 头序号。

D2. [无任期]
若重放未命中，且视图没有活跃任期，则拒绝。零写。不写退休。不写 RetirementBlocked。算法终止。
不在这一步计算 retirementId。
▷ 无任期不是重放，只在重放判定未命中之后。理由：没有活跃任期，就没有这次退休可写。不引用尚未算出的 id。

D3. [期望推进]
开立必有权威初值。不再留「开立没有初值」的活分支。
若见到没有初值的开立，则拒绝。零写。不转 D4。算法终止。
期望推进值不是工具参数，也不是第二个写入者。
入口读数已在本算法开头记下。本步不再做第一次读取。
若读不到计数，则拒绝。零写。不写 IncumbencyRetired。不写 RetirementBlocked。不转 D4。算法终止。
转入 D4 之前再读一次同一计数。
若这一次与入口读数不符，则拒绝。零写。不写 IncumbencyRetired。不写 RetirementBlocked。不转 D4。算法终止。
不符表示入口之后、追加之前，计数已经变了。
若相符，则转 D4。
▷ 声明从同一计数读出。理由：调用方不另给一个期望值。另给就会变成第二个写入者。
# 能证伪：相符时被拒绝或不转 D4；开立有初值但写出时没带上这一声明，仍继续写退休。

D4. [阻塞]
若活跃 blocker 为空，则转入 D5。
blocker 只计当前任期直接或递归拥有的 live child、background job、PTY 或 process、active tool、side-effect 或 execution lease、同步 descendant provider work，以及尚未观察到 terminal 的 cancel 或 join。
Digest 标识这组 blocker。不是工作区快照。不写入 snapshotId。
按 workId 问宿主已经留下的执行会话。
若问不到，则拒绝。零写。不写 RetirementBlocked。不启动第二台。不撤销看不见的会话。不报告已交付。算法终止。
若返回一台，且该台记下的 workId 与本次 work 相同，则转 D4.1。
若返回两台及以上，则拒绝。零写。不按到达先后挑一台。不撤销。算法终止。
若返回零台，则转 D4.2。
否则拒绝。零写。不写 RetirementBlocked。算法终止。

D4.1. [已有一台]
拒绝。零写。不写 RetirementBlocked。不启动第二台。不撤销这一台。不报告已交付。
若交付授权已置真，则清理之后从本算法入口重跑，由 D6 认领该台。不得把 D6 或 D7 当成恢复地址。
若交付授权未置真，则不认领、不启动、不撤销。
▷ 认领只在授权已置真之后。理由：授权未置真不启动执行会话。D6 不在这条路上。
算法终止。

D4.2. [零台]
追加 RetirementBlocked。载荷是 incumbencyId 与 blockerDigest。
不写 IncumbencyRetired。不 spawn。
若返回 AppendNotAttempted，则调用方看到未阻塞，可以再发。不得返回已退休。算法终止。
若返回 CommitUnknown，则既不报告已阻塞，也不报告未阻塞。不补另一笔。不截断。不得返回已退休。下一次打开才分类。算法终止。
若返回 AppendDurable，则返回未退休。清理之后可以再调用本算法。算法终止。
否则拒绝。不得返回已退休。不把未分类的返回收成已阻塞。算法终止。

D5. [持权底稿]
若交付授权未置真，则转 D8。
读这一件 work 的底稿。
若底稿缺失，或读到的内容为空，则拒绝。零写。不启动执行会话。算法终止。
digest ← 对底稿字节做确定性摘要。
path ← 这一件 work 的底稿路径。
摘要输入只有底稿字节。不把 path、执行会话标识、snapshotId 或证书放进摘要输入。
同一份底稿字节必须得到同一个 digest。算法不另造第二种摘要。
▷ 摘要与路径在启动之前定下。理由：它们是这次交付的载荷，不是启动成功之后才发明的名字。
# 能证伪：持权路径没有读底稿就启动；底稿缺失或为空仍启动。

D6. [已有会话]
按 workId 问宿主已经留下的执行会话。不凭工具返回值、不凭未落盘的猜测。
若问不到，则返回未交付。不启动另一台。不撤销看不见的会话。算法终止。
若返回零台，才启动执行会话。启动参数只有两个字符串：角色 manager，以及子会话初始 prompt。
初始 prompt 的首行逐字节是固定标记 spawn-from-authorized-delivery-v1，随后才是底稿正文。
这是 Algorithm A 的唯一 spawn 标记。不另造字段。不写进生命周期事件。不置 Manager 直接开立。不置 commission 新开。
若启动失败，则零写。返回未交付。算法终止。
若启动成功，则 execSessionId ← 这次启动返回的标识。转 D7。
若返回一台，且该台记下的 workId 与本次 work 相同，则 execSessionId ← 那一台的标识。不启动第二台。转 D7。
若返回一台，且该台记下的 workId 与本次 work 不同，则拒绝。零写。不启动。不撤销。算法终止。
若返回两台及以上，则拒绝。零写。不启动。不按到达先后挑一台。不撤销看不见的会话。算法终止。
若返回一台，且该台没有记下可比较的 workId，则拒绝。零写。不启动。不撤销。不转 D7。算法终止。
否则拒绝。零写。不启动。不转 D7。不把未分类的返回收成持权写入。算法终止。
▷ 启动不是「上次没记下」的替代。理由：上次已经启动、或 CommitUnknown 尚未分类时，会话可能已经在外面。再启动一台，同一 work 会有两台执行会话。按到达先后挑一台，等于用钟表裁事实。
# 能证伪：上次已经启动，或 CommitUnknown 尚未分类，再次进入本算法又启动了第二台；核对返回两台时按到达先后留下一台。

D7. [持权写入]
若本次没有已观察到的 XTrace 头序号，则拒绝。零写。不追加 IncumbencyRetired。不开后任。不写 RetirementBlocked。算法终止。
▷ 缺头序号与 A4、E7 同一口径。理由：游标是后任剥除的边界。没有观察到的序号，就没有可写的边界。
# 能证伪：一段式 deliver 在缺头序号时仍写出 retirementCursor，或写成 Relayed 或 Delivered。
outcome ← Delivered。
retirementCursor ← 本次已观察到的 XTrace 头序号。
retirementId ← 对固定标记 oneshot-retirement-v1 与本次写入字段做确定性摘要。
字段次序固定为 workId、ProviderRunId、ToolCallId、outcome。
摘要再加入入口读出的期望推进值。开立必有初值，这一项不省略。
不把 retirementCursor、incumbencyId、digest、path、execSessionId、snapshotId、证书或本次 XTrace 头序号放进摘要输入。
同一组字段必须得到同一个 retirementId。这与 D1.1 是同一个公式。不在重放时另造一个 id。
写出时必须带上入口读出的期望推进值。没带上则拒绝。零写。不追加。算法终止。
然后只追加 IncumbencyRetired。
载荷 ← retirementId、incumbencyId、outcome、ProviderRunId、ToolCallId、execSessionId、digest、path、retirementCursor。
载荷再含入口读出的期望推进值。开立必有初值，这一项不省略。
不写 ConfirmationCommitted。不另写一笔交付事件。不启动第二台。不撤销这一台。
若返回 AppendNotAttempted，则不得向调用方报告已交付。不撤销这一台。不把这次启动当成没发生。下一次调用从本算法入口重跑。不得把 D6 或 D7 当成恢复地址。算法终止。
若返回 CommitUnknown，则既不报告已交付，也不报告未交付。不撤销这一台。不启动第二台。不补另一笔。不截断。下一次打开才分类。算法终止。
若返回 AppendDurable，则返回 Delivered。算法终止。
否则拒绝。不得报告已交付。不启动第二台。不撤销这一台。算法终止。
▷ 先启动，后写。理由：先写再启动，启动失败会留下没有执行会话的 Delivered。
▷ 身份只从留下的字段来。理由：digest、path 与执行会话标识是这次交付的载荷，不是身份的来源。

D8. [无权]
若交付授权已置真，则拒绝。零写。不写退休。不另开一任。算法终止。
若交付授权未置真，则按下列各句写完这一支。
若本次没有已观察到的 XTrace 头序号，则拒绝。零写。不追加 IncumbencyRetired。不开后任。不写 RetirementBlocked。算法终止。
▷ 缺头序号与 A4、E7 同一口径。理由：游标是后任剥除的边界。没有观察到的序号，就没有可写的边界。
# 能证伪：一段式 deliver 在缺头序号时仍写出 retirementCursor，或写成 Relayed 或 Delivered。
outcome ← Relayed。
retirementCursor ← 本次已观察到的 XTrace 头序号。
retirementId ← 对固定标记 oneshot-retirement-v1 与本次写入字段做确定性摘要。
字段次序固定为 workId、ProviderRunId、ToolCallId、outcome。
摘要再加入入口读出的期望推进值。开立必有初值，这一项不省略。
不把 retirementCursor、incumbencyId、digest、path、execSessionId、snapshotId、证书或本次 XTrace 头序号放进摘要输入。
同一组字段必须得到同一个 retirementId。这与 D1.1 是同一个公式。不在重放时另造一个 id。
写出时必须带上入口读出的期望推进值。没带上则拒绝。零写。不追加。算法终止。
然后同一笔追加 IncumbencyRetired 与下一任 IncumbencyOpened。
落地形状在第 11 节开头：同一次追加口调用，按步骤次序交出多笔信封，宿主落成连续多行。本步不另写一套。
退休载荷 ← retirementId、incumbencyId、outcome、ProviderRunId、ToolCallId、retirementCursor。
退休载荷再含入口读出的期望推进值。开立必有初值，这一项不省略。
下一任 openingCursor ← 同一个 retirementCursor。不加一。不加十。
下一任 incumbencyId 由这一笔 retirementId 派生。不拷贝消息。
work 计数不打回初值。不另起一份。
下一任起始投影 ← 退休当时的折叠投影。
这是复制已有事实，不是按阶段选出的分支。
不因没有评审拒绝。不因不是终元素拒绝。
不派发后继提示。等待下一次请求。
若返回 AppendDurable，则退休与开任都已落地。返回 Relayed。算法终止。
若返回 AppendNotAttempted，则两笔都未提交。返回未退休。可以再发。算法终止。
若返回 CommitUnknown，则既不报告已退休，也不报告未退休。不补开任。不截断。不另开一任。下一次打开才分类。算法终止。
否则拒绝。不把未分类的返回收成 Relayed。算法终止。
▷ 无评审放行。理由：这段的写前评审只管改底稿。另一段的无评审拒绝不套到这里。
# 能证伪：无评审的本算法被拒绝；S1 的 Promoted 落盘后，下一次 deliver 被拒绝或写成 Delivered。

D9. [重放已前移]
重放判定只在 D1.1。本步不再执行。不得把本步当成第二套重放。算法终止。

### Algorithm E 确认次数为 2 的 deliver

输入：workId、本次 Host 上下文的 ProviderRunId、本次 Host 上下文的 ToolCallId、本次调用已观察到的 XTrace 头序号、当前折叠视图、活跃 blocker 集合。
workId 与写入侧用同一来源。活跃任期消失之后，围栏仍在本次上下文里。incumbencyId 只在写入新退休时从当前活跃任期读，不进重放身份。
输出：拒绝、已完成未退休、确认已落地、幂等确认、RetirementConfirmationReplayConflict、RetirementReplayConflict、RetirementBlocked、Delivered 或 Relayed。AppendDurable 才算写出的信封已落地。CommitUnknown 不报告已落地，也不报告未落地。

E1. [入口]
调用方只在 sequence 是 S4 时进入本算法。
若 sequence 不是 S4，则拒绝。零写。算法终止。
交付授权不是本算法的锁。不读证书。不读工具返回值。
入口先读 work 计数。读不到，则拒绝。零写。不进入 E1.1。不进入 E2。不写退休。算法终止。
读到了，这一读数记在本次调用里。E1.1、E6、E8、E10、E11 只用这一读数。不从活跃任期再读一份。

E1.1. [重放]
更早一层不得先返回 no active incumbency 失败字符串。
fence ← 本次 Host 上下文的 ProviderRunId 与 ToolCallId。
workId 与写入侧用同一来源。不从活跃任期读取。
若 fence 缺一，则重放不命中。不另造 id。转入 E2。
在折叠里按 workId 加 fence 查找已落盘退休。S4 载荷上的 ProjectionCut 必须与 fence 相同。
不比较一个不存在的 retirementCursor 字段。
若没查到，则重放不命中。不另造 id。不计算 retirementId。不比较闭集。转入 E2。
若查到，则 outcome ← 那笔已写下的结果名。
这个 outcome 只许是 Relayed 或 Delivered。不得用评审名重算。
retirementId ← 对固定标记 retirement-v1 与本次写入字段做确定性摘要。
字段次序固定为 workId、ProviderRunId、ToolCallId、outcome。
摘要再加入入口读出的期望推进值。开立必有初值，这一项不省略。
不把 incumbencyId、ProjectionCut、snapshotId、证书、digest、path、execSessionId 或本次 XTrace 头序号放进摘要输入。
同一组字段必须得到同一个 retirementId。这与 E8 是同一个公式。
若算出的 id 与那笔已写下的 id 不同，则拒绝。零写。返回 RetirementReplayConflict。算法终止。
比较字段是闭集。不比闭集以外的字段。
outcome 从已落盘退休读。必须等于这次重算 id 时用的结果名。
期望推进值从已落盘退休读。必须等于这次入口读到的 work 计数。不从活跃任期读。
ProjectionCut 从已落盘退休读。必须与本次 fence 相同。
不比 incumbencyId。不比本次 XTrace 头序号。S4 载荷没有名为 retirementCursor 的字段，不比它。不比 digest。不读底稿字节。
闭集每一项都相同，则返回原状态。零写。算法终止。
闭集有一项不同，则拒绝。零写。返回 RetirementReplayConflict。算法终止。
否则拒绝。零写。不转入 E2。算法终止。查到之后不得再走没查到。
▷ 重放先于无任期与无评审。理由：Delivered 成功后没有活跃任期。Relayed 成功后新任还没有评审。现行入口会先绑到后任，或先返回失败字符串。围栏还在。
# 能证伪：无活跃任期时，同一围栏重入返回 no active incumbency；身份摘要含有 XTrace 头序号。

E2. [无任期]
若重放未命中，且视图没有活跃任期，则拒绝。零写。不写确认。不退休。算法终止。
不在这一步计算 retirementId。

E3. [无评审]
若这一任没有 Reviewed，且已有确认，则拒绝。零写。不返回已完成未退休。不返回错误字符串。不写 ConfirmationCommitted。不退休。算法终止。
▷ 确认不得先于评审落地。理由：返回已完成未退休会把非法确认藏起来。
若这一任没有 Reviewed，且无确认，则返回已完成的未退休结果。
finished ← 假。
assessment_required ← 真。
零写。不返回错误字符串。不写 ConfirmationCommitted。不退休。算法终止。
▷ 无评审不得落成确认。理由：第一次确认不是代替评审。无确认才走这一句。

E4. [第一次]
若已有评审，且已有确认，则不构造。不拒绝。不写确认。不退休。转 E5。
若已有评审，且尚无确认，则构造 ConfirmationCommitted。载荷只含 incumbencyId、providerRunId、toolCallId。不写 retirementCursor。不写 ProjectionCut。不读交付授权。不区分 Promoted 与 Stayed。不写 IncumbencyRetired。
若已有评审，且尚无确认，且构造失败，则零写。调用方看到未确认。可以再发。不得返回已确认。算法终止。
若已有评审，且尚无确认，且返回 AppendDurable，则返回已确认。不结束。算法终止。
若已有评审，且尚无确认，且返回 AppendNotAttempted，则调用方看到未确认。可以再发。不得返回已确认。算法终止。
若已有评审，且尚无确认，且返回 CommitUnknown，则既不报告已确认，也不报告未确认。不补另一笔。不截断。不得返回已确认。下一次打开才分类。算法终止。
若已有评审，且尚无确认，且返回不是以上三支，则拒绝。不把未分类的返回收成已确认。算法终止。
若没有评审，则拒绝。零写。不转 E5。算法终止。无评审的正路只在 E3。
▷ 第一次只落确认。理由：两段是风险偏好。收成一段，这段就没了。
# 能证伪：已评 Stayed 的第一次调用因缺交付授权被拒；无评审却写下了 ConfirmationCommitted；无评审且无确认时返回错误字符串；无评审且已有确认时返回已完成未退休。

E5. [同一调用]
若已有确认，且 toolCallId 相同，且 providerRunId 相同，则返回原确认。不退休。不写第二笔确认。算法终止。
若已有确认，且 toolCallId 相同，且 providerRunId 不同，则返回 RetirementConfirmationReplayConflict。不退休。算法终止。
若已有确认，且本次 toolCallId 与已确认的 toolCallId 不同，则转 E6。
若已有确认，且本次 toolCallId 缺失，则拒绝。零写。不退休。不返回 RetirementConfirmationReplayConflict。不转 E6。算法终止。
若已有确认，且本次 toolCallId 与已确认的 toolCallId 相同，且 providerRunId 缺失，则拒绝。零写。不退休。不返回 RetirementConfirmationReplayConflict。不转 E6。算法终止。
若没有确认，则拒绝。零写。不转 E6。算法终止。确认只在 E4 写下。没有确认不是第二次写入的入口。
▷ 缺一半围栏没有第二身份。理由：这不是重放冲突，也不是第二次写入的入口。
不在本步写退休。不写确认。不得靠步骤次序掉进 E6。

E6. [期望推进]
本步不写 E8。写入只从 E7 进入 E8。
开立必有权威初值。不再留「开立没有初值」的活分支。
若见到没有初值的开立，则拒绝。零写。不转 E7。不转 E8。算法终止。
期望推进值不是工具参数，也不是第二个写入者。
入口读数已在本算法开头记下。本步不再做第一次读取。
若读不到计数，则拒绝。零写。不写 IncumbencyRetired。不写 RetirementBlocked。不转 E7。不转 E8。算法终止。
转入 E7 之前再读一次同一计数。
若这一次与入口读数不符，则拒绝。零写。不写 IncumbencyRetired。不写 RetirementBlocked。不转 E7。不转 E8。算法终止。
不符表示入口之后、追加之前，计数已经变了。
若相符，则转 E7。
▷ 声明从同一计数读出。理由：与 Algorithm D 的 D3 同一来源。两段式的后继是 E7，不是 E8。
# 能证伪：相符时仍写入 E8；开立有初值但写出时没带上这一声明，仍继续写退休。

E7. [来源]
第二次写入只有一个谓词：本次 toolCallId 与已确认的 toolCallId 不同，且 providerRunId 在，且 toolCallId 在，且本次调用已有 XTrace 头序号。
若谓词不成立，则拒绝。零写。不写 RetirementBlocked。不写 IncumbencyRetired。不转 E8。算法终止。
不得靠步骤次序掉进 E8。
若谓词成立，则这是第二次 deliver 的唯一入口。不得把这一支收成拒绝。
ProjectionCut ← providerRunId 与 toolCallId。
后任开任游标的来源 ← 本次调用已观察到的 XTrace 头序号。
这两个来源不是同一个字段。
不从现行 Relay 摘要读取名为 retirementCursor 的字段。那边没有这个字段。
同一 toolCallId 的重放留在 E5。本步不把它们收成第二次写入。
▷ cut 管请求身份，游标管后任剥除。理由：把游标写成 Relay 已有字段，是把一段式的名字安到没有该字段的摘要上。
▷ 谓词不成立不写。理由：写入去路只有转 E8 这一条。
转 E8。

E8. [第二次写入]
若活跃 blocker 非空，则追加 RetirementBlocked。载荷与 Algorithm D 的 D4.2 相同：incumbencyId 与 blockerDigest。不把 blocker 集合本身写进事件。不写 IncumbencyRetired。
若活跃 blocker 非空，且返回 AppendDurable，则返回未退休。清理之后可以再调用。算法终止。
若活跃 blocker 非空，且返回 AppendNotAttempted，则调用方看到未阻塞。可以再发。不返回已退休。算法终止。
若活跃 blocker 非空，且返回 CommitUnknown，则既不报告已阻塞，也不报告未阻塞。不补另一笔。不截断。不返回已退休。下一次打开才分类。算法终止。
若活跃 blocker 非空，且返回不是这三支，则拒绝。不把未分类的返回收成已阻塞。算法终止。
若活跃 blocker 为空，则只读折叠上的评审 outcome 与确认位。
若读不到其中一样，则拒绝写入。不猜测。不恢复证书。算法终止。
workId 与 E1.1 用同一来源。不从已消失的任期读取。
若折叠上的 workId 与这个来源不同，则拒绝。零写。算法终止。
reviewOutcome ← 折叠上的评审 outcome。
若 reviewOutcome 既不是 Promoted，也不是 Stayed，则拒绝。零写。算法终止。
若 reviewOutcome 是 Promoted，则 outcome ← Delivered。
若 reviewOutcome 是 Stayed，则 outcome ← Relayed。
retirementId ← 对固定标记 retirement-v1 与本次写入字段做确定性摘要。
字段次序固定为 workId、ProviderRunId、ToolCallId、outcome。
这里的 outcome 只许是结果名 Relayed 或 Delivered。不许是评审名 Promoted 或 Stayed。
摘要再加入入口读出的期望推进值。开立必有初值，这一项不省略。
不把 incumbencyId、ProjectionCut、snapshotId、证书、digest、path、execSessionId 或本次 XTrace 头序号放进摘要输入。
同一组字段必须得到同一个 retirementId。这与 E1.1 是同一个公式。不在重放时另造一个 id。
▷ 身份只从围栏和结果名来。理由：活跃任期会消失或换成后任。头序号重捕不保证相同。snapshot 与证书已经退役。评审名不是结果名。先定结果名，再算身份。
若 reviewOutcome 是 Promoted，则转 E10。
若 reviewOutcome 是 Stayed，则转 E11。

E9. [重放已前移]
重放判定只在 E1.1。本步不再执行。不得把本步当成第二套重放。
若执行到达本步，则拒绝。零写。不继续写。不进入 E10。算法终止。

E10. [Promoted]
写出时必须带上入口读出的期望推进值。没带上则拒绝。零写。不追加。算法终止。
若 reviewOutcome 是 Promoted，且已确认，则追加 IncumbencyRetired。
载荷 ← retirementId、incumbencyId、outcome、ProjectionCut。
outcome 已是 Delivered。不得在写入之后改写 outcome，也不得用评审名重算 retirementId。
载荷再含入口读出的期望推进值。开立必有初值，这一项不省略。
不写 snapshotId。不写证书。不填 digest。不填 path。不填 execSessionId。
不写名为 retirementCursor 的字段。
不 spawn。不派发。
若返回 AppendDurable，则返回 Delivered。算法终止。
若返回 AppendNotAttempted，则不得报告已结束。可以再发。算法终止。
若返回 CommitUnknown，则既不报告已结束，也不报告未结束。不补另一笔。不截断。下一次打开才分类。算法终止。
否则拒绝。不把未分类的返回收成 Delivered。算法终止。

E11. [Stayed]
写出时必须带上入口读出的期望推进值。没带上则拒绝。零写。不追加。算法终止。
若 reviewOutcome 是 Stayed，且已确认，则同一笔追加 IncumbencyRetired 与下一任 IncumbencyOpened。
落地形状在第 11 节开头：同一次追加口调用，按步骤次序交出多笔信封，宿主落成连续多行。本步不另写一套。
退休载荷的字段与 E10 相同。outcome 已是 Relayed。不得在写入之后改写 outcome，也不得用评审名重算 retirementId。
下一任 openingCursor ← E7 记下的 XTrace 头序号。不加一。不加十。
这不是从退休载荷读取名为 retirementCursor 的字段。
下一任起始投影 ← 退休当时的折叠投影。
下一任 incumbencyId 由这一笔 retirementId 派生。不拷贝消息。
work 计数不打回初值。不另起一份。
不 spawn。
若返回 AppendDurable，则退休与开任都已落地。返回 Relayed。转入 H8 的入口一。本步不在转入之前终止。
若返回 AppendNotAttempted，则两笔都未提交。不派发。不得报告已结束。可以再发。算法终止。
若返回 CommitUnknown，则既不报告已结束，也不报告未结束。不派发。不补开任。不截断。不另开一任。下一次打开才分类。算法终止。
否则拒绝。不派发。不把未分类的返回收成 Relayed。算法终止。
▷ 第二次的结果只从折叠读。理由：证书已退役，工具返回值不是事实。
# 能证伪：S4 退休载荷出现名为 retirementCursor 的字段；同一 retirementId 不同载荷仍写成第二笔；写入摘要用 Promoted 或 Stayed，重放用 Delivered 或 Relayed，同一围栏同一载荷重入返回 RetirementReplayConflict。

E12. [不要并门]
第二次调用不是再写一笔确认。
折叠上「已有确认但这对不完全相同就宽拒绝」不套到本支。
无任期与无评审只拦截折叠尚未出现该 retirementId 的调用。
重放在 E1.1，不得跳过。不得把无任期拒绝用在已经命中重放的调用上。
若执行到达本步，则不得掉进 Algorithm F。算法终止。

### Algorithm F 恢复位置

输入：已经折好的统一视图，以及底稿文件是否存在。
输出：PairIncomplete、拒绝、Conflict、DeliveredIdempotent、ActiveRebound 或 Nothing。不新造事件。不新造模块。
本节是恢复位置的唯一正文。第 9 节与第 10 节不另写格子。
三个谓词只在本节定义。别的算法说当前 Delivered、当前 Relayed、活跃任期，都跳到这里，不另写一套。

F0. [三个谓词]
从空视图起，按已落盘事件的次序改写。不读还没落盘的调用。
WorkOpened 加上首任 IncumbencyOpened：活跃任期成立。当前 Delivered 不成立。当前 Relayed 不成立。
无权 Relayed 同一笔里，退休与后任一起落地：当前 Relayed 成立，活跃任期改成后任。当前 Delivered 不成立。
持权 Delivered 落地，且没有后任：当前 Delivered 成立，活跃任期取消。
ConfirmationCommitted 只给当前活跃任期加上确认位。不取消活跃任期。不成立当前 Delivered。不成立当前 Relayed。
Reopened 落地：当前 Delivered 取消。同一任期恢复为活跃。那笔记得只留下作复制来源，不与活跃任期并存。当前 Relayed 不因这一笔成立。
RetirementBlocked 不改这三个谓词。
Reviewed、DevOpsBound、AdvanceCommitted 也不改这三个谓词。
▷ 成功重开之后不得同时成立当前 Delivered 与活跃任期。理由：J2 把同一任期恢复为活跃。并存只属于没重开的残页。
# 能证伪：Reopened 已落地，恢复仍把那笔历史 Delivered 读成当前 Delivered，并返回 Conflict。
一次调用写出的连续多行，没有整批不可见的保证。下一次打开若文件不以换行结尾，读者整份返回 StorageInvalid。不进入本算法。不裁残行。不折被拒绝的前缀。
下一次打开若只折出必成对两笔中的一笔，则位置 ← PairIncomplete。不改写三个谓词。不补另一笔。不截断。不另开一任。不把它收成 Conflict、Nothing、DeliveredIdempotent 或 ActiveRebound。转 F7。
若一次已落地的追加，整次对不上上面任何一句改写，也不是这两处必成对的缺一笔，则位置 ← 拒绝。不改写三个谓词。不进入 F2 到 F5。不跳过这一次调用。不把它收成 PairIncomplete、Conflict、Nothing、DeliveredIdempotent 或 ActiveRebound。转 F7。
▷ 只见前行、不见后任，返回 PairIncomplete。理由：读者已经折出一笔完整行。这不是没恢复位置，也不是冲突。补后任会把未知写成成功。
F4.1 不可达。
▷ 对不上的返回拒绝。理由：这是已折出的完整行对不上改写。不是待修的恢复位置。不得用通配保持上一视图。不得补后任。
# 能证伪：只见 Relayed 退休、不见后任时，恢复返回 Conflict、Nothing、DeliveredIdempotent 或 ActiveRebound，或停着不返回 PairIncomplete。

F1. [输入边界]
不读还没落盘的工具调用。不读底稿正文。
不读 ConfirmationCommitted 去发明退休。
确认事件只影响视图里有没有活跃任期。它自己不是一个恢复位置。

F2. [并存]
只读 F0 的当前谓词。不把历史 Delivered 当成当前 Delivered。
若当前 Delivered 成立，且活跃任期成立，则位置 ← Conflict。不猜谁对。转 F7。
若当前 Delivered 成立，且活跃任期不成立，则转 F3。
不得在这一支转 F4。F4 的入口要求当前 Delivered 不成立。
若当前 Delivered 不成立，且活跃任期成立，则转 F4。
这一支含成功追加 Reopened 之后，也含只有历史 Delivered。
当前 Delivered 已取消，不得命中上一支 Conflict。不得把历史 Delivered 读成当前 Delivered。
若当前 Delivered 不成立，且活跃任期不成立，则转 F5。不转 F4.1。F4.1 不可达。理由：只见 Relayed、不见后任的追加已在 F0 返回 PairIncomplete，不改写三个谓词。这一格到不了折叠视图。
以上四支都不命中，则拒绝。不转 F3。不转 F4。不转 F5。算法终止。

F3. [有交付且没有活跃任期]
若当前 Delivered 成立，且活跃任期不成立，则进入本步。更早的 Relayed 不算冲突。不因同时有 Relayed 掉出表外。先按 sequence 检查该序列实际写入的字段。缺项先于幂等返回。
若 sequence 是 S1、S2、S3，且缺 outcome、名为 retirementCursor 的字段、期望推进值、execSessionId、摘要或路径中的任一项，则位置 ← Conflict。转 F7。
若 sequence 是 S1、S2、S3，且 outcome、名为 retirementCursor 的字段、期望推进值、execSessionId、摘要与路径都在，则位置 ← DeliveredIdempotent。转 F7。
若 sequence 是 S1、S2、S3，则位置 ← Conflict。转 F7。不得返回 DeliveredIdempotent。
若 sequence 是 S4，且缺 outcome，或缺 ProjectionCut，或缺期望推进值，则位置 ← Conflict。转 F7。
若 sequence 是 S4，且载荷出现名为 retirementCursor 的字段，则位置 ← Conflict。转 F7。
若 sequence 是 S4，则缺 digest、path 或 execSessionId 不是 Conflict。
若 sequence 是 S4，且 outcome、ProjectionCut 与期望推进值都在，且没有名为 retirementCursor 的字段，则位置 ← DeliveredIdempotent。转 F7。
若 sequence 是 S4，且以上三支都不命中，则位置 ← Conflict。不返回 DeliveredIdempotent。转 F7。
# 能证伪：缺期望推进值的已落盘 Delivered，恢复返回 DeliveredIdempotent。
若 sequence 缺失，或既不是 S1、S2、S3，也不是 S4，则位置 ← Conflict。不落到 F6。不另造输出。转 F7。
▷ 摘要和路径盖不住缺字段。理由：走到本步就终止。字段检查若留在后面，缺 retirementCursor 的已退休会当成幂等。
▷ S4 不填 digest、path、execSessionId。理由：S4 的 Delivered 不 spawn。

F4. [活跃且没有交付]
若当前 Delivered 不成立，且活跃任期成立，则位置 ← ActiveRebound。
成功追加 Reopened 之后必须落到本步。不得把位置改成 Conflict。
历史 Delivered 只是复制来源，不把本步改判 Conflict。
当前 Relayed 成立，位置仍是 ActiveRebound。不得掉出表外。不得因当前 Relayed 改判 Conflict。
已落盘确认且尚未退休，落在本步。不是已退休。也不是 Delivered。
底稿不存在，仍是 ActiveRebound。不改口成已交付。
S4 没有底稿，同样落在本步。
授权位按视图里已存的值读。S4 上的假是正常活跃，不是冲突。
若读不到授权位，则位置 ← Conflict。不补 true。不补 false。不新造第五种输出。转 F7。
若授权位可读，则位置保持 ActiveRebound。不写第二笔 DevOpsBound。不把确认位改成退休。不重发未知接收。转 F7。

F4.1. [不可达]
本步不再执行。不得把本步当成恢复位置。
只见 Relayed、不见后任的追加在 F0 返回 PairIncomplete。不改写三个谓词。不把它收成 Conflict。
因此「当前 Relayed 成立，当前 Delivered 不成立，且活跃任期不成立」构造不出来。
若执行到达本步，则停。不返回 Conflict。不返回 Nothing。不另开一任。算法终止。
▷ 不可达由 F0 证明。理由：没有后任的 Relayed 追加进不了折叠视图。

F5. [都没有]
若当前 Delivered 不成立，当前 Relayed 不成立，且活跃任期不成立，则位置 ← Nothing。转 F7。
否则拒绝。算法终止。只见 Relayed、不见后任已在 F0 返回 PairIncomplete，到不了本步。
不猜测阶段。不假定已交付。

F6. [缺项已并入]
字段检查只在 F3，返回 DeliveredIdempotent 之前。本步不再执行。不得把本步当成第二套检查。
# 能证伪：S4 成功收束因缺 digest 或 path 被判成 Conflict；已确认未退休被判成已退休；底稿不存在被改口成已交付；崩溃后新造绑定；读不到授权位时补 true；有 Delivered、无活跃任期、sequence 缺失或不是这两段序列时未返回 Conflict。

F7. [下一步]
位置只活在这一次调用里。不写入事件。不存成长期字段。
若位置是 PairIncomplete，则停。不补另一笔。不另开一任。不派发。算法终止。
若位置是拒绝，则停。不跳过这一次追加。不派发。不另开一任。算法终止。
若位置是 Conflict，则停。不猜谁对。不派发。不重开。不补 true。不新造绑定。算法终止。
若位置是 DeliveredIdempotent，则停在已交付。不派发。不 spawn。不另开一任。不启动第二台。不撤销已经问到的那一台。算法终止。
若位置是 Nothing，则停。不转 Algorithm A。不转 Algorithm Q。算法终止。
若位置是 ActiveRebound，且 sequence 是 S1、S2、S3，则等待下一次用户 prompt。不派发后继提示。不在恢复里绑定 DevOps。算法终止。
若位置是 ActiveRebound，且 sequence 是 S4，且当前 Relayed 成立，且后任尚未 Reviewed，则转入 H8 的入口二。不转 H2。不转 H4。不把后任送进 assess。本步不在转入之前终止。
若位置是 ActiveRebound，且 sequence 是 S4，且当前 Relayed 不成立，则转入 H2。不转 H8。本步不在转入之前终止。
若位置是 ActiveRebound，且 sequence 是 S4，且后任已经 Reviewed，则转入 H2。不转 H8。不发第二套后继提示。本步不在转入之前终止。
否则拒绝。不派发。算法终止。
▷ 六个位置各有下一步。理由：返回一个名字之后停住，执行者会自己发明下一步。
▷ 后任尚未评审不走 H4。理由：第一条可见指令必须是后继提示。assess 会把自动循环改成评审前只读。
# 能证伪：恢复停在位置名上，没有下一句；S4 后任尚未评审却转入 H4。

### Algorithm G 历史投影

输入：后继 OpeningCursor、provider 消息序列、上一任退休事实、上一任 sequence、上一任 LWR。
输出：后继可见的消息，以及注入的 LWR。无稳定身份则拒绝装配，零猜测。

G1. [剥除]
逐条看消息。
若没有稳定身份，则拒绝装配。不猜测。算法终止。
若该身份的首次观察序晚于 OpeningCursor，则留下。
否则剥除。
比较单位是 XTrace 观察序。不用退休工具调用的下标。

G2. [不合成]
不生成合成消息。不概括前任。不把剥除前的物理历史原样写回。

G3. [交接]
把上一任 LWR 注入后继交接材料。
复用已有的 previousTenureRecord 接线。不另写一份 materialize。
LWR 不是派发的控制输入。

G4. [cut]
若上一任 sequence 缺失，则拒绝装配。不猜。算法终止。
若上一任 sequence 是 S1、S2、S3，则不查找 ProjectionCut。过期请求只由 G1 的 openingCursor 与稳定身份比较承担。算法终止。
若上一任 sequence 是 S4，且退休载荷没有 ProjectionCut，则拒绝装配。不猜。算法终止。
若上一任 sequence 是 S4，且退休载荷已有 ProjectionCut，则 cut 只判定过期请求的身份。不当成新任的活跃请求。算法终止。
否则拒绝装配。不猜。不进入 Algorithm H。算法终止。

### Algorithm H 派发选择

输入：sequence，以及折叠视图里的任期、评审 outcome、确认位。
输出：零条或一条可见指令。确认次数为 2 且 Relayed 已落盘时，另发一条后继提示。

H1. [一段式]
若 sequence 不是 S1、S2、S3，也不是 S4，则拒绝。不选资源。算法终止。
若 sequence 是 S4，则转 H2。
S1、S2、S3 不走 H3 到 H7。
Relayed 之后不派发后继提示。
若本次没有要发出的工具调用，则这是空停。
不派发后继提示。不把沉默当失败。不写事件。等待下一次用户 prompt。
退休过程只按 Algorithm D。本算法不另写一把锁。
算法终止。
# 能证伪：S4 在空停句之后终止，且未转入 H2。

H2. [读什么]
若 sequence 是 S4，则只读折叠上的三样：有没有任期、outcome、确认位。
不读工具返回值。不读证书。不读交付授权。
可见指令互斥。只选出一条。不得把几支正文一起发出。
其余格子是拒绝，或已证明不可达。不用通配落下去。
写法是直接分支。不造状态表。不把下一步存成长期字段。

H3. [无任期]
若没有任期，则不选资源。不允许 deliver。算法终止。
若有任期，则不选资源。不在本步终止。转 H4。

H4. [无评审]
若当前 Relayed 成立，且这一任尚未 Reviewed，则拒绝。不选 assess。后任第一条指令只从 H8 来。算法终止。
若有任期，且无评审，且无确认，则选出 assess 这一条。正文只写当前这一次的独立只读评估要求。不写低分如何接责。不写满分如何退场。不写后继循环。不允许 deliver。算法终止。
若不命中以上两句，则不选 assess。不写评估正文。不在本步终止。转 H4.1。

H4.1. [有确认无评审]
若有任期，且无评审，且已有确认，则拒绝。不选资源。算法终止。
▷ 这一格拒绝。理由：确认不得先于评审落地。落到 assess 会把非法确认藏起来。
若不命中上一句，则不在本步终止。转 H5。

H5. [Stayed 未确认]
若 outcome 是 Stayed，且尚无确认，则选出 work 这一条。正文写修复义务。不写工具拒绝。允许第一次 deliver。那一次只写确认并返回，不收束。算法终止。
若已有确认，则不得命中本支。不写 work 正文。不在本步终止。转 H6。
若 outcome 不是 Stayed，则不写 work 正文。不在本步终止。转 H6。

H6. [Promoted 未确认]
若 outcome 是 Promoted，且尚无确认，则选出 finish 这一条。参数是尚无确认。正文必须写明：这一次调用只落下确认，不结束。不得写入「可以结束」或「证书已绑定」。本支不读交付授权。算法终止。
▷ 第一次确认不是退场。理由：结束是另一个 toolCallId 的另一次调用。
若不命中上一句，则不写本支正文。不在本步终止。转 H7。

H7. [已确认]
若已有确认，且未退休，且 outcome 是 Promoted 或 Stayed，则选出 finish 这一条。参数是已有确认。Stayed 且已确认只命中本支。不落回 work。本支允许第二次 deliver。收束不写在本选择里。收束只发生在 Algorithm E 的第二次入口。算法终止。
若不命中上一句，则不写 finish 正文。不在本步终止。转 H7.1。

H7.1. [残格]
若这一任已经退休，仍被读成当前任期，则拒绝。不选资源。不进入 H8。不发后继提示。算法终止。
若已有评审，且 outcome 既不是 Promoted，也不是 Stayed，则拒绝。不选资源。不进入 H8。算法终止。
▷ 这两格不在 H3 到 H7。理由：已退休不是当前任期。Delivered 之后没有活跃任期。Relayed 之后当前任期是后任。非法评审名不是 Promoted，也不是 Stayed。后继提示只从 Algorithm E 的 Relayed 成功进入 H8。
不重列 H3 到 H7。不写未命中则拒绝。
不得因为已有 Relayed 就发出后继提示。
若以上各格都不命中，则拒绝。不选资源。不发后继提示。算法终止。

H8. [后继提示]
两个入口，一套正文。
入口一：E11 在 AppendDurable 之后跳转。退休与后任已经落盘。本次调用还在。
入口二：F7 在 ActiveRebound、sequence 为 S4、当前 Relayed 成立、后任尚未 Reviewed 时转入。
若不是这两个入口，则拒绝。不选资源。不转 H4。算法终止。
按后任 incumbencyId 问宿主：这一任有没有已经留下的 provider 运行。
若问不到，则既不报告已发出，也不报告未发出。不发。不把「也许已经发出」收成不发。不把「也许没发出」收成再发一套。不转 H2。不转 H4。下一次打开仍按 F7 同一句判定。算法终止。
若返回一次及以上，则不发第二套。不转 H2。不转 H4。已有运行继续。算法终止。
若返回零次，且提示被改成拼接前任消息，则停止这条派发，回到只读退休事实。不得补一条消息摘要。不发后继提示。算法终止。
若返回零次，则发一次后继提示。参数只来自序号、退休 id、cut 的 provider run。不读消息。不读 LWR。不读 H3 到 H7 的正文当控制输入。不抓工作区快照。不把 snapshotId 写入开任载荷。发出一次之后，算法终止。
否则拒绝。零写。不发后继提示。算法终止。
▷ 问不到就停在未知。理由：提示发出没有生命周期事件。没有宿主观察，发出与否分不清。
▷ 不补摘要。理由：提示函数不读消息。补摘要是在修一个不存在的断裂。
# 能证伪：问不到仍发出后继提示；问到已有运行仍再发一套；后任尚未评审却转入 H4。

### Algorithm I 评审期两道门

输入：统一折叠视图，以及这次要执行的工具或要派的任务。
折叠视图里活跃任期的阻塞摘要有没有，从这张视图读。不另给一份现场列表。
输出：放行或拒绝。不新开事件。不插入 Algorithm D 或 Algorithm E 的步骤。

I0. [两次入口]
review、js-manager、deliver、原生 read、grep、glob、跨角色 js-engineer、跨角色 js-devops，只走 I1。
ask、resume、fork、horizon、join、向绑定的固定 DevOps 派任务，只走 I2。
一次调用只属于上面一份名单。命中两份，或两份都不属于，则拒绝。零副作用。算法终止。
属于 I1 的调用不落入 I2。属于 I2 的调用不先走 I1。
I1 每支的终止不得使 I2 不可达。
▷ 名单先于两门。理由：fork 既像工具又像派工。不先钉死，执行者会让它走两门。

I1. [执行门]
若本次是 deliver，且 sequence 是 S4，且有活跃任期，且尚未评审，且已有确认，则拒绝。零写。不进入 Algorithm D。不进入 Algorithm E。不返回已完成未退休。不放行写入。不放行只读评估。算法终止。
▷ 确认不得先于评审落地。理由：无条件放行会让后文拒绝变成死句。已完成未退休会把这一格藏起来。
若本次是 deliver，则放行。不写事件。不进入后面的读写门。调用方进入 Algorithm D 或 Algorithm E。一段式无评审仍进 D8。两段式无评审且无确认仍进 E3，零写，返回已完成未退休，不返回错误字符串。不得把 deliver 收成改底稿写入。不得收成 S4 的不放行写入。算法终止。
▷ 这一句必须写在本门。理由：I0 把工具执行只转到这里。I2 的不裁 deliver 到不了工具执行。上一句已经裁掉的那一格，不进本句。
# 能证伪：S4 无评审且无确认的 deliver 被本门当成写入拒绝；一段式无评审的 deliver 被改底稿门拒绝；S4 无评审且已有确认的 deliver 仍放行并进入 Algorithm E。
名字 js-manager 留在 Schema 里，固定可见。不删除。不改名。
一段式不读确认位。确认次数为 1，没有确认事件。
S4 的 Stayed 要读确认位，用来分开修复与收口。
提示词选择仍只在 Algorithm H。本门不选出资源正文。
不读证书。不读 findings 对。
写法是直接分支。不造状态表。不把下一步存成长期字段。
▷ 不单列退任冻结。不保留这一格。理由：没读到落点。条款名不是观察。不补函数名。第 9 节同一句。

若 sequence 是 S1、S2、S3：
若本任尚未 Reviewed，则放行读与只读调查。放行本次 review 的工具执行。review 只进入 Algorithm C。拒绝改底稿写入。这一句不得扩大成拒绝 review。算法终止。
# 能证伪：一段式尚未评审时，review 被改底稿门拒绝。
若本任已经 Reviewed，则放行修底稿写入。Stayed 的修复义务不拒绝写入。算法终止。
若 sequence 是 S1、S2、S3，且上面两支都不命中，则拒绝。零副作用。不放行读。不放行写入。不得继续读 S4 句。算法终止。
一段式的写门只看本任是否已 Reviewed。
原生 read、grep、glob 不因评审窗在这一段拒绝。
对 Manager 的跨角色 js-engineer、js-devops 不套到这一段。
▷ 本门禁的是执行，不是派工，也不是退休。理由：三件事不是同一把锁。

若 sequence 是 S4：
若没有活跃任期，则拒绝。零副作用。不放行只读评估。不把缺任期读成尚未评审。算法终止。
若尚未评审，且已有确认，则拒绝。不放行只读评估。不放行写入。拒绝对 Manager 的跨角色 js-engineer、js-devops。零副作用。算法终止。
deliver 已在本门开头裁掉。join 在 I2 裁。本句不再裁这两次入口。
▷ 本句只裁还能走进本句的非 deliver 工具。理由：deliver 在本门开头已经终止。join 不进 I1。
# 能证伪：有阻塞摘要、无评审、已有确认，非 deliver 工具仍放行。
若折叠视图里这一任已有阻塞摘要，则本门只放行 deliver。join 不进本门，只在 I2。拒绝原生 read、grep、glob。拒绝对 Manager 的跨角色 js-engineer、js-devops。不在本门写派工结论。不拒绝 deliver。算法终止。
▷ 谓词是已落盘的阻塞摘要。理由：现场列表非空是另一道门。摘要已在时，deliver 仍要能进 Algorithm E。本支不覆盖上一句已经拒绝的非法确认。
这不是现场列表判定。现场列表非空仍只由 Algorithm D 与 Algorithm E 写 RetirementBlocked。
未落盘阻塞事件时，不因现场列表非空走进本支。
若尚未评审，且无确认，则只放行两样：本次 review 的工具执行，以及 js-manager 的只读取。review 的唯一副作用是进入 Algorithm C。这不是改底稿。不是修复派工。不是向绑定 DevOps 派任务。js-manager 只读，不写事件。除此以外不放行写入。拒绝原生 read、grep、glob。拒绝对 Manager 的跨角色 js-engineer、js-devops。算法终止。
# 能证伪：S4 第一次 review 因为写事件被执行门拒绝，或 js-manager 在这一格被拒绝。
# 能证伪：有任期、无评审、已有确认，仍放行只读评估。
若已评，且 outcome 是 Stayed，且尚无确认，则本门不裁 fork，也不裁向绑定 DevOps 派任务。那两格只在 I2。本门不放行、也不拒绝这两次入口。拒绝原生 read、grep、glob。拒绝对 Manager 的跨角色 js-engineer、js-devops。不得把本支收成拒绝执行。算法终止。
▷ 修复义务的派工不在本门。理由：H5 选出 work。本门再写一句放行，就和 I2 变成两套结论。
若已评，且 outcome 是 Stayed，且已有确认，则只放行清理与收口。不开新工作。fork 与向绑定 DevOps 派任务只在 I2 拒绝。本门不另写一句。拒绝原生 read、grep、glob。拒绝对 Manager 的跨角色 js-engineer、js-devops。算法终止。
若 outcome 是 Promoted，且尚无确认，则放行第一次 deliver。该次只写 ConfirmationCommitted，不收束。放行清理与收口。不开新工作。拒绝原生 read、grep、glob。拒绝对 Manager 的跨角色 js-engineer、js-devops。算法终止。
若 outcome 是 Promoted，且已有确认，则放行第二次 deliver。收口走 Algorithm E。放行清理与收口。不开新工作。拒绝原生 read、grep、glob。拒绝对 Manager 的跨角色 js-engineer、js-devops。算法终止。
▷ 第一次 deliver 不是收束。理由：E4 只写确认。第二次才走 E10。
若已评，且 outcome 既不是 Promoted，也不是 Stayed，则拒绝。算法终止。
若 sequence 是 S4，且本分支以上各句都不命中，则拒绝。零副作用。不放行只读评估。不放行写入。算法终止。

若 sequence 既不是 S1、S2、S3，也不是 S4，则拒绝。算法终止。

I2. [派工门]
若 sequence 是 S1、S2、S3，且本次是 ask，则放行。ask 是绑定触发，不是 S4 的派工。算法终止。
若 sequence 是 S4，且本次是已有只读调查的合法 resume，则放行。算法终止。
若 sequence 是 S4，且本次是 fork 的只读调查，则放行。算法终止。
修复 fork 不是只读调查。不并进上一句。不并进向绑定 DevOps 派任务。
不把没打开的函数写成已经核过。
若 sequence 不是 S4，且本次是 resume 或 fork，则拒绝。算法终止。
若本次是 horizon，则本门不把 horizon 当成派工放行。绑定走 Algorithm B。转 Algorithm B。转入之后算法终止。不得靠步骤次序掉进 join。不得落到 deliver。不得落到向绑定 DevOps 派任务。
若本次是 join，且 sequence 是 S4，且有活跃任期，且尚未评审，且已有确认，则拒绝。零写。不因阻塞摘要放行。不进 Algorithm E。不返回已完成未退休。算法终止。
▷ 与 I1 开头同一格。理由：join 不进 I1。阻塞摘要不得把这一格改放行。
若本次是 join，且 sequence 是 S4，且折叠视图里已有阻塞摘要，则放行。算法终止。
若本次是 join，则拒绝。算法终止。
若本次是 deliver，则本门不裁。不拒绝 deliver。不把跳过写成派工放行。算法终止。
若 sequence 是 S4，且要向绑定的固定 DevOps 派任务，且尚无评审，则在任何持久化副作用之前拒绝。算法终止。
若 sequence 是 S4，且本次是修复 fork，且已评，且 outcome 是 Stayed，且尚无确认，则放行。算法终止。
若 sequence 是 S4，且要向绑定的固定 DevOps 派任务，且已评，且 outcome 是 Stayed，且尚无确认，则放行。算法终止。
第 9 节把修复派工写在执行门下面。过程以本算法为准。放行只在上面两句。
若 sequence 是 S4，且本次是修复 fork，且已评，且 outcome 是 Promoted，则拒绝。零写。不因为已有评审放行。算法终止。
若 sequence 是 S4，且本次是修复 fork，且已评，且 outcome 是 Stayed，且已有确认，则拒绝。零写。不因为已有评审放行。算法终止。
若 sequence 是 S4，且要向绑定的固定 DevOps 派新任务，且已评，且 outcome 是 Promoted，则拒绝。零写。不因为已有评审放行。算法终止。
若 sequence 是 S4，且要向绑定的固定 DevOps 派新任务，且已评，且 outcome 是 Stayed，且已有确认，则拒绝。零写。不因为已有评审放行。算法终止。
若本次不是 ask，不是 resume，不是 fork，不是 horizon，不是 join，不是 deliver，也不是向绑定的固定 DevOps 派任务，则拒绝。算法终止。
若以上各格都不命中，则拒绝。零写。不放行。算法终止。
这是未点名的新种类。不与上面各格混写。
不读证书。
不写入 Algorithm D 或 Algorithm E 的步骤。
▷ 不写入 deliver 的步骤。理由：没评审时派工要停，没评审时一段式 deliver 并不停。本门不裁 deliver。
▷ 不在步骤末尾默认放行。理由：没命名的格子不是放行。
# 能证伪：S4 尚无评审时，resume 或 fork 的只读调查被这道拒绝挡住；一段式 deliver 被这道派工门拒绝；未命中的派工因落到步骤末尾而被放行。
# 能证伪：修复窗的 fork 因为不是只读而被本门末尾拒绝；收口窗的 DevOps 派工因为已有评审被放行。

### Algorithm J 重开

输入：incumbencyId，以及已落盘的 Delivered 退休。
输出：AppendDurable 时已重开；AppendNotAttempted 时保持已交付；CommitUnknown 时既不报告已重开也不报告未重开。写前拒绝则零写。

J1. [谁能写]
若 sequence 是 S4，则拒绝。零写。不写 Reopened。算法终止。
若视图已有活跃任期，则拒绝。零写。不写 Reopened。不把 Delivered 加活跃任期收成重开。算法终止。
若没有 outcome 为 Delivered 的退休，且有 outcome 为 Relayed 的退休，则拒绝。零写。不写 Reopened。不另开一任。算法终止。
若没有 outcome 为 Delivered 的退休，则拒绝。零写。不写 Reopened。算法终止。
若任期 id 与这笔记得不符，则拒绝。零写。算法终止。
只有 Delivered、没有活跃任期、任期 id 相符、序列不是 S4，才转 J2。
# 能证伪：Delivered 与活跃任期并存时仍追加 Reopened；只有 Relayed 时重开或另开一任。

J2. [复制]
若不是从 J1 的通过格转入，则拒绝。零写。不写 Reopened。算法终止。
不生成新任期 id。
work 计数延续。不重置。不另起一份。
阶段从已交付记录复制。不从 sequence 重新推下一步。
评审整份留下。
授权从那笔记得读。
若授权读不到，则拒绝。不补 true。不补 false。算法终止。
追加 Reopened。载荷只含 incumbencyId。
若返回 AppendDurable，则返回已重开。算法终止。
若返回 AppendNotAttempted，则保持已交付。返回未重开。可以再发。算法终止。
若返回 CommitUnknown，则既不报告已重开，也不报告未重开。不补另一笔。不截断。不得把已交付改口。下一次打开才分类。算法终止。
否则拒绝。不把未分类的返回收成已重开。算法终止。

### Algorithm K 权威推进

输入：一次对已有 work 的再 commission、expected、next、当前折叠视图。
输出：AppendDurable 时已推进；AppendNotAttempted 时计数不变；CommitUnknown 时既不报告已推进也不报告未推进。写前拒绝则零写。

K1. [并或不并]
权威比较与前缀重锚，默认不是同一判定。
▷ 默认留下 AdvanceCommitted，不并进前缀重锚。理由：已读到的两处不是同一函数。
不并进前缀重锚。
若实现时发现二者是同一判定且同一调用方，则停止。不删 AdvanceCommitted。不改词表。退回规划。

K2. [何时写]
若这次不是已有 work 的再 commission，则零写。返回未推进。不返回成功。算法终止。
incumbencyId ← 折叠视图的当前活跃任期。
若没有活跃任期，则拒绝。零写。不追加 AdvanceCommitted。不采用调用方另给的任期号。算法终止。
若这一任没有评审，则拒绝。零写。算法终止。
不因证书有效而拒绝。不因证书失效而推进。证书不参与本算法。

K3. [载荷]
若 expected 与折叠当前值不符，则拒绝。零写。返回未推进。不报告已推进。算法终止。
若 next 为空，则拒绝。零写。返回未推进。不报告已推进。算法终止。
若 next 等于 expected，则拒绝。零写。返回未推进。不报告已推进。算法终止。
若 next 已出现在这一 work 的推进历史上，则拒绝。零写。返回未推进。不报告已推进。算法终止。
折叠只做这四次比较。不在折叠里哈希。不比较消息 id。
▷ 没有前进就是这四次比较。理由：空、与 expected 相同、与折叠当前值不符、或已经出现过，都不是一次推进。现行谓词里的消息标识分支随 R2 去掉，不留在这一步。
追加 AdvanceCommitted。载荷 ← incumbencyId、expected、next。
不写入消息 id。不写入 snapshotId。
若返回 AppendDurable，则返回已推进。算法终止。
若返回 AppendNotAttempted，则计数不变。返回未推进。可以再发。算法终止。
若返回 CommitUnknown，则既不报告已推进，也不报告未推进。不补另一笔。不截断。下一次打开才分类。算法终止。
否则拒绝。不把未分类的返回收成已推进。算法终止。
退休事件不改这个计数。本事件不另带一套 expected 去和退休声明竞赛。
计数挂在 work 上。没有活跃任期时它仍在。本算法因没有任期而拒绝，是因为载荷没有 incumbencyId 可写，不是因为计数消失。

### Algorithm L 术语与需求包

输入：代码、需求与资源里的旧词。
输出：一套词，一个需求包，两个留下的角色目录。

L1. [映射]
Road 改为 Work。
iteration 改为 tenure。
assessment 改为 review。
suicide 改为 deliver。
Continue 不另起名。它就是 Relayed 的派发含义。
Accepted 不另起名。它就是 Delivered。
certificate 删除。不改名保留。
▷ 不留 suicide 当别名。理由：两段是确认次数，不是另一个工具名。

L2. [包]
五个需求包收成一个目录 requirements/work/。
条款锚点写成 WHAT[work-NNN]。
不留 relay- 前缀。不留 planning- 前缀。
▷ 包名用 work。理由：Work 是两边共用的载体。用 planning 或 relay 会把来路写进目录。

L3. [留下]
role/plan 与 role/manager 留下。方法行不同。
这是一段式交互与两段式自动的实例差，不是旧词。
tool/suicide 的资源并入 deliver。两段写在 S4 实例说明里。一段写在 Plan 实例说明里。
js-manager 不改名。不删除。算法终止。

### Algorithm M 测试

输入：本节算法已写死的契约。旧测试只作材料，不是现行义务。
输出：一个编号空间下的测试。一段 deliver 与两段 deliver 各有锚点。

M1. [不抄旧义务]
不把旧锚点改名后继续断言旧义务。
若要引用 GAP-192、GAP-193 或 GAP-197 的现行断言，先重读那条测试正文。
本算法不宣布它们作废，也不宣布保留。
support 文件没有 WHAT 锚点。不把它们写成条款测试。

M2. [分叉锚点]
确认次数为 1 至少一条锚点：一次调用结束，且不存在 ConfirmationCommitted。
确认次数为 2 至少一条锚点：第一次只落确认；同一对重放不第二次结束；另一个 toolCallId 才写退休。
不得用一条 deliver 测试覆盖两种语义。
无评审却写下确认，必须红。
S4 的 Promoted 把交付授权置真，必须红。
S4 因缺 digest 或 path 被判成 Conflict，必须红。
无活跃任期时，同一围栏重入必须命中已落盘退休，不得返回 no active incumbency。

M3. [契约]
追加口写出的真实字节，交给折叠读取。
一段式退休载荷必须保留 retirementId、incumbencyId、outcome、ProviderRunId、ToolCallId、名为 retirementCursor 的字段与期望推进值。持权 Delivered 还要保留 execSessionId、digest、path。不得重新生成身份。
两段式退休载荷必须保留 retirementId、incumbencyId、outcome、ProjectionCut 与期望推进值。不得重新生成身份。
retirementId 的输入是固定标记、workId、ProviderRunId、ToolCallId、outcome，外加入口读到的 work 计数。
确认次数为 1 的标记是 oneshot-retirement-v1。确认次数为 2 的标记是 retirement-v1。
两个标记不合并。用另一个标记重算，必须红。
身份里的 outcome 只许是 Relayed 或 Delivered。不许是 Promoted 或 Stayed。
不得把 cursor 当成两种退休共用的一个字段名。
不得把 retirementCursor、活跃任期标识或 XTrace 头序号放进身份。
同一围栏重算必须得到同一个 id。
缺名为 retirementCursor 的字段的合法 S4 载荷不得判红。
S4 载荷里出现这个字段，必须判红。
一段式退休载荷缺期望推进值，必须判红。持权 Delivered 缺 execSessionId、digest 或 path，必须判红。
两段式退休载荷缺期望推进值，必须判红。
改标签、改身份或改因果顺序，另一侧未改时，测试必须红。
不把键序或空白锁进契约。
不只断言写成功。下列四支各有一条锚点，缺一条就红：AppendNotAttempted、CommitUnknown、只见必成对前一行的 PairIncomplete、文件不以换行结尾的 StorageInvalid。
AppendNotAttempted：已知未提交，调用方可以再发。不得把这次写成已落地。
CommitUnknown：既不得写成已提交，也不得写成未提交。不得补另一笔。不得截断。不得另开一任。
下一次打开只见必成对的前一行：必须得到 PairIncomplete。不得补后任。不得收成 Conflict、Nothing、DeliveredIdempotent 或 ActiveRebound。
文件不以换行结尾：必须整份 StorageInvalid。不得裁残行。不得折被拒绝的前缀。不得进入 Algorithm F。

M4. [不变量只记一处]
Algorithm C 的不变量写入 work 条款一处。
不在别的条款里再抄一遍。
若这句话不能低噪地机械判定，则不新增正则脚本，不新增扫空目录的门，不新增无人运行的本地脚本。
若已有检查覆盖同一句，则加一个 known-bad fixture：长期字段里存了下一步时必须变红。
不新造第二份散文。算法终止。

### Algorithm N 删除旧轨

输入：Algorithm A 到 Algorithm M 的验收都已通过。
输出：仓库里不再有第二套生命周期事实，也没有旧账转换。

N1. [门]
若 A 到 M 有任一验收未过，则不进入本算法。算法终止。

N2. [旧账]
不读取既有 AgentJournal。
不读取既有 Relay 事件文件。
不转换。不写兼容层。
新 work 只写统一事件轨。
▷ 旧账不是新轨的输入。理由：转换层就是发展痕迹。

N3. [删除]
删除第二套生命周期事件类型、证书类型、suicide 这个名字，以及把完整物理历史写成现行义务的句子。
删除 WorkspaceSnapshot 这个类型名。删除它的独立模块。统一事件载荷不得含 snapshotId。不新造 SnapshotMismatch。不把这个模块留成无人调用的残留。
TenureReanchored 不是第二套生命周期事件。不删除它。不并进统一词表。不改前缀 epoch 折叠。
不改仓库根 plan.md。
不回退已经入库的 js-plan 描述装配。
已退役的 ask 挂起导出不是活清理项。不把它再删一遍当验收。
不留过渡名。不留别名。
若删完仍有两套折叠，或 suicide 还在当别名，则本路径没有走完。不加适配器收场。算法终止。

### Algorithm O 施工顺序

输入：Algorithm A 到 Algorithm N、Algorithm Q。
输出：当前可以开工的集合。本算法不是运行时调度，不得实现成程序计数器。

O1. [内核]
先做完同一套事件与折叠，使 A、B、C、D、E、F、J、K、Q 能跑。
验收：S1、S2、S3 与 S4 都能开任、评审、退休。
确认次数为 2 的确认事件可重放，重放不第二次结束。
未过此门，不做 O2。

O2. [并行]
O1 过后，G、H、I 与 deliver 改名可以同时做。
三处写入不同：投影、派发、工具名。
G 不等 H。H 不等工具改名。

O3. [后段]
L 在事件名已定后起草。合并晚于 O2，早于 M。
M 晚于 L。
N 最后。算法终止。

### Algorithm Q 无活跃任期时不另开一任

输入：已有 WorkOpened 的折叠视图，以及调用方是否持有一笔尚未落盘的退休。
输出：拒绝且零写。不追加 IncumbencyOpened。

Q1. [调用方]
调用方只有两处。Workflow.fs 的 tryBuildAcceptedOpening。Change/Host/Host.fs 的 activateNextIfAbsent。
两处都进本算法。都不另写一套开任。
别的入口不调用本算法。
第 9 节写明过程不在本节。过程只在本节。
首任只来自 Algorithm A。后任只来自 Algorithm D 的 D8，或 Algorithm E 的 E11。
恢复位置为 Nothing 时，也不调用本算法去补一任。

Q2. [没有合法追加格]
若视图已有活跃任期，则拒绝。零写。算法终止。
若按 Algorithm F 的 F0，当前 Delivered 成立，则拒绝。零写。不另开一任。算法终止。
历史 Delivered 已经重开、当前 Delivered 不成立，不按这一支拒绝。
若按同一处 F0，当前 Relayed 成立，则拒绝。零写。不另开一任。算法终止。
若调用方同时持有一笔尚未落盘的退休，则拒绝。零写。算法终止。
那一笔必须由 Algorithm D 或 Algorithm E 同一笔追加退休与开任。不得拆到本算法。
若视图没有活跃任期，也没有退休，则拒绝。零写。不猜 sequence 的第一元。不追加 IncumbencyOpened。算法终止。
否则拒绝。零写。算法终止。
▷ 没有可达的合法追加格。理由：同一笔开立已经带上首任。Relayed 的后任与退休同一笔追加。剩下的无任期视图不是另开一任的入口。
# 能证伪：两处调用方在无活跃任期时追加了 IncumbencyOpened。
