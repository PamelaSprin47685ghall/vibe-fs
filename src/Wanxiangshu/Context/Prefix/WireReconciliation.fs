namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Participant.Provider.Attempt

type XWireReconciliationDecision = { Cleared: bool; KeptPlan: bool }

module XWireReconciliation =

    let reconciliationDecision (hasPlan: bool) (outcome: AttemptOutcome option) : XWireReconciliationDecision =
        match hasPlan, outcome with
        | false, _ -> { Cleared = false; KeptPlan = false }
        | true, Some AttemptOutcome.Completed -> { Cleared = true; KeptPlan = false }
        | true, Some AttemptOutcome.CompletedInvalid
        | true, Some AttemptOutcome.Failed
        | true, Some AttemptOutcome.Aborted -> { Cleared = true; KeptPlan = false }
        | true, None -> { Cleared = false; KeptPlan = true }
