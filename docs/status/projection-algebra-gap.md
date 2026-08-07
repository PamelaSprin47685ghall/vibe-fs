# Projection Algebra / DSL 迁移缺口（PROJ-004..008）

目标：
- 对齐 `what/projection.md`（PROJ-001..007）、`shape/projection.md`（PROJ-004..006）与 `how/projection.md`（PROJ-008 迁移律）

当前：
- 部分实现。代码 (`Domain/ProviderProjection.fs` / `Codec/Projection.fs` / `EnforcerHost.fs` / `XTraceCapture.fs`) 直接构建与计算 `ProviderWireProjection` / `ProviderSemanticProjection` 记录。
- 未建立 `ProjectionSnapshot` 结构、未实现 `ProjectionIntent` 密封 DU（含 `keepPhysicalPrefix` / `activatePrefixEpoch` / `insertBlogFrames` / `insertPairProgrammingThought` 等）及 `Pure Projection Planner` 的 canonical order / 冲突规则。

缺口：
- 尚在 PROJ-008 描述的 Legacy Projection 阶段，未按 1–6 步骤完成向 Projection DSL 的代数迁移。
- 功能模块尚未完全隔离为只声明 `ProjectionIntent`，部分模块仍直接操作与组装消息列表。

阻塞：
- 无。按 PROJ-008 计划分阶段完成 DSL 迁移并对比 Canonical Digest。

## 量化评估（2026-08）

`rg ProjectionIntent|ProjectionSnapshot src/Wanxiangshu` 零匹配——DU、Planner、Renderer 三层均未落地，差距真实且完全未实施。

工程规模（按 PROJ-008 计划）：
1. `ProjectionIntent` 密封 DU（keepPhysicalPrefix / activatePrefixEpoch / insertBlogFrames / insertPairProgrammingThought）+ `ProjectionConflict` 判定（PROJ-005/006）——Domain 层，可独立测试；
2. `ProjectionSnapshot` 结构（PROJ-002 的只读 Host snapshot 形状）；
3. Pure Planner（canonical order 锚定当前投影前缀锚 + fail-closed 冲突）；
4. Canonical Renderer 迁移（替换 `ProviderProjection.fs` / `Codec/Projection.fs` / `EnforcerHost.fs` / `XTraceCapture.fs` 的直接组装）；
5. canonical digest 前后对比（REVIEW seal / 前缀缓存字节相等不回归）+ e2e 全量。

性质：架构演进（现状 digest/seal 工作正常，e2e 全绿），非缺陷修复。核心 transform 路径重构，风险高，应作为独立排期的多阶段项目，不宜在单次差距清理会话中半途实施（intent 无人消费 = DSL-003 零调用点死代码；Renderer 未迁移 = 双写）。
