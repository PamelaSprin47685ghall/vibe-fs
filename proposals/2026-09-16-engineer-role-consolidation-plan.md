# 新角色体系重构：最终施工方案

日期：2026-09-16  
版本：审订定稿，已纳入「fission 仅 Engineer 允许」  
项目：wanxiangshu  
源码审阅基线：`master / 2575b41d7`  
交付边界：施工方案。保存本文不等于已修改生产代码、通过测试、提交或发布。

## 0. 阅读约定与本次审订结论

本文整合本次讨论中的全部明确修订，替代此前施工草稿。已确认要求直接作为目标合同；尚未由用户明确指定的落地选择集中列出，不混写为用户决定。源码事实来自上述提交的静态阅读，不代表对应运行行为已经验证。

本次审订不只从权限表删除 Manager 的 Fission，还同步修订角色提示词、任务分工、恢复入口、案例归档与验收测试。四项关键校正如下。

1. **Fission 是 Engineer 专属能力。** Manager、Orchestrator、DevOps、Blogger、Bookkeeper、Predictor 以及其他非 Engineer 执行身份一律禁止。DevOps 的工程能力更强，不等于继承 Engineer 的 Fission 权限。
2. **Manager 的并行来自派出多名 Engineer，不来自 Manager 分身。** 旧稿中「Manager 分身共享 DevOps」不再是新系统的正常流程；相关旧状态只进入拒绝、终止与历史读取测试。
3. **案例主来源严格为 Engineer 工作轨迹。** 不把「DevOps 是高级 Engineer」自动扩大为 DevOps 轨迹也入库。DevOps 后续修复造成的文件变化，可以自然进入相关案例的 diff 维护。
4. **Engineer Fission 不增加案例归档主体。** 各 lane 的实质文件访问合并到同一逻辑工作；全部收敛后取得结束文件状态，对 Manager 只交付一次完成，对案例管线只交付一次来源。

不在本次施工中增加新 Browser 替代服务、新调度框架、新案例存储系统、操作系统级文件访问追踪或严格依赖证明系统。

## 1. 已确认的最终合同

### 1.1 角色与程序

| 对象 | 有权完成的工作 | 明确边界 |
| --- | --- | --- |
| Orchestrator | 战略战役统筹、独立道路委任、取舍与汇聚 | 不亲自修改源码，不直接调度 Engineer 或 DevOps，不 Fission |
| Manager | 独立评估、任务分解、派工、推进未尽账本、组织接力 | 不亲自调查或修改工作树，不 Fission，不逐次审批常规局部修复 |
| Engineer | 本地事实调查，读取、创建、修改、删除文件，实现与重构，编写测试源码 | 不执行真实命令，不差遣 DevOps；完成本次工作即返回；不承担 Browser 职责；唯一具备 Fission 角色能力 |
| DevOps | Engineer 的本地工程能力，加真实命令执行、终端和进程管理；主动完成非架构级修复 | 每个 Manager 固定配一名，经 resume 调用，不允许 fork DevOps；自身不 fork、不 Fission，不擅自作架构或产品决策 |
| Sphinx | 程序控制探究流程、工作项、预算、续行和收束；可以同步调用 Engineer 调研 | 不是 Role、Persona 或普通 subagent；没有独立 Fission 身份，不保留 Inquiry 驾驶层 |
| Blogger | 保留既有工作历史和上下文职责 | 不取得组织指挥权，不接管 Distiller |
| Bookkeeper | 根据 Engineer 轨迹整理案例；根据文件 diff 维护旧案例 | 更新时不读仓库、不读完整文件、不重放历史观察、不另行调查；不 Fission |
| Predictor | 保留既有内部预测、模型强度相关机制 | 不进入普通调度，不获得工程、执行或 Fission 权限 |

活跃角色路径删除：Coder、Inspector、Browser、Inquiry、Distiller。Bookkeeper、Predictor 可沿用现有内部身份表达，不为了凑齐名称而强行加入普通 Role 枚举。

### 1.2 普通工作链

```text
Orchestrator
  ├─ Manager：道路 A
  │    ├─ Engineer × N
  │    │    └─ 可选 Fission：同一 Engineer 的多个 execution lane
  │    └─ 固定 DevOps × 1：resume 调用
  └─ Manager：道路 B
       ├─ Engineer × N
       └─ 固定 DevOps × 1：resume 调用

Engineer → 完成本次工作 → Manager
DevOps → 执行／自行局部修复／重新验证 → Manager
```

Engineer 不直接调用 DevOps，也不经包装工具、同步委托、转发任务等方式间接差遣 DevOps。普通 Engineer 和 DevOps 不互相创建工程子代理。需要进一步组织工作时由 Manager 决定。

Manager 接到 Engineer 的结果后，决定派 DevOps 验证、继续派 Engineer、调整任务或组织接力。Engineer 的源码工作完成，不等于整个目标已经验收。

### 1.3 DevOps 默认修复授权

非架构级修复是 DevOps 的角色固有授权，而不是 Manager 每次任务的可选授权。

执行中发现局部问题时，DevOps 应自行调查、直接修改源码、补充必要的回归测试，再运行验证。Manager 只写「运行这组测试」而没有写「允许修复」，不构成禁止修复。

不能新增 `allowRepair`、`managerApprovedMutation`、`canFixAfterFailure` 等逐次批准字段。不能保留「只有 Manager 明确允许才可以改源码」的旧提示词暗门。

授权不意味着无限范围：明确的只读约束、用户限制和安全边界仍然有效。不得削弱断言、绕过门禁、发明产品含义、改变架构边界、兼容性或安全政策来制造绿色结果。

「非架构级」不是「仅限一行」「仅限一个文件」，也不要求只有唯一机械操作才可动手。DevOps 可以在既定需求和目标内作普通工程判断；需要选择新的系统责任、公共语义或产品规则时，交回 Manager。

### 1.4 案例合同

```text
Engineer 轨迹：案例来源
实质访问文件：可能相关的线索
轨迹结束完整文件状态：初始基线
后续文件 diff：Bookkeeper 的维护材料
```

读取、创建、修改、删除文件均纳入关联。移动或重命名关联旧、新两条路径。局部读取也关联整个文件，不维护行级依赖。

grep、glob、目录列表、只返回位置的导航、回答中提到的路径，不构成实质访问。文件关系是粗粒度线索，不求最小依赖，不求严格重放，不证明案例仍然正确。

完整文件状态复用用户已指出的现有能力。首次归档时 Bookkeeper 根据 Engineer 轨迹形成案例；后续更新时，外部变化材料只提供真实 diff，不提供完整新旧文件或仓库调查权限。Bookkeeper 仍可读取、编辑正在维护的案例自身。

### 1.5 Browser 与 Distiller

Browser 职责不给任何人。删除 Browser 角色及仓库拥有的 Browser MCP 集成，未来专业工具另立需求，不在这次留空壳或替代代理。

Distiller 删除。原先需要模型蒸馏的大输出按预算截头取尾，保留明确截断声明与程序事实。Blogger 保留，但不改名承担原 Distiller 工作。

## 2. 为执行采用的有界选择

以下是施工选择，不是新增的用户已确认要求。变更它们应局部改方案，不重开已经确认的主体合同。

| 项目 | 本方案选择 | 理由与界限 |
| --- | --- | --- |
| 固定 DevOps 跨任期存续 | 同一道路沿用一个逻辑 DevOps，当前有效 Manager 拥有控制权 | 保留终端和环境连续性；不把物理 Session 当唯一性根。若产品最终要求每任新建，应连同进程交接合同一起改 |
| 案例初版来源 | 只接 Engineer，包括其收敛后的 Fission 工作 | 不未经确认扩大到 DevOps；DevOps 修改仍作为已有案例的后续文件变化 |
| Sphinx 的公开调用者 | 第一版由 Orchestrator、Manager 调用高层工具 | 不让普通工程工作递归组织探究；以后开放其他调用者须独立改权限与预算合同 |
| Sphinx 内部 Engineer | 本次调用只读，不 Fission，不递归，不执行 | Engineer 是 Fission 唯一可能获权的角色，但不表示每次内部任务都授予全部能力 |
| 文件变化检测 | 第一版复用案例 fetch／维护入口按需检测 | 不做全仓监听或每次保存触发；多次变化可以合并为净 diff |
| 新 DevOps 创建 | Runtime 在道路初始化时绑定，模型只见稳定 Byname | 不增加 ensure-devops 工具，不让 Manager 借首次 fork 绕过禁令 |

