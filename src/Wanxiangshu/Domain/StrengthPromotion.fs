namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel.Identity

[<RequireQualifiedAccess>]
type StrengthProviderOutputEvidence =
    | RealOutput
    | TransportOnly
    | NoOutput

[<RequireQualifiedAccess>]
type StrengthPromotionDecision =
    | Promote
    | IgnoreWrongRun
    | AwaitOrAbandon

[<RequireQualifiedAccess>]
module StrengthPromotion =

    /// STRENGTH-007: ProviderRunIdentity is the causal consumption identity.
    /// A run outcome label is intentionally not an input: a failed/aborted run
    /// that already emitted real provider output still proves the provider saw
    /// its input; a failed/aborted run with no real output does not.
    let decide
        (targetProviderRun: ProviderRunIdentity)
        (observedProviderRun: ProviderRunIdentity)
        (outputEvidence: StrengthProviderOutputEvidence)
        : StrengthPromotionDecision =
        if targetProviderRun <> observedProviderRun then
            StrengthPromotionDecision.IgnoreWrongRun
        else
            match outputEvidence with
            | StrengthProviderOutputEvidence.RealOutput -> StrengthPromotionDecision.Promote
            | StrengthProviderOutputEvidence.TransportOnly
            | StrengthProviderOutputEvidence.NoOutput -> StrengthPromotionDecision.AwaitOrAbandon
