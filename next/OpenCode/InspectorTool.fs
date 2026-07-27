namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// One-shot Inspector tool for Coder/Reviewer/Meditator.
/// Creates a disposable Inspector session, optionally injects caller B,
/// runs one prompt (executor-only tools), returns A text, then aborts.
module InspectorTool =

    open ToolSurfaceEmit

    let private stringify (value: obj) : string =
        if isNull value then "null" else JS.JSON.stringify value

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

    [<Import("createHash", "node:crypto")>]
    let private createHashImport: string -> obj = jsNative

    let private sha256 (text: string) =
        let hash = createHashImport "sha256"
        hash?update text |> ignore
        unbox<string> (hash?digest "hex")

    let private readPrompt (args: obj) : string =
        if isNull args then
            ""
        elif not (isNull args?prompt) then
            unbox<string> args?prompt
        elif not (isNull args?prompts) then
            try
                let arr = unbox<obj array> args?prompts

                arr
                |> Array.choose (fun item -> if isNull item then None else Some(string item))
                |> String.concat "\n"
            with _ ->
                ""
        else
            ""

    let create
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (backgroundBFor: (string -> string option) option)
        (directoryFor: (string -> string option) option)
        (registerChildDirectory: (string -> string -> unit) option)
        : obj =
        let factory = toolModule?tool
        let backgroundOf = defaultArg backgroundBFor (fun _ -> None)

        let execute (args: obj) (ctx: obj) =
            task {
                let parentId =
                    if isNull ctx || isNull ctx?sessionID then
                        ""
                    else
                        unbox<string> ctx?sessionID

                if String.IsNullOrWhiteSpace parentId then
                    return box (stringify (createObj [ "error", box "Missing sessionID" ]))
                else
                    let prompt = readPrompt args

                    if String.IsNullOrWhiteSpace prompt then
                        return box (stringify (createObj [ "error", box "inspector prompt required" ]))
                    else
                        let parentSid = SessionId.create parentId

                        let parentDir =
                            match directoryFor with
                            | Some fn -> fn parentId
                            | None -> None

                        let parentB = backgroundOf parentId

                        let fullPrompt =
                            match parentB with
                            | Some b when not (String.IsNullOrWhiteSpace b) ->
                                sprintf "Parent work record B (background only):\n%s\n\nInspector request:\n%s" b prompt
                            | _ -> prompt

                        let parentBDigest = parentB |> Option.map sha256

                        let! created =
                            sessionPort.CreateChildSession(
                                parentSid,
                                { Title = Some "inspector"
                                  Agent = Some "inspector"
                                  Directory = parentDir }
                            )

                        match created with
                        | Error err -> return box (stringify (createObj [ "error", box err ]))
                        | Ok childId ->
                            match parentDir, registerChildDirectory with
                            | Some dir, Some reg -> reg (SessionId.value childId) dir
                            | _ -> ()

                            let tcs = TaskCompletionSource<string>()
                            let mutable sub: IDisposable option = None

                            let finish (text: string) =
                                match sub with
                                | Some d ->
                                    try
                                        d.Dispose()
                                    with _ ->
                                        ()

                                    sub <- None
                                | None -> ()

                                tcs.TrySetResult text |> ignore

                            sub <-
                                Some(
                                    sessionPort.SubscribeTerminal(
                                        childId,
                                        fun _ outcome ->
                                            match outcome with
                                            | TerminalOutcome.Completed _ ->
                                                let output =
                                                    sessionPort.GetSessionOutput childId
                                                    |> List.filter (fun line ->
                                                        not (line.StartsWith "Prompt: ")
                                                        && not (line.StartsWith "ChildPrompt: "))
                                                    |> String.concat "\n"

                                                finish output
                                            | TerminalOutcome.Aborted reason ->
                                                tcs.TrySetException(
                                                    InvalidOperationException(sprintf "Inspector aborted: %s" reason)
                                                )
                                                |> ignore
                                            | TerminalOutcome.Failed error ->
                                                tcs.TrySetException(
                                                    InvalidOperationException(sprintf "Inspector failed: %s" error)
                                                )
                                                |> ignore
                                    )
                                )

                            let! sent =
                                sessionPort.SendPrompt(
                                    childId,
                                    fullPrompt,
                                    { Model = None
                                      Agent = Some "inspector"
                                      Directory = parentDir }
                                )

                            match sent with
                            | Error err -> finish (sprintf "send failed: %s" err)
                            | Ok _ -> ()

                            // Parent cancellation initiates physical abort immediately;
                            // finally awaits the same operation before this resource returns.
                            let mutable abortTask: Task<Result<unit, string>> option = None

                            let detachAbort =
                                attachAbort ctx (fun () ->
                                    if abortTask.IsNone then
                                        abortTask <- Some(sessionPort.AbortSession childId)

                                    finish "aborted: parent cancelled")

                            try
                                let! resultText = tcs.Task

                                return
                                    box (
                                        stringify (
                                            createObj
                                                [ "inspectorId", box (SessionId.value childId)
                                                  "parentBDigest", box (defaultArg parentBDigest "")
                                                  "output", box resultText ]
                                        )
                                    )
                            finally
                                detachAbort ()

                                match abortTask with
                                | Some task ->
                                    try
                                        let! _ = task
                                        ()
                                    with _ ->
                                        ()
                                | None ->
                                    try
                                        let! _ = sessionPort.AbortSession childId
                                        ()
                                    with _ ->
                                        ()
            }

        let argsObj =
            createObj
                [ "prompt", box (createObj [ "type", box "string" ])
                  "prompts", box (createObj [ "type", box "array"; "items", box (createObj [ "type", box "string" ]) ]) ]

        applyTool
            factory
            (createObj
                [ "description",
                  box "One-shot Inspector investigation (executor only); session is disposed after return"
                  "args", box argsObj
                  "execute", box execute ])
