namespace Wanxiangshu.Mission.WorkRecord

open Wanxiangshu.Context.Trace

type CommitmentContract = | FirstPlanCompleteTodoWrite

type OpeningPolicy =
    | Immediate
    | BlindPlan of CommitmentContract

[<RequireQualifiedAccess>]
module OpeningPolicy =
    val immediate: OpeningPolicy

type LifecycleWorkRecord =
    { Opening: XTraceOpeningEvidence
      Frames: string list
      Gap: string }

[<RequireQualifiedAccess>]
module LifecycleWorkRecord =
    val render: includeOpening: bool -> record: LifecycleWorkRecord -> string

    val materialize:
        opening: XTraceOpeningEvidence -> frames: string list -> gap: string -> includeOpening: bool -> string

    val withConstitutive: opening: XTraceOpeningEvidence -> constitutiveBody: string -> XTraceOpeningEvidence
