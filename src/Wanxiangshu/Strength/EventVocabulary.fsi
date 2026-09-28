namespace Wanxiangshu.Strength

[<RequireQualifiedAccess>]
module StrengthEventTypes =
    val DelegationRequested: string
    val DelegationBound: string
    val DelegationClosed: string
    val DelegationHistoryImported: string
    val CandidatePrepared: string
    val CandidatePromoted: string
    val FramesTraced: string
    val CandidateAbandoned: string

    val all: string list
    val isStrengthEvent: eventType: string -> bool
