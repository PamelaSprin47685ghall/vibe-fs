namespace Wanxiangshu.Strength

[<RequireQualifiedAccess>]
module StrengthEventTypes =
    let CandidatePrepared = "StrengthCandidatePrepared"
    let CandidatePromoted = "StrengthCandidatePromoted"
    let FramesTraced = "StrengthFramesTraced"
    let CandidateAbandoned = "StrengthCandidateAbandoned"

    let all = [ CandidatePrepared; CandidatePromoted; FramesTraced; CandidateAbandoned ]

    let isStrengthEvent eventType = all |> List.contains eventType
