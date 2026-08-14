# Source / runtime evidence ledger

Phase C 产出：把 45 个 future package 的 WHAT 逐条映射到真实 source/runtime evidence，验证不是纯文档幻想。
本文件是 living ledger；只记录「package → 证据」映射，不复制 COVERAGE.md 的 clause 级归属。

## Method

六个证据轴（按 HANDOFF §14 Phase C）：

```text
type     = canonical domain types / pure decisions（Domain/、Kernel/）
wiring   = application workflow / wiring（Application/、Agent/）
host     = Host boundary / adapter（Host/、Infrastructure/OpenCode/、Tools/）
resource = provider-facing resources（resources/provider/**）
fact     = durable facts / projections（Journal/、Infrastructure/Persist/）
failure  = failure paths / recovery / fail-closed（Recovery、Reconciliation、Persist corruption）
```

反幻想判据：一个 package 至少命中 type/wiring/fact/resource/failure 之一且与 WHAT 一致才算 REAL；
只有 prose / semantic anchor 而无 canonical code = THIN；完全无源码 = FANTASY。

## 总判定

```text
REAL  43
META   2（requirement-system、verification-system：治理元契约，证据= AGENTS.md + scripts/checks + CI，正确无 runtime 源码）
THIN   0（external-investigation runtime 落在外部 MCP，但 WHAT 由 Role Law + semantic anchors + role-lock 固化，判 REAL）
FANTASY 0
```

---

# 1. Requirement system / verification

| Package | Evidence | Verdict |
|---|---|---|
| `requirement-system` | 无 runtime 源码（正确）。证据：`AGENTS.md`、`docs/what/document-governance.md`（GOV）、`changes/README.md`、`scripts/checks/spec-rules.mjs`、`spec.mjs`（clause 结构/唯一 owner 检查） | META |
| `verification-system` | 无 runtime 源码（正确）。证据：`docs/proof/verify.md`、`scripts/checks/**`（30 gates）、`tests/unit/verify/**`、CI workflow、`scripts/check.mjs` | META |

---

# 2. Programming / causality

| Package | Evidence | Verdict |
|---|---|---|
| `structured-workflow` | type: `Kernel/DomainFlow.fs`、`Kernel/Outcome.fs`；wiring: `Application/Manager/ManagerWorkflow.fs`、`Application/Review/ReviewerWorkflow.fs`（recursive CE，无 program counter）；failure: `scripts/checks/dsl-ownership.mjs`、`dsl-ownership-ratchet.mjs`、`scripts/checks/kolmogorov-size.mjs`；tests: `tests/unit/temporal/**`、dsl gates | REAL |
| `time-capability` | type: `Kernel/Temporal.fs`、`Process/Deadline.fs`、`Process/PtyTiming.fs`；wiring: `Process/ProcessRunner.fs`（deadline 注入）；tests: `tests/unit/temporal/**` | REAL |
| `causal-wait` | type: `Kernel/CausalWait.fs`、`Session/CausalWaitRegistry.fs`、`Session/CausalAwait.fs`、`Session/CausalWaitBridge.fs`；failure: `scripts/checks/causal-wait-boundary.mjs`（观察不入 Journal）；tests: `tests/unit/` causal-wait/frontier | REAL |

---

# 3. Session / Host substrate

| Package | Evidence | Verdict |
|---|---|---|
| `session-ontology` | type: `Kernel/SessionOwnership.fs`（ExecutionClass × Ownership）、`Session/AgentRoleIdentity.fs`、`Domain/CompanionIdentity.fs`；fact: `Journal/SessionAssociation.fs`、`LinkageProjection.fs`；tests: `tests/unit/host/session-flattening.test.mjs`、`session-execution-binding.test.mjs` | REAL |
| `managed-session-lifecycle` | type/wiring: `Session/AttachedSessionRuntime.fs`、`Session/HandleController.fs`、`Session/ForkRuntime.fs`、`Session/SatelliteRuntime.fs`、`Session/ReuseScope.fs`；fact: `Journal/LinkageProjection.fs`；tests: `tests/unit/session/**`、managed-session restore | REAL |
| `host-boundary` | host: `Host/HostDigest.fs`、`Infrastructure/OpenCode/**`、`Tools/ToolContext.fs`；wiring: `Application/Reconciliation/Reconciler.fs`、`XWire.fs`、`Session/CompanionTransform.fs`；tests: `tests/unit/host/**`（18 文件） | REAL |

---

# 4. Participant / provider world

