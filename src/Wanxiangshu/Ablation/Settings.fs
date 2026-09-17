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

            { Modes = modes
              Audit =
                { Profile = None
                  ManifestVersion = document.Version
                  ManifestFingerprint = AblationManifest.nodesFingerprint ()
                  NodeCount = document.Nodes.Length } }
        | Error _ ->
            { Modes = Map.empty
              Audit =
                { Profile = None
                  ManifestVersion = "ablation-v1"
                  ManifestFingerprint = "000000000000"
                  NodeCount = 0 } }

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

    let allowsTool (toolName: string) : bool =
        let targetNode =
            if toolName = "speculate" then
                Some(AblationNodeId.create "speculative-investigation")
            else
                AblationToolMap.tryNode toolName

        match targetNode with
        | None -> true
        | Some node -> AblationMode.allowsToolExecution (AblationRegistry.modeFor node (current ()))

    let allowsToolSchema (toolName: string) : bool =
        let targetNode =
            if toolName = "speculate" then
                Some(AblationNodeId.create "speculative-investigation")
            else
                AblationToolMap.tryNode toolName

        match targetNode with
        | None -> true
        | Some node -> AblationMode.allowsToolSchema (AblationRegistry.modeFor node (current ()))

    let allowsPrimaryAgent (agentName: string) : bool =
        let node =
            match agentName.ToLowerInvariant() with
            | "manager" -> Some(AblationNodeId.create "relay-incumbency")
            | "orchestrator" -> Some(AblationNodeId.create "change-integration")
            | "browser" -> Some(AblationNodeId.create "external-investigation")
            | "inquiry" -> Some(AblationNodeId.create "epistemic-reasoning")
            | _ -> None

        match node with
        | None -> true
        | Some id -> AblationRegistry.isActive id (current ())

    let fissionVisible () =
        AblationRegistry.isActive (AblationNodeId.create "intra-participant-parallelism") (current ())

    let allowsFactTag (factTag: string) : bool =
        match AblationFactMap.tryNode factTag with
        | None -> true
        | Some node -> AblationRegistry.isBorrowedOrActive node (current ())