明确不默认扩张：不自动清空用户旧数据、不重写历史事件、不卸载用户全局浏览器、不修改无关 MCP、不自动提交或推送。

## 3. 当前源码接点与证据范围

以下位置在 `2575b41d7` 上已经静态核对。路径用于定位，不表示生产代码已经按本文修改；本轮没有执行这些功能的运行测试。

| 编号 | 已核实的现状 | 施工后果 | 源码或规范 |
| --- | --- | --- | --- |
| C01 | Manager、Coder、Inspector、Browser、Inquiry 当前都有 Fission；DevOps 没有直接 Write/Edit/Move/Remove | 收拢权限，不能只改展示文案 | `src/Wanxiangshu/Foundation/OfficeCapability.fs`，`permissions`、`permissionsForManagerFacts` |
| C02 | Fork 权限同时展开为 fork 和 resume；工具表含旧 js-* 与 Browser MCP | 分开创建、续用能力，清理旧工具投影 | `src/Wanxiangshu/OpenCode/Tools/StaticTools.fs`，`toolNames`、`knownToolNames` |
| C03 | engineer 仍是 Coder Persona 别名；Manager 可 fork 五种旧角色 | 建立真正 Engineer 身份，Manager fork 集合只留 Engineer | `src/Wanxiangshu/Participant/Persona/ManagedCatalog.fs` |
| C04 | DevOps 提示词鼓励机械修复，但要求托付给 Coder | 改为直接修复，保留真实验证原则 | `resources/provider/role/devops/zh-CN.md` |
| C05 | Fission 的角色门禁从 OfficeCapability 投影；规范仍举 Manager 等为可裂变角色 | 保留单一权限源；改规范、运行门禁、恢复检查和测试 | `src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs`；`requirements/intra-participant-parallelism/WHAT.md` |
| C06 | Fission 采用 fresh sibling、原子准入、单逻辑完成、单 active group；未完成 group 的重启不做隐式恢复 | 不为本次角色迁移另造 Fission 恢复系统 | 同规范 001—015；`src/Wanxiangshu/Execution/Fission/Admission.fs` |
| C07 | Case 主模型仍是 Q/A 与 FileRead/GlobResult/GrepResult，带严格集合比较 | 替换观察合同，而非修补旧 replay | `src/Wanxiangshu/Repository/Knowledge/Casebook/Model.fs` |
| C08 | 案例归档按 Inspector 会话作用域收集并 finalize | 改到一次完成工作的逻辑范围，不能只用 SessionId 作 CaseId | `Casebook/Lifecycle.fs`、`SessionDraft.fs`、`Settlement.fs`，均在上述 Casebook 目录 |
| C09 | Bookkeeper 的 repository_change.patch 是哈希和搜索观察的文本；更新前后 replay 校验稳定性 | 改成真实 diff；删除前后 replay 循环 | `Casebook/BookkeeperRuntime.fs`、`Casebook/Bookkeeper.fs` |
| C10 | JS grep 会向事务 ReadSnapshots 加入扫描文件；现有文件观察接点发生在提交之前 | 案例访问不能直接复用全部 ReadSnapshots，也不能把提交前意图算成修改完成 | `src/Wanxiangshu/Repository/Programming/Js/ToolsBindings.fs`、`OpenCode/ToolWorkflow.fs` |
| C11 | Sphinx MCP 仍让调用者根据 yield/nextTool 续行；GecHost 有派发和中止计划构造 | 把驾驶循环放进程序；计划不等于真实派发或回收 | `src/Wanxiangshu/Sphinx/McpServer.fs`、`GecHost.fs` |
| C12 | Distillation 已有滚动尾部算法，之后仍创建 Distiller | 复用有界读取，删除模型运行时 | `src/Wanxiangshu/OpenCode/Tools/Distillation.fs`；`src/Wanxiangshu/Process/Spool.fs` |
| C13 | Browser MCP 有专属启动器、配置注入和环境变量 | 删除接线，不只设置 disabled | `src/Wanxiangshu/OpenCode/Host/StealthBrowserMcp*.fs` |
| C14 | build 由 node 脚本执行；标准 runner 检查构建新鲜度并支持显式文件集合 | 用正式入口验收，不跑 dotnet build 或跳过 freshness | `scripts/build.mjs`、`scripts/verify.mjs`、`requirements/verification-system/tests/run.mjs` |

用户已经明确「轨迹结束时保存完整文件状态，现有实现已具备」。本方案据此优先复用；但上述 Casebook 主路径本身不能证明该能力已经接通。P0 必须找到现有完整状态拥有者、存储引用和 diff 接口，并用正式测试证明结束基线。未核实接线不等于宣称能力不存在，也不构成重建存储的理由。

## 4. 三张必须一致的目标矩阵

### 4.1 角色能力矩阵

「允许」表示角色能力上限；真正执行还要满足任务、来源、生命周期和用户约束。「禁止」必须在运行入口生效，不只从 schema 隐藏。

| 能力 | Orchestrator | Manager | Engineer | DevOps | Blogger | Bookkeeper | Predictor |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 委任独立 Manager 道路 | 允许 | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 |
| fork Engineer | 禁止 | 允许 | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 |
| fork DevOps | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 |
| resume 固定 DevOps | 禁止 | 允许 | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 |
| 本地文件调查 | 禁止 | 禁止 | 允许 | 允许 | 禁止 | 禁止 | 禁止 |
| 创建／修改／移动／删除源码 | 禁止 | 禁止 | 允许 | 允许 | 禁止 | 禁止 | 禁止 |
| 真实命令、终端和进程控制 | 禁止 | 禁止 | 禁止 | 允许 | 禁止 | 禁止 | 禁止 |
| Fission | 禁止 | 禁止 | 允许 | 禁止 | 禁止 | 禁止 | 禁止 |
| Browser／网络调查工具 | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 | 禁止 |

Blogger 仍有自己的 chronicle 能力；Bookkeeper 仍能操作案例 staging；Predictor 保留其内部机制。这些不等于通用仓库读写。DevOps 的 join/horizon 按它拥有的进程和工作句柄解释，不由此获得代理创建或差遣权。

### 4.2 Fission 准入矩阵

```text
允许 Fission
  = 已证明的 CanonicalRole 为 Engineer
  ∧ 本次执行授权包含 Fission
  ∧ 已有 subsession 来源条件成立
  ∧ 没有活跃 Fission group
  ∧ 其他现有准入条件成立
```

唯一可赋予 Fission 的角色是 Engineer；角色名、Persona 别名、工具参数、自称身份和被附上的 Engineer 工作记录均不能授予该能力。

| 执行场景 | 结果 |
| --- | --- |
| 普通 Engineer 子会话，满足现有条件 | 允许 |
| Manager／Orchestrator／DevOps | 一律拒绝 |
| Blogger／Bookkeeper／Predictor／未知或内部辅助身份 | 一律拒绝 |
| Engineer 根会话 | 保留现有 subsession-only 限制，拒绝 |
| 已经在活跃 Fission group 中的 Engineer lane | 保留非递归限制，拒绝 |
| Sphinx 内部只读 Engineer | 本方案调用授权不含 Fission，拒绝 |
| 历史 Manager Fission 状态、旧缓存或旧提示词 | 可以读历史，不得据此重新创建 lane 或取得权限 |

### 4.3 调度与并行矩阵

Orchestrator 的多道路委任、Manager 同时派出多个 Engineer、Sphinx 程序组织独立调研、DevOps 管理多个进程，均不是 Fission。

Fission 只表示同一个 Engineer 的多个 execution present。不能通过把 DevOps 临时包装成 Engineer、复制 Manager 的身份和控制权、或复用旧 lane 接口绕过角色限制。

## 5. 施工依赖与分工

```text
P0  规范、现有完整文件状态接线、失败测试
 ↓
P1  身份、权限、Role Prompt、Engineer-only Fission
 ↓
P2  固定 DevOps：初始化、resume、接力、执行与自修
 ↓
P3  Sphinx：程序化工作流、同步 Engineer
P4  案例：轨迹、实质访问、结束状态、diff 维护
P5  输出：删除 Distiller，预算截尾
P6  Browser：角色、MCP、配置和资源删除
 ↓
P7  历史、辅助机制、编译目录、分发、跨模块验收
```

