# 行为迁移总账 (MIGRATION.md)

## Event-Stagger 规则

第一个 canary 立即启动；canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。

## 发行阻断

- **Prompt Authority**：所有插件 user-shaped message 必须经 runtime 单例 `PromptAuthorityService`。未识别来源默认 UnknownOrigin fail-closed，绝不可默认 Human。AgentOwnerRoot 必须两阶段 claim→send→AuthorityRootAccepted，禁止事后补登记。
- **Fallback Host contract**：Fallback 属于 Logical Run。新 Authority Root 创建新 epoch（Failures=0, Side=A）。真人省略 model 只继承 `LastAuthorityProfile.BaseModel`，永不继承旧 Run Side B。同一 root 内 A/A/B/B 必须在 Host 发下一 provider request 前由插件控制；未证明前不得发布 RC 或正式版本。
- **Companion eligibility**：只读 `ActiveLogicalRun.Profile.Agent`。禁止 `sessionRoles`、最后物理 user agent、transform input agent、child linkage 作为生产后备来源。
- **Review witness**：第二次 PERFECT 必须绑定 confirmation 的 physical Host message ID + 原 AuthorityRoot；文本 marker 不足。缺真实 ProviderRunId fail-closed。
- **Companion projection**：epoch 只能替换 `LastSuccessfulProjection` 已证明覆盖的完整 semantic-turn 前缀，必须保留 Blogger 未覆盖 raw tail。
- **Inspector**：仅 Executor 工具；继承 caller worktree；父取消必须 await child abort。
- **Release**：最终包独立安装、三轮 event-stagger gate、真实 Host E2E 与 crash matrix 全部通过后才可升级版本。当前开发标记为 `0.4.0-rc.3-dev`；第一个真实候选应为 `0.4.0-rc.3`。许可证为 `LICENSE` 中的临时商业许可证；包保持 private，任何外部分发须另有签署的商业协议。

## 架构迁移状态

- **KISS Agent DSL**: 基于 F# Structured Program (computation expressions) 的可编辑模型。
- **compaction**: 禁用 OpenCode 官方 compaction。
- **reconciler**: Single-flight session reconcile, 仅使用 idle/retry/deleted 信号。
- **prompt authority**: Logical Run / PromptDispatcher 事实已出现；runtime 单例与 AgentOwnerRoot 两阶段发送为当前关键路径。
- **companion**: B-head 缓存保护与 ActivePrefixEpoch 隔离；eligibility 目标态仅 Authority。
- **review guard**: 必须具有相同当前 tree 的双 PERFECT 确认，确认链绑定 physical message id。
- **PTY**: 仅 Manager 可见；TERM/KILL/INT/HUP/QUIT/USR1/USR2，TERM 默认五秒 grace。
- **Orchestrator**: Git 错误 fail-closed；review witness 必须匹配当前 candidate/tree。
