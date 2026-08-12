namespace Wanxiangshu.Domain

open System
open System.Text
open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Kernel.Fact

/// Provider-facing surfaces for Magic Todo (guideline, schema, compatibility,
/// enriched tool result). Speculative / unwired — Host hooks not attached yet.
module MagicTodoSurface =

    // ── Manager-only guideline fragment (§6) ───────────────────────────────

    /// Appended only when canonical role = Manager AND todowrite is provider-visible.
    /// Must NOT be merged into global PairProgrammingGuidelineText.
    [<Literal>]
    let MagicTodoManagerGuideline =
        "Keep the mission's living obligations truthful with todowrite.\n\
         \n\
         Planning and execution are one continuous activity. Do not stop for a\n\
         separate planning-only phase.\n\
         \n\
         Each call replaces the whole obligation account with\n\
         obligations: [{ name, work }]. Keep an obligation while it is still owed;\n\
         remove it only when the work has actually discharged it. Keep each name\n\
         stable while that obligation remains alive.\n\
         \n\
         Update todowrite whenever the truthful decomposition, discovered work,\n\
         or discharged work has materially changed.\n\
         \n\
         Each accepted call synchronizes the preceding checkpoint review and\n\
         starts the next checkpoint review. Do not emit multiple todowrite calls\n\
         in the same assistant message; any such batch is rejected entirely."

    /// Whether the Magic Todo Manager fragment should be projected.
    let shouldProjectManagerGuideline (canonicalRole: string) (todowriteProviderVisible: bool) : bool =
        canonicalRole = "Manager" && todowriteProviderVisible

    // ── Tool definition overlay (§10) ──────────────────────────────────────

    /// Provider-visible description. No dedicated reviewer / barrier / witness / 2N.
    [<Literal>]
    let TodoWriteDefinitionDescription =
        "Replace the mission's entire living obligation account. Each obligation is\n\
         {\"name\":\"stable human-readable name\",\"work\":\"what is still owed\"}.\n\
         Keep an obligation while it remains owed and remove it only after the work\n\
         has actually discharged it. Names must be non-empty and unique within one\n\
         submitted account. Each accepted call synchronizes the preceding process\n\
         review and starts the next checkpoint review. Do not emit multiple\n\
         todowrite calls in the same assistant message; the whole batch is rejected."

    [<Literal>]
    let TodoWriteDefinitionDescriptionZhCn =
        "替换 mission 的完整 living obligation account。每个 obligation 使用 {\"name\":\"稳定且可读的名称\",\"work\":\"仍然欠下的工作\"}。只要 obligation 仍未解除就保留它；只有真实工作已经完成该义务后才移除。一次提交中的 name 必须非空且唯一。每次 accepted call 会同步前一个 process review，并启动下一次 checkpoint review。同一个 assistant message 不得发出多个 todowrite call；出现时整批拒绝。"

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

    let renderObligationWriteResult (view: ObligationWriteResult) : string =
        let sb = StringBuilder()
        sb.AppendLine("Previous checkpoint review:") |> ignore

        match view.Previous with
        | None -> sb.AppendLine("None — this is the first checkpoint.") |> ignore
        | Some prev ->
            sb.Append("Verdict: ").AppendLine(ProcessReviewVerdict.wire prev.Verdict)
            |> ignore

            sb.AppendLine() |> ignore
            sb.AppendLine("Report:") |> ignore
            sb.AppendLine(prev.ReportText) |> ignore

        sb.AppendLine() |> ignore
        sb.AppendLine("Current obligations:") |> ignore
        sb.AppendLine(renderObligationListWire view.Current) |> ignore
        sb.AppendLine() |> ignore
        sb.AppendLine("Submitted obligations:") |> ignore
        sb.AppendLine(renderObligationListWire view.Submitted) |> ignore

        if view.Accepted then
            sb.AppendLine() |> ignore

            sb.AppendLine("This obligation checkpoint was accepted and is now under process review.")
            |> ignore

            sb.AppendLine("Continue useful independent work; the next todowrite will synchronize the preceding review.")
            |> ignore

        sb.ToString()

    // ── Enriched historical tool result — recovery compatibility only ─────

    type EnrichedTodoWriteResult =
        { Previous: PreviousReviewView option
          SettledCurrent: MagicTodoList
          Submitted: MagicTodoList
          RevisePreview: MagicTodoList }

    let renderEnrichedResult (view: EnrichedTodoWriteResult) : string =
        let sb = StringBuilder()

        sb.AppendLine("Previous checkpoint review:") |> ignore

        match view.Previous with
        | None -> sb.AppendLine("None — this is the first checkpoint.") |> ignore
        | Some prev ->
            sb.Append("Verdict: ").AppendLine(ProcessReviewVerdict.wire prev.Verdict)
            |> ignore

            sb.AppendLine() |> ignore
            sb.AppendLine("Report:") |> ignore
            sb.AppendLine(prev.ReportText) |> ignore

        sb.AppendLine() |> ignore
        sb.AppendLine("Settled current todo list:") |> ignore
        sb.AppendLine(renderListWire view.SettledCurrent) |> ignore
        sb.AppendLine() |> ignore
        sb.AppendLine("Submitted todo list:") |> ignore
        sb.AppendLine(renderListWire view.Submitted) |> ignore
        sb.AppendLine() |> ignore
        sb.AppendLine("If THIS checkpoint later receives REVISE,") |> ignore
        sb.AppendLine("the next settled todo list will be:") |> ignore
        sb.AppendLine(renderListWire view.RevisePreview) |> ignore
        sb.AppendLine() |> ignore
        sb.AppendLine("IMPORTANT:") |> ignore
        sb.AppendLine("The list above is only the REVISE preview.") |> ignore
        sb.AppendLine("If this checkpoint receives PERFECT,") |> ignore

        sb.AppendLine("your submitted todo list will replace the settled list exactly.")
        |> ignore

        sb.AppendLine() |> ignore
        sb.AppendLine("This checkpoint is now being reviewed.") |> ignore
        sb.AppendLine("Continue useful independent next-stage work.") |> ignore

        sb.AppendLine("Your next todowrite call will synchronize with this review if necessary.")
        |> ignore

        sb.ToString()

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

    // ── Process reviewer instruction seed (§20) — typed RequestKind later ─

    [<Literal>]
    let ProcessReviewerInstructionPreamble =
        "You are reviewing the ongoing quality and truthfulness of a work process.\n\
         \n\
         You receive:\n\
         - the original task authority (OpeningRaw);\n\
         - a frontier-bounded lifecycle work record for this checkpoint;\n\
         - the settled old todo list;\n\
         - the proposed todo list.\n\
         \n\
         Reply with exactly one verdict tool call: PERFECT or REVISE.\n\
         Process PERFECT is not a terminal Finality witness."

    /// GLORY-030 relaxation boundary: Manager may see process PERFECT/REVISE
    /// outcome + concrete ProcessReviewLWR report; never reviewer identity /
    /// session / barrier / witness / 2N / confirmation mechanics.
    let managerMaySeeProcessReviewOutcome = true
