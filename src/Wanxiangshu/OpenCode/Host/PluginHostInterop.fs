namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Context.Companion
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open CompanionProjection

module PluginHostInterop =

    [<RequireQualifiedAccess>]
    type internal HookSettlementEvidence =
        | NoOwnedExecution
        | ExactSettlementComplete
        | DurableOutcomeUnknown
        | SettlementIncomplete

    [<RequireQualifiedAccess>]
    type internal HookFailurePolicy =
        | RethrowUnchanged
        | FatalAfterSettlement
        | RejectFatalBeforeSettlement

    type internal HookFailureOutcome =
        { Failure: ExecutionFailure
          Lifecycle: DurableExecutionLifecycle
          ExecutionKey: ChatExecutionKey option
          Settlement: HookSettlementEvidence }

    [<Emit("import('@opencode-ai/plugin/tool')")>]
    let importToolModule () : Task<obj> = jsNative

    [<Emit("$0 instanceof Error ? String($0.message) : String($0)")>]
    let private diagnosticErrorText (error: obj) : string = jsNative

    let private emitFatalRecord operation error =
        Diagnostic.fatal operation [ "result", diagnosticErrorText error ]

    /// Host hook whose F# value stayed CURRIED after compilation.
    /// Keep this arity adaptation as a direct Emit call at the registration site:
    /// moving it behind an ordinary F# helper changes how Fable boxes the original
    /// function and silently turns paired hooks into curried no-ops.
    [<Emit("(args, context) => $0(args)(context)")>]
    let curriedHook (fn: obj) : obj = jsNative

    /// Host hook that Fable emitted as a two-arity arrow.
    ///
    /// Passing that arrow through an `obj` boundary can make Fable substitute a
    /// `curry2(fn)` adapter for `$0`. Calling that adapter with two JS arguments
    /// returns its second-stage function without executing the hook body. Accept
    /// both runtime shapes here: invoke the supplied callable positionally, then
    /// finish the curried second stage when Fable inserted one.
    [<Emit("(args, context) => { const result = $0(args, context); return typeof result === 'function' ? result(context) : result; }")>]
    let pairedHook (fn: obj) : obj = jsNative

    [<Emit("(args, _context) => $0(args)")>]
    let unaryHook (fn: obj) : obj = jsNative

    [<Emit("(_args, _context) => $0()")>]
    let nullaryHook (fn: obj) : obj = jsNative

    let private releaseCompleted =
        function
        | ChatAdmissionReleaseOutcome.Settled CapacityTransitionOutcome.Applied
        | ChatAdmissionReleaseOutcome.Settled CapacityTransitionOutcome.AlreadyApplied ->
            HookSettlementEvidence.ExactSettlementComplete
        | ChatAdmissionReleaseOutcome.Settled CapacityTransitionOutcome.StaleFence
        | ChatAdmissionReleaseOutcome.Settled CapacityTransitionOutcome.Conflict
        | ChatAdmissionReleaseOutcome.BoundaryFailed _ -> HookSettlementEvidence.SettlementIncomplete

    let private persistenceOutcome failure =
        match JournalAppendFailure.toExecutionFailure failure with
        | ExecutionFailure.PersistenceFailure PersistenceCommitment.NotCommitted as typed ->
            typed, HookSettlementEvidence.SettlementIncomplete
        | ExecutionFailure.PersistenceFailure PersistenceCommitment.Committed as typed ->
            typed, HookSettlementEvidence.SettlementIncomplete
        | ExecutionFailure.PersistenceFailure PersistenceCommitment.Unknown as typed ->
            typed, HookSettlementEvidence.SettlementIncomplete
        | ExecutionFailure.LocalInvariant
        | ExecutionFailure.ProtocolRejection
        | ExecutionFailure.AuthorizationDenied
        | ExecutionFailure.UserCancelled
        | ExecutionFailure.Superseded
        | ExecutionFailure.CapacityQueueFull
        | ExecutionFailure.ProviderTransient
        | ExecutionFailure.ProviderPermanent
        | ExecutionFailure.AcceptanceUnknown
        | ExecutionFailure.StreamInterruptedAfterFirstToken ->
            invalidOp "journal append failure produced a non-persistence execution failure"

    let private acceptanceFailure =
        function
        | ManagedChatAcceptanceError.IntentRejected _ ->
            ExecutionFailure.ProtocolRejection, HookSettlementEvidence.NoOwnedExecution
        | ManagedChatAcceptanceError.NotAttempted _ ->
            ExecutionFailure.PersistenceFailure PersistenceCommitment.NotCommitted,
            HookSettlementEvidence.NoOwnedExecution
        | ManagedChatAcceptanceError.CommitUnknown _ ->
            ExecutionFailure.AcceptanceUnknown, HookSettlementEvidence.DurableOutcomeUnknown
        | ManagedChatAcceptanceError.FactRejected _ ->
            ExecutionFailure.PersistenceFailure PersistenceCommitment.Committed, HookSettlementEvidence.NoOwnedExecution
        | ManagedChatAcceptanceError.EstablishedEvidenceConflict _ ->
            ExecutionFailure.LocalInvariant, HookSettlementEvidence.ExactSettlementComplete
        | ManagedChatAcceptanceError.AuthorityRegistrationRejected _
        | ManagedChatAcceptanceError.AttemptEvidenceInvalid _
        | ManagedChatAcceptanceError.AttemptKeyMismatch _
        | ManagedChatAcceptanceError.ProjectionMissingAfterCommit _
        | ManagedChatAcceptanceError.ProjectionConflictAfterCommit _ ->
            ExecutionFailure.LocalInvariant, HookSettlementEvidence.SettlementIncomplete

    let private settlementFailure =
        function
        | PreProviderSettlementError.PersistenceFailed failure -> persistenceOutcome failure
        | PreProviderSettlementError.MissingAccepted _
        | PreProviderSettlementError.EvidenceConflict _
        | PreProviderSettlementError.ProviderAlreadyStarted _
        | PreProviderSettlementError.TerminalConflict _
        | PreProviderSettlementError.InvalidDisposition _
        | PreProviderSettlementError.ProjectionMissingAfterCommit _
        | PreProviderSettlementError.ProjectionConflictAfterCommit _ ->
            ExecutionFailure.LocalInvariant, HookSettlementEvidence.SettlementIncomplete

    let private transactionFailure =
        function
        | ChatAdmissionTransactionError.AdmissionRejected _ ->
            ExecutionFailure.LocalInvariant, HookSettlementEvidence.NoOwnedExecution
        | ChatAdmissionTransactionError.AcceptanceFailed failure -> acceptanceFailure failure
        | ChatAdmissionTransactionError.AcceptanceBoundaryFailed _ ->
            ExecutionFailure.AcceptanceUnknown, HookSettlementEvidence.DurableOutcomeUnknown
        | ChatAdmissionTransactionError.PreProviderSettlementFailed failure -> settlementFailure failure
        | ChatAdmissionTransactionError.PreProviderSettlementBoundaryFailed _ ->
            ExecutionFailure.AcceptanceUnknown, HookSettlementEvidence.SettlementIncomplete
        | ChatAdmissionTransactionError.PreProviderUnbindBoundaryFailed _ ->
            ExecutionFailure.LocalInvariant, HookSettlementEvidence.SettlementIncomplete
        | ChatAdmissionTransactionError.LeaseAcquisitionFailed _ ->
            ExecutionFailure.LocalInvariant, HookSettlementEvidence.ExactSettlementComplete
        | ChatAdmissionTransactionError.LeaseTargetFailed(_, release)
        | ChatAdmissionTransactionError.LeaseTargetBoundaryFailed(_, release)
        | ChatAdmissionTransactionError.LeaseTargetProjectionFailed(_, release)
        | ChatAdmissionTransactionError.BindingFailed(_, release)
        | ChatAdmissionTransactionError.HostProjectionFailed(_, release)
        | ChatAdmissionTransactionError.LeaseCommitFailed(_, release)
        | ChatAdmissionTransactionError.LeaseCommitBoundaryFailed(_, release) ->
            ExecutionFailure.LocalInvariant, releaseCompleted release

    let private stoppedTransactionFailure =
        function
        | ChatAdmissionTransactionOutcome.Superseded _ ->
            ExecutionFailure.Superseded,
            DurableExecutionLifecycle.Terminal,
            HookSettlementEvidence.ExactSettlementComplete
        | ChatAdmissionTransactionOutcome.Cancelled _ ->
            ExecutionFailure.UserCancelled,
            DurableExecutionLifecycle.Terminal,
            HookSettlementEvidence.ExactSettlementComplete
        | ChatAdmissionTransactionOutcome.CapacityQueueFull _ ->
            ExecutionFailure.CapacityQueueFull,
            DurableExecutionLifecycle.Terminal,
            HookSettlementEvidence.ExactSettlementComplete
        | ChatAdmissionTransactionOutcome.AlreadyStarted _ ->
            ExecutionFailure.Superseded, DurableExecutionLifecycle.Terminal, HookSettlementEvidence.NoOwnedExecution
        | ChatAdmissionTransactionOutcome.AlreadyTerminal _ ->
            ExecutionFailure.Superseded, DurableExecutionLifecycle.Terminal, HookSettlementEvidence.NoOwnedExecution
        | ChatAdmissionTransactionOutcome.Settled _ ->
            invalidOp "settled chat admission cannot cross the failure membrane"

    let private lifecycleAfterFailedTransaction =
        function
        | HookSettlementEvidence.ExactSettlementComplete -> DurableExecutionLifecycle.Terminal
        | HookSettlementEvidence.NoOwnedExecution -> DurableExecutionLifecycle.NoAcceptedFact
        | HookSettlementEvidence.DurableOutcomeUnknown
        | HookSettlementEvidence.SettlementIncomplete -> DurableExecutionLifecycle.AcceptedBeforeProvider

    let private managedFailure =
        function
        | HostSignalBootstrap.ChatAdmissionHookFailure.IntentRejected _ ->
            ExecutionFailure.ProtocolRejection,
            DurableExecutionLifecycle.NoAcceptedFact,
            HookSettlementEvidence.NoOwnedExecution
        | HostSignalBootstrap.ChatAdmissionHookFailure.TransactionStopped outcome -> stoppedTransactionFailure outcome
        | HostSignalBootstrap.ChatAdmissionHookFailure.TransactionFailed failure ->
            let typed, settlement = transactionFailure failure
            let lifecycle = lifecycleAfterFailedTransaction settlement
            typed, lifecycle, settlement

    let private placeholderKey =
        { SessionId = SessionId.create "host-hook-without-managed-execution"
          PhysicalUserMessageId = PhysicalUserMessageId.create "host-hook-without-managed-execution" }

    let private policyDecision (outcome: HookFailureOutcome) =
        ExecutionFailurePolicy.decide
            { Failure = outcome.Failure
              Lifecycle = outcome.Lifecycle
              ExecutionKey = outcome.ExecutionKey |> Option.defaultValue placeholderKey
              Capacity = CapacityOwnership.NoCapacityFence
              Provider =
                { ProviderRun = ProviderRunIdentity.create "host-hook-before-provider"
                  LogicalRun = LogicalRunId.create "host-hook-before-provider"
                  RequestKind = ProviderRequestKind.WorkMain
                  RetryBudget = ProviderRecoveryBudget.Exhausted
                  FallbackBudget = ProviderRecoveryBudget.Exhausted
                  Breaker = ProviderBreakerState.Closed } }

    let internal interpretHookFailure outcome =
        let decision = policyDecision outcome

        match decision.Retry, decision.Fallback, decision.Breaker with
        | RetryDecision.NoRetry, FallbackDecision.NoFallback, BreakerDecision.NoBreakerTransition -> ()
        | RetryDecision.NoRetry, FallbackDecision.NoFallback, BreakerDecision.RecordProviderTransientFailure
        | RetryDecision.NoRetry, FallbackDecision.NoFallback, BreakerDecision.RecordProviderPermanentFailure
        | RetryDecision.NoRetry, FallbackDecision.AdvanceFallback _, _
        | RetryDecision.RetryFreshAttempt _, FallbackDecision.NoFallback, _
        | RetryDecision.RetryFreshAttempt _, FallbackDecision.AdvanceFallback _, _ ->
            invalidOp "hook membrane cannot own provider retry, fallback, or breaker transitions"

        let decisionStillRequiresSettlement =
            match decision.CapacitySettlement, decision.MessageDisposition with
            | CapacitySettlement.NoCapacitySettlement, MessageDisposition.KeepCurrentFact
            | CapacitySettlement.NoCapacitySettlement, MessageDisposition.AwaitAcceptanceReconciliation _
            | CapacitySettlement.RetainExactFence _, MessageDisposition.KeepCurrentFact
            | CapacitySettlement.RetainExactFence _, MessageDisposition.AwaitAcceptanceReconciliation _ -> false
            | CapacitySettlement.NoCapacitySettlement, MessageDisposition.TerminalizeAcceptedPreProvider _
            | CapacitySettlement.NoCapacitySettlement, MessageDisposition.TerminalizeProviderStarted _
            | CapacitySettlement.RetainExactFence _, MessageDisposition.TerminalizeAcceptedPreProvider _
            | CapacitySettlement.RetainExactFence _, MessageDisposition.TerminalizeProviderStarted _
            | CapacitySettlement.ReleaseExactFence _, MessageDisposition.KeepCurrentFact
            | CapacitySettlement.ReleaseExactFence _, MessageDisposition.TerminalizeAcceptedPreProvider _
            | CapacitySettlement.ReleaseExactFence _, MessageDisposition.TerminalizeProviderStarted _
            | CapacitySettlement.ReleaseExactFence _, MessageDisposition.AwaitAcceptanceReconciliation _ -> true

        match decision.Fatality, outcome.Settlement, decisionStillRequiresSettlement with
        | FatalityDecision.NoFatality, _, _ -> HookFailurePolicy.RethrowUnchanged
        | FatalityDecision.FatalAfterSettlement, _, true -> HookFailurePolicy.RejectFatalBeforeSettlement
        | FatalityDecision.FatalAfterSettlement, HookSettlementEvidence.NoOwnedExecution, false
        | FatalityDecision.FatalAfterSettlement, HookSettlementEvidence.ExactSettlementComplete, false
        | FatalityDecision.FatalAfterSettlement, HookSettlementEvidence.DurableOutcomeUnknown, false ->
            HookFailurePolicy.FatalAfterSettlement
        | FatalityDecision.FatalAfterSettlement, HookSettlementEvidence.SettlementIncomplete, false ->
            HookFailurePolicy.RejectFatalBeforeSettlement

    let internal normalizeHookFailure (error: obj) =
        match error with
        | :? MagicTodoHostCodec.ProviderInputRejection ->
            { Failure = ExecutionFailure.ProtocolRejection
              Lifecycle = DurableExecutionLifecycle.NoAcceptedFact
              ExecutionKey = None
              Settlement = HookSettlementEvidence.NoOwnedExecution }
        | :? HostSignalBootstrap.ChatAdmissionHookException as managed ->
            let failure, lifecycle, settlement = managedFailure managed.Failure

            { Failure = failure
              Lifecycle = lifecycle
              ExecutionKey = managed.ExecutionKey
              Settlement = settlement }
        | _ ->
            { Failure = ExecutionFailure.LocalInvariant
              Lifecycle = DurableExecutionLifecycle.NoAcceptedFact
              ExecutionKey = None
              Settlement = HookSettlementEvidence.NoOwnedExecution }

    let private handleHookFailure operation error =
        let outcome = normalizeHookFailure error

        match interpretHookFailure outcome with
        | HookFailurePolicy.RethrowUnchanged
        | HookFailurePolicy.RejectFatalBeforeSettlement -> ()
        | HookFailurePolicy.FatalAfterSettlement -> emitFatalRecord operation error

    [<Emit("(args, context) => { const handle = (err) => { $2($0, err); throw err; }; try { return Promise.resolve($1(args, context)).catch(handle); } catch (err) { return handle(err); } }")>]
    let private guardedPolicyAwareHook (operation: string) (fn: obj) (onError: string -> obj -> unit) : obj = jsNative

    let policyAwareHook (operation: string) (adaptedHook: obj) : obj =
        guardedPolicyAwareHook operation adaptedHook handleHookFailure

    let registeredHook (key: HookKey) (adaptedHook: obj) : string * obj =
        let metadata = HookPolicy.metadata key |> HookPolicy.validate
        metadata.HostKey, policyAwareHook metadata.DiagnosticOperation adaptedHook

    let projectionSessionIdFromMessages (output: obj) : string option =
        ProviderWireDecode.projectionSessionIdFromMessages output

    let toolHooks
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (workspaceDirectory: string option)
        (scope: PluginRuntimeScope)
        (currentPhysicalUserMessage: string -> string option)
        (onRunStarted: (SessionId -> Role -> string option -> unit) option)
        (parentWorkRecordFor: (string -> Task<string option>) option)
        (childWorkRecordFor: (string -> Task<string option>) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (eventPort: IEventObservationPort option)
        (casebookToolSpecs: ToolSpec list)
        : ToolRegistration =
        let jsTransactionPersistence =
            workspaceDirectory
            |> Option.bind (fun workspace -> WorkspaceEventStore.tryCurrent (RuntimePath.gitCommonDir workspace))
            |> Option.map JsToolsTransactionStore.createPersistence

        let quiescence = scope.Sessions.Quiescence :> ISessionQuiescenceGate

        let registration =
            ToolRegistry.create
                toolModule
                sessionPort
                journal
                workspaceDirectory
                scope.Sessions.SessionParents
                currentPhysicalUserMessage
                scope.Sessions.SessionDirectories
                onRunStarted
                parentWorkRecordFor
                childWorkRecordFor
                snapshot
                cancelSignals
                (fun sessionId -> quiescence.BeginToolExecution(SessionId.create sessionId))
                (fun sessionId -> quiescence.EndToolExecution(SessionId.create sessionId))
                eventPort
                (Some scope.BloggerRuntimeHost)
                scope.SyncDelegateRuntime
                (Some scope.Strength.StrengthRuntime)
                casebookToolSpecs
                jsTransactionPersistence

        // P0-RECOVERY-JOIN-001: JoinTool RequireFamilyRecovery → PluginRuntimeScope.
        registration.Runtime.AttachFamilyRecovery(fun root -> scope.RequireFamilyRecovery root)
        // EXEC-017: JoinTool Begin(user-message wake) shares this process-local
        // attempt-scoped registry.
        registration.Runtime.AttachJoinAttempts scope.Sessions.JoinInterrupts
        registration
