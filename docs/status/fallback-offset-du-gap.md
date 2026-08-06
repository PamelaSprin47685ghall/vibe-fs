# FallbackCursor 强类型 DU 缺口（FALLBACK-002）

目标：
- 对齐 `how/fallback.md` 的 FALLBACK-002 Modulo-4 Cursor、`FallbackOffset` DU 与 typed decode error。

当前：
- `Domain/AgentPairCursor.fs` 中 `FallbackCursor` 记录的 `Offset` 字段仍使用 `byte` 类型，依赖 `0uy`..`3uy` 范式匹配与 helper 函数。

缺口：
- 领域层 `FallbackCursor` 未采用 `FallbackOffset` DU (`Fork0 | Fork1 | Fork2 | Fork3`)，导致非法字节 (4..255) 可以在类型层面上合法构造。
- `byte` 类型泄露进领域层，未能将其严格限制在序列化/反序列化 (`ofByte` / `toByte`) 边界。
- 非法字节的 decode error 尚未以专用 DU 表达；不得把历史损坏误报成 Append `CommitUnknown`。

阻塞：
- 无。保持现有 Journal wire byte 形状，在 codec 边界 decode 为 `FallbackOffset`；因此不需要迁移或重写既有合法 envelope。
