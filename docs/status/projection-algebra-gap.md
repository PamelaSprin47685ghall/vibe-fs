# Projection Algebra / DSL 迁移缺口（PROJ-004..008）

目标：
- 对齐 `what/projection.md`（PROJ-001..007）、`shape/projection.md`（PROJ-004..006）与 `how/projection.md`（PROJ-008 迁移律）

当前：
- 阶段 1（PROJ-008 迁移顺序第 1 步：普通 X + ActivePrefixEpoch projection）**已完成闭环**（2026-08，见下）。
- 阶段 2（第 2 步：attempt-local PrefixProbe projection）**已完成闭环**（2026-08，见下）。
- 阶段 3–6 未实施，按 PROJ-008 顺序各自独立排期。

## 阶段 1 完成记录（普通 X + ActivePrefixEpoch projection）

落点：
- `Domain/ProjectionAlgebra.fs`（新）：`ProjectionIntent` 密封 DU（阶段 1 两个 case：`KeepPhysicalPrefix` / `ActivatePrefixEpoch`）+ `PrefixActivation` 载荷 + `ProjectionConflict` + `ProjectionPlanner.plan`（fail-closed 冲突，与注册顺序无关）+ `ProjectionRenderer.renderPrefix` / `renderMessages`（wire 层视图，与写回后 Host 解码视图同字节）。
- `Domain/XPrefixProjection.fs`：`forSnapshot` / `forChoice` 返回类型从 `XPrefixPlan` 改为 `ProjectionIntent`；`XPrefixPlan` 类型与 `replacesPrefix` 删除（被 DSL 取代，无双写）；`requiredBlob` 保留。
- `Infrastructure/OpenCode/Codec/Projection.fs`：新增 `applyRenderedPrefix`（渲染结果 → Host obj 的唯一写回适配，尾部消息对象原样保留，不重新编码）。
- `Application/Reconciliation/XWire.fs`：生产路径改为「声明 intent → `ProjectionPlanner.plan`（冲突 fail-closed raise）→ `ProjectionRenderer.renderPrefix` → `Projection.applyRenderedPrefix` → 写回」；`rawWithPrefix` 删除。XWire 不再直接组装消息列表（PROJ-001/005）。
- 测试：`tests/unit/context/projection-algebra.test.mjs`（10 用例：互斥冲突两方向、双激活冲突、单意图/空意图、renderer 映射、物理前缀原样、合成前缀 drop、越界 fail-closed、尾部对象引用保真、pure view 与写回字节同源、冻结字节 fixture）。`attempt-plan.test.mjs` / `prefix-epoch.test.mjs` / `probe-selection.test.mjs` / `host012-tool-part.test.mjs` 断言经 facade 适配后保持原语义。

验证（canary 按用户指示忽略）：
- unit 996 全绿、integration 271 全绿、`npm run lint` 全绿（spec 332 条款 / architecture 232 文件 / dsl-ownership / p0-recovery-join）。

## 阶段 2 完成记录（attempt-local PrefixProbe projection）

落点：
- `Domain/ProjectionAlgebra.fs`：`ProjectionSnapshot`（PROJ-002 阶段 2 字段：`CurrentProjection` + `CommittedPrefix`——`ActivePrefixEpoch.Snapshot` 的 Domain 形态）+ `ProjectionRenderer.cutoffDigest`（CTX-011 step 5 的 digest 证明：对 snapshot 当前投影做 cutoff 截断后计算语义 digest）。
- `Application/Reconciliation/XWire.fs`：attempt-local 快照一次构建、两处消费——`candidate` 收 `ProjectionSnapshot`（`cutoffDigest` 做 probe 证明、`snapshot.CommittedPrefix` 做 committed 对照，消除与 `state.PrefixEpoch.Snapshot` 的双源）；`requiredBlob` / `forChoice` 读 `snapshot.CommittedPrefix`；XWire 不再直接 `List.truncate` 消息列表（PROJ-001）。`prefix` 提取提前（无 await 窗口，TOCTOU 防护不变）。
- 测试：`projection-algebra.test.mjs` 新增 4 用例（snapshot 输入契约、cutoff=2/0/越界的截断语义、stale closure 区分度、`CommittedPrefix` 驱动前缀决策）。`probe-selection.test.mjs` / `attempt-plan.test.mjs` 不变（`select` 签名未动）。

验证（canary 按用户指示忽略）：
- unit 1000 全绿、integration 271 全绿、`npm run lint` 全绿。

## 剩余缺口（阶段 3–6）

- 阶段 3：Companion BloggerMain / BloggerSquash / BloggerDelta projection（`insertBlogFrames` / `suppressTransportOnly`；`ProjectionSnapshot` 追加 `BlogFrames` 字段）。
- 阶段 4：InteractionRepair projection（`insertRepair`）。
- 阶段 5：ReviewConfirmation + skeptical challenge Seal projection（`appendReviewChallenge` / `insertPairProgrammingThought`）。
- 阶段 6：Host compaction reanchor 后 projection（`reanchorAfterCompaction`）。
- 每阶段接入后：canonical order 排序表（how/projection.md）扩展、canonical digest 对比回归、删除对应旧直改路径。

## 量化评估（2026-08，阶段 2 后）

`rg ProjectionIntent src/Wanxiangshu` → `Domain/ProjectionAlgebra.fs`（定义）+ `Domain/XPrefixProjection.fs`（消费）；`rg ProjectionSnapshot src/Wanxiangshu` → 定义 + `XWire.fs` 构建/消费（attempt-local 输入契约已落地）；`rg XPrefixPlan src/Wanxiangshu` 零匹配（旧计划类型已删除）。

阻塞：
- 无。阶段 2 起按 PROJ-008 顺序推进；切换条件（所有历史 canary 轨迹 `LegacyDigest = DslDigest`）在 canary 恢复后统一验证。
