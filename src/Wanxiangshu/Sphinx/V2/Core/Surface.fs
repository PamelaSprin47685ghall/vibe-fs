namespace Wanxiangshu.Sphinx.V2.Core

open Wanxiangshu.Sphinx.V2.Core

/// The JS-native conformance surface for the v2 Core.
///
/// This module exists so contract tests reach the Core through one registered surface
/// rather than deep-importing individual dist modules. It re-exports the pure Core API
/// and nothing else: no mutable state, no store, no clock.
///
/// WHAT[js-semantic-surface-003]: registration is what makes the boundary explicit. A
/// test that deep-imports is reading an implementation module, which the gate forbids.

module Surface =

    // --- Value construction the wire layer cannot see -------------------------
    // A JS caller cannot build an F# Set, Map or list by hand, and a test that tries
    // reaches into runtime internals the boundary gate forbids. These helpers give it
    // the plain-JS way in.

    let setOf (items: string list) : Set<string> = items |> List.ofSeq |> Set.ofSeq

    let mapOf (entries: (string * float) list) : Map<string, float> =
        entries |> List.ofSeq |> Map.ofSeq

    /// A cons list is a linked structure, not a JS array. A plain array passed to an
    /// `'a list` parameter reads as empty, so the surface converts on the way in.
    let listOfItems (items: string list) : string list =
        items |> List.ofSeq

    let listCount (items: string list) : int = List.length items

    let listHead (items: string list) : string =
        match items with
        | item :: _ -> item
        | [] -> ""

    let workIdSetOf (items: string list) : Set<WorkId> =
        items |> List.ofSeq |> List.map WorkId.create |> Set.ofSeq

    /// Typed accessors for the values a test must read out of a folded state. Reading
    /// a field directly would be reading Fable's record layout, which the boundary gate
    /// forbids; these make the read part of the surface's contract instead.
    let goalTextOf (goal: GoalSpec) : string = goal.OriginalText

    let goalRevisionValue (goal: GoalSpec) : int64 = Revision.value goal.Revision

    let goalAmendmentsOf (goal: GoalSpec) : GoalAmendment list = goal.Amendments

    let statusNameOf (status: InquiryStatus) : string =
        match status with
        | InquiryStatus.Active -> "active"
        | InquiryStatus.InputRequired _ -> "input-required"
        | InquiryStatus.Suspended _ -> "suspended"
        | InquiryStatus.Cancelling -> "cancelling"
        | InquiryStatus.StopReached _ -> "completed"
        | InquiryStatus.Failed _ -> "failed"
        | InquiryStatus.Cancelled _ -> "cancelled"

    let workSpecIdOf (spec: WorkSpec) : WorkId = spec.Id

    let workSpecAttemptOf (spec: WorkSpec) : Attempt = spec.Attempt

    let workSpecFenceOf (spec: WorkSpec) : Fence = spec.Fence

    let workSpecDependenciesOf (spec: WorkSpec) : Set<WorkId> = spec.Dependencies

    let workSpecReservedOf (spec: WorkSpec) : Map<string, float> = spec.Reserved

    let workItemSpecOf (item: WorkItem) : WorkSpec = item.Spec

    let workItemStateOf (item: WorkItem) : WorkState = item.State

    let workItemStateName (item: WorkItem) : string = Work.stateName item.State

    let stateGoalOf (state: InquiryState) : GoalSpec = state.Goal

    let stateWorkOf (state: InquiryState) : Map<WorkId, WorkItem> = state.Work

    let stateStatusOf (state: InquiryState) : InquiryStatus = state.Status

    let stateRevisionOf (state: InquiryState) : Revision = state.Revision

    /// Reads the first work item in the ready set, if any.
    let firstReadyWork (state: InquiryState) : WorkItem option =
        InquiryState.readyWork state |> List.tryHead

    let readyWorkCount (state: InquiryState) : int = InquiryState.readyWork state |> List.length

    // --- Result decomposition --------------------------------------------------
    // A Result crosses the boundary as a tagged outcome, and a caller must be able to
    // tell the two apart without knowing how Fable lays them out.

    let isOk (result: Result<'value, 'error>) : bool =
        match result with
        | Ok _ -> true
        | Error _ -> false

    let isError (result: Result<'value, 'error>) : bool =
        match result with
        | Ok _ -> false
        | Error _ -> true

    let okValue (result: Result<'value, 'error>) : 'value option =
        match result with
        | Ok value -> Some value
        | Error _ -> None

    let errorValue (result: Result<'value, 'error>) : 'error option =
        match result with
        | Ok _ -> None
        | Error fault -> Some fault

    /// The fault code, when the caller's error record carries one.
    let errorCode (result: Result<'value, {| Code: string |}>) : string option =
        match result with
        | Ok _ -> None
        | Error fault -> Some fault.Code

    // --- Identity constructors -------------------------------------------------
    // Tests must build typed ids and revisions through the same surface the rest of
    // the API is reached by, so a malformed id is caught by the same validator the
    // production path uses.

    let inquiryIdCreate (value: string) : InquiryId = InquiryId.create value

    let goalIdCreate (value: string) : GoalId = GoalId.create value

    let planIdCreate (value: string) : PlanId = PlanId.create value

    let workIdCreate (value: string) : WorkId = WorkId.create value

    let roundIdCreate (value: string) : RoundId = RoundId.create value

    let nodeIdCreate (value: string) : NodeId = NodeId.create value

    let certificateIdCreate (value: string) : CertificateId = CertificateId.create value

    let eventIdCreate (value: string) : EventId = EventId.create value

    let observationIdCreate (value: string) : ObservationId = ObservationId.create value

    let attemptIdCreate (value: string) : AttemptId = AttemptId.create value

    let fenceCreate (value: string) : Fence = Fence.create value

    let workIdValue (id: WorkId) : string = WorkId.value id

    let nodeIdValue (id: NodeId) : string = NodeId.value id

    let workIdTryCreate (value: string) : Result<WorkId, string> = WorkId.tryCreate value

    // --- Union constructors ---------------------------------------------------
    // Fable emits discriminated unions as `tag` plus `fields`, and the gate on test
    // sources forbids reading that shape directly. These constructors let a test build
    // a case without knowing how Fable represents it.

    let statusCreate (tag: string) (reason: string) : InquiryStatus =
        match tag with
        | "active" -> InquiryStatus.Active
        | "input-required" -> InquiryStatus.InputRequired reason
        | "suspended" -> InquiryStatus.Suspended reason
        | "cancelling" -> InquiryStatus.Cancelling
        | "completed" -> InquiryStatus.StopReached reason
        | "failed" -> InquiryStatus.Failed reason
        | "cancelled" -> InquiryStatus.Cancelled reason
        | _ -> InquiryStatus.Active

    /// A resource kind, built through the surface so a test never touches the DU shape.
    let resourceKindCreate (tag: string) (unitName: string) : ResourceKind =
        match tag with
        | "consumed" -> ResourceKind.Consumed unitName
        | "capacity" -> ResourceKind.Capacity unitName
        | _ -> ResourceKind.Consumed unitName

    let guaranteeCreate (tag: string) (args: string list) : CertificateGuarantee =
        // A JS caller passes a plain array; F# list patterns do not match it, so the
        // arguments are read by index and a short list yields "" rather than a wrong case.
        let arg index = args |> List.tryItem index |> Option.defaultValue ""
        let hasCount count = List.length args >= count

        let empiricalAssumptions () =
            match args with
            | assumption :: _ -> [ assumption ]
            | [] -> []

        match tag with
        | "empirical-summary" -> CertificateGuarantee.EmpiricalSummary(empiricalAssumptions ())
        | "ordinal-observation" -> CertificateGuarantee.OrdinalObservation(arg 0)
        | "model-estimate" when hasCount 2 ->
            CertificateGuarantee.ModelEstimate(arg 0, arg 1)
        | "posterior-credible" when hasCount 3 ->
            CertificateGuarantee.PosteriorCredible(arg 0, float (arg 1), arg 2)
        | "frequentist-coverage" when hasCount 3 ->
            CertificateGuarantee.FrequentistCoverage(arg 0, float (arg 1), arg 2)
        | "deterministic-bound" -> CertificateGuarantee.DeterministicBound(arg 0, [])
        | "exact-within-model" -> CertificateGuarantee.ExactWithinModel(arg 0, arg 1)
        | "residual-only" -> CertificateGuarantee.ResidualOnly(arg 0)
        | _ -> CertificateGuarantee.ResidualOnly "unspecified"

    let eventBodyTag (body: InquiryEventBody) : string =
        match body with
        | InquiryEventBody.InquiryCreated _ -> "InquiryCreated"
        | InquiryEventBody.GoalAmended _ -> "GoalAmended"
        | InquiryEventBody.SnapshotRegistered _ -> "SnapshotRegistered"
        | InquiryEventBody.DecisionScopeOpened _ -> "DecisionScopeOpened"
        | InquiryEventBody.RoundOpened _ -> "RoundOpened"
        | InquiryEventBody.WorkPlanned _ -> "WorkPlanned"
        | InquiryEventBody.RoundClosed _ -> "RoundClosed"
        | InquiryEventBody.BudgetReserved _ -> "BudgetReserved"
        | InquiryEventBody.UsageSettled _ -> "UsageSettled"
        | InquiryEventBody.ReservationReleased _ -> "ReservationReleased"
        | InquiryEventBody.UsageOverrunRecorded _ -> "UsageOverrunRecorded"
        | InquiryEventBody.DispatchRequested _ -> "DispatchRequested"
        | InquiryEventBody.DispatchReceiptRecorded _ -> "DispatchReceiptRecorded"
        | InquiryEventBody.WorkAttemptTransitioned _ -> "WorkAttemptTransitioned"
        | InquiryEventBody.HostTerminalRecorded _ -> "HostTerminalRecorded"
        | InquiryEventBody.ResultAccepted _ -> "ResultAccepted"
        | InquiryEventBody.InterpretationPending _ -> "InterpretationPending"
        | InquiryEventBody.InterpretationApplied _ -> "InterpretationApplied"
        | InquiryEventBody.InterpretationFailed _ -> "InterpretFailed"
        | InquiryEventBody.GraphPatched _ -> "GraphPatched"
        | InquiryEventBody.CertificateSlotsPatched _ -> "CertificateSlotsPatched"
        | InquiryEventBody.CertificateInvalidated _ -> "CertificateInvalidated"
        | InquiryEventBody.DecisionRecorded _ -> "DecisionRecorded"
        | InquiryEventBody.AnswerPrepared _ -> "AnswerPrepared"
        | InquiryEventBody.AnswerCommitted _ -> "AnswerCommitted"
        | InquiryEventBody.CancelRequested _ -> "CancelRequested"
        | InquiryEventBody.InquiryCancelled _ -> "InquiryCancelled"
        | InquiryEventBody.InquirySuspended _ -> "InquirySuspended"
        | InquiryEventBody.InquiryFailed _ -> "InquiryFailed"
        | InquiryEventBody.InquiryStatusChanged _ -> "InquiryStatusChanged"

    let goalCreate (goal: GoalSpec) : GoalSpec = Goal.create goal

    let goalTryAmend
        (authorizedBy: string)
        (addedConstraints: string list)
        (replacementText: string option)
        (goal: GoalSpec)
        : Result<GoalSpec, GoalError> =
        Goal.tryAmend authorizedBy addedConstraints replacementText goal

    let goalTryCreate (goal: GoalSpec) : Result<GoalSpec, GoalError> = Goal.tryCreate goal

    let revisionOrigin : Revision = Revision.origin

    let revisionNext (revision: Revision) : Revision = Revision.next revision

    let revisionValue (revision: Revision) : int64 = Revision.value revision

    let revisionTryCreate (value: int64) : Result<Revision, string> = Revision.tryCreate value

    let attemptFirst : Attempt = Attempt.first

    let attemptValue (attempt: Attempt) : int64 = Attempt.value attempt

    let attemptTryCreate (value: int64) : Result<Attempt, string> = Attempt.tryCreate value

    let workStateName (state: WorkState) : string = Work.stateName state

    let workValidateSpec (spec: WorkSpec) : Result<unit, WorkError> = Work.validateSpec spec

    let workIsTerminal (state: WorkState) : bool = Work.isTerminal state

    let budgetValidateSpecs (specs: ResourceSpec list) : Result<unit, BudgetError> = Budget.validateSpecs specs

    let budgetSignedFree
        (specs: ResourceSpec list)
        (settled: Map<string, float>)
        (reserved: Map<string, float>)
        (name: string)
        : float =
        Budget.signedFree specs settled reserved name

    let budgetAvailableForNewWork
        (specs: ResourceSpec list)
        (settled: Map<string, float>)
        (reserved: Map<string, float>)
        (name: string)
        : float =
        Budget.availableForNewWork specs settled reserved name

    let budgetObservedOverrun
        (specs: ResourceSpec list)
        (settled: Map<string, float>)
        (reserved: Map<string, float>)
        (name: string)
        : float =
        Budget.observedOverrun specs settled reserved name

    let budgetMergeReserved (pools: Map<string, float> list) : Map<string, float> = Budget.mergeReserved pools

    let budgetTryReserve
        (specs: ResourceSpec list)
        (settled: Map<string, float>)
        (reserved: Map<string, float>)
        (reservation: Reservation)
        : Result<Map<string, float>, BudgetError> =
        Budget.tryReserve specs settled reserved reservation

    let certificateValidateGuarantee (guarantee: CertificateGuarantee) : Result<unit, CertificateError> =
        Certificate.validateGuarantee guarantee

    let certificateValidateSlot (slot: CertificateSlot) : Result<unit, CertificateError> =
        Certificate.validateSlot slot

    let graphRoleName (role: GraphRole) : string = GraphRole.name role

    let reducerFold (events: InquiryEvent list) : Result<InquiryState, CoreError> = Reducer.fold events

    let reducerFoldBatch (events: InquiryEvent list) : Result<InquiryState, CoreError> = Reducer.foldBatch events

    let reducerApply
        (state: InquiryState option)
        (event: InquiryEvent)
        : Result<InquiryState, CoreError> =
        Reducer.apply state event

    let projectionSemanticProjection (state: InquiryState) : SemanticProjection =
        Projection.semanticProjection state

    let projectionSemanticHash (state: InquiryState) : string = Projection.semanticHash state

    let projectionStateHash (state: InquiryState) : string = Projection.stateHash state

    let projectionTraceHash (envelopes: string list) : string = Projection.traceHash envelopes

    let stateCertificateKey (targetRef: string) (valueSpaceId: string) (scopeId: string) (semanticsModelRef: string) : string =
        InquiryState.certificateKey targetRef valueSpaceId scopeId semanticsModelRef

    let stateReservationKey (key: ReservationKey) : string = InquiryState.reservationKey key

    let stateCommandRevision (state: InquiryState) (commandId: string) : Revision option =
        InquiryState.commandRevision state commandId

    let stateIsTerminal (status: InquiryStatus) : bool = InquiryState.isTerminal status

    let stateReadyWork (state: InquiryState) : WorkItem list = InquiryState.readyWork state

    let eventTransitionType : string = SphinxV2EventTypes.transition

    let eventAllTypes : string list = SphinxV2EventTypes.all

    let eventIsKnown (eventType: string) : bool = SphinxV2EventTypes.isKnown eventType
