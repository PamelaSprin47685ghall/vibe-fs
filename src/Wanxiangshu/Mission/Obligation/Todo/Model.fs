namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Trace

open System
open System.Text
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay

/// Magic Todo Checkpoint Protocol — GrandRewrite clean-break algebra.
///
/// Provider truth is one complete `{name, work}` obligation account plus a
/// `WorkingOn` focus pointer. Accepted checkpoints own CurrentObligations
/// immediately. No Todo status/id/progress/settlement machine exists in this module.
module MagicTodo =

    // ── Checkpoint identity ─────────────────────────────────────────────────

    /// Digest(IncumbencyId + ToolCallId). Same ToolCallId replay → same id.
    type TodoWriteId = private TodoWriteId of string

    module TodoWriteId =
        let create (value: string) = TodoWriteId value
        let value (TodoWriteId value) = value

    /// Semantic version frozen into Prepared / Accepted facts.
    [<Literal>]
    let SemanticVersion = "magic-todo.v6"

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

    // ── Admission / recovery decisions ─────────────────────────────────────

    /// Pure decision taxonomy. Host classification is stricter:
    /// - call-shape cases may become provider red text;
    /// - IdentityCorruption is an infrastructure invariant break → process fatal.
    [<RequireQualifiedAccess>]
    type MagicTodoReject =
        | MultipleTodowriteInMessage of callIds: string list
        | EmptyObligationName of ordinal: int
        | DuplicateObligationName of name: string
        | IdentityCorruption of field: string
        | FirstSuicideWithoutCheckpoint

    let todoWriteId (sha256: string -> string) (incumbencyId: IncumbencyId) (toolCallId: ToolCallId) : TodoWriteId =
        TodoWriteId.create (
            sha256 (String.concat "|" [ IncumbencyId.value incumbencyId; ToolCallId.value toolCallId; "todo-write" ])
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

    /// Multiple todowrite calls in one assistant message are supported and execute
    /// with sequential execution semantics. Same ToolCallId replay remains one physical attempt.
    let admitTodowriteBatch (toolCallIdsInMessage: ToolCallId list) : Result<unit, MagicTodoReject> =
        ignore toolCallIdsInMessage
        Ok()

    type PreparedIdentity =
        { IncumbencyId: IncumbencyId
          ProviderInputDigest: string
          BaseTodoDigest: string
          ToolPartOrdinal: int }

    /// Same TodoWriteId replay must match frozen Prepared identity fields.
    let checkPreparedReplay (expected: PreparedIdentity) (observed: PreparedIdentity) : Result<unit, MagicTodoReject> =
        if expected.IncumbencyId <> observed.IncumbencyId then
            Error(MagicTodoReject.IdentityCorruption "IncumbencyId")
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
                && XTraceCursor.isAfter part.Cursor t1CallCursor
                && part.ToolCallId = Some t1ToolCallId)
            |> Option.map (fun part -> XTraceCursor.nextCursor part.Cursor)

        let candidate =
            match afterResult with
            | Some boundary -> boundary
            | None -> XTraceCursor.nextCursor t1CallCursor

        let minimum = workRecordStart openingCursor

        if XTraceCursor.isAfter candidate minimum then
            candidate
        else
            minimum

    /// Compression protects only the true Life Opening. BlindPlan/T1 may extend
    /// the WorkRecord's constitutive Opening material, but it must never enlarge
    /// the X→Y compression floor (CONTEXT-COMPRESSION-017).
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
        let ingestedThrough = RecordCoverage.ingestedThrough recordCoverage

        if XTraceCursor.isAfter ingestedThrough workRecordStartCursor then
            ingestedThrough
        else
            workRecordStartCursor

    /// First unblessed suicide must not bypass the plan commitment protocol.
    let requirePlanCommitmentBeforeFirstSuicide (planCommitted: bool) : Result<unit, MagicTodoReject> =
        if planCommitted then
            Ok()
        else
            Error MagicTodoReject.FirstSuicideWithoutCheckpoint
