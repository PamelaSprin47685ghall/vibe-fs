[Apply ASD-STE100 to source code, Mise-en-place to code design. Adapt Gall’s Law, Chesterton’s Fence, and Poka-yoke. Invoke Goodhart’s Law and Via Negativa when needed. Treat these principles as Wittgenstein’s Ladder. Build the real thing, not its imitation. Try to falsify it.] [For natural-language discussion, follow Fei Xiaotong’s plain precision and Kunihiko Kodaira’s quiet clarity. Avoid officialese, performative rhetoric, translationese, and textbook cadence. Let the subject shape the prose.]

## 工具调用纪律

- 只要动作彼此独立，就尽量并行。
- 精准修改局部代码，拒绝无谓的全量重写。

## 架构与编码风格

### 源码与运行数据分离

- `wanxiangli/` 是正式 Git submodule，跟踪 `main`。依赖升级以远端最新 `main` 为目标，只 fast-forward、不覆盖本地工作；提交宿主时记录确切 gitlink。开发更新与运行程序分开，运行时不得自行拉取或切换源码。
- npm 通过 `file:wanxiangli` 消费公开入口，构建使用该源码版本。任何 `.tgz` 二进制包不得加入 Git，不因体积小、方便本地安装或发行验收而破例。临时产包只允许写到忽略或临时目录。
- 全部项目运行数据统一放在主工作树的 `.Herdchestra/`，linked worktree 共用这份本机状态。Git 仅用于源码管理及只读拓扑查询；应用不得把日志、SQLite、快照、诊断、锁或 SSH 包装写进 `.git`，也不得安装数据同步 hooks/refspec。
- 旧 `.git/wanxiang*` 数据与运行状态 refs 已退役，不读取、不迁移、不作为恢复候选。当前不需要跨机持久化。清理只针对已确认的应用专属目录、配置和 refs，不清空 Git 的 objects、代码分支、标签或其他人的 hooks；历史改写和物理 GC 不是默认清理动作。

对应合同：`requirements/durable-events/WHAT.md` 的本机状态切换条款。禁止以删验收或伪造构建回执掩盖源码依赖尚未准备好。

### 长程委托的连续性

- 公理：使用者委托的是目标的完成，不是一条回复的结束。目标尚未完成时，长程工作不能静默停止；有下一步就继续，遇到阻塞就明确呈现，不能靠用户再次催促维持推进。
- 推论：provider 的自然 stop、空输出和进度小结都是物理回合的边界，不是业务完成证据。Manager 与 Plan 必须通过既有 `deliver` 形成有效交接或完成；否则自然停机应触发当前任务的 nudge，并说明尚未解除的义务。
- 显式等待：Plan 真正缺少用户决定或信息时使用 `ask(question)`（Manager 禁用 `ask`）。工具只返回询问已发出的占位回执，系统中断本次 attempt、保留未完成委托；新的真实用户输入到来后接续。`devops(intent)` 负责原 Plan 的固定 DevOps 调查，`join` 负责 Manager 临时等待后台工作，二者都不证明任务结束。
- 边界：禁止模型自行放弃，不等于禁止用户取消、强迫重试基础设施故障或复活已退休者。用户明确取消、已证明的交接/完成、显式提问等待和可见的运行故障分别处理；不能把失败或未知伪造成成功，也不能把 `ask` 的暂停当作逻辑运行关闭。

对应合同：`requirements/work/WHAT.md` [027]、[028]、[049]。测试必须覆盖自然停机、提问与真实回复、重复及迟到回执；仅改提示词不能替代终态准入规则。

- 崇尚朴素、简洁、直白的架构，去除不必要的过度设计，拒绝复杂的错误包装、日志堆砌和配置层级。
- 除非绝对必要，不写多余注释。不搞糊弄过关的临时补丁，想清楚再动手。
- 避免冗余的中间赋值，边界清晰即可，不引起阅读负担。
- 严禁通过一行写多件事或滥用分号来伪造行数减少。
- 变量命名务求明白晓畅，绝不用晦涩名字和让人费解的缩写。
- 重构时痛快甩掉历史包袱，不滥用外观模式（facade）逃避清理；不保留已废止的代码，不合理之处随时与下游协同修改。
- 精准实现，不搞看似保险其实谁也说不清原理的兜底实现。

