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

---

# Phase D — test/gate 分类（逐 family 标 KEEP / SPLIT / MECHANISM / DELETE）

判据（HANDOFF §14 Phase D）：

```text
KEEP(owner)     = 整个 family 语义断言唯一归一个 package，保留
SPLIT(x/y/...)  = 一个 family 内含多个 semantic owner，拆 oracle
MECHANISM       = 共享 checker/harness；语义 oracle 由各 package 拥有
DELETE          = migration-only proof；新世界基线稳定后删除
ORPHAN          = 断言存在但无 package owner（缺陷，需修）
```

## Gates（scripts/checks，24 项）

| Gate | Class | Owner(s) |
|---|---|---|
| `architecture.mjs` | MECHANISM | verification-system（layer-0 harness）；语义分属 structured-workflow（ARCH-008 禁止词）/ host-boundary（ARCH-002/003 分层） |
| `capability-isomorphism-gate.mjs` | KEEP | capability-enforcement |
| `causal-wait-boundary.mjs` | KEEP | causal-wait |
| `dsl-ownership.mjs`（+ratchet） | SPLIT | structured-workflow（positive：无程序计数器/语义 vocabulary）+ DELETE（legacy symbol blacklist 部分） |
| `e2e-watchdog-feed.mjs` | MECHANISM | verification-system（proof harness；因果续期语义借 causal-wait） |
| `enforcer-cross-family-collision.mjs` | KEEP | guidance-delivery（detection/remediation audience 分离） |
| `enforcer-rulebook-gate.mjs` | DELETE/retired | behavior-diagnosis（retired stub；tip 目录 SSOT / 唯一 TipName 由 `tests/unit/enforcer/**` catalog 测试承担） |
| `g4r-ce-vocabulary.mjs` | KEEP | structured-workflow（CE vocabulary）；ratchet 基线稳定后弱化 |
| `g4r-freeze.mjs` | DELETE | migration freeze ratchet |
| `js-surface-gate.mjs` | KEEP | repository-programming（surface 应用 capability-enforcement 同构律） |
| `kolmogorov-size.mjs` | MECHANISM | verification-system（non-blocking style signal，无 semantic owner） |
| `language-parity-gate.mjs` | SPLIT | provider-language（结构 parity）+ office-capability + action-affordance + 各 domain cognition |
| `p0-recovery-join.mjs` | SPLIT | effect-accounting（false finality：aborted≠terminal）+ crash-reconciliation（recovery） |
| `prompt-depth-ratchet.mjs` | SPLIT | cognitive-environment + office-capability + action-affordance（MECHANISM 共享 catalog） |
| `provider-leak-gate.mjs` | SPLIT | participant-horizon（positive admission law）+ DELETE（历史 DTO blacklist ratchet） |
| `provider-prose-ownership.mjs` | KEEP | provider-language（ARCH-016 Gate E：prose ownership 三向） |
| `semantic-anchors.mjs` | MECHANISM | 共享 catalog；semantic ID 逐条声明 owner（cognitive-environment / office-capability / action-affordance / epistemic-reasoning / review-judgement） |
| `session-ownership-ratchet.mjs` | KEEP | session-ontology（HOST-008 execution class × ownership） |
| `spec.mjs` + `spec-rules.mjs` | MECHANISM | requirement-system（document-governance 唯一 owner / Clause 结构）+ verification-system（可红） |
| `student-teacher-absence.mjs` | DELETE | migration absence ratchet |
| `test-boundary.mjs` | MECHANISM | verification-system（test/Fable anti-corruption boundary） |
| `tool-referential-integrity.mjs` | SPLIT | action-affordance + capability-enforcement（ARCH-007 same-name-same-contract） |
| `unified-store-gate.mjs` | KEEP | durable-events（单一 substrate / no feature store） |

## Test families（tests/unit）

