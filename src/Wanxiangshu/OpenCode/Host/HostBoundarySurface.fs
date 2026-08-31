namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// JS-native observation boundary for Host transcript/session membrane laws.
/// Raw Host objects enter here; typed snapshot identities and F# unions never
/// cross back to semantic tests.
module HostBoundarySurface =

    let sanitizeMessage (raw: obj) : obj =
        HostMessageProjection.sanitizeMessage raw

    let sanitizeMessages (raw: obj array) : obj array =
        raw |> Array.toList |> HostMessageProjection.sanitizeMessages |> List.toArray

    let roleOf (agent: string) : string =
        HostSessionContext.roleOf agent
        |> Option.map Roles.roleLabel
        |> Option.defaultValue null

    let sessionContext (raw: obj) : obj =
        let sessionId, agent = HostSessionContext.read raw

        box
            {| sessionId = sessionId
               agent = agent |> Option.defaultValue null |}

    let private toolState =
        function
        | SnapshotToolPartState.Pending -> box {| kind = "pending"; value = null |}
        | SnapshotToolPartState.Completed outputCanonical ->
            box
                {| kind = "completed"
                   value = outputCanonical |}
        | SnapshotToolPartState.Failed errorCanonical ->
            box
                {| kind = "failed"
                   value = errorCanonical |}

    let locateToolCall (toolCallId: string) (rawMessages: obj array) : obj =
        match
            SessionSnapshotPort.locateToolCall
                (ToolCallId.create toolCallId)
                (SessionSnapshotPort.projectMessages rawMessages)
        with
        | Ok location ->
            box
                {| ok = true
                   error = null
                   providerRun = ProviderRunIdentity.value location.ProviderRun
                   hostToolPartId = HostToolPartId.value location.HostToolPartId
                   toolCallId = ToolCallId.value location.ToolCallId
                   toolName = location.ToolName
                   inputCanonical = location.InputCanonical
                   state = toolState location.State |}
        | Error(SessionSnapshotPort.ToolCallLocationError.Missing _) ->
            box
                {| ok = false
                   error = "Missing"
                   providerRun = null
                   hostToolPartId = null
                   toolCallId = null
                   toolName = null
                   inputCanonical = null
                   state = null |}
        | Error(SessionSnapshotPort.ToolCallLocationError.Ambiguous _) ->
            box
                {| ok = false
                   error = "Ambiguous"
                   providerRun = null
                   hostToolPartId = null
                   toolCallId = null
                   toolName = null
                   inputCanonical = null
                   state = null |}
