namespace Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Composition.Durable.Fact

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
        let PlanCompleteDescription = "lifecycle/magic-todo/plan-complete-description"

        [<Literal>]
        let ObligationNameDescription = "lifecycle/magic-todo/obligation-name-description"

        [<Literal>]
        let ObligationWorkDescription = "lifecycle/magic-todo/obligation-work-description"

        [<Literal>]
        let ProcessReviewerPreamble = "lifecycle/magic-todo/process-reviewer-preamble"

        [<Literal>]
        let PreviousReviewBody = "lifecycle/magic-todo/previous-review-body"

        [<Literal>]
        let ObligationWriteResult = "lifecycle/magic-todo/obligation-write-result"

        [<Literal>]
        let ObligationAcceptedEpilogue = "lifecycle/magic-todo/obligation-accepted-epilogue"

    /// Whether the Magic Todo Manager fragment should be projected.
    let shouldProjectManagerGuideline (canonicalRole: string) (todowriteProviderVisible: bool) : bool =
        canonicalRole = "Manager" && todowriteProviderVisible

    /// JSON Schema fragment for tool.definition parameters / jsonSchema (both must update).
    let todoWriteJsonSchema: string =
        """{
  "type": "object",
  "additionalProperties": false,
  "required": ["planComplete", "obligations"],
  "properties": {
    "planComplete": { "type": "boolean" },
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

    /// GrandRewrite provider obligations projected into the Host's legacy TodoTable.
    /// The sink is optimistic UI state only; these fields never round-trip into
    /// canonical truth (TODO-007).
    let obligationsToCompatibilityRows (items: ObligationList) : CompatibilityTodoRow list =
        items
        |> List.map (fun item ->
            { Content = item.Name + ": " + item.Work
              Status = "in_progress"
              Priority = "medium" })

    // ── Canonical obligation wire (tool result / blob body) ────────────────

    let renderObligationListWire (items: ObligationList) : string =
        MagicTodo.canonicalObligationListWire items

    type PreviousReviewView =
        { Verdict: ProcessReviewVerdict
          ReportText: string }

    let previousReviewSubs (verdictWire: string) (reportText: string) : Map<string, string> =
        Map [ "verdict", verdictWire; "report", reportText ]

    let obligationWriteSubs (previousBody: string) (acceptedEpilogue: string) : Map<string, string> =
        Map [ "previous_body", previousBody; "accepted_epilogue", acceptedEpilogue ]

    /// Process reviewer assignment body: preamble already localized by caller.
    let renderAssignmentUserMessage (preamble: string) (sections: string list) : string =
        String.concat "\n\n" (preamble :: sections)

    /// GLORY-030 relaxation boundary: Manager may see process PERFECT/REVISE
    /// outcome + concrete ProcessReviewLWR report; never reviewer identity /
    /// session / barrier / witness / 2N / confirmation mechanics.
    let managerMaySeeProcessReviewOutcome = true
