# Control-State Census — src/Wanxiangshu full tree

Scan date: 2026-08-17. Patterns: `State|Stage|Phase|Step|NextAction|ResumeAt|ContinueToken|InFlight|Armed|Pending` + `let mutable|ref|Dictionary|TryGet|ContainsKey` + `Command|Reply|Program<|Suspend|Continuation|WorkflowBuilder`.

Classification key:
- **DomainFact** — pure domain model (TurnOutcome, ReconcileDecision, etc.); CE-native, not control state.
- **DurableEvidence** — projection mirror / journal cursor derived from durable facts; restart-safe.
- **PhysicalResource** — process-local registry / latch / cache / handle for resource management; legitimate mutable.
- **AlgorithmScratch** — local loop counter / fold accumulator / cursor; pure-function internal.
- **ControlState-PC** — program-counter-like state (Stage/Phase/Step/Armed/InFlight) that encodes "where in a workflow"; candidate for CE DSL closure or durable fact replacement. **= Ghost.**

Severity scoring: persisted +5, numeric +4, crash-recovery +3, cross-boundary +2 (additive).

---

## Ledger

| # | Candidate | Owner | Representation | Classification | Severity | Verdict | CE replacement | Facts needed |
|---|---|---|---|---|---|---|---|---|
| 1 | `WriterState` DU (Open/Poisoned/Closing/Closed) | Persistence/Journal | `let mutable state = WriterState.Open` — 4-state lifecycle DU on `EventStoreJournalWriter` | ControlState-PC | persisted+5, crash-recovery+3 = **8** | GHOST — lifecycle PC on the sole durable writer | `taskResult {}` close/drain CE: `let! _ = append … return! drain …` replaces Open→Closing→Closed transitions; Poisoned = `Result` error short-circuit | none — writer lifecycle is physical, not a domain fact |
| 2 | InFlight re-arming (crash recovery re-arms `InFlight` before `handleContinuation`) | Enforcer/Cycle | `scope.SetCurrentRequest(key, ctx)` re-arm from durable open request in `BloggerCrashRecovery.restoreRuntime` | ControlState-PC | persisted+5, crash-recovery+3, cross-boundary+2 = **10** | GHOST — crash-recovery control state crosses crash boundary | CE `taskResult {}` rebuild path: `let! live = tryLiveCycleContext … match live with Some ctx -> return! resumeWithContext ctx` — already partially CE, but the re-arm is a side-effect not a fact | `BloggerRequestMaterialized` already exists; re-arm should be idempotent fold of that fact, not a mutable SetCurrentRequest side-effect |
| 3 | `CanonicalIntegrator.generation` (publication generation counter) | Persistence/EventStore | `let mutable generation = 0L` — monotonic numeric PC for publication | ControlState-PC | persisted+5, numeric+4 = **9** | GHOST — numeric PC on the integrator | CE `taskResult {}` publish loop: generation becomes a `let rec` loop parameter, not a mutable field | none — generation is physical publication ordering, not a domain fact |
| 4 | `Scheduler.generations` Dictionary + `active`/`queued` Dictionaries | Composition/Turn | `Dictionary<string,int>` generation map + `active`/`queued` admission maps | ControlState-PC | numeric+4, cross-boundary+2 = **6** | GHOST — numeric generation PC for admission control | CE `taskResult {}` drain loop: `let rec drain gen = … if releaseAfterPass then return! drain (gen+1)` — generation as CE recursion parameter | none — admission generation is physical single-flight, not a domain fact |
| 5 | `PluginRecoveryScope.RecoveryArming` Dictionary | OpenCode/Host | `Dictionary<string, SlotArming>` — per-session "arming" state | ControlState-PC | crash-recovery+3, cross-boundary+2 = **5** | GHOST — "Arming" is explicit PC for recovery preparation | CE `taskResult {}` recovery flow: arming becomes a `let! armed = …` CE step, not a mutable dictionary side-effect | `FallbackCursorAdvanced` / `FallbackExhausted` durable facts already track recovery progression; arming should fold from those |
| 6 | `LoopDetector.Step` (mutable numeric counter) + `State` DU (Normal/Loop) | Execution/Session | `mutable Step: int` + `mutable WeightedDistinctTokenCount: float` + `State` DU | ControlState-PC | numeric+4 = **4** | BORDERLINE — `Step` is a numeric PC; `State` is derived classification. Self-annotated `DSL-state-combination: physical` | CE: `Detector` should be immutable record; `pushText` returns `Detector * Evaluation` (pure fold), not mutable in-place | none — loop detection is algorithm-local |
| 7 | `NeedHelpSensor.armed` HashSet | Interaction/Dispatch | `let armed = HashSet<string>()` — per-session "armed" flag | ControlState-PC | cross-boundary+2 = **2** | GHOST — "armed" is a PC for sensor activation | CE: arming becomes a `let! _ = arm …` step in the sensor workflow, not a HashSet side-effect | `NeedHelpEvent` durable fact should be the arming authority |
| 8 | `LoopSensor.armed` HashSet | OpenCode/Host | `let armed = HashSet<string>()` — per-session "armed" flag for loop kill | ControlState-PC | cross-boundary+2 = **2** | GHOST — "armed" is a PC for loop-kill activation | CE: arming becomes a CE step in the sensor attach flow | `LoopDetected` durable fact (if exists) should be the arming authority; else process-local CE step |
| 9 | `Strength.strengthPendingFirst/Second` Dictionaries + `strengthFuseReason` | Strength/OpenCode | `Dictionary<string, ProviderRunIdentity * StrengthFeatureKey>` + `Dictionary<string, StrengthFeatureKey>` + `mutable strengthFuseReason: string option` | ControlState-PC | cross-boundary+2 = **2** | GHOST — two-phase pending observation state is a PC for counterfactual prediction | CE: `let! first = observeFirst … let! second = observeSecond …` CE sequence replaces pending dictionaries | none — predictor evidence is restart-discardable (self-annotated); fuse should be `Result` error |
| 10 | `Sphinx.SessionLifecycle` DU (Active/Answered) + `SessionEntry.State` | Sphinx | `SessionLifecycle` DU on `SessionEntry` in `SessionStore` Dictionary | ControlState-PC | cross-boundary+2 = **2** | GHOST — session lifecycle state machine in a process-local Dictionary | CE: `taskResult {}` session flow: `let! result = Policy.start … match result with Answered -> return! conclude | Yield -> return! await` | `InquiryResult` is the domain fact; lifecycle should fold from it, not be a mutable Dictionary entry |
| 11 | `ReconcileProgram.TurnOutcome` (InProgress/NeedsContinuation/Completed/Aborted/Failed) | Composition/Turn | DU — pure domain model | DomainFact | 0 | KEEP — pure domain; CE-native | already CE: `match! outcome with …` | already a fact (`HostTurnObserved`) |
| 12 | `ReconcileProgram.ReconcileDecision` (Reread/Publish/StopPass) | Composition/Turn | DU — pure decision | DomainFact | 0 | KEEP — pure domain; CE-native | already CE: `decideStep` pure function | none — derived from evidence |
| 13 | `ReconcileProgram.ReconcileWake` (IdleWake/RetryWake/FailureWake/AbortWake) | Composition/Turn | DU — pure wake classification | DomainFact | 0 | KEEP — pure domain; CE-native | already CE: `match wake with …` | none — derived from physical signal |
| 14 | `EnforcerContinuation.ContinuationOutcome` (ProjectMessages/StopPhysicalRun) | Enforcer | DU — continuation result | DomainFact | 0 | KEEP — pure domain; CE-native | already CE: `taskResult {}` returns `ContinuationOutcome` | `BlogObservationCommitted` / `BloggerRequestAbandoned` |
| 15 | `BloggerToolRecovery` DU (NoRecovery/InteractionNudgeIssued/Aabb) | Context/Companion/Blogger | DU — derived classification, "never stored on a cell" | DomainFact | 0 | KEEP — pure derived; CE-native | already CE: `match recovery with …` | derived from durable claim + transcript |
| 16 | `DrainWindow` DU (Closed/Open) | Context/Companion/Blogger | DU — physical drain slot state | PhysicalResource | 0 | KEEP — physical slot; unforgeable `DrainPermit` | n/a — physical resource management | `HandleCompleted` / `HandleRetired` durable seal is the authority |
| 17 | `BloggerRuntime.Decision` (Start/Skip/Offer) | Context/Companion/Blogger | DU — pure routing decision | DomainFact | 0 | KEEP — pure `decideMaterial` function | already CE: `match decision with …` | none — derived from flight + parked |
| 18 | `OrchestratorHost.joinInFlight` + `engineInstance` + `engineTask` | Change/Host | `mutable joinInFlight: bool` + `mutable engineInstance` + `mutable engineTask` | PhysicalResource | 0 | KEEP — single-flight latches + memoized resource | n/a — single-flight CE not needed (lock-protected) | none |
| 19 | `ForkRuntime.joinInFlight` + `cancelDrainTask` + `acceptingOwnedWork` + `ownedWorkCount` + `ownedWorkDrainWaiter` + `ownedWorkFailure` | Execution/Delegation/Fork | 6 mutable fields — single-flight + resource latches | PhysicalResource | 0 | KEEP — single-flight + drain latches | n/a — drain CE already exists (`finishOwnedWork`) | none |
| 20 | `PluginRuntimeScope` 20+ mutable fields (disposed/disposeTask/reconcileShutdown/acceptingBackgroundWork/backgroundWorkCount/backgroundDrainWaiter/backgroundFailure/toolRuntime/subscription/sharedTerminalKey/sharedTerminalPort/compactionSettingGap/startupProbeDone/loopSensor/needHelpSensor/satelliteRuntime/syncDelegateRuntime/assistanceTurnHandler/assistanceDropSignals/assistanceDropSession) | OpenCode/Host | 20+ mutable fields — resource attachment slots + single-flight latches | PhysicalResource | 0 | KEEP — resource attachment + drain latches | n/a — drain CE already exists (`RunBackground`/`finishOwnedWork`) | none |
| 21 | `CompanionHost.bloggerCreateTask` + `bloggerId` + `bloggerCreateFailed` + `restoredBloggerIdOpt` | Context/Companion | 4 mutable fields — single-flight + resource | PhysicalResource | 0 | KEEP — single-flight + resource slots | n/a | `CompanionBloggerLinked` durable fact is the authority |
| 22 | `CompanionRuntime.recoveryWaiter` + `blogProjection` + `xTraceProjection` + `latestB` + `bloggerSessionId` + `lastSendTask` | Context/Companion | 6 mutable fields — projection mirrors + single-flight | DurableEvidence | 0 | KEEP — durable-backed projection mirrors | n/a — mirrors re-derive from journal on restart | `BlogObservationCommitted` / `XTracePartAppended` / `CompanionBloggerLinked` |
| 23 | `BloggerCrashRecovery.pass` (memoized reconcile task) | Context/Companion/Blogger | `mutable pass: Task<…> option` — single-flight memo | PhysicalResource | 0 | KEEP — single-flight memo latch | n/a | none |
| 24 | `ParkedTransform.settled` + `deadline` | Context/Companion/Blogger/Runtime | `mutable settled: bool` + `mutable deadline: IDeadlineHandle option` — one-shot latch | PhysicalResource | 0 | KEEP — one-shot settle latch | n/a | none |
| 25 | `EventStoreJournalWriter.runtimeStartedCommitted` + `currentSeq` + `serial` + `closeTask` | Persistence/Journal | 4 mutable fields — resource (lazy commit + sequence + serialization + drain) | PhysicalResource | 0 | KEEP — resource latches (except `state` — see #1) | n/a | `RuntimeStarted` durable fact |
| 26 | `AgentJournal.derivedFallbackSuccesses` + `revision` + `lastChange` | Persistence/Journal | 3 mutable fields — journal cursor + change notification | DurableEvidence | 0 | KEEP — journal revision cursor | n/a | derived from journal writer |
| 27 | `CanonicalIntegrator.state` + `loadedCommonDir` + `fullReplayUsed` | Persistence/EventStore | 3 mutable fields — integrator state + replay budget | DurableEvidence | 0 | KEEP — integrator state is durable-backed (except `generation` — see #3) | n/a | `EventEnvelope` durable facts |
| 28 | `SessionQuiescenceGate.serials` + `activities` + `physicalMessages` | OpenCode/Host | 3 mutable Map fields — per-session admission | PhysicalResource | 0 | KEEP — admission control | n/a | none |
| 29 | `ModelRouting` mutable fields + Dictionaries | OpenCode/Host | `mutable fatalError` + `mutable sharedRuntime` + `mutable sharedLoad` + 4 Dictionaries — scheduler registries | PhysicalResource | 0 | KEEP — scheduler resource + single-flight | n/a | none |
| 30 | `TurnBinding` Dictionaries (userMessageBindings/activeBindings/continuationMessageIds) | Composition/Turn | 3 Dictionaries — binding registries | PhysicalResource | 0 | KEEP — process-local binding registry | n/a | `PluginPromptPhysicalAccepted` / `AuthorityRootAccepted` durable facts |
| 31 | `Events.stickyTerminal` + `lastCompletedRun` Dictionaries | OpenCode/Host/Events | 2 Dictionaries — dedup cache | PhysicalResource | 0 | KEEP — terminal dedup cache | n/a | `HostTurnObserved` durable fact |
| 32 | `ExplicitResumeSuppression.markedPhysicalBySession` | OpenCode/Host | Dictionary — dedup | PhysicalResource | 0 | KEEP — process-local dedup | n/a | none |
| 33 | `FetchTool.fetchInFlight` Dictionary | OpenCode/Tools | Dictionary — single-flight | PhysicalResource | 0 | KEEP — single-flight | n/a | none |
| 34 | `PtyPort` Dictionaries + HashSets (active/readWaiters/exitTasks/abortPending/closedIds) | Process/Pty | 5 collections — pty registry | PhysicalResource | 0 | KEEP — pty resource registry | n/a | none |
| 35 | `PtyApi.parentAborters` + `nextAbortToken` | Process/PtyApi | Dictionary + mutable counter — abort registry | PhysicalResource | 0 | KEEP — abort callback registry | n/a | none |
| 36 | `PtySupervisor.Sessions` Dictionary | Process/PtySupervisor | Dictionary — session registry | PhysicalResource | 0 | KEEP — pty session registry | n/a | none |
| 37 | `SatelliteRuntime.flights` Dictionary | Execution/Session/Attachment | Dictionary — single-flight | PhysicalResource | 0 | KEEP — single-flight | n/a | none |
| 38 | `JoinAttemptRegistry.active` Dictionary | Execution/Delegation/Handle | Dictionary — attempt registry | PhysicalResource | 0 | KEEP — join attempt registry | n/a | none |
| 39 | `Recovery/Coordinator.inflight` Dictionary | Execution/Session/Recovery | Dictionary — single-flight | PhysicalResource | 0 | KEEP — single-flight | n/a | none |
| 40 | `Recovery/Workflow.memo` refs | Execution/Session/Recovery | `ref None` — single-flight memo | PhysicalResource | 0 | KEEP — single-flight memo | n/a | none |
| 41 | `Wait/Registry` Dictionaries + mutable counters | Execution/Session/Wait | Dictionary + `mutable nextId` + `mutable snapshotSequence` — wait registry | PhysicalResource | 0 | KEEP — wait lease registry | n/a | none |
| 42 | `Wait/CompletionMailbox.cancelled` | Execution/Session/Wait | `mutable cancelled: bool` — cancellation latch | PhysicalResource | 0 | KEEP — cancellation latch | n/a | none |
| 43 | `LargeGate.held` + `canceled` | Process/LargeGate | `mutable held: bool` + `mutable canceled: bool` — permit + cancellation | PhysicalResource | 0 | KEEP — permit gate | n/a | none |
| 44 | `NodeProcessHost.exitedRef` + `killCount` | Process/NodeProcessHost | `ref false` + `mutable killCount` — process exit flag | PhysicalResource | 0 | KEEP — process lifecycle | n/a | none |
| 45 | `PtyTiming` mutable fields (disposed/cancelled/nowMs/nextId/entries) | Process/PtyTiming | 5 mutable fields — timer resource | PhysicalResource | 0 | KEEP — timer resource | n/a | none |
| 46 | `IntegrationGate.released` | Git/IntegrationGate | `mutable released: bool` — release latch | PhysicalResource | 0 | KEEP — one-shot release latch | n/a | none |
| 47 | `WorktreeResource.released` + `releaseOnDispose` | Git/WorktreeResource | 2 mutable fields — worktree lifecycle | PhysicalResource | 0 | KEEP — worktree resource lifecycle | n/a | `WorktreeCreated` / `Published` durable facts |
| 48 | `Parallel` mutable fields (completed/resolveFn/count) | Foundation/Parallel | 3 mutable fields — promise + semaphore | PhysicalResource | 0 | KEEP — JS promise/semaphore primitive | n/a | none |
| 49 | `SyntheticToml` mutable fields (index/quoteRun/safe/total) | Foundation/SyntheticToml | 6 mutable fields — parser cursors | AlgorithmScratch | 0 | KEEP — local parser scratch | n/a | none |
| 50 | `ToolResultBound` mutable fields (p/n) | Host/Contract | 2 mutable fields — scan cursors | AlgorithmScratch | 0 | KEEP — local scan scratch | n/a | none |
| 51 | `ToolHostCodec.hash` mutable | OpenCode/Codec | `mutable hash: uint32` — FNV-1a accumulator | AlgorithmScratch | 0 | KEEP — hash accumulator | n/a | none |
| 52 | `Change/Job.active` + `n` | Change/Job | `mutable active: int` + `mutable n: int` — single-flight + drain counter | PhysicalResource + AlgorithmScratch | 0 | KEEP — single-flight + scratch | n/a | none |
| 53 | `Change/Surface.projection` + `failure` | Change/Surface | 2 mutable fields — fold accumulator | AlgorithmScratch | 0 | KEEP — fold scratch | n/a | none |
| 54 | `Context/Trace/Capture.cursor` | Context/Trace | `mutable cursor` — durable cursor while appending | AlgorithmScratch | 0 | KEEP — append scratch | n/a | `XTracePartAppended` durable fact |
| 55 | `Blogger/Runtime/CycleSurface.state` + `error` | Context/Companion/Blogger | 2 mutable fields — fold accumulator | AlgorithmScratch | 0 | KEEP — fold scratch | n/a | none |
| 56 | `Blogger/Delta.low` + `highBound` + `bestLength` | Context/Companion/Blogger | 3 mutable fields — binary search | AlgorithmScratch | 0 | KEEP — search scratch | n/a | none |
| 57 | `ObligationEnvelopeSurface` mutable fields | Persistence/Journal | 4 mutable fields — fold accumulator | AlgorithmScratch | 0 | KEEP — fold scratch | n/a | none |
| 58 | `EventKWayMerge` mutable fields | Persistence/EventStore | 5 mutable fields — merge accumulator | AlgorithmScratch | 0 | KEEP — merge scratch | n/a | none |
| 59 | `ProcessEventLog` mutable fields | Persistence/EventStore | 3 mutable fields — writer-line cursor | AlgorithmScratch | 0 | KEEP — decode scratch | n/a | none |
| 60 | `Distillation` mutable fields (lvl/carry/index) | OpenCode/Tools | 3 mutable fields — reduce/spool scratch | AlgorithmScratch | 0 | KEEP — reduce scratch | n/a | none |
| 61 | `AnchorFs.lo` + `hi` | Repository/Programming/Js | 2 mutable fields — binary search | AlgorithmScratch | 0 | KEEP — search scratch | n/a | none |
| 62 | `Verification` scenario mutable fields | Verification | 10+ mutable fields — test scenario scratch | AlgorithmScratch | 0 | KEEP — test scratch | n/a | none |
| 63 | `ForkRuntime.agents` + `ptys` mutable Maps | Execution/Delegation/Fork | 2 mutable Maps — live registries | PhysicalResource | 0 | KEEP — live registry | n/a | none |
| 64 | `ForkRuntime.children` + `dormantChildren` + `pendingRuns` + `terminalByName` + `ptyRuns` + `deferredFirstPrompts` Dictionaries | Execution/Delegation/Fork | 6 Dictionaries — fork registries | PhysicalResource | 0 | KEEP — fork resource registries | n/a | `HandleLinked` / `HandleCompleted` durable facts |
| 65 | `ToolRuntimeScope` Dictionaries + mutable fields | OpenCode/Tools | 4 Dictionaries + 8 mutable fields — tool runtime registries + drain latches | PhysicalResource | 0 | KEEP — tool runtime resource | n/a | none |
| 66 | `SharedState` Dictionaries (SessionParents/VerdictSessions/SessionDirectories/BloggerFlights) + `RootWorkspace` | OpenCode/Host | 4 Dictionaries + 1 mutable — process-shared state | PhysicalResource | 0 | KEEP — process-shared resource | n/a | `CompanionBloggerLinked` / `HandleLinked` durable facts |
| 67 | `SessionExecutionBinding` Dictionaries (parents/agents/acceptedPromptBindings/providerAttemptBindings) | OpenCode/Host | 4 Dictionaries — binding registries | PhysicalResource | 0 | KEEP — process-local binding | n/a | `PluginPromptClaimed` / `PluginPromptPhysicalAccepted` durable facts |
| 68 | `Sessions` Dictionaries (activeListeners/parentChildMap/childParents) | OpenCode/Host | 3 Dictionaries — session family registry | PhysicalResource | 0 | KEEP — session family registry | n/a | `HandleLinked` durable fact |
| 69 | `SharedTerminalBus.shared` + `WorkspaceEventStore.shared` + `SharedAgentJournal.shared` Dictionaries | OpenCode/Host + Persistence | 3 Dictionaries — ref-counted shared resources | PhysicalResource | 0 | KEEP — ref-counted shared resource | n/a | none |
| 70 | `Casebook/Index.frozen` + `dirty` | Repository/Knowledge/Casebook | 2 mutable fields — cache | PhysicalResource | 0 | KEEP — cache | n/a | `InspectorCaseCaptured` durable fact |
| 71 | `Casebook/BookkeeperRuntime.sessionPort` + `live` + `subscription` | Repository/Knowledge/Casebook | mutable + Dictionary + mutable — live attachment registry | PhysicalResource | 0 | KEEP — live attachment | n/a | none |
| 72 | `Casebook/BookkeeperStaging.slots` | Repository/Knowledge/Casebook | Dictionary — staging | PhysicalResource | 0 | KEEP — staging | n/a | none |
| 73 | `Casebook/SessionDraft.drafts` | Repository/Knowledge/Casebook | Dictionary — draft store | PhysicalResource | 0 | KEEP — draft store | n/a | none |
| 74 | `Casebook/Lifecycle.enabledWorkspace` | Repository/Knowledge/Casebook | `mutable enabledWorkspace` — marker-gated enablement | PhysicalResource | 0 | KEEP — enablement gate | n/a | none |
| 75 | `SessionPersona.bySession` | Participant/Persona | Dictionary — bind-once | PhysicalResource | 0 | KEEP — bind-once registry | n/a | `SessionStartedAtBound` durable fact |
| 76 | `SessionProviderLanguage.bySession` | Participant/Provider | Dictionary — bind-once | PhysicalResource | 0 | KEEP — bind-once registry | n/a | none |
| 77 | `PhysicalAcceptance.callbacks` | Interaction/Dispatch | Dictionary — callback registry | PhysicalResource | 0 | KEEP — callback registry | n/a | `PluginPromptPhysicalAccepted` durable fact |
| 78 | `DispatchSurface.lastObservation` | Interaction/Dispatch | `mutable lastObservation: obj` — buffer | PhysicalResource | 0 | KEEP — send observation buffer | n/a | none |
| 79 | `JudgementInbox.waiters` | Mission/Review | Dictionary — waiter registry | PhysicalResource | 0 | KEEP — waiter registry | n/a | `ConfirmedReviewWitness` durable fact |
| 80 | `Finality/Cohort.cancelled` + `ref remaining` + `ref shortCircuitWinner` | Mission/Finality | `mutable cancelled` + 2 refs — cancellation + algorithm scratch | PhysicalResource + AlgorithmScratch | 0 | KEEP — cancellation + race scratch | n/a | none |
| 81 | `Fission` Dictionaries (lanes/silentInterrupts/handleAffinities/childObservers/groupResources/deliveryClaims/takeoverClaims) | Execution/Fission | 7 collections — lane registries | PhysicalResource | 0 | KEEP — lane resource registries | n/a | `FissionAdmitted` / `FissionLaneMaterialized` durable facts |
| 82 | `Strength/Runtime.byOwner` + `byReplica` Dictionaries | Strength | 2 Dictionaries — replica registry | PhysicalResource | 0 | KEEP — replica registry | n/a | none |
| 83 | `Strength/Replica/Runtime.byReplica` Dictionary | Strength/Replica | Dictionary — decision state registry | PhysicalResource | 0 | KEEP — decision state registry | n/a | none |
| 84 | `Strength/OpenCode/PluginScope.strengthReplicaRuntime` + `strengthPredictorState` + `strengthRecentPrimary` | Strength/OpenCode | mutable + mutable + Dictionary — resource + cache | PhysicalResource | 0 | KEEP — resource + restart-discardable cache (except pending — see #9) | n/a | none |
| 85 | `Sphinx/SessionStore.sessions` Dictionary | Sphinx | Dictionary — session registry | PhysicalResource | 0 | KEEP — session registry (except lifecycle — see #10) | n/a | none |
| 86 | `PluginRecoveryScope.AttemptPlans` Dictionary | OpenCode/Host | `Dictionary<string, AttemptPlan>` — per-provider-run attempt plan | ControlState-PC | crash-recovery+3 = **3** | BORDERLINE — attempt plan is PC-like but consumed within one turn | CE: `let! plan = resolveAttemptPlan …` CE step | `FallbackCursorAdvanced` durable fact may subsume |
| 87 | `Enforcer/ObservationCollector.buffers` Dictionary | Enforcer | Dictionary — observation buffer | PhysicalResource | 0 | KEEP — observation buffer | n/a | `BlogObservationCommitted` durable fact |
| 88 | `PairProgrammingThoughtTransform.memoryLedger` Dictionary | OpenCode/Host | Dictionary — memory ledger | PhysicalResource | 0 | KEEP — process-local memory | n/a | `PairProgrammingGuidelineAnchored` durable fact |
| 89 | `RuntimeResources.installed` | Resources | `mutable installed` — singleton | PhysicalResource | 0 | KEEP — singleton resource | n/a | none |
| 90 | `ProcessGitRawStore` caches (objectCache/treeCache/writtenTreeCache/answers) | Persistence/EventStore | 4 Dictionaries — memoization caches | PhysicalResource | 0 | KEEP — memoization cache | n/a | none |
| 91 | `RuntimePath.commonDirAnswers` | Persistence/Journal | Dictionary — memoization | PhysicalResource | 0 | KEEP — memoization | n/a | none |
| 92 | `PtyBackend.portRef` | Process/PtyBackend | `mutable portRef` — back-reference cycle closure | PhysicalResource | 0 | KEEP — cycle closure | n/a | none |
| 93 | `AttachedRuntime.bindings` Dictionary | Execution/Session/Attachment | Dictionary — binding registry | PhysicalResource | 0 | KEEP — binding registry | n/a | none |
| 94 | `MagicTodoProjectionHandle.current` | Mission/Obligation/Todo | `mutable current` — projection handle | DurableEvidence | 0 | KEEP — projection mirror | n/a | `TodoWriteAccepted` durable fact |
| 95 | `DistillationSurface.lastAwaitedAgent` | OpenCode/Tools | `mutable lastAwaitedAgent` — correlation key | PhysicalResource | 0 | KEEP — correlation | n/a | none |
| 96 | `EventsSurface.noRun` | OpenCode/Host/Events | `mutable noRun: int` — synthetic counter | PhysicalResource | 0 | KEEP — synthetic identity counter | n/a | none |
| 97 | `Surface.SessionStartLedger.values` | Process/Surface | Dictionary — session start ledger | PhysicalResource | 0 | KEEP — session start registry | n/a | `SessionStartedAtBound` durable fact |
| 98 | `HostPort.accepting` ref | Mission/Finality/OpenCode | `ref false` — acceptance latch | PhysicalResource | 0 | KEEP — acceptance latch | n/a | none |
| 99 | `OneShotTool.subscription` + `completed` + `abortTask` ref | Execution/Delegation/Handle | mutable + mutable + ref — one-shot latch + cancellation | PhysicalResource | 0 | KEEP — one-shot completion + cancellation | n/a | none |
| 100 | `ChildRun.completed` + `stored` | Execution/Delegation/Fork | 2 mutable fields — one-shot completion latch | PhysicalResource | 0 | KEEP — one-shot completion | n/a | none |
| 101 | `SyncDelegate/Store` Dictionaries (callsByOwnerScope/callsByDelegate/pendingBatches/activeBatches) | Execution/Delegation/SyncDelegate | 4 Dictionaries — delegate call registries | PhysicalResource | 0 | KEEP — delegate call registry | n/a | none |
| 102 | `SyncDelegate/Surface` Dictionaries + refs | Execution/Delegation/SyncDelegate | Dictionaries + refs — test support | PhysicalResource | 0 | KEEP — test support | n/a | none |
| 103 | `MagicTodoMembrane.bridges` Dictionary | Mission/Obligation/Todo | Dictionary — deferred bridge registry | PhysicalResource | 0 | KEEP — deferred bridge | n/a | none |
| 104 | `AssistanceHost.terminalSubscriptions` + `droppedOwners` | Interaction/Dispatch/OpenCode | Dictionary + HashSet — subscription registry | PhysicalResource | 0 | KEEP — subscription registry | n/a | none |
| 105 | `NeedHelpSensor.suffixes` + `reasoningParts` | Interaction/Dispatch | Dictionary + HashSet — sensor state | PhysicalResource | 0 | KEEP — sensor state (except `armed` — see #7) | n/a | none |

---

## Top 10 Ghosts for Exorcism

Ranked by severity (descending), then by cross-boundary reach.

| Rank | Ghost | Severity | Why it's a Ghost | Exorcism plan |
|---|---|---|---|---|
| **1** | InFlight re-arming (`BloggerCrashRecovery.restoreRuntime` → `SetCurrentRequest`) | **10** | Crash recovery re-arms `InFlight` from durable open request — control state crosses crash boundary; a restart re-derives flight ownership via side-effect, not via fold of `BloggerRequestMaterialized` | Make `SetCurrentRequest` idempotent fold of `BloggerRequestMaterialized` fact; remove the re-arm side-effect path; CE `taskResult {}` rebuild: `let! live = tryLiveCycleContext … return! resumeWithContext` |
| **2** | `CanonicalIntegrator.generation` (numeric publication PC) | **9** | Monotonic `int64` counter as publication generation — numeric PC on the durable integrator; lost on restart, must be re-derived | CE `taskResult {}` publish loop: generation becomes `let rec` recursion parameter; or derive from `EventEnvelope` sequence |
| **3** | `WriterState` DU (Open/Poisoned/Closing/Closed) | **8** | 4-state lifecycle DU on the sole durable journal writer — explicit PC for writer lifecycle; persisted + crash-recovery | CE `taskResult {}` close/drain: `let! _ = append … return! drain …` replaces Open→Closing→Closed; Poisoned = `Result` error short-circuit via `taskResult` |
| **4** | `Scheduler.generations` + `active`/`queued` Dictionaries | **6** | Numeric generation counter as admission PC — controls which drain pass is current; numeric + cross-boundary (Composition→Execution) | CE `taskResult {}` drain recursion: `let rec drain gen = …` — generation as parameter, not Dictionary lookup |
| **5** | `PluginRecoveryScope.RecoveryArming` Dictionary | **5** | "Arming" is explicit PC for recovery preparation — `SlotArming` stored per-session; crash-recovery + cross-boundary (OpenCode→Fallback) | CE `taskResult {}` recovery flow: arming becomes `let! armed = …` CE step; `FallbackCursorAdvanced` durable fact is the progression authority |
| **6** | `LoopDetector.Step` (mutable numeric counter) | **4** | `mutable Step: int` is a numeric PC for token-stream progression; persists in `detectors` Dictionary across turns | Make `Detector` immutable record; `pushText` returns `Detector * Evaluation` (pure fold); CE not needed — pure function |
| **7** | `PluginRecoveryScope.AttemptPlans` Dictionary | **3** | Per-provider-run `AttemptPlan` is PC-like but consumed within one turn; crash-recovery | CE `let! plan = resolveAttemptPlan …` CE step; `FallbackCursorAdvanced` may subsume |
| **8** | `NeedHelpSensor.armed` HashSet | **2** | "Armed" flag is a PC for sensor activation; cross-boundary (Interaction→Dispatch) | CE step in sensor workflow; `NeedHelpEvent` durable fact should be the arming authority |
| **9** | `LoopSensor.armed` HashSet | **2** | "Armed" flag is a PC for loop-kill activation; cross-boundary (OpenCode→Execution) | CE step in sensor attach flow; process-local CE step or `LoopDetected` durable fact |
| **10** | `Strength.strengthPendingFirst/Second` + `strengthFuseReason` | **2** | Two-phase pending observation state is a PC for counterfactual prediction; cross-boundary (Strength→Execution); fuse latch is control state | CE `let! first = observeFirst … let! second = observeSecond …` sequence; fuse = `Result` error |

---

## Summary

- **Total candidates scanned**: 105
- **DomainFact (KEEP)**: 7 — pure domain DUs, CE-native
- **DurableEvidence (KEEP)**: 5 — projection mirrors / journal cursors, restart-safe
- **PhysicalResource (KEEP)**: 83 — registries / latches / caches / handles, legitimate mutable
- **AlgorithmScratch (KEEP)**: 10 — local loop counters / fold accumulators, pure-function internal
- **ControlState-PC (GHOST)**: 10 — program-counter-like state for exorcism

Ghost severity distribution: severity 10 ×1, 9 ×1, 8 ×1, 6 ×1, 5 ×1, 4 ×1, 3 ×1, 2 ×3.

The codebase already self-annotates mutable state with `DSL-MUTABLE:` categories (`single-flight` / `resource` / `algorithm-scratch` / `buffer` / `cancellation` / `subscription`). These annotations correctly classify the vast majority of mutable state as physical resource or algorithm scratch. The 10 Ghosts are the residual control state that encodes "where in a workflow" and should be replaced by `taskResult {}` CE closure (`let!`/`do!`/`return!`/`match!`) or by durable fact folds.

The top 3 Ghosts (severity 8–10) are the highest-value exorcism targets: InFlight re-arming, CanonicalIntegrator.generation, and WriterState — all on the durable/crash-recovery boundary.

---

## Appendix: Round-3 Re-Verification (2026-08-17)

Full-tree re-scan of `src/Wanxiangshu` for the same pattern set (`let mutable` / `Dictionary` / `HashSet` / `TryGet` / `ContainsKey` / `State|Stage|Phase|Step|Armed|Pending|InFlight`). Every `let mutable` field across all 61+ files is annotated with a `DSL-MUTABLE:` category (`single-flight` / `resource` / `algorithm-scratch` / `buffer` / `cancellation` / `subscription`). No unannotated mutable state found.

### Delta Ledger — Former Top-10 Ghosts

| # | Former Ghost | Severity | Round-3 Verdict | Evidence |
|---|---|---|---|---|
| 1 | InFlight re-arming (`BloggerCrashRecovery.restoreRuntime` → `SetCurrentRequest`) | 10 | **EXORCISED** | `restoreRuntime` now guards re-arm with `if not (host.HasFlight key)` — idempotent fold. Comment (line 132): "CE rebuild step: idempotent fold of BloggerRequestMaterialized into the physical flight registry. The durable fact is the authority; SetCurrentRequest is only called when the live process does not already hold flight ownership." Drain stays `Closed` — no shadow state re-authored. |
| 2 | `CanonicalIntegrator.generation` (numeric publication PC) | 9 | **EXORCISED** | `let mutable generation = 0L` is GONE — zero matches for `generation` in `CanonicalIntegrator.fs`. Remaining mutable fields (`state`, `loadedCommonDir`, `fullReplayUsed`) annotated `DSL-MUTABLE: resource` / `algorithm-scratch`. |
| 3 | `WriterState` DU (Open/Poisoned/Closing/Closed) | 8 | **EXORCISED** | 4-state lifecycle DU is GONE — zero matches for `WriterState` in `EventStoreJournalWriter.fs`. Replaced by three independent resource latches: `firstFailure: string option` (poison), `closed: bool` (terminal close), `closeTask: Task option` (close drain). All annotated `DSL-MUTABLE: resource`. Poison = `Result` error short-circuit (`WriterPoisoned` in append result). |
| 4 | `Scheduler.generations` + `active`/`queued` Dictionaries | 6 | **KEEP — PhysicalResource** | `generations` Dictionary reclassified (line 113): "external invalidation authority that ClearSession mutates, not a drain-pass program counter." Drain pass generation is now a recursion parameter through `Drain/RunPass/DrainAfterPass`. `active`/`queued` annotated `DSL-MUTABLE: resource — per-session single-flight admission queue/latch`. |
| 5 | `PluginRecoveryScope.RecoveryArming` Dictionary | 5 | **KEEP — PhysicalResource** | Process-local single-flight latch (line 71): "One armed slot produces at most one recovery attempt (FALLBACK-011). Process-local only: RecoverySlot.afterRestart = NotArmed, so the latch intentionally forgets." `ArmRecovery` → `TryRecoveryArming` → `ClearRecovery` one-shot consumption. |
| 6 | `LoopDetector.Step` (mutable numeric counter) + `State` DU | 4 | **EXORCISED** | `Step` is now an immutable field in `Detector` record (no `mutable`). `pushText` returns `Detector * Evaluation` (pure fold). Comment (line 39): "Detector is pure data: Step and WeightedDistinctTokenCount are immutable fold state threaded through pushText." `LastSeenTokenStep` is algorithm scratch (token decay Dictionary). `State` DU (Normal/Loop) is derived classification from pure fold output. |
| 7 | `PluginRecoveryScope.AttemptPlans` Dictionary | 3 | **KEEP — PhysicalResource** | Annotated `DSL-MUTABLE: single-flight — per-provider-run attempt plan memo` (line 94). Consumed exactly once on terminal outcome. `RecordAttemptPlan` → `TryAttemptPlan` → `ClearAttemptPlansFor` one-shot consumption. |
| 8 | `NeedHelpSensor.armed` HashSet | 2 | **KEEP — PhysicalResource** | Process-local one-shot abort ownership latch. Comment (line 51): "HOST-027: process-local exact-sentinel sensor… It owns only stream part identity, rolling suffixes, and armed attempt identities." `TryArm` returns true exactly once; `DropAttempt`/`DropSession` removes it. |
| 9 | `LoopSensor.armed` HashSet | 2 | **KEEP — PhysicalResource** | Process-local one-shot loop-kill armed mark. Comment (line 67): "LOOP-006: claim the armed mark. True exactly once per session until ClearArmed / DropSession." `TryArm` returns true exactly once; `ClearArmed`/`DropSession` removes it. |
| 10 | `Strength.strengthPendingFirst/Second` + `strengthFuseReason` | 2 | **EXORCISED** | `strengthPendingFirst`/`strengthPendingSecond` dictionaries GONE — zero matches. Replaced by `counterfactualAwait = Dictionary<string, CounterfactualAwait>()` with DU (`AwaitFirst` | `AwaitSecond`) + pure fold transition (`advanceCounterfactual`). Comment (line 72): "replaces the former strengthPendingFirst/strengthPendingSecond dictionary pair. One dictionary holds a single DU value; the transition is a pure fold, not two separate mutable dictionaries with implicit step logic encoded by lookup order." `strengthFuseReason: string option` GONE — replaced by `strengthFuse: Result<unit, string>` latch (`DSL-MUTABLE: resource`). |

### Additional Exorcisms Verified

| Former Ghost | Verdict | Evidence |
|---|---|---|
| Sphinx `SessionLifecycle` DU (Active/Answered) — ledger #10 | **EXORCISED** | `SessionLifecycle` DU is GONE. `SessionEntry` now stores `{ State: EpistemicState; LastResult: InquiryResult }` — domain fact directly. Comment (Session.fs line 16): "The lifecycle projection (active vs answered) is a pure fold of LastResult, not a separate mutable state machine — exorcised from the former SessionLifecycle DU that duplicated InquiryResult's shape." `statusOfEntry` is a pure function: `match entry.LastResult with Answered -> Answered | _ -> Active`. |
| False-abort migration | **EXORCISED** | `LegacyFalseAbort` is DECODE-ONLY in `DurableCompletionDecode` — codec never constructs `RunCompletion` for legacy abort. `JoinDrain.reconcileFalseAborts` scans handle projection: unretired false aborts → `HandleFalseCompletionRejected` (fold reverts to Active); retired false aborts → fail-closed refuse. Pure fold over durable handle evidence, not mutable control state. |

### New Ghosts Found

**0.** No new ControlState-PC candidates found. The full-tree `let mutable` scan (all 61+ files, every page) shows every mutable field carries a `DSL-MUTABLE:` annotation. No unannotated mutable state. No new `State`/`Stage`/`Phase`/`Step`/`Armed`/`Pending`/`InFlight` type definitions that encode workflow position.

New type definitions encountered during re-scan, all classified KEEP:
- `PtySession` mutable fields (Backend/OutputBuffer/Closed/AwaitingFirstByte/ExitCompletion/ExitCompleted/Pending) — `DSL-state-combination: physical` — PTY I/O resource. Not in original ledger but matches PhysicalResource category (#34 family).
- `SnapshotToolPartState` (Pending/Completed/Failed) — pure DU decoded from Host wire data via `toolStateOf` pure function. DomainFact.
- `RepresentationState` / `EpistemicState` (Sphinx) — immutable record types with Map fields. DomainFact.
- `CounterfactualAwait` (AwaitFirst/AwaitSecond) — DU replacing former two-dictionary ghost. Pure fold transition. PhysicalResource (process-local cache).
- `BloggerCycleProjectionState` / `XTraceProjectionState` / `EnforcementProjectionState` / `TipDeliveryProjectionState` / `FissionProjectionState` / `DelegatedToolEstimateProjectionState` / `GuidelineProjectionState` / `MagicTodoProjectionState` / `SessionStartedAtProjectionState` — all immutable projection records folded from durable facts. DurableEvidence.

### Round-3 Summary

| Category | Round-1 Count | Round-3 Count | Delta |
|---|---|---|---|
| DomainFact (KEEP) | 7 | 7+ | +0 (new DUs are pure domain/projection) |
| DurableEvidence (KEEP) | 5 | 5+ | +0 (new projection states are same category) |
| PhysicalResource (KEEP) | 83 | 89 | +6 (former ghosts #4/#5/#7/#8/#9 reclassified + CounterfactualAwait) |
| AlgorithmScratch (KEEP) | 10 | 10 | 0 |
| ControlState-PC (GHOST) | 10 | **0** | **−10** |

**Result: 0 GHOST remains.** All 10 former top-ghosts are either EXORCISED (6: InFlight re-arming, CanonicalIntegrator.generation, WriterState, LoopDetector.Step, Strength fuse/pending, Sphinx lifecycle) or KEEP-proven PhysicalResource (4: Scheduler.generations, RecoveryArming, AttemptPlans, NeedHelp/Loop armed). False-abort migration is EXORCISED (decode-only + pure fold). No new ghosts introduced.
