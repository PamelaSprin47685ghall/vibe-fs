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

- [x] Companion BloggerMain / BloggerSquash / BloggerDelta 接入 `insertBlogFrames`，
      并将 `BlogFrames` 纳入 snapshot。
      （`suppressTransportOnly`：Domain 骨架 + unit 证明已落地；生产路径
      `TransportMessages` 恒 `Set.empty`、未声明该 intent——不虚报生产接入。）
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

**Outcome**：生产主路径闭环（prefix / blog / repair / challenge / pair-thought /
reanchor）。八 intent 在 Domain 定义 + Planner Canonical Rank + Renderer fold 落地；
业务侧旧消息直改路径删除，不保留双写 renderer。**`SuppressTransportOnly` 未生产接线**。

**Final specification**：正式条款在 `docs/{what,shape,how,proof}/projection.md`
（PROJ-002/004/005/006/008）。本文件是历史变更记录，不是当前产品规范。
REVISE 后正式层已诚实对齐：PROJ-002 为消费者驱动字段子集；proof 登记
`projection-algebra.test.mjs` 与 Suppress 骨架边界。

**Implementation result**：

- Domain：八 `ProjectionIntent`（含 `KeepPhysicalPrefix` / `ActivatePrefixEpoch` 与
  `InsertBlogFrames`、`SuppressTransportOnly`、`InsertRepair`、`AppendReviewChallenge`、
  `InsertPairProgrammingThought`、`ReanchorAfterCompaction`）；Planner Canonical Rank；
  Renderer 逐步 fold。
- 生产接线：EnforcerHost rebuild/repair；PairThought tryInject；HostReviewGuard challenge
  字节；XWire reanchor；`InsertBlogFrames` → `CompanionProjectionBuilder` 单形状源。
- **`SuppressTransportOnly`**：仅 Domain + unit 骨架。生产路径 `TransportMessages`
  恒 `Set.empty`、从不声明该 intent；`applySuppress` 在空集上 no-op。COMPANION-012
  字段级 transport 过滤由模型边界 / `toSemantic` 路径承担；消息级 Suppress intent
  待后续变更（需 WireMessage host-id 侧信道）。
- 刻意保留：`replaceMessagesInPlace` 作为 Host 适配写回原语（SpikePlugin / XWire /
  CompanionTransform）；`CompanionHost.TransformRaw` 恒等（主会话 Host 视图）；
  ManagerNarrative 不在本变更范围。

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
