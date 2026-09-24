namespace Wanxiangshu.Participant.Cognition

open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
module WorkspaceSurface =
    val AssumeSnapshot_ofJson: canvasJson: string -> rows: obj array -> obj
    val AssumeSnapshot_empty: unit -> obj
    val AssumeSnapshot_json: snapshot: obj -> string
