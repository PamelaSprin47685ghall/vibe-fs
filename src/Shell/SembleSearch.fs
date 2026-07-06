module Wanxiangshu.Shell.SembleSearch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Thoth.Json

[<Import("Client", "@modelcontextprotocol/sdk/client/index.js")>]
type Client(info: obj, capabilities: obj) =
    member _.connect(transport: obj) : JS.Promise<unit> = jsNative
    member _.callTool(req: obj) : JS.Promise<obj> = jsNative
    member _.close() : JS.Promise<unit> = jsNative

[<Import("StdioClientTransport", "@modelcontextprotocol/sdk/client/stdio.js")>]
type StdioClientTransport(opts: obj) =
    class end

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

[<Import("appendFileSync", "node:fs")>]
let private appendFileSync (path: string) (content: string) (encoding: string) : unit = jsNative

type SembleResult =
    { filePath: string
      startLine: int
      endLine: int
      content: string
      score: float }

let private debugLogPath () : string =
    let dir = envVar "SEMBLE_INJECT_DEBUG_DIR"
    if dir = "" then "/tmp/wanxiangshu-semble-inject.log" else $"{dir}/wanxiangshu-semble-inject.log"

let debugEnabled () : bool = envVar "SEMBLE_INJECT_DEBUG" = "1"

let trace (tag: string) (detail: string) : unit =
    if not (debugEnabled ()) then ()
    else
        let ts = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        let line = $"[semble {ts}] {tag}: {detail}\n"
        try appendFileSync (debugLogPath ()) line "utf8" with _ -> ()

let dumpInjection (sessionID: string) (agent: string) (context: string) (results: SembleResult list) (pairCount: int) : unit =
    if not (debugEnabled ()) then ()
    else
        let resultLines =
            results
            |> List.mapi (fun i r ->
                $"  [{i}] {r.filePath}:{r.startLine}-{r.endLine} score={r.score}")
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

let buildReadToolParts (assistantId: string) (sessionID: string) (results: SembleResult list) : obj array =
    results
    |> List.mapi (fun i r ->
        let g = shortGuid ()
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
                "output", box (formatReadOutput r.filePath r.content r.startLine)
                "title", box $"Read {r.filePath}"
                "metadata", box (createObj [
                    "preview", box true
                    "truncated", box false
                    "loaded", box true
                    "display", box true
                ])
                "time", box (let t = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() in createObj [ "start", box t; "end", box (t + 1L) ])
            ])
        ]))
    |> Array.ofList

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

let mutable private _client: Client option = None
let mutable private _connecting: JS.Promise<Client option> option = None

let private getClient () : JS.Promise<Client option> =
    match _client with
    | Some c -> Promise.lift (Some c)
    | None ->
        match _connecting with
        | Some p -> p
        | None ->
            let p = promise {
                try
                    let c = Client(
                        box {| name = "wanxiangshu-semble"; version = "0.1.0" |},
                        box {| capabilities = {| tools = box [||] |} |})
                    let cmd = getSembleMcpCommand (envVar "SEMBLE_MCP_REF")
                    let argsStr = String.concat " " (Array.toList cmd.args)
                    trace "CONNECT" $"spawning {cmd.command} {argsStr}"
                    let transport = StdioClientTransport(box {|
                        command = cmd.command
                        args = cmd.args
                        stderr = "pipe"
                    |})
                    let stderrStream = Dyn.get transport "stderr"
                    if not (isNull stderrStream) then
                        let onData = System.Func<obj, unit>(fun chunk ->
                            let t = (string chunk).TrimEnd('\r', '\n')
                            if t <> "" then trace "STDERR" (t.[.. min 199 (t.Length - 1)]))
                        stderrStream?on("data", box onData) |> ignore
                    do! c.connect(transport)
                    _client <- Some c
                    _connecting <- None
                    trace "CONNECT" "ok"
                    return Some c
                with ex ->
                    _connecting <- None
                    trace "CONNECT" $"failed: {ex.Message}"
                    return None
            }
            _connecting <- Some p
            p

let private parseResults (result: obj) : SembleResult list =
    let content = Dyn.get result "content"
    if Dyn.isNullish content || not (Dyn.isArray content) then []
    else
        let arr = content :?> obj array
        if arr.Length = 0 then []
        else
            let first = arr.[0]
            if Dyn.isNullish first then []
            else
                let text = Dyn.str first "text"
                if text = "" then []
                else
                    try
                        match Decode.Auto.fromString<obj> text with
                        | Ok parsed ->
                            let results = Dyn.get parsed "results"
                            if Dyn.isNullish results || not (Dyn.isArray results) then []
                            else
                                results :?> obj array
                                |> Array.toList
                                |> List.choose (fun r ->
                                    let filePath = Dyn.str r "file_path"
                                    if filePath = "" then None
                                    else
                                        let line (key: string) : int =
                                            let v = Dyn.get r key
                                            if Dyn.isNullish v then 1 else unbox<int> v
                                        let scoreVal =
                                            let s = Dyn.get r "score"
                                            if Dyn.isNullish s then 0.0 else unbox<float> s
                                        Some
                                            { filePath = filePath
                                              startLine = line "start_line"
                                              endLine = line "end_line"
                                              content = Dyn.str r "content"
                                              score = scoreVal })
                        | Error _ -> []
                    with _ -> []

let search (query: string) (repoPath: string) (topK: int) : JS.Promise<SembleResult list> =
    promise {
        match! getClient () with
        | None ->
            trace "SEARCH" "skip: client not ready"
            return []
        | Some client ->
            try
                let! result = client.callTool(box {|
                    name = "search"
                    arguments = box {|
                        query = query
                        repo = repoPath
                        top_k = topK
                        max_snippet_lines = 20
                    |}
                |})
                let parsed = parseResults result
                trace "SEARCH" $"query='{query}' repo={repoPath} results={List.length parsed}"
                return parsed
            with ex ->
                trace "SEARCH" $"callTool failed: {ex.Message}"
                return []
    }
