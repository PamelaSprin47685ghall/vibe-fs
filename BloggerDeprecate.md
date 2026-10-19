# Blogger / Chronicle 废弃方案（clean-break）

> 状态：**已执行**。本文档是 2026-10-19 依赖面调查的历史快照，保留当时的检索结论与施工路径，作为本次 clean-break 的施工依据；现行规范以 `AGENTS.md` 的同名章节与 `requirements/` 各包为准。调查时的结论「blogger 与 chronicle 未被废弃」描述的是施工前的仓库状态，现已不成立：`blogger`、`chronicle`、`Y 轴`、`WorkRecord`、`Strength`/`Replica`/`Predictor` 均已从源码、需求树与资源中移除。

## 一、调查时点的现状结论（施工前，已失效）

blogger 与 chronicle 在调查时点**未被废弃**，是活跃系统组件：

- 48 个内部定义文件在 `compile-order.txt` 中参与每次构建；
- `ToolRegistry.fs` 第 355 行活跃注册 `"chronicle", ChronicleTool.admission bloggerHost`；
- `OfficeCapability.fs` 第 137 行定义 `Role.Blogger -> set [ ToolPermission.Chronicle ]`（ENFORCER-010：工具集恰为 { chronicle }）；
- 约 180 个外部消费文件横跨 Foundation、Participant、Context、Composition、Enforcer、Execution、Interaction、OpenCode、Persistence、Resources 十层；
- 40 份 requirement 文档以 Blogger/chronicle 为规范对象或角色集合成员；209 个测试文件共 1347 处引用；
- wanxiangli submodule 的 `src/identity/Roles.fs` 定义 `Role.Blogger` DU 分支，`PersonaCatalog.fs` 定义 Chronicler persona。

## 二、依赖面地图

### 内部定义（48 文件——整树删除）

- `src/Wanxiangshu/Context/Companion/Blogger/`：Toml、TomlSurface、ItemWire、Delta、DeltaSurface、Request、Projection、FrameSurface、ContextFactFold、MainContext、OpenCode/PluginScope、Runtime/（CycleProjection、CycleSurface、ParkedTransform、Host、Abandon、Coordinator）
- `Context/Companion/HostBlogger`
- `Enforcer/BlogSurface`、`Enforcer/ChronicleExecution`、`Enforcer/Cycle/BloggerProbe`
- `OpenCode/Host/BloggerChronicleSurface`、`OpenCode/Host/BloggerChronicleText`（COGNITIVE-ENVIRONMENT-015）
- `OpenCode/Tools/ChronicleTool`

### 外部消费（约 180 文件——逐个剥离，按子系统）

