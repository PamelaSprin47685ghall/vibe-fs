namespace Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Resources
open Wanxiangshu.Process
open Wanxiangshu.Context.Trace

/// Complete lifecycle for synchronous one-shot Coder/Inspector tools: create,
/// subscribe-before-send, await one terminal, then physically abort/dispose.
module OneShotAgentTool =

    let private forkInstructions (sessionId: SessionId) : ForkChildInstructions =
        let lang = ProviderProse.languageOf sessionId

        { Base = ProviderProse.instructionLines lang ForkChildPayload.BasePath Map.empty
          CommissionerRecord = ProviderProse.render lang ForkChildPayload.CommissionerRecordPath Map.empty
          Attachment = ProviderProse.render lang ForkChildPayload.AttachmentPath Map.empty
          Requirements = ProviderProse.render lang ForkChildPayload.RequirementsPath Map.empty }

    type Request = { Agent: string; Prompt: string }

    type Outcome =
        {
            ChildId: string
            Managed: ManagedAgent
            ParentBackgroundDigest: string option
            Output: string
            /// EXEC-028: child LWR (includeOpening=false) on Completed; None otherwise.
            WorkRecord: string option
        }

    /// Same management bound as Distillation / HostForkRuntime join budget.
    /// Unbounded `completion.Task` hung callers when the child never went terminal.
    [<Literal>]
    let CompletionTimeoutMs = 600_000

    let promptFrom (args: HostToolArguments) =
        match args.OptionalText "prompt", args.OptionalTexts "prompts" with
        | Some prompt, _ -> prompt
        | None, Some prompts -> String.concat "\n" prompts
        | None, None -> ""

    /// PROMPT-005: a one-shot child is prompted through the Dispatcher like any
    /// other agent-owned session.
    ///
    /// The previous version fell through to `scope.Sessions.SendPrompt` with
    /// `Metadata = None` when the scope carried no journal. That prompt was real
    /// but keyless, so PROMPT-011 could not recover it and PromptIngress could only
    /// classify its reply as UnknownOrigin.
    let private send (scope: ToolRuntimeScope) childId prompt agent directory : Task<Result<PromptKey, string>> =
        task {
            match scope.Journal with
            | None -> return Error "No journal: a one-shot agent prompt cannot be claimed"
            | Some journal ->
                let dispatcher = PromptDispatcher.forJournal journal
                // PROMPT-007 Detached: one-shot dispatch does not wait for PhysicalAccepted.
                return!
                    dispatcher.SendAgentOwnerRoot
                        scope.Sessions
                        childId
                        prompt
                        agent
                        directory
                        PromptDispatcher.AwaitMode.Detached
                        None
        }

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
                    // EXEC-006: parent → child keeps Opening.
                    let! parentWorkRecord = scope.ParentWorkRecordFor context.SessionId

                    let fullPrompt =
                        ForkChildPayload.relay (forkInstructions parentId) request.Prompt parentWorkRecord None [] None

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

                        // COMPANION-003 / EXEC-006: child's OpeningMaterial is the
                        // ORIGINAL oneshot assignment (not the rendered relay
                        // envelope), matching HostForkAgent. PromptIngress skips
                        // Opening for AgentOwnerRoot; capture before send.
                        do! XTraceCapture.captureOpening scope.Journal childId request.Prompt []

                        // Ok carries (formal text, optional WorkRecord); Error is the
                        // Result.Error channel (timeout sibling) — not SetException.
                        let completion = TaskCompletionSource<Result<string * string option, string>>()
                        emitJsExpr completion.Task "$0.catch(() => {})" |> ignore
                        // DSL-MUTABLE: subscription — one-shot terminal subscription
                        let mutable subscription: IDisposable option = None
                        // DSL-MUTABLE: resource — one-shot completion latch
                        let mutable completed = false

                        let finish setResult =
                            if not completed then
                                completed <- true
                                subscription |> Option.iter (fun active -> active.Dispose())
                                subscription <- None
                                setResult ()

                        let succeed text workRecord =
                            finish (fun () -> completion.SetResult(Ok(text, workRecord)))

                        let fail (error: exn) =
                            finish (fun () -> completion.SetException error)

                        let childWorkRecord () =
                            task {
                                match! scope.ChildWorkRecordFor(SessionId.value childId) with
                                | Some wr when not (String.IsNullOrWhiteSpace wr) -> return Some wr
                                | _ -> return None
                            }

                        subscription <-
                            Some(
                                scope.Sessions.SubscribeTerminal(
                                    childId,
                                    fun _ outcome ->
                                        match outcome with
                                        // COMPANION-005 / HOST-005: a tool result is
                                        // this turn's formal report, not session-wide
                                        // A. The old `FinalText` was the cumulative
                                        // text including host-visible reasoning, so
                                        // the calling model received the child's
                                        // reasoning stream as if it were the answer.
                                        // EXEC-028: Completed requires child LWR
                                        // (includeOpening=false); missing → Error.
                                        | TerminalOutcome.Completed terminal ->
                                            task {
                                                match! childWorkRecord () with
                                                | Some wr -> succeed terminal.TurnFormalText (Some wr)
                                                | None ->
                                                    // notifyCompleted / some hosts may fire
                                                    // TerminalOutcome without writing XTrace
                                                    // terminal first; includeOpening=false then
                                                    // yields empty LWR. Capture TerminalText
                                                    // then re-query ChildWorkRecordFor.
                                                    let providerRun =
                                                        if isNull (box terminal.ProviderRun) then
                                                            ProviderRunIdentity.create ""
                                                        else
                                                            terminal.ProviderRun

                                                    do!
                                                        XTraceCapture.captureTerminalText
                                                            scope.Journal
                                                            childId
                                                            terminal.TerminalText
                                                            providerRun

                                                    match! childWorkRecord () with
                                                    | Some wr -> succeed terminal.TurnFormalText (Some wr)
                                                    | None ->
                                                        finish (fun () ->
                                                            completion.SetResult(
                                                                Error
                                                                    "EXEC-028: Completed without LifecycleWorkRecord (WorkRecord missing or empty)"
                                                            ))
                                            }
                                            |> ignore
                                        | TerminalOutcome.Aborted reason ->
                                            fail (InvalidOperationException(sprintf "%s aborted: %s" roleLabel reason))
                                        | TerminalOutcome.Failed error ->
                                            fail (InvalidOperationException(sprintf "%s failed: %s" roleLabel error))
                                )
                            )

                        match! send scope childId fullPrompt managed.Name directory with
                        | Error sendError -> succeed (sprintf "send failed: %s" sendError) None
                        | Ok _ -> ()

                        // DSL-MUTABLE: cancellation — parent-abort child session task slot
                        let mutable abortTask: Task<Microsoft.FSharp.Core.Result<unit, string>> option =
                            None

                        let detachAbort =
                            context.AttachAbort(fun () ->
                                if abortTask.IsNone then
                                    abortTask <- Some(scope.Sessions.AbortSession childId)

                                succeed "aborted: parent cancelled" None)

                        try
                            // Bound the wait: race completion against a management timer.
                            // On timeout abort the child and return Error — never hang.
                            let outputTask = completion.Task

                            let! finished = PtyTiming.raceExit (outputTask :> Task) CompletionTimeoutMs

                            if not finished then
                                if abortTask.IsNone then
                                    abortTask <- Some(scope.Sessions.AbortSession childId)

                                finish (fun () -> ())

                                return Error(sprintf "%s timed out after %d ms" roleLabel CompletionTimeoutMs)
                            else
                                let! settled = outputTask

                                match settled with
                                | Error err -> return Error err
                                | Ok(output, workRecord) ->
                                    return
                                        Ok
                                            { ChildId = SessionId.value childId
                                              Managed = managed
                                              ParentBackgroundDigest =
                                                parentWorkRecord |> Option.map ToolHostCodec.digest
                                              Output = output
                                              WorkRecord = workRecord }
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
