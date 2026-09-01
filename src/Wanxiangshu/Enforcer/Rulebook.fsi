namespace Wanxiangshu.Enforcer

type ObservationUnit =
    { TipName: string option
      FrameDigest: string option
      FrameBody: string option }

type WorkLogObservation =
    { TipName: string
      CycleId: string
      FrameDigest: string option }

[<RequireQualifiedAccess>]
module RulebookObservation =
    val pairTipsAndFrames:
        tips: string list -> frames: (string * string option) list -> ObservationUnit list

    val ofTipsAndFrames:
        tips: (string * string) list -> frameDigests: string list -> WorkLogObservation list

    val workLogFromUnits:
        tipCycles: (string * string) list -> units: ObservationUnit list -> WorkLogObservation list
