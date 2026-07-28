# P1 Structural Refactor — File Changes

## 1. Fallback (P1.1)

### Added
- `next/Domain/AgentPairCursor.fs` — pure AABB fallback cursor (`ModelSide`, `FallbackCursor`, `AuthorityAgentPair`, `FallbackAttemptIdentity`); the only place these types live.
- `tests-next/Domain/AgentPairCursorTests.fs` — 12-round AABBAABB cycle and `effectiveAgent` mapping.

### Deleted
- `next/Session/Fallback.fs`
- `next/OpenCode/EffectiveAgentResolver.fs`
- `next/OpenCode/HostSignalBootstrapTimers.fs` (only used by old `PluginFallbackRetry`)
- `tests-next/Session/FallbackContractTests.fs`

### Modified
- `next/Wanxiangshu.Next.fsproj`
  - Added `Domain/AgentPairCursor.fs` after `Kernel/Roles.fs`.
  - Removed `Session/Fallback.fs`, `OpenCode/EffectiveAgentResolver.fs`, `OpenCode/HostSignalBootstrapTimers.fs`.
- `tests-next/Wanxiangshu.Next.Tests.fsproj`
  - Added `Domain/AgentPairCursorTests.fs`.
  - Removed `Session/FallbackContractTests.fs`.
- `next/Journal/AgentFacts.Types.fs` — removed duplicate `ModelSide` and `module FallbackProjection`; kept the `FallbackProjection` record.
- `next/Journal/AgentFacts.Fallback.fs` — uses `AgentPairCursor.advance` and `AgentPairCursor.failureIdentity`.
- `next/Session/DurableFallback.fs` — returns `AgentPairCursor.FallbackCursor`; removed `FallbackDecision`, `FallbackMemory`, `isDead`.
- `next/OpenCode/PromptAuthority.fs` — `agentPair`/`effectiveAgentAt` are thin wrappers over `AgentPairCursor`; added `effectiveAgentFromManaged`.
- `next/OpenCode/FallbackDetect.fs` — `recordFallbackFailure` returns `AgentPairCursor.FallbackCursor`.
- `next/OpenCode/RetrySignalHandler.fs` — only durable `FallbackFailureRecorded` writer; uses `AgentPairCursor` identity and `PromptAuthority.stableLogicalRunId`.
- `next/OpenCode/PluginFallbackRetry.fs` — reduced to pending-continuation send/accept handling; it never writes the cursor and uses no timer.
- `next/OpenCode/ProviderErrorFallback.fs` — added causal non-retryable provider-error handling: arm on the first idle, reconcile/send on the settled idle, and keep `RetrySignalHandler` as the only durable cursor writer.
- `next/OpenCode/TerminalPolicyHelpers.fs` — `sessionDead` now only checks `j.IsPoisoned`.
- `next/OpenCode/TerminalPolicies.fs` — `TurnFailed` branch no longer advances fallback from terminal classification; it `NotifyTerminal Failed`.
- `next/OpenCode/HostSignalBootstrap.fs` — delegates host-stopped retry continuation to `ProviderErrorFallback`; raw terminal/idle classification still cannot advance the cursor.
- `tests-next/Session/DurableFallbackTests.fs` — rewritten for `AgentPairCursor.FallbackCursor`.
- `tests-next/Journal/AgentFactsTests.fs` — uses `AgentPairCursor.SideA`/`SideB` and `AgentPairCursor.side`.
- `tests-next/OpenCode/ManagedAgentTests.fs` — uses `AgentPairCursor` and `PromptAuthority.effectiveAgentFromManaged`.
- `tests-next/MockOpenCode/FallbackModelSelectionTests.fs` — uses `AgentPairCursor`.
- `tests-next/MockOpenCode/FallbackIntegration.fs` — uses `AgentPairCursor`; removed `isDead` check.

## 2. Prompt Authority (P1.2)

