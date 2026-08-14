# Mission / judgement / finality

## `obligation-ledger`

WHY: 长期 mission 需要一个持续诚实的“当前还欠世界什么”账本；若用 phase/status/meta-work 代替，系统会把计划、等待、评审等过程动作冒充用户债务。

OWNS:
- current obligations = mission debt，而不是 workflow stage。
- 一个 accepted account 立即成为当前 obligations 的 canonical statement；后续 accepted account supersede 前一 account。
- obligation identity/continuity、增加/完成/删除的语义。
- checkpoint 作为真实 commitment point，而不是隐藏 Activation state。
- accepted account 不因 reviewer REVISE 自动 rollback；需要更真实的新 account 才改变当前义务。
- process review cadence 与 obligation checkpoint 的交界，只拥有“何时产生/消费 review obligation”的账本侧规则。

DOES NOT OWN:
- Reviewer judgement meaning。
- Review evidence/witness assurance。
- Finality acceptance。
- Host TodoTable/UI sink。
- 当前 `todowrite` schema、字段名、T1 文案具体 wording。
- Manager Persona/Role Law。

DEPENDS ON: `durable-events`, `effect-accounting`, `semantic-trace`。

PROVIDES: Manager/mission lifecycle 的 canonical outstanding-obligation account。

FAILURE MEANING: RED = 当前 mission debt 无唯一真相源，或 workflow/meta-work 被当成用户仍欠的结果，或 REVISE 能静默回滚已经 accepted 的 account。

INDEPENDENT CHANGE: 从当前 `todowrite` UI/schema 改成另一 obligation authoring surface，而 canonical account/checkpoint semantics 不变。

CURRENT EVIDENCE: `docs/why/todo.md`；TODO-001..015；type `Domain/{MagicTodo,MagicTodoFacts,MagicTodoAdmission,MagicTodoAfter,MagicTodoObligationCodec}.fs`；wiring `Application/Reconciliation/{MagicTodoMembrane,MagicTodoLocality}.fs`；fact `Journal/{MagicTodoProjection,MagicTodoFactCodec}.fs`；magic-todo、`tests/unit/reconciliation/magic-todo-membrane.test.mjs`。

---

## `review-judgement`

WHY: PERFECT/REVISE 是对工作是否足以被接受的判断，不是“越谨慎越多拒绝”的表演，也不是固定 checklist 的机械总分。

OWNS:
- acceptance/rejection 都必须由 discrimination 挣得。
- judgment 相对 root requirement/current reviewed object，而非 reviewer mood。
- material defect 才能 withhold acceptance；non-blocking workmanship 可与 acceptance 共存。
- PERFECT 不等于全知或字面无瑕；REVISE 必须购买实质更好/更真的结果。
- evidence、inference、preference 与 defect 的判断边界。
- Reviewer 的 epistemic/craft guidance；fixed report DTO 非必要。

DOES NOT OWN:
- 一次 judgement 是否被因果确认/可消费。
- dual-confirmation/seal/witness protocol。
- process-review cadence。
- finality lifecycle。
- Reviewer hidden session mechanics。

DEPENDS ON: `cognitive-environment`, `participant-horizon`。

PROVIDES: `PERFECT | REVISE` 等 judgement 的语义合同。

FAILURE MEANING: RED = reviewer 可以凭表演式谨慎、固定 checklist 或无证据偏好拒绝/接受工作。

INDEPENDENT CHANGE: 全面重写 Reviewer Ledger/判断哲学，而 review witness/seal/finality protocol 不变。

CURRENT EVIDENCE: `docs/why/review.md` judgement sections；type `Application/Review/{VerdictWorkflow,ReviewerEvidence}.fs`；resource `resources/provider/role/reviewer/`、`resources/provider/library/reviewer/`、`resources/provider/review/challenge/`；reviewer verdict tests。

---

## `review-assurance`

WHY: 一个 reviewer 输出了 judgement，不等于系统已经证明该 judgement 针对正确对象、消费了必要 challenge、并带着可供 caller 使用的完整证据。

