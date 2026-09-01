# cognitive-environment — WHAT

## COGNITIVE-ENVIRONMENT-001: 五层组合，每份材料恰属一个主权威

系统提示词与上下文遵循五层组合模型，每份自然语言材料具有唯一的主权威源：
- **World**：普适世界观与通用公理（Common Law）。
- **Role**：参与者职责与自我模型（Role Law，fast/deep 共享）。
- **Library**：继承的技术知识与职位经验（Office Library）。
- **Runtime**：本次调用时刻成立的生命周期与事件注入。
- **Mission**：本次委托的具体目标与当前任务。

## COGNITIVE-ENVIRONMENT-002: 层可告知，不得冒充；冲突按语义所有权裁决

各认知层可以向参与者告知相关事实，但严禁冒充其它层级。若材料间发生内容冲突，严格按各事实的领域语义所有权裁决，不设立简单的层级全序覆盖规则。

## COGNITIVE-ENVIRONMENT-003: canonical 组合顺序

标准组合顺序为：
```text
SYSTEM:  Common Law → Role Law → Office Library
TOOLS:   当前生成的工具描述集合（独立于 Role 章节）
RUNTIME: 生命周期与事件注入
USER:    当前分配的 Mission 任务
```

## COGNITIVE-ENVIRONMENT-004: Tools 不是 Role Prompt 章节

System Prompt 包含职位职责、认识论原则、权能边界与易犯错误，严禁在其中机械枚举当前环境的全部可用工具。工具的可用性由工具面定义，拥有工具不等于获得权能。

## COGNITIVE-ENVIRONMENT-005: Role Law 是长期 self-model 层

Role Law 负责定义「我是什么样的参与者」。同一 Office 的 fast 档与 deep 档共享完全相同的 Role Law 与自我模型，提示词中不出现 `fast-*` 或 `deep-*` 等底层机器路由名称。

## COGNITIVE-ENVIRONMENT-006: Office Library 是继承的技术书籍，不是 Common Law

Office Library 是历代职位沉淀的技术经验与操作指南，其地位从属于具体的任务需求，不具备定义系统公理或职位权能的效力。

## COGNITIVE-ENVIRONMENT-007: 知识可跨 authority 边界流动，authority 不随知识流动

技术书籍可跨越权能边界传授识别缺陷与验证分析的方法，但阅读书籍不代表被赋予修改代码或执行环境的权能。权能严格保持与职位相绑定。

## COGNITIVE-ENVIRONMENT-008: Library 三轴：Class × Delivery × Audience

Office Library 遵循三轴分类：
- **Class**：Rulebook、Handbook、Ledger、Atlas、Field Notes。
- **Delivery**：Inherited Volume、Triggered Folio、Request-Bound Volume。
- **Audience**：按职位角色或请求契约绑定，不按模型推理深度分叉。

## COGNITIVE-ENVIRONMENT-009: Library 禁令

严禁书籍扩大角色权能；严禁向所有角色灌输通用的全能手册；严禁同角色的 fast 与 deep 档阅读不同版本的书籍；严禁将隐藏评审编排写入评审者书籍。

## COGNITIVE-ENVIRONMENT-010: 生命周期文本只 orient，不 educate

生命周期文本（Activation、Reawakening、Continuation、Handoff、Fission、Departure）仅用于为模型建立当前上下文的朝向（orient），不进行重复的说教或知识灌输，亦不得触发 System Prompt 的动态替换。

## COGNITIVE-ENVIRONMENT-011: 瞬时 runtime/mission 不重写长期 self-model

瞬时任务与运行时事件通过会话消息通道传递，严禁借由提示词路径伪造激活或篡改长期的 Role 自我模型。

## COGNITIVE-ENVIRONMENT-012: Reviewer prompt 不灌输流程机制

Reviewer 的提示词由 Role Law 与评审账本组合而成。双重确认（Double PERFECT）等流程机制完全由 Host 主持，不写入 Reviewer 的提示词中。

## COGNITIVE-ENVIRONMENT-013: Pair Hint 是 canonical craft payload

结对提示（Pair Programming Hint）是标准的技能注入负载，统一承载以下核心工作原则：
- 使用统一的中文（或对应绑定语言）思考纪律。
- 暴露 `todowrite` 时，将其视为实时事实账本：一旦工作义务或焦点发生变化，必须先更新账本再执行后续操作。
- 持续维护就绪前沿（Ready Frontier）：一旦子任务 A 解锁后继 A1，A1 立即并发发出，不等待同批次其它未完成任务；依赖图仅为事实快照，不构成人为的阶段屏障。
- 先抽象，再 `assume`：形成将据以行动的判断或需要跨轮保留的结构后，用同一次 `assume(update, query)` 把它写入 jq 画板并取回下一步需要观察的视图；没有实质新信息时不反复推翻已经钉住的判断。复杂写作、研究、设计、规划可借此维护非线性结构，简单任务不为使用工具而制造结构。
- 空名称 `skill({ name: "" })` 仅作为内部合成 wire 标识，模型不可主动调用，真实存在的非空 skill 工具保持完全可用。

## COGNITIVE-ENVIRONMENT-014: delegated tool estimate 是校准提示，不是服从预算

当委任者提供预估工具调用次数（`expected_tool_calls`）时，该数值仅作为参与者评估工作规模与调整验证路径的参考校准提示，不构成硬性的执行上限或停机条件。

## COGNITIVE-ENVIRONMENT-015: Blogger 可按 provider model 临时获得 chronicle-direct assistant text nudge

针对特定易产生冗余思考的前缀白名单模型（当前为 `step-3.5-flash`），Blogger 在每次 provider 请求边界可被注入单次临时的直接记账 assistant 文本提示。该提示仅在当次转换中生效，不持久化至日志，亦不污染历史。

## COGNITIVE-ENVIRONMENT-016: Pair Hint 只保留微原语的高频触发，不重复完整心理合同

在暴露相应工具动作时，Pair Hint 仅保留对 `assume`、`enough`、`abandon`、`defer`、`celebrate`/`regret` 及 `subscribe`/`publish` 的短触发提醒。`assume` 的 jq 语法、双程序执行细节、画板设计原则与长篇使用指南只存在于工具描述，不在每轮交互中重复注入。
