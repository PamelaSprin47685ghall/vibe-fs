namespace Wanxiangshu.Ablation

[<RequireQualifiedAccess>]
module AblationToolMap =

    let mutable private cached: Map<string, AblationNodeId> option = None

    let private load () =
        match AblationManifest.loadToolMap () with
        | Ok document -> document.Tools |> Map.map (fun _ value -> AblationNodeId.create value)
        | Error _ -> Map.empty

    let private map () =
        match cached with
        | Some value -> value
        | None ->
            let value = load ()
            cached <- Some value
            value

    let resetCache () = cached <- None

    let tryNode (toolName: string) = map () |> Map.tryFind toolName

    let allTools () = map () |> Map.toList