P1 公共类型先确定。P3—P6 可由不同 Engineer 并行；同一文件、共享类型、公共编译清单必须由明确负责人串行收口。不要为了并行先做四套临时兼容接口。

Manager 通过 fork 多名 Engineer 分工，自己不 Fission。Engineer 可在任务内使用 Fission，但同一文件和存在顺序依赖的修改仍需串行。固定 DevOps 执行测试并自行修复非架构级问题；公共设计改变由 Manager 重新安排。

每一阶段交付：规范变化、正式回归测试、源码实现、实际验证记录、删除清单和剩余阻塞。文档保存与生产实施分开；提交和发布另需明确授权。

## 6. P0：先固定合同、存储接线与失败行为

### 6.1 修订规范，不让总方案成为第二套产品权威

本文负责跨包导航，产品语义落回 `requirements/<package>/`。按照仓库惯例更新 WHY → WHAT → 正式测试 → HOW → 实现 → GAP；不要只在总说明写「新方案优先」，却让旧 WHAT 保留相反要求。

| 规范包 | 必须修改的合同 |
| --- | --- |
| office-capability | 五分法撤销；Engineer 调查与实现；DevOps 直接工程能力和角色固有修复授权；删除 Manager Fission |
| participant-identity、participant-horizon | Engineer 真身份、旧角色退出、Manager fork 仅 Engineer、固定 DevOps 的可见语义 |
| capability-enforcement | Fork/Resume 分离；Fission 仅 Engineer；内部身份与动态入口拒绝；DevOps 写入；无 Browser 权限 |
| delegation | Engineer 完成即返回；无 Engineer→DevOps；无 DevOps→Coder/Inspector；Sphinx 程序内部同步 Engineer |
| intra-participant-parallelism | 012 的角色集合改为仅 Engineer；保留 subsession、非递归、原子准入和一次完成；移除活跃 Manager 场景 |
| relay-incumbency、relay-retirement、relay-context-projection | 删除 Manager 分身前提；固定 DevOps 的任期控制权与工作记录交接 |
| relay-assessment、change-integration | Engineer 调查支持独立评估；DevOps 修复改变快照；汇聚后重新验证 |
| epistemic-reasoning | 无 Inquiry；Sphinx 程控续行；内部 Engineer 只读、同步、有预算、可取消 |
| knowledge-reuse、semantic-trace、work-record | 一次逻辑工作归档；Fission 合并访问与轨迹；完整结束状态；diff 维护；无严格 replay |
| repository-programming、js-semantic-surface、repository-investigation | 新 Engineer 文件工具；实质访问与事务 ReadSnapshots 分开 |
| output-distillation、process-execution | 删除模型蒸馏；保留 spool、程序事实、有界尾部和明确截断 |
| external-investigation | 撤销 Browser 功能，不转交其他角色 |
| managed-session-lifecycle、interaction-authority、crash-reconciliation | 身份替换、旧状态收束、固定操作员恢复、内部权限收窄 |
| execution-model-routing、feature-ablation、provider-language、distribution | 角色和模型资源、双语 Prompt、工具映射、功能目录、打包同步 |

撤销的条款可以在需求演进记录中说明，但不能在活跃 WHAT 中同时保留相反行为。同步维护 `requirements/INDEX.md`、适用范围与需求测试锚。

### 6.2 定位现有完整文件状态

在写新存储类型前，沿当前状态保存路径回答三个问题：完整内容由谁产生，如何持久化并引用，怎样取得两个状态之间的 diff。记录真实模块与函数，不猜名称。

必须先有正式契约测试：

```text
本次工作读取 A
本次工作把文件改成 B
逻辑工作结束并保存完整状态
之后其他工作把文件改成 C

断言：案例初始基线是 B；更新材料是 B → C。
```

若发现现有保存能力只在会话关闭时触发，把接点迁到工作完成边界；若它保存了完整内容但 Casebook 未引用，接通引用。只有证实确有缺失，才补最小必要的缺口，不能先造平行快照仓库。

### 6.3 第一组必须变红的测试

先分配正式 WHAT 编号，新增仓库惯例的 `NNN.test.mjs`；不要以一次性探针代替回归测试。

第一组锁方向：Engineer 真身份；DevOps 直接写入和默认修复授权；所有非 Engineer 的 Fission 被拒；Manager 不可 fork DevOps；Engineer 无真实执行与 DevOps 调度入口；grep 不入案例关联；Bookkeeper 收到真实 diff；大输出零 Distiller。

退出条件：目标合同与失败测试已经落盘，现有快照接线有证据，后续实现不会沿旧五分法继续加功能。

## 7. P1：角色身份、权限、提示词与 Fission 一次收拢

### 7.1 身份与名字

主要位置：

```text
src/Wanxiangshu/Foundation/Roles.fs / .fsi
src/Wanxiangshu/Foundation/RolesSurface.fs / .fsi
src/Wanxiangshu/Participant/Persona/Catalog.fs / .fsi
src/Wanxiangshu/Participant/Persona/ManagedCatalog.fs / .fsi
src/Wanxiangshu/OpenCode/Tools/ManagedAgent.fs / .fsi
src/Wanxiangshu/Resources/PromptResources.fs
```

施工顺序：先定义 Engineer 与合法调用集合；再迁移调用方；最后删除活跃旧身份、别名和错误建议。不能把外面显示的名字换成 Engineer，内部仍依赖 Coder 的 Persona、模型或权限分支。

Manager 的公开 fork 候选只保留 Engineer。DevOps 仍有正式身份和模型配置，但创建权归合法 Runtime 绑定流程；不能通过普通 root/primary 入口另起一个不受管理的 DevOps。

Bookkeeper、Predictor 沿用内部身份边界。历史数据识别与活跃名字解析分开，旧 Inspector 不得静默升级成可写 Engineer，旧 DevOps 不得被误解析成可 Fission 的 Engineer。

### 7.2 文件、执行和委托能力

Engineer 与 DevOps 共享本地工程能力集合：Read、Write、Edit、Glob、Grep、Move、Remove。复用能力集合不等于建立角色继承树；尤其不得用 Engineer 全权限集合直接派生 DevOps。

真实执行 Exec、Pty 仅给 DevOps。检查 `run`、`query-shell`、terminal、JS 包装层与内部 ToolSpec 的门禁，Engineer 不能绕过上层 schema。

拆开 Fork 与 Resume，保留已定义的外部语义。删除失去用途的 Inspect、Behavior 和对应普通同步委托工具；保留通用 SyncDelegate 设施中 Sphinx 真正需要的部分，不一刀切删除公共生命周期代码。

生成 `js-engineer`，更新 `js-devops` 的文件能力。JS sandbox 仍受原有模块、文件系统与执行限制，不能因为 Engineer 获得写权限就允许任意 shell、网络或宿主导入。

### 7.3 Fission 的完整修改面

修改 `OfficeCapability.permissions`，只在 Engineer 分支授予 Fission。Manager 所有任期状态，包括完整能力、评估前、接责后、清理窗口，都不能重新得到 Fission。

修改 `Execution/Fission/OpenCode/Tool.fs` 的准入投影及其依赖，确保宿主入口消费已证明的执行身份和本次能力，而不是可伪造的工具参数。

继续使用一个权限源投影至：Host agent 配置、provider tool map、工具可见 schema、工具实际 admission、内部直接调用与恢复续行。不要维护第二张「Fission 特批角色表」。

保留已有 N≥2、fresh sibling、all-or-none、原 caller silent interrupt、单 active group、keyed convergence 和单 logical completion。它们是 Engineer 内部并行的已有合同，不因删 Manager 而重写。

旧 Manager Fission 状态不隐式恢复成 Engineer。保留历史事实，按既有中断／退休规则结束活跃权能；不能让晚到 terminal 或旧 lane 缓存重新打开管理、执行或 Fission 权限。当前规范已不自动恢复未完成 group，本次不增加自动恢复系统。[C05、C06]

原有面向 Manager 的 lane affinity、派工广播或 DevOps 共享特例，逐项判断：仅服务旧 Manager Fission 的生产分支删除；仍服务 Engineer 合法行为或历史解码的部分保留在正确边界。不要用「通用」掩护不可达活跃路径。

### 7.4 Role Prompt 可直接使用的核心段落

中英文一起改。以下是必须保留的语义，不要求多处逐字复制；Manager 认知、角色自我模型、工具说明应同源一致。

#### Engineer

