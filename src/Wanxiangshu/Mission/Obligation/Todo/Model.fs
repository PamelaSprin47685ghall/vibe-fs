namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

open System
open System.Text
open Wanxiangshu.Foundation
open Wanxiangshu.Mission.Review
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

/// Magic Todo Checkpoint Protocol — GrandRewrite clean-break algebra.
///
/// Provider truth is one complete `{name, work}` obligation account plus a
/// `WorkingOn` focus pointer. Accepted checkpoints own CurrentObligations
/// immediately; process review owns only lag-1 semantic feedback. No Todo
/// status/id/progress/settlement machine exists in this module.
module MagicTodo =

    // ── Checkpoint / review identity ────────────────────────────────────────

    /// Digest(ManagerLifeId + ToolCallId). Same ToolCallId replay → same id.
    type TodoWriteId = private TodoWriteId of string

    /// Digest(ManagerLifeId + TodoWriteId). One review obligation per Accepted checkpoint.
    type TodoReviewId = private TodoReviewId of string

    /// Logical dedicated process-reviewer identity for one Manager Life.
    type DedicatedReviewerId = private DedicatedReviewerId of string

    module TodoWriteId =
        let create (value: string) = TodoWriteId value
        let value (TodoWriteId value) = value

    module TodoReviewId =
        let create (value: string) = TodoReviewId value
        let value (TodoReviewId value) = value

    module DedicatedReviewerId =
        let create (value: string) = DedicatedReviewerId value
        let value (DedicatedReviewerId value) = value

    /// Semantic version frozen into Prepared / Accepted facts.
    [<Literal>]
    let SemanticVersion = "magic-todo.v4"

    // ── Provider account ───────────────────────────────────────────────────

    /// The only provider-visible todo item shape after GrandRewrite.
    type Obligation = { Name: string; Work: string }

    type ObligationList = Obligation list

    /// Provider-visible checkpoint input. The bool is a declaration about the
    /// road being complete enough to entrust, not a workflow stage.
    type TodoWriteInput =
        { PlanComplete: bool
          WorkingOn: string
          Obligations: ObligationList }

    [<RequireQualifiedAccess>]
    type WorkingOnValidationError =
        | MustBeEmptyForEmptyAccount of actual: string
        | MustMatchObligationName of actual: string

    let validateWorkingOn (workingOn: string) (items: ObligationList) : Result<unit, WorkingOnValidationError> =
        match items with
        | [] when workingOn = "" -> Ok()
        | [] -> Error(WorkingOnValidationError.MustBeEmptyForEmptyAccount workingOn)
        | _ when items |> List.exists (fun item -> item.Name = workingOn) -> Ok()
        | _ -> Error(WorkingOnValidationError.MustMatchObligationName workingOn)

    [<RequireQualifiedAccess>]
    type ProcessReviewVerdict =
        | Perfect
        | Revise

    module ProcessReviewVerdict =

        let ofGuard (verdict: ReviewGuardVerdict) : ProcessReviewVerdict =
            match verdict with
            | ReviewGuardVerdict.Perfect -> ProcessReviewVerdict.Perfect
            | ReviewGuardVerdict.Revise -> ProcessReviewVerdict.Revise

        let wire (verdict: ProcessReviewVerdict) : string =
            match verdict with
            | ProcessReviewVerdict.Perfect -> "PERFECT"
            | ProcessReviewVerdict.Revise -> "REVISE"

    // ── Admission / recovery decisions ─────────────────────────────────────

    /// Pure decision taxonomy. Host classification is stricter:
    /// - call-shape cases may become provider red text;
    /// - IdentityCorruption is an infrastructure invariant break → process fatal;
    /// - AwaitingConsumableReview is a legal causal wait, never a rejection.
    [<RequireQualifiedAccess>]
    type MagicTodoReject =
        | MultipleTodowriteInMessage of callIds: string list
        | EmptyObligationName of ordinal: int
        | DuplicateObligationName of name: string
        | IdentityCorruption of field: string
        | AwaitingConsumableReview of pendingTodoWriteId: string
        | FirstSuicideWithoutCheckpoint

    let todoWriteId (sha256: string -> string) (lifeId: ManagerLifeId) (toolCallId: ToolCallId) : TodoWriteId =
        TodoWriteId.create (
            sha256 (String.concat "|" [ ManagerLifeId.value lifeId; ToolCallId.value toolCallId; "todo-write" ])
        )

    let todoReviewId (sha256: string -> string) (lifeId: ManagerLifeId) (writeId: TodoWriteId) : TodoReviewId =
        TodoReviewId.create (
            sha256 (String.concat "|" [ ManagerLifeId.value lifeId; TodoWriteId.value writeId; "todo-review" ])
        )

    let dedicatedReviewerId (sha256: string -> string) (lifeId: ManagerLifeId) : DedicatedReviewerId =
        DedicatedReviewerId.create (
            sha256 (String.concat "|" [ ManagerLifeId.value lifeId; "dedicated-todo-reviewer" ])
        )

    let private escapedJsonCharacter character =
        match character with
        | '"' -> "\\\""
        | '\\' -> "\\\\"
        | '\n' -> "\\n"
        | '\r' -> "\\r"
        | '\t' -> "\\t"
        | _ when int character < 0x20 -> sprintf "\\u%04x" (int character)
        | _ -> string character

    let private jsonString (value: string) =
        let builder = StringBuilder(value.Length + 8)
        builder.Append('"') |> ignore

        for character in value do
            builder.Append(escapedJsonCharacter character) |> ignore

        builder.Append('"') |> ignore
        builder.ToString()

    /// Byte-stable durable/provider account body.
    let canonicalObligationListWire (items: ObligationList) : string =
        items
        |> List.map (fun item -> sprintf """{"name":%s,"work":%s}""" (jsonString item.Name) (jsonString item.Work))
        |> String.concat ","
        |> sprintf "[%s]"

    let obligationListDigest (sha256: string -> string) (items: ObligationList) : string =
        canonicalObligationListWire items |> sha256

    /// Provider-visible identity is the stable human-readable obligation name.
    let validateObligations (items: ObligationList) : Result<ObligationList, MagicTodoReject> =
        let rec loop ordinal seen remaining =
            match remaining with
            | [] -> Ok items
            | head :: tail when String.IsNullOrWhiteSpace head.Name ->
                Error(MagicTodoReject.EmptyObligationName ordinal)
            | head :: _ when Set.contains head.Name seen -> Error(MagicTodoReject.DuplicateObligationName head.Name)
            | head :: tail -> loop (ordinal + 1) (Set.add head.Name seen) tail

        loop 0 Set.empty items

    /// More than one distinct todowrite call in one assistant message is a call
    /// protocol error. Same ToolCallId replay remains one physical attempt.
    let admitTodowriteBatch (toolCallIdsInMessage: ToolCallId list) : Result<unit, MagicTodoReject> =
        let distinct = toolCallIdsInMessage |> List.map ToolCallId.value |> List.distinct

        if List.length distinct > 1 then
            Error(MagicTodoReject.MultipleTodowriteInMessage distinct)
        else
            Ok()

    type PreparedIdentity =
        { ManagerLifeId: ManagerLifeId
          ProviderInputDigest: string
          BaseTodoDigest: string
          ToolPartOrdinal: int }

    /// Same TodoWriteId replay must match frozen Prepared identity fields.
    let checkPreparedReplay (expected: PreparedIdentity) (observed: PreparedIdentity) : Result<unit, MagicTodoReject> =
        if expected.ManagerLifeId <> observed.ManagerLifeId then
            Error(MagicTodoReject.IdentityCorruption "ManagerLifeId")
        elif expected.ProviderInputDigest <> observed.ProviderInputDigest then
            Error(MagicTodoReject.IdentityCorruption "ProviderInputDigest")
        elif expected.BaseTodoDigest <> observed.BaseTodoDigest then
            Error(MagicTodoReject.IdentityCorruption "BaseTodoDigest")
        elif expected.ToolPartOrdinal <> observed.ToolPartOrdinal then
            Error(MagicTodoReject.IdentityCorruption "ToolPartOrdinal")
        else
            Ok()

    // ── Lag-1 / Opening boundaries ─────────────────────────────────────────

    /// Accepted checkpoints derive desired lag-1 cutoff without a Requested fact.
    /// T1 has no prior → None. Tk → Before(T(k-1)).
    let desiredLag1Cutoff (acceptedInOrder: TodoWriteId list) : TodoWriteId option =
        match acceptedInOrder with
        | []
        | [ _ ] -> None
        | items -> items |> List.rev |> List.skip 1 |> List.tryHead

    let workRecordStart (openingCursor: XTraceCursor) : XTraceCursor = XTrace.nextCursor openingCursor

    /// Process-review consumption is a reviewer-knowledge frontier, not the
    /// Manager's current compression/opening floor. The first checkpoint starts
    /// immediately after the Life opening; later checkpoints continue from the
    /// exact Manager frontier durably assigned to the last concluded review.
    /// In particular, accepting the current checkpoint as T1 must not
    /// retroactively move this request's start past Before(T1).
    let managerCheckpointLwrStart
        (openingCursor: XTraceCursor)
        (latestConcludedManagerReviewFrontier: XTraceCursor option)
        : XTraceCursor =
        latestConcludedManagerReviewFrontier
        |> Option.defaultValue (workRecordStart openingCursor)

    type TracePartAnchor =
        { Cursor: XTraceCursor
          Kind: string
          ToolCallId: ToolCallId option }

    /// BlindPlan OpeningBoundary = exclusive end after constitutive T1 call+result.
    let blindPlanOpeningBoundary
        (openingCursor: XTraceCursor)
        (t1CallCursor: XTraceCursor)
        (t1ToolCallId: ToolCallId)
        (parts: TracePartAnchor list)
        : XTraceCursor =
        let afterResult =
            parts
            |> List.tryFind (fun part ->
                part.Kind = "tool_result"
                && part.Cursor.Sequence > t1CallCursor.Sequence
                && part.ToolCallId = Some t1ToolCallId)
            |> Option.map (fun part -> XTrace.nextCursor part.Cursor)

        let candidate =
            match afterResult with
            | Some boundary -> boundary
            | None -> XTrace.nextCursor t1CallCursor

        let minimum = workRecordStart openingCursor

        if candidate.Sequence > minimum.Sequence then
            candidate
        else
            minimum

    let private committedOpeningFloor
        (openingCursor: XTraceCursor)
        (t1CallCursor: XTraceCursor option)
        (t1ToolCallId: ToolCallId option)
        (parts: TracePartAnchor list)
        =
        match t1CallCursor, t1ToolCallId with
        | Some callCursor, Some callId -> Some(blindPlanOpeningBoundary openingCursor callCursor callId parts)
        | _ -> Some(workRecordStart openingCursor)

    /// Pre-T1 uses the dynamic XTrace head; post-T1 uses the constitutive T1 boundary.
    /// Accepted planning checkpoints do not close Opening.
    let effectiveOpeningFloor
        (hasOpenLife: bool)
        (planCommitted: bool)
        (openingCursor: XTraceCursor)
        (t1CallCursor: XTraceCursor option)
        (t1ToolCallId: ToolCallId option)
        (xTraceHeadSequence: int64)
        (parts: TracePartAnchor list)
        : XTraceCursor option =
        if not hasOpenLife then
            None
        elif not planCommitted then
            Some { Sequence = xTraceHeadSequence }
        else
            committedOpeningFloor openingCursor t1CallCursor t1ToolCallId parts

    let bloggerEffectiveStart (recordCoverage: RecordCoverage) (workRecordStartCursor: XTraceCursor) : XTraceCursor =
        if recordCoverage.IngestedThrough.Sequence > workRecordStartCursor.Sequence then
            recordCoverage.IngestedThrough
        else
            workRecordStartCursor

    /// First unblessed suicide must not bypass the plan commitment protocol.
    let requirePlanCommitmentBeforeFirstSuicide (planCommitted: bool) : Result<unit, MagicTodoReject> =
        if planCommitted then
            Ok()
        else
            Error MagicTodoReject.FirstSuicideWithoutCheckpoint
