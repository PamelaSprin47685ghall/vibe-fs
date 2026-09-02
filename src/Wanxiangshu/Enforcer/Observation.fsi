namespace Wanxiangshu.Enforcer

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ObservationProjection =
    val observationsOf:
        enforcement: EnforcementProjectionState option -> blog: BlogProjectionState option -> WorkLogObservation list

    val observationsAfterSquash:
        coveredFrameCount: int ->
        enforcement: EnforcementProjectionState ->
        blog: BlogProjectionState ->
            WorkLogObservation list