> 你负责本地事实调查与源码工作，可以读取、创建、修改、移动、删除文件，完成实现、重构和测试源码。你不执行真实命令，不调用或差遣 DevOps。需要运行验证时，交回已完成工作和待验证事项，由 Manager 决定下一步。
>
> 你是唯一允许使用 Fission 的角色。Fission 是同一 Engineer 的多路执行，不是创建新的独立代理；各路仍承担本次工作，必须收敛成一次返回。相互依赖或重叠写入的任务不能靠 Fission 消除顺序要求。
>
> 本次工作完成或到达需要 Manager 决策的边界时，立即返回，不额外组织验证链或等待案例整理。你不承担外部浏览职责。

#### DevOps

> 你具有完整的本地工程能力与真实执行能力，不是只接受命令字符串的执行器。执行中发现非架构级问题时，应自行调查、直接修复源码、补充必要回归测试并重新验证。此权限由角色直接授予，不要求 Manager 在本次任务中另行授权。
>
> 不要仅为了报告一个可以自行解决的中间失败而停止。不要发明架构、产品、兼容性或安全政策，也不要弱化验证来获得成功；到达这些边界时交回 Manager。明确只读约束与用户限制仍须遵守。
>
> 你不能 Fission，不创建或差遣其他工程代理，不 fork DevOps。你可以管理多个真实进程；进程并发不是代理分裂。只有真实执行事实才能支持运行结论。

#### Manager

> 你管理任意数量的 Engineer 与唯一绑定的 DevOps。通过 fork Engineer 获得并行，通过 resume 固定 DevOps 获得执行和局部修复。你不能 Fission，不能创建自己的管理分身，也不能 fork 第二个 DevOps。
>
> Engineer 完成后向你返回。DevOps 已有非架构级修复授权，你应给出目标、约束和验收要求，不必逐项批准局部修复。不要把它降格为命令包装器，也不要把架构决策藏进顺手修复。
>
> 你负责独立判断结果、推进未尽账本、组织后续工作与接力，不亲自调查、修改或运行工作树。

#### Sphinx 内部 Engineer 的调用附加约束

> 本次调用仅调研现有本地事实。你没有修改、执行、DevOps、Fission 或递归 Sphinx 的调用权。完成指定调研后返回程序调用点，后续步骤由程序决定。

Orchestrator、Blogger、Bookkeeper、Predictor 的 Prompt 或内部能力定义同步明确无 Fission。不要再留「Manager 可分身」「DevOps 因能力更强可分身」的示例。

退出条件：真实身份、能力矩阵、schema、实际入口、双语 Prompt 和错误提示全部一致；权限测试覆盖正常调用和伪造／恢复入口。

## 8. P2：固定 DevOps 的绑定、接力和直接修复

### 8.1 唯一性与创建

按第 2 节施工选择，以稳定道路拥有一个逻辑 DevOps，当前有效 Manager 获得控制权。初次创建由 Runtime 进行，并持久化绑定；模型获得稳定 Byname，之后只 resume。

复用现有逻辑 owner、身份、模型绑定、handle、PromptAuthority 和事件设施。若缺少绑定事实，只新增必要的绑定／转交事实，不建立 DevOps 专用队列、租约框架或第二调度状态机。

DevOps 的物理会话可以因故障恢复而替换，但一个逻辑操作员不能同时对应两个可执行权威。初始化幂等、接收不明、晚到创建结果和重复恢复都是唯一性测试对象，不再测试「Manager 分身正常共享」。

### 8.2 resume 与忙碌处理

保留当前 DELEG-024、026、027 的合同：resume 等待 exact assignment 接收确认后返回承接后果，工作结果由 join/horizon 获取；同一路同时至多一个 active work unit。

DevOps 忙时明确拒绝新的 assignment，不将新任务伪装成 BusyAgentNudge。Manager 合并后续需求或等待当前工作结束，不以新建操作员绕过忙碌状态。

接收结果不明时保留原 PromptKey 和恢复权，不盲目重发，不虚构已运行，也不把不明接收交给 join 死等。已完成但未 join 的结果，不是创建第二名 DevOps 的理由。

### 8.3 Manager 接力

下一任 Manager 获得同一道路的固定 DevOps 调用权，旧任立即失去新派工权。已接收的工作与进程仍有明确 owner；完成记录不能因旧任退出而丢失，也不能被新任误当成另一个新任务的完成。

DevOps 保留执行事实和环境连续性；其前任结论不替代新任的独立评估。恢复沿用已绑定模型／Persona 规则，不顺手允许 resume 任意换模型。

道路真正关闭时，按照现有资源责任规则收束进程与会话。发 signal 不等于已经退出；不能仅因为 Manager 不再关注就遗留进程。

### 8.4 执行、自修与验证

```text
Manager：给目标和验收要求
DevOps：执行并观察真实失败
DevOps：本地调查、直接修改、补回归测试
DevOps：重新执行并观察
DevOps：返回修改、运行事实和未决问题
```

删除旧 DevOps→Inspector、DevOps→Coder、establish-behavior/repair-behavior 的代理差遣路径。DevOps 到达架构边界后由 Manager 派 Engineer；Engineer 完成后再由 Manager 决定验证。

同时运行的 Engineer 与 DevOps 不能无协调地修改同一验证对象。复用现有工作树、快照和任务边界：Manager 在验证窗口避免重叠写入，或让运行针对固定快照。不要新增「自动架构分类器」或用前后文件哈希相同冒充运行期间未变。

DevOps 修改过源码后，旧评估／证书不能自动证明新状态。报告必须区分改动、实际运行结果、尚未执行的验证；测试源码存在不等于测试运行过。

主要接点：`Execution/Delegation/Fork/`、`Handle/`、`Handoff*`、`Change/` 和现有 Manager 任期／退休实现。源码归属先定位后修改，不按旧稿猜测已移动目录。

退出条件：一条道路只有一个可执行 DevOps；多次 resume 与任期交接正确；DevOps 在未获逐次写入授权时仍能完成非架构级自修，并在真正语义边界返回。

## 9. P3：Sphinx 从工具驾驶层收敛为程序工作流

### 9.1 对外合同

新增或收敛一个高层 Sphinx 工具入口，输入问题、约束和预算，返回探究结果、证据界限或明确未决后果。低层 phase、yield、nextTool 不再需要调用模型一轮轮驾驶。

```text
高层请求
→ 创建／恢复现有 Sphinx 探究
→ 内核决定下一工作项
→ 程序执行确定性步骤
→ 需要语义调研时同步调用 Engineer
→ 接纳合法调研结果
→ 内核决定继续或结束
→ 返回有界结果
```

「完全程控」指预算、调度、状态推进和终止归程序；不要求把 Engineer 的语义调研变成确定性算法。Engineer 不能自己扩充预算、决定跳阶段、另起研究道路或递归调用。

### 9.2 接入真实生命周期

复用现有 SyncDelegate、容量、取消、恢复与 EventStore。Sphinx 只注入本次需要的 Engineer 调研能力，不私建会话池，不借用固定 DevOps。[C11]

同步表示调用方等待本次结果，不要求所有独立调研串行。程序可组织独立调用，但不使用 Fission：Sphinx 不是角色，内部 Engineer 本次授权也不含 Fission。

父调用等待时不能耗尽子任务必需的容量；取消应贯穿父工具、子 Engineer 和结果接纳。已完成且已接纳的调研不能因外层重试重复购买，晚到结果必须按工作身份处理。

`GecHost` 的计划对象不能代替真实派发、abort 或 drain。验收必须观察真实 child、实际执行、因果正确的返回以及真正的回收，不以返回 `aborted = true` 作为进程／会话已经结束的证据。

### 9.3 删除与保留

删除 Inquiry Role/Persona/Prompt/模型槽位和普通 fork 选项；迁走它持有的 Sphinx 权限。清理 SyncDelegateRole.Inspector/Coder 的活跃分支，以真实用途收敛到程序内部 Engineer。

Sphinx MCP 不因删除 Browser MCP而连带删除。可以作为内部 transport 或独立接口保留，但普通调用者的高层程序化入口不能只是改名包装一个仍需模型驾驶的旧循环。

不夹带重写 Sphinx 数学内核、预算理论或事件机制。

退出条件：没有 Inquiry；一次高层调用真实贯通程序推进、只读 Engineer 调研和程序收束；无递归、Fission、DevOps 或遗留子会话。

