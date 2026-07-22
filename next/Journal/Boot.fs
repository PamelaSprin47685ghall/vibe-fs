namespace Wanxiangshu.Next.Journal

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

module private NodeFsBoot =
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("readdirSync", "node:fs")>]
    let readdirSync (path: string) : string array = jsNative

    [<Import("readFileSync", "node:fs")>]
    let readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("statSync", "node:fs")>]
    let statSync (path: string) : obj = jsNative

    [<Import("join", "node:path")>]
    let pathJoin (a: string, b: string) : string = jsNative

    [<Import("basename", "node:path")>]
    let pathBasename (p: string) : string = jsNative

type Frontier = Map<RuntimeId, int64>

type BootSnapshot =
    { Envelopes: Envelope list
      Frontier: Frontier
      Diagnostics: string list }

module Boot =

    let private getRuntimeIdFromFilename (filePath: string) : RuntimeId =
        let name = NodeFsBoot.pathBasename filePath
        let idx = name.LastIndexOf('.')
        let cleanName = if idx > 0 then name.Substring(0, idx) else name
        RuntimeId.create cleanName

    let captureFrontiers (directory: string) : Frontier =
        if not (NodeFsBoot.existsSync directory) then
            Map.empty
        else
            NodeFsBoot.readdirSync directory
            |> Array.filter (fun filePath -> filePath.EndsWith(".ndjson"))
            |> Array.map (fun name ->
                let filePath = NodeFsBoot.pathJoin (directory, name)
                let runtimeId = getRuntimeIdFromFilename filePath
                let stat = NodeFsBoot.statSync filePath
                let size = unbox<int64> (stat?size)
                (runtimeId, size))
            |> Map.ofArray

    let private parseLines (filePath: string) (frontierLength: int64) : Envelope list * string list =
        try
            if frontierLength <= 0L || not (NodeFsBoot.existsSync filePath) then
                [], []
            else
                let text = NodeFsBoot.readFileSync (filePath, "utf-8")

                let actualText =
                    if int64 text.Length > frontierLength then
                        text.Substring(0, int frontierLength)
                    else
                        text

                let lines =
                    actualText.Split([| "\r\n"; "\n" |], StringSplitOptions.RemoveEmptyEntries)

                let rec collect idx acc =
                    if idx >= lines.Length then
                        List.rev acc, []
                    else
                        match Envelope.deserialize lines.[idx] with
                        | Ok env -> collect (idx + 1) (env :: acc)
                        | Error err ->
                            let diag =
                                sprintf "Failed to parse line %d in %s: %s" idx (NodeFsBoot.pathBasename filePath) err

                            List.rev acc, [ diag ]

                collect 0 []
        with ex ->
            [], [ sprintf "IO error reading %s: %s" (NodeFsBoot.pathBasename filePath) ex.Message ]

    let private readPrefixEnvelopes (filePath: string) (frontierLength: int64) : Envelope list * string list =
        if frontierLength <= 0L || not (NodeFsBoot.existsSync filePath) then
            [], []
        else
            parseLines filePath frontierLength

    let kWayMerge (streams: Envelope list list) : Envelope list =
        let rec merge (queues: Envelope list list) acc =
            let active = queues |> List.filter (not << List.isEmpty)

            if List.isEmpty active then
                List.rev acc
            else
                let heads = active |> List.map List.head

                let minHeadEnv =
                    heads
                    |> List.reduce (fun acc env -> if Envelope.compareSortKey env acc < 0 then env else acc)

                let rec pickAndRemove headsList =
                    match headsList with
                    | [] -> [], []
                    | (hd :: tl) :: rest when Envelope.compareSortKey hd minHeadEnv = 0 -> tl :: rest, [ hd ]
                    | q :: rest ->
                        let remainingQueues, picked = pickAndRemove rest
                        q :: remainingQueues, picked

                let nextQueues, picked = pickAndRemove active
                merge nextQueues (picked.Head :: acc)

        merge streams []

    let boot (directory: string) : BootSnapshot =
        let frontier = captureFrontiers directory

        if not (NodeFsBoot.existsSync directory) then
            { Envelopes = []
              Frontier = Map.empty
              Diagnostics = [] }
        else
            let files =
                NodeFsBoot.readdirSync directory
                |> Array.filter (fun f -> f.EndsWith(".ndjson"))

            let mutable allDiags = []

            let streamEnvelopes =
                files
                |> Array.map (fun filename ->
                    let filePath = NodeFsBoot.pathJoin (directory, filename)
                    let runtimeId = getRuntimeIdFromFilename filePath
                    let len = Map.tryFind runtimeId frontier |> Option.defaultValue 0L
                    let envs, diags = readPrefixEnvelopes filePath len
                    allDiags <- allDiags @ diags
                    envs)
                |> Array.toList

            let mergedEnvelopes = kWayMerge streamEnvelopes

            { Envelopes = mergedEnvelopes
              Frontier = frontier
              Diagnostics = allDiags }
