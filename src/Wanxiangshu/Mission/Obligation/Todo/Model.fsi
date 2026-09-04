namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay

module MagicTodo =
    type TodoWriteId = private TodoWriteId of string

    module TodoWriteId =
        val create: value: string -> TodoWriteId
        val value: TodoWriteId -> string

    [<Literal>]
    val SemanticVersion: string = "magic-todo.v6"

    [<RequireQualifiedAccess>]
    type ObligationHorizon =
        | Near
        | Mid
        | Far

    [<RequireQualifiedAccess>]
    module ObligationHorizon =
        val wire: (ObligationHorizon -> string)
        val tryParse: (string -> ObligationHorizon option)

    type Obligation =
        { Name: string
          Horizon: ObligationHorizon
          Work: string }

    type ObligationList = Obligation list

    type TodoWriteInput =
        { PlanComplete: bool
          WorkingOn: string
          Obligations: ObligationList }

    val normalizeWorkingOn: workingOn: string -> items: ObligationList -> string

    [<RequireQualifiedAccess>]
    type MagicTodoReject =
        | MultipleTodowriteInMessage of callIds: string list
        | EmptyObligationName of ordinal: int
        | DuplicateObligationName of name: string
        | IdentityCorruption of field: string
        | FirstSuicideWithoutCheckpoint

    val todoWriteId: sha256: (string -> string) -> incumbencyId: IncumbencyId -> toolCallId: ToolCallId -> TodoWriteId
    val canonicalObligationListWire: items: ObligationList -> string
    val obligationListDigest: sha256: (string -> string) -> items: ObligationList -> string
    val validateObligations: items: ObligationList -> Result<ObligationList, MagicTodoReject>
    val validateTodoWriteInput: input: TodoWriteInput -> Result<TodoWriteInput, MagicTodoReject>
    val admitTodowriteBatch: toolCallIdsInMessage: ToolCallId list -> Result<unit, MagicTodoReject>

    type PreparedIdentity =
        { IncumbencyId: IncumbencyId
          ProviderInputDigest: string
          BaseTodoDigest: string
          ToolPartOrdinal: int }

    val checkPreparedReplay: expected: PreparedIdentity -> observed: PreparedIdentity -> Result<unit, MagicTodoReject>
    val desiredLag1Cutoff: acceptedInOrder: TodoWriteId list -> TodoWriteId option
    val workRecordStart: openingCursor: XTraceCursor -> XTraceCursor

    type TracePartAnchor =
        { Cursor: XTraceCursor
          Kind: string
          ToolCallId: ToolCallId option }

    val blindPlanOpeningBoundary:
        openingCursor: XTraceCursor ->
        t1CallCursor: XTraceCursor ->
        t1ToolCallId: ToolCallId ->
        parts: TracePartAnchor list ->
            XTraceCursor

    val effectiveOpeningFloor:
        bool ->
        bool ->
        XTraceCursor ->
        XTraceCursor option ->
        ToolCallId option ->
        int64 ->
        TracePartAnchor list ->
            XTraceCursor option

    val bloggerEffectiveStart: recordCoverage: RecordCoverage -> workRecordStartCursor: XTraceCursor -> XTraceCursor
    val requirePlanCommitmentBeforeFirstSuicide: planCommitted: bool -> Result<unit, MagicTodoReject>
