namespace Wanxiangshu.Execution.Delegation.Handle.OpenCode

open Wanxiangshu.OpenCode
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
open FsToolkit.ErrorHandling
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
open Wanxiangshu.Process
open Wanxiangshu.Context.Trace

/// Complete lifecycle for synchronous one-shot Coder/Inspector tools: create,
/// subscribe-before-send, await one terminal, then physically abort/dispose.
module OneShotAgentTool =

    let private forkInstructions (sessionId: SessionId) : ForkChildInstructions =
        let lang = SessionProviderLanguage.languageOf sessionId

        { Base = ProviderProse.instructionLines lang ForkChildPayload.BasePath Map.empty
          CommissionerRecord = ProviderProse.render lang ForkChildPayload.CommissionerRecordPath Map.empty
          Attachment = ProviderProse.render lang ForkChildPayload.AttachmentPath Map.empty
          Requirements = ProviderProse.render lang ForkChildPayload.RequirementsPath Map.empty }

    type Request = { Agent: string; Prompt: string }

    /// DSL-state-combination: domain — optional parent background and WorkRecord
    /// preserve evidence attached to one completed tool outcome; they do not
    /// represent independent runtime stages.
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

    let private unknownAgentMessage (agent: string) =
        match ManagedAgent.parse agent with
        | Error parseError -> ManagedAgent.formatParseError parseError
        | Ok _ -> sprintf "Unknown managed agent '%s'." agent

    let private agentRejection (expectedNames: string list) (agent: string) (roleLabel: string) =
        match ManagedAgent.tryParse agent with
        | None when String.IsNullOrWhiteSpace agent ->
            Error(sprintf "agent is required; use %s" (String.concat " or " expectedNames))
        | None -> Error(unknownAgentMessage agent)
        | Some managed when not (List.contains managed.Name expectedNames) ->
            Error(sprintf "%s tool requires agent %s" roleLabel (String.concat " or " expectedNames))
        | Some managed -> Ok managed

    let private nonEmptyWorkRecord (workRecord: string option) =
        match workRecord with
        | Some text when not (String.IsNullOrWhiteSpace text) -> Some text
        | _ -> None

    let private childWorkRecord (scope: ToolRuntimeScope) (childId: SessionId) =
        task {
            let! workRecord = scope.ChildWorkRecordFor(SessionId.value childId)
            return nonEmptyWorkRecord workRecord
        }

    let private providerRunOf (terminal: AgentRunResult) =
        if isNull (box terminal.ProviderRun) then
            ProviderRunIdentity.create ""
        else
            terminal.ProviderRun

    let private ignoreAbortResult (pending: Task<Result<unit, string>>) =
        task {
            try
                let! _ = pending
                return ()
            with _ ->
                return ()
        }

    let private ensureAbortStarted
        (abortTask: Task<Result<unit, string>> option ref)
        (startAbort: unit -> Task<Result<unit, string>>)
        =
        if abortTask.Value.IsNone then
            abortTask.Value <- Some(startAbort ())

    let private drainAbort
        (abortTask: Task<Result<unit, string>> option)
        (startAbort: unit -> Task<Result<unit, string>>)
        =
        task {
            match abortTask with
            | Some pending -> do! ignoreAbortResult pending
            | None -> do! ignoreAbortResult (startAbort ())
        }

    let private noteSendFailure (succeed: string -> string option -> unit) (sendResult: Result<PromptKey, string>) =
        match sendResult with
        | Error sendError -> succeed (sprintf "send failed: %s" sendError) None
        | Ok _ -> ()

    let private settleOutput
        (managed: ManagedAgent)
        (parentWorkRecord: string option)
        (childId: SessionId)
        (settled: Result<string * string option, string>)
        : Result<Outcome, string> =
        match settled with
        | Error err -> Error err
        | Ok(output, workRecord) ->
            Ok
                { ChildId = SessionId.value childId
                  Managed = managed
                  ParentBackgroundDigest = parentWorkRecord |> Option.map ToolHostCodec.digest
                  Output = output
                  WorkRecord = workRecord }

    /// One-shot completion latch: dispose subscription at most once.
    type private CompletionLatch() =
        // DSL-MUTABLE: subscription — one-shot terminal subscription
        let mutable subscription: IDisposable option = None
        // DSL-MUTABLE: resource — one-shot completion latch
        let mutable completed = false

        member _.SetSubscription(active: IDisposable) = subscription <- Some active

        member _.Finish(setResult: unit -> unit) =
            if not completed then
                completed <- true
                subscription |> Option.iter (fun active -> active.Dispose())
                subscription <- None
                setResult ()

    let private recoverMissingWorkRecord
        (scope: ToolRuntimeScope)
        (childId: SessionId)
        (terminal: AgentRunResult)
        (succeed: string -> string option -> unit)
        (latch: CompletionLatch)
        (completion: TaskCompletionSource<Result<string * string option, string>>)
        =
        task {
            match!
                XTraceCapture.captureTerminalTextWithReceipt
                    scope.Journal
                    childId
                    terminal.TerminalText
                    (providerRunOf terminal)
            with
            | Error error ->
                latch.Finish(fun () ->
                    completion.SetResult(Error(sprintf "EXEC-028: terminal trace capture failed: %A" error)))
            | Ok _ ->
                let! workRecord = childWorkRecord scope childId

                match workRecord with
                | Some wr -> succeed terminal.TurnFormalText (Some wr)
                | None ->
                    latch.Finish(fun () ->
                        completion.SetResult(
                            Error "EXEC-028: Completed without LifecycleWorkRecord (WorkRecord missing or empty)"
                        ))
        }

    let private settleCompletedTerminal
        (scope: ToolRuntimeScope)
        (childId: SessionId)
        (terminal: AgentRunResult)
        (succeed: string -> string option -> unit)
        (latch: CompletionLatch)
        (completion: TaskCompletionSource<Result<string * string option, string>>)
        =
        task {
            let! workRecord = childWorkRecord scope childId

            match workRecord with
            | Some wr -> succeed terminal.TurnFormalText (Some wr)
            | None ->
                // notifyCompleted / some hosts may fire TerminalOutcome without writing
                // XTrace terminal first; includeOpening=false then yields empty LWR.
                // Capture TerminalText then re-query ChildWorkRecordFor.
                do! recoverMissingWorkRecord scope childId terminal succeed latch completion
        }

    let private settleCompletedOwned
        (scope: ToolRuntimeScope)
        (childId: SessionId)
        (terminal: AgentRunResult)
        (succeed: string -> string option -> unit)
        (fail: exn -> unit)
        (latch: CompletionLatch)
        (completion: TaskCompletionSource<Result<string * string option, string>>)
        : Task =
        task {
            try
                do! settleCompletedTerminal scope childId terminal succeed latch completion
            with ex ->
                fail ex
        }
        :> Task

    let private admitCompletedTerminal
        (scope: ToolRuntimeScope)
        (childId: SessionId)
        (terminal: AgentRunResult)
        (succeed: string -> string option -> unit)
        (fail: exn -> unit)
        (latch: CompletionLatch)
        (completion: TaskCompletionSource<Result<string * string option, string>>)
        =
        let admitted =
            scope.RunOwnedWork(fun () -> settleCompletedOwned scope childId terminal succeed fail latch completion)

        if not admitted then
            fail (ObjectDisposedException "ToolRuntimeScope")

    let private onTerminal
        (roleLabel: string)
        (scope: ToolRuntimeScope)
        (childId: SessionId)
        (succeed: string -> string option -> unit)
        (fail: exn -> unit)
        (latch: CompletionLatch)
        (completion: TaskCompletionSource<Result<string * string option, string>>)
        (_sessionId: SessionId)
        (outcome: Wanxiangshu.OpenCode.TerminalOutcome)
        =
        match outcome with
        // COMPANION-005 / HOST-005: a tool result is this turn's formal report, not
        // session-wide A. The old `FinalText` was the cumulative text including
        // host-visible reasoning, so the calling model received the child's
        // reasoning stream as if it were the answer.
        // EXEC-028: Completed requires child LWR (includeOpening=false); missing → Error.
        | Wanxiangshu.OpenCode.TerminalOutcome.Completed terminal ->
            admitCompletedTerminal scope childId terminal succeed fail latch completion
        | Wanxiangshu.OpenCode.TerminalOutcome.Aborted stop ->
            fail (InvalidOperationException(sprintf "%s aborted: %s" roleLabel stop.Reason))
        | Wanxiangshu.OpenCode.TerminalOutcome.Failed stop ->
            fail (InvalidOperationException(sprintf "%s failed: %s" roleLabel stop.Reason))

    let private raceCompletionDeadline
        (outputTask: Task<Result<string * string option, string>>)
        (roleLabel: string)
        (abortTask: Task<Result<unit, string>> option ref)
        (startAbort: unit -> Task<Result<unit, string>>)
        (latch: CompletionLatch)
        (managed: ManagedAgent)
        (parentWorkRecord: string option)
        (childId: SessionId)
        : Task<Result<Outcome, string>> =
        task {
            // Bound the wait: race completion against a management timer.
            // On timeout abort the child and return Error — never hang.
            let! finished = PtyTiming.raceExit (outputTask :> Task) CompletionTimeoutMs

            if not finished then
                ensureAbortStarted abortTask startAbort
                latch.Finish(fun () -> ())
                return Error(sprintf "%s timed out after %d ms" roleLabel CompletionTimeoutMs)
            else
                let! settled = outputTask
                return settleOutput managed parentWorkRecord childId settled
        }

    let private awaitBoundedCompletion
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (childId: SessionId)
        (managed: ManagedAgent)
        (parentWorkRecord: string option)
        (roleLabel: string)
        (completion: TaskCompletionSource<Result<string * string option, string>>)
        (latch: CompletionLatch)
        : Task<Result<Outcome, string>> =
        task {
            // DSL-MUTABLE: cancellation — parent-abort child session task slot
            let abortTask = ref None
            let startAbort () = scope.Sessions.AbortSession childId

            let detachAbort =
                context.AttachAbort(fun () ->
                    ensureAbortStarted abortTask startAbort
                    latch.Finish(fun () -> completion.SetResult(Ok("aborted: parent cancelled", None))))

            try
                return!
                    raceCompletionDeadline
                        completion.Task
                        roleLabel
                        abortTask
                        startAbort
                        latch
                        managed
                        parentWorkRecord
                        childId
            finally
                detachAbort ()
                let! _ = drainAbort abortTask.Value startAbort
                ()
        }

    let private runChildSession
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (request: Request)
        (managed: ManagedAgent)
        (roleLabel: string)
        : Task<Result<Outcome, string>> =
        taskResult {
            let parentId = SessionId.create context.SessionId
            let directory = scope.DirectoryFor context.SessionId
            // EXEC-006: parent → child keeps Opening.
            let! parentWorkRecord = scope.ParentWorkRecordFor context.SessionId |> TaskResultCE.ofTask

            let fullPrompt =
                ForkChildPayload.relay (forkInstructions parentId) request.Prompt parentWorkRecord None [] None

            let! childId =
                scope.Sessions.CreateChildSession(
                    parentId,
                    { Title = Some managed.Name
                      Agent = Some managed.Name
                      Directory = directory }
                )

            directory
            |> Option.iter (fun path -> scope.RegisterDirectory(SessionId.value childId, path))

            // COMPANION-003 / EXEC-006: child's OpeningMaterial is the
            // ORIGINAL oneshot assignment (not the rendered relay
            // envelope), matching HostForkAgent. PromptIngress skips
            // Opening for AgentOwnerRoot; capture before send.
            let! _ =
                XTraceCapture.captureOpeningWithReceipt scope.Journal childId request.Prompt []
                |> TaskResult.mapError (fun error -> sprintf "one-shot opening trace capture failed: %A" error)

            // Ok carries (formal text, optional WorkRecord); Error is the
            // Result.Error channel (timeout sibling) — not SetException.
            let completion = TaskCompletionSource<Result<string * string option, string>>()
            emitJsExpr completion.Task "$0.catch(() => {})" |> ignore
            let latch = CompletionLatch()

            let succeed text workRecord =
                latch.Finish(fun () -> completion.SetResult(Ok(text, workRecord)))

            let fail (error: exn) =
                latch.Finish(fun () -> completion.SetException error)

            latch.SetSubscription(
                scope.Sessions.SubscribeTerminal(
                    childId,
                    onTerminal roleLabel scope childId succeed fail latch completion
                )
            )

            let! sendResult = send scope childId fullPrompt managed.Name directory |> TaskResultCE.ofTask
            noteSendFailure succeed sendResult

            return! awaitBoundedCompletion scope context childId managed parentWorkRecord roleLabel completion latch
        }

    let run
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (request: Request)
        (expectedNames: string list)
        (roleLabel: string)
        =
        taskResult {
            if String.IsNullOrWhiteSpace context.SessionId then
                return! Error "Missing sessionID"

            if String.IsNullOrWhiteSpace request.Prompt then
                return! Error(sprintf "%s prompt required" (roleLabel.ToLowerInvariant()))

            let! managed = agentRejection expectedNames request.Agent roleLabel
            return! runChildSession scope context request managed roleLabel
        }
