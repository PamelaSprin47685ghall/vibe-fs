namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Kernel.Identity

/// Process-review prompt assembly (protocol §18 / §20).
/// Speculative: does not fork reviewer sessions; only shapes the typed request body
/// once Host-owned dedicated reviewer runtime is wired.
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
            OldTodo: MagicTodoList
            ProposedTodo: MagicTodoList
        }

    let renderAssignmentUserMessage (req: ProcessReviewRequest) : string =
        String.concat
            "\n\n"
            [ MagicTodoSurface.ProcessReviewerInstructionPreamble
              "=== OpeningRaw (task authority) ==="
              req.OpeningRaw
              "=== ManagerCheckpointLWR (includeOpening=false; frontier-bounded) ==="
              req.ManagerCheckpointLwr
              "=== OLD TODO LIST (Ck) ==="
              MagicTodoSurface.renderListWire req.OldTodo
              "=== PROPOSED TODO LIST (Pk) ==="
              MagicTodoSurface.renderListWire req.ProposedTodo ]

    /// ensureReview obligation predicate (protocol §15):
    /// Accepted ∧ ¬TodoReviewConcluded → Rk must be ensure-able from any reentry site.
    let needsEnsureReview (accepted: bool) (concluded: bool) : bool = accepted && not concluded
