# 行为迁移总账 (MIGRATION.md)

## Event-Stagger 规则

第一个 canary 立即启动；canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。

## 架构迁移状态

- **KISS Agent DSL**: 基于 F# Structured Program (computation expressions) 的可编辑模型。
- **compaction**: 禁用 OpenCode 官方 compaction。
- **reconciler**: Single-flight session reconcile, 仅使用 idle/retry/deleted 信号。
- **companion**: B-head 缓存保护与 ActivePrefixEpoch 隔离。
- **review guard**: 必须具有相同当前 tree 的双 PERFECT 确认。
