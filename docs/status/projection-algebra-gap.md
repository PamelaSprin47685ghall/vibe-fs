# Projection Algebra 活跃差距

## Target clauses

- PROJ-004
- PROJ-005
- PROJ-006
- PROJ-008

## Active physical gap

PROJ-008 的前两步已不再构成差距。剩余生产路径尚未全部通过统一的
`ProjectionSnapshot → ProjectionIntent → Planner → Canonical Renderer` 链：

1. Companion BloggerMain / BloggerSquash / BloggerDelta：尚需接入
   `insertBlogFrames`、`suppressTransportOnly`，并把 `BlogFrames` 纳入 snapshot。
2. InteractionRepair：尚需接入 `insertRepair`。
3. ReviewConfirmation 与 skeptical challenge seal：尚需接入
   `appendReviewChallenge`、`insertPairProgrammingThought`。
4. Host compaction reanchor：尚需接入 `reanchorAfterCompaction`。

每个接入点还需补齐该组合的 canonical order、digest 等价回归，并删除对应旧的消息直改路径。

## Evidence / blocker

迁移顺序由 PROJ-008 定义。当前没有外部 blocker；完成判据见 `proof/projection.md`。