### Added
- `next/Domain/PromptAuthority.fs` — pure types (`RootAuthorityKind`, `ContinuationKind`, `PromptOrigin`, `AuthorityExecutionProfile`, `AttemptExecutionProfile`, `PromptClaim`, `PromptAuthorityProjection`) and pure operations (`empty`, `originLabel`, `parseAgentName`, `stableLogicalRunId`, `createAuthorityRoot`, `registerAuthority`, `claimAgentOwnerRoot`, `claimContinuation`, `resolveKnownOrigin`, etc.); no Fable/Host/Journal dependencies.
- `next/Journal/PromptAuthorityLedger.fs` — durable `PromptAuthorityProjection` fold and queries; owns `empty`, `foldAuthorityRootAccepted`, `foldPluginPromptClaimed`, `foldPluginPromptAccepted`, `foldPluginPromptAbandoned`, `foldInteractionRepairClaimed`.
- `next/OpenCode/PromptIngress.fs` — encapsulates `chat.message` handling; classifies `HumanRoot`, `AgentOwnerRoot`, `Continuation`, `UnknownOrigin`.
- `tests-next/Domain/AgentPairCursorTests.fs` (already noted in P1.1) and test updates to `Journal/JournalTestSupport.fs`, `OpenCode/HostEventRouterTerminalTests.fs`, `OpenCode/ManagedAgentTests.fs`.

### Deleted
- `next/OpenCode/ChatMessageOrigin.fs`
- `next/OpenCode/MessageOriginDecoder.fs`
- `next/OpenCode/PromptAuthorityAccept.fs`
- `next/OpenCode/PromptAuthorityRestore.fs`
- `next/OpenCode/PromptAuthoritySend.fs`
- `next/OpenCode/PromptAuthorityService.fs`
- `next/OpenCode/HostSignalChatMessage.fs` (logic moved into `PromptIngress`)

### Modified
- `next/Wanxiangshu.Next.fsproj`
  - Added `Domain/PromptAuthority.fs` after `AgentPairCursor.fs`.
  - Added `Journal/PromptAuthorityLedger.fs` after `AgentFacts.FoldHelpers.fs`.
  - Added `OpenCode/PromptIngress.fs` and kept `OpenCode/PromptDispatcher.fs`; removed the deleted PromptAuthority modules.
- `tests-next/Wanxiangshu.Next.Tests.fsproj`
  - Removed `OpenCode/PromptAuthorityTests.fs`, `OpenCode/PromptAuthoritySendTests.fs`, `OpenCode/PromptAuthorityChatMessageTests.fs`, and `Journal/PromptAuthorityFactTests.fs` (these fixtures tested deleted module surfaces and need fresh tests).
- `next/Journal/AgentFacts.Authority.fs` — delegates PromptAuthority folds to `PromptAuthorityLedger`.
- `next/Journal/AgentFacts.Types.fs` — removed `PromptAuthorityProjection` and related types (now in `Journal/PromptAuthorityLedger`).
- `next/Journal/AgentFacts.fs` — `foldPluginPromptAbandoned` payload now includes `Reason`.
- `next/OpenCode/PromptAuthority.fs` — public facade for `Domain.PromptAuthority`; exposes `sha256Hex` and case convenience values.
- `next/OpenCode/PromptDispatcher.fs` — single point for calling host `prompt_async`; uses the new `Domain.PromptAuthority` operations and `PromptAuthorityLedger` for reads.
- `next/OpenCode/HostSessionNudge.fs` — uses `PromptAuthority.createAuthorityRoot` with `PromptAuthority.sha256Hex`.
- `next/OpenCode/HostReviewGuard.fs` — uses `PromptAuthority.ContinuationKind` cases directly.
- `next/Session/HostForkBusyNudge.fs` — uses `PromptAuthority.ContinuationKind.BusyAgentNudge`.
- `tests-next/Journal/JournalTestSupport.fs` — updated `AgentTier` references to `Wanxiangshu.Next.Kernel.AgentTier`; uses `PromptAuthority.roleLabel`/`tierLabel`.
- `tests-next/OpenCode/ManagedAgentTests.fs` — updated to `PromptAuthority.effectiveAgentFromManaged`.

## 3. Host Event / Reconcile (P1.3)

### Added
- `next/OpenCode/HostEventCodec.fs` — only module that unwraps raw `obj` `session.status` events; produces typed `HostSignal`.
- `next/OpenCode/TurnBinding.fs` — in-memory root/physical/continuation binding store plus durable projection recovery.
- `next/OpenCode/TurnReconcile.fs` — pure `snapshot + binding -> ReconciledTurn option`; resolves send-admission placeholders to the real root/continuation physical user message in the SDK snapshot.
- `tests-next/OpenCode/TurnReconcileAdmissionTests.fs` — covers admission-root and admission-continuation physical-message reconciliation.
- `next/OpenCode/ReconcileSupervisor.fs` — per-session single-flight reconcile supervisor; `dirty` latch + `inFlight: Map<SessionId, Task>`; at most 3 causal yields.
- `next/OpenCode/TurnCompletionProgram.fs` — single continuous `apply` program that sequences `TerminalPolicies` logic and side effects.