## 10. P4：Engineer 轨迹、文件关联与 Bookkeeper diff 维护

### 10.1 数据最小模型

不要从旧 Observation 类型继续加字段。新模型直接表达当前需要的信息；下面是概念字段，不要求照抄为新框架。

```text
Case
  identity                  本次逻辑工程工作的稳定案例身份
  sourceTrace               不可变轨迹引用及其工作范围
  question / answer         可维护的案例内容
  relatedPaths              实质访问路径的去重集合
  completionFileState       轨迹结束时的完整文件状态引用，永不改写
  maintenanceFileState      最近一次成功维护所对应的完整文件状态引用
  accessOrder               沿用现有索引和淘汰机制
```

初始时两个文件状态引用可以指向同一份存储，不复制完整文件。原始轨迹和结束基线保持不变，案例正文与维护基线随维护推进。

CaseId 复用本次 invocation 的已有因果身份及工作范围；不新建 durable Stage/Phase/ActiveWorkUnit，也不继续把物理 SessionId 当作唯一 CaseId。

### 10.2 实质访问采集

| 操作 | 记录方式 |
| --- | --- |
| 成功读取文件正文，包括局部读取 | 记录规范路径；关联整个文件 |
| 成功创建 | 记录新路径 |
| 成功修改 | 记录目标路径 |
| 成功删除 | 保留原路径；结束状态允许 Missing |
| 成功移动／重命名 | 记录旧路径与新路径 |
| grep、glob、ls、只展示符号位置 | 不记录 |
| 在回答、附件、旧案例或命令字符串里提到路径 | 不据此推断访问 |
| 工具执行失败，修改没有提交 | 不记录修改成功；此前已发生的真实读取仍可记录 |

采集在实际工具执行层完成，不让模型上报「我认真读过哪些文件」，不设置阅读行数阈值，也不构造仓库依赖闭包。

直接文件工具与 JS 文件 API 都要覆盖。路径规范化复用现有工作区边界，不因为采集而放宽越界、符号链接或敏感文件的访问规则。原本无权访问的内容，不能由快照逻辑替模型读出来。

### 10.3 JS 的两个集合必须分开

当前 ReadSnapshots 服务事务一致性，包含 grep 实现为了扫描而读取的文件，不能直接作为案例关联。[C10]

保留原事务集合，再提供独立的实质访问记录：显式 read 的路径、已经成功提交的 create/edit/remove/move。复用已有工具执行事实或注入一个轻量观察回调即可，不建新的事件总线。

提交前的 effectPaths 是意图，不是已发生修改。成功事件在提交成功后收集；执行失败、回滚、提交结果不明按现有工具事实处理，不能把 prepare 当 commit。不要为了案例采集改变源码事务的成功／失败定义。

程序内部 grep 实际打开文件，并不表示 Engineer 实质阅读了它。需要专门回归测试防止以后有人为了复用而把两个集合重新合并。

### 10.4 轨迹范围与完成边界

同一 Engineer 连续 resume 三次，应得到三段逻辑工作来源，而不是一个越积越大的会话案例。采用已有 XTrace 与调用的开始／结束范围，包含真实工具行为，不只保存最后一句回答或 Q/A 摘要。[C08]

正常完成包括：实现完成、调查完成、或清楚交回了需要 Manager 判断的边界。它不等于测试通过。失败、取消和恢复中断保留真实来源与终结状态，不虚构成功；首版不要求为每个半途取消都强制运行 Bookkeeper。

Sphinx 内部 Engineer 的完成返回程序调用点，不改道差遣 Manager 或 DevOps。它若产生案例，使用自身真实逻辑工作范围，与普通 Engineer 相同的访问和基线合同，不把 Sphinx 主体伪装成角色。

### 10.5 Engineer Fission 的合并规则

```text
同一逻辑 Engineer 工作
  = 裂变前已发生的工作
  + 各 lane 的 keyed 工作记录与实质访问
  + 确定性收敛与最终 takeover 的工作
```

把 lane 映射到原 logical owner 与当前 invocation。访问路径取集合并集；轨迹按已有 keyed convergence 规则保留各路归属，不依赖到达顺序拼接。

不能在某一 lane 完成时提前结束全局轨迹或截取案例快照。只有本次逻辑工作真正完成、最终接管已结束，才冻结文件集合与完整状态。Manager 获得一次完成，Casebook 获得一次归档输入。

重复的 lane terminal、晚到的旧 work unit 完成或 Blogger 压缩事件，不得再次归档。不要为每条 lane 单独启动 Bookkeeper，也不要让 Manager 为 Fission 子分身增加 join 义务。

Fission 取消或中断按现有生命周期收束；不能把少数已完成 lane 的文件状态冒充整项工作完成状态。

### 10.6 结束时保存完整状态，不在 Bookkeeper 开始时才取

```text
逻辑工程工作终结
→ 固定 sourceTrace 与 relatedPaths
→ 取得／引用本次结束时的完整文件状态
→ 发布一次完成及案例来源
→ Runtime 按现有机制组织 Bookkeeper 归档
```

这里的顺序是基线语义，不要求 Engineer 额外写报告或等待模型归档。用现有完成事实和效应记录承接后续归档，不增加一个独立 durable 作业调度器。

完整状态至少能表达 Present（完整内容引用）与 Missing（当时不存在）。读取失败与权限错误不是 Missing；不能把未知状态当空文件生成删除 diff。二进制文件保存真实状态引用，维护时只提供能诚实表达的变化信息。

若捕获失败，工程已经完成的事实不撤销，也不重跑 Engineer；保留明确的案例未完成结果。不能延后读取当前文件再谎称那就是原结束快照。

复用现有完整状态设施，不追求跨整个共享工作树的全局原子快照，不追踪是谁在什么纳秒改了文件。Manager 的任务边界和现有工作区规则负责避免显然的重叠写入。

### 10.7 两个基线与「外部变化」

```text
Engineer 首次读取：A
Engineer 自己修改并完成：B
DevOps 或下一次工作修改：C
再有后续修改：D

初次：completion = B；maintenance = B
第一次维护：diff B → C；成功后 maintenance = C
第二次维护：diff C → D；成功后 maintenance = D
completion 始终保持 B。
```

「外部」指本段轨迹之外，不要求判断修改者身份。同一 Engineer 下一次 resume 的修改，也属于上一段轨迹之外的变化。

DevOps 的修复按 B→C 自然更新相关案例；不自动将 DevOps 的会话放进 Engineer 归档路径。无关联案例时，本次修改不会凭空生成一个 Engineer 案例。

### 10.8 更新时只读 diff

Bookkeeper 的更新输入是：当前案例、关联文件的真实 diff、必要的路径与创建／删除／二进制变化信息。没有仓库 read/glob/grep，没有完整新旧文件，没有 Inspector，没有 Engineer 调查，没有历史观察 replay。

真实 diff 应由现有完整文件状态和差异工具计算，涵盖已关联的未提交、创建和删除状态；不能仅比较 Git HEAD 而漏掉工作树内容。已有 Git/blob 或快照能力够用就复用，不为此先加新依赖。

初次 CaseFinalize 根据轨迹整理案例，后续 CaseRefresh 根据旧案与 diff 更新。两种请求分别渲染，避免把「只读 diff」误写成首次整理也看不到轨迹。

保留 `js-bookkeeper` 的案例 staging 能力。Bookkeeper 能编辑案例，不等于获得文件系统 write/edit，更不能借 staging API 打开仓库文件。[C09]

### 10.9 一次目标捕获，不做稳定性重放

```text
读取旧案和维护基线 B
→ 捕获一次本次目标状态 T
→ 计算 B → T
→ 无差异：返回旧案
→ 有差异：Bookkeeper 维护
→ 成功：将案例结果与 maintenance=T 一起提交
→ 失败：保留旧案与原 maintenance
```

Bookkeeper 工作期间 T 又变成 U，不取消本次结果，也不要求二次回读证明稳定：本次处理到 T，后续处理 T→U。删除现有 replay-before/replay-after 机制，不用新的名称复活它。

文件变化不强迫正文变化。Bookkeeper 可以判定与结论无关而保持正文不变，成功后仍推进基线，避免反复购买同一份 diff。

案例和基线必须在同一次现有事件／提交边界发布，不能只推进基线却丢掉正文更新。并发维护复用现有案例冲突处理；旧事务不得覆盖新结果。这里守的是最小存储一致性，不是恢复观察证明系统。