## 具体工作与流程

- 严禁使用 `dotnet build`。本仓只有 Fable 编译目标，构建必须使用 `node scripts/build.mjs` 或 `npm run format-build-test`，严禁引入或依赖 .NET 编译构建。
- 宁慢且稳，严禁使用自动化程序批量增删改查程序代码。手工编辑，步步为营。慢即是快。

1. 工作流程
  - 普通小型修复、重构与测试补充不要求创建 Change；在单次提交内原子闭环。
  - 基本闭环：弄清为什么做（why）→ 明确要做成什么样（what）→ 阅读相关规范理解依据 → 调整与补充测试 → 落实实现（how）→ 检查所有相关测试全绿 → 闭环结束。
  - 按施工计划认领的工作，每个工作包完成后同步更新计划状态、验收证据与剩余边界；不能只在聊天中报告完成，也不能把局部证明写成整个 TODO 或 GAP 已闭合。
2. 两种典型失败
  - 写完代码才想起看文档：代码已经按旧想法写死，要么彻底返工，要么硬把旧语义塞进系统，导致代码与规范越走越远。
  - 陷在局部细节里丢了大局：虽然修好了表面报错，却违背了整体规则（例如只给旧类型打补丁加字段、加适配器凑合，合起来就是在维护混乱）。
3. 交付门禁
  - 涉及行为、持久化、Host/provider、Git 或跨包边界时，跑对应 requirement 测试套件，尽量不全量漫跑。
  - 测试文件按 WHAT 条款编号统一命名为 `NNN.test.mjs`；需求与测试的映射由测试用例标题中的 `WHAT[PREFIX-NNN]` 锚点定义。
  - 提交前确认没有残留的临时文件、调试打印、旧路径残骸或未跟踪的生成物。
4. 修改纪律
  - 工作区可能包含用户的未提交改动。动手前先看 `git status` 与对应 diff，妥善保留无关改动。
  - 自动 git commit 并推送到 `master`；严禁对 `master` 执行 force push。提交成功、验证通过、已部署运行分别报告，不相互代替。



# Blogger / Chronicle / WorkRecord / Replica 废弃方案（clean-break）

> 状态：**已裁决，待施工**。本文档由 2026-10-19 的依赖面调查与所有者终局裁决产出，记录彻底废弃 blogger、chronicle、Y 轴、WorkRecord 与 Replica（StrengthReplica / speculative-investigation）的完整施工路径。所有数据来自源码检索与 `compile-order.txt` 核查，工作树含未提交的 advisor 开发改动（`src/Wanxiangshu/Execution/Delegation/Advisor/`、`resources/provider/lifecycle/advisor/`）。

## 一、现状结论

1. **Blogger / Chronicle（Y 轴）**：48 个内部定义文件在 `compile-order.txt` 中参与每次构建；`ToolRegistry.fs` 注册 `"chronicle", ChronicleTool.admission bloggerHost`；`OfficeCapability.fs` 定义 `Role.Blogger -> set [ ToolPermission.Chronicle ]`；约 180 个外部消费文件横跨十层；40 份 requirement 文档及 209 个测试文件（1347 处引用）涉及；`wanxiangli` submodule 定义 `Role.Blogger` 与 Chronicler persona。
2. **WorkRecord**：源码约 86 个文件（含生成物共 107 处）横跨 Execution（46）、Mission（12，含 3 个 owner fsproj）、OpenCode（10）、Context（9）、Change（4）、Persistence（1），作为三段式结构（Opening + Chronicle + Recent work）承担跨 horizon 返回、join 交付与 attachment 背景。
3. **Replica（`StrengthReplica`）**：`speculative-investigation`（「门外探路的，只是候选」）的执行载体，分布在 `Context/Prefix/Wire`（`isReplicaSession` 分支）、`Context/Companion/CompressionSurface`、`Context/Prefix/Surface`、`Execution/Session/Association` 与 `Ownership`（`AttachmentKind.StrengthReplica`）、`Interaction/Authority/Model`（只读权限矩阵）、`Execution/Failure/Policy`（预算豁免）、`OpenCode/Host`（`ProviderSystemTransform` 约束行、`HostTurnObserver` leaf-local 观察、`PluginHostInterop`）及 14 份规范文档。

