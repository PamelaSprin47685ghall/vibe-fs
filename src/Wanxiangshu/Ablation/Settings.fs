namespace Wanxiangshu.Ablation

open System

[<RequireQualifiedAccess>]
module AblationSettings =

    let private env (name: string) =
        match Environment.GetEnvironmentVariable name with
        | null -> None
        | value when String.IsNullOrWhiteSpace value -> None
        | value -> Some(value.Trim())

    let private profile () = env "WANXIANGSHU_ABLATION_PROFILE"

    let private envNodeKey (AblationNodeId raw) =
        let normalized = raw.Replace('.', '_').Replace('-', '_').ToLowerInvariant()
        $"WANXIANGSHU_ABLATION_{normalized}"

    let private explicitOverrides (nodeIds: AblationNodeId list) =
        // DSL-MUTABLE: algorithm-scratch — temporary error holding for overrides parsing
        let mutable err = None

        let pairs =
            nodeIds
            |> List.choose (fun id ->
                let rawOpt = env (envNodeKey id)

                match rawOpt, Option.bind AblationMode.parse rawOpt with
                | None, _ -> None
                | Some _, Some mode -> Some(id, mode)
                | Some raw, None ->
                    err <- Some(InvalidMode(AblationNodeId.value id, raw))
                    None)

        match err with
        | Some e -> Error e
        | None -> Ok pairs

    let load () : Result<AblationRegistry, AblationLoadError> =
        AblationManifest.loadNodes ()
        |> Result.bind (fun document ->
            explicitOverrides (AblationManifest.nodeIds document)
            |> Result.bind (fun overrides ->
                AblationManifest.buildRegistry document (profile ()) (Map.ofList overrides)))

    let private productionFallback () =
        match AblationManifest.loadNodes () with
        | Ok document ->
            let modes =
                document.Nodes
                |> List.map (fun node -> AblationNodeId.create node.Id, AblationMode.Active)
                |> Map.ofList

            let nodeMap =
                document.Nodes
                |> List.map (fun node -> AblationNodeId.create node.Id, node)
                |> Map.ofList

            { Modes = modes
              Audit =
                { Profile = None
                  ManifestVersion = document.Version
                  ManifestFingerprint = AblationManifest.nodesFingerprint ()
                  NodeCount = document.Nodes.Length }
              Nodes = nodeMap }
        | Error _ ->
            { Modes = Map.empty
              Audit =
                { Profile = None
                  ManifestVersion = "ablation-v1"
                  ManifestFingerprint = "000000000000"
                  NodeCount = 0 }
              Nodes = Map.empty }

    // DSL-MUTABLE: resource — memoized ablation registry
    let mutable private cached: AblationRegistry option = None

    let current () : AblationRegistry =
        cached
        |> Option.defaultWith (fun () ->
            let reg = load () |> Result.defaultWith (fun _ -> productionFallback ())
            cached <- Some reg
            reg)

    let setCache (registry: AblationRegistry) = cached <- Some registry

    let resetCache () = cached <- None

    let speculativeInvestigationMode () =
        AblationRegistry.modeFor (AblationNodeId.create "speculative-investigation") (current ())

    let strengthForcedOff () =
        speculativeInvestigationMode () = AblationMode.Ablated

    let private isItemInBorrowedSurface (nodeId: AblationNodeId) (item: string) (reg: AblationRegistry) : bool =
        let nodeOpt = AblationRegistry.tryFindNode nodeId reg
        let parentNodeOpt =
            nodeOpt
            |> Option.bind (fun n -> n.Parent)
            |> Option.bind (fun pId -> AblationRegistry.tryFindNode (AblationNodeId.create pId) reg)

        let inSelf =
            nodeOpt
            |> Option.map (fun n -> n.BorrowedSurface |> List.contains item)
            |> Option.defaultValue false

        let inParent =
            match nodeOpt, parentNodeOpt with
            | Some node, Some parentNode ->
                parentNode.BorrowedSurface |> List.contains node.Id
                || parentNode.BorrowedSurface |> List.contains item
            | _ -> false

        inSelf || inParent

    let private isNodeAllowed (node: AblationNodeId) (item: string) : bool =
        let reg = current ()
        match AblationRegistry.modeFor node reg with
        | AblationMode.Active -> true
        | AblationMode.Ablated -> false
        | AblationMode.Borrowed -> isItemInBorrowedSurface node item reg

    let allowsTool (toolName: string) : bool =
        let targetNode =
            if toolName = "speculate" then
                Some(AblationNodeId.create "speculative-investigation")
            else
                AblationToolMap.tryNode toolName

        targetNode
        |> Option.map (fun node -> isNodeAllowed node toolName)
        |> Option.defaultValue true

    let allowsToolSchema (toolName: string) : bool =
        let targetNode =
            if toolName = "speculate" then
                Some(AblationNodeId.create "speculative-investigation")
            else
                AblationToolMap.tryNode toolName

        targetNode
        |> Option.map (fun node -> isNodeAllowed node toolName)
        |> Option.defaultValue true

    let allowsPrimaryAgent (agentName: string) : bool =
        let reg = current ()
        match agentName.ToLowerInvariant() with
        | "manager" -> AblationRegistry.isActive (AblationNodeId.create "relay-incumbency") reg
        | "orchestrator" -> AblationRegistry.isActive (AblationNodeId.create "change-integration") reg
        | "inquiry" -> AblationRegistry.isActive (AblationNodeId.create "epistemic-reasoning") reg
        | "browser" -> false
        | _ -> true

    let fissionVisible () =
        AblationRegistry.isActive (AblationNodeId.create "intra-participant-parallelism") (current ())

    let allowsFactTag (factTag: string) : bool =
        AblationFactMap.tryNode factTag
        |> Option.map (fun node -> isNodeAllowed node factTag)
        |> Option.defaultValue true
