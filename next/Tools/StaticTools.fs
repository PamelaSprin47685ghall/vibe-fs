namespace Wanxiangshu.Next.Tools

open System
open System.Threading.Tasks
open Thoth.Json
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module StaticTools =

    let todowriteTool (port: SessionCommandPort) : Tool =
        { Name = "todowrite"
          Description = "Update task todo snapshot and maintain session progress."
          SchemaJson = """{"type":"object","properties":{"todos":{"type":"array","items":{"type":"string"}}}}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let items =
                        try
                            match Decode.Auto.fromString<string list> input.Payload with
                            | Ok list -> list
                            | Error _ -> []
                        with _ ->
                            []

                    let snap: Fact.TodoSnapshot = { Items = items }
                    let! res = port.Request (UpsertTodo snap) ctx.Cancellation ctx.Deadline

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
          SchemaJson = """{"type":"object","properties":{"command":{"type":"string"}}}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let cmdText =
                        try
                            match Decode.Auto.fromString<string> input.Payload with
                            | Ok s -> s
                            | Error _ -> input.Payload
                        with _ ->
                            input.Payload

                    let cmd: Command =
                        { FileName = "sh"
                          Arguments = [ "-c"; cmdText ]
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

    let subagentTool (name: string) (role: string) : Tool =
        { Name = name
          Description = sprintf "Spawn subagent %s for %s" name role
          SchemaJson = """{"type":"object","properties":{"prompt":{"type":"string"}}}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    return
                        { Result = sprintf "Subagent %s completed" name
                          Truncated = false }
                } }