### 10.10 预算、失败与有效性边界

第一版在 fetch／维护入口检测相关文件，不每次 read 扫描全仓，不做文件监听服务。一次访问只做有界维护，不因文件持续变化进入稳定性重试循环。

大轨迹和大 diff 都遵守预算截头取尾，保留截断声明、来源引用与变更路径信息。Bookkeeper 不能把只见尾部说成已审阅全部变更；能够据此保守缩小适用范围则明确记录，不能作出有效更新时保留旧案并说明维护未完成，不推进维护基线。不要通过反复自动调用、恢复 Distiller 或读取全文件来绕过预算。

案例维护失败不导致已完成的 Engineer 工作失败。沿用 fetch 对不可用／未更新材料的显式处理；不把过时案例假称为当前事实，不静默丢弃历史记录。

接受这套近似方法的固有限制：无关行变化可能触发维护，未访问文件中的真实依赖变化可能漏掉；二进制、重命名及截断 diff 的语义能力有限。这些不是本次必须补齐的依赖证明漏洞。

对外只描述「关联文件未检测到变化」或「已根据所提供差异维护」，不描述「案例已验证正确」。索引仍保持既有低信任、有限公开信息，不把内部状态和会话拓扑泄露到模型索引。

### 10.11 代码迁移与删除

保留并改造：Casebook Store、Index、Lifecycle、BookkeeperRuntime、BookkeeperStaging、事件投影和现有完整文件状态存储。

删除或替换：`Observation.FileRead/GlobResult/GrepResult` 的活跃模型、`ObservationIdentity`、`Observations.classifyReplay`、`CasebookReplay/replayAll`、Inspector 专属 finalize 生命周期、哈希／grep 文本伪 patch、更新后的稳定性 replay。旧格式若需读历史，隔离在历史解码边界。

退出条件：read/create/edit/delete 全入关联；grep 不入；Fission 只归档一次；结束基线正确；Bookkeeper 只根据 diff 维护；无严格 replay 和新存储体系。

## 11. P5：删除 Distiller，统一预算截头取尾

### 11.1 新输出合同

```text
真实程序事实（退出码、超时、取消、真实终结）
+ 明确的截断声明与必要定位
+ 预算内的原始尾部
```

没有模型摘要，不保证尾部包含全部关键错误。错误可能出现在被截去部分，这一能力损失必须写进规范，不能继续保留「关键错误必定保留」的旧承诺。

程序事实不从日志文本推断，也不随尾部被截断。进程退出码、signal、超时和取消的区别沿用真实执行事件；发送 signal 不等于已退出。

### 11.2 实现迁移

从 `OpenCode/Tools/Distillation.fs` 复用 `retainLatestBytes`、`readLatestTail` 的有界读取思路，迁到实际输出拥有者。保留 spool 流式落盘与原始日志的既有可定位能力，不为大输出构造完整字符串。[C12]

让尾部函数接收明确预算，不把 `Spool.ChunkSizeBytes` 的读取窗口无条件当成所有工具的输出上限。为结果事实、截断说明和编码封装预留空间；最终 provider-visible 结果仍需在约定预算内。

小输出原样返回；大输出保留尾部；正确处理 UTF-8 截断边界；空输出、取消、超时和 spool I/O 错误均诚实表示。字符数不能冒充字节预算。

删除 Distillation 私有代理创建、await、permit 等待、取消补偿、模型槽位、Prompt、Host 注册和 `condensation-failed`。保留真实进程的取消和回收，不能连带删除。

Blogger 的工作历史机制不改成本函数，也不接替 Distiller。Opening、权限、当前义务等程序维护的信息，不交给原始日志截尾函数随意裁掉。

`output-distillation` 的有效输出条款与测试可迁到 `process-execution`；旧目录撤销时同步更新需求索引、Ablation 和分发，不保留一个名称错误的永久 facade。

退出条件：任意输出大小均零 Distiller、内存有界、预算有界、UTF-8 正确、事实不丢失、截断明确。

## 12. P6：Browser 及 MCP 全链清理

### 12.1 仓库拥有的集成必须删除

删除已定位的专属文件及 `.fsi` 配对：

```text
src/Wanxiangshu/OpenCode/Host/StealthBrowserMcp.fs
src/Wanxiangshu/OpenCode/Host/StealthBrowserMcpConfig.fs
src/Wanxiangshu/OpenCode/Host/StealthBrowserMcpConfigSurface.fs
```

清理 `ManagedAgentConfig` 的注入、`StaticTools` 的 wildcard 与 js-browser、ProviderSystemTransform、Role/Persona/ManagedCatalog、PromptResources、模型池、功能映射、编译清单和打包资源。[C13]

删除以下旧集成项：`stealth-browser-mcp`、`stealth-browser-mcp_*`、`js-browser`、`STEALTH_BROWSER_MCP_DISABLED`、`STEALTH_BROWSER_MCP_FIXTURE`、`STEALTH_BROWSER_MCP_REF`。目标是本项目不注册、不生成、不启动，而不是继续生成 enabled=false 的定义。

共享 MCP SDK 若仍被 Sphinx 或其他真实能力使用，不能一起删除。只删除 Browser 专属依赖、fixture 和接线。

### 12.2 职责不得转移

删除 `resources/provider/role/browser/`，删除 Manager 及其他角色中的 Browser 委托说明。Engineer 和 DevOps 不增加网页浏览、外部搜索或网络事实调查 Prompt。

DevOps 执行已有构建流程时的正常依赖下载，不等于承担 Browser 职责；但不能用 curl、通用 shell 或临时 JS 包装复活外部调查角色。这次不另建完整网络沙箱，也不新增替代专业工具。

### 12.3 测试和分发

检查 external-investigation、host-boundary、capability-enforcement、repository-investigation、feature-ablation、distribution 的专属断言。已撤销功能的正向测试删除，补成「不存在集成与可达权限」的负向测试。

不能按关键词全仓删除所有 browser：业务浏览器测试、无关文档例子和其他依赖仍按实际用途保留。不能修改用户无关或全局 MCP 配置。

退出条件：角色、职责、权限、Host 启动、环境变量、模型资源和分发同时清除；未来工具没有以空角色或兼容分支的方式提前塞进来。

## 13. P7：历史、辅助机制与跨包收尾

### 13.1 保留辅助机制，不扩大权限

Blogger 的适用身份、Prompt 选择和投影迁到新角色，删除 Distiller 专属分支；保留正常工作的既有上下文规则。

Bookkeeper 保持内部身份和案例 staging，不进入公开 fork，不 Fission。Predictor 保持内部用途，不从被预测目标继承工具；对 Engineer 的投机只读机制不能因此获得写入、真实执行或 Fission。

旧 Coder／Inspector 的统计可以作为历史记录，但不能把它直接宣称为新 Engineer 的效果；角色合并后的真实表现由新的任务样本观察。

### 13.2 配置、资源和编译闭包

至少检查：

```text
resources/wanxiangshu.mjs
resources/provider/role/
resources/ablation/tool-map.json
src/Wanxiangshu/Ablation/
src/Wanxiangshu/Strength/
src/Wanxiangshu/Context/Companion/
src/Wanxiangshu/Interaction/Authority/
scripts/build.mjs
scripts/checks/js-surface-gate.mjs
相关 .fsproj 的 Compile Include / ProjectReference
requirements/distribution/ 的资源与打包测试
```

新配置不再要求旧角色模型槽位。移除文件时同步更新 `.fs/.fsi`、编译包含顺序、导出 surface、resource catalog、requiredNames 和功能索引。不能通过通配编译或保留死文件掩盖闭包问题。

Ablation 的移除会影响站点与门禁，逐项迁移实际能力，不能简单删除编号让后续配置静默错位。Sphinx 保留的能力必须仍能独立开关，不能因撤销 Browser 同时被关掉。

### 13.3 历史读取与运行 clean break

新任务只接受新身份；历史事件保持原样。不要重写 EventStore，不把旧 Inspector 升权，不把旧 Manager Fission 继续当合法当前状态，也不删除用户工作记录来省迁移代码。

必要的旧格式解码隔离在历史边界，不能进入当前权限目录。旧活跃会话按已有中断／退休机制显式收束；没有证明合法当前身份的运行不能自动恢复。

