namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module HostSignalBootstrap =

    type WiredSignals =
        { Reconciler: SessionReconciler
          SignalRouter: HostSignalRouter
          Subscription: IDisposable option
          RegisterOwned: string -> unit
          RegisterSource: string -> SessionSignalSource -> unit
          BindUserMessage: string -> string -> unit
          BindActiveRun: SessionId -> AgentRole -> string option -> unit
          CurrentPhysicalUserMessage: string -> string option
          ChatMessageHook: obj
          ObserveEvent: obj -> unit }

    let wire
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (snapshotOpt: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (sessionRoles: Dictionary<string, string>)
        (sessionParents: Dictionary<string, string>)
        (verdictSessions: HashSet<string>)
        (nudgeSent: HashSet<string>)
        (managerGuardNudges: HashSet<string>)
        (ownedSessions: HashSet<string>)
        (userMessageBindings: Dictionary<string, MessageId>)
        (fallbackFailures: HashSet<string>)
        (disposeExecutorRuntime: string -> unit)
        (input: obj)
        : WiredSignals =
        let snapshot =
            match snapshotOpt with
            | Some port -> port
            | None ->
                { new ISessionSnapshotPort with
                    member _.GetMessages _ =
                        Task.FromResult(Ok([]: SessionMessage list)) }

        let abortedSessions = HashSet<string>()
        let mutable reconcilerRef: SessionReconciler option = None

        let continuationAccepted (sessionId: SessionId) (messageId: MessageId) =
            reconcilerRef
            |> Option.iter (fun reconciler -> reconciler.BindContinuationUserMessage(sessionId, messageId))

        let onTurn (turn: ReconciledTurn) =
            TerminalPolicies.applyWithContinuation
                sessionPort
                eventPort
                journal
                gitTreePort
                verdictSessions
                nudgeSent
                managerGuardNudges
                sessionParents
                disposeExecutorRuntime
                abortedSessions
                continuationAccepted
                fallbackFailures
                turn

        let reconciler = SessionReconciler(snapshot, onTurn)
        reconcilerRef <- Some reconciler

        let onSignal (signal: HostSignal) =
            match signal with
            | ProviderRetry retry -> RetrySignalHandler.handle journal fallbackFailures userMessageBindings retry
            | ProviderError err ->
                // Non-retryable provider failure never produces an assistant
                // message, so TurnFailed policies never run. Drive AABB here.
                // Local+global dual delivery of the same session.error is common.
                // Dedupe on the physical error identity (session+reason+status),
                // NOT failure count — count advances after the first delivery and
                // would let the second copy record another failure.
                let sid = SessionId.value err.SessionId

                let dedupeKey =
                    sprintf
                        "provider-error|%s|%s|%s"
                        sid
                        err.Reason
                        (err.StatusCode |> Option.map string |> Option.defaultValue "-")

                if fallbackFailures.Add dedupeKey then
                    let failuresSoFar =
                        match journal with
                        | None -> 0
                        | Some j ->
                            match Map.tryFind err.SessionId (AgentJournal.snapshot j).AgentProjections.Sessions with
                            | Some session ->
                                session.Fallback
                                |> Option.map (fun fb -> int fb.Offset + 1)
                                |> Option.defaultValue 0
                            | None -> 0

                    let assistantId =
                        MessageId.create (sprintf "provider-error-%s-%d" sid (failuresSoFar + 1))

                    // Record failure now; ProviderRetryAttempt is deferred to
                    // SessionIdle so host prompt_async is not rejected as busy.
                    PluginFallbackRetry.handleTurnFailure
                        sessionPort
                        eventPort
                        journal
                        fallbackFailures
                        err.SessionId
                        assistantId
                        err.Reason
                        None
                        (Some(fun messageId -> continuationAccepted err.SessionId messageId))
                    |> ignore
            | SessionIdle sessionId ->
                reconciler.HandleSignal signal

                // Host emits multiple idle ticks while tearing down after
                // session.error. Debounce: only the last quiet period sends
                // ProviderRetryAttempt so prompt_async can start a real loop.
                PluginFallbackRetry.scheduleFlushOnIdle
                    sessionPort
                    eventPort
                    journal
                    sessionId
                    (Some(fun messageId -> continuationAccepted sessionId messageId))
                    250
            | SessionDeleted _ -> reconciler.HandleSignal signal

        let signalRouter = HostSignalRouter(ownedSessions, onSignal)

        let subscription =
            match HostSignalSubscribe.trySubscribe input signalRouter.ObserveGlobal with
            | Error err -> raise (InvalidOperationException err)
            | Ok(sub, _source) -> sub

        let registerOwned (sessionId: string) =
            if not (String.IsNullOrWhiteSpace sessionId) then
                ownedSessions.Add sessionId |> ignore
                signalRouter.RegisterOwned(SessionId.create sessionId)

        let registerSource (sessionId: string) (source: SessionSignalSource) =
            if not (String.IsNullOrWhiteSpace sessionId) then
                signalRouter.RegisterSource(SessionId.create sessionId, source)

        let bindUserMessage (sessionId: string) (messageId: string) =
            if
                not (String.IsNullOrWhiteSpace sessionId)
                && not (String.IsNullOrWhiteSpace messageId)
            then
                let sid = SessionId.create sessionId
                let mid = MessageId.create messageId
                userMessageBindings.[sessionId] <- mid
                reconciler.BindUserMessage(sid, mid)
                abortedSessions.Remove sessionId |> ignore
                registerOwned sessionId
                registerSource sessionId LocalPluginEvent

        let bindContinuationMessage (sessionId: string) (messageId: string) =
            if
                not (String.IsNullOrWhiteSpace sessionId)
                && not (String.IsNullOrWhiteSpace messageId)
            then
                reconciler.BindContinuationUserMessage(SessionId.create sessionId, MessageId.create messageId)

        let workspaceDir =
            if isNull input || isNull input?directory then
                None
            else
                let d = unbox<string> input?directory
                if String.IsNullOrWhiteSpace d then None else Some d

        let bindActiveRun (sessionId: SessionId) (role: AgentRole) (directory: string option) =
            let key = SessionId.value sessionId
            registerOwned key
            // Child sessions in a different worktree directory are observed via
            // global SSE only; local plugin events belong to that worktree's
            // own plugin instance.
            match directory, workspaceDir with
            | Some childDir, Some root when childDir <> root -> registerSource key GlobalForeignDirectoryEvent
            | Some _, None -> registerSource key GlobalForeignDirectoryEvent
            | _ -> registerSource key LocalPluginEvent

            reconciler.BindActiveRun
                { SessionId = sessionId
                  RunId = None
                  RootUserMessageId =
                    match userMessageBindings.TryGetValue(SessionId.value sessionId) with
                    | true, mid -> Some mid
                    | false, _ -> None
                  PhysicalUserMessageId =
                    match userMessageBindings.TryGetValue(SessionId.value sessionId) with
                    | true, mid -> Some mid
                    | false, _ -> None
                  ContinuationMessageIds = Set.empty
                  AgentRole = Some role
                  Directory = defaultArg directory "" }

        let chatMessageHook =
            HostSignalChatMessage.createHook journal sessionRoles bindUserMessage bindContinuationMessage registerOwned

        { Reconciler = reconciler
          SignalRouter = signalRouter
          Subscription = subscription
          RegisterOwned = registerOwned
          BindUserMessage = bindUserMessage
          BindActiveRun = bindActiveRun
          CurrentPhysicalUserMessage =
            (fun sessionId ->
                reconciler.TryPhysicalUserMessage(SessionId.create sessionId)
                |> Option.map MessageId.value)
          ChatMessageHook = chatMessageHook
          RegisterSource = registerSource
          ObserveEvent = signalRouter.ObserveLocal }
