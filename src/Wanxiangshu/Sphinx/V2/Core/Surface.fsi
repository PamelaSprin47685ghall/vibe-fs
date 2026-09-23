namespace Wanxiangshu.Sphinx.V2.Core

open Wanxiangshu.Sphinx.V2.Core

/// The JS-native conformance surface for the v2 Core. Pure functions only: no store,
/// no clock, no mutable state. Contract tests reach the Core through this one surface
/// rather than deep-importing implementation modules.
module Surface =
    // Value construction the wire layer cannot see.
    val setOf: string list -> Set<string>
    val mapOf: (string * float) list -> Map<string, float>
    val listOfItems: string list -> string list
    val listCount: string list -> int
    val listHead: string list -> string
    val workIdSetOf: string list -> Set<WorkId>

    // Typed accessors, so a test never reads Fable's record layout directly.
    val goalTextOf: GoalSpec -> string
    val goalRevisionValue: GoalSpec -> int64
    val goalAmendmentsOf: GoalSpec -> GoalAmendment array
    val statusNameOf: InquiryStatus -> string
    val workSpecIdOf: WorkSpec -> WorkId
    val workSpecAttemptOf: WorkSpec -> Attempt
    val workSpecFenceOf: WorkSpec -> Fence
    val workSpecDependenciesOf: WorkSpec -> Set<WorkId>
    val workSpecReservedOf: WorkSpec -> Map<string, float>
    val workItemSpecOf: WorkItem -> WorkSpec
    val workItemStateOf: WorkItem -> WorkState
    val workItemStateName: WorkItem -> string
    val stateGoalOf: InquiryState -> GoalSpec
    val stateWorkOf: InquiryState -> Map<WorkId, WorkItem>
    val stateStatusOf: InquiryState -> InquiryStatus
    val stateRevisionOf: InquiryState -> Revision
    val firstReadyWork: InquiryState -> WorkItem option
    val readyWorkCount: InquiryState -> int

    // Result decomposition, without knowing how Fable lays a Result out.
    val isOk: Result<'value, 'error> -> bool
    val isError: Result<'value, 'error> -> bool
    val okValue: Result<'value, 'error> -> 'value option
    val errorValue: Result<'value, 'error> -> 'error option
    val errorCode: Result<'value, {| Code: string |}> -> string option

    // Identity constructors. Tests build typed ids through the same surface the rest of
    // the API is reached by, so a malformed id is caught by the same validator the
    // production path uses.
    val inquiryIdCreate: string -> InquiryId
    val goalIdCreate: string -> GoalId
    val planIdCreate: string -> PlanId
    val workIdCreate: string -> WorkId
    val roundIdCreate: string -> RoundId
    val nodeIdCreate: string -> NodeId
    val certificateIdCreate: string -> CertificateId
    val eventIdCreate: string -> EventId
    val observationIdCreate: string -> ObservationId
    val attemptIdCreate: string -> AttemptId
    val fenceCreate: string -> Fence
    val workIdValue: WorkId -> string
    val nodeIdValue: NodeId -> string
    val workIdTryCreate: string -> Result<WorkId, string>

    // Union constructors, so a test never reads Fable's tag/fields representation.
    val statusCreate: string -> string -> InquiryStatus
    val resourceKindCreate: string -> string -> ResourceKind
    val guaranteeCreate: string -> string list -> CertificateGuarantee
    val eventBodyTag: InquiryEventBody -> string

    val goalCreate: GoalSpec -> GoalSpec
    val goalTryAmend: string -> string list -> string option -> GoalSpec -> Result<GoalSpec, GoalError>
    val goalTryCreate: GoalSpec -> Result<GoalSpec, GoalError>

    val revisionOrigin: Revision
    val revisionNext: Revision -> Revision
    val revisionValue: Revision -> int64
    val revisionTryCreate: int64 -> Result<Revision, string>

    val attemptFirst: Attempt
    val attemptValue: Attempt -> int64
    val attemptTryCreate: int64 -> Result<Attempt, string>

    val workStateName: WorkState -> string
    val workValidateSpec: WorkSpec -> Result<unit, WorkError>
    val workIsTerminal: WorkState -> bool

    val budgetValidateSpecs: ResourceSpec list -> Result<unit, BudgetError>
    val budgetSignedFree: ResourceSpec list -> Map<string, float> -> Map<string, float> -> string -> float
    val budgetAvailableForNewWork: ResourceSpec list -> Map<string, float> -> Map<string, float> -> string -> float
    val budgetObservedOverrun: ResourceSpec list -> Map<string, float> -> Map<string, float> -> string -> float
    val budgetMergeReserved: Map<string, float> list -> Map<string, float>

    val budgetTryReserve:
        ResourceSpec list ->
        Map<string, float> ->
        Map<string, float> ->
        Reservation ->
            Result<Map<string, float>, BudgetError>

    val certificateValidateGuarantee: CertificateGuarantee -> Result<unit, CertificateError>
    val certificateValidateSlot: CertificateSlot -> Result<unit, CertificateError>
    val graphRoleName: GraphRole -> string

    /// A minimal state for one inquiry, built the way the fold builds it.
    val stateOfCreate: string -> string -> Result<InquiryState, CoreError>

    val reducerFold: InquiryEvent list -> Result<InquiryState, CoreError>
    val reducerFoldBatch: InquiryEvent list -> Result<InquiryState, CoreError>
    val reducerApply: InquiryState option -> InquiryEvent -> Result<InquiryState, CoreError>

    val projectionSemanticProjection: InquiryState -> SemanticProjection
    val projectionSemanticHash: InquiryState -> string
    val projectionStateHash: InquiryState -> string
    val projectionTraceHash: string list -> string

    val stateCertificateKey: string -> string -> string -> string -> string
    val stateReservationKey: ReservationKey -> string
    val stateCommandRevision: InquiryState -> string -> Revision option
    val stateIsTerminal: InquiryStatus -> bool
    val stateReadyWork: InquiryState -> WorkItem list

    val eventTransitionType: string
    val eventAllTypes: string list
    val eventIsKnown: string -> bool
