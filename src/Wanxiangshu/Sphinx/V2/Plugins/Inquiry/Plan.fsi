namespace Wanxiangshu.Sphinx.V2.Plugins

type PlanCard =
    { PlanId: string
      Description: string
      TargetArtifactRefs: string list
      Capability: string
      ExpectedContribution: string
      Conditions: string list
      Continuation: string
      /// Estimated resource need. The Host's cost model decides feasibility.
      Reserved: Map<string, float>
      /// Tool capability names the plan needs. A request, not a grant.
      PermissionNeeds: string list
      /// Where this proposal came from.
      SourceObservation: string
      /// True when this card is the direct render action.
      IsAnswerNow: bool }

type PlanProposal =
    { Cards: PlanCard list
      ProvisionalTiers: string list list
      Conditions: string list }

[<RequireQualifiedAccess>]
type PlanFault =
    | BlankDescription
    | BlankCapability
    | UnknownTargetRef of artifactRef: string
    | NoPlans
    | DuplicatePlanId of planId: string
    | AnswerNowWithoutRenderReserve

module Plan =
    val validateCard: PlanCard -> Result<unit, PlanFault>
    val validateProposal: Set<string> -> PlanProposal -> Result<PlanProposal, PlanFault>
