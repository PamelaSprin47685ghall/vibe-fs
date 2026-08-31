namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type internal ChatAdmissionBindingKind =
    | ExternalRoot
    | ActiveHumanContinuation
    | PendingPrompt of PromptKey

type internal ChatAdmissionBindingReceipt =
    private
        { Identity: ExecutionAdmissionExactIdentity
          Kind: ChatAdmissionBindingKind }

type internal HostModelProjectionReceipt =
    private | HostModelProjectionReceipt of ExecutionAdmissionExactIdentity * OpencodeModel

[<RequireQualifiedAccess>]
/// DSL-class: ExternalSignal
type internal ChatAdmissionTransactionStep =
    | ResolveState
    | Accept
    | AcceptedWitness
    | AcquireLease
    | LeaseTarget
    | BindExecution
    | ProjectHost
    | CommitLease
    | TerminalizeAccepted
    | UnbindExecution
    | ReleaseBeforeProvider
    | Settled

[<RequireQualifiedAccess>]
type internal ChatAdmissionTransactionOutcome =
    | Settled of
        witness: ManagedChatAcceptanceWitness *
        target: ModelRoutingTarget *
        binding: ChatAdmissionBindingReceipt *
        hostProjection: HostModelProjectionReceipt
    | Superseded of ManagedChatAcceptanceWitness
    | CapacityQueueFull of ManagedChatAcceptanceWitness
    | Cancelled of ManagedChatAcceptanceWitness
    | AlreadyStarted of ProviderStartedEvidence
    | AlreadyTerminal of ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
type internal ChatAdmissionReleaseOutcome =
    | Settled of CapacityTransitionOutcome
    | BoundaryFailed of exn

[<RequireQualifiedAccess>]
/// DSL-class: Evidence
type internal ChatAdmissionTransactionError =
    | AdmissionRejected of ChatAdmissionError
    | AcceptanceFailed of ManagedChatAcceptanceError
    | AcceptanceBoundaryFailed of exn
    | PreProviderSettlementFailed of PreProviderSettlementError
    | PreProviderSettlementBoundaryFailed of exn
    | PreProviderUnbindBoundaryFailed of exn * release: ChatAdmissionReleaseOutcome
    | LeaseAcquisitionFailed of exn
    | LeaseTargetFailed of ExecutionAdmissionRejection * release: ChatAdmissionReleaseOutcome
    | LeaseTargetBoundaryFailed of exn * release: ChatAdmissionReleaseOutcome
    | LeaseTargetProjectionFailed of exn * release: ChatAdmissionReleaseOutcome
    | BindingFailed of exn * release: ChatAdmissionReleaseOutcome
    | HostProjectionFailed of exn * release: ChatAdmissionReleaseOutcome
    | LeaseCommitFailed of commit: CapacityTransitionOutcome * release: ChatAdmissionReleaseOutcome
    | LeaseCommitBoundaryFailed of exn * release: ChatAdmissionReleaseOutcome

type internal ChatAdmissionTransactionInput =
    { Intent: ChatAdmissionIntent.Decision
      CurrentState: ChatExecutionState option }

type internal ChatAdmissionTransactionPorts =
    { Accept: ChatAdmissionIntent.Decision -> Task<Result<ManagedChatAcceptanceWitness, ManagedChatAcceptanceError>>
      Acquire: ManagedChatAcceptanceWitness -> Task<Result<ExecutionAdmissionAcquisition, exn>>
      LeaseTarget: ExecutionAdmissionLease -> Result<ModelRoutingTarget, ExecutionAdmissionRejection>
      Bind: ChatAdmissionIntent.Decision -> ManagedChatAcceptanceWitness -> OpencodeModel -> Result<unit, exn>
      ProjectHost: OpencodeModel -> Result<unit, exn>
      Commit: ExecutionAdmissionLease -> ExecutionAdmissionExactIdentity -> CapacityTransitionOutcome
      ReleaseBeforeProvider: ExecutionAdmissionLease -> CapacityTransitionOutcome
      SettlePreProvider:
          ChatExecutionKey
              -> AcceptedChatExecutionEvidence
              -> ChatExecutionTerminalDisposition
              -> Task<Result<PreProviderTerminalWitness, PreProviderSettlementError>>
      Unbind: ChatExecutionKey -> unit }

