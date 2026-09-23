namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
type CrashWindow =
    /// Intent persisted, Host never called.
    | DispatchPending
    /// Host created the child, receipt not yet written.
    | ReceiptPending
    /// Prompt sent, work not yet marked Running.
    | RunningUnmarked
    /// Result produced, not yet accepted.
    | ResultPending
    /// Accepted, interpretation not yet applied.
    | InterpretationPending
    /// Cancel requested, Host has not confirmed.
    | CancelPending
    /// Draft prepared, final commit not written.
    | CommitPending

type Reconciliation =
    {
        Window: CrashWindow
        /// The single action to take on restart.
        Action: string
        /// True when taking it may spend real money.
        MaySpend: bool
        Reason: string
    }

type RecoveryError = { Code: string; Message: string }

module Recovery =
    /// What a restart does for each window. Anything durably accepted is not repeated;
    /// anything not durably written is not invented.
    val reconcile: CrashWindow -> Reconciliation

    /// A restart may not act on a terminal inquiry except to await a cancel terminal.
    val admissible: InquiryState -> CrashWindow -> Result<Reconciliation, RecoveryError>
    val maySpend: Reconciliation -> bool

    /// Intent identity is the reconciliation key: two restarts must converge on one child.
    val intentKey: InquiryId -> WorkId -> Attempt -> string

    /// What the store can honestly claim about duplicate physical calls.
    val adoptionGuarantee: bool -> string
