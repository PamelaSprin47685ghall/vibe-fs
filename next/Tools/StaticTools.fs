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
                    let mutable replyVal = Ok SessionCommandResult.Upserted
                    let! res = port.Request (UpsertTodo(snap, (fun r -> replyVal <- r))) ctx.Cancellation ctx.Deadline

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
                          Deadline = Some ctx.Deadline
                          PtyOptions = None }

                    let procCtx: ProcessContext =
                        { WorkingDirectory = None
                          DefaultTimeout = Some(Deadline.remaining (fun () -> DateTimeOffset.UtcNow) ctx.Deadline) }

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