旧案例若能解析现有完整结束状态，迁入真实引用；若只有哈希且找不到完整状态，就保留为历史材料或明确不能自动刷新。不得以当前文件伪造旧结束快照，也不得仅为兼容它保留活跃 replay 管线。

新旧兼容不要求双运行体系：保存历史是数据责任，不等于继续维护旧的调度、权限、Browser 或 Distiller。

### 13.4 独立评估和道路汇聚

Manager 独立评估依赖任务与证据隔离，不依赖 Inspector 这个名称。可以使用新的只读 Engineer 工作实例；实现者的结论不能替评估者决定答案。

DevOps 自修会改变工作树，Manager 不得假定「交给 DevOps 只是读和运行」。旧快照上的测试、评估或证书不能被搬到新改动上冒充验证。

Orchestrator 区分互补道路集成和竞争道路取舍；多条道路各自通过，不等于汇聚结果通过。保留已有变基、证书失效和汇聚后验证合同，不为角色减少而取消这些必要边界。

退出条件：新系统可独立配置、构建、启动和分发；辅助机制仍工作；历史可诚实处理；没有旧身份或权限暗道。

## 14. 正式验收矩阵

下面 T01—T36 是本方案的施工检查编号，不是已经存在或已经通过的 WHAT 编号。实施时映射到实际规范条款与正式测试，新增测试遵循 `NNN.test.mjs` 和用例标题 `WHAT[PREFIX-NNN]`。

### 14.1 身份、权限与 Fission

| 编号 | 场景 | 必须证明 |
| --- | --- | --- |
| T01 | engineer 身份解析、配置与提示词选择 | 真正的 Engineer，不是 Coder 别名；旧角色不会进入新活跃调度 |
| T02 | Manager fork 列表和手工伪造调用 | 只能 fork Engineer；DevOps、旧角色和内部身份均不能通过普通 fork 启动 |
| T03 | Engineer 请求 run/query-shell/terminal/内部执行包装 | 实际门禁拒绝，不只 schema 隐藏；无可达 DevOps 差遣链 |
| T04 | DevOps read/create/edit/move/delete | 真实文件操作成功；权限不依赖本次 Manager 的 allowRepair 字段 |
| T05 | 枚举全部角色与内部执行身份的 Fission | 仅 Engineer 的合法执行上下文允许；其余全部拒绝且不预留资源、不创建 lane |
| T06 | Manager 各任期状态、旧缓存、旧 Prompt 手工调用 Fission | 无正常或恢复入口升权；拒绝文案不泄露隐藏角色和拓扑 |
| T07 | Engineer root、活跃 lane、Sphinx 内部只读调用 | 分别按 subsession-only、非递归、任务授权约束拒绝 |
| T08 | Engineer 合法 Fission，lane 顺序变化、重复 terminal、创建失败 | 原子准入、失败回滚、确定性合并、最终一次 logical completion；沿用既有恢复边界 |
| T09 | 旧 Manager Fission 历史与晚到 lane 事件 | 可以忠实读历史，但不会重新创建合法 Manager lane、操作 DevOps 或增加当前权限 |

### 14.2 固定 DevOps 与修复行为

| 编号 | 场景 | 必须证明 |
| --- | --- | --- |
| T10 | 初始化重复、反复 resume、已完成未 join | 始终同一个逻辑 DevOps；不会因重复调用创建额外操作员 |
| T11 | assignment busy、拒绝、接收不明、恢复 | 不把新任务当 nudge；不自动重发；不虚构接收；不以新 DevOps 绕过不明状态 |
| T12 | Manager 任期接力 | 新任获得控制权、旧任失权；既有任务与完成记录不丢失、不串到下一次调用 |
| T13 | DevOps 物理会话崩溃恢复、道路结束 | 一个逻辑执行权威；不会重复运行命令；真实进程和句柄被收束 |
| T14 | Manager 仅要求运行验证，出现可局部修复失败 | DevOps 可直接补测试／改源码／重跑，不等待逐次授权或差遣 Engineer |
| T15 | 失败需要架构、产品或安全政策选择 | DevOps 返回事实与边界，不擅自决定，不削弱验证制造成功 |
| T16 | DevOps 修改后沿用旧评估／证书 | 不把旧快照的结论当成新状态验证；返回区分源码修改和真实运行结果 |

### 14.3 案例、文件状态与 diff

| 编号 | 场景 | 必须证明 |
| --- | --- | --- |
| T17 | read/create/edit/delete/move 与重复、局部访问 | 正确路径去重；局部读取关联整个文件；移动关联双路径；删除保留 Missing |
| T18 | grep/glob/ls/只展示位置与 JS grep ReadSnapshots | 不进入案例关联；事务一致性集合仍正常工作 |
| T19 | 提交失败、回滚、读取失败、路径越界 | 不把意图算修改、不把读取错误算 Missing、不扩大文件访问权限 |
| T20 | 读 A→改 B→结束→外部改 C | 原始结束状态 B；真实 diff B→C，不自失效、不使用首次读取 A |
| T21 | 同一 Session 多次 resume | 多段独立工作来源，不相互覆盖、不等待会话销毁 |
| T22 | Engineer Fission 多 lane 与最终 takeover | 访问合并、轨迹范围完整、全部收敛后快照；只归档一次 |
| T23 | Bookkeeper 初次整理与后续维护 | 初次以轨迹为来源；更新以旧案和真实 diff 为材料，无仓库／完整文件读取权限 |
| T24 | B→C 维护期间又变 D | 本次发布到 C；下次 C→D；没有 replay-before/replay-after 或稳定性死循环 |
| T25 | 无关 diff、维护失败、并发旧事务 | 正文可不变而推进基线；失败不推进；正文和基线一起发布；旧事务不覆盖新结果 |
| T26 | 删除再创建、二进制、未提交文件、大 diff 截尾 | 变化诚实表达；不漏掉工作树状态；不把部分输入当全量证明 |
| T27 | DevOps 自修与无关联案例 | 修改更新已有相关案例，但不会以 DevOps 会话伪造 Engineer 来源 |

### 14.4 Sphinx、输出、删除与辅助机制

| 编号 | 场景 | 必须证明 |
| --- | --- | --- |
| T28 | 一次高层 Sphinx 调用 | 程序推进全过程，不要求模型驾驶 nextTool；没有 Inquiry 角色 |
| T29 | Sphinx 调研、预算、取消、失败恢复 | 真实 Engineer 执行与回收；不递归、不 Fission、不借 DevOps、不重复接纳旧结果 |
| T30 | 大输出、小输出、UTF-8、取消与超时 | 零 Distiller；预算和内存有界；尾部原始；程序事实与截断说明完整 |
| T31 | Browser MCP 配置、环境变量、旧名称 | 不生成／启动集成；任何角色均无网络调查权限；旧 Browser 不映射成 Engineer |
| T32 | 共享 MCP 依赖和其他浏览器用途 | Sphinx 等真实使用者不被误删；非角色的业务 browser 内容不受无关破坏 |
| T33 | Blogger、Bookkeeper、Predictor 与投机副本 | 既有职责保留，均不能 Fission；副本不继承 Engineer/DevOps 的写入或执行权 |
| T34 | 旧事件、旧案例、旧活跃会话 | 历史诚实保留；不伪造结束状态；不自动升权或恢复旧运行链 |
| T35 | 新配置、编译闭包、资源与分发 | 新系统无需旧角色槽位即可工作，包内没有废弃的活跃注册与资源 |
| T36 | Manager 完整任务与 Orchestrator 多路汇聚 | 无 Manager Fission；多 Engineer 并行仍工作；DevOps 自修、独立评估和汇聚后验证形成闭环 |

### 14.5 三类证据不能混淆

Prompt 文本测试证明授权语义存在，能力与 Host 契约测试证明工具可达性和生命周期，真实任务 canary 才能观察模型是否会合理自主修复且在语义边界停止。

不能把字符串断言、调用次数或内部字段存在说成「DevOps 已能正确自主修复」。至少保留以下正式可复现 canary，并纳入仓库标准入口：

```text
明确局部缺陷 → DevOps 自行修复、回归验证、返回事实
存在不同产品含义的失败 → DevOps 交回 Manager 判断
本地纯调查任务 → Engineer 返回，不执行、不差遣 DevOps
适合独立调查的工程任务 → Engineer Fission 收敛，Manager 不分身
```

canary 的行为观察与确定性单元测试分别报告。多次重跑直到偶然成功不算证明，不能通过增加超时或弱化断言掩盖问题。

