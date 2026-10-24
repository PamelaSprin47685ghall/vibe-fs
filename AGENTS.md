[GUIDE: Apply ASD-STE100 to source code. Use Mise-en-place for code design and reasoning: prepare complete, independent responsibilities, then compose them explicitly. Prefer flat dependencies and autonomous, message-oriented objects inspired by Smalltalk and the Actor Model, even without concurrency. Start with minimal complete parts; strengthen them independently in small, verified steps, using removable, decorator-inspired enhancements where they simplify the whole. Adapt Gall’s Law, Chesterton’s Fence, and Poka-yoke. Invoke Goodhart’s Law and Via Negativa when needed. Treat these principles as Wittgenstein’s Ladder. Build the real thing, not its imitation. Try to falsify it.] [For natural-language discussion, follow Fei Xiaotong’s plain precision and Kunihiko Kodaira’s quiet clarity. Avoid officialese, performative rhetoric, translationese, and textbook cadence. Let the subject shape the prose.]

## AXIOM

1. [使用者] 万象术从不涉及语义判断，也不怀疑用户与 LLM 的清醒：除最低限度的防呆外，从不阻拦形式合规的请求。在用户已明确授予的权限范围内，信任其决定，不重复请求同一授权，也不增加与目标无关的操作限制；防呆只校验真实误操作及其后果（身份、围栏、形状、可逆性与幂等），不审查内容、动机或结论。
    - 理由：语义判断把实现者的偏好变成对使用者的否决；形式合规的请求若因内容被拦，系统就不再是工具。反复审批已决定的事同样打断工作。

2. [使用者、开发者] 代理之间的散文消息，父→子与子→父两个方向都渲染为文档顶部的 `# ...` 注释，而不是字段里的三引号字符串值：字段只承载材料，注释只承载指令、指导与报告。
    - 理由：散文是发送者自己的话，接收者按话本身去理解，不能被当成数据去解析、匹配或按模板裁剪；材料（事实、结构化结果、可机读载荷）才进字段，保留机器可校验的结构。把散文塞成字段值会把「话」变成「数据」，把材料塞成注释又会丢掉结构。发送者自由决定说什么、怎么说，不设内容模板；可酌情附简短 FYI，不强制，也不堆积。

## 工具调用纪律

- 只要动作彼此独立，就尽量并行。

## 架构与编码风格

### 源码与运行数据分离

- `wanxiangli/` 是正式 Git submodule，跟踪 `main`。依赖升级以远端最新 `main` 为目标，只 fast-forward、不覆盖本地工作；提交宿主时记录确切 gitlink。开发更新与运行程序分开，运行时不得自行拉取或切换源码。
- npm 通过 `file:wanxiangli` 消费公开入口，构建使用该源码版本。任何 `.tgz` 二进制包不得加入 Git，不因体积小、方便本地安装或发行验收而破例。临时产包只允许写到忽略或临时目录。
- 全部项目运行数据统一放在主工作树的 `.Herdchestra/`，linked worktree 共用这份本机状态。Git 仅用于源码管理及只读拓扑查询；应用不得把日志、SQLite、快照、诊断、锁或 SSH 包装写进 `.git`，也不得安装数据同步 hooks/refspec。
- 旧 `.git/wanxiang*` 数据与运行状态 refs 已退役，不读取、不迁移、不作为恢复候选。当前不需要跨机持久化。清理只针对已确认的应用专属目录、配置和 refs，不清空 Git 的 objects、代码分支、标签或其他人的 hooks；历史改写和物理 GC 不是默认清理动作。

对应合同：`requirements/durable-events/WHAT.md` 的本机状态切换条款。禁止以删验收或伪造构建回执掩盖源码依赖尚未准备好。

### 长程委托的连续性

- 公理：使用者委托的是目标的完成，不是一条回复的结束。目标尚未完成时，长程工作不能静默停止；有下一步就继续，遇到阻塞就明确呈现，不能靠用户再次催促维持推进。
- 推论：provider 的自然 stop、空输出和进度小结都是物理回合的边界，不是业务完成证据。所有承担任务的角色（Manager、Orchestrator、Engineer、DevOps、Plan）必须通过既有 `deliver` 形成有效交接或完成；否则自然停机应触发当前任务的 nudge，并说明尚未解除的义务。internal 末梢 Advisor/Bookkeeper 不走公开 deliver，沿各自的专用提交事实收敛，不以一段内部散文解除外层目标义务。
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

# wanxiangshu → wanxiangli 与 HNI 迁移

> 本节是抽取迁移、HNI 接入与残余架构交账的唯一迁移指南，与本文件前述协作规则共同生效。原分散迁移 Markdown 已删除；可执行代码与原始验收数据保留，历史材料从 Git 查询，不再维护独立迁移指南。
> 状态截至 2026-10-24 汇总原记录，不代表对当前工作区重新验收。卡片 DONE 只证明其声明范围，不能推导后继事项已闭合。正式行为以 `requirements/*/WHAT.md` 为准，测试缺口以 `requirements/GAP.md` 为准。

## 先读：这是 clean-break 重构，不是机械迁移

**这是一次 clean-break 重构，目的是把功能彻底做好，不是机械迁移。迁移包 = 一部分程序状态及其行为从 `wanxiangshu` 移到 `wanxiangli`；最终 = 全部移走，`wanxiangshu = empty`。** 这不是“尽量抽取”或“宿主足够薄”：每个迁移包必须让实际状态所有权退出旧仓，最终不在 `wanxiangshu` 保留程序状态、功能逻辑或永久宿主胶水层。每次施工都必须综合 HNI 的宿主中立要求，重新判断职责、状态与协作的归属；不能先照搬旧设计，再把 HNI 当作外围补丁。

**开始设计和修改前，必须阅读并遵守 [wanxiangli/AGENTS.md](wanxiangli/AGENTS.md)**，进入具体职责时继续阅读其指向的 `src/AGENTS.md` 与局部规则。它是库侧设计约束的权威来源，不是可选背景资料；本指南不复制另一套规则。尤其落实其中 LEMMA 2–8 与 SPEC 2–6：

- **每包必须迁走程序状态。** 不把 `wanxiangli` 限定为无状态工具函数集合。凡属于独立职责的程序状态、跨调用状态、不变量与行为，应一起由库内对象拥有；组合者也可以拥有必要的在途协作状态。只搬类型、函数、接口或文件，没有迁走实际状态所有权，不算完成一个迁移包。外部通过消息请求行为、取得结果，不直接摆弄内部状态，不保留宿主与库两份可互相覆盖的权威。状态所有权迁入库不等于将运行数据写入库源码目录；`.Herdchestra/` 的物理存放规则不变。
- **采用 Smalltalk / Actor 风格的 OO。** 先构造完整、自治、可独立使用和验证的对象，再显式组织合作；优先扁平依赖，允许对象私有可变状态，不强制不可变状态传递。同步方法调用也可以是消息，不因此建设 Actor 运行时、邮箱或通用调度框架。
- **主动抽走逻辑，而不只抽数据结构。** 领域判断、生命周期、错误与恢复、状态维护及可独立成立的协作逻辑都要审查能否进入 `wanxiangli`。不能把真正的算法和调度留在宿主，只给库套接口、注入宿主回调或让宿主继续维护库对象的不变量。
- **HNI 与抽取一起设计。** 库不得反向依赖宿主或具体 provider；必要的宿主能力以清楚的端口与结果表达，由明确的组合者组织。不能用万能 Context、服务定位器或逐层转发把旧耦合藏起来；也不能因为目前只有一个消费者就拒绝封装独立职责。
- **干净切换，不兼容旧结构本身。** 按真实需求重定公开契约，迁移全部消费者，删除被替代的实现、接线、别名、过期测试与说明，不为维护旧形状保留 facade、shim 或双轨执行。真实用户承诺与数据后果必须逐项裁决，不能用 clean-break 掩盖能力丢失或未知效果。
- **终点是全部移走，不是给残留盖章。** 本文历史记录中的“保留主仓”“retained”“DONE”只描述当时范围，不授予永久豁免。暂不能抽走的状态与逻辑必须明确记为未完成，并写明真实耦合、下一步及解除条件，不把困难升格为架构公理。最终验收必须逐项确认全部程序状态及其行为已迁走、旧 owner 全部退出、新组合兑现完整功能，且 `wanxiangshu = empty`；“足够薄”、文件数下降或历史卡片全 DONE 都不能替代这个终点。

以最小完整、可运行的小步抵达这一终点：准备职责与反例 → 独立实现 → 真实组合 → 验证承诺 → 删除旧 owner。不得以遥远的大切换推迟当前正确性，也不得把阶段性通过冒称整体迁移完成。

---

