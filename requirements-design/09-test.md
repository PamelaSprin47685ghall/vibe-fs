# `effect-accounting`

WHY: 向外部世界请求一个 effect、该 effect 可能已经发生、以及系统已经确认其发生，是三个不同事实；把它们压成一个 bool 会在中断窗口造成重复 effect 或虚假成功。

OWNS:
- Requested/Claimed 与 Accepted/Created/Published 等 effect states 的语义分离。
- Requested-only = outcome unknown，不等于效果不存在。
- Accepted 不折回 Requested；重复 acceptance 必须幂等。
- reconciliation 先查物理 effect identity；只有证明 effect 不存在且领域合同允许幂等重试时才能重试。
- 外部 effect 的 durable intent/accounting 先于权威内存状态更新。

DOES NOT OWN:
- EventStore 编码/提交机制。
- Prompt 特有 PromptKey/no-resend policy。
- Git publish/worktree、repository transaction 等具体 reconcile algorithm。
- effect 的业务授权。

DEPENDS ON: `durable-events`。

PROVIDES: dispatch、change integration、repository programming、managed lifecycle 可共享的 effect accounting law。

FAILURE MEANING: RED = 系统无法区分“请求过”“可能已经发生”“已证明发生”，从而可能重复 effect 或宣称不存在的成功。

INDEPENDENT CHANGE: 某类 effect 改用 Host 原生 idempotency-token 确认，而 event substrate 不变。

CURRENT EVIDENCE: PERSIST-009；fact `Journal/PromptFactFold.fs`（Requested/Accepted）、`Journal/OrchestratorFactFold.fs`、`Domain/MagicTodoFacts.fs`（TodoWritePrepared→Accepted）；failure `Infrastructure/Git/IntegrationGate.fs`（PublishClaimed 三分支）、`Domain/EventStore.fs`（Requested/Accepted）；tests `tests/unit/persist/**`、MagicTodo membrane。
