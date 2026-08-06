# FallbackCursor 强类型 DU 缺口（FALLBACK-002）

目标：
- 对齐 `what/fallback.md`（FALLBACK-002 Modulo-4 Cursor）与 `how/fallback.md`（`FallbackOffset` 强类型 DU 定义）

当前：
- `Domain/AgentPairCursor.fs` 中 `FallbackCursor` 记录的 `Offset` 字段仍使用 `byte` 类型，依赖 `0uy`..`3uy` 范式匹配与 helper 函数。

缺口：
- 领域层 `FallbackCursor` 未采用 `FallbackOffset` DU (`Fork0 | Fork1 | Fork2 | Fork3`)，导致非法字节 (4..255) 可以在类型层面上合法构造。
- `byte` 类型泄露进领域层，未能将其严格限制在序列化/反序列化 (`ofByte` / `toByte`) 边界。

阻塞：
- 无。重构 `AgentPairCursor.fs` 内部 `Offset` 字段为 `FallbackOffset` DU 即可对齐。
