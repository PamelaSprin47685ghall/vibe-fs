namespace Wanxiangshu.Strength

[<RequireQualifiedAccess>]
module StrengthEventTypes =
    val CandidatePrepared: string
    val CandidatePromoted: string
    val FramesTraced: string
    val CandidateAbandoned: string

    val all: string list
    val isStrengthEvent: eventType: string -> bool