| Package | Evidence | Verdict |
|---|---|---|
| `participant-identity` | type: `Domain/PersonaCatalog.fs`、`Kernel/Roles.fs`、`Session/AgentRoleIdentity.fs`、`Domain/ManagedAgentCatalog.fs`；fact: `Journal/SessionAssociation.fs`；tests: `tests/unit/agent/**`（session-persona）、`tests/unit/host/session-execution-binding.test.mjs` | REAL |
| `office-capability` | type: `Kernel/Roles.fs`（canonical five-office）；resource: `resources/provider/role/{manager,coder,inspector,devops,reviewer,orchestrator,browser,inquiry,bookkeeper,blogger,distiller}/`；failure: `scripts/checks/semantic-anchors.mjs` OFFICE_CAPABILITY_ANCHORS；tests: `tests/unit/verify/language-parity-gate.test.mjs`、prompt-semantic-depth | REAL |
| `capability-enforcement` | type: `Domain/ManagedAgentCatalog.fs`、`Domain/JsCapability.fs`、`AttemptPlanner.fs`（ToolCapabilitySet）；failure: `scripts/checks/capability-isomorphism-gate.mjs`、`js-surface-gate.mjs`、`agent-permission-gate.mjs`；tests: `tests/unit/` capability-isomorphism、agent-permission、`tests/unit/js-tools/js-surface.test.mjs` | REAL |
| `participant-horizon` | type: `Domain/ProjectionIntent.fs`、`Domain/ProviderProjection.fs`、`Domain/ToolResultBound.fs`；failure: `scripts/checks/provider-leak-gate.mjs`、`tests/unit/verify/provider-leak-gate.test.mjs`、`horizon-surface.test.mjs` | REAL |
| `cognitive-environment` | resource: `resources/provider/{world/common-law, role/**, library/{ingress,closing,kolmogorov,scarcity,reviewer}}/`；wiring: `Infrastructure/Resources/PromptResources.fs`（World/Role/Library 组合）；tests: `tests/unit/resources/prompt-semantic-depth.test.mjs`、PromptRestoration | REAL |
| `action-affordance` | resource: `resources/provider/tool/**`（fork/commission/inspect/judge/suicide/run/… 各含 act/时机/负边界/后果/参数）；failure: `scripts/checks/tool-referential-integrity.mjs`、`prompt-depth-ratchet.mjs`；tests: `tests/unit/verify/` prompt-semantic-depth、tool 描述 | REAL |
| `provider-language` | type: `Domain/ProviderLanguage.fs`；wiring: `Infrastructure/Resources/{ProviderResources,ProviderProse}.fs`；failure: `scripts/checks/language-parity-gate.mjs`、`tests/unit/verify/language-parity-gate.test.mjs`；tests: `tests/unit/host/session-execution-binding.test.mjs`（bind-once） | REAL |
| `provider-projection` | type: `Domain/{ProjectionIntent,ProjectionPlanner,ProjectionRenderer,ProviderProjection,SyntheticToml,XPrefixProjection}.fs`；tests: `tests/unit/` projection-algebra、`tests/unit/strength/projection-algebra.test.mjs`、`tests/unit/strength/projection-adapter.test.mjs` | REAL |
| `external-investigation` | resource: `resources/provider/role/browser/`（Role Law + provenance contract）；wiring: `Kernel/StealthBrowserMcp.fs`、`Agent/AgentProgram.fs`（browser office）；failure: `scripts/checks/semantic-anchors.mjs` Browser consequence、MCP role-lock（capability-enforcement 交叉）；tests: 默认 disabled（uvx 不打真实 git，见 AGENT-026） | REAL（runtime 薄：真实 browsing 在外部 MCP） |

---

# 5. Interaction / effect / durability

