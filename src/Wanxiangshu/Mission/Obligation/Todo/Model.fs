namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Trace

open System
open System.Text
open Wanxiangshu.Foundation
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
    let SemanticVersion = "magic-todo.v5"

    // ── Provider account ───────────────────────────────────────────────────

    [<RequireQualifiedAccess>]
    type ObligationHorizon =
        | Near
        | Mid
        | Far

    [<RequireQualifiedAccess>]
    module ObligationHorizon =
        let wire =
            function
            | ObligationHorizon.Near -> "near"
            | ObligationHorizon.Mid -> "mid"
            | ObligationHorizon.Far -> "far"

        let tryParse =
            function
            | "near" -> Some ObligationHorizon.Near
            | "mid" -> Some ObligationHorizon.Mid
            | "far" -> Some ObligationHorizon.Far
            | _ -> None

    /// Provider-visible owed work plus its resolution relative to the current frontier.
    type Obligation =
        { Name: string
          Horizon: ObligationHorizon
          Work: string }

    type ObligationList = Obligation list

    /// Provider-visible checkpoint input. The bool is a declaration about the
    /// road being complete enough to entrust, not a workflow stage.
    type TodoWriteInput =
        { PlanComplete: bool
          WorkingOn: string
          Obligations: ObligationList }

    let private levenshteinDistance (left: string) (right: string) : int =
        let rightCharacters = right |> Seq.toList

        let nextRow rowIndex leftCharacter previous =
            let rec build leftValue previousDiagonal previousTail characters reversed =
                match characters, previousTail with
                | rightCharacter :: remainingCharacters, previousAbove :: remainingPrevious ->
                    let insertion = leftValue + 1
                    let deletion = previousAbove + 1

                    let substitution =
                        previousDiagonal + if leftCharacter = rightCharacter then 0 else 1

                    let value = min insertion (min deletion substitution)

                    build value previousAbove remainingPrevious remainingCharacters (value :: reversed)
                | [], [] -> List.rev reversed
                | _ -> failwith "levenshtein row shape mismatch"

            match previous with
            | previousHead :: previousTail -> build rowIndex previousHead previousTail rightCharacters [ rowIndex ]
            | [] -> failwith "levenshtein requires an initial column"

        let _, finalRow =
            left
            |> Seq.fold
                (fun (rowIndex, previous) leftCharacter ->
                    let nextIndex = rowIndex + 1
                    nextIndex, nextRow nextIndex leftCharacter previous)
                (0, [ 0 .. right.Length ])

        finalRow |> List.last

    /// Canonicalise the provider's focus pointer at the input boundary.
    /// Horizon is planning resolution, not admission authority. Misspellings resolve
    /// against the whole account so bookkeeping remains available even when the
    /// provider's current decomposition is imperfect.
    let normalizeWorkingOn (workingOn: string) (items: ObligationList) : string =
        match items |> List.tryFind (fun item -> item.Name = workingOn) with
        | Some exact -> exact.Name
        | None ->
            items
            |> List.mapi (fun ordinal item -> levenshteinDistance workingOn item.Name, ordinal, item.Name)
            |> List.sortBy (fun (distance, ordinal, _) -> distance, ordinal)
            |> List.tryHead
            |> Option.map (fun (_, _, name) -> name)
            |> Option.defaultValue ""

    [<RequireQualifiedAccess>]
    type ProcessReviewVerdict =
        | Perfect
        | Revise

    module ProcessReviewVerdict =

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
        |> List.map (fun item ->
            sprintf
                """{"name":%s,"horizon":%s,"work":%s}"""
                (jsonString item.Name)
                (jsonString (ObligationHorizon.wire item.Horizon))
                (jsonString item.Work))
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

    let validateTodoWriteInput (input: TodoWriteInput) : Result<TodoWriteInput, MagicTodoReject> =
        match input.Obligations with
        | [] -> Ok { input with WorkingOn = "" }
        | items ->
            Ok
                { input with
                    WorkingOn = normalizeWorkingOn input.WorkingOn items }

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

    let workRecordStart (openingCursor: XTraceCursor) : XTraceCursor = XTraceCursor.nextCursor openingCursor

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
            |> Option.map (fun part -> XTraceCursor.nextCursor part.Cursor)

        let candidate =
            match afterResult with
            | Some boundary -> boundary
            | None -> XTraceCursor.nextCursor t1CallCursor

        let minimum = workRecordStart openingCursor

        if candidate.Sequence > minimum.Sequence then
            candidate
        else
            minimum

    /// Context compression protects only the true Opening message. Planning stage
    /// does not grant a larger raw-X prefix: pre-T1 and post-T1 both start Y at the
    /// first XTrace position after Opening. WorkRecord may still classify BlindPlan
    /// T1 material as constitutive Opening; that archival projection is independent
    /// from provider-context compression.
    let effectiveOpeningFloor
        (hasOpenLife: bool)
        (_planCommitted: bool)
        (openingCursor: XTraceCursor)
        (_t1CallCursor: XTraceCursor option)
        (_t1ToolCallId: ToolCallId option)
        (_xTraceHeadSequence: int64)
        (_parts: TracePartAnchor list)
        : XTraceCursor option =
        if not hasOpenLife then
            None
        else
            Some(workRecordStart openingCursor)

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
