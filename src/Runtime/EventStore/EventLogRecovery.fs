module Wanxiangshu.Runtime.EventLogRecovery

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Runtime.EventLogCodec

[<Import("createHash", "crypto")>]
let private createHash (algorithm: string) : obj = jsNative

let computeSha256 (buf: obj) : string =
    let hashObj = createHash "sha256"
    unbox<string> (hashObj?update(buf)?digest("hex"))

[<Import("mkdir", "fs/promises")>]
let private mkdirAsync (path: string, options: obj) : JS.Promise<unit> = jsNative

[<Import("writeFile", "fs/promises")>]
let private writeRecoveryFileAsync (path: string, data: obj) : JS.Promise<unit> = jsNative

[<Import("readdir", "fs/promises")>]
let private readdirAsync (path: string) : JS.Promise<string[]> = jsNative

[<Import("unlink", "fs/promises")>]
let private unlinkAsync (path: string) : JS.Promise<unit> = jsNative

[<Import("stat", "fs/promises")>]
let private statAsync (path: string) : JS.Promise<obj> = jsNative

[<Import("open", "node:fs/promises")>]
let private openFileHandleAsync (path: string) (flags: string) : JS.Promise<obj> = jsNative

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let saveCorruptTail (workspaceRoot: string) (tailBuf: obj) (tailHash: string) : JS.Promise<unit> =
    promise {
        try
            let dir = sprintf "%s/.wanxiangshu-recovery" workspaceRoot
            do! mkdirAsync (dir, {| recursive = true |})
            let timestamp = System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")
            let filePath = sprintf "%s/ndjson-tail-%s-%s.bin" dir timestamp tailHash
            do! writeRecoveryFileAsync (filePath, tailBuf)
            let! files = readdirAsync dir
            let tailFiles = files |> Array.filter (fun f -> f.StartsWith("ndjson-tail-") && f.EndsWith(".bin"))
            let! filesWithStats =
                tailFiles
                |> Array.map (fun f ->
                    promise {
                        let fullPath = sprintf "%s/%s" dir f
                        let! st = statAsync fullPath
                        let mtime = unbox<float> st?mtimeMs
                        return (f, mtime)
                    })
                |> Promise.all
            let sortedFiles = filesWithStats |> Array.sortBy snd |> Array.map fst
            if sortedFiles.Length > 3 then
                for i = 0 to sortedFiles.Length - 4 do
                    do! unlinkAsync (sprintf "%s/%s" dir sortedFiles.[i])
        with ex ->
            printfn "Error saving corrupt tail to disk: %s" ex.Message
            return raise ex
    }

let buildRepairEvent (badOffset: int) (removedBytes: int) (badLine: int) (reason: string) (tailHash: string) : string =
    let repairEvent =
        { V = 1
          Session = "system"
          Kind = "event_log_repaired"
          At = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
          Payload = Map [
            "badOffset", string badOffset
            "removedBytes", string removedBytes
            "badLine", string badLine
            "reason", reason
            "tailHash", tailHash
            "repairVersion", "1"
          ]
          EventId = None
          WriterId = None
          Sequence = None
          Checksum = None }
    wanEventToLine repairEvent + "\n"

let executeTruncateAndAppend 
    (workspaceRoot: string)
    (handle: obj) 
    (validOffset: int) 
    (needsNewline: bool) 
    (badOffset: int) 
    (badLine: int) 
    (reason: string) 
    (removedBytes: int) 
    (tailBuf: obj) =
    promise {
        do! handle?truncate(validOffset)
        do! handle?sync()
        let mutable currentOffset = validOffset
        if needsNewline then
            let newlineBuf = nodeBuffer?from("\n", "utf-8")
            let! _ = handle?write(newlineBuf, 0, 1, float currentOffset)
            do! handle?sync()
            currentOffset <- currentOffset + 1
        let tailHash = computeSha256 tailBuf
        do! saveCorruptTail workspaceRoot tailBuf tailHash
        let repairLine = buildRepairEvent badOffset removedBytes badLine reason tailHash
        let repairBuf = nodeBuffer?from(repairLine, "utf-8")
        let! _ = handle?write(repairBuf, 0, repairBuf?length, float currentOffset)
        do! handle?sync()
        return ()
    }

let repairAndTruncateFile (workspaceRoot: string) (filePath: string) : JS.Promise<unit> =
    promise {
        let! handle = openFileHandleAsync filePath "r+"
        try
            let! stats = handle?stat()
            let size = unbox<int> stats?size
            if size > 0 then
                let buffer = nodeBuffer?alloc(size)
                let! readRes1 = handle?read(buffer, 0, size, 0)
                match scanEventLog buffer with
                | Clean _ -> ()
                | ValidFinalLineMissingNewline(validOffset, _) ->
                    do! handle?truncate(validOffset)
                    do! handle?sync()
                    let newlineBuf = nodeBuffer?from("\n", "utf-8")
                    let! _ = handle?write(newlineBuf, 0, 1, float validOffset)
                    do! handle?sync()
                    return ()
                | CorruptTail(validOffset, badOffset, badLine, reason, removedBytes, _)
                | CorruptMiddle(validOffset, badOffset, badLine, reason, removedBytes, _) ->
                    let mutable needsNewline = false
                    if validOffset > 0 then
                        let lastByteBuf = nodeBuffer?alloc(1)
                        let! readRes2 = handle?read(lastByteBuf, 0, 1, float (validOffset - 1))
                        if unbox<byte> (lastByteBuf?(0)) <> 10uy then
                            needsNewline <- true
                    let tailBuf = buffer?subarray(validOffset, size)
                    do! executeTruncateAndAppend workspaceRoot handle validOffset needsNewline badOffset badLine reason removedBytes tailBuf
        finally
            handle?close() |> ignore
    }