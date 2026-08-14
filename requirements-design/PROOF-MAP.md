# Initial proof ownership projection

本表是**迁移设计**，不是当前 test runner 配置。目标是让未来每个 executable semantic oracle 恰好有一个 package owner；同一个 checker/framework 可以被多个 package 调用，但一条断言不能靠“大家都负责”维持。

## Meta / architecture

| Future owner | Current proof evidence / migration note |
|---|---|
| `requirement-system` | 未来新增 manifest/dependency/unique-proof-owner verifier；现有 document-governance 只能作考古证据，不直接搬 Clause gate。 |
| `verification-system` | `docs/proof/verify.md`、test runner/watchdog、coverage/fresh-dist/release/Long Stroke proof architecture；具体产品断言迁回产品 owner。 |
| `structured-workflow` | DSL ownership/static semantic scans、Flow/DSL pure-law tests；旧 symbol blacklist 仅作 migration proof，最终应以 positive structure ownership 取代。 |
| `time-capability` | ambient clock/timer scans + fake/virtual time tests。 |
| `causal-wait` | causal CE observability / wait-fact temporal tests；必须证明 diagnostic observation 不写 authority facts。 |

## Session / Host

| Future owner | Current proof evidence / migration note |
|---|---|
| `session-ontology` | HOST-008 execution-class × ownership classification tests；不得由 agent name/tool set 猜 kind。 |
| `managed-session-lifecycle` | Attached session reuse/replacement/cascade cleanup、Linkage tombstone/retire、restore tests。 |
| `host-boundary` | Host canaries、snapshot/transform/tool-context adapter contract tests；feature-specific semantics 从 Host proof 中拆回 owner。 |

## Participant / provider

| Future owner | Current proof evidence / migration note |
|---|---|
| `participant-identity` | `tests/unit/prompt/session-persona.test.mjs`；`prompt-stability.test.mjs` 中 Persona/Role/Binding stability 部分。 |
| `office-capability` | Gate F / `OFFICE_CAPABILITY_ANCHORS`；Manager Role Law + fork projection 的 consequence equivalence。checker 可共享，canonical consequence oracle 只归本包。 |
| `capability-enforcement` | `agent-permission-gate.test.mjs`、capability-isomorphism、Attempt ToolCapabilitySet/schema/runtime gate parity、MCP role locks。 |
| `participant-horizon` | `provider-leak-gate.test.mjs`、`horizon-surface.test.mjs`、provider identity leak；blacklist 最终应向 consequence/admission positive law 收敛。 |
| `cognitive-environment` | Role/Common/Library composition + semantic-depth assertions，但其中 office/review/Sphinx 的 canonical meaning 要由各 owner 提供，不在本包复制。 |
| `action-affordance` | `TOOL_DESCRIPTION_ANCHORS` 与 high-risk action local-contract tests；action description 的 mirrored fact 可引用其它 package semantic IDs。 |
| `provider-language` | language parity：locale leaves、placeholder structure、invariant identifiers、session bind-once/inheritance；Role/tool semantic correctness分别由其 owner验。 |
| `provider-projection` | `projection-algebra.test.mjs` 中 intent order/merge/conflict/permutation/deterministic render、Semantic vs Wire codec tests。具体 intent 合法性不归本包。 |
| `external-investigation` | Browser Role Law provenance/source-closest/visual/disagreement anchors + Browser-only network capability integration；未来应补 source/provenance behavioral oracle，不只 prompt regex。 |

## Interaction / durability

| Future owner | Current proof evidence / migration note |
|---|---|
| `interaction-authority` | `authority.test.mjs` 中 PhysicalUser≠Authority、Root/Continuation/Unknown origin、profile invariants。 |
| `effect-accounting` | PERSIST-009 Requested/Accepted/unknown outcome tests；跨 prompt/worktree/repository transaction 应共用 law oracle。 |
| `dispatch-protocol` | `fire-and-forget.test.mjs`、send/accept lifecycle、PromptKey/recovery no-blind-resend 部分；从 `authority.test.mjs` 裂出。 |
| `durable-events` | EventStore append/publish/fold/corruption/payload-ref tests；feature event semantics不归 store proof。 |
| `durable-convergence` | dumb-server / remote converge / concurrent-head tests；Casebook object conflict只由 Casebook/knowledge owner解释。 |

## Work / context

