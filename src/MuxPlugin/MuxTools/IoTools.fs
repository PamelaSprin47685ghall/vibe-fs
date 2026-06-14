module VibeFs.MuxPlugin.MuxTools.IoTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.ExecutorKernel
open VibeFs.Kernel.Permission
open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.Delegate
open VibeFs.MuxPlugin.MuxTools.Shared
open VibeFs.Opencode.ToolCopy
open VibeFs.Shell.ExecutorShell
open VibeFs.Shell.TreeSitterShell

[<Emit("import('node:fs/promises')")>]
let private fsAsync () : JS.Promise<obj> = jsNative
[<Import("resolve", "node:path")>]
let private pathResolve (cwd: string) (p: string) : string = jsNative
[<Import("dirname", "node:path")>]
let private pathDirname (p: string) : string = jsNative
[<Emit("$0.mkdir($1, { recursive: true })")>]
let private mkdir (fs': obj) (dir: string) : JS.Promise<unit> = jsNative
[<Emit("$0.writeFile($1, $2, 'utf-8')")>]
let private writeFile (fs': obj) (p: string) (content: string) : JS.Promise<unit> = jsNative

let private summarizerDisabledTools : string list =
    canonicalToolNames @ [ "read"; "write"; "edit"; "bash"; "bash_.*"; "task"; "task_.*"; "patch"; "fetch"; "fetch_.*"; "webfetch"; "webfetch_.*"; "websearch"; "websearch_.*"; "stealth_browser_mcp_.*" ]
    |> List.distinct

let private summarizeOutput (deps: obj) (config: obj) (options: ExecuteOptions) (result: ExecuteResult) : Async<string> =
    async {
        let prompt = Prompts.executorSummarizerSystemPrompt + "\n\n" + buildSummaryPrompt options result
        let experiments =
            createObj [ "subagentRole", box "summarizer"
                        "toolPolicy", box (createObj [ "disabledTools", box (Array.ofList summarizerDisabledTools) ]) ]
        let opts = createObj [ "aiSettingsAgentId", box "explore"; "experiments", box experiments ]
        return! delegateToSubAgent deps config "explore" prompt "Executor summary" (Some opts) |> Async.AwaitPromise
    }

let executorTool (deps: obj) : ToolDefinition =
    { name = "executor"
      description = executor
      parameters = mkSchema (createObj [ "language", box (strEnumProp Params.executorLanguage [| "shell"; "python"; "javascript" |]); "program", box (strProp Params.executorProgram); "dependencies", box (strArrayProp Params.executorDeps); "timeout_type", box (strEnumProp Params.executorTimeout [| "short"; "long" |]) ]) [| "language"; "program"; "timeout_type" |]
      execute = fun config args ->
          if strField config "workspaceId" = None then resolveStr "executor requires workspaceId"
          else
              let lang = match Dyn.str args "language" with "python" -> Python | "javascript" -> Javascript | _ -> Shell
              let timeout = match Dyn.str args "timeout_type" with "long" -> Long | _ -> Short
              let deps = if Dyn.isNullish (Dyn.get args "dependencies") then [] else Dyn.get args "dependencies" :?> obj array |> Array.map string |> List.ofArray
              let options : ExecuteOptions =
                  { program = Dyn.str args "program"; language = lang; dependencies = deps
                    timeoutType = timeout; cwd = Some (Dyn.str config "cwd") }
              async {
                  let! result = execute options (Dyn.str config "workspaceId") |> Async.AwaitPromise
                  let output = match result with Completed o | Truncated(o, _) | Failed o -> o | MissingExecutable(_, o) -> o
                  if not (shouldSummarize output) then return output
                  else return! summarizeOutput deps config options result
              } |> Async.StartAsPromise
      condition = None }

let writeTool : ToolDefinition =
    { name = "write"
      description = "Write content to a file. Resolves relative paths against the current working directory, creates parent directories if they don't exist, and runs syntax checking on the written content."
      parameters = mkSchema (createObj [ "file_path", box (strProp "The absolute or relative path of the file to write"); "content", box (strProp "The content to write to the file") ]) [| "file_path"; "content" |]
      execute = fun config args ->
          match strField args "file_path", strField args "content" with
          | None, _ -> resolveStr "Error: `file_path` must be a string"
          | _, None -> resolveStr "Error: `content` must be a string"
          | Some filePath, Some content ->
              async {
                  let! api = fsAsync () |> Async.AwaitPromise
                  let resolved = pathResolve (Dyn.str config "cwd") filePath
                  do! mkdir api (pathDirname resolved) |> Async.AwaitPromise
                  do! writeFile api resolved content |> Async.AwaitPromise
                  let! diagnostics = appendSyntaxDiagnostics resolved content false |> Async.AwaitPromise
                  let baseMsg = "Successfully wrote to " + resolved
                  return match diagnostics with Some d -> baseMsg + "\n\n" + d | None -> baseMsg
              } |> Async.StartAsPromise
      condition = None }

let mutable hostFileReadExecute : obj option = None

[<Emit("$0.readdir($1, { withFileTypes: true })")>]
let private readdirWithTypes (fs': obj) (dir: string) : JS.Promise<obj array> = jsNative
[<Emit("$0.stat($1)")>]
let private statAsync (fs': obj) (p: string) : JS.Promise<obj> = jsNative
[<Emit("$0.readFile($1, 'utf-8')")>]
let private readFileAsync (fs': obj) (p: string) : JS.Promise<string> = jsNative
[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

let private direntName (e: obj) : string = e?name
let private direntIsDir (e: obj) : bool = e?isDirectory ()
let private direntIsLink (e: obj) : bool = e?isSymbolicLink ()

let private statIsDirectory (s: obj) : bool = s?isDirectory ()
let private statIsFile (s: obj) : bool = s?isFile ()
let private statSizeOf (s: obj) : int = s?size
let private statMtimeMs (s: obj) : float = s?mtimeMs

[<Emit("(() => { const d = new Date($0); const p = (n) => String(n).padStart(2, '0'); return d.getFullYear()+'-'+p(d.getMonth()+1)+'-'+p(d.getDate())+' '+p(d.getHours())+':'+p(d.getMinutes()); })($0)")>]
let private formatTime (ms: float) : string = jsNative

let listDirectoryEntries (dirPath: string) : JS.Promise<string> =
    async {
        try
            let! fsApi = fsAsync () |> Async.AwaitPromise
            let! entries = readdirWithTypes fsApi dirPath |> Async.AwaitPromise
            let visible =
                entries
                |> Array.filter (fun e -> not (ExcludedDirs.isExcludedDir (direntName e)))
            let! details =
                visible
                |> Array.map (fun e -> async {
                    let name = direntName e
                    let isDir = direntIsDir e
                    let isLink = direntIsLink e
                    let typeChar = if isDir then 'd' elif isLink then 'l' else '-'
                    let suffix = if isDir then "/" else ""
                    try
                        let! s = statAsync fsApi (pathJoin dirPath name) |> Async.AwaitPromise
                        let size = statSizeOf s |> string
                        let mtime = formatTime (statMtimeMs s)
                        return (name, typeChar, isDir, size, mtime, suffix)
                    with _ ->
                        return (name, typeChar, isDir, "?", "?", suffix)
                })
                |> Async.Parallel
            let sorted = details |> Array.sortBy (fun (name, _, isDir, _, _, _) -> (not isDir, name))
            let lines =
                sorted
                |> Array.map (fun (name, typeChar, _, size, mtime, suffix) ->
                    sprintf "%c  %s  %s  %s%s" typeChar size mtime name suffix)
            return String.concat "\n" lines
        with ex ->
            return sprintf "Error: failed to list directory `%s`: %s" dirPath (ex.Message)
    } |> Async.StartAsPromise

let readFileWithLineNumbers (filePath: string, offset: int option, limit: int option) : JS.Promise<string> =
    async {
        try
            let! fsApi = fsAsync () |> Async.AwaitPromise
            let! content = readFileAsync fsApi filePath |> Async.AwaitPromise
            let lines =
                if content = "" then [||]
                else
                    let normalized = content.Replace("\r\n", "\n")
                    let arr = normalized.Split('\n')
                    if arr.Length > 0 && arr.[arr.Length - 1] = "" then arr.[0..arr.Length - 2] else arr
            let sliceStart = (offset |> Option.defaultValue 1) - 1
            let sliceEnd = match limit with Some l -> sliceStart + l | None -> lines.Length
            if sliceStart >= lines.Length || sliceEnd <= sliceStart then
                return ""
            else
                let endIdx = min sliceEnd lines.Length
                let slice = lines.[sliceStart..endIdx - 1]
                let numbered = slice |> Array.mapi (fun i line -> sprintf "%d: %s" (sliceStart + i + 1) line)
                return String.concat "\n" numbered
        with ex ->
            return sprintf "Error: failed to read `%s`: %s" filePath (ex.Message)
    } |> Async.StartAsPromise

let readTool : ToolDefinition =
    { name = "read"
      description = "If path is a directory, returns a formatted directory listing (equivalent to ls -la). Use this instead of running `ls` via runner."
      parameters =
          mkSchema
              (createObj
                  [ "path", box (strProp "The absolute or relative path to read")
                    "offset", box (numProp "Line to start from, 1-indexed")
                    "limit", box (numProp "Maximum lines to read") ])
              [| "path" |]
      execute = fun config args ->
          match strField args "path" with
          | None -> resolveStr "Error: `path` must be a string"
          | Some filePath ->
              async {
                  try
                      let! fsApi = fsAsync () |> Async.AwaitPromise
                      let resolved = pathResolve (Dyn.str config "cwd") filePath
                      let! stat = statAsync fsApi resolved |> Async.AwaitPromise
                      if statIsDirectory stat then
                          return! listDirectoryEntries resolved |> Async.AwaitPromise
                      elif statIsFile stat then
                          let offset = optInt args "offset"
                          let limit = optInt args "limit"
                          match hostFileReadExecute with
                          | Some fn ->
                              let hostArgs = {| path = resolved; offset = offset; limit = limit |}
                              let hostOpts = {| abortSignal = Dyn.get config "abortSignal" |}
                              let! result = Dyn.call2 fn hostArgs hostOpts :?> JS.Promise<obj> |> Async.AwaitPromise
                              if Dyn.truthy (Dyn.get result "success") then
                                  return string (Dyn.get result "content")
                              else
                                  return string (Dyn.get result "error")
                          | None ->
                              return! readFileWithLineNumbers (resolved, offset, limit) |> Async.AwaitPromise
                      else
                          return sprintf "Error: `%s` is not a file or directory" resolved
                  with ex ->
                      return sprintf "Error: failed to read `%s`: %s" filePath (ex.Message)
              } |> Async.StartAsPromise
      condition = None }
