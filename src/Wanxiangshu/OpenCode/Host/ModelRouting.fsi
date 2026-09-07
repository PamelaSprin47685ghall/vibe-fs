namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module ModelRouting =
    val internal failureOfExecutionAdmissionAcquisition: ExecutionAdmissionAcquisition -> ExecutionFailure option
    val internal capacityOwnership: lease: ExecutionAdmissionLease -> CapacityOwnership

    val invokeScheduler:
        scheduler: obj ->
        role: string ->
        running: ModelRoutingTarget array ->
        previous: ModelRoutingTarget option ->
            ModelRoutingTarget option

    val configPath: unit -> string
    val bootstrapAndLoadAt: path: string -> template: string -> Task<obj>
    val bootstrapDefault: unit -> Task<obj>
    val toOpenCodeModel: target: ModelRoutingTarget -> OpencodeModel
    val ofOpenCodeModel: model: OpencodeModel -> ModelRoutingTarget option
    val sameTarget: expected: ModelRoutingTarget -> observed: OpencodeModel -> bool

    type internal ModelRoutingRuntime =
        new: scheduler: obj -> ModelRoutingRuntime

        member AcquireExecutionAdmission:
            sessionId: string *
            physicalUserMessageId: string *
            role: Role *
            participant: string *
            lenderSessionId: string option ->
                Task<ExecutionAdmissionAcquisition>

        member ExecutionAdmissionTarget:
            lease: ExecutionAdmissionLease -> Result<ModelRoutingTarget, ExecutionAdmissionRejection>

        member CommitExecutionAdmission:
            lease: ExecutionAdmissionLease * observed: ExecutionAdmissionExactIdentity -> CapacityTransitionOutcome

        member ReleaseExecutionAdmissionBeforeProvider:
            lease: ExecutionAdmissionLease * observed: ExecutionAdmissionExactIdentity -> CapacityTransitionOutcome

        member ExecutionAdmissionLifecycle:
            lease: ExecutionAdmissionLease -> Result<string, ExecutionAdmissionRejection>

        member TryReserveManaged:
            sessionId: string * role: Role * lenderSessionId: string option -> ModelRoutingTarget option

        member TryLease:
            sessionId: string *
            physicalUserMessageId: string *
            role: Role *
            participant: string *
            lenderSessionId: string option ->
                ModelRoutingTarget option

        member internal ReleaseExecution: sessionId: string -> CapacityTransitionOutcome

        member internal ReleasePhysicalExecution:
            sessionId: string * physicalUserMessageId: string -> CapacityTransitionOutcome

        member CancelPendingExecution: sessionId: string -> CapacityTransitionOutcome
        member CapacitySnapshot: unit -> CapacityInvariantEvidence

        member EnterProviderStep:
            sessionId: string * physicalUserMessageId: string * visibleProviderRuns: Set<string> -> Task

        member EndProviderStep: sessionId: string * physicalUserMessageId: string * providerRun: string -> unit
        member TakeProviderRunTarget: providerRun: string -> ModelRoutingTarget option
        member SuppressProviderStep: sessionId: string * physicalUserMessageId: string -> unit
        member SnapshotOccupied: unit -> ModelRoutingTarget array
        member PendingCount: int
        member PendingBound: int
        member PendingContractVersion: int

    val initialize: unit -> Task

    val internal takeProviderRunTarget: providerRun: ProviderRunIdentity -> ModelRoutingTarget option
    val internal markProviderFailed: provider: string -> unit
    val internal hasTheoreticalCapacity: role: string -> bool

    val internal acquireExecutionAdmission:
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId ->
        role: Role ->
        participant: string ->
        lenderSessionId: string option ->
            Task<ExecutionAdmissionAcquisition>

    val internal executionAdmissionTarget:
        lease: ExecutionAdmissionLease -> Result<ModelRoutingTarget, ExecutionAdmissionRejection>

    val internal commitExecutionAdmission:
        lease: ExecutionAdmissionLease -> observed: ExecutionAdmissionExactIdentity -> CapacityTransitionOutcome

    val internal releaseExecutionAdmissionBeforeProvider:
        lease: ExecutionAdmissionLease -> observed: ExecutionAdmissionExactIdentity -> CapacityTransitionOutcome

    val hasRuntime: unit -> bool

    val tryReserveManaged:
        sessionId: SessionId -> role: Role -> lenderSessionId: string option -> ModelRoutingTarget option

    val tryLease:
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId ->
        role: Role ->
        participant: string ->
        lenderSessionId: string option ->
            ModelRoutingTarget option

    val internal releaseExecution: sessionId: SessionId -> CapacityTransitionOutcome

    val internal releasePhysicalExecution:
        sessionId: SessionId -> physicalUserMessageId: PhysicalUserMessageId -> CapacityTransitionOutcome

    val internal observePhysicalResource: key: ChatExecutionKey -> PhysicalResourceObservation
    val internal cancelUnacquiredExecution: sessionId: SessionId -> CapacityTransitionOutcome
    val internal capacitySnapshot: unit -> CapacityInvariantEvidence

    val enterProviderStep:
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId ->
        visibleProviderRuns: Set<ProviderRunIdentity> ->
            Task

    val endProviderStep:
        sessionId: SessionId -> physicalUserMessageId: PhysicalUserMessageId -> providerRun: ProviderRunIdentity -> unit

    val suppressProviderStep: sessionId: SessionId -> physicalUserMessageId: PhysicalUserMessageId -> unit
    val projectHostModel: output: obj -> model: OpencodeModel -> Result<unit, exn>
