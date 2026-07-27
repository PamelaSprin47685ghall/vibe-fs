namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module ExecutorTool =
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

    [<Emit("""
        (ctx, callback) => {
          const signal = ctx && (ctx.abort || ctx.abortSignal || ctx.signal);
          if (!signal || typeof signal.addEventListener !== "function") return () => {};
          if (signal.aborted) callback();
          else signal.addEventListener("abort", callback, { once: true });
          return () => signal.removeEventListener("abort", callback);
        }
    """)>]
    let private attachAbort (context: obj) (callback: unit -> unit) : (unit -> unit) = jsNative

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

    let create
        (toolModule: obj)
        (runtimeFor: obj -> Result<HostForkRuntime, string>)
        (executorRuntimeFor: obj -> HostForkRuntime)
        (workspaceDirectory: string option)
        (directoryFor: (string -> string option) option)
        : obj =
        let factory = toolModule?tool

        let execute (args: obj) (context: obj) =
            task {
                match runtimeFor context with
                | Error error -> return box (stringify (createObj [ "error", box error ]))
                | Ok runtime ->
                    let sid =
                        if isNull context || isNull context?sessionID then
                            ""
                        else
                            unbox<string> context?sessionID

                    let targetDir =
                        if not (String.IsNullOrWhiteSpace sid) then
                            match directoryFor with
                            | Some dirFn -> dirFn sid |> Option.orElse workspaceDirectory
                            | None -> workspaceDirectory
                        else
                            workspaceDirectory

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
                              WorkingDirectory = targetDir
                              Environment = None
                              Stdin = None
                              Deadline = None
                              PtyOptions = None }

                        use cancellation = new CancellationTokenSource()
                        let detachAbort = attachAbort context (fun () -> cancellation.Cancel())

                        let procCtx: ProcessContext =
                            { WorkingDirectory = targetDir
                              DefaultTimeout = None }

                        let! result =
                            try
                                Runner.execute command estimate procCtx cancellation.Token
                            finally
                                detachAbort ()

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
                        | Ok(RunnerOutcome.Spooled(exitCode, spoolPath, totalBytes, chunkCount)) ->
                            try
                                try
                                    let execRuntime = executorRuntimeFor context

                                    let! summary =
                                        ExecutorSummarize.summarizeSpool
                                            (ExecutorSummarize.asExecutorRuntime execRuntime)
                                            spoolPath

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
                                finally
                                    Spool.delete spoolPath
                            with ex ->
                                Spool.delete spoolPath

                                return
                                    box (
                                        stringify (
                                            createObj
                                                [ "error", box (sprintf "Executor summarizer failed: %s" ex.Message) ]
                                        )
                                    )
                        | Ok(RunnerOutcome.OutputExceeded(bytesWritten, spoolPathOpt)) ->
                            match spoolPathOpt with
                            | Some path -> Spool.delete path
                            | None -> ()

                            return
                                box (
                                    stringify (
                                        createObj
                                            [ "error", box "Output exceeded hard limit"
                                              "bytesWritten", box bytesWritten
                                              "spoolPath", box (defaultArg spoolPathOpt "") ]
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
