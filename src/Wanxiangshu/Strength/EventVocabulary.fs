namespace Wanxiangshu.Strength

[<RequireQualifiedAccess>]
module StrengthEventTypes =
    let DelegationRequested = "DelegationRequested"
    let DelegationBound = "DelegationBound"
    let DelegationClosed = "DelegationClosed"
    let DelegationHistoryImported = "DelegationHistoryImported"
    let CandidatePrepared = "StrengthCandidatePrepared"
    let CandidatePromoted = "StrengthCandidatePromoted"
    let FramesTraced = "StrengthFramesTraced"
    let CandidateAbandoned = "StrengthCandidateAbandoned"

    let all =
        [ DelegationRequested
          DelegationBound
          DelegationClosed
          DelegationHistoryImported
          CandidatePrepared
          CandidatePromoted
          FramesTraced
          CandidateAbandoned ]

    let isStrengthEvent eventType = all |> List.contains eventType
