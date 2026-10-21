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
    val stateGoalOf: InquiryState -> GoalSpec
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
    val goalIdCreate: string -> GoalId

    // Union constructors, so a test never reads Fable's tag/fields representation.
    val statusCreate: string -> string -> InquiryStatus
    val resourceKindCreate: string -> string -> ResourceKind
    val guaranteeCreate: string -> string list -> CertificateGuarantee
    val guaranteeKind: CertificateGuarantee -> string
    val eventBodyTag: InquiryEventBody -> string

    val goalCreate: GoalSpec -> GoalSpec
    val goalTryAmend: string -> string list -> string option -> GoalSpec -> Result<GoalSpec, GoalError>

    val revisionValue: Revision -> int64
    val revisionTryCreate: int64 -> Result<Revision, string>


    val workStateName: WorkState -> string

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

    /// A minimal state for one inquiry, built the way the fold builds it.
    val stateOfCreate: string -> string -> Result<InquiryState, CoreError>

    val reducerApply: InquiryState option -> InquiryEvent -> Result<InquiryState, CoreError>

    val projectionSemanticHash: InquiryState -> string

    val stateCertificateKey: string -> string -> string -> string -> string
    val stateCommandRevision: InquiryState -> string -> Revision option
