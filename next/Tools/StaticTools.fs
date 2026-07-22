namespace Wanxiangshu.Next.Tools

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module NodeFs =
    [<Import("readFileSync", "fs")>]
    let readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("writeFileSync", "fs")>]
    let writeFileSync (path: string, data: string, encoding: string) : unit = jsNative

    [<Import("existsSync", "fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("statSync", "fs")>]
    let statSync (path: string) : obj = jsNative

module NodeProcess =
    [<Import("platform", "process")>]
    let platform: string = jsNative

module StaticTools =

    let todowriteTool (port: SessionCommandPort) : Tool =
        { Name = "todowrite"
          Description = "Update task todo snapshot, report progress, and methodology."
          SchemaJson =
            """{"type":"object","properties":{"todos":{"type":"array","items":{"type":"string"}}},"required":["todos"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let items =
                        try
                            let decoder =
                                Decode.object (fun get -> get.Required.Field "todos" (Decode.list Decode.string))

                            match Decode.fromString decoder input.Payload with
                            | Ok list -> list
                            | Error _ ->
                                match Decode.Auto.fromString<string list> input.Payload with
                                | Ok list -> list
                                | Error _ -> []
                        with _ ->
                            []

                    let snap: Fact.TodoSnapshot = { Items = items }
                    let! res = port.Request (UpsertTodo(snap, ignore)) ctx.Cancellation ctx.Deadline

                    match res with
                    | Ok _ ->
                        return
                            { Result = sprintf "Updated %d todo items" items.Length
                              Truncated = false }
                    | Error err ->
                        return
                            { Result = sprintf "Failed: %A" err
                              Truncated = false }
                } }

    let executorTool () : Tool =
        { Name = "executor"
          Description = "Execute shell command within timeout budget."
          SchemaJson = """{"type":"object","properties":{"command":{"type":"string"}},"required":["command"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let cmdText =
                        try
                            let decoder = Decode.field "command" Decode.string

                            match Decode.fromString decoder input.Payload with
                            | Ok s -> s
                            | Error _ ->
                                match Decode.Auto.fromString<string> input.Payload with
                                | Ok s -> s
                                | Error _ -> input.Payload
                        with _ ->
                            input.Payload

                    let isWindows = NodeProcess.platform = "win32"
                    let fileName = if isWindows then "cmd.exe" else "sh"
                    let argFlag = if isWindows then "/c" else "-c"

                    let cmd: Command =
                        { FileName = fileName
                          Arguments = [ argFlag; cmdText ]
                          WorkingDirectory = None
                          Environment = None
                          Stdin = None
                          Deadline = Some ctx.Deadline }

                    let procCtx: ProcessContext =
                        { WorkingDirectory = None
                          DefaultTimeout = Some(TimeSpan.FromSeconds 30.0) }

                    let! res = ProcessFlows.runFlow procCtx ctx.Cancellation (ProcessFlows.execute cmd)

                    match res with
                    | Ok procRes ->
                        let resultText =
                            sprintf "Exit: %d\nStdout: %s\nStderr: %s" procRes.ExitCode procRes.Stdout procRes.Stderr

                        return
                            { Result = resultText
                              Truncated = procRes.StdoutTruncated || procRes.StderrTruncated }
                    | Error err ->
                        return
                            { Result = sprintf "Error: %A" err
                              Truncated = false }
                } }

    let fileReadTool () : Tool =
        { Name = "read"
          Description = "Read file content from filesystem."
          SchemaJson = """{"type":"object","properties":{"filePath":{"type":"string"}},"required":["filePath"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let filePath =
                        try
                            let decoder = Decode.field "filePath" Decode.string

                            match Decode.fromString decoder input.Payload with
                            | Ok p -> p
                            | Error _ ->
                                match Decode.Auto.fromString<string> input.Payload with
                                | Ok p -> p
                                | Error _ -> input.Payload
                        with _ ->
                            input.Payload

                    if not (NodeFs.existsSync filePath) then
                        return
                            { Result = sprintf "File not found: %s" filePath
                              Truncated = false }
                    else
                        let content = NodeFs.readFileSync (filePath, "utf8")
                        return { Result = content; Truncated = false }
                } }

    let fileWriteTool () : Tool =
        { Name = "write"
          Description = "Write file content to filesystem."
          SchemaJson =
            """{"type":"object","properties":{"filePath":{"type":"string"},"content":{"type":"string"}},"required":["filePath","content"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let parsedOpt =
                        try
                            let decoder =
                                Decode.object (fun get ->
                                    let path = get.Required.Field "filePath" Decode.string
                                    let c = get.Required.Field "content" Decode.string
                                    (path, c))

                            match Decode.fromString decoder input.Payload with
                            | Ok res -> Some res
                            | Error _ -> None
                        with _ ->
                            None

                    match parsedOpt with
                    | None ->
                        return
                            { Result = sprintf "Failed to parse JSON payload for write tool: %s" input.Payload
                              Truncated = false }
                    | Some(filePath, content) ->
                        NodeFs.writeFileSync (filePath, content, "utf8")
                        let stat = NodeFs.statSync filePath

                        let size =
                            if isNull stat || isNull stat?size then
                                content.Length
                            else
                                unbox<int> stat?size

                        return
                            { Result = sprintf "Wrote %s (%d bytes)" filePath size
                              Truncated = false }
                } }

    let fileEditTool () : Tool =
        { Name = "edit"
          Description = "Edit file content in filesystem using exact string replacement."
          SchemaJson =
            """{"type":"object","properties":{"filePath":{"type":"string"},"oldString":{"type":"string"},"newString":{"type":"string"}},"required":["filePath","oldString","newString"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let parsedOpt =
                        try
                            let decoder =
                                Decode.object (fun get ->
                                    let path = get.Required.Field "filePath" Decode.string
                                    let oldStr = get.Required.Field "oldString" Decode.string
                                    let newStr = get.Required.Field "newString" Decode.string
                                    (path, oldStr, newStr))

                            match Decode.fromString decoder input.Payload with
                            | Ok res -> Some res
                            | Error _ -> None
                        with _ ->
                            None

                    match parsedOpt with
                    | None ->
                        return
                            { Result = sprintf "Invalid edit payload: %s" input.Payload
                              Truncated = false }
                    | Some(filePath, oldString, newString) ->
                        if not (NodeFs.existsSync filePath) then
                            return
                                { Result = sprintf "File not found: %s" filePath
                                  Truncated = false }
                        else
                            let content = NodeFs.readFileSync (filePath, "utf8")

                            if not (content.Contains oldString) then
                                return
                                    { Result = sprintf "oldString not found in file %s" filePath
                                      Truncated = false }
                            else
                                NodeFs.writeFileSync (filePath, content.Replace(oldString, newString), "utf8")

                                return
                                    { Result = sprintf "Edited %s" filePath
                                      Truncated = false }
                } }

    let submitReviewTool (port: SessionCommandPort) : Tool =
        { Name = "submit_review"
          Description = "Submit review task result."
          SchemaJson = """{"type":"object","properties":{"report":{"type":"string"}},"required":["report"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let reportText =
                        try
                            let decoder = Decode.field "report" Decode.string

                            match Decode.fromString decoder input.Payload with
                            | Ok r -> r
                            | Error _ -> input.Payload
                        with _ ->
                            input.Payload

                    let cmd = SubmitReview(reportText, ignore)
                    let! res = port.Request cmd ctx.Cancellation ctx.Deadline

                    match res with
                    | Ok SessionCommandResult.ReviewSubmitted ->
                        return
                            { Result = "Review submitted and structured fact recorded"
                              Truncated = false }
                    | Ok _ ->
                        return
                            { Result = "Review submitted"
                              Truncated = false }
                    | Error err ->
                        return
                            { Result = sprintf "Failed to record review submission: %A" err
                              Truncated = false }
                } }

    let returnReviewerTool (port: SessionCommandPort) : Tool =
        { Name = "return_reviewer"
          Description = "Return verdict from reviewer."
          SchemaJson = """{"type":"object","properties":{"verdict":{"type":"string"}},"required":["verdict"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let verdictText =
                        try
                            let decoder = Decode.field "verdict" Decode.string

                            match Decode.fromString decoder input.Payload with
                            | Ok v -> v
                            | Error _ -> input.Payload
                        with _ ->
                            input.Payload

                    let cmd = ReturnVerdict(verdictText, ignore)
                    let! res = port.Request cmd ctx.Cancellation ctx.Deadline

                    match res with
                    | Ok SessionCommandResult.VerdictReturned ->
                        return
                            { Result = sprintf "Reviewer verdict returned: %s" verdictText
                              Truncated = false }
                    | Ok _ ->
                        return
                            { Result = sprintf "Reviewer verdict returned: %s" verdictText
                              Truncated = false }
                    | Error err ->
                        return
                            { Result = sprintf "Failed to record reviewer verdict: %A" err
                              Truncated = false }
                } }

    let subagentTool (name: string) (role: string) (script: ChildScript) : Tool =
        { Name = name
          Description = sprintf "Spawn subagent %s for %s" name role
          SchemaJson = """{"type":"object","properties":{"prompt":{"type":"string"}},"required":["prompt"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let promptText =
                        try
                            let decoder = Decode.field "prompt" Decode.string

                            match Decode.fromString decoder input.Payload with
                            | Ok p -> p
                            | Error _ -> input.Payload
                        with _ ->
                            input.Payload

                    let req: ChildRequest = { Prompt = promptText }
                    let flow = ChildFlows.runChild script req
                    let! res = Flow.run script ctx.Cancellation flow

                    match res with
                    | Ok(CompletedChild out) ->
                        return
                            { Result = sprintf "Subagent %s completed: %s" name out
                              Truncated = false }
                    | Ok(FailedChild err) ->
                        return
                            { Result = sprintf "Subagent %s failed: %s" name err
                              Truncated = false }
                    | Error err ->
                        return
                            { Result = sprintf "Subagent %s flow error: %A" name err
                              Truncated = false }
                } }
