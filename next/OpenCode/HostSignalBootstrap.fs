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
        { Reconciler: ReconcileSupervisor.Supervisor
          SignalRouter: HostSignalRouter
          Subscription: IDisposable option
          RegisterOwned: string -> unit
          UnregisterOwned: string -> unit
          CancelSignals: SessionId seq -> unit
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
        (scope: PluginRuntimeScope)
        (input: obj)
        : WiredSignals =
        let snapshot =
            match snapshotOpt with
            | Some port -> port
            | None ->
                { new ISessionSnapshotPort with
                    member _.GetMessages _ =
                        Task.FromResult(Ok([]: SessionMessage list)) }

        let resolveProjection (sessionId: SessionId) : AgentProjectionSet option =
            match journal with
            | None -> None
            | Some j -> Some((AgentJournal.snapshot j).AgentProjections)

        let binding = TurnBinding.Store()

        let mutable reconcilerRef: ReconcileSupervisor.Supervisor option = None

        let continuationAccepted (sessionId: SessionId) (messageId: MessageId) =
            reconcilerRef
            |> Option.iter (fun reconciler -> reconciler.BindContinuationUserMessage(sessionId, messageId))

        let onTurn (turn: ReconciledTurn) =
            // Manager sessions run inside their own worktree, not the plugin's
            // root workspace. The review-guard tree check must resolve that
            // worktree's GitTreePort; otherwise it compares against a
            // different Git object graph and can never see the confirmed tree.
            let sessionKey = SessionId.value turn.SessionId

            let managerGitTreePort =
                match scope.SessionDirectories.TryGetValue sessionKey with
                | true, directory when not (String.IsNullOrWhiteSpace directory) -> Some(GitTree.create directory)
                | _ -> gitTreePort

            TurnCompletionProgram.applyWithContinuation
                sessionPort
                eventPort
                journal
                managerGitTreePort
                scope.VerdictSessions
                scope.NudgeSent
                scope.ManagerGuardNudges
                scope.SessionParents
                scope.DisposeExecutorRuntime
                scope.AbortedSessions
                continuationAccepted
                scope.FallbackFailures
                turn

        let reconciler =
            ReconcileSupervisor.Supervisor(snapshot, binding, onTurn, ?projection = Some resolveProjection)

        do reconcilerRef <- Some reconciler

        let ensureAuthorityFromSnapshot (sessionId: SessionId) =
            task {
                match HostSessionNudge.tryActiveProfile journal sessionId with
                | Some _ -> return true
                | None ->
                    let! messages = snapshot.GetMessages sessionId

                    let root =
                        match messages with
                        | Error _ -> None
                        | Ok values ->
                            values
                            |> List.tryPick (fun message ->
                                match message.Role, message.Agent with
                                | "user", Some agent ->
                                    match PromptAuthority.parseAgentName agent with
                                    | Ok _ -> Some(message.Id, agent)
                                    | Error _ -> None
                                | _ -> None)

                    match root, journal with
                    | None, _ -> return false
                    // PROMPT-005: accepting an Authority Root is a durable act. With
                    // nowhere to persist it there is nothing to accept, so this
                    // reports failure rather than producing a profile that would
                    // vanish with the process.
                    | Some _, None -> return false
                    | Some(messageId, agent), Some durable ->
                        let runtime = PromptDispatcher.forJournal durable

                        match runtime.AcceptHumanRoot sessionId messageId (Some agent) with
                        | Error _ -> return false
                        | Ok _ ->
                            scope.UserMessageBindings.[SessionId.value sessionId] <- messageId
                            reconciler.BindUserMessage(sessionId, messageId)
                            return true
            }

        let providerContinuation =
            ProviderFailureContinuation(
                sessionPort,
                journal,
                scope.FallbackFailures,
                scope.UserMessageBindings,
                ensureAuthorityFromSnapshot,
                continuationAccepted
            )

        let onSignal (signal: HostSignal) =
            match signal with
            | ProviderFailure failure ->
                // session.error is only a wakeup; wait for the following idle
                // admission before sending so the Host is no longer busy.
                providerContinuation.Observe failure
            | ProviderRetry retry ->
                // Provider retry is the only durable fallback writer.
                RetrySignalHandler.handle journal scope.FallbackFailures reconciler.RootBindings retry
            | SessionIdle sessionId ->
                reconciler.Signal(signal)
                providerContinuation.OnIdle sessionId
            | SessionDeleted sessionId ->
                providerContinuation.Remove sessionId
                scope.DisposeSession(SessionId.value sessionId)
                reconciler.Signal(signal)

        let signalRouter = HostSignalRouter(scope.OwnedSessions, onSignal)

        let subscription =
            match HostSignalSubscribe.trySubscribe input signalRouter.ObserveGlobal with
            | Error err -> raise (InvalidOperationException err)
            | Ok(sub, _source) -> sub

        do scope.TrackSubscription subscription

        let registerOwned (sessionId: string) =
            if not (String.IsNullOrWhiteSpace sessionId) then
                scope.OwnedSessions.Add sessionId |> ignore
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
                scope.UserMessageBindings.[sessionId] <- mid

                let agentRole =
                    HostSessionNudge.tryActiveProfile journal sid
                    |> Option.bind (fun profile ->
                        profile.CanonicalRole
                        |> PromptAuthority.roleLabel
                        |> AgentRoleIdentity.roleOfString)

                reconciler.BindUserMessage(sid, mid, ?agentRole = agentRole)
                scope.AbortedSessions.Remove sessionId |> ignore
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
                    match scope.UserMessageBindings.TryGetValue(SessionId.value sessionId) with
                    | true, mid -> Some mid
                    | false, _ -> None
                  PhysicalUserMessageId =
                    match scope.UserMessageBindings.TryGetValue(SessionId.value sessionId) with
                    | true, mid -> Some mid
                    | false, _ -> None
                  ContinuationMessageIds = Set.empty
                  AgentRole = Some role
                  Directory = defaultArg directory "" }

        let chatMessageHook =
            PromptIngress.createHook journal bindUserMessage bindContinuationMessage registerOwned

        let cancelSignals (ids: SessionId seq) =
            ids |> Seq.iter (fun id -> signalRouter.UnregisterOwned id)

        { Reconciler = reconciler
          SignalRouter = signalRouter
          Subscription = subscription
          RegisterOwned = registerOwned
          UnregisterOwned = (fun sessionId -> signalRouter.UnregisterOwned(SessionId.create sessionId))
          CancelSignals = cancelSignals
          BindUserMessage = bindUserMessage
          BindActiveRun = bindActiveRun
          CurrentPhysicalUserMessage =
            (fun sessionId ->
                reconciler.TryPhysicalUserMessage(SessionId.create sessionId)
                |> Option.map MessageId.value)
          ChatMessageHook = chatMessageHook
          RegisterSource = registerSource
          ObserveEvent = signalRouter.ObserveLocal }
