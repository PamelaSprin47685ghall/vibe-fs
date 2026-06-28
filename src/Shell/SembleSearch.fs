module Wanxiangshu.Shell.SembleSearch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn

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

type SembleResult =
    { filePath: string
      startLine: int
      endLine: int
      content: string
      score: float }

let formatReadOutput (content: string) (startLine: int) : string =
    content.Split('\n')
    |> Array.mapi (fun i line -> sprintf "%6d|%s" (startLine + i) (line.TrimEnd('\r')))
    |> String.concat "\n"

let private shortGuid () =
    let g = System.Guid.NewGuid().ToString("N")
    g.[..7]

let private synthId () = $"semble-synth-{shortGuid ()}"

let buildReadPair (sessionID: string) (agent: string) (result: SembleResult) : Message<obj> list =
    let callID = $"semble-{shortGuid ()}"
    let assistantId = synthId ()
    let resultId = synthId ()
    let baseInfo id role =
        { id = id
          sessionID = sessionID
          role = role
          agent = agent
          isError = false
          toolName = "read"
          details = null
          time = null }
    let assistantMsg : Message<obj> =
        { info = baseInfo assistantId Assistant
          parts = [ ToolPart("read", callID, None, null) ]
          source = classifySource assistantId
          raw = null }
    let toolResultMsg : Message<obj> =
        let output = formatReadOutput result.content result.startLine
        let state =
            { status = "completed"
              output = output
              error = ""
              input = box (createObj [
                  "path", box result.filePath
                  "offset", box result.startLine
                  "limit", box (result.endLine - result.startLine + 1)
              ])
              operationAction = "" }
        { info = baseInfo resultId ToolResult
          parts = [ ToolPart("read", callID, Some state, null) ]
          source = classifySource resultId
          raw = null }
    [ assistantMsg; toolResultMsg ]

let isBreakpoint (final: obj array) : bool =
    if final.Length = 0 then false
    else
        let last = final.[final.Length - 1]
        let info = Dyn.get last "info"
        Dyn.str info "role" = "toolResult"

let extractContextFromMessages (messages: Message<obj> list) : string =
    let rec loop (msgs: Message<obj> list) (acc: string list) =
        match msgs with
        | [] -> acc
        | m :: rest ->
            match m.info.role with
            | Assistant when m.parts |> List.exists (function ToolPart(_, _, None, _) -> true | _ -> false) -> acc
            | Assistant ->
                let texts = m.parts |> List.choose (function TextPart t when t <> "" -> Some t | _ -> None)
                loop rest (texts @ acc)
            | ToolResult ->
                let outputs = m.parts |> List.choose (function ToolPart(_, _, Some s, _) when s.output <> "" -> Some s.output | _ -> None)
                loop rest (outputs @ acc)
            | _ -> loop rest acc
    loop (List.rev messages) []
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
                    let transport = StdioClientTransport(box {|
                        command = cmd.command
                        args = cmd.args
                    |})
                    do! c.connect(transport)
                    _client <- Some c
                    _connecting <- None
                    return Some c
                with _ ->
                    _connecting <- None
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
                        let parsed = JS.JSON.parse text
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
                    with _ -> []

let search (query: string) (repoPath: string) (topK: int) : JS.Promise<SembleResult list> =
    promise {
        match! getClient () with
        | None -> return []
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
                return parseResults result
            with _ -> return []
    }
