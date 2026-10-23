namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module PhaseWindow =

    type PhaseCheckpoint = { ToolCallId: ToolCallId }

    type PhaseCommitWindow = { Checkpoints: PhaseCheckpoint list }

    let emptyWindow: PhaseCommitWindow = { Checkpoints = [] }
