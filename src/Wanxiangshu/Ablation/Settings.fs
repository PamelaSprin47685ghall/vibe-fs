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