## 二、所有者终局裁决（阶段 0 已闭环）

### 裁决 1：advisor 立即改锚，不等落地后置
- **事实核查**：`requirements/advisor/WHAT.md` 仅有 [001]–[006]，原草案所称「仿 `CompanionBloggerLinked`」「仿 `EnsureBloggerAsync`」仅存在于施工分期计划文本中；实际已落地的 `AdvisorSeat` 采用进程内 seat 表、无 durable link、不写 journal。真实代码耦合集中在 `src/Wanxiangshu/Execution/Delegation/Advisor/View.fs`（引用 `BloggerDeltaItem`、`BloggerDeltaPart`、`CompanionPrompt.newWorkMessage` 共 8 处）。
- **结论**：趁 advisor 尚未提交，立即改锚。将 TOML 视图渲染词汇（`SyntheticToml` 及表结构组装）收归通用或 advisor 自有，剥离 `Companion.Blogger` 依赖，同步清理 `advisor/WHAT.md`、`WHY.md`、`README.md` 及 `resources/provider/role/advisor/` 中关于 Blogger/Chronicle 的锚定表述。

### 裁决 2：彻底取消 Y 轴，WorkRecord 整体退出，返回与上下文清理全权交由 view 机制
- **彻底没有 Y**：不再有 Y 轴、Blogger、Chronicle、`MemoryPreamble`（4 文件）与 `FrozenRecordPrefix` 的 Y 覆盖概念（24 文件）。
- **完整 turn 的覆盖证明不证自明**：历史截断与保留完全由模型在编程面显式提交的 `view` 声明（`prefixUntil` / `thenRemove`）决定，不另设独立的 Y 物化覆盖证明。
- **attempt 失败恢复策略简化**：废除 Y 恢复材料与复杂的 prefix probe 候选机制。provider attempt 失败后的恢复策略统一为：**第一次原样重试，第二次把该 Provider 拉黑后重试，以此类推**。
- **WorkRecord 趁机彻底退出**：删除 `Mission/WorkRecord` 整包及全仓 WorkRecord 三段式物化/投影体系。
- **委托返回合同重定义为 `js.deliver` 的 view**：
  - 子工作返回给调用方的内容，就是 `js.deliver` 交回的 `view`（`js.deliver` 带 `view` 参数决定如何交回），渲染成 TOML。
  - 最后的 assistant 散文要不要交回，由 LLM 自己说了算——`view` 里面有就有，没有就没有。
  - **去重规则**：本次工作之前已经返回过的内容，即使本次 `view` 里面包含，也不再返回（上次 `deliver` 已经给过返回机会，绝不重复交付）。
  - **接力上下文管理**：`js.deliver` 在 S1/S2/S3/S4 的接力机制不再主动清除上下文，上下文的保留与裁剪全部依靠 `view` 机制。
- **tip 与诊断语料废弃**：`guidance-delivery`（Enforcer Tip）与 `behavior-diagnosis` 的运行时提交与注入功能彻底废弃；其中有价值的规则与经验重整后直接融入 Common Law（`resources/provider/world/common-law/`）。

### 裁决 3：durable 历史清空（clean-break）
- **结论**：授权整体退役本机 `.Herdchestra/` 运行状态，不读取、不迁移、不为旧 Blogger/WorkRecord/Replica 事件保留兼容解码。在阶段 6 构建与测试验证全绿后一次性清空。

### 裁决 4：Replica 机制彻底废弃
- **结论**：整包废除 `speculative-investigation` 及 `ProviderRequestKind.StrengthReplica` / `AttachmentKind.StrengthReplica`。删除 Common Law 中「门外探路的，只是候选」章节及 Predictor 槽位相关表述，删除 `Prefix/Wire` 中的 `isReplicaSession` 分支、只读权限特例与 Host 观察分支，14 份牵连规范同步收缩。

## 三、依赖面地图

### 内部定义整树删除

