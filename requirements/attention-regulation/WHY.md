# attention-regulation — WHY

这里记录的是注意力调节工具集的收敛决定：独立的 `enough`、`abandon` 与 `defer` 工具以及专门的 `DeferredWork` 持久队列拆散了注意力表达，并在自然终点与 Manager 退休处引入了额外的续行与待办核对通道。

现在这些职责收敛到已有原语：

- `enough`（信息已足够）与 `abandon`（放下自建计划或推论）由 `assume(assumption)` 统一表达；
- 非阻塞旁支与跨步备忘由编程面（`todowrite-for-<role>`）程序控制流、ANCHOR 视图声明的 `view.thenMap` 摘要以及 `assume` 承载，不再维护独立的 `Attention` 持久投影或终点续行呈现。

## DEPENDS ON

- `participant-identity`
- `cognitive-environment`
- `repository-programming`
