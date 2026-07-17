module Wanxiangshu.Runtime.SembleMcp

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Runtime.Dyn
open Thoth.Json

[<Import("Client", "@modelcontextprotocol/sdk/client/index.js")>]
type Client(info: obj, capabilities: obj) =
    member _.connect(transport: obj) : JS.Promise<unit> = jsNative
    member _.callTool(req: obj) : JS.Promise<obj> = jsNative
    member _.close() : JS.Promise<unit> = jsNative

[<Import("StdioClientTransport", "@modelcontextprotocol/sdk/client/stdio.js")>]
type StdioClientTransport(opts: obj) = class end

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

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
      score: float
      totalLines: int }

let private debugLogPath () : string =
    let dir = envVar "SEMBLE_INJECT_DEBUG_DIR"

    if dir = "" then
        "/tmp/wanxiangshu-semble-inject.log"
    else
        $"{dir}/wanxiangshu-semble-inject.log"

let debugEnabled () : bool = envVar "SEMBLE_INJECT_DEBUG" = "1"

let trace (tag: string) (detail: string) : unit =
    if not (debugEnabled ()) then
        ()
    else
        let ts = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        let line = $"[semble {ts}] {tag}: {detail}\n"

        try
            appendFileSync (debugLogPath ()) line "utf8"
        with _ ->
            ()

let mutable private _client: Client option = None
let mutable private _connecting: JS.Promise<Client option> option = None

let private getClient () : JS.Promise<Client option> =
    match _client with
    | Some c -> Promise.lift (Some c)
    | None ->
        match _connecting with
        | Some p -> p
        | None ->
            let p =
                promise {
                    try
                        let c =
                            Client(
                                box
                                    {| name = "wanxiangshu-semble"
                                       version = "0.1.0" |},
                                box {| capabilities = {| tools = box [||] |} |}
                            )

                        let cmd = getSembleMcpCommand (envVar "SEMBLE_MCP_REF")
                        let argsStr = String.concat " " (Array.toList cmd.args)
                        trace "CONNECT" $"spawning {cmd.command} {argsStr}"

                        let transport =
                            StdioClientTransport(
                                box
                                    {| command = cmd.command
                                       args = cmd.args
                                       stderr = "pipe" |}
                            )

                        let stderrStream = Dyn.get transport "stderr"

                        if not (isNull stderrStream) then
                            let onData =
                                System.Func<obj, unit>(fun chunk ->
                                    let t = (string chunk).TrimEnd('\r', '\n')

                                    if t <> "" then
                                        trace "STDERR" (t.[.. min 199 (t.Length - 1)]))

                            stderrStream?on ("data", box onData) |> ignore

                        do! c.connect (transport)
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

    if Dyn.isNullish content || not (Dyn.isArray content) then
        []
    else
        let arr = content :?> obj array

        if arr.Length = 0 then
            []
        else
            let first = arr.[0]

            if Dyn.isNullish first then
                []
            else
                let text = Dyn.str first "text"

                if text = "" then
                    []
                else
                    try
                        match Decode.Auto.fromString<obj> text with
                        | Ok parsed ->
                            let results = Dyn.get parsed "results"

                            if Dyn.isNullish results || not (Dyn.isArray results) then
                                []
                            else
                                results :?> obj array
                                |> Array.toList
                                |> List.choose (fun r ->
                                    let filePath = Dyn.str r "file_path"

                                    if filePath = "" then
                                        None
                                    else
                                        let line (key: string) : int =
                                            let v = Dyn.get r key
                                            if Dyn.isNullish v then 1 else unbox<int> v

                                        let scoreVal =
                                            let s = Dyn.get r "score"
                                            if Dyn.isNullish s then 0.0 else unbox<float> s

                                        let sLine = line "start_line"
                                        let snippet = Dyn.str r "content"
                                        let snippetLines = snippet.Split('\n')
                                        let fallbackTotal = max snippetLines.Length (sLine + snippetLines.Length - 1)

                                        Some
                                            { filePath = filePath
                                              startLine = sLine
                                              endLine = line "end_line"
                                              content = snippet
                                              score = scoreVal
                                              totalLines = fallbackTotal })
                        | Error _ -> []
                    with _ ->
                        []

let search (query: string) (repoPath: string) (topK: int) : JS.Promise<SembleResult list> =
    promise {
        match! getClient () with
        | None ->
            trace "SEARCH" "skip: client not ready"
            return []
        | Some client ->
            try
                let! result =
                    client.callTool (
                        box
                            {| name = "search"
                               arguments =
                                box
                                    {| query = query
                                       repo = repoPath
                                       top_k = topK
                                       max_snippet_lines = 20 |} |}
                    )

                let parsed = parseResults result
                trace "SEARCH" $"query='{query}' repo={repoPath} results={List.length parsed}"
                return parsed
            with ex ->
                trace "SEARCH" $"callTool failed: {ex.Message}"
                return []
    }
