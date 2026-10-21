namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Participant.Provider.Attempt

type XWireReconciliationDecision = { Cleared: bool; KeptPlan: bool }

/// Host-neutral reconcile decision: does an attempt outcome clear the attempt plan?
module XWireReconciliation =

    val reconciliationDecision: hasPlan: bool -> outcome: AttemptOutcome option -> XWireReconciliationDecision
