namespace Wanxiangshu.Sphinx.V2.Runtime

open System
open Wanxiangshu.Sphinx.V2.Core

/// Crash-window reconciliation.
///
/// WHAT[sphinx-v2-011]: a restart must never re-run a paid call to "recover" a result
/// that was already accepted, and must never invent one that was not. Every window in
/// the crash surface has exactly one reconciliation action, chosen from what was
/// durably written, never from what looks plausible now.
///
/// WHAT[sphinx-v2-005]: no state is patched from outside the fold. A crash cannot leave
/// a placeholder node, an invented parent or a generated identity.
/// The seven durable preconditions a restart can find itself in. Each one names the
/// single next action; anything else is a defect.
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

    let private error code message : Result<'value, RecoveryError> =
        Error { Code = code; Message = message }

    /// What a restart does for each window. The rule is deliberately narrow: anything
    /// that was already durably accepted is *not* repeated, and anything that was not
    /// is *not* invented.
    let reconcile (window: CrashWindow) : Reconciliation =
        match window with
        | CrashWindow.DispatchPending ->
            { Window = window
              Action = "dispatch"
              MaySpend = true
              Reason = "the intent is durable but the Host was never called, so the work still has to run" }
        | CrashWindow.ReceiptPending ->
            { Window = window
              Action = "reconcile-by-intent"
              MaySpend = false
              Reason = "the Host may already hold a child; reconcile before creating a second one" }
        | CrashWindow.RunningUnmarked ->
            { Window = window
              Action = "reconcile-by-intent"
              MaySpend = false
              Reason = "the prompt may already be in flight; marking Running is a state fact, not a new call" }
        | CrashWindow.ResultPending ->
            { Window = window
              Action = "accept-if-valid"
              MaySpend = false
              Reason = "a produced result is accepted from durable evidence, never regenerated" }
        | CrashWindow.InterpretationPending ->
            { Window = window
              Action = "interpret"
              MaySpend = false
              Reason = "interpretation is a pure function over the stored response" }
        | CrashWindow.CancelPending ->
            { Window = window
              Action = "await-terminal"
              MaySpend = false
              Reason = "an unconfirmed cancel stays cancelling until the Host reports a physical terminal" }
        | CrashWindow.CommitPending ->
            { Window = window
              Action = "commit-or-render"
              MaySpend = true
              Reason = "a prepared draft may be committed, or re-rendered if the reserve still allows" }

    /// A restart may only take actions that do not spend unless the budget still allows
    /// it, and may never take an action for a terminal inquiry.
    /// A terminal inquiry admits nothing except awaiting an already-requested cancel,
    /// because that is the one action that only observes a physical terminal.
    let admissibleOnTerminal (state: InquiryState) (window: CrashWindow) : Result<Reconciliation, RecoveryError> =
        match window with
        | CrashWindow.CancelPending -> Ok(reconcile window)
        | _ ->
            error
                "inquiry-terminal"
                (sprintf "inquiry %s is terminal; no restart action may change it" (InquiryId.value state.Id))

    let admissible (state: InquiryState) (window: CrashWindow) : Result<Reconciliation, RecoveryError> =
        match InquiryState.isTerminal state.Status with
        | true -> admissibleOnTerminal state window
        | false -> Ok(reconcile window)

    /// The reconciliation that spends is exactly the one that has real work left to do.
    /// A restart that neither dispatches nor renders spends nothing.
    let maySpend (action: Reconciliation) : bool = action.MaySpend

    /// Intent identity is the reconciliation key: two clients that both restart on the
    /// same dispatch must converge on the same physical child, not create two.
    let intentKey (inquiryId: InquiryId) (workId: WorkId) (attempt: Attempt) : string =
        String.concat
            "|"
            [ InquiryId.value inquiryId
              WorkId.value workId
              string (Attempt.value attempt) ]

    /// A restart may only claim at-most-once *semantic* adoption when the store can
    /// identify a duplicate physical call. Without that, the honest claim is that a
    /// duplicate may exist and is auditable.
    let adoptionGuarantee (durableIdentity: bool) : string =
        match durableIdentity with
        | true -> "at-most-once semantic adoption, auditable duplicate physical calls"
        | false -> "at-most-once semantic adoption only; duplicate physical calls are auditable but not excluded"