| Package | Evidence | Verdict |
|---|---|---|
| `interaction-authority` | type: `Domain/PromptAuthority.fs`、`Domain/PromptAuthorityRun.fs`、`Application/Prompting/PromptIngress.fs`；fact: `Journal/PromptFactFold.fs`、`PromptAuthorityLedger.fs`；tests: `tests/unit/prompt/authority.test.mjs` | REAL |
| `dispatch-protocol` | wiring: `Application/Prompting/{PromptDispatcher,PromptDispatcherSend}.fs`；fact: `Journal/PromptAuthorityLedger.fs`；failure: `Application/Reconciliation/PromptRecovery.fs`（Pending→PhysicalAccepted，at-most-one）；tests: dispatch/fire-and-forget | REAL |
| `effect-accounting` | fact: `Journal/PromptFactFold.fs`（Requested/Accepted）、`Journal/OrchestratorFactFold.fs`、`Domain/MagicTodoFacts.fs`（TodoWritePrepared→Accepted）；failure: `Infrastructure/Git/IntegrationGate.fs`（PublishClaimed 三分支）、`Domain/EventStore.fs`（Requested/Accepted）；tests: `tests/unit/persist/**`、MagicTodo membrane | REAL |
| `durable-events` | type/fact: `Infrastructure/Persist/{CanonicalEventCodec,EventStore,EventStoreFold,GitRawStore,ProcessGitRawStore,StoreTypes}.fs`、`Journal/{Envelope,FactCodec,Fold}.fs`、`Domain/EventStore.fs`；tests: `tests/unit/persist/{event-store-append,fold,identity-collision}.test.mjs` | REAL |
| `durable-convergence` | type: `Infrastructure/Persist/EventStoreMerge.fs`（set-union/DomainConflict）；tests: `tests/unit/persist/{event-store-merge,event-store-converge}.test.mjs` | REAL |

---

# 6. Work / execution

| Package | Evidence | Verdict |
|---|---|---|
| `delegation` | type: `Kernel/SyncDelegate.fs`、`Domain/SyncDelegatePrompt.fs`、`Domain/ForkChildPayload.fs`；wiring: `Session/{SyncDelegateRuntime,SyncDelegateWorkflow,SyncDelegateWait,SyncDelegateCallStore,ForkRuntime}.fs`；resource: `resources/provider/tool/{fork,commission,inspect,sync-delegate}/`、`resources/provider/delegation/**`；tests: sync-delegate、fork | REAL |
| `process-execution` | type: `Process/{Pty,PtySession,PtyTypes,PtyBackend,PtySupervisor,ProcessRunner,NodeProcessHost,NodeProcessWait}.fs`；failure: onExit-only completion、`Process/Deadline.fs`；tests: `tests/unit/process/**`、PTY/run/signal/exit | REAL |
| `output-distillation` | resource: `resources/provider/role/distiller/`（fragment humility）；wiring: Distiller office（`Agent/AgentProgram.fs`、distill tool）；failure: `Process/LargeGate.fs`、`Domain/ToolResultBound.fs`；tests: Distiller surface | REAL |
| `change-integration` | type/wiring: `Infrastructure/Git/{IntegrationGate,GitGateway,WorktreeResource,HookDispatcher,GitOperations,GitSubject}.fs`；fact: `Journal/OrchestratorProjection.fs`、`Journal/OrchestratorFactFold.fs`；failure: PublishClaimed 三分支 CAS、`Application/Reconciliation`（restart reconcile）；tests: `tests/unit/orchestrator/**` | REAL |

---

# 7. Context continuity

| Package | Evidence | Verdict |
|---|---|---|
| `semantic-trace` | type: `Domain/XTrace.fs`；wiring: `Application/Reconciliation/XTraceCapture.fs`、`Session/CompanionProgram.fs`；fact: `Journal/XTraceProjection.fs`、`Journal/CompanionProjection.fs`；tests: XTrace append/capture/frontier | REAL |
| `work-record` | type: `Domain/LifecycleWorkRecord.fs`、`Domain/MagicTodoLwr.fs`、`Domain/SyncDelegatePrompt.fs`；wiring: `Application/Finality/LifecycleWorkRecordProjection.fs`；fact: `Journal/ManagerOpeningFloor.fs`；tests: `tests/unit/glory/lifecycle.test.mjs`、canonical LWR materializer | REAL |
| `context-compression` | type: `Domain/{PrefixCandidate,PrefixProbeSelection,BloggerDelta,BloggerRequestContext}.fs`；wiring: `Session/{Companion,CompanionHost,BloggerCoordinator,CompanionHostBlogger}.fs`；fact: `Journal/{CompanionProjection,BlogProjection}.fs`；tests: `tests/unit/context/**` | REAL |
| `prefix-stability` | type: `Domain/{XPrefixProjection,ProviderProjection}.fs`、`Domain/MagicTodoPrefixEpoch.fs`；fact: `Journal/PrefixEpochProjection.fs`、`Journal/ContextFactFold.fs`；failure: `Application/Reconciliation/XWire.fs`（isAppendOnlyPrefix）；tests: prompt-stability、`tests/unit/host/pair-thought-anchored.test.mjs` | REAL |

