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

    /// What the composition root needs back from `wire`.
    ///
    /// Exactly the members `SpikePlugin` calls. Six more used to hang here —
    /// `Reconciler`, `SignalRouter`, `Subscription`, `UnregisterOwned`,
    /// `RegisterSource`, `BindUserMessage` — with no consumer anywhere: the
    /// subscription is already tracked by the scope inside `wire`, and the three
    /// functions are called internally by the binding helpers. Handing them out as
    /// well made the signal stack look like it had six more entry points than it does.
    type WiredSignals =
        { RegisterOwned: string -> unit
          CancelSignals: SessionId seq -> unit
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

        let onTurn (turn: ReconciledTurn) : Task =
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
                turn

        let reconciler =
            ReconcileSupervisor.Supervisor(snapshot, binding, onTurn, ?projection = Some resolveProjection)

        /// FALLBACK-003: every Host signal is a wake and nothing else.
        ///
        /// `ProviderFailure` and `ProviderRetry` used to run their own writers here
        /// — a second and third writer of the durable cursor, each deciding from
        /// event fields whether an attempt had failed. Both are gone: the
        /// reconciled snapshot decides, and FallbackController performs the advance.
        let onSignal (signal: HostSignal) =
            match signal with
            | SessionIdle _
            | ProviderRetry _
            | ProviderFailure _ -> reconciler.Signal signal
            | SessionDeleted sessionId ->
                scope.DisposeSession(SessionId.value sessionId)
                reconciler.Signal signal

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
                let physical = PhysicalUserMessageId.create messageId
                scope.UserMessageBindings.[sessionId] <- physical

                let agentRole =
                    HostSessionNudge.tryActiveProfile journal sid
                    |> Option.bind (fun profile ->
                        profile.CanonicalRole
                        |> PromptAuthority.roleLabel
                        |> AgentRoleIdentity.roleOfString)

                reconciler.BindUserMessage(sid, physical, ?agentRole = agentRole)
                scope.AbortedSessions.Remove sessionId |> ignore
                registerOwned sessionId
                registerSource sessionId LocalPluginEvent

        let bindContinuationMessage (sessionId: string) (messageId: string) =
            if
                not (String.IsNullOrWhiteSpace sessionId)
                && not (String.IsNullOrWhiteSpace messageId)
            then
                reconciler.BindContinuationUserMessage(
                    SessionId.create sessionId,
                    PhysicalUserMessageId.create messageId
                )

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

            // A host-registered run knows its physical opening message; the
            // Authority Root is derived from it by PROMPT-002 promotion rather than
            // read out of a second binding table.
            let physical =
                match scope.UserMessageBindings.TryGetValue key with
                | true, bound -> Some bound
                | false, _ -> None

            reconciler.BindActiveRun
                { SessionId = sessionId
                  RunId = None
                  AuthorityRootUserMessageId = physical |> Option.map PhysicalUserMessageId.promoteToAuthorityRoot
                  PhysicalUserMessageId = physical
                  ContinuationMessageIds = Set.empty
                  AgentRole = Some role
                  Directory = defaultArg directory "" }

        let chatMessageHook =
            PromptIngress.createHook journal bindUserMessage bindContinuationMessage registerOwned

        let cancelSignals (ids: SessionId seq) =
            ids |> Seq.iter (fun id -> signalRouter.UnregisterOwned id)

        { RegisterOwned = registerOwned
          CancelSignals = cancelSignals
          BindActiveRun = bindActiveRun
          CurrentPhysicalUserMessage =
            (fun sessionId ->
                reconciler.TryPhysicalUserMessage(SessionId.create sessionId)
                |> Option.map PhysicalUserMessageId.value)
          ChatMessageHook = chatMessageHook
          ObserveEvent = signalRouter.ObserveLocal }
