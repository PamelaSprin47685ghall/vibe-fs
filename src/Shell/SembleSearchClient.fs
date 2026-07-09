module Wanxiangshu.Shell.SembleSearchClient

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SembleSearchTypes

open Thoth.Json

[<Import("Client", "@modelcontextprotocol/sdk/client/index.js")>]
type Client(info: obj, capabilities: obj) =
    member _.connect(transport: obj) : JS.Promise<unit> = jsNative
    member _.callTool(req: obj) : JS.Promise<obj> = jsNative
    member _.close() : JS.Promise<unit> = jsNative

[<Import("StdioClientTransport", "@modelcontextprotocol/sdk/client/stdio.js")>]
type StdioClientTransport(opts: obj) = class end

[<Global("process")>]
let private nodeProcess: obj = jsNative

let private envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

[<Import("resolve", "node:path")>]
let private pathResolve (cwd: string) (filePath: string) : string = jsNative

[<Import("promises", "node:fs")>]
let private fsPromises: obj = jsNative

let private readFileAsync (p: string) : JS.Promise<string> = fsPromises?readFile (p, "utf-8")

let mutable private _client: Client option = None
let setClientForTest (c: Client option) : unit = _client <- c
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

let private enrichSembleResult (repoPath: string) (r: SembleResult) : JS.Promise<SembleResult> =
    promise {
        try
            let fullPath = pathResolve repoPath r.filePath
            let! content = readFileAsync fullPath
            let total = content.Split('\n').Length
            return { r with totalLines = total }
        with _ ->
            return r
    }

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
                let! enriched = parsed |> List.map (enrichSembleResult repoPath) |> Promise.all
                trace "SEARCH" $"query='{query}' repo={repoPath} results={List.length parsed}"
                return Array.toList enriched
            with ex ->
                trace "SEARCH" $"callTool failed: {ex.Message}"
                return []
    }
