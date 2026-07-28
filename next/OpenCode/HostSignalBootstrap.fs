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
            TurnCompletionProgram.applyWithContinuation
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

                    match root with
                    | None -> return false
                    | Some(messageId, agent) ->
                        let runtime =
                            match journal with
                            | Some j -> PromptDispatcher.forJournal j
                            | None -> PromptDispatcher.ephemeral ()

                        match runtime.AcceptHumanRoot sessionId messageId (Some agent) with
                        | Error _ -> return false
                        | Ok profile ->
                            let key = SessionId.value sessionId
                            userMessageBindings.[key] <- messageId
                            sessionRoles.[key] <- PromptAuthority.roleLabel profile.CanonicalRole
                            reconciler.BindUserMessage(sessionId, messageId)
                            return true
            }

        let providerErrors =
            ProviderErrorFallback(
                sessionPort,
                journal,
                fallbackFailures,
                reconciler.RootBindings,
                ensureAuthorityFromSnapshot,
                continuationAccepted
            )

        let onSignal (signal: HostSignal) =
            match signal with
            | ProviderRetry retry ->
                // Provider retry is the only durable fallback writer.
                RetrySignalHandler.handle journal fallbackFailures reconciler.RootBindings retry
            | ProviderError error -> providerErrors.Observe error
            | SessionIdle sessionId ->
                reconciler.Signal(signal)
                providerErrors.OnIdle sessionId
            | SessionDeleted sessionId ->
                providerErrors.Remove sessionId
                reconciler.Signal(signal)

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

                let agentRole =
                    match sessionRoles.TryGetValue(sessionId) with
                    | true, role -> AgentRoleHelpers.roleOfString role
                    | false, _ -> None

                reconciler.BindUserMessage(sid, mid, ?agentRole = agentRole)
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

        let onAuthorityResolved (sessionId: SessionId) (profile: PromptAuthority.AuthorityExecutionProfile) =
            let key = SessionId.value sessionId
            sessionRoles.[key] <- PromptAuthority.roleLabel profile.CanonicalRole

        let chatMessageHook =
            PromptIngress.createHook journal bindUserMessage bindContinuationMessage registerOwned onAuthorityResolved

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