OWNS:
- reviewed object/tree/frontier 的 identity binding。
- bounded canonical work record/evidence materialization；session head 不能冒充 request-bounded evidence。
- witness self-containment；外围 mutable map 不能成为确认依据。
- reviewed object 变化使旧 witness 失效。
- 对需要额外确认的 review，challenge 必须在第二次 judgement 的真实 input/seal 中被证明消费。
- `VerdictKnown` 与 `ConsumableReview(record-ready)` 分型。
- infrastructure failure 不伪装成 REVISE（review-side 本地负边界；三态分离：tool 语法红 → `capability-enforcement`、语义 REVISE → `review-judgement`、infra fatal fail-fast → `host-boundary`/`crash-reconciliation`）。
- process review 与 terminal review 可以有不同 assurance strength；两者不得互相计数。

DOES NOT OWN:
- PERFECT/REVISE 的判断哲学。
- obligation account 本身。
- Finality 是否需要什么 review cohort。
- Host/provider identity acquisition HOW。
- WorkRecord prose 的业务内容；只要求 bounded canonical source/coverage。
- tool red / infra fatal 的分类本身（分属 `capability-enforcement` 与 `host-boundary`/`crash-reconciliation`）；本包只拥有 review-side 的“不把 infra 伪装成 REVISE”负边界。

DEPENDS ON: `review-judgement`, `semantic-trace`, `durable-events`, `causal-wait`。

PROVIDES: “这个 judgement 对这个对象已经有资格被消费”的 assurance guarantee。

FAILURE MEANING: RED = 系统可以消费针对旧 tree/错误 frontier/未看 challenge/缺 report 的 judgement，或把基础设施失败当业务 REVISE。

INDEPENDENT CHANGE: 将 dual-PERFECT+seal 换成另一 causally verifiable confirmation protocol，而 judgement meaning/finality contract 不变。

CURRENT EVIDENCE: REVIEW-003/006/013..020；`docs/why/review.md`；type `Domain/{ReviewWitness,ReviewChallenge}.fs`；wiring `Application/Review/{ReviewBarrierWorkflow,ReviewerContinuation}.fs`、`Application/Reconciliation/ReviewSeal.fs`；fact `Journal/{ReviewBarrier,ReviewProjection,ReviewFactFold}.fs`、`Journal/FinalityReviewCohort.fs`；tests `tests/unit/review/**`、seal/witness。

---

## `finality`

WHY: participant 自己认为 mission 完成不等于世界允许不可逆结束；终结资格必须建立在当前义务、当前被审对象与合格 review evidence 上，同时把 rejection、accepted-but-still-finishing、rest 分成不同经验。

OWNS:
- terminal request 的资格与 irreversible life completion semantics。
- terminal request 前未消费 process obligations 的 drain rule。
- current tree/current evidence 上的 terminal review cohort/assurance consumption。
- rejection / blessed / rest 三种 participant experience；Acceptance ≠ rest。
- rejection 后继续工作的合法 continuation；accepted 后 non-blocking workmanship 不撤销 acceptance。
- final answer/last words 与 successful completion 的关系。
- hidden terminal quality mechanism 不变成 Manager checklist；只暴露会改变下一行动的 consequence/report。

DOES NOT OWN:
- Reviewer judgement standard。
- assurance primitive 的内部实现。
- obligation account semantics。
- 当前 `suicide` 字面工具名或叙事风格必须永久保持。
- generic session lifecycle（life completion 触发的 dedicated reviewer session 退休由 `managed-session-lifecycle` owner-closure 消费，是下游 effect，非 finality 定义前提）。

DEPENDS ON: `obligation-ledger`, `review-assurance`, `participant-horizon`。

PROVIDES: mission 何时真正可以结束的 product guarantee。

FAILURE MEANING: RED = participant 可绕过 outstanding obligations/review 直接结束，或 acceptance/rejection/rest 被压成一个含混 terminal state。

INDEPENDENT CHANGE: 改 terminal UX/tool 名/hidden reviewer cohort shape，而“只有合格证据才允许 life completion”的 WHAT 不变。

CURRENT EVIDENCE: `docs/why/glory.md`；GLORY/TODO/REVIEW finality clauses；wiring `Application/Finality/{FinalityWorkflow,CohortWorkflow,BlessingWorkflow,RevisionWorkflow,RecordWorkflow}.fs`、`Application/Manager/ManagerFinality.fs`；type `Domain/{FinalityPrompt,MagicTodoFinalityCohort}.fs`；tests `tests/unit/glory/**`。
