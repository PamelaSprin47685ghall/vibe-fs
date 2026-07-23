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

    [<Import("openSync", "node:fs")>]
    let openSync (path: string, flags: string) : int = jsNative

    [<Import("readSync", "node:fs")>]
    let readSync (fd: int, buffer: obj, offset: int, length: int, position: obj) : int = jsNative

    [<Import("closeSync", "node:fs")>]
    let closeSync (fd: int) : unit = jsNative

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

    let private getStatSize (stat: obj) : int64 =
        stat?size |> unbox<double> |> int64

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
                let size = getStatSize stat
                (runtimeId, size))
            |> Map.ofArray

    let private readPrefixEnvelopes (filePath: string) (frontierBytes: int64) : Envelope list * string list =
        if not (NodeFsBoot.existsSync filePath) then
            [], []
        else
            let stat = NodeFsBoot.statSync filePath
            let actualFileSize = getStatSize stat
            let readLen = min frontierBytes actualFileSize
            if readLen <= 0L then
                [], []
            else
                let fd = NodeFsBoot.openSync (filePath, "r")
                let mutable res = [], []
                try
                    let buffer = Array.zeroCreate<byte> (int readLen)
                    let bytesRead = NodeFsBoot.readSync (fd, buffer, 0, int readLen, null)
                    let effectiveBytes =
                        if bytesRead <= 0 then [||]
                        else
                            let mutable lastNewline = -1
                            let mutable i = bytesRead - 1
                            while i >= 0 && lastNewline = -1 do
                                if buffer.[i] = 10uy then
                                    lastNewline <- i
                                i <- i - 1

                            if lastNewline = -1 then
                                [||]
                            else
                                buffer.[0 .. lastNewline]

                    let text = System.Text.Encoding.UTF8.GetString(effectiveBytes)
                    let lines = text.Split([| "\r\n"; "\n" |], StringSplitOptions.RemoveEmptyEntries)
                    let expectedRuntimeId = getRuntimeIdFromFilename filePath

                    let rec collect idx expectedSeq acc =
                        if idx >= lines.Length then
                            List.rev acc, []
                        else
                            match Envelope.deserialize lines.[idx] with
                            | Ok env ->
                                if env.RuntimeId <> expectedRuntimeId then
                                    let diag = sprintf "RuntimeId mismatch in %s: expected %s, got %s" (NodeFsBoot.pathBasename filePath) (RuntimeId.value expectedRuntimeId) (RuntimeId.value env.RuntimeId)
                                    List.rev acc, [ diag ]
                                else
                                    let seqVal = LocalSeq.value env.LocalSeq
                                    if seqVal <> expectedSeq then
                                        let diag = sprintf "LocalSeq anomaly in %s: expected %d, got %d" (NodeFsBoot.pathBasename filePath) expectedSeq seqVal
                                        List.rev acc, [ diag ]
                                    else
                                        collect (idx + 1) (expectedSeq + 1L) (env :: acc)
                            | Error err ->
                                let diag =
                                    sprintf "Failed to parse line %d in %s: %s" idx (NodeFsBoot.pathBasename filePath) err
                                List.rev acc, [ diag ]

                    res <- collect 0 1L []
                with ex ->
                    res <- [], [ sprintf "IO error reading %s: %s" (NodeFsBoot.pathBasename filePath) ex.Message ]
                try NodeFsBoot.closeSync fd with _ -> ()
                res

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
