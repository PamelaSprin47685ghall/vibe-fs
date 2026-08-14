# `work-record`

WHY: 一段 work 跨 participant、跨等待方式、跨 review/finality 边界传递时，需要一个 bounded、canonical、self-contained 的工作陈述；若改用 session head、terminal summary、receiver-relative 摘要或固定 report DTO，会丢失 invocation 边界、复制 Opening 或让 Host 替 participant 解释工作。

OWNS:
- WorkRecord belongs to a piece of work, not to a receiver。
- record 有明确 frozen work range/frontier；reusable session memory 不扩大下一次 record。
- canonical record 可由 preserved Opening、compressed Chronicle、uncovered Recent work 构成；这些是 representation coverage，不是“读者是否最近看过”。
- Opening 是 preserved semantic interval，不从 Assignment/requirements 文本重建；commitment boundary 一旦关闭不移动。
- canonical record 保留 Opening，即使某投影省略它。
- parent→child / child→parent / sync/async 可选择不同 projection，但消费同一 canonical record protocol。
- invocation 的正式 claim 是 bounded Recent work 中最后一条 assistant statement；不增加独立 Closing report channel。
- WorkRecord claims are prose, not universal schema；禁止强制 Summary/Files/Tests/Risks 等固定字段。
- RecordCoverage 与 PrefixCoverage 是不同证明量纲；WorkRecord 可含 canonical RawGap，但 RawGap 不证明 prefix replaceable。

DOES NOT OWN:
- semantic trace 的原始 capture/store。
- Y/Companion 如何生成 Chronicle。
- context compression policy。
- review judgement/assurance。
- delegation/finality 何时请求 record。
- 当前 type/module/三段标题字面必须永久不变。

DEPENDS ON: `semantic-trace`, `context-compression`, `participant-horizon`。

PROVIDES: delegation、review、finality、process feedback 可共享的 bounded canonical work statement。

FAILURE MEANING: RED = consumer 收到的工作记录混入其它 invocation/session 历史、丢失 constitutive Opening、因 receiver 不同而改变事实，或要求 participant 填固定 DTO 才算完成。

INDEPENDENT CHANGE: 完全重写 renderer/section representation，只要 work-boundedness、preserved Opening、coverage 分型、prose-claim 与 projection semantics 不变；review/finality/delegation WHAT 无需改变。

CURRENT EVIDENCE: COMPANION-003/014/015；ARCH-015；TODO-008；REVIEW-014/016；`docs/why/companion.md`；`docs/why/todo.md`；`docs/why/review.md`；canonical LWR materializer/proofs。
