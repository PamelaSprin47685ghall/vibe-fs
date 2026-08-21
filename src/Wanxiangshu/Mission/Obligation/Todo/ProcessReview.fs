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
open Wanxiangshu.Foundation.Identity

/// Process-review prompt assembly and RequestKind (REVIEW-013 / TODO-006/008).
/// Host-owned dedicated reviewer runtime consumes this typed body; it does not
/// guess process vs Finality from pendingChallenge.
module MagicTodoProcessReview =

    [<RequireQualifiedAccess>]
    type ReviewRequestKind =
        /// Checkpoint process review — not a Finality witness.
        | TodoProcess
        /// Terminal Finality review (existing path; listed for typed separation).
        | FinalityTerminal

    type ProcessReviewRequest =
        {
            TodoReviewId: TodoReviewId
            TodoWriteId: TodoWriteId
            ManagerLifeId: ManagerLifeId
            /// OpeningRaw authority text (separate from LWR; includeOpening=false on LWR).
            OpeningRaw: string
            /// Frontier-bounded ManagerCheckpointLWR text (Y + canonical RawGap).
            ManagerCheckpointLwr: string
            /// Effective commitment relation for this checkpoint. False means
            /// review the honesty/completeness of a planning account; true means
            /// review the mission-debt account. Once true it never returns false.
            EffectivePlanComplete: bool
            OldTodo: ObligationList
            ProposedTodo: ObligationList
        }

    /// `preamble` is already-localized ProcessReviewer prose (PROMPT-019).
    let renderAssignmentUserMessage (preamble: string) (req: ProcessReviewRequest) : string =
        let instructions =
            if System.String.IsNullOrWhiteSpace req.OpeningRaw then
                [ preamble ]
            else
                [ preamble; req.OpeningRaw ]

        LlmFacing.instructions instructions
        |> LlmFacing.withData
            [ LlmFacing.Data.stringField "manager_checkpoint_lwr" req.ManagerCheckpointLwr
              LlmFacing.Data.boolField "effective_plan_complete" req.EffectivePlanComplete
              LlmFacing.Data.stringField
                  "prior_current_obligations"
                  (MagicTodoSurface.renderObligationListWire req.OldTodo)
              LlmFacing.Data.stringField
                  "accepted_obligation_account_under_review"
                  (MagicTodoSurface.renderObligationListWire req.ProposedTodo) ]
        |> LlmFacing.render

    /// ensureReview obligation predicate (HOST-021 / TODO-006):
    /// Accepted ∧ ¬TodoReviewConcluded → Rk must be ensure-able from any reentry site.
    let needsEnsureReview (accepted: bool) (concluded: bool) : bool = accepted && not concluded