## 目录
1. [执行原则、红线与两仓治理边界](#1-执行原则红线与两仓治理边界)
2. [wanxiangli 架构与分发规则](#2-wanxiangli-架构与分发规则)
3. [构建、锁定与运行时执行规范](#3-构建锁定与运行时执行规范)
4. [CUT-00..41 看板完整职责去向与现状总表](#4-cut-0041-看板完整职责去向与现状总表)
5. [未闭合事项、门禁棘轮与精确状态](#5-未闭合事项门禁棘轮与精确状态)
6. [HNI 宿主中立接口完整体系](#6-hni-宿主中立接口完整体系)
7. [迁移来源文件全量清单与保留/删除边界](#7-迁移来源文件全量清单与保留删除边界)

---

## 1. 执行原则、红线与两仓治理边界

### 1.1 执行红线
- **严禁裸执行 `dotnet build`**：所有 F# 编译必须经由统一编排脚本（`node scripts/build.mjs`，若宿主机配置了并发排队锁如 `flock` 则必须遵循排队规则）。严禁绕过脚本直接调用 dotnet，否则破坏构建依赖序并污染产物。
- **保护用户工作区修改**：不得回退、覆盖、重置用户未提交的修改或子模块脏工作树。
- **严禁虚构验证状态**：未在真实环境成功执行过的命令不得标记为已通过。历史 SHA、commit 散列、行号及覆盖率数据一律标明“历史证据”，不得直接等同于当前工作区状态。
- **纯手工原子补丁**：代码和文档修改必须精确应用，严禁利用批量脚本无差别修改。
- **自然停机与交付语义**：主工作树 `.Herdchestra` 为状态唯一权威。会话停机规则以系统正式规范 WHAT 为准，严禁复制废弃的历史角色终态逻辑。

### 1.2 两仓治理与提交边界
- **两仓分工**：
  - **`wanxiangli` (独立核心库)**：承接生命周期、确定性持久化、进程抽象、追踪、归属所有权、事件日志规范等。

- **单向接入原则**：
  - `wanxiangshu` 仅通过 npm 包形式消费 `wanxiangli`（通过子模块 `file:wanxiangli` 安装），严禁在主仓源码中反向引用 `wanxiangli` 内部源码文件。
  - 宿主消费 `wanxiangli` 的公开入口（如 `wanxiangli/work`, `wanxiangli/durable-work`, `wanxiangli/process`, `wanxiangli/trace`, `wanxiangli/ownership`, `wanxiangli/convergence`, `wanxiangli/retention`, `wanxiangli/segments` 等）。
  - 两仓协同开发时，必须在各自仓库独立完成 commit，子模块指针更新需显式提交。

---

## 2. wanxiangli 架构与分发规则

### 迁移单元与协作方法

每个迁移包必须把一部分程序状态及其行为迁入 `wanxiangli`，并删除 `wanxiangshu` 中对应的状态 owner；最终全部移走，`wanxiangshu = empty`。迁移清单必须记录迁前状态、旧 owner、迁后库对象、新 owner、真实消费者与旧 owner 退出证据，不能用文件搬迁量代替状态迁移。暂存必须说明理由、边界、维护者与解除条件，明确计为未完成，不构成永久保留裁决；经批准删除的能力也不能冒充状态已迁入库。正式 WHAT 中的用户承诺不能因抽取而悄悄改变；需要改变契约时明确裁决并同步实现与验收。旧 T/WP/GAP 只是溯源索引，不是新的切分依据。

准备完整、独立的参与者 A、B，由 C 组合；A、B 不互相调用，C 不复制它们的领域判断。状态由对象自己维护，输入输出不暴露可变别名；处理重入与迟到回执。不要把旧 Context、ServiceProvider、Journal 全集塞入新接口，也不为每个函数制造组合者。先兑现最小完整承诺，再添加可移除的增强。

每刀按下表闭环，不把“库已经发布”误写成“生产已经切换”：

| 阶段 | 必须留下的结果 |
| --- | --- |
| 定义范围 | 本包迁走的具体程序状态、行为、旧 owner、新库对象、真实消费者、WHAT 条款、候选删除清单 |
| 固定契约 | 输入、结果、错误、副作用、身份、合法状态、行为差异裁决 |
| 找反例 | 可推翻边界的真实失败与现行正确行为的正控 |
| 独立实现 | 隔离构建、公开产物、行为测试；不回读宿主源码 |
| 实际消费 | 库提交已在 main，宿主 gitlink 精确固定，临时包干净消费与包内验收 |
| 接回真实链 | 组合者接线，覆盖失败、重复、未知与重启 |
| 旧 owner 退出 | 状态所有权已真实迁入库，全部调用者迁移，旧状态容器、实现、工程条目与过期测试退出，历史数据义务明确 |
| 审查集成 | 相同输入环境下 base/candidate 证据、已迁状态及剩余状态清单、已知不足、回退条件；未迁状态保持未完成 |

认领记录卡号、owner、非作者 reviewer、两仓 base SHA、工作树与分支、精确编辑路径、阻塞项。package/lock/exports、gitlink、compile-order/fsproj、ToolRegistry、顶层恢复接线与共享夹具需预约串行修改；阅读不受限制。离线交接先核对分支、未提交内容与运行进程，不靠超时自动回收编辑权。

不同参与者使用独立工作树、node_modules、dist、构建缓存和测试临时仓；不在别人的工作树执行会替换依赖的 `npm ci`，不共享活动产物或借 `NODE_PATH` 消费邻居工作树。linked worktree 共享本机应用状态，物理测试必须使用独立临时仓与隔离配置。只有纯计算可以新旧双算；真实命令、模型请求、会话创建和 Git 写入禁止双执行。

协议破坏性变更须连同所有消费者接线进入同一集成候选。发布顺序为库实现与验证 → 库 main → 主仓 gitlink 与调用链 → 旧实现退出 → 宿主验收。跨仓不能原子提交，须记录精确 SHA；不复用版本覆盖产物，不强推主线。运行时不得 fetch、切换或更新源码；开发更新仅在保留本地工作的前提下 fast-forward 到远端 main。

净改进准入与完整发布不同：历史无关 TODO 可如实保留，但本卡引入的回归、重复效果、原文丢失、扩权、错误读写历史、提前释放资源、反向依赖或无法干净安装都必须阻止合入。缩小承诺需明确裁决，不能通过改门禁、白名单、TODO 或 skip 隐藏失败。

代码抽取、协议变化、数据迁移分别裁决。逐卡盘点未开始、预留未承接、绑定未确认、活动、终结、损坏/未知状态及其 owner；未知效果先查询，不盲目重发。CUT-01 不翻译旧 work 日志的特定决定不能推广到其他存储。出现身份不明、未知追加或资源关闭未证实，停止新准入、保留事实；不能仅回退源码而忽略在途效果与已经写入的新格式。

运行数据只在主工作树 `.Herdchestra/`，linked worktree 共用。`.git` 只作源码管理和只读拓扑查询，不写应用日志、SQLite、快照、诊断、锁、SSH 包装或同步 hooks/refspec。旧 `.git/wanxiang*` 数据与状态 refs 不读取、不迁移、不作为恢复候选；清理不得损及其他 Git 对象、分支、标签或他人 hooks。

### 2.1 技术属性
- **源码与编译**：`wanxiangli` 由 F# (`.fs` 与 `.fsproj`) 实现，通过 **Fable** 转译为独立 **JavaScript (ESM)**，并生成对应的 TypeScript 类型声明（`.d.ts`），构建统一使用 `scripts/build.mjs`。
- **运行时支持**：支持 **Node.js >= 22.13.0** 及 **Bun** 运行时。
- **数据库与持久化实现**：
  - 不使用重型 ORM，通过内置原生的 `DatabaseSync`（来自 `node:sqlite`）或 `Database`（来自 `bun:sqlite`）接入 `wanxiangli/src/eventstore/SqliteJournal.fs`。
  - **SQLite PRAGMA 初始化参数**（源码依据：`wanxiangli/src/eventstore/SqliteJournal.fs:65`）：
    ```sql
    PRAGMA busy_timeout=0;
    PRAGMA journal_mode=WAL;
    PRAGMA synchronous=FULL;
    ```
  - **确定性流式成帧**：基于多 writer 的日志流严格遵循 cut-tail 律，单段文件写入具有自闭环校验。

### 2.2 核心迁移机制与状态律（MIGRATION 规范）
- **三元分工：Work 内存、SqliteJournal 持久化与 DurableWork 协调**：
  - **A. Work 纯内存**：`new Work(workId, records?)` 独立于物理存储运行，构造只接收序列化记录，仅负责纯内存领域状态迁移，操作结果返回 `Applied`。
  - **B. SqliteJournal 持久存储**：提供跨连接原子追加、fsync 及版本号控制。
  - **C. DurableWork 提交协调**：`new DurableWork(workId, journal)`（来自 `wanxiangli/durable-work`），负责协调 Work 状态与 Journal 持久化，返回 `Committed`、`Conflict`、`NotWritten`、`Unknown` 等。
- **Applied 与 Committed 严格分离**：
  - 内存中状态应用（`Applied`）绝对不等于物理落盘（`Committed`）。业务行为必须以持久层的 `Committed` 事实为准。
- **Unknown 结果只查询不重发**：
  - 当 `DurableWork` 提交返回 `Unknown`（无法确定是否落盘）时，只允许通过 `recover` 返回 `ObserveCommit` 重新重开组合并核对同一请求，**严禁盲目重新发起写入或重复派发**。
- **交付启动时序律**：
  - `DeliveryRequested` 属于领域意图事实；真实会话创建与启动（如外部 session 启动）**必须在核对 DurableWork 首次 Committed 之后**方可执行。
  - 交付状态严格遵循 `DeliveryRequested` → `DeliverySessionBound`（或 `ResolveDelivery`）→ `Accepted` 退休状态序；绑定会话后，首个 prompt 返回重新读取已安装的资源 owner。
- **唯一追踪标识符贯穿**：
  - `operationId -> execSessionId` 必须精确贯穿整个交付生命周期，不可中断、不可重置、不可丢失。
- **参与者身份与日志**：
  - 新 work 不对旧生命周期的历史 log 进行回溯翻译，旧格式历史保持原始记录。
- **Trace 原字节与稳定观察身份**：
  - 原始观察、全部用户原件与固定取材区间为同一事实的不同视图。仅追加已确认部件与原始字节，保持稳定观察身份，严禁复制为相互覆盖的账本。
- **Ownership 归属与排空**：
  - 采用作用域森林与唯一资源预留。`Reserved`、`Unknown`、信号与超时均不释放归属；固定父范围资源不随子任期关闭。阻塞期间保留 pending，排空（Drain）后结算同一 operation。
- **Process 物理进程边界**：
  - 物理进程退出（`exitCode`）不等于领域执行成功。进程退出码、有界 stdin 截断与 POSIX 信号由监管者单独裁决。

### 2.3 生命周期收尾六项契约（Lifecycle Closeout）
依据 `proposals/lifecycle-cutover-closeout.md`，生命周期收尾确立了以下 6 项核心行为契约与隔离边界：
1. **单修订版本投影 (Single Revision Projection)**：`DurableWorkHost.inspectedJournal` 单次向 durable owner 读取 records，再由纯 Work 从同一批数据生成 snapshot 与 history。两次读取之间绝不混入并发提交，下一次读取才可见新任，不增加跨库锁，不维护第二份写投影。
2. **DevOps 身份与模型目标分离**：宿主分别暴露 `DevOpsId` 与 `DevOpsTarget`。目标从已验证历史的 `DevOpsBound` 事实取得；恢复路由只使用 `DevOpsTarget`，严禁将身份字符串直接交给模型路由器。
3. **逐 work 恢复隔离 (Per-Work Recovery Boundary)**：首次读历史与异步恢复过程具备逐 work 错误隔离边界。记录损坏时仅在该 work 上保留 workId 和诊断原因报错，其他合法独立 work 继续恢复，单个坏 work 绝不阻断其他工作。
4. **交付、资源与发现边界**：状态流转严格遵循 `DeliveryRequested` → `DeliverySessionBound` → `Accepted`；正常交付在首 prompt 返回后重新读取资源；发现请求最多取两条候选，满页或模糊匹配严禁判定为“唯一执行者”。
5. **集成与交付残留清理**：authority 推进彻底删除对未使用的旧 `WorkspaceEventStore` 的前置调用；工具清单杜绝重复注册 deliver；严格以十四个 Journal 切片核验。
6. **验收器输出边界与 IPC 权威通道**：在测试 worker 内，子进程诊断全部转发至 stderr，stdout 纯净留给原生结果协议；配合管道与增量 UTF-8 解码，IPC 保持为唯一权威结果通道，杜绝反序列化截断导致结果误判。
*环境基线*：Node.js 最低版本统一为 **22.13.0**。

### 2.4 分发与引用机制
- **Git 子模块与 npm 绑定**：
  - 主仓将 `wanxiangli` 作为 git submodule 挂载于主仓根目录 `/wanxiangli`。
  - 主仓 `package.json` 中配置依赖为：`"wanxiangli": "file:wanxiangli"`。
  - 打包发布遵循 `bundledDependencies`，主仓 git 中**严禁提交 `.tgz` 二进制归档**。
  - 0.5.1 版本生成的 reproducible tgz 校验参考值（历史证据）：`sha512-UxENQYqeAOSL/W9gt/ATyYaOu9eBOICD6anfz2zkLZqeAboxg4pW+LEJHoRJKBPNjjhpHj5HreEvXDLlm2UUZA==`。

---

## 3. 构建、锁定与运行时执行规范

### 3.1 构建命令与排队锁
- **标准编译命令**：
  ```bash
  node scripts/build.mjs
  ```
- **多任务宿主机排队锁机制**：
  在多 agent 或并发任务的宿主环境中，若配置了全局构建排队锁，构建必须显式持有排队锁执行，避免并发 dotnet 编译竞争破坏增量构建缓存：
  ```bash
  flock <lockfile> node scripts/build.mjs
  ```
  *注：本地独立开发或无排队锁文件的环境直接运行 `node scripts/build.mjs`。*

### 3.2 离线检查与门禁命令
- **架构门禁与适配器纯洁度检查**：
  ```bash
  node scripts/checks/architecture.mjs
  node scripts/checks/adapter-thinness.mjs
  node scripts/checks/fsharp-control-pyramid.mjs
  ```
- **HNI Omp 适配器镜像与离线测试**：
  ```bash
  # 验证 TypeScript 端口镜像与 F# 签名严格逐字节一致
  node proposals/host-neutral-interface/adapters/omp/mirror-check.mjs
  
  # 执行离线 fixture 测试（无需启动 omp 进程、无需模型配额）
  bun test proposals/host-neutral-interface/adapters/omp/fixtures/fixture.test.ts
  ```

---

## 4. CUT-00..41 看板完整职责去向与现状总表

本表严格忠实于 `proposals/extraction-kanban/BOARD.md` 与 44 个单卡文件，准确记录每项 CUT 的原定职责、责任人和实际去向（抽入独立库 / 移入主仓新模块 / 废弃删除 / 原地保留）。

| 卡号 | 卡片标题与原职责 | 归属库与版本 / 真实去向 | 原始看板责任人 | 状态 |
|:---|:---|:---|:---|:---:|
| **CUT-00** | 两仓准备、差异裁决与并行开工 | 建立子模块与基础构建契约 | eng-cut00 | **DONE** |
| **CUT-01** | 工作生命周期与原子日志接线 | `wanxiangli/lifecycle` 0.1.0/0.2.0 (`DurableWorkHost.fs` / `Work` / `SqliteJournal`) | 历史已合入 | **DONE** |
| **CUT-02** | 非 PTY 进程执行 | `wanxiangli/process` 0.3.0 | eng-cut02 | **DONE** |
| **CUT-03** | 原始轨迹与固定区间 | `wanxiangli/trace` 0.4.0 | eng-cut03 | **DONE** |
| **CUT-04** | 唯一资源归属与关闭 | `wanxiangli/ownership` 0.5.0/0.5.1 | eng-cut04 | **DONE** |
| **CUT-05** | 双语资源与包内定位 | `wanxiangli/resources` 0.6.0 | — | **DONE** |
| **CUT-06** | WorkRecord 取材与视图 | **REMOVED**：旧三段式 WorkRecord 废除；交付视图格抽入 `wanxiangli/delivery` 0.9.0 | eng-cut06-goose | **DONE** |
| **CUT-07** | 参与者身份与执行绑定分离 | `wanxiangli/identity` 0.11.0 | eng-cut07b-goose | **DONE** |
| **CUT-08** | 能力事实与双侧门禁 | `wanxiangli/capability` 0.18.0 / 0.20.0 / 0.22.0 | eng-cut08c-goose | **DONE** |
| **CUT-09** | 来源、逻辑根与关闭权威 | `wanxiangli/authority` 0.28.0 | eng-cut09c-goose | **DONE** |
| **CUT-09-a** | 入口准入决策（ChatAdmission/Intent）纯裁决 | `wanxiangli/authority` 0.36.0 (admit 纯函数) | eng-cut09e-goose | **DONE** |
| **CUT-09-b** | interaction-authority-018 关闭见证精确匹配 | `wanxiangli/authority` 0.57.0 (关闭见证单次消费) | eng-cut25h-goose | **DONE** |
| **CUT-10** | 模型路由与精确容量许可 | `wanxiangli/routing` (commit `f982f8070`), `permit` 0.33.0, `capacity` 0.35.0, `admission-queue` 0.37.0, `borrowing` 0.43.0, `run-witness` 0.47.0 | eng-cut10g-goose | **DONE** |
| **CUT-11** | 派发认领与外部效果记账 | `wanxiangli/dispatch` 0.40.0, 0.42.0, 0.45.0 | eng-cut09f-goose | **DONE** |
| **CUT-12** | 会话关联与执行事实 | `wanxiangli/association` 0.46.0, `chat-execution` 0.44.0, `attachment-lease` 0.50.0 | eng-cut12d-goose | **DONE** |
| **CUT-13** | 委派、复用与结果认领 | `wanxiangli/handle-lifecycle` 0.52.0 / 0.53.0, `delegate-batch` 0.55.0 | eng-cut12d-goose | **DONE** |
| **CUT-14** | 失败预算、恢复决议与结算 | `wanxiangli/failure` 0.49.0, `failure-budget` 0.54.0 | eng-cut14c-goose | **DONE** |
| **CUT-15** | 前缀世代与候选提交 | `wanxiangli/prefix` 0.10.0 | eng-cut15c-goose | **DONE** |
| **CUT-16** | 压缩覆盖与 checkpoint | `wanxiangli/compression` 0.21.0, 0.25.1 | eng-cut16d-goose | **DONE** |
| **CUT-17** | 请求投影与信息边界 | `wanxiangli/projection` 0.28.0, 0.34.0, 0.39.0, 0.41.0 | eng-cut17f-goose | **DONE** |
| **CUT-18** | 规范事实编码与校验 | `wanxiangli/events` 0.7.0 (规范事件编解码) | eng-cut18 | **DONE** |
| **CUT-19** | 多 writer 存储与收敛 | `wanxiangli/convergence` 0.14.0, `retention` 0.16.0, `events.checkClosure` 0.19.0, `segments` 0.23.0 | eng-cut19d-goose | **DONE** |
| **CUT-19-a** | 段共存的字节裁决与 writer 流成帧律 | `wanxiangli/segments` 0.24.0 (字节序与流成帧契约) | eng-cut19d-goose | **DONE** |
| **CUT-19-b** | Integrator 规则隔离与 cut-tail 载荷律 | `wanxiangli/quarantine` 0.38.0 | eng-cut27d-goose | **DONE** |
| **CUT-19-c** | Journal/旧 Store 逐事实族退出与残留 Git 类型去向 | **RETAINED & REMOVED**：`Persistence/Journal` 原地保留；零消费者残留类型 (`GitObjectId`, `RootOid`, `StoreSnapshot`, `PublishError`) 彻底删除 | eng-cut09k-goose | **DONE** |
| **CUT-20** | PTY 与完整输出外存 | `wanxiangli/spool` 0.13.0, `pty` 0.15.0 | eng-cut20b-goose | **DONE** |
| **CUT-21** | 显式时间与因果等待 | `wanxiangli/time`, `wait` 0.8.0 | — | **DONE** |
| **CUT-22** | attention、concern 与邮箱归属 | **REMOVED**：无活算法可抽，概念废弃不留垫片 | eng-cut09c-goose | **REMOVED** |
| **CUT-23** | Git 工作树、委任与集成 | `wanxiangli/git-porcelain` 0.48.0 / 0.61.0 | eng-cut25j-goose | **DONE** |
| **CUT-24** | 文件读取、锚点与原子编辑 | `wanxiangli/fs` 0.7.0 | eng-cut24 | **DONE** |
| **CUT-25** | 受限 JS 编程面与执行组合 | `wanxiangli/sandbox` 0.23.2 | eng-cut25h-goose | **DONE** |
| **CUT-26** | 需求发现与材料溯源 | `wanxiangli/grounding` 0.7.0 | eng-cut26 | **DONE** |
| **CUT-27** | Casebook / 知识复用去向 | `wanxiangli/casebook` 0.26.0, `maintenance` 0.29.0, `access` 0.31.0；主仓 Bookkeeper/Settlement/Host 保留 | eng-cut27d-goose | **DONE** |
| **CUT-28** | Fission 多 lane 执行 | `wanxiangli/fission-group` 0.59.0 / 0.60.0 / 0.61.0 | eng-cut28d-goose | **DONE** |
| **CUT-29** | Strength / 投机调查去向 | `wanxiangli/warm-start` 0.51.0；Strength / Replica / 投机调查概念彻底删除 | eng-cut09k-goose | **DONE** |
| **CUT-30** | Rulebook、指导与退化观察去向 | `wanxiangli/degeneration` 0.12.0；Rulebook / Enforcer / Blogger 废除删除 | eng-cut06-goose | **DONE** |
| **CUT-31** | Sphinx 与认识运行时去向 | `wanxiangli/ordinal` 0.56.0, `bayes` 0.58.0；Ordinal Fit / DesignCheck 删除；Questionnaire / Core / Runtime 主仓保留 | eng-cut09k-goose | **DONE** |
| **CUT-32** | 可独立验证的执行设施 | **RETAINED**：主仓 supervisor 与 `verification-system` (016/021/006) 设施全部保留在主仓 | eng-cut06-goose | **DONE** |
| **CUT-33** | 两仓构建、打包与发布闭环 | 建立 submodule + `file:wanxiangli` + SPEC[build-008] 闭环 | eng-cut06-goose | **DONE** |
| **CUT-34** | OpenCode 适配边界 | 门禁 ⑦ ⑧ 建立；主仓与适配层解耦（`Wanxiangshu.OpenCode` 命名空间引用从 31 降至 0） | eng-cut34c-goose | **DONE** |
| **CUT-35** | 顶层组装与逐域恢复 | `Composition/` 清理无消费者导出与死代码，恢复顶层装配 | eng-cut35b-goose | **DONE** |
| **CUT-36** | 第二宿主真实 canary | omp 18.12.4 Canary 实测授权推进；O-1/O-4 落地，O-2/O-3 未闭合，真实 Smoke 仍未通过 | — | **DOING** |
| **CUT-37** | 消融、诊断与实测成本 | `wanxiangli/ablation` 0.17.0 | eng-cut06b-goose | **DONE** |
| **CUT-38** | 全仓残余职责与终局交账 | 拆分子项 F38-01..10 逐项锁定与清退 | eng-cut35b-goose | **DONE** |
| **CUT-39** | advisor 专席去向 | `wanxiangli/advisor` 0.63.0 (原 F38-01 分流) | eng-cut39-goose | **DONE** |
| **CUT-40** | 域内适配子目录棘轮 | 门禁 ⑨ (17 -> 5 -> 0)，落地适配器纯洁度收敛 | eng-cut40-goose, eng-cut40-fu | **DONE** |
| **CUT-41** | GAP 台账逐条核实与精确重定向 | GAP-005/015/050/075/088/123 逐项核实与重定向 | gap-ledger | **DONE** |

---

## 5. 未闭合事项、门禁棘轮与精确状态

### 5.1 F38-01..10 残余交账与终局状态（依据 cards/CUT-38.md §7.6）

- **F38-01 (新包 `advisor` 抽取评估)**：
  - *实际情况*：建板后仓主新增需求包 `advisor`（commit `131a79106`，位于 `Execution/Delegation/Advisor/`），未在原 COVERAGE 中，未做抽取评估。
  - *去向*：交由 **CUT-39** 处理，已抽取至 `wanxiangli/advisor` 0.63.0。状态：**DONE**。
- **F38-02 (域内适配子命名空间 `*.OpenCode` 暴露)**：
  - *实际情况*：8 个内核文件 + 8 个 Surface 文件仍 `open` 域内适配子命名空间 `Wanxiangshu.<域>.OpenCode`，而门禁 ⑧ 只统计顶层 `Wanxiangshu.OpenCode`。
  - *去向*：交由 **CUT-40** 建立门禁 ⑨ 处理。状态：**DONE**。
- **F38-03 (`CompanionLeaseRuntime` 与 `AttachmentLeaseRegistry` 存废)**：
  - *实际情况*：经核查两者仍为活代码（`AttachmentLeaseRegistry` 在 `AttachedRuntime.fs:36` 构造，`CompanionLeaseRuntime` 支撑 `AttachmentSurface.fs:235` 与 `managed-session-lifecycle-001`）。
  - *裁决与处置*：**明确保留（RETAINED）**在主仓；同批删除了 `CompanionHost` 上无人传入且未读取的死参数 `?satelliteRuntime`（commit `938b966ff`）。状态：**DONE (RETAINED)**。
- **F38-04 (`fsharp-control-pyramid` 控制流金字塔门禁)**：
  - *实际情况*：属于尚未闭合后继项。`node scripts/check.mjs` 在 master 上红态（117~137 条违例，随仓主 delivery 重构批次浮动）。`RED-check-land` 分支通过静态拉平在分支上测得 137 → 0，但**未做 focused-build 编译/测试验证**；后续 master 上随仓主改动仍有 **122~131 条既有红态**。
  - *边界*：由仓主当前的 delivery 重构批次（CUT-32 门禁 owner 复核）承接，非 0 违规全绿。状态：**尚未闭合项 / 待后继复核**。
- **F38-05 (`shard-edge-audit` 2 条 high 边处置)**：
  - *实际情况*：master 上实际的 2 条 high 边是 **js-capability** 边（`action-affordance.opencode-tools-statictools` → `repository-programming-js-capability`、`delegation.execution-delegation-advisor-seat` → `repository-programming-js-capability`），**不是**早期草稿误写的 `AgentJournal`/`TaskDelivery` 边（后者早被 master 修掉）。
  - *处置*：给上述两 shard 补全真实 ProjectReference，静态测量 high 降为 0；未给 `delegation-recovery-runtime` 加 ProjectReference 以防打破闭包。状态：**DONE (静态交账)**。
- **F38-06 (GAP 台账 203 行状态梳理)**：
  - *实际情况*：台账中 144 条历史叙述状态行未归一，13 条单值 OPEN/PARTIAL。
  - *处置*：commit `1d8b66d1d` 仅归一了 19 行（归档 18 / 关闭 53 / 开放 11 / 部分 112 / 退役 9，共 203 行保留），其余 100+ 条叙述行未动；后继 CUT-41 仅针对交接点名的 6 行实施重定向，台账整体未闭合。状态：**PARTIAL（尚未闭合项 / 部分处置）**。
- **F38-07 (`owner-impact-corpus.json` 基线更新)**：
  - *实际情况*：历史快照包含已删文件，重新测量并固化基线时间（commit `6109ed096`）。状态：**DONE**。
- **F38-08 (低价值/零消费者导出清理)**：
  - *实际情况*：余下 14 个成员、9 个同文件重名值等导出收益极低。处置明确定位为按需保留，不另开卡，不计为抽取或清理完成。状态：**按需保留（非 DONE）**。
- **F38-09 (基础发布门禁既有红态)**：
  - *实际情况*：发布门禁在 base 上即为红态：wanxiangli `build-001` sandbox 失败、64 文件 format 违规、pyramid 122 条、`run.mjs` 在 `rp-032/036` 挂起、`durable-events-022` 超时 300s。
  - *边界*：非本次引入，如实记录不代表问题已解决；由发布 owner（CUT-32 后继）承接解决。状态：**OPEN（尚未闭合项，由发布 owner 承接）**。
- **F38-10 (`adapter-thinness` 门禁 ⑩ 偶发退化修复)**：
  - *实际情况*：rebase 到最新 master 后 product-domain 引用由 24 升至 26（全仓 695 → 697）。
  - *处置*：通过改别名和非限定引用，使引用数回到 24，全仓实测 596（在 689 基线以下）。状态：**DONE**。

### 5.2 CUT-40 域内适配子目录与门禁 ⑦/⑧/⑨/⑨b/⑩ 棘轮（依据 cards/CUT-40.md）

- **门禁 ⑦ (`host-sdk-boundary`)**：限制宿主 SDK 类型不外溢至产品核心。
- **门禁 ⑧ (`adapter-import-ratchet`)**：
  - 限制顶层 `Wanxiangshu.OpenCode` 的引用。基线为 **0**。
  - 经 F40-03 升级，同时扫描 `.fs` 与 `.fsi` 文件。
- **门禁 ⑨ (`domain-adapter-import-ratchet`)**：
  - 统计非适配区代码引用 `Wanxiangshu.<域>.OpenCode` 的文件数。
  - **诚实起点为 17**（初始 16 个 + F40-02 命名空间修正后显形的 `XWireSurface.fs` 1 个）。
  - 历经 `17 → 5 → 0`（8 个内核文件归零，9 个 Surface 逐刀清理/搬迁）。**终点计数：0**。
- **门禁 ⑨b (`adapter-namespace-folder`)**：
  - 新增门禁：目录结构与命名空间必须严格一致，禁止在非适配目录声明适配命名空间，反之亦然。违例由 6 条降至 **0**。
- **门禁 ⑩ (`adapter-thinness`)**：
  - ⑩a product-reference（适配器引用产品命名空间数）：基线 **689**，分支实测 **596 / 585**（单调下降）。
  - ⑩b without-port（无中立端口的适配器文件数）：基线 **150 / 149**，分支实测 **137 / 127**。
- **子项已落地情况**：
  - `F40-01`：`JoinResultRenderer` / `JoinSurface` 移入 `Fork/`，清理无用 `open`。
  - `F40-02`：`Wire.fs` 修正命名空间，拆分中立的 `WireReconciliation`，建立门禁 ⑨b。
  - `F40-03`：`.fsi` 签名文件纳入门禁 ⑧ 与 ⑨ 扫描。

### 5.3 CUT-41 GAP 台账审计与精确去向（依据 cards/CUT-41.md 与 GAP-audit-2026-10-21.md）

台账（`requirements/GAP.md`）中的 6 条核心项处置全部明确定位为 **C（精确重定向）**，严禁擅自标注为已闭合：

- **GAP-005** (`requirement-system` [015])：
  - 状态：**OPEN**（保持不变）。
  - 剩余工作：无 faithful mechanical oracle，依赖人工审阅；机器落点需规范侧先改写为可判定谓词。
- **GAP-050** (`requirement-system` [003])：
  - 状态：**OPEN**（保持不变）。
  - 剩余工作：无 faithful mechanical oracle，依赖人工语义审阅；机器落点需独立于本仓的权威模型。
- **GAP-015** (`feature-ablation` [005] + `capability-enforcement` [005])：
  - 状态：**PARTIAL**（改指）。
  - 处置：命题退役。治理清理完成，功能本身不恢复（commit `584c30c61` 删包）。
- **GAP-075** (`capability-enforcement` [025]/[026])：
  - 状态：**PARTIAL**（保持不变）。
  - 进展与剩余工作：2026-10-22 已落地真实独立 oracle（`025.test.mjs:422` 真实并发 overlap，`026.test.mjs:371` 跨进程能力隔离）；但因缺编译产物未在 `dist/` 上真实运行，保持 PARTIAL。
- **GAP-088** (`repository-programming` [003]/[006]/[017]/[025]/[026]/[027])：
  - 状态：**PARTIAL**（保持不变）。
  - 进展与剩余工作：2026-10-22 将 4 条 `test.todo` 转为真实测试（`006:163`, `017:32`, `025:325`, `025:413`）；静态检查通过但未在 `dist/` 上运行，保持 PARTIAL。
- **GAP-123** (`interaction-authority` [018] + `managed-session-lifecycle` [020]/[026])：
  - 状态：**OPEN**（保持不变）。
  - 真实产品缺口：涉及四类宿主 durable source terminal 事实；`AuthorityRootAccepted` 原子携带 `ExpectedClosureKind`；writer 不再硬编码 `RootKind="HumanRoot"`；3 条 todo 待转为真实测试。

### 5.4 CUT-36 第二宿主真实 Canary、O-1..O-5 决策与未决项

仓主已正式指认 **omp 18.12.4** 为第二宿主，并授权真实模型调用。当前看板状态：**DOING，真实 Smoke 仍未通过**。

#### O-1..O-5 规则与实现状态（依据 HNI-13 与 evidence/omp-feasibility.md）：
- **O-1 (`TranscriptMessageAddress` 映射)**：**已决并落地 (DECIDED)**。 neutral 的消息地址严格对应 omp 的持久化条目 ID (`SessionEntry.id`)。严禁合成 `omp:<index>` 位置地址。若条目无合法 id 则按 `HostError.Transport` 失败返回并标明位置。子会话同样通过子实例的 `sessionManager.getEntries()` 读取。
- **O-2 (`ProviderParams` 编解码)**：**已决但阻塞 (BLOCKED)**。适配器按各模型提供商家族独立编码，遇到无法完整映射的情况执行 fail-closed；因当前 omp 宿主环境缺失上游可达的 provider 参数暴露接口，`ProviderParams` 能力暂不宣告。
- **O-3 (`activateOnOmp` 真实产品绑定与编译门禁)**：**未闭合 (OPEN)**。Fable 编译出的 JS 产物与 `activateOnOmp` 的真实入口链路尚未完全接通；且在上游引入 P-19 必选配置端口后，严格 `tsc` 检查报 `TS2741: Property 'Config' is missing in type ... but required in type 'HostPorts'`。未伪造空配置对象，保持真实报错。
- **O-4 (`parentSession` 解析)**：**已决并落地 (DECIDED)**。omp 原生会话头中 `parentSession` 可能是 ID 字符串也可能是文件路径。适配器经由 `readSessionInfo` 解析，若不可解析则明确返回 `HostError.NotFound`（保留原始值），严禁将路径假冒为会话 ID。
- **O-5 (Session ID 二元性裁决)**：**已决并落地 (DECIDED)**。omp 内部存在持久化 ID (`SessionManager.getSessionId()`) 与 Provider ID (`AgentSession.sessionId`)。中立端口的 `SessionId` **绝对绑定为持久化 ID**，保证跨会话查找和父子关联永不脱节。

### 5.5 vs-006 验证套件根因诊断事实（依据 evidence/vs-006-root-cause.md）

- **表现**：`requirements/verification-system` 中编号 **006** 的套件在持续集成中偶发显红（RED）。
- **历史真实诊断证据**：
  - 并非确定性业务逻辑错误，而是 supervisor 监管者的判决静默窗口（verdict-silence window）在目标工作真正建立前即被激活导致的计时竞争。
  - **机制与耗尽点**：Watchdog 在 inner runner 真正启动前即装载；在首个测试体执行与首个判决产生前，启动开销已耗尽 1000ms（用例级）或大部分 5000ms（套件级）预算；此外 teardown 侧还存在 `spawnSync ps ETIMEDOUT`（2000ms 上限）超时。
  - 典型运行采样结果：不同次抓取中分别出现 46/59 通过（13 失败）、43/59 通过（16 失败）、41/59 通过（18 失败）。
  - **受限证明与非全局证明**：在直接运行 `run-inner.mjs` 及放宽至 30000ms 窗口的两项受控测量中未出现判决丢失，但**该结论严格受限于这两次受控测量，绝不等于在未经插桩的实际失败运行中全局证明了未丢判决**（实际失败运行中 fixture 在判决被捕获前即被杀）。
  - 30000ms 窗口仅为诊断工具，**严禁作为准入或验收配置放宽**。套件在未修改配置下依然保持 **RED**。

---

## 6. HNI 宿主中立接口完整体系

### 6.1 分层架构：Tier A 端口 vs Tier B 领域接缝
HNI 采用严格分层架构，防止宿主概念侵入产品核心：
- **Tier A (中立端口层)**：
  - 源码位置：`src/Wanxiangshu/Host/Ports/`
  - 命名空间：严格限定为 `Wanxiangshu.Host.Ports`
  - 规范要求：纯宿主抽象，仅依赖 Foundation/Host/System，严禁包含任何具体宿主特有概念。由 `gen-ports.mjs` 自动机械生成 `ports.ts`，受门禁 ⑪ ⑫ 约束。
- **Tier B (领域接缝层)**：
  - 源码位置：`src/Wanxiangshu/Host/Domain/`
  - 命名空间：严格限定为 `Wanxiangshu.Host.Domain`
  - 规范要求：专供领域内部（Fork, SyncDelegate, Fission 等）向宿主请求特定上下文，**不生成 TypeScript 镜像**，受门禁 ⑬ 约束。

### 6.2 P-00..P-20 端口注册表与实现现状

**当前空闲端口号**：**P-20**（下一个新增端口必须分配 P-20）。

| ID | 端口名与核心契约 | 所在文件 | 状态 | 职责与接入现状 |
|:---|:---|:---|:---:|:---|
| **P-00** | `HostError`, `HostResult`, `HostCapability`, `WorkspacePath`, `AgentRole` 等通用词汇 | `Host/Ports/Vocabulary.fs(i)` | **WIRED-OC / SKELETON-OMP** | 统一宿主错误与能力字典，彻底替代 `string` 错误通道 |
| **P-01** | `ISessionPort`, `SessionSpec`, `SessionInfo` | `Host/Ports/Sessions.fs(i)` | **WIRED-OC / SKELETON-OMP** | 覆盖 Create, Parent, Children, Abort, Interrupt, Close, FindByTitle 七大动词；OpenCode 适配器已全量接入 |
| **P-02** | `IPromptPort`, `PromptRequest`, `DeliveryVerdict` | `Host/Ports/Prompts.fs(i)` | **WIRED-OC / SKELETON-OMP** | 提示词投递与收执通道；派发主路径已切入 neutral 契约 |
| **P-03** | `ITranscriptPort`, `TranscriptMessage`, `TranscriptPart` | `Host/Ports/Transcript.fs(i)` | **WIRED-OC / SKELETON-OMP** | 轨迹与历史消息；SessionRecoveryHost 与派发恢复已接入，Fork 恢复因工具面依赖暂保留 |
| **P-04** | `IToolRegistrar`, `ToolSpec`, `ToolInvocation`, `ToolOutcome` | `Host/Ports/Tools.fs(i)` | **DECLARED / SKELETON-OMP** | 工具注册中立契约 |
| **P-05** | `IToolGate`, `ToolCallVerdict`, `ToolResultEdit` | `Host/Ports/ToolGates.fs(i)` | **DECLARED / SKELETON-OMP** | 工具调用前置/后置拦截门禁 |
| **P-06** | `ITurnTransform`, `TurnAdmission`, `ContextEdit`, `TurnContext` | `Host/Ports/TurnTransforms.fs(i)` | **DECLARED / SKELETON-OMP** | 轮次上下文改写与注入；Sphinx v2 适配层已承载 neutral `TurnContext` |
| **P-07** | `ISignalSource`, `HostSignal`, `TurnTerminal` | `Host/Ports/Signals.fs(i)` | **DECLARED / SKELETON-OMP** | 宿主生命周期事件与信号源 |
| **P-08** | `IModelCatalog`, `ModelRoute`, `SpawnRoute` | `Host/Ports/ModelRouting.fs(i)` | **WIRED-OC / SKELETON-OMP** | 模型目录枚举与会话选型（不支持时返回 Unsupported，不假冒空列表） |
| **P-09** | `ICompactionGate`, `CompactionContribution`, `ContinueVerdict` | `Host/Ports/Compaction.fs(i)` | **WIRED-OC / SKELETON-OMP** | 上下文压缩拦截与自动续航门禁 |
| **P-10** | `IHostPaths` (StateRoot, ProjectRoot, ProjectId) | `Host/Ports/Storage.fs(i)` | **WIRED-OC / SKELETON-OMP** | 宿主与产品存储路径单一权威；StateRoot 严格指向主工作树 `.Herdchestra` |
| **P-11** | `IHostPlugin`, `HostPorts`, `ProductHooks`, `HostCapabilities` | `Host/Ports/Lifecycle.fs(i)` | **WIRED-OC / SKELETON-OMP** | 插件顶层激活契约与能力集宣告 |
| **P-12** | `IDiagnosticSink` | `Host/Ports/Diagnostics.fs(i)` | **WIRED-OC / SKELETON-OMP** | 结构化诊断输出，受 `WANXIANGSHU_DIAG=1` 与 CTX-014 校验保护 |
| **P-13** | `CommandDeclaration`, `CommandArgument`, `ArgumentShape` | `Host/Domain/CommandDeclaration.fs(i)` | **WIRED-OC (Tier B)** | 领域命令形态声明（commit `58660d4d9`） |
| **P-14** | `IDelegationCallerHost`, `DelegationCaller` | `Host/Domain/DelegationCaller.fs(i)` | **WIRED-OC (Tier B)** | 委派调用上下文接缝（commit `58660d4d9`） |
| **P-15** | `IDelegationCommandHost`, `DelegationReply` | `Host/Domain/DelegationCommand.fs(i)` | **WIRED-OC (Tier B)** | 委派命令返回中继（待与 P-14 合并至领域模型） |
| **P-16** | `PairGuidanceTranscript`, `PairGuidanceLedger` | `Host/Domain/PairGuidance.fs(i)` | **WIRED-OC (Tier B)** | 配对指导事实提取接缝（commit `3e2f12213` 草稿曾误记为 P-13，经 D-01 碰撞裁决正式分配并保留为 P-16） |
| **P-17** | `FissionHostPort`, `FissionSessionPort`, `FissionEventPort` | `Host/FissionPort.fs(i)` | **WIRED-OC (Transitional)** | Fission 多分支并发接缝，正向 P-01/P-07 汇聚 |
| **P-18** | `HostToolContext`, `ToolAdmission` | `Host/ToolContext.fs(i)` | **RETIRED (ID 废弃)** | 原与 P-04 文件名冲突，更名后将融入 P-04，本 ID 永久作废不复用 |
| **P-19** | `IHostConfigPort`, `RequiredSetting`, `HostSettingValue` | `Host/Ports/HostConfig.fs(i)` | **WIRED-OC** | 宿主配置动态读取与校验门禁（commit `93a51ca15`） |
| **P-20** | *(保留中)* | — | **NEXT FREE** | 下一个新增端口的保留 ID |

### 6.3 架构门禁 ⑩ / ⑪ / ⑫ / ⑬ 规则

- **Gate ⑩ (适配器薄度棘轮，`adapter-thinness.mjs`)**：
  - 限制适配器内部私有领域引用量，只能单调收敛，禁止新增违背中立端口的直连逻辑。
- **Gate ⑪ (端口纯洁性门禁，严格 0 容忍)**：
  - `Host/Ports/` 下所有文件仅允许引用 Foundation、Host、System，**严禁引用任何具体宿主 (OpenCode / Omp) 概念**。
- **Gate ⑫ (端口家族完整性门禁，严格 0 容忍)**：
  - `Host/Ports/` 下每个接口文件必须且仅能声明一个 `HNI P-xx` 标记；禁止重名冲突；禁止在 `Host/` 根目录直接建立同名覆盖文件（防范历史 D-07 冲突）。
- **Gate ⑬ (命名空间分层完整性门禁，严格 0 容忍)**：
  - `Host/Ports/` 必须声明 `Wanxiangshu.Host.Ports`（Tier A）；
  - `Host/Domain/` 必须声明 `Wanxiangshu.Host.Domain`（Tier B）；
  - 严禁任何根文件越级声明上述命名空间；对引用 Tier B 内部接缝的适配器文件数量执行基线收敛检查（`domain-seam-baseline.json`）。

### 6.4 会话端口迁移剩余项与深层依赖（依据 evidence/session-port-leftovers.md 2026-10-22 快照）

本节忠实记录 P-01 (`ISessionPort`) 与 P-03 (`ITranscriptPort`) 迁移中的确切剩余依赖。**此为历史观察快照，不冒称现态，杜绝泛化假迁**：

1. **Fork Recovery 缓迁原因与 24 文件 / 126 处调用历史扇出**：
   - `ChildRecoveryWorkflow.Ports` 注入点为 `Fork/Host/Restart.fs`，其 snapshot 来自 `HostForkRuntime`（`Fork/Host/Runtime.fs`）。
   - `HostForkRuntime` 构造点由 `Change/Host/Host.fs` 及 `OpenCode/Tools/ToolRuntimeScope.fs` 共同供给。
   - `ToolRuntimeScope` 跨 **24 个文件、126 处调用**（直接驱动 `JoinTool`、`DeliverTool`、`ReviewBackendManager` 等）。
   - **有损逆向投射严格禁止**：下游工具（如 `ReviewBackendManager.fs:134,143`）深度依赖 `ISessionSnapshotPort` 专有的 `SessionToolPart`、`ToolParts` 阵列及 `locateToolCall`。中立端口 `ITranscriptPort` 将工具调用与结果融合为 `TranscriptPart.ToolUse`，未暴露私有部分。HNI 禁止将中立端口有损逆向包裹为伪 snapshot port，故必须等待下游工具链完成领域化抽象后方可切换。全仓仍有 44 个文件直接或间接引用 `ISessionSnapshotPort`。

2. **未迁移的生命周期核心构造链**：
   - **Fork 集群**：`HostForkRuntime` 构造函数接收 `sessions: ISessionHostPort`（在 `Change/Host/Host.fs`、`ToolRuntimeScope.fs`、`HostForkPtySurface.fs`、`JoinSurface.fs` 共 7 处实例化），`Agent.fs` 的 `forkNewChild` 仍待迁移至 `sessionPort.Create`。
   - **同步委派集群**：`SyncDelegateRuntime` 构造接收 `sessions: ISessionHostPort`（在 `SyncDelegate/Surface.fs` 与 `PluginSessionWiring.fs` 实例化），内部 `managedChildObservation` 依赖 `ChildSessionInfo list`，待改为 `SessionInfo list`。
   - **顾问席位 (Advisor)**：`AdvisorSeatDeps` record 声明 `Sessions: ISessionHostPort`（在 `ToolRegistry.fs:552` 构造）。**核心不匹配**：`AdvisorSeat` 依赖 `SubscribeTerminal` 监听子会话完结，而中立 `ISessionPort` 是纯无状态生命周期动词接口，**不含任何终端事件订阅**；终端事件订阅必须移入 **P-07 信号源或进程总线，绝不能塞进 P-01**。
   - **OpenCode 会话适配层家族树**：`InjectedSessionPort`（`Sessions.fs:421`）仅实现了 `ISessionHostPort`，未实现 `ISessionPort`。其内部持有的 `activeListeners`、`parentChildMap`、`childParents`、`knownRoots` 等家族树与祖先证明**必须留在领域层 (`Execution/Session`)，中立端口只执行宿主物理动作**。

3. **四项 Foundation 类型存续现状与 JS 字段名锁**：
   - HNI-10 slice C 已将 4 个名字就地改名（`OpenCodeChildOptions` → `ChildSessionOptions`、`OpenCodeChildInfo` → `ChildSessionInfo`、`OpenCodePromptOptions` → `SessionPromptOptions`、`OpencodeModel` → `HostModel`）。
   - **类型本身仍不能删除**：`git grep` 仍有 36 处引用 / 17 个文件。
   - **JS 字段名锁与 Shard 阻断**：Fable 转译出的 JS 属性名为 `providerID` / `modelID` / `variant`，而中立 `ModelRoute` 字段为 `Provider` / `Model` / `Variant`。类型名在 JS 中被擦除，但字段名不会。改字段名会打破冻结的 manifest 模块及约 13 个 JS 测试断言；且 `HostModel` 在叶子 shard `host-boundary.host-opencode-types`，消费它的 15 个 shard 中有 11 个未引用中立端口 shard，因此不能直接删除。

4. **`IOpenCodePort` 删除前置**：
   - `IOpenCodePort`（`OpenCode/Host/OpenCodeContract.fs(i)`，涉及 12 个文件 / 37 处）在 `InjectedSessionPort` 与其余 `ISessionHostPort` 消费方彻底切到 `ISessionPort` / `IPromptPort` 之前绝对不能删除。

5. **`HostPlugin.fs` 重复 stateRoot 计算未证等价**：
   - `Host/SecondHost/HostPlugin.fs:319` 仍有一处按 git worktree 顶层独立计算 `RuntimePath.stateRoot`，而 `ports.Paths.StateRoot` 则是适配器 memo 的 `RuntimePath.stateRoot workspace`。linked worktree 与主 worktree 计算未必相等，在未给出等价性证明前保留原样并在此显式登记。

### 6.4 omp 适配器骨架关键契约与生命周期门闩

- **类型系统显式映射**：
  - F# 的 `System.Threading.CancellationToken` 映射为 TypeScript 的原生 `AbortSignal`。
  - F# 的 `System.IDisposable` 映射为 `IDisposableHandle`（`{ dispose(): void }`）。
- **单次挂载门闩 (Activation Latch)**：
  - omp 的 `ExtensionAPI.on` 只追加监听器且无卸载 API。
  - 为防止重复注册导致同一事件多次分发，`activateOnOmp` 维护全局单例门闩，所有事件处理函数从动态插槽中读取活跃 hook。
  - `session_shutdown` 触发时清空插槽并执行 dispose，保证下次 `session_start` 能干净重新挂载。
- **传输收执为准入语义 (Admission Receipt)**：
  - `Prompts.Send` 成功时返回 `AdmittedWithReceipt`，收执格式为 `accepted-omp-<sessionId>`。
  - 该收执仅代表**宿主已接受排队投递**，绝非物理消息持久化 ID，严禁提升为权威根。
  - 投递分为准备期（校验不支持字段，失败为 `Fatal`）与投递期（宿主实际入队，异常为 `AcceptanceUnknown`）。
- **不支持字段校验先于关联写入**：
  - 在宿主会话路径与子会话路径上，不支持字段的校验（`Role` / `Route` / `Workspace` / `ToolPolicy`）必须**严格先于**向会话日志写入 `wxs.prompt-key` 关联条目。被拒绝的提示词绝不能在会话日志中遗留虚假幽灵关联。
- **类型化明确拒绝 (Typed Refusal)**：
  - 遇到宿主无法支持的能力，必须返回 `HostError`（`Unsupported` / `Refused` / `NotFound`），严禁静默假成功或静默丢弃参数。
  - 拒绝原因严格具备路径作用域区分（例如：`omp child prompts carry text only: cannot carry Role` 与宿主路径拒绝原因精确区分），子会话拒绝绝不冒充宿主会话拒绝。
- **Late 回调与终态语义**：
  - `late({ OwnedSettled })` 回调**仅在投递取得 `AdmittedWithReceipt` 结果时触发**。当宿主拒绝返回 `Retryable` 时，由于未达成确切排队，绝不触发任何 late 裁决。
  - 调用方提供的 late callback 在 delivery 内部 `try` 块之外执行，调用方自身的异常绝不会将已完成的投递误判为 `AcceptanceUnknown`。
- **投递可观察性不对称性**：
  - **Child 子会话路径可观察**：调用 `children.prompt` 返回 Promise 解析为布尔值，`false` 直接映射为 `Retryable`，执行异常直接 reject，投递结果物理可判定。
  - **Host 宿主会话路径不可观察**：omp 原生 `ExtensionAPI.sendUserMessage` 返回类型为 `void`。omp 将文本排入队列后不向调用方反馈，且若投递被内部拒绝也会被 omp 自身吞没。适配器无法区分“已入队”与“入队后失败”，因此 `accepted-omp-<sessionId>` 仅断言宿主接受了处理，不能当作送达见证。

### 6.5 CUT-36 真实 Smoke 测试执行规范与环境变量

真实 Smoke 用于验证 Fable 编译产物在真实第二宿主进程（`omp 18.12.4`）内运行，且必须断言**真实的物理事实**（宿主事件或磁盘物理文件），严禁依赖产品自身的自述报告。

*注意：本测试当前处于 DOING 状态，真实 Smoke 仍未通过，严禁声称已全绿。*

#### 离线断言测试与独立凭证重验（不消耗额度）
```bash
# 离线断言单元测试（覆盖 24 种 payload 结构及惰性产物、陈旧归属等负例，无需 omp 进程与配额）
node --test proposals/host-neutral-interface/smoke/assert-smoke.test.mjs

# 独立重新验证已有观察凭证与摘要（不启动新宿主）
node proposals/host-neutral-interface/smoke/assert-smoke.mjs \
  --observer <tmp>/cut36-omp-observer.jsonl \
  --summary <tmp>/cut36-omp-smoke-summary.json
```

#### 真实宿主 Smoke 运行命令（需显式 opt-in）
测试不包含在默认门禁（`npm test` / `verify.mjs`）中，每次真实运行在默认网关模型上消耗 **2 次模型请求**（父轮次 + 子委派）。必须显式设置环境变量触发：

```bash
# 1. 确保已在宿主机完成构建（若存在并发排队锁需先持锁）
node scripts/build.mjs

# 2. 显式开启环境变量运行真实宿主 Smoke（消耗真实模型额度：每次 2 次调用）
CUT36_OMP_REAL_SMOKE=1 \
  node proposals/host-neutral-interface/smoke/run-smoke.mjs
```

#### 全部 8 项环境变量控制
1. `CUT36_OMP_REAL_SMOKE`: 必须置为 `1`，否则直接以退出码 2 安全退出，不拉起 omp 进程。
2. `CUT36_OMP_BUNDLE`: 待载入的扩展产物路径，默认为 `<repo>/dist/Composition/Omp/OmpExtension.js`。
3. `CUT36_OMP_WORKSPACE`: 启动工作区路径，默认为仓根目录。
4. `CUT36_OMP_OBSERVER_OUT`: 观察者 JSONL 事件捕获输出路径，默认为临时文件。
5. `CUT36_OMP_PROMPT`: 真实测试执行的单轮提示词模板（包含随机 nonce）。
6. `CUT36_OMP_NONCE`: 本次运行生成的专属安全随机 nonce。
7. `CUT36_OMP_TIMEOUT_MS`: 真实宿主执行超时上限，默认为 `600000` (10 分钟)。
8. `CUT36_OMP_TOOL_PATTERN`: 产品委派工具识别正则，严格匹配 `^wxs_deliver$`。

#### 断言的 7 项物理事实（宿主观测与磁盘事实，缺损直接 FAIL，绝不 skip）
1. **产品产物真实载入**：宿主触发 `session_start` 事件（含匹配的 sessionId 与 cwd），且宿主在 `before_provider_request` 中向模型提供商宣告了匹配 `^wxs_deliver$` 的产品工具。若载入的产物为**惰性产物 (INERT)**（未注册工具）或仅匹配宿主内置工具（如 `task`），测试直接判定失败。
2. **真实模型请求发生**：必须捕获到 `before_provider_request` 载荷，且 assistant 响应触发了包含 `stopReason` 的 `turn_end`。
3. **双语资源真实抵达模型**：system prompt 与 `wxs_deliver` 工具描述中必须同时包含来自 `resources/provider/world/common-law/{en,zh-CN}.md` 的中英文句子，且用户提示词明确排除在中英文检查之外，覆盖 Anthropic、OpenAI Responses、Chat Completions、pi-ai 等全部载荷形态。
4. **真实子会话派生**：本次运行新建的会话文件中，子会话头 `parentSession` 必须等于本次父会话 ID，绝不扫描历史陈旧会话。
5. **提示词交付真实完成**：子会话自身的会话轨迹中必须包含携带本次 nonce 的用户消息以及模型的 assistant 回复。
6. **Join 汇聚真实完成**：父会话中观察到的 `wxs_deliver` 工具调用结果（`tool_execution_end`）必须包含同一 nonce，且所有产品工具调用严禁出现 `isError`。
7. **Work 与 Ownership 物理落盘且专属于本次运行**：
   - **主工作树路径精确解析**：通过 `git worktree list --porcelain` 与 `git rev-parse --git-common-dir` 精确解析主工作树的 `.Herdchestra` 目录。**若 Git 拓扑解析失败则严格报错退出 (fail closed)，绝不猜测路径**。
   - **持久化日志存在**：`<stateRoot>/wanxiangshu/work.sqlite` 必须存在且由 `node:sqlite` 可读。
   - **Ownership 流自动发现**：在 SQLite 的 `journal_entries` 表中自动发现以 `ownership:` 为前缀的 stream，不硬编码任何动态生成的 registry id。
   - **预运行基线锁定 (journalBaseline)**：在宿主启动前预先抓取 journal 各 stream 的最大修订号 `max(revision)` 形成基线快照。若基线缺失或损坏直接判定失败 (fail closed)。
   - **作用域与版本严格绑定**：Ownership 记录的 `scopeId` 必须等于 `host-activation:<workspace>`；子会话 durable work 流名称必须为 `host2-<sha256(childSessionId)[0:32]>`，且其 revision 必须严格大于预运行基线。

### 6.6 Scope / Pair Hint clean-break 的实证反例（2026-10-24）

本节记录这次迁移中实际出现过的错误、反例和修复方法，不建立另一份领域规范；具体承诺仍以 `requirements/context-compression/WHAT.md` [033]–[041]、`wanxiangli/src/pair-hint/AGENTS.md` 为准。以下验证来自离线构建和物理适配器测试，不等于真实模型并发调用已被证明。

- **先定注入时机，再设计接口。** 起初分别考虑过 OMP `tool_result` 和 OpenCode `tool.execute.after`；最终两宿主统一只在**发往 LLM 的请求副本**热修正。用户消息和已完成／失败工具结果是两条独立的文本写入线；真实工具回调结果、原始用户消息及持久化历史不承受提醒尾巴。Pair Hint 的 durable occurrence 事实仍由原 owner 管理，不能和这两条可见文本线混为同一种副作用。
- **搬走状态，也搬走判断。** `wanxiangli/pair-hint` 的自治 `PairHint()` / `ScopeSession` 私有保存每个会话的 prompt 身份、初始化状态和 `turnBudget`；`planRequest` / `planFilter` 只消费中立消息并返回保留槽位、后缀目标和弱提示。中立输入仅描述 `kind`、操作员归属、时间、调用 ID、`resultFor` 和 part 位置等；OMP `role/content` 与 OpenCode `info/parts/state.output` 只能在各自物理适配器中解码和回写。曾出现 `Surface → Protocol → library` 逐层转发和重复 raw filter，必须连旧实现、旧状态容器及全部消费者一起 clean-break 删除；不能把搬函数当成搬权威。HNI 中立计划不凭空增加 `P-xx` 宿主端口。
- **OpenCode 的数组身份和消息身份不同。** provider hook 的宿主持有原 `messages` 数组绑定，单纯 `output.messages = replacement` 可能对发出的请求无效；必须在原数组里替换元素（`HostMessageProjection.replaceMessagesInPlace`）。但被替换的 message、part、tool state 要复制，旧对象和持久历史不能被原地修改。物理测试应同时断言「数组引用不变」和「原消息对象的内容不变」。OMP context hook 走返回请求副本的另一种物理约定。
- **reset 的调用与结果必须按身份配对。** `SessionView.ofSnapshotMessages` 可把历史 tool call 与 tool result 装进同一条 assistant 消息的不同 parts。初版过滤只保留了旧 `resetScope` 调用，丢失其同消息结果；专门做出的两项反例先红后绿，修复是在中立 part 中携带 `resultFor`，按 toolCallId 同时保留历次 reset 调用／结果。最新 reset 同批之前的普通工作删去，同批之后的工作保留。注意**上下文中的排序与切分不证明真实工具执行屏障**，后者必须另做宿主实测。
- **用户消息不是 reset 边界。** 原始 operator prompt 与 steer 按序存活；新的真实操作员消息使旧预算失效，但 agent side-request、陈旧 snapshot 不得回退当前 prompt、重复扣预算或添加零预算 steer。「新用户消息后先估算首个状态闭环，再立即 `resetScope`」是强初始化指令；预算耗尽只是自然边界上的弱建议，不得打断正在做的工具工作，也不得误写成每回合强制 reset。
- **动态预算不能回写已呈现的历史字节。** 曾在每次 provider 请求中把所有旧工具结果的 `[Remaining turnBudget: N]` 重新计算成 `N-1`，实测同 epoch 的 reset 结果由 `6` 变 `5`，打断可缓存前缀。正确做法是让 `ScopeSession` 按工具结果身份固定**首次发往模型时**的尾巴，后续逐字重放；仅新结果显示新的预算，只有已提交 `ScopeReanchored` 才允许在冷 epoch 重建。新 operator steer 也不是重锚；agent side-request 不得先行冻结未来正常请求。该修复保留发送前热修正且不写真实输出；独立进程恢复原尾巴与真实 provider 命中率仍需单独观察，不能以离线字节测试冒充。反例见 `requirements/prefix-stability/tests/016.test.mjs` 及 OMP/OpenCode physical canary。
- **持久提交先于状态公布。** `resetScope` 只有在 `ScopeReanchored` 确实提交后，才能把新的预算和初始化事实写进内存 actor；反序会在 Journal 失败时制造虚假的已重置状态。会话生命周期由宿主发事实：OpenCode `session.deleted` 只清掉该会话的 actor，其他会话的预算不受影响。重复、失败、迟到和侧路事件都要有独立反例。
- **提示词用语和字节本身也是协议。** `oldScopeVerboseVerbatimExperience` 里的 `verbose` 允许不怕烦地写，`verbatim` 要求原文不改，`experience` 容纳尚未提炼为结论的经历；不要为了英语更规整换成 `handoff/excerpts` 或加上 `critical` 等筛选门槛。Pair Hint 的 `<system>` 包裹、NUL+BOM 分隔、历史 marker 原位重放、去重和剥离都有确定性语义，不能只以某个提示词「出现过」作为测试终点。
- **先打破假设，再验收。** 库内 `src/pair-hint/tests/` 验证中立规划和写入器；主仓 `requirements/context-compression/tests/038.test.mjs` 验证中立契约，`requirements/host-boundary/tests/omp-scope-seam.test.mjs`、`scope-hni-physical.test.mjs` 验证真实 OMP/OpenCode envelope、旧消息不变、同消息 reset 配对与会话删除。Fable union 和深层 dist 的物理表征只能放进明确列入 Host physical canary 的测试；普通语义测试不得靠扩大 `js-boundary-gate` 豁免获得绿灯。遇到红态先核对形状和调用时序，不为测试便利复活旧接口。
- **绿灯必须注明层级。** 库内单测、`node scripts/build.mjs` 的 Fable 编译、相关 WHAT、前缀重放、语言校验和 `node scripts/check.mjs` 分别验证不同事情；长命令 park/yield 不代表退出成功，需追踪真实进程及最终构建状态。TODO/GAP 不能记作通过。本包当时离线测试通过，却未用真实模型 canary 证实并发执行 barrier；静态检查还留有与本包无关的 `Host/FissionPort.fs` namespace-tier 问题。这些边界只能如实记账，不能说成全绿，更不能顺手改掉别人的 P-17 来掩盖。
- **两仓交付按职责精确暂存。** 修改前检查两棵工作树、未提交 diff、子模块指针和正在运行的构建。混有 Advisor/Delegation 等其他人的改动时只暂存属于本包的文件或 hunk，不使用一把 `git add -A`；先确认 `git diff --cached --check` 和暂存清单，再提交库、推送库、提交主仓精确 gitlink，均不强推。此次迁移凭证为 wanxiangli `e48678d`、wanxiangshu `89bc2739b`，只说明 2026-10-24 的已提交状态，未来使用时仍要重新验收。


### 6.7 跨参与者只传当前指令：借问句复用父 prefix（2026-10-24）

- **上下文不是移交物，而是协商来源。** 父→子、SyncDelegate、DevOps、Advisor 只给当前 charge/intent，不交 post-scope 历史、TOML `[[context]]`、`attached_view` 或过滤后的背景副本。Fork 的 typed `ForkChildPayload` 和 HNI `DelegationEnvironment` 不能再保留可读取这类背景的参数或能力；`LlmFacing.instructionsOnly` 是最后一道纯语义净化门。子代理确有歧义，使用 `negotiate(questions: string)` 集中提出多个问题，不靠吞下整段历史猜测。
- **第一次必须问，后续随时可问。** 受委托方的首次 `negotiate` 是强制步骤，发问即得到投递受理回执，不同步等待。只有带精确回执、代际、接收方身份的真实回答 steer 进入接收方请求，才解除首轮 `resetScope` 抑制；以后 `negotiate` 可选且绝不暂停正常 `resetScope`。同一对委托／受委托会话复用不重置首轮协商，换委托者或删除会话必须废弃旧回执。
- **缓存前缀复用应发生在 provider 物理边界。** 从委托者最后一次实际出站请求扣留 system、messages、model（包括已观察的 variant）**只读副本**；临时孙会话在 OpenCode 层只存一个协商问题，发 LLM 前在两种 transform 中把父 system、messages 原位热替换，并把唯一问题追加在末尾。保持原数组身份、既有字节和父会话历史不变；任一快照缺失必须拒绝复用，不能临时猜另一模型或重新渲染旧材料。父子层级本身不等于 provider cache 命中，最终命中仍要读真实 provider 数据。
- **工具定义不为临时 BTW 改动，执行绝不越界。** 临时会话使用父方实际模型/agent 配置，不通过 `tools: {}` 破坏前缀；但 `tool.execute.before` 对该会话任意工具调用必定在副作用前拒绝。记录拒绝事实，再用提示让临时回合重新尝试文本回答；任何原始工具体都不得跑。SDK 报错、无答案、迟到问答与用户消息落地必须分别验收；不能以发问受理回执冒充 answer steer。
- **测实际连接而不只测 helper。** 库内 `src/pair-hint/tests/008.test.mjs` 验证协商代际和首次/后续状态；主仓 `requirements/host-boundary/tests/scope-hni-physical.test.mjs` 应经实际注册的 OpenCode hooks 检查父前缀热替换、工具前置拒绝、重试、物理 steer 和首轮 reset 激活；Fork/Advisor 的真实物理 prompt 还须断言不携带父历史。离线字节相等不自动证明提供商真实缓存率，临时会话是否 UI 不可见也不能仅凭 parentID 推断。

---

## 7. 迁移来源文件全量清单与保留/删除边界

迁移指南已合入本文件，保留既有协作规则，不再维护独立指南；正式需求仍按各自合同维护。

### 7.1 已合并并删除的迁移文档来源
以下为来源索引，不是仍可访问的路径或第二套指南：
- **`proposals/extraction-kanban/` 体系**：
  - `README.md`, `BOARD.md`, `CARDS.md`, `COVERAGE.md`, `TEMPLATES.md`
  - `cards/` 目录下全部 44 个单卡文件：`CUT-00.md` ~ `CUT-08.md`, `CUT-09.md`, `CUT-09-a.md`, `CUT-09-b.md`, `CUT-10.md` ~ `CUT-41.md`
  - `proposals/extraction-kanban/evidence/` 目录下全部迁移分析 Markdown；可执行夹具（如 `CUT-09-b/probe-tampered-witness.mjs`、`CUT-19/shadow-*.mjs`）与原始测试数据不属于本次删除范围。
- **生命周期裁决文档**：
  - `proposals/lifecycle-cutover-closeout.md`
- **`wanxiangli/` 子模块根迁移指南**：
  - `wanxiangli/MIGRATION.md`
  - `wanxiangli/施工顺序.md`
- **`proposals/host-neutral-interface/` 体系中所有文档说明**：
  - `BOARD.md`, `REGISTRY.md`, `DESIGN.md`
  - `adapters/omp/README.md`
  - `smoke/README.md`
  - `evidence/` 目录下全部分析文档：`session-port-leftovers.md`, `adapter-thinness-gate.md`, `hni14-convergence.md`, `omp-feasibility.md`, `omp-provider-params-contract.md`, `vs-006-root-cause.md`

### 7.2 本次保留的资产
以下资产不属于本次文档删除范围；后续仍按各自需求维护：
- **可执行脚本与门禁**：
  - `scripts/checks/adapter-thinness.mjs`
  - `scripts/checks/architecture.mjs`
  - `scripts/checks/fsharp-control-pyramid.mjs`
  - `scripts/checks/domain-seam-baseline.json`
- **HNI 适配器实现与代码生成器**：
  - `proposals/host-neutral-interface/adapters/omp/gen-ports.mjs`
  - `proposals/host-neutral-interface/adapters/omp/mirror-check.mjs`
  - `proposals/host-neutral-interface/adapters/omp/ports.ts`
  - `proposals/host-neutral-interface/adapters/omp/adapter.ts`
  - `proposals/host-neutral-interface/adapters/omp/sdk-children.ts`
  - `proposals/host-neutral-interface/adapters/omp/session-files.ts`
  - `proposals/host-neutral-interface/adapters/omp/check.sh`
  - `proposals/host-neutral-interface/adapters/omp/tsconfig.json`
  - `proposals/host-neutral-interface/adapters/omp/fixtures/fixture.test.ts` *(用户修改严禁触碰)*
  - `proposals/host-neutral-interface/adapters/omp/fixtures/events.json`
- **Smoke 测试套件可执行源码**：
  - `proposals/host-neutral-interface/smoke/run-smoke.mjs`
  - `proposals/host-neutral-interface/smoke/assert-smoke.mjs`
  - `proposals/host-neutral-interface/smoke/assert-smoke.test.mjs`
  - `proposals/host-neutral-interface/smoke/observer-extension.mjs`
  - `proposals/host-neutral-interface/smoke/omp-api-conformance.mjs`
- **独立库规范与 API**：以 `wanxiangli/AGENTS.md`、各职责局部规则和公开类型为准；迁移指南不固定另一份库侧文档清单。
- **主仓现有配置与独立任务文档**：
  - 本文件的既有协作规则；迁移合同已直接合入本文件，不再安排独立指南替换。
  - 其他任务的交接材料与无关产品提案，不因名称包含“迁移”而删除。
