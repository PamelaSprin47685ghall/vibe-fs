# provider-projection — WHAT

## PROVIDER-PROJECTION-001: 投影是纯代数管线而非 AST 解释器

Provider 消息投影必须通过类型化组合子与直接执行的计算管线表达。严禁引入中介的 AST 与二次解释器，严禁各功能模块直接接收并就地修改消息列表（Message list）。

## PROVIDER-PROJECTION-002: 投影输入为不可变的 ProjectionSnapshot

投影管线的核心输入是只读、不可变的 `ProjectionSnapshot`。快照仅包含当前已接线意图实际需要的事实子集，不直接查询外部存储或推测会话状态。

## PROVIDER-PROJECTION-003: 输出管线严格分层且 Semantic 与 Wire 类型隔离

投影输出依次遵循 `SemanticEventTree → ProviderSemanticProjection → ProviderWireProjection`。`ProviderSemanticProjection`（语义视图，去除易失 ID，供跨会话等价性比对与规范摘要）与 `ProviderWireProjection`（Wire 视图，包含物理 ID 与本地时间线序号）属于不同类型，严禁隐式混用。

## PROVIDER-PROJECTION-004: 严格划分 Coordinator、Planner 与 Renderer 三层

系统结构严格划分为三层：
1. **Effectful Coordinator**：负责读取宿主状态并生成不可变快照；
2. **Pure Projection Planner**：负责收集意图、规范排序与冲突仲裁；
3. **Canonical Renderer**：负责纯函数式渲染最终字节与确定性表示。

## PROVIDER-PROJECTION-005: 功能模块仅声明 ProjectionIntent

功能模块与渲染器之间的交互仅限于预定义的封闭意图集合（如保持物理前缀、激活前缀纪元、插入博客帧、插入修复指令等）。严禁功能模块直接拦截或修改原始消息流。

## PROVIDER-PROJECTION-006: 规范排序与显式合并冲突，禁止注册顺序依赖

不同意图作用于同一锚点时，必须遵循预定义的确定性合并律；若存在互斥冲突，必须显式返回 `ProjectionConflict` 并终止（fail-closed）。投影结果与冲突判定严禁依赖意图的注册顺序。

## PROVIDER-PROJECTION-007: 投影 DSL 不承担生命周期驱动

投影管线仅负责从不可变快照生成确定性呈现，严禁在投影层启动/等待 Agent、执行工具、写入存储、推进生命周期状态或处理在线心跳。

## PROVIDER-PROJECTION-008: SyntheticToml 是唯一的 TOML 渲染器且故意不设解析器

所有合成 TOML 文本的布局、转义与字符串编码统一由 `SyntheticToml` 负责，确保相同输入产生完全一致的字节序列。`SyntheticToml` 故意不提供反向解析器（parser），从结构上禁止业务逻辑反向读取渲染文本作为控制流依据。

## PROVIDER-PROJECTION-009: 指令面与数据面的划分由消费语义决定

合成内容中的分面规则基于接收方角色对内容的消费语义裁决：面向行动与认知的即时指导归入 Instruction Plane（顶部 `#` 注释），状态、参数与证据值归入 Data Plane（TOML 字段/表格）。来源可信度或语法外形不得作为划分依据。

## PROVIDER-PROJECTION-010: 表示层严禁反向创造权威与状态

投影是单向的“类型化状态 → 表示层”映射。合成的角色标记（user/system/assistant 文本）不得反向推导为领域权威根或完成凭证，严禁从输出文本反推业务控制流。

## PROVIDER-PROJECTION-011: 规范摘要唯一派生自语义投影

规范摘要（Canonical Digest）必须对 `ProviderSemanticProjection` 进行确定性规范序列化后计算 SHA-256。计算过程必须剥离时间戳、耗时、成本等传输层专用字段，严禁通过解析 Wire 文本反算摘要。

## PROVIDER-PROJECTION-012: 确定性渲染器保证同输入必同字节

渲染器必须保证纯函数确定性：统一采用 LF 换行、固定的转义规则与字段排序，以 UTF-8 字节计算长度。相同语义输入在任何运行环境下必须产生完全相同的字节序列。
