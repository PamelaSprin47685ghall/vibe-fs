namespace Wanxiangshu.Ablation

[<RequireQualifiedAccess>]
module AblationFactMap =

    // DSL-MUTABLE: resource — memoized ablation fact map
    let mutable private cached: Map<string, AblationNodeId> option = None

    let private load () =
        match AblationManifest.loadFactMap () with
        | Ok document -> document.Facts |> Map.map (fun _ value -> AblationNodeId.create value)
        | Error _ -> Map.empty

    let private map () =
        match cached with
        | Some value -> value
        | None ->
            let value = load ()
            cached <- Some value
            value

    let resetCache () = cached <- None

    let tryNode (factTag: string) = map () |> Map.tryFind factTag

    let allFacts () = map () |> Map.toList
