namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Kernel.Fact

/// Provider-facing surfaces for Magic Todo (guideline, schema, compatibility,
/// enriched tool result). Rendered by MagicTodoHostCodec via the membrane hooks.
/// Prose meaning lives in `resources/provider/lifecycle/magic-todo/**` (PROMPT-019).
module MagicTodoSurface =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let ManagerGuideline = "lifecycle/magic-todo/manager-guideline"

        [<Literal>]
        let TodoWriteDescription = "lifecycle/magic-todo/todowrite-description"

        [<Literal>]
        let ProcessReviewerPreamble = "lifecycle/magic-todo/process-reviewer-preamble"

        [<Literal>]
        let PreviousNone = "lifecycle/magic-todo/previous-none"

        [<Literal>]
        let PreviousReviewBody = "lifecycle/magic-todo/previous-review-body"

        [<Literal>]
        let ObligationWriteResult = "lifecycle/magic-todo/obligation-write-result"

        [<Literal>]
        let ObligationAcceptedEpilogue = "lifecycle/magic-todo/obligation-accepted-epilogue"

        [<Literal>]
        let EnrichedWriteResult = "lifecycle/magic-todo/enriched-write-result"

        [<Literal>]
        let EnrichedReviseNotes = "lifecycle/magic-todo/enriched-revise-notes"

        [<Literal>]
        let EnrichedReviewingEpilogue = "lifecycle/magic-todo/enriched-reviewing-epilogue"

    /// Whether the Magic Todo Manager fragment should be projected.
    let shouldProjectManagerGuideline (canonicalRole: string) (todowriteProviderVisible: bool) : bool =
        canonicalRole = "Manager" && todowriteProviderVisible

    /// JSON Schema fragment for tool.definition parameters / jsonSchema (both must update).
    let todoWriteJsonSchema: string =
        """{
  "type": "object",
  "additionalProperties": false,
  "required": ["obligations"],
  "properties": {
    "obligations": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["name", "work"],
        "properties": {
          "name": { "type": "string", "minLength": 1 },
          "work": { "type": "string" }
        }
      }
    }
  }
}"""

    // ── Compatibility sink (§14) ───────────────────────────────────────────

    /// Host TodoTable row: no stable id.
    type CompatibilityTodoRow =
        { Content: string
          Status: string
          Priority: string }

    [<RequireQualifiedAccess>]
    type ReviewingSinkStrategy =
        /// Host / UI tolerate the fifth status string.
        | PreserveReviewing
        /// Compatibility-only downgrade; canonical status stays reviewing.
        | DowngradeToInProgress

    let compatibilityStatus (strategy: ReviewingSinkStrategy) (status: TodoStatus) : string =
        match status, strategy with
        | TodoStatus.Reviewing, ReviewingSinkStrategy.DowngradeToInProgress -> "in_progress"
        | other, _ -> TodoStatus.wire other

    /// Strip historical identity/progress fields before the builtin executor.
    let toCompatibilityRows (strategy: ReviewingSinkStrategy) (items: MagicTodoList) : CompatibilityTodoRow list =
        items
        |> List.map (fun item ->
            { Content = item.Content
              Status = compatibilityStatus strategy item.Status
              Priority = item.Priority })

    /// GrandRewrite provider obligations projected into the Host's legacy TodoTable.
    /// The sink is optimistic UI state only; these fields never round-trip into
    /// canonical truth (TODO-007).
    let obligationsToCompatibilityRows (items: ObligationList) : CompatibilityTodoRow list =
        items
        |> List.map (fun item ->
            { Content = item.Name + ": " + item.Work
              Status = "in_progress"
              Priority = "medium" })

    type RawObligationFields =
        { Name: string option
          Work: string option }

    let decodeObligation (raw: RawObligationFields) : Obligation =
        { Name = defaultArg raw.Name ""
          Work = defaultArg raw.Work "" }

    let decodeObligations (rows: RawObligationFields list) : ObligationList = rows |> List.map decodeObligation

    // ── Tagged input decode — historical recovery compatibility only ──────

    /// Minimal structural decode of one provider item object fields.
    /// Caller supplies already-parsed field map (Host JSON layer).
    type RawTodoFields =
        { Kind: string option
          Id: string option
          Content: string option
          Status: string option
          Priority: string option }

    let decodeInputItem (raw: RawTodoFields) : Result<MagicTodoInputItem, MagicTodoReject> =
        match raw.Kind with
        | None -> Error MagicTodoReject.MissingKind
        | Some "existing" ->
            match raw.Id with
            | None
            | Some "" -> Error MagicTodoReject.ExistingMissingId
            | Some idText ->
                match raw.Status |> Option.bind TodoStatus.parse with
                | None -> Error(MagicTodoReject.UnknownStatus(defaultArg raw.Status ""))
                | Some status ->
                    Ok(
                        MagicTodoInputItem.Existing(
                            TodoItemId.create idText,
                            defaultArg raw.Content "",
                            status,
                            defaultArg raw.Priority ""
                        )
                    )
        | Some "new" ->
            match raw.Id with
            | Some _ -> Error MagicTodoReject.NewCarriesId
            | None ->
                match raw.Status |> Option.bind TodoStatus.parse with
                | None -> Error(MagicTodoReject.UnknownStatus(defaultArg raw.Status ""))
                | Some status ->
                    Ok(MagicTodoInputItem.New(defaultArg raw.Content "", status, defaultArg raw.Priority ""))
        | Some _ -> Error MagicTodoReject.MissingKind

    let decodeInputItems (rows: RawTodoFields list) : Result<MagicTodoInputItem list, MagicTodoReject> =
        let rec loop remaining acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | head :: tail ->
                match decodeInputItem head with
                | Error e -> Error e
                | Ok item -> loop tail (item :: acc)

        loop rows []

    // ── Canonical list wire (tool result / blob body) ──────────────────────

    let renderListWire (items: MagicTodoList) : string = MagicTodo.canonicalListWire items

    let renderObligationListWire (items: ObligationList) : string =
        MagicTodo.canonicalObligationListWire items

    type PreviousReviewView =
        { Verdict: ProcessReviewVerdict
          ReportText: string }

    type ObligationWriteResult =
        { Previous: PreviousReviewView option
          Current: ObligationList
          Submitted: ObligationList
          Accepted: bool }

    let previousReviewSubs (verdictWire: string) (reportText: string) : Map<string, string> =
        Map [ "verdict", verdictWire; "report", reportText ]

    let obligationWriteSubs
        (previousBody: string)
        (currentWire: string)
        (submittedWire: string)
        (acceptedEpilogue: string)
        : Map<string, string> =
        Map
            [ "previous_body", previousBody
              "current_wire", currentWire
              "submitted_wire", submittedWire
              "accepted_epilogue", acceptedEpilogue ]

    let enrichedReviseSubs (revisePreviewWire: string) : Map<string, string> =
        Map [ "revise_preview_wire", revisePreviewWire ]

    let enrichedWriteSubs
        (previousBody: string)
        (settledWire: string)
        (submittedWire: string)
        (reviseNotes: string)
        (reviewingEpilogue: string)
        : Map<string, string> =
        Map
            [ "previous_body", previousBody
              "settled_wire", settledWire
              "submitted_wire", submittedWire
              "revise_notes", reviseNotes
              "reviewing_epilogue", reviewingEpilogue ]

    type EnrichedTodoWriteResult =
        { Previous: PreviousReviewView option
          SettledCurrent: MagicTodoList
          Submitted: MagicTodoList
          RevisePreview: MagicTodoList }

    /// Build enriched view: previous consumable review + Ck + Pk + merge preview.
    let buildEnrichedResult
        (previous: PreviousReviewView option)
        (settledCurrent: MagicTodoList)
        (submitted: MagicTodoList)
        : EnrichedTodoWriteResult =
        { Previous = previous
          SettledCurrent = settledCurrent
          Submitted = submitted
          RevisePreview = MagicTodo.semanticMerge settledCurrent submitted }

    /// Process reviewer assignment body: preamble already localized by caller.
    let renderAssignmentUserMessage (preamble: string) (sections: string list) : string =
        String.concat "\n\n" (preamble :: sections)

    /// GLORY-030 relaxation boundary: Manager may see process PERFECT/REVISE
    /// outcome + concrete ProcessReviewLWR report; never reviewer identity /
    /// session / barrier / witness / 2N / confirmation mechanics.
    let managerMaySeeProcessReviewOutcome = true