---

# 8. Failure / recovery

| Package | Evidence | Verdict |
|---|---|---|
| `provider-attempt-recovery` | type: `Domain/AgentPairCursor.fs`、`Domain/RecoverySlot.fs`；wiring: `Application/Recovery/{FallbackEvidence,FallbackLedger,ProviderRecoveryWorkflow}.fs`；fact: `Journal/{FallbackProjection,FallbackFactFold}.fs`；tests: `tests/unit/fallback/cursor.test.mjs` | REAL |
| `crash-reconciliation` | wiring: `Application/Reconciliation/{SessionRecoveryWorkflow,ChildRecoveryWorkflow,PromptRecovery,BloggerCrashRecovery,BloggerRecoveryProbe}.fs`；fact: `Journal/RecoveryClosureProjection.fs`、`Domain/SessionRecovery.fs`、`Domain/ChildRecovery.fs`；tests: `tests/unit/reconciliation/**` | REAL |
| `degeneration-guard` | type: `Session/LoopDetector.fs`（4-gram + 指数核、固定内存）；wiring: `Session/CompletionMailbox.fs`、LoopKill→`Application/Recovery/ProviderRecoveryWorkflow.fs` 桥接；tests: `tests/unit/` loop detector | REAL |

---

# 9. Mission / judgement / finality

| Package | Evidence | Verdict |
|---|---|---|
| `obligation-ledger` | type: `Domain/{MagicTodo,MagicTodoFacts,MagicTodoAdmission,MagicTodoAfter,MagicTodoObligationCodec}.fs`；wiring: `Application/Reconciliation/MagicTodoMembrane.fs`、`MagicTodoLocality.fs`；fact: `Journal/{MagicTodoProjection,MagicTodoFactCodec}.fs`；tests: magic-todo、`tests/unit/reconciliation/magic-todo-membrane.test.mjs` | REAL |
| `review-judgement` | type: `Application/Review/{VerdictWorkflow,ReviewerEvidence}.fs`；resource: `resources/provider/role/reviewer/`、`resources/provider/library/reviewer/`、`resources/provider/review/challenge/`；tests: reviewer verdict | REAL |
| `review-assurance` | type: `Domain/{ReviewWitness,ReviewChallenge}.fs`；wiring: `Application/Review/{ReviewBarrierWorkflow,ReviewerContinuation}.fs`、`Application/Reconciliation/ReviewSeal.fs`；fact: `Journal/{ReviewBarrier,ReviewProjection,ReviewFactFold}.fs`、`Journal/FinalityReviewCohort.fs`；tests: `tests/unit/review/**`、seal/witness | REAL |
| `finality` | wiring: `Application/Finality/{FinalityWorkflow,CohortWorkflow,BlessingWorkflow,RevisionWorkflow,RecordWorkflow}.fs`、`Application/Manager/ManagerFinality.fs`；type: `Domain/{FinalityPrompt,MagicTodoFinalityCohort}.fs`；tests: `tests/unit/glory/**`（lifecycle/rewrite-consistency/opening-floor） | REAL |

---

# 10. Feedback

| Package | Evidence | Verdict |
|---|---|---|
| `behavior-diagnosis` | type: `Domain/{EnforcerCatalog,EnforcerCodec,EnforcerCycle,RulebookObservation}.fs`；resource: `resources/enforcer/**`（124 tip 目录 = Diagnosis 检测边界）；wiring: `Session/{EnforcerHost,EnforcerCycleCommit,EnforcerFrameRecovery}.fs`；tests: `tests/unit/enforcer/**` | REAL |
| `guidance-delivery` | type: `Journal/{TipDeliveryProjection,GuidelineProjection,ObservationProjection}.fs`；wiring: `Session/{EnforcerTipGuidance,EnforcerRepair,EnforcerContinuation}.fs`、`Domain/EnforcerCycle.fs`（Full/Identity）；failure: `scripts/checks/enforcer-cross-family-collision.mjs`；tests: `tests/unit/enforcer/**` | REAL |

---

# 11. Repository knowledge / programming

