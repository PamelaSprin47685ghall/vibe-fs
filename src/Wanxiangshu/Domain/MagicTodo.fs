namespace Wanxiangshu.Domain

open System
open System.Text
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// Magic Todo Checkpoint Protocol — pure algebra (see Magic Todo change proposal).
///
/// Speculative / unwired: not yet hooked into tool.execute.before/after or Manager
/// lifecycle. Keep illegal states unrepresentable; recovery reads durable facts, not
/// Stages / bools / wall-clock (ARCH-001, protocol §17).
///
/// Coverage split (protocol SSOT):
///   Process review → RecordCoverage / LWR (RawGap allowed)
///   Lag-1 rebase   → PrefixCoverage / proven Y only (RawGap forbidden)
module MagicTodo =

    // ── Identity wrappers (protocol §7.2 / §11.2 / §18) ─────────────────────

    /// Digest(ManagerLifeId + ToolCallId). Same ToolCallId replay → same id.
    type TodoWriteId = private TodoWriteId of string

    /// Digest(ManagerLifeId + TodoWriteToolCallId + newItemOrdinal).
    type TodoItemId = private TodoItemId of string

    /// Digest(ManagerLifeId + TodoWriteId). One obligation per Accepted checkpoint.
    type TodoReviewId = private TodoReviewId of string

    /// Logical dedicated process-reviewer identity for one Manager Life.
    type DedicatedReviewerId = private DedicatedReviewerId of string

    module TodoWriteId =
        let create (value: string) = TodoWriteId value
        let value (TodoWriteId v) = v

    module TodoItemId =
        let create (value: string) = TodoItemId value
        let value (TodoItemId v) = v

    module TodoReviewId =
        let create (value: string) = TodoReviewId value
        let value (TodoReviewId v) = v

    module DedicatedReviewerId =
        let create (value: string) = DedicatedReviewerId value
        let value (DedicatedReviewerId v) = v

    /// Protocol semantic version frozen into Prepared / Accepted facts.
    [<Literal>]
    let SemanticVersion = "magic-todo.v1"

    // ── Status algebra (§8) ────────────────────────────────────────────────

    [<RequireQualifiedAccess>]
    type TodoStatus =
        | Pending
        | InProgress
        | Reviewing
        | Completed
        | Cancelled

    module TodoStatus =

        let parse (raw: string) : TodoStatus option =
            match raw with
            | "pending" -> Some TodoStatus.Pending
            | "in_progress" -> Some TodoStatus.InProgress
            | "reviewing" -> Some TodoStatus.Reviewing
            | "completed" -> Some TodoStatus.Completed
            | "cancelled" -> Some TodoStatus.Cancelled
            | _ -> None

        let wire (status: TodoStatus) : string =
            match status with
            | TodoStatus.Pending -> "pending"
            | TodoStatus.InProgress -> "in_progress"
            | TodoStatus.Reviewing -> "reviewing"
            | TodoStatus.Completed -> "completed"
            | TodoStatus.Cancelled -> "cancelled"

        /// Productive progress chain ranks. `Cancelled` is a disposition, not a rank.
        let tryProgressRank (status: TodoStatus) : int option =
            match status with
            | TodoStatus.Pending -> Some 0
            | TodoStatus.InProgress -> Some 1
            | TodoStatus.Reviewing -> Some 2
            | TodoStatus.Completed -> Some 3
            | TodoStatus.Cancelled -> None

        let isProductive (status: TodoStatus) = tryProgressRank status |> Option.isSome

    // ── Item shapes (§7) ───────────────────────────────────────────────────

    /// Tagged provider input — structurally unconfusable; no optional id guessing.
    [<RequireQualifiedAccess>]
    type MagicTodoInputItem =
        | Existing of id: TodoItemId * content: string * status: TodoStatus * priority: string
        | New of content: string * status: TodoStatus * priority: string

    /// Canonical / settled / submitted item always carries a stable Host-assigned id.
    type MagicTodoItem =
        { Id: TodoItemId
          Content: string
          Status: TodoStatus
          Priority: string }

    type MagicTodoList = MagicTodoItem list

    /// GrandRewrite provider account. These are the only list fields that may
    /// cross the provider horizon for a new Magic Todo checkpoint (TODO-002).
    /// The older TodoItem/status representation remains an internal recovery /
    /// compatibility shape only and is never decoded from new provider input.
    type Obligation = { Name: string; Work: string }

    type ObligationList = Obligation list

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

    // ── Rejection taxonomy (fail closed; no Prepared/Accepted) ─────────────

    /// DSL-class: Decision — fail-closed refusal taxonomy for TodoWrite admission
    /// and settle (no Prepared/Accepted durable states live here).
    [<RequireQualifiedAccess>]
    type MagicTodoReject =
        | MissingKind
        | ExistingMissingId
        | NewCarriesId
        | UnknownStatus of raw: string
        | DuplicateId of id: string
        | UnknownExistingId of id: string
        | IllegalCompletedTransition of id: string * fromStatus: string * toStatus: string
        | NewItemCompleted of ordinal: int
        | MultipleTodowriteInMessage of callIds: string list
        | EmptyObligationName of ordinal: int
        | DuplicateObligationName of name: string
        | IdentityCorruption of field: string
        | NoOpenManagerLife
        /// Lag-1 wait signal (TODO-006): T(k+1) / suicide must block until
        /// ConsumableReview ≡ TodoReviewConcluded is durable. Not a fail-closed
        /// provider reject and not invalidOp red text.
        | AwaitingConsumableReview of pendingTodoWriteId: string
        | FirstSuicideWithoutCheckpoint

    // ── Identity digests (§7.2 / §11.2 / §18) ───────────────────────────────

    /// TodoWriteId = digest(ManagerLifeId + ToolCallId)
    let todoWriteId (sha256: string -> string) (lifeId: ManagerLifeId) (toolCallId: ToolCallId) : TodoWriteId =
        TodoWriteId.create (
            sha256 (String.concat "|" [ ManagerLifeId.value lifeId; ToolCallId.value toolCallId; "todo-write" ])
        )

    /// TodoItemId = digest(ManagerLifeId + ToolCallId + newItemOrdinal)
    let todoItemId
        (sha256: string -> string)
        (lifeId: ManagerLifeId)
        (toolCallId: ToolCallId)
        (newItemOrdinal: int)
        : TodoItemId =
        TodoItemId.create (
            sha256 (
                String.concat
                    "|"
                    [ ManagerLifeId.value lifeId
                      ToolCallId.value toolCallId
                      string newItemOrdinal
                      "todo-item" ]
            )
        )

    /// TodoReviewId = digest(ManagerLifeId + TodoWriteId)
    let todoReviewId (sha256: string -> string) (lifeId: ManagerLifeId) (writeId: TodoWriteId) : TodoReviewId =
        TodoReviewId.create (
            sha256 (String.concat "|" [ ManagerLifeId.value lifeId; TodoWriteId.value writeId; "todo-review" ])
        )

    /// Logical dedicated reviewer id for the Life (stable across physical replacement).
    let dedicatedReviewerId (sha256: string -> string) (lifeId: ManagerLifeId) : DedicatedReviewerId =
        DedicatedReviewerId.create (
            sha256 (String.concat "|" [ ManagerLifeId.value lifeId; "dedicated-todo-reviewer" ])
        )

    let private jsonString (value: string) =
        let builder = StringBuilder(value.Length + 8)
        builder.Append('"') |> ignore

        for character in value do
            match character with
            | '"' -> builder.Append("\\\"") |> ignore
            | '\\' -> builder.Append("\\\\") |> ignore
            | '\n' -> builder.Append("\\n") |> ignore
            | '\r' -> builder.Append("\\r") |> ignore
            | '\t' -> builder.Append("\\t") |> ignore
            | _ when int character < 0x20 -> builder.Append(sprintf "\\u%04x" (int character)) |> ignore
            | _ -> builder.Append(character) |> ignore

        builder.Append('"') |> ignore
        builder.ToString()

    /// Canonical durable body for BaseTodo / ProposedTodo / SettledCurrent blobs.
    /// Its SHA-256 is therefore the one digest carried by the corresponding fact.
    let canonicalListWire (items: MagicTodoList) : string =
        items
        |> List.map (fun item ->
            sprintf
                """{"id":%s,"content":%s,"status":%s,"priority":%s}"""
                (jsonString (TodoItemId.value item.Id))
                (jsonString item.Content)
                (jsonString (TodoStatus.wire item.Status))
                (jsonString item.Priority))
        |> String.concat ","
        |> sprintf "[%s]"

    /// Canonical list digest for BaseTodoDigest / ProposedTodoDigest / identity checks.
    let listDigest (sha256: string -> string) (items: MagicTodoList) : string = canonicalListWire items |> sha256

    /// Byte-stable provider account body. Object field order is frozen here so
    /// Journal blob digests and replay identity do not depend on JS object order.
    let canonicalObligationListWire (items: ObligationList) : string =
        items
        |> List.map (fun item -> sprintf """{"name":%s,"work":%s}""" (jsonString item.Name) (jsonString item.Work))
        |> String.concat ","
        |> sprintf "[%s]"

    let obligationListDigest (sha256: string -> string) (items: ObligationList) : string =
        canonicalObligationListWire items |> sha256

    /// Provider-visible identity is the stable human-readable obligation name.
    /// No status/id/priority state machine crosses the horizon (TODO-002/003).
    let validateObligations (items: ObligationList) : Result<ObligationList, MagicTodoReject> =
        let rec loop ordinal seen remaining =
            match remaining with
            | [] -> Ok items
            | head :: tail when String.IsNullOrWhiteSpace head.Name ->
                Error(MagicTodoReject.EmptyObligationName ordinal)
            | head :: _ when Set.contains head.Name seen -> Error(MagicTodoReject.DuplicateObligationName head.Name)
            | head :: tail -> loop (ordinal + 1) (Set.add head.Name seen) tail

        loop 0 Set.empty items

    /// CurrentObligations settlement (TODO-005): PERFECT promotes the submitted
    /// account exactly; REVISE keeps the prior settled account until the Manager
    /// rewrites it after consuming the canonical process-review report.
    let settleObligations
        (old: ObligationList)
        (proposed: ObligationList)
        (verdict: ProcessReviewVerdict)
        : ObligationList =
        match verdict with
        | ProcessReviewVerdict.Perfect -> proposed
        | ProcessReviewVerdict.Revise -> old

    // ── Completed gate (§8.1) — historical compatibility model ─────────────

    /// `old != completed AND proposed == completed` → old must be exactly reviewing.
    /// New items may never enter as completed.
    let validateCompletedGate (oldStatus: TodoStatus option) (proposed: TodoStatus) : bool =
        match proposed with
        | TodoStatus.Completed ->
            match oldStatus with
            | None -> false
            | Some TodoStatus.Reviewing
            | Some TodoStatus.Completed -> true
            | Some _ -> false
        | _ -> true

    // ── Normalize + validate provider input against Ck (§7 / §8 / §11) ─────

    /// Allocate new ids, validate existing ids / uniqueness / completed gate.
    /// On success returns normalized proposed list (all items have stable ids).
    let normalizeProposed
        (sha256: string -> string)
        (lifeId: ManagerLifeId)
        (toolCallId: ToolCallId)
        (old: MagicTodoList)
        (inputs: MagicTodoInputItem list)
        : Result<MagicTodoList, MagicTodoReject> =
        let oldById =
            old |> List.map (fun item -> TodoItemId.value item.Id, item) |> Map.ofList

        let rec loop
            (remaining: MagicTodoInputItem list)
            (newOrdinal: int)
            (acc: MagicTodoItem list)
            (seen: Set<string>)
            =
            match remaining with
            | [] -> Ok(List.rev acc)
            | MagicTodoInputItem.Existing(id, content, status, priority) :: rest ->
                let idText = TodoItemId.value id

                if Set.contains idText seen then
                    Error(MagicTodoReject.DuplicateId idText)
                else
                    match Map.tryFind idText oldById with
                    | None -> Error(MagicTodoReject.UnknownExistingId idText)
                    | Some oldItem ->
                        if not (validateCompletedGate (Some oldItem.Status) status) then
                            Error(
                                MagicTodoReject.IllegalCompletedTransition(
                                    idText,
                                    TodoStatus.wire oldItem.Status,
                                    TodoStatus.wire status
                                )
                            )
                        else
                            let item =
                                { Id = id
                                  Content = content
                                  Status = status
                                  Priority = priority }

                            loop rest newOrdinal (item :: acc) (Set.add idText seen)
            | MagicTodoInputItem.New(content, status, priority) :: rest ->
                if status = TodoStatus.Completed then
                    Error(MagicTodoReject.NewItemCompleted newOrdinal)
                else
                    let id = todoItemId sha256 lifeId toolCallId newOrdinal
                    let idText = TodoItemId.value id

                    if Set.contains idText seen then
                        Error(MagicTodoReject.DuplicateId idText)
                    else
                        let item =
                            { Id = id
                              Content = content
                              Status = status
                              Priority = priority }

                        loop rest (newOrdinal + 1) (item :: acc) (Set.add idText seen)

        loop inputs 0 [] Set.empty

    // ── semanticMerge (§9) ─────────────────────────────────────────────────

    /// Conservative cancelled merge: disagreeing sides where either is cancelled
    /// keep old.status. Both cancelled → cancelled. Both productive → minProgress.
    let private mergeStatus (oldStatus: TodoStatus) (proposedStatus: TodoStatus) : TodoStatus =
        match oldStatus, proposedStatus with
        | TodoStatus.Cancelled, TodoStatus.Cancelled -> TodoStatus.Cancelled
        | a, b when a <> b && (a = TodoStatus.Cancelled || b = TodoStatus.Cancelled) -> oldStatus
        | a, b ->
            match TodoStatus.tryProgressRank a, TodoStatus.tryProgressRank b with
            | Some ra, Some rb ->
                let rank = min ra rb

                match rank with
                | 0 -> TodoStatus.Pending
                | 1 -> TodoStatus.InProgress
                | 2 -> TodoStatus.Reviewing
                | _ -> TodoStatus.Completed
            | _ -> oldStatus

    /// REVISE merge: union by id; status = conservative min; content/priority = proposed.
    /// Order: old-only items keep relative old order first, then proposed-only in
    /// proposed order; intersection items appear at proposed position with merged fields.
    let semanticMerge (old: MagicTodoList) (proposed: MagicTodoList) : MagicTodoList =
        let oldById =
            old |> List.map (fun item -> TodoItemId.value item.Id, item) |> Map.ofList

        let proposedIds =
            proposed |> List.map (fun item -> TodoItemId.value item.Id) |> Set.ofList

        let oldOnly =
            old
            |> List.filter (fun item -> not (Set.contains (TodoItemId.value item.Id) proposedIds))

        let mergedProposed =
            proposed
            |> List.map (fun p ->
                match Map.tryFind (TodoItemId.value p.Id) oldById with
                | None -> p
                | Some o ->
                    { Id = p.Id
                      Content = p.Content
                      Priority = p.Priority
                      Status = mergeStatus o.Status p.Status })

        oldOnly @ mergedProposed

    /// Settlement (§4): PERFECT = full replace; REVISE = semanticMerge.
    let settle (old: MagicTodoList) (proposed: MagicTodoList) (verdict: ProcessReviewVerdict) : MagicTodoList =
        match verdict with
        | ProcessReviewVerdict.Perfect -> proposed
        | ProcessReviewVerdict.Revise -> semanticMerge old proposed

    // ── Same-message admission (§11.1) ─────────────────────────────────────

    /// >1 distinct ToolCallId todowrite in one assistant message → all fail closed.
    /// No ordinal winner arbitration.
    let admitTodowriteBatch (toolCallIdsInMessage: ToolCallId list) : Result<unit, MagicTodoReject> =
        let distinct = toolCallIdsInMessage |> List.map ToolCallId.value |> List.distinct

        if List.length distinct > 1 then
            Error(MagicTodoReject.MultipleTodowriteInMessage distinct)
        else
            Ok()

    // ── Prepared replay identity (§11.2.1) ─────────────────────────────────

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

    // ── Lag-1 desired cutoff (§16.7) ───────────────────────────────────────

    /// Accepted checkpoints derive desired lag-1 cutoff without a Requested fact.
    /// T1 has no prior → None (no replacement). Tk → Before(T(k-1)).
    let desiredLag1Cutoff (acceptedInOrder: TodoWriteId list) : TodoWriteId option =
        match acceptedInOrder with
        | []
        | [ _ ] -> None
        | xs ->
            // Before latest = the previous Accepted id (covered-before trigger).
            xs |> List.rev |> List.skip 1 |> List.tryHead

    /// Immediate-policy WorkRecordStart = Opening exclusive end after InitialCharge.
    let workRecordStart (openingCursor: XTraceCursor) : XTraceCursor = XTrace.nextCursor openingCursor

    /// One XTrace part locator for BlindPlan OpeningBoundary derivation.
    type TracePartAnchor =
        { Cursor: XTraceCursor
          Kind: string
          ToolCallId: ToolCallId option }

    /// BlindPlan OpeningBoundary = exclusive end after constitutive T1 call+result.
    /// Minimal correct nail: max(OpeningCursor+1, end after T1 result when present,
    /// else end after T1 call). Never reads ProtectedPrefixEnd.
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

    /// Production Blogger / Companion Opening floor (TODO-001 / GLORY-074).
    ///
    /// Pre-T1: dynamic head — protect the whole unclosed Opening; no durable nail.
    /// Post-T1: WorkRecordStart = OpeningBoundary after T1 call+result.
    /// No CurrentLife → None. Never reads ProtectedPrefixEnd / WorkActivated.
    let effectiveOpeningFloor
        (hasOpenLife: bool)
        (todoWriteAcceptedCount: int)
        (openingCursor: XTraceCursor)
        (t1CallCursor: XTraceCursor option)
        (t1ToolCallId: ToolCallId option)
        (xTraceHeadSequence: int64)
        (parts: TracePartAnchor list)
        : XTraceCursor option =
        if not hasOpenLife then
            None
        elif todoWriteAcceptedCount < 1 then
            Some { Sequence = xTraceHeadSequence }
        else
            match t1CallCursor, t1ToolCallId with
            | Some callCursor, Some callId -> Some(blindPlanOpeningBoundary openingCursor callCursor callId parts)
            | _ -> Some(workRecordStart openingCursor)

    /// Manager Blogger effectiveStart = max(RecordCoverage, Life.WorkRecordStart).
    let bloggerEffectiveStart (recordCoverage: RecordCoverage) (workRecordStartCursor: XTraceCursor) : XTraceCursor =
        if recordCoverage.IngestedThrough.Sequence > workRecordStartCursor.Sequence then
            recordCoverage.IngestedThrough
        else
            workRecordStartCursor

    // ── First unblessed suicide gate (§0 / §21) ────────────────────────────

    /// Zero TodoWriteAccepted → first unblessed suicide fail closed.
    let requireCheckpointBeforeFirstSuicide (acceptedCount: int) : Result<unit, MagicTodoReject> =
        if acceptedCount < 1 then
            Error MagicTodoReject.FirstSuicideWithoutCheckpoint
        else
            Ok()
