module Wanxiangshu.Kernel.Methodology.NoteSections

open System

let private noteKeys (noteDescription: string) : string list =
    noteDescription.Split([| ','; '|' |], StringSplitOptions.RemoveEmptyEntries)
    |> Array.map (fun s -> s.Trim())
    |> Array.filter (fun s -> s <> "")
    |> Array.toList

let private headerKey (keySet: Set<string>) (line: string) : string option =
    let t = line.Trim()

    let trySep (sep: char) =
        let i = t.IndexOf(sep)

        if i <= 0 then
            None
        else
            let candidate = t.Substring(0, i).Trim().ToLowerInvariant()
            if Set.contains candidate keySet then Some candidate else None

    match trySep ':' with
    | Some k -> Some k
    | None -> trySep '='

let private afterHeader (line: string) : string =
    let t = line.Trim()
    let iColon = t.IndexOf(':')
    let iEq = t.IndexOf('=')

    let i =
        if iColon >= 0 && (iEq < 0 || iColon <= iEq) then
            iColon
        else
            iEq

    if i >= 0 then t.Substring(i + 1).Trim() else ""

/// Split freeform note into sections keyed by noteDescription tokens.
let splitNoteSections (noteDescription: string) (noteText: string) : (string * string) list =
    let keys = noteKeys noteDescription
    let body = if isNull noteText then "" else noteText.Trim()

    if body = "" then
        []
    elif List.isEmpty keys then
        [ "note", body ]
    else
        let keySet = keys |> List.map (fun k -> k.ToLowerInvariant()) |> Set.ofList
        let mutable current: string option = None
        let mutable buf: string list = []
        let mutable acc: Map<string, string list> = Map.empty

        let flush () =
            let text = buf |> List.rev |> String.concat "\n" |> fun s -> s.Trim()
            buf <- []

            if text <> "" then
                let k = defaultArg current (List.head keys)
                acc <- Map.add k (text :: (Map.tryFind k acc |> Option.defaultValue [])) acc

        for line in body.Split('\n') do
            match headerKey keySet line with
            | Some k ->
                flush ()
                current <- Some k
                let after = afterHeader line
                if after <> "" then buf <- [ after ]
            | None -> buf <- line :: buf

        flush ()

        let ordered =
            keys
            |> List.choose (fun k ->
                match Map.tryFind (k.ToLowerInvariant()) acc with
                | Some parts -> Some(k, String.concat "\n\n" (List.rev parts))
                | None ->
                    match Map.tryFind k acc with
                    | Some parts -> Some(k, String.concat "\n\n" (List.rev parts))
                    | None -> None)

        if List.isEmpty ordered then
            [ List.head keys, body ]
        else
            ordered
