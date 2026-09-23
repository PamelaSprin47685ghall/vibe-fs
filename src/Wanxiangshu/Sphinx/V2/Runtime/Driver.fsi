namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
type AdvanceOutcome =
    | AwaitingResults of pendingCount: int
    | InputRequired of authorization: string
    | NoRunnalbeWork of reason: string
    | Terminal of status: string
    | RefinementPending of remaining: int

type AdvancePlan =
    { Events: InquiryEventBody list
      Outcome: AdvanceOutcome }

module Driver =
    val maxPureSteps: int
    val advance: InquiryState -> AdvancePlan
    val classify: InquiryState -> AdvanceOutcome
