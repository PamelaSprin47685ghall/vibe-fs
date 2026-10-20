# attention-regulation 测试与证明范围

本目录不定义额外规则。`enough`、`abandon` 与 `defer` 均已退役（superseded by `assume` and the `todowrite-for-<role>` ANCHOR `view.thenMap` summary declaration）。

测试验证：
- `001.test.mjs` / `002.test.mjs`：`enough` 与 `abandon` 不在工具目录或权限矩阵中暴露，其语义由 `assume` 描述资源承载。
- `003.test.mjs`：`defer` 从 `StaticTools`、`ToolRegistry`、`tool-map.json`、`pair-programming-guideline` 与 `resources/provider/attention-regulation` 中彻底移除，旁支摘要由编程面 ANCHOR `view.thenMap` 摘要与 `assume` 承载。
- `004.test.mjs`：`Interaction/Attention`、`AttentionTools`、`AttentionToolSurface` 与 `AttentionConcernJournalAdapter` 模块及编译分片已删除。
- `005.test.mjs`：`OrdinaryTurnWorkflow` 与 `DeliverTool` 不再携带 `DeferredWork` 呈现或 `pendingAttentionWorkPairs` 查询。
- `006.test.mjs`：`Fact`、`Projection`、`ProjectionUpdate`、`Fold`、`AgentJournalPortAdapter` 与 `fact-map.json` 不再包含 `Attention` 持久家族。
