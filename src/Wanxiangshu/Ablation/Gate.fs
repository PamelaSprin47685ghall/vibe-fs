namespace Wanxiangshu.Ablation

open System.Collections.Generic

[<RequireQualifiedAccess>]
module AblationGate =

    let deniedPath = "tool/registry/denied-ablation"

    let toolDenied (toolName: string) =
        not (AblationSettings.allowsTool toolName)

    let filterToolPermissionMap (permissions: Map<string, bool>) =
        permissions
        |> Map.map (fun name allowed ->
            if allowed && not (AblationSettings.allowsToolSchema name) then
                false
            else
                allowed)

    let filterKnownToolNames (names: string list) =
        names |> List.filter AblationSettings.allowsToolSchema

    let deniedFactPath = "journal/denied-ablation"

    let factDenied (factTag: string) =
        not (AblationSettings.allowsFactTag factTag)
