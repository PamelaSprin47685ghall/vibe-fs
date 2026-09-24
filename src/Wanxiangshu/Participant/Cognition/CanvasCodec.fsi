namespace Wanxiangshu.Participant.Cognition

[<RequireQualifiedAccess>]
module CanvasCodec =
    val isJsonValue: value: obj -> bool
    val toJson: value: obj -> string
    val emptyCanvasJson: string
