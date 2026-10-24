# Changelog — 版本历史

## Unreleased — 迁移文档统一入口

- `wanxiangshu → wanxiangli` 抽取、Lifecycle cutover 与 HNI 的迁移契约、施工状态、遗留项和历史验收边界统一收敛到根目录 `AGENTS_MIGRATION.md`；删除原分散迁移文档并更新入口。该文件未来替换 `AGENTS.md`，本次不替换现有协作规则。
- 正式 WHAT/GAP、非迁移产品计划、库 API 参考和可执行适配器/夹具保留；历史验收不冒充本轮运行结果。

## Unreleased — resetScope 协议与自然停机返回

- **唯一的上下文边界**：`resetScope(oldScopeVerboseVerbatimExperience?, newScopeObjective, newScopeTurnBudget)` 取代 ANCHOR/`prefixUntil` 视图声明，成为划定上下文的唯一手段；调用即删除上一次 reset（或会话开始）到本次之间的全部内容，只保留用户消息与既往 reset 成对记录，之后的内容进入新 scope。`turnBudget` 在新 scope 上倒计时，工具结果尾部披露 `[Remaining turnBudget: N]`；会话开始要求先估预算再 reset，归零只发弱提醒，不中断在飞工作。
- **删除的旧机制**：ANCHOR 锚点编号、`prefixUntil`/`prefixCount` 前缀声明、`carry` 数组挑材料、以及配套的压缩重放与 checkpoint 口径一并移除；已退役的旧名字只留在历史条目与本条说明里，不再是现行合同。
- **前缀 epoch**：`resetScope` 落一条 `ScopeReanchored` durable 事实推进前缀 epoch（Previous→Next），与任期交接的重锚事实并列，不再靠视图声明换 epoch。
- **父→子传递**：fork/resume/commission/devops 咨询/dag/advisor 一律把指令散文渲染为 `#` 注释头，把发送方本次 scope 之后面向模型的完整视图作为 TOML `[[context]]` 表随文档发出。
- **子→父传递**：Engineer 与 DevOps 自然停机即为交付——本轮正常停下时，子方完整的 post-scope 视图作为 TOML `[[session]]` 返回，父方 `join` 收取该份；这两个角色没有 `this.deliver`，也不再被追加偷停提醒。Manager 与 Plan 保留生命周期 host tool `deliver`（一次调用完成或接力）。
- **顾问席**：`advisor(intent)` 收敛为一次请求，形态等同父方问子方一个问题，返回 `{ ok, context }`。
- **测试证据**：context-compression 36/36、work 188/188、advisor 6/6、delegation 118/118、capability 23/23、prefix-stability 58/58；Fable build generation 81 成功。以上为受影响套件的实测数字，**不表示整树套件结论**——完整套件由另一路 runner 复核中。
- **未验证**：本轮未调用真实 provider、未部署、未跑真实 Host 链上的端到端会话。

## Unreleased — 修复 carry 重复地址触发 HOST-013 会话终止

- `PairProgrammingThoughtTransform` 不再把压缩视图中重复的 Host 地址当成非法输入；保留所有正文副本与顺序。Before/After 分别取首/末副本，cursor 提示只落在最后一个可承载副本；同消息续写的纯 prose 不吞掉旧 tool result 的提示。缺失地址继续拒绝。
- `context-compression/tests/031.test.mjs` 固定回归 `msg_167f66a46001l05Pw2Nb0UkjUs`，旧产物实跑复现同一错误；新增 fast-check（seed `0x484f5313`，1000 runs），覆盖重复引用、前缀重叠、重排、user/completed/error 载体、边界续写与重复注入。断言每份正文保留、原始输入不变、提示仅注入一次、durable pair 不重复追加。
- 验证：Fable build generation 417 成功；031 经正式 runner 为 25 passed / 0 failed；关联 13 文件为 85 passed / 0 failed / 6 原有 TODO，runner 因待证项返回 1，非完整验收。直接运行完整 `maybeInjectGuideline`：同一地址保留三份、历史提示仅一份、重放字节一致、会话终止次数为 0。未调用真实 provider、未部署。

## Unreleased — AXIOM 整改收尾记录（2026-10-22/23）

本节保存原根目录《整改方案》的实施收尾记录，避免审计工作稿继续留在根目录。全文（审查基线、逐条发现与索引）留在 Git 历史，最后版本为 `c68ce2fad`。

- **提交与推送**：子模块 `wanxiangli` main `56f085a`；宿主 `master` `9b4d031a2` 记录该 gitlink，fast-forward 推送，未使用 force push。提交成功、验证通过、部署运行是三种事实：本轮到验证为止，未部署、未运行真实 provider。
- **用户少做的一步**：
  - 发布不再要求先取得生命周期评审记录：`Change/Program.fs`、`Change/Host/Host.fs` 移除了 `LifecycleReviewed` 与对应资格搬运；发布仍按 target、candidate、expected head、ff-only CAS 与重复发布识别把关。
  - 建隔离 worktree 不再要求整个工作树干净：`Change/Runtime.fs` 的 `IsDirty` 门与 `GitPort.IsDirty` 已删除；合入目标前的 `verifyClean` 保留并指出具体阻挡路径。
  - Change 不再解释库的字符串协议：`Projection.fs`/`Program.fs` 改为类型化结果，`wanxiangli/integration` 整包删除。
  - 交付只需一次 `deliver`：完成或接力由是否指定后继决定，评审成为可选协作事实；`ExecutionResult` 补上 `Conflict`；`DurableWorkHost` 不再投影 `Confirmation`/`LastRetiredReview`。已评审的 Manager 仍可 Read/Glob/Grep；`managerReviewReadOnly` 删除。
  - 普通编程调用不再预加载交付归属；快照读取失败报 `ContextUnavailable` 而不是“没有正文”；交付凭证提交区分 `Committed`/`NotCommitted`/`Unknown`。暂停句柄的交付门按真实资源分类，默认阻止；只有显式标记 `__jsKnownNoEffect` 的暂停会在交付时自动收束。
  - 等待预算不再按文本字节计价：程序与 `this.call` 的 `planB` 计价删除，改为显式 `timeoutMs` 或默认 10000ms。计划底稿没有写前评审门；`PlanReviewRequired` 协议删除。
  - Sphinx 公开五个真实工具（start/submit/status/cancel/export）；平局按声明规则记录，无平局不写平局；未执行的种子、无效证书状态与事件删除；依赖未成功的子项不派发。退化统计只做诊断，不再中断；案例缓存冲突不再杀宿主进程，`PhaseConflict` 为非致命报告。
  - 门禁只保留真实后果检查：控制金字塔、适配器计数棘轮与基线、格式文件名白名单、统一测试阶梯退出必过列表；`verify.mjs` 增加 `--suites` 聚焦入口；结构快照改为不变量断言。
  - 库侧：`delivery` 由持有围栏的对象一次完成 run+toolCallId 判定，claim 非破坏、handOff 原子提交、旧票据失效；`wait` 不再写调用者的 `cancelCount`，Lease 面与运行时一致；`runNaive` 移入测试支持；`resources` 只校验所选语言，发行版完整性移到 `scripts/checks/language-parity-gate.mjs` 与 distribution [012]；`selfPackageRoot` 有终止条件。
  - 文档与提示按真实行为更新：根 `AGENTS.md`、`wanxiangli/AGENTS.md`、README/OWNERSHIP/MIGRATION、`requirements/README.md`、`requirements/INDEX.md`、WP-019 历史状态说明、`world/common-law`、`planning/s1..s3`、`role/plan`、`tool/js-plan`、`tool/deliver`。
- **实际验证**：`node scripts/build.mjs` exit 0（generation 25）；`cd wanxiangli && npm test` 885/885；宿主 work 192/192、capability-enforcement 135/0、change-integration 141/0、sphinx-v2 234/0、degeneration-guard 59/0、knowledge-reuse 143/0、verification-system 502/0、requirement-system 16/0、provider-projection 76/0、provider-language 25/0、distribution 48/0、structured-workflow 95/0、causal-wait 52/0、host-boundary 237/1（013）。
  - 干净基线对照（HEAD `70e9e1732` + gitlink `c2afb1af`，临时 worktree 构建后运行）：repository-programming 245/344（99 fail）、process-execution 166/182（13 fail）、host-boundary 228/246（13 fail）。当前 repository-programming 333/350（17 fail）、process-execution 172/186（11 fail），残余失败均为基线失败子集；本轮修复的 `repository-programming-005` 是唯一新增后又被解决的一项。
  - `node scripts/check.mjs`：仅剩 12 条既有 `js-boundary-gate` 问题（context-compression 031、delegation 033、interaction-authority 018），均来自其他并行未提交工作。
- **未解与未测**：17 条 `repository-programming` 失败、11 条 `process-execution` 失败与 `host-boundary-013` 属基线既有欠账，本轮未修；12 条 js-boundary 问题同样不属本轮文件。Sphinx 的物理 provider 适配仍未接线：闭环为 start → 计划 → 持久派发意图 → worker 提交结果 → answer，未声称已调用外部模型。未运行真实 provider、真实远端 Git 发布、部署与 Long Stroke e2e。两仓工作树仍有与本轮无关的未提交改动（子模块 13 项、宿主 177 项），未纳入提交。
- **文档归位（历史）**：根目录《保姆级交接与展望》的会话端口残留清单曾移入 HNI 证据文档，现统一收录于 `AGENTS_MIGRATION.md`；其余历史快照随该工作稿退役。

## Unreleased — HandleWorkConsumed 不再因过期 cell 触发 journal semantic cut

- **根因**：`HandleController.consumeAdmittedWork` 在 `RefreshProjection` 之后仍用调用方传入的过期 `HandleRecord.Lifecycle` 作为 `HandleWorkConsumed` 的 expected cell。fold 用 durable `CompletedAwaitingJoin`/`Abandoned` 与 fact 做 `cellEqualsExpected`；调用方 cell 与 durable 不一致时 `ConsumptionMismatch` → journal fact reject → `FatalProcess.trip "journal-semantic-cut"`。
- **修复**：consumption evidence 只取 refresh 后 durable work 的 cell；append 前对同一 cell 做 `HandleProjection.consumeWork` dry-run，会 reject 的不写 fact（软失败，不 cut）。
- **证明**：`008.test.mjs` fast-check（seed `0x48434f4e`，2000 runs）覆盖 matching/mismatch/blank-id/Abandoned 形状；production 路径 64 组「durable cell ≠ stale caller cell」全部 consume 成功且重复 consume 为 `AlreadyRetired`，无 journal-cut。
- **验证**：`node --test --test-name-pattern='matching durable|stale caller|EXEC_009_a_replayed|EXEC_004_a_retirement|fold_refuses|retirement removes|consume_abandoned' requirements/managed-session-lifecycle/tests/008.test.mjs` → 7/7 pass。fork 路径 `VIEW_DECLARATION_INVALID`（prefixUntil）为既有工作区问题，非本改。

## Unreleased — 评审成员化与义务台账收敛、免确认流、Plan 起草门禁与工程守卫清理

- **评审成员能力化（`this.review`）与纯数据回执**：
  - 撤除独立 `review` 宿主工具注册（`ReviewShell` 退出独立工具清单）；评审能力作为 `JsCapability.Review` 直接投影入 Manager 与 Plan 的编程面（`this.review(findings)`），由 `wanxiangli/programming` 统一将 `ReviewAssessment` 与 `Review` 投影为 `Review` 能力与 `review` 成员。
  - **接口与删除语义**：入参为按键对象数组 `[{ key: string, value: string | null }]`；`value: null` 显式删除对应键，空数组 `[]` 提交不抹去既有台账；禁止空串删除别名与旧版遗留字段。
  - **宿主闭包**：通过 `this._api.review(findings)` 返回纯数据包（`{ ok: true, outcome: "Promoted" | "Stayed", firstReview: bool, obligations: [...] }`），支持单 attempt 内多次更新，不再输出独立渲染 prose。
- **首次空定资格与新任期投递晋级**：
  - 首次评审定资格（first-empty eligibility）：仅在任期内**首次评审**且生成台账为空时确立晋级资格（`ReviewOutcome = Some "Promoted"`）；台账非空则定为 `Stayed`；后续调用仅作逐键台账增删，不改变已定资格。
  - 评审本身不推进工作阶段，晋级与完成由当前持有资格的任期执行 `deliver` 动作；对于 Plan 流程，合格任期的 `deliver` 退休当前任期并在推进后的阶段开启后继新任期（S1/S2 写 `IncumbencyRetired(Relayed)` 与 `IncumbencyOpened(stage + 1)`，S3 写 `PlanAuthorized`）。
- **全面免除二次确认门禁（No confirmation）**：
  - 彻底清理 `seedConfirmation`、`hasConfirmation`、`confirmation` 状态与参数残留；合规请求直接执行流转，不再增设阻断合规动作或重复审批已决事项的伪防呆门禁，严格恪守公理一。
- **Plan 普通起草路径与写入门禁（Plan draft path / write gate）**：
  - Plan 角色起草流程收拢至统一草稿路径；增设评审前置写入围栏（`isReviewFinished` 为真后方可解锁草稿写入，`reviewFinishedOnly`）；普通起草路径与 deliver 读取同源。
- **PlanB 文本字节等待预算（planB byte wait budget）**：
  - `this.call(command, planB)` 要求必填非空 `planB`，按 UTF-8 字节数 × 50ms 定价武装让出定时器；程序级调用 `{ program, planB? }` 缺省为无定时器（避免万能零定时器），传入非空 `planB` 时武装让出定时器；`unpark` 支持以新 `planB` 重新定价等待时长。空串与空白字符 `planB` 拒绝执行（`CALL_COMMAND_UNSUPPORTED`）。
- **工作树创建前干净状态门禁（Worktree clean gate）**：
  - 变更集成流程在创建隔离工作树（`git worktree add`）前严格校验主工作树状态，检测到未受管修改、未暂存、未追踪或脏子模块时直接在创建前拒绝，防止脏工作树泄漏入新环境。
- **退化守卫（Degeneration Guard）彻底退役与统计删除**：
  - 彻底移除 `degeneration-guard` 子系统、相关测试及规范文档；删除 `LoopDetector`、`LoopSensor`、`LoopEventCodec` 及包导入 `#wanxiangshu-loop-detector-envelope`；清理 `Foundation/HostSession` 中 `AbortCause`、`DegenerationKind`、`LoopTextDelta` 等遗留符号与退化统计。
- **强制控制金字塔检查（Mandatory pyramid gate）**：
  - `scripts/checks/fsharp-control-pyramid.mjs` 正式入闸为强制静态检查，对 F# 代码分支深度（`if` 链与嵌套模式匹配）执行扁平化限制，禁止深层分支金字塔结构。
- **A2A 顶层散文注释与 body/carry schema4**：
  - 代理间（A2A）消息以文档顶部的 `# ` 注释承载主散文（指令、指导与报告），严禁把散文塞入字段值或使用汇报模板；`ReturnedMaterial` 规范为 `{ body: string[], carry: string[] }` 纯文本拆分（`CompletionCodec` schemaVersion 4，拒绝旧格式）；空消息体触发 `empty-body abort` 拦截，防止无散文交付。
- **PR 63 选择性采纳（PR63 selective adoption）**：
  - **采纳**：CI 缓存与运行条件优化、SecondHost 计时器抽象与适配（`HostPlugin.create clock createTimer` 与 `nodeClockPort`/`nodeTimerPort` 适配）、持久化写入者标识隔离（writer-ID）、测试夹具与离线探针健全性补丁（`time/004`、`verification/006`、`durable/005`、`dispatch/010`、`delegation/027`）。
  - **跳过**：排除旧版生命周期与退化守卫代码复活尝试、Fork `.gitmodules`/`gitlink` 覆盖，以及与当前公理体系（无二次确认、按键台账门禁、A2A 顶层散文）冲突的旧版回退实现。
- **落地现状与验证状态**：
  - 本批次能力定义、模版资源、退化守卫清理、PR 63 选择性采纳及静态门禁已全部闭环；本轮聚焦验收套件（work 191/191、change-integration 002 15/15、repository-programming 050/058 10/10、context-compression 031/032 27/27、obligation-ledger 17/17 + 1 todo、advisor 15/15、wanxiangli lifecycle/capability 67/67，共 342 题通过、0 失败）及各安装工具 smoke 均已通过实跑验证。历史变更记录全量保留。

## Unreleased — Authority 接收不再落错 closure kind（journal semantic cut 根因）

- **根因**：`Dispatcher.authorityRootFact` 与三个 wire codec（`CodecSurface`、`FactCodecSurface`、`ProviderFailureSurface`，以及 `TemporalSurface`）在 AgentOwnerRoot 无法证明 life 时，把 `ExpectedClosureKind` 默认成 `HumanRootManagedRun`。该值属于 HumanRoot，`PromptFactCodec` 的校验因此拒绝接收（`ExpectedClosureKind ... does not match AuthorityKind AgentOwnerRoot`），journal 写 semantic cut 并 `FatalProcess.trip`。
- **SSOT 修复**：closure kind 的推导只有一个 owner——`AuthorityLifeClassification.closureFor` / `recordedClosureKind`。`closureFor` 对无法证明 life 的 AgentOwnerRoot 使用既有 `coarseAgentOwnerLife`（`Child`），永不落到 HumanRoot 类；`recordedClosureKind` 只在记录值与 root kind 相符时保留记录值，否则改用推导值。`Dispatcher` 与四个 codec 全部改为调用这两个函数，删除各自的 `Option.defaultValue ("HumanRootManagedRun", None)` 副本。
- **不变量与 fast-check**：`requirements/interaction-authority/tests/018.test.mjs` 新增属性测试（seed 18018，400 runs）：任意 root kind / manager 位 / life / 记录值下，最终 closure kind 必须属于同一 root kind 且有 witness kind；AgentOwnerRoot 永不落 HumanRoot 类。固定回归覆盖触发 fatal 的输入（AgentOwnerRoot + 无 life + `HumanRootManagedRun` → `AgentOwnerChildWork`）。
- **验证**：`node --test requirements/interaction-authority/tests/018.test.mjs` → 14 tests / 11 passed / 3 failed；三个失败均为既有的 GAP-123 `terminalSourceFact` 缺失（`RuntimeSurface` 尚未暴露该入口，先于本改动存在），新增属性测试通过。**注意**：该修复需重启宿主进程才生效；运行中的旧 `dist` 仍会继续 semantic cut。

## Unreleased — 投递后中断仍向父方返回交付（delegation-033）

- **规则**：`deliver` 之后的 attempt 中断保留（模型不应看到 deliver 回复），但中断不得吞掉交付。宿主在发出中断之前，先用已提交的交付见证结算本次投递子运行（`HostForkRunLifecycle.publishDeliveredReturn` 发布与该见证一致的 `Completed`），父方 `join` 因此仍收到该次交付的 view；中断只结束 attempt。Manager/Plan 的生命周期 deliver 没有父 join，行为不变。
- **`DELIVERY_NOT_ESTABLISHED` 收紧与散文归属**：有 journal 的生产宿主中，交付调用无法归属（交付调用的 assistant 消息没有散文、同一回合的物理输入之后也没有散文、没有物理输入、没有逻辑运行或快照）时不再静默降级为「仅进程内交付格」。`ToolWorkflow` 以 `UnattributedDelivery` 拒绝，调用方看到失败且不留可呈现的交付格；只有无 journal 的 harness 继续走 cell-only 路径。同时，`ToolRegistry` 的交付归属接受**同一回合中本次调用物理输入之后的最近一条 assistant 散文**：交付合同本就要求「先讲散文再 deliver」，provider 常把两步拆成两条 assistant 消息；此前的严格同消息要求让真实交付无法形成见证。这条同时解释了「子代理以为自己 deliver 了，Manager 的 join 永远等不到」的现场：交付从未形成见证。
- **需求与文档**：新增 `requirements/delegation/WHAT.md` [033]（投递后中断不得吞掉返回）；`repository-programming` [054] 补交付后中断与见证失败规则；`requirements/INDEX.md` delegation 计数 30 → 31；`任务书A.md`、`保姆级交接与展望.md`、`CHANGELOG.md` 同步该口径。
- **验证**：`node --test requirements/delegation/tests/033.test.mjs` → 2 passed / 0 failed（见证结算发布 Completed；未归属交付被拒且无进程内交付格）。**整树构建仍未闭合**：Fable 编译通过，但 `js-module-linkage` 仍报 8 条陈旧 `dist` 导出不匹配（先于本改动存在），真实 fork 夹具在本机同样超时；installed-Host 回归待 `dist` 重建后由正式 runner 复核。

## Unreleased — 2026-10-22 文档收口批次（Ledger lane）

- **GAP 台账（`requirements/GAP.md`）**：17 行列形状归一（表 1 GAP-025/038；表 2 GAP-210/077；表 3 GAP-225…GAP-237），0 行形状不一致；203 行 GAP 行一行未删、无重复 id；状态词未改（PARTIAL 113 / CLOSED 53 / ARCHIVED 18 / OPEN 10 / RETIRED 9）。文字实际变化 9 行（GAP-025 转义 `\|`、GAP-038/210/077 并格、GAP-231…237 前缀中性化 + 并格）；GAP-005/GAP-050 维持 `OPEN`（当前合同下无 faithful mechanical oracle，需人工审阅）。本批 register 的 GAP 证据更新：GAP-075、GAP-088、GAP-123 各追加 lane checkpoint 证据注记，**状态维持 PARTIAL**（GapBehavior 为设计态无生产改动；Authority 6 项编译修复已落地但终态生产者未接线）；无 GAP 在本批被标记 CLOSED。
- **HNI 端口登记（现归 `AGENTS_MIGRATION.md`）**：HNI-12 逐行审证追加（A1 `SessionInfo.Workspace` 已落地、A2 产品侧订阅、P-04 `ProviderRun`/`ToolAdmission`、P-14/P-15 `DelegationEnvironment`）；next free port id 仍为 **P-20**（A1 仅增字段，未分配新家族）。OpenCode 侧 P-04/P-05/P-06/P-07 仍无 WIRED-OC（`Unsupported*` 或 omp-only），不标 RETAINED。
- **BOARD / CUT-36**：CUT-36 维持 `DOING`（`.Herdchestra/takeover/secondhost.md` checkpoint 缺失，无 lane-verified smoke；阻塞原因已写入 card §12）。
- **交接文（`保姆级交接与展望.md`）**：顶部新增权威段（目标、十一条并行线、已核实事实、本机证据不随 Git 分发）；清除 11 处旧额度/停电/内存紧张措辞（最终扫描 0 命中），历史事实与验证失败保留。
- **证据来源**：`.Herdchestra/takeover/{authority,tools,hni,domains,gap-behavior,integration-cases,verification}.md`；`fork.md` 与 `secondhost.md` 不存在，已登记为缺失。


## Unreleased — 构建入口修复（`node scripts/build.mjs` 此前对所有调用都失败）+ 验收命令纠正

- **代码落地（已推 `origin/master`）**：`ff0b22f8c79f5c507958c80bb315e43e782e7192`（范围 `dc5d98b92..ff0b22f8c`），提交标题 `fix(build): repair the build entrypoint the impact-closure change broke`。改动文件只有 `scripts/build.mjs`（+2/-2）。
- **坏掉的症状**：`node scripts/build.mjs` 对**所有**调用形式都立即失败：

  ```
  ════════════════════════════════════════════════════════════════════════════════
    BUILD FAILED
  ════════════════════════════════════════════════════════════════════════════════
  clean is not defined
  ```

  这不是环境问题，是 master 上的真实回归：提交 `9ffb26a66`（`fix(build): compile the impact closure and forbid a full rebuild`，2026-10-22 11:01）删掉了 `main()` 里的 `const clean = process.argv.includes('--clean')`，却保留了 `scripts/build.mjs:774` 的 `await runBuild({ targetRoot: root, clean })` —— 一个自由变量，ReferenceError。
- **两处修复**：
  - `scripts/build.mjs:774`：`runBuild({ targetRoot: root, clean })` → `runBuild({ targetRoot: root })`。
    `runBuild` 本就只走影响闭包，`determineBuildDecision` 自己的 `clean = false` 默认值仍在（`scripts/build.mjs:260`）。**不恢复 `--clean` 旗标**：全量重编必须不可达，`scripts/checks/aggregate-retired.mjs:112-120` 专门禁止 `resetOutputDirectory(` 出现在 build.mjs 里。
  - `scripts/build.mjs:282`：`existingManifest.compiler?.toolIdentity` → `existingManifest?.compiler?.toolIdentity`。
    同一提交遗留的第二个崩溃点：没有 build manifest 的树（例如新克隆）在算出影响集之前就对 null 解引用。缺 manifest 现在是「没有基线」，不是崩溃。
- **验证（实测）**：`node scripts/build.mjs --plan` exit 0、输出合法 plan；`node scripts/checks/aggregate-retired.mjs` → `aggregate-retired: OK`；`node scripts/checks/architecture.mjs` → `architecture: OK`。
- **验收序列的连带纠正**：`node scripts/build.mjs --clean` **不再是合法命令**（`9ffb26a66` 故意移除 `--clean`，`scripts/build.mjs:741` 现将其作为未知参数拒绝并提示 "full compilation is forbidden"）。正确的构建命令是 `node scripts/build.mjs`（只编译影响闭包）或 `node scripts/build.mjs --plan`（只读预览）。历史验收表（CUT-38 §6 / §7.7）里的 `--clean` 是当时跑的快照，按「不改旧历史快照」保留原文。
- **`scripts/suites-manifest.sh` 的两处活调用方已改干净**（该脚本是活脚本，不是历史快照）：
  - 头部 Contract 注释里的示例构建命令；
  - `dist` 缺失时那条 FATAL 提示里的构建命令。
  两处原先都写着 `flock /home/hermes/work/vfs-build.lock node scripts/build.mjs --clean` —— 既包含已作废的 `--clean`，又引用了一个**在本仓不存在的外部锁路径**（本仓没有 `locks/` 目录；构建互斥现在由 `scripts/build.mjs` 内部的 `CrossProcessMutex` 持有，锁文件 `.fable-build/build.lock`）。现已改为 `node scripts/build.mjs`，并说明构建自己持锁、无需外部 `flock`。同文件里另外两处提到 `build-slot` / `vfs-build.lock` 的注释一并更正。
- **实测 smoke（参数路径）**：`bash -n scripts/suites-manifest.sh` 通过；`--help` 输出正常；`REPO` 指向无 `dist` 的目录时打印新的 FATAL 文本并退出 2；`REPO=<本仓> bash scripts/suites-manifest.sh advisor` 正确解析出 4 个 advisor 测试文件并落到 `summary.txt`（该次 group 运行 exit 1 是**运行器自身的陈旧度门**按预期拒绝：`runner: refusing to run. / Compiler inputs changed` —— 当时并发 worker 正在改 `src/`，与本改动无关）。
- **提交与部署的区别（本条只到「已推」为止）**：**提交成功、已推送 `origin/master`（fast-forward `dc5d98b92..ff0b22f8c`，无 force push）。** 但**尚未部署**、**尚未在合并后的整棵树上通过完整验收**：当前整合树**仍编译不过**（139 个 F# 错误 / 23 个文件），已按文件归属分派给各 owner。`scripts/build.mjs` 的入口修复只保证「构建能启动」，**不构成「构建通过」**。
- **同批附带修正（`f237b5701`，已推 `origin/master`）**：`scripts/suites-manifest.sh` 原先把 `LOGDIR` 默认成 `$REPO/.test-logs/suites`，那是**把运行数据写进工作树**，违反 AGENTS.md「全部项目运行数据统一放在主工作树的 `.Herdchestra/`，linked worktree 共用这份本机状态」。现改为经**既有的** `scripts/lib/state-root.mjs` 解析主工作树的 state root，落到 `<state root>/takeover/suites`；该 helper 只查询 Git 拓扑，**不写任何 Git 状态**。`LOGDIR` 仍可显式覆盖；解析不出 state root 时是 FATAL 并提示改设 `LOGDIR`，不静默回落到树内路径。实测 `stateRoot()` 在本仓解析为 `/home/kunweiz/Desktop/vibe/wanxiangshu/.Herdchestra`，且树内不再产生 `.test-logs/`。
- **文档批次（本次）**：`requirements/GAP.md` 的列形状归一与 `保姆级交接与展望.md` 顶部权威段更新。
  - **GAP.md**：归一前列形状不一致的 **17 行**（表 1 的 GAP-025、GAP-038；表 2 的 GAP-210、GAP-077；表 3 的 GAP-225…GAP-237 共 13 行）→ 现 **0 行**。**203 行 GAP 行一行未删、无重复 id**；状态词**一个未改**（逐行比对，0 行的状态词集合发生变化）；命题与证据文字**未删**。文字实际变化的 **9 行**：GAP-025（代码跨度内的裸 `|` 写成 `\|`）、GAP-038 / GAP-210 / GAP-077（末列并入前一列）、GAP-231…GAP-237（重复的状态前缀中性化 + 证据列并格）。表头由 3 段变为 5 段（新增表 4 = 8 列，表 5 = 6 列），**行内容未动**。
  - **归一后的台账口径**（由独立解析器复核）：GAP 行 **203**；状态单值 **PARTIAL 113 / CLOSED 53 / ARCHIVED (package REMOVED) 18 / OPEN 10 / RETIRED 9**（合计 203）；形状不一致 **0 行**。
  - **交接文顶部**：修正三处过时口径——主树 HEAD（此后 Integration 连推 4 个提交，`origin/master` 现为 `fbee2d7d5`；工作区当前非空，多为各线在飞编辑）、Authority 线状态（该组 6 个文件在第二轮诊断编译**不再被报出此前的类型错误**，但整轮 Fable 编译仍失败，**这不能证明产物可执行**）、GAP 归一计数（13 → 17 行列形状 / 9 行文字变化）。另同步了 Domains / Tools / HostNeutral 三行的现状（`Host/Ports/Tools.fs` 的 `ToolInvocation.ProviderRun` 与 `ToolSpec.Admission` 已落、`Host/Ports/Sessions.fs` 的 `Workspace` 已落、`Sphinx/V2/Hosts/OpenCode/Adapter.fs` 的单 match 金字塔已改），以及静态门禁的当前命中点（已从 Sphinx Adapter 移到 `Persistence/Journal/PromptFactCodec.fs:305`）。
  - **GAP-005 / GAP-050 维持 `OPEN` 不动**：两者在当前合同下**无 faithful mechanical oracle**——[015] 是提交纪律约定，[003] 是公理地位与无裁决覆盖的语义原则；机器落点的前置条件在规范侧改写命题，不在测试补写范围。
- **文档批次（第二次，接 7cdd5f60d 之后）**：交接文顶部权威段按**真实回执**更新。
  - **第三轮诊断编译**（HostNeutral 批次 ready 后）：**53 个错误 / 9 个文件**（139/23 → 114/19 → 53/9，36 s）。HostNeutral 的 9 个文件全部不再报出此前的类型错误；同时变干净的还有 `Fork/OpenCode/Tool.fs`、`Execution/Session/OpenCode/HorizonTool.fs`、`Fork/JoinRun.fs(.fsi)`、`Casebook/OpenCode/BookkeeperToolSurface.fs`。剩余 53 按归属：SecondHost 28（`Host/SecondHost/HostPlugin.fs` 15、`Omp/Adapter.fs` 13）、Tools 17（`Casebook/BookkeeperPolicy.fs` 11、`Execution/Tool/AdmissionTable.fs` 3、`OpenCode/Tools/ToolRuntimeScopeSurface.fs` 2、`OpenCode/Tools/ToolRegistry.fs` 1）、Authority 8（`Interaction/Authority/Fold.fs` 4、`Model.fs` 2、`Run.fs` 2 —— 本轮新增，属在飞重构的新输入）。**口径限定**：整轮 Fable 编译仍以失败告终，「不再报错」不能证明产物可执行。
  - **构建入口被未归属的新文件阻断**（构建的既定契约：影响闭包拒绝覆盖无归属的生产源，所以一个没登记的文件会挡住整棵树，不绕过）：Tools 线新增的 `Execution/Tool/ToolArgs.fs` 与 `Execution/Tool/ToolResultText.fs` 曾未登记 → **已由该线登记**（fsproj `…capability-enforcement.execution-tool-product.fsproj` + compile-order），已闭合；**当前**唯一未归属的是 HostNeutral 新增的 `Execution/Session/TerminalRegistry.fs` —— 它**已进 compile-order（790–791）但未进任何 fsproj**，故 `node scripts/build.mjs --plan` 仍拒绝。已通知该线。**下一轮重编的前置条件**：该文件登记进 owner fsproj → 等各线编译修复 ready → 一次刷新 + 一次重编。
  - **主树 HEAD** 更新为 `7cdd5f60d`（Integration 连推 5 个提交后），并说明工作区当前非空（约 55 项）多为各线在飞编辑、均未提交。
  - **HostNeutral 行**按其回报改写：本轮切片 = B1（dispatch 集群迁到中立 P-02 send + 产品侧 terminal 注册表），`RunLifecycle` / `AdvisorSeat` / `Bookkeeper` 三个订阅点由 Fork / Authority / Domains 各自接入；A1 `SessionInfo.Workspace` 已落地（OpenCode 侧），omp 侧与 TS 镜像由 SecondHost 同步；**本批 `Host/Ports/*` 公共接口未改动**，下一个空闲端口号仍是 P-20；A4 改为「方向已批准，typed shape 待裁定」（原写「已批准」）。并记下围栏为硬约束：订阅须带 exact ProviderRun / AuthorityRootUserMessageId 围栏，且**已关闭的 run 不得再被终态投递**（F39-02 的失败形态）。
  - **静态门禁行**更新为当前实测：pyramid 唯一命中已从 `Sphinx/V2/Hosts/OpenCode/Adapter.fs:63` 移到 `Persistence/Journal/PromptFactCodec.fs:305`（Authority 线），`adapter-thinness` 582/126（基线 689/147，只降不升）。
  - **快照刷新机制**登记：`.Herdchestra/takeover/refresh-snapshot.sh` 同步全部跟踪输入面，并打印编译输入摘要与 WHAT-016 验证输入摘要的前后值作为输入边界证据；库 commit 与 gitlink 不一致或库有本地改动即 fail closed。已跑三轮的摘要序列：`bf986b5f→36cfd68d→01caf035`（compiler）与 `15ea430f→d756d40a→1e260567`（WHAT-016 验证输入）。


## Unreleased — vs-006 控制金字塔切片落地（`SessionSnapshotPort.fs` 2 条 match-pyramid 清零）+ 006 红例精确诊断

- **代码落地（已推 `origin/master`）**：`a46af54fda09a53d420671f86d8fc74e5d3f9fdf`（范围 `a2d2787cb..a46af54fd`，1 个文件，+10/-14），提交标题 `refactor(opencode-host): name the session-row identity decision instead of nesting a match`。改动文件只有 `src/Wanxiangshu/OpenCode/Host/SessionSnapshotPort.fs`。
  - `sessionInfoOfRow`：原来的 `if isNull row || isNull row?id then None else match readString row?id with …` 改为复用本模块既有的 `sessionIdOfRow`，再用 `Option.map` 组装 record（`Parent = readString row?parentID |> Option.map SessionId.create`、`Role = readString row?agent |> Option.map AgentRole`、`Title = readString row?title`）。
  - `parentOfPayload`：原来的 `if isNull session then Error(…) else match readString session?parentID with …` 改为 `session?parentID |> readString |> Option.map SessionId.create |> Ok`。
  - 语义不变：`None` / `Error` 分支、错误串 `"Missing session response"`、字段取值均不变；`parentID` / `agent` / `title` 的宿主读取仍然只在 id 存在时发生（record 在 `Option.map` 内部构造）。四个被触及的函数都保持原名与元数，无新增未用符号。
  - **未改门禁**：`scripts/checks/fsharp-control-pyramid-baseline.json` 保持 `{"version":1,"files":{}}`；未加白名单、未改阈值、未改 scanner、未用「搬移 helper」消警。
- **门禁证据（静态，在推送的那棵树 `a46af54fd` 上跑的分离 worktree `/home/hermes/tmp/vs-006/land-doc`）**：`node scripts/checks/fsharp-control-pyramid.mjs` → `fsharp-control-pyramid: clean (0 nested decisions)`，exit 0；`node scripts/checks/architecture.mjs` → `architecture: OK`，exit 0。落地前同一门禁的红基线是 2 条：`SessionSnapshotPort.fs:444` 与 `:473`，均 `depth=2 chain=if → match`。
- **未验证（重要）**：本 lane **没有跑任何 F#/Fable/dotnet 编译**（`scripts/build.mjs`、`dotnet build`、`build-slot` 均未执行；仓主允许静态落地，任何编译须走 `build-slot`）。因此**没有编译或运行时 F# 证明**：`Option.map AgentRole` 是否通过类型检查（`type AgentRole = AgentRole of string`，`src/Wanxiangshu/Host/Ports/Vocabulary.fs:32`）、`|> Ok` 是否按返回标注推成 `Result<SessionId option, HostError>`、以及改动区的 offside 是否按原样编译，都**未**被编译器确认。**交付团队必须构建 master 并 type-check 该文件**（归口 shard：`…host-boundary.opencode-host-sessionquiescencegate`）。
- **落地路径说明**：本地提交 `9fcb45b23` 落在 `origin/master` 前进之前，推送被拒（非快进）。**没有**在共享的脏 worktree 里 rebase/reset；改用分离 worktree `git worktree add --detach … && git rebase origin/master`（exit 0），在 rebase 后的树上重跑两道静态门禁（均 exit 0）后 `git push origin HEAD:master`（`a2d2787cb..a46af54fd`，**无 force push**），随后删除临时 worktree。
- **006 红例的精确诊断（本条只记录诊断，不修红）**：`requirements/verification-system/tests/006.test.mjs` **仍然红**（本机三次完整快照：46 通过 / 13 失败、43 通过 / 16 失败、41 通过 / 18 失败，均 exit 1；失败集合随子进程竞态抖动，三次是同一 59 用例套件的独立快照，**不可相加**）。根因是**监督器的静默窗口在被监督的工作存在之前就被武装**：看门狗在 `…/tests/e2e/support/supervise-node-test.mjs:336-337` 以 `timeoutMs: silenceMs` 起表，内层 runner 在 `:368` 才 spawn，而第一个进度事件 `runner:file-start` 要到 `…/tests/support/run-inner.mjs:160`（`run()` 之后）才发。
  - **精确 z1 采样**（instrumented、放宽窗口的诊断跑，`silenceMs: 30000`，**不是验收配置**；`/home/hermes/tmp/vs-006/repro/runs/z1/timing.log`）：`spawn-inner` wall `1792631019784` → `runner:file-start` IPC `1792631021234`（**spawn 后 +1450 ms**）→ `body-enter` `1792631022582`（**+2798 ms**）→ 首个 `test:complete` `1792631022993`（**+3209 ms**）。未改的预算是 fixture 级 1000 ms（`…/tests/support/supervised-tool-reclamation-tests.mjs:248`、`…/tests/006.test.mjs:868`）与 suite 级 `UNIT_VERDICT_SILENCE_MS` = 5000 ms（`…/tests/e2e/support/time-budget.js:149`）：启动开销在任何用例体开始前就已超过 1000 ms 的 fixture 窗口。
  - **未改窗口的采样**（`silenceMs: 1000`，`/home/hermes/tmp/vs-006/repro/runs/s5/out.txt`）：`WATCHDOG: 'synchronous-verdicts' silent for 1030ms (limit 1000ms); 0 blocking progress update(s)`、`1 queued file(s) have not started`。两个采样合起来说明**启动预算被耗尽**。
  - **失败分类（按跑分列，各列自洽到该跑自己的失败数；三次跑是同一 59 用例套件的三个独立快照，不可相加）**：**run A**（`/home/hermes/tmp/vs-006/test006-with-redunit-dist.out`，46 通过 / 13 失败）= 7 条 ownership-marker / diagnostic-marker（监督者第一次 `inspectProcessTree` 约 +1105 ms，而 `tool-ownership.json` 约 +5416 ms 才写 ⇒ `…/tests/support/supervised-tool-reclamation-tests.mjs:152` ENOENT，caller catch `:254` 再一次，`caught.json`（`:257`）永不写出，`:307` 断言失败——这是叶子根本没跑到那一步的后果）+ 1 条文件流形状不符（`continuing worker cost IPC …` 期望 `/0 drained, 1 active, 0 queued/`，实得 `0 drained, 0 active, 1 queued`）+ 3 条静默窗口过期（`continuing body entry diagnostics …` 300 ms、`physical backstop diagnostics …` 5000 ms、`synchronous work and microtasks …` 1000 ms）+ 1 条缺 last-progress 标记（`transport scheduling cannot renew unfinished work or background noise`，期望 `/last progress: test:(?:pass|complete):synchronous work 7/`）+ 1 条裁决摘要正则不符（`transport scheduling preserves after-hook failure verdicts`）= **13**（7+1+3+1+1）；**run B**（`/home/hermes/tmp/vs-006/repro/full1.out`，43 通过 / 16 失败）= 上述 7 + 1 + 3 + 1 + 1，再加 1 条 `caught.json` ENOENT（`…/tests/support/normal-exit-residual-support.mjs:164` 读 `owned.json`，叶子在 5000 ms 静默被杀掉故未写，`caught.json`（`:166`）也就没出现；读点在 `…/tests/support/normal-exit-residual-tests.mjs:49`）+ 2 条额外静默诊断断言（`real silence diagnostics …` 期望 `/file streams: 1 drained, 1 active, 1 queued/`；`silence failure reclaims its process group …` 报「The real held leaf must have started before silence」）= **16**（7+1+3+1+1+1+2）；**run C**（`/home/hermes/tmp/vs-006/repro/full2.out`，41 通过 / 18 失败）= 上述 7 + 1 + 3 + 1 + 1 + 1 + 2，再加 1 条 ownership-marker（`invalid worker cost IPC leaves actual runner verdicts and process cleanup unchanged`）与 1 条裁决摘要正则不符（`worker cost diagnostics preserve actual success and assertion failure with a closed exit sink`，期望 `/0 passed, 1 failed; 1\/1 planned file\(s\) completed/`）= **18**（8+1+3+1+2+1+2）。
  - **teardown 里的 `ps` 超时**：run B / run C 中 `spawnSync ps ETIMEDOUT`（`…/tests/e2e/support/supervise-node-test.mjs:76`/`:82`/`:134`，撞上 2000 ms 的 `PROCESS_TREE_TIMEOUT_MS`，`…/tests/e2e/support/time-budget.js:229`）出现在**静默过期那几条失败之内**，是 teardown 症状，不是额外的失败用例——所以各跑的行数不另给它留位。
  - **不能说「失败全是启动」**：ownership/diagnostic-marker 与 `caught.json` 两类是同一预算耗尽的**下游后果**（叶子根本没跑到写 marker 那一步）；另有若干条在 **teardown** 里失败（`ps` 自己超时，见上一条），这是与启动竞态不同的症状，不能并入「只是启动」。
  - **对照跑只作诊断，不是通过验收**：同一 fixture 在 30000 ms 放宽窗口下 exit 0、8/8 通过并给出完整 `runner:summary`（`/home/hermes/tmp/vs-006/repro/runs/z1/out.txt`）；`run-inner.mjs` 单跑 8 passed / 0 failed。**在这两组对照跑（直接内层跑 + 放宽窗口跑）里没有丢失任何真实裁决**——该结论**仅限这两组对照跑，并未在未插桩的失败跑上全局证明**（在未插桩的跑里，fixture 是在裁决可被观测之前就被杀掉的）。红因是预算/次序问题；但 **30000 ms 是诊断仪器，绝不构成验收口径**，未改配置下套件仍然红。
  - 红是**负载相关**的：本机当时 loadavg 21.6/32.4/36.4（4 CPU），裸 `node -e 0` 需 361–748 ms；空闲机器上同 fixture 通过。
  - 迁移相关根因与接手边界现见 `AGENTS_MIGRATION.md`。**本条未改任何断言、预算或产品看门狗**，红例仍然敞开。

## Unreleased — GAP-075 / GAP-088 残余清单转真实 oracle（gap-075-088 切片）

- **范围**：把 [迁移指南](AGENTS_MIGRATION.md) CUT-41 登记在两条台账行的残余工作落成独立、可单独变红的测试，
  并在每条测试旁写清**根因**。**未改任何生产代码**（`src/` 与子模块 `wanxiangli` 均未动）。
- **GAP-075（`capability-enforcement`）**
  - `requirements/capability-enforcement/tests/025.test.mjs:422`
    `P15_concurrent_physical_rg_overlap_with_review_acceptance_admits_inflight_and_refuses_subsequent`：
    拦截 `processes.spawn` 的 `rg`，真实子进程起后 `SIGSTOP` 暂停（断言 `exitCode`/`signalCode` 均为 `null`，即调用确实在飞），
    窗口切换前落盘接纳评审 → 重叠的第二次调用返回 `REVIEW_ONLY`；`SIGCONT` 恢复 → 在飞调用放行
    （`code === undefined`、`data.exitCode === 0`）→ 其后调用仍 `REVIEW_ONLY`（`spawns === 1`）。
  - `requirements/capability-enforcement/tests/026.test.mjs:371`
    `D11_cross_process_devops_dispatch_capability_isolation` + 新支撑夹具
    `requirements/capability-enforcement/tests/support/devops-dispatch-isolation.fixture.mjs`
    （env `WXS_DEVOPS_ISOLATION_DIRECTORY` / `WXS_DEVOPS_ISOLATION_PHASE`，相名 `produce` / `recover`）：
    produce 相评审接纳前以 `assessment-pending-for-devops` 拒绝且 durable 快照逐字节不变、接纳后放行（`outcome = 'created'`）；
    recover 相在**另一个 OS 进程**重开同一工作区，未评审会话仍 `assessment-pending-for-devops`、已确认会话 `dispatch-closed`，
    两者 durable 增量均为零。夹具复用 `plugin-fixture.mjs` 的 `startPluginIncarnation(directory)`。
- **GAP-088（`repository-programming`）**——原 4 条 `test.todo` 全部转真测试，`006`/`017`/`025`/`027` 现共 **0 条 `test.todo`**：
  - `006.test.mjs:163` `JS054_3_steady_allocation_loop_is_contained_by_deadline_and_host_survives_for_subsequent_programs`——
    根因：`JsSandbox` 经 `node:vm` 进程内运行，V8 `resourceLimits` 不生效、无纯内存栅栏，
    `PROGRAM_RESOURCE_LIMIT`（`JsFailure.ProgramResourceLimit`）无任何生产者；实际约束是 `deadlineEpochMs` → `PROGRAM_TIMEOUT`。
  - `017.test.mjs:32` `JS017_concurrent_calls_in_one_assistant_turn_commit_in_deterministic_serial_order_without_lost_update`——
    根因：宿主按助理消息顺序逐个派发 `tool.execute`，各调用为独立事务 + CAS 新鲜度检查 → 按序串行、无丢失更新。
  - `025.test.mjs:325` `JS ${phase} actual EventStore port ${scenario} bad-input defense settles rollback or cut before one injected fatal incident`——
    结算 owner 是 EventStore append-payload store，追加 `ProjectionCutTail`（`rule: 'JsTransaction'`）；
    断言结算先于单次注入致命事件（`js-transaction-semantic-cut`，`SIGKILL`）、既有文件未被触碰、冷读进程可复核。
  - `025.test.mjs:413` `uncertain persistence and interrupted settlement preserve third-party changes across process restart`——
    根因：事实层从不改写工作区，`Prepared` 收据只是持久证据；跨进程冷启后第三方修改逐字保留。
- **验证口径（重要）**：**未在已编译 `dist/` 上真实运行**。主仓缺 `dist/`，
  `build-slot node scripts/build.mjs` 解析 1404 个源文件后触及 180 s 槽位上限（宿主负载 23.5–28.9），按站规停止编译。
  已做的全部是静态验证：7 个 `.mjs`（含新夹具 `capability-enforcement/tests/support/devops-dispatch-isolation.fixture.mjs`
 与既有 `repository-programming/tests/support/transaction-cut-child.mjs`）全部 `node --check` 通过，
  `node scripts/checks/architecture.mjs` → `architecture: OK`。
  故 **GAP-075 与 GAP-088 均保持 PARTIAL**，不改记 CLOSED。
- **台账与文档同步**：`requirements/GAP.md`（GAP-075 / GAP-088 两行改写为「已落地 oracle + 根因 + 静态验证边界」）、
  原 GAP-audit 与 CUT-41 看板/职责卡（迁移结论现统一见 `AGENTS_MIGRATION.md`）、
  `保姆级交接与展望.md`（2026-10-22 新段）。

## Unreleased — red-unit 测试线收编：027/006/prefix-stability/065/host-boundary-034

- **收编范围（6 个提交，已落 `origin/master`）**：本线（分支 `land/redunit-034`）把 `origin/wip/redunit`（tip `cec1235db`）上一条面向「发布门禁 unit 26 红」的修复栈逐条裁决后，只落 6 个提交，其余 4 个判为「master 已有更强版本」或「会复活已退休的 fake surface」，一律不落。
- **已落地的 6 个提交**：
  - `50b0d6dfc` test(delegation-027)：子回合在取回自己的 prompt 之前先等待 Host 侧 ingress（`plugin-fixture.mjs` 加 `__awaitIngress`/`admitted`，`real-fork.mjs` 的 `childTurn` 顶部 `await runtime.awaitIngress()`）。
  - `5413bdf57` test(delegation-006)：placement/resume 用例迁到真实交付路径。
  - `6ee673640` test(prefix-stability-001)：子方在 `delegation.settle` 之前先 `recordFencedDeliveredView`。
  - `64f5373ef` test(repository-programming)：删除 `requirements/repository-programming/tests/065.test.mjs`——该包 WHAT 只到 060，065 是无条款的过期重复文件。
  - `3a02cc040` test(verification-system-006)：被持有的工具在写 ownership marker 之前先观察自己的**同一时刻** live identity（一次 `ps -o pid=,pgid=` 读三份身份，5 ms 步进、2 s 硬顶），取不到就大声失败而非写进 NaN 身份的 partial marker。
  - `4e7f5e500` spec(host-boundary-034)：补上缺失的 `## [034] adapter 薄度与 Host 命名空间层级的门禁契约` 条款（`requirements/host-boundary/WHAT.md`，3 段规范性文字）并把 `requirements/INDEX.md` 计数 33→34——这是 requirement-system-017 全树扫描报告 `034 is absent` 的根因，条款号 034 永久占用、不复用。
- **4 个被放弃的提交及理由**（都在 `cec1235db` 上，判为不落）：
  - delegation-032（`8d4c788d7`）：master `9fa7c1a6c` 更强——稳定的拒绝码 `assessment-pending-for-devops`/`unknown-calling`，分支只断言 `ok = false`。
  - effect-accounting-013（`cec1235db`）：master `1032521d2` 用 `ToolRuntimeScopeSurface.establishedDeliveryCallId : string option`，分支把整个 surface 换成 `establishedDelivery` record，是降级。
  - effect-accounting-002（`a4ef9f392`）：master `24dc3dc2b` **故意退役** `HostForkRunLifecycleSurface`（fake `journal=None` surface）；落地分支版会复活该文件并回退 master 的 real-fork 证明（`696afe6be`）。
  - delegation-026（`defa8641f`）：master `7b3d72464` 重做了同一用例并**保留**分支丢弃的 ghost-run bounded-join 断言；master 更强，无可落内容。
- **`origin/wip/redunit`（tip `cec1235db`）已归档为本地 tag `archive/wip-redunit-pre-delete`（`cec1235dbf9f3812846a4eea5d852c4c907939d2`），远端分支已删除**。归档是**本地** tag，不 push 到远端（`git ls-remote --tags origin` 无此项）。
- **逐路径清点（如实记录，非「0 个未合并内容」的强断言）**：`git diff cec1235db origin/master` 计 391 个不同路径（105 A / 9 D / 248 M + 29 处重命名）；`-M -C` 视角下只有 9 个「wip 独占」路径是 `D`（master 已删除）——即 `065.test.mjs` 与 `HostForkRunLifecycleSurface`/`DelegatedToolEstimate` 及 4 个 `*.fsproj`，恰好是被放弃提交会引入/复活的产物。**但**全树（`ls-tree` 逐 blob）视角下 wip 另有约 37 个「wip 独有 blob」路径是 `OpenCode/Host/*`、`OpenCode/Join*` 等已被 CUT-34/40/HNI-08 迁移出 `OpenCode/` 命名空间、blob 内容已留在 master 历史的文件——它们不是本线内容，而是 wip 分支停在被迁移前的旧基线上，所以**不能**简单说「wip 相对 master 只差这 9 条」。收编口径是：本线的 6 条已落，4 条被放弃，分支整体作废归档；不声称两条分支其余内容逐字节一致。
- **未验证（重要）**：本 lane **没有**跑 F#/Fable 构建、没有重建 `dist/`、没有执行全量 unit 门禁、没有跑 `scripts/check.mjs`。已跑过并得到结果的是：`node --test requirements/requirement-system/tests/017.test.mjs`（2/2 通过）、`node --test requirements/host-boundary/tests/034.test.mjs`（9/9 通过）、`node scripts/checks/architecture.mjs`（`architecture: OK`，exit 0；在推上 tip `4e7f5e500` 的 scratch 分离工作树上、复用仓库 `node_modules` 软链跑的）；`verification-system-006` 也单独跑过，**仍红**（本机 59 用例 44/45 通过、15/14 失败，见《保姆级交接与展望》§2.2 的如实记录），红因是 `silenceMs: 1000` 的 watchdog 与 Node 子进程启动的竞态，非本 lane 引入。其余套件（delegation-006/026/027/032、prefix-stability-001、effect-accounting）本 lane 均未单独跑。绿不绿由正式 runner 给出，本条不把「收编」等同于「门禁转绿」。

## Unreleased — master-red：恢复 origin/master 的编译（5 处 F# 编译错误 + 2 处 fsproj 损坏 + 1 处套件清单）

- **五个 F# 编译错误修复**（静态验证，非编译验证；见「验证口径」）：
  - `4c84009c2`：`Execution/Delegation/Fork/JoinRun.fsi` 补回 `[<Literal>]`（实现侧 `JoinRun.fs` 的 `let [<Literal>] DevOpsJoinTimeoutMs` 与签名侧 `val DevOpsJoinTimeoutMs: int` 必须属性一致，否则 FS0349）；`OpenCode/Host/SessionSnapshotPort.fs:402` 把 `found |> Option.toList @ sessions` 改为 `found |> Option.toList |> List.append sessions`（`@` 比 `|>` 紧，原式解析成 `found |> (Option.toList @ sessions)`，类型对不上，FS0001 ×2）。
  - `94218a2be`：`Execution/Delegation/Fork/Command.fs:1307` 保留类型化 `Ok handleId`（`HandleId.tryAgent` 本就返回 `AgentHandleId option`）；`OpenCode/Host/SessionsSurface.fs:500-503` 把裸 `Failed`/`Aborted`/`Completed` 显式限定为 `TerminalOutcome.*`（否则绑定到 `Wanxiangshu.Host.Ports.FissionPtyStatus` 而非 `Foundation.TerminalSignal.TerminalOutcome`）；`OpenCode/Plugin/PluginHostWiring.fs` 恢复 `open Wanxiangshu.Execution.Session.ChatExecution` 并缩短一个持久化引用（per-file 产品域引用数维持 11，未改任何基线）。
- **两个 fsproj 损坏修复**（均为良构 XML 修复，编译项路径不变）：
  - `46cf4cab9`：`…session-ontology.opencode-host-sessionbindingsurface.fsproj` 把落在 `</Project>` 之后的 `host-neutral-ports.fsproj` `ProjectReference` 移回 `ItemGroup`（原先 MSB4025 多根元素）。
  - `d0959adb2`：`…intra-participant-parallelism.execution-fission-opencode-host.fsproj` 补回缺失的 `</ItemGroup></Project>`（`4403f579b` 引入、`e6cf370e7` 暴露）。
  - 修复后全仓 **242 个** shard `fsproj` 经 sax + 独立 Python ElementTree 两套解析器验证**全部良构**（之前 2 个损坏）；两文件的 item 路径多重集不变（18→18、52→52）。**只改包容器，未改端口登记**——下一空闲端口 id 仍为 **P-19**。
- **套件清单修复** `d55fa1eff`：`scripts/suites-manifest.sh`（新增）把组表赋给 bash 只读特殊数组 `GROUPS` 会被静默替换成调用方的补充组 id，导致无参运行跳过所有组；改名为 `SUITE_GROUPS`。同时运行前 unset 继承的 `WXS_TIER_INTEGRATION` / `WXS_TIER_RELEASE`（继承的 tier 不得让任何测试在 build-slot 外编译），并让 `REPO`/`LOGDIR` 默认由脚本位置推导。
- **验证口径（重要）**：**没有任何一个提交做过编译或测试。** 编译槽在 ~3 分钟机器级上限下被杀死（`build-slot` exit 124/143；直接 `fsc.dll` 只报 4 条 FS0084 netstandard facade 引用错误，类型检查本身未决出）。全部证据是静态的：fantomas（exit 0）、`architecture.mjs`（OK）、`adapter-thinness.mjs`（product-reference 595 ≤ baseline 689，无违规）、`fsharp-control-pyramid`（findings 均为既存 `SessionSnapshotPort.fs`）。**交付团队必须构建 master 并跑 requirement 套件。**
  - 各修复文件归口 shard（交付团队按此逐个 type-check）：JoinRun/Command → `…delegation.execution-delegation-hostturnobservedsurface`（闭包 153/880）；SessionSnapshotPort → `…host-boundary.opencode-host-sessionquiescencegate`（最小，29/146）；SessionsSurface → `…session-ontology.opencode-host-sessionbindingsurface`（82/404）；PluginHostWiring → `…opencode-plugin.plugin-composition`（216/1252）。

## Unreleased — 2026-10-21 作废裁决：未交付的子工作以作废结清，而非完成

- **裁决全文**：未交付的子工作**永不写完成**，但**可以**写一个作废（void）。作废**像完成一样结清那份义务**。落在实现上是：重启加载时，上一进程遗留、本进程无法继续持有的孤儿子工作在加载期被作废——有 scope 的已准入工作追加 `ChildWorkVoided`（具体工作单元格 `Works[exact id]` 转 `Abandoned(Voided)`，其 `activeLogicalRun` 清空，它承载的那份**工作义务已是终态**；底层 handle 绑定仍**可复用**，它标识可再次参与的参与者身份，不是被作废的那份工作，`summary` 投影叠盖工作状态并读出终态），合法但无 scope 的历史追加 `ChildRunVoided`（无独立 work 单元格，被作废的就是该 unscoped handle 绑定本身，它转 `Abandoned(Voided)`）。**两类作废事实都走 abandon，没有一类是 abandon 规则的例外**，差别只在于被 abandon 的是工作单元格还是 handle 绑定；两者都结清工作义务生命周期，终态**绝不**是 `Completed*`。**作废不是完成**：不写完成正文，也不把已有交付视图当作完成凭据。
- **可见性与结清**：作废后该工作在 horizon 名册里显示为 `voided`（`resources/provider/tool/horizon/voided/*`，`Path.Voided = "tool/horizon/voided"`），join 报告它**恰好一次**，状态为 `voided`（`resources/provider/tool/join/agent-voided/*`，`Path.AgentVoided = "tool/join/agent-voided"`），**既不**是 `completed` **也不是** `undelivered`；报告之后该工作退役（`Retired`）、离开名册。结清是幂等的：每个孤儿子工作在其一生中只产生一条作废事实，作废的工作永不产生完成事实；**重启的进程不得再次作废同一份孤儿工作**（journal 里不会出现第二条作废事实，join 也不再重复报告）；报告之后该 handle 经 `durableLifecycleByname` 读出 `Retired`——**这是经 `summary` 投影读出的终态**，不是从原始 handle 绑定直接读出的。
- **文档侧同步**：delegation、crash-reconciliation、participant-horizon、repository-programming 的条款与 `tests/README.md`、模型侧文案（`resources/provider/tool/js-program/**`）、抽吸看板 CUT-35 行、`requirements/GAP.md` GAP-218 与 `proposals/W2-实施-WP-025-horizon泄露重置-2026-10-10.md` 已按此合同改写。
- **未验证（重要）**：本条只记录**文档合同**，不记任何测试结论。加载期作废接线、void 报告恰好一次、重启后不重复、交付过又被 abort 的子工作报 `voided` 且 join 关闭，这些**行为**结论由行为泳道与正式 runner 给出；本 lane **没有跑 F# 构建、没有重建 dist、没有执行任何 requirement 套件**。
- **取代的旧口径**：此前「重启后遗留子工作保持活跃、join 跨重启保持打开、`horizon` 与 `join` 对它都为空」的口径**已被本裁决取代**；相关旧段落已在 2026-10-21 的改写中移除或改写，历史条目按 append-only 原则保留原样，只作历史记录。
- **作废的持久原因已入法律集合（补记）**：`Voided` 是 `HandleAbandonReason` 的**第四个合法构造子**，`wanxiangli/handle-lifecycle` 的 `recoverChild()` 接受的 `durableReason` 全集是 `ParentCancelled`｜`DeadlineExceeded`｜`HostSessionGone`｜`Voided`；**已被 2026-10-21 作废裁决取代的旧口径**是「只有前三项」。库侧已发布 0.66.0（`663ba59`）接受 `Voided`。**主仓的 `wanxiangli` gitlink 也已随 `cfa8d0dad` 前移到 0.66.0（`663ba59`）**（该 bump 已随行为泳道的 `6ccc761c2` 进入 `origin/master`），落地链上这一环**已闭合**。

## Unreleased — 委派 032/026：post-rebase 验证缺口已闭合（真编译 + 真跑 runner）

- **缺口已闭合**。上一版记录的是「四个套件的裁决量在 rebase 之前的构建上」；本轮**重新编译**（`build-slot node scripts/build.mjs` → `[build] build ok (generation 3)`，151 surfaces / 734 modules，**没有** `--skip-staleness-check`）后，在**同一棵能编译通过的 upstream 树 `aa43d3455`** 上重跑了正式 runner。
- **被测树**：`aa43d3455`（`refactor(HNI-06): join wire rules leave the OpenCode adapter for host-neutral Fork/JoinRules`），是 `origin/master` 的祖先（`merge-base --is-ancestor` → exit 0），本线五个提交全在其中。
- **正式 runner 裁决**（`UNIT_VERDICT_SILENCE_MS=30000 NODE_TEST_CONCURRENCY=2`，`TESTS_MJS_FILES` 逗号分隔四个文件，槽外、不重编、**不放宽任何期限**）：`[test-summary] 4 file(s), 22 passed, 3 failed, 2 skipped, 1 todo (92.24s wall, 123.21s test time)`。逐文件：`032 = 3 passed / 0 failed`（12.53 s）、`026 = 5 passed / 1 failed / 1 todo`（18.12 s）、`024 = 7 passed / 0 failed`（29.76 s）、`027 = 7 passed / 2 failed / 2 skipped`（51.78 s）。
- **与 rebase 之前的数字逐项一致**：合并总数与失败用例名（`026` case 6 `RESTART_PENDING_CHILD_WORK`、`027` 两条 `prepared busy guidance`）**完全相同** ⇒ 先前报告的裁决在重新编译后**成立**。
- **同树复跑的门禁**：`node scripts/checks/architecture.mjs` → `architecture: OK`；smoke（仓库外）→ exit 0，`devopsRefusalCode: "unknown-calling"` / `deliveryWitnesses: 1`；`package-lock.json` 解析正常（lockfileVersion 3）；`wanxiangli` gitlink `fde10a080e86c80c3db05c73ecdfdbc4ef2c2e37` **等于**其 `origin/main` 且是祖先（不比 master 新），子模块干净。
- **没有闭合到最新 tip，原因在 upstream 自身**：`origin/master` 的 tip（`e6cf370e7` → 之后 `f6f142d5a`）**编译不过**，错误全在别的 lane 的文件里（`Execution/Delegation/Fork/JoinRun.fs`/`.fsi` 的 `[<Literal>]` 签名失配、`OpenCode/Plugin/PluginHostWiring.fs` 缺 `HostProviderTerminalOutcome` 的 open、`OpenCode/Host/SessionSnapshotPort.fs` / `SessionsSurface.fs` 的类型与模式匹配、`Execution/Delegation/Fork/Command.fs` 的 `AgentHandleId` 类型）。**本线没有改动任何 `.fs`**（自 `d82b3e131` 起只改 `.md`），且一份 pristine clone 的报错文件 blob 与 upstream **逐一相同**，故该失败**不是本线造成的**。上游至今未修（这些 site 在其后的提交里一个都没被改）。
- **仍红、未修的项（如实列出）**：`027` 的 2 条 `prepared busy guidance`（EMR-010，既存，非本线引入）；`026` case 6（`RESTART_PENDING_CHILD_WORK`，归 ruling-void，本轮未动其任何文件）。**最新 tip 上的四套件裁决未测**（编译不过）——**没有**用 `--skip-staleness-check` 或任何绕过手段凑数。
- **改动文件**：`CHANGELOG.md`、`proposals/TODO施工分册-2026-10-03/05-逐项清单.md`（T117 / T112 行）、`保姆级交接与展望.md`（未完事项 #8 的实测结果）。**不含任何 `.mjs` 断言改动**。
- **未验证（重要）**：本提交**只改 `.md`**，会让构建的 generated digest 再次失配 ⇒ 落地后需再跑一次 `build-slot node scripts/build.mjs` 刷新 manifest，正式 runner 才愿意开跑。上面那批数字对的是**本提交之前**的那棵树。


## Unreleased — 委派 027：EMR-010 模型路由租约缺失根因修复与 installed-Host 产品缺口定位

- **EMR-010 修复（`a38ef7770`）**：`requirements/delegation/tests/027.test.mjs` 中用例 `prepared busy guidance for Ada/devops stays bound to the original active work` 报错 `EMR-010: managed provider step for physical user message msg-host-child-2-...-2 has no committed model-routing lease`。根因已确认：`HostSignalBootstrap.fs` 原 `admissionInFlight` 仅以 `ChatExecutionKey` 为索引，将针对同一物理消息 ID 的 `PublishInput`（purpose 0，无模型租约，生成 `DeferredInput`）与后续 `EnterProviderStep`（purpose 1，需领取/续签模型租约）合并到了同一个飞行任务中，导致 provider step 直接采纳前者的无租约结果跳过领租，后续在 `SessionExecutionBinding.fs:116` 抛出 EMR-010。
- **实现改动**：`src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs` 将 `admissionInFlight` 改为由 `(ChatExecutionKey * ChatAdmissionPurpose)` 二元组索引，使两类不同目的的准入飞行独立调度，互不吞没；二度 `Accept` 经由 `Acceptance.fs` 保持幂等。
- **历史追溯与排查（静态历史分析，未做提交级运行二分）**：依据 `evidence/09-history.txt` 与 `dbg-admit.log`，该缺陷实为 `48a0bf426`（2026-10-07，引入 Dual Admission Purpose 时遗留的飞行表键值设计缺陷）引入的潜伏 Bug，因 `c0246c36d`（2026-10-21，切换到 `real-fork.mjs` 并引入并发第 2 笔 guidance）而被触发暴露。此前怀疑的 `6b7ae3a46`（wanxiangli 0.65.0 bump）、`cf1f0d2a9`（测试集自身修改）、`e058233a3`（顾问渲染）并非结构性源头缺陷（沙箱调度等时序影响未测）。`suite-reds` 报告中将其归为 pre-existing 系相对基线提交 `07173a147` 而言。
- **installed-Host 两条 Canaries 缺口定位（GUIDANCE / GUIDANCE_STOP，产品缺口交接）**：027 中两条 `installed Host releases Manager Join...` 测试在缺少 `INTEGRATION_TIER=1` 时被跳过；在真实宿主运行日志（`dbg027.err`）中确认为产品生命周期结算缺口：子会话投递步（ProviderRun `8WnS5D`）成功提交了持久交付见证（`TaskDeliveryCommitted`），随后按设计由投递后中断（`DeliverTool.closeAndArmDelivered` / `PluginTransforms.fs:issueDeliveryInterrupt`）将后继步（ProviderRun `Z49eiW`）中止为 `Aborted`。`RunLifecycle.complete`（lines 841, 805）将 `Aborted` 视为仅观测事实，且要求结算匹配同一 run ID 的终态，导致工作单元永远处于 `Active`、Manager 的 Join 永远不闭合而超时。未交付的后继步绝不能合成完成或持久作废（Failed/CANCELLED/observation 绝非 durable void）；桥接方案需在保证 scope/authority 及精确见证 fence 下由交付见证结合设计内中断结算该工作单元（该方案未实现、未验证，留待后续泳道）。另外，日志中偶发的 5000 ms 宿主启动超时因 stdout/stderr 均为空，确切原因未知（推测为机器高负载争用），不能断言为缺 postinstall 链接，亦不得放宽超时。
- **验证与限制**：在打入补丁的 `dist/`（携带 `flightKeyOf` 区分 purpose）上复测，针对性用例 2 条全绿（`pass 2 / fail 0`），全文件测试 9 通过 / 0 失败 / 2 跳过。因机器满载构建保护，未执行全量 1400 源码文件的重新编译；`dist/` 验证仅作为语义行为证据。门禁 `scripts/checks/architecture.mjs` 绿。

## Unreleased — 013 的 Boot 装配面改为惰性导入（首裁决提前，但 tier 静默窗口的结构性缺口未闭合）

- **修复**：`requirements/knowledge-reuse/tests/013.test.mjs` 顶层静态 import 的 `dist/OpenCode/Plugin/PluginHostWiringSurface.js`（实测静态 import 图 **3340 ms** 的模块求值成本，391 个 dist 模块 / 4,050,480 B）改为**惰性**导入（`let hostFinalizeModule` + `loadHostFinalize()`），只在真正需要它的三个异步用例体内解析。`??=` 只在 `await` 成功后才赋值，故 import 失败不会缓存被拒的 promise，也不会留下半初始化的模块。
- **判据**：`verdict-feed.mjs` 的 `BLOCKING` 集合只有 `test:pass` / `test:fail` / `test:complete`；`test:stdout` / `test:stderr` / `test:diagnostic` 是后台事件，**不续窗**。unit 层看门狗是**整个 tier 共享**的 5,000 ms 窗口。
- **验收（改动前基线上的实测，即 rebased 之前的那棵 dist）**：六个文件**各自独立** `node --test` 直跑全绿 —— 011 tests 8 / pass 7 / todo 1（16,223 ms）、012 tests 5 / pass 5（2,993 ms）、013 tests 32 / pass 31 / todo 1（69,225 ms）、014 tests 3 / pass 2 / todo 1（1,088 ms）、015 tests 3 / pass 3（4,636 ms）、016 tests 15 / pass 15（4,805 ms），全部 exit 0。门禁 `node scripts/lib/test-surface-scan.mjs` → exit 0 且静默；`node scripts/checks/js-surface-manifest.mjs` → `OK — 149 registered surfaces`。
- **如实记录的边界（未闭合）**：**scoped tier 仍然红**。`TESTS_MJS_FILES=...011..016.test.mjs node requirements/verification-system/tests/run.mjs --skip-staleness-check` → exit 1（35,425 ms，load 13.75），`WATCHDOG: 'tests/unit' silent for 5001ms (limit 5000ms); 127 blocking progress update(s), last progress: test:pass:WHAT[knowledge-reuse-013] original Host finalization observer-throws without cuts retains the ordinary settlement after actual Capture`，`runner: 1 file(s) had not reported completion` / `5 drained, 1 active`（active = `013.test.mjs`）。原因是**结构性的**：013 的四个 Boot leaf 各自 await **一个不可中断的真实子进程探针**，实测 **10,151–11,814 ms**，其内部**不可能**产生裁决（子测试的裁决也只在**其结束时**才发），因此**任何保留断言的测试改动都无法满足这个 tier 全局窗口**。
- **两个合法出路（都不在本 lane 范围）**：① 把真实进程 leaf 移进 integration harness 层，其 case 静默预算是 20,000 ms（`time-budget.js` 的 `HARNESS_CASE_SILENCE_MS`）；② 给验证系统一条针对子进程工作的**真实因果进展通道**（不是心跳字节）。
- **本 lane 的额外上下文**：全量 unit tier 在**冻结基线**上本场就已经是红的（`762 test file(s)`，`27/762 planned file(s) completed`，同一个看门狗），所以 011–016 的连带取消是**既有 tier 级症状**，不是这六个文件自身的缺陷。
- **改动文件**：`requirements/knowledge-reuse/tests/013.test.mjs`。
- **未验证（重要）**：本轮**没有跑 F# 构建**，**没有**从新基线重建 `dist`，**没有**跑全量 unit tier。上面的运行数对的是**改动前那棵 dist**。本提交已 rebase 到上游 `e352aef71`（CUT-40 把 `runProgram` 从 `BookkeeperSurface` 拆到新的 `dist/Repository/Knowledge/Casebook/OpenCode/BookkeeperToolSurface.js`），**新基线的 `dist` 因此是陈旧的**：`node --test` 直跑 011 / 013 / 015 / 016 现在会在 import 期就以 `ERR_MODULE_NOT_FOUND: .../BookkeeperToolSurface.js` 失败（012 / 014 通过），`node scripts/checks/js-surface-manifest.mjs` 现在报 8 个 `missing emitted surface`（含 `Execution/Fission/Canaries.js`、`Execution/Session/Attachment/SatelliteSurface.js`、`Execution/Delegation/Fork/JoinResultRendererSurface.js` 等上游新增面）。**尝试用 `build-slot` 重建 `dist` 失败**：两个编译槽在 180 s 内都没抢到，命令以 **exit 75**（"机器负载太重，请以静态代码分析替代实际编译测试"）退出。**因此本次落地的内容在**新基线的 `dist`** 上尚未被验证**，需要 owner 在可编译的机器上重建 `dist` 后复跑这六个文件与 scoped tier。


## Unreleased — 委派 032/026 落地：push 手法的如实更正

- **本线曾发出过一条 force push 命令，被拒绝，远端历史未被改写**（如实记录，不作豁免）。命令为 `git push --force-with-lease=refs/heads/master:7f17f9bb9 origin HEAD:master`，输出 `! [rejected] HEAD -> master (stale info)`（exit 1，**什么也没推上去**）；起因是 `7f17f9bb9` 推上去后别人又推了 master、本地 HEAD 落后、lease 过期。**该命令本身违反「NEVER force-push」的规矩**——哪怕被拒、哪怕无害；改用「普通 push 被拒 → rebase → 再普通 push」可达成同样结果，后来走的正是这条路。
- **远端历史完好的证据**：`git merge-base --is-ancestor 7f17f9bb9 origin/master` → exit 0；`git reflog show origin/master` 全部条目只有 `fast-forward` / `update by push`（**无** `forced-update`）；本线四个提交在 `540f45007..origin/master` 区间里一个不少、无重复；`git ls-remote --heads origin` 里没有本线建的分支。两次落地 push 均为 fast-forward：`540f45007..7f17f9bb9`、`f267c8645..6c9ee4f92`。完整审计见 lane `deleg-032` 的 `audit-force.log`。

## Unreleased — 委派 026 的准入用例改走真实插件接线（case 1 移植 red-unit `defa8641f`）

- **`requirements/delegation/tests/026.test.mjs` 的 case 1（`RESUME_synchronous_admission`）改走真实插件路径**（`support/real-fork.mjs`），用例正文**逐字移植自 red-unit 的 `defa8641f`**（同一份内容，只差一个空行），并只取该提交在 `requirements/verification-system/tests/support/plugin-fixture.mjs` 里的 `nextPromptReceiptOnly` 片段，别无他物。`real-fork.mjs` 未改。
- **cases 2–5 的无界 await 收敛为有界 join**：新增 `boundedJoin` 助手（1500 ms 窗口，**永不放宽**，timer 在 `finally` 清理）。case 2 保留原有六条断言并各加一条有界 join 断言；case 3 原本无界的 join 改为有界；cases 4/5 换掉各自内联的 `Promise.race` 与不清理的 timer。
- **case 6（`RESTART_PENDING_CHILD_WORK`）一字未动**，仍属既有产品缺口。
- **验收**：正式 runner（`requirements/verification-system/tests/run.mjs` + `TESTS_MJS_FILES`，`UNIT_VERDICT_SILENCE_MS=30000`）下 `delegation/026` → **pass 5 / fail 1**，唯一失败是 case 6（既有缺口，非本轮引入）；改动前该文件 case 1 挂到 60 s 上限并连累后续 case 取消。回归 `024` → 全绿，`032` → 3/3，`027` → 5 pass / 4 fail（与本批无关的既存失败）。
- **改动文件**：`requirements/delegation/tests/026.test.mjs`、`requirements/verification-system/tests/support/plugin-fixture.mjs`。

## Unreleased — 委派 032 迁到真实交付路径（跨角色差遣在角色边界上被拒，不落到别的门）

- **`requirements/delegation/tests/032.test.mjs` 迁到真实插件路径**：跨角色差遣那一条不再走旧 fork harness 的 `executeManagerFork` / `settle`（隔离的进程内 runtime，**不是**生产的差遣入口），改为真实 `todowrite-for-manager` 的 `this.fork` 成员（生产裁决放行与拒绝）。原来的两条（角色→权限投影、SyncDelegate 词汇）**原样保留**。
- **拒绝按稳定码断言，不再用 `ok = false`**：拒绝码取自数据信封的 `code` 字段（渲染后的 TOML `[data]` 表），与 `capability-enforcement/026` 同一读法。角色边界那条断言 `code = "unknown-calling"`（`ManagedCatalog.fs:71` 的 `managerForkableRoles = [ Role.Engineer ]` + `Tool.fs` 的 `derivedForkCalling` / `managerCallingBindings` / `resolveDerivedManagedCalling`）。
- **对照证明拒绝来自角色边界而不是别的门**：同一条 DevOps 差遣在评估未落时停在 `code = "assessment-pending-for-devops"`（生命周期门），评估经生产命令面落盘后改停在 `unknown-calling`（角色门）。同一调用、两个不同的码 ⇒ 后一次确实是角色边界。
- **原断言集全部保留**：被拒的跨角色差遣不建子会话（`createdIds` 不变）、不发 prompt（prompt 计数不变、charge 不落在任何会话）、不留 durable 把手（**冷** journal 折出的工作单元只有 Engineer 那一个）。
- **证据无伪造**：评估由生产的 `seedReviewAs` → `work?execute` 命令面落下（`Mission/Lifecycle/Surface.fs` / `DurableWorkHost.fs`），交付见证由生产的 settle 侧落下；测试与 smoke 只**读** durable 事实（冷工作快照、`TaskDeliveryCommitted`）。本批不写任何 durable 见证。
- **验收（本 clone 自建 dist，`[build] build ok` + `.fable-build/build-manifest.json`）**：正式 runner（`requirements/verification-system/tests/run.mjs` + `TESTS_MJS_FILES`，`UNIT_VERDICT_SILENCE_MS=30000`）下 `delegation/032` → **3 passed / 0 failed**；`node --test` 直跑同文件 → tests 3 / pass 3 / fail 0 / cancelled 0（约 20 s）。**基线对照**：同一 dist 上 origin 版 `032.test.mjs` → tests 3 / pass 2 / fail 0 / **cancelled 1**（挂到 300 s 上限，即本批要修的真实红）。
- **改动文件**：`requirements/delegation/tests/032.test.mjs`（生产路径 smoke 脚本不入仓：它是本轮的一次性取证脚本，不是被测资产）。
## Unreleased — 冷读者不再替整个 Boot 图买单（knowledge-reuse 冷分支只加载自己用得到的图）

- **修复：冷读者进程不再静态加载它根本不用的 Boot 图**。`requirements/knowledge-reuse/tests/support/boot-capture-physical-child.mjs` 此前在**模块顶层**静态 import 了 `IndexSurface` / `LifecycleSurface` / `BookkeeperSurface` / `PluginHostWiringSurface` / `casebook.mjs` / `bookkeeper-session-support.mjs`，而这些**只有 `measure` 分支**用；`cold` 分支（一个必须从「只有 durable 字节」起步的独立进程）因为这些顶层 import，被迫在真正读盘之前先付掉整个 Boot 依赖图的加载代价——**一个冷起点先变成了第二次 Boot**，这恰好抵消了「冷读者独立观测」这个测试的全部意义。
- **改动形状：只搬 import 位置，不动任何断言、期限、进程独立性**。measure 分支改为在自己的分支内按**模块图原有的求值顺序**动态 import（Index → Lifecycle → Bookkeeper → PluginHostWiring → `casebook.mjs` → `bookkeeper-session-support.mjs`），`assert.equal(observed, 1)` 这类顺序敏感的断言的求值顺序**逐条保持不变**。canonical Q/A 抽到新的 `support/canonical.mjs`（**无任何 `dist` import**），供冷读者与 `bookkeeper-session-support.mjs` 共用；`bookkeeper-session-support.mjs` 改为 re-export，其余 13 个 import 它的测试文件**无需改动**。冷读者仍是**独立进程**，冷分支仍只加载 `EventStore/Surface.js` + `Casebook/Surface.js`。
- **静态分析佐证（不依赖运行时）**：按 `from "..."` 逐层遍历，013 全部 8 个入口的传递图 = **419 个 dist 模块**；其中冷分支只需 **163 个**，measure 分支需 **415 个**。即本次改动让冷分支少加载 **252 个模块 / 3,099,165 字节**的模块图，且 `IndexSurface` / `LifecycleSurface` / `BookkeeperSurface` / `PluginHostWiringSurface` **均不在冷分支的传递图内**。
- **实测（load 已随每次测量一并记录；本机 4 CPU，整场 load 30–110，负载来自其他租户的 `dotnet pack` / `fsc.dll`）**：冷分支 import 图 **11,556 / 4,505 / 5,035 / 4,856 / 5,873 / 6,321 ms**（load 54–78）vs 改动前同一进程要付的整图 **26,727 / 17,824 / 15,759 / 15,662 / 14,375 / 12,828 ms**（同 load 区间）——**约 3 倍差距**。
- **验收**：`node --test requirements/knowledge-reuse/tests/013.test.mjs` → **tests 32 / pass 31 / fail 0 / cancelled 0 / skipped 0 / todo 1**（`GAP-160` 的 `test.todo` 原样保留），duration 149,625 ms（load 96–110）。
- **如实记录的未闭合项**：**本次改动不保证消除 `requirements/knowledge-reuse/011–016` 的连带取消**。unit 层的裁决静默看门狗是**整个 tier 共享**的 5,000 ms 窗口（`UNIT_VERDICT_SILENCE_MS`），只有 **blocking** 裁决（`test:pass` / `test:fail` / `test:complete`）能续窗；`test:stdout` / `test:stderr` / `test:diagnostic` 是后台事件，**不续窗**。因此：①**首个** verdict 之前必须过完整条 import 链，而 013 自身的 import 图就要 4,967 ms（011）到 10,526 ms（013，改动前实测）；②**任意单个 leaf** 超过 5,000 ms 也会 breach 同一个 tier 全局窗口——013 的四个 Boot leaf 在 load 40–41 下实测 **5,900 / 5,850 / 6,680 / 6,690 ms**（bundled 实验口径），在本次 load 96–110 的实测里更达 **20,188 / 26,087 / 24,938 / 20,763 ms**。**在负载高的机器上，Boot leaf 仍可超过 5 s，连带取消的症状会继续出现**；本次改动只把**冷读者**这一段的成本降下来，不动 Boot leaf 本身。
- **本次实测的连带取消复现**：`TESTS_MJS_FILES=requirements/knowledge-reuse/tests/011.test.mjs,...,016.test.mjs node requirements/verification-system/tests/run.mjs --skip-staleness-check` → **exit 1**，日志 `WATCHDOG: 'tests/unit' silent for 5006ms (limit 5000ms); 0 blocking progress update(s)`（load 90–96）。**该症状在本次改动后依旧存在**——如实记录，不作豁免。
- **改动文件**：`requirements/knowledge-reuse/tests/support/boot-capture-physical-child.mjs`、`requirements/knowledge-reuse/tests/support/bookkeeper-session-support.mjs`、`requirements/knowledge-reuse/tests/support/canonical.mjs`（新增）、`CHANGELOG.md`。

## Unreleased — effect-accounting-002 的「保持 pending」改到真实 fork 路径上证明

- **问题**：`requirements/effect-accounting/tests/002.test.mjs` 整文件挂起。它在伪造面 `HostForkRunLifecycleSurface` 上等一个**永远不会 settle** 的完成 cell：该面的 `complete` 恒以 `journal = None` 调生产，其 `Completed` 分支找不到围栏匹配的 durable 见证，于是走 LC-004 的「只观察、不结算」分支（空白 view 不是结算），既不 `HandleCompleted` 也不 `run.Source.SetResult`——`await lifecycle.completion(run)` 永远不返回。
- **改法（混合形状）**：
  - **pending 的两条 case 迁到真实 fork 路径**（`requirements/delegation/tests/support/real-fork.mjs`）：真实 `this.fork`、真实子会话、真实共享终端端口。`MISSING_FINAL_REPORT` 的停止由 `eventsSurface.notifyForAuthority` 以该 work 自己的 admitted authority root 发布到共享端口（生产收它、只观察不结算）；「空 Completed」由一次**无交付**的子 turn 表达。两条 case 只断言**不等待**的 durable 事实：工作单元仍是 `Active`，且**从未接受过任何完成**（`lifecycleKind` / `completionRef` / `completionDigest` 全为 `null`）——不 await 任何可能不 settle 的 cell，也不做定时的重复断言（只取一次 durable 快照）。
  - **失败与单次赋值的三条 case 也迁到真实 fork 路径**（本次提交）：`Failed` 的结算**不经过 durable 见证**（`deliverFailedCompletion` 直接以停止原因为失败结算，`Handle/CompletionCodec.fs` 的 `FailedV2` 只按 child id 归属），所以在真实路径上以该 work 自己的 admitted authority root 发布 `Failed`（`eventsSurface.notifyForAuthority`）即可真实结算；单次赋值也只能在真实路径上证明。
  - **单次赋值的「Completed 在前」一半也搬到真实路径**：settled 的 `Completed` 必须携带**真正交付的材料**；旧断言里的 `view: ''` 因此**不是一个可被真实路径产出的结局**，已删除；改为断言真实交付的材料与 durable 完成身份（`lifecycleKind = "Terminal"` + 非空 `completionDigest`），以及后来的陈旧失败不能二次占用该 cell。
- **恢复走同一个已准入的工作单元（不是新根）**：pending 的两条 case 里「后来的已证明交付」用新增的夹具原语 `deliverIntoOpenWork`（`requirements/delegation/tests/support/real-fork.mjs`）表达——它在**同一个物理输入**下交付，因而落在**同一个 authority root**、结算**原来那个**工作单元；该物理输入已用掉它唯一一次 provider step（同一物理消息上的第二次 provider step 会以 EMR-010 失败关闭），所以这次交付不再进入 provider，而是直接交付进那个仍然开着的 work。`resume` 会新起一个物理输入、准入**另一个**工作单元，只能证明一个新义务，因此**没有**用它。
- **join 只在结算之后调用**：对仍然开着、仍可交付的工作单元调用 join 是**合法地等待**，所以 pending 的断言阶段不调用 join；join 在工作单元结算之后才调用。
- **新增的公开观察面**：`Execution/Delegation/Handle/JournalSurface.fs` 的 `recordView` 增加 `lifecycleKind`（该 work 最后接受的那次完成的种类：Terminal / SendFailure / Cancelled）、`completionRef`、`completionDigest`（该次完成的 durable 身份）。这三项在 work 尚未接受任何完成时为 `null`——这正是「保持 pending」所需的**非等待**观察面。`snapshot` 的既有字段与字节输出不变。
- **断言口径没有放宽**：pending 仍然断言「没有结算、没有被判为失败」，且新增「从未接受过完成」这一更强的 durable 事实；交付材料改为断言**真实交付的那份**（含在 join 回执里），而不是空白。
- **结算与发布是两件事（新增的等待口径）**：在共享终端端口上**发布**一条终局，与在 journal 上**提交**该 work 的完成，是两个不同的动作；结算发生在终局被观察**之后**。因此三处结算点都改为**有界的 durable 轮询**（`settleWithin`：轮询 journal 投影直到 `lifecycle !== "Active"`，硬上限 10s，超时即断言失败）——它等的是一个真实 durable 状态迁移，既不是固定 sleep，也不是放宽断言。`terminals.awaitRun` 的 15s 上限未动。
- **伪造面 `HostForkRunLifecycleSurface` 已退役（本次提交）**：该面的 `complete` 恒以 `journal = None` 调生产，其 `Completed` 分支拿不到围栏匹配的 durable 见证，LC-004 让它只观察、永不结算——那是一个**假 API**。它唯一的活消费者就是本文件（经源码/构建/清单的完整清点确认），而它最后的三条 case 已全部迁到真实 fork 路径，因此该面连同它在 `Wanxiangshu.Owner.delegation.runtime.fsproj`、`compile-order.txt`、`scripts/lib/test-surface-scan.mjs` 的面清单、`requirements/effect-accounting/APPLIES-TO` 中的登记**一并删除**，真实 fork 路径成为该义务的**唯一所有者**。无兼容层、无转发。
- **仍未关闭的缺口（如实记录）**：本次删除的是**面**与它的假 API；`journal = None` 所暴露的那条产品缺口（生产在 journal-less 分支拿不到围栏匹配的见证，只能观察不结算）**并未**在生产侧修复——它之所以不再可见，是因为真实 fork 路径**本来就带真实 journal**（`Fork/Host/Runtime.fs` 的 `HostForkRunLifecycle.complete` 传真实 journal 与真实 sessions 端口），所以生产路径不受影响；若将来再引入 journal-less 的调用方，该缺口会原样重现。
- **改动文件**：`src/Wanxiangshu/Execution/Delegation/Handle/JournalSurface.fs`、`requirements/effect-accounting/tests/002.test.mjs`、`requirements/delegation/tests/support/real-fork.mjs`；退役提交另加 `src/Wanxiangshu/Execution/Delegation/Fork/Host/HostForkRunLifecycleSurface.fs` / `.fsi`（删除）、`src/Wanxiangshu/Wanxiangshu.Owner.delegation.runtime.fsproj`、`src/Wanxiangshu/compile-order.txt`、`scripts/lib/test-surface-scan.mjs`、`requirements/effect-accounting/APPLIES-TO`。

## Unreleased — fission lane 的材料不再手工搬进 owner 的交付格

- **修复：takeover 完成时不再把 lane 的交付视图写进 owner 的交付格**。`Execution/Fission/OpenCode/Host.fs` 的 `CompleteOwner` 分支此前读出最终 lane 的交付视图声明，再 `recordView` 到 owner 会话自己的交付格——一次**跨 session 的手工搬运**。owner 交付格没有正当的生产消费者：`deliveredView` / `tryPeekRendered` 按**读者自己**的 anchor ledger 渲染，把 lane 的锚点声明放进 owner 的格子里，只会让 owner 的编号重新解释那些锚点——这正是缺陷本身。lane 的材料经 `aggregateView`（已含各 lane 的 `FissionLaneMaterialized` work blob）进入接管 prompt，不需要第二个可见交付格。
- **保留的消费者行为不变**：本次只删掉那处跨 session 写入；`tryPeekRendered`（`materializeCompletedLane` 用）、`peekView` / `recordView` 本身都没动，`intra-participant-parallelism/WHAT.md` 的措辞已同步。
- **验证的边界（如实记录）**：`requirements/intra-participant-parallelism/tests/014.test.mjs` 走真实准入与真实交付路径，断言 owner 交付格在 owner 自己交付前为空、交付后只含 owner 自己的锚点与材料、不含任何 lane 锚点或 lane 材料。**但真实 takeover 链在夹具里仍不可达**（lane turn 不结算为稳定 `TurnCompleted`，故无 `FissionLaneMaterialized`、无接管 prompt），因此**没有** takeover 分支的 failing-before/passing-after 动态回归；该缺口仍是 `GAP-158`。同批曾带一条「读源码文本」的结构性守卫，因其以源码文本而非行为为证据，**已删除**；剩下的 owner-cell 用例进不到该分支、改动前后都通过，**不作为本次改动的证明**。
- **改动文件**：`src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs`、`requirements/intra-participant-parallelism/tests/014.test.mjs`、`requirements/intra-participant-parallelism/WHAT.md`。
- **验收**：`intra-participant-parallelism/014` → tests 3 / pass 2 / fail 0 / todo 1（`GAP-158` 的 todo 原样保留）；全 `intra-participant-parallelism` 套件 → **46 tests / 31 pass / 0 fail / 0 cancelled / 15 todo**。

## Unreleased — 已证明的终态必须结算（见证在、活格没了，也照样结算）

- **修复：durable 交付见证存在、但进程内交付格已丢失（或视图读取故障）时，运行不再永远挂着**。`Execution/Delegation/Fork/Host/RunLifecycle.fs` 的 `complete` 在命中围栏匹配的 durable 见证后，只认「活格 / 视图读取」这一路材料；活格被别的读者取走、或视图读取故障时该路为空，于是走 LC-004 的「只观察、不结算」分支——**交付明明已被证明**，父侧的 `this.join()` 却永远等不到结算，工作单元停在 `Active`。
- **判据：见证自带材料**。durable 见证（`TaskDeliveryWitness`）本来就携带那次交付的 `View`（物化后的交付物）、`ViewDeclaration`、`FormalText` 与 `MaterialDigest`。因此当活格与视图读取都拿不到材料时，改用**见证所携带的那份材料**结算——不伪造任何事实，这正是已交付的那份材料。见证为空（无证明）时行为不变：仍不结算。
- **`requirements/managed-session-lifecycle/tests/028.test.mjs` 迁到真实插件路径**：三条 case 不再用旧 harness 的 `settle` / `prepareTerminalDelivery` / `injectPendingPersistFailure`（前者从不下落围栏匹配的 durable 见证，整文件挂起），改为真实 `this.fork` / 真实 `this.deliver`（生产落唯一那条 durable 见证）/ 生产的 `message.updated` → `session.idle` 终态发布 / 真实 `this.join`。唯一注入的故障是**真实的 append 失败**：把 durable 事件日志置为只读，由写者自己的 EACCES 打穿结算，不写任何伪造事实。
- **断言口径换成真实事实**：交付格丢失时，已证明的终态仍结算为 `CompletedAwaitingJoin`，join 返回 `ok = true` 且视图含交付原文；结算期 append 失败时 join **及时返回** `ok = false` / `code = "not-ready"`（不挂起），durable 工作单元仍是 `CompletedAwaitingJoin`（失败不释放材料）；结算期 append 失败且终态本身无法落盘时，父侧 join 同样及时返回 `ok = false` / `code = "not-ready"`，durable 工作单元**仍是 `Active`**——未知的持久提交既不放行新工作，也不冒充成功。
- **验收**：`node --test requirements/managed-session-lifecycle/tests/028.test.mjs` → tests 3 / pass 3 / fail 0 / cancelled 0（改动前整文件挂起）；`024` / `027` / `006` / `007` / `008` / `013` / `015` / `022` / `028` 合跑 tests 91 / pass 85 / fail 2 / cancelled 0 / skipped 2 / todo 2（那 2 个 fail 是 `delegation-027` 的「prepared busy guidance」，在该基线上单跑同样失败，与本次改动无关）。
- **改动文件**：`src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs`（净增 47 行，无新模块、无新分层、无测试专属入口）、`requirements/managed-session-lifecycle/tests/028.test.mjs`。

## Unreleased — 生命周期 022 迁到真实交付路径（Engineer 的完成必须是一次真实交付，父侧才拿得到草稿）

- **`requirements/managed-session-lifecycle/tests/022.test.mjs` 迁到真实插件路径**：`finalize prerequisite uses the original plugin scope for Manager admission, attached Engineer completion and its production draft` 与 `original child and owner deletion archive the completed Engineer and same-scope dispose awaits actual Bookkeeper completion` 此前只在 harness 里把文本 push 进 assistant 消息，**从未发生一次真实交付**——生产因此看不到 durable 交付见证，父侧的 `await` 永不结算（实测：缺见证时 `this.deliver` 直接报 `AcceptedExecutionMissing`），整条 case 挂起。现在交付改为真实 `this.deliver`（`hooks.tool['todowrite-for-engineer'].execute`），由生产落唯一那条 durable 交付见证，随后照旧 `message.updated` → `session.idle` 发布终态。
- **断言口径换成真实事实**：交付后 `invokeEngineer` 立刻返回 `ok = true` 且 view 含交付原文；`semanticTrace.terminalEvidenceForProviderRun` 给出 `blobs/<sha256>` 且其 payload 恰为交付原文；`takeEngineerDraft` 一次取到 `{ sessionId: child, turns: [{ question: charge, answer: view }] }`、第二次取到 `null`（草稿是消费式的，不是只读窥视）；删除 child 与 owner 后，同作用域 dispose 内的真实 Bookkeeper `SendPrompt` 收到原始 charge/answer，`EngineerCaseCaptured` 恰好两条且 identity 为该 child、canonical Q/A 正确、canonical 前缀逐字节保留、decoy 未被改写，冷子进程复核 `verified = true`。
- **其余 case 原样保留**：三条 casebook finalize 归档（未提交不发布、只提交一次、空草稿不建 Bookkeeper 子会话）本就在真实插件路径上，不依赖旧 harness。
- **验收**：`022` → tests 6 / pass 5 / fail 0 / cancelled 0 / todo 1（原有 GAP-133 `test.todo` 原样保留）。
- **改动文件**：`requirements/managed-session-lifecycle/tests/022.test.mjs`。

## Unreleased — 生命周期 013 / 015 迁到真实交付路径（跨实例消费由 durable 墓碑仲裁，绑定跨重启不变）

- **`requirements/managed-session-lifecycle/tests/013.test.mjs` 迁到真实插件路径**：`a stale-projection consumer is arbitrated by the durable consumption tombstone, not its own fold` 不再用 harness 的 `settle`（从不下落围栏匹配的 durable 见证，因而永远等不到结算），交付改为真实 `this.deliver` + 生产的 `message.updated` → `session.idle` 终态发布。冷消费（另一个写者 + 重放）先消费成功并交出载荷；旧运行时随后消费时**不交出任何载荷**（`ok = false`），durable 面上该格为 `Retired`、再次冷消费得到 `AlreadyRetired`、总交付次数恒为 1。**不比较错误码**是有意的：生产 join 报 `nothing-to-join`、旧 harness 报 `AlreadyRetired`，二者同义（没有可交出的载荷），所以仲裁这一事实断言在耐久面（冷读 `Retired` / 冷消费 `AlreadyRetired` / 交付次数不增）上，比断言文案更贴切。
- **`requirements/managed-session-lifecycle/tests/015.test.mjs` 迁到真实插件路径**：`actual admissions and replay retain exact handle child participant binding` 与 `actual recovered fork uses its runtime Agent ID handle and rebinds the same physical child (GAP-133)` 不再用 harness 的 `settle`，改为真实 `this.fork` / `this.resume` / `this.deliver` 与生产终态发布。第一次与第二次准入共享**同一个 handle、同一个物理 child、同一个参与者身份**，只有 root 不同；未被接受的 root 的消费被拒（`WorkNotAdmitted`）且快照不变；未知 Byname 得到有类型的拒绝（`person-unknown`）且**不新建物理 child**、快照不变。**GAP-133 的多进程编排被压缩**为「同一插件运行时 + 一次冷恢复读」——真实路径没有多进程编排，而冷恢复读（新写者 + 全量重放）证明的正是该 case 要证的事：durable 绑定在创建它的写者之外依然成立（同一 handle、同一物理 child）。
- **验收**：`013` → tests 7 / pass 6 / fail 0 / cancelled 0 / todo 1（原有 `test.todo`）；`015` → tests 11 / pass 11 / fail 0 / cancelled 0；`024` / `027` / `006` / `007` / `008` / `013` / `015` 合跑 tests 80 / pass 77 / fail 0 / cancelled 0 / skipped 2 / todo 1。
- **改动文件**：`requirements/managed-session-lifecycle/tests/013.test.mjs`、`requirements/managed-session-lifecycle/tests/015.test.mjs`。

## Unreleased — 委派 027 迁到真实交付路径（旧 root 停机、busy 指引、attachment 绑定）

- **`requirements/delegation/tests/027.test.mjs` 迁到真实插件路径**：三个测试组（2 个 old-root、1 个 busy guidance、6 个 prepared busy guidance）不再走旧 fork harness 的 `settle`（它从不下落围栏匹配的 durable 见证，因此永远等不到结算），改为真实 `this.fork` / `this.resume` / `this.join` / `this.deliver` 与生产的 `message.updated` → `session.idle` 终态发布。测试只**观察**共享终端端口，从不发布终态、从不写 durable 见证。
- **old root 停机信号**改由生产面 `EventsSurface.notifyForAuthority` 发布带 root 归属的 `Failed` / `Aborted` 终态：断言从「await 一个没有见证的终态完成」改为「注入停机信号 → 工作单元的 durable 快照不变」，随后当前轮仍走真实交付完成并被 join 取到。
- **busy 指引**的全部行为断言保留：指引期间原工作生命周期仍为 `Active`、共享端口没有该子会话的终态、指引作为真实 prompt 落到 Host、同一文本两次发送是两次独立发送（promptKey 不同）、完成后生命周期转为 `CompletedAwaitingJoin`、idle 后的 resume 起新工作（新的物理输入根）且 join 同时拿到两轮交付。
- **公共夹具**：`requirements/delegation/tests/support/real-fork.mjs`（纯 Host I/O：真实顺序的子轮、终态观察、`fork`/`resume`/`join` 调用封装、工作单元生命周期读取），`024` 与 `027` 共用；它不含任何业务判定或持久逻辑。
- **验收**：`node --test requirements/delegation/tests/027.test.mjs` → tests 11 / pass 9 / fail 0 / cancelled 0（2 个 `integrationTest` 按 tier 门禁 skip）；`024` 仍 tests 5 / pass 5 / fail 0 / cancelled 0；两文件合跑 tests 16 / pass 14 / fail 0 / cancelled 0。
- **改动文件**：`requirements/delegation/tests/027.test.mjs`、`requirements/delegation/tests/support/real-fork.mjs`（新增）、`requirements/delegation/tests/024.test.mjs`（改为共用该夹具）。

## Unreleased — 生命周期 006 迁到真实交付路径（消费后的工作留下墓碑，重放不复活它）

- **`requirements/managed-session-lifecycle/tests/006.test.mjs` 迁到真实插件路径**：唯一一条依赖旧 harness 的 case（`exact admission resumes the road without reviving its consumed work`）不再用 harness 的 `settle`（从不下落围栏匹配的 durable 见证，因而永远等不到结算）、`consumeWorkWithOutcome` 与 `replayBinding` 这几个只有 harness 提供的面，改为真实 `this.fork` / `this.resume` / `this.join` / `this.deliver` 与生产的 `message.updated` → `session.idle` 终态发布。测试只**观察**共享终端端口，从不发布终态、从不写 durable 见证。
- **断言口径换成真实事实**：交付由真实 `this.deliver` 落见证并由生产发布终态；「消费」由生产的 `this.join()` 完成——它交出已交付的视图，并把被消费的工作转成 `Retired` 墓碑；「重放」由真实 `resume` 复用同一 Byname 完成——它开出**新的 root**、保持同一 handle 与同一物理 child，而**旧 root 仍是 `Retired`**（墓碑既不被复活，也不被新的工作覆盖）。
- **公共夹具**：`requirements/managed-session-lifecycle/tests/support/real-fork.mjs` 只是跨目录 re-export（不复制实现、不引入第二套状态），委派侧的 `requirements/delegation/tests/support/real-fork.mjs` 仍是唯一的真实路径夹具。
- **验收**：`node --test requirements/managed-session-lifecycle/tests/006.test.mjs` → tests 22 / pass 22 / fail 0 / cancelled 0（其余 21 条纯 `Handle/Surface` 折叠与策略 case 原样保留）；`024` / `027` / `006` 合跑 tests 38 / pass 36 / fail 0 / cancelled 0 / skipped 2。
- **改动文件**：`requirements/managed-session-lifecycle/tests/006.test.mjs`、`requirements/managed-session-lifecycle/tests/support/real-fork.mjs`（新增）。

## Unreleased — 生命周期 007 迁到真实交付路径（两轮完成各占一格，迟到终态不夺格）

- **`requirements/managed-session-lifecycle/tests/007.test.mjs` 迁到真实插件路径**：两条依赖旧 harness 的 case（`canonical A and B retain separate cells across late A and cold replay`、`completed work remains single-assignment before consumption`）不再用 harness 的 `settle`（从不下落围栏匹配的 durable 见证，因而永远等不到结算）、`consumeWorkWithOutcome`、`replayBinding` / `replayWorkCompletion`、`emitTerminalForRoot` 这几个只有 harness 提供的面，改为真实 `this.fork` / `this.resume` / `this.join` / `this.deliver` 与生产的 `message.updated` → `session.idle` 终态发布。测试只**观察**共享终端端口，从不发布终态、从不写 durable 见证。
- **断言口径换成真实事实**：交付由真实 `this.deliver` 落见证；「消费」由生产的 `this.join()` 完成（交出视图并把该格转成 `Retired` 墓碑）；「迟到终态」由生产的带 root 归属终态面发布（`EventsSurface.notifyForAuthority`），它**不改**已完成格的事实——第一个完成者仍持有 `completionRef` / `completionDigest`，另一格仍 `Active`；「冷读」由既有冷恢复面在同一工作区上重新折叠 durable 事件日志得到（`coldWorkSnapshot`），与热读一致。
- **公共夹具**：`requirements/delegation/tests/support/real-fork.mjs` 增加 `coldWorkSnapshot`（冷恢复读），生命周期 006 / 007 直接 import 委派侧这一份夹具（不设转发层）。
- **验收**：`node --test requirements/managed-session-lifecycle/tests/007.test.mjs` → tests 14 / pass 14 / fail 0 / cancelled 0（改动前该文件为挂起）；`006` 仍 tests 22 / pass 22；`024` / `027` / `006` / `007` 合跑 tests 52 / pass 50 / fail 0 / cancelled 0 / skipped 2。
- **改动文件**：`requirements/managed-session-lifecycle/tests/007.test.mjs`、`requirements/delegation/tests/support/real-fork.mjs`（增补冷恢复读）。

## Unreleased — 生命周期 008 迁到真实交付路径（提交失败的消费不交出载荷，工作至多交付一次）

- **`requirements/managed-session-lifecycle/tests/008.test.mjs` 迁到真实插件路径**：四条依赖旧 harness 的 case（`exact consume with {before|after} commit failure never releases an unconfirmed payload`、`concurrent exact consumers deliver a work at most once`、`actual join withholds payload until retirement append confirms and survives each crash cut with one delivery (GAP-133)`）不再用 harness 的 `settle`（从不下落围栏匹配的 durable 见证，因而永远等不到结算），交付改为真实 `this.deliver` + 生产的 `message.updated` → `session.idle` 终态发布；测试只**观察**共享终端端口，从不写 durable 见证。
- **断言口径换成真实事实**：失败的消费**不交出载荷**（无 `view`）——before 切时该格仍 `CompletedAwaitingJoin`（后续 confirmed 消费可成功并带出视图），after 切时该格**已 `Retired`**（后续 confirmed 消费必然 `AlreadyRetired`）；第三次消费必失败（工作单次赋值）。两个并发消费同一格时**恰好一个**成功并带出载荷，另一个被生产的单飞保护挡下（`join-in-progress`），冷读该格为 `Retired`。两个崩溃切之间**恰好一次交付**。
- **崩溃切的注入点**：只有冷恢复面能制造「append 未提交 / 提交后回执丢失」（生产的 `this.join()` 不暴露这种注入口），因此 before / after 两种切继续走既有冷恢复消费面 `coldConsumeWorkWithOutcome`——它只对**durable 事实**结算，失败是真实的 append 失败，不写任何伪造事实；admit / 交付 / 成功消费 / 冷读 / 单次赋值全部走真实路径。
- **验收**：`node --test requirements/managed-session-lifecycle/tests/008.test.mjs` → tests 10 / pass 10 / fail 0 / cancelled 0（改动前该文件为挂起）；`024` / `027` / `006` / `007` / `008` 合跑 tests 62 / pass 60 / fail 0 / cancelled 0 / skipped 2。
- **改动文件**：`requirements/managed-session-lifecycle/tests/008.test.mjs`。


## Unreleased — 交付登记不再让宿主反向依赖应用组合层

- **修复：host → application-composition 的反向边消失**。`Repository/Programming/Js/OpenCode/ToolWorkflow.fs` 此前直接引用 `DeliveryAttemptLedger.recordEstablished` 来登记「本次交付已成立」，为此整个 `repository-programming-js-toolbindings`（subsystem = host）必须引用 `managed-session-lifecycle.opencode-tools-toolruntimescope`（subsystem = application-composition）——宿主层反向依赖应用组合层。唯一的符号依赖就是那一次登记。
- **改法：登记挂在 durable 提交点上**。`DeliveryWitnessWriter.create` 增加一个 `onCommitted` 回调，**只在** `TaskDeliveryJournal.commit` 返回成功之后才调用；提交被拒则一次也不登记（零登记）。`IDeliveryWitnessWriter` **不加任何成员**（仍只有 `CommitDeliveryWitness`），回调没有空默认值，必须显式传；全部调用点已迁（`DeliveryWitnessWriter.create` 全仓仅一处调用，已注入 ledger 登记）。`ToolWorkflow.fs` 里私有的 `registerEstablishedDelivery` 随之删除。
- **回调抛错不抹除已 durable 的事实**：回调运行时 `TaskDeliveryCommitted` 事实已写入 journal 并已折叠进投影；回调抛错只让本次调用以异常渠道失败（不转成拒绝、不吞异常），已写入的事实既不回滚也不被改写——journal 写入先于回调，回调没有写权限。
- **改动文件**：`OpenCode/Host/DeliveryWitnessWriter.fs`/`.fsi`、`OpenCode/Tools/ToolRegistry.fs`、`Repository/Programming/Js/OpenCode/ToolWorkflow.fs`、`Wanxiangshu.Owner.repository-programming.repository-programming-js-toolbindings.fsproj`（去掉那条 ProjectReference，未新增任何 application-composition 引用）。
- **验收**：`node scripts/build.mjs` → `[build] build ok`；`js-surface-manifest: OK — 149 registered surfaces`；`js-module-linkage: OK — 715 emitted modules linked and loaded`；`node scripts/checks/subsystems.mjs` → `subsystems: OK — 25 subsystems, 240 compile shards, 646 sources, 1954 refs, shard DAG, largest subsystem cycle=10`（改前 1955 refs / cycle=16）；host → application-composition 边 = 0。回归：`effect-accounting/013` + `semantic-trace/013` + `work/011` → tests 24 / pass 24 / fail 0 / cancelled 0。


## Unreleased — 交付格的消费按完整围栏，不只按 provider run

- **修复：同一 provider run 内另一次调用的交付不会被错取**。交付格记的是产生它的那次调用的完整围栏（provider run + tool call id），而消费面只比 provider run。于是同一 run 里的第二次交付会**覆盖**第一次的格子，而只持 run 的终态会把**另一次调用**的材料取走并推进 returned 游标——真正拥有该交付的那次调用随后拿不到自己的材料。这不是可接受的降级，是一次真实的错取。
- **消费改为完整围栏比对**。新增 `tryTakeRenderedForCall`：先读交付格的围栏，provider run 与 tool call id **都**相符才取；不符则**不取、不推进游标**，格子原地不动。比对与取之间没有 await，且 owner 的 fence/claim/handOff 不触碰用户代码，因此在单线程 JS 下这一步是原子的。
- **生产消费方全部接线**。fork 侧不再把 durable 见证压成一个布尔：它保留见证对象，取见证自带的 `Fence.ToolCallId` 作为精确围栏；无 journal 的夹具路径只按交付格**自身**的完整身份（该子会话 + 该 run）取，绝不取别次调用的格。sync delegate 侧优先直接返回 durable 见证的材料；无 journal 时只从「该子会话 + 该 run」这一格自身身份取，**不从父方工具调用序列猜子方那次交付**（那序列是父方的，交付是子方的）。
- **弱围栏读取器降级为仅夹具**。`tryTakeRenderedFor`（只比 run）保留，但注释改成实话：同一 run 内另一调用可取走该格并推进游标；生产消费方一律走精确围栏或 durable 见证。
- **改动文件**：`Repository/Programming/Js/DeliveredValues.fs`/`.fsi`、`Repository/Programming/Js/RuntimeSurface.fs`/`.fsi`（新增精确围栏测试钩子）、`Execution/Delegation/Fork/Host/RunLifecycle.fs`、`Execution/Delegation/SyncDelegate/Runtime.fs`。
- **验收**：`repository-programming/059` 新增永久回归「同一 run 的另一次调用不得取走该格」——错取返回 null、围栏不变、returned 游标未推进，正确调用随后可取自己的材料（该用例在只比 run 的旧实现下为红）。回归：`059` + `delegation/007|008|009|010|011|012|023|025|033` → tests 54 / pass 49 / fail 0 / cancelled 0 / todo 5。

## Unreleased — 构建的 up-to-date 判据要看产物是否真的在盘上

- **修复：产物缺失不再被报成「build up-to-date (no-op)」**。构建的「已是最新」判定此前只看**编译快照**（编译器输入摘要、生成语料摘要、产物语料摘要），这份快照说的是「源码没变」，不是「产物还在」。于是 `dist/` 被清空、被部分删除、或只复制了一半时，编译缓存仍会命中，构建日志照样打印 `build up-to-date (no-op)`，而磁盘上的产物已经缺失或不全——一次「构建绿」什么也没产出。
- **判据改为「快照一致 **且** 产物齐全」**。新增 `checkArtifactsPresent`：按构建自己的产物清单（manifest 的 `outputs` 全集）逐条检查每个产物是否**真的在磁盘上**，少一条即判缺失。清单取自构建自己的 manifest，不硬编码任何文件数或路径列表。
- **缺失即重编**。`determineBuildDecision` 在 `outputsValid` 之外再加一道 `artifactsPresent` 门：产物缺失时 reason 为 `artifacts-missing`，mode 强制 `full`（重编），不再复用缓存。
- **改动文件**：`scripts/build.mjs`（新增 `checkArtifactsPresent` 导出函数 + 决定函数里的一道门；既有行为零改动，正常路径仍是 no-op）。
- **验收**（隔离 worktree 副本，未动共享 dist）：产物齐全 → `[build] build up-to-date (no-op)`；单独删掉一个中间依赖 `dist/Execution/Fission/OpenCode/Host.js` → `[build] Compiling F# (full)...` / `[build] compiled full impact (1308 items in 52103ms)` / `[build] build ok (generation 107)`，该文件回到且 `.js` 计数 724 与删除前一致；再跑一次 → `[build] build up-to-date (no-op)`。回归：`requirements/distribution/tests/005.test.mjs` → tests 18 / pass 17 / fail 0 / todo 1；`requirements/verification-system/tests/008.test.mjs` → tests 9 / pass 9 / fail 0。

## Unreleased — 交付已证明的轮次发布真实终态（委派 join 不再永远等不到结算）

- **修复：已交付的一轮不再因「有交付见证」而跳过终态发布**。`Composition/Turn/OrdinaryTurnWorkflow.fs` 的 `longWorkGate` 此前把「生命周期已结算」与「本轮有围栏匹配的 durable 交付见证」并成一条早退，两种情形都判 `Wait`。于是恰恰是**已经证明交付**的那一轮永远走不到 `completeAgent`（`TerminalReporter.completeWithEvidence`），共享终端端口上不出现任何终态：父侧的 `this.join()` 一直挂着，子运行停在 `Active`——交付明明发生了，父侧却拿不到结算。
- **判据分开**。`settled`（生命周期已结算）仍判 `Wait`；`hasDeliveryEvidence` 改为按角色分类：Manager / Plan 仍判 `Wait`（它们各有自己的生命周期所有权，不因一次交付被复活），其余角色且本轮 `TurnCompleted` 走 `Ordinary`——复用既有的完成所有者（`TerminalReporter`），不新增任何层、模块或端口。未交付的一轮没有见证，仍走原来的 `Continue` / nudge 路径；显式等待与过期轮次的 `Wait` 判定不变。
- **验收**（真实插件路径，无伪造 durable 见证）：`requirements/delegation/tests/024.test.mjs` 全部 5 个 case 绿（tests 5 / pass 5 / fail 0 / cancelled 0）——其中三个 fork 侧 case 已迁到真实接线（`this.fork` / `this.resume` / `this.join` / `this.deliver` + 生产的 `message.updated` → `session.idle` 终态发布），两个 sync 侧 case 保持原样。对照证据：修复前同一真实路径 `join` 挂起且共享端口终态数为 0；修复后终态真实发布、`join` 返回 `status = "completed"` 且视图含交付材料；未交付对照仍无终态、工作单元仍 `Active`、`join` 仍 not-ready。
- **改动文件**：`src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs`（净增 14 行，无新模块、无新分层）、`requirements/delegation/tests/024.test.mjs`。

## Unreleased — parked source 的执行点标记按程序脚本位置换算

- **修复：parked 载荷的 `source` 不再丢失执行点标记**。`Process/JsSandbox.fs` 的栈换算此前取 `evalmachine.<anonymous>:` 之后的数字；沙箱以 `new Function` 求值程序后，V8 把程序帧渲染为 `at Js.run (eval at <runner> (evalmachine.<anonymous>:226:19), <anonymous>:150:33)`，那个数字是 runner 自身源码的行、恒定落在程序行范围之外，于是每个帧都被判为不在范围内，标记退化为整篇 ` | `，没有一行带 `>| `。现在取程序帧尾随的 `<anonymous>` 位置，并按程序在脚本中的起始行（基类行数 + 4）换算到模型行。
- **只认程序帧**：经 `eval` 求值字符串的帧，尾随位置是该字符串自己的行号，与程序行号数值可能相同；按数值范围判定会误标到无关的程序行上。这类帧不参与换算，标记落在程序自身的调用点上。
- **改动文件**：`Process/JsSandbox.fs`（栈帧换算重写，旧的 runInContext 兼容分支一并删除——当前沙箱只有 `new Function` 一条路径）、`requirements/repository-programming/tests/065.test.mjs`（新增回归）、`requirements/repository-programming/WHAT.md`（换算规则并入既有 [048] 条款，不新增重复条款）、`requirements/repository-programming/tests/README.md`。
- **验收**：`repository-programming/048` 三例全绿；`repository-programming/065` 一例全绿（`eval` 场景对旧算法会误标到 `const snippet = ...` 一行，故为真回归；parked handle 以显式 abandon 收尾）；`repository-programming/002|028|029|030|031|032|047|049|056` 全绿；`fsharp-control-pyramid` 在 `JsSandbox.fs` 上净减 3 处（1 处 match-pyramid、2 处 branch-pyramid），`js-boundary-gate` / `provider-leak-gate` / `clean-break-symbols` 绿。

## Unreleased — 锚声明：单一 typed 规则，公开面改回 JS-native 回执

- **重构：锚声明的判定收敛为一条 typed 规则**。`AnchorDeclarationRule`（`Repository/Programming/Js/Anchor/AnchorModel.fs`）成为唯一的声明形状判定核心：覆盖律决定形状，宿主把首个违规映射到稳定失败码。F# 调用方直接消费这个 typed core，JS 边界只是同一判定的回执出口，不再各自复述一份解析。
- **公开面改为 JS-native 回执**。`AnchorSurface.parseDeclaration` / `parseReportMapping` / `reportMaterial` 现在返回普通 JS 回执：`{ ok: true, prefixUntil, thenMap }` 或 `{ ok: false, code, reason }`；材料回执带 `{ ok, material }`。`thenMap` 保持 record 形态（不是数组），无 `toJSON` 深度解析。`AnchorSurface.fsi` 的签名同步改为 `obj`。
- **改动文件**：`Repository/Programming/Js/Anchor/AnchorModel.fs` / `.fsi`、`Repository/Programming/Js/Anchor/AnchorSurface.fs` / `.fsi`、`Execution/Delegation/Advisor/Surface.fs`、`Repository/Programming/Js/DeliveredValues.fs`、`Repository/Programming/Js/ToolsBindings.fs`、`OpenCode/Tools/ToolRegistry.fs`。
- **验收**：`node scripts/build.mjs` → `[build] build ok (generation 104)`（`js-surface-manifest: OK — 151 registered surfaces`、`js-module-linkage: OK — 724 emitted modules linked and loaded`）；`context-compression/031|032`、`repository-programming/059|063|064`、`work/020|034`、`delegation/033`、`advisor/006` → tests 68 / pass 68 / fail 0 / cancelled 0；`delegation/029`、`managed-session-lifecycle/018`、`repository-programming/048` → tests 13 / pass 11 / fail 0 / cancelled 0 / todo 2。

## Unreleased — 委派：旧 root 的失败终局不再结算新工作单元；恢复运行时不再引用外来持久句柄

- **修复：属于旧工作单元的 `Failed` 终局不再结算当前工作单元**。`HostForkRunLifecycle.complete` 此前对任何带 root 的 `Failed` 停机信号一律走 `deliverFailedCompletion`：携带另一个（更早的）工作单元 Authority Root 的停机信号会占用当前运行的完成格，写下 `completionDigest`/`completionRef`，生命周期被置为 `CompletedAwaitingJoin`——等于用旧工作单元的终局结算了新工作单元。现在 `Failed` 分支先按该运行自己被准入的 Authority Root 核对归属：root 不匹配的停机信号只被观察，不结算、不留完成证据；无 authority root 的会话级停机信号仍然可以结算（WHAT[delegation-025] 允许的唯一无 root 例外）。
- **修复：恢复运行时不再持有外来持久句柄**（WHAT[delegation-029]）。`ChildRecoveryWorkflow` 原先直接引用 `AgentJournal` 并自行读取交付见证投影，越过了分片边界（该分片的编译闭包本就不含持久层与宿主事实层）。现在恢复运行时只消费一条窄能力 `DeliveryWitnessReader`（子会话 × provider run × Authority Root → 是否存在交付见证），具体的持久读取留在既有持久组合层（`TaskDeliveryJournal.tryFindByFenceFields`），由重启面在构造端口时注入；未新增分片、未新增适配层。
- 验收：`requirements/delegation/tests/029.test.mjs` 的分片与词法审计绿；`requirements/delegation/tests/019|020|021` 绿；`requirements/managed-session-lifecycle/tests/018.test.mjs` 的 `unknown-stop` 场景由红转绿（修复前旧 root 的 `Failed` 会把运行判成 `CompletedAwaitingJoin`），无 authority root 的 `fission-external-abort` 场景保持 `CompletedAwaitingJoin` 不变。
- 未闭合（不在本批修改）：`requirements/delegation/tests/027.test.mjs` 的 `Failed`/`Aborted` 变体在 `settle` 一步挂起，同一现象也出现在 `requirements/delegation/tests/006|024|026`。根因在 D6 门禁与宿主夹具面之间：`Completed` 分支要求一笔围栏匹配的持久交付见证（`Fork/Host/RunLifecycle.fs` 的 `witnessed` 判定），真实工具路径由 `Repository/Programming/Js/OpenCode/ToolWorkflow.fs` 的 `CommitDeliveryWitness` 落盘，而宿主夹具面 `Fork/OpenCode/ToolSurface.fs` 的 `settle`/`prepareTerminalDelivery` 只补轨迹文本、从不提交见证，于是「子会话已交付」在夹具里永远不被证明，运行停在 `Active`、等待视图永不返回。收口方向已定：**走真实交付路径**——用例迁到真实的 `this.deliver` 与宿主结束流程，由真实交付落盘见证；**不得伪造 durable 见证**来放行门禁，门禁口径也不放宽。与本次 root 归属修复无关。

## Unreleased — 前缀世代重新由已提交事实推进（CUT-15）

- **修复：宿主压缩与任期交接不再反复重锚**。cutover（6eef89d1e）之后，持久投影折叠 `ContextReanchored` / `TenureReanchored` 时只清检查点、退休辅助呈现，不再推进 `PrefixEpoch`：世代永远停在 0，已处理的压缩 run 也不入账。于是 `HostCompactionGate` 每次核对都对同一次压缩再写一行 0→1，每一行都把会话此后重建的 view 检查点清空、把 guidance 可见性再退休一次。现在折叠经前缀 owner 提交：只有被接受的推进才清检查点与退休可见性；重放行与重复观察被吸收、什么都不改；非后继世代照旧报 PERSIST-010。
- **前缀规则改由 `wanxiangli/prefix`（0.10.0 起）唯一拥有**：世代推进（两类证据：宿主压缩伪 run、Plan 任期 incumbencyId）、候选与提交分离、吸收策略、view 检查点去重、同代追加律（provider/model/variant/工具集/system/逐条消息字节）。主仓 `Context/Prefix/Epoch`、`PhaseWindow`、`ProviderProjection.isAppendOnlyPrefix` 只做类型投影；`HostCompactionPolicy.nextReanchor` 删除，选哪一个压缩由库的 `proposeCompaction` 回答。
- 既有日志：重放时首个压缩行生效，同 run 的重复行被吸收；故障期间若真有第二次压缩被写成 0→1，它会被当作过期行吸收，下一次核对由宿主重新观察到并以正确世代补写——自愈，不改写原件。

## Unreleased — 宿主工程循环改名：js.run → js.call

- **宿主工程循环成员改名，混淆随之解除**。`js.run` 与模型程序入口 `this.run` 同名，模型在两处都看见 `run`，分不清「执行一次工程」和「运行我的程序」。宿主面成员改为 `js.call(class extends js.Callable { ... })`，只在全局 `js` 对象上调用；基类不提供 `this.call(...)`。模型程序入口仍是基类的 `async run()`——两个不同的名字，两件不同的事。
- **基类随之改名**：`js.Runnable` → `js.Callable`（宿主在全局 `js` 上绑定的空基类）。旧名「Runnable」描述的是被宿主驱动的循环，新名描述的是「可被调用」的类，与成员名同源。
- **失败码族整体改名**：`RUNNABLE_SPEC_INCOMPLETE` / `RUN_COMMAND_UNSUPPORTED` / `RUN_WAIT_TIMEOUT` / `RUN_CHILD_UNKNOWN` / `RUN_PARK_UNSUPPORTED` / `RUN_HOST_UNAVAILABLE` → `CALLABLE_SPEC_INCOMPLETE` / `CALL_COMMAND_UNSUPPORTED` / `CALL_WAIT_TIMEOUT` / `CALL_CHILD_UNKNOWN` / `CALL_PARK_UNSUPPORTED` / `CALL_HOST_UNAVAILABLE`。clean-break：不留旧码别名，旧码不再被发出。
- **宿主实现标识符同步**：`JsRunLoop` 模块 → `JsCallLoop`（`Process/JsRunLoop.fs` → `Process/JsCallLoop.fs`，fsproj 与 compile-order 一并更新）、`JsRunHost` → `JsCallHost`、`makeRunMember` → `makeCallMember`、`RunnableValidated` → `CallableValidated`、`validateRunnableSpec` → `validateCallableSpec`、`RunnableSpecIncomplete` 等 union case → `CallableSpecIncomplete` 等。
- **规范与教学同步**：`requirements/repository-programming` [050]/[051]/[052]/[058] 与 `requirements/process-execution` 的条款改写为新面；双语 `run-rules` / `run-recipes` / `ultra-devops` 改写为 `js.call(class extends js.Callable { ... })`，并删去「两者同名是有意为之的碰撞」那段已经失效的解释——改名后不再有碰撞需要解释。

## Unreleased — 验收层收口：集成层恢复全绿，退役概念清零

- **集成层（integration tier）恢复全绿**。cutover 之后无人跑过这一层，它当时是红的，本轮把三处根因修掉：
  - 36 个文件残留着 `open Wanxiangshu.Context.Prefix`（`XProjectionChoice` 随 prefix probe 一起退役后留下的死引用）。这些 open 指向的命名空间不在各自 shard 的编译闭包里，shard 独立编译因此报 `The namespace 'Prefix' is not defined`；死引用全部删除。
  - shard 图缺三条边：`repository-programming-js-capability` 缺 `foundation-taskresult`（`JsRunLoop` 用 `taskResult`），`execution-fission-opencode-host` 缺 `host-tool-adapter`（fission 工具用 `ToolHostCodec`），`composition-durable-fact` 缺 Host 事实词汇。前两条补上引用；第三条不能补——`Host/Fact.fs`（投影）依赖持久事实词汇，而持久事实词汇又依赖 `HostFactCases`（`Host/Facts.fs`），补引用会成环。改为把词汇与投影拆成两个 shard：`durable-composition.host-fact-cases` 持 `Host/Facts.*`，`durable-composition.host-fact` 持 `Host/Fact.*` 并引用它，持久事实 shard 引用前者。环消失，闭包自足。
  - 验收夹具 `arch010-cases.mjs` 有一处 cutover 留下的半截 `assertTrue(`（删 Blogger 断言时切断），整个 integration harness 因此无法加载；同时两条 CTX-010 prefix-probe 用例与一条 companion frame 断言仍在验证已退役的机制，一并删除。
- **测试层清零退役概念**。退役概念（Blogger / Predictor / Chronicle / StrengthReplica）在 `src`、`resources`、`scripts` 已无残留；测试层里仍在*提供*这些概念的死代码一并清掉：e2e 隔离宿主配置不再注册 blogger / predictor agent，默认路由源不再导出 `predictorConfiguration`，strict mock provider 不再对 blogger turn 特判，`WANXIANGSHU_BLOGGER_CONTEXT_LIMIT`（产品早已不读）不再下发；dispatch-protocol 与 interaction-authority 的 root selection 不再用 `predictor` → `inspector` 的映射冒充角色。规范与说明文字同步：context-compression / feature-ablation / capability-enforcement 里的退役条款保留（负向断言的锚点），过期注释与 README 段落改写为当前事实；`office-capability-015` 那条以退役概念为对象的待证 TODO 删除。
- **canary 文件观察不再读半截 JSON**。`run-user-input-canary.mjs` 监听插件原地重写的诊断快照，watch 事件可能在写完前触发，`JSON.parse` 于是抛 `Unexpected end of JSON input` 并杀死 canary；现在半截读取不视为答案，等下一个事件。
- **未闭合**：release 层的 e2e（Long Stroke 场景）仍红——编排器第三轮之后停住，manager / engineer 的 lane 期望一条未发出。本轮已排除本次清理（回退清理后同样停在同一处），停点属于 cutover 之前的既有欠账：pre-cutover 基线连 shard 覆盖都不闭合，Long Stroke 场景与新的接力/工作记录语义尚未对齐。
- 验收：`node scripts/build.mjs` 与 `node scripts/check.mjs` 全绿；全量单元测试 3972 通过 / 0 失败（105 跳过、293 待证）；集成层 suites 622 通过 / 0 失败、harness 288 通过 / 0 失败、package 通过。

## Unreleased — 重启后交还子会话不再被判身份冲突

- 加载归位的作废结算（`ChildWorkRecovery.settleOrphanedChildRuns`）改回激活任务的第一件持久动作。它原先排在由 git 子进程支撑的交付恢复之后：重启后 manager 把工作交还给上一 runtime 留下的子会话时，新根的登记会抢在作废落定之前读投影，读到的仍是活跃旧根，于是子会话被判 `ActiveRunIdentityConflict`，交还回执退化成「派发回执不确定」。
- 作废事实与新根的登记事实现在按写入顺序先后落定：作废先折进投影，新根读到的是已作废的遗留 run，准入放行，交还由子会话本人接下。
- 规范：`requirements/crash-reconciliation/WHAT.md` [020] 补「该结算是加载归位的首个持久动作：作废事实必须先于下一次交接的身份判定落定，否则新交接会读到仍然活跃的旧根，把已经退休的子会话判成身份冲突」。
- 验收：`requirements/intra-participant-parallelism/tests/010.test.mjs` 的两段式重启回归此前红在交还一步，现绿；全量单元测试 3972 通过 / 0 失败（`WXS_ACCEPT_TODO=1`），`node scripts/build.mjs` 与 `node scripts/check.mjs` 全绿。

## Unreleased — Fission 准入：owner 已交付视图降为可选上下文

- Fission 准入不再以 owner 是否已交付视图为条件。`ownerViewPort` 读不到 owner 的 `js.deliver` 视图时交出「无视图」，不再回 `lifecycle_view_unavailable`；`FissionAdmission.admitReserved` 删去「owner view materialization returned empty」的整组拒绝——未交付的 owner 一样准入其 lanes，不再被判成「Fission could not establish the full replacement group」。
- lane 启动文本按 `FissionStartup.render` 的新契约拼装：有 owner 已交付视图时附带该视图，没有时只留 lane 输入。owner 视图是 lane 的上下文，不是准入前提。
- durable 事实保持诚实：`FissionAdmitted` 的 owner view blob 写的就是 lanes 实际收到的那段文本（owner 未交付时为空视图），接管聚合读同一 blob，事实与 lanes 不会各说各话。
- JS 准入面随之改语义：`ownerView` 依赖返回 null/空串表示「尚无视图」，`admit` 结果的 `ownerView` 在无视图时为 null。
- 规范：`requirements/intra-participant-parallelism/WHAT.md` [003] 改写为「初始任务由该 lane 输入组成，caller 已有已交付视图时附带 caller 当时的已交付视图；caller 尚未交付视图不构成准入阻碍」。
- 测试：`requirements/intra-participant-parallelism/tests/003.test.mjs` 的 owner view 夹具改名 `CANONICAL-OWNER-VIEW`，并新增「owner 未交付视图时 lanes 仍然准入、启动文本只带 lane 输入」一例。

## Unreleased — ToolResultBound 上限改读宿主 tool_output

- `ToolResultBound` 不再把宿主截断层的缺省值（2000 行 / 51200 字节）当作自身上限：插件 `config` hook 读取宿主公开的 `config.tool_output.max_lines` / `max_bytes`（`@opencode-ai/sdk` Config 类型），写入 `setHostLimits`；内容预算（`ContentMaxLines()` / `ContentMaxBytes()`）在绑定时从当前宿主上限即时减去标记自身的成本（2 行 / 34 字节），使 `Marker + tail` 恰好贴满而不超过宿主窗口——宿主抬高或压低上限，下一次绑定即跟随。宿主未声明 `tool_output` 时退回与传输层一致的缺省；非数字、非正数或低于标记成本的声明一律忽略，配置残缺不会放宽界限。`host-boundary-015` 新增抬高、压低与非法声明三组正反例。

## Unreleased — js.run sanity 只交参数数组

- `sanity()` 仍返回 `{ nextDone, watchVeto }` 两个 thunk，但 thunk 只拨实例状态并返回参数数组，不再自己调用 `next`/`watch`。宿主用该数组调用对应方法：`next` 必须返回 `null`，`watch` 必须一次否决。返回值不是数组，或调出的方法没有出口，整次 `js.run` 在调用点 park，什么都不执行。空操作 `() => null` / `() => false` 因此过不了自证。

## Unreleased — js.rg(argv)

- 编程面 Grep 投影为 `rg(argv)`：一个字符串数组。内容搜索固定加上 `--json`；`--files`、`--files-with-matches`、`-l`、`--count` 等与 `--json` 互斥的模式原样执行，结果收成 `paths`。返回 JSON 对象，不是文本。不追加 `--no-ignore`。

## Unreleased — PLAN.md 合规审计：生命周期统一实现的决策与工程修正

以 `PLAN.md`（Manager/Plan 统一重组，cutover 分支 `feat/lifecycle-cutover` 已并入）为规范，对现行实现逐算法（A–Q）逐条 35 个「能证伪」条件做合规审计，修正下列真实分歧；被后续决策取代的条目（LWR_prev 交接、Reopen 语义、back-to-back 无评审接力、工作计数→durable authority 身份输入、spawn 标记）逐条记录为 SUPERSEDED，未回退。

- **Reviewed 事实三字段**：内核 encode/decode 只含 `incumbencyId/stage/outcome`（work-WHAT [003]）；自证与二次评审门移入 `Commands.decide`（抛错、零写），事件不再携带 reviewer identity（wanxiangli `State.fs`/`Work.fs` + lifecycle-002 新锚点）。
- **fence 归属分类**：记录的工具调用围栏带 owner——Confirmation 复用报 `RetirementConfirmationReplayConflict`（E5），Retirement/Delivery 报 `RetirementReplayConflict`（D1.1/E1.1/E11）。
- **Algorithm E1.1 重放排序**：`DeliverTool` 先按 fence 查折叠，再谈无任期/无评审；`ToolRegistry` 的 deliver 执行门对已结算 fence 放行重放（此前 Delivered 成功后重入同一围栏会得到 `no active incumbency` 类拒绝）。work-007 新锚点三例。
- **启动 fence 重放修复**：`PlanTools.replayRecordedFence` 不再用当前状态重建 Deliver 命令（那会以 `RequestIdConflict` 冲突），直接以已落盘退休作答（[007]：重放只返回原退休结果、零写）。work-026 新锚点。
- **工作开立闭集**：`PluginTransforms` 的 S4 开立门由「非 Plan 即开」收紧为 `managerSessionOf`——DevOps/Engineer/Orchestrator 会话不再开 S4 work（探针前后对照 + work-036 新锚点「只有 Manager 会话开执行 work」）。
- **派工门 [026] 重写为 Algorithm I 匹配**：未评审拒绝；Promoted 或已确认走新的 `dispatch-closed` 文案；仅 reviewed-Stayed-未确认（修复窗）放行 DevOps 派工与修复 fork；新增资源 `resume/dispatch-closed/{en,zh-CN}` 与 capability-enforcement-026 的 D08/D09/D10。
- **后继投影**：无稳定 Host 身份的后继语义行改为拒绝装配（与 Plan 的 `TenureIsolation` 同判据），work-008 新锚点。
- **术语零残迹（G5/G8）**：8 个 owner shard 改名 `Wanxiangshu.Owner.relay-*` → `Wanxiangshu.Owner.work.*`（24 条 ProjectReference + `subsystems.json` 的 `relay` 子系统移除）；`Road`→`Work`、`iteration`→`tenure`、`assessment`→`review`（错误串与文案）、`Continue`/`Accepted` 在 e2e oracle/scenario 的遗留名；资源目录 `road-*` → `work-*`、`library/relay/quality-ledger` → `library/work/quality-ledger`；53 处源码锚点改名；work-007 的本地重实现伪锚点重写为真实字节断言。
- **契约措辞**：work-WHAT [019] 补「无任期且围栏未命中已落盘退休时拒绝；命中按 [007] 重放」的豁免句，与 [007] 及新锚点一致。
- **验收回执**：`WXS_ACCEPT_TODO=1 UNIT_VERDICT_SILENCE_MS=30000 npm run verify:release` → **PASS（exit 0，489.8s）**：format:check / check / build 绿；unit **5148 passed / 0 failed / 865 文件**；integration OK（suites **754/0**、harness **293/293**、package 组 OK）；e2e OK（4/4，`journal=458/699 sse=2037/3351`）；package OK（1692 members，闭包 + 隔离消费者）。内核 lifecycle+assembly **72/0**；`requirements/work` **190/0**；submodule `71c155f`（Reviewed 三字段 + fence 归属）。

## Unreleased — 集成层修复、委托绑定与 WorkRecord Opening 数据丢失修复、文档漂移与构建图补齐

- **集成层（integration tier）既有红清零**：`.git` 旧根夹具（change-integration-002、crash-reconciliation-020、harness 的 event-store-gate-facts、verification-system 015、work 029、repository-programming 007/011/016/018/019、knowledge-reuse 006/support 等）统一改走 `<workspace>/.Herdchestra` 状态根；`journal-observer.js` 增加 `primaryWorktree`/`stateRoot` 并让 `eventsDir` 指向生产布局；`durable-events-009` 的 shock-cut 源码扫描改指 `ProcessEventLog.fs`；rulebook 打包夹具以 `node_modules/wanxiangli` 符号链接修复解析；`distribution-008` 的 GAP-210 安装场景 TODO 落地为真实 `npm pack` + 闭包校验（package 组 4/4 绿）。
- **canary 簇修复**：`run-user-input-canary.mjs`（chronicle v2 字段、`estimated_readonly_rounds`、`.Herdchestra/diagnostics` 路径、join 渲染断言）、`run-chronicle-provider-canary.mjs`（v2 + legacy 拒绝支线）、`run-native-todo-replacement-canary.mjs`（title/chronicle 请求分流、view 边界、ViewCheckpointCommitted 嵌套事实解码）、spec-inv-013（退役 `defer` 断言）——delegation-027 11/11、host-boundary-033 8/8、cognitive-environment-015 22/22、obligation-ledger-002 2/2、spec-inv-013 13/13。
- **生产修复 1 — 子完成终态投递**：`RunLifecycle.installRun` 改用 sticky 订阅（`SubscribeTerminal`），关闭「预 provider 拒绝在订阅安装前同步通知」的竞态；既有 `stopBelongsToRun`/`completionBelongsToRun` 守卫过滤外来终态。managed-chat-execution-015 5/5。
- **生产修复 2 — 委托物理绑定（Strength）**：`StrengthDelegate.resolveSurface` 改为优先使用 Host 盖印的 frame `PhysicalUserMessageId`（在任何投影改写之前读取），wire 读取降为回退——Companion 投影的 delta 行（role=user、`source="physical-delta"`、id 为内容摘要）不再被误当 Host 地址，`owner provider run is not uniquely bound` 归零；`Diagnostic.AllowedFields` 补 `decision_id` 使 `strength-delegation-requested` 记录可见。013 常驻 canary 断言 0 unbound / 4 captures。
- **生产修复 3 — WorkRecord Opening 数据丢失**：`Materialize.fs` 的 `constitutiveStart` 与 `openingEnd` 曾取同一 frontier，constitutive 区间恒空、首个语义 part（如前置 tool result/media）在所有投影中不可见；现以唯一 `openingInterval` 权威（endExclusive = WorkRecordStart）渲染，新增 4 条真实测试（含 `includeOpening=false` 反向），GAP-109 收窄。
- **生产修复 4 — relay 后继判别器**：`NarrativeTransform.staleRetirement` 增加「本请求最后一条 user 消息在当前 logical run 下有 ChatExecution 接受证据、且 wire 中已无退休 cut」的后继条件，修复 humanroot 后继请求被误判 stale 而清空 wire；work-011 全绿（8/8）。
- **构建图补齐**：新增 16 条真实缺失的 `ProjectReference`（含 enforcer-repair → interaction-authority-projection-queries 等），focused 编译不再报未定义符号；`shard-edge-audit` 分类器按「词法可见性 + 前缀解析」提纯（524→388 条，high 92→16，合成召回 17/17）。
- **死件清理（其余）**：`GitTypes` 删除零消费者的 `IGitRawStore`/`GitTree`/`TreeEntry`/`ConvergeError` 死簇；`unified-store-gate` 的物理读观察者清单与 durable-events-019 期望同步去掉已删文件。
- **性能复测（GAP-225 CLOSED）**：事件级摘要（3398 事件冷读 296ms、热读 1ms 级、摘要与独立原始解析一致）、clean no-op（现行 [010] 语义下收敛写 0，阳性对照 43 写）、跨进程锁争用（超时抛错、释放后 1ms 重取、无锁残留）、目录解析（5 次 stateRoot 仅 1 次 git spawn）——数字与命令见 `proposals/W1-实施-WP-023-数字对比-2026-10-10.md` 复测节。GAP-038/GAP-210 复核后维持 PARTIAL（隔离消费者与归档闭包已有真实证据；仍缺一次同代完整 release 执行记录、release-only 阶段的红例、产物级注册断言与多 OS 证据）。
- **死面清理（GAP-225 残留）**：`ProcessEventLog.fsi` 的 7 个零消费者公开值（`physicalFingerprint`、`writerPhysicalStats`、`readWriterFileBytes`、`mergeWriterTextWithActivity`、`mergeWriterText`、`removeWriter`、`writerKeyOfFileName`，`WriterStreamSync`（2026-10-15 `cacf72f8c` 整模块删除）遗留）连同其专属私有助手（`fingerprintFiles`、`physicalStats`、`hashUpdateText`、`currentWriterActivity`、`earlierRemoteActivity`、`reconcileSeriesActivity`、`someWhen`、`writerFilesOf`、`removeSupersededFiles`、`localLogicalText`）与 `ProcessEventLog.fs` 中描述已删除物化缓存的陈旧注释一并从 `.fs`/`.fsi` 删除；删除前经 `src/`、`requirements/`、`scripts/`、`dist/` 全仓按词边界与 `nameof`/字符串检索确认零消费者。证据：`node scripts/build.mjs` 绿（generation 315）、`node scripts/check.mjs` exit 0；durable-events 243 passed / 0 failed（14 skipped、6 TODO）、verification-system 491 passed / 1 failed（该 1 例为并行 e2e 场景改写所致，非本清理：HEAD 的 `long-stroke.toml` 满足 003 用例钉值 `manager-current-action=11`，工作区未落盘改写后为 24）。
- **文档与台账**：`work/WHAT.md` 状态路径、`context-compression`/`cognitive-workspace`/`action-affordance`/`effect-accounting`/`process-execution`/`speculative-investigation` 的 todowrite/run/pty 陈旧词汇、`host-boundary` 与各包 `APPLIES-TO` 死路径、`INDEX.md` 10 处条款计数、`work-record` [009]/[013]（BlindPlan→Opening 冻结）、`distribution` [003]/[004]/[007] 归档边界（GAP-211 CLOSED）、4 条陈旧 `test.todo` 清理并给 obligation-ledger-003 落地真实 oracle。
- **产品边界裁定 — Host 声明的 MCP 命名空间按「可见但不准入」投影**：`StaticTools.permissionObj` 的 `"*": "deny"` 兜底此前把 Host 自己配置的 MCP 工具一并打成 deny，Host 因而从不把 MCP 工具送上 managed agent 的 provider wire（Long Stroke DELEGATE 4.1 的红因）。按 capability-enforcement-006（工具权威只由领域能力令牌授予，Host/效用工具不进 `Roles.permissions`）与 capability-enforcement-007（技术来源不得另立权限映射表）裁定：MCP 工具既不是 office 能力、也不由插件另行裁决，因此新增 `OpenCode/Host/ExternalToolNamespace`（只读 live Host config 的 `mcp` 映射，取 `enabled !== false` 的 server 名并投影为 `<server>_*` 一条通配键）与 `OpenCode/Host/ExternalToolAdmission`（运行期 fail closed：名字落在已声明外部命名空间且会话持 canonical office role 时，在任何 stage 之前以 `tool/registry/denied-external` 拒绝，零副作用）；gate 只对 managed session 生效。投影写的是**本次 config 应用自己 resolve 出的**模式，而 gate 读的声明集合**单调只增**——session permission map 的寿命长于写下它的那次 config 应用（重载可以关掉 server，HOST-012 又让同一进程的多个 per-directory 实例共享该 module 单例），集合整体替换会让旧 map 仍 advertise 的 namespace 失去门禁；单调集合保证 gate 真值始终覆盖本进程写过的任何 map（capability-enforcement-002 的 fail-closed 方向：无人 advertise 的 namespace 也拒绝），同时不把别的 worktree 的 namespace 串进本配置的 wire。`"*": "deny"` 与全部既有 per-role 边界不变（Blogger 恰为 `{chronicle}`、predictor 恰为 `todowrite-for-predictor`、Bookkeeper 全 deny、engineer/devops/manager/plan 照旧）；未声明或 `enabled: false` 的连接不产生任何键，Host 原生 `todowrite` 等名字不被该门波及（action-affordance-015 不变）。oracle：`capability-enforcement/tests/007.test.mjs` 新增 3 条 wire 侧 + 2 条 integration 运行期用例（含「后一次更窄 config 不能撤销在线会话的 namespace」）。
- **e2e 与生产收尾（release tier 绿）**：`Change/Host` 恢复 open-tenure 守卫（此前的 ContinueLoop 无界链会冻结插件进程）、`continueLoop` 对 Delivered 退休回 `Ok None`、`DeliveredTenureHandOff` 以既有 Reopened+IncumbencyOpened 对完成机器冲突后的下一 tenure 轮转、发布证据改读 `InspectedWork.LastRetiredReview`；`ToolHost`/`ToolRegistry` 的 js 面根目录改为会话注册目录（Change 作业 worktree）；`RunLifecycle` sticky 重放加「仅本 run 地址」守卫（防前任终态误结新 run）；`ExternalToolNamespace`/`ExternalToolAdmission` 按 capability-enforcement-006/007 把 Host 声明的 MCP 命名空间改为「wire 可见、执行 fail-closed」，并修掉跨 config 应用的 fail-open 分歧；Long Stroke 场景与 oracle 同步（估算字段、冲突制造、按 decision 配对、不可归因断言移除）。
- **验收回执**：`WXS_ACCEPT_TODO=1 UNIT_VERDICT_SILENCE_MS=30000 npm run verify:release` → **PASS（exit 0，492.1s）**：format:check OK / check OK / build OK / unit OK（**5135 passed / 0 failed / 865 文件**）/ integration OK（suites **754 passed / 0 failed**、harness **293/293**、package 组 OK）/ e2e OK（4/4，`[event-ceiling] journal=460/699 sse=2073/3351`）/ package OK（1691 members，闭包校验通过）。GAP-038/GAP-210 的「同代完整 release 执行记录」由本回执补齐。

## Unreleased — 全仓质量门归零：金字塔门 160 处压平、架构门两桥迁移、npm 夹具自包含选择

- **控制金字塔门归零（160 处 / 32 文件）**：按 `scripts/checks/fsharp-control-pyramid-guide.mjs` 修复手册逐处压平嵌套决策——提取有真实职责的命名决策（如 `classifyRemoveError`、`decidePortProbe`、`chooseWorkRecord`、`settleExecutionOutcome`），Result/Option 机械传播改用 `result`/`option` CE，Fable `task` CE 不支持的「条件早退」改为显式分支或拆分为两段函数；`fsharp-control-pyramid-baseline.json` 保持空表（零债），未使用任何缩进/改名/注释类规避。全仓 scanner 复扫 0 命中。
- **架构门归零**：`Foundation/CanonicalJson.fs(.fsi)` 迁至 `Host/CanonicalJson.fs(.fsi)`（命名空间 `Wanxiangshu.Foundation` → `Wanxiangshu.Host`，与 `Host/Digest.fs` 同域的 runtime-platform 桥惯例一致）；14 处消费者与 `compile-order.txt`、owner fsproj 的 Compile 路径同步，fsproj 文件名保持不变。`Foundation/GlobMatch.fs(.fsi)` 全仓零消费者（与 `JsGlobFs` 同桥冗余），连同其 owner fsproj、compile-order 条目与 `repository-programming-js-filesystem.fsproj` 的 ProjectReference 一并删除。
- **消融门归零**：`resources/ablation/tool-map.json` 删除 7 条已退役直连工具（`read`/`write`/`edit`/`glob`/`grep`/`mv`/`rm`）的陈旧映射；`AblationToolMap.tryNode` 对未知工具的既有语义（无人认领即不拒绝）不变。
- **npm 夹具自包含选择**：`verification-node-tools.mjs` 抽出可复用的声明生产依赖闭包检查 `firstMissingNpmProductionLibrary`/`assertSelfContainedNpmRoot`；`node-tool-candidate-tests.mjs` 与 `npm-install-fixture.mjs` 的候选 npm 选择改为「首个自包含安装」——本机 Arch 系统 npm（`@npmcli/fs` 声明 `semver` 但未随包分发）被跳过，`verification-system-016` 的 16 项夹具失败全部修复。
- **fantomas 兼容**：`Prose.fs`/`ProviderResources.fs` 的 `resources()?method(...)`（函数应用接动态调用）是 fantomas 6.3.0 的解析死角，改为先绑定 `let api = resources ()` 再调用；全仓 37 个存量未格式化文件一次格式化，`format:check` 由红转绿。
- **结构断言同步**（重构等价，非语义变更）：`repository-programming-031`（deliver 分派形态）、`repository-programming-041`（Parked 审计调用形态 `site.Handle site.SessionId`）、`structured-workflow-011`（CanonicalJson 新路径）三处源码结构正则随重构更新；`JsRunLoop`/`ToolsBindings` 的 `// DSL-MUTABLE:` 标注随重写恢复（structured-workflow-005 扫描 0 违规）。
- **证据**：`node scripts/build.mjs` 绿（generation 291）；`node scripts/check.mjs` exit 0（20 门全绿）；`npm run format:check` 绿；全量 unit 套件 **865 文件 / 5113 passed / 0 failed / 114 skipped / 337 todo**；定向回归（repository-programming 031/041、structured-workflow 005/011、verification-system 016）**316 passed / 0 failed**。集成层（integration tier）既有红未随本批修复：18 项失败分属四簇——`.git` 旧根夹具（change-integration-002、crash-reconciliation-020、harness）、`WriterStreamSync.fs` 陈旧引用（durable-events-009）、rulebook 打包夹具缺 `wanxiangli` 解析（behavior-diagnosis-002/005、guidance-delivery-008）、以及需要固定 Host 版本的 canary（本机 opencode 1.19.0 vs `package.json` 固定 1.18.29），另 package 组独立失败；以上不属本批回归，另行立案。

## Unreleased — JS 工具当前工作区构建恢复

- 补齐实现已有但签名遗漏的 `Prose.Rm/Mv`、对应资源路径与 `JsFailure.Abandoned`，不回退 rm/mv 或 abandon 功能。
- 将跨 park/unpark 保存的 `JsAsIsLedger` 从后编译的 `ToolsBindings` 模块移到现有 `TransactionStore` 协议；同步所有类型引用，并让事务协议先于 `ParkedSites` 编译。无新包装层、兼容别名或 `obj` 强转。
- 保留既有逐笔 As-Is 写语义。回归不再用整批追加次数判断重试，改验每笔写的唯一身份与具体失败；失败写的 Prepared 事实须跨重启保留、不得自动重放。移除源码拷贝和固定文案断言，保留实际文件效果、失败码及涉事路径验证。
- 当前工作区构建成功（generation 24），172 个公开 surface 与 829 个产物模块链接加载通过。实际启动 `opencode serve` 加载当前 `dist`，`js-manager` 完成 glob、park/unpark、review 与两段 deliver，终态中断早于工具返回，Host 最终 idle；未终止已有用户会话。
- 定向回归：repository-programming 全套、work/011、managed-session-lifecycle/016 合计 235 通过、0 失败、9 跳过、9 TODO；跳过与 TODO 不计入通过。

## Unreleased — Manager 终态交付保证停止

- `DeliverTool` 在 S4 `Delivered`（无继任者）落盘后，使用既有 `JournalSurface.appendAuthorityLogicalRunClosed` 关闭对应 Manager/HumanRoot logical run，随后等待 `Sessions.InterruptAttempt`，最后才返回“已结束”。原生产路径既漏了 logical run 关闭，也漏了 interrupt，导致模型在已退休且无权限的任期内继续调用工具。
- `Confirmed` 与有继任者的 `Relayed` 不发终态中断；不放宽 `MANAGED-SESSION-016` 的根会话保护，不删除会话、不级联取消子资源。Host 中断拒绝或抛错时，工具保留真实 Delivered 事实并报错，不伪装停止成功。
- 验证：`work/011` 的等待中断回执、SDK 拒绝与异常回归通过；work 全套 174 通过、1 项既有 TODO，managed-session-lifecycle/016 的 3 项通过。隔离 `opencode serve` 与本地脚本化 provider 实跑：Delivered 的 interrupt 请求及确认均早于终态工具返回，Host 最终 idle，Manager 只请求 3 次模型；Relayed 无 interrupt，继任者发出第 4 次模型请求并保持 busy。额外运行的 interaction-authority/018 有 1 项因测试仍读取旧 `.git/wanxiangshu/events` 路径而失败，另有 2 项既有 TODO；不宣称该套件全绿。

## Unreleased — 终态投递必达：子完成不再被静默吞掉

- 事故（2026-10-16，KTraderNew 会话 `ses_ebcc542feffeWWeVr6kxBLsBWW`）：DevOps 子会话 run 于 07:21:13 到达 `Terminal(Completed)`（journal 有事实），父侧却无任何 `HandleWorkCompleted` 落账；manager 的 join 永久等待，直到 07:35:28 关停才以 `Cancelled` 收场。前 5 次同类完成都在 0.2 秒内落账并唤醒父侧——第 6 次事件发生了，却没有触发正确的结果。
- 根因一（join guard 扣留无出口）：`handleCompleted` 在 `outstandingBackground`（DevOps 有未 join 后台/活 PTY）时扣留完成、只发 JoinGuard nudge；但 nudge 的 `NotSent`/`Superseded`/`AdmissionRejected` 出口与无 idle permit 的场合都静默返回，而 reconcile pass 已把该 published turn 去重——此 terminal 永不再被重放，扣留即永久悬挂。修复（`OrdinaryTurnWorkflow`）：`applyJoinGuardNudge` 返回类型化决定，只有 `Sent`/`AlreadyOutstanding`（存在 durable continuation claim）或已发布 Failed 的场合才扣留；否则立即发布真实完成。
- 根因二（完成结算故障吞 Source）：`installRun` 订阅回调里 work-record 读取抛错或 `complete()` 在 claim 前抛错时，异常被 `observeOwnedWork` 存进 `ownedWorkFailure` 只等关停重抛，`run.Source` 永不 settle——Fork await 与 join 双双悬挂。修复（`RunLifecycle`）：work-record 读取故障降级为空记录并记 `FORK-WORKRECORD` 诊断；`complete()` 故障走 `deliverFailedCompletion` 显式落账可 join 的失败，兜底 `trySetResult` 保证 claim 后 Source 必 settle；`startClaimDelivery` 整体 try/catch，结算路径自身故障也 settle 为 PERSIST 失败。`HostForkRuntime.Complete`（sync-delegate/Fission 入口）同样加固。
- 根因三（capture 失败吞完成）：`TerminalReporter` 在 XTrace 终态捕获失败时直接返回 `CaptureFailed` 不发 `NotifyTerminal`——与同库其它 capture-error 语义（SyncDelegate `FailCall`、插件 fail-closed）相悖且父侧悬挂。修复：捕获失败发布诚实 `Failed("terminal trace capture failed")`，绝不静默。
- 根因四（fan-out 无隔离）：`HostEventPort.notify` 同步循环派发，任一订阅者抛异常会饿死后续订阅者（含写完成单元的 Fork lifecycle）并把异常带回已提交终态的发布方。修复（`Events`）：逐监听器 try/catch 隔离，故障记 `EVENT-DISPATCH` 诊断，其余订阅者照常送达。
- 回归：新增 `requirements/managed-session-lifecycle/tests/028.test.mjs`——(1) work-record 读取注入故障后完成仍落账为 `CompletedAwaitingJoin` 且 join 可消费；(2) 经物理 Notify 边界投递的 terminal 必产生可观察终态，join 必以显式结果终止。`createRuntimeWithWorkRecordRead` 钩子扩展到 child work-record 读取路径。managed-session-lifecycle（022 的 2 个失败为基线既有：其一为 `.git` 环境问题）、interaction-authority、delegation 全套件绿。

## Unreleased — 接力 no-op 预案：背靠背无评审接力拒绝

- 事故（2026-10-16，`ses_ebc92c052ffe3CaPXlX5vjq0ms`，22 条 record）：S1 Stayed 修复后模型连续 17 次 `deliver`，其中 13 任从未评审即接力（07:41 内 13 连跳，游标每跳 +5、阶段钉死 S1）。每次接力退休未评审任期、开同阶段继任；继任读到的 `LWR_prev` 是上一棒的交棒散文，宿主注入的 Plan Mode reminder 触发新回合，模型复判「义务已履行」再次交棒——接力自增强成 no-op 死循环，规划收束永不发生。根因：内核允许任意背靠背的无评审接力（[004]/[013] 只保护单笔不被拒），链长无上界。
- 预案（内核硬门，`wanxiangli/src/lifecycle`）：`Work` 新增折叠派生 `LastRoundWasUnreviewedRelay`（读退休轮次的 `reviewOutcome`/`outcome`，不新增事实或第二账本）；Plan `Deliver` 在「本任未评审 且 上一笔已落盘退休是无评审接力」时返回 `ReviewRequired` 观察、零写、本任不退休。单笔无评审接力仍合法；背靠背第二笔被拒，runner 提交本任 `review` 后接力恢复、链长归零。S4 不受影响（本就要求评审）。
- 宿主接线：`PlanTools.settlePostS3Transition` 补 `ReviewRequired → Error "assessment required"`（`interpretDeliverReceipt` 原已有同映射），Plan 接力路径经 `enterPostS3OrRelay` 的拒绝得以呈现；`tool/deliver` 双语提示词写明背靠背门与「先 review 再交棒」；`tool/deliver/assessment-required` 双语措辞由陈旧的「八维质量评分」改为三问 findings 语义（[038] 本就禁止八维必填）。
- 契约同步：work WHAT [004]/[013] 的「无评审 deliver 不被拒」限定为单笔并交叉引用 [025]；[025] 接力契约写入背靠背门（折叠派生、不进事件载荷、拒绝为 ReviewRequired 零写）。wanxiangli lifecycle AGENTS.md SPEC 2 同步。测试：wanxiangli `002.test.mjs` 新增背靠背拒绝与评审恢复反例；work `025.test.mjs` 连跳夹具改为每任先评（Stayed 保持阶段），新增「第二笔连续无评审接力拒绝零写、评审后恢复」反例。

## Unreleased — lifecycle-cutover 合并前收口

- Manager `deliver` 原子入队后继提示与禁用 `ask`：（1）Manager 两段式 `deliver` 的第二次调用以 `Relayed`（`Stayed`）退休时，`DeliverTool` 在工具返回前立即通过 `ContinueManagerLoop`（`ManagerWorkflow.maybeDeliverLoop`）把后继提示（`runtime/manager-assess`，「你是接手此任务的第 N 个 Manager...」）排进队列，随后才返回 `deliver` 结果，消除 `deliver` 返回与后继提示入队之间的空窗期（避免旧回合在后继提示入队前先把下一轮请求发到 LLM 侧）；`BeginPhysicalProviderAttempt` 同时跳过已被 `RelayNarrativeTransform.isStaleRetiredAttempt` 识别的旧任残余请求，交由 `relay-projection-cut` 直接清空并停止；（2）Manager 禁用 `ask`：从 `OfficeCapability` 的 Manager 权限集（含收尾与清理阻塞窗口）、`PromptFactFold.foldUserQuestionAsked`、`ToolRuntimeScope.AskUser` 以及 `role/manager`、`work/manager-continue`、`tool/ask` 双语资源中移除 Manager 的 `ask` 准入，`ask` 仅向 Plan 开放。
- Plan 提示词、评审返回与 `post-S3` 交付大修：（1）`role/plan`、`planning/s1`、`planning/s2`、`planning/s3`、`tool/review`、`tool/deliver` 双语提示词明确 `S1` 评审的是离能晋级 `S2`（`S2` 能开始）的差距、`S2` 评审的是离能晋级 `S3`（`S3` 能开始）的差距，不再把下一阶段未完成的工作误判为当前阶段的差距；（2）`review` 空集晋级时，`ReviewShell` 当场在工具返回值中附带下一阶段的方法要求（`planning/s2` 或 `planning/s3`），本任即可按新阶段要求开始第一棒写作，无需等 `deliver` 后下一任才能看到；（3）`S3` 持权任期调用 `deliver` 改为将规划结算进 `post-S3` 态、永久关闭该会话的自动催促（nudge），并在 Host 确认工具结果完成时像 `ask` 一样中断（interrupt）当前对话等待用户，不直接启动 `S4`；用户在 `post-S3` 态下输入指令要求执行 `deliver` 时，模型调用 `deliver` 即可启动无上级的 `S4`（Manager）执行会话，且用户可反复要求启动 `S4`。
- Plan 第一条消息在同一条 transform 里被开两次。`tenure-isolation` 先按 S1–S3 打开，`capture-xtrace` 随后无条件调用 `ensureManagerWorkOpened`，按 S4 再开同一 session，报 `existing lifecycle work has a different sequence`。另外 `PlanWorkOpenWiring` 曾把 root 写成 `.Herdchestra/wanxiangshu`。现已改用工作树根，并只在非 Plan 会话上走 Manager 开立。
- Plan 任期隔离自 `9e81ebf` 生命周期切换起被存根为 no-op（`applyResolvedTenure` 注释「Stopped. inspect has no work sequence」），Plan 会话每一任都不再剥除前任 assistant/tool，post-S3 因此混入前面所有若干任的上下文并触发无稳定 XTrace 身份的报错。`InspectedWork` 现已带 `OpeningCursor`/`PredecessorRange`/`Stage`，据此恢复 `buildTenureOf`/`previousTenureRecord`/`isolatePlanTenure`/`applyResolvedTenure`：活跃任期按 openingCursor 剥除并注入 `LWR_prev`；新增 `Delivered` 分支（post-S3）以最后退休游标为界剥除 S3 及之前全部 assistant/tool，且不注入 `LWR_prev`（post-S3 不继承前文）。为此在 `InspectedWork` 增 `LastRetirementCursor`。新增 work-034「post-S3 projection strips all S3-and-earlier context and injects no LWR_prev」回归。
- Plan 未授权 `deliver` 报 `Relay requires nextOwnerId`：S1 评审未晋级时内核要接力并开下一任，`PlanTools.executeDeliver` 没传 `nextOwnerId`。未授权或有阻塞时现以当前 work id 作为下一任 owner。
- `wanxiangli` 改回跟踪 `main` 的正式 submodule，npm 依赖为 `file:wanxiangli`。运行数据只写主工作树 `.Herdchestra/`。应用不再安装 Git hook、不再改 SSH，也不读写 `refs/wanxiang/*`。仓库内不再跟踪任何 `.tgz`。
- Lifecycle SQLite 宿主边界不再静态绑定 Node：`DurableWorkHost` 统一按实际 JS 运行时选择 Node `DatabaseSync` 或 Bun `Database`，Surface 的内存 probe 复用同一入口。新增 work-047 在 Node/Bun 真实加载、持久开立、关闭与重开；修复前 Bun 在模块加载阶段即因 `node:sqlite` 失败。
- Plan 持权交付恢复 wanxiangli 的真实外部效果边界：只有首次 `Committed + DeliveryRequested` 可启动；`AcceptanceUnknown` 与创建异常保持 pending，不再伪装 `NotStarted`；同请求 replay 只观察原 operation。启动前重新核对计划 digest/path，已变更则明确 NotStarted。work-039 从原先实际未触发 spawn 的假绿测试改为真授权 S1→S3 路径，验证原文 prompt、execSessionId 与 unknown/reconcile/retry。
- 交付依赖改为仓库跟踪的 `vendor/wanxiangli-0.5.2.tgz`，移除 superproject 的无 `.gitmodules` gitlink；根 `engines.node` 收紧为 `>=22.13.0`。0.5.2 在 `DeliveryRequested` 与 prompt 接受之间增加 durable `DeliverySessionBound(operationId, execSessionId)`：真实 session 创建后先绑定身份，再发送首 prompt；重启只观察该 session，不重复创建或重发。最终 wanxiangshu tarball 通过 `bundleDependencies` 自包含 wanxiangli，release verifier 将其作为唯一允许的 `node_modules` 闭包逐字节校验，并删除消费者顶层副本后再导入插件。

## Unreleased — Fission lane 执行接纳修复（现场停摆根因）

- 现场症状：`fission` 开出七条 lane 后立刻全部停住，Manager 的 `join` 永久挂起。日志每条 lane 都是 `HOST-BOUNDARY-008: provider attempt plan freeze failed (accepted-execution-missing)`。
- 根因：`PromptDispatcher.AcceptManagedChatIntent` 用 authority profile 的 `SessionId` 而不是本次 intent key 的 session 去写 `ChatExecution.Accepted`。对普通 child 两者相同所以一直没暴露；对 Fission lane，profile 是 logical owner 的，于是 accepted execution 记在 owner 名下，而 lane 自己的 transform 按 `(laneSessionId, physical)` 查表必然落空。
- 修复：`PromptAuthority.withSessionId` 把已接受证据重新绑定到本次 intent 的 session。`Dispatcher.AcceptManagedChatIntent` 的 `accept` 现在接收 intent key 的 session；`AgentJournalPortAdapter.forWire` 把 `activeProfile` 投给当前 session。`SyncDelegateRuntime.issueCurrentOwnerIdentitySeed` 由自带的重复实现改为复用 `PromptAuthorityProjectionQueries.issueCurrentOwnerIdentitySeed`，lane 因此也能为 fork 子会话签发继承身份。
- 回归：新增 `requirements/intra-participant-parallelism/tests/003.test.mjs` 的真实注册插件用例——真 `fork` 建立 Engineer，再 `fission` 两条 lane，逐条断言 `ChatExecution` 处于该 lane 自己的 `accepted ∧ providerStarted` 且 transform 不再以 `AcceptedExecutionMissing` 拒绝。修复前该用例红，修复后绿；证据已存档。
- 重启后残留未结 `FissionGroup` 吞掉重托完成事件（二次挂起根因）：前一次运行里 `ruling-materials` 调用 `fission` 写下 `FissionAdmitted(Terminal = Open)` 后进程重启；`ChildWorkRecovery` 只作废了子会话的 `ActiveLogicalRun`（`ChildWorkVoided`），没有在启动恢复阶段结算残留 `Open` 的 `FissionGroup`。重启后 Manager 用 `resume` 重托同一个子会话 `ruling-materials`，子会话跑完 15 步在 14:38:51 正常结束（`ChatExecution.Terminal(Completed)`），但 `HostTurnObserver` 与 `FissionHost.observeLaneTurn` 查 `FissionProjection.tryActiveForOwner` 仍命中旧的 `Open` group，把该子会话当成“已被 Fission 取代的旧身体”静默吞掉了整个完成回合（不记 `TerminalOutputCaptured`、不写 `HandleWorkCompleted`、不唤醒 `PendingRuns`），导致 Manager 随后 `join()` 永久挂起。修复（`intra-participant-parallelism-010`）：`ChildWorkRecovery.settleOrphanedChildRuns` 在启动恢复阶段同时扫描 `ActiveByOwner` 中仍为 `Open` 的孤儿 `FissionGroup`，追加 `FissionFailed(reason = "process_interrupted")` 并调用 `FissionRuntime.clearOwner`，使 `ActiveByOwner` 与进程内静默中断标记一并清空。新增 `requirements/intra-participant-parallelism/tests/010.test.mjs` 双插件实例重启回归（修复前 `join` 超时挂起，修复后正常收到重托子会话报告）。
- `ManagerWorkflow.ensureManagerWorkOpened` 幂等门缺失导致每次 transform 重写 `WorkOpened` + `IncumbencyOpened` 并触发 `ProjectionCutTail`（CPU 与 I/O 风暴根因）：`ensureManagerWorkOpened` 原先硬编码 `alreadyOpened = false`，导致每个回合的 `CaptureXTraceMessages` 都向工作区 EventStore 追加一对重复的 `WorkOpened` / `IncumbencyOpened` 并被 `ProjectionCutTail("second WorkOpened")` 截尾。修复：调用 `LifecycleKernel.openWork` 前先由 `LifecycleKernel.tryWorkOfStore held sessionId |> Option.isSome` 判定 `alreadyOpened`。新增 `requirements/work/tests/036.test.mjs` 连续两次 transform 不重复追加生命周期开立事件的回归。
- 第二任 Manager 接棒时旧任收尾回合在 `chat.params` 抛 `PROMPT-006` 杀死新任（第二任 Manager 停摆根因）：第一任 Manager 以 `Stayed` 完成两段式 `deliver` 后，Host 继续进入 `deliver` 之后的下一步 LLM 请求。`ApplyRelayProjection`（`RelayNarrativeTransform`）判定该请求属于已退休尝试，调用 `continueAfterRetiredAttempt`：（1）原先在 `stopRetiredAttempt` 里直接调了 `ModelRouting.releasePhysicalExecution`，并且对 root 会话调用 `InterruptAttempt` 被 `MANAGED-SESSION-016` 拒绝（不调用 `session.abort`）；（2）紧接着同步调用 `deliverSuccessorPrompt` 发送第二任 Manager 的接棒提示（`ManagerGuard`），其 `chat.message` 准入立即在同一会话上将第一任的物理消息标记为 `supersededPhysical` 并替换为第二任的 active lease；（3）由于 OpenCode Host 在 `experimental.chat.messages.transform` 把 `output.messages` 清空为 `[]` 后**仍会继续调用本次请求的 `chat.params`**，且传入的 `input.messageID` 仍是第一任已被释放/取代的旧物理消息 ID，`ChatParamsHook.validateManagedObservation` 查 `readAdmission` 得到 `None` 而 `isManagedExecution` 为 `true`，当场抛出 `PROMPT-006: managed provider run 'manager' ... has no committed execution lease`，直接炸断了正在启动第二任的会话循环。修复：（1）`PluginTransforms` 的 `stopRetiredAttempt` 只保留 `ModelRouting.suppressProviderStep` 归还 step token，不再提前调 `releasePhysicalExecution`（由后继 `chat.message` 原子取代）；（2）`ChatParamsHook` 对 `ModelRouting.wasExecutionSuperseded key = true` 的已被同会话新任原子取代的旧物理消息放行并投影温度，不再在被 `RelayNarrativeTransform` 拦截清空的旧任收尾请求上误抛 `PROMPT-006`。`requirements/work/tests/011.test.mjs` 补充后继接棒与被取代旧物理消息经 `chat.params` 不抛 `PROMPT-006` 的回归断言。
- 证据：`node scripts/build.mjs` 绿（generation 246）；intra-participant-parallelism + interaction-authority + managed-chat-execution + managed-session-lifecycle + provider-attempt-recovery + crash-reconciliation + execution-model-routing + work（`WANXIANGSHU_PROVIDER_LANGUAGE=en`）全绿。

## Unreleased — 生命周期统一：Plan 与 Manager 收成一条内核

- `Mission/Lifecycle/`（Facts/Fold/Codec/Kernel/Surface）成为唯一生命周期内核：开立、绑定、评审、一段式与两段式 deliver、恢复位置、重开、权威推进都走这一套事件（`WorkOpened`/`DevOpsBound`/`IncumbencyOpened`/`ConfirmationCommitted`/`IncumbencyRetired`/`Reviewed`/`Reopened`/`AdvanceCommitted`/`RetirementBlocked`），结果名只有 `Relayed` 与 `Delivered`，评审结果只有 `Promoted` 与 `Stayed`。序列写在 `WorkOpened`：S1–S3 为 Plan，S4 为 Manager；确认次数由序列导出。
- `deliver` 只有一个注册名与一份资源。准入同时接受 Manager（Finality）与 Plan（Deliver），路径按工作序列分派：S4 走两段式，S1–S3 走一段式。删掉了 `planningSpecs()` 里重名的 `deliver` 规格——JS 对象字面量后写覆盖先写，Manager 的 deliver 曾因此被 Plan 规格遮蔽并答角色拒绝。
- Manager 侧改读统一事实：`DeliverTool` 的绑定、评审位、冻结刷新都读 `Lifecycle` 当前折叠，不再读 Relay fold；`ReviewBackendManager` 的评审写入改为 `LifecycleKernel.review` 提交 `Reviewed`（binding 仍先校验，八个字段不进事件）；`Workflow` 的 S4 资源选择不再要求 Relay `RoadView`，`runtime/manager-finish` 的 `{{closeout}}` 由 `ManagerWorkflow.finishCloseoutText` 统一填充。两段式收口模块与分片改名为 `DeliverTool` / `mission-deliver-tool`。
- Plan 侧改读统一事实：`PlanWorkOpen` 开立改读 `Lifecycle` 当前折叠、并按开立时观察到的 XTrace 头写 `openingCursor`（缺头序号零写拒绝）；`ReviewPlanBackend` 的活跃任期门改读 `Lifecycle`；`PlanTools.executeDeliver` 改为 `LifecycleKernel.deliverOneshot`，退休游标由 deliver 自身的 XTrace 前沿提供。
- Change 发布门改读统一事实：`RelayPort` 增 `LifecycleReviewed`，`buildPublicationEvidence` 由该事实（`Promoted`/`Stayed` + 工作权威计数）构造，无持久评审即零写拒绝；证书与工作区快照不再是输入。
- `OfficeCapability.permissionsForManagerFacts` 恢复 `capability-enforcement-025` 的语义：无活跃任期空集，有确认无评审空集，清理阻塞收窄到 Join+Finality，末任保留读/清理/收口，已评（有 findings）移除评审专用只读权限。
- 测试资产：新增 `Mission/Lifecycle/Surface.js` 的存储播种面（`seedOpen`/`seedReview`/`seedDeliver`/`workView`），`plugin-fixture.mjs` 的 `openIncumbency`/`injectAcceptedAssessment`/`completeManagerLife`/`activateLife` 改为向插件的工作区 EventStore 播种真实统一事件；统一契约测试迁到 `requirements/work/tests/011…019`，锚点 `WHAT[work-NNN]`；`planning` 的 007/008/020/022/023 与 `capability-enforcement` 024–028 按统一契约重写。
- 后继派发与旧尝试停止改由统一事实决定：`NarrativeTransform` 的陈旧尝试判定读 `LifecycleWork`（S4、当前任期已 Relayed、后继尚未 review），不再读 Relay fold；没有统一 work 的会话完全不动消息集，首任不剥除。统一 S4 `Delivered` 退休在 Journal 投影里收束 human-root authority 与 attention life（`Fold.settleCompletedLife`，由 Journal 集成规则接受 `IncumbencyRetired` 驱动）。`DeliverTool` 从本次调用的 owning exchange 观察退休游标，两段式的第二次调用才能退休。`JournalHandle` 暴露其 writer 持有的 store，journal 启动集成器注册生命周期规则，测试因此能经 handle 播种统一事实。
- 证据：`node scripts/build.mjs` 绿；`requirements/verification-system/tests/run.mjs` 5013 passed / 0 failed（827 文件）。唯一非通过容器是 `/tmp/failed-test-file-*` 下两个刻意的坏夹具（断言坏夹具必须失败）；runner 退出码 1 来自其 pending-todo 策略。
- 旧轨删除（PLAN.md `Algorithm N`）：第二套词表两族都已删除——`RelayEvent`/`RelayTransaction`/`RelayFactCases`/`IncumbencyOpening`、relay 折叠与其 JS 面、`AgentFact.Relay`、`foldRelay`、`AppendRelay`、`Relay` 投影槽、`Envelope` 编解码条目，以及 `PlanEvent`/`PlanWorkState`/`PlanFold`/`PlanStage`/`PlanEventStore`/`PlanEventCodec`/`PlanEventTypes`/`PlanRecovery`/`PlanActionGate` 与 `Mission/Planning/Surface` 的事件面。仍然读它们的地方改读统一折叠（manager loop 信号、continuation、权威推进、能力事实、序号、退任检查、首次 DevOps 绑定与冲突重绑拒绝、Plan 阶段资源、崩溃恢复）。`PlanTools` 的旧 deliver 决策链删除，ask 经内核绑定 DevOps。`LifecycleCodec.absentOrString` 修正：present JSON null 视为 absent（此前未绑定 target 的 `DevOpsBound` 追加成功却被折叠丢弃）。
- 命名收口（PLAN.md `Algorithm L1`）：`Mission/Relay/` 迁到 `Mission/Work/`，命名空间 `Wanxiangshu.Mission.Work`。删掉零引用的 `RoadId`、`AssessmentId`、`IncumbencyPhase`、`RetirementSummary`、relay 本地 `PhysicalUserMessageId` 与重复的 `RetirementOutcome`。`AssessmentFinding`/`AssessmentFindings`/`AssessmentBinding` 改为 `ReviewFinding`/`ReviewFindings`/`ReviewBinding`。`EnsureRoadDevOpsBound`、`ensureManagerRoadOpened`、`TerminateRoadResources` 改为 Work 名。`ManagerLoopGate.gateKind` 仍发出 `manager-loop:`。`scripts/check.mjs` 另有一处本工作之前就存在的红：`fsharp-control-pyramid-baseline.json` 为空表。

## Unreleased — git-hook 关闭开关

- `src/Wanxiangshu/Git/Hook/Dispatcher.fs`：新增 `WANXIANGSHU_DISABLE_GIT_HOOKS=1` 操作员开关。置位后 `HookDispatcher.ensure` 不再安装 `reference-transaction` / `pre-push`，并在启动时移除已安装的自有 hook（外部 hook 保留）；同步与 SSH multiplex 一并跳过。未置位时行为不变（默认开启，符合 durable-convergence-008）。

## Unreleased — js-plan 复用共享锚点教程

- `js-plan` 的模型可见描述不再使用独立短教程。它复用其它 `js-*` 的同一套教程：有序锚点、`text(from, to)`、精确 edit、结构重组和失败记忆。教程签名槽按工具填写，Plan 面为 `file(matches = [])`、`edit(changes)`、`rewrite(newText)`；仓库面仍为带路径签名。
- `tool/js-plan` 只保留单一底稿、禁路径和写前评审这三项作用域约束。新增一份无路径、按底稿职责成形的双语样例 `tool/js-program/ultra-plan`。公开基类只展示无路径方法签名，不暴露运行时内部失败对象。
- `requirements/planning` [011] 增加工具描述复用条款；`011.test.mjs` 验收中英文交付描述都包含共享锚点教程且不含路径参数。
- 证据：`node scripts/build.mjs` 绿（generation 214）；planning-011、planning-014、repository-programming-002、repository-programming-005、repository-programming-022 共 20 项全绿；`language-parity-gate` 334 项资源对等绿。

## Unreleased — Plan 最后回合丢失修复（planning-007 / 017 / 021）

- Plan 发给 LLM 的请求在任期隔离阶段丢失最后回合。根因是 `TenureIsolation` 与真实 Host 消息形态脱节：`AssistantMessage` 没有 `cursor` 字段（SDK types.gen.d.ts:213-247）、工具结果挂在 assistant 的 `parts` 上，而隔离把「消息数组下标 + 缺失的 cursor」当任期归属证据；同时把全部 U 提前再拼 assistant/tool 打乱因果顺序，非首任更在无 cursor 时整段丢弃当前 assistant/tool。生产接力另有两处语义缺陷：退休游标用 `openingCursor + 10` 伪造、上一任期 LWR 回调固定返回空串。
- `src/Wanxiangshu/Mission/Planning/TenureIsolation.fs`：`assemble` 接受规范 `TenureMessage` 列表（Id/Role/IsSynthetic/HasSemanticParts/Raw）与 `messageCursors: Map<string,int64>`；任期归属由稳定 Host 消息身份在 XTrace 中的**首次观察游标**决定，跨 epoch 稳定，工具调用与其结果作为整段物理交互保留或剥除；按原交错顺序输出，`LWR_prev` 注入在首条本任物理消息之前，无本任消息时置于历史 U 之后。合成 LWR 行在装配前剥除，仅注入一条当前记录；非空上任范围缺 LWR、或语义 assistant/tool 无规范身份，一律 fail-closed，不静默丢消息也不放行旧交互。`ReanchorRequested = IsFreshHandover`（每任期一次），不再每次空停重发。
- `src/Wanxiangshu/Mission/Planning/Surface.fs` + `src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs`：`assembleTenureMessages` 现取 4 参（末位 id→bigint 游标映射），生产隔离改为在过滤前用 `XTraceCapture` 以稳定 Host 身份捕获本请求全部新观察物理消息（重试 continuation 行保位不产生语义 part），再由 `XTraceProjection.hostMessageFirstCursors` 供游标；`TenureReanchored` 事实改为 await 提交，经 `PrefixEpochProjection.isTenureReanchored` 去重，失败即抛出，不再 fire-and-forget。`executeDeliver` 契约收紧为必填 `int64` 退休游标（不接受 option/缺省），无证据在任何写入或 spawn 前失败。
- `src/Wanxiangshu/Mission/Planning/PlanTools.fs` + `src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs`：删除 `openingCursor + 10` 猜测。生产 deliver 通过 `ToolRuntimeScope.Snapshot` + `XTraceCapture.captureSessionMessagesWithReceipt` 取本次 deliver 所属**exact** 物理 assistant/tool 交互（`ctx.ProviderRunId`/`ctx.ToolCallId` 身份验证）的 XTrace 消费前沿作退休游标；后继 `openingCursor =` 退休游标（consumed-through，不再 +1），deliver 期间到达的更新用户/其它 provider run 不扩大本次退休范围；快照/journal 缺失、owning 身份不符或捕获失败均拒绝接力、零写入。
- `src/Wanxiangshu/Mission/Planning/Fold.fs`：新增 `PredecessorRange`（`PlanWorkState` 字段），继任开任时由 `LatestRetirement` 派生，`Delivered`/`Reopened` 原样保留；重开不再把自身交付记录当作上任。`PluginTransforms.buildTenureOf` 改读 `PredecessorRange` 作 LWR 上任范围。
- `requirements/planning/WHAT.md`：[007] 明确退休游标必须来自 exact 物理交互的 consumed-through XTrace 边界，禁止固定增量/下标/缺省伪造；[017] 重写为规范身份归属、因果顺序保留、LWR 注入点、fail-closed 与每任期一次 await 重锚；[021] 明确 reprompt 保留继承的上任范围。
- `requirements/semantic-trace/tests/002.test.mjs` 与 `requirements/planning/tests/support/run-plan-request-loss-smoke.mjs`：前者用 canonical XTrace 跨 epoch 事实驱动 Plan 装配（追加 part 与重锚复制都不能把旧消息变成新任消息）；后者用真实安装 OpenCode + 生产插件 + 严格 mock provider 捕获实际 HTTP 请求体，断言最新用户回合与最新工具结果在接力前后完整保留。
- 退役：`TenureIsolation.assembleAskContinuation`/`messageOfRaw`/`tenureOfRaw`（两段式 ask 协议遗骸）、`extractInt64Opt` 的数组包装与数字回退、`host-canary-plan-relay.test.mjs`（`test.todo` 假 canary，行为并入编号用例）。测试夹具收敛到单一 `{info, parts}` Host 形态加 `msgId`/`msgRole`/`msgText` 访问器，不再依赖消息上的 `cursor`。
- 修复两处测试自身的语义笔误：`007` 连续三接力实退休 3 个任期（原断 4）、`021` 上任范围继承改走 planning-004 授权的合法三段 ladder。
- 证据（2026-10-12 终态）：`node scripts/build.mjs` 全量绿（generation 462，1550 items、175 surfaces、845 modules）；planning + semantic-trace-002 + execution-model-routing 45 文件 215 passed / 0 failed / 8 todo（todo 均既有设计内）；host-boundary 集成层（`WXS_TIER_INTEGRATION=1`）33 文件 278 passed / 0 failed / 3 todo（019/029/032 三条设计内 canary todo）；真实安装 OpenCode 1.18.29 + 生产插件冒烟实跑 5 个 provider 请求，最后回合、工具交换与接力后继断言全绿；两债务门（fsharp-control-pyramid、js-boundary-gate）对改动文件 0 违规。

## Unreleased — Plan 模式（接力式规划协议）完整实现

- Plan 模式按 `feat-PLANNER/001.md` D1–D20 重写交付（`24babfac8`）：Role.Plan 四工具集 `js-plan`/`ask`/`review`/`deliver`（handoff 并入 deliver、resume 退役）；`review` 晋级硬门（与 Manager 共用同一工具、每任一次、评审目标 = 下一里程碑）；`deliver` 双路径（无权接力退休开下任、持权交付并起无 parent 的 Manager 执行会话）；`ask` 同步委派固定 DevOps（取代两段式）；自然停止（Plan 空停等用户 prompt）；tenure-isolation 与前缀重锚、崩溃恢复保留。
- 后续 `8af4fc11c` 合并重复条款并补齐测试锚点：requirements/planning 包现为 20 条条款、74 项测试（73 过 + 1 canary todo 设计内）。

## Unreleased — Blogger Provider 容量泄漏修复（GAP1 / GAP2）

- `managed-session-lifecycle` [026]（`e0527f7bb`）：Main 会话收束（宿主 `SessionDeleted` 且有 durable `CompanionBloggerLinked`）时，级联结算每个 linked Blogger 名下全部已准入 execution——pre-provider 复用 `PreProviderSettlement.settle`、after-provider-start 复用 `ManagedChatProviderLifecycle.terminal`，durable 提交确认后逐个 exact 归还容量；`AlreadyApplied`/`StaleFence` 幂等接受，`Conflict` 不吞没。
- `managed-session-lifecycle` [027]（`4133e0aa2`）：Session 收束（`ClearSession`）时取消自身与每个 linked Attached InternalLeaf 名下的全部 retained continuation input，并以 `admissionOwner.ReleasePhysical` 的 exact 路径回放其挂起的旧 credit 归还；`AlreadyApplied`/`StaleFence` 幂等接受，`Conflict` 显式暴露（带 exact key 的 `InvalidOperationException`）并保留挂起记录；不触发 `ReleaseExecution`/`ReleaseSession` 等 force 路径，重复收束幂等；`surface` 补 `sharedRetainContinuationInput` 对称暴露 shared runtime 保留操作。
- `managed-session-lifecycle` [027] 失败隔离：`ClearSession` 的 Main 自身结算/保留取消/释放、每个 linked Attached InternalLeaf 的结算/取消/释放、以及无条件 per-session 清理组逐段隔离，任一步失败不跳过其余独立义务，首个真实失败在其余义务完成后重抛；`PluginRuntimeScope.DisposeSession` 承接失败后仍执行其后续清理组。
- 对应测试：`requirements/managed-session-lifecycle/tests/026.test.mjs`、`requirements/managed-session-lifecycle/tests/027.test.mjs`、`requirements/managed-chat-execution/tests/015.test.mjs`、`requirements/provider-attempt-recovery/tests/024.test.mjs`；运行结果见提交时的工作记录。
- `managed-chat-execution` [015]（`fa40d6fbb`）：transform 在 provider 启动边界拒绝执行（attempt plan freeze 失败）时，对 exact `Accepted ∧ ¬ProviderStarted ∧ ¬Terminal` 执行写 typed pre-provider `Failed` terminal，durable 提交确认后精确归还其容量；projection 中无该 key 时不伪造结算、交准入侧；原拒绝异常仍照常上报。
- `provider-attempt-recovery` [024]（`2da1be87e`）：加载归位废弃 stale Blogger open request（`stale-open-at-load`）时，同源 `Accepted ∧ ¬ProviderStarted` 执行一并定夺为 `Failed` 并在 boot 场景发出幂等 release 请求；已启动/已终态与其他 session 不动，重复加载幂等。
- 剩余边界：终态拒绝分支显式化（GAP-227）、全量 boot sweep（GAP-228）登记于 `requirements/GAP.md`。

## Unreleased — 开发者意见 W1–W6 施工

- W1 持久化与工具链：插件路径统一 `<git-common-dir>/wanxiangshu/`（WP-001）；ndjson 事件行内嵌载荷、取消旁挂 payloads（WP-021）；UTC 日期分组 GC（WP-022）；writer 增量读与有界锁等待（WP-023，修复前后数字对比未验证）；git hook 修复后默认重开（WP-024）。
- W2 provider 与恢复：运行时漂移校验退役、交测试期性质保证（WP-003）；重启后首个新指令 prepend 状态指导（WP-020）；horizon 崩溃后不泄露未决工作（WP-025）；provider 可见文案改称「事实」（WP-026）；失败恢复去刻板实现（WP-027）；assume 穿透 LWR 核对（WP-028）。
- W3 工具面与通信：celebrate/regret 整包退役（WP-036）；enough/abandon 合入 assume（WP-029）；defer 新消费语义与 Pair Hint 鼓励（WP-030、WP-031、WP-032）；会话自动订阅自身名字（WP-033）；publish 至 user 弹窗并复制 root（WP-034）；邮箱语义双语解释（WP-035）；fork/commission calling 可选（WP-009、WP-011）；底层 id 可见面收敛核对（WP-012）。
- W4 relay：评审对象改为 findings pairs（WP-014、WP-015）；两段式 suicide 确认（WP-018）；末任收尾（WP-016）；快照去形式化（WP-017）；证书只作历史（WP-019）。
- W5 重构与遗骸：normalTransform 具名 stage 管线（WP-004）；共享 ProtocolArgumentStash 原语（WP-005）；tool.execute 具名 stage 序列（WP-006）；工具门链具名化与 capability 单源（WP-007）；Sphinx 收敛 MCP-only（WP-039）；query-shell 遗骸丢弃（WP-040）；CHANGELOG 遗骸清理（WP-041）。
- W6 文档收尾：环境变量 KISS（WP-037）；验证入口 KISS（WP-038）；指南 42 条状态标注与文档同步（算法 F）。
- 证据：各包套件与门禁由 DevOps 实跑，数字见 `proposals/` 施工记录；WP-023 修复前后同输入的数字对比未验证。

## Unreleased — WP-021 / WP-026（ndjson 内嵌载荷与措辞清理）

- `durable-events-012`：取消旁挂 payloads 目录，事件行自包含载荷。`EventEnvelope` 增内嵌 `Payloads`；`CanonicalEventCodec` 仅在事件确实引用载荷时写出 `payloads` 键（无载荷事件的 canonical 字节不变）；`IEventStore.WritePayload` 改为进程内暂存、`Append` 内嵌；`ICanonicalIntegrator.TryPayload` 成为已提交内嵌载荷的唯一读口，Store 不再自行 `readStreams` 重建缓存。删除 `ProcessEventLog` 的 payload 文件 API、`WriterStreamSync` 与 `RetentionSurface` 的远端 payload 树及缓存。条款同步 `durable-events` [002]/[003]/[010]/[012]、`durable-convergence` [010]、`speculative-investigation` [006]。
- `WP-026`：provider 可见文案统一改称「事实 / 事实链 / facts」；`证据 / evidence` 只保留在 `evidence` 字段名与「举证责任」法律用语上。双语同步，语言对等门绿。
- 修复两处测试加载器：`007-composition-loader.mjs` 与 `prefix-digest-mutation-loader.mjs` 原先对 `Uint8Array` 源做 `String()`，且在同一模块上重复应用 mutation。现按 Node 版本正确解码，并保证每个模块只改一次。
- 证据：Node 25（CI pin 为 Node 22）下 `requirements/verification-system/tests/run.mjs` 4895 passed / 0 failed；`node scripts/check.mjs` 与 `node scripts/build.mjs` 绿；`fantomas --check src/Wanxiangshu` 全树绿。

## Unreleased — 开发者意见实现手册

- 新增 `人工审订语义指南的保姆级多人协作实现法/000.md`（Knuth 式伪代码、ASD-STE100 风格短句）：把《用于人工审订的当前语义指南.md》中全部 42 条 `> {开发者…}` 意见逐条落成工作包（WP-001—WP-042），含波次划分（W0—W6）、顺序约束、冲突面、每卡算法与验收、42 行台账与覆盖率校验。本文是施工流程，不是产品规范；与 `requirements/` 冲突时以后者与源码为准。

## Unreleased — spec/000 过程规范

- 新增 `spec/000.md`（Knuth 式伪代码、简体短句）：记录本插件对 OpenCode 的全部实质性增强，27 节加附录，覆盖装载期到发布验证的完整链路；附录 A 如实登记三处观察（`sphinx` 原生工具与 `query-shell` 未在 `ToolRegistry` 注册，`CHANGELOG` 的 obligations 改名条目已被 `c8a742d23` 撤销）。README 仓库结构补 `spec/` 一行。本文是阅读地图，不是权威规范；与 `requirements/` 冲突时以后者与源码为准。

## Unreleased — S03 源码目录身份与 NuGet 协议夹具

- gen112完整Node22/npm11.12.1/SDKroot仍5003ms静默、17/18排空；缺SDKroot的436/0/7skip/2TODO只属较窄截面，误名单仅setup。npm第一正例拆真实准备完成与实际install/assert判决叶，保原强断言/held负例/预算，gen113最终待附件，不提前记绿；Archive本批未施工。

- Git源码owner私有捕获canonical parent/root dev/ino，在publication、revalidate前后和cleanup拒绝foreign；同库存目录置换正式0/2→定向12/0，完整Git/.git/bytes/mode断言保留。TOCTOU/ABA、完整FD与其它owner仍未闭合。
- 15个NuGet非法图仅共享真实不改写Git前提，每叶SDK/restore/HOME/feed/packages独立，原typed/calls断言不变；195→13是静态Git成本，不冒称CI改善。最终输入结果待[本批附件](proposals/archive/2026-10-04/S03源码目录身份与NuGet协议夹具-2026-10-04.md)。
- 9909真实CI在300000ms backstop仅817/818、active016、无权威summary，是实际超时而非pending-only；预算与默认workers不变，T418/T419继续保留。
- aac79 CI另已818/818、4253/0/103skip/404TODO，仅pending退出1，未含本批修复，不认作9909原因消除。NuGet同15叶各15/0、23.782s→13.165s只是本机单次观测；source第三个parent失配/rootmissing例加入后完整定向13/0、2.942s，最终统一输入仍待验收。source红例只观察旧revalidate接受，未运行旧dispose删除；修复后dispose拒绝与foreign库存另有正式断言。

## Unreleased — S03 编译目录身份与清理

- Fable编译owner私有捕获canonical parent/root物理dev/ino；消费、发布、revalidate与cleanup拒绝同路径foreign目录，即使完整库存相同也不能删除foreign。正常parent且rootENOENT幂等，清理拒绝保留原Error/null与Aggregate cause，不扫描unknown parked路径。
- 正式parent/root置换0/2真实红例保留；初次cp mode setup失败不冒称产品红。新批结果以[验收附件入口](proposals/archive/2026-10-04/S03编译目录身份与清理-2026-10-04.md)为准，preflight仍有TOCTOU/ABA，不声称其它owner、完整FD或readonly已闭合。
- 第二批9909/gen110已429/0/6skip/2TODO、18/18排空、groupaccepted=true；其绿色不替代新输入验收，T418/T419保持。

## Unreleased — S03 独立工具进程回收

- 判决输送让步移到beforeEach，前一项runtime判决可在下一段同步工作之前送出；原三项正式回归3/3。真实失败完整原因/位置/stack立即打印，后续挂住仍可见；相同事实不重印，不合并共享Error的不同测试，021完整18/18。

- 薄monitor拥有actual工具组，EOF或实际exit/error后回收；调用方取消Error/null在正常cleanup后精确保留，cleanup失败保留Aggregate cause。supervisor冻结原组并只等待已观察额外组，foreign对照保持。
- 四个既有取消用例先await settlement，再发第二SIGTERM并加强ESRCH；完整库存、原原因及资源断言保留。注册/仅屏障正式红例后实际接入单项1/1，最终选集待[本批附件](proposals/archive/2026-10-04/S03独立工具进程回收-2026-10-04.md)绑定。
- 首批74fc/gen108已有18/18排空、418/0/6skip/2TODO；新批不升级为fullsetsid/crash/ABA/OS或readonly闭包，控制面/bin/ps未完整固定，T418/T419保持。

## Unreleased — S03 运行器因果输送与回收

- coverage 改为异步等待真实runner/c8，父进程可响应子进程HTTP；完整报告不能掩盖signal失败，silent子进程获得stdin EOF。原分母/报告断言保留，011定向18/0。
- supervisor静默失败先收原进程组和exact HOME，再交还caller；同步spawn错误停watchdog并保原cause，真实清理失败不吞verdict或null原因。Node/npm非法角色在读归档/分配root前typed拒绝，合法库存和完整探针不变。
- [记录](proposals/archive/2026-10-04/S03运行器因果输送与回收-2026-10-04.md)分开绑定正式定向红绿与gen108的统一18验收（418/0/6skip/2TODO）。新fe9 CI4188/0只证明其输入，保gen103失败/旧72e83无结论；detached tool接上方独立监护批，runtime readonly、ABA、T418/T419未闭合。

## Unreleased — S03 实际单项目 Fable 编译

- 以选定 SDK/Fable DLL 编译已准备单项目；原 artifacts 精确复制到自有 seed，原源码、SDK、工具与工程 owner 保持，JS 及中间产物完整库存绑定四份输入身份。
- 正式编译、进程 adapter 与诊断分开记证；SDK packs 和工具自带 FSharp.Core 不被四包 NuGet graph 覆盖，只读执行、完整工程图、OS 与实际 verify 同候选仍待证明。见[记录](proposals/archive/2026-10-04/S03实际单项目Fable编译-2026-10-04.md)。
- 保留前批72e83 CI在300000ms backstop下817/818完成、无权威summary/活动身份的无结论状态；最新接续从上方运行器记录认领，不以新输入CI或本地定向通过替代旧次结果。

## Unreleased — S03 工程 NuGet 单项目准备

- 从已准备源码与 SDK 恢复原单 net10.0/no ProjectReference 项目，使用明确归档和私有 feed；派生 lock 后清空包缓存，再实际 locked 复验，完整 graph/产物身份绑定原输入。
- 区分 raw nupkg SHA512 与 NuGet contentHash；源码准备补 canonical parent、普通全库存及公开 receipt 复核。实际 Fable、全仓工程图、只读执行及同候选 verify 仍待证明，见[记录](proposals/archive/2026-10-04/S03工程NuGet单项目准备-2026-10-04.md)。

## Unreleased — S03 选定本地工具恢复

- 明确包 ID、版本和 SHA512，以已准备 SDK 私有恢复原本地工具 manifest，绑定真实 resolver、运行 DLL 与完整输出库存；取消或失败保留原因并回收自有根。
- 真实 Fable/Fantomas 版本消费不代替工程 restore 或编译；S03 不可变执行与同候选 verify 仍未闭合。见[记录](proposals/archive/2026-10-04/S03本地工具恢复-2026-10-04.md)。

## Unreleased — upstream 31e69b3dd 合并

- 吸收 raw Chronicle 基数统一检查与显式 ABSORB 声明，保持过滤前拒绝及 live 规则名核对。修正新增测试的实际日志观察与排空，保留尚未接通的 registered Host TODO。
- 两次普通合并的取舍与监督验收见[最新记录](proposals/archive/2026-10-04/Upstream增量-31e69b3dd-2026-10-04.md)；制度学习机制提炼与 S03 剩余边界未关闭。

## Unreleased — upstream aaa123b12 增量

- 吸收 occurrence frontier/coverage 分离、持久事实兼容解码、review 重放及跨任期 call 防线。补完整 binding 重放和开任期误退邮箱的正式红绿回归。
- 保留 LifeCompleted Surface 的成功接线，撤回未闭合失败恢复的新增 Suicide 邮箱退休调用；GAP-157 与生产终结 TODO 保留。合并与验收见[记录](proposals/archive/2026-10-04/Upstream增量-aaa123b12-2026-10-04.md)。

## Unreleased — S03 选定 SDK 准备

- 从明确摘要的完整 SDK 归档独立物化，在私有配置目录下使用原 global.json 实际探测 SDK 与 runtime，拒绝越界工具路径、外部 SDK 借用和身份不符。探测后完整复核，失败与取消回收自有目录并保留原原因。
- 真实 SDK10.0.302 完整库存及原10.0.100/latestFeature选择有正式定向证据；这只证明准备边界，NuGet/tool restore、实际 Fable、只读执行与 verify 同候选仍待施工。见[记录](proposals/archive/2026-10-04/S03选定SDK准备-2026-10-04.md)。

## Unreleased — upstream 590a3f69e 增量

- 合入会话 dormant 身份登记、冷读取持久消费投影刷新、取消与删除的公开 Surface，以及沙箱顶层无 JSON 返回值的类型化拒绝。吸收实际工程缺边及持久化、effect、生命周期回归。
- 保留本地 S03 准备 owner 与两个 TODO，拒绝已被真实父目录替换反例证伪的 copy/chmod 快照接线；不将顺序冷读取证明写成跨实例同时消费已闭合。编译闭包 48→56 按本批新增依赖的八个源如实记录。
- 本地受影响验收与合并裁决见[同步记录](proposals/archive/2026-10-04/Upstream增量-590a3f69e-2026-10-04.md)。
- S03 另补本仓精确 Git 输入与完整 npm11.12.1 工具归档的真实依赖安装，独立产物的 Fable、Acorn、Tar 消费者及回收已定向通过；native Host、SDK/NuGet 和不可变执行仍未证明。

## Unreleased — S03 工具归档安装与挂载输出清理

- 新增从完整选定 Node/npm 归档执行真实 npm ci 的入口，沿原工具目录运行，安装 receipt 绑定工具摘要；安装前后及返回前复核完整工具成员，失败和取消回收自有目录。
- 编译输出 reset 保留真实目录根，仅删除目录内成员，拒绝根链接或普通文件；真实 macOS 挂载输出回归证明旧删除根实现报 EBUSY，修复后可清理和写入，失败编译保留原产物。
- upstream 的复制加 chmod 快照仍能通过父目录替换读取错误源码后恢复，并返回 PASS；保留正式反例、T418/T419 与 S03 未完成范围。见[本批记录](proposals/archive/2026-10-04/S03工具归档安装与输出根-2026-10-04.md)。

## Unreleased — S03 真实 npm 安装与 Node/npm 准备

- 从选定 package/lock 字节执行真实 npm ci，显式核对 Node/npm 入口身份，使用私有配置、缓存及 HOME，禁用 lifecycle scripts。完整安装清单与独立归档产物逐项一致后才返回；锁外来源、完整性失败及取消均回收自有产物。
- Node/npm 工具准备覆盖选定归档的全部成员，必需的声明生产依赖在 npm 包内闭合，拒绝父目录补库。实际版本探测从独立目录运行，探测结束再复核完整清单；取消排空实际进程组并保留原原因。共用归档物化规则，保留外部链接、特殊文件及截断归档拒绝。
- 这两项仍是准备边界：未接入实际 verify，SDK/NuGet、只读执行和阶段内改后恢复尚待证明，T418/T419 保留。见[施工记录](proposals/archive/2026-10-04/S03真实npm与Node工具准备-2026-10-04.md)。

## Unreleased — S03 明确身份的依赖归档准备

- 新增依赖准备owner：完整读取调用者指定SHA-256的安装归档，解析后独立物化node_modules，复核全部目录、字节、权限和闭合内部链接，摘要绑定归档与源码锁文件字节。拒绝外部/悬空/循环链接、重复路径、链接祖先、特殊文件及截断归档。
- 本批不执行npm安装，不证明归档符合锁文件来源，也未接入实际verify或只读执行。T418/T419保留；见[依赖准备记录](proposals/archive/2026-10-04/S03依赖归档准备-2026-10-04.md)。

## Unreleased — S03 指定 Git tree 源码准备

- 从明确指定的Git tree读取完整原始blob，保留路径、执行位和字节；在自有Git目录重新构造index/tree核对身份，避免工作区、attributes、replace refs和继承Git环境改变候选。缺对象、promisor仓、不支持的类型或无法忠实物化的路径均失败。
- 普通合并upstream `46935e5bd` 的Replica请求收尾user行；新增真实注册hook回归，验证它不建立新的physical acceptance，原预算/终态仍绑定原bootstrap，语言变更只影响提示正文。
- 源码准备尚未接入实际verify，依赖与只读执行未封闭，T418/T419保留。见[本批记录](proposals/archive/2026-10-04/S03指定树源码准备-2026-10-04.md)。

## Unreleased — S03 输入枚举前置

- 验证输入根、普通输入及tracked corpus路径的符号链接映射现在明确失败，不再静默漏收或读取外部可写目标；合法输出根链接与普通同名文件保持原边界。
- 新增正式WHAT016反例；不可变候选、同阶段改后恢复及依赖隔离仍待完成，两项TODO保留。见[S03记录](proposals/archive/2026-10-03/S03输入链接边界-2026-10-03.md)。

## Unreleased — 忙碌子任务可随时接收指导

- Manager 的 `resume` 可向忙碌 Engineer 或固定 DevOps 追加 `BusyAgentNudge`，影响下一次 LLM 请求；保留原任务、Root、完成回调和 handoff frontier，不取消、重派或新建任务。删除忙碌拒绝及按旧 charge 文本假装成功的路径，返回明确的指导已发送结果。
- 用户输入只释放已经开始的 Join，由 Manager 决定后续动作。唤醒移到 Host 保存后的可见回执，重复消息不打断下一次 Join。
- 同 run 的追加材料先持久接纳，旧 exact lease 保留到新材料实际选入 provider 请求后才交接同一 capacity credit，涵盖旧输出已自然 stop 的窗口；修复真实 Host 中保存前唤醒及提前撤销旧 lease 导致的 EMR-009/010。补充连续提示、owned/borrowed credit 交接与取消、迟到旧回调和原工作冷重放回归，以及安装版 OpenCode 1.18.29 的完整 Join → resume → child 下一次 wire → 原任务完成场景。

## Unreleased — 用户输入只打断 Join 等待

- 删除 PR #51（`558ed1c75`）引入的外部输入物理中断端口及全部接线。新 user prompt 不再调用 Host abort，当前 LLM 输出与非 Join 工具自然完成，新输入留到下一次 LLM 请求；Join 继续使用独立等待唤醒，不取消 child。
- 准入资源仍按 exact execution 交接，旧回调不能释放新执行。同步纠正 managed-session-lifecycle 016 与 delegation 015，删除“新输入排空 Guard”的旧测试合同。
- 回归包含 unwanted abort 的先红后绿、pending demand 替代，以及安装版 OpenCode 1.18.29 的 SSE/生产工具悬置场景；默认容量与 Manager 单槽容量均检查旧结果完整、新输入出现在下一次 provider wire 和物理清理。

## Unreleased — 程序无返回值不再击穿沙箱

- 现场：`js-predictor` 程序只 `console.log`、没有 `return`，沙箱报 `PROGRAM_FAILED: undefined is not an object (evaluating 'json.startsWith')`。根因：`JSON.stringify(undefined)`（及函数、Symbol）返回 `undefined` 而非字符串，`decodeRunResult` 对它调用 `StartsWith`。现在包装器在序列化结果不是字符串时与循环引用同样返回 `INVALID_RETURN_VALUE`，符合 repository-programming WHAT[011]。
- 回归：`repository-programming/tests/011` 覆盖缺省返回、`return undefined`、函数与 Symbol。

## Unreleased — Replica 请求不得以 model turn 结尾

- Gemini Cloud Code Assist 拒绝以 model turn 结尾的请求（400 `Requests ending with a model turn are not supported`）。Replica 的镜像加本决策已完成批次总以 assistant 行结尾；`StrengthReplicaTransform` 在写回 Host 前，若末行为 assistant，则追加一条 user 行，正文为与启动时相同的 `delegation/readonly-investigation` 提示。该行 id 由 session 与末行 id 派生，重复 transform 字节一致；末行已是 user 时不追加。
- 回归：`speculative-investigation/tests/009`（末行恒为 user、重复 transform id 稳定、仅 user 的镜像不变）；005、009 旧断言改为含收尾 user 行的新形态。

## Unreleased — upstream fcfba389e 与 PR 合并冲突

- 普通合并上游 `3f9aa1636` / `fcfba389e`，清理误提交的零字节工程临时文件，吸收 CI 镜像依赖和经过复核的工程接线。保留既有 exact work、Guard 交接和固定 DevOps 取消边界。
- 正式回归阻止 `WXS_ACCEPT_TODO` 将欠证判成完整通过；保持单次真实 canary、精确版本围栏及完整 Long Stroke。cold-boundary 改匹配完整双语资源及合成 ack，不以 Chronicle 关键词删普通用户历史。
- 更新同步和施工入口；本批验证范围、吸收/拒绝依据与原始证据见[同步记录](proposals/archive/2026-10-03/Upstream增量-fcfba389e-2026-10-03.md)。既有 GAP 状态不因上游 CI 绿或统计自述而关闭。

## Unreleased — upstream b7768f478 增量

- 合并 Blogger 双语提示资源和真实 journal/lease 注入观察，以及制度学习 candidate 机械准入、Born 持久事实/投影和 revision 计算。GAP-077/181 仅进入 PARTIAL，保留真实 Host、语义提炼、生产并发及原子提交义务。
- 按现行需求保留 exact AdmittedWork、墓碑、固定 DevOps 原终态回调和取消排空；Sphinx 保留严格 Revision 与 native MCP，词汇登记改引用 Core 清单。修正新增 Surface 的 opaque resource 登记，拆平新增控制分支，不增加基线。
- ProcessHost 的 verbose/debug 诊断进入 stderr，避免破坏 canary JSON 协议；保持原预算和默认并发。正式结果及原始失败见 `proposals/archive/2026-10-03/Upstream增量-b7768f478-2026-10-03.md`。

## Unreleased — 合并 upstream e1e7dd3f1

- 合入四个上游提交，包含空只读Replica终态、重启后空闲DevOps Join、Predictor正文降格与往复恢复，以及Sphinx canonical持久化和MCP合同。
- 合并保留本地Guard替代、Host就绪与判决输送修复；补齐Sphinx严格事件版本登记、原生MCP协议与opaque permit的Fable JSON拒绝，以及exact scoped子工作的加载期void。
- 固定DevOps的空闲道路可见性、父取消后在途工作与终态结算继续按实际所有者处理；受影响测试的准入前置改用真实physical acceptance，不由裸Root或稳定handle补造工作权限。
- 更新施工总计划、生命周期/Sphinx分册与GAP台账。具体红绿、生成输入和未完成边界见[本批同步记录](proposals/archive/2026-10-03/Upstream同步-e1e7dd3f1-2026-10-03.md)；Sphinx的业务driver、现存inquiry读取DTO/trace及完整发布验收仍待完成。

## Unreleased — 原工作区迁移代码编译修复

- 补齐 Sphinx Representation、BodyDto、Persistence Surface 的编译登记，调整 completion codec 的声明和实现顺序；修正直接阻塞编译的局部缩进、元组、保留字、缺失声明/引用，以及类型和参数名不一致。
- 直接在原工作区通过 Fable 构建，并验证 predictor 正文降为 reasoning、原生 reasoning 不回传、重启及子 owner 往复；未回退现有迁移或扩展同步逻辑。

## Unreleased — predictor 正文降格回传与往复恢复

- predictor → Main 现在回传 assistant 正文并降为 reasoning，原生 reasoning 不回传；混合工具消息与无后续请求的终止正文都纳入 frame 的持久化、校验、重放和 XTrace 覆盖。
- 最终 Host 编码补齐模型传输坐标，防止合成 reasoning 被 Host 再转为普通正文。Main → predictor 保持普通正文与思考的原语义；已同步正文恢复原类型，合并 predictor 自身 reasoning，且不重复插入。
- 补充往复前缀性质、重复正文次数、EventStore 重开与终止正文回归；真实 Host 验证常驻 predictor、重启重放和子 owner 的 reasoning_content 外发。

## Unreleased — 修复重启后空闲伴随句柄阻塞 Manager Join

- 现场根因：进程重启后，Manager 经 `horizon` 重新收养持久化的空闲 `devops` 伴随句柄时，`syncAdoptDevOps → AdoptExisting → runtime.Restore` 会新建一个 completion cell 打开、CTS 未取消的 `ChildRun`，使 `ActiveRunCount ≥ 1`，即使该句柄当前没有任何在跑任务。`HostForkJoin.parentHasJoinWork` 以 `runtime.Runtime.ActiveRunCount > 0` 短路判为有工作，Manager 的无超时 `join` 于是永久等待一个永远不会完成的幽灵运行，整条主会话挂起。
- 修复：`parentHasJoinWork` 只在**无 journal 的纯 PTY 模式**才以进程内 `ActiveRunCount` 作为 agent 工作信号；有 journal 时真实 Host agent 运行由 `PendingRuns`/`PendingCompletionCount`/`PtyRuns` 可见，可 join 的 durable handle 由 journal 投影可见，重启后重新收养的空闲伴随句柄只注册身份、不误判为有工作。真实在跑的 agent 仍会经 `PendingRuns` 正确阻塞 join。
- 回归：新增 `RESTART_ADOPTED_IDLE_DEVOPS_does_not_block_join_with_hang`，重开同一 durable journal 后收养空闲 devops 再 `join` 必须返回 `NothingToJoin`。修复前该测试 `JOIN_HANG_DETECTED` 失败，修复后通过；完整 delegation 套件 71 通过 / 0 失败 / 22 TODO。

## Unreleased — 修复只读同伴在空正文提前停止时导致的决策挂起

- 现场根因：子代理（如 gate-scout）在委托只读同伴（Strength Replica）调查时，若模型在最后回合直接以 `finish='stop'` 结束且输出正文为空（仅有 step-start/step-finish 等骨架），OpenCode 的分类器会将其判定为需要交互修复的 `TurnNeedsContinuation EmptyFormalText`。
- 由于只读同伴属于内部叶子（InternalLeaf），不享有也不执行 InteractionRepair，此前 `StrengthReplicaRuntime` 忽略了该结果，导致 completion `Task` 永不结算，决策永久卡死在 `Bound`，进而使主模型会话永远阻塞在下一次变换与 `join` 等待。
- 现将该物理停止信号在只读同伴所有权内正确确认为决策提前结束（`TextCompleted`），并安全清理物理会话与租约；不放宽普通 Work 会话的交互修复门禁。
- 补齐空正文提前停止、前缀证据保留、常驻副本连续复用及真实 Host 下 6 次委托（含空终止闭环）的端到端可复现回归测试。

## Unreleased — predictor 回传到主会话的映射修复

- 现场 `InvalidRequestOrdinal (1, 2)` 来自第二次常驻委托：首轮工具名误成 `<tool_call>js-predictor`，Host 记录错误，次轮正常只读调用完成。筛掉首轮后仍保留材料编号 2，导致 bundle 拒绝并返回 500。现在 surviving readonly batches 连续编号，provider 请求预算仍独立记账；不放宽帧校验、不压制熔断。
- Candidate 与 replay 都按 owner 角色映射 `js-predictor`；最终 Host 外发边界将逻辑 call/result 合并为原生 completed tool parts。追踪前仍保留完整交换与稳定 ID，已有 Host 行原样保留；JSON-looking 工具结果不再被解析，输出文本字节不变。
- 补齐中间 provider 请求的消费确认：工具循环尚未结束时，下一次主变换按真实快照中的 exact assistant、physical parent、完成状态与模型输出提交 Promoted，再进入 XTrace 和 Traced。错误、取消、未完成、错 owner/parent 与空输出不升格。
- 回归覆盖筛空首批/中间批、原生输出字节、并行 call/result 配对、物理行保留及 exact 消费拒绝边界。真实 OpenCode + 隔离 HTTP provider 场景覆盖两轮只读、错误工具后恢复、常驻复用、进程重启和 Engineer 子 owner：四次 Bound/Prepared、九次 predictor 请求，主模型实际收到五条完整交换，两个决策完成 Promoted → Traced；无 bundle 错误或主请求 500。

## Unreleased — predictor 的源消息、常驻生命周期与物理父链纠正

- `../wanxiang/.git/wanxiang/events/b1ca41f13bc24d709cc25219737ec160.ndjson` 中 14 次只读委托全部在 Requested 阶段以 `cannot-continue` 关闭，零 Bound；授权记录把下一条 assistant 占位消息当成发出工具批次的源消息。现在源 ID 与工具批次从同一实际 assistant 取出，并核对它的真实 physical parent；新输入不能借用旧批次的估计。日志未开启逐门诊断，不能把全部历史关闭逐一归因于同一分支。
- 常驻 predictor 在第一次 await 前认领 owner；同一 decision 共享准备结果，不同 decision 不创建竞争子会话。取消与卸载会排空未完成的创建；无模型容量的未发送委托立即退出，不等待不存在的物理终态。复用只订阅未来终态，退出时移除监听；终态按实际 physical input 精确匹配，旧回合不能结束新委托。结果保留到持久发布确认，避免物理清理先到时把并发消费者误判为恢复孤儿。
- 每次结果收集仅扫描本次 bootstrap 之后的消息，历史工具材料与旧纯文本结尾不再污染或中止新委托。重启复用依据 durable Bound 中的子会话 ID 与 Host 的精确 agent/title；查询失败、历史多候选或元数据冲突明确拒绝，不另建副本，不按标题或时间任意收养，也不删除历史会话。
- Host 父链在缓存缺失时按原生 session metadata 恢复；创建与查询统一使用实际 family root，子 owner 的 predictor 与其他受管同伴不再成为物理孙会话。逻辑 owner、预算、语言和材料归属不变；父链查询失败或出现环时不创建会话。需求条款补上不变量与条件性进展证明，模型是否给出合法正数估计及外部服务可用性不作无条件保证。
- root/worktree 实例按同一 git common-dir runtime key 共享 predictor 登记表、物理协调器与 fuse；引用计数归零才卸载。通用实例清理不释放共享常驻会话及其 owner 的模型租约，最后卸载再交还两者。带有真实 physical parent 证据的早到终态先暂存，再由实际绑定精确重放；不丢失通知，也不借旧通知认领新决策。

## Unreleased — provider 错误不再被空输出修复抢走

- 修复 `CompletedTurnClassifier` 将 `completed + error + 空/XML-only 输出` 降级为 `TurnNeedsContinuation` 的错误。错误终态现在始终是 `TurnFailed`：先到的 idle 等待 exact typed failure，不再发送 `missing-final-report`；确切失败观察仍由原有 provider recovery 记账、派发 fresh `ProviderRetryAttempt` 并重新选择可用的 LWR 前缀。无 provider error 的空/XML-only `stop` 仍走内容修复，operator abort 仍为取消。
- 现场证据：`../wanxiang` 的 DevOps 会话 `ses_f0dca4962ffeQSTPc5KnX6V0tZ` 在连续 400 超限期间记录了 210 次 `InteractionRepair`，却只有 8 次 `FailureRecorded` / `ProviderRetryAttempt`。repair 反复携带 cutoff=450 的旧 probe；最后一次真正的 provider retry 才选到 Blogger 已证明的 cutoff=656。修复不按 400 文案分支，不引入容量估算、非法截断或新会话兜底。
- 回归覆盖空输出、reasoning-only、XML-only、partial answer、error finish、正常内容修复及取消边界；新增真实 `ReconcilePass.run` 的 idle 先到、exact provider terminal 后到回归。修改前回归失败为 `TurnNeedsContinuation`；修改后，用数据库中本次报错的原始 assistant message 离线重放，idle 交付 0 次，exact failure 交付 1 次 `TurnFailed / ProviderTransient`，不携带 idle repair 权限。
- 定向验证：相关 12 个测试文件 90 通过、0 失败、5 TODO；构建通过。用真实 400 消息继续驱动 retry policy、生产 LWR candidate 物化与 Host-id prefix replacement 的离线 smoke，覆盖历史确实移除，Opening 与当前回合原样保留。扩展套件 630 项中 563 通过、20 失败、47 TODO；失败集中在未修改的 Chronicle 旧文案断言、Blogger flight/capacity 交错及 ingress provenance 测试。仓库 check 另报 184 项，未定位到本次修改文件；不宣称全仓门禁绿。

## Unreleased — F# 控制金字塔债务清零

- **`fsharp-control-pyramid` 从 42 项降为 0，baseline 清空为 `{ "version": 1, "files": {} }`**：13 个文件逐处按 `structured-workflow-004` 提取具名 helper 或改用组合子消除 `depth>=2` decision，不再依赖按文件记账的 ratchet。`Batching.fs`（10）把 charge 渲染、消息替换、pending 取走各自成函数；`Delegate.fs`（8）把 wire 批次扫描收成 `wireBatchStep` 单步 Result、把预算聚合收成 `ofRounds`/`ofParsedResults`/`parseCall`；`InvestigationEstimateContract.fs`（8）拆出 `validateNumber`/`validateNoteText` 与中英文文案表；`Send.fs`（4）拆出 `persistSubmittedFact`/`admissionVerdict`/`settleAdmittedReceipt`，try 只包一层。
- **其余清零点**：`PhysicalAcceptance.fs` 环境变量解析、`AgentJournal.fs` 用 `Result.map fst`、`ToolRegistry.fs` 提取 `executeReplica`、`ProviderSystemTransform.fs` 提取 `isReplicaConstraintLine`、`SessionRecoveryHost.fs` 提取 `settleUnresumedFor`、`OpenCodePort.fs` 提取 `promptDispatchOutcome`（HTTP 前缀判定改为 guard）、`ModelRouting.fs` 提取 `normalizeProviderStepRecording`、`Runtime.fs` 提取 `settleInFlightDelegateExecution`、`PluginHooks.fs` 提取参数恢复与估计校验 helper（`toolAfter` 的 participating 恢复语义不变）。
- **门禁说明同步**：`fsharp-control-pyramid-guide.mjs` 第 14 节改为「baseline 为空对象，任何 depth>=2 decision 即新增债务」，删去按文件历史记账的表述。
- 验证：`node scripts/build.mjs` 绿（165 surfaces / 836 modules）；`node scripts/check.mjs` 中 `fsharp-control-pyramid` 报 0（余下 174 项为既有 `js-boundary-gate` 债务，不在本次范围）；定向套件 `structured-workflow-004`、`host-boundary-032`、`speculative-investigation-{002,004,011,012,013,016}`、`delegation-{007..012,023..025,028,031,032}`、`execution-model-routing-{004,010}`、`managed-session-lifecycle-{004,009,014}`、`provider-attempt-recovery-021`、`feature-ablation-002`、`crash-reconciliation-015` 共 32 个文件 0 failed。

## Unreleased — Pair Programming 指引七条纪律真理版本

- `resources/provider/host/pair-programming-guideline/zh-CN.md` 重写为七条编号纪律（使用中文、小步快跑、不要吝賂、进度更新、极高并发、超越常识、善于内省）；`en.md` 按该真理版本忠实重写，语言纪律按绑定语言镜像（英文版要求全程英文）。cognitive-environment WHAT 013/016 与 tests/013、016 锚点同步：`ready frontier` 改为就绪前沿、`先抽象，再笃定` 改为分析与抽象后钉住反常识判断，并新增 en 版七条纪律结构断言。

## [Unreleased]

- speculative-investigation / host-boundary: 规范切换到  新合同。
  - **共享合同与协议修订版 2**：新增 `src/Wanxiangshu/Strength/InvestigationEstimateContract.fs`，将面向模型的意图性字段 `delegate_readonly_rounds` clean-break 重构为事实性连续只读轮数估计 `estimated_readonly_rounds`（0..2147483647 原生整数）。
  - **条件性调查展望**：`self_note` 明确为条件参数——估计大于 0 时必须提供一至三句非空白未来展望；估计为 0 时必须完全省略。提供与规则解释文案彻底解耦的机器稳定标识（`NotePresentWhenZero`、`MissingOrBlankNoteWhenPositive` 等）。
  - **逐工具判定矩阵**：严格按工具操作性质判定（全系统 12 个参与工具装饰，28 个不参与工具无任何增量）。
  - **身份边界与历史防重**：彻底剔除执行意图与信任叙事；同伴防递归完全由真实身份边界保证，去除副本必须填 0 的虚假约定；旧字段 clean-break 废止，运行时拒绝旧字段与新旧混用，历史落盘事件原样保留不重写；古老 K1/K2 迁移工具保持契约修订版 1，当前运行时采用修订版 2。
  - **负资产清理与教训**：
    1. 彻底删除纸糊门禁 `scripts/checks/test-source-read-gate.mjs` 三件套。教训：该门禁仅对静态路径做 `allowMap.has(relPath)` 查表，从不核对测试运行时真实读取路径；单行正则完全漏检多行 `path.join` 写法；白名单中 33 项记录大面积超额登记。抓不住真实反例、靠超额豁免制造虚假安全感的门禁比没有更糟。
    2. 堵死 `requirements/verification-system/tests/e2e/support/long-stroke-oracles.mjs` 约 L1415-1424 的降级断言分支。教训：原实现在两个协议标记皆缺失时，退化为拿整段工具描述当增量文本做负向断言，将“描述装饰丢失”伪装成“描述包含旧叙事”，使排查者顺着报错永远查不到真因；现改为严格断言标记存在，标记缺失立即亮红。


## Unreleased — PROMPT-006 执行绑定去层化收尾（P1–P6）

- **删除 Host 侧平行执行身份/模型权威（PROMPT-006 全类根因）**：`SessionExecutionBinding` 的进程本地身份表（`agents`/`parents`/`internalRoots`/`hostAuxiliaryChildren`/`acceptedPromptBindings`/`providerAttemptBindings`/`persistentDevOpsModels`）与 JS 测试面 `SessionBindingSurface` 全部删除。模块只保留两件事：durable `Accepted` 证据查询（`isManagedExecution`）与 transform 的 provider-step 门（`beginPhysicalProviderAttemptForTransform`）。原 24 处 `PROMPT-006` 报错（"no accepted execution binding"/"no frozen agent binding"/agent drift from cache 等）随平行表一起消失；`chat.params` 只剩 4 处基于 exact 证据的 fail-closed（agent/model 漂移、缺 lease、缺观察）。
- **工具边界改为 exact run→physical**：`ModelRouting` 新增 `providerStepIdentityByRun`，只在权威 Host start observation（assistant `message.updated` 的 `parentID`）与 `EndProviderStep` 写入，`ReleaseExecution`/`ReleasePhysicalExecution` 随执行退休清除；`ToolRegistry.providerToolBoundary` 用 `tryProviderStepIdentity` 解析本 run 的 physical，不再读 session-current 绑定副本。
- **chat.params 三态判定**：durable `Accepted` + committed lease → 校验漂移并投影 temperature；durable `Accepted` 无 lease → fail closed；无 durable `Accepted` → 完全交给 Host（Host compaction/title/auxiliary 不再误报）。
- **发送端 model-free + 死参数删除**：`SessionBindingIntent` 从 `OpenCodePromptOptions` 与全部调用方删除；`prepareManagedPrompt`/`prepareUserFacingPrompt`/`participantAgent` 链删除。
- **父边 durable 化**：`HostSignalBootstrap.durableParentOf` 直接读 `HandleByChildSession` + `SessionAssociationProjection`，不再有可被重启清掉的父边缓存。
- **`ChatExecutionState` 改为互斥 union**（`Accepted | Started | EndedBeforeStart | EndedAfterStart`）："ProviderStarted 无 started evidence"、"Terminal 无 terminal evidence"、"pre-start Completed" 不可表示；durable wire 格式不变。
- 测试迁移：`bindManagedChild` 改为追加真实 durable `Execution.HandleLinked`；crash-021/host-006/host-033/ia-011/pid-008/office-007/ipp-013/behavior-diagnosis-006/emr-010 全部迁到 durable 证据出口；`providerBinding` 观察字段（第二表计数）删除，改为 `exactLeaseCommitted`。

## Unreleased — Manager 循环 clean cutover

- **修复 Blogger 同伴每个新请求的首个 provider step 自杀，覆盖材料永久断流、主会话前缀替换不再前移（context-compression-024 / dispatch-protocol-006 / capability-enforcement-021）**：实机 2026-09-28 00:58:03 与 07:27:19，主会话派发 Blogger 请求后，同伴 run 在调 provider 之前就被插件自己 cancel（journal：`ChatExecution Terminal Cancelled/PreProvider` 早于宿主 `cancel` 15 ms），flight 一直被占，后续材料全走 `SkippedInFlight`，不再有 `BlogObservationCommitted`，K 窗口 cutoff 不前移，主会话 raw 历史涨到 1 MiB 上限报 400。三层根因叠加：
  1. **step 位置判错**：transform 输入里没有正在构造的 step；新 physical message 的首个 step 看到的“最后一个 assistant”是回答上一条 physical message 的历史尾巴（这里是被中止的空 assistant），continuation 却把它当成本 step 的散文终态去修复；
  2. **落地证据按 payload 覆盖**：所有 Blogger Main 派发都是同一段固定指令、同一 payload digest，`AcceptedDispatches` 以 `(session, payloadDigest)` 为键，后一次落地覆盖前一次，07:02:41 一条同载荷 claim 被放弃时还清空了该槽，旧 physical message 的落地证据就此消失；
  3. **Unproven 被当作 Current**：证明不了归属就照常认领修复，修复 episode 回 `PendingRepairWait`，continuation 于是 `StopPhysicalRun` 停掉自己刚派发的 run；而停下的 turn 在 idle 时是 `TurnAborted`，idle 又不把它交给修复 owner，flight 永远不释放。同一路径还会让每次 nudge / AABB / ProviderRetry 续提示在首个 step 自杀。
  重写：`EnforcerCycleDecode.stepPosition` 按 Host `MessageV2.latest` + assistant `parentID` 给出 typed `First | After(previous) | NoRequest`，修复谓词全部作用于 typed `AssistantStep`；continuation 先判位置、再证归属：首个 step 从不评判历史，只投影 live request 的 canonical 视图（本 request 的 repair 落地时在视图后保留这条 repair 原文）；之后的 step 只有 physical message 被证明为 Current 才可 commit、交给 repair owner 或停止 run，Superseded/Unproven 一律原样投影；删除 durable open 回读（`resolveCycleContext`）与未归属 fatal 分支。PromptAuthority 投影新增按 physical message 精确保留的 `PhysicalLandings`，归属判定、`Child.ensureForLinkedChild` 与 Dispatcher 的两处“这条 physical 是否由该 claim 落地”全部改读它；`BloggerRequestOwnership.decide` 把“open 尚未绑定 PromptKey”判为 Unproven；coordinator 的 transform / idle 入口对 Unproven 返回新增的 `UnprovenIgnored`，不消耗预算。`CompletedTurnClassifier.bloggerIdleRoute` 让被中止（非 degeneration guard）的 Blogger turn 在 idle 交给同一 repair owner 后再走常规中止观察。
  验证：`requirements/context-compression/tests/024.test.mjs` 新增 6 条回归（首个 step 不评判历史尾巴、superseded 步永不 commit/stop、repair 落地首步可见 repair 原文、Unproven idle 不耗预算、未绑定 open 判 Unproven、同载荷 claim 放弃不抹去 open request 依赖的落地）；`dispatch-protocol/tests/006.test.mjs` 新增同载荷落地逐 physical 精确保留；`capability-enforcement/tests/021.test.mjs` 新增 Unproven 不耗预算与中止 turn 的 idle 路由；既有 021/024 与 flight 交错支撑改用真实 durable 归属（生产写入口：HumanRoot + 落地派发 + `bindRequestDispatch`），`018` 结构断言改为“continuation 从不回读 durable open 权威”；真实 journal 副本离线重放 07:27:18 那一步：修复后首个 step 投影 canonical 视图、不含旧 aborted 尾巴，旧 P0 步判为 superseded 且 flight 保持。单元层 3799 通过；余 11 项失败全部落在本改动未触及的在途文件（speculative-investigation、crash-reconciliation 测试形态、Fission `mutable` 注解、integration 入口登记）。

- **修复跨进程重启后同伴 Blogger 恢复派发抛 PROMPT-006 导致覆盖材料永久断流（crash-reconciliation-020）**：进程重启后 `SessionExecutionBinding` 丢弃内存会话绑定，从 durable journal 恢复子会话身份时，`SessionBindingRecovery.evidenceFor` 原先仅查询 `HandleByChildSession`，遗漏了以 `SessionAssociationProjection.Associations` 形式记录的 Companion / Blogger 会话。这导致重启后首个由主会话触发的 `BloggerRequestMaterialized` 请求在 `prepareUserFacingPrompt` 中被误判为 user-facing 会话，继而抛出 `PROMPT-006: user-facing session has no observed user binding` 并标记 `PluginPromptAbandoned`，使同伴再也无法写入 `BlogObservationCommitted`，主会话前缀覆盖永久冻结并在几轮后突破 1M 上下文限制触发超限错误。
  修复：`SessionBindingRecovery.evidenceFor` 补齐对 `SessionAssociationProjection.tryMainSessionOf` 的回退查找，正确还原 `(parentSessionId, "blogger")`，使重启后同伴会话仍被正确判定为 managed session 并通过 `prepareManagedPrompt` 顺利派发。

- **修复重启后 Blogger catch-up 永久停摆（crash-reconciliation-020 / context-compression-024）**：实机 manager 会话的 Blogger 在某次重启后 coverage 冻结在 173（已折叠的 prefix cutoff），而主 loop 一路跑到 turn 232 —— ctx 只能继续膨胀。根因：那次重启让一个已 materialize 的 Blog 请求（`4d17d08d…`）的 provider run 在提交后被取消，durable open `BloggerRequestMaterialized` 从此悬空。悬空 open 让 coordinator 每次都走 `hasOpenProducer` 的"暂存材料"分支（只 `OfferMaterial`，不再物化新请求），而它等的 parked producer 是进程本地的、已随旧 runtime 消失；enforcer 对"durable open 但无 live cycle context"的既有处置是明确等待 owner 复活。Load Phase 的子 run 结算（`ChildWorkRecovery`）只覆盖解析得到 delegation handle 的子会话，companion/Blogger 无 handle，因此每次重启都只剩 `CompanionBloggerLinked`、再无 cycle。
  修复：`BloggerAbandon.staleOpenRequests`（纯枚举：仍未 commit/abandon 的 open request，排除本进程仍有同 RequestId live flight 的）+ `BloggerAbandon.settleStaleOpenAtLoad`（逐条置 `BloggerRequestAbandoned`，reason `stale-open-at-load`），由 `PluginRecoveryWiring` 在 Load Phase 与子 run 结算同一处、`PluginRuntimeReloaded` 之前调用；Abandon / PromptKey bind / crash recovery 仍共用同一 materialization admission 与单一 abandon writer。
  验证：真实 journal 副本上跑生产结算——结算前 11 条悬空 open request（含 manager 的 `4d17d08d…`），结算后 0 条且写入 11 条 `BloggerRequestAbandoned`（reason `stale-open-at-load`）；`requirements/crash-reconciliation/tests/020.test.mjs` 新增两条回归（无 live flight 的 open request 被枚举/结算、结算后 open slot 清空且枚举幂等；Load Phase 在 `PluginRuntimeReloaded` 之前调用）；crash-reconciliation + context-compression 401 项、prefix-stability/host-boundary/provider-attempt-recovery 414 项（余 1 项为既有 WIP 失败 host-boundary-032 C35，未含本改动时同样失败）。

- **大重构：子会话解析改为“durable 为真源、进程表为缓存”（crash-reconciliation-020 / managed-session-lifecycle-024）**：本会话四次故障（权威冲突、绑定缺失、登记缺失、门禁拒绝，到 `[COMMIT-REUSE-ERR: "Unknown agent id: opghu8"]`）同一个病根——进程本地表被当成存在性真源，重启后每处都要单独补一次。本次按一条规则统一切换：**durable handle 投影回答“这个子会话是否存在、属于谁、用哪个执行 agent”；进程内表只记录“本进程当前在驱动什么”**。
  落地：新增 `Execution/Delegation/DurableChildLookup`（`byHandleId` / `byByname` 纯查询）；`HostForkRuntime` 新增 `TryChildFromDurable`、`TryFindAgentOrAdopt`、`ReusableChildOrAdopt`、`AwaitChild`、`HasChild`，并把 `Reuse`、`TryChildSession`、join 的目标 await、reuse 门禁、`announceChild`、orchestrator 的 `hasChild`、父取消时 devops 绑定的保留全部改走它们；`SessionExecutionBinding` 增加 `installDurableChildEvidence`，`tryParent`/`tryAgent` 未命中本地缓存时按需从 durable 解析并回填；`SessionBindingRecovery` 重写为“安装该解析器”（`installFrom`/`install`）。删除两个装载期特例通道 `DurableChildAdoption`（重新登记）与原先的绑定预热扫描——同一件事现在只有一个按需规则。
  验证：构建 generation 42/43；新增回归 `CRASH_020_reuse_resolves_the_child_from_its_durable_handle`、`CRASH_020_reuse_reports_unknown_for_an_unlinked_handle_id`、`CRASH_020_host_owned_hidden_leaves_yield_no_binding_evidence`，并把既有重绑/结算用例改为断言 durable 解析与“handle 仍 Active、joinable 为空”；fission lane 同规则（`FissionRuntime.installDurableLaneEvidence` + `SESSIONBINDING` 的 `fissionLaneFor`）；`crash-reconciliation + delegation + managed-session-lifecycle + change-integration + host-boundary + intra-participant-parallelism` 共 828 项，余 5 项为本会话之前即存在的 WIP（host-boundary-032 C34–C38）。

- **激进重构第一刀：reuse/placement/await 改为以 durable handle 为单一真源（crash-reconciliation-020）**：实机 `[COMMIT-REUSE-ERR: "Unknown agent id: opghu8"]` 追到 `Fork/Host/Agent.fs:761` —— `Reuse` 只用**进程本地**的 `TryReusableChild` 判存在，重启后空表即判「Unknown agent id」，而同一个 handle 在 durable 投影里完好（`opghu8` = `triage-misc`）。这是本会话第四次同类病：进程本地表被当作存在性真源。
  改动：`HostForkRuntime` 新增 `TryChildFromDurable`（按 handle id 读 durable 投影）与 `ReusableChildOrAdopt`（先查进程登记，未命中则按需收养后返回子会话），`Reuse` 改用它；`.fsi` 同步；`Tool.fs` 的 `printfn "[COMMIT-REUSE-ERR]"` 调试输出替换为 `Diagnostic.emit "fork-reuse-commit-failed"`。
  验证：构建 generation 36；crash-reconciliation + delegation + managed-session-lifecycle + host-boundary 共 655 项，余 5 项为既有 WIP（host-boundary-032 C34–C38）。运行时级回归（重启后直接 reuse 既有 byname）待补：现有 Fork surface 无运行时入口，需要一个测试缝。

- **重启归位改为“作废中断 run”：horizon / join / reuse 三处状态一致（crash-reconciliation-020）**：上一版归位把遗留子 run 写成 `HandleCompleted(Cancelled)`，留下一个没有 body 的“未收交付”；但 `horizon` 的名单只收本进程持有或 Abandoned 的 handle（收养的子会话故意 dormant），于是 horizon 空空、join 无可收，而 reuse 门禁却认定有未清交付——三处各说各话（实机：六个既有 Engineer 子会话逐一 resume 全被答“此人目前无法再接下另一项托付”）。用户裁决：中断的 run 什么结果都没产出，就不欠任何人交代——horizon 为空、join 为空、直接 resume 才是正确时序。
  修复：新增 durable 事实 `ExecutionFactCases.ChildRunVoided`（fold 只产出 `TerminatedChildHandle`，关闭子 run 权威、**不动 handle 生命周期**；codec 与 `ExecutionFact` 帮手同步），`ChildWorkRecovery` 的 Load Phase 结算改用它——子会话保持可复用，horizon 与 join 都不再出现幽灵交付。另修 `Tool.fs::reuseResolvedAgent` 的 `TryFindAgent = None` 分支：重启后的进程登记可能还没有该子会话，此时以 durable handle 为准走正常 reuse 路径，而不是回 `person-unavailable`。
  验证：`CRASH_020_run_without_terminal_is_settled_at_load` 改为断言“结算后 handle 仍 Active、join 可收集合为空”；`CRASH_020_cancelled_completion_is_reported_and_retired_by_join` 继续覆盖真正带 `Cancelled` 完成时的单次报告；`crash-reconciliation + delegation + managed-session-lifecycle` 共 400 项全绿（durable-events 的 3 红为本会话之前即存在的 WIP：Strength fact 父边/oracle 清单）。

- **取消的归位结果必须能被 join 收取：无主体的 `Cancelled` 完成改为报告一次后退休（crash-reconciliation-020）**：重启归位会把遗留子 run 结算成 `HandleCompleted(Cancelled)`，该完成没有 body；`JoinDrain.missingBodyOutcome` 对 `CompletedAwaitingJoin{Cancelled}` 返回 `None`（静默跳过），于是 join 报“没有在外可接收的工作”，而 fork 的 reuse 门禁又认定该子会话有未清交付——实机表现是 manager 对 domain-core / replica-runtime / spec-author / schema-contract / frame-integrity / recon 六个既有 Engineer 子会话逐一 resume 全被答“此人目前无法再接下另一项托付”，只能不断 fork 新名字。
  修复：`JoinDrain` 新增 `tryConsumeCancelledCompletion`，对无 body 的 `Cancelled` 完成按既有单次报告语义消费（`afterConsumeCas` 参数化 runId 前缀为 `cancelled-`），报告后退休该 handle，等价于 Abandoned 的单报墓碑；有 body 的完成路径不变。
  验证：新增 `CRASH_020_cancelled_completion_is_reported_and_retired_by_join`（真实 `drainFromJournalWhere` + 假 journal port：一次报告、durable append、handle 退休、不重复投递）；crash-reconciliation + delegation + managed-session-lifecycle 共 400 项全绿。

- **移除 `/continue` 命令：重启归位改由系统在加载阶段自行完成（crash-reconciliation-018）**：显式续传命令及其全部痕迹删除——`ExplicitSessionResume`、`ExplicitResumeSurface`、`ExplicitResumeSuppression`、`SessionResumePort` 四个模块，`HookKey.CommandExecution` / `HookEffect.AdmitExplicitResume` 策略项，`PluginHooks` 的命令注册与 `command.execute.before` 钩子，以及 `HostSignalBootstrap`/`ChatParamsHook`/`HostTurnObserver`/`PluginTransforms`/`HostSessionDeletion`/`Companion/Transform` 中的 disclosure-only 分支与 `ExplicitResumeDisclosure` 变换模式。规范 [018] 改写为：重启后由系统自行归位（结算遗留子 run、重建执行绑定、重新登记子会话），只做持久记账、不重放命令；被中断的工具保持失败并留在可见历史。测试同步改写：`018.test.mjs` 改为断言“加载不注册任何命令 + 源码树无显式续传残骸”，`019/020` 与 host-boundary `014/019` 去掉命令与分支探针，`context-compression/018` 删除两条披露材料用例。

- **重启后 reuse 子会话被答 person-unavailable：Load Phase 重建 fork runtime 的子会话登记（crash-reconciliation-020）**：`fork` 的 reuse 门禁 (`Execution/Delegation/Fork/OpenCode/Tool.fs` 的 `reuseResolvedAgent`) 依赖进程本地的 `runtime.TryFindAgent`；重启后登记为空，于是对任何 durable 子会话（固定 DevOps、forked Engineer 子会话如 `frame-integrity`）都回 “此人目前无法再接下另一项托付”——（此前只有已移除的 `/continue` 会重新登记，普通重启没有。）
  修复：新增 `OpenCode/Host/DurableChildAdoption.fs(i)`（纯计划）并在 `PluginHooks` 的 Load Phase 调用既有的 `ToolRuntimeScope.AdoptExistingChild` 重新登记：只登记 `Active` / `CompletedAwaitingJoin` 且 `DurableParentHandle` 的 handle；`Abandoned`/`Retired` 墓碑与 Host-owned hidden 叶子不入列。登记只是记账，不发任何 prompt、不重放命令，复用仍是 manager 的显式动作。
  验证：新增回归 `CRASH_020_durable_children_are_addressable_after_a_plain_restart`（普通重启后现存 durable 子会话可被 reuse）与 `CRASH_020_tombstones_and_hidden_leaves_are_not_addressable`（retired / host-hidden 不可用，awaiting-join 仍可用）；crash-reconciliation + managed-session-lifecycle + delegation 共 412 项 411 pass。

- **重启后仍被拒绝的托付：上一个 runtime 遗留的活跃子 run 在 Load Phase 结算（crash-reconciliation-020）**：硬杀进程时子 run 可能既无 terminal 也无后续事实（实机：devops run 01:57:46 登记、01:58:01 `ProviderStarted`，02:07 重启时无任何 terminal），`ChatExecutionRecovery` 把上一个 runtime 的物理证据判为 stale 而 `Ignore`，于是 `ActiveLogicalRun` 永远开着，下一次交接继续被 `ActiveRunIdentityConflict` 拒绝、join 无对象。
  修复：新增 `OpenCode/Host/ChildWorkRecovery.fs(i)`，在 durability 激活前的 Load Phase 为每个「子工作 run 仍 Active 且能解析到 Activity 父 handle」的会话追加一条 `HandleCompleted(Cancelled)`；已有的 delegation fold 从该事实导出 `TerminatedChildHandle`，关闭子会话权威并让父 handle 进入 `CompletedAwaitingJoin`。Manager/HumanRoot 自身道路与无 handle 的 session 不入列；transcript 保留，不重发任何命令。
  验证：新增回归 `CRASH_020_run_without_terminal_is_settled_at_load`（无 terminal 的子 run → 计划出 orphan → 结算事实 fold 后权威关闭且 handle 得 `Cancelled`）与 `CRASH_020_human_root_and_unlinked_sessions_are_never_settled`；crash-reconciliation + managed-session-lifecycle + delegation 共 410 项 409 pass。

- **重启后固定 DevOps 无法接手：进程本地执行绑定在恢复时重建（crash-reconciliation-020 / managed-session-lifecycle-024）**：重启丢掉 `SessionExecutionBinding` 的进程本地绑定（父边 + 冻结 agent），于是下一次交接走 `prepareManagedPrompt` 时被拒 —— 实机事实 `Prompt.PluginPromptAbandoned { Reason = ["SendFailed", "PROMPT-006: parented session has no frozen agent binding"] }`，manager 看到“尚不能再接下另一项托付”，改去 join 那个永无结果的 handle。
  修复：新增 `OpenCode/Host/SessionBindingRecovery.fs(i)`，在 `PluginRecoveryWiring` 启动阶段用 durable handle 投影重建每个父会话→子会话的绑定（只恢复 `Active` / `CompletedAwaitingJoin`；`Abandoned`/`Retired` 墓碑不复活），模型与 Persona 仍按既定验收路径冻结，不重发任何旧命令。恢复的是 handle 的 `TargetAgent`（Host 执行 agent），不是逻辑复用地址 `Byname`：forked Engineer 子会话正是 `engineer` 与 `decision-record-readonly` 两个名字，绑错会在下一次派发时以 participant drift fail closed。
  验证：新增回归测试（同文件）`CRASH_020_restart_rebinds_the_durable_parented_child`（重启后能重新派发到固定 DevOps）、`CRASH_020_engineer_child_rebinds_its_execution_agent_not_its_byname`（Engineer 子会话按执行 agent 重建）与 `CRASH_020_abandoned_handle_tombstone_is_never_rebound`；crash-reconciliation + managed-session-lifecycle + delegation 共 408 项全绿。

- **重启后固定 DevOps 无法归位：子工作的中断运行现在会显式重置（crash-reconciliation-020 / managed-session-lifecycle-024）**：子工作 run（AgentOwnerRoot）以非 `Completed` 终态结束（进程被杀、回合取消、派发被拒）时，先前只写入 `ChatExecution.Terminal`，没人关闭该会话的 `ActiveLogicalRun`，父会话的 handle 也停在 Active。后果：道路的 DevOps 重新派发被 `ActiveRunIdentityConflict` 拒绝，manager 的 join 又等在那个永不会完成的 handle 上，状态无法归位（实机：devops 会话 08:01 启动的 run 在 08:18 记作 Cancelled，之后每次交接都被拒）。
  修复：`Composition/Durable` 新增 `DelegationProjectionBridge.settleUncompletedChildRun`，并在 `Fold` 的 ChatExecution 分支应用：子 run 以非 `Completed` 终态结束时，关闭该子会话的逻辑 run（下一次交接重新按全新 AgentOwnerRoot 生根，horizon/join 看到的是刚出生的 devops；transcript 自然保留），同时把父 handle 结算为 `Cancelled` 完成（join 得到明确结果，而不是无限等）。规范禁止自动续跑，因此重启/重做仍是 manager 的显式决定；这里只做持久记账的归位，不重发任何命令。
  验证：crash-reconciliation 与 managed-session-lifecycle 全绿，新增回归 `requirements/crash-reconciliation/tests/020.test.mjs`（中断子 run → 子权威关闭 + 父 handle 得到 `Cancelled` 完成；正常完成仍走原完成路径）；delegation 144/144；构建 176 surfaces / 831 modules linkage 通过。

- **修复配置 Predictor 后宿主内建工具的装饰抛错、会话不可用（speculative-investigation-013）**：opencode 1.18.32 交给 `tool.definition` 的定义里，内建工具（`read`/`edit`/`question`/`glob`…）的 `parameters` 是 Effect 参数 schema、`jsonSchema` 为空，provider 侧 schema 由宿主 `ToolJsonSchema.fromTool`（`src/tool/json-schema.ts`）现场渲染。原装饰要求 `parameters.properties` 存在，对这类定义直接抛 `Tool … parameters schema missing object properties`，工具解析失败，用户消息到不了 provider（实机现象：manager 续话直接不工作）。
  修复：新增 `OpenCode/Host/ToolSchemaJson.fs(i)`，用宿主同版本（`4.0.0-beta.83`）的 `effect` 调 `Schema.toJsonSchemaDocument(parameters, { additionalProperties: true })`，并复刻宿主 `normalize`/`inlineLocalReferences`/`dropDefinitionsIfResolved` 的渲染结果，按 schema 对象缓存；装饰改为发布到 JSON schema 视图（`output.jsonSchema`），`parameters`（含 Effect schema）保持原样供宿主解码参数；带 `properties` 的定义仍在原地装饰并同时发布为 JSON schema。`effect` 升为显式运行时依赖。
  验证：新增真实 Host canary（`requirements/host-boundary/tests/support/run-readonly-delegation-schema-canary.mjs` 与 `requirements/speculative-investigation/tests/013.test.mjs`）：配置 Predictor 后 wire 上参与工具带 `required: [estimated_readonly_rounds]` 与条件性 `self_note`（非参与工具零增量），内建工具原有 required 不变，协作说明送达，携带协议字段的内建调用真实执行且 wire 历史保留原调用；另与宿主 `/experimental/tool` 逐工具对拍 11 个内建工具，0 处差异。

- **只读委托升级说明（DELEGATE 12 交付项）**：旧 Strength 环境变量（`WANXIANGSHU_STRENGTH_MODE`、`WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET`、`WANXIANGSHU_STRENGTH_HOST_CANARY`、`WANXIANGSHU_STRENGTH_K1_MARGIN`、`WANXIANGSHU_STRENGTH_K2_MARGIN`、`WANXIANGSHU_STRENGTH_K2_MIN_EVIDENCE`、`WANXIANGSHU_STRENGTH_CONTROL_BPS`、`WANXIANGSHU_STRENGTH_POLICY_VERSION`、`WANXIANGSHU_STRENGTH_SAVED_DEEP_*` 等）全部失效——残留旧值不阻止运行，也不启用任何功能；模型调度配置升级到协议 2（`routingProtocol = 2`、四参数 `route(role, running, previous, purpose)`、`hasTheoreticalCapacity(role, purpose)`、`predictorConfiguration()` 三态存在性查询；`readonly-delegate` 用途从 Predictor 模型池选择，角色与参与者身份不变），旧三参数配置被加载器明确拒绝并给出可操作错误，运行时不覆盖用户已有的 `wanxiangshu.mjs`；Predictor 模型配置成为唯一生产启用依据（配置存在即启用，未配置即无委托，没有独立开关、环境变量或消融选项）；旧 Strength 历史经离线脚本 `scripts/migrate-delegation-history.mjs` 在 EventStore 备份副本上迁移（只追加 `DelegationHistoryImported` 导入事实，cold replay 对照，拒绝活库），未迁移的旧存储在集成规则入口被拒绝消费。用户升级指引落在 README「升级：只读委托（调度协议 2）」节；README 环境变量表与人工巡检文档中的旧变量示范同批清除。

- **修复 K 窗口前缀折叠在单条用户消息的 agent loop 内永不生效（context-compression-028/029）**：`XWire` 的常规 WorkMain 折叠上界原先取“请求里最后一条 `role=user` 消息”的回合。OpenCode 的 loop 把 tool result 挂在 assistant 消息上，因此一条 user 消息驱动的整段 loop 里该上界恒为 turn 0，`PrefixProbeSelection.limit = min(desired, coverage, 0) = 0` 使 `framesThroughCutoff` 恒为空集 → `NoCoverage` → 阶段窗口虽已提交、coverage 也追平，前缀却永不折叠，raw 历史无界增长（实机：manager 会话 15+ 次 assume 提交、coverage 29、窗口 desire 27，`PrefixRebaseCommitted` 0 次，而所有带第二条 user 消息的会话均按 Bⱼ 正常折叠）。
  修复：新增 `XTraceProjection.frontierTurn`（当前 generation 最新语义回合，即本请求正在回答的消息），`XWire.requestStartCutoff` 改由它推导；折叠上界不再与“是否来了新用户消息”耦合，loop 内的 phase-boundary probe 与既有 CTX-011/012 提升路径一致生效。删除失效的 retry 位置回退与 `ProviderWireCapture.trySemanticTurnOfHostMessageId` 在 Wire 的调用点。
  验证：`requirements/context-compression/tests/028.test.mjs` 新增生产级回归（真实 `XWire.applyTransform` + K=2 窗口 + loop 形态请求：修复前 `NoProbeReason` 让计划退化为 `UseCommittedEpoch`，修复后冻结 `UsePrefixProbe` 且 cutoff = B₁，并把被覆盖前缀换成 LWR memory、Opening 与窗口内回合保持 raw）；context-compression 278、prefix-stability 71、host-boundary + provider-attempt-recovery 321 全绿；对该仓库真实 journal 离线重放，manager 会话的 loop 内请求现取 cutoff 27。

- **构建系统增量编译、签名风险检测与模式判定统一（修复与优化）**：
  - **签名风险检测与注释剥离**：签名风险判定改为先严格剥离 F# 注释与字符串（覆盖嵌套块注释与多行/转义字符串）再进行模式扫描，彻底消除注释/字符串中 `[<Literal>]`、`let inline` 等字样导致的误升级（实锤文件改动实测保持 focused 模式）；同时补齐复合属性中任意位置的 Literal 以及全限定名 `Microsoft.FSharp.Core.Literal` 的精确识别。
  - **未归属源文件守卫**：`planImpactCompile` 与 `planImpactFromInventory` 对未归属 `.fs` 改动统一抛出明确的覆盖失配异常（`coverage mismatch unassigned=[...]`），废除原静默 full 兜底。
  - **构建跟踪输入补齐**：将 `compile-order.txt`、根 `global.json`、`scripts/lib/compile-shards.mjs` 及 `scripts/lib/build-state.mjs` 纳入追踪输入；`compile-order.txt` 变更判定为工具链/项目结构变更并触发 full 构建。
  - **增量编译暂存（staging）隔离与导入重定向**：`compileIncremental` 产物先写入独立暂存目录，成功后原子同步至 `dist/`，编译失败不污染输出目录；同步前对逃逸出暂存目录且物理目标落在输出目录内的相对导入执行相对路径重定向（解决跨目录产物 import 逃逸问题），越界与正常导入保持不变。
  - **Fable 缓存复用与输出隔离**：focused 编译的 flat 项目工作目录改按“不含源字节的集合指纹”确定，复用 Fable 编译缓存；编译产物输出目录仍严格按内容指纹隔离；warm 重入继续保证必定调用编译器。
  - **构建模式判定对齐与清单保护**：`scripts/build.mjs` 中 plan 与 run 共享模式判定逻辑（涵盖 no-op / focused / full / clean）；非 `.fs/.fsi` 跟踪输入或工具链变更进入 full 构建且保留 `dist/`（仅显式 `--clean` 或源图删除、重命名清空输出目录）；构建失败或中断不再预先删除旧清单，仅在全链路编译、验证与输出同步成功后才原子写入新 manifest。
  - **回归测试覆盖**：012 套件补齐签名形态独立断言、反面注释/字符串误命中、未映射源抛错、指纹稳定与隔离、staging 失败保护与相对导入重定向、以及构建模式规划与运行判定一致性等多项严格回归断言。

- **修复 Manager 退休被 road 级固定 DevOps PTY 误拦（road/incumbency 资源分层）与固定 DevOps 工作返回收束**：
  - **分层阻塞语义与生产接线**：`RetirementBlockersFor` 明确区分 road 级基础设施与 incumbency 级任期资源——通过递归会话树判定排除固定 DevOps 子会话及其递归子树的 runtime，并在 Manager runtime 上通过显式身份精确剔除 devops agent 资源，使固定 DevOps 的 PTY/agent/pending 不再计入 Manager 的 suicide blockers，同时完好保持 Engineer 等 incumbency 派生子会话与 Manager 自身任务的阻塞语义；完成 `relay-retirement` [003]/[009] 的生产接线，使生产行为与 `decideWithRoadResources` 纯函数规格完全一致。
  - **managed-session-lifecycle 新增 [025] 规约**：PTY 物理退出即清 `HostForkRuntime` 记账（`ptyRuns`/`terminalByName`）；固定 DevOps 每次工作返回（run 终态结算）时收束其 PTY（单 PTY 精准 TERM → 有界等待 → KILL → 等真实物理退出），由 DevOps 工作完成触发而非 Manager 退休触发，不误伤其他会话；[024] 崩溃恢复收束边界保持不变。
  - **PTY 收束能力接线**：新增 `PtyPort.ClosePty`、`HostForkRuntime.CloseOwnedPtys`/`DrainOwnedWork` 及可选 `drainChildPtys` 注入；`ToolRuntimeScope` 中的 `createRuntime` 完成生产接线。
  - **质量缺陷结构修复（D1-D3）**：
    - D1：将 `ToolRuntimeScopeSurface` 提取为独立文件 `ToolRuntimeScopeSurface.fs`/`.fsi`，纯强类型 F# 实现 `DummySessionHostPort`，彻底拔除核心文件中的 `emitJsExpr` raw JS 假桩，`017.test.mjs` 静态门禁自然合规；
    - D2：删除 `isDevOpsPty` 子串匹配启发式（`pty.Contains "devops"`），改由会话树所有权精确判定，彻底消除误伤含 `devops` 命名之正常任期 PTY 的风险；
    - D3：pending-runs 统计彻底废除跨字典混合减法（`PendingRunCount - devopsAgentRunsCount`），改为基于显式身份过滤的 `relevantAgents.Length` 单一真源。
  - **能力插件勘误**：`capability-enforcement-010` 插件工具常量勘误（`'sphinx'` → `'js-manager'`），严格区分插件注册面与角色权限面。
  - **真实执行验证**：`node scripts/build.mjs --clean` 通过（176 surfaces / 829 modules）；`npm run format-build-test` 全绿（format/check/build；unit 3696 passed / 0 failed / 24 skipped；integration 261 passed / 0 failed）；`relay-retirement/009`（5）、`managed-session-lifecycle/017`（4）与 `025`（3）目标用例全数通过。

- 清除 HEAD 上残留的验证红灯（测试、静态门禁与格式化），工作区回到 `format-build-test` 全绿：
  - **语义面债务清零**（`js-semantic-surface-002/004`、`verification-system-013`）：语义测试区最后 5 个深层 dist 导入全部改走已登记 surface。`capability-enforcement` 002/008/025 与 `prefix-stability` 016 的断言改由 owner 面表达：`ToolSurface` 新增评审工具目录（`reviewToolNames`/`isReviewTool`/`reviewToolPermissions`）与角色 Host 权限投影（`rolePermissionRules`）；`OfficeCapabilitySurface` 以普通对象承载 Manager 收口事实（`managerFacts`/`managerFactsAllowed`）；`JsRuntimeSurface.createApiFor` 按 JS capability label 组装绑定（未授予的写成员在 `api.js` 上直接不存在）；`PluginHooksSurface.decorateReviewToolDefinition` 走真实 `tool.definition` 装饰。`verification-system` 夹具 `plugin-fixture.mjs` 的 `injectAcceptedAssessment` 改为 `ObligationJournalSurface.grantWorkOwned`（幂等：已开道路复用 active incumbency/snapshot/authority revision，已接纳评审不重放），夹具不再自建 relay fact，20 个使用方行为不变。`OfficeCapability.permissionLabel`/`permissionOfLabel` 成为 `ToolPermission` ↔ label 的唯一映射点（此前 `OfficeCapabilitySurface` 与 `GeneratorSurface` 各存一份）。
  - **生产控制金字塔清零**：`ManagerReviewContract`、`PluginHooks`、`ToolRegistry`、`Fork/OpenCode/Tool`、`Cognition/{Runtime,Workspace}`、`Js/{ToolWorkflow,ToolsBindings}` 逐处提取具名 helper 消除 `if`/`match`/`try` 嵌套（`fsharp-control-pyramid` 36 项 → 0，基线未放宽）；`plugin-composition` 分片把 `ManagerReviewContract` 排到消费它的 `PluginHooksSurface` 之前（与 `compile-order.txt` 一致）。
  - **测试与门禁对齐**：`prefix-stability/tests/016.test.mjs` 两条用例锚定 001/007，按 `requirement-system-018` 合并进 `001.test.mjs`/`007.test.mjs` 并删除该文件；`cognitive-workspace` 的 005 用例移出 `001.test.mjs` 独立成 `005.test.mjs`；`delegation-028` adapter ratchet 307 → 312（durable spine 与 prefix 分片把 `context-prefix-epoch` 词表改为显式 `ProjectReference` 声明，WHAT 同批记账）；`structured-workflow-014` 的 statictools 分片编译项补 `ManagerReviewTools`；`host-boundary/032` 接入 integration 步序；`capability-enforcement-026` 断言接受真实英文资源文案；`context-compression-016` 的失败夹具补帧 `cutoff`，使 corrupt blob 读取路径真正被走到。
  - 全仓 `fantomas` 一次性对齐（24 个文件此前未格式化）。
  - 验证：`npm run format-build-test` 全阶段绿（unit 3690 passed / 0 failed / 24 skipped；integration 3 组绿，suites 261 + harness 265 passed / 0 failed；`check.mjs` exit 0；js-surface-manifest 175 面、js-module-linkage 828 模块）。
- relay-context-projection 修订（随本批一并落盘）：物理 transcript、durable audit 与下一迭代 provider 投影保留同一份完整历史，`ProjectionCut` 收窄为 durable retirement binding 的请求身份判定与 stale 拦截；`NarrativeTransform`/`ProjectionSurface` 与 WHAT/WHY、`relay-incumbency-010`、`relay-retirement-008`、`relay-context-projection` 001~009 同步。
- **Long Stroke release 层与治理门禁同步到「保留完整历史」合同（relay-context-projection-001）**：Manager 后继迭代不再丢弃退休流量，因此冷边界准入、剧本阶梯、oracle 计数全部重写，`verify:release` 从红转绿。
  - 冷边界准入（`cold-boundary.js` 的 `manager-loop`）：系统/工具计划不变的前提下，上一条 wire 必须是下一条的**有序前缀**，前缀内唯一容许的差异是已退休的辅助注入（Host 的 `\0\uFEFF` guidance 后缀、companion frame 的 preamble 行与其合成 ack 行），源自 context-compression-019 / GAP-022；改写、丢弃或插入历史消息仍 fail closed。历史评测保留在被比较的消息体内，不靠“truncate 后再比”。
  - harness gate case 同步（`cold-boundary-cases.mjs`）：`MANAGER_RETIRED` 现在带真实的 guidance 后缀，`MANAGER_NEXT` 是「同历史 + 去掉已退休 occurrence + 追加自己的 fresh-head prompt」；新增「丢弃已保留的 nudge / 插入合成 wake」变红用例，旧「丢 nudge 被接受」断言删除（它是旧合同的直接表达）；派生序列（forest-lib）保持旧的两交付形状，并在注释里注明后继迭代由 production 组装、不在可派生范围内。
  - 剧本与 oracle（`long-stroke.toml`、`long-stroke-oracles.mjs`、`014.test.mjs`）：Manager 会话认知工具改 `assume`（update/todos）、工具列表与 `toolsGate` 同步；`manager-reopened-loop` 文案改为现行 `runtime/manager-assess`，阶梯补到 step 3/4（fork → join → close），后继审计计数按 session 统计（`matchCount(id, sessionId)`，复用 turn family 在两个 lane 上都出现）；preflow 保留 `humanroot-*` 族各一次交付、后继迭代改由 assess-resource 族回答；已废弃的 `[[epoch]] manager-loop`（manager-loop.0 / humanroot-loop.0）声明删除，改为唯一 `manager-reopened-loop.0` 声明。
  - 已退休的 todowrite 膜 canary 收口：`assume` 是直写工具，不再有 provider-arg 重写与 after 钩子 enrichment 可供观察，故删除 wrapper 插件 `manager-tool-surface-canary-plugin.mjs`（及其在 `scenario-parallel.js` / `scenario-driver.mjs` / 剧本 `setup.magicTodoHostCanary` 的接线）与 792 行膜断言，改由 `manager-tool-surface-evidence.mjs` 用真实 wire 证据断言「`assume` 已公布、Manager 脊柱工具齐备、已退休 `todowrite` 从未出现」；host-boundary-019 保留自带的膜载体。
  - 验证：`npm run verify:release` 全阶段绿（format:check / check / build --clean / unit 3690 / integration 261 + harness 265 / **e2e Long Stroke** / package）。

- Manager 直接只读取证的窗口写回提示词（此前角色法一律禁止 Manager 亲自检视，与既有条款相互矛盾）：`role/manager` 双语在开篇与“亲自检视”一节写明唯一例外——评审未接纳前用评审专用只读工具（`js-manager`：Read/Glob/Grep）直接阅读静态快照，评审接纳即关闭，窗口外仍不亲自调查、修改或运行工作树；评审阶段“建立事实”条目补上这条直接阅读通道。`lifecycle/manager/t1-revelation` 双语同步：不再一律禁止检视，改为评审未接纳前静态快照由 Manager 亲自直接阅读，接纳后事实来自 Engineer 与 DevOps。`office-capability-007` 新增 `manager_direct_read_window_is_bounded_by_review_acceptance`，断言角色法双语同时写明只读工具与评审接纳的开闭边界；`docs/index.html` 角色表 Manager 行同步订正（读代码标为“评审前只读”，边界句去掉“不读写源码，全靠委派”）。验证：office-capability（24 passed）、provider-language、relay-assessment、participant-horizon、cognitive-environment、distribution、prefix-stability（71 passed）、action-affordance 与 capability-enforcement-025 全绿；`language-parity-gate` OK（333 资源）；`check.mjs` 本批文件零告警。

- **context-compression-028/029 落地：K=2 阶段窗口真正决定前缀 cutoff**（`PhaseWindow` 此前只有纯函数与测试，无生产消费者；默认 K 连常量都没有）。本批把窗口接到既有 freeze 机制上，不新增事件类型、不改动已提交事件的载荷：
  - 冻结默认与载体：`PhaseWindow.defaultK = 2`（owner 开启时冻结的唯一来源，`validateK` 拒绝非正数）；新增有界窗口 `PhaseWindow.PhaseCommitWindow`（`PhaseCallIds`，commit 顺序，`appendPhase` 保留最后 K 个）与 `desiredCutoffOf`（注入 `ToolCallId → turn` 映射以保持纯函数；最旧保留阶段的回合不可寻址时返回 `NoPhases`，不回退到下一个阶段）。
  - 投影接线：`AgentProjectionSet.PhaseCommits: Map<SessionId, PhaseCommitWindow>`（键为会话，与认知投影“哪个画板是当前”的 owner 键分开），由 `Composition/Durable/Fold.fs` 在**认知提交被接纳时**追加（被拒绝或重放的行不移动窗口），经 `AgentJournalPortAdapter.forWire` 进入 `WireSessionState`。
  - turn 对齐：`XTraceProjection.tryTurnOfToolCallId` 只在当前 generation 回答阶段自己的回合起点。
  - 材料子集：`BlogFrame.CutoffExclusive`（每帧自己的完整回合证明，entry 取观测的 next cutoff、squash 取被替换帧的最大值）+ `BlogProjection.framesThroughCutoff`——跨过边界的帧不用而不是截断（“必须使用可验证的完整材料子集，否则本次不前移”）。
  - 决定与证明：`PrefixProbeSelection.limit` 给出实现上界（窗口 desire、已证 coverage、request start 三者取小），`select` 改为校验**调用方实际冻结的材料边界**并分型拒绝（新增 `BeyondPhaseBoundary`、`MaterialBeyondBoundary`，删除不再可能出现的 `CoverageNotAheadOfRequest`）；证明改为在 Companion 自己的 cutoff 上核对 digest，快照记录材料边界的 digest——两者不再是同一个数。`ProbeBound.PhaseBoundary | CoverageOnly` 把常规窗口推进与 WHAT-029 的失败恢复例外显式分开（后者按已证 coverage 折叠，可越过窗口）。
  - 生产接线：`Wire.planPhaseBoundaryPrefix` 在**常规 WorkMain**请求上按窗口 desire 建 probe（`Wire.candidate` 从覆盖前沿改为窗口+子集），用该请求在 ChatExecution 投影里记录的真实 `PromptOrigin` 冻结 attempt plan，并只在本 attempt 成功时由既有 `PrefixRebaseCommitted` 提升 epoch；阶段内部窗口不变，故同一阶段内两次请求投影同一份 committed prefix（freeze 不变）。写回把窗口内 assume 回合作为 `retainedAssumeCallIds` 传入，紧急 probe 越过窗口时活动画板不会被摘要替换。
  - 测试：`028.test.mjs` 增补默认 K、窗口等价性（`min(N,K)` 截断后与全历史取同一 `Bj`）、按 commit 顺序有界、无地址阶段不给 cutoff；新增 `029.test.mjs`（窗口边界限幅、材料越过窗口/请求上界拒绝、紧急例外可越过窗口、不回退已提交 cutoff、前沿与材料两个 digest 的分工、失配 fail-closed）；`016`/`018` 与 `host-boundary 008/020/021`、`provider-attempt-recovery 011` 的夹具按新证明契约更新（材料边界与 digests）。验证：以上五包 `694 passed / 0 failed`，`node scripts/build.mjs` 绿（174 surfaces / 827 modules）。
  - 已知未验证：常规 arm 的真实 provider wire 证据（方案 P7 的退出条件，需要 long-stroke/e2e）；聚合 fold → 窗口的投影级单测因 `js-boundary` 债务门禁不允许测试直接 import `dist/Composition/Durable/*.js`，改由编译与代码路径覆盖。

- 文档对齐（README 与万象术投资材料按当前实现修正）：README 角色工具面表补 `js-engineer`、`js-devops` 与 Engineer 的 `bash-honeypot`，权限落点由 `Roles.permissions` 更正为 `OfficeCapability.permissions`（`src/Wanxiangshu/Foundation/OfficeCapability.fs`）；构建表述改为默认增量（no-op / focused / clean 三模式，拓扑变化或 `--clean` 时清空 `dist/` 全量重建，其余情况增量编译）；验证日志与 CI 表述对齐实际（默认写 `.fable-build/verify-logs/`，被 .gitignore 忽略；`.github/workflows/ci.yml` 运行 `verify:release`，无 artifact 上传步骤）。万象体系两份投资材料同步补充 Orchestrator 与角色订正。
- README 订正两处滞后表述并补一处仓库结构：角色工具面表移除 DevOps 的 `sphinx`（`OfficeCapability.permissions` 的 DevOps 集合为 Read/Write/Edit/Glob/Grep/Move/Remove/Exec/Pty/Join/Horizon，无 `ToolPermission.Sphinx`，`StaticTools.permissionFor` 亦无 DevOps 单开 sphinx 的特例；Manager/Orchestrator/Engineer 的 sphinx 不变）；`WANXIANGSHU_SKIP_AUTO_INJECTED` 一行删去「`provider=cursor` 时同样跳过新注入」的失效限定（skip 逻辑忽略 providerId，cursor 模式已统一至所有 provider，已落盘历史 pair 仍 replay）；仓库结构清单补 `万象体系/`（投资人材料 DOC.html、PPT.html）一行。
- 万象体系投资人稿（`万象体系/DOC.html`、`万象体系/PPT.html`）修正四处与实现不符的表述：制度学习段（DOC）由「自动制度学习仍属研究方向」改为机制落地叙述——经验经 `celebrate` 与 `regret` 登记，有界评估决定被既有规则吸收、直接淘汰或申请成为新规则，通过准入门槛的规律才进入统一制度规则库，全程受纪律约束；附录注释（DOC）订正内部机制归类——Bookkeeper（案例维护）与 Predictor（Strength 降级指定廉价模型）属不进公开调度的内部机制，Coder / Inspector / Browser / Inquiry / Distiller 已退役；Fission 点名两处（DOC 正文与 PPT Slide 9）——工程师可现场把工作裂成并行路线，路线仍属于同一个工程责任，不因多开执行路径而分裂出多个责任主体。
- 修复 `suicide` 后旧 Manager 未被中断：`Continue` 退休保留 LogicalRun，不能据此将旧请求当作后继。Transform 以当前物理消息的正式 manager-loop gate／新 HumanRoot／已接纳的人类 continuation 识别后继；旧请求释放 exact provider step 并等待 Host interrupt 后才派发下一迭代。Host 允许仍持有旧 authority root 的退休 attempt 中断，拒绝中断不再静默吞掉；`relay-retirement-008` 覆盖旧请求清空、Host abort、下一迭代派发及不继承旧中断。
- verification-system 契约面测试行为化重写与验证闭环（行为化重写与门禁闭合）：
  - `requirements/verification-system/tests/008.test.mjs` 行为化重写：删除全部匹配源代码/编译器内部表示的脆弱断言（深层导入未注册 dist 模块、mangled 符号 `BloggerDelta_nextChunk`/`Parallel_mapBounded` 等、`surfaceOf` 反射探测及 dist 全遍历动态加载）；保留并加强行为级回归（`assertBuildFresh` 防陈旧/防损坏、manifest 损坏、产物缺失/陈旧、reverse-consumer 重编译、未跟踪源盘点失败及 release 重置），呼应重写后 WHAT [001] 新增的「测试应该测试行为而非实现，因此不要写匹配源代码的测试」；VERIFICATION-SYSTEM-008 机器载体 = `requirements/verification-system/tests/008.test.mjs`（9/9 全绿）。
  - `scripts/checks/js-module-linkage.mjs` 新增 `validateModuleLoadability`：构建期动态 import 全部生产 dist 模块（821 个），加载失败即门禁失败；run/check/runCli 同步执行静态链接与动态可加载性校验；`scripts/build.mjs:190` 相应改为 await 调用，修复原同步假设导致的 `Promise !== 0` 假阴性。
  - `scripts/lib/test-surface-scan.mjs`：移除 008.test.mjs 的 `BUILD_VERIFICATION_FILES` 豁免，使 js-boundary-gate 在零豁免下全面管辖该文件（A-D 扫描零债务）；清理 5 条指向不存在文件的幽灵条目，保留 9 条有真实理由的条目。
  - VERIFICATION-SYSTEM-004 机器载体补齐：`requirements/verification-system/tests/004.test.mjs` 新增 js-boundary-gate 受控反例回归，运行时动态拼装含 deep-dist-import 与 export-discovery 的临时夹具，断言门禁精确报告两类债务、run 退出码 1、清除后归零，补齐 [004] 对每个静态门禁具备可红性的要求（该门禁此前无反例回归）。
  - 运行器中断断言归一化与死代码清理（依据 [010]）：在 001/002/009/010.test.mjs 中删除四处机械复制的 runner 失败中断断言（五处经逐行复核实质等价，保留 005.test.mjs 正本），清理因删除伴生的未用导入与死代码夹具，明确删除冗余治理断言不属于放宽验收红线。
  - `scripts/checks/js-surface-manifest.mjs` 头部注释校准为 WHAT[js-semantic-surface-003] / WHAT[verification-system-013]，准确反映现行条款关系。
  - 验证证据：`npm run check` 18/18 通过；`build` 成功（js-surface-manifest 173 注册面闭合、js-module-linkage 821 模块链接且加载通过）；008 9/9；单元套件全绿；集成套件 3 组全绿（34 文件 242 例 + distribution + harness 265 例）。
- verification-system 规范重写与验证金字塔收敛（规范修正）：WHAT.md 重写为朴素四层金字塔（Pure laws → Temporal → Adapter → Long Stroke），删除原第 0 层静态门禁层与第 5 层 Release 层的冗余层述，层序由 scripts/verify.mjs 固定阶段调度的事实保证，并明令「测试应该测试行为而非实现」；条款标题与正文去实现细节（[002] Long Stroke、[003] 阶梯、[005] 中止、[006] 因果推进、[012] 禁止机械行数检查、[013] JS 语义边界）；VERIFICATION-SYSTEM-006 禁止退化清单整理为 14 项；VERIFICATION-SYSTEM-016 收窄为「执行前捕获输入快照与内容哈希」与「运行期输入被改写、新增或删除时立即中断并非零退出」两条核心不变量（logDirectory 与 TESTS_MJS_FILES 剥离等偶发细节移出规范，实现与测试保留不动）。影响面落点：WHY.md 增补 [012]/[015]/[016] 存在理由与 RED 不变量，scripts/verify.mjs 每个 step 后即时 diff 输入快照、扰动即中断后续阶段，看门狗语义用例自 004.test.mjs 迁至 006.test.mjs 且 tests 树旧编号清零，src 下 13 处 F# 注释条款引用更新，README 对齐四层金字塔与入口分流，requirements/INDEX.md 条款区间更正为 verification-system-001 ~ 016。
- 修复 provider 限流（429 / `Rate limit reached`）后既不重试也不报错的静默停摆：实机事故为 Manager 会话连续两步 rate limit，第二步 errored assistant message 因 formal 文本为空被 PAR-008 判为 `TurnNeedsContinuation`，而该失败 attempt 的 durable `ProviderStarted` 缺失（`Accepted ∧ ¬ProviderStarted` 形状），恢复链取不到 request kind 直接 `notifyFailure` 收摊；同时宿主精确终态投影被 `settleExactTerminal` 的 start 证据门禁拒绝，发送栅栏永不 observe，任何已授权重投都会在 `AwaitStop` 上永久等待。三处修正：① `continueProviderStart` 在 start 事实持久化失败时仍执行 `continueStartedLifecycle`，② `settleExactTerminal` 对 `ProviderFailure` 不再要求 start 证据（精确终态投影本身即 attempt-stop 观察），③ `requestKindFor` 对非 satellite session 在缺 start 事实时以 accepted evidence 的 ordinary origin 命名 kind（Blogger 仍要求 durable start）；另修 Manager `observe` 静默丢弃 `TurnNeedsContinuation` 的旁路。落点：`provider-attempt-recovery` WHAT[004]/[008]/[022]、`requirements/provider-attempt-recovery/tests/004.test.mjs`（accepted-without-start 仍命名 ordinary kind）、`008.test.mjs`（unfinished manager turn 进入 bounded Interaction Repair）、managed-chat-execution 005/006 形状断言。
- 修复 Inquiry/主会话并发流式调用多个 Inspector 时后续调用返回“未能完成”：采用延期收拢与 Transform 历史持久化替换方案。流式阶段各 `inspect` 调用瞬时登记并返回占位接受文本（避免流式阶段单项早决锁死与批次冲突）；宿主进入下一步前在 `PluginTransforms`（`experimental.chat.messages.transform`）集中提取本轮全部 charges 并单次调度 Inspector 子会话，取得权威 `WorkRecord` 与 sibling 引用持久化缓存，并在每次 Transform 时就地替换所有历史 tool 结果。端到端验证覆盖 56 步 Long Stroke G2 及全部 33 项 delegation 单元测试。
- 实机事故补证（PAR-021 残余偏差）：首败保留目标上线后，用户实测仍观察到未经连续失败的 provider 被换路。根因：`failedAttemptWasLwrRetry` 按 `(session, physicalUserMessageId)` 读 durable `ProviderRetryAttempt` 接受事实，但一个物理用户消息驱动多个 provider step（agent loop）：LWR 重试 step0 成功后，同一请求 step2 的失败仍命中该事实，被误判为「LWR 重试失败」而首次失败即定罪换路。修复：定罪需双 durable 事实——continuation 接受 AND 失败的 `ProviderRunIdentity` 正是为该请求建立 `ProviderStarted` 的 run（请求级唯一，后续 step 不覆写）；后续 step 失败走 retain 路径重新绑定＋再试 LWR。`ProviderFailureSurface.wasLwrRetryAttempt` 加 providerRun 参数并新增 `establishProviderRun` 测试 seam；落点：`requirements/provider-attempt-recovery/tests/lwr-retry-before-condemn.test.mjs` 双事实断言（pr-run-1 定罪 / pr-run-2 后续 step 不定罪）。
- 修正 provider 失败驱逐时机（PAR-021 / EMR-017）：失败恢复不再在 `Retry.attempt` 之前无条件 `markProviderFailed`，消除首次失败即全进程驱逐 provider 的偏差。首次确认失败保留原物理目标：结算以 exact witness 写入单次消费的 recovery retry 绑定，下一次 fresh admission 优先路由回该目标，并按既有 wire 机制以 LWR 替换上下文发起重投；只有失败 attempt 自身就是 `ProviderRetryAttempt` continuation（durable 接受事实，绝不按失败序号推断）时，才 poison 该 provider 并进入调度轮换。普通恢复、SyncDelegate 装饰器与恢复重入共用 `Fallback/Workflow.fs` 内同一结算；重复通知、旧回调、取消与提交未知不取得该权限。落点：`requirements/provider-attempt-recovery/tests/lwr-retry-before-condemn.test.mjs`（6/6），同步更新 WHAT/HOW/WHY 与 EMR-017。
- 操作者决定：Host 错误边界不再做失败分类（新增 EXECFAIL-009）。除 typed control（abort → `UserCancelled`、supersede → `Superseded`）外，`session.error` 与 assistant message error 一律解码为 `ProviderTransient` 进入 provider recovery；工具调用等语义错误仍由 OpenCode 在自身循环内处理，不经过本边界。封闭代数保留（provider adapter / 持久化边界 / legacy 解码仍使用），HOSTFAIL-004 的 hook 失败 fail-loud 与 HOSTFAIL-006 的预算耗尽唯一 typed terminal 不变；重试以替换上下文方式发出（wire 重建 prefix + `ProviderRetryAttempt` continuation）。`HostEventCodec` 删除 status/name 映射表与 `statusCodeOf`；落点更新：host-codec（EXECFAIL-001/008）、cancel-retry-stream（EXECFAIL-002）、provider-terminal-accounting（CHATEXEC-006）。
- 修复子会话 provider 报错直达调用方（无重试）：实际事故为上游代理返回 `{type:"upstream_error"}` 且 assistant 正文只有 reasoning（formal 文本为空）。旧路径把这类出错 attempt 判为 `TurnFailed`（无 typed provider 证据 → 策略终结），子会话于是把失败直接交回 main session。按 PAR-008 修正：`CompletedTurnClassifier.classifyErroredContent` 对 formal 文本不可用（`TerminalValidity`：空/XML-only）的出错 attempt 保留 `TurnNeedsContinuation`；`ReconcilePass.materializeFailureWitness` 仅在 `failureWitnessMintsTerminal`（confirmed provider 类，或 formal 内容可用）时铸造 `TurnFailed`。这类失败因此落入 `InteractionRepairWorkflow.repairMissingFinalReport`（idle 门控、替换失效上下文的 repair nudge），既不推进失败预算也不向调用方报终态；confirmed provider 类（transient/permanent）仍由策略决定重试/终结，fallback 行为不变。落点：`WHAT[PAR-008] an_errored_attempt_with_unusable_content_never_mints_a_provider_terminal`、`WHAT[PAR-008] RECON_formal_content_gate_is_shared_with_terminal_validity`。
- 事故：首条消息 hang。根因是 git `pre-push` 钩子进程（`resources/git/wanxiang-hook.mjs pre-push`）在 push 结束后成为孤儿（PPID 1）并卡住，但仍持有 `.git/wanxiang.lock`（`owner.json` 指向该活进程）；`ProcessEventLog.acquireStoreLock` 只在 owner pid 死亡时才判定陈旧，且以 `retries.forever` 等待，因此之后每次 durable 激活（首条消息）永久阻塞。处置：杀掉孤儿钩子进程并释放锁（本仓库实测锁1ms 可获取），git-hook 集成默认关闭（`WANXIANG_GIT_SYNC=1` 才安装），仓库内两个 `Wanxiangshu HookDispatcher` 钩子已移除；事件真值仍为 `.git/wanxiang/events/<WriterId>.ndjson`，本地追加不受影响。待办：钩子内的 converge 不得跨网络 I/O 持锁且必须有超时，`acquireStoreLock` 应有界重试并给强类型诊断。
- 修复 HostInternal 分类误把带 synthetic 回显 part 的真实用户消息（TUI @-文件提及）视为纯 Host 合成提示，导致跳过受管 admission/模型投影、`cursor/auto` 残留并使 `chat.params` 以 PROMPT-006 drift fail-closed 的 def 缺：synthetic 判定收紧为整条消息所有 part 均为 `synthetic:true` 才归 HostInternal；INTERACTION-AUTHORITY HOW 补充混存消息约定；新增 `MISC_ingress_host_synthetic_requires_fully_injected_material` 回归测试，真实 wire payload 端到端复验为 `ExternalRootIntent` 进受管路径。
- 审计并移除 `mission-relay-suicidetool` 与 `foundation-temporal` 对 `chat-execution/outcome` 的 2 条冗余跨 subsystem ProjectReference，彻底切断 `relay -> chat-execution` 与 `process -> chat-execution` 两条跨子系统边，全仓声明引用数降至 1874，全部通过独立 Fable 聚焦编译及消费者闭包验证。
- 清理 `Distillation.fs` 与 `DistillationRuntime.fs` 中未使用的 `Strength` 命名空间引入，将 `process-largegatesurface` 对 `host-digest` 的宽引用收窄为 `runtime-platform/digest`，并移除其对 `execution-session-recovery-model` 与 `process-processrequest` 的 2 条冗余跨 subsystem ProjectReference，全仓声明引用数降至 1876，全部通过独立 Fable 聚焦编译及消费者闭包验证。
- 清理 `Capture.fs`、`SyncDelegate/Runtime.fs` 与 Fork/Host 源码中未使用的 `Host.Contract`、`Strength` 与 `Strength.Prediction` 命名空间引入；系统审计并移除 `execution-fission-opencode-host`、`opencode-codec-providerprojectionsurface`、`opencode-host-requirementgrounding-runtime`、`enforcer-guidance-tip`、`interaction-authority-fold`、`opencode-host-chatadmission-transaction`、`execution-session-opencode-horizontool`、`mission-relay-reviewtool`、`mission-relay-suicidetool`、`context-trace-semantictracesurface`、`strength-opencode-settings` 与 `strength-turnevidence` 等 12 个分片中的跨 subsystem ProjectReference，全仓声明引用数降至 1878，全部通过独立 Fable 聚焦编译及消费者闭包验证。
- 审计并移除 `hostturnobservedsurface`、`host-diagnostics-runtime`、`casebook-lifecyclesurface`、`magictodosemanticsurface`、`opencode-tools-ptytool` 与 `participant-provider-attempt-planner` 中 6 条无真实符号消费的跨 subsystem ProjectReference，显式补齐真实消费声明，全仓声明引用数降至 1890，全部通过独立 Fable 聚焦编译验证。
- 审计并移除 `resources-promptsurface`、`context-companion-companionfactfold`、`context-compression-runtime-surface`、`delegation-fold`、`delegation-ledger`、`strength-persistence-durabilityport` 与 `strength-policy` 等 7 个分片中 7 条无真实符号消费的跨 subsystem ProjectReference，全仓声明引用数降至 1895，全部通过独立 Fable 聚焦编译及消费者闭包验证。
- 审计并移除 `enforcer/enforcer-codec`、`enforcer/enforcer-continuation` 与 `enforcer/repair` 中 8 条无真实源码符号消费的跨 subsystem ProjectReference，全仓声明引用数降至 1902，全部通过聚焦 Fable 编译及消费者闭包验证。
- 审计并移除 `interaction/opencode-tools-executortoolsurface`、`enforcer/enforcer-institutionallearning-fold`、`knowledge/repository-knowledge-casebook-bookkeeper` 等分片中 5 条无真实源码符号消费的跨 subsystem ProjectReference，全仓声明引用数降至 1910，全部通过聚焦 Fable 编译及消费者闭包验证。
- 审计并移除 `persistence/git-hook-sync` 对 `change/change-fact` 的冗余跨 subsystem ProjectReference，并通过聚焦 Fable 编译及消费者闭包验证。
- 审计并移除 `opencode-tools-executortoolsurface` 对 `opencode-tools-ptytool` 的冗余 ProjectReference，使其前向闭包从 144 项目缩减为 141 项目，并通过独立 Fable 聚焦编译验证。
- persistence（17分片）、provider（17分片）与 host（17分片）共计 51 个 compile shard 全部补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，全仓全部 26 个子系统共 213 个编译分片达成 100% 显式归属。
- session-lifecycle 与 delegation 两个子系统剩余 35 个 compile shard 补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这两个子系统达成 100% 显式归属，全仓共 23 个子系统实现全显式归属。
- interaction 与 context 两个子系统剩余 23 个 compile shard 补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这两个子系统达成 100% 显式归属，全仓共 21 个子系统实现全显式归属。
- authority、relay、chat-execution 与 enforcer 四个子系统剩余 28 个 compile shard 补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这四个子系统达成 100% 显式归属，全仓共 19 个子系统实现全显式归属。
- repository-programming、requirements、dispatch、knowledge 与 work 五个子系统剩余 19 个 compile shard 补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这五个子系统达成 100% 显式归属，全仓共 15 个子系统实现全显式归属。
- change、strength、resources、output 与 repository-investigation 五个子系统剩余 15 个 compile shard 补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这五个子系统达成 100% 显式归属，全仓共 10 个子系统实现全显式归属。
- participant、sphinx、process 与 verification 四个子系统剩余 compile shard 补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这四个子系统达成 100% 显式归属。
- causal-wait、time-capability 与 host-boundary 边界验证统一迁移至 subsystem/compile-shard inventory，移除对已退役 `readCompileShardInventoryV1` helper 及其 legacy locality/kind 属性的依赖；彻底清理 `readCompileShardInventoryV1`。
- EventStore 编译边界与容量禁用检查统一迁移至 subsystem/compile-shard inventory，移除重复 fsproj readdir/XML legacy kind 字符串解析及 legacy owner 属性依赖；按 persistence/strength subsystem 与显式 compile shard 验证闭包排除、生产源码预算及禁止容量词汇。显式元数据及非契约分片、错误 subsystem 反例通过，生产工程不变。

- 增量编译证明的临时工程改用显式 subsystem／compile-shard，去掉旧 owner／locality／kind 夹具参数。保留精确影响集合、合并编译、缓存及 CLI 断言；实现错误扩入反向消费者和签名遗漏反向消费者的两个反例均被拒绝，生产 planner 与工程不变。

- 生产 compiler-boundary 证明统一读取现有 subsystem inventory，移除重复 fsproj 扫描、XML 引用解析和 GitGateway 的旧 locality 标签断言。保留 GitGateway、NodeFs／工具合同、request kind／fallback facts 的源码与依赖边界；显式元数据正例、错误 subsystem 与缺失物理 provider 反例通过，planner fixtures 和生产工程不变。

- Delegation 编译边界证明复用 subsystem/compile-shard inventory，移除旧 kind 与 owner 文件名筛选依赖，验证真实 subsystem 归属。保留 DELEG-028 明文预算、增长 ratchet、物理隔离与必要 provider 断言；显式元数据正例及错误归属、Process 依赖反例通过，生产工程与合同不变。

- Host 闭包测试复用 compile-shard/subsystem inventory，移除退役 locality/kind、数量预算与 legacy owner 文件筛选；保留真实依赖与隔离断言，覆盖显式 Host 分片和 runtime-platform 摘要归属。显式声明正例及工具能力泄漏、错误归属反例通过，生产工程与行为不变。

- 按用户裁决同步 HOST-BOUNDARY-026：独立列出消息、SDK 类型、终端合同与工具适配器，摘要原语归 runtime-platform；普通业务契约按实际知识消费窄合同，不再受两个旧名称限制。保留物理能力隔离、唯一实现和失败语义；未修改源码、工程或验证阈值，残余 Host legacy 验证缺口记入 GAP-033。

- 将既有 ToolHostCodec／ToolHostSurface 独立为 Host 工具适配分片，工具消费者不再为参数解码、schema 与注册编入信号路由和终端总线；bootstrap 显式装配工具与信号两侧。保留所有源码、公开签名、取消释放、输出截断与 aggregate 顺序，不复制物理实现。

- 终端事件合同不再传递 SDK 类型、MessagePart 或摘要实现；SessionSnapshot 显式引用唯一消息合同，诊断与消息可见性移除不使用的终端引用。补齐 signal adapter 原本漏报的 failure、chat-execution 与 RuntimePath 静态依赖，清除失效 namespace 引入；保持终端重放、精确 authority、取消与公开签名。

- 将既有 OpencodeTypes sibling 源码独立到零引用的 Host SDK 类型分片；OpenCodeContract 与模型路由直接消费它，不再为 OpencodeModel 引入终端事件和 MessagePart。保持 SDK wire 类型、模型投影、公开签名与 aggregate 顺序；既有编译边界回归拒绝两条 consumer 恢复宽 Host 引用。

- 将既有 MessagePart sibling 源码独立到零引用的 host/message contract 分片；HostMessageCodec 直接消费它，不再编入无关 SDK DTO、终端事件和摘要实现。保持消息 union、decoder 行为、全部公开签名与 aggregate 顺序；编译闭包回归明确拒绝重新引入宽 Host 引用。

- 删除无调用方的 OpenCode GitTree 适配器与 GitTreePort，保留 EventStore GitTree 和 Relay snapshot；JoinAttemptRegistry 独立为窄 delegation 分片，补齐 PluginSessionScope 的真实静态依赖，不再靠聚合编译补入 registry。

- Provider wire decoder 与 Git hook 分片直接引用 runtime-platform/digest，去掉未使用的 OpenCode 消息／事件合同闭包；保留媒体 URL 摘要、Git common-dir socket key、用户 SSH 配置、全部源码与公开签名。

- Change 事实分片直接引用 runtime-platform/digest，不再为 RuntimePath 的工作区摘要引入 OpenCode 消息／事件合同；保留 Git common-dir、XDG fallback、事实投影与全部公开签名。

- Requirement Grounding 模型与 Relay workspace snapshot 直接引用 runtime-platform/digest，去掉未使用的 OpenCode 消息／事件编译输入；保留规范材料与 package 摘要字节、Git snapshot canonical 输入和全部公开签名。

- Institutional Learning 的事实／Enhancer 分片直接引用 runtime-platform/digest，不再通过摘要取得 OpenCode 消息、事件和角色闭包；保留规则版本输入、学习 disposition 与冻结重放语义。

- 将既有 UTF-8 字符串 SHA-256 原语抽到无领域引用的 runtime-platform/digest 编译分片；Casebook 与 Sphinx 删除重复 crypto 实现，Sphinx 全部调用方迁移并删除旧摘要导出。摘要输入、canonical JSON、事件身份、salt 与 Casebook null 语义不变；二进制 SHA-1／SHA-256 不合并。真实捕获测试改为独立固定摘要，删除只测试测试内 crypto 的伪 Host 证明。

- CompletionMailbox、Change VerdictMailbox 与 HostForkJoin 的等待竞争改用 typed Choice，删除手写数字标签和无类型结果字段解码；保持单次 Promise 映射、注册顺序、drain-first 与局部中断语义。补充真实 verdict mailbox 的优先级、waiter 释放和有界 FIFO 证明，删除误称等待证明的重复 renderer／源码 token 检查。

- ToolHostSurface 的 schema 解包移回 ToolHostCodec 的 internal typed 合同，删除 Fable 私有表示探针，避免把原生 schema 的 `.value` 错解为返回值；保留私有 HostSchema 构造器。删除迎合旧解包的 mock 形状测试，改以真实 SDK validator 和旧败新胜的 literal schema 反例验证。

- Prefix Wire 的 Replica 识别与 Authority 检查改用既有 StrengthRuntime／StrengthReplicaBinding 合同，删除私有字典路径、无类型字段读取和异常吞没；Fallback Workflow 改用 SessionAssociationProjection 的类型安全查询，删除 Map 扫描与手写 union tag。两处补齐真实静态依赖，不改变重试预算、前缀选择、材料等待或 Replica 权限策略。

- CanonicalIntegrator 的 Casebook／JS transaction oracle、需求接地 glob 匹配和 repository 观察 Surface 恢复静态 typed 调用及真实编译引用，删除动态模块加载、编译器 union 布局解码与缺失模块 fallback。Generator 的既有 `typedRole` 合同公开给真实 composition consumer，不复制生成流程；接地失败词汇、观察失败不阻断已提交修改、事务 Current 与重放语义保持不变。

- RequirementGroundingTransform 与 PromptResources 恢复对既有消息投影、ProviderResources 的静态 typed 调用及真实 ProjectReference，删除动态加载、手写成功 union、空资源与静默跳过校验的 fallback。规范重放证明改为比较冻结终端结果字节，验证磁盘内容变化不会改写历史，新的内容版本仅追加；删除只匹配入口符号的伪证明。

- 修复成功 retry 的后续 tool step 因新增 Blogger coverage 再次选择 prefix probe、造成未声明冷边界的问题。候选资格读取已结算的连续失败计数，零失败保持 committed prefix 并跳过候选物化；已有 frozen plan 保持不变，新失败仍可恢复 probe。

- 修复 HostSignalBootstrap 动态查找 LoopSensor 失败时静默丢失退化保护：静态构造并装配真实 sensor，reset／observe／drop 全部使用 typed 合同，复用异常类别资源映射。真实插件回归证明 managed child 重复流触发一次物理中断，root 与 foreign session 保持豁免。

- ToolRegistry 与 PluginHooks 恢复静态 typed 工具注册、Casebook 门禁／观察捕获／工具接线，删除动态模块查找、备用权限判定和静默漏注册。JS 事务持久化能力从 PluginHostInterop factory 到 registry 全程保持 `IJsTransactionPersistence`；真实 consumer 编译补齐 ExecutorTool 已有 Distillation 依赖，不以丢失持久化或隐藏引用冒充解耦。

- Host provider 校验、chat admission 绑定／释放和证明 Surface 恢复对 `SessionExecutionBinding` 的静态 typed 调用，移除动态加载、手写 union、静默 no-op 和虚假零值；SyncDelegate 证明入口直接构造并执行真实 Inspector tool，不依赖生成 JavaScript 的构造器布局。真实 consumer 并集编译同时清除 JoinSurface 的陈旧 Manager namespace 引入，保留 exact settlement、child 复用与 bounded WorkRecord 语义。

- Fork WarmStart 与 Prefix WorkRecord 恢复静态 typed 依赖，删除动态加载、union 解码和默认成功／备用渲染路径；保留 WarmStart 查询级 fail-open、无关键词零工作、同 session Opening 排除与 frame 读取失败语义。移除只匹配源码注释的 Opening 伪证明，记录真实行为证明的覆盖边界。

- Coder WarmStart 恢复静态 typed 调用，保留查询级 fail-open，移除 adapter 动态加载、union 解码与 catch-all；Blogger context 构造归入 MainContext，删除旧 Enforcer Host 和 Recovery 副本。Coverage 出生门测试改走真实 trace fold／生产函数，补足无法映射与同 turn 推进的反例，移除虚假 precheck 证明；JoinGuard 证明 adapter 独立编译并显式声明实际依赖。

- `ToolRuntimeScope` 的 Relay 查询改用既有 typed `RoadView`，移除 JavaScript Map／union 布局探针；退休围栏以 `IncumbencyId` 存储并暴露，保持 assessment、证书三项绑定、同任期冻结与新任期清除旧围栏的语义。

- 恢复 `ToolRuntimeScope` 对 `OrchestratorHost` 的静态构造和 typed dependencies，消除 `createObj + box` 抹掉回调调用约定后触发的 `computation.then is not a function`；工作区快照保持 `WorkspaceSnapshotId`，取消与卸载直接调用真实 Host，不再动态查找模块或以默认成功掩盖缺失。

- 恢复规则书校验与 Context fact fold 的静态类型依赖，移除动态模块查找、手写 union tag 和校验缺失时的默认成功；将 Nudge、Enforcer repair、provider system transform 分成可由真实 consumer 独立编译的窄分片。
- authority gate 分离源码 subsystem 身份与 WHAT package 归属，退役 legacy 身份别名；结构测试改用唯一归属、合法增长、真实 shard DAG／subsystem SCC 和平台隔离的正反例，不再锁住迁移数量快照。
- 修复已有 execution parent binding、共享 parent cache 尚空时，真实子会话被误判为根会话并禁用 Fission 的问题。请求投影与父关系发现使用一致证据；真实 chat hook 回归同时保留根请求和 `/continue` 的 origin deny。

- 证明缺口与测试错误分离：缺少 active test/HOW proof edge 继续输出 GAP，保持 OPEN/PARTIAL，不再导致 check 或 meta-verifier 失败；已有证明的悬空引用、非法归属与真实断言失败仍严格报错。共享 proof graph 不伪造证明边。

- 删除独立 FCS 扫描器、compiler-dependent extractor/report CLI、扫描计时命令、专用 fixtures 与所有 compiler-evidence 消费分支；DSL、authority、decorator、owner-contract 检查恢复纯源码路径。187 项针对性回归通过；真实 check 越过原挂点并在证明追踪缺口处正常失败。STRUCTURED-WORKFLOW-013 的完整新合同证明继续保持 GAP-031 OPEN，不以删除代码代替证明。

- 修复 fork/resume 将正常本机派发回执误报为“不确定”：派发成功且 Submitted 已持久化即返回已承接，不等待 PhysicalAccepted 或 child completion；后续真实消息仍负责绑定 Authority Root，真正发送结果未知时保留恢复权且不重发。

- 全仓自建 FCS 扫描禁令（规范修正，取代下条旧 FCS 优化 rationale）：仓库自建 FCS（FSharp.Compiler.Service）扫描在全仓任何位置一律禁止——直接调用、wrapper/reflection/fsx 封装、typed AST / symbol / application / inferred type / source-edge 提取等任何形态，whole-tree、focused/locality、fixture、report-only、CLI、CI、prebuild、cache/snapshot/delta/reuse/externally supplied evidence 等任何执行入口，均不得作为验收证据、门禁手段或临时 report lane；Fable 内部正常编译与纯源码文本静态门禁不受影响，但不得为提取证据而加做额外/instrumented 编译。F# 执行依据为声明式 ProjectReference DAG 与精确编译闭包、sibling `.fsi` 与普通 Fable 签名/私有可见性编译 canary、已注册行为证明；C(W) 只含 direct byte-backed 的 explicit interop 与 JS/generated observations（见 VERIFICATION-SYSTEM-001；STRUCTURED-WORKFLOW-011 载有同规则并回指 001；`requirements/GAP.md` GAP-031 已同步改写为 PARTIAL 缺口记录）。下条“优化 check 的 FCS 路径”保留为历史记录，其优化思路已被本禁令取代，不得再作为批准依据；现存 FCS producer/consumer/test 及引用 FCS 证据的过期 schema、测试与文档断言尚未移除，属未解决的非合规缺口。

- 优化 check 的 FCS 路径：DSL 只提取完整 declaration/application evidence，不执行无消费者的 capability 类型递归分类；逐文件 checker 调用改为一次 implementation project check，反射属性元数据按类型复用。完整 report 保留原有分类覆盖，真实 compiler fixture 精确验证两条提取路径的证据等价。

- 执行故障链收敛为单一 `ExecutionFailureResolution`：删除可组合出非法状态的 retry/fallback/message 三轴，F# CE 每回合只返回一个互斥恢复或终结动作；provider fallback 以 exact `ProviderRunIdentity` + durable authorization 去重，managed-chat 不再成为第二 retry owner。Reconciler 的 `TurnFailed` 强制等待匹配的 typed physical witness，idle/retry 抢先到达不能裸终结；delegated completion 维持 first-proven-terminal 单次赋值。Long Strike 现连续注入两个非重试 provider failure，证明两次独立 durable recovery、第三 provider 成功、无并发 terminal exhaustion。
- Host failure presentation 明确边界：恢复期 Wanxiangshu 不额外产生 final presentation，耗尽后仅一个 typed terminal；OpenCode 1.18.29 的 post-publication plugin event 无法拦截上游原始 `session.error`，不再声称虚假 UI suppression。依赖同步至当前解析版本：`@fable-org/fable-library-js` 2.6.0、`@opencode-ai/plugin` / `opencode-ai` 1.18.29、`zod` 4.5.4；lockfile 与 Host compatibility fixture 同步。
- **修复 ProviderRetryAttempt 的 repair 抑制永久化（interaction-authority-023）**：`missing-final-report` / `interaction-repair` 对 `ProviderRetryAttempt` continuation 的抑制原先以 durable physical 身份为键（`AcceptedContinuationIds[PhysicalUserMessageId]`，全历史只增不删），一旦某条重试续提示成为该会话当前 physical，此后所有稳定但未满足 gate 的 terminal（`finish=length`、空/XML-only `stop`）都被永久压掉，idle nudge 一次不发——实机 manager 会话 00:13:01 因 1M 上下文窗口截断（`finish=length`、output=1 token、仅 reasoning）而静默停机，其后 7h17m 内 `Agent/Prompt/PluginPromptClaimed` 记录 0 条，直到人工输入“继续”。
  修复：抑制改为 attempt 维度——只有该 retry attempt 尚未终结（观测 `TurnUnknown`/`TurnInProgress` 且无 durable terminal）才抑制；稳定 terminal 观测或 exact durable `ChatExecution` Terminal 任一先到即解除，fresh `ProviderRunIdentity` 按 interaction-authority-019 重获 nudge 资格。新增纯判据 `CompletedTurnClassifier.retryContinuationSuppressesRepair` 与 exact 证据读口 `TurnObservationJournalPort.HasExecutionTerminal`，删除身份轴抑制 `isRecoveryContinue`。
  验证：`requirements/interaction-authority/tests/023.test.mjs` 新增 4 组回归（在途抑制、稳定 terminal 解除并换回 `RequestRepair`、durable terminal 独立解除、非 retry 不受影响）；interaction-authority + requirement-system 109、9 个受影响包 981、host-boundary/verification-system/managed-chat-execution/intra-participant-parallelism/prefix-stability 548 全绿。`verification-system-013` 的 2 项 semantic-debt/boundary 失败为 HEAD 既有（由已提交的 `context-compression/tests/028.test.mjs` 深度 dist 导入触发），与本提交无关。

- Manager baton/successor 模型按 clean cutover 退役，无别名与兼容路径：删除 `BatonSource`、`BatonId`、`ProjectionCutId`、`BatonEnvelope`、`ActiveSource` 与存储态 `OpenObligations`，删除 `SuccessorRequested` / `SuccessorActivated` 与 `Decision.activateSuccessor`；`Decision.openIncumbency` 不再接受 source 参数，初始开启唯一经真实权威接受后的 Plugin `BeginPhysicalProviderAttempt`，Change Host 不再伪造 `PhysicalUserMessageId`。
- 新循环语义：每一轮 Manager 都在共享工作区上从权威用户消息重新开始并独立评估，评审后指派的修复由本轮承担，完成后清理资源并退出；是否开启下一轮只由系统裁决 `RetirementOutcome = Continue | Accepted of QualityCertificateId`，`Accepted` 提供证书绑定的候选接受、发布成功则退出，若 Change 准入因快照/rebase/CAS 现实变化使证书失效则以另一轮普通独立迭代继续。`ProjectionCut = { ProviderRunId; ToolCallId }` 精确绑定已退休 provider run 与结束工具调用；`RetirementSummary = { Id; IncumbencyId; ProjectionCut; SnapshotId; AuthorityRevision; Outcome }`（其中 `SnapshotId: WorkspaceSnapshotId`）以工作区快照与权威版本绑定退休观察，拒绝陈旧 `Accepted`/`Continue` 重放。
- 上下文切段：新 active 迭代只保留类型化权威消息与本轮消息，移除所有前轮消息与仅用于唤醒循环的首个非权威用户延续，并跳过 XWire/Companion 历史投影；两种 retirement 都在 transform 边界清空退休 run 的后续请求并精确归还其 provider-step admission，`Continue` 随后自动激活下一轮，`Accepted` 只终止旧 attempt。发布成功即退出；若 Change 准入因快照/rebase/CAS 现实变化使证书失效，则以另一轮普通独立迭代继续。审计保留全量历史，证书、CAS 发布与退休资源围栏保持不变；推进提醒去重归 durable `PromptAuthority` 门控，`ExitRequiredNudgeScheduled` 重复 Relay 状态已删除。
- 配套改名：`RelaySuccessorGate` → `ManagerLoopGate`（kind 前缀 `manager-loop:`），`RequestSuccessor` → `ContinueLoop : ManagerJobId -> Task<Result<IncumbencyId,string>>`（去掉 reason 与冗余 WorktreePath），`runtime/relay-successor` 与 `runtime/relay-exit-required` 由 `runtime/manager-assess`、`runtime/manager-work`、`runtime/manager-finish` 取代，无 `runtime/manager-loop` 资源，循环唤醒与 `AuditPending` 提醒均复用规范 `runtime/manager-assess`；公开文档不再教授接力棒或合成交接，`requirements/GAP.md` 移除已过时的后继准入 GAP-033。

## 0.9.0

- JS capability-projected 编辑面升级为渐进式双层协议：
  - 新增默认 `edit(path, changes)`，用 `{ find, put, all? }` 覆盖精确替换、插入、删除、全匹配与同文件批量修改；所有 change 基于同一不可变快照规划并至多暂存一个 Rewrite，既有 `rewrite(path, newText)` 继续作为完整文件计算与结构重组的无上限逃生舱。
  - 新增 `INVALID_EDIT`、`EDIT_NOT_FOUND`、`EDIT_AMBIGUOUS`、`EDIT_OVERLAP` 稳定失败码；近似文本只生成有界、双语、copy-ready 诊断，绝不自动获得写权限。
  - 工具说明改为 action-first 决策阶梯与 replace / insert / delete / all 规范示例；说明、成员、示例与 runtime binding 均按实际 capability 裁剪，较弱模型不再被推荐调用不存在的方法。
  - 保持事务、ReadSet 冲突检测、CRLF、同路径单意图、跨文件全有或全无与 no-op 零写盘语义。

## 0.8.4

- Obligation & Magic Todo 强类型化与恢复去令牌化（OBL-002/004）：
  - MagicTodo checkpoint 生命周期及进度追踪实现全链路强类型化，彻底消除字符串弱类型推导。
  - 删除 `JobRecoveryAction` 控制令牌调度器；崩溃恢复流程从持久化 facts 重新进入普通 CE workflow。
  - 清理 AGENTS.md 历史义务账与旧控制流。

- Finality & Review Judgement 裁决去未决态与生命周期收口：
  - 审阅裁决（`judge`）提交流程引入强类型请求标识与去重；消除未决分支（undecided outcomes）。
  - Finality 工具支持数组提示词（array prompts），完善审阅者裁决差距（reviewer judgement gaps）。

- Degeneration Guard / Loop Detection 动态校准：
  - LoopDetector 常量解耦硬编码，转为基于构建产物动态校准分布参数。
  - LoopSensor 准确捕获并处理 reasoning 与 thinking 增量。

- Session 生命周期与 Abort / 级联中断模型精细化：
  - 会话级联中断与中止（cascading abort / InterruptAttempt）模型细化，引入 typed assistance outcomes。
  - 增强会话终止、Daemon 管理与父子会话发现（`bindManagedChild`）。
  - 内部中断后续生命周期收口。

- Host Boundary & 执行模型路由适配：
  - 修复连续 user message 之间自动插入 assistant dot message，符合 Host 对话契约。
  - 插件 Hook 增强柯里化函数与生成适配器的兼容性处理。
  - 强化物理执行绑定（execution binding）、披露类参数与会话 ID 抽取；显式 `/continue` 命令处理与抑制保持。

- Blogger / Chronicle 与借用容量管理：
  - 稳定 Blogger 飞行状态与路由默认值；重构 chronicle thought 注入。
  - 实现借用容量（borrowing capacity）与 credit source 路由管理。

- Requirement Grounding 规范接地系统：
  - 引入 APPLIES-TO 清单与规范接地上下文压缩、观测闭环。

## 0.8.3

- 依赖整备：bun-pty `^0.4.10`、gpt-tokenizer `^4.0.0`（`o200k_base` API 不变，滴定常量未漂移）、@opencode-ai/plugin `^1.18.18`、opencode-ai `1.18.18`、smol-toml `1.8.0`；删除零引用的 `eventsource`。Fable 5.13.0 / fable-library-js 2.5.1 已是最新。
- HOST-BOUNDARY-008 projection catch-up 事件驱动化：armed retry 的 bounded re-read 改由 session `message.updated` 信号唤醒（`MessageVisibilityHub`，ITimerPort deadline 仅作无信号 backstop）；消除 Fable 5.13.0 把 `Task.Delay` 编译为 fable-library-js 未导出的 `delay`、导致 dist 模块图不可加载的根因。authoritative suite 回到 0 fail。

- Managed agent 默认温度硬编码为 1.0：`chat.params` hook 在校验 observed provider 绑定的同时，对 managed agent provider request 输出投影 `temperature = 1.0`；非 managed 会话保持 untouched。

- Managed model routing 改为 `~/.config/opencode/wanxiangshu.mjs` 单一 authority：同步 `route(role, running)` 返回 `{ model, reasoning } | null`；`running` 是同一 OpenCode process 跨 root/worktree plugin instance 共享的 session×EffectiveAgent lease multiset，`null` 形成事件驱动 backpressure，不推进 provider AABB failure。
  - 文件缺失时以原子 create-if-absent 生成可编辑推荐模板；已有文件永不覆盖。模板只承载推荐七组策略，runtime 不拥有 lane / capacity / candidate 算法。
  - `opencode.json` managed agent `model` 不再参与路由，也不再要求 fast/deep 物理 model 不同；managed request 在 `chat.message` 被 lease model+variant 覆盖，`chat.params` 只验证真实 provider binding。
  - 缺 catalog 名由 `config` hook 投影到 live Host config，不再要求 `opencode.json` 手写 22 个 agent；旧名仍 fail-closed。
  - `fast-browser` / `deep-browser` 独立配置；Host `title` / `compaction` 不属于此 model-routing 合同。

- 机械检查瘦身（2026-08-15，用户要求）：删除 `kolmogorov-size` 行数 advisory（`scripts/checks/kolmogorov-size.mjs` + baseline + `kolmogorov-size-advisory.test.mjs`）与 `enforcer-cross-family-collision` A40 机械替代（gate + GD-010 条款 + 本体测试）。
  - 行数从此不做任何机械检查（VERIFICATION-SYSTEM-012 更新：非门禁且无 advisory）；检测语料可区分性归 review 判断（A40 人类 tournament）。
  - VERIFICATION-SYSTEM-012 机器载体 = `requirements/verification-system/tests/012.test.mjs`（结构性 absence：本包 tests 与 scripts/checks 无行数检查指纹）。
  - check.mjs wired gate 20→18；proof-ladder 下限同步下调；e2e/support 13 处 advisory 注释清理；verification-system 四文档与 guidance-delivery 三文档同步。

- 结构重排第一轮（平衡树式旋转，2026-08-14）：`Application/Reconciliation` 拆散归各语义 owner，
  `Journal` 掏空为持久化基板；此后 **namespace = dir** 为仓库规则。
  - `Composition/Turn/`（ReconciledTurn→Observation、TurnBinding→Binding、Reconciler→Scheduler、
    ReconcileSupervisor→Supervisor、TurnWorkflow→Workflow + TurnReconcile/ReconcilePass/OrdinaryTurnWorkflow）、
    `Composition/Durable/`（AgentProjection→Projection、ProjectionState、ProjectionUpdate、FoldRejection、Fold 路由）、
    `Composition/Bridges/FinalityReview/`（FinalityReviewCohort 接缝显式化）。
  - 各 bounded projection/fact-fold 归家：Context/{Trace,Prefix,Companion/Blogger}、
    Interaction/{Authority,Dispatch,Repair}、Feedback/Enforcer(+Guidance)、
    Execution/{Session,Delegation,Fission}、Mission/{Manager/Life,Obligation/Todo,Review,Review/Barrier,Review/Assurance}、
    Change/Orchestration、Participant/Provider/Attempt/Fallback、OpenCode/Contract。
  - `Persistence/Journal/` 仅剩 substrate：Envelope/Codec/Writer/Boot/AgentJournal/SharedJournal/RuntimePath/FactCodec/EventStoreJournalWriter。
  - `Kernel/Fact.fs` 外层 union 与 per-family facts 拆分（第三刀）留待下一轮（wire-compat 评估后）。
  - 移动文件 namespace 跟随目录；引用按编译器驱动补 opens；测试 dist import 与 requirements 文档路径同步更新；
    `dsl-ownership` host-boundary 白名单扩展（过渡项，第二轮后移除）。

- Requirement Package cutover 收尾：`docs/`、`changes/`、`tests/` 全部腾空。
  - 45 包 normative 树 `requirements/<package>/{WHY,WHAT,HOW,PROOF}.md` 为唯一语义权威；
    旧 Clause 与变更记录已归档（2026-08-14 cutover；git 历史可回溯）。
  - 测试全部分包：`tests/unit` 146 文件 MOVE/SPLIT/DELETE 归各包 `tests/`；
    `tests/eval` → `office-capability`；`tests/integration` suites 归 owner 包；
    e2e Long Stroke、support harness、unit/integration runner 归 `verification-system/tests/`。
  - `package.json` release ladder 与新路径对齐；meta-verifier 骨架源迁入 `requirements/INDEX.md`。
  - 迁移 ratchet 退休：`g4r-freeze`、`student-teacher-absence`、`enforcer-rulebook-gate`（retired stub）。

## 0.8.2

- Provider Surface Grand Repair：ARCH-017 Office Capability；PROMPT-020 Tool Affordance；PROMPT-021 Critical Semantic Redundancy；ARCH-016 Gate F。Role Law 教身份，Tool Law 教动作，Delegation Law 教他人能成为什么。
- HOST-013 ordinary renderer：OpenCode Host 不再写 pending FakeReq。每个 occurrence 在 ResultGap 渲染一条 completed `auto-injected` tool part，由 `toModelMessagesEffect` 展开为 provider tool-call + tool-result，消除伪中断文案。
- 持久化、Git 与 session 工作流统一采用异步 Task 调用链，减少 Node 事件循环中的同步等待；GitGateway、EventStore、AgentJournal 与 blob 路径完成贯通。
- SyncDelegate 语义批处理、WorkRecord/Lifecycle 物化、HostFork/Join/Recovery/Enforcer/Finality/Manager/Review 的 durable 顺序进一步收口。
- 发布 Fork `attach`、Horizon 最新子 Agent 工作摘要，以及 Magic Todo / dedicated reviewer 的 obligation 与 assignment 改进；包入口与 durable store schema version 不变。

## 0.8.1

- REVIEW-003 skeptical challenge 迁入 `resources/provider/review/challenge`；tool result / nudge / seal 跟 Reviewer session `ProviderLanguage`；英文 canonical 字节不变（`ChallengeTextVersion = 1`）。
- journal / 公开 wire 合同相对 0.8.0：无 domain protocol 破坏。

## 0.8.0

- Provider-visible prose ownership（PROMPT-019 / ARCH-016 Gate E）：进入 participant horizon 的 Class A 自然语言经 `ProviderResources` 装载、由 session `ProviderLanguage` 管辖。Gate E baseline `{}`。
- Gate C 现行面补齐：叶对 + `{{placeholder}}` 集合一致 + Role Law semantic-anchor 同 ID 双语命中（`scripts/checks/semantic-anchors.mjs`）。
- HOST-013 pair guideline 迁入 `resources/provider/host/pair-programming-guideline`；生产路径 `ProviderProse.render`，禁止 `match lang` 挑选正文。
- Role Law 是身份文本，不列工具名。REVIEW-003 challenge 仍为固定英文协议句。
- journal / 公开 wire 合同相对 0.7.0：无 domain protocol 破坏。

## 0.7.0

- Kolmogorov 所有权二级拆分（语义汇流点，非按行数切文件）：
  - LWR journal 物化 → `LifecycleWorkRecordProjection`；`XTraceCapture` 只保留 semantic capture。
  - Manager durable open / migrate / activate → `ManagerLifeWorkflow`；`ManagerNarrativeTransform` 只保留 wire 门控与 provider rewrite。
  - `PluginTransforms` 只保留 hook 顺序；Strength replay/traced → `StrengthReplay`。
  - `HostSignalBootstrap` 退回订阅/路由；政策 → `HostTurnObserver` / `HostCompactionObserver` / `HostSessionDeletion`。
  - `Reconciler.Scheduler`（coalesce/drain）与 `ReconcilePass`（causal reread/publish）分居；`ReconcileProgram` 不变。
- Wave 0–5 Kolmogorov 重构收口：`kolmogorov-size` ratchet；JsTools / ProjectionAlgebra / PluginRuntimeScope / Fold / EnforcerHost / SpikePlugin / Codec Projection / HostForkJoin / SyncDelegate 等按 owner 装箱（详见 `changes/completed/refactor.md`）。
- Strength 提案闭环；EnforcerContinuation 从 EnforcerHost 二次抽出。
- G6 Casebook / G9 Session ownership ratchet Product Exit（问卷八 kind + 接线 gate）。
- JS capability-projected tools：structured TOML result；grep/glob 能力面收紧。
- AGENT-026/027：Stealth Browser MCP Host 接线 + 内部 Semble MCP；共享 `McpLaunch` 词汇（Disabled / Fixture / Uvx），消除 dup-cases。
- 持久化写入延迟：EventStore 的 Git raw store 不再为每个对象 spawn 一次 `git`。新 `GitObjectDatabase` 直接读写 loose object（`sha1` + `zlib` + `objects/xx/yyyy`，tmp+rename），并对内容寻址的对象/tree 读取与 `mktree` 结果做实例级 memo。单事件 append 由 **24 次同步 git 子进程 / ~60ms** 降到 **2 次 / ~7.5ms**；由于 `execFileSync` 会阻塞 Node 事件循环，这段成本此前会让同一 Host 内所有 session 串行等待。oid、on-disk 布局与 `git cat-file` 可读性完全不变（`tests/integration/persist/object-identity.test.mjs` 对真实 git 二进制逐项比对）；`gc` 之后的 packed 对象仍回落 git CLI 读取。
- FALLBACK-013：Host abort/cleanup 残留（在途工具被标 `status=error` + `metadata.interrupted=true`）不再推进 A/A/B/B cursor、不消耗自动恢复预算。此前 owner 的一次 provider 失败会被记两次——一次来自它自己的失败路径，一次来自被同一次 abort 清理打断的 Companion cycle（且用 Blogger 的 `ProviderRunIdentity`，FALLBACK-003 去重无法折叠）——导致 provider 可见的 A/A/B/B 顺序取决于两次 append 的竞争，恢复可能落回刚失败的同一侧。Companion 侧仍注入一次 `# Protocol repair`，有界性由 ENFORCER-153 marker 保证；`ToolExecutionError`（无 `interrupted`）仍按 ENFORCER-065/068 推进 cursor。
- journal / 公开 wire 合同相对 0.6.0：无 domain protocol 破坏；控制流与 Host 边界所有权收紧。

## 0.6.0

- Causal CE / 时序所有权：可观察因果等待、Wait Graph、waitFact 续期归因；Reconciler 去业务轮询；Join interrupt / user-wake 收口；Diagnostic Bridge。
- Manager Finality / lifecycle：`FinalityTool`、terminal frontier、sibling steering / durable revision；PERFECT 后的收口与 rest-in-peace 路径。
- HOST-013：guideline pair 永久 append-only；prefix-cache 不变量；idle-derived continuation 资格门控（SessionQuiescenceGate）。
- Student–Teacher CE collapse：Teacher 侧单一 CE await 链；durable evidence；相关单元/回归收口。
- Projection Algebra / Glory：attempt-local PrefixProbe 与 plain-X 前缀投影迁入投影 DSL；idle / revise / MISSING_FINAL_REPORT 观察路径加固。
- Coder 工具面：`bash-honeypot` 禁未授权 shell；严禁 Coder 跑测试；PTY prompt 补齐换行。
- EXEC-028：同步 one-shot `inspector`/`coder` 返回统一为 entry-local LWR 注释（`includeOpening=false`）+ 末条 TurnFormalText，禁字段式 `work_record`；与 Join 共用 COMPANION-003 物化器。Opening 在 send 前从原始 assignment 捕获以便物化；`Completed` 无法物化非空 LWR 时 fail-closed 返回工具级 `error=`，不 soft-omit。
- LWR 段标题在 materialize 中为纯文本（`Opening task` / `Work log` / …）；`# ` 仅由 `SyntheticToml.comment` 在 wire 注入，消除 join/oneshot/finality 上的 `# # Work log`。
- Enforcer / Blogger-as-Enforcer rebase 文档收口：`how`/`shape`/`proof` 对齐 tip-v2 基线（PartOrdinal-first 多调用 tip、物理所有权轴、`§13` 证明清单）。`bounds.test.mjs` 永久回归锁定归并 size/count 越界 fail-closed（>32 calls / text >512 KiB / evidence >128 KiB）；未恢复 wire/runtime score 路径。
- 文档治理：变更单文件生命周期 `changes/{proposed,active,completed}`；条款 ID 唯一归属正式层；`PENDING.md` 收口为 COMPLETED/HISTORICAL；`AGENTS.md` 修正 architecture 文件数与 `gate:dsl-ownership --threshold=0`。
- Canary unbend：纠正迎合错误生产的声明扭曲；e2e 事件驱动等待取代固定 poll slice。
- journal / 公开 wire 合同相对 0.5.4 兼容方向：控制流、投影与 Host 不变量收紧；破坏性细节见上列条目与 `docs/`。

## 0.5.4

- AGENT-019：managed agent Host-final permission 固定 `external_directory = allow`，覆盖 Host 默认 ask，取消项目外路径的交互确认。
- DSL 全面主导化（ARCH-001 / FLOW）：门禁债 `157 → 0`。
  - 删业务 Program AST / Interpreter；Child/Session Recovery、Orchestrator/Reconcile/Join 直接 CE。
  - `Kernel/Flow` → `Kernel/Parallel`（仅 `mapBounded`）；`CycleDisposition`、`DrainWindow`、`BloggerRuntimeHost`。
  - `dsl-ownership` 契约：合法 mutable（Domain/Session/Application/Parallel）；Host 边界 `open` basename 白名单；`--threshold=0`。
- e2e 稳定性：`gitConflictProof` 挂 worktree 已存在之后；`ProcessHost.stop` 在 leak assert 前回收残留 listen 端口。
- AGENTS.md 收束为现行纪律（P0–P3 施工表退役）；`TASK.md` 作历史档案。
- 无 journal / wire 协议破坏；控制流与门禁契约收紧，产品对外协议与 0.5.3 兼容。

## 0.5.3

- No runtime protocol changes.
- Normalized source, resource, specification, test, and build layouts.
- Replaced the generated Enforcer catalog with packaged runtime data.
- Packaging now uses the repository root and includes resources directly.
- Removed migration evidence, generated conformance ledgers, and legacy gates.
- Renamed internal files and test directories without changing public behavior.

## 0.5.2 — 全 SSOT 收敛

- 收敛目标：Active 规范全部收敛。
- 规范：spec/14 Strength、spec/16 Student&Teacher、ENFORCER nudge/throttle/规则目录迁出到 `RFC/`，spec/15 仅保留 0.5.1 已交付的 Blogger 工具化子集。
- 版本：全仓文案从 `0.5.0-rc.1` / `0.5.1` 统一到 `0.5.2`。

## 0.5.1 — Blogger vertical-slice convergence (spec/15)

生产闭环 Blogger 请求形状 / 挂起 / Squash / 恢复载体（不做 Enforcer throttle、nudge、Strength、Student&Teacher）。

### Runtime authority
- 生产 `BloggerRuntimeCell`（Idle / InFlight / Parked / Disposed）
- `CurrentRequest` 与 `PendingOffer` 双槽；唯一 busy 定义 = InFlight
- 唯一入口 `BloggerCoordinator.onMainMaterial`；删除 `offerToBlogger` 旁路与 `inFlightTask` busy 权威

### Projection & commit
- 发送前冻结 typed context 并落盘 `BloggerRequestMaterialized`
- 首次 / resume / Squash 共用 `CompanionProjectionBuilder`；删除 raw TOML 抽取与 `BloggerNeedsReset`
- Squash 迁入 blog tool continuation，提交 `BlogSquashCommitted`（coverage 不变）
- 仅 `KnownCommitted` 后 Park；`KnownNotCommitted` / `CommitUnknown` 不 Park、不重问
- 统一 `BloggerCycleReceipt`（Entry|Squash）按 ProviderRun 幂等
- 一次 `RepairSpent` repair；资源上限；Main Entry 成功清 fallback

### Recovery & teardown
- crash-window recovery 挂 `EnsureRecoveryDone`；live CurrentRequest 不 stomp
- fail-closed `loadEffectiveFrames`；`CompanionIdentity.newWorkMessageId`
- Host 重建消息带 synthetic/source 标记；main dispose 清 linked Blogger waiter

### Evidence
- layer-4：`host-transform-capability-canary`（park/resume、第三 turn 单飞、materialize）
- layer-4：`companion-canary`（同 child 两轮 blog tool）
- 静态 `blogger-convergence` 防回退门禁
- 条款收敛：`COMPANION-005/008`、`CTX-006/007/012`、`ENFORCER-010`

## 0.5.0 — 正式版

- 正式发布：0.5.0（从 rc.1 收口；breaking changes 见 `0.5.0-rc.1` 条目）
- 生产可用：canary 森林 17 驱动（18 剧本）× 3 轮全绿，`test:release`（gate:static →
  build → unit → harness → P0×3）完整通过
- Review 双 PERFECT 见证（REVIEW-006/007）
- Orchestrator 恢复链（ORCH-005/006/007）：restart 后 exactly-once publish、rebase
  冲突恢复
- guard nudge seal 稳定性修复（ORCH-006/ARCH-004）：session worktree 目录绑定
- 来源解析顺序（PROMPT-004/009）、发送格式（PROMPT-006）、fire-and-forget（PROMPT-007）
- 工具权限双层 fail-closed（AGENT-007）
- 未验证条款清零（8 条批量段条款补第 1 层判据）

## 0.5.0-rc.1 — docs freeze / RC development

Breaking changes:
- All agents now require explicit `fast-*` or `deep-*` names
- Unprefixed agent names, `build`, `plan` aliases removed
- Agent-to-model bindings read exclusively from `opencode.json`
- All Wanxiangshu model environment variables removed
- No longer persists or overrides model IDs
- Provider fallback cycles A/A/B/B within budget（Cursor 无限定义；自动恢复上限默认 12 连续失败）
- Provider retry count no longer kills a Logical Run
- Blogger and Executor Agent are now internal fast/deep pairs
- Pre-0.5.0 runtime journals not supported

## 0.4.0 — 最终版

- Structured Agent Program (Flow CE, no Stage/Phase/Lease platform)
- Prompt Authority / Logical Run rules
- Companion + ActivePrefixEpoch / FrozenB cache protection
- Manager `fork-agent / join / list`; Orchestrator `fork-manager / join`
- Static role matrix with full system prompts
- Logical-Run Fallback A/A/B/B with durable retry writer
- Dual PERFECT Review with ProviderRunIdentity binding
- Process/Executor: 3× estimate, large gate, 200KB ripple-carry
- PTY via DevOps `fork-pty` only; onExit-only completion; structured signals
- Orchestrator: clean gate, worktree, serial publish lock, rebase, re-review, ff-only
- OpenCode adapter: idle/retry/deleted signal + single-flight reconcile
- Private distribution: `private: true`, provisional commercial LICENSE
