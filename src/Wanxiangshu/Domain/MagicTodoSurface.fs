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
        "Keep the todo list continuously accurate with todowrite.\n\
         \n\
         Planning and execution are one continuous activity.\n\
         Do not stop for a separate planning-only phase.\n\
         \n\
         Update todowrite whenever the truthful decomposition, discovered work,\n\
         or progress has materially changed.\n\
         \n\
         For every previously returned todo, submit kind:\"existing\" with that exact id.\n\
         For a genuinely new todo, submit kind:\"new\" and omit id.\n\
         \n\
         A todo must pass through reviewing before it can become completed.\n\
         \n\
         While preceding work is being reviewed, continue useful independent\n\
         next-stage work. Do not idle merely waiting for that review.\n\
         \n\
         Each accepted todowrite synchronizes the preceding checkpoint review\n\
         and starts the next checkpoint review.\n\
         Do not emit multiple todowrite calls in the same assistant message;\n\
         any such batch is rejected entirely."

    /// Whether the Magic Todo Manager fragment should be projected.
    let shouldProjectManagerGuideline (canonicalRole: string) (todowriteProviderVisible: bool) : bool =
        canonicalRole = "Manager" && todowriteProviderVisible

    // ── Tool definition overlay (§10) ──────────────────────────────────────

    /// Provider-visible description. No dedicated reviewer / barrier / witness / 2N.
    [<Literal>]
    let TodoWriteDefinitionDescription =
        "Replace the entire todo list with a tagged Magic Todo V2 payload.\n\
         \n\
         Each item is either:\n\
         - {\"kind\":\"existing\",\"id\":\"…\",\"content\":\"…\",\"status\":\"…\",\"priority\":\"…\"}\n\
           Reuse the exact id previously returned for that todo.\n\
         - {\"kind\":\"new\",\"content\":\"…\",\"status\":\"…\",\"priority\":\"…\"}\n\
           Omit id; the Host assigns a stable id in the tool result.\n\
         \n\
         Status values: pending | in_progress | reviewing | completed | cancelled.\n\
         A todo must be reviewing before it can become completed\n\
         (completed→completed is allowed; pending/in_progress/new→completed is rejected).\n\
         \n\
         Keep the list continuously accurate. Each accepted call synchronizes the\n\
         preceding checkpoint's process review (PERFECT or REVISE) and starts the\n\
         next checkpoint review. Do not emit multiple todowrite calls in the same\n\
         assistant message — any such batch is rejected entirely."

    /// JSON Schema fragment for tool.definition parameters / jsonSchema (both must update).
    let todoWriteJsonSchema: string =
        """{
  "type": "object",
  "additionalProperties": false,
  "required": ["todos"],
  "properties": {
    "todos": {
      "type": "array",
      "items": {
        "oneOf": [
          {
            "type": "object",
            "additionalProperties": false,
            "required": ["kind", "id", "content", "status", "priority"],
            "properties": {
              "kind": { "const": "existing" },
              "id": { "type": "string", "minLength": 1 },
              "content": { "type": "string" },
              "status": {
                "type": "string",
                "enum": ["pending", "in_progress", "reviewing", "completed", "cancelled"]
              },
              "priority": { "type": "string" }
            }
          },
          {
            "type": "object",
            "additionalProperties": false,
            "required": ["kind", "content", "status", "priority"],
            "properties": {
              "kind": { "const": "new" },
              "content": { "type": "string" },
              "status": {
                "type": "string",
                "enum": ["pending", "in_progress", "reviewing", "completed", "cancelled"]
              },
              "priority": { "type": "string" }
            },
            "not": { "required": ["id"] }
          }
        ]
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

    /// Strip kind/id before builtin executor (definition ads V2; executor still V1).
    let toCompatibilityRows (strategy: ReviewingSinkStrategy) (items: MagicTodoList) : CompatibilityTodoRow list =
        items
        |> List.map (fun item ->
            { Content = item.Content
              Status = compatibilityStatus strategy item.Status
              Priority = item.Priority })

    // ── Tagged input decode (§7) — structural, not optional-id guessing ───

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

    let renderListWire (items: MagicTodoList) : string =
        MagicTodo.canonicalListWire items
    // ── Enriched tool result (§22) — byte-stable renderer ──────────────────

    type PreviousReviewView =
        { Verdict: ProcessReviewVerdict
          ReportText: string }

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
