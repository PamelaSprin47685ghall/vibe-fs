namespace Wanxiangshu.Next.Journal

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

module private NodeFsReader =
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("openSync", "node:fs")>]
    let openSync (path: string, flags: string) : int = jsNative

    [<Import("readSync", "node:fs")>]
    let readSync (fd: int, buffer: obj, offset: int, length: int, position: obj) : int = jsNative

    [<Import("closeSync", "node:fs")>]
    let closeSync (fd: int) : unit = jsNative

    [<Import("statSync", "node:fs")>]
    let statSync (path: string) : obj = jsNative

    [<Import("basename", "node:path")>]
    let pathBasename (p: string) : string = jsNative

module Reader =

    let getRuntimeIdFromFilename (filePath: string) : RuntimeId =
        let name = NodeFsReader.pathBasename filePath
        let idx = name.LastIndexOf('.')
        let cleanName = if idx > 0 then name.Substring(0, idx) else name
        RuntimeId.create cleanName

    let getStatSize (stat: obj) : int64 =
        if isNull stat || isNull stat?size then 0L
        else stat?size |> unbox<double> |> int64

    let readPrefixEnvelopes (filePath: string) (frontierBytes: int64) : Envelope list * string list =
        if not (NodeFsReader.existsSync filePath) then
            [], []
        else
            let stat = NodeFsReader.statSync filePath
            let actualFileSize = getStatSize stat
            let readLen = min frontierBytes actualFileSize
            if readLen <= 0L then
                [], []
            else
                let fd = NodeFsReader.openSync (filePath, "r")
                let mutable res = [], []
                try
                    let buffer = Array.zeroCreate<byte> (int readLen)
                    let bytesRead = NodeFsReader.readSync (fd, buffer, 0, int readLen, null)
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
                                    let diag = sprintf "RuntimeId mismatch in %s: expected %s, got %s" (NodeFsReader.pathBasename filePath) (RuntimeId.value expectedRuntimeId) (RuntimeId.value env.RuntimeId)
                                    List.rev acc, [ diag ]
                                else
                                    let seqVal = LocalSeq.value env.LocalSeq
                                    if seqVal <> expectedSeq then
                                        let diag = sprintf "LocalSeq anomaly in %s: expected %d, got %d" (NodeFsReader.pathBasename filePath) expectedSeq seqVal
                                        List.rev acc, [ diag ]
                                    else
                                        collect (idx + 1) (expectedSeq + 1L) (env :: acc)
                            | Error err ->
                                let diag =
                                    sprintf "Failed to parse line %d in %s: %s" idx (NodeFsReader.pathBasename filePath) err
                                List.rev acc, [ diag ]

                    res <- collect 0 1L []
                with ex ->
                    res <- [], [ sprintf "IO error reading %s: %s" (NodeFsReader.pathBasename filePath) ex.Message ]
                try NodeFsReader.closeSync fd with _ -> ()
                res