| Package | Evidence | Verdict |
|---|---|---|
| `repository-investigation` | resource: `resources/provider/tool/{inspect,query-shell}/`、`resources/provider/role/inspector/`；wiring: `Agent/AgentProgram.fs`（inspect）、`Infrastructure/RepositoryWarmStart.fs`；failure: Semble 低信任 hint 不冒充 fact（`Kernel/SembleMcp.fs`、`Infrastructure/SembleMcpStdio.fs`）；tests: inspect/query-shell、warm-start | REAL |
| `knowledge-reuse` | type: `Domain/Casebook.fs`；wiring: `Infrastructure/{CasebookCapture,CasebookIndex,CasebookLifecycle,CasebookReplay,CasebookSessionDraft,CasebookWorkflow,CasebookBookkeeper,BookkeeperStaging,BookkeeperRuntime}.fs`；fact: `Infrastructure/CasebookStore.fs` + EventStore（InspectorCase*）；tests: `tests/unit/casebook/**`（15 文件） | REAL |
| `repository-programming` | type: `Domain/{JsCapability,JsSurface,JsDescription,JsFailure,JsAnchor,JsTransaction}.fs`；wiring: `Infrastructure/{JsToolsBindings,JsAnchorFs,JsToolsTransactionStore,JsGlobFs,JsMutationFs,JsUtf8Fs}.fs`、`Process/JsSandbox.fs`；failure: `scripts/checks/js-surface-gate.mjs`；tests: `tests/unit/js-tools/**`（9 文件） | REAL |

---

# 12. Optimization / epistemics

| Package | Evidence | Verdict |
|---|---|---|
| `speculative-investigation` | type: `Domain/{StrengthBudget,StrengthCostModel,StrengthEvents,StrengthFrame,StrengthPolicy,StrengthPredictor,StrengthPromotion,StrengthProjection,StrengthRollout,StrengthCommit,StrengthBatchCollector}.fs`；wiring: `Application/Strength/**`、`Session/StrengthRuntime.fs`；fact: `Infrastructure/Persist/{StrengthDurability,StrengthStore}.fs`；tests: `tests/unit/strength/**`（16 文件） | REAL |
| `epistemic-reasoning` | type: `Sphinx/{Types,State,Search,Bayes,MonteCarlo,Value,Policy,Closure,Methodology,Representation,Absorb}.fs`；host: `Sphinx/{McpServer,Codec,WireEncode,DecodePrimitives}.fs`；tests: `tests/unit/sphinx/**`（9 文件） | REAL |

---

# 13. Delivery

| Package | Evidence | Verdict |
|---|---|---|
| `distribution` | type: `Infrastructure/Resources/{PackageResources,RuntimeResources}.fs`（fixed-relative-path lookup）；wiring: `package.json` `files=[dist/,resources/]`；tests: `tests/unit/resources/**`、package integration（contents/install/import/resources）、`npm pack --dry-run` | REAL |

---

# Phase C 结论

- **43 REAL + 2 META + 0 THIN + 0 FANTASY**：每个 package 的 WHAT 都能在 source/runtime 中定位到 canonical type、wiring、durable fact、resource 或 failure path；无「文档幻想包」。
- **两个 meta 包正确无 runtime 源码**：`requirement-system`/`verification-system` 是治理元契约，证据 = `AGENTS.md` + `scripts/checks/**` + CI，本就不该有业务 runtime（HANDOFF §24 的「无 supra-package 产品事实」不适用于包系统自身的元治理）。
- **最薄 runtime 点（非幻想）**：`external-investigation` 的真实 browsing 在外部 `stealth-browser-mcp`，Wanxiangshu 只注入服务器 + 按角色锁 + Browser Role Law 固化 provenance contract；其 WHAT 由 role 资源 + semantic anchors + `capability-enforcement` role-lock 证明，不靠 browser 后端本身。
- **每个 package 的 proof 归属**仍以 `PROOF-MAP.md` 为准（Phase D 目标）；本文件只证明「WHAT 有实现对应物」，不重新划分 proof owner。

## 本轮 delta

```text
Boundary:  UNCHANGED 45（无拆/并/增/删；证据已回写各包 CURRENT EVIDENCE 无需改）
Coverage:  0 新 ORPHAN / 0 新 OVERLAP（source 面未发现 Phase A 漏判的命题）
Proof:     0 新 owner 变化（Phase D 再按 test/gate 逐条投影）
Dependency: 0 新增/删除 hard edge
```

## 待 Phase D 关注

1. `external-investigation` 目前 proof 主要靠 semantic anchors + role-lock，缺少真实 browser 的 provenance canary（测试默认 disabled）；Phase D 需标「MECHANISM shared / 需补 browser provenance oracle」。
2. `verification-system` 的 proof ladder 是否真被 `scripts/check.mjs` 分层执行，Phase D 逐 gate 验「可红」。
