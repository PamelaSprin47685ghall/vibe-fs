module VibeFs.Shell.FileReadCache

let [<Literal>] private maxSize = 256

let mutable private store: Map<string, string> = Map.empty
let mutable private order: string list = []

let private touch (key: string) : unit =
    order <- (order |> List.filter (fun k -> k <> key)) @ [key]

let private evict () : unit =
    while List.length order > maxSize do
        match order with
        | oldest :: rest ->
            store <- store |> Map.remove oldest
            order <- rest
        | [] -> ()

let get (path: string) : string option =
    match store |> Map.tryFind path with
    | Some content -> touch path; Some content
    | None -> None

let set (path: string) (content: string) : unit =
    store <- store |> Map.add path content
    touch path
    evict ()

let invalidate (path: string) : unit =
    store <- store |> Map.remove path
    order <- order |> List.filter (fun k -> k <> path)

let clear () : unit =
    store <- Map.empty
    order <- []