### Deleted
- `next/OpenCode/HostSignalChatMessage.fs` (logic moved to `PromptIngress` and `HostSignalBootstrap`)
- `next/OpenCode/MessageOriginDecoder.fs` (P1.2)
- `next/OpenCode/HostSignalBootstrapTimers.fs` (P1.1)

### Modified
- `next/Wanxiangshu.Next.fsproj`
  - Added `OpenCode/HostEventCodec.fs`, `OpenCode/TurnBinding.fs`, `OpenCode/TurnReconcile.fs`, `OpenCode/ReconcileSupervisor.fs`, `OpenCode/TurnCompletionProgram.fs` in compile order before `HostSignalBootstrap`.
- `next/OpenCode/SessionReconciler.fs` — rewritten as a backward-compatible wrapper over `ReconcileSupervisor` + `TurnBinding`; keeps `MarkDirty` and `HandleSignal`.
- `next/OpenCode/HostSignalBootstrap.fs` — uses `HostEventCodec`, `ReconcileSupervisor`, `TurnBinding`, `TurnCompletionProgram`; provider retry is the only durable fallback writer.
- `next/OpenCode/HostSignalAdapter.fs` and `next/OpenCode/HostSignalSubscribe.fs` — use `HostEventCodec` for decoding.
- `next/OpenCode/CompletedTurnClassifier.fs` and `next/OpenCode/TerminalSessionA.fs` — kept as pure helpers.
- `next/OpenCode/TerminalPolicies.fs` and `next/OpenCode/TerminalPolicyHelpers.fs` — terminal decisions feed into `TurnCompletionProgram`.
- `next/OpenCode/RetrySignalHandler.fs` — uses `PromptAuthority.stableLogicalRunId` with `PromptAuthority.sha256Hex`.
- `next/OpenCode/HostReviewGuard.fs` — uses `PromptAuthority.ContinuationKind` cases.
- `next/OpenCode/HostSessionNudge.fs` — uses `PromptAuthority.createAuthorityRoot` with `PromptAuthority.sha256Hex`.
- `next/OpenCode/VerdictSurface.fs`, `next/OpenCode/CoderTool.fs`, `next/OpenCode/InspectorTool.fs`, `next/OpenCode/ManagedAgent.fs`, `next/Session/HostForkAgentOwner.fs`, `next/Session/CompanionHostBlogger.fs` — minor reference updates to `PromptAuthority`/`AgentPairCursor` types.

## 4. Process lifecycle consolidation

### Added
- `next/Process/ProcessRequest.fs` — stable command, estimate, context, outcome and error protocol.
- `next/Process/ProcessOutput.fs` — bounded stdout/stderr collection and spool threshold transition.
- `next/Process/NodeProcessHost.fs` — the single Node child-process and spool-file interop boundary.
- `next/Process/ProcessRunner.fs` — one readable Flow program for large-gate acquisition, spawn, deadline/kill, output construction and release.
- `next/Kernel/AsyncSupport.fs` — the minimal Fable-compatible `TaskCompletionSource` completion helpers used by physical resource adapters.

### Deleted / replaced
- Deleted the horizontal slices `ProcessTypes.fs`, `Command.fs`, `ProcessBudget.fs`, `Pump.fs`, `RunnerCore.fs`, and `RunnerPrimitives.fs`; `Runner.fs` is now a compatibility facade over `ProcessRunner`.
- `LargeGate.fs` and `Spool.fs` retain domain behavior while delegating JS host operations to `NodeProcessHost`.
- Removed obsolete narrow process tests; the existing `ProcessBoundedTests` and release canaries cover deadline, cancellation, spooling, chunking and runtime behavior.

## Build / Test Notes
- `npm run build` and `npm run test:compile` are green (150 production sources, 70 test sources).
- `npm run test:next`: 269 passed, 0 failed.
- `npm run test:manager-tools`: 1 passed, 0 failed.
- `node testkit/opencode/tests/gate-testkit.mjs`: 29 passed, 0 failed.
- `npm run test:e2e:p0:three`: all 18 canaries passed across all 3 required rounds.
- `npm run test:release`: complete release gate passed.
