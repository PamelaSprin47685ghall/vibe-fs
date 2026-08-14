# Feedback

## `behavior-diagnosis`

WHY: 工程行为问题不能因为某个词或一次失败就自动成立；diagnosis 必须有 trigger、negative evidence 与 distinction，避免把普通现象升级成病理标签。

OWNS:
- behavior pathology/lesson 的 diagnosis contract。
- 每个 diagnosis 的独立 semantic identity，而不是评分向量。
- trigger/positive evidence、negative evidence、confounder/distinction 的边界。
- diagnosis occurrence 是一次语义事件；历史压缩不创造新 occurrence。
- diagnosis 不自动决定修复 authority。

DOES NOT OWN:
- diagnosis 如何/何时展示给 Main。
- feedback dedupe/coverage。
- 当前 Blogger/Chronicler 工具名、目录格式、tip 名。
- fixed score vector/ordinal。

DEPENDS ON: `semantic-trace`。

PROVIDES: 可复用、可引用的 behavior diagnosis facts。

FAILURE MEANING: RED = 模糊关键词、评分或单一表象就能制造 diagnosis，或 history rewrite 被误当成新问题发生。

INDEPENDENT CHANGE: 规则载体从 Markdown directory 改成 typed catalog，而 diagnosis semantics 不动。

CURRENT EVIDENCE: `docs/why/enforcer.md`；tip directory SSOT；chronicle occurrence；Observation pairing；score-vector clean break。

---

## `guidance-delivery`

WHY: diagnosis 成立不等于应该在每轮重复全文；反馈是否需要再次进入当前 horizon，是独立于 diagnosis truth 的 delivery/coverage 问题。

OWNS:
- diagnosis occurrence 与 guidance delivery 的分离。
- 首次提供可执行 guidance、重复时避免无界全文膨胀的 dedupe policy。
- durable delivery frontier 与 current-horizon semantic coverage 分离。
- reanchor/compaction 丢失 coverage 后可重新给全文，但这不是新 occurrence。
- detection material 与 remediation material 可面向不同 audience；共享 semantic identity，不互相泄漏职责。
- guidance delivery 不创建新的 interaction authority。

DOES NOT OWN:
- diagnosis 是否成立。
- provider projection mechanics。
- horizon admission general law。
- 当前 `main.md/enforcer.md` 物理布局。
- interaction authority 的创建/继续权（`interaction-authority` 拥有）；delivery 只经 projection 进 horizon，不 mint authority。

DEPENDS ON: `behavior-diagnosis`, `participant-horizon`, `durable-events`。

PROVIDES: 同一 diagnosis 在 durable history 与当前 participant horizon 之间的可恢复 delivery guarantee。

FAILURE MEANING: RED = guidance 可无限重复、reanchor 后永久丢失，或重新交付被误记成新 pathology occurrence。

INDEPENDENT CHANGE: 从 Full/Identity 改成摘要+按需展开，而 diagnosis ontology 不变。

CURRENT EVIDENCE: ENFORCER TipDeliveryFrontier / TipSemanticCoverage；Full/Identity；Main overlay 与 Blogger Detection Wing。
