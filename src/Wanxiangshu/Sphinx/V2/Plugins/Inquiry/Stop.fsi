namespace Wanxiangshu.Sphinx.V2.Plugins

[<RequireQualifiedAccess>]
type StopReason =
    /// The model's estimate preferred direct rendering.
    | ModelRankedStop
    /// An ordinal judgement preferred direct rendering.
    | OrdinalStop
    /// A declared model's own stopping condition held.
    | CertifiedWithinModel of modelRef: string
    /// The authorized resource boundary was reached.
    | ResourceLimited
    /// Every remaining plan is infeasible (capability or permission).
    | NoExecutablePlan
    | UserCancelled
    | Failed of reason: string
    | Suspended of reason: string

type StopClaim =
    {
        Reason: StopReason
        /// The plans the claim was evaluated over. Stops never cover more than this.
        CoveredPlanIds: string list
        /// True when the value of further computation was actually measured.
        VocMeasured: bool
        VocValue: float option
        /// True when the claimed reason is a property of a declared model only.
        ModelRelative: bool
    }

[<RequireQualifiedAccess>]
type StopFault =
    | ClaimCoversNothing
    | VocClaimedWithoutMeasurement
    | ModelRelativeReasonClaimedAbsolutely

module Stop =
    val reasonName: StopReason -> string
    val isModelRelative: StopReason -> bool
    val validateClaim: StopClaim -> Result<StopClaim, StopFault>