## 15. 验证入口与逐阶段执行方式

### 15.1 每阶段

先写对应正式失败测试，再实现。共享类型迁移期间的编译未收敛必须如实记录；阶段交付前，选定编译范围和受影响测试应完成闭环。

构建使用仓库 Fable/Node 入口：

```bash
npm run build
```

然后把本阶段实际存在的测试路径组成逗号分隔的 `CASE_FILES`，通过标准 runner 执行：

```bash
: "${CASE_FILES:?请先设置本阶段真实测试文件的逗号列表}"
TESTS_MJS_FILES="$CASE_FILES" \
  node requirements/verification-system/tests/run.mjs
```

`CASE_FILES` 必须来自已经创建或实际修改的测试文件，不能填目录、本文 T 编号或不存在的示意文件。该变量是选择步骤，不是预先存在的环境配置。

当前 runner 会检查构建新鲜度并公开提示 scoped 范围；不能使用 `--skip-staleness-check` 把旧 dist 跑绿。实际工具验证须经标准入口，不以临时脚本或只读源码替代。[C14]

### 15.2 合并后与发布前

本次触及角色、权限、Host、持久化与跨包分发，合并后必须执行完整的日常流水线：

```bash
npm run format-build-test
```

真正准备分发时再执行发布验证：

```bash
npm run verify:release
```

禁止 `dotnet build`；不能跳过 freshness、弱化断言、用放大超时抵消资源泄漏，或仅运行手工挑出的私有路径就宣布完成。

验证报告分别列出：实际执行命令、退出结果、覆盖范围、警告、未运行项及原因。计划中的测试不计入已通过。

## 16. 合并分工与删除审计

### 16.1 分工建议

| 负责人范围 | 工作包 | 交付边界 |
| --- | --- | --- |
| 公共角色与调度 Engineer | P1、P2 | 身份／权限唯一源、Engineer-only Fission、固定 DevOps、Prompt 与接口 |
| Sphinx Engineer | P3 | 程序驱动与同步调研、预算与真实回收 |
| 案例 Engineer | P4 | 轨迹／lane 汇聚、实质访问、结束状态引用、diff 与 Bookkeeper |
| 输出及清理 Engineer | P5、P6 | 零 Distiller、预算截尾、Browser/MCP 全链删除 |
| Manager | 跨包合同、任务分解、独立评估、接力与合并 | 通过多个 Engineer 并行，不 Fission，不亲自改源码 |
| 固定 DevOps | 各阶段正式验证及非架构级修复 | 真实命令和进程收束；架构边界交回 Manager |

共享 `.fsi/.fs`、角色目录、ManagedAgentConfig、模型资源和编译清单由一个负责人最终合并。任何 Engineer 的 Fission 都不能把重叠写入变成无主竞争。

### 16.2 最终搜索与人工分类

在活跃生产代码、资源、配置和规范中检索旧角色名、旧 js-*、Inspect/Behavior 委托、Browser MCP 环境项、Distillation 运行时、replayAll 和 Manager Fission。

搜索命中必须分类：应删除的活跃行为、应更新的规范／测试、必要的历史解码、无关业务用词。不要以全仓零关键词为目标，也不要保留所有命中都标注成「历史兼容」。

审计以下常见暗道：

```text
权限表已删，但 requestToolMap 或 Host primary 配置仍放行
Manager Prompt 已删 Fission，但任期动态能力仍加入 Fission
DevOps 共享 Engineer 全权限集合，意外得到 Fission
Sphinx 内部 Engineer 被强制归成普通全权限工程会话
旧角色错误提示继续建议调用 coder / inspector / browser
Bookkeeper 的 patch 仍只是哈希列表
JS grep 的实现读取混进案例路径
Fission 某个 lane 提前归档或终结全局工作
Distiller 改名为 Blogger 后继续运行
Browser 只 disabled，启动定义和依赖仍被分发
旧 Manager Fission lane 在恢复中重新成为活跃身份
```

## 17. 最终交付门禁

- [ ] 只有 Engineer 能获得 Fission；其他角色、内部身份、旧状态和绕过入口全部拒绝。
- [ ] Manager 并行只通过派出多个 Engineer；Engineer Fission 保持一个逻辑 owner、一次完成和一次案例来源。
- [ ] DevOps 固定、只经 resume 调用、不可 fork／Fission，拥有直接工程与真实执行能力。
- [ ] DevOps 的非架构级修复由 Role Prompt 直接授权，Manager 明白并使用这项能力；没有逐次 allowRepair 开关。
- [ ] Engineer 不执行真实命令、不差遣 DevOps，工作完成即返回；Sphinx 内部 Engineer 返回程序调用点。
- [ ] Sphinx 无 Inquiry 驾驶层，真正程控；子调研的权限、预算、取消和恢复均有运行证据。
- [ ] 案例来自 Engineer 轨迹；read/create/edit/delete 全入关联；grep 等浅扫不入关联。
- [ ] 完整文件状态使用正确结束边界；Bookkeeper 更新只看旧案和真实 diff；没有严格 replay。
- [ ] Distiller、Browser 职责、Browser MCP 和对应活跃注册／资源全部删除；没有换名保留。
- [ ] Blogger、Bookkeeper、Predictor 保留且无 Fission；历史处理不升权、不伪造、不抹除数据。
- [ ] 正式回归、Host 契约、持久化、canary 和最终集成实际运行；未验证项如实列明。
- [ ] 无临时脚手架、调试输出、误入生成物、过渡 facade 或新旧双运行路径。
- [ ] 最终 diff 已审查；提交、推送和发布只在得到明确授权后执行。

## 18. 本文交付状态

本文最初是讨论后的施工定稿；以下状态记录把后续实施与原计划分开。方案本身不构成生产实现完成证明。提交、推送和发布仍须明确授权。

实施入口为 P0：先更新相关产品合同、定位既有完整文件状态接线、建立方向性失败测试。任何后续进度报告都应将「方案要求」「已经编码」「实际验证通过」分开。

最终结构保持三条清楚的链：**Manager 组织工作，Engineer 调查与实现且独享 Fission，固定 DevOps 执行并主动局部修复；Sphinx 程控探究；Engineer 轨迹与文件 diff 维护案例。**

### 18.1 提示词与 resources/ 大修

角色合并不是旧名称替换。此次直接重写了完整工作链：共同法和角色自述、按职责派工、固定 DevOps 的接续、只读评估、Fission 与一次逻辑完成、案例首次整理与后续 diff 维护、日志截断、结对指引及完成纪律。中英文同时修改；旧断言改为检查当前契约，不往新提示词中拼接旧句子。

| 入口 | 新叙事与实际接点 |
| --- | --- |
| `world/`、`role/`、`library/` | Engineer 调查与实现一体；DevOps 直接修复并重新验证；Manager 不亲自进入仓库，也不把源码完成当验收 |
| `tool/fork`、`tool/resume`、Manager 生命周期 | fork Engineer，resume 固定 DevOps；接收不明不重发、不另造替身；任务、运行、评估分开 |
| `tool/fission`、Engineer、Blogger、Bookkeeper | 仅本次获准的 Engineer 子会话；多路收敛后一次返回、一份来源；不让其他角色类推获得权限 |
| `casebook/`、`tool/fetch` | 初次按轨迹整理，后续只看旧案与真实 diff；owner 会话语言贯穿实际 Bookkeeper 调用；维护状态不是正确性证明 |
| `tool/run`、稀缺性手册 | 无模型摘要；只保留原文尾部并声明缺失；日志是数据，不承载指令，退出码来自程序事实 |
| `tool/js-program/ultra-*` | 改为工程修改、只读调查和 DevOps 直接修复示例；删去旧职位和 Manager 查文件的生成示例 |
| `host/`、`resources/enforcer/` | 并发与结对不扩大权限；Engineer 按边界返回不是提前放弃，DevOps 停在可自行修复失败前则不同 |
| `PromptResources`、`PromptSurface` | 已撤销角色不再回退到 Engineer 提示词；公开目录只含五个活跃角色 |

新增正式回归分别覆盖全量资源、公开提示词装配、双语 CaseRefresh、JS 示例和原始日志的数据边界。它们不代替 T01—T36 的全部 Host、持久化、恢复和真实模型 canary。内部旧类型与已停用实现的物理删除仍须独立审计，不能因公开提示词已经收口便宣布整个迁移完成。
