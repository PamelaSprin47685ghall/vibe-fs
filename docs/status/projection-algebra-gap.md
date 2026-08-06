# Projection Algebra / DSL 迁移缺口（PROJ-004..008）

目标：
- 对齐 `what/projection.md`（PROJ-001..007）、`shape/projection.md`（PROJ-004..006）与 `how/projection.md`（PROJ-008 迁移律）

当前：
- 部分实现。代码 (`Domain/ProviderProjection.fs` / `Codec/Projection.fs` / `EnforcerHost.fs` / `XTraceCapture.fs`) 直接构建与计算 `ProviderWireProjection` / `ProviderSemanticProjection` 记录。
- 未建立 `ProjectionSnapshot` 结构、未实现 `ProjectionIntent` 密封 DU（`keepPhysicalPrefix` / `activatePrefixEpoch` / `insertBlogFrames` 等）及 `Pure Projection Planner` 冲突合并矩阵。

缺口：
- 尚在 PROJ-008 描述的 Legacy Projection 阶段，未按 1–7 步骤完成向 Projection DSL 的代数迁移。
- 功能模块尚未完全隔离为只声明 `ProjectionIntent`，部分模块仍直接操作与组装消息列表。

阻塞：
- 无。按 PROJ-008 计划分阶段完成 DSL 迁移并对比 Canonical Digest。
