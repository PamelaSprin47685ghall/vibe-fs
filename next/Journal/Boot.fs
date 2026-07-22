namespace Wanxiangshu.Next.Journal

open System
open System.IO
open System.Text
open Wanxiangshu.Next.Kernel.Identity

type Frontier = Map<RuntimeId, int64>

type BootSnapshot =
    { Envelopes: Envelope list
      Frontier: Frontier
      Diagnostics: string list }

module Boot =

    let private getRuntimeIdFromFilename (filePath: string) : RuntimeId =
        let name = Path.GetFileNameWithoutExtension(filePath)
        RuntimeId.create name

    let captureFrontiers (directory: string) : Frontier =
        if not (Directory.Exists(directory)) then
            Map.empty
        else
            Directory.GetFiles(directory, "*.ndjson")
            |> Array.map (fun filePath ->
                let runtimeId = getRuntimeIdFromFilename filePath
                let fileInfo = FileInfo(filePath)
                (runtimeId, fileInfo.Length))
            |> Map.ofArray

    let private parseLines (filePath: string) (frontierLength: int64) : Envelope list * string list =
        try
            use stream =
                new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)

            let actualReadLen = min frontierLength stream.Length
            let buffer = Array.zeroCreate<byte> (int actualReadLen)
            let mutable bytesRead = 0
            let mutable continueLoop = true

            while bytesRead < buffer.Length && continueLoop do
                let n = stream.Read(buffer, bytesRead, buffer.Length - bytesRead)

                if n = 0 then
                    continueLoop <- false
                else
                    bytesRead <- bytesRead + n

            let text = Encoding.UTF8.GetString(buffer, 0, bytesRead)

            let fullText, partialDiag =
                if text.EndsWith("\n") then
                    text, None
                else
                    let lastNewline = text.LastIndexOf('\n')

                    if lastNewline < 0 then
                        if bytesRead > 0 then
                            match Envelope.deserialize text with
                            | Ok _ -> text, None
                            | Error _ ->
                                "", Some(sprintf "Partial trailing line ignored in %s" (Path.GetFileName(filePath)))
                        else
                            "", None
                    else
                        let prefix = text.Substring(0, lastNewline + 1)
                        let segment = text.Substring(lastNewline + 1)

                        if String.IsNullOrEmpty(segment) then
                            prefix, None
                        else
                            match Envelope.deserialize segment with
                            | Ok _ -> prefix + segment + "\n", None
                            | Error _ ->
                                prefix, Some(sprintf "Partial trailing line ignored in %s" (Path.GetFileName(filePath)))

            let lines =
                fullText.Split([| "\r\n"; "\n" |], StringSplitOptions.RemoveEmptyEntries)

            let rec collect idx acc =
                if idx >= lines.Length then
                    let initialDiags =
                        match partialDiag with
                        | Some d -> [ d ]
                        | None -> []

                    List.rev acc, initialDiags
                else
                    match Envelope.deserialize lines.[idx] with
                    | Ok env -> collect (idx + 1) (env :: acc)
                    | Error err ->
                        let diag =
                            sprintf "Failed to parse line %d in %s: %s" idx (Path.GetFileName(filePath)) err

                        let initialDiags =
                            match partialDiag with
                            | Some d -> [ d ]
                            | None -> []

                        List.rev acc, initialDiags @ [ diag ]

            collect 0 []
        with ex ->
            [], [ sprintf "IO error reading %s: %s" (Path.GetFileName(filePath)) ex.Message ]

    let private readPrefixEnvelopes (filePath: string) (frontierLength: int64) : Envelope list * string list =
        if frontierLength <= 0L || not (File.Exists(filePath)) then
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

        if not (Directory.Exists(directory)) then
            { Envelopes = []
              Frontier = Map.empty
              Diagnostics = [] }
        else
            let files = Directory.GetFiles(directory, "*.ndjson")
            let mutable allDiags = []

            let streamEnvelopes =
                files
                |> Array.map (fun filePath ->
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
