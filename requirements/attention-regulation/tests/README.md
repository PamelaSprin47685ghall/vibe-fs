# attention-regulation 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录验证注意力调节的两个入口（`assume` 与 scope 文本）是唯一通路，不存在独立注意力工具或持久状态。

- `001.test.mjs` / `002.test.mjs`：`enough` 与 `abandon` 不在工具目录或权限矩阵中暴露，其语义由 `assume` 描述资源承载。
- `003.test.mjs`：`defer` 不在 `StaticTools`、`ToolRegistry`、`tool-map.json` 与 pair-programming-guideline 中出现，旁支备忘由编程面、当前活跃 scope 文本与 `assume` 承载。
- `004.test.mjs`：`Interaction/Attention`、`AttentionTools`、`AttentionToolSurface` 与 `AttentionConcernJournalAdapter` 模块及编译分片不存在。
- `005.test.mjs`：`OrdinaryTurnWorkflow` 与 `Deliver` 不携带旁支待办呈现；`PromptContinuationKind` 取值集合不含此类标签。
- `006.test.mjs`：`Fact`、`Projection`、`ProjectionUpdate`、`Fold`、`AgentJournalPortAdapter` 与 `fact-map.json` 不含 Attention 持久状态。
