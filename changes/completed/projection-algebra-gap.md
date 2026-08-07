# Projection Algebra 闭环

> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

## Work origin

本工作项由原 `docs/status/projection-algebra-gap.md` 迁入。没有独立 Proposal 文件；
以下内容保存原 Status 的未完成问题背景，不冒充 Original proposal。

## Active work

### Specification impact

目标条款：PROJ-004、PROJ-005、PROJ-006、PROJ-008。
正式迁移顺序只由 PROJ-008 定义，完成证明见 `docs/proof/projection.md`。

### Remaining work

- [x] Companion BloggerMain / BloggerSquash / BloggerDelta 接入 `insertBlogFrames`、
      `suppressTransportOnly`，并将 `BlogFrames` 纳入 snapshot。
- [x] InteractionRepair 接入 `insertRepair`。
- [x] ReviewConfirmation 与 skeptical challenge seal 接入 `appendReviewChallenge`、
      `insertPairProgrammingThought`。
- [x] Host compaction reanchor 接入 `reanchorAfterCompaction`。
- [x] 为每个组合补齐 canonical order、digest 等价回归，并删除旧消息直改路径。
- [x] 运行相关 proof、测试和仓库门禁。

### Completion criteria

1. PROJ-008 的剩余生产路径全部通过
   `ProjectionSnapshot → ProjectionIntent → Planner → Canonical Renderer`。
2. 每个组合的顺序、冲突与 digest 行为有永久证明。
3. 旧消息直改路径删除，不保留双写 renderer。
4. `docs/`、实现和 proof 一致，相关检查通过。

### Blockers

当前没有外部 blocker。

### Progress notes

- 已完成 PROJ-008 迁移第 1–2 步（`KeepPhysicalPrefix` / `ActivatePrefixEpoch` + PrefixProbe；测试 `projection-algebra.test.mjs`）。
- 已完成 PROJ-008 第 3–6 步：Domain 八 intent（含 `InsertBlogFrames`、`SuppressTransportOnly`、`InsertRepair`、`AppendReviewChallenge`、`InsertPairProgrammingThought`、`ReanchorAfterCompaction` 与既有 prefix lifecycle）、Planner Canonical Rank、Renderer fold；生产接线覆盖 EnforcerHost rebuild/repair、PairThought tryInject、HostReviewGuard challenge 字节、XWire reanchor、InsertBlogFrames→CompanionProjectionBuilder 单形状源。
- 业务直改路径已收敛：`replaceMessagesInPlace` 仅保留为 Host 适配写回原语；不保留双写 renderer。
- 验证：`npm run build`；`projection-algebra.test.mjs` 43 pass；companion+blog projection 44 pass；`npm run lint` 绿。

## Final outcome

**Outcome**：已闭环。PROJ-008 剩余生产路径全部进入
`ProjectionSnapshot → ProjectionIntent → Planner → Canonical Renderer`；八 intent、
Canonical Rank 与 Renderer fold 落地；业务侧旧消息直改路径删除，不保留双写 renderer。

**Final specification**：正式条款仍在 `docs/{what,shape,how,proof}/projection.md`
（PROJ-004/005/006/008）。本文件是历史变更记录，不是当前产品规范；本变更默认 docs 已对齐 PROJ-008，未改正式层。

**Implementation result**：

- Domain：八 `ProjectionIntent`（含 `KeepPhysicalPrefix` / `ActivatePrefixEpoch` 与
  `InsertBlogFrames`、`SuppressTransportOnly`、`InsertRepair`、`AppendReviewChallenge`、
  `InsertPairProgrammingThought`、`ReanchorAfterCompaction`）；Planner Canonical Rank；
  Renderer 逐步 fold。
- 生产接线：EnforcerHost rebuild/repair；PairThought tryInject；HostReviewGuard challenge
  字节；XWire reanchor；`InsertBlogFrames` → `CompanionProjectionBuilder` 单形状源。
- 刻意保留：`replaceMessagesInPlace` 作为 Host 适配写回原语（SpikePlugin / XWire /
  CompanionTransform）；`CompanionHost.TransformRaw` 恒等（主会话 Host 视图）；
  ManagerNarrative 不在本变更范围。
- 剩余限制：WireMessage 无 host-id 时 `SuppressTransportOnly` 为骨架/索引类行为等，
  不伪装为已消解的完整身份剔除语义。

**Verification**：

- `npm run build` 通过
- `tests/unit/context/projection-algebra.test.mjs` 43 pass
- companion + blog projection 共 44 pass
- `npm run lint` 绿

**References**：`docs/{what,shape,how,proof}/projection.md`；
`src/Wanxiangshu/Domain/ProjectionAlgebra.fs`；`src/Wanxiangshu/Domain/CompanionProjectionBuilder.fs`；
`src/Wanxiangshu/Session/EnforcerHost.fs`；`src/Wanxiangshu/Application/Reconciliation/XWire.fs`；
`src/Wanxiangshu/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.fs`；
`tests/unit/context/projection-algebra.test.mjs`、`companion-projection.test.mjs`、
`blog-projection.test.mjs`。
