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
        nodeIds
        |> List.choose (fun id ->
            env (envNodeKey id)
            |> Option.bind AblationMode.parse
            |> Option.map (fun mode -> id, mode))

    let load () : Result<AblationRegistry, AblationLoadError> =
        AblationManifest.loadNodes ()
        |> Result.bind (fun document ->
            let overrides = explicitOverrides (AblationManifest.nodeIds document) |> Map.ofList
            AblationManifest.buildRegistry document (profile ()) overrides)

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

    let resetCache () = cached <- None

    let speculativeInvestigationMode () =
        AblationRegistry.modeFor (AblationNodeId.create "speculative-investigation") (current ())

    let strengthForcedOff () =
        speculativeInvestigationMode () = AblationMode.Ablated

    let allowsTool (toolName: string) : bool =
        match AblationToolMap.tryNode toolName with
        | None -> true
        | Some node -> AblationMode.allowsToolExecution (AblationRegistry.modeFor node (current ()))

    let allowsToolSchema (toolName: string) : bool =
        match AblationToolMap.tryNode toolName with
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
