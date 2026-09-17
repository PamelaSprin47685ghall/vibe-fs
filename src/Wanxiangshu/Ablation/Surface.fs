namespace Wanxiangshu.Ablation

open Fable.Core.JsInterop

/// JS-native semantic surface for ablation registry tests and gates.
module AblationSurface =

    let private registryToJs (registry: AblationRegistry) : obj =
        let pairs =
            registry.Modes
            |> Map.toList
            |> List.map (fun (AblationNodeId id, mode) -> (id, AblationMode.wire mode))
            |> List.toArray

        let modesObj = emitJsExpr pairs "Object.fromEntries($0)"

        box
            {| ok = true
               profile = registry.Audit.Profile |> Option.toObj
               manifestVersion = registry.Audit.ManifestVersion
               manifestFingerprint = registry.Audit.ManifestFingerprint
               nodeCount = registry.Audit.NodeCount
               modes = modesObj |}

    let private errorToJs (error: AblationLoadError) : obj =
        let kind, detail =
            match error with
            | MissingManifest path -> "MissingManifest", path
            | InvalidManifest reason -> "InvalidManifest", reason
            | UnknownProfile name -> "UnknownProfile", name
            | InvalidMode(node, raw) -> "InvalidMode", $"{node}={raw}"
            | DagViolation reason -> "DagViolation", reason

        box
            {| ok = false
               kind = kind
               error = detail |}

    let resetCache () : unit =
        AblationSettings.resetCache ()
        AblationToolMap.resetCache ()
        AblationFactMap.resetCache ()

    let load () =
        resetCache ()

        match AblationSettings.load () with
        | Ok registry ->
            AblationSettings.setCache registry
            registryToJs registry
        | Error(InvalidMode(node, raw)) -> invalidOp $"InvalidMode: {node}={raw}"
        | Error error -> errorToJs error

    let modeFor (nodeId: string) : string =
        AblationRegistry.modeFor (AblationNodeId.create nodeId) (AblationSettings.current ())
        |> AblationMode.wire

    let allowsTool (toolName: string) : bool = AblationSettings.allowsTool toolName

    let allowsToolSchema (toolName: string) : bool =
        AblationSettings.allowsToolSchema toolName

    let allowsPrimaryAgent (agentName: string) : bool =
        AblationSettings.allowsPrimaryAgent agentName

    let strengthForcedOff () : bool = AblationSettings.strengthForcedOff ()

    let fissionVisible () : bool = AblationSettings.fissionVisible ()

    let toolMapEntry (toolName: string) =
        match AblationToolMap.tryNode toolName with
        | None -> null
        | Some(AblationNodeId id) -> id

    let allowsFact (factTag: string) : bool = AblationSettings.allowsFactTag factTag

    let factMapEntry (factTag: string) =
        match AblationFactMap.tryNode factTag with
        | None -> null
        | Some(AblationNodeId id) -> id

    let manifestNodeIds () : string array =
        match AblationManifest.loadNodes () with
        | Ok document -> document.Nodes |> List.map (fun node -> node.Id) |> List.toArray
        | Error _ -> [||]

    let profileIds () : string array =
        match AblationManifest.loadProfiles () with
        | Ok document -> document.Profiles |> Map.keys |> Seq.toArray
        | Error _ -> [||]