| Dir（文件数） | Class | Owner(s) |
|---|---|---|
| `agent/`（6） | SPLIT | participant-identity（catalog/persona）+ capability-enforcement（inquiry/sphinx/browser/semble 权限）+ external-investigation（browser）+ epistemic-reasoning（sphinx MCP）+ knowledge-reuse（semble warm-start） |
| `casebook/`（14） | KEEP | knowledge-reuse（Case/fetch/replay/Bookkeeper）+ durable-convergence（并发 DomainConflict）交叉 |
| `codec/`（5） | SPLIT | durable-events（canonical JSON）+ provider-projection（Semantic/Wire） |
| `context/`（24） | SPLIT | semantic-trace（x-trace）+ work-record（lifecycle-work-record）+ context-compression（blog/probe/recovery-slot）+ prefix-stability（prefix-epoch）+ provider-projection（synthetic-toml/projection-algebra）+ durable-events（fold） |
| `domain/`（7） | SPLIT | degeneration-guard（loop-detector/sensor）+ obligation-ledger（magic-todo*）+ crash-reconciliation（session-recovery-combine）+ structured-workflow（reconcile-program） |
| `enforcer/`（22） | SPLIT | behavior-diagnosis（catalog/codec/observation/cycle）+ guidance-delivery（tip-guidance/Full-Identity/frontier）+ context-compression（blogger convergence/squash）交叉 |
| `execution/`（24） | SPLIT | delegation（join/fork）+ process-execution（process-wait）+ output-distillation（executor-summarize）+ managed-session-lifecycle（handle）+ time-capability（timer-port/devops-join-timeout）+ effect-accounting（join-aborted-not-terminal）+ work-record |
| `fallback/`（1） | KEEP | provider-attempt-recovery（cursor） |
| `git/`（4） | KEEP | change-integration（IntegrationGate/GitGateway）+ durable-events（store ref） |
| `glory/`（4） | KEEP | finality（lifecycle/opening-floor）+ obligation-ledger + review-assurance 交叉 |
| `host/`（18） | SPLIT | host-boundary（flattening/events/snapshot/quiescence）+ session-ontology（session-ownership）+ provider-language（execution-binding）+ interaction-authority（idle-continuation/needhelp）+ prefix-stability（pair-thought-anchored）+ capability-enforcement（managed-agent-config） |
| `invariants/`（6） | SPLIT | durable-events（corruption/payload）+ effect-accounting + prefix-stability |
| `journal/`（10） | SPLIT | durable-events（fold/codec/writer）+ 各 domain projection（semantic-trace / work-record / obligation-ledger / finality / behavior-diagnosis） |
| `js-tools/`（9） | KEEP | repository-programming（surface/sandbox/transaction/anchor） |
| `kernel/`（4） | SPLIT | structured-workflow（DomainFlow/Outcome）+ session-ontology（SessionOwnership）+ causal-wait + participant-identity（Roles） |
| `orchestrator/`（4） | KEEP | change-integration + delegation 交叉 |
| `persist/`（5） | KEEP | durable-events + durable-convergence |
| `process/`（9） | KEEP | process-execution（PTY/run/signal/exit）+ output-distillation（LargeGate）+ time-capability（Deadline） |
| `prompt/`（6） | SPLIT | interaction-authority（authority）+ dispatch-protocol（fire-and-forget/send）+ participant-identity（session-persona）+ prefix-stability/provider-language（prompt-stability） |
| `reconciliation/`（4） | SPLIT | crash-reconciliation（child-recovery）+ host-boundary（xwire）+ obligation-ledger（magic-todo-membrane）+ interaction-authority（completed-turn-classifier） |
| `review/`（2） | SPLIT | review-assurance（seal/witness）+ review-judgement |
| `session/`（12） | KEEP | managed-session-lifecycle（Attached/Fork/Satellite/Handle）+ delegation（SyncDelegate） |
| `sphinx/`（8） | KEEP | epistemic-reasoning |
| `strength/`（16） | KEEP | speculative-investigation（candidate/promotion/policy）+ semantic-trace 交叉（unpromoted≠history） |
| `temporal/`（6） | KEEP | time-capability（fake clock）+ structured-workflow + causal-wait |
| `tools/`（14） | SPLIT | repository-programming（js tools）+ process-execution（pty tools）+ capability-enforcement（tool registry） |
| `verify/`（24） | SPLIT | 最大混合 dir：participant-horizon + provider-language + office-capability + action-affordance + cognitive-environment + verification-system（MECHANISM） |
| `resources/`（1） | KEEP | distribution（package resources）+ provider-language（localization） |
| 顶层 3 文件（domain.meta / guide-contract / verdict-feed）+ run.mjs | MECHANISM/SPLIT | verification-system（run harness）+ review-judgement（verdict-feed）+ requirement-system（domain.meta） |

