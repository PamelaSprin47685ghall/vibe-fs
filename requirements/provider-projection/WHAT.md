# provider-projection — WHAT

## PROVIDER-PROJECTION-001: 投影是纯代数管线而非 AST 解释器

Provider 消息投影必须通过类型化组合子与直接执行的计算管线表达。严禁引入中介的 AST 与二次解释器，严禁各功能模块直接接收并就地修改消息列表（Message list）。

## PROVIDER-PROJECTION-002: 投影输入为不可变的 ProjectionSnapshot

投影管线的核心输入是只读、不可变的 `ProjectionSnapshot`。快照只包含当前 canonical semantic projection；不得携带 feature cache、lifecycle state，不直接查询外部存储或推测会话状态。

## PROVIDER-PROJECTION-003: 输出管线严格分层且 Semantic 与 Wire 类型隔离

投影输出依次遵循 `SemanticEventTree → ProviderSemanticProjection → ProviderWireProjection`。`ProviderSemanticProjection`（语义视图，去除易失 ID，供跨会话等价性比对与规范摘要）与 `ProviderWireProjection`（Wire 视图，包含物理 ID 与本地时间线序号）属于不同类型，严禁隐式混用。

## PROVIDER-PROJECTION-004: 严格划分 Coordinator、Planner 与 Renderer 三层

系统结构严格划分为三层：
1. **Effectful Coordinator**：负责读取宿主状态并生成不可变快照；
2. **Pure Projection Planner**：负责收集意图、规范排序与冲突仲裁；
3. **Canonical Renderer**：负责纯函数式渲染最终字节与确定性表示。

Feature owner 必须先把己方决策完整物化为 provider rows；Renderer 只解释 generic row intent。Host write-back adapter 只把渲染结果写入原生对象，不得重新解释 feature policy。

## PROVIDER-PROJECTION-005: 功能模块仅声明 ProjectionIntent

功能 owner 必须先把己方策略结果完整物化为 provider rows，然后只能提交 `ReplaceMessageBase` 或 `InsertMessageRows` 两种 generic `ProjectionIntent`。Provider projection 不得定义 prefix/context/repair/Strength 等 feature-specific intent；功能模块不得直接拦截或修改原始 Host 消息流。

## PROVIDER-PROJECTION-006: 规范排序与显式合并冲突，禁止注册顺序依赖

不同 message-base replacement 同批出现，或同一 insertion key 对应不同 anchor/rows/Host metadata 时，必须显式返回 `ProjectionConflict` 并终止（fail-closed）。同值重复必须幂等；合法 insertion 必须按 anchor+key 产生与注册顺序无关的 canonical 顺序。

## PROVIDER-PROJECTION-007: 投影 DSL 不承担生命周期驱动

投影管线仅负责从不可变快照和 generic rows 生成确定性呈现。严禁在投影层物化 prefix/context/repair/Strength policy，启动/等待 Agent、执行工具、写入存储、推进生命周期状态或处理在线心跳。

## PROVIDER-PROJECTION-008: SyntheticToml 是唯一的 TOML 渲染器且故意不设解析器

所有合成 TOML 文本的布局、转义与字符串编码统一由 `SyntheticToml` 负责，确保相同输入产生完全一致的字节序列。`SyntheticToml` 故意不提供反向解析器（parser），从结构上禁止业务逻辑反向读取渲染文本作为控制流依据。

## PROVIDER-PROJECTION-009: 指令面与数据面的划分由消费语义决定

合成内容中的分面规则基于**当前接收 Agent 对内容的消费语义**裁决，而不是内容的来源：凡是要求当前 Agent 行动、约束当前 Agent、改变其推理前提、为其续接责任或告诉它“接下来据此做什么”的内容，均属于广义 Instruction Plane，必须位于顶部连续 `#` 注释；只有当前 Agent 仅作参考、不由该材料本身产生行动要求的状态、参数、观测与证据值，才属于 Data Plane，必须编码为 TOML 字段/表格。

因此“事实”不天然等于 Data。典型反例：child → parent 的 LifecycleWorkRecord 虽然是已发生事实，但其接收语义是把未完成责任交还父 Agent 继续执行，所以属于 Instruction Plane；repository hint、可核对的状态快照等仅供参考材料才属于 Data Plane。来源可信度、生产模块、原始语法或是否称作 record/report 均不得代替接收语义判断。

## PROVIDER-PROJECTION-010: 表示层严禁反向创造权威与状态

投影是单向的“类型化状态 → 表示层”映射。合成的角色标记（user/system/assistant 文本）不得反向推导为领域权威根或完成凭证，严禁从输出文本反推业务控制流。

## PROVIDER-PROJECTION-011: 规范摘要唯一派生自语义投影

规范摘要（Canonical Digest）必须对 `ProviderSemanticProjection` 进行确定性规范序列化后计算 SHA-256。计算过程必须剥离时间戳、耗时、成本等传输层专用字段，严禁通过解析 Wire 文本反算摘要。

## PROVIDER-PROJECTION-012: 确定性渲染器保证同输入必同字节

渲染器必须保证纯函数确定性：统一采用 LF 换行、固定的转义规则与字段排序，以 UTF-8 字节计算长度。相同语义输入在任何运行环境下必须产生完全相同的字节序列。

## PROVIDER-PROJECTION-013: 所有 LLM-facing 合成内容只有一个表示所有者

所有 production 中由 Wanxiangshu 合成并最终可被 LLM 看到的 system prompt、user/assistant synthetic message、continuation、fork/fission/delegation handoff、tool result、provider projection 注入、context/prefix material 与其它广义提示，只能先构造成 `LlmFacing.Document`，再由 `LlmFacing.render` 一次性得到最终文本。业务模块只能声明 Instruction Plane 与 Data Plane 的语义内容，不得自行添加 `#`、`[table]`、`key = value`、XML/Markdown envelope、空行分隔、TOML 转义，也不得把多个已经 render 的 document 再用字符串拼接。

`SyntheticToml` 是 `LlmFacing` 背后的低层 canonical TOML writer，不是业务模块的格式化 API。任何新的 LLM-facing 表示需求必须扩展 `LlmFacing` 的强类型构造能力，而不是在 feature owner 中复制格式规则。

## PROVIDER-PROJECTION-014: 一个物理 LLM-facing payload 只能 render 一次

Instruction 与 Data 必须在 render 前组合。禁止 `render(A) + render(B)`、`render(A) + prose`、`prose + render(B)` 或任何等价的后渲染拼接。该规则确保所有 instruction 永远位于第一个 data field/table 之前，并阻止后追加 bare field 被 TOML 最后一个 table 静默吸收。批量 join、warm-start appendix、parent delta、finality record、T1 tool result 等组合场景同样适用。
