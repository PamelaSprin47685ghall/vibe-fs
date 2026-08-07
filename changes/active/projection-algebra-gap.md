# Projection Algebra 闭环

> 本文件是变更工作记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

## Work origin

本工作项由原 `docs/status/projection-algebra-gap.md` 迁入。没有独立 Proposal 文件；
以下内容保存原 Status 的未完成问题背景，不冒充 Original proposal。

## Active work

### Specification impact

目标条款：PROJ-004、PROJ-005、PROJ-006、PROJ-008。
正式迁移顺序只由 PROJ-008 定义，完成证明见 `docs/proof/projection.md`。

### Remaining work

- [ ] Companion BloggerMain / BloggerSquash / BloggerDelta 接入 `insertBlogFrames`、
      `suppressTransportOnly`，并将 `BlogFrames` 纳入 snapshot。
- [ ] InteractionRepair 接入 `insertRepair`。
- [ ] ReviewConfirmation 与 skeptical challenge seal 接入 `appendReviewChallenge`、
      `insertPairProgrammingThought`。
- [ ] Host compaction reanchor 接入 `reanchorAfterCompaction`。
- [ ] 为每个组合补齐 canonical order、digest 等价回归，并删除旧消息直改路径。
- [ ] 运行相关 proof、测试和仓库门禁。

### Completion criteria

1. PROJ-008 的剩余生产路径全部通过
   `ProjectionSnapshot → ProjectionIntent → Planner → Canonical Renderer`。
2. 每个组合的顺序、冲突与 digest 行为有永久证明。
3. 旧消息直改路径删除，不保留双写 renderer。
4. `docs/`、实现和 proof 一致，相关检查通过。

### Blockers

当前没有外部 blocker。