## Test families（integration / e2e / eval）

| Dir | Class | Owner(s) |
|---|---|---|
| `integration/package/`（4） | KEEP | distribution |
| `integration/persist/`（3） | KEEP | durable-events + durable-convergence（dumb-server） |
| `integration/plugin/`（5） | SPLIT | repository-programming（file-mutation）+ capability-enforcement（manager-tool/bash-honeypot/auto-injected）+ obligation-ledger（magic-todo-sink） |
| `integration/resources/`（2） | KEEP | distribution + behavior-diagnosis（enforcer-rulebook 资源，tip SSOT） |
| `integration/strength/`（1） | KEEP | speculative-investigation |
| `e2e/entry`（1） | MECHANISM | verification-system（Long Stroke harness） |
| `eval/provider-office-boundary`（1） | KEEP | office-capability + participant-horizon |

## ORPHAN / missing oracle（需补，不是 DELETE）

1. `external-investigation`：目前 proof = Browser Role Law anchors + role-lock，**缺真实 browser provenance behavioral oracle**（测试默认 disabled，不打真实 git）；需补 source/provenance canary，否则该包 acceptance 只能靠 prompt regex。
2. `output-distillation`：Distiller fragment-humility 目前主要靠 prompt/proof table，**缺 behavioral fixtures**（fragment≠整体成功、不发明因果、locatability）。
3. `verification-system` proof ladder：是否真被 `scripts/check.mjs` 按 static→pure→temporal→adapter→Long Stroke 分层执行，需逐层验「可红」，否则 ladder 是文档声明不是门禁。
4. 家族级 ORPHAN：0（所有 family 均有 owner）。file-level ORPHAN 审计推迟到 cutover 时逐断言做。

## Mandatory splits / deletes（最终清单）

```text
SPLIT:
  1. tests/unit/prompt/authority.test.mjs        → interaction-authority + dispatch-protocol
  2. scripts/checks/semantic-anchors.mjs         → MECHANISM + 逐 ID 声明 owner
  3. scripts/checks/language-parity-gate.mjs     → provider-language(结构) vs office/tool/domain(语义)
  4. projection-algebra.test.mjs                 → provider-projection(algebra) vs 各 feature intent 语义
  5. tests/unit/verify/（24 文件）                → 按断言逐条拆 owner（最大混合 dir）
  6. tests/unit/context/、execution/、host/、enforcer/、domain/  → 按表内 owner 拆 oracle

DELETE（migration-only，新世界基线稳定后删）:
  - scripts/checks/student-teacher-absence.mjs
  - scripts/checks/g4r-freeze.mjs
  - dsl-ownership ratchet 的 legacy symbol blacklist 部分
  - provider-leak-gate 的历史 DTO blacklist（转为 positive admission law 后）
  - 各 absence/clean-break tests（legacy prompt paths、old catalogs、double renderer）
```

## Phase D 统计

```text
Gates:  KEEP 10 / SPLIT 8 / MECHANISM 6 / DELETE 2（部分组合项按主导 class 计）
Family: KEEP 18 / SPLIT 14 / MECHANISM 3（integration 层 KEEP 5 / SPLIT 1 / MECHANISM 1）
ORPHAN: 0（family 级）；missing oracle 3 项待补
```
