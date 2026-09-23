namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
type AdvanceOutcome =
    /// Work is dispatched and awaiting results.
    | AwaitingResults of pendingCount: int
    /// Waiting on a user decision the program cannot make.
    | InputRequired of authorization: string
    /// Nothing runnable; the reason says why.
    | NoRunnalbeWork of reason: string
    /// The inquiry reached a terminal state.
    | Terminal of status: string
    /// The pure step limit was hit with work still refining.
    | RefinementPending of remaining: int

module Driver =
    /// A plugin loop that never settles stops here and reports what is left.
    [<Literal>]
    val maxPureSteps: int = 128

    /// Pure classification of what to do next. The caller performs only what this names.
    val classify: InquiryState -> AdvanceOutcome
