module Wanxiangshu.Shell.SembleSearch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SembleMcp

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

let private readFileAsync (path: string) : JS.Promise<string> =
    promise { return readFileSync path "utf-8" }

[<Global("Buffer")>]
let private nodeBuffer : obj = jsNative

let private byteLength (str: string) (encoding: string) : int = unbox<int> (nodeBuffer?byteLength(str, encoding))

let private maxLineLength = 2000
let private maxBytes = 50 * 1024
let private defaultReadLimit = 2000

let dumpInjection (sessionID: string) (agent: string) (context: string) (results: SembleResult list) (pairCount: int) : unit =
    if not (debugEnabled ()) then ()
    else
        let resultLines =
            results
            |> List.mapi (fun i r -> $"  [{i}] {r.filePath}:{r.startLine}-{r.endLine} score={r.score}")
            |> String.concat "\n"
        let ctxHead = context.[.. min 199 (context.Length - 1)]
        let detail =
            $"session={sessionID} agent={agent} pairs={pairCount} ctxLen={context.Length}\n"
            + $"  ctx: {ctxHead}\n"
            + resultLines
        trace "INJECT" detail

let private shortGuid () =
    let g = System.Guid.NewGuid().ToString("N")
    g.[..7]

let readLinesForInjection (filePath: string) (offset: int) : JS.Promise<ReadSlice> =
    promise {
        let! raw = readFileAsync filePath
        let lines = raw.Split('\n')
        let startIdx = max 0 (offset - 1)
        let mutable bytes = 0
        let mutable count = 0
        let mutable more = false
        let mutable cut = false
        let acc = ResizeArray<string>()
        for i in 0 .. lines.Length - 1 do
            count <- count + 1
            if count <= startIdx then ()
            elif acc.Count >= defaultReadLimit then more <- true
            else
                let line =
                    if lines.[i].Length > maxLineLength
                    then lines.[i].Substring(0, maxLineLength) + $"... (line truncated to {maxLineLength} chars)"
                    else lines.[i]
                let size = byteLength line "utf-8" + (if acc.Count > 0 then 1 else 0)
                if bytes + size <= maxBytes then
                    acc.Add(line)
                    bytes <- bytes + size
                else
                    cut <- true
                    more <- true
        return { raw = acc.ToArray(); offset = offset; totalLines = lines.Length; more = more; cut = cut }
    }

let buildReadToolParts (assistantId: string) (sessionID: string) (results: SembleResult list) : JS.Promise<obj array> =
    promise {
        let resultsArr = results |> List.toArray
        let! slices =
            resultsArr
            |> Array.map (fun r -> readLinesForInjection r.filePath r.startLine)
            |> Promise.all
        return
            (resultsArr, slices)
            ||> Array.mapi2 (fun i r slice ->
                let g = shortGuid ()
                let truncated = slice.more || slice.cut
                let lineStart = slice.offset
                let lineEnd = slice.offset + slice.raw.Length - 1
                box (createObj [
                    "type", box "tool"
                    "tool", box "read"
                    "callID", box $"semble-call-{g}"
                    "id", box $"prt_{g}"
                    "sessionID", box sessionID
                    "messageID", box assistantId
                    "state", box (createObj [
                        "status", box "completed"
                        "input", box (createObj [ "filePath", box r.filePath; "offset", box r.startLine; "limit", box 2000 ])
                        "output", box (formatReadOutput r.filePath slice)
                        "title", box $"Read {r.filePath}"
                        "metadata", box (createObj [
                            "preview", box (if slice.raw.Length > 20 then slice.raw.[..19] |> String.concat "\n" else slice.raw |> String.concat "\n")
                            "truncated", box truncated
                            "loaded", box true
                            "display", box (createObj [
                                "type", box "file"
                                "path", box r.filePath
                                "text", box (slice.raw |> String.concat "\n")
                                "lineStart", box lineStart
                                "lineEnd", box lineEnd
                                "totalLines", box slice.totalLines
                                "truncated", box truncated
                            ])
                        ])
                        "time", box (let t = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() in createObj [ "start", box t; "end", box (t + 1L) ])
                    ])
                ]))
    }

let isBreakpoint (final: obj array) : bool =
    if final.Length = 0 then false
    else
        let last = final.[final.Length - 1]
        let info = Dyn.get last "info"
        Dyn.str info "role" = "toolResult"

let mutable private lastBreakpoint: Map<string, int> = Map.empty

let breakpointStart (sessionID: string) : int option = Map.tryFind sessionID lastBreakpoint

let markBreakpoint (sessionID: string) (index: int) : unit =
    lastBreakpoint <- Map.add sessionID index lastBreakpoint

/// Context = user/assistant text in [startIndex, end). Tool I/O excluded.
let extractContextFromMessages (startIndex: int) (messages: Message<'raw> list) : string =
    let rec safeSkip n xs =
        if n <= 0 then xs
        else match xs with [] -> [] | _ :: t -> safeSkip (n - 1) t
    safeSkip startIndex messages
    |> List.collect (fun m ->
        match m.info.role with
        | User | Assistant ->
            m.parts |> List.collect (fun part ->
                match part with
                | TextPart t when t <> "" -> [t]
                | RawPart raw ->
                    let r = box raw
                    if Dyn.str r "type" = "reasoning" then
                        let txt = Dyn.str r "text"
                        if txt <> "" then [txt] else []
                    else []
                | _ -> [])
        | _ -> [])
    |> String.concat "\n"
    |> fun s -> s.Trim()
