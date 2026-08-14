# Interaction

## `interaction-authority`

WHY: 物理 user-shaped message 不等于 authority；只有 typed provenance 能证明一次消息有资格创建新的 logical interaction 或继续既有 interaction。

OWNS:
- `PhysicalUserMessage ≠ AuthorityTurn`。
- Authority Root / Continuation / HostInternal / UnknownOrigin 的语义区别。
- 什么 provenance 可创建新的 LogicalRun；什么 provenance 只能延长已有 LogicalRun。
- root 可建立哪些 interaction-level facts；continuation 不得重新建立它们。
- UnknownOrigin fail closed。

DOES NOT OWN:
- transport claim/submission/physical acceptance protocol。
- provider projection、attempt recovery、Persona definition。
- 当前 `AttemptExecutionProfile` 具体 record layout。

DEPENDS ON: `participant-identity`, `session-ontology`。

PROVIDES: “新 root / existing interaction continuation”的 typed guarantee。

FAILURE MEANING: RED = synthetic/unknown/continuation 可冒充新 root 或重置只有 root 才能建立的 authority。

INDEPENDENT CHANGE: 新增一种合法 continuation provenance，而物理 dispatch protocol 完全不变。

CURRENT EVIDENCE: PROMPT-001..004/018；`PromptAuthority.fs`、`PromptIngress.fs`；`authority.test.mjs` 的 root/continuation/origin proof。

---

## `dispatch-protocol`

WHY: 已获授权的 logical interaction 穿过不可靠 Host 时，transport receipt 或 uncertain outcome 不能许可重复发送同一逻辑动作。

OWNS:
- logical prompt dispatch 的 durable claim。
- Claim / transport submission / physical acceptance 的分型。
- transport receipt ≠ physical message identity。
- physical acceptance 只能由真实物理 message evidence 建立。
- deterministic/idempotent dispatch identity；同 payload 的两个独立 logical acts 仍可区分。
- uncertain physical outcome 不自动重发。
- detached/fire-and-forget 只改变调用方等待，不绕过 claim/accounting。
- 核心 guarantee = at-most-one logical effect，不虚构 exactly-once physical delivery。

DOES NOT OWN:
- interaction 是否有 authority。
- generic effect-accounting law。
- provider representation、attempt recovery。
- 当前 recovery tail/budget 精确常数。

DEPENDS ON: `interaction-authority`, `effect-accounting`, `host-boundary`, `durable-events`。

PROVIDES: physical acceptance 的可信证据与 prompt-specific at-most-one guarantee。

FAILURE MEANING: RED = 一次 logical send 可因 receipt 混淆、restart 或 retry 产生重复 logical effect，或无真实物理证据时被宣称 accepted。

INDEPENDENT CHANGE: Host 原生提供可靠 idempotency key 时，可整体替换当前 send HOW，而 authority WHAT 不动。

CURRENT EVIDENCE: PROMPT-005/007/011；`PromptDispatcherSend.fs`、`PromptAuthorityLedger.fs`；fire-and-forget 与 dispatch tests。
