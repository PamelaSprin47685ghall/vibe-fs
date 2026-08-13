namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Kernel.Identity

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
            OldTodo: ObligationList
            ProposedTodo: ObligationList
        }

    /// `preamble` is already-localized ProcessReviewer prose (PROMPT-019).
    let renderAssignmentUserMessage (preamble: string) (req: ProcessReviewRequest) : string =
        MagicTodoSurface.renderAssignmentUserMessage
            preamble
            [ "=== OpeningRaw (task authority) ==="
              req.OpeningRaw
              "=== ManagerCheckpointLWR (includeOpening=false; frontier-bounded) ==="
              req.ManagerCheckpointLwr
              "=== PRIOR CURRENT OBLIGATIONS ==="
              MagicTodoSurface.renderObligationListWire req.OldTodo
              "=== ACCEPTED OBLIGATION ACCOUNT UNDER REVIEW ==="
              MagicTodoSurface.renderObligationListWire req.ProposedTodo ]

    /// ensureReview obligation predicate (HOST-021 / TODO-006):
    /// Accepted ∧ ¬TodoReviewConcluded → Rk must be ensure-able from any reentry site.
    let needsEnsureReview (accepted: bool) (concluded: bool) : bool = accepted && not concluded
