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
        (modelConfig: ModelResolver.ModelConfig option)
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
                turn

        let reconciler = SessionReconciler(snapshot, onTurn)
        reconcilerRef <- Some reconciler

        let onSignal (signal: HostSignal) =
            match signal with
            | ProviderRetry retry -> RetrySignalHandler.handle journal fallbackFailures userMessageBindings retry
            | SessionIdle _
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
            box (fun (inputObj: obj) (outputObj: obj) ->
                if not (isNull inputObj) then
                    let sessionId =
                        if isNull inputObj?sessionID then
                            ""
                        else
                            unbox<string> inputObj?sessionID

                    let messageId =
                        if not (isNull inputObj?messageID) then
                            unbox<string> inputObj?messageID
                        elif not (isNull inputObj?messageId) then
                            unbox<string> inputObj?messageId
                        elif
                            not (isNull outputObj)
                            && not (isNull outputObj?message)
                            && not (isNull outputObj?message?id)
                        then
                            unbox<string> outputObj?message?id
                        elif
                            not (isNull outputObj)
                            && not (isNull outputObj?info)
                            && not (isNull outputObj?info?id)
                        then
                            unbox<string> outputObj?info?id
                        elif not (isNull outputObj) && not (isNull outputObj?id) then
                            unbox<string> outputObj?id
                        else
                            ""

                    let agent =
                        if not (isNull inputObj?agent) then
                            Some(unbox<string> inputObj?agent)
                        else
                            None

                    registerOwned sessionId

                    let canonicalAgent = agent |> Option.bind HostSessionContext.canonicalRole

                    canonicalAgent |> Option.iter (fun role -> sessionRoles.[sessionId] <- role)

                    // `chat.message` is the only external prompt-acceptance
                    // boundary. A plugin continuation has a pre-recorded key;
                    // it must not replace the active Authority Root here.
                    let promptKey =
                        if isNull inputObj?metadata || isNull inputObj?metadata?wanxiangshu_prompt_key then
                            None
                        else
                            Some(unbox<string> inputObj?metadata?wanxiangshu_prompt_key)

                    let continuationOrigin =
                        if isNull inputObj?metadata || isNull inputObj?metadata?wanxiangshu_origin then
                            None
                        else
                            Some(unbox<string> inputObj?metadata?wanxiangshu_origin)

                    let selectedModel =
                        if isNull inputObj?model then
                            None
                        elif not (isNull inputObj?model?providerID) && not (isNull inputObj?model?modelID) then
                            Some(unbox<string> inputObj?model?providerID, unbox<string> inputObj?model?modelID)
                        else
                            None

                    match journal, canonicalAgent, promptKey with
                    | Some j, Some role, None when
                        not (String.IsNullOrWhiteSpace sessionId)
                        && not (String.IsNullOrWhiteSpace messageId)
                        ->
                        match
                            AgentJournal.appendAgent
                                (StreamId.Session(SessionId.create sessionId))
                                (Some(TurnId.ofMessageId (MessageId.create messageId)))
                                (AgentFact.AuthorityRootAccepted
                                    {| SessionId = SessionId.create sessionId
                                       LogicalRunId = Guid.NewGuid().ToString("N")
                                       HostMessageId = messageId
                                       AuthorityKind = "HumanRoot"
                                       Agent = role
                                       BaseProviderID = selectedModel |> Option.map fst
                                       BaseModelID = selectedModel |> Option.map snd
                                       Variant = None |})
                                j
                        with
                        | Ok _ -> bindUserMessage sessionId messageId
                        | Error failure ->
                            raise (
                                InvalidOperationException(
                                    sprintf "Authority root persistence failed: %A" failure.Failure
                                )
                            )
                    | None, _, None when
                        not (String.IsNullOrWhiteSpace sessionId)
                        && not (String.IsNullOrWhiteSpace messageId)
                        ->
                        bindUserMessage sessionId messageId
                    | Some j, _, Some _key when
                        (continuationOrigin = Some "ReviewerGuard"
                         || continuationOrigin = Some "ReviewConfirmation")
                        && not (String.IsNullOrWhiteSpace sessionId)
                        && not (String.IsNullOrWhiteSpace messageId)
                        ->
                        // chat.message is the authoritative physical user-message
                        // id for confirmation causality. Always bind it, and
                        // record GuardPromptAccepted with this real id so the
                        // second PERFECT can match ConfirmationPromptMessageId.
                        match
                            AgentJournal.appendAgent
                                (StreamId.Session(SessionId.create sessionId))
                                None
                                (AgentFact.GuardPromptAccepted
                                    {| TargetSessionId = SessionId.create sessionId
                                       GuardKey =
                                        // Stable per-session confirmation slot;
                                        // fold overwrites ConfirmationPromptMessageId.
                                        sprintf "review-guard:%s:confirm-perfect" sessionId
                                       HostMessageId = messageId |})
                                j
                        with
                        | Ok _ -> bindContinuationMessage sessionId messageId
                        | Error failure ->
                            raise (
                                InvalidOperationException(
                                    sprintf "Reviewer guard acceptance persistence failed: %A" failure.Failure
                                )
                            )
                    | _, _, Some _ -> bindContinuationMessage sessionId messageId
                    | _ -> ()

                    // Fallback is attempt-local. A newly accepted authority root
                    // must not be rewritten to a prior run's Side B selection.
                    ())

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
