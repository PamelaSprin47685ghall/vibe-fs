namespace Wanxiangshu.Next.OpenCode

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module ExecutorTool =
    [<Emit("Math.random().toString(36).slice(2, 8)")>]
    let private newAgentId () : string = jsNative

    [<Emit("$0.schema.string()")>]
    let private stringSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.number()")>]
    let private numberSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.enum($1)")>]
    let private enumSchema (tool: obj) (values: string array) : obj = jsNative

    [<Emit("$0($1)")>]
    let private applyTool (factory: obj) (definition: obj) : obj = jsNative

    [<Emit("(args, context) => $0(args, context)")>]
    let private uncurriedExecute (fn: obj) : obj = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringify (value: obj) : string = jsNative

    let private textArg (args: obj) name =
        if isNull args || isNull args?(name) then
            ""
        else
            unbox<string> args?(name)

    let private intArg (args: obj) name fallback =
        if isNull args || isNull args?(name) then
            fallback
        else
            unbox<int> args?(name)

    let private memoryArg (args: obj) =
        match textArg args "estimated_mem_usage" with
        | "large" -> EstimatedMemory.Large
        | _ -> EstimatedMemory.Medium

    let private completionText (completion: RunCompletion) =
        match completion.Outcome with
        | Ok text -> text
        | Error error -> raise (InvalidOperationException error)

    let private summarizeChunk (runtime: HostForkRuntime) (chunk: byte[]) (index: int) =
        task {
            let content = Encoding.UTF8.GetString chunk

            let prompt =
                sprintf
                    "Summarize command output chunk %d. Preserve errors, decisions, paths, and exact numbers; omit raw code.\n%s"
                    index
                    content

            let! fork = runtime.Fork(newAgentId (), AgentRole.Executor, prompt)

            match fork with
            | Error error -> return raise (InvalidOperationException error)
            | Ok _ ->
                let! completion = runtime.Join()

                match completion with
                | Error error -> return raise (InvalidOperationException(error.ToString()))
                | Ok result -> return completionText result
        }

    let private summarize (runtime: HostForkRuntime) (chunks: byte[][]) =
        task {
            let summaries = ResizeArray<string>()

            for index in 0 .. chunks.Length - 1 do
                let! summary = summarizeChunk runtime chunks.[index] index
                summaries.Add summary

            let combined = String.concat "\n" summaries

            let reducePrompt =
                sprintf
                    "Reduce these command-output summaries into one dense report. Preserve failures and exact facts; do not include raw code.\n%s"
                    combined

            let! fork = runtime.Fork(newAgentId (), AgentRole.Executor, reducePrompt)

            match fork with
            | Error error -> return raise (InvalidOperationException error)
            | Ok _ ->
                let! completion = runtime.Join()

                match completion with
                | Error error -> return raise (InvalidOperationException(error.ToString()))
                | Ok result -> return completionText result
        }

    let create
        (toolModule: obj)
        (runtimeFor: obj -> Result<HostForkRuntime, string>)
        (workspaceDirectory: string option)
        : obj =
        let factory = toolModule?tool

        let execute (args: obj) (context: obj) =
            task {
                match runtimeFor context with
                | Error error -> return box (stringify (createObj [ "error", box error ]))
                | Ok runtime ->
                    let commandText = textArg args "command"

                    if String.IsNullOrWhiteSpace commandText then
                        return box (stringify (createObj [ "error", box "Missing command" ]))
                    else
                        let estimate =
                            { EstimatedRuntime = RuntimeSeconds(float (intArg args "estimated_running_secs" 30))
                              EstimatedOutput = OutputBytes(int64 (intArg args "estimated_output_bytes" 65536))
                              EstimatedMemory = memoryArg args }

                        let command =
                            { FileName = "sh"
                              Arguments = [ "-lc"; commandText ]
                              WorkingDirectory = workspaceDirectory
                              Environment = None
                              Stdin = None
                              Deadline = None
                              PtyOptions = None }

                        let! result =
                            Runner.execute
                                command
                                estimate
                                { WorkingDirectory = workspaceDirectory
                                  DefaultTimeout = None }
                                CancellationToken.None

                        match result with
                        | Error error -> return box (stringify (createObj [ "error", box (error.ToString()) ]))
                        | Ok(RunnerOutcome.Completed(exitCode, stdout, stderr, _)) ->
                            return
                                box (
                                    stringify (
                                        createObj
                                            [ "exitCode", box exitCode; "stdout", box stdout; "stderr", box stderr ]
                                    )
                                )
                        | Ok(RunnerOutcome.Spooled(exitCode, spoolPath, totalBytes, chunkCount, chunks)) ->
                            try
                                let! summary = summarize runtime chunks

                                return
                                    box (
                                        stringify (
                                            createObj
                                                [ "exitCode", box exitCode
                                                  "summary", box summary
                                                  "spoolPath", box spoolPath
                                                  "totalBytes", box totalBytes
                                                  "chunkCount", box chunkCount ]
                                        )
                                    )
                            with ex ->
                                return
                                    box (
                                        stringify (
                                            createObj
                                                [ "error", box (sprintf "Executor summarizer failed: %s" ex.Message) ]
                                        )
                                    )
                        | Ok(RunnerOutcome.OutputExceeded(bytesWritten, spoolPath)) ->
                            return
                                box (
                                    stringify (
                                        createObj
                                            [ "error", box "Output exceeded hard limit"
                                              "bytesWritten", box bytesWritten
                                              "spoolPath", box spoolPath ]
                                    )
                                )
            }

        let args =
            createObj
                [ "command", box (stringSchema factory)
                  "estimated_output_bytes", box (numberSchema factory)
                  "estimated_running_secs", box (numberSchema factory)
                  "estimated_mem_usage", box (enumSchema factory [| "medium"; "large" |]) ]

        applyTool
            factory
            (createObj
                [ "description", box "Execute a shell command with explicit output, time, and memory estimates."
                  "args", box args
                  "execute", uncurriedExecute (box execute) ])
