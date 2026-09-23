# Sphinx Clean-Break 重写指南

版本：1.0  
编写日期：2026-09-23  
交付性质：替代旧设计稿的实施规范，不是已完成的实现报告。  
适用对象：接手真实仓库的开发者或编码智能体。  
目标：从当前混合实现，重写为以 LLM 语义调查驱动、以最终答案贡献估值调度的 Sphinx。

> **一句话定义**：LLM 理解目标、提出路径、比较贡献、发现条件和组织答案；Sphinx 设计问题与问法，保存调查条件，用声明的数学模型估值，在资源与权限范围内选择下一步。
>
> **一句话验收**：不仅能生成工单、拟合排序和导出 JSON，而且这些环节真实相连；改变有效调查结果，确实会改变下一项工作或停止决定；重启后能够从权威事件继续。

---

## 阅读导航

| 部分 | 章节 |
|---|---|
| 设计边界 | [00. 本文的效力、来源和使用边界](#s00)<br>[01. 已确定的方向与本次工作默认](#s01)<br>[02. 当前快照的定位：哪些地方必须真正改掉](#s02)<br>[03. 新设计不可违反的 18 条规则](#s03)<br>[04. 目标架构与目录](#s04)<br>[05. 先分清四个对象：目标、材料、计划、调查](#s05)<br>[06. 调度对象是探究计划，不是方法名称](#s06) |
| 问法与数学 | [07. 调查协议：每种问题取得什么关系](#s07)<br>[08. 探针库：16 个完整的起始规格](#s08)<br>[09. 科学问法的运行规则](#s09)<br>[10. 从语义调查到数学输入：量尺与识别契约](#s10)<br>[11. 估值与动作选择：把“哪个高选哪个”实现清楚](#s11)<br>[12. 统一证书：共享状态，不混淆数学含义](#s12)<br>[13. Bayes、A*、Graph-MCTS 的实际实现边界](#s13) |
| 运行与接入 | [14. 核心类型、插件 ABI 与状态所有权](#s14)<br>[15. 命令、事件、事务与幂等](#s15)<br>[16. 唯一运行闭环与并发调度](#s16)<br>[17. 资源、失败、重试与取消](#s17)<br>[18. MCP、JS Surface 与 v2 Wire 协议](#s18)<br>[19. OpenCode 与 Provider：真实适配，不返回模拟成功](#s19)<br>[20. 终局、导出、哈希与安全边界](#s20)<br>[21. 默认 profile、配置与可重开的工程假设](#s21) |
| 实施与验收 | [22. 全量文件处置表：54 对，108 个文件](#s22)<br>[23. 快照之外必须核对的接入面](#s23)<br>[24. F# 编译顺序、构建与变更策略](#s24)<br>[25. 逐工作包实施顺序：从契约到完整替换](#s25)<br>[26. 验收测试矩阵](#s26)<br>[27. 可直接照写的精确测试样例](#s27)<br>[28. 如何验证问法和调度确实有用](#s28)<br>[29. 最终 Definition of Done 与交付报告](#s29) |
| 交接与出处 | [附录 A. 可直接交给实施智能体的任务书](#app-a)<br>[附录 B. 材料溯源与本次取舍](#app-b)<br>[附录 C. 外部资料与核对范围](#app-c) |

---

<a id="s00"></a>
# 00. 本文的效力、来源和使用边界

## 00.1 替代什么，不替代什么

本文替代本次提供的 `Sphinx.md` 与 Next-Gen 短稿，作为此次 **Sphinx clean-break** 的设计基线。不再执行它们的 Legacy Adapter、旧八工具兼容、旧阶段轨迹复现和绞杀式迁移路线。

本文不凭空取代真实仓库中的共享基础设施约束。尤其不能因重写 Sphinx，就另造全仓 EventStore、Host 会话池、权限系统或发布包。对这些接入点，先定位实际 owner，再按本文的接入契约改动。

冲突按以下顺序处理：

1. 本次用户已经明确的目标与修订。
2. 安全、数据保护、真实仓库的共享 owner 边界。
3. 本文明确标为“本次设计”的接口与实施规则。
4. 旧稿和旧实现，仅作为材料或反例。

若全仓旧 requirements 仍要求兼容旧 Sphinx，先在 Sphinx 所属的 requirements/ADR 中记录 clean-break 的 supersede 关系；不得同时保留两套互相冲突的验收标准。与 Sphinx 无关的共享要求不随之作废。

## 00.2 三份材料与标记

| 标记 | 材料 | 使用方式 |
|---|---|---|
| `[R]` | `repomix-output(5).xml` | 判断当前文件、接口、实现行为的直接依据 |
| `[L]` | `Sphinx.md` | 借鉴问法实验、混合证书、运行机制；不继承兼容路线 |
| `[N]` | `粘贴的 markdown (1)。md(5)` | 借鉴具体探针与序数交互；不继承过强保证和示例常量 |
| `[U]` | 本次对话的明确修订 | clean-break、最终答案质量目标、LLM 负责语义判断 |
| `[D]` | 本文新增设计 | 为了使实现可执行而提出的默认、接口和算法约束 |
| `[W#]` | 文末核对过的外部原始资料 | 只补充数学出处和宿主协议事实，不替代仓库代码 |

**除标为材料事实的段落外，本文的“必须／禁止／默认”均是本次实施规范，不表示旧代码已经做到，也不表示用户逐项批准了所有工程细节。**

本文代码片段分为三类：`json` 是可解析的示例或 schema；`fsharp` 是接口与实现契约片段，需按本文依赖补齐成实际模块；`text` 是流程、字段或算法规约。本文不是声称可以脱离仓库直接编译的源代码包。

## 00.3 快照边界

实读快照包含 **108 个文件，即 54 对 `.fs/.fsi`**。文件条目路径以 `Core/…`、`Plugins/…` 及顶层模块开头。本文用 `$SPHINX/` 表示在真实仓库中定位到的 Sphinx 目录，不把快照路径误认为仓库根目录。[R：目录与文件条目]

快照没有提供可据以核实的完整 `.fsproj`、package lock、package exports、全仓测试目录、Host owner 实现或 EventStore 完整接口。因此：

- 本文给出这些位置的**发现步骤和改动清单**，不捏造它们的精确路径、SDK 版本或现成构建命令。
- 现有 `open Wanxiangshu.Persistence.EventStore` 和 composition 调用可证明依赖存在，不能单靠调用方证明跨进程 CAS、批量原子性或外部调用幂等已经成立。
- 本文没有运行原仓编译、Fable 生成、MCP 集成或真实模型实验。实现后的通过结论必须由测试报告产生。
- Repomix 是只读材料；修改真实源文件，不直接改 XML 来冒充仓库重构。[R：L23–L25]

快照 SHA-256：

```text
8ef8c10bd48e14d5f27c7af35fb4cc0a154bc1ae345b6570e2ce1d15099db213
```

## 00.4 clean-break 不等于破坏历史数据

新 inquiry 使用独立的 v2 事件类型和投影键；旧事件保持原样，不自动转换为新语义，不执行目录清空。旧运行时可以退出发布构建，旧数据仍可保留在权威日志中，并以原版本工具离线读取。

新程序必须明确拒绝把旧 inquiry 当作 v2 恢复：返回 `LEGACY_INQUIRY_UNSUPPORTED` 或经版本识别的同义错误，附不含敏感内容的恢复说明。不得悄悄返回空状态。

修改代码、提交 Git、运行付费 LLM、上传私人材料、部署服务和删除数据是不同操作。本文是一份指南，不自动授予其中任何额外权限。

<a id="s01"></a>
# 01. 已确定的方向与本次工作默认

## 01.1 已确定的方向 `[U]`

| 编号 | 决定 | 实现含义 |
|---|---|---|
| U-01 | clean-break | 不保留旧行为适配器，不要求旧接口和 revision 轨迹不变 |
| U-02 | 数学标尺是最终答案质量 | 复测、发散、重构、综合和停止比较同一目标下的终局贡献 |
| U-03 | 按数学估值选择 | 不固定“先复测”或“先发散”；有效调查可改变下一步 |
| U-04 | 语义判断交给 LLM | 不手写答案维度权重、证据分级、方法适用性权重 |
| U-05 | 主要设计科学的问题和问法 | 调查关系、条件、顺序、组合和反事实，不让模型填概率表成为必经步骤 |
| U-06 | 不先制定固定答案评分表 | 不把“隔离模型按我们设计的评分表盲评答案”设成系统目标的定义 |

“语义交给 LLM”不等于放弃 schema、ID、授权、资源上限和数学前提检查。这些检查不评价论证好坏。

## 01.2 本次工作默认 `[D]`

| 默认 | 选择 | 代价与重开条件 |
|---|---|---|
| 语言与部署 | 延续 F#/Fable 和现有发布 owner | 全仓实读发现约束不同才调整，不先新建 TS 项目 |
| 权威状态 | 接现有 canonical EventStore | owner 缺少必需能力时，扩展 owner 契约，不建私有存储 |
| 首条生产闭环 | 直接比较“预算约束下的探究计划” | 只承诺声明调查模型下的估值，不声称已解真实全局最优 |
| 默认选择规则 | 同一调查作用域内，选模型估计贡献最大的可行计划 | 不混用不同预算、候选版本、目标和问法模型的潜变量 |
| 元层深度 | 默认 1 层，资源上限可配 | 避免为了决定问什么无限再问；这是计算近似，不是最优性证明 |
| 观测汇聚 | 一轮测量完成或显式关闭后再作该轮决定 | 部分响应不会因先返回而获得额外票权 |
| 执行模式 | 默认 delegated；独立 Host/provider 是显式配置 | delegated 不宣称盲化、独立或相同前缀实验已经成立 |
| 状态恢复 | 只回放已持久化事实 | 重新调用 LLM 是新实验，不是相同回放 |

数学引擎、问法、数值容差和资源上限均记录在 manifest。配置项不是对用户隐藏的语义评分表。

## 01.3 这次必须交付到哪一层

不是只做一个问卷库，也不是只做三个数学 demo。重写完成必须含：

```text
Goal + Snapshot
  → 可执行计划与必要调查
  → 真实 WorkItem
  → 真实提交与持久化
  → 观测模型与贡献估值
  → 选择下一项工作 / 直接成稿
  → 新材料吸收与作用域更新
  → 可恢复的终局输出
```

Bayes、A*、MCTS 作为同一图上的可组合能力实现并验收；开放任务没有满足它们的输入前提时，允许不激活，不能为凑齐三个名字制造概率、收益和确定性下界。

<a id="s02"></a>
# 02. 当前快照的定位：哪些地方必须真正改掉

以下是**静态阅读**发现，不是通过运行测试得出的生产故障报告。全量去留表见第 22 章。

| 位置 | 快照中的行为 | 重写要求 |
|---|---|---|
| `Core/Model.fs` | 已有图、工作状态、统一证书及保证类型 | 不再把“创建一个 ValueCertificate record”当作重构完成；补作用域、版本、来源与失效语义 |
| `Core/Reducer.fs:createFromOrigin` | 收到 root，但构造的 `InquiryState` 没保存 root | 新状态必须保存原目标与材料引用；回放不能丢根问题 |
| `Core/Reducer.fs:requireDeterministicGuarantee` | 缺少 guarantee 时 `None -> Ok()` | 声称 deterministic 的槽必须显式携带该保证；空缺不得通过 |
| `Core/Reducer.fs:applyObservation` | 检查 work、attempt、锁与 schema，未在这里完整验证 branch/fence/执行状态 | 所有入口共用同一 admission；不能依赖某个 Host 恰好补了检查 |
| `Core/Reducer.fs:applyCertificate` | 整张证书替换 | 改为带基础版本与来源依赖的 slot patch，防止互相覆盖 |
| `Core/Reducer.fs:semanticView` | 包含 session/fence、eventHead 等字段 | 分离完整状态哈希与语义投影哈希；不能一面包含物理字段一面声称 Host 无关 |
| `Runtime/Agenda.fs:schedule` | 按 ID 排序装入批次；Pareto 另行返回 | 拆开价值选择与可行性检查；ID 只能用于稳定排序/最终平局，不是价值 |
| `Runtime/Agenda.fs:readyFor` | 本批已选依赖也可视为 ready | 计划 DAG 可以包含依赖链，但同时派发集合必须是已满足依赖的节点 |
| `Runtime/Agenda.fs:evaluateClosure` | 含几个专门的有限图/仿射示例 | 移到 conformance fixtures；真实 closure 按注册算子依赖驱动 |
| `Runtime/Plugin.fs` | `BoundPlugin` 只包装 manifest | 补可执行 capability 注册、Observe/Propose/Refine 等纯入口 |
| `Runtime/Certificate.fs` | sample/latent 槽要求 coverage | 允许诚实保存“仅经验摘要”“近似后验”，不逼生产者编造频率学覆盖率 |
| `GecInquiry.fs` | 通用 registry 登记 revision 和 results，不产出判定 | 由新 Runtime 驱动完整闭环；不再作为另一套权威状态 |
| `GecHost.fs` | dispatch 为计划对象；abort/drain 返回成功形状而非实际 Host 调用 | 新适配器必须区分 Intent、Receipt、Terminal；没有物理证据不能报告已终止 |
| `GecHost.fs:foldHostEvents` | 提取事件种类/工作 ID，证书为空、状态固定 active | 删除这条伪投影；回放必须使用唯一 Core fold |
| `GecSurface.fs:replay` | 数组分支只按长度 hash；对象分支 stateHash 来源为 events | 删除重载歧义；traceHash 与 projectionHash 不得混称 |
| `GecSurface.fs:applyWithRetry` | 缺节点时在 fold 外创建 placeholder 后重试 | 禁止无事件修补状态；缺节点应拒绝，或者先有显式建节点事件 |
| `Plugins/Ordinal/Inference.fs` | 已有真实 BTL 梯度、Hessian、阻尼、连通性诊断 | 可移植数学核心，但补稳定 log-likelihood、协方差、作用域和 tie 协议 |
| `Plugins/Mcts/Refiner.fs` | 使用有限枚举 transition；自身已把半径降为 reference-only | 接真正生成器端口；保留诚实保证，不把该半径升级为置信界 |
| `Plugins/Stop/Certificate.fs` | caller 提供 evidence；缺 VOC 的 `Option.forall` 可通过 | 重写停止语义；缺失证据是缺失，不是检查通过 |
| `InquiryRuntime.fs` | 运行的是 `EpistemicState`/旧 Request 的串行流程 | 重写为 v2 Runtime 唯一驱动；迁走单纯的 I/O 机制，不延续旧 Policy |
| `ServeEntry.fs` | 未设置目录时退回 legacy 内存服务器 | 生产入口无持久配置则明确失败；测试内存端口只在显式测试 profile |

依据：[R] 上述同名函数；重点全局行段 929–979、1034–1106、1306–1360、4890–4945、5508–5533、8670–9053、10722–11358、12145–12423、15761–15840。这里列的是重写要覆盖的检查点；不声称已经证明所有其它入口也存在同样问题。

<a id="s03"></a>
# 03. 新设计不可违反的 18 条规则

| ID | 规则 | 直接验收 |
|---|---|---|
| CB-01 | 原目标只可由用户授权修订 | LLM 重构生成新的解释节点，不覆盖 Goal |
| CB-02 | 不写死语义评分 | 源码无 Why/How→权重、发现数量→答案质量、固定 gateway gain |
| CB-03 | 观测和解释分开 | 原始回答可回放；模型重新拟合不改原回答 |
| CB-04 | 语义提议不是运行授权 | 提议新工具不扩大实际权限 |
| CB-05 | 排序量不自动成为可加收益 | 缺少 scale bridge 时拒绝跨状态加法/期望价值输入 |
| CB-06 | 每项估值有作用域 | 目标、快照、候选版本、预算、协议模型不匹配则失效 |
| CB-07 | “没有判断”不是平局 | tie / abstain / conditional / invalid 独立处理 |
| CB-08 | 同一 ballot 不重复算独立票 | rank 展开 pair 必须保留 cluster；MaxDiff 用其联合似然 |
| CB-09 | 不确定性不是错误概率的别名 | 近似后验、经验方差、置信区间各自标明 |
| CB-10 | 一套 inquiry 状态机 | MCP、OpenCode、JS surface 共用 Runtime/Core |
| CB-11 | 先记录意图，再执行副作用 | append 失败不得派发 provider/Host 工作 |
| CB-12 | 接受结果至多一次 | work+attempt+receipt+payload 幂等且冲突拒绝 |
| CB-13 | 资源如实计费 | 失败、取消、重复实际调用都可能耗费；不能因不采纳而抹账 |
| CB-14 | 时间不偷偷裁决业务 | timer 只能经 Host 观察事件触发状态转换 |
| CB-15 | 局部保证不自动跨算子升级 | Ordinal/ModelEstimate 不可转成 ProvenBound |
| CB-16 | 相同事件输入有确定 fold | 回放不联网、不生成新随机数、不重跑 LLM |
| CB-17 | 完成与收敛分开 | budget、cancel、no-valued-action、model-ranked-stop 明确区分 |
| CB-18 | clean-break 无运行时 Legacy | 旧事件不删除，旧行为不再进入新调用图 |

<a id="s04"></a>
# 04. 目标架构与目录

## 04.1 依赖方向

```text
                         Composition
                      /      |       \
             Wire/MCP   Host adapters  Canonical persistence adapter
                      \      |       /
                        Runtime
                      /         \
            Plugin contracts    Core mechanics
                    |
     Built-in inquiry / questionnaire / ordinal / mathematical refiners
                    |
        只产出 delta、工作提议与数学结果，不自行执行副作用
```

Core 不导入内置 inquiry 插件、LLM provider、MCP SDK、OpenCode SDK 或业务语义评分。Runtime 接受注册能力，通过端口实施副作用。内置插件之间通过明确的模型/量尺引用衔接，不靠读另一个模块的可变全局表。

数学代码允许为性能使用局部数组与局部 mutable scratch；不允许把算法 scratch 变成另一个权威 inquiry store。

## 04.2 目录清单 `[D]`

每个生产 `.fs` 都有对应 `.fsi`；下列树为避免重复只列 `.fs`。不要因为目录名新就一口气建空文件，按工作包完成模块。

```text
$SPHINX/
  Core/
    Ids.fs                 # 强类型 identity / revision / epoch
    Envelope.fs            # schema 引用与 canonical payload
    Goal.fs                # 原目标、材料引用、授权修订
    Graph.fs               # 通用版本化节点/超边
    Certificate.fs         # slot、作用域、保证及依赖
    Work.fs                # 不可变 work spec、attempt、lease 状态
    Budget.fs              # 预留、结算、超支事实
    Events.fs              # 单一运行事件 vocabulary
    State.fs               # 唯一 inquiry state
    Commands.fs            # 纯命令 admission
    Reducer.fs             # 唯一 fold
    Projection.fs          # state/semantic/trace 投影定义
  Runtime/
    Contracts.fs           # plugin 执行协议；无 SDK
    Registry.fs            # manifest、schema、版本、能力绑定
    Context.fs             # 快照及可见输入装配
    Admission.fs           # work/result/certificate 的共同准入
    Refinement.fs          # 增量依赖传播及 dirty queue
    Decision.fs            # 使用估值的动作选择与 decision receipt
    Agenda.fs              # 只做 DAG、冲突、资源与容量可行性
    Driver.fs              # advance 循环
    Recovery.fs            # 恢复、fence、in-flight 对账
    Ports.fs               # EventStore、Host、provider、digest 端口
  Plugins/
    Inquiry/
      Model.fs             # 计划、理由、条件、回答片段的语义 envelope
      Plan.fs              # 提出/修订可执行计划；answer.now
      Observe.fs           # 结构化语义回答变成带来源的 graph delta
      DecisionModel.fs     # 同域贡献估值与调查选择
      Render.fs            # 最终回答工单与提交
      Stop.fs              # 停止依据，不裁决“真理”
    Questionnaire/
      Model.fs             # 问题、响应变体、作用域
      Design.fs            # 批次、排列、处理组、可识别性检查
      Prompts.fs           # 版本化问法模板
      Decode.fs            # 精确解析语义响应，不默填
    Probes/
      Catalog.fs           # 明确可执行的探针 manifest
      Prompts.fs           # 第 08 章给出的模板
    Ordinal/
      Model.fs
      Pairwise.fs          # stable BTL 与 tie-aware likelihood
      Ranking.fs           # PL/MaxDiff 或声明的 composite likelihood
      Fit.fs               # 受约束拟合、后验近似、诊断
      DesignCheck.fs       # 连通、design rank、可用样本与分离
    Bayes/Exact.fs
    AStar/Refiner.fs
    Mcts/Refiner.fs
  Persistence/
    Codec.fs               # v2 batch ↔ canonical EventEnvelope
    Integrator.fs          # canonical owner 注册的唯一 Sphinx v2 rule
    Export.fs              # 脱敏与 full replay bundle
  Wire/
    Schema.fs              # v2 wire schema / hashes
    Decode.fs
    Encode.fs
    Surface.fs             # JS-native API，opaque runtime handle
  Hosts/
    Mcp/Contract.fs
    Mcp/Server.fs
    OpenCode/Adapter.fs
    Provider/Adapter.fs
  Composition/
    Bind.fs                # 同一 runtime 绑定具体 owner
  ServeEntry.fs            # 可保留发布入口位置，内容彻底重写
  Mcp.fs                   # 启动定位/身份，不含判断
```

保留 `ServeEntry.fs` 路径是部署选择，不是旧 API 兼容承诺。若发布 owner 更适合新的入口路径，集中修改 exports、CLI、调用方和测试，不保留双入口分别跑不同内核。

## 04.3 不建第二套 owner

`Persistence/Integrator.fs` 只是 Sphinx 自己的领域 fold 插件，不是另一套日志后端；`Core/Reducer.fs` 是被它调用的纯函数，不是并行维护的 history。

`Hosts/OpenCode/Adapter.fs` 管 Sphinx work 到共享 Host identity 的映射；共享 Host 管实际 session、capacity、终止和失败生命周期。两者事件必须通过明确 identity 关联，不各自靠 timer 猜测对方状态。[L：第十三章；R：ServeEntry、IntegrationRules、GecHost 的 owner 边界]

<a id="s05"></a>
# 05. 先分清四个对象：目标、材料、计划、调查

## 05.1 GoalSpec：原目标不被优化器换掉

必备字段：`goalId`、`goalRevision`、用户原文、显式补充约束、材料引用、授权引用、创建来源。原文按字节保留；显示版本可另做规范化。

LLM 可以提出 `GoalInterpretation`、`ReframingProposal`、`MissingConstraint`，它们都是带来源的语义节点。只有显式的用户 goal amendment command 可以更改 GoalSpec。新解释并不自动等于新用户要求。

新目标版本使依赖旧目标的估值失效，但不抹除原始材料与观测。终局输出说明使用的 goal revision。

## 05.2 ContextSnapshot：不是一句“共同前缀”

一个快照至少绑定：

```text
snapshotId / canonical content hash
GoalSpec revision
included artifact revisions + exact content hashes
excluded item IDs（及处理规则，不必泄露其内容给 witness）
context selection / summarizer version
protocol purpose: measurement | intervention | generation | rendering
visibility policy
model-visible bytes hash
```

语义摘要由 LLM 或声明的抽取方法产生，其内容不是 Core 事实。保存原材料引用、摘要版本和实际可见字节。模型没有看到被截断的段落，就不能在 manifest 中声称“同样的全部上下文”。

根快照可用于初次独立生成；后续调查可以使用新的 epoch 快照。只要求**同一对照轮中的可比项**共享已声明条件，不要求永远从初始 M₀ 重新开始。

## 05.3 Artifact 与关系

内置 inquiry 插件使用如下语义类别，但它们不是 Core 枚举：

`candidate`、`argument`、`assumption`、`condition`、`counterexample`、`question-reframe`、`answer-fragment`、`plan`、`judgment`。

内容版本是不可变的。修订产生新 revision/新内容 hash，并记录 `supersedes`；不就地覆盖已经施测的候选。

关系也有来源与适用条件：`supports`、`challenges`、`equivalent-under`、`requires`、`alternative-to`、`revises`。它们首先是 **LLM 在给定材料下提出的关系**。相似不等价；等价不自动做并查集合并；反事实回答变化不自动证明现实因果。

Core 只检查端点存在、revision 有效、schema 符合和引用可访问。

## 05.4 DecisionScope：所有分数的地址

```text
scopeId = hash(
  goalId + goalRevision,
  snapshotId,
  decisionEpoch,
  alternatives: [(planId, planRevision)],
  remainingDecisionBudget,
  continuationPolicyRef,
  elicitedConstruct,
  observationModelRef,
  protocolFamilyRef
)
```

调查 ticket 的 ID、物理回包时间不是语义作用域字段；实际 visible content、被调查构念和剩余决策资源则是。

同一 epoch 可有多张 ballot。增加候选、修订候选、改变剩余执行预算、吸收改变材料的新观测，都需要新 scope，或由明确模型给出可审计的迁移规则。默认不迁移旧潜变量数值。

measurement 轮开始时先预留本轮调查成本；问题显示的是调查结束后可用于执行的共同预算。不要一张票显示 30 次调用、另一张票显示 27 次，又把它们当同条件重复测量。

## 05.5 四种 graph 不可混成一张 DAG

认识图可以有循环论证、互相支持和条件关系；工作依赖 DAG 必须无环；计划树可以包含结果分支；数学 refiner 的状态图遵循自己的模型条件。

可共用 Graph 存储形式，但 `graphRole` 和 capability 类型必须区分。禁止为了让工作调度拓扑排序通过，删除认识图里的语义循环。

<a id="s06"></a>
# 06. 调度对象是探究计划，不是方法名称

## 06.1 PlanCard 的最小内容

| 字段 | 谁提供 | 用途 |
|---|---|---|
| `id/revision` | Runtime | 稳定引用 |
| `description` | LLM | 这条路径实际做什么 |
| `targetArtifactRefs` | LLM 提议、Runtime 检查 | 目标材料 |
| `workTemplate/capability` | LLM 提议、registry 解析 | 能否真正执行 |
| `expectedContribution` | LLM 文本 | 为什么可能改善原目标，不转换成固定分数 |
| `conditions` | LLM | 哪些前提会改变该判断 |
| `continuation` | LLM/计划插件 | 做完后还可做什么 |
| `resourceReservation` | Host 成本模型与预算策略 | 可行性和比较条件 |
| `permissionNeeds` | 实际工具契约 | 不是 LLM 自授权限 |
| `sourceObservation` | Runtime | 来源与追溯 |

plan 可以是单步，也可以是短序列、条件分支、组合批次。比较时展示相同的信息槽，不能给偏爱的路径额外塞“高收益／最科学”之类结论词。

## 06.2 `answer.now` 必须进入候选集合

它的语义是：使用当前可用材料和同一成稿协议，在剩余资源内组织回答。不要求已有完整候选答案，也不把它当零成本。

每次 inquiry 必须预留成稿资源；比较其它计划时考虑其资源消耗及后续成稿可能性。若连成稿都不可执行，返回 `INSUFFICIENT_RENDER_BUDGET`/可用的已有 draft，不凭空合成新答案。

“现在作答”与“再进行一次综合修订”可以是不同计划，但要解释差别，不能让同一个成稿动作以两个名称重复占优。

## 06.3 开放空间与候选遗漏

候选集合是当前可见的计划，不是穷尽全部可能性。计划集合至少允许表达：

- 检验当前答案的关键前提；
- 寻找新解释或遗漏条件；
- 重新表述求解路径但保持原目标；
- 组合或修订已有材料；
- 调查哪条路径更值得做；
- 现在作答。

这里是**允许的能力**，不是每轮必跑清单。生成器可以返回空列表、提出新方法，或认为现在作答最好。停止证据只覆盖已列出的计划，不能证明没有尚未生成的更优路径。

## 06.4 不按方法名造先验收益

删除 `Why → Abduction 0.9`、`ExperimentDesign 1.089`、`gateway × 0.65` 一类生产规则。成本可估计，贡献由调查得来。未调查计划可以是 `Unestimated`，不能赋 `0` 冒充差，也不能赋 `1` 冒充值得探索。

没有足够数据拟合时，可使用初次生成中明确返回的暂定顺序，标成 `single-response-provisional`；不能把它包装成独立 panel 结果。

<a id="s07"></a>
# 07. 调查协议：每种问题取得什么关系

## 07.1 统一提问前缀

下面的前缀由 protocol 编译器装配，不让各探针私自重写目标：

```text
原目标：{用户原文与显式修订}
当前可用材料：{本次快照中实际包含的内容}
后续可用资源：{扣除本轮调查与预留成稿成本后的共同预算}
本次比较的对象：{候选计划，按本轮随机顺序展示}

请根据对原目标的预计贡献判断，而不是按名称、篇幅或已经投入的成本判断。
请同时考虑执行消耗和执行之后仍能开展的工作。
可以并列、暂不判断、给出改变结论的条件，或提出没有列出的更好路径。
不要填写概率、置信度或自行编造数值权重。
只返回本次响应 schema；理由可以用自然语言放在相应字段内。
```

最后一句不是“禁止思考”或“禁止 prose”。它只约束结果封装。理由要求简短的结论依据与可检查的引用，不要求输出完整私有思维过程。

## 07.2 八个调查原语

这些原语按需要调用，不构成固定问卷。

### Q-01 计划贡献成对比较

**要问的关系**：在同一目标、材料、预算与后续策略下，计划 P 和 Q 哪个预计使最终回答更好。

```text
优先执行哪条路径，预计更有助于达成原目标？
不要仅比较眼前一步的产出；把各自完成后还可以做的工作算在内。
```

合法响应：`prefer-left`、`prefer-right`、`tie`、`abstain`、`conditional`。可附理由、引用、新计划提议。

**数学消费**：前两种进入声明的 pairwise likelihood；tie 进入 tie likelihood；abstain 不产生方向票；conditional 产生条件关系和新调查候选。不得将 abstain 变成 0.5 胜场。

### Q-02 小集合排序／best–worst

**要问的关系**：一个给定集合内的局部顺序。

```text
在这些路径中，哪项预计贡献最大，哪项最小？
无法区分的项可以并列；材料不足以判断的项单独列出。
```

输出显式 `presentedSet`、`rankedTiers` 或 `best/worst`、`unjudged`。未排序项不默认最差；没有展示的项不属于该 ballot。

**数学消费**：MaxDiff 使用联合 best–worst 似然；严格完整/前 k 顺序可用匹配的 ranking 模型。把一张排序拆成多对比较时，仅能作为保留 ballot cluster 的 composite likelihood，不能制造独立样本数。

### Q-03 排序反转条件

```text
哪项当前尚未确定的信息，最可能改变 P 与 Q 的先后？
说明在条件成立和不成立时，各自更值得做什么。
没有这样的具体条件也可以明确说明。
```

输出条件文本、材料引用、两种条件下的关系，允许其中一边仍未知。

**数学消费**：生成条件化 scope；不自行给条件赋概率，不把 `if F` 视为事实 F 已成立。

### Q-04 组合与先后

```text
比较“先 P 再根据结果决定是否 Q”与“先 Q 再决定是否 P”。
两条路径使用相同总预算。哪些结果会改变后续选择？
```

输出带分支的 plan card、偏好/并列/条件关系。

**数学消费**：整条路径作为可比较对象；不是把 P、Q 的单项 theta 相加。序列中依赖工作必须在前置实际完成后派发。

### Q-05 继续调查的价值

```text
当前下一步倾向是 {候选路径}，仍缺少 {具体关系或条件}。
比较：立即推进该路径；先回答 {拟议问题} 再决定。
考虑提问成本，以及回答有可能改变的实际行动，哪条路径更有贡献？
```

该问题本身也收费。默认元层深度为 1；不再自动递归问“是否值得问 Q-05”。深度上限是明确的计算近似。

**数学消费**：问题是一个计划动作，和执行计划放在同一目标作用域比较。不能因“信息熵高”就固定调用。

### Q-06 改善幅度比较

```text
比较“从材料/回答状态 A 改善为 B”和“从 C 改善为 D”：
哪一段更能推进原目标？哪些条件会让两者顺序改变？
```

**数学消费**：为可选的间隔尺度模型提供差值序数约束。单独这类回答不建立风险偏好，也不证明不同随机路径的期望效用可用；见第 10 章。

### Q-07 候选关系调查

```text
这些候选哪些是在相同条件下表达同一方案，哪些只是共享一部分机制？
指出可以区分它们的条件；不能确定时保留分开。
```

**数学消费**：只增加带条件和来源的关系。合并必须是可撤回视图，不销毁节点或历史票。

### Q-08 直接作答与继续探究

```text
在当前剩余资源下，比较：现在用已有材料组织回答；执行 {具体计划} 后再回答。
哪条路径预计更有助于原目标？指出继续工作能够改变答案的具体部分。
```

**数学消费**：`answer.now` 与其它计划共用估值。不把“答得更确定”“多找了一条证据”当自动收益。

## 07.3 通用响应类型

```fsharp
// 放在 Plugins/Questionnaire/Model.fs，不是 Core 的语义枚举。
[<RequireQualifiedAccess>]
type PairwiseJudgment =
    | PreferLeft
    | PreferRight
    | Tie
    | Abstain of reason: string
    | Conditional of conditionRef: string

type PairwiseResponse =
    { Judgment: PairwiseJudgment
      Rationale: string option
      ReferencedArtifacts: string list
      ProposedAlternatives: string list }
```

这里的 `ProposedAlternatives` 是指向本次响应所附新 plan card 的局部引用。Runtime 接受响应后才分配 canonical plan ID。Worker 不得通过猜 ID 直接添加生产 graph 节点。

Schema 只能检测字段、枚举、引用和大小；不能机械检查“这个理由是否充分”。需要进一步判断时，产生新的语义调查，而不是在 decoder 里写自然语言关键字评分。

<a id="s08"></a>
# 08. 探针库：16 个完整的起始规格

来源：[N] 的 11 个展开探针及 [L] 的反思/问法设计；下列中性问法、返回字段与调度衔接是本次改写 `[D]`。数量不是产品验收指标，**每个启用探针都必须真的能被提出、派发、吸收和重新估值**。

所有探针共享 `scopeRef`、`inputArtifactRefs`、`applicability = applicable | not-applicable | unclear`、`findings[]`、`unresolved[]`。`not-applicable`、空发现、未找到反例都是成功完成的合法结果，不重试到模型说出预设答案。

| ID / 能力 | 给 LLM 的核心问题 | 额外响应字段 | 吸收与下一步 |
|---|---|---|---|
| P-01 边界与退化 | 在方案声称的范围内，把关键量推向边界，哪些结论仍成立、哪些需要改写？不适用无穷极限时指出。 | `testedConditions[] {condition, assessment, explanation}` | 建条件/修订节点；不存在边界不处罚 |
| P-02 数量级与单位 | 哪些量有明确单位？主要结论依赖哪些数量级关系？给出依据不足的估计，不假装精确测量。 | `quantities[]`, `orderRelations[]`, `missingMeasurements[]` | 请求测量可以成为计划；高阶项不由 Core 自动删除 |
| P-03 结构类比 | 能否找到结构相近的问题来启发当前目标？指出映射成立与失效的部分。 | `mapping[]`, `transferredIdeas[]`, `mappingLimits[]` | 作为启发提议，不把类比称成定理/同构证明 |
| P-04 新解释 | 哪个新假设能解释重要异常？分别列出能解释和仍解释不了的部分。没有更好的假设也可说明。 | `hypotheses[]`, `explains[]`, `doesNotExplain[]` | 增加候选，不增加“已证实”标签 |
| P-05 隐藏变量 | 当前争论是否把某个可变条件当作固定？改变它会产生哪些新路径？ | `variables[]`, `newOptions[]` | 新条件作用域；不预设必有隐藏变量 |
| P-06 钢人化 | 在不换掉原目标的前提下，给较弱方案提出其最有力的理由；它在哪些条件下值得考虑？ | `strongestCase`, `conditions[]`, `limits[]` | 增加理由与条件，不强求“唯一解” |
| P-07 反例与边界修复 | 在方案宣称的适用范围内寻找反例或矛盾；找到后给出最小必要修订。 | `counterexamples[]`, `affectedClaims[]`, `repairs[]` | 反例是模型提出的材料；无反例合法 |
| P-08 条件干预 | 假设 F 被改变而其余指定条件保持，哪些结论预计改变？哪些保持设定并不自洽？ | `intervention`, `heldFixed[]`, `predictedChanges[]`, `incoherence[]` | 记录模型条件判断，不直接标 `isTrueMechanism` |
| P-09 约束与瓶颈 | 当前哪些约束限制目标？是否存在多个瓶颈，或随着规模/环境变化而转移？ | `constraints[]`, `regimes[]`, `suggestedChanges[]` | 调查解除约束的计划贡献；不强制单瓶颈 |
| P-10 组合方案 | 哪些候选可以互补，哪些不能一起采用？组合新增了什么条件和成本？ | `compatibleSets[]`, `conflicts[]`, `combinedPlanCards[]` | 联合计划整体估值；不把单项 theta 相加 |
| P-11 无损综合 | 哪些发现可以合并表达，哪些分歧必须分开？压缩会丢掉什么对目标有用的信息？ | `groups[]`, `retainedDifferences[]`, `draftFragments[]` | draft 不自动终局；保留来源链 |
| P-12 原则与案例 | 哪些一般原则与具体判断仍有张力？应修订哪一边，或暂时保留分歧？ | `tensions[]`, `revisionOptions[]` | 不要求达到“无内部张力”；可生成下一调查 |
| P-13 问题重构 | 保持用户原目标，是否有更能推进求解的表述或分解？指出它保留了什么、可能漏掉什么。 | `reframings[]`, `goalPreservationNotes[]`, `omissions[]` | 新解释节点，不覆盖 GoalSpec |
| P-14 最小区分实验 | 现在哪个可执行观察最能区分会导致不同行动的解释？列出不同结果各自改变的下一步。 | `experiment`, `possibleObservations[]`, `conditionalActions[]`, `toolNeeds[]` | 工具权限另行 gate；无概率不自动等权 |
| P-15 匿名理由后修订 | 阅读指定的新理由后，当前判断应如何变化？没有变化也说明适用理由。 | `priorResponseRef`, `revisedResponse`, `newReasons[]` | 标为 intervention，不把前后票当同分布重复样本 |
| P-16 缺口与停止 | 哪一项未完成工作仍可能实质改变最后回答？把它与直接成稿比较；没有值得做的项可明确停止。 | `remainingPlans[]`, `stopComparison`, `answerChanges[]` | 接 Q-08 和 Stop，不以缺口数量阈值结束 |

每个 probe manifest 必须附：输入/输出 schema、模板 hash、适用条件说明、可要求的工具能力、默认无写权限、如何转成 graph delta、失败类型、至少两个合法样例和一个格式失败样例。

**禁止**在 Catalog 中新增 `qualityWeight`、`methodUtility`、`expectedRootGainDefault` 等字段。可有实际成本统计、返回 token 上限、所需资料类型；语义适用性和贡献仍由 LLM 调查。

## 08.1 动态问题不是任意运行代码

LLM 可以提出库外问题。Runtime 把它封装为 `sphinx.probe.open-question@2`：自然语言问题 + 可见材料 + 已注册的通用 response schema + 已授权工具。它不能提交新 F# 插件、动态 import 或任意工具脚本。

重复有效的动态问题可以在后续版本中成为新模板；本 inquiry 中不自动修改 plugin lock。

## 08.2 一个完整 probe 工单示例

```json
{
  "capability": "sphinx.probe.hidden-variable@2",
  "goalRef": "goal_demo@1",
  "snapshotRef": "snapshot_demo_2",
  "inputArtifactRefs": ["candidate_A@1", "candidate_B@1"],
  "question": "当前两条路径是否把某个可变条件当作固定？改变它会产生哪些更有助于原目标的路径？未发现时直接说明。",
  "response": {
    "applicability": "applicable",
    "variables": [
      {
        "localId": "v1",
        "description": "是否必须一次性替换所有部署节点",
        "newOptions": ["按接口边界分段替换，但保持对外目标不变"],
        "sourceRefs": ["candidate_A@1", "candidate_B@1"]
      }
    ],
    "unresolved": ["原目标是否确实要求同时切换，材料中没有明确说明"]
  }
}
```

这是格式样例，不是对实际 Sphinx 部署策略作出的新用户约束。

<a id="s09"></a>
# 09. 科学问法的运行规则

## 09.1 测量与干预分开记录

同一材料下的换序、标签遮蔽是 measurement 设计；加入新理由、反例、候选、解释是 intervention。前者用于研究相同条件下的判断；后者用于推进思考。[L：附录 H、K]

“问前”和“问后”都留下回答与可见输入。问后改变不自动说明改善；问后不变不自动说明正确。方向与价值仍需围绕原目标判断。

## 09.2 每轮协议 manifest

必须持久化以下实际值，不能只填 `blind=true`：

```text
protocolRunId, scopeId, constructId, purpose
questionTemplateId + version + exact rendered question hash
snapshotId + visible bytes hash
candidate/plan revisions, presented set, omitted set
candidate order, opaque label map（host-private）
assignment seed, RNG algorithm/version
model/provider/config fingerprint（取得多少记录多少）
independence unit / cluster id / branch lineage
expected completion set, retry policy, maximum attempts
missingness / exclusion handling
fit model version, regularization, diagnostics policy
```

Worker 收到 opaque label 和候选内容，不收到 label→真实作者/原排名的映射。真正 ID 只存在 host-private ticket 中。不能把 `blindTokenMap` 原样返回给将要作盲评的模型。

## 09.3 独立性的三个不同层次

| 层次 | 可以实际核查的内容 | 不能自动推出 |
|---|---|---|
| 信息隔离 | 不读 sibling 未授权输出，实际提示词符合 manifest | 各回答统计独立 |
| 调用隔离 | 独立请求/独立 child，固定输入条件 | 同模型不共享偏差 |
| 模型统计假设 | 观测模型声明的独立单元和相关结构 | 外部正确性 |

同一 child 中反复比较是同一 cluster；retry 是同一 work 的新 attempt；复测必须是新的 work。三者不得互换。

## 09.4 排列与区组

默认采用显式 seed 的 Fisher–Yates 或仓库已有合格 shuffle，记录算法版本。需要严格均匀整数时使用 rejection sampling，不能仅凭 `% n` 就声称完全无偏。

成对调查先检查比较图连通。初始调查可用 spanning comparisons 建连通结构；补问选择优先考虑当前会改变决策的边。控制开销的候选上限在配置中明示。

“平衡区组”与“平衡不完全区组设计 BIBD”不是同义词。只有实际满足并验证 `vr=bk`、`r(k−1)=λ(v−1)` 及所有成对出现次数时才命名 BIBD；否则记录真实 exposure 矩阵和不平衡，不美化名称。

## 09.5 先到先得不是测量协议

不默认采用“第一个合法回答胜出，余下取消”来估计比较偏好。响应速度可能与内容有关；这样做会改变观测机制。

默认轮内所有计划中的独立测量 work 都进入 expected set；完成或显式终止后关闭 round。超时、失败、拒答与弃权各自记录。提前关轮只在 manifest 已声明该规则时允许，不能事后因为某个结果合意而取消其它分支。

## 09.6 问法版本不能偷换

模板文字变了就改 prompt hash；含义变了还要新 construct/protocol version。反向措辞只有经确认仍调查同一个构念才共享观测模型；否则它是另一个 intervention。系统不得按字符串编辑就宣布语义等价。

<a id="s10"></a>
# 10. 从语义调查到数学输入：量尺与识别契约

## 10.1 三种量要分清

| 对象 | 含义 | 允许用途 |
|---|---|---|
| 原始序数关系 | LLM 在指定条件下更偏向哪个计划 | 保存偏序、拟合观测模型、提出下一问题 |
| 模型潜变量 `theta` | 在声明 link/尺度假设下拟合出的相对贡献位置 | 同域排名和模型条件下的决策近似 |
| 终局效用 `U` / 价值 `Q` | 支持相加、分支期望或跨状态比较的价值尺度 | 只有显式 scale/transition bridge 存在时才能用于数值 Bellman/MCTS |

**不得把 softmax(theta) 命名为“方案正确率”，也不得把 latent strength 直接塞进 A* 成本下界。**

用户要求量化，并不要求无条件捏造每个量。系统必须输出“在哪个模型条件下算出的什么量”。缺输入时可换一种调查或使用当前可得的序数选择，不因缺少全局效用而堵死全部工作。

## 10.2 默认 pairwise 模型 `[D]`

在固定 `scope` 内，对规范方向 i、j：

\[
\eta_{rij}=\theta_i-\theta_j+\beta o_{rij},\qquad
P(i\succ j)=\sigma(\eta_{rij}).
\]

`o=+1` 表示规范候选 i 展示在前，`o=-1` 表示 i 展示在后。只有随机设计提供可识别的两种位置时才估计 beta；否则 beta 固定为 0，记录“未估计位置效应”，不能报告效应为零。

约束 `sum(theta)=0`，或使用正交零和基矩阵 A 令 `theta=A z`。正则化写为显式先验/惩罚，不说“没有先验”：

\[
\ell(z,\beta)
=\sum_r \log p(y_r\mid A z,\beta)
-\frac{\lambda_\theta}{2}\|Az\|^2
-\frac{\lambda_\beta}{2}\beta^2.
\]

基线 fit 使用受约束 MAP 和 Hessian 的局部 Laplace 近似。`estimateKind=map-laplace` 必须显式记录。它不是无模型的贡献读数，也不是外部正确性的后验。

## 10.3 tie-aware likelihood

有并列的 panel 不允许把 tie 擅自拆成两张相反票。内置一个声明的三类模型：

\[
(p_i,p_j,p_=)
=\operatorname{softmax}(\eta/2,-\eta/2,\kappa).
\]

当 `kappa -> -∞` 时，非并列部分回到二项 logistic。有限 kappa 表示单独的并列机制；可用显式正则估计 kappa。本文把它作为新增观测模型规范，而不是声称任意并列判断均符合它。

若样本全为并列或没有足够有效方向信息，返回 `NoDirectionalEvidence`。不得仅凭对称先验给一项硬排名。拟合三类模型的数据与只包含明确二选一的数据可保留在同一 dataset 中，但训练入口必须使用匹配的似然。

`abstain` 与 `conditional` 默认不进入这三类似然；它们保留为独立结果，并影响缺失性诊断与下一计划。

## 10.4 MaxDiff 与排名

best–worst 观测采用 [N] 给出的模型形式：

\[
P(b,w\mid S,\theta)=
\frac{\exp(\theta_b-\theta_w)}{\sum_{i\ne j\in S}\exp(\theta_i-\theta_j)}.
\]

只有问题实际要求并返回一个 distinct best/worst pair 时才能使用。并列 best/worst 需对应扩展，不能自动枚举成所有独立 pair。

严格无并列排名可使用 Plackett–Luce 形式：

\[
P(\pi\mid S)=\prod_{k=1}^{K}
\frac{e^{\theta_{\pi_k}}}{\sum_{j\in S\setminus\{\pi_1,\ldots,\pi_{k-1}\}}e^{\theta_j}}.
\]

必须区分“真实 top-k，其余比已选项低”和“只调查一个子集，其余未知”。并列 tiers 若暂未实现对应似然，仍保存观测；通过 Q-01 获取必要比较，不能静默按任意顺序打散。

Borda 只作为描述性基线。除 complete equal-exposure 情形外，必须标明扩展与 exposure；平均出现次数并不自动消除对手强弱差异。[L：附录 I]

## 10.5 数值实现要求

`Plugins/Ordinal/Fit.fs` 必须做到：

1. 使用稳定 `logSigmoid(x)=-softplus(-x)`、`logSumExp`；不先 sigmoid 再 log 导致极值下 `log(0)`。
2. 参数 gauge 与协方差使用同一坐标系统；在自由坐标计算，再投回完整候选空间。
3. Newton/L-BFGS 等方法明确算法名、梯度、线搜索与停止条件；不把固定梯度步叫 Newton。
4. 缺少方向信息、连通性失败、design rank 不足、分离、非有限结果、步进失败分别返回 typed status。
5. 返回 Hessian 或其受约束逆所对应的完整协方差/可查询协方差算子，而不只给每项 `1/sqrt(N)`。
6. `Var(theta_i-theta_j)=Sigma_ii+Sigma_jj-2 Sigma_ij`，不得遗漏协方差。
7. capped/line-search-failed 不能标 `Converged=true`；上限按请求配置，内部再截断必须回显实际值和理由。
8. 用真实 ballot/cluster 标识计数。对相关调用，不声称 Hessian 近似已经校正全部相关性；可选 cluster bootstrap 需保存设计与 seed。
9. 原始 ranking/ballot 永久保留；算法升级从原始数据重拟合，不从已经聚合的 Borda 分数倒推原始观测。

## 10.6 数据不可识别与模型不适配

比较图不连通时，即使正则化使解数值有限，跨分量排名也可能主要来自先验。默认返回 `DisconnectedComponents`，提出桥接比较，不报告经验支持的全局第一。

出现稳定循环 `A>B, B>C, C>A` 时不能当坏票删除。保留 tournament；可用成对关系/显式选择规则生成近似决定，并标 `NonTransitiveEvidence`。固定全序 BTL 仍可输出拟合，但不能把拟合全序称为“发现了真实一致偏好”。

没有任何合法比较时，不调用空数据拟合并返回均匀 posterior 作停止依据。使用已声明的 provisional plan order；没有该顺序则优先请求一次有预算的计划判断，预算不足则成稿或明确阻塞。

## 10.7 间隔尺度与期望效用桥接 `[D，可选能力]`

生产默认直接调查整条计划的预计贡献，不要求先建立全局 U。需要真正展开随机分支时，另注册 `value.bridge.interval@…` 或 `value.bridge.expected-utility@…`。

桥接契约至少包括：

```text
valueSpaceId / goalRevision / anchorEpoch
适用状态与终局对象
尺度类型: ordinal | interval | expected-utility
锚点及其来源
观测模型、随机性模型与风险假设
跨状态可比性条件
允许的组合算子: compare / difference / add / expectation
经验/模型误差与失效条件
```

Q-06 可提供 `U(B)-U(A) > U(D)-U(C)` 的差值顺序。它不能单独确定全部数值，也不能单独建立随机路径的 expected utility。

可选的离散彩票比较可以让 LLM 在“确定结果”与“已声明概率的结果组合”之间选择；概率由试验设计给出，不要求 LLM 直填。但其期望效用解释依赖额外偏好假设，作为实验插件提供，不作为首版强制问卷。

若桥接未提供，`MISSING_VALUE_BRIDGE` 只阻塞依赖可加 U 的数值展开；直接计划比较仍可运行。禁止回退到开发者评分表。

<a id="s11"></a>
# 11. 估值与动作选择：把“哪个高选哪个”实现清楚

## 11.1 理想目标与生产近似分开

以 s 表示已知状态，b 表示剩余预算向量，a 表示最终答案。理想目标是：

\[
V^*(s,b)=\max\left\{
J(s,b),\ \max_{e\in\mathcal F(s,b)}
\mathbb E[V^*(T(s,e,O),b-C_e)]\right\}.
\]

J 是在预算 b 内按相同成稿协议现在回答的价值。C_e 只计动作 e 本次实际消耗；调查、生成和后续成稿分别在对应动作发生时扣账，成稿预留不是第二次费用。可行集合必须保留已经承诺的终局资源，不能把负预算状态传入递推。该式是设计目标；只有 U、转移模型和预算过程有明确语义时才是可计算模型。元层计算决策的研究可作为背景 [W5]，不能据此宣布本系统已经找到真实最优。

**默认生产路线**：直接让 LLM 比较有限预算内的 plan continuations，以 theta 表示该 scope 下的相对预计终局贡献；用该模型的 posterior/MAP 近似作选择。不把每步局部奖励相加，不要求模型提前枚举所有未来文字。

## 11.2 默认估值输出

```text
ContributionEstimate
  scopeId, planId@revision
  modelRef, fitRef, observationRefs
  location                       # 默认局部 Laplace posterior mean 近似
  uncertainty                    # 方法、参数化与适用范围
  valueSpaceId                   # 只在同 scope 可比
  fitStatus, assumptions
  supportedComparisons
  unavailableComparisons
```

`location` 是近似模型值。MAP-Laplace 的对称局部近似中用 mode 作为该高斯近似的均值，必须标清；后续若使用其它后验算法，按实际均值计算。

默认推荐：

\[
\hat p=\arg\max_{p\in\mathcal F} \widehat{\mathbb E}[\theta_p\mid D].
\]

这是本次工作默认，不把“最有概率排第一”与“期望 theta 最高”混用。两种准则可能不同，配置、receipt 和测试必须对应实际采用的一种。

## 11.3 没有可信数值间距时怎样走

raw relation、fit 状态和 selection outcome 分离。顺序如下：

1. 模型条件满足且估值可用：按上式选最大。
2. 只有有效 ordinal tiers：在最佳 tier 中选择，不捏造差值；标 `ordinal-only`。
3. 多个模型/条件给出不同优先项：可以提出 Q-03/Q-04，也可以保留分歧并按已声明近似选一项；不要静默平均不同比较构念。
4. 全部未知：请求可承担的一次计划判断，或使用明确 provisional 顺序。
5. 资源不够：保留成稿资源，给当前可交付答案，记录 `resource-limited`。

在同一最佳 tier 中，默认只用**操作性平局规则**：优先无需新增权限的计划；再按逐资源支配关系去除更耗费但语义上并列的项；仍并列用预记录随机 seed 选择。没有精确并列证据时，不自行以“差不多”偷换成平局。

## 11.4 下一道问题也要有决策价值

固定 scope 下可定义一个模型内的单步调查价值：

\[
\widehat{\operatorname{KG}}(q)=
\mathbb E_{Y_q\mid D}
\left[\max_p \mathbb E(\theta_p\mid D,Y_q)\right]
-\max_p\mathbb E(\theta_p\mid D).
\]

实现时用有限后验样本和响应枚举/模拟重拟合估计。所有 samples、seed、内层拟合上限进入 receipt。

这只描述**固定候选、固定价值空间**中额外调查的模型信息价值；若提问消耗改变可执行路径，或问题本身改变认识状态，不能直接拿这个数当总终局净收益。此时把“先问 q 再做决定”作为完整 plan 交给 Q-05 比较，或者使用有预算状态和 bridge 的显式元 MDP。

默认不硬塞 `lambda × tokens`。物理预算是硬约束；资源机会成本已经作为 plan 条件交给 LLM。要做效用单位的成本扣减，必须声明资源到价值的转换模型，且避免与问题中的机会成本重复扣除。

## 11.5 初始与元层的有限默认

没有历史数据时，先调用一次 `plan.bootstrap`：返回当前可执行计划、直接成稿选项、暂定 tiers、可能改变顺序的条件。该一次调用是初始化开销，不包装成“系统已数学证明值得问”。

有预算时再通过独立调查改善排序；预算很小时，可以直接使用 bootstrap 的 provisional tiers。生成与初排来自同一回答，manifest 标明，不能称为独立评审。

元层上限按配置限制：默认不超过一层“比较提问计划与执行计划”。达到上限后使用当前最好的已有估值，不新增无限“调查调查的价值”。记录 `meta-horizon-truncated`。

## 11.6 不预设发散与复测优先

下列情形都必须通过端到端 fixture：

- 复测预计贡献更大，下一工作是复测。
- 新解释预计贡献更大，下一工作是开放生成。
- 当前领先答案已稳定，但生成新候选计划估值仍高，继续生成。
- 问题条件改变后，先前落后计划变为优先，原决定保留在历史中。
- 现在成稿估值最高，停止新增探究并派发 renderer。

路径名称、插件加载顺序、ID 和先返回顺序不得决定这些结果。

## 11.7 每次选择都生成 DecisionReceipt

```text
decisionId, scopeId, baseSemanticRevision
availablePlans, excludedPlans + typed operational reason
estimateRefs, fitting diagnostics
selectionRule/version, selectedPlan, tieBreak if used
budget before / reservation / render reserve
consideredQuestions, unestimatedAlternatives
approximation tags
source observation IDs
```

receipt 是可审阅的依据，不是逐步思维链。它必须能回答“为什么本次做 P 而不是 Q”，且可以定位实际调查记录。

<a id="s12"></a>
# 12. 统一证书：共享状态，不混淆数学含义

## 12.1 证书地址不能只有 NodeId

建议 key：

```text
CertificateKey = (targetRef, valueSpaceId, scopeId, semanticsModelRef)
```

同一个候选在不同目标、预算、epoch 和模型下可以有多份证书。Core 不把它们自动覆盖成一份“最新真值”。

每个 slot 内容：producer、schema、baseRevision、scope、payload、source observation/event refs、依赖 artifact revisions、guarantee、status。

## 12.2 保证分型

```fsharp
[<RequireQualifiedAccess>]
type Guarantee =
    | EmpiricalSummary of assumptions: string list
    | OrdinalObservation of protocolRef: string
    | ModelEstimate of modelRef: string * approximation: string
    | PosteriorCredible of modelRef: string * mass: float * approximation: string
    | FrequentistCoverage of coverageRef: string * delta: float * scope: string
    | DeterministicBound of theoremRef: string * assumptions: string list
    | ExactWithinModel of modelRef: string * numericError: string
    | ResidualOnly of reason: string
```

数值范围、引用存在性、producer 能力、value-space 类型由 Runtime 校验；定理适用性由持证算子给出可核查条件，不能只接受 Worker 自填 `proof=true`。

后验 credible mass 与频率学 coverage 不能互换。sample mean 没有区间也可保存为 EmpiricalSummary；ExactWithinModel 只表示给定模型内的计算结果，不表示模型描述现实已被证明。[L：附录 F；R：MCTS 的 reference-only 声明]

## 12.3 patch 与失效

`CertificatePatch` 包含 `expectedSlotRevision`。独立槽可按 canonical order 合并；同槽冲突则重算或拒绝，不最后写入获胜。

新增信息可能推翻旧前提，证书也可能变宽或失效。只有同一模型、同一作用域、相容约束下的 sound refinement 才要求集合收缩。跨 epoch 不强制“永远更确定”。

证书状态：`Current`、`Stale(reason)`、`Invalidated(sourceRevision)`、`Conflicted`。历史证书保留；调度器只消费当前匹配 scope 的可用槽。

LLM 问题反例导致 premise 修订时，按依赖索引传播失效。后代证书不能因先前从暂定前提推过三步就变为确定。

## 12.4 算子之间只走显式 bridge

| 来源 | 可直接用来做什么 | 不能自动做什么 |
|---|---|---|
| pairwise / ranking | ordinal fit、下一调查、同域计划选择 | 世界真值判断、硬剪枝 |
| Bayes 模型后验 | 该模型内的概率推断 | “概率=实际正确率”的无条件保证 |
| A* bound | 指定确定性最短路模型下的展开/剪枝 | 开放语义方案的终局质量上下界 |
| MCTS sample | 指定模型/奖励尺度内的路径估值 | 对未知真实目标的确定性最优证明 |
| 语义反例 | 新材料、条件、修订提议 | Core 无条件删候选 |

统一对象是图、证书、工作和作用域；不是固定的 `MCTS → LLM → Bayes → A*` 流水线。

<a id="s13"></a>
# 13. Bayes、A*、Graph-MCTS 的实际实现边界

## 13.1 Bayes exact

可借 `Plugins/Bayes/Exact.fs` 的 log-space 归一化和 typed error 结构，重写输入来源与因子契约。

输入要求：明确有限状态空间、先验来源、因子覆盖、非负有限权重、归一化条件、观测依赖模型。内置“似然”路径限制 `[0,1]`；未来一般势函数 factor 与概率 likelihood 分开 schema，不混名。

不让 LLM 必填 `likelihoods`。数字可来自声明的观测模型、实际实验频率模型或模拟器。没有来源就不启用 exact。

**依赖组处理改写**：同一次 observation 的重复 delivery 去重；相关但不同观测不因同一个 DependencyKey 就任取字典序首项丢弃。必须声明联合因子、cluster likelihood 或保守取舍规则及丢弃记录。[R：Exact.canonicalHeads 是需要重新审视的默认]

没有有效新因子时，若先验有效，可返回 `prior-only` 的模型分布；这不是获得新证据。全零归一化应返回 `ZeroPartition`，不能用均匀分布遮掩。

## 13.2 A* / bound refiner

实现增量 `initialize / step / snapshot / restore`，不要只暴露一口气 `solve`。有限确定图、非负成本、固定目标、heuristic 前提明确；更优 g 到来时 reopen。

若没有 admissibility 依据，最保守合法起点可取 `h=0`，前提是成本非负；若使用 LLM 估计 h，则标为 heuristic guidance，不能称 certified lower bound。

全局界：存在 incumbent U 时，未闭合前沿的 lower 与 incumbent 一起用于全局 certificate；默认 `L=min(U, min_OPEN f)`。OPEN 为空且已有 incumbent，L=U；无可达解则返回独立 `Unreachable`，不用 JSON Infinity 表示状态。

增量 state 保存 OPEN/CLOSED/bestG/parents 和模型 revision。模型图拓展或 goal 改变时按算法契约失效/重建，不能沿用旧“最优”证书。

A* 优化的是其声明模型中的成本，不是自然等于答案质量。用于终局收益优化的 branch-and-bound 必须有单独 value bridge 和合法上下界，而不是把 token 最短路直接当最好回答。

## 13.3 Graph-MCTS

当前快照主要是有限枚举转移上的 seeded 搜索。新插件增加生成器端口：

```text
SampleTransition(modelRef, stateRef, actionRef, rngState)
  -> SampleResult(nextStateRef, terminalOrStepReturn, consumedUsage, provenance)
```

纯内存模拟器可同步；需要 LLM 的 rollout 必须转成持久 WorkItem，不能藏在 `for` 循环里直接发网络调用。真实调用也计入全局预算。

state key 默认包含 history/remaining horizon/goal/value bridge revision。graph transposition 只有在 Markov 状态同义、剩余预算/时域一致且 backup 规则正确时才共享；不能把 `DagSafe=true` 当证明。语义候选相同不代表决策状态相同。

默认 finite horizon，不从名次构造 step reward。末端 reward 来源要么是合格 bridge，要么是明确的 preference-based search 模型；二者输出分别标记，不能把“比较投票胜率”改名为客观终局效用。

unvisited 动作先处理，再使用按 reward range 缩放的 UCT/其它明确规则；数值项全部在同一 value space。保存 visits、均值、方差、模型版本和 seed。

**coverage 默认 empirical-only**。自适应树策略下的普通 i.i.d. 半径不得充当 hard bound；生产停止不靠这个半径声称 95% 可靠。[R：Plugins/Mcts/Refiner.fs L1974–L1979]

## 13.4 真正的协同例子

一个 scope 可以同时存在：序数模型决定下一计划；一个已经形式化的子问题用 A* 求其最低可行成本；Bayes 更新该子问题的观测模型；MCTS 在有合格 reward bridge 的短计划模型上估值。

调度通过声明依赖读取这些结果。没有相应 bridge 的分数保留在各自证书里，不求和。算子未适用是正常状态，不伪造输入让三个算子每轮都“亮灯”。

## 13.5 closure 只传播可以传播的变化

维护 dirty queue，key 至少包含 producer、target、scope、input fingerprint。相同输入已成功计算且无失效时不重复运行。

默认按依赖拓扑处理纯算子；有循环时采用显式 iteration budget、数值 residual 和模型假设。没有 contraction/单调性等依据时只报告 `bounded-iteration`，不宣布联合 fixed point。

每一次实际改变产生 derivation patch；无变化不增加 inquiry revision。不得用“循环跑到 20 次”伪装为数学收敛。

<a id="s14"></a>
# 14. 核心类型、插件 ABI 与状态所有权

## 14.1 强类型与 wire 表示

Core 使用强类型 ID：InquiryId、GoalId、SnapshotId、PlanId、WorkId、AttemptId、ObservationId、DecisionId、CertificateId、EventId、RoundId。它们互不隐式转换。

revision 在 F# 中使用明确整数类型，在 wire 中使用十进制字符串，避免 JavaScript 大整数精度漂移。实际计数如 `attempt` 使用检查过的正安全整数。schema hash 是真实 canonical schema 的 SHA-256，不是 `sphinx-schema-v2` 这样的名字。

所有输入 decoder 检查 `NaN/Infinity`、非整数 attempt、负资源、重复引用、未知 schema 和过大 payload。需要无上界/不可达状态时用显式 DU/tag，不向 JSON 输出 Infinity。

## 14.2 InquiryState 最小字段

```text
Identity
  inquiryId, apiVersion, transitionRevision, eventHead
Goal
  immutable original + authorized amendments
Artifacts
  graph nodes/edges + immutable revisions + origin refs
Context
  snapshots, materialEpoch, semanticRevision
Decision
  scopes, plan sets, receipts, current selected continuation
Observations
  accepted results, interpretation status, source/cluster bindings
Certificates
  scope-keyed slots + dependency index + invalidation state
Work
  immutable specs, attempt table, outstanding intents, logical fences
Rounds
  expected members, received members, terminal members, closure record
Budget
  authorized limits, reservations, settled usage, debt/overrun facts
Lock
  executable plugin refs, schema refs, numeric/model/profile config hash
Terminal
  status, stop reason, answer ref, unresolved work and unresolved semantic refs
```

物理 session ID、provider request ID、SSE cursor 和 transport receipt 可以存在运行记录中，但不加入默认 semantic projection。它们不可丢失，因为恢复和计费需要；只是不能和语义等价混为一谈。

## 14.3 ExecutablePlugin：不能只有 Manifest

```fsharp
// Contracts.fs 的接口方向；引用类型由相邻 Core/Runtime 模块定义。
type PluginDelta =
    { GraphPatches: GraphPatch list
      CertificatePatches: CertificatePatch list
      WorkProposals: WorkProposal list
      Invalidations: Invalidation list
      Diagnostics: Diagnostic list }

type ExecutablePlugin =
    { Manifest: PluginManifest
      Initialize: PluginContext -> JsonEnvelope -> Result<PluginDelta, PluginError>
      Observe: PluginContext -> AcceptedObservation -> Result<PluginDelta, PluginError>
      Propose: PluginContext -> Result<WorkProposal list, PluginError>
      Refine: PluginContext -> RefinementInput -> Result<PluginDelta, PluginError> }
```

某能力不支持的方法由明确 capability 路由，不用 `failwith "not implemented"` 作为正常运行路径。Registry 在启动时确保当前 profile 所需的全部能力都有可执行 implementation，而不只是同名 manifest。

PluginContext 是只读快照：goal ref、scope、graph view、certificate view、budget view、锁定配置及随机输入。插件无法取得网络、filesystem、clock、EventStore 或 Host mutable handle。

插件想调用 LLM 时返回 WorkProposal；Runtime 决定是否可执行并先持久化，Host 才实施。插件随机计算使用传入的 RNG state，并返回更新状态/采样 receipt。

## 14.4 插件版本锁

lock 包含实现 artifact hash、版本、ABI hash、schema hashes、prompt hashes、数值配置、依赖版本及 profile hash。不是只比较字符串版本。

inquiry 中不得热换实现。升级时创建新 inquiry/明确的新模型修订流程；历史计算回放用已存 patch，不重跑新算法假装得到旧结果。

新插件加入旧 inquiry 需要显式变更事件和受影响证书失效；默认 profile 不支持这种动态变更，以减少可执行依赖漂移。LLM 新问法用注册 open-question 能力承载，不需要换插件。

## 14.5 四种纯函数边界

```text
decode(wire)                  -> typed command / typed error
admit(state, command)         -> transition batch / typed error
reduce(state, event)          -> state / typed corruption error
observe(plugin, state, raw)   -> proposed semantic delta / plugin error
```

不要让 decoder 直接改 graph，不要让 renderer 直接改 budget，不要在 replay 里为缺失节点补洞。纯插件 delta 也经过统一 admission，不是“自己写的插件所以可信”。

<a id="s15"></a>
# 15. 命令、事件、事务与幂等

## 15.1 只接受领域命令，不对外暴露任意事件写入

生产接口接受 Start、ClaimWork、SubmitResult、Cancel、AmendGoal、ReadStatus、Export 等命令。Worker 只交结果，不能提交 `CertificatePatched`、`BudgetDebited`、`AnswerCommitted` 等权威事件。

调试 replay 接口只读，隔离于生产 worker 能力；输入仍经过完整版本/schema/hash 检查。

## 15.2 v2 事件建议

这些是运行事实，不是 Core 对语义的裁决：

```text
InquiryCreated
GoalAmended
SnapshotRegistered
DecisionScopeOpened
WorkPlanned
RoundOpened / RoundClosed
BudgetReserved / UsageSettled / ReservationReleased / UsageOverrunRecorded
DispatchRequested / DispatchReceiptRecorded
WorkAttemptTransitioned
ResultAccepted / InterpretationPending / InterpretationApplied / InterpretationFailed
GraphPatched
CertificateSlotsPatched / CertificateInvalidated
DecisionRecorded
AnswerPrepared / AnswerCommitted
CancelRequested / HostTerminalRecorded / InquiryCancelled
InquirySuspended / InquiryFailed
```

GraphPatched 的 relation/payload 由插件定义。不得增加 Core 的“HypothesisTrue”“EvidenceReliable”“ReflectiveEquilibriumReached”事件。

## 15.3 单个 canonical envelope 承载原子 TransitionBatch

快照证明 canonical EventStore 被使用，但没有提供完整原子写契约。因此本次选择：一个 Sphinx transition 将多个逻辑事件封在**一个 canonical EventEnvelope** 中，Integrator 对 batch 先完整验证并 fold 到临时值，再整体接受。

```text
TransitionBatch
  schemaVersion
  inquiryId
  previousRevision + previousHead
  revision
  commandId + commandFingerprint
  orderedEvents[]
  postStateFingerprint（可选校验值，不作信任来源）
```

同一 batch 内用 `(transitionRevision, itemIndex)` 定位事件；inquiry revision 每 batch 加一，不是每个内部 item 加一。创建 batch 为 revision 0。

这避免假定“Append [e1;e2;e3]”天然业务原子。仍须实测 canonical owner 对单 envelope 的 durable receipt、冲突、cut 和 recovery 语义。

## 15.4 持久化顺序

```text
授权检查 → 读取当前投影 → 幂等命令检查
→ 校验命令/生成 candidate batch
→ 对临时 state 做完整 fold
→ canonical Append
→ 验证 receipt 确认被接受（不是仅收到写调用返回）
→ 读取/更新可丢弃投影
→ 返回成功 / 发布待执行 intent
```

append 失败，既不更新对外可见状态，也不发出新 Host/provider 调用。已有副作用结果回收失败时保持 pending receipt，恢复后重交；不能重新跑模型来补“丢失”的语义结果。

## 15.5 并发写：序列化队列不是跨进程锁

每 inquiry 的本地 admission queue 可以避免单进程竞争，但不能证明全仓多进程唯一写者。

必需 owner 能力：同一 inquiry stream 对 previousHead/revision 的条件接受，或独占 writer fence 加 canonical 冲突判定。两个客户端同时提交不能都基于同一父 revision 成为 current。

若当前 owner 只持久化不裁决分支，先在 owner 既有机制上补条件接受/branch-conflict，再开放多 writer。未完成前生产 profile 明确单 writer，不用“进程内 mutex 已加”宣称支持多进程。

## 15.6 三层幂等

| 层 | key | 相同 key、相同内容 | 相同 key、不同内容 |
|---|---|---|---|
| 控制命令 | inquiry + commandId | 返回先前 receipt，不再执行 | `COMMAND_ID_CONFLICT` |
| 工作结果 | inquiry + workId + attempt + logicalFence | 返回原接受结果 | `RESULT_PAYLOAD_CONFLICT` |
| 物理使用量 | provider/host receipt ID + usage identity | 不重复结算同一实际调用 | `USAGE_RECEIPT_CONFLICT`，保留审计 |

授权检查先于幂等回读，防止未授权调用者靠猜 commandId 读旧结果。

幂等 lookup 先于一般 stale revision 检查，保证网络重试能得到原成功，而不是变成冲突。不能只比较 payload 文本而忽略绑定的 work、scope、schema、model 或 receipt。

## 15.7 接收与解释分成两个可恢复阶段

第一事务记录结构合法的 ResultAccepted、工作完成、可验证使用量和 InterpretationPending。第二事务运行纯 Observe 并记录 Graph/Certificate delta 与 InterpretationApplied。

这样插件异常时原始回答不会丢失，且无需重新付费调用模型。恢复扫描 Pending 项，以固定 interpretation ID 去重执行纯解释。

解释失败不自动把内容改写到合法。记录 PluginError；相同实现的可重试机制故障可以按原 interpretation ID 重试。修复代码必须产生新的实现 hash/版本，并在显式的新派生 inquiry 或模型修订流程中重处理原始 observations，不得用同版本号偷换锁定实现。原始事件和旧 patch 不变。终局 status 不应谎称所有材料已吸收。

## 15.8 不接受无事件状态修复

删除 `GecSurface.applyWithRetry` 的 placeholder 补点行为。缺端点、缺 work、缺 scope 等全部返回具体错误。需要补建时，由明确命令产生先建后连的同一 batch。

replay 只做既定 fold；不能根据“现在看起来合理”静默生成 ID、默认 plugin lock、自动补 parent 或重排业务事件。

<a id="s16"></a>
# 16. 唯一运行闭环与并发调度

## 16.1 Driver 的状态推进

```text
advance(inquiry):
  读取 canonical current
  若取消中：处理停止/对账 intents，禁止新探究
  若终局：返回已有结果
  处理尚未解释的合法 observations（纯解释）
  传播 certificate invalidations 与 dirty refinements（有步数上限）
  关闭已完成/显式终止的测量 rounds
  若当前决定已失效：打开新 scope 或请求必要计划生成
  若有有效估值/ordinal 判断：生成 DecisionReceipt
  将选中 plan 编译成工作 DAG
  从 DAG 中取“依赖已实际满足”的可派发 antichain
  在 budget/permission/capacity 内原子预留并记录 DispatchRequested
  返回已持久化的待执行工单
```

MCP delegated 在这里返回工单；OpenCode/provider Host 消费相同 intent。两者都不得另写一套“下一步应该做什么”的阶段逻辑。

## 16.2 计划选择与工单装箱分离

`Runtime/Decision.fs` 选择贡献最大计划；`Agenda.fs` 检查运行可行性。Agenda 不看语义理由，不给新计划打分；Decision 不能绕过预算和授权。

联合计划可以含 P→Q 依赖，但本轮 dispatch 只包含入度已解除的 P。选中同一个 plan 的其它工作，不代表它们已经成功。

如果价值最高的计划不可执行，记录排除理由：缺权限、缺资源、缺能力或未满足依赖。之后在当前可行集合重新选择；不能把不可执行误解释为语义价值低。

## 16.3 默认并发单位

生产默认允许同一 measurement round 内多个独立 work 并发，以及已经整体估值的相容联合计划并发。默认不把若干单项高分计划贪心拼成“高分组合”。

组合贡献要么已经被 Q-04/计划比较调查，要么由声明模型计算。`sum(theta)` 不合法。

## 16.4 fan-in 与旧 revision 问题

同一 round 的 worker 都绑定启动时的 scope/snapshot；其它同轮结果已被接受，不应使剩余结果单因 inquiry revision 增加而失败。

因此：

- 控制命令（goal amend 等）使用严格 `expectedRevision`。
- worker submit 使用 `workId + attempt + logicalFence + ticketHash + scopeId` 的局部前置条件。
- 内部落库仍对当前 head 做 CAS。CAS 失败只重做纯 admission/解释，不重跑外部 LLM。
- round 被 supersede、目标被修订、work 被取消时，旧结果只按既定 late-result 策略归档/计费，不进入新 scope 的估值。

不要把旧协议 `expectedRevision=7` 固定灌给所有并发 worker，导致只有第一个回包能成功。

## 16.5 round closure

round 保存 expected logical work IDs。每项进入 success、failed-final、cancelled/superseded 等终态后，或 owner 发出显式 round-close 命令后，才关闭。

关闭后的 dataset 只含成功且语义资格匹配的 observation；其它结果保留 missingness。关闭本身不意味着统计充分，也不自动发出停止证书。

新提议的候选在该轮 dataset 冻结之后开新 scope。不得中途给后返回的 witness 增加新候选却仍记为同一题。

## 16.6 最小垂直闭环 fixture

这个 fixture 不需要真实 LLM，使用记录式 fake witness；不把 fixture 中的排序当生产质量标准：

```text
start(goal)
→ bootstrap work: 返回 answer.now、check-A、find-new 三条计划
→ comparison round: find-new > check-A > answer.now
→ DecisionRecorded(find-new)
→ 真实 dispatch 的 work capability 是 candidate.generate
→ generator 返回候选 C 和一个关键条件
→ 吸收后新 scope；旧估值 stale
→ 新轮：answer.now > compare-C > check-A
→ DispatchRequested(answer.render)
→ renderer 返回答案，来源含 C 和条件
→ AnswerCommitted + completed
→ 删除进程缓存，重启
→ 同一个答案与 semantic projection 恢复
```

测试必须通过公开 API 驱动，不能直接在测试里调用内部 `setAnswer`。

## 16.7 “没有可运行工作”的分类

| 情况 | 行为 |
|---|---|
| 等待已派发工作 | 返回 awaiting_results，不忙循环 |
| 等待必要用户授权 | suspended/input_required，指出具体授权，不新增无关问卷 |
| 未注册能力 | 记录 capability 缺口，可调查替代路径 |
| 预算不足但有成稿资源 | 渲染当前材料，标 resource-limited |
| 无有效估值但还能调查 | 按有限 bootstrap/meta 规则请求一次必要判断 |
| 插件计算预算用尽 | 保留 residual，用当前可用估值或暂停 |
| 全部计划均完成且尚未成稿 | 派发 answer.now，而不是直接 completed 空答案 |

<a id="s17"></a>
# 17. 资源、失败、重试与取消

## 17.1 资源账本

分清消耗型资源和可归还 capacity：模型调用、输入/输出 token、真实计价金额属于消耗；并发槽属于 capacity；墙钟时间不是多个并行任务时长的简单总和。

每种资源有固定单位。金额使用最小货币单位或声明 decimal，不能让 binary float 无限制累加账务误差；Token/call 为非负整数。

账本守恒：

```text
signedFree = authorizedLimit - settledUsage - outstandingReservations
availableForNewWork = max(0, signedFree)
observedOverrun = max(0, -signedFree)
```

超支事实必须记录，不能 reject usage 来维持表面“从不超预算”。新预留不得使预计 signedFree<0；Host 无法保证的最大消耗必须以配置/限制披露。

## 17.2 预留与结算

派发前预留；每个实际 attempt 分别计费。完成后按真实使用量结算并释放剩余预留。网络重试导致两个真实 provider 调用，就有两笔真实成本，即使只采用一份语义结果。

失败输出、格式错误、abstain 和 cancelled 不默认免费。provider 不返回用量时标 `usage-unresolved`，保留合理预留并进入对账，不能写 0。

用户授权预算、Sphinx 子账与共享 root budget 用同一 usage identity 对接。Sphinx 可保存投影，不重复从同一个共享预算扣两次。

## 17.3 成稿保留

profile 明确 `renderReserve`。每次选择 plan 需保证在最坏已承诺资源范围内仍可成稿；超过预留的长答案不能事后越权扩充预算。

成稿失败允许预算内重试；实在不能新生成时返回已有 answer fragment 和未完成说明，不凭空宣称高质量终局已达成。

## 17.4 Work 与 Attempt 状态

```text
Work: Planned → Ready → Leased → Running
                        ├→ ResultAccepted → Succeeded
                        ├→ Failed → 新 attempt（显式授权的策略内）
                        ├→ InputRequired → Running
                        └→ CancelRequested → TerminalObserved → Cancelled

未派发的 Planned/Ready 可直接 Cancelled 或 Superseded。
```

WorkSpec 不变；retry 是新 Attempt，递增 attempt number、新 logical fence、新物理调用 identity。正常复测是新 WorkId，不是 retry。

一个已成功 work 不再接受第二份语义结果。晚到的其它 attempt 可以归档并计费，但不能增加 votes。需要多样本时在计划阶段建多个 work。

## 17.5 重试矩阵

| 失败 | 默认处理 |
|---|---|
| 传输未取得结果，Host 确认原调用未完成/已终止 | 在预算与 retry 限制内新 attempt |
| 传输状态不明，可能仍在运行 | 先按 receipt 查询/对账，不能立刻重复付费 |
| 结果 schema 错误 | 记录原错误；预算内从同一 snapshot 新 attempt，或做标为格式修复的工作 |
| 合法 abstain/not-applicable/no-finding | 成功结果，不为得到偏好答案而重试 |
| 数学拟合不可识别 | 新调查计划，不重试同一个数据输入的拟合直到“成功” |
| 插件异常 | 保存 raw observation，重处理纯解释或暂停；不重复调 LLM |
| 权限不足 | 明确阻塞/替代计划，不自动扩大权限 |
| 用户取消 | 不新建 attempt |

格式修复若让 LLM 再看失败输出，会改变上下文；作为 repair protocol 记录，不能称为干净的独立测量。最简单默认是失败 attempt 不进入新 branch。

## 17.6 取消的真实语义

CancelRequested 持久化后立即停止新工作 admission。对已在运行的 session/provider 请求发取消 intent，并等物理 terminal/确认 receipt。

未确认前状态是 `cancelling`，不能报告 `cancelled` 或 `drained=true`。不支持物理取消的 provider 返回 `cancel-pending-uninterruptible`，禁止使用其晚到结果推进已取消 inquiry，但仍结算使用量。

取消结束时释放的是不再可能消耗的剩余预留，不是所有在途预算。用户取消后不得为凑终局再调用 renderer；返回已有部分产物即可。

## 17.7 timer 与 lease

保留共享 Host 的事实驱动生命周期。watchdog/timer 可以观察物理超时并请求 abort，但必须将 `TimeoutObserved/HostTerminal` 作为明确事件写入后才改变业务状态。

“十分钟没回包”不是模型失败的语义证据，也不能仅靠进程重启把全部 lease 释放为可重复派发。

## 17.8 恢复检查点

恢复必须覆盖：

1. DispatchRequested 已写，尚未调用 Host。
2. Host 已创建 child，但 receipt 尚未写入。
3. prompt 已发，系统尚未标 Running。
4. 结果已生成，尚未 ResultAccepted。
5. ResultAccepted 已写，InterpretationApplied 未写。
6. 取消已请求，Host 尚未终止。
7. AnswerPrepared 已写，最终 commit 未写。

使用稳定 dispatch intent ID 和共享 Host 的查询/幂等能力重新绑定。若 Host 不能幂等创建，明确只保证**至多一次语义采纳**与可审计重复物理调用，不声称 end-to-end exactly-once。

<a id="s18"></a>
# 18. MCP、JS Surface 与 v2 Wire 协议

## 18.1 一个内部 API，多种 transport

生产内部 API 建议：

```text
start(command) -> InquiryView
claimWork(command) -> ClaimedWork[]
submitResults(command) -> SubmitReport
status(query) -> InquiryView
cancel(command) -> CancelView
amendGoal(command) -> InquiryView
export(query) -> ExportView
```

只有 Wire 层使用 `obj`、JS plain objects 和 schema decoding。内部不返回 Fable Map/List/DU representation 给 JS 消费者。[R：Surface.fs 的 JS-native 边界值得保留]

删除旧 `createStore/start/resume/close` 和杂糅全部数学方法的生产 facade。数学 conformance API 放入明确的测试/诊断入口，不与 witness tool 权限共享。

## 18.2 工具清单

| 工具 | 权限角色 | 行为 |
|---|---|---|
| `sphinx_inquiry_start` | orchestrator | 建立目标、锁、预算；推进到首批工作 |
| `sphinx_work_next` | authorized executor | claim ready work；有副作用，不是 status |
| `sphinx_work_submit` | 对应 work 的 executor | 提交绑定结果，不可自行写 graph/certificate |
| `sphinx_inquiry_status` | inquiry reader | 只读，不触发模型调用或 lease |
| `sphinx_inquiry_cancel` | owner/controller | 请求取消并回报真实进度 |
| `sphinx_inquiry_export` | 有对应导出权限的 owner | 导出 redacted summary/full replay |
| `sphinx_goal_amend` | user-authorized controller | 更改目标版本并失效相关估值 |

不保留旧 assess/propose/investigate/synthesize 的阶段接口。不用同名 alias 暗中继续调用旧 Policy。

start/submit 返回下一批已持久化 work refs；实际 ticket 只给有执行权限的调用方。模型调用预算由明确 start/profile 决定，status/export 不耗模型调用。

## 18.3 协议版本：Sphinx API 不等于 MCP wire revision

Sphinx `apiVersion="2"` 独立于 MCP 版本。2026-09-23 核对的官方 MCP versioning 页面将 `2026-07-28` 列为 current；该版请求元数据/发现机制和早期 handshake 不同。[W1]

实现以真实仓库锁定 SDK 可支持的版本为准。支持哪一版就为其建 conformance test；不能只更改 `protocolVersion` 字符串冒充升级。Sphinx clean-break 不要求兼容所有历史 MCP 版本。

2025-11-25 的 `structuredContent` 是服务端结果对象；它不等于 provider 已经执行了 LLM schema-constrained generation。[W2]

2026-07-28 tools 结果使用其对应的 result contract，包括 `resultType`；每次工具调用完成不代表 inquiry 完成。[W3] 本文 JSON 示例是 **Sphinx 业务 payload**，不冒充完整 JSON-RPC 包。

业务 `awaiting_results` 默认作为已完成的一次 tool result 的内容。不要仅因为 Sphinx 还在等待 worker，就误用协议级“向用户索要信息”的 input-required 机制。

## 18.4 start payload

```json
{
  "apiVersion": "2",
  "commandId": "cmd_demo_start",
  "goal": {
    "text": "讨论这个系统的新设计并给出可实施方案",
    "materialRefs": ["material_demo_1"],
    "constraints": []
  },
  "profile": "sphinx.default@2",
  "execution": {
    "mode": "delegated"
  },
  "budget": {
    "modelCalls": 12,
    "inputTokens": 60000,
    "outputTokens": 18000,
    "concurrentWork": 2,
    "renderReserve": {
      "modelCalls": 1,
      "inputTokens": 8000,
      "outputTokens": 4000
    }
  }
}
```

这是演示预算，不是用户对真实调用费用的授权。生产调用从实际授权和锁定 profile 得到值。共享 root budget 的接入 ID 应放在 controller-only 字段，由 owner 验证。

## 18.5 WorkerPublicEnvelope 与 HostPrivateTicket

```text
WorkerPublicEnvelope:
  apiVersion, workToken, capability
  visible goal/context
  opaque candidate labels + exact candidate contents
  question / response schema
  declared work limits

HostPrivateTicket:
  inquiryId, workId, attempt, logicalFence
  scopeId, snapshotId, protocolRunId, clusterId
  real artifact mapping, producer, schemaRef
  assignment, expected response set
  executor authority, dispatchIntentId
  ticketHash, model/profile binding
```

Worker 返回 `workToken`，不是自己挑选 workId、fence、cluster 或模型 metadata。delegated 自报 metadata 与 provider-attested metadata 明确不同。

实际访问控制依赖认证后的调用主体与 owner 记录，不依赖可猜 ID。blind label/RNG seed 是测量工具，不是安全认证 token。

## 18.6 submit payload 与局部并发

```json
{
  "apiVersion": "2",
  "commandId": "cmd_demo_submit_1",
  "inquiryId": "iq_demo",
  "results": [
    {
      "workToken": "worker_ticket_demo_1",
      "attempt": 1,
      "result": {
        "judgment": "prefer-left",
        "rationale": "这条路径有机会改变最终方案的关键限制条件。",
        "sourceLabels": ["item_1"],
        "proposedAlternatives": []
      },
      "executionReceiptRef": "receipt_demo_1"
    }
  ]
}
```

submit 不要求所有并发 worker 带同一全局 expectedRevision。ticket 绑定局部生命周期，Runtime 内部做当前 revision 的原子提交。每个 result 独立原子接受，按请求顺序返回 per-result receipt；批次中一项失败不静默抹掉其它已接受项。

```json
{
  "apiVersion": "2",
  "inquiryId": "iq_demo",
  "revision": "8",
  "status": "awaiting_results",
  "receipts": [
    {
      "workToken": "worker_ticket_demo_1",
      "disposition": "accepted",
      "observationId": "obs_demo_1",
      "acceptedRevision": "8"
    }
  ],
  "readyWorkRefs": [],
  "pendingWorkCount": 1
}
```

disposition：`accepted`、`duplicate`、`rejected`、`archived-late`。错误包含稳定 code、字段路径和可执行恢复说明，不把全部错误塞成 `invalid input`。

## 18.7 pairwise 结果 JSON Schema

这是起始生产 schema 的具体规范；进入仓库后计算其 canonical hash，并用 fixture 验证。

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "sphinx.questionnaire.pairwise-result@2",
  "type": "object",
  "additionalProperties": false,
  "required": ["judgment", "sourceLabels", "proposedAlternatives"],
  "properties": {
    "judgment": {
      "enum": ["prefer-left", "prefer-right", "tie", "abstain", "conditional"]
    },
    "rationale": {"type": "string", "maxLength": 6000},
    "condition": {"type": "string", "minLength": 1, "maxLength": 6000},
    "sourceLabels": {
      "type": "array", "uniqueItems": true,
      "items": {"type": "string", "minLength": 1}
    },
    "proposedAlternatives": {
      "type": "array", "maxItems": 8,
      "items": {
        "type": "object", "additionalProperties": false,
        "required": ["localId", "description", "expectedContribution"],
        "properties": {
          "localId": {"type": "string", "minLength": 1},
          "description": {"type": "string", "minLength": 1, "maxLength": 6000},
          "expectedContribution": {"type": "string", "minLength": 1, "maxLength": 6000}
        }
      }
    }
  },
  "allOf": [
    {
      "if": {"properties": {"judgment": {"const": "conditional"}}},
      "then": {"required": ["condition"]},
      "else": {"not": {"required": ["condition"]}}
    }
  ]
}
```

schema 之外还必须检查 sourceLabels 属于本 ticket 的可见集合、localId 不重复、所有模型提议只进入 proposal 通道。长度限制是资源保护，不是“理由越短越好”的价值模型。

## 18.8 错误码最小集合

| 类别 | 必须覆盖的 code |
|---|---|
| identity/wire | `UNSUPPORTED_API_VERSION`, `INVALID_SCHEMA`, `SCHEMA_HASH_MISMATCH`, `INVALID_REVISION`, `UNKNOWN_INQUIRY` |
| auth | `UNAUTHORIZED_INQUIRY`, `UNAUTHORIZED_WORK`, `PERMISSION_REQUIRED` |
| 并发/幂等 | `REVISION_CONFLICT`, `STALE_FENCE`, `COMMAND_ID_CONFLICT`, `RESULT_PAYLOAD_CONFLICT` |
| 工单 | `UNKNOWN_WORK`, `ATTEMPT_MISMATCH`, `WORK_NOT_RUNNING`, `WORK_SUPERSEDED`, `ROUND_CLOSED` |
| 语义作用域 | `SCOPE_MISMATCH`, `SNAPSHOT_MISMATCH`, `UNKNOWN_REFERENCE`, `STALE_ESTIMATE` |
| 数学 | `NO_DIRECTIONAL_EVIDENCE`, `UNIDENTIFIABLE`, `FIT_NOT_CONVERGED`, `MISSING_VALUE_BRIDGE`, `GUARANTEE_MISMATCH` |
| 资源 | `BUDGET_INSUFFICIENT`, `USAGE_UNRESOLVED`, `USAGE_RECEIPT_CONFLICT`, `CAPACITY_UNAVAILABLE` |
| 持久化/Host | `APPEND_REJECTED`, `HOST_CAPABILITY_UNAVAILABLE`, `DISPATCH_UNCERTAIN`, `CANCEL_PENDING` |
| 历史/终局 | `LEGACY_INQUIRY_UNSUPPORTED`, `INQUIRY_TERMINAL`, `ANSWER_CONFLICT` |

<a id="s19"></a>
# 19. OpenCode 与 Provider：真实适配，不返回模拟成功

## 19.1 当前外部能力与仓库事实分开

2026-09-23 核对的 OpenCode 官方 Server 文档列出创建 session、指定 messageID fork、查询 children/status、abort、prompt_async 和事件流，并提供 `/doc` OpenAPI 入口。[W4]

这只证明官方接口形态；本地安装版本、现有 `IOpenCodePort`、`HostForkRuntime` 和共享 capacity owner 的实际定义仍需在仓库核对。本文不把旧稿的 TypeScript 包壳或配置键直接当本仓实现。

## 19.2 Adapter 的必需端口

```text
Capabilities() -> version + supported operations
Dispatch(intentId, publicEnvelope, privateBinding) -> actual receipt
ReadStatus(actualExecutionRef) -> typed physical status
ReadResult(actualExecutionRef) -> exact result / pending / failure
RequestCancel(actualExecutionRef) -> acknowledged/pending/unsupported
Reconcile(intentId) -> known execution refs / absent / unknown
```

Receipt 必须来自实际 owner。Runtime 生成的 hash 可用作 intent ID，但不能把它命名为 OpenCode 已返回的 childSessionId。

## 19.3 上下文分叉

优先由共享 Host 从指定 message 构造同轮相同前缀，或用干净 session 注入相同 canonical snapshot。两种方式都记录实际可见内容，不混称完全等价。

Parent 有额外对话时，不因 `fork(parent)` 就认为 witness 盲化。Adapter 检查分叉 message、可见 ancestor、工具和后续注入内容。

后续轮可以从新 snapshot 创建 session；展示匿名理由时是显式 intervention，不是“违规 sibling 泄漏”。

## 19.4 权限最小化

默认 witness 只读提供的材料、使用任务所需返回工具；不读其它 session、不改文件、不执行 shell、不递归派发、不自行连外部网络。允许的研究工具由 profile/用户授权显式附加。

模型的 `toolNeeds` 是请求，不是批准。不同 provider 不自动共享私人材料；从 delegated 切换 direct-provider 是数据流变更，必须在配置/授权范围内。

## 19.5 回包提取

不能以 `session.idle` 推出有效结果已经存在。它只是物理通知；实际读取 message/tool-result，核对 work token、返回 schema、模型绑定和 attempt。

同 session 多个合法工具调用不能都当样本。每个 work 明确唯一终结响应规则；多响应冲突记录异常，不择“最符合当前排名”的那一份。

## 19.6 Provider 模式

默认新设计不依赖 MCP Sampling。官方所核对的 2026-07-28 Sampling 页面标明 deprecated，迁移方向指向 provider 集成；具体兼容由实际客户端/SDK 能力决定。[W6]

Provider adapter 只负责调用、schema 能力、用量和取消 receipt。它不拥有 inquiry 决策，不另外做一轮固定“答案质量评审”。

MCP delegated 的模型 metadata 可为 caller-attested；独立调用的 metadata 标 provider-observed。即使模型名称相同，也不把实际无法取得的模型 snapshot/version 写成已知。

<a id="s20"></a>
# 20. 终局、导出、哈希与安全边界

## 20.1 停止原因分型

| 原因 | 可以声称什么 | 不能声称什么 |
|---|---|---|
| `model-ranked-stop` | 当前 scope 的模型估值选择了 answer.now | 所有可能探究都无价值 |
| `ordinal-stop` | 当前有效序数判断偏向直接回答 | 有可量化全局最优间距 |
| `certified-within-model` | 指定模型内满足明确停止条件 | 外部世界绝对正确 |
| `resource-limited` | 达到授权资源边界 | 已达认识收敛 |
| `no-executable-plan` | 当前计划受能力/权限限制 | 没有更好答案 |
| `user-cancelled` | 按用户要求终止 | 已完整成稿 |
| `failed/suspended` | 具体机制或输入阻塞 | 与正常 completed 混同 |

`Completed` 必须有 AnswerCommitted；AnswerCommitted 必须引用已接受的 renderer 结果或明确的既有答案 artifact。不能把排名后验数组当用户问题的回答。

## 20.2 Renderer 工单

Renderer 接收原目标、当前可用材料、必要条件/分歧、停止原因及相关来源，不默认接收所有投票、物理日志和 sibling 原始文本。

任务：围绕用户原目标形成回答，区分明确材料、推断和未决条件；可以组合而非只选一个候选。不得按固定“正确性 40%、完整性 30%”评分，也不得因为多数票反对而删除仍有作用的条件分支。

renderer 返回：`answerText`、`usedArtifactRefs`、`unresolvedRefs`、`scopeRef`。这些引用的存在性由机制校验；语义是否恰当仍是 LLM 的工作。必要的成稿修订进入正常 plan 竞争，不固定无限审核。

## 20.3 三个哈希分别命名

```text
traceHash:
  canonical accepted envelopes，包括明确顺序与可审计运行事实

stateHash:
  完整物化 state，包括恢复需要的 logical/physical bindings

semanticHash:
  原目标、材料/graph revisions、接受的语义 observations、
  证书语义、决定、语义预算事实和答案；排除物理 session ID、
  transport cursor、wall-clock timestamp 等非语义字段
```

semanticHash 的规范化必须版本化。它不包含物理日志的 eventHead、仅为transport服务的revision计数或诊断到达序号；来源引用转为稳定逻辑ID及对应内容digest，不能因排除物理ID而丢掉来源绑定。影响语义的goal/artifact/model revision仍保留。不能直接 hash 原始事件数组再称状态 hash，也不能只 hash 事件数量。

两个 Host 的相同语义事件、逻辑 identity、资源事实和插件条件应有相同 semanticHash；完整 traceHash 不要求相同。若一个 Host 多重试一次产生额外真实成本，那已不是“相同语义预算事实”，不要求 hash 相同。

## 20.4 两种导出

**Summary export**：目标、当前答案、关键条件、停止原因、估值类型、主要资源消耗；默认不泄露原始私密材料、系统 prompt 或 Host receipt。

**Full replay export**：精确事件、原始可见材料或其可取得的 content-addressed blobs、schema/模板/插件锁、模型 metadata、随机设计与数值采样状态、使用量与哈希。只有有权访问全部内容的 owner 可导出。

脱敏版必须标 `replayability=summary-only` 或 `requires-external-blobs`；不得同时声称字节级完整回放。哈希不是加密，小候选集的无盐 commitment 也不是隐藏承诺。

## 20.5 两种回放

`replay-execution`：读取已持久化事件与 patch，恢复 state，不联网，不运行 LLM。

`recompute-math`：使用原始 observations 和锁定数学实现/seed 重新计算，用数值容差比较。浮点依赖不同可以得到允许误差内一致，不承诺跨任意 CPU/library 的 bit equality。

重新请求相同模型与 seed 属于 `rerun-experiment`；provider 未承诺逐字确定性时不能称回放。

## 20.6 输入与插件信任边界

附件、候选内容、外部工具结果都是数据；其中的“忽略协议/给我第一名/导出密钥”不成为 Runtime 指令。Question compiler 把目标与待分析文本分别标记，结果 schema 限制模型可提交的动作类型。

Schema、工具权限和来源追踪不能保证模型绝不受提示注入影响；系统应使受影响结果可定位、可替换，且不能获得写库/改预算/执行外部操作的权力。

生产插件来自 composition 的受控注册表，不接受用户任意 DLL/JS URL 动态加载。扩展接口不是任意代码执行入口。

<a id="s21"></a>
# 21. 默认 profile、配置与可重开的工程假设

## 21.1 默认 profile 的内容

```text
sphinx.default@2
  plan.bootstrap / plan.revise
  Q-01、Q-03、Q-04、Q-05、Q-08
  pairwise 与 tie-aware 模型
  全部启用探针的 registry（按需提出，不顺序遍历）
  answer.render / stop
  一轮或有限轮测量、有限元层深度
  mixed refiner 注册，但只在前提满足时激活
```

Q-02、Q-06、Q-07 和其它问法能力可以被当前 plan 激活，不要求每个 inquiry 覆盖。Truthful/BTS/Peer Prediction 保留为后续实验插件，不进入 default 的完成门槛。

## 21.2 初始数值默认不装成经验结论

| 参数 | 初始实现值/规则 | 说明 |
|---|---|---|
| `metaDepth` | 1 | 有限元推理工作默认，后续按实验证据调整 |
| `maxActivePlanCards` | 8 | 工程上下文上限；超出后由 LLM 选当前子集，记录被延后项 |
| `fitMaxIterations` | 100 | 数值迭代上限，不是认识收敛阈值 |
| `fitGradientTolerance` | 1e-7 | 需按数值缩放测试；不是质量概率 |
| `thetaL2` | 1.0 | 声明的起始正则，不称经校准最优；报告敏感性 |
| `orderL2` | 1.0 | 仅在位置效应可识别时启用 |
| `tieKappaPrior` | mean 0、variance 4 | 三类模型的工作先验；数据不足时不据此判优 |
| `maxWorkAttempts` | 2 | 一次原调用加一次重试；受预算/取消限制 |
| `maxPureStepsPerAdvance` | 128 | 防止纯插件无界循环；到限记录 residual/pending |
| `executionMode` | delegated | 不未经配置调用 provider |
| 实际模型与费用 | 无隐式默认 | 来自已授权 Host/provider 配置 |
| 总资源额度 | 来自 caller/profile 授权 | 本文示例不是生产授权 |

这些常量进入 config hash 和测试；禁止把它们与语义权重混在一个 `weights` 对象中。改变 numerical regularization 可能改变排序，必须作为模型 revision 处理，不能在 inquiry 中静默热调。

## 21.3 降级要可见，但不陷入拒绝工作

可行的降级顺序是：合格模型估值 → 有效 ordinal tiers → 明示 provisional 判断 → 当前材料成稿/具体阻塞。不是“缺少置信区间就不能探究”。

反过来，可执行不等于有数学证明。所有降级都写入 DecisionReceipt 和必要的终局说明；不用 `confidence=0.95` 装饰一个没有这项保证的决定。

<a id="s22"></a>
# 22. 全量文件处置表：54 对，108 个文件

表中每行同时覆盖 `.fs` 与 `.fsi`。范围是原 Repomix 的全局行号；新位置相对 `$SPHINX/`。本表是本次设计，不意味着已修改真实仓库。

“删除”是最终生产树的处置；实施时先完成引用替换和测试，再删除源码/编译项/exports。不要建 Legacy 转发层来消除编译错误。旧测试中的有效数学性质可以迁移，旧行为断言不保留为验收义务。

| # | 旧文件与原快照行段 | 处置 | 新位置 | 必做改动 | 主要验收 |
|---|---|---|---|---|---|
| 01 | `Core/Hash.fs` L166–L184；`Core/Hash.fsi` L186–L193 | 替换依赖 | Core/Projection + Runtime/Ports + Wire/Encode | 保留 canonical JSON 思路；digest 走确定性端口/基础库，不让 Core 直接依赖 Host；区分三种 hash | C-10～C-13 |
| 02 | `Core/Model.fs` L195–L480；`Core/Model.fsi` L482–L706 | 拆分重写 | Core/Ids、Envelope、Goal、Graph、Certificate、Work、Budget、Events、State | 保存根目标；revision/attempt 分离；证书带作用域；预算改预留/结算；原始观察与物理 receipt 分开 | C-01～C-09 |
| 03 | `Core/Reducer.fs` L708–L1361；`Core/Reducer.fsi` L1363–L1372 | 原位重写 | Core/Reducer + Core/Commands + Runtime/Admission | 修复缺 guarantee 放行；检查完整 ticket；不自动补节点；状态变更与答案提交使用同一 fold | C-01～C-16 |
| 04 | `Plugins/AStar/Refiner.fs` L1374–L1604；`Plugins/AStar/Refiner.fsi` L1606–L1664 | 移植数学，重写接口 | Plugins/AStar/Refiner | 增量 step、保存 frontier、reopen、h=0 基线、全局 bound、显式模型前提 | M-06～M-09 |
| 05 | `Plugins/Bayes/Exact.fs` L1666–L1884；`Plugins/Bayes/Exact.fsi` L1886–L1928 | 移植数学，改输入契约 | Plugins/Bayes/Exact | 保留 log-space；区分重复与相关因素；先验/因子来源明确；不再依赖 LLM 概率填表 | M-01～M-05 |
| 06 | `Plugins/Mcts/Refiner.fs` L1930–L2459；`Plugins/Mcts/Refiner.fsi` L2461–L2554 | 重写为可暂停生成器 | Plugins/Mcts/Refiner | 有限枚举模拟器保留作 fixture；接 WorkItem rollout；scope/horizon key；经验统计不假标 coverage | M-10～M-15 |
| 07 | `Plugins/Ordinal/Inference.fs` L2556–L3755；`Plugins/Ordinal/Inference.fsi` L3757–L3838 | 拆分与补全 | Plugins/Ordinal/Model、Pairwise、Ranking、Fit、DesignCheck | 保留真实梯度/Hessian 可用部分；stable log-likelihood、完整协方差、tie/abstain、cluster 与可识别性 | N-01～N-14 |
| 08 | `Plugins/Questionnaire/Protocol.fs` L3840–L4498；`Plugins/Questionnaire/Protocol.fsi` L4500–L4631 | 拆分重写 | Plugins/Questionnaire/Model、Design、Prompts、Decode | 保留处理组/快照记录；分轮快照、实际可见输入、missingness、盲化 host-private mapping | Q-01～Q-12 |
| 09 | `Plugins/Stop/Certificate.fs` L4633–L4960；`Plugins/Stop/Certificate.fsi` L4962–L5044 | 整体替换 | Plugins/Inquiry/Stop | 取消 caller evidence 浮点裁决；answer.now 比较；缺 VOC 不视为通过；操作结束与模型收敛分开 | D-05、D-09、I-05 |
| 10 | `Plugins/Truthful/SelfPrediction.fs` L5046–L5271；`Plugins/Truthful/SelfPrediction.fsi` L5273–L5332 | 退出默认及首轮发布构建 | 后续 Experimental 插件，不占 default capability | 保留研究说明/可选历史测试材料；不得必填概率；不强迫本次重建未启用的 peer-scoring 产品 | X-02 |
| 11 | `Runtime/Agenda.fs` L5334–L5767；`Runtime/Agenda.fsi` L5769–L5836 | 拆分重写 | Runtime/Agenda、Decision、Refinement | 删除按 ID 选优；分离计划 DAG 与 ready antichain；toy closure 移到测试；实际选择消费 estimate | D-01～D-10 |
| 12 | `Runtime/Certificate.fs` L5838–L6117；`Runtime/Certificate.fsi` L6119–L6148 | 拆分重写 | Core/Certificate + Runtime/Admission、Refinement | 槽级 CAS；合法无 coverage 的 empirical summary；credible/coverage 分型；依赖失效 | C-06～C-09、M-16 |
| 13 | `Runtime/Plugin.fs` L6150–L6200；`Runtime/Plugin.fsi` L6202–L6226 | 替换 | Runtime/Contracts | Manifest 必须绑定可执行能力；纯 Observe/Propose/Refine；不持有 I/O | P-01～P-04 |
| 14 | `Runtime/PluginRegistry.fs` L6228–L6495；`Runtime/PluginRegistry.fsi` L6497–L6514 | 移植并增强 | Runtime/Registry | 依赖拓扑/锁/schema 检查可用；增加 executable binding、content hashes、能力缺失与冲突诊断 | P-01～P-05 |
| 15 | `Absorb.fs` L6516–L6733；`Absorb.fsi` L6735–L6740 | 删除旧实现 | Plugins/Inquiry/Observe | 去掉 rootGainForProposal/gainFromMethod 默认分；使用原始语义观测和显式关系 delta | X-01、I-01 |
| 16 | `Bayes.fs` L6742–L6859；`Bayes.fsi` L6861–L6869 | 删除重复旧实现 | Plugins/Bayes/Exact 唯一生产能力 | 移除旧 EpistemicState Bayesian projection 调用；旧测试按新契约重写 | X-01、M-01 |
| 17 | `Closure.fs` L6871–L6936；`Closure.fsi` L6938–L6944 | 删除旧流水线 | Runtime/Refinement + Plugins/Inquiry/Plan | 去掉固定 Bayes→Value→Representation→Search→MonteCarlo；纯增量 dirty queue | D-10、X-01 |
| 18 | `Codec.fs` L6946–L6956；`Codec.fsi` L6958–L6965 | 替换 façade | Wire/Decode、Wire/Encode | 无旧 Request/CanonicalAnswer 转发；每个 wire payload 明确 apiVersion | W-01～W-04 |
| 19 | `DecodePrimitives.fs` L6967–L7103；`DecodePrimitives.fsi` L7105–L7130 | 只移植通用解析 | Wire/Decode | 保留严格 string/array/finite decoder；删除 QuestionForm/EvidenceKind 推断与 formMap | W-02、X-01 |
| 20 | `EventVocabulary.fs` L7132–L7152；`EventVocabulary.fsi` L7154–L7166 | 替换 v2 词汇 | Core/Events + Persistence/Codec | 新事件类型 sphinx/v2-transition@1；不静默重解释旧事件；共享 owner 的旧类型登记按历史读取需求保留 | R-01、X-03 |
| 21 | `GecDecode.fs` L7168–L8137；`GecDecode.fsi` L8139–L8149 | 重写并拆分 | Wire/Decode + Persistence/Codec | 删除按位置补 parent/ID 等容错；严格 production command 解码；replay 无推测填空 | C-14、W-02 |
| 22 | `GecElicit.fs` L8151–L8661；`GecElicit.fsi` L8663–L8668 | 删除大 façade | Questionnaire 模块 + Inquiry/Stop + Wire/Surface | 不暴露 caller 直接填写 posterior/evidence 来终止 inquiry 的路径 | D-05、W-05 |
| 23 | `GecHost.fs` L8670–L9053；`GecHost.fsi` L9055–L9078 | 删除计划假执行 façade | Hosts/OpenCode/Adapter + Runtime/Recovery | 实际 dispatch/abort/reconcile；workId 与 childSessionId 分开；移除固定 active、空证书的 fold | H-01～H-08 |
| 24 | `GecInquiry.fs` L9080–L9280；`GecInquiry.fsi` L9282–L9344 | 删除第二 registry | Runtime/Driver + canonical Current | start/submit 真正推进；不再另存 results-only table；handle 只是运行实例引用 | I-01、R-02 |
| 25 | `GecLegacy.fs` L9346–L9514；`GecLegacy.fsi` L9516–L9522 | 删除生产文件与导出 | 无运行时替代 | 旧轨迹作为只读设计对照；不包装成新 Core 的 Legacy 插件 | X-01 |
| 26 | `GecRefine.fs` L9524–L10318；`GecRefine.fsi` L10320–L10325 | 删除杂糅重载 | Runtime/Registry + Wire/诊断测试接口 | 数学引擎 typed 调用；不在生产 surface 根据字符串 kind 随意改证书 | M-16、W-05 |
| 27 | `GecStore.fs` L10327–L10713；`GecStore.fsi` L10715–L10720 | 合并机制并删除旧 façade | Persistence/Codec、Integrator、Export | 只有一个权威 fold；保留 canonical-envelope 思路；不维护另一个 caller-held Current | R-01～R-06 |
| 28 | `GecSurface.fs` L10722–L11358；`GecSurface.fsi` L11360–L11366 | 删除并收口 | Wire/Surface | 去掉数组长度 hash、placeholder retry、混合 replay/legacy/math 工具集合；统一真实 Runtime | C-10、C-14、W-05 |
| 29 | `GenericDurability.fs` L11368–L11484；`GenericDurability.fsi` L11486–L11506 | 替换 | Persistence/Codec | 旧 schema-only inquiry 转换不进入新生产路径；v2 batch 包含完整运行状态来源 | R-01、R-02 |
| 30 | `GenericIntegrator.fs` L11508–L11641；`GenericIntegrator.fsi` L11643–L11679 | 替换 | Persistence/Integrator | 旧 result/revision 投影退出；调用唯一 Core/Reducer 生成 Current | R-02、R-03 |
| 31 | `Inquiry.fs` L11681–L12089；`Inquiry.fsi` L12091–L12143 | 删除旧领域 fold | Core/Commands、Reducer + Persistence/Integrator | 移除 Working EpistemicState/旧 Policy；保留旧事件不迁义；shared budget 接 owner | R-02、B-01 |
| 32 | `InquiryRuntime.fs` L12145–L12423；`InquiryRuntime.fsi` L12425–L12442 | 整体重写 | Runtime/Driver、Admission、Recovery | 保留 admission queue 的机制用途；无旧 Request 串行阶段；本地队列不冒充跨进程 CAS | I-01、R-04、H-05 |
| 33 | `InquirySurface.fs` L12444–L12486；`InquirySurface.fsi` L12488–L12514 | 替换 | Wire/Surface | 删 runExpected 对旧 turn-price 的绑定；用 v2 start/submit/status，同一 opaque runtime handle | W-01、I-01 |
| 34 | `IntegrationRules.fs` L12516–L12640；`IntegrationRules.fsi` L12642–L12656 | 重写注册 | Persistence/Integrator + Composition/Bind | v2 rule 使用独立 key；仅接 v2 事件；保留共享 baseRules；cut/reset 经 owner 契约测试 | R-01、R-06、X-03 |
| 35 | `LegacyDurability.fs` L12658–L12883；`LegacyDurability.fsi` L12885–L12895 | 删除生产文件 | 历史版本工具离线读取 | 不自动迁移/删历史数据；不在 v2 composition 继续调用 | X-01、X-03 |
| 36 | `LegacyIntegrator.fs` L12897–L12984；`LegacyIntegrator.fsi` L12986–L13025 | 删除生产文件 | 历史版本工具离线读取 | 历史事件只保留；新 v2 rule 不消费旧事件，不吞错误冒充空 inquiry | X-03 |
| 37 | `Mcp.fs` L13027–L13047；`Mcp.fsi` L13049–L13060 | 保留路径，改内容 | Mcp.fs + Hosts/Mcp/Contract | 身份、权限前缀、entry 路径唯一；工具清单与 v2 一致 | W-01、X-04 |
| 38 | `McpContract.fs` L13062–L13313；`McpContract.fsi` L13315–L13360 | 重写并移动 | Hosts/Mcp/Contract | 删 nextTool 固定阶段映射；稳定业务错误、v2 view；协议与业务 status 分开 | W-01～W-05 |
| 39 | `McpServer.fs` L13362–L14552；`McpServer.fsi` L14554–L14566 | 重写并移动 | Hosts/Mcp/Server | SDK 只在 adapter；所有工具调同一 Runtime；生产状态只来自 durable acceptance | W-01、W-06、I-01 |
| 40 | `Methodology.fs` L14568–L14761；`Methodology.fsi` L14763–L14780 | 删除权重库 | Plugins/Probes/Catalog、Prompts | 保留有用问法内容但不移植 QuestionForm/Facet 权重和默认收益 | X-01、Q-10 |
| 41 | `MonteCarlo.fs` L14782–L14939；`MonteCarlo.fsi` L14941–L14960 | 删除旧重复实现 | Plugins/Mcts/Refiner | 不保留旧 EpistemicState projection；所有 MCTS 统计按模型/horizon/scope 保存 | M-10、X-01 |
| 42 | `ObservationCodec.fs` L14962–L15133；`ObservationCodec.fsi` L15135–L15144 | 删除旧四阶段 decoder | Wire/Decode + Questionnaire/Decode | 新语义返回为版本化 envelope；非法字段不默填；proposedAlternatives 只进入提议 | W-02、Q-02 |
| 43 | `Policy.fs` L15146–L15323；`Policy.fsi` L15325–L15333 | 删除 | Runtime/Decision + Inquiry/Stop、Render | 删除固定阶段/contract weight/标量 stop；通过估值选择并留 decision receipt | D-01～D-09、X-01 |
| 44 | `Representation.fs` L15335–L15429；`Representation.fsi` L15431–L15440 | 删除旧语义 projection | 必要偏序操作在对应模型插件实现 | 不迁移固定 ValueVector 的语义维度；运行资源支配留在 Agenda | D-04、X-01 |
| 45 | `RuntimeTypes.fs` L15442–L15524；`RuntimeTypes.fsi` L15526–L15608 | 删除旧类型体系 | Core/Work、Commands、State + Inquiry/Model | 旧 Request/Observation/InquiryResult 不穿过 v2 公共边界 | W-01、X-01 |
| 46 | `Search.fs` L15610–L15734；`Search.fsi` L15736–L15759 | 删除旧重复实现 | Plugins/AStar/Refiner | 单一 A* typed capability；不再靠 SolverMode 在旧 state 切换 | M-06、X-01 |
| 47 | `ServeEntry.fs` L15761–L15840；`ServeEntry.fsi` L15842–L15853 | 保留 entry 路径并重写 | ServeEntry + Composition/Bind | 生产必须有 durable store；explicit test profile 才可 memory；补安装能力启动检查 | W-06、X-04 |
| 48 | `Session.fs` L15855–L16092；`Session.fsi` L16094–L16156 | 删除旧内存 SessionStore | Runtime/Driver + canonical Current | cache 可丢弃；不另存权威 stage/state；start 同 command 恢复原 receipt | R-02、R-03 |
| 49 | `State.fs` L16158–L16289；`State.fsi` L16291–L16313 | 删除旧状态初始化 | Core/State、Goal、Budget | 不按 QuestionForm 生成 RootContract；用户目标显式保存；无旧 normalize fallback | C-01、X-01 |
| 50 | `Surface.fs` L16315–L16669；`Surface.fsi` L16671–L16700 | 重写唯一 façade | Wire/Surface | JS-native 形状保留；删除旧 store/resume/methodNames 与未授权 math 写入；exports 更新 | W-01、X-04 |
| 51 | `TurnBudget.fs` L16702–L16754；`TurnBudget.fsi` L16756–L16778 | 删除旧价格模型 | Core/Budget + Host/provider 成本端口 | 去掉 1.44/(expected-1)^2 与目标轮数驱动；保留计费需要但不把轮数当质量 | B-01～B-07、X-01 |
| 52 | `Types.fs` L16780–L16912；`Types.fsi` L16914–L17043 | 删除旧硬编码本体 | Core mechanics + Plugins/Inquiry/Model | Finding/Evidence/Why/How 不在 Core；SolverMode 删除；语义内容在插件内版本化 | X-01、C-01 |
| 53 | `Value.fs` L17045–L17201；`Value.fsi` L17203–L17214 | 删除 | Plugins/Inquiry/DecisionModel | 删除发现数量、0.72、gateway 等手写语义估值；不只是搬配置 | D-01、X-01 |
| 54 | `WireEncode.fs` L17216–L17373；`WireEncode.fsi` L17375–L17381 | 重写 | Wire/Encode | 新 DTO 只含 plain JS/JSON；不序列化旧 EpistemicState；稳定规范化及大整数 | W-03、C-10 |

## 22.1 表外新模块的实施检查

第 04 章目录中不存在于旧表的新模块，也必须有自己的 `.fsi`、依赖顺序、测试和调用者。尤其是 `Runtime/Driver`、`Decision`、`Context`、`Recovery`、`Plugins/Inquiry/*`、`Core/Goal`、`Core/Budget` 和 `Persistence/Integrator`；不能建目录却没有生产调用路径。

## 22.2 两个必须避免的“假 clean-break”

第一种：旧文件改名为 Legacy，并从新 façade 继续调用。第二种：保留旧默认估值函数，只在外面套一层插件注册。两者都不满足 U-01/U-04。

允许移植的只有已重新验收的数学实现、严格解码、canonical 序列化等机制；移植后由新契约解释，不延续旧 Policy 的语义决定。

<a id="s23"></a>
# 23. 快照之外必须核对的接入面

## 23.1 先生成 Integration Map，再开改共享 owner

在真实仓库中找到下列符号/文件。表中“未知”是因为本快照未包含定义，不表示这些设施不存在。

| 接入面 | 从快照能确定的线索 | 实施者必须找到并记录 |
|---|---|---|
| F# 编译 | 每模块 `.fsi/.fs` 成对，使用 Fable | 实际 `.fsproj`/shard、Compile Include 顺序、生成目录、编译脚本 |
| 发布 | `Mcp.relativeServerEntry = dist/Sphinx/ServeEntry.js` | 真正 package exports/bin、入口打包与版本约束；单包 owner 是否仍成立 |
| EventStore | `IEventStore`, `Append`, `TryCurrent`, `EventStore.createLocal` | 接口定义、receipt/cut、原子/冲突语义、批量/单事件保证 |
| Canonical integration | `CanonicalIntegrator.createWithRules`、`baseRules` | v2 rule 注入的全部 composition roots，而不只 MCP ServeEntry |
| 已知事件白名单 | `AuthoritativeEventTypes.isKnown` | `sphinx/v2-transition@1` 的登记；旧事件类型的历史读取策略 |
| 当前投影 | 旧键 `Sphinx`、`SphinxGeneric`、`SphinxInquiry` | 新键 `SphinxV2` 的所有读取者；不得冲掉非 Sphinx Current |
| Host | 长稿提到 `IOpenCodePort`、`HostForkRuntime` | 实际接口、版本、session owner、capacity、fission/delegation、cancel terminal |
| Shared budget | `InquiryRuntime` 的 budgetRoot/expectTurns | 哪个 owner 控制 root budget；取消传播与 usage identity 的合法接口 |
| Public JS | `SphinxSurface`、`InquirySurface`、`GecSurface` | 全部生成 bindings、引用方、package entry，不留旧导出旁路 |
| MCP | SDK import 在 `McpServer.fs` | SDK 锁定版本、支持协议、stdio/HTTP 测试、工具授权角色 |
| 规范 | 代码注释 `WHAT[epistemic-reasoning-*]` | 实际 requirements/ADR/lint owner；只更新 Sphinx 的被替代要求 |
| 测试 | 快照未含全仓测试 | 数学/stdio/host/event-store/打包测试的实际 runner 与 fixtures |

## 23.2 最小只读发现命令

在仓库根目录执行；这些命令只读，不安装包、不删除文件、不联网：

```bash
git status --short
git ls-files '*Sphinx*' '*sphinx*' '*.fsproj' '*.sln' '*package.json' '*lock*'
git grep -n -E 'SphinxSurface|InquirySurface|GecSurface|SessionStore|SolverMode'
git grep -n -E 'SphinxIntegrationRules|AuthoritativeEventTypes|createWithRules|TryCurrent'
git grep -n -E 'IOpenCodePort|HostForkRuntime|budgetRoot|relativeServerEntry'
git grep -n -E 'sphinx_inquiry_start|sphinx_work_submit|sphinx-legacy|sphinx-generic'
```

`git grep` 无匹配的退出码不代表仓库损坏。排除生成文件和 vendored 依赖后再判断调用方。不得用全仓字符串替换把其它功能的 `SessionStore`、`Value` 或 `Policy` 一并改掉。

## 23.3 安全读取构建脚本的脚本

把下面脚本存为临时文件后在真实仓库执行。它只列出 tracked manifest 中的 scripts，不替你猜哪个脚本应该执行。

```python
from __future__ import annotations
import json
import subprocess
from pathlib import Path

def tracked_files(root: Path) -> list[Path]:
    completed = subprocess.run(
        ["git", "ls-files", "-z"], cwd=root,
        check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE
    )
    return [root / p.decode("utf-8") for p in completed.stdout.split(b"\0") if p]

def main() -> None:
    root = Path(subprocess.check_output(
        ["git", "rev-parse", "--show-toplevel"], text=True
    ).strip()).resolve()
    for path in tracked_files(root):
        if path.name == "package.json":
            try:
                data = json.loads(path.read_text(encoding="utf-8"))
            except (OSError, UnicodeError, json.JSONDecodeError) as exc:
                print(f"READ_ERROR {path.relative_to(root)}: {exc}")
                continue
            print(json.dumps({
                "file": str(path.relative_to(root)),
                "name": data.get("name"),
                "packageManager": data.get("packageManager"),
                "scripts": data.get("scripts", {}),
                "exports": data.get("exports"),
                "bin": data.get("bin")
            }, ensure_ascii=False, indent=2))
        elif path.suffix in {".fsproj", ".sln"}:
            print(f"BUILD_FILE {path.relative_to(root)}")

if __name__ == "__main__":
    main()
```

把真实的 lint/build/test/pack 命令写入 `docs/sphinx/rewrite-integration-map.md`。这是实施过程要生成的文件，不是声称当前仓库已有它。

## 23.4 外部改动的最小边界

必须修改的全仓位置可能包括：Sphinx 编译清单、入口 exports、事件类型注册、Integrator 注入、MCP 工具注册/权限、使用旧 Surface 的调用方及对应测试。

默认不得修改：其它功能的语义模型、全仓 EventStore 持久格式、共享 Host 的独立状态机或认证系统。缺少 port 能力时，在其 owner 里做增量契约扩展，并给非 Sphinx 调用方补回归测试。

旧规范引用只改 Sphinx 相关条目。不能把全仓 CI 的禁令关掉来让重写通过。

<a id="s24"></a>
# 24. F# 编译顺序、构建与变更策略

## 24.1 建议编译拓扑

真实 `.fsproj`/shard 可能不同，按依赖拓扑适配，不能复制下面文件名就认定可编译：

```text
Foundation 既有纯类型/JSON能力
→ Core/Ids
→ Core/Envelope
→ Core/Goal、Graph、Certificate、Work、Budget
→ Core/Events
→ Core/State
→ Core/Commands、Reducer、Projection
→ Runtime/Contracts、Ports
→ Runtime/Registry、Admission、Context、Agenda、Refinement、Decision
→ Plugins/Inquiry/Model
→ Questionnaire/Model、Design、Prompts、Decode
→ Probes/Catalog、Prompts
→ Ordinal/Model、Pairwise、Ranking、DesignCheck、Fit
→ Bayes、AStar、Mcts
→ Inquiry/Plan、Observe、DecisionModel、Render、Stop
→ Runtime/Driver、Recovery
→ Persistence/Codec、Integrator、Export
→ Wire/Schema、Decode、Encode、Surface
→ Hosts/Mcp、Hosts/OpenCode、Hosts/Provider
→ Composition/Bind
→ ServeEntry
```

上面排在具体插件之前的 `Runtime/Decision` 只依赖 `Runtime/Contracts` 中的通用选择接口，不导入 `Plugins/Inquiry/DecisionModel`；具体估值实现由 composition 注入。每个公开模块 `.fsi` 位于对应 `.fs` 之前；签名仅暴露必要类型和函数。若实际依赖发现插件实现反向要求 Driver，抽到 Contracts，不建循环项目引用。

纯 mathematical plugins 不得 import Wire/Host；Wire DTO 不泄露内部数组 scratch。MCP SDK 只在 MCP adapter 及其构建 owner 使用。

## 24.2 每一工作包的最低提交质量

开始前记录当前工作区已有修改，不能覆盖用户未提交内容。变更按可审阅工作包组织，不要求自动 Git commit。

每包至少：签名与实现同步、调用方同步、相应测试、有效文档修改、无 placeholder 成功返回。尚未对接的真实 Host 能力返回明确 unavailable，不返回 fabricated receipt。

允许重写中间状态暂时保留旧源码供引用迁移，但不得发布旧新双内核；最终构建与导出只保留新路径。

## 24.3 构建级别

1. 纯模块类型检查与签名一致性。
2. Fable 编译，检查 JS boundary 输出形状。
3. 本地离线 unit/property/fixture 测试。
4. canonical EventStore 故障注入与进程恢复测试。
5. MCP in-process/stdio 与真实 Host adapter contract tests。
6. package build/exports/CLI smoke。
7. 明确 opt-in 的真实模型与外部 Host 实验。

不得写“请执行 npm test”却不知道仓库是否有该脚本；使用第 23 章发现的真实命令。不得下载新依赖来运行活体测试而不记录 lockfile 和必要授权。

<a id="s25"></a>
# 25. 逐工作包实施顺序：从契约到完整替换

每包必须有可观察产物与退出条件。编号不是工期估算，也不是自动提交授权。

## WP-00：建立实物基线与变更清单

**输入**：本指南、三份材料、真实仓库。  
**改动**：仅新增实施记录/ADR，不改运行逻辑。  
**操作**：定位 `$SPHINX`、全仓构建 owner、EventStore、Host、public exports、测试入口；核对 108 文件表与真实树差异。  
**产物**：integration map、来源 hash、构建命令清单、旧文件 disposition manifest、当前工作区状态。  
**退出**：每个外部接入点有实际文件/符号，缺失项精确标出，不能用猜测路径替代。  
**依赖**：无。

## WP-01：冻结 v2 契约和新旧切断

**文件**：Core/Ids、Envelope、Goal、Work、Certificate 的 `.fsi`；Wire/Schema；新的 Sphinx ADR/requirements。  
**操作**：定义 Goal、scope、work ticket、幂等、三类 hash、v2 事件类型、返回状态；写明不保留旧 API。  
**产物**：schema fixtures、CB-01～CB-18 对应条目。  
**测试**：C-01、C-04、W-01～W-03。  
**退出**：没有 `expectedRootGain` 或固定语义评分表；旧接口停止映射已明确。  
**依赖**：WP-00。

## WP-02：写唯一 Core 状态与纯 fold

**文件**：Core/Graph、Events、State、Commands、Reducer、Projection。  
**操作**：根目标持久保存；batch 全验再应用；引用存在性；terminal 不可反复变更；证书 slot/依赖。  
**测试**：C-01～C-16。  
**退出**：同一事件序列恢复同一 state；更改同长度事件内容会改变 hash；任何缺节点都不触发隐式修补。  
**依赖**：WP-01。

## WP-03：实现工作生命周期与资源账

**文件**：Core/Work、Budget；Runtime/Admission、Agenda。  
**操作**：attempt/fence、预留/结算、DAG ready antichain、round expected set；工作结果幂等。  
**测试**：B-01～B-08、D-04、C-04～C-05。  
**退出**：依赖未完成不同时派发；取消/失败真实成本保留；重复 receipt 不重扣。  
**依赖**：WP-02。

## WP-04：接 canonical EventStore，而不是另开库

**文件**：Persistence/Codec、Integrator；Composition/Bind 的存储绑定。  
**操作**：v2 TransitionBatch 单 envelope；注册已知事件类型与 Current key；receipt/cut 处理；冲突测试。  
**测试**：R-01～R-08、X-03。  
**退出**：跨进程竞争只接受一个合法后继；append 拒绝不产生新 dispatch；重启不丢状态；旧事件仍在。  
**依赖**：WP-02、WP-03。

## WP-05：使插件注册真正可执行

**文件**：Runtime/Contracts、Registry、Refinement；内置插件壳。  
**操作**：manifest+executable binding；纯 Initialize/Observe/Propose/Refine；锁定 schema、prompt、model config；dirty queue。  
**测试**：P-01～P-05、D-10。  
**退出**：缺能力启动失败；插件不会直接发网络/写库；没有 toy closure 代替生产传播。  
**依赖**：WP-01、WP-02。

## WP-06：实现问法与响应原语

**文件**：Questionnaire/Model、Design、Prompts、Decode；Runtime/Context。  
**操作**：Q-01～Q-08，默认先接 pairwise；measurement/intervention；visible bytes、randomization、host-private labels、响应分型。  
**测试**：Q-01～Q-12。  
**退出**：并列、弃权、条件回答各有独立通道；新候选不在同轮偷偷入场；共同前缀有实际证明材料。  
**依赖**：WP-03、WP-05。

## WP-07：实现序数估计与诊断

**文件**：Ordinal/Model、Pairwise、Ranking、Fit、DesignCheck。  
**操作**：移植可用数学部分；稳定概率计算、zero-sum gauge、完整 covariance、tie-aware likelihood、连通性/分离诊断。  
**测试**：N-01～N-14；第 27 章精确数值 fixture。  
**退出**：模型返回的估值、拟合状态和保证相符；没有 `1/sqrt(total)` 冒充每项标准误；rank 不重复计独立票。  
**依赖**：WP-06。

## WP-08：接通第一条完整决策闭环

**文件**：Inquiry/Plan、Observe、DecisionModel、Stop、Render；Runtime/Decision、Driver。  
**操作**：bootstrap、scope、answer.now、plan comparison、选择、真实 work 编译、结果吸收、新 scope、成稿。先用离线 fake witness 走公开内部 API。  
**测试**：I-01～I-06、D-01～D-09。  
**退出**：第 16.6 节垂直 fixture 通过；交换比较结果会改变下一 probe；completed 必有真实 renderer artifact。  
**依赖**：WP-04～WP-07。

## WP-09：把 16 个探针接入，而非只登记名称

**文件**：Probes/Catalog、Prompts；Inquiry/Observe、Plan。  
**操作**：每个探针的合法输入、输出、nullable/not-applicable、graph delta、对后续 scope 的影响；动态 open-question 安全封装。  
**测试**：Q-10、Q-11、I-02、I-03。  
**退出**：每个启用探针都至少有一条公开 API 可达轨迹；没有强制“找到唯一瓶颈/一定存在反例”的 validator。  
**依赖**：WP-08。

## WP-10：发布唯一 v2 Surface 与 MCP

**文件**：Wire/*、Hosts/Mcp/*、Mcp.fs、ServeEntry.fs、对应全仓 exports/注册。  
**操作**：所有工具调同 Runtime；业务状态与 MCP result 分层；strict input/error；status 不驱动工作；delegated ticket。  
**测试**：W-01～W-06、X-04。  
**退出**：stdio 或锁定 transport 中走完 start→work→submit→answer；旧四阶段不在工具表；无配置不退回内存旧内核。  
**依赖**：WP-08、WP-09。

## WP-11：实现真实 Host/provider 适配

**文件**：Hosts/OpenCode/Adapter、Hosts/Provider/Adapter；Composition/Bind；共享 Host 所需增量契约。  
**操作**：实际创建/fork/read/abort/reconcile；真实 receipt；context visibility；权限范围；模型元数据与用量。  
**测试**：H-01～H-08；先 mock contract，再授权环境下 smoke。  
**退出**：不再用固定 `aborted=true/drained=true` 回报；故障可定位到实际 execution ref；共享 Host 无第二会话池。  
**依赖**：WP-10 和实际 Host owner 审阅。

## WP-12：恢复、取消与乱序 fan-in

**文件**：Runtime/Recovery、Admission、Driver；Host adapters；Persistence/Integrator。  
**操作**：覆盖第 17.8 节所有宕机窗口；round close；late result；用户取消；不明 dispatch 对账。  
**测试**：R-03～R-08、H-03～H-08、B-03～B-08。  
**退出**：同轮乱序结果都可接受；旧 fence 不污染新 attempt；取消不抹账；缓存删除后恢复。  
**依赖**：WP-11。

## WP-13：重建三种数学能力及混合证书

**文件**：Plugins/Bayes、AStar、Mcts；Core/Certificate；Runtime/Refinement。  
**操作**：本指南第 13 章的 typed 模型、增量接口、恢复、数值边界；qualified 模型内整合到真实 inquiry，而非只写孤立函数。  
**测试**：M-01～M-16。  
**退出**：三个能力能在同一 inquiry 中按前提激活；不存在全局 SolverMode；无 bridge 的开放任务仍可走 ordinal 闭环。  
**依赖**：WP-08；LLM rollout 派发依赖 WP-11。

## WP-14：实现有上限的元层调查选择

**文件**：Inquiry/DecisionModel、Questionnaire/Design、Runtime/Decision。  
**操作**：Q-05、固定 scope KG 近似、完整问后 continuation 比较、meta depth/CPU/token 上限；记录 approximation。  
**测试**：D-06、D-07、D-08、I-04。  
**退出**：估值本身收费；没有递归无限问卷；信息价值与终局收益不混称；能够选择直接行动。  
**依赖**：WP-08、WP-13。

## WP-15：导出、可回放实验与质量比较设施

**文件**：Persistence/Export、Core/Projection；新测试/实验 runner。  
**操作**：full/summary 区分；actual context/prompt/model/usage manifest；execution replay、math recompute、experiment rerun 分开；离线 synthetic 与 opt-in real 分层。  
**测试**：C-10～C-13、R-02、R-07、I-06。  
**退出**：full bundle 在独立进程恢复；脱敏文件不假称完整回放；实验输出不造通过率。  
**依赖**：WP-12～WP-14。

## WP-16：删除全部旧生产路径与残余引用

**文件**：第 22 章所有 delete/replace 文件、真实编译清单、exports、tool registry、requirements、旧行为测试。  
**操作**：按 disposition manifest 删除/替换；旧数学测试迁新契约；旧序号/阶段兼容测试退出；静态禁止清单。  
**测试**：X-01～X-05，全套离线与打包测试。  
**退出**：生产调用图中无 Legacy、SolverMode、EpistemicState、旧 SessionStore、手写语义 gain；旧数据没有删除。  
**依赖**：WP-15。

## WP-17：交付与发布检查

**文件**：实施报告、接口文档、examples、manifest、发布 owner。  
**操作**：记录所有命令、真实结果与失败项；Host/SDK 锁定版本；对照第 29 章 DoD；清理只属于本重构的临时文件。  
**退出**：所有声称已实现的能力有公开路径测试；所有未开启扩展明确列为未启用，不留伪实现；不擅自部署/付费调用/提交。  
**依赖**：WP-16。

<a id="s26"></a>
# 26. 验收测试矩阵

测试文件名按真实 runner 约定命名，以下 ID 是稳定的规范引用。既要验证成功，也要验证**不能获得不应有的保证或权限**。

## 26.1 Core、状态与哈希

| ID | 场景 | 必须观察到的结果 |
|---|---|---|
| C-01 | create 后删缓存重放 | 原目标、材料引用、goal revision 完整存在 |
| C-02 | LLM 返回新问题表述 | 新 interpretation 节点；GoalSpec 原文不变 |
| C-03 | 用户授权修改目标 | goal revision 增加，相关估值 stale，未受影响材料仍保留 |
| C-04 | 两次提交相同 ticket/payload | 同 observation、同原 receipt，无重复语义计数 |
| C-05 | 相同 ticket 不同 payload | typed conflict，不覆盖原数据 |
| C-06 | exact/bound 缺 guarantee | 拒绝，不因 None 放行 |
| C-07 | empirical sample 无 coverage | 可保存经验摘要，不能进入 certified bound |
| C-08 | 改 premise revision | 依赖证书失效；历史证书仍可查 |
| C-09 | 同一 slot 两份旧 base patch | 一份接受，另一份冲突/重算，不 last-write-wins |
| C-10 | 同数量事件仅修改一项值 | trace/state hash 随规范变化，不仅依赖数组长度 |
| C-11 | 只改 Host session ID | full state/trace 可变，语义字段不变时 semanticHash 不变 |
| C-12 | 改实际资源消耗 | semantic budget projection 变化，不能伪装 Host 等价 |
| C-13 | maps 插入顺序不同 | canonical projection hash 一致；业务数组顺序不被乱排 |
| C-14 | 缺 graph endpoint/work/scope | 明确拒绝；没有 placeholder 隐式补洞 |
| C-15 | terminal 后新业务写入 | 拒绝；允许合法幂等读回和晚到成本审计 |
| C-16 | batch 中后半事件非法 | 整个 batch 不成为 accepted current，无部分状态 |

## 26.2 插件与问法

| ID | 场景 | 必须观察到的结果 |
|---|---|---|
| P-01 | Manifest 有 capability，但无实现 | startup/capability gate 明确失败 |
| P-02 | 依赖缺失/循环/重复版本 | 分别诊断，不任取一个版本 |
| P-03 | inquiry 中换 prompt/schema 实现 | lock mismatch 拒绝或显式模型修订流程 |
| P-04 | plugin 想直接调用网络/写库 | 依赖/能力测试阻止；只能产出 WorkProposal |
| P-05 | 纯 plugin 抛异常 | raw observation 保留，解释 pending/failed，不重调 LLM |
| Q-01 | opaque label 重命名 | 对关系结果反映射一致；不声称 LLM 实际回答必不变 |
| Q-02 | tie/abstain/conditional | 分别进入 tie 模型、无方向观测、条件关系 |
| Q-03 | worker 返回未知 source label | 精确拒绝该引用，不偷换为公开真实 ID |
| Q-04 | 同一快照两种位置 | 真实显示顺序、seed、题目 hash 完整记录 |
| Q-05 | 不同快照回答 | 不聚合成同 scope 重复票 |
| Q-06 | 新理由前后改变排序 | 标 intervention；不把前后票当 i.i.d. |
| Q-07 | 同 child 重复回答/重试 | cluster/attempt 分清，不自动增加独立 N |
| Q-08 | 部分分支失败 | round 记录 missingness，不能丢失失败组后宣称完整随机实验 |
| Q-09 | BIBD 声明 | 验证所有 incidence 条件；不满足就不叫 BIBD |
| Q-10 | 16 个 probe 返回 no-finding/not-applicable | 结果合法，不重试到找到预设内容 |
| Q-11 | 动态问题要求执行 shell | 仅生成权限请求/不可执行提议，不运行代码 |
| Q-12 | 同轮半途新增候选 | 新候选进入下一 scope，原 ballot presented set 不改 |

## 26.3 序数与数值模型

| ID | 场景 | 必须观察到的结果 |
|---|---|---|
| N-01 | 二候选 8:2，无正则 | theta 差收敛于 log(4)，zero-sum 正确 |
| N-02 | 梯度/Hessian 与有限差分 | 在声明容差内一致；选择不同 gauge 结果关系一致 |
| N-03 | 极端 eta 正负 1000 | log-likelihood/gradient 有限或按极限稳定返回；不 NaN |
| N-04 | 比较图不连通 | typed unidentifiable/分量，不以正则伪装全局数据支持 |
| N-05 | 完全分离且无正则 | 非有限/不收敛诊断；有显式正则时标明模型依赖 |
| N-06 | 位置与候选身份混淆 | 不估计不可识别 beta；报告 design rank |
| N-07 | 正常可识别位置扰动数据 | 可恢复方向性参数，报告拟合误差，不固定宣传效果量 |
| N-08 | 全并列 | NoDirectionalEvidence，不凭候选 ID 拟合语义第一 |
| N-09 | 全弃权 | 不产生胜负票，不把 N 个弃权当 N 个平局 |
| N-10 | 一张 ranking 展成 pairs | 保留 ballot cluster，不把 pair 数当独立 sample size |
| N-11 | MaxDiff best/worst | 联合概率和为 1；best=worst 的非法输入被拒 |
| N-12 | theta contrast uncertainty | 使用 Sigma_ii+Sigma_jj−2Sigma_ij |
| N-13 | capped/line-search failure | Converged=false，诊断区分失败类型 |
| N-14 | 循环偏好/clone 插入/候选移除 | 原始关系保留；不要求不成立的 clone invariance 或全序一致性 |

## 26.4 决策与元层

| ID | 场景 | 必须观察到的结果 |
|---|---|---|
| D-01 | 有效排序只交换复测与发散 | 下一 dispatch 随之改变，非按 ID 或注册顺序 |
| D-02 | 领先答案稳定但发散计划估值更高 | 发散仍可被选择，不被稳定性 stop 抢先终止 |
| D-03 | 先做 P 可解锁 Q，单步看 P 无益 | 计划级比较能够选择 P→Q；不只看即时收益 |
| D-04 | 联合计划含 P→Q | 本次只派发 ready antichain，不同时运行尚依赖 P 的 Q |
| D-05 | answer.now 估值最高 | 停止新增探究，派发真实 renderer；缺 VOC 不算通过 |
| D-06 | 问题耗费改变可执行集合 | 不用固定集合 KG 冒充总终局收益；重新比较 continuation |
| D-07 | 一再建议调查“问法值不值得” | meta depth 到限使用已有估值，不无限递归 |
| D-08 | 估值本身消耗预算 | usage/reservation 包含调查和内层 LLM rollout |
| D-09 | budget stop 与 model stop | 不同 stop reason 和保证，不统一标 equilibrium |
| D-10 | 纯 refiner 无变化/循环 | 无变化不刷 revision；循环有上限与 residual |

## 26.5 数学能力与证书协同

| ID | 场景 | 必须观察到的结果 |
|---|---|---|
| M-01 | Bayes 有效离散先验与因子 | 等于手算归一化结果 |
| M-02 | 零先验、全零 partition | 零先验保持零；全零明确失败，不回均匀 |
| M-03 | 缺失/非有限/越界 factor | typed failure，不能进入 ExactWithinModel |
| M-04 | 同 observation 重复 receipt | 不重复乘因子；相关不同观测不任意丢弃 |
| M-05 | prior-only | 可返回明确 prior-only，但不算新增信息或收敛 |
| M-06 | A* 非负图与 h=0 | 与最短路基线一致 |
| M-07 | admissible 但 inconsistent h | 更优 g 时 reopen，返回正确成本 |
| M-08 | A* 中途暂停恢复 | 同状态继续得到相同路径/界；OPEN/CLOSED 不丢 |
| M-09 | 负边/非有限/无路径 | 拒绝非法模型或返回 typed Unreachable，无 JSON Infinity |
| M-10 | MCTS 两动作 deterministic returns | 固定预算/seed 下正确累计与 backup，较高均值动作可被推荐 |
| M-11 | 未访问动作 | 不做 0/0 后继续排序，先处理 unvisited |
| M-12 | LLM rollout | 真正走 WorkItem/usage，不隐藏网络调用 |
| M-13 | 同 semantic node 不同 horizon/budget | 默认不共享不相容统计 |
| M-14 | 无 reward/value bridge | 数值 MCTS 不启动；ordinal inquiry 仍能运行 |
| M-15 | 自适应 sample radius | 不进入 deterministic/固定时刻覆盖保证 |
| M-16 | 同 inquiry 多 refiner | 共用证书/依赖图，slot 不互相覆盖；不存在 SolverMode 开关 |

## 26.6 持久化、预算与 Host

| ID | 场景 | 必须观察到的结果 |
|---|---|---|
| R-01 | 新事件类型与 Integrator | 被 canonical owner 正确识别；未知 v2 schema 拒绝 |
| R-02 | 删除全部进程缓存后恢复 | 完整状态、原目标、work、answer 恢复 |
| R-03 | ResultAccepted 后宕机 | 恢复只做纯解释，不重调模型 |
| R-04 | 两进程同 parent 同时提交 | 仅一条合法后继 current，另一条冲突可重试 |
| R-05 | append 拒绝/cut | 不返回成功，不派发新 work |
| R-06 | owner cut/reset/恢复 | 遵循实际 owner 语义，不留下假 head 或幽灵 lease |
| R-07 | full export 独立进程 replay | projection 一致；缺 blob 明确阻塞，不伪造材料 |
| R-08 | goal amendment 与旧结果竞态 | 旧结果不污染新 scope，仍保留相关审计/真实 usage |
| B-01 | 多资源预留/完成 | 逐资源守恒，capacity 与消耗型计费分开 |
| B-02 | 两并发工作争抢最后预算 | 不双花，未获预留的工作不发出 |
| B-03 | 失败/格式错误已消费 token | 实际成本入账 |
| B-04 | 相同 provider receipt 重复 | 不重扣 |
| B-05 | retry 实际再次调用 | 新成本入账，语义仍至多一次采纳 |
| B-06 | provider 实耗超预留 | 记录 overrun 并停止新消耗；不通过丢账保持“守恒” |
| B-07 | provider 未返回 usage | 标 unresolved，保留必要预留，不写零 |
| B-08 | cancel 在途调用 | 只释放确认不会再消费的部分，晚到使用量可结算 |
| H-01 | dispatch | 获得真实 execution receipt，不拿 intent hash 冒充 child ID |
| H-02 | snapshot fork | actual visible content 和 prefix 与 manifest 相符 |
| H-03 | Host idle 无有效返回 | 状态仍 pending/失败，不自动成功 |
| H-04 | abort 不支持/尚未确认 | cancelling/pending，不能直接 cancelled |
| H-05 | receipt 未写入时宕机 | 按 intent reconcile，不盲目重复创建 |
| H-06 | 旧 attempt 晚到 | 不计入新 attempt 的语义观测 |
| H-07 | 用户取消后晚到有效答案 | 不复活 inquiry；保存允许的审计/费用 |
| H-08 | 同轮乱序回包 | 都按局部 ticket 接受；不因全局 revision 变化拒绝合法后续票 |

## 26.7 公共接口、端到端与切断旧路径

| ID | 场景 | 必须观察到的结果 |
|---|---|---|
| W-01 | v2 tools list 与 start | 工具/请求/返回 schema 一致，旧四阶段接口消失 |
| W-02 | malformed JSON/字段/整数/引用 | typed errors，不默认猜值 |
| W-03 | Fable → JS boundary | plain objects/arrays/strings，无内部 DU/Map 编码泄露 |
| W-04 | status/export | 不创建 lease、不调用模型、不改变业务状态 |
| W-05 | Worker 试图提交证书/预算/任意事件 | 无此生产入口，或权限/schema 明确拒绝 |
| W-06 | 未配置持久化/协议 SDK 不支持 | 显式启动/能力错误，不回 legacy memory，不伪称新协议 |
| I-01 | 第 16.6 节完整公开 API 轨迹 | 实际从 start 达到 AnswerCommitted，所有步骤有事件 |
| I-02 | 新候选改变当前答案 | 新 scope 估值与 renderer 内容可反映变化 |
| I-03 | 条件反转和问题重构 | Goal 保持原义，条件分别留存，不硬合成唯一事实 |
| I-04 | 小预算运行 | 不先耗光在问卷上；能直接按 provisional/ordinal 成稿并标近似 |
| I-05 | 取消/能力缺失/预算用尽 | 可区分终态，不返回 completed 空答案 |
| I-06 | MCP 与 OpenCode 的同语义轨迹 | 相同语义输入/资源下 semanticHash 一致，物理 receipt 可不同 |
| X-01 | 扫描 production dependency graph | 无旧 Policy/Value/Methodology/SolverMode/Legacy 运行路径 |
| X-02 | default profile | 不强制 self-prediction/BTS、概率填表或答案评分表 |
| X-03 | 旧事件与新版数据 | 历史数据未删除；旧 inquiry 不被错读为 v2 |
| X-04 | package/CLI/export smoke | 所有入口只绑定新 Runtime；没有孤立旧 surface |
| X-05 | 全仓非 Sphinx 回归 | shared owner 的非 Sphinx 消费者未被破坏 |

## 26.8 测试不能伪造的东西

合成数据能测试算法是否按假设工作，不能证明真实 LLM 符合这些假设。mock Host 能验证调用契约，不能证明本机安装的 OpenCode 支持所有能力。fixture 中的稳定随机数能验证回放，不保证远端模型逐字重现。

测试报告必须注明层次、样本来源、配置和失败。把测试名叫 `scientific_convergence` 不等于获得科学保证。

<a id="s27"></a>
# 27. 可直接照写的精确测试样例

## 27.1 BTL 二候选

输入：A 胜 B 8 次，B 胜 A 2 次；无 tie、无位置参数、无正则；zero-sum gauge。

期望：

```text
theta_A - theta_B = log(8/2) = log(4)
theta_A = log(2)
theta_B = -log(2)
P(A beats B) = 0.8
```

拟合结果容差按数值实现设定。它检验二项模型的实现，不表示真实任务的 A 正确率为 80%。

检查受约束 Hessian 的 contrast 方差近似：

```text
Var(theta_A - theta_B) = 1/8 + 1/2 = 0.625
```

这是该似然的局部曲率近似；不声称 10 个 LLM 回答足以获得精确频率覆盖。

## 27.2 Bayes 归一化

```json
{
  "hypotheses": [
    {"key": "h1", "prior": 0.5},
    {"key": "h2", "prior": 0.5}
  ],
  "factors": [
    {"observationId": "o1", "likelihoods": {"h1": 0.8, "h2": 0.4}}
  ]
}
```

期望 posterior：`h1=2/3`、`h2=1/3`。重复 delivery 同一 o1 后结果不变；另一条真正条件独立的新 observation 不能因为文本相同而自动去重。

## 27.3 A* 必须 reopen 的图

```json
{
  "start": "S",
  "goal": "G",
  "edges": [
    {"from": "S", "to": "A", "cost": 3},
    {"from": "S", "to": "B", "cost": 1},
    {"from": "B", "to": "A", "cost": 1},
    {"from": "A", "to": "G", "cost": 2},
    {"from": "B", "to": "G", "cost": 100}
  ],
  "heuristic": {"S": 4, "A": 0, "B": 3, "G": 0}
}
```

此图最短路是 `S→B→A→G`，成本 4。A 会先以 g=3 展开，随后 B 找到 A 的 g=2，必须 reopen。不能因为 A 已在 CLOSED 而返回成本 5。

## 27.4 Graph-MCTS 的最小数值模型

horizon=1；动作 a 的 terminal return 恒为 0.25，b 恒为 0.75；range=[0,1]；iterations≥2，确保每项访问过；固定 seed。

检查：访问计数和为真实次数；每项 sample mean 等于其确定 return；没有 0/0；较优行动的推荐规则符合插件明示的规则。把某一动作 return 改掉，estimate 必须改变。这里的数字来自测试模拟器，不从 LLM 名次生成。

## 27.5 排序不能用于随机期望的反例

终局状态满足 `x<y<z`。两个计划：P 一定得到 y；Q 一半 x、一半 z。

数值表示 1：`U(x)=0,U(y)=2,U(z)=3`，则 P 比 Q 的期望高。  
数值表示 2：`U(x)=0,U(y)=1,U(z)=4`，则 Q 比 P 的期望高。

两套 U 的确定性排序完全一样。因此测试必须确认：仅有 x<y<z 的系统，不会宣布已由此确定 P/Q 的期望优先级。它可以直接向 LLM 调查整条路径，或者要求合格 expected-utility bridge。

## 27.6 “边界变窄不等于答案变好”

同一个被评估对象的区间由 `[0.2,0.9]` 变为 `[0.5,0.7]`。检查 certificate precision 可变，但不可因此自动触发 `AnswerQualityImproved`。质量比较仍由相应 value-space 解释。

## 27.7 哈希与假回放回归

构造两份长度相同的事件序列：只将一个候选文本由“允许离线”改成“必须在线”。traceHash 和相应 semanticHash 应改变。把相同 maps 的插入顺序改变，canonical hash 不应改变。

旧 `replay(array)` 只 hash 长度的行为必须由这个测试直接捕获，而不是靠代码审查记忆。

## 27.8 并发局部前置条件

round R 在 inquiry revision 7 时派发 w1、w2。w1 被接受后 revision 变 8。w2 携相同 scope 但自己的合法 ticket 返回，应能在 revision 8 的 current 上接受为新 batch。

随后修改 goal revision；w3 若属于旧 goal scope，则不进入新 scope 的估值。两个情形不能一概用“stale global revision”处理。

<a id="s28"></a>
# 28. 如何验证问法和调度确实有用

## 28.1 质量验证不反过来成为 Core 评分表

实现正确性由可重复测试检验。问法与调度对真实答案的帮助，则需要实验，不能由“有了 BTL 和 MCTS”推出。

实验可采用同一任务、同一授权预算的配对设计：直接回答、固定轮次探究、本次贡献调查调度。最终比较仍围绕用户任务，由用户判断、任务自带可验证结果，或明确声明的 LLM 比较协议提供。没有一把手写的跨任务“质量=0.4正确性+0.3完整性”标尺。

独立 LLM 对最终答案的比较可以作为**可选实验观测**，不是默认系统的定义性裁判。它应按原目标作相对比较，报告模型和协议条件，不把用户之前拒绝的评分表换名恢复。

## 28.2 最小实验记录

```text
任务来源/选择方式
原目标、材料版本
方法组/对照组
共同预算与实际使用量
question/template/model versions
assignment/permutation/seeds
最终答案 artifact refs
比较响应（包含 tie/abstain/conditional）
用户/任务结果（实际有时）
失败、取消与缺失率
是否用于选模板，是否为 held-out
```

同一任务上不断调问法，再用同一任务证明新问法最好，是训练内结果。区分开发集与留出任务，保留失败，不只展示成功的案例。

## 28.3 消融应回答具体设计问题

可依次比较：是否遮蔽来源、是否允许弃权/条件、是否把问法干预与复测分开、是否比较整条 continuation、是否保留 answer.now、是否限制元层开销。

不要把所有改变打包成“新系统”，然后无法分清哪项有帮助。也不承诺每项都必然更好；实验可以得到某种问法没有收益、或在某类任务中退化的结果。

## 28.4 初次发布可声明与不可声明

可声明：已实现某些问法能力、公开闭环、条件下的数学正确性、真实 Host 对接、持久恢复和指定预算内运行。

没有对应实验不得声明：更高最终答案质量、节省多少 token、消除位置偏差、揭示模型真实信念、已证明异步混合收敛。

这不阻止发布可工作的系统；它只约束发布说明不把愿景写成结果。

<a id="s29"></a>
# 29. 最终 Definition of Done 与交付报告

## 29.1 生产重写完成的门槛

| 门槛 | 必须同时满足 |
|---|---|
| 目标一致 | U-01～U-06 被落实，无固定语义评分表/方法权重 |
| 闭环完整 | public start→plan→question→result→estimate→action→answer 真实可达 |
| 调度真实 | 输入关系变更会改变下一步，不由 ID/顺序决定 |
| 开放空间 | 新候选/条件/重构可进入后续 scope；不固定候选全集 |
| 数学诚实 | 排名、间隔效用、后验、coverage、bound 分开；桥接有前提 |
| 原子与恢复 | canonical 事件持久化、CAS/owner fence、宕机恢复与幂等经过测试 |
| 运行资源 | 预留/结算/超支/取消的账一致；状态查询不偷偷耗模型预算 |
| Host 真实 | receipt 来自实际 adapter；取消/排空不伪造成功 |
| 安全 | Worker 不改预算/目标/证书，不默认上传私人材料或启用写工具 |
| clean-break | 108 文件去留表完成；旧 API/内核退出 production build；旧数据未删除 |
| 公开接口 | Fable/JS、MCP、exports、CLI 与实际 SDK 版本一致 |
| 回放与文档 | full/summary 导出明确；独立进程回放通过；已知限制写实 |

三种 refiner 的真实 typed implementation 与 conformance 是本次工程交付的一部分。开放任务暂未建立全局 expected-utility bridge，不得用 stub 假装已建立，也不阻塞直接调查计划贡献的主闭环。

## 29.2 明确不属于首轮发布的强制项目

不强制：旧八工具兼容、旧 revision 轨迹、自我预测概率问卷、BTS/Peer Prediction、32 个名称数量目标、对任意开放任务的确定性最优证明、跨任意运行时的浮点 bit equality。

不强制不等于可以把空实现标成已完成。未实现能力不进可用 capability 清单；文档区分“计划”“实验”“已实现”“已验收”。

## 29.3 实施报告模板

```text
Baseline
  source commit / repomix hash / local differences

Files
  108-file disposition completion
  added modules / deleted modules / external owner changes

Behavior
  public entrypoint and complete inquiry trace
  a ranking change that changed actual dispatch
  answer.now / generation / conditional reframe examples

Verification
  exact commands
  passed/failed/skipped tests and reasons
  Fable/SDK/Host versions actually tested
  live tests, authorization and actual usage

Durability
  crash-window tests
  idempotency / CAS / retry / cancel results

Math
  enabled observation models
  approximation/identifiability failures
  guarantee scopes and missing bridges

Data
  legacy events preserved
  full replay / redacted export handling

Remaining
  concrete limitations and impacted actions
  no unsupported quality/optimality claims
```

不要用“所有测试通过”代替命令与结果，也不要把未运行测试写成 skipped 后仍宣布全部已验收。

## 29.4 最后检查旧路径是否仍活着

在真实 production sources/exports/compile graph 中检查以下残留。只检查 Sphinx 相关作用域；允许历史文档、测试反例和注释保留相应词语。

```text
SolverMode
旧 EpistemicState / Request / Observation / SessionStore
LegacyPlugin / GecLegacy / LegacyDurability / LegacyIntegrator
旧 GecInquiry registry 与 results-only 的独立 current
旧 GecSurface 的 placeholder、length-only replay hash
按方法/问题形式写死的 gain/utility/weight
0.72 synthesis factor / 0.65 gateway / 0.4 gap + 0.6 uncertainty
expectTurns→TurnPrice 的旧效用模型
aborted=true / drained=true 却没有真实 Host receipt
缺少存储配置时回退旧内存内核
```

关键不是搜到字符串就删，而是证明新生产调用图中不存在这些旧行为。代码重命名不能逃过行为测试。

<a id="app-a"></a>
# 附录 A. 可直接交给实施智能体的任务书

下面这段是执行本指南的入口，不替代正文契约。实施权限以实际用户授权为准。

```text
任务：依据《Sphinx Clean-Break 重写指南》重构真实仓库中的 Sphinx。

已经决定：
1. clean-break，不建立 Legacy Adapter，不保留旧阶段行为。
2. LLM 负责目标理解、探究路径生成、贡献比较、条件判断和答案综合。
3. 系统负责科学问法、关系记录、声明模型下的数学估值和资源内调度。
4. 不新增手写答案评分表、语义方法权重或强制概率填写问卷。
5. 直接预算化计划比较是默认可工作的闭环；不得因缺基数效用而停止整个项目。
6. 排序量、模型后验、频率学覆盖、确定性界限必须分开。

先做：
- 阅读仓库已有指导文件；记录用户未提交修改，不能覆盖。
- 定位真实 Sphinx 目录、项目编译清单、package exports、测试命令。
- 定位 canonical EventStore、Integrator、Host/session/capacity/权限 owner。
- 生成 source inventory、integration map、requirements supersede 记录。
- 将指南第22章的54对文件逐一标记到实际路径，所有108文件都有结论。

实现顺序：
- 依第25章 WP-00 至 WP-17 及真实依赖推进。
- 尽早通过第16.6节的公开API垂直闭环，不能先做一堆孤立数学函数。
- 使用一个Core reducer、一个Runtime driver、一套持久化inquiry状态。
- 各Host仅执行已经持久化的工作，不自行决定认识阶段。
- 所有对外数据经过Wire decoder；不要把Fable DU/Map当普通JS对象交付。
- 清理旧导出和编译引用时同步更新调用方，不留调用旧Policy的同名代理。

关键禁止：
- 不能把repomix XML改了就算修改仓库。
- 不能用固定ID顺序、假gain或问卷票数当作贡献最优化。
- 不能把语义偏好后验塞进A*硬界，或把自适应MCTS半径称可靠覆盖。
- 不能让Worker写目标、预算、事件、证书或权限。
- 不能在无事件情况下补节点、补parent、生成缺失身份或重写回放。
- 不能在没有真实Host receipt时报告aborted、drained或独立分叉成功。
- 不能用mock通过代替真实adapter contract，也不能把未运行测试标为已通过。
- 不能自动删除旧事件、部署、上传私有材料或使用未授权付费模型。

遇到资料缺口：
- 首先在真实仓库查找，不把已有材料问题再交给用户回答。
- 找不到共享owner能力时，记录准确接口缺口和受影响工作。
- 可以继续纯模块、fake-witness闭环、schema、数学与恢复测试。
- 不猜测调用签名，不创建重复存储/会话池，不填返回成功的空壳。
- 未获授权的外部操作保持阻塞；其它已授权工作继续。

验收：
- 第26章的case逐项实现并报告；可共享fixture，但不可合并掉行为差异。
- 第27章数值反例与第29章完成条件必须落实。
- 输出实际修改清单、运行命令、通过/失败/未运行项、真实版本、剩余限制。
- 最后证明公开入口能从用户目标走到答案，调查关系改变能改变实际调度，
  并且重启后按权威事件恢复；不是只展示一个成功JSON。
```

<a id="app-b"></a>
# 附录 B. 材料溯源与本次取舍

## B.1 文件身份

行号均指所交付原文件的**全局行号**；repomix 中每个源文件的范围见第 22 章。它们用于定位，不要求实施者把行号保持不变。

| 标记 | 文件 | SHA-256 |
|---|---|---|
| `[R]` | `repomix-output(5).xml` | `8ef8c10bd48e14d5f27c7af35fb4cc0a154bc1ae345b6570e2ce1d15099db213` |
| `[L]` | `Sphinx.md` | `1b922080ce9c3462da98d54c16b1a76c6de7872c1515060ca54607e839f2f682` |
| `[N]` | `粘贴的 markdown (1)。md(5)` | `b52a2a99c629dd4f67e19bb1a101dd5022e054da84deea593aa88be790937923` |

`[U]` 来自这次对话，不是从代码推断用户偏好：clean-break；所有探究路径以最终答案贡献竞争；语义判断交给 LLM；主要设计问题与问法；不采用先定语义评分表的方案。

## B.2 借鉴与替换对应关系

| 内容 | 原材料位置 | 本文如何处理 |
|---|---|---|
| 数学与语义分工 | N 31–35；L 81–116 | 第01、03、14章；进一步禁止把手写语义裁判搬进插件 |
| 单Core多Host | L 47–116、1060–1074 | 第04、16、18、19章；遵守实际仓库owner，不复制存储/会话池 |
| 兼容旧工具/绞杀迁移 | L 122–175、628–641 | 明确替代；第00、22、25、29章定义clean-break |
| 持久事件与幂等 | L 380–392；R GenericDurability/InquiryRuntime/ServeEntry | 第15、17章；补局部work admission、atomic batch、跨进程竞态与实际receipt边界 |
| 价值序与精化序 | L 536–562 | 第10–13章；不同scope和前提修订允许失效/变宽 |
| 成对、排序、best-worst | N 64–100；L 948–958、2158–2250 | 第07、09、10章；区分联合观测、tie/abstain/partial ranking，不伪造独立票 |
| 问法改变认识状态 | L 2075–2154、2312–2372 | 第09章；测量与促成新判断分开，真实条件进入manifest |
| 六阶段调查流水线 | L 922–991 | 拆成按需能力，不设每题必经问卷 |
| 候选关系与多模式 | L 936–946、2229–2250 | 第05、07、10章；保存条件关系，不急于硬合并 |
| 11个具体探针规格 | N 196–343 | 第08章中性改写并补完整卡片；新增卡片均属D，不谎称原稿已交32个 |
| 固定M₀/禁止所有兄弟内容 | N 378–384；L 1343–1370 | 第09、19章；按round snapshot与显式信息传递协议隔离 |
| Bayes/A*/MCTS统一 | L 1619–2001、2434–2474 | 第12、13章；统一类型、作用域和执行，不宣称任意语义都满足同一定理 |
| 标量gap/uncertainty权重 | N 587–606；R Value.fs | 删除；第11章改为调查终局贡献并声明近似 |
| 停止稳定性门槛 | L 2377–2430；R Plugins/Stop/Certificate.fs | 第20章重写；answer.now参加选择，预算结束与模型内证书分开 |
| Truthful/BTS/自我预测 | L 993–1010、2254–2307 | 保留实验扩展位置，不进入默认路径，不强制概率报告 |
| 不可识别边界 | L 2482–2521 | 保留结论适用范围；不把产品目标改成只测模型信念 |
| 全量逐文件任务 | R 108个文件条目 | 第22章54对完整覆盖；第23章另列快照外接入面 |

旧稿中的外部事实与数学断言不会因出现于材料就自动视为已核实。本文不沿用“完全消除偏差”“标准误统一随总票数缩小”“3σ即可硬剪枝”等结论。相关替换见第09–13章；新增模型明确标为工作假设。

<a id="app-c"></a>
# 附录 C. 外部资料与核对范围

以下页面核对日期为 **2026-09-23**。它们只支持注明的范围；实际安装的SDK、Provider和仓库Host版本仍必须由实施者核对。网址保留为文本，便于离线复制。

## [W1] MCP Versioning

标题：Model Context Protocol — Versioning。  
地址：`https://modelcontextprotocol.io/specification/versioning`

所查页面将 `2026-07-28` 标为当前协议版本，并说明版本机制。本文只据此区分现代协议与历史握手，不据此猜测仓库SDK已支持新版本。对应第18.3节。

## [W2] MCP Tools，2025-11-25

标题：Model Context Protocol — Server Features / Tools。  
地址：`https://modelcontextprotocol.io/specification/2025-11-25/server/tools`

用于核对旧版本中的工具输入/输出schema及structured result约定。协议返回结构与LLM生成约束是两个不同接入面；本文不给缺少实际provider证据的输出贴上“严格结构生成”标签。对应第18章。

## [W3] MCP Tools，2026-07-28

标题：Model Context Protocol — Tools。  
地址：`https://modelcontextprotocol.io/specification/2026-07-28/server/tools`

用于核对新版本的tool result contract。本文没有在业务示例里伪造完整JSON-RPC兼容包；transport包装必须按实际SDK版本测试。对应第18.3节。

## [W4] OpenCode Server

标题：OpenCode — Server。  
地址：`https://opencode.ai/docs/server/`

用于核对session创建、fork、查询、abort、异步prompt及OpenAPI入口等服务器能力。官方有某API不等于本仓的受管Host端口已经暴露它，也不证明应用实际执行过。对应第19章。

## [W5] Selecting Computations: Theory and Applications

作者：Nicholas Hay、Stuart Russell、David Tolpin、Solomon E. Shimony。  
地址：`https://arxiv.org/abs/1408.2048`

核对范围为论文摘要与书目信息。用于说明“把计算活动当作决策动作、以后续决策改善评价计算”的研究背景。本文的资源状态、作用域、调查接口和默认调度是本次设计，不把它们说成该论文已经证明。对应第11章。

## [W6] MCP Sampling，2026-07-28

标题：Model Context Protocol — Client Features / Sampling。  
地址：`https://modelcontextprotocol.io/specification/2026-07-28/client/sampling`

所查页面标明Sampling已弃用并给出直接provider集成方向。本文因此不把MCP Sampling设成新默认闭环的必需基础；支持历史客户端是独立的适配决定。对应第19.6节。

## [W7] Preferential Bayesian Optimization

作者：Javier González、Zhenwen Dai、Andreas Damianou、Neil D. Lawrence。  
出版：ICML 2017，PMLR 70，1282–1291。  
地址：`https://proceedings.mlr.press/v70/gonzalez17a.html`

核对范围为论文页面摘要与书目信息。可借鉴通过比较访问潜在目标的建模思路；它不使任意排序自动成为可加的期望效用，也不是本文必须采用某种Gaussian Process的依据。对应第10章的观察模型边界。

## C.1 哪些仍不是已验证事实

本文没有验证：Sphinx对真实任务的答案质量提升、默认正则参数最优、LLM样本条件独立、问法效应可以普遍消除、任意开放问题存在可识别的全局基数效用，或所有混合算子联合收敛。

这些限制不妨碍按本文构建可运行闭环。它们决定的是报告应该声称什么，以及哪些后续能力需要实验、额外输入或证明，而不是替编码智能体制造一个无限等待的前置条件。

---

实施从 WP-00 的真实仓库定位与接入清单开始。第一个具有产品意义的检查点，是公开入口完成“调查改变估值、估值改变动作、动作产出答案”的闭环；最终完成则以全部文件去留、真实接入、恢复测试与明确的数学保证共同验收。