type private AdmissionResolution =
    | AdmissionRequired
    | ExistingOutcome of ChatAdmissionTransactionOutcome

type private AcceptedAdmission =
    { Input: ChatAdmissionTransactionInput
      Witness: ManagedChatAcceptanceWitness }

type private LeasedAdmission =
    { Accepted: AcceptedAdmission
      Lease: ExecutionAdmissionLease }

type private AdmissionSettlementDecision<'outcome> =
    { Evidence: AcceptedChatExecutionEvidence
      Disposition: ChatExecutionTerminalDisposition
      Outcome: Result<'outcome, ChatAdmissionTransactionError> }

[<RequireQualifiedAccess>]
type private AdmissionAcquisitionOutcome =
    | LeaseAcquired of LeasedAdmission
    | AdmissionStopped of ChatAdmissionTransactionOutcome

type private TargetedAdmission =
    { Leased: LeasedAdmission
      Target: ModelRoutingTarget
      Identity: ExecutionAdmissionExactIdentity
      Model: OpencodeModel }

type private BoundAdmission =
    { Targeted: TargetedAdmission
      Binding: ChatAdmissionBindingReceipt }

type private ProjectedAdmission =
    { Bound: BoundAdmission
      HostProjection: HostModelProjectionReceipt }

[<RequireQualifiedAccess>]
type private TargetPreparationError =
    | OwnerRejected of ExecutionAdmissionRejection
    | BoundaryFailed of exn
    | ProjectionFailed of exn

[<RequireQualifiedAccess>]
type private CommitError =
    | OwnerRejected of CapacityTransitionOutcome
    | BoundaryFailed of exn

[<RequireQualifiedAccess>]
module internal ChatAdmissionTransaction =

    let private intentKey (intent: ChatAdmissionIntent.Decision) : ChatExecutionKey =
        match intent with
        | ChatAdmissionIntent.Decision.ExternalRootIntent evidence ->
            { SessionId = evidence.Key.SessionId
              PhysicalUserMessageId = evidence.Key.PhysicalUserMessageId }
        | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent evidence ->
            { SessionId = evidence.Key.SessionId
              PhysicalUserMessageId = evidence.Key.PhysicalUserMessageId }
        | ChatAdmissionIntent.Decision.PendingPromptIntent evidence ->
            { SessionId = evidence.Key.SessionId
              PhysicalUserMessageId = evidence.Key.PhysicalUserMessageId }
        | _ -> invalidArg "intent" "managed chat transaction requires a managed intent"

    let private message (intent: ChatAdmissionIntent.Decision) : ChatAdmissionMessage =
        let key = intentKey intent

        { SessionId = key.SessionId
          PhysicalUserMessageId = key.PhysicalUserMessageId
          ExplicitAgent =
            match intent with
            | ChatAdmissionIntent.Decision.ExternalRootIntent evidence -> Some evidence.ExplicitAgent
            | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent evidence -> Some evidence.EffectiveAgent
            | ChatAdmissionIntent.Decision.PendingPromptIntent _ -> None
            | _ -> invalidArg "intent" "managed chat transaction requires a managed intent" }

    let private exactIdentity
        (witness: ManagedChatAcceptanceWitness)
        (target: ModelRoutingTarget)
        : ExecutionAdmissionExactIdentity =
        let evidence = ManagedChatAcceptanceWitness.evidence witness

        { SessionId = SessionId.value evidence.SessionId
          PhysicalUserMessageId = PhysicalUserMessageId.value evidence.PhysicalUserMessageId
          EffectiveAgent = evidence.EffectiveAgent
          Target = target }

    let private keyOfEvidence (evidence: AcceptedChatExecutionEvidence) : ChatExecutionKey =
        { SessionId = evidence.SessionId
          PhysicalUserMessageId = evidence.PhysicalUserMessageId }

    let private settleAccepted observe ports key evidence disposition =
        task {
            observe ChatAdmissionTransactionStep.TerminalizeAccepted

            try
                let! settled = ports.SettlePreProvider key evidence disposition

                return
                    settled
                    |> Result.mapError ChatAdmissionTransactionError.PreProviderSettlementFailed
            with error ->
                return Error(ChatAdmissionTransactionError.PreProviderSettlementBoundaryFailed error)
        }

    let private settleWitness observe ports witness disposition =
        let evidence = ManagedChatAcceptanceWitness.evidence witness
        settleAccepted observe ports (keyOfEvidence evidence) evidence disposition

    let private settleAdmission observe ports decision =
        taskResult {
            let! _ =
                settleAccepted observe ports (keyOfEvidence decision.Evidence) decision.Evidence decision.Disposition

            return! decision.Outcome
        }

    let private release observe ports lease =
        observe ChatAdmissionTransactionStep.ReleaseBeforeProvider

        try
            ports.ReleaseBeforeProvider lease |> ChatAdmissionReleaseOutcome.Settled
        with error ->
            ChatAdmissionReleaseOutcome.BoundaryFailed error

    let private unbind observe ports key =
        observe ChatAdmissionTransactionStep.UnbindExecution

        try
            ports.Unbind key
            None
        with error ->
            Some error

    let private compensationError createError unbindError released =
        unbindError
        |> Option.map (fun error -> ChatAdmissionTransactionError.PreProviderUnbindBoundaryFailed(error, released))
        |> Option.defaultWith (fun () -> createError released)

    let private compensate observe ports witness lease disposition createError =
        taskResult {
            let! _ = settleWitness observe ports witness disposition
            let key = ManagedChatAcceptanceWitness.evidence witness |> keyOfEvidence
            let unbindError = unbind observe ports key
            let released = release observe ports lease
            return! Error(compensationError createError unbindError released)
        }

    let private bindingReceipt intent identity =
        { Identity = identity
          Kind =
            match intent with
            | ChatAdmissionIntent.Decision.ExternalRootIntent _ -> ChatAdmissionBindingKind.ExternalRoot
            | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent _ ->
                ChatAdmissionBindingKind.ActiveHumanContinuation
            | ChatAdmissionIntent.Decision.PendingPromptIntent evidence ->
                ChatAdmissionBindingKind.PendingPrompt evidence.PromptKey
            | _ -> invalidArg "intent" "managed chat transaction requires a managed intent" }

    let private modelFromTarget target =
        try
            Ok(ModelRouting.toOpenCodeModel target)
        with error ->
            Error error

    let private accept ports profile =
        task {
            try
                let! accepted = ports.Accept profile
                return accepted |> Result.mapError ChatAdmissionTransactionError.AcceptanceFailed
            with error ->
                return Error(ChatAdmissionTransactionError.AcceptanceBoundaryFailed error)
        }

    let private acquire ports profile =
        task {
            try
                let! acquired = ports.Acquire profile
                return acquired |> Result.mapError ChatAdmissionTransactionError.LeaseAcquisitionFailed
            with error ->
                return Error(ChatAdmissionTransactionError.LeaseAcquisitionFailed error)
        }

    let private effectValue invocation =
        try
            Ok(invocation ())
        with error ->
            Error error

    let private effect invocation =
        effectValue invocation |> Result.bind id

    let private resolve
        observe
        (input: ChatAdmissionTransactionInput)
        : Task<Result<AdmissionResolution, ChatAdmissionTransactionError>> =
        observe ChatAdmissionTransactionStep.ResolveState

        match input.CurrentState with
        | Some { Lifecycle = ChatExecutionLifecycle.Terminal disposition } ->
            ExistingOutcome(ChatAdmissionTransactionOutcome.AlreadyTerminal disposition)
            |> Ok
            |> Task.FromResult
        | Some { Lifecycle = ChatExecutionLifecycle.ProviderStarted
                 ProviderStarted = Some evidence } ->
            ExistingOutcome(ChatAdmissionTransactionOutcome.AlreadyStarted evidence)
            |> Ok
            |> Task.FromResult
        | Some { Lifecycle = ChatExecutionLifecycle.ProviderStarted
                 ProviderStarted = None } -> invalidOp "ProviderStarted projection is missing exact provider evidence"
        | None
        | Some { Lifecycle = ChatExecutionLifecycle.Accepted } -> AdmissionRequired |> Ok |> Task.FromResult

    // semantic-decorator-owner: managed-chat-execution
    // semantic-decorator-WHAT: CHATEXEC-003
    // semantic-decorator-trace-relation: one Accept step before the acceptance attempt and one AcceptedWitness step only after durable acceptance; business trace unchanged
    // semantic-decorator-proof: requirements/managed-chat-execution/tests/admission-transaction.test.mjs::WHAT[CHATEXEC-003] managed admission has one fixed success order
    // semantic-decorator-failure-policy: a typed acceptance failure stops the sequence at Accept; the settlement path owns every later step
    // semantic-decorator-cancel-policy: step notification is synchronous and adds no cancellation boundary
    // semantic-decorator-deadline-policy: step notification is time-independent and adds no deadline
    // semantic-decorator-invocation-bound: 2
    let private acceptAdmission observe ports input =
        task {
            observe ChatAdmissionTransactionStep.Accept
            let! accepted = accept ports input.Intent

            match accepted with
            | Ok witness ->
                observe ChatAdmissionTransactionStep.AcceptedWitness
                return Ok { Input = input; Witness = witness }
            | Error(ChatAdmissionTransactionError.AcceptanceFailed(ManagedChatAcceptanceError.EstablishedEvidenceConflict(established,
                                                                                                                          _)) as original) ->
                let decision =
                    { Evidence = established
                      Disposition = ChatExecutionTerminalDisposition.Rejected
                      Outcome = Error original }

                return! settleAdmission observe ports decision
            | Error error -> return Error error
        }

    let private stoppedAdmissionSettlement accepted outcome =
        let evidence = ManagedChatAcceptanceWitness.evidence accepted.Witness

        match outcome with
        | ExecutionAdmissionAcquisition.QueueFull ->
            { Evidence = evidence
              Disposition = ChatExecutionTerminalDisposition.Failed
              Outcome =
                ChatAdmissionTransactionOutcome.CapacityQueueFull accepted.Witness
                |> AdmissionAcquisitionOutcome.AdmissionStopped
                |> Ok }
        | ExecutionAdmissionAcquisition.Cancelled ->
            { Evidence = evidence
              Disposition = ChatExecutionTerminalDisposition.Cancelled
              Outcome =
                ChatAdmissionTransactionOutcome.Cancelled accepted.Witness
                |> AdmissionAcquisitionOutcome.AdmissionStopped
                |> Ok }
        | ExecutionAdmissionAcquisition.Superseded ->
            { Evidence = evidence
              Disposition = ChatExecutionTerminalDisposition.Cancelled
              Outcome =
                ChatAdmissionTransactionOutcome.Superseded accepted.Witness
                |> AdmissionAcquisitionOutcome.AdmissionStopped
                |> Ok }
        | ExecutionAdmissionAcquisition.Admitted _
        | ExecutionAdmissionAcquisition.Queued _ -> invalidOp "handled acquisition outcome reached terminal settlement"

    let rec private acquisitionOutcome observe ports accepted =
        function
        | ExecutionAdmissionAcquisition.Admitted lease ->
            AdmissionAcquisitionOutcome.LeaseAcquired { Accepted = accepted; Lease = lease }
            |> Ok
            |> Task.FromResult
        | ExecutionAdmissionAcquisition.Queued node ->
            task {
                let! completed = node.Completion.Task
                return! acquisitionOutcome observe ports accepted completed
            }
        | outcome -> stoppedAdmissionSettlement accepted outcome |> settleAdmission observe ports

    let private acquireAdmission observe ports accepted =
        task {
            observe ChatAdmissionTransactionStep.AcquireLease
            let! acquired = acquire ports accepted.Witness

            match acquired with
            | Error error ->
                let decision =
                    { Evidence = ManagedChatAcceptanceWitness.evidence accepted.Witness
                      Disposition = ChatExecutionTerminalDisposition.Failed
                      Outcome = Error error }

                return! settleAdmission observe ports decision
            | Ok acquisition -> return! acquisitionOutcome observe ports accepted acquisition
        }

    let private readTarget ports lease =
        try
            ports.LeaseTarget lease |> Result.mapError TargetPreparationError.OwnerRejected
        with error ->
            Error(TargetPreparationError.BoundaryFailed error)

    let private prepareTarget
        (ports: ChatAdmissionTransactionPorts)
        (leased: LeasedAdmission)
        : Result<TargetedAdmission, TargetPreparationError> =
        readTarget ports leased.Lease
        |> Result.bind (fun target ->
            modelFromTarget target
            |> Result.mapError TargetPreparationError.ProjectionFailed
            |> Result.map (fun model ->
                { Leased = leased
                  Target = target
                  Identity = exactIdentity leased.Accepted.Witness target
                  Model = model }))

    let private targetError =
        function
        | TargetPreparationError.OwnerRejected rejection ->
            fun release -> ChatAdmissionTransactionError.LeaseTargetFailed(rejection, release)
        | TargetPreparationError.BoundaryFailed error ->
            fun release -> ChatAdmissionTransactionError.LeaseTargetBoundaryFailed(error, release)
        | TargetPreparationError.ProjectionFailed error ->
            fun release -> ChatAdmissionTransactionError.LeaseTargetProjectionFailed(error, release)

    let private targetAdmission observe ports leased =
        observe ChatAdmissionTransactionStep.LeaseTarget

        match prepareTarget ports leased with
        | Ok targeted -> Task.FromResult(Ok targeted)
        | Error error ->
            compensate
                observe
                ports
                leased.Accepted.Witness
                leased.Lease
                ChatExecutionTerminalDisposition.Failed
                (targetError error)

    let private bindAdmission observe ports targeted =
        observe ChatAdmissionTransactionStep.BindExecution

        match
            effect (fun () ->
                ports.Bind targeted.Leased.Accepted.Input.Intent targeted.Leased.Accepted.Witness targeted.Model)
        with
        | Ok() ->
            { Targeted = targeted
              Binding = bindingReceipt targeted.Leased.Accepted.Input.Intent targeted.Identity }
            |> Ok
            |> Task.FromResult
        | Error error ->
            compensate
                observe
                ports
                targeted.Leased.Accepted.Witness
                targeted.Leased.Lease
                ChatExecutionTerminalDisposition.Failed
                (fun release -> ChatAdmissionTransactionError.BindingFailed(error, release))

    let private projectAdmission observe ports bound =
        observe ChatAdmissionTransactionStep.ProjectHost

        match effect (fun () -> ports.ProjectHost bound.Targeted.Model) with
        | Ok() ->
            { Bound = bound
              HostProjection = HostModelProjectionReceipt(bound.Targeted.Identity, bound.Targeted.Model) }
            |> Ok
            |> Task.FromResult
        | Error error ->
            compensate
                observe
                ports
                bound.Targeted.Leased.Accepted.Witness
                bound.Targeted.Leased.Lease
                ChatExecutionTerminalDisposition.Failed
                (fun release -> ChatAdmissionTransactionError.HostProjectionFailed(error, release))

    let private commit ports projected =
        effectValue (fun () -> ports.Commit projected.Bound.Targeted.Leased.Lease projected.Bound.Targeted.Identity)
        |> Result.mapError CommitError.BoundaryFailed
        |> Result.bind (function
            | CapacityTransitionOutcome.Applied
            | CapacityTransitionOutcome.AlreadyApplied -> Ok()
            | CapacityTransitionOutcome.StaleFence ->
                Error(CommitError.OwnerRejected CapacityTransitionOutcome.StaleFence)
            | CapacityTransitionOutcome.Conflict -> Error(CommitError.OwnerRejected CapacityTransitionOutcome.Conflict))

    let private commitError =
        function
        | CommitError.OwnerRejected rejected ->
            fun release -> ChatAdmissionTransactionError.LeaseCommitFailed(rejected, release)
        | CommitError.BoundaryFailed error ->
            fun release -> ChatAdmissionTransactionError.LeaseCommitBoundaryFailed(error, release)

    let private settled projected =
        ChatAdmissionTransactionOutcome.Settled(
            projected.Bound.Targeted.Leased.Accepted.Witness,
            projected.Bound.Targeted.Target,
            projected.Bound.Binding,
            projected.HostProjection
        )

    // semantic-decorator-owner: managed-chat-execution
    // semantic-decorator-WHAT: CHATEXEC-003
    // semantic-decorator-trace-relation: one CommitLease step before the lease commit and one Settled step only after it succeeds; business trace unchanged
    // semantic-decorator-proof: requirements/managed-chat-execution/tests/admission-transaction.test.mjs::WHAT[CHATEXEC-003] managed admission has one fixed success order
    // semantic-decorator-failure-policy: a commit failure stops the sequence at CommitLease; the compensation path owns every later step
    // semantic-decorator-cancel-policy: step notification is synchronous and adds no cancellation boundary
    // semantic-decorator-deadline-policy: step notification is time-independent and adds no deadline
    // semantic-decorator-invocation-bound: 2
    let private commitAdmission observe ports projected =
        observe ChatAdmissionTransactionStep.CommitLease

        match commit ports projected with
        | Ok() ->
            observe ChatAdmissionTransactionStep.Settled
            settled projected |> Ok |> Task.FromResult
        | Error error ->
            compensate
                observe
                ports
                projected.Bound.Targeted.Leased.Accepted.Witness
                projected.Bound.Targeted.Leased.Lease
                ChatExecutionTerminalDisposition.Failed
                (commitError error)

    let private executeAdmission observe ports input =
        taskResult {
            let! accepted = acceptAdmission observe ports input
            let! acquisition = acquireAdmission observe ports accepted

            match acquisition with
            | AdmissionAcquisitionOutcome.AdmissionStopped outcome -> return outcome
            | AdmissionAcquisitionOutcome.LeaseAcquired leased ->
                let! targeted = targetAdmission observe ports leased
                let! bound = bindAdmission observe ports targeted
                let! projected = projectAdmission observe ports bound
                return! commitAdmission observe ports projected
        }

    let executeWith
        (observe: ChatAdmissionTransactionStep -> unit)
        (ports: ChatAdmissionTransactionPorts)
        (input: ChatAdmissionTransactionInput)
        : Task<Result<ChatAdmissionTransactionOutcome, ChatAdmissionTransactionError>> =
        task {
            match! resolve observe input with
            | Error error -> return Error error
            | Ok(ExistingOutcome outcome) -> return Ok outcome
            | Ok AdmissionRequired -> return! executeAdmission observe ports input
        }

    let private bindIntent
        (intent: ChatAdmissionIntent.Decision)
        (witness: ManagedChatAcceptanceWitness)
        (model: OpencodeModel)
        =
        let evidence = ManagedChatAcceptanceWitness.evidence witness

        match intent with
        | ChatAdmissionIntent.Decision.ExternalRootIntent intentEvidence ->
            SessionExecutionBinding.acceptExternalExecution
                intentEvidence.Key.SessionId
                intentEvidence.Key.PhysicalUserMessageId
                evidence.EffectiveAgent
                model
        | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent intentEvidence ->
            SessionExecutionBinding.acceptExternalExecution
                intentEvidence.Key.SessionId
                intentEvidence.Key.PhysicalUserMessageId
                evidence.EffectiveAgent
                model
        | ChatAdmissionIntent.Decision.PendingPromptIntent intentEvidence ->
            SessionExecutionBinding.acceptPromptExecution
                intentEvidence.Key.SessionId
                intentEvidence.PromptKey
                intentEvidence.Key.PhysicalUserMessageId
                evidence.EffectiveAgent
                model
        | _ -> invalidArg "intent" "managed chat transaction requires a managed intent"

    let private bind intent witness model =
        effectValue (fun () -> bindIntent intent witness model)

    let production
        (journal: AgentJournal)
        (acceptManagedIntent:
            ChatAdmissionIntent.Decision -> Task<Result<ManagedChatAcceptanceWitness, ManagedChatAcceptanceError>>)
        (projectHostModel: OpencodeModel -> Result<unit, exn>)
        : ChatAdmissionTransactionPorts =
        { Accept = acceptManagedIntent
          Acquire =
            fun witness ->
                task {
                    let evidence = ManagedChatAcceptanceWitness.evidence witness

                    try
                        let! acquired =
                            ModelRouting.acquireExecutionAdmission
                                evidence.SessionId
                                evidence.PhysicalUserMessageId
                                evidence.EffectiveAgent

                        return Ok acquired
                    with error ->
                        return Error error
                }
          LeaseTarget = ModelRouting.executionAdmissionTarget
          Bind = bind
          ProjectHost =
            fun model ->
                try
                    projectHostModel model
                with error ->
                    Error error
          Commit = ModelRouting.commitExecutionAdmission
          ReleaseBeforeProvider = fun lease -> ModelRouting.releaseExecutionAdmissionBeforeProvider lease lease.Identity
          SettlePreProvider = PreProviderSettlement.settle journal
          Unbind = fun key -> SessionExecutionBinding.releaseAcceptedExecution key.SessionId key.PhysicalUserMessageId }

    let execute ports input = executeWith ignore ports input