| 子系统 | 文件（代表） | 牵连内容 |
|---|---|---|
| Foundation | Roles、OfficeCapability、Identity、Outcome、RolesSurface、SyntheticToml | 角色集合、权限矩阵、blogger input 的 TOML 渲染词汇 |
| Participant | Persona/Catalog、ManagedCatalog、Provider/Attempt/*（RequestKind、RetryPolicy、PlannerSurface、Fallback）、Provider/Projection/Model | BloggerMain RequestKind、BloggerRetryPolicy |
| Context/Companion | Builder、Facts、Fold、Host、Identity、Model、Projection、Prompt、Runtime、Transform、CompanionFactFold、CompressionSurface 等 | 整个 Companion 层围绕 Blogger 构建 |
| Context/Prefix | Wire、WirePort、Surface、XWireSurface | FrozenRecordPrefix = Opening + Y frames（COMPANION-009/CTX-011）；MemoryPreamble 在 Wire 三处渲染 |
| Context/Trace | Capture、Materialization、Model、Projection | XTrace/YTrace 分界 |
| Composition/Durable | CompanionFact、Fold、Projection、AgentJournalPortAdapter、DomainFamilyBridge | CompanionBloggerLinked 事实、BloggerCycles 投影状态、mainSealedForBlogger |
| Composition/Turn | OrdinaryTurnWorkflow、TurnObservationPort、Workflow | BloggerCoordinator 消费链 |
| Enforcer | Cycle/（Model、Decode、Commit、Recovery）、Continuation、Codec、Catalog、Guidance/Tip*、Observation*、Projection、Repair、Surface | chronicle 调用的验证协议、tip 提交 |
| Execution | Delegation/Fork/Host/ 7 文件、Handle、LinkageProjection、SyncDelegate、Session/（Association、Attachment、ChatExecution、Recovery、RecoveryClosureProjection、HorizonTool） | 派发与恢复路径 |
| Interaction | Authority/、Dispatch/、Repair/ | 角色集合准入、SendManagedAssignment 共用路径 |
| Mission/WorkRecord | Materialize、Model、Surface | LWR(X) = Opening + **Chronicle（Y frames）** + Recent work 三段结构 |
| OpenCode/Host | 约 25 文件（ProviderLifecycle、PluginHost*、PluginSessionScope、PluginRuntimeScope、SessionRecovery*、SharedState 等） | Plugin 体系与 Blogger 生命周期绑定 |
| OpenCode/Plugin | PluginHooks、PluginRecoveryWiring、PluginSessionWiring、PluginTransforms、SpikePlugin | 插件接线 |
| OpenCode/Tools | StaticTools、ToolRegistry、ToolRegistrySurface、ToolSurface | 工具注册与表面 |
| Persistence/Journal | CodecSurface、Envelope、EventStoreJournalWriter、Surface | **durable 事件类型本身**（BloggerRequestMaterialized、BlogObservationCommitted、CompanionBloggerLinked） |
| Resources | EnforcerCatalogResource、PromptCatalog、PromptResources、PromptSurface、RuntimeResourceAssembly | 资源装配 |
| Repository/Programming/Js | Capability、GeneratorSurface | todowrite-for-blogger 工具生成 |
| 其余 | 12 个 owner .fsproj、compile-order.txt、Strength、Verification 若干 | 构建清单 |

### submodule wanxiangli

- `src/identity/Roles.fs`：`Role.Blogger` DU 分支（L16）、allRoles 列表（L25）、canonicalName（L44）、parse（L53）
- `src/identity/PersonaCatalog.fs`：Chronicler persona
- `src/identity/index.d.ts`、`src/identity/AGENTS.md`、`src/identity/tests/001.test.mjs`、`src/compression/AGENTS.md`

### resources / scripts

- 删除：`resources/provider/role/blogger/`、`resources/provider/tool/chronicle/`（arg-evidence、description、nothing-to-remember、remembered）、`resources/provider/cognitive-environment/blogger-chronicle-text/`、`resources/provider/lifecycle/companion/`（normal、memory-preamble）
- 改写：`resources/provider/role/advisor/`（en.md L25「view 与 Chronicle 同形」、zh-CN.md L15 同义表述）、`resources/provider/world/common-law/`、`resources/ablation/tool-map.json`、`resources/wanxiangshu.mjs`（L9 公开角色表、L118 BLOGGER_POOL、L138）、`scripts/build.mjs`（L188）、`scripts/checks/js-surface-gate.mjs`（L45 todowrite-for-blogger）、`scripts/checks/provider-leak-gate.mjs`（L22 ChronicleTool 白名单、L66 FAST_DEEP_BINDING_RE）、`scripts/verify-package.mjs`（L787）、`scripts/checks/owner-impact-corpus.json`、`scripts/diagnostics/` 若干

### 规范面（40 份文档；测试 209 文件 1347 处）

以 Blogger/chronicle 为直接规范对象的条款：

- capability-enforcement [006]（内部专用工具）、[010]、[021]（Blogger repair 单一 owner 状态机）
- cognitive-environment [015]（Blogger 临时记账提示）
- context-compression：Y/BloggerMain/BloggerDeltaLimitBytes/BloggerRetryPolicy 贯穿全文
- crash-reconciliation [016]（live owner）、[084]（外部 effect 登记含 Blogger receipt）、[020]
- provider-attempt-recovery [010]（Blogger maintenance retry）、[011]、[019]/[021]
- work-record [003]（Chronicle 与 Recent work 按 coverage 分界）
- guidance-delivery（tip 交付链、Blogger system prompt 样板）
- behavior-diagnosis（检测与处置语料进 Blogger system prompt、tip 提交）
- effect-accounting [033]（BloggerRequestMaterialized/BlogObservationCommitted）

角色集合引用（收缩改写）：execution-model-routing [122]、interaction-authority [095]、intra-participant-parallelism [071]、participant-identity [005]/[041]、participant-horizon [035]/[051]、dispatch-protocol [048]、managed-session-lifecycle [123]/[134]/[140]、speculative-investigation [009]/[059]/[173]、work [296]、epistemic-reasoning [144]、prefix-stability [064]。

测试大头：cognitive-environment/015（146 处）、context-compression/018+024（135 处）、behavior-diagnosis/017+009、capability-enforcement/021+006+010+011、managed-chat-execution/015、provider-attempt-recovery/024、prefix-stability、verification-system（blogger-ownership.mjs、long-stroke oracles）等。

## 三、clean-break 分阶段方案

### 阶段 0 —— 三个裁决点（先于一切改动，归 Manager）

**裁决 1：与 advisor 的正面冲突。** `requirements/advisor/WHAT.md` [014] 明确规定 advisor 的 view 按 blogger input 格式渲染（`BloggerToml`/`SyntheticToml` 词汇、`[[new_work_to_record]]` 表），[036] 规定专席归属仿 `CompanionBloggerLinked`、[037] 规定单飞 ensure 仿 `CompanionHost.EnsureBloggerAsync`；`resources/provider/role/advisor/` 双语提示词已写明「view 与 Chronicle 同形」。可解耦路径：`SyntheticToml` 位于 Foundation 层，本质是通用 TOML 渲染词汇，可保留（必要时改名）作为 advisor 的 view 格式；advisor 仿写的是机制模式而非代码复用。但 advisor/WHAT 与 role 资源的措辞必须改写去锚。**要么裁决 advisor 改锚，要么废弃暂缓至 advisor 落地。**

**裁决 2：Y 轴职责真空。** context-compression WHY 写明「Blogger 是记录者，不是主工作者的额外负担。持续追平最新事实、共享一个重建规则，避免漏记和恢复时的材料分叉」。废弃后：Main session 失去持续记忆层；WorkRecord 退化为 Opening + Recent work 两段；FrozenRecordPrefix 失去 Y 覆盖证明；guidance tip 无产出者；行为诊断语料无提交入口。这是架构级变更，需明确「接受退化」还是「有替代设计」，不得默认无替代。

**裁决 3：durable 历史清空。** 旧 journal 中的 Blogger 事件（BloggerRequestMaterialized、BlogObservationCommitted、CompanionBloggerLinked 等）在新 codec 下是未知事件，EventStore fail-closed 触发 StorageInvalid 全局停摆。clean-break 唯一出路是仿 `.git/wanxiang*` 退役先例：本机 `.Herdchestra/` 运行状态整体退役，不读取、不迁移、不作为恢复候选。需确认可接受（当前不需要跨机持久化，AGENTS.md 已有此声明）。

### 阶段 1 —— 规范先行

逐包改写 WHAT/WHY：删除 Blogger/chronicle 专属条款，收缩角色集合条款，advisor/WHAT 改锚。先规范后代码，避免「代码写死再返工」的典型失败。

### 阶段 2 —— submodule（wanxiangli）

`src/identity/Roles.fs` 删 `Role.Blogger` 分支及 allRoles/canonicalName/parse 三处；PersonaCatalog 删 Chronicler；index.d.ts、AGENTS.md、tests/001 同步。submodule 提交后宿主记录确切 gitlink。

### 阶段 3 —— 宿主源码（自内向外，手工编辑）

删除顺序：Enforcer/Cycle 协议 → Companion 层 → Prefix/Trace 的 Y 轴 → Composition/Durable → Execution/Interaction 消费点 → Foundation 角色与权限 → Tools 注册 → Persistence 事件类型 → compile-order.txt 与 12 个 owner fsproj。48 文件删除 + 约 180 文件剥离。严禁自动化批量改写（AGENTS.md）。

### 阶段 4 —— resources / scripts

删 4 个资源目录；改写 advisor、common-law 文案；tool-map.json、wanxiangshu.mjs、build.mjs、三个 gate 脚本同步。

### 阶段 5 —— 测试

Blogger 专属套件整条删除（cognitive-environment/015、capability-enforcement/021、context-compression Y 系列、provider-attempt-recovery/010 等）；角色集合类测试改写；`run-chronicle-provider-canary.mjs` 等支持脚本删除。

### 阶段 6 —— 验证收束

1. `node scripts/build.mjs` 全绿（严禁 dotnet build）；
2. 受影响 requirement 套件重跑（不全量漫跑）；
3. 清空 `.Herdchestra/` 本机运行状态（裁决 3 通过后）；
4. `git status` 无残留临时文件；
5. 自动提交 master，submodule gitlink 一并记录。

## 四、量级评估

约 230 个源文件 + 40 份规范文档 + 209 个测试文件 + 1 个 submodule。这是多次提交、多个工作包的架构手术，无法单次原子闭环。三个裁决点中：**advisor 冲突最紧迫**（决定废弃是现在做还是等 advisor 落地）；**Y 轴职责真空最根本**（决定这是纯删除还是重设计）；**durable 清空影响面最小**但需明确授权。

## 五、执行边界

- 本文档仅为方案。执行前必须先获得三个裁决点的明确结论。
- 废弃期间 advisor 开发若并行，需 Manager 划出不重叠的验证窗口（advisor 的 view 渲染锚点与 SyntheticToml 去留直接相关）。
