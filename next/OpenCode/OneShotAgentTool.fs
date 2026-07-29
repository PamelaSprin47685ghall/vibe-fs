namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Complete lifecycle for synchronous one-shot Coder/Inspector tools: create,
/// subscribe-before-send, await one terminal, then physically abort/dispose.
module OneShotAgentTool =

    type Request = { Agent: string; Prompt: string }

    type Outcome =
        { ChildId: string
          Managed: ManagedAgent
          ParentBackgroundDigest: string option
          Output: string }

    let promptFrom (args: HostToolArguments) =
        match args.OptionalText "prompt", args.OptionalTexts "prompts" with
        | Some prompt, _ -> prompt
        | None, Some prompts -> String.concat "\n" prompts
        | None, None -> ""

    let private send (scope: ToolRuntimeScope) childId prompt agent directory =
        match scope.Journal with
        | Some journal ->
            task {
                let dispatcher = PromptDispatcher.forJournal journal

                match!
                    dispatcher.SendAgentOwnerRoot
                        scope.Sessions
                        childId
                        prompt
                        agent
                        directory
                        None
                with
                | Ok messageId -> return Ok messageId
                | Error error -> return Error error
            }
        | None ->
            scope.Sessions.SendPrompt(
                childId,
                prompt,
                { Model = None
                  Agent = Some agent
                  Directory = directory
                  Metadata = None }
            )

    let run
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (request: Request)
        (expectedNames: string list)
        (roleLabel: string)
        =
        task {
            if String.IsNullOrWhiteSpace context.SessionId then
                return Error "Missing sessionID"
            elif String.IsNullOrWhiteSpace request.Prompt then
                return Error(sprintf "%s prompt required" (roleLabel.ToLowerInvariant()))
            else
                match ManagedAgent.tryParse request.Agent with
                | None ->
                    let message =
                        if String.IsNullOrWhiteSpace request.Agent then
                            sprintf "agent is required; use %s" (String.concat " or " expectedNames)
                        else
                            match ManagedAgent.parse request.Agent with
                            | Error parseError -> ManagedAgent.formatParseError parseError
                            | Ok _ -> sprintf "Unknown managed agent '%s'." request.Agent

                    return Error message
                | Some managed when not (List.contains managed.Name expectedNames) ->
                    return Error(sprintf "%s tool requires agent %s" roleLabel (String.concat " or " expectedNames))
                | Some managed ->
                    let parentId = SessionId.create context.SessionId
                    let directory = scope.DirectoryFor context.SessionId
                    let background = scope.BackgroundFor context.SessionId

                    let fullPrompt =
                        match background with
                        | Some text when not (String.IsNullOrWhiteSpace text) ->
                            sprintf
                                "Parent work record (background only; B preferred, else session A):\n%s\n\n%s request:\n%s"
                                text
                                roleLabel
                                request.Prompt
                        | _ -> request.Prompt

                    match!
                        scope.Sessions.CreateChildSession(
                            parentId,
                            { Title = Some managed.Name
                              Agent = Some managed.Name
                              Directory = directory }
                        )
                    with
                    | Error createError -> return Error createError
                    | Ok childId ->
                        directory
                        |> Option.iter (fun path -> scope.RegisterDirectory(SessionId.value childId, path))

                        let completion = TaskCompletionSource<string>()
                        let mutable subscription: IDisposable option = None
                        let mutable completed = false

                        let finish setResult =
                            if not completed then
                                completed <- true
                                subscription |> Option.iter (fun active -> active.Dispose())
                                subscription <- None
                                setResult ()

                        let succeed text = finish (fun () -> completion.SetResult text)
                        let fail (error: exn) = finish (fun () -> completion.SetException error)

                        subscription <-
                            Some(
                                scope.Sessions.SubscribeTerminal(
                                    childId,
                                    fun _ outcome ->
                                        match outcome with
                                        | TerminalOutcome.Completed terminal -> succeed terminal.FinalText
                                        | TerminalOutcome.Aborted reason ->
                                            fail (InvalidOperationException(sprintf "%s aborted: %s" roleLabel reason))
                                        | TerminalOutcome.Failed error ->
                                            fail (InvalidOperationException(sprintf "%s failed: %s" roleLabel error))
                                )
                            )

                        match! send scope childId fullPrompt managed.Name directory with
                        | Error sendError -> succeed (sprintf "send failed: %s" sendError)
                        | Ok _ -> ()

                        let mutable abortTask: Task<Microsoft.FSharp.Core.Result<unit, string>> option = None

                        let detachAbort =
                            context.AttachAbort(fun () ->
                                if abortTask.IsNone then
                                    abortTask <- Some(scope.Sessions.AbortSession childId)

                                succeed "aborted: parent cancelled")

                        try
                            let! output = completion.Task

                            return
                                Ok
                                    { ChildId = SessionId.value childId
                                      Managed = managed
                                      ParentBackgroundDigest = background |> Option.map ToolHostCodec.digest
                                      Output = output }
                        finally
                            detachAbort ()

                            match abortTask with
                            | Some pending ->
                                try
                                    let! _ = pending
                                    ()
                                with _ ->
                                    ()
                            | None ->
                                try
                                    let! _ = scope.Sessions.AbortSession childId
                                    ()
                                with _ ->
                                    ()
        }
