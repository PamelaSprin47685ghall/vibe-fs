namespace Wanxiangshu.Sphinx.V2.Plugins

/// The stop semantics.
///
/// WHAT[sphinx-v2-030]: a stop claim covers only the plans that were actually listed.
/// It never asserts that no better path exists, and a missing value-of-computation
/// figure is a missing figure — not a check that passed.
///
/// WHAT[sphinx-v2-029]: model-ranked stop means the model's estimate, in this scope,
/// preferred answer.now. It is a statement about the estimate, not about the world.

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
    { Reason: StopReason
      /// The plans the claim was evaluated over. Stops never cover more than this.
      CoveredPlanIds: string list
      /// True when the value of further computation was actually measured.
      VocMeasured: bool
      VocValue: float option
      /// True when the claimed reason is a property of a declared model only.
      ModelRelative: bool }

[<RequireQualifiedAccess>]
type StopFault =
    | ClaimCoversNothing
    | VocClaimedWithoutMeasurement
    | ModelRelativeReasonClaimedAbsolutely

module Stop =

    let reasonName (reason: StopReason) : string =
        match reason with
        | StopReason.ModelRankedStop -> "model-ranked-stop"
        | StopReason.OrdinalStop -> "ordinal-stop"
        | StopReason.CertifiedWithinModel _ -> "certified-within-model"
        | StopReason.ResourceLimited -> "resource-limited"
        | StopReason.NoExecutablePlan -> "no-executable-plan"
        | StopReason.UserCancelled -> "user-cancelled"
        | StopReason.Failed _ -> "failed"
        | StopReason.Suspended _ -> "suspended"

    /// A model-relative reason must be labeled as such. Claiming an absolute stop from
    /// a model-internal condition is the overclaim this type exists to prevent.
    let isModelRelative (reason: StopReason) : bool =
        match reason with
        | StopReason.CertifiedWithinModel _ -> true
        | _ -> false

    let validateClaim (claim: StopClaim) : Result<StopClaim, StopFault> =
        let noVocsMeasured () = claim.VocValue.IsSome && not claim.VocMeasured
        let inconsistentRelativity () = isModelRelative claim.Reason <> claim.ModelRelative

        match List.isEmpty claim.CoveredPlanIds, noVocsMeasured (), inconsistentRelativity () with
        | true, _, _ -> Error StopFault.ClaimCoversNothing
        | false, true, _ -> Error StopFault.VocClaimedWithoutMeasurement
        | false, false, true -> Error StopFault.ModelRelativeReasonClaimedAbsolutely
        | false, false, false -> Ok claim
