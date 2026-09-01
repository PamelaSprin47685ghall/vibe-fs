namespace Wanxiangshu.Strength.Replica

open Wanxiangshu.Foundation.Identity

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
    val decide:
        targetProviderRun: ProviderRunIdentity ->
        observedProviderRun: ProviderRunIdentity ->
        outputEvidence: StrengthProviderOutputEvidence ->
            StrengthPromotionDecision