- **Blogger / Companion（48+ 文件）**：
  - `src/Wanxiangshu/Context/Companion/Blogger/` 全目录（Toml、TomlSurface、ItemWire、Delta、DeltaSurface、Request、Projection、FrameSurface、ContextFactFold、MainContext、OpenCode/PluginScope、Runtime/*）
  - `src/Wanxiangshu/Context/Companion/`（HostBlogger、Builder、Facts、Fold、Host、Identity、Model、Projection、Prompt、Runtime、Transform、CompanionFactFold、CompressionSurface 等围绕 Blogger/Replica 构建的模块）
  - `src/Wanxiangshu/Enforcer/`（BlogSurface、ChronicleExecution、Cycle/BloggerProbe、Guidance/Tip*、Observation* 等）
  - `src/Wanxiangshu/OpenCode/Host/`（BloggerChronicleSurface、BloggerChronicleText）
  - `src/Wanxiangshu/OpenCode/Tools/ChronicleTool.fs`
- **WorkRecord（12 核心文件 + 3 owner fsproj）**：
  - `src/Wanxiangshu/Mission/WorkRecord/`（Materialize、Model、Surface）及对应 `.fsproj`
- **Replica / Strength（相关模块与分支）**：
  - `StrengthReplicaAssociationHints`、`AttachmentKind.StrengthReplica`、`ProviderRequestKind.StrengthReplica`、`ProviderSystemTransform` 中的 replica 约束逻辑、`Strength` 子系统中服务于 speculative-investigation replica 的运行时组件。

### 外部消费剥离与改写（按子系统）

| 子系统 | 代表文件 | 施工动作 |
|---|---|---|
| Foundation | Roles、OfficeCapability、Identity、Outcome、RolesSurface、SyntheticToml | 删 `Role.Blogger` 与 `ToolPermission.Chronicle`；保留/整理 `SyntheticToml` 供 advisor 与 `js.deliver` 的 TOML view 渲染复用 |
| Participant | Persona/Catalog、ManagedCatalog、Provider/Attempt/*（RequestKind、RetryPolicy、PlannerSurface、Fallback） | 删 `BloggerMain`、`StrengthReplica`、`BloggerRetryPolicy`；将 attempt 恢复落实为「首次原样重试、二次拉黑 Provider 重试」 |
| Context/Prefix & Trace | Wire、WirePort、Surface、XWireSurface、Trace/* | 删 `FrozenRecordPrefix` 的 Y 覆盖逻辑、`MemoryPreamble`、`isReplicaSession` 分支；上下文裁剪仅依从 `view` 声明 |
| Composition | Durable/（CompanionFact、Fold、Projection、AgentJournalPortAdapter）、Turn/* | 删 `CompanionBloggerLinked`、`BloggerCycles`、`BloggerCoordinator` |
| Execution | Delegation/（Advisor/View、Fork/Host/*、Handle、SyncDelegate）、Session/（Association、Attachment、Ownership、Recovery、HorizonTool）、Failure/Policy | Advisor 改锚；Delegation/Join/Horizon 退出 WorkRecord，切换为 `js.deliver(value, view)` 的增量去重 TOML 视图返回；S1–S4 接力移除主动清上下文逻辑；删 `StrengthReplica` |
| Interaction | Authority/Model、Dispatch/、Repair/ | 删 Blogger 准入与 `StrengthReplica` 只读权限矩阵 |
| OpenCode | Host/（约 25 文件）、Plugin/*、Tools/（StaticTools、ToolRegistry） | 剥离 Blogger 生命周期、Chronicle 工具注册与 Replica 钩子 |
| Persistence | Journal/（CodecSurface、Envelope、EventStoreJournalWriter、Surface） | 移除 Blogger / Companion / WorkRecord / Replica 相关 durable 事件类型 |
| Repository/Programming/Js | Capability、GeneratorSurface、ToolsBindings、RuntimeSurface | 删 `todowrite-for-blogger`；更新 `js.deliver` 签名与实现以支持 `view` 参数、TOML 渲染与跨次去重 |
| Resources & Scripts | `resources/provider/**`、`resources/wanxiangshu.mjs`、`scripts/**` | 删 blogger/chronicle/companion 资源目录；将 tip 与行为诊断精华并入 `common-law`，同时删去 `common-law` 中 Blogger、WorkRecord、门外探路（Replica/Predictor）表述；更新 build 与 gate 脚本 |

### submodule `wanxiangli`

- `src/identity/Roles.fs`：删 `Role.Blogger` 分支、allRoles、canonicalName、parse
- `src/identity/PersonaCatalog.fs`：删 Chronicler persona
- `src/identity/index.d.ts`、`src/identity/AGENTS.md`、`src/identity/tests/001.test.mjs`、`src/compression/AGENTS.md` 同步更新并记录 gitlink。

## 四、分阶段施工顺序

### 阶段 1 —— 规范与 Common Law 先行
1. **整包删除**：`requirements/work-record/`、`requirements/speculative-investigation/`、`requirements/guidance-delivery/`、`requirements/behavior-diagnosis/`（提取有价值原则并入 Common Law 后删除）。
2. **核心机制改写**：
   - `requirements/advisor/`：去锚 Blogger/Chronicle，确立自有 TOML view 契约。
   - `requirements/context-compression/` 与 `requirements/prefix-stability/`：移除 Y 轴、200 KiB Blogger delta 合同与 Y 覆盖证明；确立 `view` 声明为唯一截断机制，完整 turn 覆盖不证自明。
   - `requirements/provider-attempt-recovery/` 与 `requirements/execution-failure-policy/`：重写为「首次原样重试，第二次拉黑 Provider 后重试」，移除 `BloggerRetryPolicy`、prefix probe 及 `StrengthReplica` 分支。
   - `requirements/delegation/`、`requirements/repository-programming/`、`requirements/participant-horizon/`：全面退出 `WorkRecord`；确立 `js.deliver` 携带 `view` 参数、TOML 渲染、按已交付游标去重（不重复返回前次已给过机会的内容）、LLM 自主决定是否纳入末尾散文，以及 S1/S2/S3/S4 接力不再主动清除上下文。
3. **角色与集合收缩**：清理其余约 35 份规范中对 `Blogger`、`chronicle`、`WorkRecord`、`StrengthReplica` 的枚举与引用。

### 阶段 2 —— submodule（`wanxiangli`）
剥离 `Role.Blogger` 与 Chronicler，跑通 submodule 自身验证并提交，宿主更新 gitlink。

### 阶段 3 —— 宿主源码（自内向外，手工步步为营）
1. **Advisor 改锚与通用 TOML 视图沉淀**：将 `View.fs` 切换到不依赖 Blogger 的 TOML 渲染函数。
2. **`js.deliver` 视图化与 S1–S4 接力改造**：实现带 `view` 参数的 `deliver`、已返回内容游标去重、TOML 渲染，移除 S1–S4 主动清上下文逻辑，替换 Delegation / Join / Horizon 中的 `WorkRecord` 管道，删除 `Mission/WorkRecord`。
3. **移除 Replica 与重构 Attempt 恢复**：删除 `StrengthReplica` 全链路，简化 attempt retry 为原样重试 + 拉黑 Provider。
4. **整树拆除 Y 轴 / Blogger / Chronicle / Enforcer Tip & 诊断**：删除 Companion/Blogger、ChronicleTool、Enforcer 提示与诊断注入、Prefix/Wire 的 Y 逻辑及 Persistence 事件，同步更新 `compile-order.txt` 与各 owner `.fsproj`。

### 阶段 4 —— resources / scripts
删除废弃资源目录，完成 Common Law 双语文本重整（融合 tip/诊断经验，移除 Blogger/WorkRecord/门外探路章节），更新 `wanxiangshu.mjs`、`tool-map.json`、`build.mjs` 及全部门禁脚本。

### 阶段 5 —— 测试清理与补齐
删除 Blogger / WorkRecord / Replica / Tip 专属测试套件；改写角色集合测试；为 `js.deliver` 的 `view` 去重渲染、S1–S4 接力保留上下文及「首次原样重试、二次拉黑 Provider」补充锚定测试。

### 阶段 6 —— 验证收束与状态退役
1. `node scripts/build.mjs` 编译与模块图链接验证全绿（严禁 `dotnet build`）；
2. 跑通受影响 requirement 测试套件；
3. 清空 `.Herdchestra/` 本机运行状态；
4. 检查 `git status` 确认无残骸，提交并推送到 `master`。