| Future owner | Current proof evidence / migration note |
|---|---|
| `delegation` | fork/commission/inspect/sync-delegate contract tests；同-road continuation、returned bounded record、hidden machine topology。 |
| `process-execution` | PTY/process run/signal/onExit/cancel/join physical behavior tests。 |
| `output-distillation` | Distiller fragment-humility/merge-conflict/locatability tests；目前主要是 prompt/proof table，未来需要 behavioral fixtures。 |
| `change-integration` | Orchestrator job/rebase/publish claim/CAS/recovery tests。 |
| `semantic-trace` | XTrace append/capture/frontier/range tests；Strength unpromoted absence只作为消费方 cross-check。 |
| `work-record` | COMPANION LWR materialization、Opening preserved、request-range bound、RecordCoverage/RawGap、no fixed Closing/report schema。 |
| `context-compression` | context failure-driven probe/squash/coverage tests；candidate failure must write no committed fact。 |
| `prefix-stability` | `prompt-stability.test.mjs` byte invariants、append-only prefix law、ActivePrefixEpoch/reanchor/rebase proofs。 |

## Recovery / mission / review

| Future owner | Current proof evidence / migration note |
|---|---|
| `provider-attempt-recovery` | fallback controller/cursor/confirmed-failure/budget tests；identity/prompt stability引用 identity/language package guarantee。 |
| `crash-reconciliation` | startup recovery families、pending effect reconciliation、Attached restore、Orchestrator recoveryAction；domain outcome断言回各 domain owner。 |
| `degeneration-guard` | LoopDetector bounded-memory/pathology fixtures + kill→normal-recovery bridge。 |
| `obligation-ledger` | MagicTodoProjection/admission/checkpoint/current-obligations/T1 tests；Host TodoTable 仅 compatibility sink proof。 |
| `review-judgement` | Reviewer judgement semantics、PERFECT/REVISE discrimination/minor-nonblocking fixtures；不能只靠 prompt anchors。 |
| `review-assurance` | challenge seal、tree invalidation、自包含 witness、VerdictKnown vs ConsumableReview、request-bounded evidence。 |
| `finality` | suicide/finality lifecycle、drain、rejection/blessed/rest、last_words、hidden review surface。 |

## Feedback / repository / optimization

| Future owner | Current proof evidence / migration note |
|---|---|
| `behavior-diagnosis` | Enforcer rule validation、tip occurrence/selection/Observation pairing；score-vector absence最终只作 migration evidence。 |
| `guidance-delivery` | Full/Identity、TipDeliveryFrontier vs TipSemanticCoverage、reanchor redelivery/no-new-occurrence。 |
| `repository-investigation` | Inspector causal-readonly/action boundary、RepositoryWarmStart low-trust/no-fake-read tests、typed observation capture。 |
| `knowledge-reuse` | Casebook fetch/replay/freshness≠correctness/Bookkeeper no-new-evidence/concurrent conflict tests。 |
| `repository-programming` | JS capability projection, sandbox, file/glob/grep semantics, transaction staging/all-or-nothing/conflict/return validation。 |
| `speculative-investigation` | Strength candidate/promotion/no-reflection/replay/K0/failure isolation tests。 |
| `epistemic-reasoning` | Sphinx No-Free-Information、dependency-aware equivalence、qualified posterior、controller-owned continuation、A*/Bayes/MCTS degeneration proofs。 |
| `distribution` | `tests/integration/package/{contents,install,import,resources}.test.mjs` + `npm pack --dry-run` / release artifact proof。 |

## Mandatory splits / deletes before normative cutover

1. `authority.test.mjs` 必须至少拆 `interaction-authority` 与 `dispatch-protocol`；legacy agent-name assertions 单独判定是否删除。
2. `semantic-anchors.mjs` 可以继续作为共享 checker/catalog mechanism，但 semantic IDs 必须声明 package owner；不能再用一个“大 Prompt Gate”拥有所有 cognition。
3. `language-parity-gate.test.mjs` 把语言结构 parity 与 office/tool/domain semantic meaning 解耦。
4. `projection-algebra.test.mjs` 保留 algebra oracle；Repair/Review/Companion/Strength 的产品语义断言迁回各 owner。
5. provider leak blacklist 可在迁移期保留作为 ratchet，但未来 horizon proof 应主要验证 positive information-admission law，而不是不断累加历史 DTO token 名。
6. clean-break/absence tests（Student/Teacher、legacy prompt paths、old catalogs、double renderer 等）逐项判断：若只证明迁移完成，则在新世界基线稳定后删除，不进入永久 package verifier。
