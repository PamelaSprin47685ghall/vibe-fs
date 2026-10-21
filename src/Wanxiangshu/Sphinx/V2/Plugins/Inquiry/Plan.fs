namespace Wanxiangshu.Sphinx.V2.Plugins

/// The semantic envelope of an inquiry plan.
///
/// WHAT[sphinx-v2-001]: a PlanCard is a semantic proposal, not an authorization. The LLM
/// writes the description, the target artifacts, the expected contribution and the
/// conditions; the Runtime resolves the capability, checks permission and cost, and
/// assigns the canonical id. A plan a worker invents does not exist until the Runtime
/// accepts it.
///
/// WHAT[sphinx-v2-029]: `answer.now` is a plan like any other. It is never free, never
/// zero-contribution, and never a fallback that only appears when nothing else runs.

type PlanCard =
    {
        PlanId: string
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
        IsAnswerNow: bool
    }

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

    /// A card must say what it does and name a capability. Everything else may be
    /// incomplete; a card that cannot be executed is excluded with a typed reason, not
    /// silently dropped.
    let validateCard (card: PlanCard) : Result<unit, PlanFault> =
        match System.String.IsNullOrWhiteSpace card.Description, System.String.IsNullOrWhiteSpace card.Capability with
        | true, _ -> Error PlanFault.BlankDescription
        | _, true -> Error PlanFault.BlankCapability
        | false, false -> Ok()
