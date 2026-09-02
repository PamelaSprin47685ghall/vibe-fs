namespace Wanxiangshu.Strength.Prediction

open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Strength

[<RequireQualifiedAccess>]
module StrengthBatchCollector =
    val collectCompleteBatches: messages: ProviderProjection.WireMessage list -> StrengthRequestBatch list
