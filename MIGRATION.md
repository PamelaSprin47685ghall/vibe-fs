# 行为迁移总账 (MIGRATION.md)

## Event-Stagger 规则

第一个 canary 立即启动；canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。

## 发行阻断

- **Fallback Host contract**：内部 provider retry 必须在下次 provider request 建立前提供模型选择与拒绝边界，才能实现同一 root user turn 的 A/A/B/B。未证明前不得发布 RC 或正式版本。
- **Companion Host contract**：官方 `experimental.chat.messages.transform` 输入类型是 `{}`；若消息输出也没有可用 session role / agent，插件无法安全决定是否创建 Blogger。不得以“未知 root 一律 Orchestrator”推测角色；必须由 Host 提供 session role/metadata 读取或在接受 prompt 时提供 typed role。
- **Review witness**：第二次 PERFECT 的 root user message 必须是第一次后 guard 接受的 confirmation prompt；缺真实 ProviderRunId fail-closed。
- **Companion**：epoch 只能替换 `LastSuccessfulProjection` 已证明覆盖的完整 semantic-turn 前缀，必须保留 Blogger 未覆盖 raw tail。
- **Inspector**：仅 Executor 工具；继承 caller worktree；父取消必须 await child abort。
- **Release**：最终包独立安装、三轮 event-stagger gate、真实 Host E2E 与 crash matrix 全部通过后才可升级版本。当前许可证为 `LICENSE` 中的临时商业许可证；包保持 private，任何外部分发须另有签署的商业协议。

## 架构迁移状态

- **KISS Agent DSL**: 基于 F# Structured Program (computation expressions) 的可编辑模型。
- **compaction**: 禁用 OpenCode 官方 compaction。
- **reconciler**: Single-flight session reconcile, 仅使用 idle/retry/deleted 信号。
- **companion**: B-head 缓存保护与 ActivePrefixEpoch 隔离。
- **review guard**: 必须具有相同当前 tree 的双 PERFECT 确认。
- **PTY**: 仅 Manager 可见；TERM/KILL/INT/HUP/QUIT/USR1/USR2，TERM 默认五秒 grace。
- **Orchestrator**: Git 错误 fail-closed；review witness 必须匹配当前 candidate/tree。
