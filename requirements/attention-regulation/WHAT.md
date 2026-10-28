# attention-regulation — WHAT

注意力调节只有两个入口：`assume` 工具与活跃 scope 文本。本包条款规定这两个入口各管什么，以及注意力状态不得回流为独立工具或持久状态机。

## [001] 信息足够判断由 assume 表达

声明「当前信息已足以支持下一步行动；除非出现此前未消费且足以改变决策路径的新事实，否则不再重开同一判断」的语义由 `assume(assumption)` 承担。它不证明决策正确，不创造权威事实或持久认知状态；无新事实的重复调用不增加语义。工具目录里不存在 `enough` 这个名字，也不以兼容、别名或同义名注册同类工具。

## [002] 放下自建承诺由 assume 表达

放弃自行形成的计划、推论或假设（无须审批或理由检验，使其不再自动占用注意力）的语义由 `assume(assumption)` 承担。放下自我承诺不取消真实任务义务、不撤销用户授权、不删除工作产物、不终止会话。工具目录里不存在 `abandon` 这个名字，也不以兼容、别名或同义名注册同类工具。

## [003] 旁支工作由编程面与 scope 文本承载，跨 scope 备忘经 resetScope 逐字抄写

非阻塞旁支工作由编程面（`todowrite-for-<role>`）程序编排、活跃 scope 内部文本与 `assume(assumption)` 承载；工具目录（`StaticTools.knownToolNames`、`permissionFor`、`requestToolMap`、`ToolRegistry`、`resources/ablation/tool-map.json`）中不存在 `defer` 工具。跨 scope 的关键备忘在 `resetScope` 时逐字原样抄写进 `oldScopeVerboseVerbatimExperience`（见 context-compression-033）。Pair Hint（`resources/provider/host/pair-programming-guideline/**`）不注入鼓励旁支记录的句子。

## [004] 不存在 Interaction/Attention 运行时或工具模块

生产编译图不包含 `Interaction/Attention`、`OpenCode/Tools/AttentionTools`、`OpenCode/Tools/AttentionToolSurface` 或 `Composition/Durable/AttentionConcernJournalAdapter` 模块，也不含 `Wanxiangshu.Owner.attention-regulation.*` 编译分片。注意力表达不拥有独立运行时。

## [005] 自然终点不注入旁支待办呈现

`OrdinaryTurnWorkflow` 在 Engineer、DevOps 或 Orchestrator 自然终止时不读取、不发送任何旁支待办续行；Manager 的 `Deliver` 不从 `AgentProjection` 读取旁支待办列表。`PromptContinuationKind` 的取值集合为 InteractionRepair、JoinGuard、ManagerGuard、BusyAgentNudge、HumanMessage、ManagedDelegationAssignment、ProviderRetryAttempt、FissionHandoff，不含旁支待办呈现。

## [006] durable fact 与 projection 不含 Attention

`AgentFact`、`AgentProjectionSet`、`ProjectionUpdate`、`Fold`、`AgentJournalPortAdapter` 与 `resources/ablation/fact-map.json` 不含 `Attention`、`DeferredWorkRecorded`、`DeferredWorkConsumed` 或同义持久状态。注意力不是持久事实，只活在当前 scope 文本与 `assume` 声明里。
