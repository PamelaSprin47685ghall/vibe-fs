namespace Wanxiangshu.Context.Prefix

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module PhaseWindow =

    type PhaseCheckpoint = { ToolCallId: ToolCallId }

    type PhaseCommitWindow = { Checkpoints: PhaseCheckpoint list }

    [<Import("Prefix", "wanxiangli/prefix")>]
    let private ctor: obj = jsNative

    let private owner: obj = createNew ctor ()

    let emptyWindow: PhaseCommitWindow = { Checkpoints = [] }

    /// Record one successful view declaration checkpoint. Re-entry of the same call
    /// identity is a replay of the same fact; the rule is owned by `wanxiangli/prefix`.
    let appendCheckpoint (callId: ToolCallId) (window: PhaseCommitWindow) : PhaseCommitWindow =
        let current =
            window.Checkpoints
            |> List.map (fun checkpoint -> ToolCallId.value checkpoint.ToolCallId)
            |> List.toArray

        let next: string array =
            unbox (owner?appendCheckpoint (current, ToolCallId.value callId))

        { Checkpoints =
            next
            |> Array.toList
            |> List.map (fun id -> { ToolCallId = ToolCallId.create id }) }
