# Failure / recovery

## `provider-attempt-recovery`

WHY: 单次 provider attempt 失败后，系统需要在不重新选择 Authority、不改变 participant identity 的前提下换物理执行绑定继续，同时防止无限自动消耗资源。

OWNS:
- confirmed provider-attempt failure → bounded retry opportunity。
- retry 只改变 EffectiveExecutionBinding，不改变 AuthorityRoot、Role、Persona、ProviderLanguage、system identity。
- retry cursor/budget 的单一 controller；同一失败只能推进一次。
- provider/Host attempt ordinal 不等于领域连续失败计数。
- success 可关闭 failure streak；不得靠持久化 transport attempt 数推断。
- budget exhausted 后停止自动 recovery；内部 cursor/budget 不进入 participant horizon。

DOES NOT OWN:
- crash/restart 后临时状态恢复。
- loop pathology detector。
- interaction authority 本身。
- current AABB names/offset representation/具体预算数值。

DEPENDS ON: `participant-identity`, `interaction-authority`。

PROVIDES: same-participant bounded retry guarantee。

FAILURE MEANING: RED = provider failure 可以重选 authority/换人格，或同一 failure 被重复记账导致错误消耗 recovery budget。

INDEPENDENT CHANGE: 把当前 cursor policy 换成别的 bounded peer-selection policy，而 interaction/identity contracts 不变。

CURRENT EVIDENCE: `docs/why/fallback.md`；FallbackController/Projection；PROMPT-006/014；fallback tests。

---

## `crash-reconciliation`

WHY: restart 会丢失 process-local state，却不会自动撤销已经发生的外部事实；恢复必须从 durable facts + 可信物理 observation 重新进入普通程序，不能从缓存、时间或“上次大概做到哪”猜状态。

OWNS:
- process-local state 不作为 crash recovery authority。
- restart 从 durable facts/projections 与 Host/Git 等物理 observation 重建当前世界。
- unresolved external effect 先 reconcile，再决定是否可重试。
- recovery 复用正常 workflow entry points，不发明永久 `RecoveryStage` 程序计数器。
- ambiguous/multiple/missing evidence 时 fail closed。
- process-local permits/waiters/sensors restart 后可安全消失；没有 fresh evidence 就没有自动 effect。

DOES NOT OWN:
- 各 domain 的具体 durable facts。
- effect Requested/Accepted law。
- provider-attempt retry。
- managed-session replacement、publish reconcile 等 domain-specific恢复规则。

DEPENDS ON: `durable-events`, `effect-accounting`, `structured-workflow`, `host-boundary`。

PROVIDES: restart 不制造新事实、不重复未知 effect 的 recovery guarantee。

FAILURE MEANING: RED = restart 后必须相信临时内存/日志/时间猜测才能继续，或会把 outcome unknown 的 effect 当作未发生而重放。

INDEPENDENT CHANGE: 把 startup sweep 改成 lazy on-demand reconciliation，而 durable/domain contracts 不变。

CURRENT EVIDENCE: PERSIST recovery；Prompt pending recovery；AttachedSession restore；Orchestrator recoveryAction；Context/Reviewer event-driven recovery；DSL-004/FLOW-005。

---

## `degeneration-guard`

WHY: provider attempt 可能在尚未正常结束前进入高重复、低信息的退化生成；若不提前截断，会持续污染 transcript 并推迟正常 recovery。

OWNS:
- attempt-local streaming pathology detection。
- detector 只读取窄字符流特征，不把 stream delta 积分成业务事实。
- bounded-memory、fixed/explicit detector semantics；不按角色/自然语言动态放宽。
- detector 命中只停止当前 physical attempt，并桥接到既有 provider-attempt recovery；不创建第二 retry state machine。
- detector 生命周期绑定一次 ProviderRun，attempt 结束即丢弃。

DOES NOT OWN:
- retry cursor/budget。
- transcript semantic truth。
- current 4-gram/exponential-kernel algorithm 必须永久存在。
- arbitrary quality judgement。

DEPENDS ON: `provider-attempt-recovery`, `host-boundary`。

PROVIDES: 在病态输出扩大前，把该 attempt 转交标准 recovery 的 signal。

FAILURE MEANING: RED = 明显退化 attempt 可以无限污染历史，或 detector 自己成为新的业务 truth/retry controller。

INDEPENDENT CHANGE: 换掉当前 detector，只要它仍 attempt-local、bounded、非权威并复用标准 recovery。

CURRENT EVIDENCE: `docs/{why,what}/loop.md`；LoopDetector；LoopKillArmed→FallbackController bridge；loop tests。
